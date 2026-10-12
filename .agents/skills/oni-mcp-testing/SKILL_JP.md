---
name: oni-mcp-testing
description: ONI MCP サーバーの機能・接続状態・ツールの利用可否についてテスト、検証、監査を求められたときや、MCP の操作感・使いやすさを評価するときに使用する。接続、情報取得、マップ、建築、カメラ、作業指示などの主要機能を系統的にテストし、テストレポートを作成する。
---

# ONI MCP 機能テスト

## 使用する場面

- 「MCP をテストして」「MCP が使いやすいか確認して」「接続を確認して」と頼まれたとき
- ONI MCP サーバーの状態、ツールで対応できる範囲、操作感の監査を求められたとき
- 新しい環境やセーブデータへの初回接続後、MCP が正常に動作するか手早く確認するとき
- MCP ツールの呼び出し失敗、異常なデータ、想定と異なる挙動を調査するとき

## テストの原則

- **読み取りを優先する**: 最初の2段階では読み取り専用ツールだけを使い、ゲームの状態を変更しない。
- **先に dryRun を行う**: 書き込み・実行操作はすべて、まず dryRun で問題がないことを確認してから実行する。
- **影響を最小限にする**: 実際に建築・採掘を試す場合は、影響が最も小さい場所と規模を選ぶ。
- **重点を決める**: 基本的なツールのテストは適度に絞り、「マップの分かりやすさ」と「計画の立てやすさ」を重点的に評価する。

## テスト手順

### 第1段階: 接続と基本状態の確認（読み取り専用）

サーバーへの接続と、ゲームの基本状態を確認する。

```
server_control domain=diagnostics action=status → サーバーの読み込み状態、ポート、ツール数
game_control domain=speed action=time                  → ゲーム内時刻、サイクル、一時停止の状態
colony_control domain=snapshot action=get      → コロニー全体のスナップショット（6回の個別呼び出しを置き換える）
```

確認項目:

- [ ] `server_control domain=diagnostics action=status` が返す `loaded` が true である。
- [ ] `toolCount` が主要な公開ツールの数を示している。通常は 8。
- [ ] `game_control domain=speed action=time` が有効なサイクルと時刻を返す。
- [ ] `colony_control domain=snapshot action=get` が複製人間、食料、設備、研究、警告の情報を返す。

### 第2段階: 重要な情報の取得（読み取り専用・簡略版）

計画の判断に特に重要な情報取得ツールを並列にテストする。重要度の低い照会は省略する。

```
read_control domain=infrastructure action=power_summary includeDetails=true   → 電力回路に未接続の設備を重点的に確認
dupes_control domain=info action=status_check → 複製人間の到達可能性、閉じ込めのリスク
building_control domain=planning action=search_defs query=厕所     → 中国語での検索と素材の利用可否
```

確認項目:

- [ ] `read_control domain=infrastructure action=power_summary` で未接続の設備（例: circuitId=-1）を検出できれば加点する。
- [ ] `dupes_control domain=info action=status_check` で閉じ込めのリスクがなく、移動経路のサンプルが妥当である。
- [ ] `building_control domain=planning action=search_defs` で中国語のキーワードが使え、`placement.anchor` の規則が返される。

### 第3段階: マップの分かりやすさ（読み取り専用・重点項目）

テストの中心となる段階。マップの出力が、実際に「読み取れる・計画を立てられる・判断に使える」ものかを重点的に確認する。

```
read_control domain=world action=text_map
  view=base, includeBuildings=true, includeItems=true, format=markdown
  → 連続区間で表すマップの読みやすさ、凡例の網羅性、対象の表示位置の正確さを確認

read_control domain=world action=text_map
  view=temperature, format=markdown
  → 温度ビューで hot/mild/cold の領域をすばやく識別できるか確認

read_control domain=world action=area_snapshot
  preset=utilities, overlays=[power,gas_conduits,liquid_conduits,logic]
  compact=true
  → 複数のオーバーレイの情報密度、コンパクトモードの実用性、競合の診断結果を確認
```

確認項目（マップの読みやすさ）:

- [ ] 連続区間で表すマップがセル単位のグリッドより読みやすく、行と列の座標が明確である。
- [ ] 凡例に `bld`/`dup`/`itm`/`tile`/`sol`/`bp`/`bp_a` など、すべてのマーカーが含まれる。
- [ ] オブジェクト一覧に `anchor`/`footprint`/`size` が含まれ、複数セルにまたがる設備の境界が明確である。
- [ ] 温度ビューが区間ごとに `hot`/`mild`/`cold` を示し、広い範囲で区別がつかなくなることがない。
- [ ] `compact=true` ではオブジェクト表の空欄が省略され、重要なデータを失わずに情報密度が高まる。

確認項目（診断への有用性）:

- [ ] `unsupported` の設備を自動検出できる。支えとなるセルが不足している座標も表示される。
- [ ] `overlap` の競合を自動検出できる。tile@x,y や building:Door@x,y など、具体的な競合対象も表示される。
- [ ] 電力オーバーレイが `w`（電線）/ `p`（電力設備）/ `.`（空き）を正しく区別する。
- [ ] 自動化オーバーレイが空の場合は、冗長な表示をせず簡潔に出力される。

### 第4段階: 計画の立てやすさ（dryRun と分析・重点項目）

MCP が、コロニーの計画を立てる際に必要な判断の負担を効果的に減らせるかを確認する。

```
read_control domain=world action=layout_candidates purpose=barracks limit=5
  → 評価点、分類、必要な採掘量・タイル数、危険なセル、到達可能性

read_control domain=world action=layout_candidates purpose=bathroom limit=5
  → 用途に応じて異なる候補が推薦されるか確認

building_control domain=planning action=preview prefabId=Outhouse x=... y=... dryRun=true
  → 直接指定した座標が拒否され、操作ファイル経由の方法が案内されること

world_editor command=read path=/active/ops/tools.md
  → 現在公開されている型付きファイル・ツールだけを選ぶ。非公開の coordinate_control と /active/ops/coordinate.md は使わない

world_editor command=read path=/active/map/viewport.md
  → 対象の空きセルのトークンを `建筑名:优先级[#材料字]`（建築名:優先度[#素材記号]）に変更し、外側の dryRun=true/confirm=false でプレビュー
```

確認項目（配置候補の計画）:

- [ ] `read_control domain=world action=layout_candidates` が `score` と `scoreExplanation` を返し、減点の理由が分かる。
- [ ] 候補の分類（`open_ready`/`mixed_platform` など）が、すばやい判断に役立つ。
- [ ] `requiredDig`/`requiredTiles`/`existingSupportCells` によって、改造に必要な作業量が数値で分かる。
- [ ] `hazardCells` に液体や危険な元素の座標が示され、状況を確認せずに採掘することを避けられる。

確認項目（建築の事前チェックにかかる手間）:

- [ ] 通常の `building_control` で x/y を直接指定すると、明確な拒否と操作ファイル経由の案内が返される。
- [ ] マップのトークン編集の dry-run が、支え、障害物、占有範囲（footprint）の事前チェック結果を返す。
- [ ] 原則として SEARCH/REPLACE ブロックは1つ。複数のブロックは外側の `allowPartial=true` がある場合にのみ許可され、トランザクションとしてまとめてロールバックできない。
- [ ] 操作ファイルの各 replacement に、実行可能なコマンドがちょうど1つ含まれる。
- [ ] `material=auto` が、現在の小惑星で利用できる素材を自動で選ぶ。

### 第5段階: 一括操作と領域単位の計画（dryRun または小規模な実行）

広い範囲の計画を支える機能をテストする。

```
read_control domain=area action=define x1=... y1=... x2=... y2=... label=plan_zone
read_control domain=area action=blocks blockWidth=20 blockHeight=20 label=plan_blocks
read_control domain=area action=merge areaIds=[blk1,blk2]
  → 領域の定義、ブロックへの分割、結合が一貫して使えるか確認

building_control domain=planning action=build_area prefabId=Tile anchors=[[x1,y1],[x2,y2],[x3,y3]] dryRun=true
  → 通常のツールが anchor の直接指定を拒否することを確認

world_editor command=read path=/active/ops/orders.md
  → `挖 x1=... y1=... x2=... y2=... priority=7 dryRun=true` で、正確な矩形を指定する dry-run の経路を確認

orders_control domain=area action=dig areaId=... dryRun=true detail=true
orders_control domain=area action=sweep areaId=... dryRun=true detail=true
  → 領域単位の作業指示をプレビューし、リスクの検出と previewToken の再利用を確認
```

確認項目:

- [ ] `read_control domain=area action=define` / `read_control domain=area action=blocks` / `read_control domain=area action=merge` で生成した areaId を、後続のツールで再利用できる。
- [ ] 通常の `building_control` が anchor の直接指定を拒否し、一部の建築予定だけが残ることがない。
- [ ] dry-run 用の外側の edit は `dryRun=true` と `confirm=false`（または confirm を省略）を使う。別途実行する edit は `dryRun=false` と `confirm=true` を使い、内側と外側のフラグが矛盾しない。
- [ ] `orders_control domain=area action=dig` が対象の元素、質量、温度、リスクの一覧を返す。
- [ ] `previewToken` を再利用でき、同じ走査を繰り返さずに済む。

### 第6段階: カメラとビューの操作（簡略版）

少数の呼び出しで、利用可能であることを確認する。

```
navigation_control action=get_view
navigation_control action=focus_cell x=... y=...
navigation_control action=switch_view view=power screenshot=false
navigation_control action=coordinate_screenshot areaId=... focusCamera=true
```

確認項目:

- [ ] カメラの状態取得、セルへのフォーカス、オーバーレイの切り替え、座標付きスクリーンショットが正常に動作する。

### 第7段階: 一括呼び出しとゲーム操作（任意）

```
server_control domain=batch action=call_many responseMode=summary
  items:
    - { t: navigation_control, a: { action: get_view } }
    - { t: building_control, a: { domain: planning, action: search_defs, query: 梯子, limit: 3 } }
```

確認項目:

- [ ] 一括呼び出しが成功し、`requireAllValid=true` が正しく動作する。

### 第8段階: 実際の建築による検証（任意）

第1〜7段階がすべて通った場合は、任意で小規模な建築を1回実行して検証する。

```
world_editor command=read path=/active/map/viewport.md
# 対象の空きセルのトークンを Ladder:5 に置き換える。外側は dryRun=true、confirm=false または confirm を省略する
# プレビューを確認してから、外側を dryRun=false、confirm=true にした新しい edit を実行する
read_control domain=world action=text_map x1=... y1=... x2=... y2=...
  → 直線状の建築予定を出した後、マップに bp マーカーが正しく表示されるか確認
```

規則:

- 梯子など、影響がごく小さいものだけを建築する。
- マップのトークン編集の dry-run で、問題がないと確認済みの場所を優先する。
- 建築操作後は、必ずマップの `bp` / `bp_a` マーカーを確認する。
- ユーザーが明示的に求めていなければ、この段階は省略してよい。

## テストレポートのテンプレート

各テスト後に、「マップの分かりやすさ」と「計画の立てやすさ」を重点的に評価した簡潔なレポートを返す。

```
## ONI MCP テストレポート

### 接続状態
| 項目 | 結果 |
|------|------|
| サーバーの読み込み | ✅/❌ |
| ツール数 | N 個 |
| ゲームの状態 | 第 X サイクル、一時停止中/実行中 |

### 機能テストの結果
| 分類 | 状態 | 備考 |
|------|------|------|
| 基本的な情報取得 | ✅/❌ | |
| マップの読みやすさ | ✅/❌ | |
| マップの診断への有用性 | ✅/❌ | unsupported/overlap/overlay |
| 配置候補の質 | ✅/❌ | read_control domain=world action=layout_candidates の評価点と分類 |
| 建築の事前チェックにかかる手間 | ✅/❌ | 複数セル・支え・障害物の検出 |
| 一括操作と領域 | ✅/❌ | 原子性、areaId の再利用 |
| カメラ/ビュー | ✅/❌ | |
| 実際の建築 | ✅/❌/省略 | |

### 見つかった問題
- （あれば記載）

### 総合評価
| 評価項目 | 点数 | 説明 |
|------|------|------|
| 使いやすさ | ⭐x/5 | |
| 安全性 | ⭐x/5 | |
| マップの読みやすさ | ⭐x/5 | 連続区間、凡例、オーバーレイの分かりやすさ |
| 計画の立てやすさ | ⭐x/5 | 候補の評価点、事前チェックの診断、一括操作の原子性 |
```

## よくある問題の調べ方

| 現象 | 考えられる原因 | 確認手順 |
|------|---------|---------|
| `server_control domain=diagnostics action=status` が `loaded=false` を返す | ゲームが起動していない、または MOD が読み込まれていない | ゲームの起動と MOD の有効化を確認する |
| ツールの呼び出しが `not found` を返す | ツール名の綴りが誤っている、または主要ツールの一覧に含まれない | `server_control domain=catalog action=search` で正しいツール名を探す |
| 通常の `building_control preview` に x/y を直接渡している | 現在の公開ランタイムは座標の直接指定を拒否する | 拒否されれば想定どおりの成功とし、`/active/map/viewport.md` のトークン編集の dry-run をテストする |
| 通常の作業指示ツールが座標の直接指定を拒否する | 現在の公開ランタイムは操作ファイル経由の実行を要求する | `/active/ops/tools.md` を読み、非公開の coordinate の入口は使わず、`/active/ops/orders.md` を使う |
| `colony_control domain=snapshot action=get` の警告が更新されない | 設備がまだ建築予定の段階で、完成していない | 建築予定は完成済みの設備とは異なる。複製人間の建築完了を待って読み直す |
| 中国語での検索に結果が出ない | 検索語がローカライズデータに含まれない | 英語のキーワードや `category` による絞り込みを試す |
| `orders_control domain=area action=dig` のリスク表示が不十分 | 明らかなリスクだけを検出しており、網羅性は保証されない | `read_control domain=world action=text_map` も使い、隣接する液体・高温・真空を自分で確認する |
| `read_control domain=world action=layout_candidates` の評価点が低い | 危険なセルが多い、または大量の採掘が必要な領域である | `hazardCells` と `requiredDig` を確認し、purpose を変えるか対象範囲を広げる |

## 手早くテストする手順（短縮版）

時間が限られている場合は、主要な一連の操作だけを確認する。

```
server_control domain=diagnostics action=status
game_control domain=speed action=time
colony_control domain=snapshot action=get profile=brief
read_control domain=world action=text_map x1=... y1=... x2=... y2=... format=markdown
read_control domain=world action=layout_candidates purpose=barracks limit=3
building_control domain=planning action=preview prefabId=Outhouse x=... y=... dryRun=true  # 座標の直接指定が拒否されること
world_editor command=read path=/active/ops/tools.md
# 現在公開されている型付きファイル・ツールだけを使う。非公開の coordinate_control と /active/ops/coordinate.md は使わない
world_editor command=read path=/active/map/viewport.md
```

8回の呼び出しで、接続、情報取得、マップの読みやすさ、配置候補、座標の直接指定の拒否、マップ編集の経路を確認できる。
