# 診断の未取得値と正常値の区別

対象はstatus_checkの主要欲求6項目（stamina、calories、stress、bladder、breath、bodyTemperature）と、それらを使う危険／待機診断です。未取得の呼吸を100としていた初期値を廃止し、未取得・取得例外・NaN/Infinityはnull、有限な実値0は0として返します。値の取得失敗を正常値や危険値へ置き換えません。

インストール済みONIのAmount.LookupとModifiersExtensions.GetAmountsを読み取り確認。AmountsはComponentではなくModifiersが保持するため、GetComponent<Amounts>()では値を取得できませんでした。固定のDb.Amounts定義からLookup(MinionIdentity)で取得し、翻訳名・部分一致には依存しません。詳細診断のAmounts一覧もGetAmounts経由へ修正しました。

従来のneedsスカラーは維持し、次の項目を追加します。

| 項目 | 意味 |
| --- | --- |
| needsEvidence.<項目>.status / available | observed / trueは実値取得、unknown / falseは未取得 |
| source / attemptedSource / amountId | 成功した取得元、試みた取得元、ゲームの固定Amount ID。未取得のsourceはnull |
| unit / valueFormat | ゲームのAmount.unitsとraw_amount。取得できた単位はFlatまたはKelvinなど。Flatをkcal・百分率に自動変換しない。caloriesもゲームの生の値、bodyTemperatureはKelvin |
| reasonCode | amount_missing、read_failed、non_finite_value、未試行のnot_read。成功時null |
| unknownNeeds | 未取得の主要欲求名 |
| needsAssessmentComplete | 主要6項目をすべて取得できたか。全ゲーム状態の安全保証ではない |

確認できた危険がないが欲求取得が不完全ならrisk=unknown、理由にneeds_unavailableを付け、summary.unknownにも件数を返します。既知の警告／重大な危険はwarning／criticalを維持し、欠測で隠しません。flaggedにはunknownも含まれます。クライアントはunknownを確認済みの危険や救助の必要性と同一視せず、reasonsとneedsEvidenceを確認してください。

バイオニックなどでそのAmountが存在しない場合も、現時点ではnull＋amount_missingとします。観測根拠なしにnot_applicableや正常へ置き換えません。既存の危険判定しきい値の単位設計や他ツールの欠測処理は、この修正の範囲外です。

## 液体中の滞在とリスク判定

足元の液体は `environment.standingInLiquid` に記録し、単独では警告理由にしません。旧呼出元の `standing_in_liquid` も理由から除外します。呼吸欠測はunknown、低呼吸や移動不能はcritical、独立した温度警告はwarningを維持します。

`riskScope` は欲求と局所移動の観測範囲を示します。帰路・呼吸可能地点・移動負荷は未評価なので、`returnSafety.assessmentComplete=false`、`status=not_evaluated`、`safe=null` とします。risk=okは安全な帰還の保証ではなく、足元の液体だけで頭部の潜水も断定しません。

公開版のホスト回帰で液体のみ・呼吸欠測・低呼吸・独立温度警告を検証済みです。公開IDでの実機検証は未実施です。
