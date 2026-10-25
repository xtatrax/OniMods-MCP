# 共通欲求の取得状態と登録根拠

`dupes_control domain=info action=status_check` の共通欲求を、実値取得、未取得、検証済みの対象外に分ける。`status` も同じ診断経路を使う。対象外は本体の登録構成と実インスタンスを照合した限定的な分類であり、モデル名や値の欠測だけでは決めない。

## 返却する値と状態

`needs` の既存スカラーと、`needsEvidence` の状態・根拠を組み合わせて読む。

| スカラーのキー | 固定Amount ID |
| --- | --- |
| `stamina` | `Stamina` |
| `calories` | `Calories` |
| `stress` | `Stress` |
| `bladder` | `Bladder` |
| `breath` | `Breath` |
| `bodyTemperature` | `Temperature` |

有限な実値は既存の小数2桁の要約で返す。真の0は0のまま保持する。生のAmount単位を独自の百分率やkcalへ換算しない。値の欠測・読取例外・非有限値を、正常な初期値や0で補完しない。

| `needsEvidence.<key>.status` | `needs.<key>` | `available` | 意味 |
| --- | --- | --- | --- |
| `observed` | 取得値の要約 | true | 正しい固定定義と一意な実インスタンスから有限値を取得した |
| `unknown` | null | false | 取得または適用根拠が不十分。正常・危険・対象外のいずれとも推測しない |
| `not_applicable` | null | false | 下記の登録照合により、対象の共通欲求が設定されていないことを確認した |

`source`、`attemptedSource`、`amountId`、`unit`、`valueFormat`、`reasonCode` を併せて返す。`available` は数値取得の成否であり、対象外が解決済みであることとは別。対象外の `source` は登録照合の取得元で、数値の取得成功を意味しない。取得できた単位などは、値が未取得でも残る場合がある。

## 対象外にできる範囲

`not_applicable` の候補は `Stamina`、`Calories`、`Bladder` だけ。`Breath`、`Stress`、`Temperature` の欠測は、バイオニックであっても対象外へ変えない。

本体の標準バイオニック構成は、共通の `HitPoints`、`ImmuneLevel`、`Breath`、`Stress`、`Toxicity`、`Temperature`、`Decor`、`RadiationBalance` と、固有の `BionicOil`、`BionicGunk`、`BionicInternalBattery`、`BionicOxygenTank` の12登録を持つ。この構成をそのまま全個体へ仮定せず、次を照合する。

- 本体のバイオニックmodelとPrefabTagが一致し、固有monitor読取と同じPrefab識別が成立する。
- 同じ個体の汎用 `Modifiers`、`MinionModifiers`、`MinionIdentity.modifiers` が一致する。`Amounts` の参照と所有GameObjectも対象個体へ結び付く。
- 個体と `MinionModifiers` が初期化・spawn済みで、`initialAmounts` が上記12個の重複しない登録に一致する。
- 12登録すべてについて、固定の本体定義に一致する実インスタンスが一意に存在する。実リスト全体のnull・空ID・重複も拒否する。動的な病気量など、別の一意な実インスタンスが追加されていることだけでは拒否しない。
- 対象の共通欲求は初期登録にも実リストにも存在せず、固定定義自体の取得失敗でもない。

初期化・spawnのフラグだけでは、初期化処理の成功や本体全体の準備完了を証明しない。登録と実インスタンスの一致も必要である。他MODで構成が変わった場合や未知モデルでは、対象外の判定を控えることがある。

実在する正しいAmountはこの対象外判定より先に読む。バイオニックへ後から追加された共通欲求も、有限な0を含め実値を保持する。存在する値がNaN・Infinity・例外ならunknownとし、対象外で隠さない。登録されているのに実インスタンスがない場合は `registered_amount_missing`。重複、定義の取り違え、foreignな所有元なども正常値へ補わない。

正当な汎用 `Modifiers` と所有コンテナーがあれば、バイオニック用の構成証明がなくても実値を返せる。一方、別個体のコンテナーは実値の根拠にも使わない。所有元が読取途中で変わった場合は、先に得た値・登録根拠を破棄する。

## 登録根拠の読み方

既存の `includeDetails=true` を明示した場合だけ、compact／fullの両方で、候補3項目の `needsEvidence.<key>.registrationEvidence` に次を追加する。読取可能な場合の最大3項目であり、データベースや所有元などを取得できなければ、この追加オブジェクト自体がない場合もある。不在をfalseや対象外へ補わない。

| 項目 | 意味 |
| --- | --- |
| `modelId` | 読み取った本体modelの識別名。表示名だけによる対象外判定ではない |
| `configured` / `configuredOccurrences` | 対象IDが初期登録リストにあるか／その出現数。リスト未取得ならnull |
| `instanceCount` | 実リストで対象IDが一致した件数。これだけで定義の正しさや数値取得を保証しない |
| `stockBionicProfileVerified` | 上記12登録と所有・実体の照合がすべて成立したか |
| `profileRegisteredCount` | 読み取れた初期登録リストの件数。未取得ならnull。全runtime量の総数ではない |
| `profileInstantiatedCount` | profile検証成功時は12、未試行・不成立・途中までの照合ではnull |
| `source` / `reasonCode` | 登録照合の取得元と、その照合結果の理由 |

`profileInstantiatedCount=null` を0として扱わない。普通モデルでprofile検証を行わなかった場合もnullであり、実体が0件だったという意味ではない。対象ごとの既知 `instanceCount=0` や、読取済みリストの件数とは別の値である。profileの不成立理由と、個々の数値の取得状態も別に読む。

## 既定応答と診断への影響

`status_check` の `includeDetails` が省略またはfalseなら、nestedな `registrationEvidence` を返さない。`detailMode=full` だけを指定しても有効にならない。`includeDetails=true` では既存のcompact／fullの両方で限定した根拠を返す。新しい操作名や専用の引数は追加しない。全登録リストや全実インスタンスを応答へ展開するものでもない。

登録照合と診断の計算自体は既定でも行う。投影のON/OFFで、共通欲求の数値・status・source・reason、対象外判定、riskや取得範囲を変えない。既定応答でも既存の基本項目からunknownと検証済み対象外を区別でき、詳細な登録構成を必要なときだけ追加取得できる。

通常の一覧と `action=detail` の既存返却へ登録根拠を一律追加しない。`action=needs` は自身の欲求summary経路を保ち、このstatus診断用の登録根拠を追加しない。明示needs／full statusの `amountDetails.Breath` は、最大値・本体表示を扱う別の詳細契約を維持する。共通欲求の新readerは値と単位を読み、最大・最小・増減・formatterを新たに呼ばない。既存の固有量や詳細読取全体がformatterを呼ばないという保証ではない。

`needsAssessmentComplete` は、共通6項目が実値取得または検証済み対象外で解決したかを示す。`unknownNeeds` は未解決の項目だけを含む。全個体・全環境の安全保証ではない。

電池、酸素タンク、油、残滓の4monitorと `bionicNeedsAssessmentComplete` は独立したまま扱う。共通3欲求が対象外でも、固有monitorの欠測を正常へ変えない。確認済みのcritical／warningは、他の欠測や対象外で消さない。酸素の補充目安、Amountの鏡像、実storage残量、属性の増減と実消費も同一視しない。局所的な `risk=ok` は帰路や救助不要の保証ではない。

## 読取と検証の限界

既存のデータベースcacheと、既にある登録リスト・実インスタンスを読む。DB初期化、Prefab生成、Amountの追加／削除、値やmodelの修復、UIのRefresh、時間進行によって読取結果を作らない。前後で再照合する所有元・コンテナー参照と、読取時にコピーしたリストを使う。全native状態の原子的なsnapshotや、あらゆる他MODの変更を検出する保証ではない。

ホスト試験は実reader・Environment・呼出側の全文をリンクし、通常／バイオニック／未知モデル、0・欠測・非有限値・例外、登録不整合、所有・初期化境界、既知危険と固有monitor欠測をnative境界代替で扱う。 通常／バイオニックの既定とfalseでnested根拠がなく、明示trueのcompact／fullで必要な根拠が返ること、およびそれ以外の値・状態・理由・riskが一致することも別に照合する。旧全文を使う負対照では、compile成功後の意味的な失敗を確認する。これは本体API面の参照ビルドや、実際に導入したDLLでの読取・通常UI照合・環境復帰とは別の根拠である。

通常画面に行がないことだけでは、対象外を証明しない。表示cacheの未準備や非表示もあり得る。実機受入は対象ビルド・DLL・識別子ごとに別途記録し、ホストの欠測・例外境界や他環境へ一般化しない。油・酸素・残滓の不足／危険の再生成、救助・回復の全工程、帰路や呼吸媒体の判定は、この限定した登録照合の受入に含めない。
