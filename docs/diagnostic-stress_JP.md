# 診断・長期運転計画のストレス読取

`diagnostics_control` の診断・アラートと `survival_plan_control` の計画は、既存の `Db` キャッシュにある固定の `Stress` 定義から `Lookup(MinionIdentity).value` を読みます。通常の集約経路では `Db.Get()` による初期化、`GetComponent<Amounts>()`、翻訳名・部分一致による検索、診断の更新、値の書込を行いません。対象は従来どおり `Components.LiveMinionIdentities.Items` 全体です。ストレスだけを現在の惑星へ絞る変更ではありません。

取得できた有限値だけを既知とします。実値の `0` は取得済みの `0` のまま、定義・インスタンス・キャッシュの欠測、取得例外、NaN・Infinity は未知です。列挙内の null エントリーも未知として数えます。列挙自体が取得できない場合に、架空の人数や正常な空配列へ補完しません。

## 集約と判定

診断のトップレベルと長期計画の `metrics` に `stressCoverage` を返します。個々の複製人間の情報は追加しません。

| フィールド | 意味 |
| --- | --- |
| `observed` | ストレス値を取得できたエントリー数 |
| `unknown` | ストレス値が未知のエントリー数 |
| `complete` | `unknown == 0`。空の列挙では true |
| `status` | `empty`、`complete`、`partial`、`unknown` |

`status` は、0 件なら `empty`、全件既知なら `complete`、既知と未知があれば `partial`、既知がなく未知だけなら `unknown` です。従来の人口フィールドは既存の `Count` のままです。`Count` と列挙が一致しない場合も、coverage の人数を黙って補正しません。

`maxStress` は全件取得できた非空の集団だけで最大値を返します。一部でも未知なら `null`、複製人間がいない場合も `null` です。値は従来と同じ小数 1 桁です。取得できた一部だけの最大値を、全体の最大値には使いません。

| 経路 | 既知の高ストレス | 一部または全部が未知 |
| --- | --- | --- |
| 診断 | 従来の `> 40` で `stress` warning | 取得済みの高値の warning を残し、`stress_assessment_incomplete` info を追加 |
| 長期計画 | 従来の `>= 60` で `stress` warning | 他に既知の critical がなければ `canAttemptLongRun: null`、`decision: assessment_incomplete` |

不完全な集計の高値警告は「取得できた値の最大」と明記します。診断の情報項目には数値の最大値を載せません。アラートのみの取得でも未評価を伝えるための MCP 独自のデータ品質情報であり、本体が発した警告や重大な危険と同一視しません。アラートのみの取得は、従来どおり一覧の形で返します。

長期計画で他の既知の critical がある場合は、ストレスが未知でも従来どおり `canAttemptLongRun: false` と `fix_critical_before_100_cycle_run` を返します。全件既知なら従来の許可判定を使います。複製人間 0 人の既存 critical も維持します。ストレス欠測から critical を作ったり、長期運転を許可したりはしません。

値の取得元は本体ですが、この警告しきい値と長期運転の提案は MCP 側の既存判断です。本体の Stress 定義の単位 enum は `Flat`、表示は `AsPercent` です。既存の 0–100・百分率表示の意味を引き継ぎ、単位名から新しい換算を推測しません。

## 互換性と範囲

`SnapshotNeedReadings` とその snapshot 出力は変更しません。旧 `DupeAmountUtil.StressValue` と `AmountValueByName` は、過去ソースを使うホスト負試験をコンパイルできるよう残しています。フォールバックを含むこれらの旧 API は、現在の診断・長期計画の通常経路からは呼びません。新規コードは値と取得可否を区別できる経路を使います。

ストレス以外の食料・酸素・建物数・建設計画、診断設定の更新、本体の警告条件、DLC の適用可否を変更するものではありません。欠測の原因を複製人間ごとに展開する機能も今回の対象外です。

## ローカライズ

追加・変更したストレス文言は `Localization/po/onimcp.pot` と 3 言語の PO に置き、現在の実行時は `Localization/Catalogs/diagnostic_stress.{en,ja,zh-Hans}.json` を読みます。

| Runtime key | PO msgctxt |
| --- | --- |
| `diagnostic_stress.warning.complete` | `OniMcp.MCP.DIAGNOSTIC_STRESS.WARNING.COMPLETE` |
| `diagnostic_stress.warning.partial` | `OniMcp.MCP.DIAGNOSTIC_STRESS.WARNING.PARTIAL` |
| `diagnostic_stress.assessment_incomplete` | `OniMcp.MCP.DIAGNOSTIC_STRESS.ASSESSMENT_INCOMPLETE` |

`zh-hant.po` は暫定的なソースファイル名です。既存の `zh-Hans` 実行時互換は別訳として保持します。関係のない既存文言の一括移行やゲーム言語の変更は行いません。

## 検証の区別

実製品の読取・集約・診断・長期計画のハンドラーを連結するホスト試験と、実ゲームでの確認は別の証拠です。ホストの本体 getter・UI・食料・地図などの代替は、実ゲームの初期化状態を証明しません。実機の取得値、通常再読込、公開側の確認結果は、それぞれ実施した範囲だけ追記します。この文書の契約説明だけで未検証シナリオの完了を示すものではありません。


## 限定受入結果

開発build.79・公開固有ID build.23の各ソースで、製品全文を連結したホスト465条件と本体参照ビルドの警告・エラー0を確認した。開発側の旧caller/reader全文と6意味変異は、コンパイル後の実handler検査で検出した。共有ヘルパーを連結する既存snapshot457条件も、開発側で回帰を確認した。

開発初回36・通常再読込36、公開初回31・通常再読込31、計134検査を独立受入れた。同じ停止入力の全3名で別のnative読取もStress=0を返し、取得率3/0/complete、診断・警告一覧・長期計画・既存snapshotが一致した。反復・別名・食料可視範囲、既存の非stress項目、native値/health/個体と停止時計6項目を照合した。ops親のedit_blocks数と子の読取件数を別単位として扱い、カウンターだけを非変更の根拠にしない。

実機の入力は全員取得済み0であり、正値・高ストレス・欠測・非有限・部分集計・長期判断nullはホスト境界の検証である。修正前の同じ入力も0のため、実機で誤った正常値の発生から改善までを再現したとは主張しない。通常UIの同時刻描画、呼吸、全cacheの非変更、全モデル/DLC方式の受入へ広げない。

両版の保存ログに新規例外・Assert・ERRORタグは無く、既存の警告は基準版と区別した。新規保存・ゲーム進行・Amount値編集や診断設定書込は行っていない。元セーブの停止ロードと選択先、導入物/設定25ファイル、保護入力、共有保存領域241ファイル、非MCP MOD・DLCを照合して復帰した。元版の未ロード応答と本体MainMenu.OnSpawn到達を確認し、prefsは書き戻さずSESSION_IDの自然更新を保持した。全prefs一致や画面最前面の描画を証明するものではない。
