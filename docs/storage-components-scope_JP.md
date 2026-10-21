# Storage component別の読取範囲

`building_control domain=storage action=detail` は、既定の `storageScope=primary` と、明示的な `storageScope=all_components` を受け付ける。省略時の内容物は従来どおり先頭の `Storage` だけを返し、他のStorageの内容やcomponent数を走査しない。一覧取得は変更しない。

## 範囲と応答

| 指定 | 読取範囲 | 内容物の場所 |
| --- | --- | --- |
| 省略 / `primary` | `GameObject.GetComponent<Storage>()` で得たprimary | top-level `items` |
| `all_components` | 同じGameObjectの `GetComponents<Storage>()` | `storageComponents[].items` のみ |

どちらもtop-levelの `capacityKg` と `massStoredKg` はprimaryの要約であり、全componentの合計ではない。`all_components` は内容物の重複を避けるためtop-levelの `items` を返さない。各componentの所有物を平坦化・重複排除せず、nativeの各リストを個別に示す。

応答は `storageScope`、`summaryScope=primary`、`componentScope=same_game_object`、`includesChildStorages=false`、`includesOtherIStorageImplementations=false`、`primarySelectionSource` を含む。全component指定時は `componentEnumerationSource`、`componentEnumerationStatus`、`storageComponentCount`、`storageComponents` を追加する。

各componentは `componentIndex`、exact reference比較による `isPrimary`、`storageIdTag`、`showInUI`、rawの `storageFilters`、容量・質量・内容物を返す。`componentIndex` はこの応答内だけの列挙番号で、永続的なIDや操作対象指定子ではない。`storageIdTag` は本体の `Storage.storageID` のTag名であり、一意なインスタンスIDではない。設備のKPrefabIDや収納アイテムのIDとは別の体系で、同じTagが複数componentに付いてよい。本体の `Tag.Invalid` は正常な未割当として `storageIdTag=null`、`storageIdStatus=native_unassigned` を返し、それだけでpartialにはしない。解決済みは `resolved`、割当済みTagの名前欠測は `unresolved`、読取例外は `read_failed` と理由を返す。フィルターの順序と重複は変更しない。取得できたnativeフィルターがnullの場合は、rawの `storageFilters=null` と `storageFiltersStatus=native_null` を返し、正常な未設定としてそれだけでpartialにはしない。非nullのリストは `read`、読取例外は `read_failed` と理由を返す。nullから全受入や制限なしとは推測しない。

本体収納UIの `SimpleInfoScreen.RefreshStoragePanel` は、子オブジェクトを含む `IStorage` のうち `ShouldShowInUI()` がtrueのものから内容を集める。このAPIの `all_components` はそのUI範囲を意味しない。非表示のStorageも同じGameObjectにあれば対象になり、子のStorageや他のIStorage実装は対象外になる。UIを再描画して値を作らず、既存のnative fieldと読取getterだけを使う。

`Storage.capacityKg`、`Storage.MassStored()`、`Storage.items` を取得元とし、収納物の情報には既存のPrimaryElement読取を再利用する。`MassStored()` はnativeの集計値であり、全アイテムの情報が完全だったという別証明ではない。`acceptedTags` は従来どおりTreeFilterableの選択内容で、componentのraw `storageFilters` とは別物である。`userMaxCapacityKg` が存在する場合も、Storage固有容量と区別して返す。ManualDeliveryの供給量上限や入出力用途をcomponent順から推定しない。

## 欠測と拒否

- detailの容量・質量が未取得、例外、NaN、Infinityなら `null` と `unavailableFields: [{field,reason}]` を返す。正当な0は0のまま返す。
- 内容リストが取得できない場合は `items=null`。取得できた空リストは `[]`。all_componentsで個別itemが欠測ならその位置をnullにし、他のitemを保持する。primaryでは従来どおりnullのitemを省略し、欠測理由を残す。
- component列挙が失敗・欠測・同一reference重複・primary未解決の場合、countとcomponentsをともにnullとし、primaryの容量・質量は独立に読み取る。未知をStorage0台とは報告しない。
- componentの一部のfieldが取得できなくても、他のfieldとcomponentは保持する。`partial` は当該応答内の欠測を示し、正常・安全・利用可能という判定ではない。
- `storageScope` はJSON文字列の `primary` / `all_components` だけを受け付ける。明示null、他の型、未知の値は拒否する。
- `storageScope` が一覧・フィルター変更・他domainへ付いていたら、今回storageScopeを定義した公開・互換集約入口とStorageのhidden直接入口ではdetail専用として拒否する。これらの入口で指定を無視して書込へ進まない。別のhidden FilterTools等への未知引数を全般的に規制する機能ではない。

既存のフィルター書込は、同じ対象の `TreeFilterable.UpdateFilters` を一度呼ぶ経路を維持する。本体が設定により複数Storageへ影響させる場合もあるため、「先頭Storageだけへの書込」とは断言しない。読取scopeで対象・回数・native方針を拡張しない。

## 検証状態

ホスト試験は既存120＋166と新規271の計557条件が成功し、修正前StorageTools全文と11種の意味変異はコンパイル後の動作検査で検出した。Unity列挙、native field/getter、TagやTreeFilterableはfixtureであり、実Unityの証明とは分ける。

開発build.77の本体参照ビルドは警告・エラー0。既存20種類・23設備を停止状態で取得し、初回316・通常再読込319検査を受入れ、容量・内容・拒否・一覧・時刻が一致した。同一GameObject上に3つのStorageがある現物で、各20,000kg・primary要約20,000kgと空内容を確認した。同じTagでも統合せず、この現物のshowInUIは3つともfalseだった。別の単一Storageで実内容と独立propertiesの温度・菌・IDも一致した。

全取得の副Storageに内容物がある実例、描画済みUIとの照合、移送やフィルター変更によるnative実挙動は未受入。正常な未割当Tag・nullフィルター、列挙失敗・非有限値はホスト境界であり、今回の停止現物ではTag解決済み・リスト取得済みだった。APIの同一GameObject範囲を本体UI全体や全設備へ一般化しない。公開固有IDのbuild.21でも、実際の公開ソースを連結したホスト557条件と、警告・エラー0の本体参照ビルドが成功した。公開版の初回316・通常再読込316検査を独立受入れ、開発版と容量・内容・一覧・先頭エラー本文・停止時刻が一致した。補給品方針の別追記は各版の既存設定に従う差として保持し、収納結果と混ぜて一致とはしない。両版・再読込を含む4段階は計1,267検査、新規例外行なし。

試験後は停止指定の通常ロードで元セーブの選択へ戻し、保存せず通常終了して元の導入版へ復帰した。控えた導入物・設定25ファイル、原本と試験入力、共有保存領域241ファイル、非MCP MOD・DLC・保存先選択を照合済み。prefsは書き戻さず通常SESSION_ID変化を保持した。開始時と同じ元版の未ロード応答と本体MainMenu.OnSpawn入口を確認し、最前面の描画確認や全prefs一致とは区別する。

## ローカライズ

新しい説明・エラーはPOの `OniMcp.MCP.STORAGE_COMPONENTS.*` を原文とし、runtimeの `storage_components.*` へ対応させる。PO4ファイルとruntimeのen・ja・既存zh-Hansを同期する。暫定zh-hantを根拠に既存中国語runtimeを変換しない。
