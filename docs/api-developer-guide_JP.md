# ONI MCP API 開発者ガイド

この文書は開発者と上級ユーザーを対象に、HTTP JSON-RPC 2.0 を通じて Oxygen Not Included MCP サーバーとやり取りする方法を説明します。

**API の安定性に関する注意:** `OniMcp` の `1.0.0` リリースまでは、HTTP の動作、ツール名、パラメーター構造、リソースパス、応答フィールドに互換性を損なう変更が入る可能性があります。サードパーティー製クライアントは対象バージョンを固定し、実行時に `server_control domain=catalog action=manifest` または `oni://tools/manifest` を読み取ってください。フィールドの欠落、改名、意味の変更に対応する互換処理も用意してください。

## クイックスタート

### 1. サーバーを起動する

1. `OniMcp` MOD を ONI の `mods/` ディレクトリにインストールします。
2. ゲームを起動し、**ONI MCP Server** を有効にします。
3. セーブデータをロードします。
4. MCP サーバーは既定で `http://localhost:8788/mcp/` に起動します。

LAN 内からアクセスする場合は、`OniMcpConfig.json` を作成または編集します。

```json
{
  "Host": "0.0.0.0",
  "Port": 8788,
  "AuthEnabled": true,
  "AuthToken": "replace-with-a-strong-token"
}
```

認証を有効にした場合、クライアントは次を送信します。

```text
Authorization: Bearer <token>
```

次の形式にも対応しています。

```text
X-Oni-Mcp-Token: <token>
```

### 2. MCP クライアントを設定する

Claude Desktop / Cursor の設定例:

```json
{
  "mcpServers": {
    "oni": {
      "url": "http://localhost:8788/mcp/"
    }
  }
}
```

### 3. プロトコルをネゴシエーションする

OniMcp は2世代のプロトコルに対応しています。新しいクライアントは MCP `2026-07-28` のセッションを使わない経路を優先して試し、従来のクライアントは `2025-11-25` / `2025-06-18` の `initialize` + `Mcp-Session-Id` を使う経路を引き続き利用します。

#### MCP 2026-07-28: セッションなしでの機能検出

新しい方式のリクエストでは `initialize` を実行せず、`Mcp-Session-Id` の作成・要求・返却も行いません。各リクエストがプロトコルのメタデータを持ち、HTTP ヘッダーでメソッドを宣言します。リソース名やツール名が必要な場合は、それに対応する `Mcp-Name` も送信します。

まず `server/discover` を呼び出すことを推奨します。

```bash
curl -sS -X POST http://localhost:8788/mcp/ \
  -H 'Content-Type: application/json' \
  -H 'MCP-Protocol-Version: 2026-07-28' \
  -H 'Mcp-Method: server/discover' \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "server/discover",
    "params": {
      "_meta": {
        "io.modelcontextprotocol/protocolVersion": "2026-07-28",
        "io.modelcontextprotocol/clientCapabilities": {},
        "io.modelcontextprotocol/clientInfo": { "name": "cli", "version": "1.0" }
      }
    }
  }'
```

現在、この新しい経路は意図的に機能を絞っています。`server/discover`、`resources/list`、`resources/templates/list`、`resources/read` と、読み取り専用の `benchmark` に対する `tools/list` / `tools/call` に対応しています。2025 core Tasks、MRTR、購読、ゲーム状態を変更するツールは、対応機能として通知しません。呼び出し側は `server/discover` が返す capability と `tools/list` を基準にし、旧方式の全ツールを新しい経路でも使えるとは考えないでください。

#### MCP 2025: 従来の initialize/session

既存のゲーム制御ツール全体を必要とする従来のクライアントは、引き続き初期化ハンドシェイクを使用します。

```bash
curl -sS -X POST http://localhost:8788/mcp/ \
  -H 'Content-Type: application/json' \
  -H 'Mcp-Protocol-Version: 2025-11-25' \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
      "protocolVersion": "2025-11-25",
      "capabilities": {},
      "clientInfo": { "name": "cli", "version": "1.0" }
    }
  }'
```

サーバーの応答ヘッダーには `Mcp-Session-Id` が含まれます。その後の従来方式のリクエストには、次を付ける必要があります。

```text
Mcp-Session-Id: <session id>
Mcp-Protocol-Version: 2025-11-25
```

## 推奨するツールインターフェース

`world_editor` の正式なモデル:

- セーブデータはディレクトリとして表されます。`latest/` は、現在または最新のセーブデータを指す固定の別名です。
- `cd latest` でセーブデータに入り、`cd` または `cd ~` で `/` に戻ります。
- ワールドは、操作エンドポイントではなく、構造化されたファイルで表現されます。
- ワールドの変更には、1つのファイルに対する SEARCH/REPLACE 編集を使います。原則として1ブロックを使ってください。複数ブロックには外側の `allowPartial=true` が必要で、トランザクションとしてまとめてロールバックすることはできません。
- 操作用の `*.patch` ファイルを追加しないでください。編集対象のファイルに応じて処理を振り分けます。

既定で公開するツールは、コンパクトな集約ツール群です。

| ツール | 用途 |
|------|---------|
| `benchmark` | 読み取り専用のプロトコルベンチマーク。新しい `2026-07-28` の経路では、現在これだけをツールとして公開しています。 |
| `building_control` | 建築計画、素材、設定、収納、フィルター、生産、サイドパネル、ロケット。 |
| `game_control` | 一時停止、速度、セーブデータ、サンドボックス、UI の操作。 |
| `navigation_control` | カメラ移動、ワールド切り替え、レイヤー表示、フォーカス、スクリーンショット。 |
| `orders_control` | 領域単位の作業指示、優先度、作業指定の変更、配管・電線の切断。 |
| `server_control` | MCP の診断、カタログ、バッチ呼び出し、リソース、サーバー操作。 |
| `world_editor` | ファイルシステム形式のワールドエディター。`/active/ops/` 配下の型付き操作ファイルを含め、仮想的なセーブ・ワールドファイルの読み取り・検索と SEARCH/REPLACE 編集に対応します。 |

仮想ファイルの内部操作は、直接呼び出せる MCP ツールではありません。

- `colony_control`、`coordinate_control`、`dupes_control`、`read_control`、`search_control` は、検証済みの `world_editor` 仮想ファイル経由でのみ使えるよう登録されています。
- これらの名前を `tools/call params.name` として送信しないでください。`world_editor`、構造化リソース、または上記の公開集約ツールを使ってください。
- 正確な位置への作業指示では、`/active/ops/tools.md` を読み、対応する型付き操作ファイルを編集します。生の座標を扱う互換用の入り口は内部専用であり、クライアント向けの公開インターフェースではありません。

新たに連携を実装する場合は、過去の内部操作名をハードコードせず、`tools/list` から公開集約ツールの入り口を取得してください。

## パラメーター設計の規約

新しいツールインターフェースは、検索と操作を優先する設計です。

- ワールドや設備の検索には `world_editor command=search`、ツールの検索には `server_control domain=catalog action=search` を使います。内部の `search_control` は仮想ファイルの実装の一部であり、クライアントが直接呼び出すツールではありません。
- `query`、`target`、`search`、`name`、`id`、`areaId` の指定を優先します。
- 公開ツールは、生の `x/y`、`x1/y1/x2/y2`、`dx/dy`、`points`、`anchors` を受け付けません。正確な位置への作業指示では `/active/ops/tools.md` を読み、`/active/ops/orders.md` を編集します。現在公開されている型付きファイル・ツールだけを選び、非公開の `coordinate_control` と `/active/ops/coordinate.md` の互換用の入り口は使用しません。
- 書き込みや実行の操作は、`dryRun` または `confirm` に対応するものとします。
- 危険な操作や広範囲の正確な位置指定を伴う操作では、一時停止 → 読み取り・計画 → dry-run → 確認 → 検証の順序を必ず守ります。
- タスクの実行結果には、`reachable`、`executable`、失敗理由、不足している条件、推奨される次の行動を返すものとします。
- 建築に関する結果には、素材面で実行可能かどうかを返し、少なくとも必要素材、利用可能な素材、不足量を示すものとします。

現在の従来方式の公開ツールでは、すべての `tools/call` に、その呼び出しで行うユーザーのタスクを説明する空でない文字列の `arguments.task` が必要です。省略や空文字列は、ツールへの振り分け前に拒否されます。

## 呼び出し例

### ツール一覧を取得する（従来の 2025 セッション経路）

```bash
curl -sS -X POST http://localhost:8788/mcp/ \
  -H 'Content-Type: application/json' \
  -H "Mcp-Session-Id: $SID" \
  -H 'Mcp-Protocol-Version: 2025-11-25' \
  -d '{
    "jsonrpc": "2.0",
    "id": 2,
    "method": "tools/list",
    "params": {}
  }'
```

新しい `2026-07-28` のクライアントでは `Mcp-Session-Id` を送信しません。上記のセッションなしの規則に従い、`_meta`、`MCP-Protocol-Version`、`Mcp-Method` を付けてください。現在の新方式の `tools/list` は、読み取り専用の `benchmark` だけを通知します。

### ツールを検索する

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "server_control",
    "arguments": {
      "task": "Find the public tool for wiring and build materials",
      "domain": "catalog",
      "action": "search",
      "query": "wire build material",
      "detail": "brief"
    }
  }
}
```

### コロニーの状態を読み取る

```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "resources/read",
  "params": {
    "uri": "oni://colony/status"
  }
}
```

### 利用可能な型付き操作を確認する

```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "tools/call",
  "params": {
    "name": "world_editor",
    "arguments": {
      "task": "Inspect the typed operation files before planning an exact order",
      "command": "read",
      "path": "/active/ops/tools.md"
    }
  }
}
```

領域や正確な位置が必要な場合は、`world_editor` が公開するマップと型付き操作ファイルを基準にしてください。内部の `read_control` / `coordinate_control` を直接呼び出さないでください。

### 建築計画と素材をプレビューする

```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "tools/call",
  "params": {
    "name": "building_control",
    "arguments": {
      "task": "Preview a Manual Generator near the Printing Pod",
      "domain": "planning",
      "action": "preview",
      "prefabId": "ManualGenerator",
      "material": "CopperOre",
      "query": "near printing pod"
    }
  }
}
```

想定される応答には、次のようなフィールドが含まれます。

```json
{
  "reachable": true,
  "executable": true,
  "materials": {
    "requirementKnown": true,
    "requiredKg": 400.0,
    "selectedAvailableKg": 910.0,
    "satisfied": true,
    "shortageKg": 0.0,
    "availableMaterials": [
      { "tag": "CopperOre", "availableKg": 910.0 }
    ]
  }
}
```

`materials.satisfied=false` の場合、クライアントは必要量、利用可能な素材、不足量をユーザーに示し、建築の実行に進んではいけません。

### 正確な位置に建築計画を配置する

正確な位置に建築するには、まず `/active/map/viewport.md` を読みます。必要に応じて zoom や `symbols/glyphs.md` の読み取りを行ってください。次に、編集可能なマップの Markdown に SEARCH/REPLACE を適用し、対象の空きセルのトークンを `建筑名:优先级` に置き換え、必要に応じて `#材料字` を付けます。これらは建築名・優先度・素材の記号を表す構文です。この経路では、内部で `building_control build_area` の anchors に変換されます。`/active/ops/build.md` は、生の座標を使わない意味ベースの `plan` / `auto_connect` などの型付き呼び出し専用です。原則として1ブロックを使います。複数ブロックは外側に `allowPartial=true` がある場合だけ許可され、トランザクションとしてまとめてロールバックできません。プレビューでは外側の edit に `dryRun=true` と `confirm=false`（または省略）を指定します。実行時には別の edit を新たに作成し、外側に `dryRun=false` と `confirm=true` を指定して、内側のコマンドのフラグと矛盾しないようにします。実行後はマップや状態を読み直して検証してください。

### 配線・配管を切断する

```json
{
  "jsonrpc": "2.0",
  "id": 8,
  "method": "tools/call",
  "params": {
    "name": "orders_control",
    "arguments": {
      "task": "Cut conduits in the starter-wire area",
      "domain": "conduit",
      "action": "cut_conduits",
      "areaId": "starter-wire",
      "type": "auto",
      "confirm": true
    }
  }
}
```

`type=auto` には、既定で気体パイプ、液体パイプ、コンベアレール、電線、自動化ワイヤーが含まれます。電線だけを切断する場合は `type=wire`、自動化ワイヤーだけの場合は `type=logic` を指定します。

## リソースの読み取り

よく使うリソース:

| URI | 説明 |
|-----|------|
| `oni://colony/status` | サイクル、複製人間の人数、速度、一時停止の状態 |
| `oni://colony/diagnostics` | 酸素不足、食料不足、過熱などの診断 |
| `oni://colony/summary` | 行動計画用のコロニー概要 |
| `oni://resources/inventory` | 資源の在庫 |
| `oni://resources/food` | 食料の在庫と保存状態 |
| `oni://dupes/status-check` | 複製人間の位置、作業、欲求、到達不能の疑い |
| `oni://power/summary` | 電力網の概要とバッテリーの状態 |
| `oni://power/ports` | 電力ポートのセル、基点、接続点、ポートに電線があるかどうか |
| `oni://world/text-map` | テキストマップ |
| `oni://buildings/defs` | 建築可能な設備の定義 |
| `oni://tools/manifest` | ツール一覧 |
| `oni://tools/guide` | 目的に応じたツールの呼び出し手順 |

## Agent Program

`server_control domain=program action=execute` で、小規模なツールスクリプトを実行できます。構造が明確な短い手順にだけ使用し、まず `dryRun=true` で構造とツール名を検証することを推奨します。

主な仕様:

- `saveAs` は、ツールが返した JSON を保存します。
- `$name.path` は、変数のパスを読み取ります。
- 式は `eq/ne/lt/lte/gt/gte/and/or/not/add/sub/mul/div/mod/contains/exists` に対応します。
- `maxSteps` は、実行ステップ数を制限します。

## 開発用ディレクトリ

```text
mods/OniMcp/
├── ModInfo.cs           # KMod のエントリーポイント
├── Config/              # オプション
├── Core/                # MCP プロトコルの型
├── Localization/        # STRINGS
├── Patches/             # Harmony パッチとゲーム操作方針
├── UI/                  # 実行時のオーバーレイ
├── Server/              # HTTP/MCP サーバー
├── Support/             # ログ、パス、リフレクション
└── Tools/
    ├── Core/            # ツールとリソースの登録
    ├── Entry/           # 集約エントリーポイント（*Control* / Read / 英語の説明）
    ├── WorldEditor/     # 仮想ワールドのファイルシステム
    ├── Shared/          # 共通の補助処理
    └── Impl/            # 各ドメインの実装
```

開発時の推奨事項:

1. 新しい公開機能は、まず `Tools/Entry/` の集約エントリーポイントの拡張で対応します。
2. ドメインの実装は `Tools/Impl/<Domain>/` に置きます。
3. 検索、素材、到達可能性の共通ロジックは `Tools/Shared/` に置きます。正確な位置を扱う操作は、`/active/ops/` 配下の型付きファイルを通して処理します。
4. 既定で公開するツールの説明は `Tools/Entry/CoreToolEnglishDescriptions.cs` で管理し、英語を維持します。
5. `server_control domain=catalog action=static_audit` と `manifest` を使って登録結果を検証します。

## クライアントの互換性に関する推奨事項

- 新しいクライアントは、まず `2026-07-28` の `server/discover` を試し、検出結果に従って現在の新方式の capability を使います。従来の全ツールが必要な場合は、引き続き対応している 2025 の initialize/session 経路を使います。
- 古い細分化されたツール一覧をハードコードしません。
- 通常のツールに座標を渡しません。正確な位置への作業指示は `/active/ops/orders.md`、正確な位置への建築は `/active/map/viewport.md` のマップトークン編集を使います。非公開の座標互換用の入り口は使用しません。
- まず manifest を読み取り、その後 `domain/action` に基づいて呼び出しを組み立てます。
- フィールドの欠落、未知の action、`executable=false` に対応します。
- 危険な操作では必ずユーザーの確認を求め、実行後に状態を読み取って検証します。
