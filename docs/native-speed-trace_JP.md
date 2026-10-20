# 本体の停止・速度適用の観測

停止点検中の時刻差を調べるため、ゲーム本体の速度入口と実適用の履歴を、必要な時だけ少量取得する窓口です。過去の再開主体を推定して埋めず、[生存監視mainの修正](watcher-main-safety_JP.md)や終了時停止の成否とは別に扱います。

## 読取窓口

`game_control domain=speed action=trace` を使います。既存の `action=time` の返却は変更しません。

- `limit` は既定8、最大32。省略時は最新側を返します。
- `afterSequence` は非負の整数で、以前の応答の `traceEpoch` と組み合わせます。
- 異なるepoch、未来のcursor、不正な型や範囲を空の成功へ読み替えません。
- 読取は非消費です。同じcursorで読むこと自体で履歴やゲーム状態を変えません。
- 固定容量64件のメモリー履歴とし、ファイルへ保存しません。容量超過やcursorで失われた範囲を明示します。

応答の `events` にmethod・callId/parentCallId・sessionRelation・before/after・threw/exceptionTypeを返します。`available`・`completeCoverage`・method別 `coverage`、`observerFaultCount` と各状態の `observationStatus` も確認します。`oldestSequence`・`newestSequence`・`droppedCount`・`cursorGap`・`hasMore`・`nextAfterSequence` は、記録の範囲とページ継続を示します。

観測が利用不能・不完全な場合と、観測できた範囲の記録がゼロの場合を区別します。プロセスを再起動するとepochが変わるため、古いcursorで継続しません。各記録のゲーム文脈の世代を比較し、別ロードのゲーム時計を同じ連続時間として引き算しません。 `contextGeneration` は既存MCPロード世代、`observedContextGeneration` はevent/read時に観測したGame.Instance参照の変化で、UIからの全ロード捕捉を保証しません。

## 観測する本体経路

`SpeedControlScreen` の通常の `Pause`、`Unpause`、`SetSpeed`、`ToggleRidiculousSpeed` の前後と、実際に速度が適用される `OnChanged` を観測します。同期ネストはcall IDで結び、同じフレームの一時的な再開と停止も別の呼出として残します。速度フラグや時刻だけから、その間にシミュレーションが進んだとは判断しません。

実時間のUTC・単調時計、Unity frame、native game clock、前後の停止・選択速度・timeScaleを区別します。取得不能値はnullとし、数値0や正常値で補いません。 `observationStatus=observed` はobserver例外がなかったことを表し、各値が完全・正常という判定ではありません。nullは未知のまま扱います。入口前の状態と、OnChanged内で既に論理状態が更新された後の適用前状態は、method名とネストから区別します。 他MODのpatchが元メソッドを省略・変更した場合も、ここで確認できるのは呼出前後に観測した値です。元メソッドの全命令が実行されたことや、同じ値がno-opになった原因を推測しません。

これは観測専用です。予測したゲーム規則、フレーム監視、自動pause、再送、速度の直接書換えや失敗時の代替操作を追加しません。観測自身の失敗がnativeの返り値や例外、呼出順序を変更しないことを検証します。元メソッドの欠落・patch不成立・観測失敗は、取得済みの空履歴と分けて返します。 `completeCoverage` は対象の五つのhook登録状態を表し、速度変更の全経路・他MOD・直接書換えの網羅を示しません。

## 出所の意味と残る範囲

記録時のMCPセッション文脈を、読取側と同じ接続・別接続・出所不明へ射影します。生のセッションID、認証情報、task本文、クライアント名、保存先、要求引数は返しません。同じJSON-RPC IDを使う別接続を、同じ操作主へ統合しません。

MCP文脈がない呼出は、UI操作・他MOD・遅延処理などのどれかを、この情報だけから確定できません。本体メソッド名は通った経路の根拠であり、人間が操作した証明ではありません。遅延処理の同期親子関係や過去の要求元を推測して付けません。

今回の窓口はHTTP受付時刻やrequest ID、遅延task/coroutineの因果関係まで追跡するものではありません。直接のtimeScale書換えなど、観測した入口を通らない経路も網羅しません。原本の過去観測を復元すること、通信断中の停止、期限内停止の保証は範囲外です。親TODO02の過去原因と追加の出所追跡は残します。

## 検証

実装、ホスト試験、ゲーム参照ビルド、開発IDの実機、公開IDの実機、元環境への復元を分けて記録します。

| 区分 | 状態 |
| --- | --- |
| 実装・ホスト | 実store/observer/patch/reader連結238条件と既存ダイアログ774条件が成功。snapshot参照共有・epoch無視・生session漏出・原例外消去・非有限値未検査の5変異を一時ソースで検出。既存監視main120条件・6変異検出、停止1373条件・通信回帰も成功。 |
| ゲーム参照ビルド | 開発build.54が警告0・エラー0。最初のbuild.53で判明した本体DateTime型との名前衝突をSystem.DateTime明示で修正した。失敗候補は配備していない。 |
| 開発IDの実機 | build.54のメニュー43条件・通常ロード4条件、二接続/停止照合98条件を確認。通知の停止層観測は下記。導入DLLと実ロード版・固有IDを照合した。 |
| 公開IDの実機 | 公開作業ツリーのhost238/774、翻訳12キー、警告0・エラー0の本体参照build.11が成功。公開IDでメニュー43・通常ロード4・通知/二接続/停止112条件と、実際の旧process epoch拒否を確認。 |
| 復元 | 元保存先へ通常ロードし、原本を保存せず通知を通常OKして終了。25ファイル、原本2件、未保存分岐2件、保存先選択と開始時のゲーム未起動を独立照合。新規Player.logのException行0。 |

実機ではToggleRidiculousSpeedの実行、意図的な通信断や深いキュー遅延、native例外を試していません。ToggleRidiculousSpeedの署名/bindingは実機で確認し、その呼出、native例外・observer障害の挙動はホストで確認しました。通信断や深いキュー遅延の因果追跡は未受入です。個別の保存先・生応答・ログ・画像・本体アセンブリや逆コンパイル内容を、この一般説明や公開差分へ含めません。

### 開発実機で得た区別

停止中の反復trace読取で履歴sequence・記録本文・ゲーム時計が変わらないことと、cursorの型/範囲/epoch違い拒否、既定8件と五つのbinding、公開スキーマの翻訳説明を確認しました。

自然発生したMOD不一致通知を通常OKした際は、Unpauseが一回呼ばれても別の停止層が残り、呼出前後とも停止・timeScale=0・ゲーム時計不変でした。「PauseとUnpauseが必ず両方呼ばれる」という最初の試験期待はnativeの参照数付き停止に合わなかったため、保存した観測と通常OK実装を照合して訂正しました。製品の挙動を試験期待へ合わせていません。

二つの別セッションから短い通常再開と停止を行い、同じ履歴が読取者ごとにsame/otherへ正しく射影され、ロード後のambient文脈がない呼出はunattributedのまま残ることを確認しました。OnChangedの同期親子、単調時刻/frame、現在の実状態と最終停止が一致し、生session IDは返却にありません。Unpause内で選択済み速度へ戻る呼出と、その後の要求速度への変更も別の観測として残りました。これらの呼出間にシミュレーションが進んだとは推測しません。

## 翻訳キーの対応

独自説明・エラーの `speed_trace.*` はPOの `OniMcp.MCP.SPEED_TRACE.*` と `Localization/Catalogs/speed_trace.{en,ja,zh-Hans}.json` に対応します。公開引数の説明は `tools.game_control.parameters.afterSequence`・`traceEpoch` と対応するPO contextへ揃えます。既存のruntime中国語はzh-Hansを維持し、暫定名zh-hant.poだけを理由に変換しません。

### 公開IDの受入と復元

公開版でも、反復readで履歴とゲーム時計が変わらず、通常通知のUnpause後に既存停止層が残ること、二接続のsame/other射影、OnChangedの同期親子、実状態と最終停止が一致しました。observerFaultCountは0でした。開発プロセスで実際に取得したepochは、公開プロセスで拒否されました。

復帰の通常ロードでは、同じプロセスepochのままGame.Instance参照の観測世代が増え、別のゲーム時計を区別できました。この一例を全UIロードの捕捉とは扱いません。導入DLL・固有IDと実ロード版を両版で照合し、原本/設定は変更せず元環境へ戻しました。privateな生応答や保存先は公開記録へ含めません。
