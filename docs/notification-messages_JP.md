# チュートリアル等の通知本文の取得

`colony_control domain=notification action=list includeMessageBody=true` で、現在のメッセージ通知の本文とツールチップを取得する。通常の一覧は既定で本文を省略する。

公開経路の例（`world_editor`）:

```json
{
  "action": "edit",
  "path": "/active/ops/colony.md",
  "content": "<<<<<<< SEARCH\n=======\ncall domain=notification action=list query=チュートリアル limit=1 includeMessageBody=true detail=true\n>>>>>>> REPLACE",
  "dryRun": false,
  "confirm": true,
  "task": "チュートリアル本文の読み取り"
}
```

この仮想操作ファイルの呼び出し形式はeditだが、実行対象の`notification action=list`は読取専用。`detail=true`は操作結果の本文を返すために必要。長い一覧は操作結果の文字数制限に達する場合があるので、`query`と`limit`で絞り込む。queryは従来の通知メタデータを検索し、本文は検索しない。

## 返却値

指定時のみ、各通知へ`message`を追加する。

- `messageType`: ゲームのMessage派生型。対象がなければnull。
- `format`: `game_rich_text`。ゲーム側の言語・改行・リンクや装飾タグを保持。MCP表示言語への再翻訳やHTML実行は行わない。
- `body` / `tooltip`: それぞれ`available`、`text`、`source`、`reason`、`errorType`を持つ。
- `source`: 公開APIの`MessageNotification.message.GetMessageBody()`／`GetTooltip()`。
- 取得成功した空文字は`available=true`。nullは`returned_null`、Message欠落は`message_missing`、通常の非メッセージ通知は`not_message_notification`、例外は`read_failed`。未取得のtextはnull。

本文とツールチップの例外を個別に扱い、一方の失敗で他方や一覧全体を失わない。本文取得は検索・件数制限後の対象だけに行う。通知のクリック、ダイアログ表示、削除、既読化の操作は呼び出さない。通常の警告通知の動的ツールチップや動画そのものの内容は今回の対象外。
