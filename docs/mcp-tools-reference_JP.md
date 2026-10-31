# ONI MCP ツールリファレンス

**座標の向き: Y+（Yが増える方向）は上（地表方向）、Y-（Yが減る方向）は下（地下方向）。** 地図は上から下へYが減る順に表示します。

この文書は、`OniMcp` で現在推奨されるツールインターフェースを説明します。実行時のツール一覧は、常に `server_control domain=catalog action=manifest` と `oni://tools/manifest` を正とします。

## クイックスタート

- MCP のアドレス: `http://localhost:8788/mcp/`
- 対応プロトコル: `2026-07-28` のセッションなしの互換経路と、`2025-11-25` / `2025-06-18` の従来の initialize/session 経路
- 従来方式・既定の公開ツール: `benchmark`、`world_editor`、`game_control`、`navigation_control`、`building_control`、`orders_control`、`server_control` の7つ
- 新しい `2026-07-28` の `tools/list`: 現在は読み取り専用の `benchmark` だけを通知
- 旧来の集約エントリーポイント: 仮想ファイル操作の内部処理としてのみ使用し、MCP ツールとしては登録しない
- `coordinate_control` は現在の公開実行時インターフェースに含まれず、通常の集約ツールは生の座標を拒否する
- ツールの説明: 既定で公開するツールとパラメーターの説明は英語

新しい `2026-07-28` のリクエストでは `initialize` を使わず、`Mcp-Session-Id` の要求や返却も行いません。各リクエストに `_meta`、`MCP-Protocol-Version`、`Mcp-Method` を付けます。`resources/read` / `tools/call` など、具体的なリソース名やツール名があるリクエストには、対応する `Mcp-Name` も必要です。まず `server/discover` を呼び出し、実際に返される capability を基準にすることを推奨します。

従来の `2025-11-25` / `2025-06-18` クライアントは、引き続き最初に `initialize` を呼び出します。その後のリクエストには、ネゴシエーションで得た `Mcp-Session-Id` と `Mcp-Protocol-Version` を付けます。

## 対象の指定と実行の原則

正式なモデル:

- セーブデータはディレクトリとして表されます。`latest/` は、現在または最新のセーブデータを指す固定の別名です。
- `cd latest` でセーブデータに入り、`cd` または `cd ~` でメインメニュー・ルートを表す `/` に戻ります。
- セーブデータの内容は、`map/terrain.oni`、`buildings/plans.oni`、`infrastructure/power.oni`、`views/power.png` などの構造化されたワールドファイルです。
- 操作用のパッチファイルはありません。ワールドの変更には `world_editor command=edit` を使い、原則として SEARCH/REPLACE は1ブロックにします。複数ブロックには外側の `allowPartial=true` が必要で、トランザクションとしてまとめてロールバックすることはできません。
- 編集後の観測は、同じファイルを再度読み取ることで行います。

編集例:

```text
`<<<<<<< SEARCH`
# 観測済みのテキスト、または空の計画テキスト
`=======`
用铜矿连接电池到制氧机
`>>>>>>> REPLACE`
```

例に含まれる中国語の計画文や記号は、ツールに渡す原文の入力例を保持しています。上の計画文は、銅鉱石を使ってバッテリーと酸素散布装置を接続する指示です。

`world_editor` は、ワールドとやり取りする既定のツールです。ロード済みのセーブデータを仮想フォルダーとして扱い、マップのビューをファイルとして公開します。

- `world_editor command=ls path=/`
- `world_editor command=read path=/world/map/text.txt`
- `world_editor command=read path=/world/views/power.png`
- `world_editor command=search domain=buildings query="wire"`
- `world_editor command=plan plan="用铜矿连接电池到制氧机"`
- `world_editor command=connect plan="connect battery to oxygen diffuser"`

検索、計画、操作、建築、作業指示、ナビゲーション、ゲーム操作、複製人間の操作、座標指定へのフォールバックは、`world_editor` を通して処理します。既定の公開インターフェースでは、目的を絞った直接呼び出し用に `benchmark`、`game_control`、`navigation_control`、`building_control`、`orders_control`、`server_control` も引き続き利用できます。それ以外の旧来の集約エントリーポイントは、仮想ファイルの内部操作専用であり、MCP ツールとしては呼び出せません。

新しいツールインターフェースは、検索と操作を優先する設計です。

- `query`、`target`、`search`、`name`、`id`、`areaId` の使用を優先します。
- 専用の検索には `world_editor command=search` または型付きの `/active/ops/search.md` の手順を使います。基盤となる `search_control` は内部操作であり、直接呼び出せる MCP ツールではありません。
- 公開集約ツールは、生の `x/y`、`x1/y1/x2/y2`、`dx/dy`、`points`、`anchors` を受け付けません。正確な位置への作業指示では `/active/ops/tools.md` を読み、`/active/ops/orders.md` を編集します。現在公開されている型付きファイル・ツールだけを使い、非公開の `coordinate_control` と `/active/ops/coordinate.md` の互換用の入り口は使用しません。
- タスクの応答には、できる限り `reachable`、`executable`、失敗理由、不足している条件、推奨される次の行動を含めるものとします。
- 領域を定義して `areaId` を取得する必要がある場合は、`world_editor` の型付き `/active/ops/read.md` の手順で行います。基盤となる `read_control` は内部操作であり、MCP ツールとして直接呼び出すものではありません。
- 書き込み、実行、危険な操作は `dryRun` または `confirm` に対応し、実行後に状態を読み直して検証するものとします。
- 危険な操作や広範囲の正確な位置指定を伴う操作では、一時停止 → 読み取り・計画 → dry-run → 確認 → 検証の順序を必ず守ります。

## 主要ツール

| ツール | 主な domain/action | リスク | 用途 |
|------|--------------------|------|------|
| `benchmark` | `cases`、`iterations`、`tool`、`includeDetails` | read | 固定のツール呼び出し経路のベンチマークと診断。ゲーム状態は変更しない |
| `world_editor` | `cd`、`ls`、`read`、`search`、`edit`、`plan`、`connect` | read/write/execute | 仮想ファイルを通じたワールドへのアクセス、検索、計画、制御された編集 |
| `server_control` | `catalog`、`batch`、`program` | read/execute | 健全性チェック、ツール一覧、ツール検索、目的別ガイド、バッチ呼び出し、Agent Program |
| `game_control` | `speed`、`state`、`save`、`sandbox`、`ui` | read/execute/dangerous | 一時停止、再開、速度変更、セーブデータ、サンドボックス、UI の編集マーク |
| `navigation_control` | `camera`、または既知のカメラ `action` から推定 | execute | カメラ移動、ワールド切り替え、レイヤー表示、フォーカス、スクリーンショット |
| `building_control` | `planning`、`config`、`storage`、`filter`、`production`、`side_surface`、`rocket` | read/write/execute | 建築計画、素材の確認、ブループリント、設備のサイドパネル設定、収納フィルター、生産キュー、ロケット |
| `orders_control` | `area`、`priority`、`designation`、`conduit` | execute/dangerous | 採掘、片付け、モップ掛け、解体、優先度、領域単位の作業指示、配線・配管の切断 |

## 建築計画

`building_control domain=planning` が新しい建築の入り口です。

| action | 用途 |
|--------|------|
| `search_defs` | 建築可能な設備の定義を検索する |
| `materials` | 設備に使える素材と在庫を調べる |
| `preview` | 建築計画の基点1つを事前検証し、実行可能性と必要素材を返す |
| `placement_candidates` | 領域内や対象の近くで配置可能な位置を探す |
| `build_area` | 地図パッチ内部の実装。公開ツールからの直接呼び出しは禁止 |
| `auto_connect` | 旧手順との互換用の明示的な接続処理。新しい手順では通常、個別の呼び出しは不要 |

### 素材に関する応答

建築計画の事前検証と配置の結果には、素材面で実行可能かどうかを返すものとします。従来のフィールド `materialSelection` は互換性のために残し、新しい `materials` をタスク単位の判断に使います。

```json
{
  "materials": {
    "requirementKnown": true,
    "requiredKg": 200.0,
    "selectedAvailableKg": 1240.0,
    "satisfied": true,
    "shortageKg": 0.0,
    "availableMaterials": [
      { "tag": "SandStone", "availableKg": 1240.0 }
    ]
  }
}
```

呼び出し側は、まず `materials.satisfied` を確認します。`false` の場合は、必要素材、利用可能な素材、不足量をユーザーに返し、建築予定の配置に進んではいけません。

### 配線と配管

`build_area` は、次の線状設備の自動接続に対応します。

- `Wire`
- `LogicWire`
- `GasConduit`
- `LiquidConduit`
- `SolidConduit`

`search_defs` の `placement.operationGuide` と配置予行の `operationGuide` は、許可された地図パッチの読取・予行・実行・検証を返します。`parse_plan` の建築テンプレートは施工せず `preview` を返し、実際の施工は `executionPlan.construction` に従います。

一マス設備は空きセルを `Tile:5` などに置き換えます。複数マス設備は左下アンカーの空きセル1つを `Bed:5` などに置き換えるか、占有する矩形全体を同じトークンに置き換えます。設備全体が収まる空間を読取で確認してください。隣接した複数台を一つの矩形と誤認させないよう、原則1台ずつ扱います。素材は現在の凡例の記号を `#` に続けます。読取と編集の `compact=false`・`view=default` を揃え、SEARCHには取得した行をそのまま使います。回転はトークンに埋め込まず、外側の `world_editor command=edit` に `orientation=Neutral/R90/R180/R270` または `rotation` を指定します。設備の許可回転は `building_control domain=geometry action=definition prefabId=...` の `permittedRotations` で確認します。

`config list` の `configurationReads` は、各capabilityについて `stateIncluded`（現在の返答に設定のフィールドが含まれるか）と読取先を返します。これは値が有効・取得済みであることの保証ではなく、温度の `samplePending` などは引き続き個別に確認します。洗面方向は `building_control domain=side_surface surface=option action=list kind=direction query=WashBasin`、スマート電池上下限は `building_control domain=side_surface surface=activation action=list query=BatterySmart` で読みます。返答のInstanceIDを `expectedTargetId` と照合してください。詳細読取先を保証できないcapabilityは `read=null` とし、取得済みのように見せません。

意味ベースの建築には、`plan`、`blueprint`、`areaId`、検索結果、または `/active/ops/build.md` の意味ベースの呼び出しを優先します。正確な位置に配置する場合は、`/active/map/viewport.md` を読み、必要に応じて zoom や `symbols/glyphs.md` の読み取りを行います。次に、マップの Markdown を編集し、対象の空きセルのトークンを `建筑名:优先级` に置き換え、必要に応じて `#材料字` を付けます。これは建築名・優先度・素材の記号を指定する構文です。マップ経由の処理では、これらのトークンを内部の `building_control build_area` の anchors に変換します。`/active/ops/build.md` は生の座標を受け付けません。

使用例:

原則として SEARCH/REPLACE は1ブロックにします。複数ブロックには外側の `allowPartial=true` が必要で、トランザクションとしてまとめてロールバックできません。操作ファイルの各置換には、実行可能なコマンドを必ず1つだけ含めます。プレビューでは外側の `world_editor edit` に `dryRun=true` と `confirm=false`（または省略）を指定します。実行時には新しい edit を作成し、外側に `dryRun=false` と `confirm=true` を指定して、コマンドのフラグが矛盾しないようにします。その後、マップや状態を読み直します。

これにより連続した配線・配管を直接作成でき、後から別途接続する手順は不要です。

## 作業指示と切断

`orders_control` は意味ベースの対象指定と領域ハンドルに対応します。正確な矩形範囲には、まず `/active/ops/tools.md` を読み、非公開の座標互換用の入り口を使わずに、`/active/ops/orders.md` を編集します。例: `挖 x1=10 y1=20 x2=18 y2=20 priority=7 dryRun=true`。プレビューでは外側に `dryRun=true` を指定し、確認は付けません。実行は、新しい edit で外側に `dryRun=false` と `confirm=true` を指定し、コマンドのフラグに矛盾がない場合にのみ行います。

```json
{
  "domain": "area",
  "action": "dig",
  "areaId": "a1",
  "confirm": true
}
```

配管や配線を切断する場合:

- `cut_conduits type=auto` には、既定で気体パイプ、液体パイプ、コンベアレール、電線、自動化ワイヤーが含まれます。
- `type=wire` は電線だけを切断します。
- `type=logic` は自動化ワイヤーだけを切断します。
- `type=all` は、より広い対象範囲が明確に必要な場合に使います。

## カメラとビュー

`navigation_control` は、カメラ、レイヤー表示、スクリーンショット専用です。次の操作に対応しています。

- `get_view`: 現在のカメラ位置、ズーム、アクティブなワールドを読み取る。
- `set_active_world`: アクティブなワールドを切り替え、カメラを移動する。
- `set_view` / `move`: カメラを設定、または平行移動する。
- `switch_view`: 酸素、電力、配管、温度などのレイヤー表示を切り替える。必要に応じてスクリーンショットも取得する。
- `focus_cell` / `focus_dupe`: セルや複製人間にフォーカスする。
- `screenshot` / `coordinate_screenshot`: 通常のスクリーンショット、または座標グリッド付きの領域スクリーンショットを保存する。

建築や作業指示には、`building_control` と `orders_control` を直接使います。各ツール呼び出しに必須の `task` テキストはプレイヤーのマウス付近に自動表示されるため、別途表示位置を指定する必要はありません。

## よく使うリソース

| URI | 説明 |
|-----|------|
| `oni://colony/status` | サイクル、複製人間の人数、速度、一時停止の状態 |
| `oni://colony/diagnostics` | 取得範囲を明示したMCP独自チェック。本体HUD通知・日次酸素収支・過熱診断は取得しない |
| `oni://colony/alerts` | MCP独自チェックの既存配列。空配列でもHUD警告なしとは判断しない。本体通知は `oni://colony/notifications`、日報は `oni://colony/report` で別取得 |
| `oni://colony/summary` | 行動計画用のコロニー概要 |
| `oni://resources/inventory` | 資源の在庫 |
| `oni://resources/food` | 食料の在庫と保存状態 |
| `oni://dupes` | 複製人間の一覧 |
| `oni://dupes/status-check` | 複製人間の位置、作業、欲求、閉じ込めの疑い |
| `oni://power/summary` | 電力網の概要とバッテリーの状態 |
| `oni://power/ports` | 電力ポートのセル、基点、接続点、ポートに電線があるかどうか |
| `oni://rooms/list` | 既存の部屋の判定・広さ・設備。`includeCriteria=true` で現在の部屋の条件文、`includeNeutral=true` で多目的室も取得 |
| `oni://rooms/types` | 未成立も含む部屋種別の定義。ゲームの言語で成立条件文と効果を取得 |
| `oni://thermal/overheat-risk` | 設備の過熱リスク |
| `oni://world/text-map` | テキストマップ |
| `oni://buildings/defs` | 建築可能な設備の定義 |
| `oni://tools/manifest` | ツール一覧 |
| `oni://tools/guide` | 目的に応じたツールの呼び出し手順 |
| `oni://guide/mechanics` | ゲームの仕組み、計算式、境界条件の早見表 |

## ソースコードの構成

```text
mods/OniMcp/
├── ModInfo.cs           # KMod のエントリーポイント
├── Config/              # オプション
├── Core/                # MCP プロトコルの型
├── Localization/        # STRINGS
├── Patches/             # Harmony パッチとゲーム操作方針
├── UI/                  # 実行時のオーバーレイ（座標グリッド、吹き出し）
├── Server/              # HTTP/MCP サーバー
├── Support/             # ログ、パス、リフレクション
└── Tools/
    ├── Core/            # ツールとリソースの登録
    ├── Entry/           # 集約エントリーポイント（*Control* / Read / 英語の説明）
    ├── WorldEditor/     # 仮想ワールドのファイルシステム
    ├── Shared/          # 共通の補助処理
    └── Impl/            # 各ドメインの実装（Build/Dupes/World/...）
```

新しい公開機能は、まず `Tools/Entry/` の集約エントリーポイントの拡張で対応します。ドメインの実装は `Tools/Impl/<Domain>/` に置きます。検索、到達可能性、素材確認の共通処理は `Tools/Shared/` に置き、正確な位置を扱う操作は `/active/ops/` 配下の型付きファイルを通して処理します。

## 互換性について

新しいクライアントは、次のように2世代のプロトコルへ対応することを推奨します。

1. まず `2026-07-28` で `server/discover` を呼び出し、返された capability を使います。新方式のリクエストでは `Mcp-Session-Id` を送信しません。
2. 接続先のサーバーが旧プロトコルにしか対応していない場合は、`initialize` + `Mcp-Session-Id` を使う `2025-11-25` / `2025-06-18` の経路に切り替えます。
3. 古い細分化されたツール一覧をハードコードしません。使用中のプロトコルに従って `tools/list` を呼び出すか、実行時の manifest を読み取ります。
4. `server_control domain=catalog action=search` を呼び出すか、`oni://tools/guide` を読み、目的の手順を探します。
5. 意味ベースの対象指定パラメーターを優先します。
6. 危険な操作には `confirm: true` を指定します。
7. 実行後はリソースや領域のスナップショットを読み取り、状態を検証します。

## 部屋の成立条件と効果を調べる

コロニーをロードした状態で `oni://rooms/types?type=GreatHall` を読み取ると、ゲームに読み込まれている大広間の定義を取得できます。`type` と `query` は id/名前の部分一致（大文字小文字を区別しない）、`limit` は既定80・最大300です。多目的室も既定で含み、`includeNeutral=false` で除外します。ワールドの空洞数や、現在その部屋が成立しているかには依存しません。

各 `roomTypes` 行には `id`、`name`、`categoryId`、`categoryName`、`description`、`criteria`（成立条件文）、`effectText`（効果の説明）、`effect`、`effects`、`priority` を返します。条件と効果はゲームのネイティブな `RoomType.GetCriteriaString()` / `GetRoomEffectsString()` から取得し、独自の条件表は使いません。MODが追加した部屋種別も読み込み済み定義に含まれていれば対象です。

操作ファイルでは `/active/ops/read.md` に次の読み取りを記述できます。

```text
call domain=infrastructure action=room_types type=GreatHall limit=10
```

これは部屋の条件図鑑です。特定の空洞に不足する条件や、扉を置く場所を診断するものではありません。現在の部屋は引き続き `oni://rooms/list?includeBuildings=true&includeCriteria=true&includeNeutral=true` で照合してください。メニュー画面ではデータベースを初期化せずエラーを返します。


## データベースの本文を調べる

`world_editor action=search domain=database` で読み込み済みCodexの項目を検索し、`/active/ops/read.md` から `call domain=database action=query id=OXYGEN limit=1 maxChars=4000 detail=true` で本文を取得する。キャッシュを初期化せず、DLC・アンロック制限を守る。レシピ等の専用パネルは未対応として返すため、`content.complete` と `unsupportedWidgets` を確認する。詳細と例は[Codex本文取得](codex-read_JP.md)を参照。
