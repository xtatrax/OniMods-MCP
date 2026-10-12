# 食料集計の利用可能量と未到達量

実装日: 2026-10-08。可視食料合計をそのまま生存余力と扱わないための内訳。

## 読み取り

`world_editor action=read path=/active/resources/food.oni worldId=0` で、既存の食料集計に `access`、`nativeRations`、取得時刻・サイクルを追加する。`worldId` 省略時は全ワールド。既存の `totalCaloriesKcal` は従来どおり探索範囲内の全食料であり、利用可能量へ意味を変更しない。`limit` は返却する種類数だけを制限し、合計と内訳を切り詰めない。

各食品の `access` も同じ基準。現物サンプルは各種類最大4個で、`truncatedLocations` が省略数。サンプルは全現物の一覧ではない。資源の現物検索に追加した `foodAvailability` で、対象の状態と取得元を照合できる。

## 内訳

| `access` の項目 | 意味 |
| --- | --- |
| `availableCaloriesKcal` | 同じワールドの少なくとも1人が到達でき、同じ人に食用許可があり、fetch可能・私有収納でない・消費中でない・自動搬送専用収納でない食料の未予約分 |
| `unreachableCaloriesKcal` | 同じワールドの現在の複製人間が誰も対象へ到達できない食料。複製人間のいないワールドも含む |
| `excludedCaloriesKcal` | 私有収納、消費中、fetch不可、食用を許可された到達可能な人がいない、または自動搬送専用収納 |
| `reservedCaloriesKcal` | 上記の利用条件を満たす現物のうち予約によって未予約量から外れた分 |
| `unknownCaloriesKcal` | 利用可否・予約割合を確認できない現物の、測定できたカロリー |
| `unknownCalorieItems` | カロリー自体が未取得・非有限値・負数の現物数。0カロリーとは区別する |
| `unknownAvailabilityItems` | カロリーは測定できたが利用判定が未確定の現物数。実際の0カロリーでも欠測を隠さない |
| `assessmentComplete` | 未測定カロリーと未判定現物がともにないか |
| `reachableCaloriesKcal` / `reachabilityUnknownCaloriesKcal` | 到達性だけの別集計。主要5区分と重複するので足し合わせない |

主要5区分は測定できた食料を分割する。確定した未到達を先に分類し、その後に確定した利用対象外、未判定、未予約／予約の順で扱う。別の条件が未取得でも、確認できた阻害理由は保持する。表示は小数1桁へ丸めるため、表示値同士の和に丸め差が生じ得る。

利用可能量は**確認済みの未予約量**であり、全員に食料が行き渡る保証ではない。孤立した1人・個人別の食事制限・食事への優先度・将来の配送／調理・品質や腐敗の予測を保証しない。特定の複製人間自身が予約している食料も共用の未予約量には含めない。

## ゲーム本体の取得元

- 到達性: `Navigator.CanReach(Pickupable)`。単なる座標への到達ではなく本体の作業対象と接近オフセットを使用。同じ人の `ConsumableConsumer.IsPermitted(Edible.FoodID)` と組み合わせる。バイオニック等のモデル固有食事制限も本体へ委ねる。
- fetch可否: `FetchableMonitor.Instance.IsFetchable()`。私有収納タグは `KPrefabID.HasTag(GameTags.StoredPrivate)`、消費中は `Edible.isBeingConsumed`、搬送専用は `Storage.automatable.GetAutomationOnly()`。
- 予約割合: `Pickupable.UnreservedFetchAmount / Pickupable.FetchTotalAmount`。本体の両値を同じfetch単位で読む。`TotalAmount` の個数とfetch質量を混ぜない。欠測、非有限値、分母0、負数、分母を超える未予約量は未判定。
- 収納の所有者、`ComplexFabricator` の存在、複製人間の収納であるかを別々に返す。私有タグだけから「調理中」と断定しない。タグだけ存在し収納オブジェクトを取得できない場合もその事実を保持する。
- ロード後は `WorldInventory.HasValidCount` が成立するまで到達性・fetch可否を未判定にする。初期化中のfalseを未到達として確定しない。ナビゲーター／経路グリッド／食事制限の欠測や例外も未判定。

`nativeRations` は本体 `RationTracker.Get().CountAmount(null, world.worldInventory, true) / 1000`。ローカル本体DLLの `MeterScreen_Rations.InternalRefresh` が同じ集計を使うことを確認した。未初期化のワールドは `caloriesKcal=null`・`complete=false`。全ワールド指定では1つでも未初期化なら全体の数値はnullとし、取得できたワールド別の値は残す。

これは要求ワールドの**本体食料集計をその場で読み取った値**。`visibleOnly` と `limit` に依存しない。画面のキャッシュ済みHUD文字列・右側資源パネルの値・その更新時刻そのものではない。HUDとの比較では現在表示中のワールドを明示する。取得UTC時刻とゲーム時刻は応答時点を示し、各表示の同時更新を保証しない。

## 他の集計・診断

コロニーsnapshotは既存の可視合計を保持し、同じ `access` と本体集計を添える。最小要約にも `foodAvailableKcal`、`foodUnreachableKcal`、`foodUnknownKcal`、`foodAssessmentComplete` を返す。

食料警告と生存計画は同じ確認済み未予約量に基づく。取得不足は `food_assessment_incomplete` として別に知らせる。しきい値は従来の複製人間数に基づく設定を維持し、モデル別消費量や個人への公平な配分を今回の集計から推定しない。

## 必要時だけ詳細を取得する

食料読み取りは `foodDetail=summary` が既定。全量を集計し、合計・利用可能／未到達／対象外／予約済み／未判定と判定完了状態を返す。食品一覧・現物サンプル・長い根拠説明は含めない。

- `foodDetail=types`: 種類別内訳を追加。`limit` は種類数のみを制限し、全体集計は変えない。
- `foodDetail=full`: 種類ごとの現物サンプル（最大4件）・座標・収納先・根拠と、本体食料集計を追加。

`worldId` で対象惑星を限定する。`foodDetail` は応答粒度、操作文の `detail=true` は操作結果データの展開であり別の指定。旧来の詳細を必要とする呼び出しは `foodDetail=full` を明示する。
