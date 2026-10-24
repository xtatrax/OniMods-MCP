# 呼吸値・個体ステータス画面の取得契約と限定検証

## 製品仕様：必要なときだけ詳細を取得する

複製人間情報の明示的な `needs` では `amountDetails.Breath`、`status_check` の `includeDetails=true, detailMode=full` では `needsDetail.amountDetails.Breath` を返す。通常の一覧、compact、詳細を要求しないstatus、既存snapshotには、この追加詳細のための最大値・formatter読取を増やさない。

既存の `Db._Instance.Amounts.Breath.Lookup(dupe)` から対象個体のAmountを取得し、`value`、個体ごとの `GetMax()`、native単位enum、`GetValueString()` の結果を項目別に返す。データベースを初期化したり、最大100などの固定値や独自の百分率へ置き換えたりしない。`rawUnit` は物理単位の公開根拠がないためnullと理由を返す。native単位enumの `Flat` だけで物理単位は確定しない。

取得した真の0は既知の0として保持する。欠測・例外・非有限値はnullと `unavailableReasons` へ分け、取得できた他の項目は残す。`available/status` と項目別の `fieldSources` を返し、前回の成功値や初期値で補完しない。数値または最大値を取得できなければformatterを呼ばない。モデル名だけでバイオニックのBreathを対象外と決めない。

### 数値・JSON表現・通常表示は別の契約

| 取得物 | 意味 |
| --- | --- |
| 新しい詳細の `value/maximum` | nativeのfloatを読み、丸めずdoubleへ拡張して返す。元の測定精度がdoubleへ増えるわけではない |
| 既存経路のfloatのJSON表現 | 同じnative値でも、doubleへ拡張した詳細値とは末尾の表記が異なる場合がある |
| 既存の診断・欲求summary | 対応する数値を小数2桁へ丸めた要約。詳細値の代用にしない |
| 詳細の `displayValue` | 本体formatterが返す文字列。独自の数値から表示を再構成しない |
| UI statusの `breath.label` | 対象の通常画面に保存済みのLocText。詳細経路のformatterとは別に読む |

float/doubleは実装内の型であり、JSON上に別々の数値型が宣言されるわけではない。生の数値、要約の丸め、本体の表示文字列を、JSON文字列の完全一致だけで比較しない。表示文字列の丸め方を今回の数例から一般化しない。

## 製品仕様：個体パネルの通常UI経路

`game_control` の `domain=ui, uiDomain=selection` は、正確な非0のcanonical Int32個体IDで1体だけを対象とする。IDはJSON整数で渡し、名前・座標・検索・world・forceなどの代替セレクターは併用できない。

| action | 契約 |
| --- | --- |
| `status` | 既存の選択・画面・対象キャッシュを受動的に読む。選択や描画更新を行わない |
| `select` | `SelectTool.Select` を1回呼ぶ。要求受付と、その後のキャッシュ更新完了を分ける |
| `scroll_up/scroll_down` | 選択済み個体の所有する縦スクロール領域へ、通常の仮想 `ScrollRect.OnScroll` を1回だけ委譲する。上は+1、下は-1のwheel delta |
| `close` | 同一対象・所有ボタン・単独callbackを照合し、通常の `DeselectAndClose` を1回呼ぶ |

`select/close/scroll_up/scroll_down` は非空のtaskを必要とし、実行には `confirm=true` が必要。`dryRun=true` は引数・対象・通常UI状態の検証だけを行い、選択、閉鎖、ホイールイベント生成・送信、描画更新を行わない。

ゲーム停止、可視な現在ワールドの個体、選択ツール、モーダルや競合UIがないことを照合する。UIが利用不能・busy・所有不一致なら拒否し、別タブへの自動切替、直接の状態変更、低レベルの再試行へ切り替えない。

statusはKSelectable、DetailsScreen、SimpleInfoScreen、MinionVitalsPanelの対象を同一個体へ結び付け、固定Breath定義に一致する行の既存 `LocText.text` だけを読む。Refresh、SetTarget、TryUpdate、formatterを呼んでUIの読取値を作らない。`ToolTip.toolTip` に公開getterがないため、ツールチップは今回の返却に含めない。

本体の初回個体選択では、初期化済みだが非表示・未spawnのDetailsScreenが通常選択によって表示され、その後に起動処理が完了する。共通入口は初期化済みを要求し、画面の読取成功とcloseにはspawn済み・active状態も要求する。statusの `detailsScreenInitialized/detailsScreenSpawned` は既存状態を観測する。取得不能な初期状態を正常値で補わない。

スクロールは対象行、所有する一意な既知のScrollRect/KScrollRect、content、viewport、live EventSystemを実行前後で照合する。位置・速度・Amountを直接変更せず、本体の更新や慣性処理を独自に再現しない。1回のnative呼出し後も `pending/partial` とし、移動距離、完了、呼吸行の可視化を保証しない。後続statusと撮影は呼出側が明示する。

`status` およびselect後の読取での `available=true` は、同一対象の既存キャッシュを取得できたことを示す。previewでは検証成功、closeでは閉鎖後条件の成功という別の意味になる。更新待ちはpendingとして残し、close後は取得可能なstatusで `selectionEmpty/detailsTargetEmpty/detailsScreenActive` を照合できる。

UI読取の `breath.pixelVisibilityVerified=false` は維持する。キャッシュを取得できても、行がスクロール領域の外にclipされている場合がある。スクロールのpending応答自体は画素の可視性や完了を主張しない。可視化の受入には、別途composed screenの撮影で実際の行を確認する。

## ローカライズと検証の境界

独自の案内・エラー・引数説明はPO4と既存runtime3言語に対応させ、暫定Hantと既存Hansを分けて保持する。呼吸行自体は本体が既に解決した文字列を読む。モデル別の対象外判定、ロード直後の初期化、実際の呼吸媒体・帰路判定の網羅は別の検証範囲とする。

公開版自身の実ソースでBreath reader166条件、UI1518条件と関連する速度238・保存MOD情報193・MOD不一致774条件、snapshot457・stress465条件を確認し、本体参照ビルドも警告0・エラー0で成功した。初回開始ダイアログの開発専用経路・試験は取り込んでいない。POとruntimeの対応を限定照合したが、既存の全体ローカライズ検査の失敗を解消した結果ではない。

公開固有の識別子・設定先と自身の導入DLLを照合した停止実機で、通常デュプの満量・息止め時の減少・酸素下での通常回復・満量復帰を、同じ停止窓の詳細値・本体formatter・所有する呼吸行のキャッシュ・別撮影へ照合した。生値と要約の丸め、本体表示を分けて確認し、観測したHPは保持された。status、select/closeの予行・実行・後続状態と、初回未表示からの通常選択、明示した下向きwheel呼出しを確認した。キャッシュの取得成功だけでは行が画面内にあるとは限らず、追加の通常スクロール後の撮影で可視性を確認した。

元の停止セーブ・選択速度・報告カメラへ戻して保存せず通常終了し、導入物・設定・保護セーブ・他MOD・既存画像と開始時の未起動状態へ復帰した。preferencesは本体のセッション値以外が一致し、MCPからpreferencesを書き換えていない。

この受入は通常呼吸の回復であり、RecoverBreathChoreの実行、状態ID一覧、全UIキャッシュの同時更新、窒息限界・救助・全モデル・全DLCを確認したものではない。欠測・真0・例外・非有限値はホストの確認範囲と分ける。個別のセーブ、対象識別子、座標、時刻、生応答、画像、環境設定と私的補助は公開差分へ含めない。
