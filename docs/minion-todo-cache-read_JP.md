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

## 別に残るnative getterの整理

`Context.CompareTo` は読取比較です。一方、既存の `IsPotentialSuccess` と失敗理由の取得が呼ぶ `chore.GetPreconditions()` には、別の前提条件cacheのdirty時にそのリストをsortし、dirtyフラグを戻すnative lazy getterが含まれます。この限定修正はその経路を変えません。ホストの潜在判定と文脈射影は代替境界であり、native getterまで純粋になった証明ではありません。作業の再評価を足したり、低層への代替書込で状態を戻したりせず、別の読取非変更の残課題へ保持します。
