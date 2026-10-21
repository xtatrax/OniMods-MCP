# Snapshotの複製人間欲求値と欠測

snapshotに含む `stress`、`stamina`、`calories` を、同じ複製人間の本体Amountから取得する。以前のsnapshotは、Modifiersが所有する非Componentの `Amounts` を `GetComponent<Amounts>()` で探し、見つからない値を0としていた。個別診断と基本一覧の修正済み読取とは別の経路だった。

## 読取元と値

既存の `Db._Instance` cacheを型・宣言元・static属性を検証したreflectionで読み、`Db.Amounts.Stress`、`Stamina`、`Calories` の固定定義それぞれの `Lookup(MinionIdentity).value` を取得する。`Amount.Lookup` は本体の `GetAmounts()` から既存Modifiers.amountsを参照し、その定義と同じAmountInstanceを検索する。読取でDb.Getや初期化、追加・更新・再評価を呼ばない。翻訳名、表示名、部分一致で項目を選ばない。

3つのscalarは有限なfloatだけを取得成功とし、従来どおり1桁に丸める。既知の0は数値の0、cache・定義・instanceの欠測、例外、非有限値はnullとする。別の項目が失敗しても他の値を捨てず、以前の成功値を次の読取へ引き継がない。caloriesはnativeのraw値でありkcalへ変換しない。native Amount.unitsを証拠に添え、Flatを独自にkcalや百分率へ読み替えない。

`full` profileの各rowだけに `needsEvidence` を付ける。項目ごとに `source`、`attemptedSource`、`amountId`、`unit`、`valueFormat=raw_amount`、`available`、`status=observed/unknown`、`reasonCode` を返す。未取得の成功sourceはnull。brief/standardではこの詳細証拠を省く。バイオニック等で共通Amountが無くてもunknownとし、対象外や正常を推測しない。バイオニック固有の管理境界、個別診断、基本一覧、HP取得は変更しない。

## 対象集合と集約

取得率は要求worldに合う生存複製人間の全対象集合を使い、`dupeLimit` で表示を切る前に計算する。`needsCoverage` は3項目それぞれの `observed`、`unknown`、`complete` を持つ。full/standard/briefのdupe要約、minimalのmetrics、watchOnlyのtop-levelへ配置し、同じ応答内に詳細証拠を重複させない。

- stressが一人でも未取得なら、全体のmaxStress/metrics.stressとstressed_dupesはnull。取得できた部分集合の最大値を全体最大とは呼ばない。
- staminaが一人でも未取得ならlowStamina/low_staminaはnull。全員取得時だけ `0 <= stamina < 30` を数え、実値0を低値に含む。
- 対象0人では最大ストレスはnull、該当人数は0、取得率のcompleteはtrue。未取得の人がいる状態と区別する。
- 取得できたstressに40超があれば、他の人が未取得でも既存のwarningを残す。部分取得時の文言は取得できた集合の最大と明示する。人数集計の40以上と警告の40超の境界は従来のまま。
- 表示順は取得済みstressの降順、その後に未知、同順位は名前順とする。

要約の「複製人間なし」は対象0人だけに使う。対象が存在して全体stressが未知なら別の翻訳文を使い、3欲求のどれかに未取得があれば短い追記で示す。取得率の不足だけで新しいinfo警告を作らない。alertLevelは既存の警告集約であり、greenが全欲求の取得・安全を証明することにはならない。

watchのnullは数値閾値へ一致させず、実値0の閾値判定を保つ。deltaでは数値からnull、nullから数値への遷移を保持する。profileに応じた省略と欠測nullを混同しない。

## Native UIとの関係

本体MinionVitalsPanelは固定Amountを選び、同じ定義のAmountInstanceを既存リストから表示する。ただし表示時にはnative formatterや最大値を使う項目もある。APIのraw一致は描画済みUIとの同時比較を証明しない。UI更新や表示関数を呼んで値を作らず、同時刻のUI・Amount値・返却値の比較は別に検証する。

## 検証状態

実BuildDupesと新reader・下流集約・投影・watch/delta・serializerを含む全文連結ホストは、既存148＋追加309の計457条件が成功した。修正前ColonyMetrics全文と8種の意味変異はコンパイル後の動作検査で検出した。native境界はfixtureで、主SnapshotTools入口・無関係なfood/time計算の実行やUnity本体そのものはこのホストで証明しない。

開発build.78の本体参照ビルドは警告・エラー0。修正前は通常の2人にもstamina/caloriesが0だった同じ試験入力を使い、full・compact・limit・実在する無人world・全world・watch・deltaを初回53/通常再読込56検査で受入れた。3人の同じIDを基本一覧のnative ID別raw値と個別診断へ照合し、通常2人の正数、既知stress0、共通instanceが無い項目のnullを区別。全対象のcoverageと集約null、反復・再読込、停止時計も一致した。

watchのオブジェクト閾値は既存の型付きworld_editor読取payloadで渡して検証した。テキストopsがオブジェクトを文字列として保持する既存契約は変更しておらず、その経路で閾値が未適用だった最初のprobeは未完走として別記録に保った。主結果と別の補給品方針追記は分けて扱った。

正のstress・cache/定義欠測・例外・非有限値やunknownと数値のdelta遷移はホスト境界であり、実機で再現したとはしない。息止め中のnative値・描画UIの同時照合、対象外判定、全環境の安全性は未受入。公開固有IDのbuild.22でも実際の公開ソースを連結したホスト457条件と、警告・エラー0の本体参照ビルドが成功した。公開版の初回53・通常再読込53検査を独立受入れ、開発版との4段階計215検査で、同じnative IDの値・取得率・full/compact・watch・差分・停止時刻が一致した。正規opsの編集block数と子の読取件数は別単位であり、変更なしの根拠をカウンターだけへ依存させない。基本一覧のnative値/healthの前後一致・時計窓・読取ソースを個別に確認した。補給品方針の追記は主結果と混ぜず、各版の既存設定を保った。両版の保存済みログで新規Exception/Error/Assert行0。

元セーブの停止ロードと選択先へ戻し、保存せず通常終了して元の導入版へ復帰した。導入物・設定25ファイル、原本/試験入力、共有保存領域241ファイル、非MCP MOD・DLC・元選択先を照合済み。prefsは書き戻さずSESSION_IDの通常変化を保持した。開始時と同じ元版の未ロード応答と本体MainMenu.OnSpawn入口を確認し、全prefs一致や最前面描画の視覚証明とは区別する。

## ローカライズ

追加文言はPOの `OniMcp.MCP.SNAPSHOT_NEEDS.*` とruntimeの `snapshot_needs.*` を対応させる。PO4ファイルとen・ja・既存zh-Hans runtime3カタログを同期し、既存snapshotカタログの文言・キーを維持する。
