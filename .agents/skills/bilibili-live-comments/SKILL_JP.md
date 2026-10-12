---
name: bilibili-live-comments
description: Bilibili のライブ配信ルームのコメント・弾幕の読み取り、定期取得、送信を行うときに使用する。読み取りには可能な限り公開 API を使い、認証が必要な送信には .env の BILI_COOKIE を使う。
---

# Bilibili ライブコメント

ユーザーから Bilibili のライブ配信ルームのコメントの読み取り、定期取得、監視、送信を依頼されたときに、このスキルを使用する。

## 安全性と認証情報

- 読み取り専用のコメント確認には、利用可能であればルームの公開コメント API を使う。
- コメントの送信には、`bili_jct` を含むログイン済みの Bilibili Cookie が必要。
- Cookie は `.env` の `BILI_COOKIE=...` から読み込む。
- ユーザーから送信を依頼され、`.env` に `BILI_COOKIE` がない場合は、新しい Cookie の提供を依頼する。
- Cookie を出力、引用、コミット、アップロードしない。
- Cookie を保存する前に、`.env` が git の追跡対象から除外されていることを確認する。

```bash
git check-ignore -v .env
```

除外されていなければ、`.gitignore` に `.env` を追加する。

## コマンド

既存のメッセージも表示して、コメントを一度読み取る。

```bash
python .agents/skills/bilibili-live-comments/scripts/bilibili_live_comments.py --room-id "$BILI_ROOM_ID" --print-existing
```

コメントを定期的に取得する。

```bash
python .agents/skills/bilibili-live-comments/scripts/bilibili_live_comments.py --room-id "$BILI_ROOM_ID" --interval 60
```

`.env` を使ってコメントを1件送信する。

```bash
python .agents/skills/bilibili-live-comments/scripts/bilibili_live_comments.py --room-id "$BILI_ROOM_ID" --send 'message'
```

シェルに `>` を直接記述せず、アシスタントの状況報告コメントを送信する。

```bash
python .agents/skills/bilibili-live-comments/scripts/bilibili_live_comments.py --room-id "$BILI_ROOM_ID" --assistant-send 'message'
```

## 補足

- スクリプトは、現在の作業ディレクトリから親ディレクトリを順にたどり、`.env` を自動で探す。
- スクリプトは Cookie を書き込まず、`BILI_COOKIE` を読み取るだけ。
- 送信後の状況報告は簡潔にする。レスポンス全体ではなく、Bilibili が `code: 0` を返したかどうかを伝える。

Room configuration: set `BILI_ROOM_ID` to the room you intend to access. The CLI requires `--room-id`; no default room is configured.
