# MOD 不一致ダイアログの読取と確認

`game_control domain=ui uiDomain=dialog` は、セーブ読込時にゲーム本体が表示する「MOD 不一致」の通知を読み取り、その通知の通常の確認ボタンを押す窓口です。対象は `dialog=mod_mismatch` のみです。

## 基本の流れ

1. `action=status` で現在の通知と本文を読む。
2. 返された `dialogs` が 1 件であることと、内容・`canAcknowledge` を確認する。
3. 必要なら、同じ `dialogToken` で `action=acknowledge dryRun=true` を直接呼び、現在の UI を検証する。
4. 確認すると判断した場合に、同じ `dialogToken` と `confirm=true` で実行する。
5. `closed` と `partial` を確認し、必要に応じて再度 `status` を読む。

### 現在の通知を読む

ツール: `game_control`

```json
{
  "task": "現在のMOD不一致通知を確認する",
  "domain": "ui",
  "uiDomain": "dialog",
  "dialog": "mod_mismatch",
  "action": "status"
}
```

`status` は、表示中のタイトル・本文・確認ボタンのラベルを返します。ゲームの時間、一時停止状態、UI の表示や選択を変更せず、読取のために画面を生成・再表示しません。本文を再計算するために MOD イベント一覧を消費することもありません。

| フィールド | 意味 |
| --- | --- |
| `available` | この窓口で現在の UI 状態を判定できるか。 |
| `present` / `count` | 認識済みで現在表示中の対象通知の有無と件数。未準備・取得不能時はともに `null`。 |
| `dialogs[].dialogToken` | 対象 Popup の生成世代を特定する値。次の要求へそのまま渡す。 |
| `title` / `message` / `confirmLabel` | 実際にゲームが表示している文字列。ゲーム側の言語に従う。 |
| `canAcknowledge` | 現時点で対象画面とボタンの検証を通過するか。実行時に改めて検証する。 |
| `reasonCode` / `reasonMessage` | 操作できない理由の機械識別子と、MCP 側の言語設定による説明。 |

`available=false` は「通知がない」という確定ではありません。API の形状が非対応、UI が未準備、または読取不能である場合は、`present=null`・`count=null` として返します。通常の不在は `available=true`・`present=false`・`count=0` です。

`dialogToken` は Popup が生成されるたびに変わります。同じ画面インスタンスが別の Popup に再利用された場合も、それまでの値は無効です。同じ Popup の非表示・再表示だけでは生成世代は変わりません。トークンを保存して別のロードや将来の通知へ使い回さず、その都度 `status` を読み直してください。

`canAcknowledge=true` は、既定の `keepPaused=true` に必要な速度制御画面の存在までは保証しません。この条件は `acknowledge` の実行前に追加で検証します。

### 通常 UI の対象検証だけを行う

ツール: `game_control`

```json
{
  "task": "表示中のMOD不一致通知を閉じられるか確認する",
  "domain": "ui",
  "uiDomain": "dialog",
  "dialog": "mod_mismatch",
  "action": "acknowledge",
  "dialogToken": "<直前のstatusで取得した値>",
  "dryRun": true,
  "keepPaused": true
}
```

この直接呼出しの `dryRun=true` は、実行と同じ対象・ボタン・前面モーダル・一時停止維持の条件を検証します。クリックも一時停止操作も行わず、トークンも消費しません。成功時は `preview=true`・`wouldAcknowledge=true`・`requested=false` を返します。プレビュー後に画面が変わる可能性があるため、実行時の再検証は省略されません。

### 通知を確認する

ツール: `game_control`

```json
{
  "task": "内容を確認したMOD不一致通知を閉じる",
  "domain": "ui",
  "uiDomain": "dialog",
  "dialog": "mod_mismatch",
  "action": "acknowledge",
  "dialogToken": "<直前のstatusで取得した値>",
  "confirm": true,
  "keepPaused": true
}
```

実行には `confirm=true` が必要です。`confirm`・`dryRun`・`keepPaused` は JSON の真偽値で指定し、文字列の `"true"` などは渡しません。`keepPaused` の既定値は `true` です。通常のモーダル終了処理で一時停止が解除された場合は、同じ処理内でゲーム本体の一時停止窓口から維持します。既に停止中なら重ねて停止回数を増やしません。`keepPaused=false` は、モーダルの通常の一時停止解除に任せる指定で、強制的にゲームを再開する指定ではありません。

実際の経路は `ConfirmDialogScreen.confirmButton.KButton.SignalClick` です。対象ボタンの通常の `OnSelect_OK` を通り、MCP が画面を直接破棄・非表示にする代替処理はありません。この操作は MOD の有効化・無効化、構成の書換え、再起動を行うものではありません。

## 対象の限定と拒否条件

ゲーム本体の現在の MOD マネージャーが、ネイティブの「セーブと MOD 構成が異なる」タイトル・本文テンプレートと通常の親 Canvas を使って生成した通知だけを記録します。タイトル文字列が似ている別画面や、任意の確認ダイアログは対象になりません。生成時の由来を記録していない画面を、読取要求で後から対象へ昇格させることもありません。

実行時には、現在の生成世代、表示文、確認ボタンの同一性、画面・ボタンの初期化と有効状態、操作可能性、通常の OK バインドを再検証します。キャンセル・追加選択肢・独自コールバック・追加クリックハンドラーなどがある未知の形状は拒否します。

対象より前面に別のモーダルがある場合や、ゲーム本体が入力を無効化している場合は `blocked` です。別画面を閉じて対象へ到達したり、無効化を迂回したりしません。対象通知が複数ある場合も、MCP 側で一つを推測して選択しません。

## 結果の読み方

| 結果 | 読み方 |
| --- | --- |
| `requested=false` | 対象のクリックは要求していない。プレビューまたは実行前の拒否。 |
| `ok=true, requested=true, closed=true, partial=false` | 通常クリックを要求し、その処理後に対象の終了を確認した。 |
| `requested=true, closed=false, partial=true` | クリック後も対象が残っている。完了扱いにしない。 |
| `ok=false, requested=true, partial=true` | クリック・一時停止維持・終了確認の途中で失敗した。既に起きた変化があり得る。 |
| `closed=null` / `pausedAfter=null` | 該当状態を確定できなかった。正常値へ置き換えて解釈しない。 |

`pausedAfter` は処理後に読み取れた実際の停止状態です。`ok` や ops の `applied` 件数だけで終了を判断せず、内側の `closed`・`partial`・`pausedAfter` を確認してください。

実クリック直前にトークンを消費します。クリックが例外になった場合も同じ値による再試行は行いません。`partial` の場合は、現在の `status` を読み直して状態を判断します。通知が残っていても消費済みトークンは `token_stale` となり、自動連打や低レベル処理への切替は行いません。

主な拒否理由は次のとおりです。

| `reasonCode` | 内容 |
| --- | --- |
| `unavailable` | ネイティブ API または UI の取得・判定ができない。 |
| `not_present` | 認識済みの対象通知が現在表示されていない。 |
| `ambiguous` | 対象通知が複数ある。 |
| `token_required` / `token_stale` | トークンがない、現在の世代と異なる、または消費済み。 |
| `blocked` | 別の前面モーダルまたは入力無効化により操作できない。 |
| `button_unavailable` | 通常の確認ボタンが未準備・無効・操作不能など。 |
| `unsupported_shape` | 通知の構造やコールバックが対象の通常形状と異なる。 |
| `pause_unavailable` | 一時停止の維持または処理後の停止状態の確認ができない。 |
| `click_failed` | 通常ボタンのクリック処理中に例外が発生した。 |
| `confirm_required` / `invalid_boolean` | 実行確認がない、または真偽値の型が不正。 |
| `unsupported_action` / `unsupported_dialog` | 対応していない操作または対象種別。 |

## ops ファイルを使う場合

`world_editor command=read path=/active/ops/game.md` で形式を確認し、`## Edit Commands` の下へ 1 編集につき 1 コマンドを渡します。`/active/ops/game.md` は `game_control` の型付き窓口です。

```text
call domain=ui uiDomain=dialog dialog=mod_mismatch action=acknowledge dialogToken="<直前のstatusで取得した値>" confirm=true keepPaused=true detail=true
```

`world_editor` 側にも実行時の `confirm=true`・`dryRun=false` が必要です。外側の `task` は子要求へ継承できます。実行する場合は SEARCH/REPLACE の現行形式に従って渡してください。

`world_editor` の外側の `dryRun=true` は構文・経路・展開内容のプレビューです。子の `game_control` は呼ばれず、実画面・トークン・ボタンの検証結果ではありません。コマンド行側に `dryRun=true` を付けた場合も ops は子を実行せずにプレビューを返します。通常 UI の対象を検証したい場合は、前述の直接 `game_control` のプレビューを使ってください。

ops 実行では、コマンド行の `detail=true` によって子の応答本文を保持します。返されたネスト内の `result` を読み、`closed`・`partial`・`pausedAfter` を確認してください。外側の成功や件数だけでは、ダイアログの終了を意味しません。

## 検証の扱い

実際の adapter・handler・game→ui 転送を使うホスト試験は 774 条件を通過し、起源・トークン・クリック経路・追加コールバックのガードを壊した 5 種類の変異も検出しました。Unity とネイティブ UI 境界は試験用の代替であり、本体実行の証明とは分けています。

ゲームビルド 744825 を参照した開発版の Release ビルドは警告・エラーとも 0 でした。開発版固有 ID の実機で、メニューでの不在、自然に生成された 2 回の通知、本文の一致、繰り返し読取、誤トークン・確認漏れ・真偽値の型不正の拒否、直接の予行、既定の停止維持、再ロードでの世代変更と旧トークン拒否、正式 ops の詳細結果を確認しました。`keepPaused=false` でも元からの停止は維持され、ゲーム内時刻・速度選択・視点は変化しませんでした。実時間は別に扱っています。

他モーダル・未知コールバック・クリックや速度制御の例外・モーダルだけの停止解除はホスト試験の範囲です。任意のダイアログや全 MOD 組合せの対応を保証しません。公開ソースでも同じホスト 774 条件と翻訳 26 キーの照合、警告・エラー 0 の参照ビルドが成功しました。公開固有 ID の実機でもメニュー・通知本文・誤操作拒否・直接予行・正式 ops の詳細結果・既定の停止維持を確認しました。復帰時に発生した別世代の通知でも旧トークンは拒否され、新しいトークンで通常 OK が成功しました。

元セーブを通常ロードで選び直し、保存せず通常終了した後、開始時の導入物・設定 25 ファイル、元の不在ファイル、原本 2 件と試験分岐 2 件の保存内容をハッシュで独立照合しました。開始時のゲーム未起動状態へ戻し、明示されていた他 MOD の有効状態も保持しました。確認した開発・公開ログの Exception / ERROR 行は 0 でした。個別のセーブ・設定・生応答・画面は公開文書へ含めません。
