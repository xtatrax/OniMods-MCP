# バイオニック固有項目の公開値取得

公式配布のゲームDLL（U59-744825-SCRPAN）のpublicメンバーを調査し、直接参照で実装した。専用の安定したMOD SDKとして文書化されているとの確認ではない。ゲーム更新時には再ビルド・実機照合が必要。

`dupes_control domain=info action=needs`（操作ファイル `/active/ops/dupes.md` からも利用可能）に `amountDetails` と `bionic` を追加。`status_check` にも `bionicNeeds` を追加し、既定のcompact診断から参照できる。従来のamountsと共通欲求は維持する。2026-10-06の診断修正で固有項目もriskへ統合した（異常・救出試験 (private runtime report omitted)）。通常デュプはbionic=null、対象Amountを持たなければamountDetailsは空。

| 固定Amount ID | 生値の単位 | 最大値の取得 | ネイティブ判定・公開閾値 |
|---|---|---|---|
| BionicOil | kg | AmountInstance.GetMax / oilAmount.GetMax | 公開定数 OIL_REFILL_TRESHOLD。残量割合が20%以下でWantsOilChange |
| BionicGunk | kg（画面の量表示は％） | AmountInstance.GetMax | 個体のGunkMonitor.Instance.defの閾値。既定60%以上でmild urge、90%以上でcritical urge。スケジュール許可は別フラグ |
| BionicOxygenTank | kg | AmountInstance.GetMax、モニターのstorage.capacityKg | SAFE_TRESHOLD以上でsafe（85%）、CRITICAL_TRESHOLD以下でcritical（0%）。safe=falseだけを危険と断定しない |
| BionicInternalBattery | J | AmountInstance.GetMax | 公開関数IsCriticallyLow / ChargeIsBelowNotificationThreshold / DoesNotHaveCharge。閾値の数値はpublic定数・プロパティではないためnull＋public_predicate_only |

## 読取契約

- `amountDetails`は固定IDをキーに、生値・下限・個体補正込み最大値・範囲内割合・ネイティブ表示文字列・単位と取得元を返す。範囲内割合は勝手に0〜1へ丸めない。
- `attributeDeltaPerSecond`はAmount属性だけのGetDeltaであり、ストレージやモニターによる増減を含む総変化量ではない。電力のWattageを別途返す。0だから実際の消費がないとは判断しない。
- 酸素のAmount鏡像とストレージのAvailableOxygenを別々に読む。RefreshAmountInstance等の書込・更新操作は呼ばない。
- 数値取得失敗はnull＋errors、有限な実値0は保持。表示文字列の取得失敗はdisplayValueAvailable=falseとし、数値の取得成否と分ける。モニター欠落・例外・非有限値はavailable=falseと理由を返す。
- 電力バンクは充電済み数・空数・個体補正込み容量を返す。充電量の割合、バンク個数による危険判定、通知判定を同一視しない。
- 共通欲求のamount_missingをnot_applicableへ変換する実装と、更新MODの境界状態再試験は別TODOとして残す。固有項目のrisk統合と異常・救出の実機観測は異常・救出試験の資料を参照。
