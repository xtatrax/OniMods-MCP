---
name: oni-mcp-opening-setup
description: Oxygen Not Included で新しいコロニーを開始するとき、またはユーザーが開始時の設定や初期構成（bootstrap）を求めたときに使用する。検討前の一時停止、時間をずらしたスケジュール、能力に応じた複製人間の改名と命名スタイルのユーザー確認、自動消毒の無効化、開始する小惑星の調査、序盤の左右への拡張と製造ポッド付近の研究室の計画を扱う。
---

# ONI MCP 開始時の設定

## 使用する場面

新規ゲームや序盤のサイクルで、次の設定を行うときに使用する。

- 開始時の設定
- 開始時の初期構成（bootstrap）
- スケジュール設定
- 能力に応じた複製人間の改名
- 開始する小惑星の概要確認
- 製造ポッド付近の最初の採掘・土台・研究室の計画

## 必須ルール

分析や計画の前に、まずゲームを一時停止する。

```
game_control domain=speed action=pause
```

状態の読み取り、計画、質問、設定コマンドの発行中は一時停止を維持する。計画が完了し、次のいずれかを満たした場合に限り再開する。

- ユーザーが明示的に続行を求めている
- 現在の依頼に「設定後に再開する」ことが明確に含まれている

ユーザーが検討、確認、計画、設定を求めた場合は、まず一時停止する。

## ユーザーに必ず確認すること

複製人間を改名する前に、命名スタイルをユーザーに尋ねる。「複製人間の一括改名」だけの依頼には、改名用の簡易手順を使う。開始時の一式のスナップショット取得、スケジュール設定、消毒設定、周辺調査は行わない。

確認は短く、1回だけ行う。例:

- `役割の接頭辞`: `Dig-Ada`, `Build-Meep`
- `日本語の役割名`: `採掘-エイダ`, `建築-ミープ`
- `短いラベル`: `Digger`, `Builder`, `Cook`
- ユーザー独自のスタイル

ユーザーの回答を得るまで、`dupes_control domain=command action=auto_rename apply=true` は実行しない。プレビューは行ってよい。

```
dupes_control domain=command action=auto_rename style=<candidate> apply=false
```

## ツール呼び出しのルール

- 同じ読み取り専用ツールを、同じ引数で連続して2回呼び出さない。2回目の前に不足している情報を説明し、より適切なツールに切り替えるか、作業を止めてユーザーに確認する。
- `dupes_control domain=info action=status_check` を名簿、能力値、改名のツールの代わりに使わない。これは健康状態や到達可能性に関する緊急度の判断にのみ使用する。
- 現在のツール一覧に目的のツールが見当たらない場合は、先に `server_control domain=catalog action=search query=<tool or action>` または `server_control domain=catalog action=manifest group=<group>` でスキーマを確認してから呼び出す。古いツール名を推測して使わない。
- 単純な設定作業では、その作業に必要な情報だけを読み取る。改名のためにマップ、スクリーンショット、食料、電力、環境を読み取らない。

## 開始時の手順

### 1. 一時停止とスナップショット

```
game_control domain=speed action=pause
colony_control domain=snapshot action=get profile=standard includeAtmosphere=false
colony_control domain=read action=worlds
navigation_control action=get_view
```

詳細が不足している場合を除き、`colony_control domain=snapshot action=get` を使う。status/dupe/food/research を個別に呼び出す形には分けない。

### 2. 時間をずらしたスケジュールの設定

現在のスケジュールを読み取る。

```
colony_control domain=management kind=schedule action=list
```

時間をずらしたシフトをプレビューする。

```
colony_control domain=management kind=schedule action=optimize apply=false
```

依頼が明確に設定・構成作業である場合、またはユーザーの確認を得た場合に限り適用する。

```
colony_control domain=management kind=schedule action=optimize apply=true prefix="AIシフト"
colony_control domain=management kind=schedule action=list
```

序盤から利用時間を分散し、トイレ、ベッド、娯楽設備の混雑を減らすことが目的。ユーザーが具体的な数を指定しない限り、シフト数は既定の自動設定を使う。

### 3. 能力に応じた複製人間の改名

一括改名だけの依頼には、次の簡易手順を使う。

```
game_control domain=speed action=pause
# 命名スタイルが未選択なら、まず1回だけ尋ね、作業を止めて回答を待つ。
dupes_control domain=command action=auto_rename style=<user style> apply=false
# ユーザーがプレビューを確認した後:
dupes_control domain=command action=auto_rename style=<user style> apply=true
colony_control domain=read action=dupes
```

通常は、先に `colony_control domain=read action=dupes`、`dupes_control domain=info action=attributes`、`dupes_control domain=skill action=list` を呼び出さない。`dupes_control domain=command action=auto_rename apply=false` でプレビューを取得できるため。次の場合に限り、関連情報を追加で読み取る。

- ユーザーが各役割の判断理由を求めている。
- プレビュー結果が明らかに不自然、または複製人間が抜けている。
- ユーザーが一部の複製人間の名前を個別に指定したいと求めている。

役割の説明や個別の命名が必要な場合は、判断に十分な複製人間の情報を読み取る。

```
colony_control domain=read action=dupes
dupes_control domain=info action=attributes
dupes_control domain=skill action=list
```

能力値や興味から役割を判断する。

- 採掘・建築: excavation、construction、strength
- 研究・操作: science、machinery
- 農業・牧畜・料理: agriculture、ranching、cuisine
- 供給・片付け: athletics、strength、専門分野への特化が弱いこと

適用前に命名スタイルを確認してから、次を使う。

```
dupes_control domain=command action=auto_rename style=<user style> apply=false
dupes_control domain=command action=auto_rename style=<user style> apply=true
```

または、名前を明示して改名する。

```
# 先に server_control domain=catalog action=search で利用可能なツール名を確認する。現在よく使われる名前は dupes_control domain=command action=rename または rename_dupe。
dupes_control domain=command action=rename id=<dupeId> newName=<name>
```

`colony_control domain=read action=dupes` で結果を確認する。

### 4. 自動消毒の無効化

開始時に広範囲の消毒命令を出さない。

オブジェクトごとにユーザーメニューを操作せず、全体の方針を設定するツールを使う。

```
colony_control domain=diagnostic action=set_auto_disinfect disabled=true applyNow=true confirm=true
```

開始時の設定では、`AutoDisinfectable` オブジェクトを順にたどって `building_control domain=side_surface surface=user_menu action=batch` を呼び出すことは絶対にしない。目的は、自動消毒を全体で無効にし、既存のオブジェクトも新たに発見するオブジェクトも無効のままにすること。

### 5. 開始地点の調査と小惑星の概要確認

現在のカメラ範囲や製造ポッド付近のマップ情報を取得する。

```
read_control domain=world action=area_snapshot preset=utilities encoding=plain includeScreenshot=false
```

範囲が狭すぎる、または製造ポッドが見えない場合は、確認できた開始地点の座標を中心に、40x30 から 60x40 セル程度まで範囲を広げる。その際もセル数は `maxCells` 未満に保つ。

次の点をユーザーに要約する。

- 現在アクティブなワールドと小惑星の種類
- 開始地点のバイオームを判断する手がかり
- 近くの固体・液体・気体
- 差し迫った危険
- 食料・植物・資源の手がかり
- 拡張できる方向
- 序盤の酸素、水、温度に緊急の問題があるか

座標に関する判断にはテキストマップを使い、スクリーンショットは使わない。

### 6. 最初の建築・拡張計画

開始時の基本的な提案:

- 製造ポッドから左右へ拡張する。
- 両側に、小規模で慎重な矩形の採掘範囲を指定する。液体や危険な空洞を避ける。
- 土台、足場、タイル、梯子を正確に配置するには、まず `/active/map/viewport.md` を読む。必要なら zoom を使うか `symbols/glyphs.md` を読み、その後、対象の空きセルのトークンを `建筑名:优先级` に変更する。必要に応じて `#材料字` を付ける。
- 製造ポッド付近の空間を、序盤の研究室用に確保する。
- 研究・電力設備は、先に床や支えを用意してから、人力発電機、バッテリー、研究端末、接続用の電線を配置する。

最初の作業は小規模にとどめる。通常の第1サイクルの作業では、まずコンパクトなスナップショットを1回取得し、`building_control` / `orders_control` の意味を指定する操作で実行する。小さな資源区画を採掘したり短い足場を置いたりするためだけに、正式な計画を作成しない。

配置:

```
building_control domain=planning action=placement_candidates prefabId=<PrefabId> areaId=<area> limit=8
world_editor command=read path=/active/map/viewport.md
# マップの Markdown を編集する。空きセルのトークンを 建筑名:优先级[#材料字] に置き換える、単一の SEARCH/REPLACE ブロックを優先する。
# プレビュー: 外側を dryRun=true、confirm=false または省略にする。
# 実行: 外側を dryRun=false、confirm=true にした新しい編集を行い、その後マップを読み直す。
```

通常の集約ツールは座標の直接指定（raw coordinates）を受け付けない。`points`/`anchors`/`x`/`y` を直接渡さない。`/active/ops/build.md` は、座標を直接指定しない、意味を指定する plan/auto_connect にのみ使う。正確な経路の指定には、編集可能なマップトークンを使う。複数のブロックは外側に `allowPartial=true` を指定した場合にのみ許可され、一括で元に戻すトランザクションのロールバックはできない。

採掘:

```
orders_control domain=area action=dig confirm=true ...
```

`orders_control domain=designation action=attack` を採掘に使うことは絶対にしない。

### 7. 簡易手順と正式な計画

単純で低リスクな開始時の操作には、通常、次の簡易手順を使う。

```
server_control domain=batch action=call_many dryRun=true responseMode=summary requireAllValid=true stopOnError=true items=[...]
server_control domain=batch action=call_many dryRun=false responseMode=summary requireAllValid=true stopOnError=true items=[...]
```

対象は、スケジュール設定、全体の自動消毒設定、小規模で安全な矩形の採掘範囲、短い支えや床の列、および dry-run を通過した最初の研究室・電力設備の設計。

開始時の計画が広範囲、複数段階、危険を伴う、ユーザーの計画に基づく、または後で再開する必要がある場合に限り、`plan_harness` を使う。

```
plan_harness_create objective="Opening setup and Cycle 1 expansion" riskTolerance=low requireVerification=true
plan_harness_record stage=plan summary="Opening setup plan" payload={...}
plan_harness_validate id=<planId>
```

操作を実行した場合は、`colony_control domain=snapshot action=get profile=brief` と、`read_control domain=world action=area_snapshot preset=construction encoding=plain` などの読み取り可能なマップで結果を検証する。harness の検証記録は、harness が存在する場合にのみ残す。

## 実行方針

ユーザーが開始時の設定・構成を求めた場合、このスキルでは次の設定操作を実行してよい。

- `game_control domain=speed action=pause`
- `colony_control domain=management kind=schedule action=optimize apply=true`
- ユーザーが命名スタイルを回答した後に限り、`dupes_control domain=command action=auto_rename apply=true`
- `colony_control domain=diagnostic action=set_auto_disinfect disabled=true applyNow=true confirm=true`
- `orders_control domain=area action=dig`
- `building_control domain=planning action=placement_candidates` と、`/active/map/viewport.md` のトークン編集

設備を配置する前には、引き続き prefab、素材、支えを確認する。

計画・設定が完了し、ユーザーが続行を許可するまで、ゲームを再開しない。

## 最終回答の形式

次の点を簡潔に伝える。

- 一時停止の状態
- スケジュール設定の結果
- 改名の状況、または命名スタイルについて未確認の質問
- 自動消毒の設定結果
- 小惑星の概要
- 拡張・研究室の計画の要約
- ゲームを一時停止のままにしているか、明示的な依頼に従って再開したか
