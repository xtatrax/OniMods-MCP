# 作業一覧で失敗作業snapshotの共有リストを変更しない

`dupes_control domain=side_screen action=todos` の読取が、本体の直近作業検査snapshotにある失敗リストを直接sortし、整理フラグをfalseへ変更する問題の限定修正です。本体の作業選択規則や作業実行、優先度・移動・UI選択を追加しません。

## 根拠と最小変更

実ゲーム参照の静的確認では、`ChoreConsumer.PreconditionSnapshot` はclass、失敗リストは `List<Chore.Precondition.Context>` です。`GetLastPreconditionSnapshot()` は保持中の参照を返すため、getterの後のリスト並べ替えとフラグ代入が共有状態へ届きます。`includeBlocked=false` でもこの書込が発生していました。

修正後は、`includeBlocked=true` かつ本体の整理フラグがtrueの場合だけ表示用リストの副本を作り、同じnative比較順で副本をsortします。この失敗作業snapshotの共有リストと整理フラグは変更しません。falseなら複製・sortもしません。整理済みの順序、潜在成功filter、文脈の射影、件数制限、元snapshotの総数、現在作業と実行可能作業の処理は維持します。返却schemaや本文・言語キーは変更しません。

これは本体の直近キャッシュの読取です。作業の再評価・refreshや現在の選択へ介入して一覧を新鮮に見せません。空・欠測を作業完了や問題なしと解釈しません。

## 検証範囲

| 区分 | 結果 |
| --- | --- |
| ホスト | 製品のMinionTodoInfo実メソッドを含むファイル全体を連結し197検査成功。ON/OFF・dirty・潜在filter・limitの16組合せを反復し、空/missing・理由表示も確認。旧実装とsort/flag各単独故障の3負試験を検出。 |
| 実ゲーム参照ビルド | 開発build.57・公開build.13、各警告・エラー0。 |
| 開発IDの実機 | build.57で非空の失敗候補を使用し、ON/OFF・潜在filter・1件制限・反復の5条件が成功。現在作業・状態・時刻・取得可能なカメラ等も不変。実行可能候補はこの停止時点では空で、非空条件はホストの範囲。 |
| 公開IDの実機 | 公開build.13でも非空失敗候補の同5条件と、現在作業・状態・時刻・取得可能なカメラ等の保持を確認。内部cache flag/listの独立実機観測ではない。 |
| 復元 | 元の導入物・設定25ファイル、保護原本、未保存分岐、元保存先選択、開始時のゲーム未起動を独立照合。新規Player.logのException行0。 |

共有リストの参照・順序と整理フラグの保持はホストで実メソッドを連結して検証します。実機では正式な `/active/ops/dupes.md` の1コマンドから既存住民の一覧を反復し、ON/OFF・潜在filter・件数と現在作業、時刻、取得できるカメラ状態等を照合します。共有cacheの参照・フラグ、ActiveTool・全selectionには独立した公開読取窓口がないため、それらの実機非変更を観測したとは主張しません。通常画面の完全一致、全read経路や全MOD組合せの純粋性へ一般化しません。個別の保存先・住民ID・座標・生応答・本体の逆コンパイル実装は公開差分へ含めません。

## 前提条件cacheを整理しない追加修正

作業の潜在可否と失敗理由を読む既存の `IsPotentialSuccess` / `GetPreconditions` には、dirtyな共有条件リストのsortとflag解除が含まれていた。また、条件判定のsingleton getterにも未生成時の初期化がある。追加修正は、既存cacheとsingletonを受動reflectionで観測し、準備済みの場合だけ本体の潜在可否判定へ委譲する。

- 本体の型・private fieldの型・getterの実装元が一致しない場合は `unavailable`。dirtyは `pending`、不正な条件indexは `out_of_range` とする。リストの整理・singleton生成・状態の書戻しは行わない。
- `potentialSuccess` は未取得ならnull。`includePotentialOnly=true`でも未知の行は残し、既知falseの行だけを除外する。行の `preconditionState`、返却行中の `counts.returnedUnknownPotential` と必要時だけの案内を添える。
- 失敗indexは保持し、未取得の理由id/descriptionを補完しない。準備済みの理由は同じnative cacheから読む。観測は各文脈一回とし、フィルターと射影でgetterを重複呼出ししない。
- 変更した説明10キーはPO4と既存runtime3へ同時移行した。中国語runtime互換と暫定POファイルの扱いは既存のローカライズ規則に従う。

### 追加修正の検証

実production readerとhelperを連結したホスト337条件（既存197＋追加140）が成功した。dirty保持、singleton未生成、未知override、null cache、不正index、filter・limit・反復、既存理由、実localizerの3言語を扱う。完全旧readerはコンパイル0警告0エラー後、dirty cache保持の意味検査で失敗した。PO/runtimeの対象10キーは349検査成功。開発側の全体ローカライズ検査の不足19キーは変更前HEADと完全一致する既存失敗であり、全体成功とは扱わない。公開側の全体検査成功も主張しない。

開発build.159・公開build.75は、それぞれ本体参照0警告0エラー。両版自身の停止実機で非空のblocked snapshotを使い、通常読取・blocked OFF・potential filter・1件制限・反復の5条件を確認した。潜在可否・理由・返却件数と現在作業、snapshot総数、時計・取得できる視点・保存先を照合した。実機で非空のsucceeded候補は観測していない。公開側は固有IDと設定先を使用し、通常MOD不一致確定後も停止を保持した。通常終了・Steam再起動と停止ロードにより、元版・元設定内容・保護データと開始時の一時画像を復帰照合した。nativeのsession bookkeepingと内容同一のmtimeは観測記録に残し、強制巻戻ししない。

### 検証の境界

実機で観測したのは準備済みcacheの通常読取であり、dirty内部flag/listを独立した公開APIで観測したものではない。内部保持と未生成singleton非初期化の根拠は、本体静的経路とホストの代替境界による。名前・report・既存isCurrent等のgetter、未知MODの全callback、任意の別スレッドからの変更、全read/予行の非変更まで証明した意味ではない。既存の本体AddPreconditionはlock外のdirty代入も持つため、guardだけで任意スレッドの競合安全性を主張しない。親No.03は未完了のまま維持する。
