# ONI MCP Server

> 公開用の独立した実験用フォークです。ID: `PublicFork.OniMcp`。問い合わせ先 (this fork’s issue tracker)。Steam公開は無効。オリジナル版と同時に有効化しないでください。

[![ONI MCP Server のプレビュー](preview.png)](README_JP.md)

ONI MCP Server は、Oxygen Not Included 向けの MOD です。ローカルの MCP サービス（`http://localhost:8788/mcp/`）を公開し、`oni://` リソースを通じたコロニーの状態確認と、制御された操作を提供します。AI やクライアントと安全に連携するための設計です。

## 目次

- [用途と対象範囲](#用途と対象範囲)
- [インストールと起動](#インストールと起動)
- [エンドポイントとクライアント](#エンドポイントとクライアント)
- [設定とセキュリティ](#設定とセキュリティ)
- [ツールの構成](#ツールの構成)
- [主なリンクとリソース](#主なリンクとリソース)
- [互換性と安定性](#互換性と安定性)
- [更新と検証](#更新と検証)
- [参考情報と謝辞](#参考情報と謝辞)

## 用途と対象範囲

### できること

- コロニーの状態、警告、資源、マップ単位のデータを読み取る。
- 集約ツールを通じて、一時停止、速度変更、スクリーンショット、スケジュール設定、建築、優先度に関する作業など、小規模なゲーム操作を行う。
- ユーザーの明示的な確認を伴う、MCP クライアントからの安全な操作経路を提供する。

### 対象外となること

- 長期のプレイをすべて任せる、完全自律型のプレイヤーではありません。
- 大規模な採掘・解体、再起動、ロードなどの高リスクな操作は、クライアント側で内容を明示し、確認を得てから実行してください。

## インストールと起動

1. MOD を購読し、ゲーム内の **Mods** メニューで **ONI MCP Server** を有効にします。
2. ゲームを再起動します。メインメニューやコロニーの画面で、サービスが自動起動します。
3. `oni://` リソースに関するツールには、ロード済みのコロニーが必要です。一部の読み取り専用チェックは、それ以前のメインメニューでも利用できます。

## エンドポイントとクライアント

- ブラウザー向けページ: `http://localhost:8788/`
- MCP エンドポイント: `http://localhost:8788/mcp/`
- クライアントの設定例とリクエストの説明: [docs/api-developer-guide_JP.md](../../docs/api-developer-guide_JP.md)
- MCP リソースのリファレンス: [docs/mcp-tools-reference_JP.md](../../docs/mcp-tools-reference_JP.md)

## 設定とセキュリティ

- 設定ファイル: `OniMcpConfig.json`
- 主なフィールド、既定値、読み込み先の優先順位: [mods/OniMcp/ModInfo.cs](ModInfo.cs)
- `localhost`、`127.0.0.1`、`::1` では、既定でトークンを必要としないローカル開発用の設定を維持します。既定値は `AuthEnabled=false` です。
- ループバック以外のアドレス（`0.0.0.0`、LAN の IP アドレス、リモートのホスト名を含む）では、`AuthEnabled=true` が必須です。無効のままでは、設定の保存や待ち受け開始は安全側に倒して失敗し、ネットワークの待ち受けを開始しません。
- Bearer トークンが提供するのは認証だけで、通信の暗号化ではありません。リモートへ直接 `http://` で接続するとトークンが平文で送信されます。リモートアクセスには、信頼できる VPN・トンネルか、HTTPS を終端する TLS リバースプロキシを優先し、OniMcp 側はループバック・ローカルの接続に保ってください。
- セキュリティ設定の移行では移行バージョンを記録するだけで、既存の `AuthEnabled` の選択を変更したり、旧設定の待ち受け範囲を黙って広げたりはしません。認証が有効な旧設定では、引き続きクライアントにトークンを要求します。
- 設定の変更後は、オプション画面の **Restart MCP server** を押すか、ゲーム全体を再起動して反映します。

## ツールの構成

- マニフェストの入り口: `oni://tools/manifest` または `server_control domain=catalog action=manifest`
- 既定の公開集約ツール:
  - `benchmark`: 読み取り専用のツール呼び出し経路のベンチマークと診断
  - `world_editor`: 仮想ファイルを通じたワールドへのアクセス（`cd`、`ls`、`read`、`search`、`edit`）
  - `game_control`: ゲームの状態と制御
  - `navigation_control`: カメラ、レイヤー表示、スクリーンショット
  - `building_control`: 建築計画、配置、配線・配管の操作
  - `orders_control`: 採掘、片付け、モップ掛け、解体の作業指示
  - `server_control`: サーバーの状態、スクリーンショット、バックグラウンドタスク

## 主なリンクとリソース

- `oni://tools/manifest`
- `oni://tools/guide`
- `oni://colony/status`
- `oni://dupes/status-check`
- `oni://world/text-map`
- `oni://buildings/defs`
- `oni://resources/inventory`
- [scripts/verify_onimcp_tool_surface.py](../../scripts/verify_onimcp_tool_surface.py)
- [CHANGELOG_JP.md](CHANGELOG_JP.md)

## 互換性と安定性

- `1.0.0` より前は、ツール名、パラメーター、応答フィールドに互換性を損なう変更が入る可能性があります。
- サードパーティー製クライアントは、バージョンを固定し、実行時のマニフェストを互換性判断の基準にしてください。
- `2025-11-25` / `2025-06-18` のクライアントは、`initialize` + `Mcp-Session-Id` による全ツールの経路を引き続き使えます。
- `2026-07-28` には、`server/discover`、`resources/list`、`resources/templates/list`、`resources/read` を使うセッションなしの互換経路があります。ツールは意図的に絞られており、`tools/list` は読み取り専用の `benchmark` だけを通知し、`tools/call` もこのツールだけを受け付けます。新方式のリクエストには `_meta`、`Mcp-Protocol-Version`、`Mcp-Method` が必要で、リソースの読み取りとツール呼び出しには、対応する `Mcp-Name` も必要です。この経路では `Mcp-Session-Id` を作成・返却しません。
- 新方式の経路は、従来の core Tasks フィールドを公開せず、Tasks、MRTR、購読、ゲーム状態を変更するツールも、まだ対応機能として通知しません。リクエスト単位の状態管理と、それぞれの 2026 ヘッダー・スキーマの規約への移行が完了するまで、これらのツールは 2025 のセッション経路に残します。

## 更新と検証

- 変更履歴: [CHANGELOG_JP.md](CHANGELOG_JP.md)
- 検証: [scripts/verify_onimcp_tool_surface.py](../../scripts/verify_onimcp_tool_surface.py)

## 参考情報と謝辞

- リポジトリ: [LIghtJUNction/OniMods](https://github.com/LIghtJUNction/OniMods)
- MCP エコシステム: [modelcontextprotocol](https://github.com/modelcontextprotocol)
- 関連プロジェクト: [FastTrack](https://github.com/peterhaneve/FastTrack)、[Harmony](https://github.com/pardeike/Harmony)
- 開発・テスト: gpt5.5、gpt5.6、glm5.2、Kimi k2.6、Kimi k3、Gemini 3.5 Flash、grok4.5、LIghtJUNction

## 表示言語

Web 設定 `/settings/` の「MCP の言語」で `auto`・`en`・`ja`・`zh-Hans` を選べます。公開ツールの説明と引数説明、共通エラー、Web 設定画面に適用します。ゲーム由来の名称はゲーム側の表示言語を使います。適用範囲・クライアント再接続・翻訳追加方法は [言語切替ガイド](../../docs/mcp-localization_JP.md) を参照してください。
