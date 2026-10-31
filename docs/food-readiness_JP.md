# 食料の利用可否と取得準備

食料の取得準備や到達性が未判定のとき、利用可能量の下限 `0` を「食料が確定で不足」として扱わないための契約です。確認できた数値と、判定の完了状態を分けて返します。

## 状態と数値

食料の `access` に次の項目を返します。既存の5区分のカロリーは維持します。

| 項目 | 意味 |
| --- | --- |
| `assessmentComplete` | 対象の在庫準備がすべて取得済みtrueで、利用可否・カロリーの未判定項目もない |
| `assessmentState` | `complete`、`initializing`、`unavailable` |
| `inventoryReady` | 本体の在庫準備を集約した `true` / `false` / `null` |
| `inventoryInitializingWorlds` | 本体が準備未完了と返したワールド数 |
| `inventoryUnavailableWorlds` | ワールドIDが分かる準備未取得状態の数 |
| `availableCaloriesKcal` | 確認できた未予約・利用可能カロリーの下限 |
| `unknownCaloriesKcal` | 利用可否が未判定の、測定済みカロリー |

`complete` は全件判定済みです。対象に準備未完了の在庫が1つでもあれば `initializing`、それ以外の欠測・例外・未判定が残る場合は `unavailable` です。`inventoryReady=true` でも個別食料の利用可否が不明なら、全体は `complete` になりません。対象範囲そのものを取得できない場合に、架空の未取得ワールド数を補いません。

`availableCaloriesKcal=0` だけでは、食料不足の確定根拠になりません。判定未完了なら未取得分を含まない下限です。`assessmentComplete=true` の取得済み0は実際の0として維持し、従来の不足しきい値で警告します。既知の食料合計を、そのまま利用可能な生存余力にはしません。

## 取得元と警告

準備状態は既存の `WorldInventory.HasValidCount` を読みます。これは本体が保持するboolの取得であり、更新や条件評価を要求しません。1回の要求で作るreader内だけに結果を保持し、次の要求へ準備状態を使い回しません。食料が0件でも、先に対象ワールドの準備状態を観測します。

停止中の再読取で、本体の準備が必ず完了する保証はありません。本体のfetchable登録・集計更新には別の更新経路があるため、待機時間だけで完了を推測しません。MCPから準備フラグの書込、強制Refresh、初期化処理の呼出、自動的な時間進行は追加しません。

判定未完了では `food_assessment_incomplete` を返し、未判定の下限0から `food` の確定不足警告を作りません。本体の非常モード、観測できた高ストレス、その他の既知の指摘は維持します。長期運転計画の既存の未判定ブロックも維持し、判定未完了のまま長期運転を許可しません。

## 取得粒度と範囲

`/active/resources/food.oni` の既定 `foodDetail=summary` は集計だけ、`types` は種類別の集計を返します。新しいワールド別根拠配列 `access.inventoryReadiness` は `foodDetail=full` の場合だけ追加し、各要素に `worldId`、nullableの `ready`、`reason`、`source` を返します。`source` は `WorldInventory.HasValidCount` です。snapshotの `profile=full` は、この新しい配列を展開する指定ではありません。

最小snapshotでは `metrics.foodAssessmentComplete`、`metrics.foodAssessmentState`、`metrics.foodAvailableKcal`、`metrics.foodUnknownKcal` を返します。watch-onlyはトップレベルの `foodAssessmentState` と、要求したwatch指標を返します。要約も利用可否が未判定であることを明示します。

snapshotは指定ワールド、食料読取は `worldId` 省略時に全ワールドです。snapshotの全体指定 `worldId=-1` と同じ範囲の食料を読む場合、食料側では `worldId` を省略します。診断と長期運転計画は従来の全ワールド対象を維持し、アクティブワールド限定の食料と同一視しません。

既存の `alertCoverage`、HUD通知・日次酸素収支の未取得表示、要約末尾の範囲説明、警告の配列形式は維持します。MCPの警告レベルだけでHUD全体の安全を判定する機能ではありません。

## 検証状況

開発版と公開版それぞれのホスト試験で、取得準備・未取得・取得済み0と、既存の別のcritical警告の保持を確認しました。修正前の全文を使う負対照はコンパイル成功後に食料判定の期待差で失敗しました。両版それぞれの参照ビルドと実機で、停止中の初期化待ち・同範囲再読取・取得不能範囲・通常登録後の判定完了、既定集計と明示詳細の粒度、従来配列形式を確認しています。全ワールド集計では未初期化のワールドが残る場合も確認しており、1ワールドの完了を全体の完了にはしません。取得済み食料0の独立した実機シナリオは未確認で、ホストの0条件を実機受入へ一般化しません。試験後は元の配備物・独立設定・保護セーブと停止状態への復帰を照合しました。
