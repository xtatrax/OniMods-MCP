---
name: oni-mcp-quick-demo
description: ONI MCP の手早いデモ、機能紹介、capability showcase、MCP ツールの試用を求められたときや、「デモして」「手早く見せて」「showcase」「MCP で何ができるか見たい」と言われたときに使用する。あらかじめ定めた10ステップのデモを実行し、仮想ファイル、ビューの切り替え、複製人間の管理、スケジュール、消毒、スクリーンショット、ツールを使う手順の検索、リアルタイムのワールド分析、建築・採掘の事前チェック、計画機能を紹介する。確認のやり取りを抑えながら、MCP ツールでできることをすばやく分かりやすく示す。
---

# ONI MCP クイックデモ

## 実行の原則

- **すばやく実行する**: 1つずつ確認を求めず、順番にツールを呼び出す。
- **実行しながら説明する**: 各ステップが終わるたびに、何を実演したかを1文でユーザーに伝える。
- **エラーが起きても続ける**: 1つのステップが失敗しても後続のステップを止めず、失敗の原因を記録してデモを続ける。
- **読み取りをまとめる**: 独立した読み取りは `server_control domain=batch action=call_many` にまとめ、やり取りの回数を減らす。
- **原則としてセーブデータに影響を与えない**: 書き込み・実行機能は、`dryRun`、`apply=false`、読み取り専用のプレビューを優先する。改名、スケジュール変更、採掘、建築は、ユーザーが実際の操作を伴うデモを明示的に求めた場合にのみ行う。
- **ツールを検索する**: サーバーの変更に伴い、例に示すツール名が変わることがある。実行前に現在のツール一覧に該当ツールがなければ、`server_control domain=catalog action=search query=<action>` でスキーマを確認する。

## 10ステップのデモ手順

順番に実行する。各ステップが終わったら簡潔に報告し、すぐに次へ進む。

### ステップ1: 仮想ファイルへのアクセス

```
world_editor command=ls path=/active/
```

**説明**: 仮想ファイルを通じて、現在のセーブデータの構成を一覧表示しました。各呼び出しの taskDescription は、プレイヤーのマウスカーソル付近に自動で表示されます。

### ステップ2: ビューの切り替え

```
navigation_control action=switch_view view=temperature screenshot=true
navigation_control action=switch_view view=oxygen screenshot=true
navigation_control action=switch_view view=none
```

**説明**: 温度ビューと酸素ビューにすばやく切り替えてスクリーンショットを取得し、最後に通常のビューへ戻しました。

### ステップ3: 複製人間の管理・改名のプレビュー

まず、まとめて情報を取得する。

```
server_control domain=batch action=call_many responseMode=summary items=[
  { t: colony_control, a: { domain: read, action: dupes } },
  { t: colony_control, a: { domain: management, kind: schedule, action: list } }
]
```

一括改名はプレビューで実演し、実際の名前は変更しない。

```
dupes_control domain=command action=auto_rename style=cn_job apply=false
```

**説明**: 複製人間の一覧を取得し、役割に応じて自動で名前を付ける結果をプレビューしました。セーブデータは変更していません。

### ステップ4: スケジュールの設定

ステップ3で取得した `colony_control domain=management kind=schedule action=list` の結果を使い、スケジュール変更の事前チェックを実演する。

```
colony_control domain=management kind=schedule action=set_block schedule=<スケジュール名> hour=0 group=Sleep dryRun=true
```

**説明**: 最初のスケジュールの最初の時間帯を睡眠に設定する事前チェックで、スケジュール編集機能を実演しました。変更は適用していません。

ユーザーが実際の操作を伴うデモを求めた場合は、`colony_control domain=management kind=schedule action=optimize apply=false` で時間をずらした交代制のスケジュールをプレビューし、確認後にのみ `apply=true` を使う。

### ステップ5: 自動消毒の方針を設定する機能

```
server_control domain=catalog action=search query="auto disinfect" detail=brief
```

**説明**: 自動消毒の全体設定を変更するツールを検索し、呼び出せることを示しました。通常のデモではセーブデータを変更しません。ユーザーが実際に開始時の設定を求めた場合は、`colony_control domain=diagnostic action=set_auto_disinfect disabled=true applyNow=true confirm=true` を呼び出します。

### ステップ6: スクリーンショットによる分析

```
navigation_control action=screenshot
```

**説明**: 現在のゲーム画面を取得しました。この画像を使って、状況を目で確認しながら追加分析できます。

### ステップ7: ツールを使う手順の検索

```
server_control domain=catalog action=guide goal="build and power an electrolyzer" detail=brief
```

**説明**: 電解装置を建築して電力を供給するという目標に応じて、推奨されるリソース、使用するツール、安全に実行する手順を取得しました。

### ステップ8: ワールド内の元素のリアルタイム分析

```
read_control domain=world action=element_summary
```

**説明**: 現在のワールドにある元素の質量と温度を読み取り専用で集計し、セーブデータをリアルタイムに分析する機能を示しました。

### ステップ9: 建築・採掘の事前チェック

```
world_editor command=read path=/active/map/viewport.md
building_control domain=planning action=placement_candidates prefabId=Tile x1=<viewport.x1> y1=<viewport.y1> x2=<viewport.x2> y2=<viewport.y2> limit=3
building_control domain=planning action=preview prefabId=Tile material=auto x=<bestCandidate.x> y=<bestCandidate.y> dryRun=true
orders_control domain=area action=dig x1=<viewport.x1> y1=<viewport.y1> x2=<viewport.x1+2> y2=<viewport.y1+2> dryRun=true detail=true
```

**説明**: 仮想マップを読み取り、タイルの設置と小さな範囲の採掘をそれぞれ事前チェックしました。実際の作業指示は出していません。

### ステップ10: 計画機能

```
game_control domain=ui uiDomain=edit_mark action=list limit=3
```

**説明**: ゲーム内の編集マーク・計画依頼の一覧を取得しました。プレイヤーはゲーム内で範囲を選択して計画を入力でき、エージェントはここから読み取って実行可能な手順にまとめます。

## 終了時の報告

10ステップがすべて終わったら、次の内容をユーザーにまとめて伝える。

1. 実演した10項目の機能一覧（各項目1文）
2. 成功・失敗したステップ
3. 次にできることを1文で案内する（例: 「実際の建築や計画の実行に進みますか？」）

まとめは5文以内に収める。
