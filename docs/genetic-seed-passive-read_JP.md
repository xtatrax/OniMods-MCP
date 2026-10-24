# 植物分析機の種子選択肢を受動的に読む

`building_control domain=story_facility kind=genetic_analysis_station action=list` は、既存の植物分析機と発見済みの種子選択肢を返す。選択肢の判定は本体サイドスクリーンと同じ順序を保つ。発見済みspeciesのうち亜種が複数あるものから、既存prefabの `SeedProducer` が指す発見済みseedだけを対象とする。名前は本体の解決済み値を使う。

## 許可状態と欠測

本体の `GetSeedForbidden` は、内部の禁止集合がnullなら空集合を代入する。そのため一覧読取からは呼ばず、既知のnative型が宣言するprivate instance field `forbiddenSeeds` を読取だけで参照する。型・宣言元・可視性が違う場合は欠測として扱い、getterへのfallback、field書込、初期化、更新、UI表示は行わない。既定以外の集合comparerも呼ばない。

| 根拠 | allowed / forbidden | permissionState | permissionReason |
| --- | --- | --- | --- |
| 既存の集合にseedが無い | true / false | observed | null |
| 既存の集合にseedがある | false / true | observed | null |
| 集合が未初期化 | null / null | unavailable | forbidden_cache_uninitialized |
| fieldが取得できない | null / null | unavailable | forbidden_cache_unavailable |
| 既定以外のcomparer | null / null | unavailable | forbidden_cache_comparer_unsupported |
| field読取に失敗 | null / null | unavailable | forbidden_cache_read_failed |
| 内部投影へ渡されたstationがnull | null / null | unavailable | station_unavailable |

通常の一覧で対象設備が見つからない場合はstationsが0件となる。上表のstation欠測は内部投影の境界であり、不在設備の行を新規作成する契約ではない。

未知の許可状態でも選択肢のID・名前・亜種数は保持する。既存の `allowed` と `forbidden` キーは保ち、型をboolからnullable boolへ広げる。未知は明示的なJSON nullで返し、false・既知の空集合・選択肢ゼロと混同しない。`permissionState` と `permissionReason` は機械的な状態IDである。

prefabの欠落には警告を出す取得関数を使わず、既存キャッシュの `TryGetPrefab` を使う。亜種一覧がnullの場合も初期化せず、その不整合なspeciesを飛ばす。全ての未発見IDや禁止集合全体は出力しない。

## 書込と検証範囲

`action=set_seed` の既存の選択肢ID照合と明示的な本体setterは保つ。状態不明を理由に、発見済みの有効なIDを一覧から落とさない。setterは搬入禁止や収納物の排出を伴い得るため、状態確認や既知値の作成には使わない。今回の修正はその書込経路全体を移行・受入したものではない。

実ソースの新partialと実JSON設定をリンクしたホスト回帰35項目で、未初期化／空／禁止集合、反復、独自comparerのcallback非呼出、発見条件、欠落prefab、Unityの擬似null、明示nullのwire出力を確認した。開発側で旧2メソッドを正確に抽出した負対照はコンパイル後、初期化を起こすgetter呼出の検査で失敗した。旧ファイル全体の負対照や、実際のlist/setハンドラーの完全なホスト検証とは区別する。

両版それぞれで実ソースを使うホスト35項目と、警告・エラー0の本体参照ビルドを確認した。各自身のDLLによる停止実機で、許可済みの試験分岐に本体の即時建設経路で植物分析機1台だけを完成し、14セル形状、初回一覧・反復2回、不在IDのJSONキー照合による0件対照、時計保持を確認した。発見状態を作る操作や植物分析機のサイドスクリーン表示・種子選択は行わず、選択肢は全3回とも0件だった。未初期化集合の非変更を直接計測した試験ではなく、空一覧からカタログの初期化状態を確定しない。

一覧のid引数だけでは絞り込まれず、queryは投影後のJSON部分文字列照合である。今回の対象照合はJSONのidキーを含むqueryと、返却行の実IDの両方で行った。limitは返却件数を制限するだけで、全対象の投影量を制限するものではない。

両版それぞれで試験設備を保存せず破棄し、元セーブへ通常ロードで復帰した。通常終了を確認して元の導入物・設定・他MOD・既存画像・セーブ選択先と未起動状態へ戻した。公開版では実応答の設定先と所有パッチIDも照合し、開発版の結果を流用していない。

field欠落・読取例外の全ABI変化、全listの走査量・絞込／上限、カタログ自体の欠測と既知の空一覧の区別は今回の限定範囲外。発見状態別の種子選択と実搬入・排出を全て確認したとはしない。実機で対象や選択肢がゼロなら、その結果を未検証条件の成功へ読み替えない。
