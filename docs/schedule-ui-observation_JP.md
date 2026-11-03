# 時間割画面の送信と表示を分けて確認する

時間割画面へ入力を送れたことと、実際に画面が開いたことは別です。`screen=schedule` では、既存の通常Action送信と、本体の画面所有・active状態の観測を分けて返します。別の管理画面・一般Actionの既存契約は変更しません。

## 状態だけを読む

公開ツール `game_control` を使います。`management_status` は時間割画面だけに対応し、画面を開かず、準備済みの本体状態を受動的に読みます。`task` はこの読取にも必要です。`confirm` は要求しません。

```json
{
  "domain": "ui",
  "uiDomain": "action",
  "action": "management_status",
  "screen": "schedule",
  "task": "時間割画面が表示されているか確認する"
}
```

本体の管理メニューが持つ時間割画面参照、画面のactive状態、本体入力フォーカスを読みます。別の静的singletonは対象同定の必須条件にせず、管理メニューが持つ参照を対象とします。未準備・所有不一致・読取失敗・状態不整合は取得不能です。読取のために表示、初期化、更新や状態修復を呼びません。フォーカスが無い場合でも、他の条件を満たす既存状態は読めます。

## 開く前に予行し、明示して開く

```json
{
  "domain": "ui",
  "uiDomain": "action",
  "action": "open_management",
  "screen": "schedule",
  "dryRun": true,
  "task": "時間割画面を開く前に既存のUI状態を確認する"
}
```

予行は既存状態の確認だけで、通常Actionを送信しません。閉じている画面へ入力を送るには、本体の入力フォーカスも必要です。予行が通っても、その後の表示を保証しません。

実行する場合は、`dryRun=false` と `confirm=true` を明示します。

```json
{
  "domain": "ui",
  "uiDomain": "action",
  "action": "open_management",
  "screen": "schedule",
  "dryRun": false,
  "confirm": true,
  "task": "時間割画面を開いて内容を確認する"
}
```

`confirm` と `dryRun` はJSONのbooleanです。文字列や数値で代用できません。予行以外で確認が無ければ拒否します。`task` は公開経路の共通middlewareが検証します。

画面がすでに開いていれば `alreadyOpen=true` とし、Actionを送らずno-opにします。通常のtoggleを再送して閉じることを避けます。閉じている場合だけ、従来と同じ通常dispatcherへ1回要求し、その直後に同じ所有者の状態を読み直します。送信失敗や未表示でも、自動再送しません。private toggle、直接のShow／Activate／Refresh、field変更へのfallbackもありません。

## 応答を読む

| フィールド | 意味 |
| --- | --- |
| `requestedScreen` | 要求した画面。ここでは `schedule` |
| `dispatched` | 通常dispatcherの呼出しを確認できたか。状態読取・予行・no-opでは `false`、送信中断で結果が不明なら `null` |
| `observedOpen` | 正確な所有関係を確認した画面が開いているか。既知の閉鎖は `false`、取得不能・送信後の所有不明は `null` |
| `status` | `observed`、`preview`、`pending`、`partial`、`unavailable` |
| `stateAvailable` | 本体UI状態を読めたか。要求対象としての観測成立は `observedOpen` と併せて確認する |
| `alreadyOpen` | 開く要求の前から表示済みで、再送を省略したか |
| `focused` | 本体が報告した入力フォーカス。取得不能は `null` |
| `opened` | `status=observed` かつ `observedOpen=true` のときだけ付く互換フィールド |
| `reasonCode` | 拒否・待機・部分結果の機械可読な理由 |

`observed` は、読取対象の状態を観測できたという意味です。閉じた状態の読取も含むため、表示確認には `observedOpen=true` が必要です。`opened` が付いても、別途要求するスクリーンショットの画素や、画面内の全内容まで確認した結果ではありません。

共通dispatcherの既存の入力割当・準備状態ガードで拒否された場合は、その既存エラー契約を保ちます。表中の観測フィールドがすべて付くとは限りません。

`pending` は送信後も開いた状態を観測できていない結果です。`partial` は送信の中断や所有者変更などで、要求の最終状態を確定できない結果です。`dispatched=true` や `ok=true`、通信・tool呼出しの成功だけで表示完了と判断しないでください。必要なら、後から明示的に `management_status` やスクリーンショットを要求します。

この読取・予行の「UI無操作」は、管理画面への入力・表示・更新を行わないという範囲です。従来の共通task説明表示は保持され、通常の公開経路では設定に応じてtaskの吹き出し等が表示される場合があります。読取でUI全体の画素が絶対に変化しないという保証ではありません。

## 公開説明と翻訳の共有

公開schemaの `screen` 説明に使う `tools.game_control.parameters.screen` は、`ui_action.management_screen_description` と同じ訳へ揃えます。POでは既存の `OniMcp.MCP.UI_ACTION.MANAGEMENT_SCREEN_DESCRIPTION` を共有し、前者をruntime aliasのコメントとして明示します。別の翻訳本文やcontextは増やしません。この説明の共有は、画面名のenumを追加する変更ではありません。

## 検証の範囲

公開版自身の実ソース・実カタログをリンクしたホスト502条件（追加408・既存94）、PO／runtimeの翻訳483検査、本体参照によるbuild.81の警告・エラー0件を確認しました。旧全文の誤openedと、拒否previewのdryRun誤表示を検出する意味的負対照も保持しています。

公開版自身の停止実機で、メニュー未取得null／ロード後の既知閉鎖false、status・previewの無送信、確認不足拒否、単発の通常dispatcher要求による実表示、後続statusと完成PNG、既表示no-opの表示継続、通常Actionによる閉鎖を確認しました。未取得時の拒否previewもdryRun=trueを保持します。非対象の健康管理Actionの表示・画像・閉鎖と、その間の時間割false、停止時計・速度・視野も照合しました。本体入力focusは自然に得られた条件で、OSフォーカスを操作していません。

所有者変更・読取例外・非フォーカス・送信中断等の失敗枝はホストの代替境界による確認で、実機で全生成したものではありません。単発要求はソース・ホスト・呼出し記録の根拠で、全native consumerへの配送回数を計測した意味ではありません。全管理画面の観測対応や、UI全体の画素／同期保証へ一般化しません。個別のセーブ、実応答、生ログや画像は公開資料に含めません。
