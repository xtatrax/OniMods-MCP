# 医療一覧の病気取得状態

既存の患者一覧は、病気を読み取れなかった場合と、読み取った一覧が空だった場合を区別します。新しい公開toolは追加せず、`oni://medical/patients` resourceの既存 `includeHealthy` / `query` / `worldId` / `limit` と、内部のmanagement→medical→patients読取へ同じ結果を返します。現環境で直接呼べるtool名と内部dispatch名は同一とは限りません。

## 病気欄の契約

| 項目 | 意味 |
|---|---|
| `sicknesses` | 既存native Sicknesses.ModifierListの観測結果。object/list未取得は明示null、取得済みの空一覧は[]。存在するindexのinstance/definitionを読めなければnull slotを保持します。 |
| `hasSickness` | 非nullの既存SicknessInstanceと病気定義が少なくとも1件読めたらtrue。正当に観測した空一覧ならfalse。全entry不明・object/list未取得ならnull。 |
| `sicknessRead.status` | 全取得ならobserved、一部取得ならpartial、取得不能ならunknown。getter/listを取得した範囲を示し、ロード後の全状態更新や医療UI全体の確定を意味しません。 |
| `sicknessRead.source` | `GameObject.GetSicknesses().ModifierList`。既存のModifiers.sicknessesと既存Listだけを参照します。 |
| `sicknessRead.reasonCode` / `unavailableFields` | object/list欠測、entry欠測、getter/label取得失敗等の理由。field単位の成功を他の失敗で消しません。 |
| `totalEntries` / `observedEntries` / `unknownEntries` | 同じsicknessRead内のnative entry数・定義を読めたentry数・不明entry数。全体のobject/listが未取得なら全てnull、真の空なら全て0。患者人数や全コロニーの有病率ではありません。 |

既知の病気が1件あれば、別entryや病名ラベルの欠測があってもhasSicknesstrueを保ちます。nonnull定義の存在と、全表示fieldの取得成功は別です。欠測を空一覧・病気なしへ補いません。分割やdedupを行わずnative Listの順序と重複を保持します。

各病気の `id` は従来のnative HashedString値を維持します。`name` と `descriptiveSymptoms` は、既存のStringKeyに対応するStrings.TryGetのcached StringEntry.Stringだけを返します。旧descriptiveSymptomsのStringKey.ToStringによるS/H/Valueデバッグ表現から、既存の翻訳文へ返却の意味を変更します。未登録keyや欠測textはnullと理由を返し、キーを再構成したり、Strings.Getの未登録値fallbackを呼んだりしません。native Name/症状ToStringを再評価せず、動的GetSymptoms callbackや症状計算も呼びません。

`exposure` は既存SicknessExposureInfo.sourceInfoの文字列です。正常なnull・空文字を読取失敗へ変えません。感染源の観測文と、実際の感染行動・時刻の独立証明は異なります。回復や発症を起こすsetter、native Sicknessesの生成、Add/Remove/感染/治療/回復処理、DB初期化はこの病気readerでは行いません。

## 既定一覧と要医療の未判定

既存の怪我などが陽性、またはhasSicknesstrueなら、`needsMedicalAttention=true`を維持します。他の既知陽性が無く病気が未判定ならnull、病気の取得済み空と旧来の他陽性なしならfalseです。この変更は病気欠測の扱いに限定され、医療評価全体の完全性を保証するものではありません。

既定のincludeHealthy=falseでも、trueとnullの行は残します。includeHealthy=trueは従来のfalse行も含めます。順序はtrue→null→false、その中で従来の名前順です。病気unknownをfalseへcastしたり、既定一覧から見えなくしたりしません。queryは取得済みの複製人間名、または既知の病気id/nameによる一致です。不明な病名を推測して一致させず、null slotにも安全に対応します。queryで0件だったことは、全体の病気なしの証拠ではありません。worldと件数制限の既存範囲を保ち、負のworld指定は従来通り全worldを意味します。

## 今回変えない範囲と受入の区別

既存のHealth・Stress・radiationBalance・医療ベッド割当・診療所・医薬品・医療管理/書込の経路は変更しません。特に既存Amounts取得やbedのDB参照・名前照合は今回の病気readerとは別の残る範囲です。医療一覧全体でlazy初期化が一切無いこと、falseが全項目の健康を確定したこと、全cacheが変更されないことまで主張しません。native通常医療UIが追加する放射線効果等を病気一覧へ独自に再現しません。

効果・症状・通知の大量展開は既定結果へ追加しません。現在のSicknesses、status APIの症状、active effects、関連通知、取得時刻/ゲーム時刻の対応付けと通常UIの同時照合は別の受入として残ります。環境status_checkの局所risk=okやHP100を病気なしとは解釈しません。過去に通知と空一覧が食い違った原因も、このnull区別だけで断定しません。

ホストでは病気object/list未取得・真の空・既知陽性・部分欠測・表示ラベル欠測/例外、nullable serializerと既定一覧保持を実reader/callerへ通す必要があります。実機のhealthy fixtureでは真の空と既知の怪我、既存項目、query/world/limit、停止時計、通常再読込を照合できますが、病気陽性や欠測の再現まで証明しません。故障/数値注入や自動進行は読取のために行いません。この読取契約の受入と、親項目の通常UI・症状/通知照合の完了は別です。


## 限定した実機受入

開発版と公開固有IDの両方で、初回と通常再読込後に同じ3名を停止状態で照合しました。各人の病気一覧は取得済みの空配列、hasSicknessはfalse、sicknessReadはobservedでentry数は全て0でした。既存の怪我がある1名は要医療trueを保ち、既定一覧に残りました。他の2名が既定一覧に出ないことを、全医療項目の健康確定とは解釈しません。

従来の患者項目と既存status APIの対象identity・status ID、query/world/limit、反復結果と6項目の停止時計を照合しました。status APIとの比較は描画された通常医療UIや症状本文・過去通知の同時照合ではありません。実ソースを連結したホスト回帰と、本体参照ビルドも別に通しています。病気object/list未取得、部分欠測、病気陽性、ラベル欠測や例外はホスト境界で検証しており、今回の実機で発症や故障を注入したものではありません。

開始時の導入物・原本・共有保存・保存先選択とDLC状態を照合し、他MODを保った元の導入版の未ロードメニュー入口へ復帰しました。preferencesは対象DLCのraw値と保存先選択を照合し、その他の差分は別に記録します。これは全preferencesのbyte一致、接続sessionの同一ID保持、描画された最前面画面までの証明を含みません。

この受入は病気読取の欠測と空一覧の区別に限定されます。親の医療項目は、通常UI・症状/effects・関連通知と時刻の対応付けが未完了のままです。既存Amounts/stress/radiation・医療ベッドの残る取得経路、全cache非変更、他MOD/DLCの全組合せについても解消や保証を主張しません。
