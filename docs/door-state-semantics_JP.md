# ドアの制御状態と特殊な封印状態

既存の設備設定読取に、nativeの `doorType` と短い機械メタデータ `isSealedMeaning=native_special_seal` を追加します。既存の `current`・`requested`・`isOpen`・`isSealed` は値と読取方法を保ちます。新しい公開toolや設定操作は追加しません。

## 各fieldの意味

| field | 読み取るもの | この値だけでは示さないもの |
| --- | --- | --- |
| `doorType` | native `Door.doorType` のenum名。Pressure / ManualPressure / Internal / Sealedなど | 現在の開閉・施錠・封印や気体遮断の実測 |
| `current` | native `Door.CurrentState` の適用済み制御モード | 安定した物理閉鎖や気密の保証 |
| `requested` | native `Door.RequestedState` の要求モード | 作業・アニメーション・物理反映の完了 |
| `isOpen` | native `Door.IsOpen()` のopen-state predicate | falseのときに安定したclosed状態であること |
| `isSealed` | native Controllerの特殊な `isSealed` SMパラメータ | 通常の施錠、閉鎖一般、気体遮断 |

`doorType` は既存fieldの文字列化であり、種類名から状態やnative規則を作り直しません。Sealed型でも開封後は特殊sealパラメータがfalseになり得ます。`isSealedMeaning` は返却値の意味の注記であり、別の判定結果ではありません。

## 通常制御と特殊seal

通常のAuto / Opened / Lockedはcontrol modeです。本体のドア設定画面もcurrentとrequestedを分け、要求が残る場合はpendingとして表示します。モードと扉の実際の開閉遷移を同じ値として扱いません。

nativeの特殊sealパラメータは、未開封のSealed型が特殊なSealed状態へ入る際などに使われます。本体では通常control欄と開封用欄の切替えにも使われます。通常のAuto閉鎖・Locked状態でisSealed=falseでも矛盾ではなく、そこから「気体が通る」「気密なし」と推測しません。

native IsOpen()はopenだけでなくclosedelay/closeblockedでもtrueを返し、opening/closing等ではfalseになり得ます。その反転を新しいisClosedや安定閉鎖の判定へ置き換えません。気体・液体への物理反映はdoor type・native状態・Sim側と関係する別の範囲です。今回isAirtight/blocksGas等の派生boolは追加しません。

native isSealed propertyはcontroller未取得時にfalseへfallbackします。この既存規約を変更せず、false単独を取得成功の証拠にしません。DoorInfoでは他の既存getterも読むため、controller欠測時に一覧返却が成功する保証でもありません。getter例外の補完・nullable化・反射によるcontroller読取は今回の対象外です。

## 取得経路と受入範囲

既存の `building_control domain=config action=list capability=door` や設定resourceを使います。設定ファイルのDoor.State、設定要求・native state/chore・同意とガードは変更しません。説明のためにUI refresh・Seal/Unseal・controller作成・状態起動・Sim更新を呼びません。

両版の初回・通常再読込で現存15件を停止読取し、既存4fieldとID/位置・他の既存情報を保持したままnative doorTypeと意味IDだけが加わることを照合しました。既存resourceと公開窓口の一致、件数制限・空結果・特殊sealの絞込み・反復と停止時計を確認しました。AutoでisSealed=falseの現物と、LockedでisSealed=trueの特殊seal現物は別の観測として記録し、falseから気密なしとは推論しません。観測した型・モード・対象範囲だけの受入です。

追加は静的metadata2行に限定し、新しい代替hostは作成していません。既存host suiteはこのDoorInfoを対象としていないため、今回の動作証拠として再実行・件数加算していません。元source全文からの限定差と旧4getterの保持、本体参照ビルド、両版の現物読取を別の根拠として扱います。今回観測しない型・通常モード比較・要求pending・開閉遷移・controller欠測、描画UIの同時照合、気体遮断・漏れ・流量の実験は未試験です。

元導入物・対象設定・原本・共有保存・保存先選択・他MOD/DLCと元版の未ロード入口へ復帰しました。同じ接続sessionや全preferencesのbyte一致を主張するものではありません。意味metadataを追加したことを、本体状態や通常UIを変更したこと、物理遮断や要求完了の保証へ広げません。
