# 食品現物の鮮度と現在の腐敗属性

`world_editor` から `/active/objects/details.oni` を `section=freshness`、現物の `id` 付きで読みます。既存の `query` / `worldId` / `limit` も使用でき、既定は1件、上限20件です。対象は現在の live registry に存在する正の `KPrefabID.InstanceID` で、同じ prefab の別個体へ置き換えて解決しません。食品の分割・統合・消費などでIDが失効したら、新たに現物を探してください。

既定の `status`、`properties`、`personality`、`work`、食料集計、Storage の現物詳細へ鮮度を常時追加する変更ではありません。食料の summary/types/full は集計、温度だけなら既存 `section=properties` または Storage 詳細、現在の本体表示文なら `section=status` が代替です。food full の locationSamples は種類ごとの最大4件であり、全現物一覧ではありません。KPrefabIDが無い場合の food full のUnity fallback IDまで、このdetailsで解決できるとは保証しません。

## 観測値

各 item の `freshness` に次を返します。未取得はJSONの `null` と `unavailableFields` の field/reason で示し、0や鮮度100%に補いません。getterごとの成功値は他の欠測で消しません。新readerはnativeの状態を初期化・更新せず、既存参照だけを読みます。

| 項目 | 意味・読取元 |
|---|---|
| `canRot` | 既存 `Edible.FoodInfo.CanRot`。falseは非腐敗食品、未取得はnull。食品でない対象も別の適用外理由を返します。 |
| `nativeFraction` | 既存 `Rottable.Instance.RotConstitutionPercentage`。単位は比率で、通常の0..1を100倍したpercentではありません。本体getterはRotValue/def.spoilTimeです。有限の範囲外値もclampせず保持し、理由を付けます。 |
| `rottableRunning` | 既存SMIの `IsRunning()`。停止または未取得なら、cached数値は残して残時間の推定をしません。 |
| `rotAmount` | 既存DB cacheの固定Rot定義からLookupした `value`、`minimum`、`maximum`、`actualDeltaPerSimulationSecond`、`paused`、`nativeUnits`。Rotの本体UnitsはFlatで、valueを秒とは呼びません。 |
| `temperatureK/C` | 食品現物のPrimaryElement温度。CはKからの変換を小数2桁に丸めます。セル温度や冷蔵庫設定温度による代用ではありません。 |
| `simulationPaused` | 現在のゲーム一時停止。Amount自身の`rotAmount.paused`とは別です。 |
| `stateTags` | 既存KPrefabIDのFresh/Stale/Refrigerated/DeepFrozen/Preserved/Entombedのうち現在付いているtag。温度から保存状態を再判定しません。 |
| `sources` / `units` | 取得を試みるnative経路と数値の単位。そこに経路があること自体は観測成功を意味せず、null/理由も併せて確認します。 |

`nativeFraction`の分母とUIのAmount maxは別の本体値です。MOD等によるmax変更があると両者が異なり得るため、一つの割合へ統合しません。`status=partial`なら一部の項目が取得不能または範囲外です。`status=observed`はgetterを読み取れたことを示し、ロード後の全補正が現在の環境に合わせて再評価済みであることを意味しません。`not_applicable`は非食品/非腐敗食品であり、永久保存の予測ではありません。

`actualDeltaPerSimulationSecond` は `AmountInstance.GetDelta()` の現在の属性値で、単位はnative amount / simulation secondです。UIOnly補正を除く本体合計を返します。本体tooltipの表示合計はUIOnlyも含み得るため、tooltipから数値を逆算せず、表示速度を実効速度へ流用しません。読取のためにRottableを作成したり、RefreshModifiers/SetStatusItems/状態遷移/Amount更新を行ったりしません。DBが未準備ならDb.Getで初期化せず欠測にします。

## 明示した場合だけの保存補正

同じ読取へ `includeFreshnessModifiers=true` を加えると、既存Rot delta属性の `modifiers` を返します。既定falseでは一覧も件数も返しません。true時は本体順序、重複、負/0/正の生値、`isMultiplier` と `uiOnly` を保ちます。最大16件で、`modifierCount` はnative全件数、`returnedModifiers` は返却数、`modifierLimit` は16、`modifiersTruncated` は省略の有無です。null rowや個別getterの欠測も正常な0や空一覧へ置き換えません。

これは現在保存されている補正の観測です。冷却・雰囲気・スパイスの係数をMCPで再実装せず、現物温度から補正を再評価しません。raw modifierの単独値は乗算補正等を含むため、そのまま足し合わせて本体合計と見なさないでください。UIOnlyも一覧には保持しますが、本体actual deltaへの寄与とは区別します。表示用formatter/tooltipの評価は追加しません。

## 残保存時間は派生推定

本機能が確認した本体APIには、将来の保存条件まで保証する残時間getterはありません。`remainingEstimate` は本体残時間ではなく、**現在条件が維持される場合**の現在属性値からの派生見積りです。`source=derived_from_current_attribute_rate`、`assumption=current_conditions_unchanged`、`timeBasis=simulation_seconds` を返します。

腐敗対象であること、SMIが稼働中であること、nativeFractionが有限かつ0..1であること、Amountのraw/min/max/delta/pausedが取得済みであること、min=0、max>0、rawが範囲内、Amountがpauseしていないこと、actual delta<0を全て満たす場合だけ `simulationSeconds=value/(-delta)` を返します。raw=0なら推定値0もあり得ますが、腐敗イベント完了やRotPile生成を意味しません。

現在rate=0、rate>0、Amount pause、停止SMI、非腐敗、欠測、非有限、割合/Amount範囲不成立では推定秒をnullにし理由を返します。Infinity、永久保存、通常値への補完は行いません。ゲーム全体がpauseしていても既存属性rateは0へ書き換えず、推定の時間軸は再開後に進むsimulation secondsです。現実時間の到着時刻ではありません。将来の温度・雰囲気変化、搬送、消費、吸収/分割、状態機械の更新間隔や別MODの操作を予測しません。「現在条件が維持される」とは、この推定に使った観測rateを一定と仮定することです。物理環境が一定でも、後続の本体更新や別の作用によってrateが変わり得るため、将来の更新結果を保証しません。

## 検証範囲

実装の読取契約と、hostでのgetter境界、実機の同時刻現物照合、UIの視覚一致、時間を進めた実際の減少、腐敗完了は別の根拠です。fixtureに存在しない非有限/欠測/停止SMI/正rate/UIOnly補正はhostで代替し、実機で故障や数値を注入したとは扱いません。停止ロード後に現物温度と既存status/補正が一致して見えなくても、その理由をnative更新待ちと断定せず、各取得元の現値として分けます。通常表示UI・将来腐敗・恒久保存・全cacheの非変更・全MOD組合せまで検証したという意味ではありません。


## 限定した実機受入

開発版と公開固有IDの両方で、初回と通常再読込後に同じ食品現物12件を停止状態で照合しました。腐敗対象5件と非腐敗食品7件を区別し、負の現在rateを持つ4件では条件付き派生推定、rateが0の1件では推定秒nullと現在減少なしの理由を確認しました。値0や非腐敗を恒久保存・腐敗完了と解釈する試験ではありません。

同じ現物の既存properties温度、status APIの鮮度表示と丸めの範囲、定義specの腐敗対象フラグを比較し、補正の既定省略/明示取得、対象選定・件数制限、未ロード時の拒否、停止時計を確認しました。温度とcached補正は別の既存値として読み、温度から保存状態や補正を再評価していません。実装を連結した追加host回帰と既存suite、本体参照ビルドを別に通し、異常・欠測等の合成条件を実機再現とは分けました。

元の導入物・設定・原本・共有保存・保存先選択・他MOD・DLCと開始時の未ロード状態へ復帰しました。ここで受入れたのは明示した現物の読取契約です。実際に描画されたUI、将来の減少/腐敗イベント、永久保存、全cacheの非変更、全MOD組合せは保証しません。
