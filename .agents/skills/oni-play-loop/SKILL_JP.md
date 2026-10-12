---
name: oni-mcp-play-loop
description: ユーザーが MCP を通じた Oxygen Not Included の反復プレイ、一定時間の自動プレイ、コロニー運営の続行、または一時停止・計画・再開を繰り返すループを求めたときに使用する。一時停止、観察、計画、実行、短時間の再開、一時停止、検証の順序を厳守し、game_control domain=ui uiDomain=edit_mark action=list を読み取り、ゲームを進める時間を制限する。リスクや曖昧さのある判断の前には作業を止め、ユーザーの確認を待つ。
---

# ONI MCP プレイループ

## 目的

短時間で制御されたループを繰り返して ONI を進める。

```
pause -> observe -> plan -> execute -> resume briefly -> pause -> verify -> report/next loop
```

エージェントは、ゲームの進行中に検討、計画、新たなコマンドの追加を絶対に行わない。

## 必須ルール

- 各ループは `game_control domain=speed action=pause` から始める。
- 状態の読み取り、プレイヤーの計画の読み取り、計画、検証、コマンドの発行中は、一時停止を維持する。
- 現在の計画が十分にまとまり、進捗を観察できる段階になった場合に限り、ゲームを再開する。
- 再開後は、あらかじめ決めた短い時間だけ待ち、直ちに再び一時停止する。
- ループを際限なく連続させない。ユーザーが複数回のループや継続的なプレイを明示的に求めていない限り、1回だけ実行する。
- 高リスクで元に戻せない操作、広範囲の採掘、破壊的なコマンド、戦闘、セーブ・ロード、サンドボックス・デバッグ、新しい複製人間の受け入れ・製造を行う前には、作業を止めてユーザーに確認する。

## ループの手順

### 1. 一時停止

```
game_control domain=speed action=pause
```

既に一時停止している場合は、次へ進む。

### 2. 状況確認

まず、情報をコンパクトにまとめて読み取る。

```
colony_control domain=snapshot action=get profile=brief includeAtmosphere=false
dupes_control domain=info action=status_check radius=8
game_control domain=ui uiDomain=edit_mark action=list limit=5
```

必要な場合に限り、対象を絞った読み取りを追加する。

- `read_control domain=world action=area_snapshot preset=construction|utilities encoding=plain includeScreenshot=false`
- `read_control domain=resources action=inventory limit=30`
- `read_control domain=resources action=food limit=20`
- `read_control domain=infrastructure action=power_summary`
- `read_control domain=infrastructure action=rooms`
- `colony_control domain=bio bioDomain=farming action=list_harvestables`

プレイヤーがゲーム内で計画の依頼を作成している場合は、独自に配置計画を立てる前に `game_control domain=ui uiDomain=edit_mark action=list` を使う。

### 3. 計画

単純で低リスクな維持管理のループには、簡易手順を使う。意図を1〜2文で説明し、利用できる場合は先に dry-run を行う。その後、コンパクトなバッチで実行し、最後に検証する。

```
server_control domain=batch action=call_many dryRun=true responseMode=summary requireAllValid=true stopOnError=true items=[...]
server_control domain=batch action=call_many dryRun=false responseMode=summary requireAllValid=true stopOnError=true items=[...]
```

対象は、小範囲の採掘、短い床、安全な設定変更、収穫・片付け・モップ掛けの命令、および dry-run を通過した配線・配管などの経路。

大きな計画（複数段階のコロニー整備、プレイヤーの編集マークによる依頼、広範囲の採掘、危険な液体・気体・熱への曝露、解体）では、まず実行するツール呼び出し、主要な引数、前提、停止条件を回答に示す。利用できる場合は先に dry-run を行い、その後で実行する。

建築:

```
building_control domain=planning action=placement_candidates prefabId=<PrefabId> areaId=<area> limit=8
world_editor command=read path=/active/map/viewport.md
# 必要なら zoom を使うか symbols/glyphs.md を読み、マップの空きセルのトークンを 建筑名:优先级[#材料字] に置き換える。
# 外側を dryRun=true、confirm=false または省略にしてプレビューする。
# 外側を dryRun=false、confirm=true にした新しい編集で実行し、その後マップを読み直す。
```

採掘:

```
world_editor command=read path=/active/ops/orders.md
# 次のようなコマンドを1つ編集する: 挖 x1=... y1=... x2=... y2=... priority=7 dryRun=true
# 実行は新しい編集でのみ行い、外側を dryRun=false、confirm=true にする。コマンド内のフラグと矛盾させない。
```

`orders_control domain=designation action=attack` を採掘に使うことは絶対にしない。ツール検索の結果が地形の作業に attack を提案しても採用せず、検索や読み取りをやり直す。
通常の集約ツールは座標の直接指定（raw coordinates）を受け付けない。正確な座標への命令には `/active/ops/orders.md`、正確な建築には編集可能なマップトークンを使う。`/active/ops/tools.md` を読む際は、現在公開されている型付きのファイル・ツールだけを選び、非公開の `coordinate_control` と `/active/ops/coordinate.md` は無視する。既定では単一のブロックを使う。操作ファイルでは、各置換に含める実行可能なコマンドを必ず1つだけにする。危険または広範囲の操作では、pause -> read/plan -> dry-run -> confirm -> verify の順序を必ず維持する。

### 4. 一時停止したまま実行

このループに必要で、検証済みかつ対象範囲が明確な呼び出しだけを実行する。

- 小規模な採掘や命令のバッチ
- dry-run を通過した建築
- 安全な設定変更
- 目的に直接関係する収穫や片付け

独立した操作には、`server_control domain=batch action=call_many responseMode=summary requireAllValid=true stopOnError=true` を優先する。誤った前提でコロニーに被害を与えないよう、バッチは小規模に保つ。

### 5. 短時間だけ再開

複製人間の作業を進めるためにだけ、ゲームを再開する。

```
game_control domain=speed action=resume
```

既定の観察時間:

- 通常の建築・採掘: 実時間で8〜15秒
- 緊急救助・窒息への対応: 実時間で3〜6秒
- 長い運搬・建築作業: 実時間で最大20秒

この間は、コマンドを追加しない。

### 6. 一時停止して検証

直ちに一時停止する。

```
game_control domain=speed action=pause
```

その後、コンパクトな読み取りで結果を検証する。

```
colony_control domain=snapshot action=get profile=brief includeAtmosphere=false
dupes_control domain=info action=status_check radius=8
read_control domain=world action=area_snapshot areaId=<area> preset=construction|utilities encoding=plain
```

検証結果を記録する。目的に向けて進展したか、見つかった問題、次のループを続ける必要があるかを示す。

## 停止条件

次の場合はプレイループを停止し、報告する。

- `dupes_control domain=info action=status_check` で、`risk=critical` の複製人間がいる
- 食料、酸素、温度、電力について、新たに critical の警告が出た
- 設計や命令の実行が繰り返し阻まれる
- プレイヤーから新たな指示がある
- 次の操作に広範囲の採掘、戦闘、解体、セーブ・ロード、サンドボックス・デバッグ、または大規模な設計変更が必要
- 不確かな点の判断に、テキストマップに表れていない視覚情報が必要

## ループ内の優先順位

序盤のコロニーでは、次の順に優先する。

1. 複製人間の安全: 身動きが取れない状態、窒息、空腹、危険な温度。
2. トイレと睡眠のための基本設備。
3. 食料と収穫可能なもの。
4. 機械を置く前に、安定した通路と床を確保する。
5. 電力・研究設備は、支えと電線の経路が成立してから整備する。
6. 液体、真空、高温、ヘドロ、または敵対的な動物のいる空洞に通じる穴を開けずに済む場合に限り、拡張する。

## 報告形式

各ループの後に、簡潔な日本語で報告する。

```
ループの結果:
実行したこと:
確認できたこと:
リスク:
次の手順:
状態: 一時停止中 / 再開済み
```

ゲームが現在一時停止しているかを、必ず示す。停止条件によりループを終了した場合は、該当する具体的な条件を説明する。
