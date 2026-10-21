# 収納現物の温度・病原菌読取

`building_control domain=storage action=detail id=<設備ID> worldId=<world>`の`items`は、既存の名前・prefabId・質量に現物ID、元素、温度、菌の生値と取得根拠を添えます。液体貯蔵庫の内容を、名前や親の合計量だけから判断せずに確認できます。

| 項目 | 意味・単位 |
| --- | --- |
| `id` | 現物のKPrefabID。取得不能ならnull。別のID領域で代用しない |
| `elementId` | 本体の元素ID |
| `massKg` | 現物の質量kg、既存どおり小数3桁 |
| `temperatureK` / `temperatureC` | 本体のK生値と、小数2桁の摂氏値 |
| `diseaseIndex` / `diseaseCount` | 本体の菌指数と菌数の生値 |
| `diseaseId` / `diseaseName` | 既存の本体Disease定義から解決できたIDとゲーム言語の名前 |
| `primaryElementPresent` | componentを確認できた場合の有無。確認自体の失敗はnull |
| `unavailableFields` | 未取得のfieldと機械的reasonの組 |

現物IDを`world_editor command=read path=/active/objects/details.oni section=properties id=<現物ID> worldId=<world> syncView=false focusCamera=false`へ渡し、同じ現物の既存詳細と照合できます。取得できないIDを別の現物やセルの菌数へ置き換えません。

## 0・欠測・菌名解決の区別

各getterは独立して読みます。PrimaryElementなし、getter例外、非有限の質量・温度はnullと理由を返し、0へ補いません。真の0は0を保持します。一項目の失敗で他の取得済み値を破棄しません。

`diseaseDefinitionStatus`は、解決済みの`resolved`、本体の255指数の`native_none_index`、未知指数の`unresolved_index`、定義読取失敗の`lookup_failed`、菌指数未取得の`raw_unavailable`を分けます。菌名を解決できなくても取得済みの指数・菌数を保持します。`native_none_index`では病名をnullとし、未解決の定義と区別します。

本体の菌getter自身が、内部の参照が未準備な場合にも255/0を返すことがあります。このAPIは本体の報告値を示し、殺菌済み・飲用可・本体内部の初期化完了を保証しません。浄水と殺菌も同一視しません。病名は既存Dbキャッシュだけから読み、lazy初期化を行う`Db.Get()`や定義の代替生成を呼びません。対応するキャッシュを取得できなければ名前は未取得です。

## 範囲と互換性

一覧は従来の容量・合計質量・フィルター要約のままで、現物詳細を自動追加しません。書込やset_filterの対象、先頭Storageを使う既存の設備選択も変えません。複数Storageの全取得は別の課題です。親の容量・合計質量の取得方式を一括で変更したものではありません。

## 検証

ホスト・参照ビルド・各IDの実機結果を区別して記録します。実機の生値比較は既存の現物propertiesが対象で、病名そのものを別窓口から独立に取得したという意味ではありません。病名は既存のnative cacheから解決した値です。


実StorageTools・選択resolver・追加helper・serializerを全文連結したホストで、既存120・新規166条件が通過しました。旧StorageTools全文と8種の意味変異は、コンパイル後の動作試験で検出しました。Unity・native getter・DB cacheと定義は代替境界で、実際のゲームの菌シミュレーションを実行したものではありません。取得案内1キーのPO4/runtime3対応を維持しています。

本体同梱DLLの参照ビルドは開発build.72で警告・エラー0、ソース・配備DLL・実応答版も照合しました。開発IDの実機で62条件が成功しました。既存6収納のうち空2、現物4（菌数あり3・本体255指数で菌数0の1）を読み、全現物IDの既存propertiesと元素・温度・菌指数/数・質量を比較しました。反復取得、一覧の非拡張と件数制限、誤ID・誤world、前後時計も確認済みです。通常メニューから同じ既存セーブを再読込し、もう一度62条件と現物・容器の完全一致を確認しました。新規保存や試験設備の生成、進行・菌数変更は行っていません。

公開固有IDのbuild.19でも参照ビルドは警告・エラー0、同じホスト286条件が成功しました。実機は初回59条件、通常メニュー経由の再読込後も59条件が成功し、容器と現物の取得結果は開発版とも一致しました。開発版との差3条件は、開発試験時だけ必要だったMOD不一致の通常確認です。両版のログに新規Exception行はありません。

元セーブを停止指定で通常ロードして保存先を選び直し、保存せず終了して元の導入版へ戻しました。控えた導入物・設定25ファイル、原本と試験入力、共有保存領域241ファイル、非MCP MOD状態・DLC設定・元保存先選択を照合済みです。開始時と同じ未ロード状態と本体MainMenu.OnSpawnのログを確認しましたが、最前面の描画を視覚確認した意味ではありません。prefs全体は書き戻さず、通常起動によるSESSION_IDの変化を保持しています。

PrimaryElement欠測・getter例外・非有限値・未解決菌指数などはホストの検証範囲です。UIでの病名の独立照合、全native内部cacheの純粋性、殺菌・飲用安全や複数Storage全体の取得を受入れたものではありません。
