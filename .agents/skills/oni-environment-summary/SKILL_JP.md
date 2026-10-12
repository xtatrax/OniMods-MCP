---
name: oni-mcp-environment-summary
description: ONI の周辺環境や資源、開始地点の概要、近くの危険、拠点を拡張する方向について手早い要約を求められたときや、「周りには何がある？」と聞かれたときに使用する。トークン消費の少ない読み取り専用の MCP ツール、特に read_control domain=world action=area_snapshot を使い、次の判断に役立つ簡潔な環境・資源の要約を返す。ゲーム操作は実行しない。
---

# ONI MCP 周辺環境の要約

## 目的

現在画面に映っている範囲、選択範囲、製造ポッドの近く、またはユーザーが指定した座標の周辺について、状況を手早くまとめる。このスキルは読み取り専用とし、採掘、建築、片付け、収穫、設定変更は行わない。

## 最初に行う確認

最小限の読み取りで質問に答える。

```
colony_control domain=snapshot action=get profile=brief includeAtmosphere=false
navigation_control action=get_view
read_control domain=world action=area_snapshot preset=utilities encoding=plain includeScreenshot=false
```

ユーザーが `areaId` や座標を指定している場合は、それらを `read_control domain=world action=area_snapshot` に渡す。

プレイヤーの編集マークによる依頼がある場合、またはユーザーが「範囲選択した領域」「マークした範囲」「プレイヤーの計画」に言及している場合は、まず次の読み取りを行う。

```
game_control domain=ui uiDomain=edit_mark action=list limit=5
read_control domain=world action=area_snapshot areaId=<request.areaId> preset=utilities encoding=plain
```

## 詳細を追加で確認する場合

最初の確認だけでは情報が足りない場合に限り、次の読み取りを追加する。

- `read_control domain=world action=element_summary state=solid|liquid|gas`: ワールド全体の質量や温度に関する情報を確認する。
- `read_control domain=resources action=inventory limit=30`: 把握済みの在庫や、地面に落ちている資源を確認する。
- `read_control domain=resources action=food limit=20`: 食料の数量とカロリー（kcal）を確認する。
- `colony_control domain=bio bioDomain=farming action=list_harvestables x1/y1/x2/y2 readyOnly=false limit=80`: 近くの野生植物や食料を確認する。
- `read_control domain=world action=text_map profile=standard encoding=plain`: `read_control domain=world action=area_snapshot` の範囲が狭すぎる場合に、より広い範囲の地形を調べる。

ユーザーが目視確認を求めている場合を除き、スクリーンショットの取得は控える。

## マップの読み方

まず `maps.base`、または `read_control domain=world action=text_map` の base ビューを読む。

- `sol`: 自然に生成された固体のセル。採掘や資源確保の候補。
- `tile`: 人工のタイルや土台。
- `liq`: 液体。元素の詳細が得られた場合は、水・汚染水・塩水などの種類も示す。
- `oxy/po2/co2/hyd`: 酸素・汚染酸素・二酸化炭素・水素。
- `bld`: 設備。
- `dup`: 複製人間。
- `itm`: 地面に落ちているアイテムや資源。
- `unk`: 不明、未探索、またはワールドの外側。

次に、各レイヤーを読む。

- `maps.power`: 電線と、電力を消費・発電する設備。
- `maps.gas_conduits`、`maps.liquid_conduits`: 既存の気体パイプ・液体パイプの経路。
- `maps.logic`: 自動化。
- `maps.solid_conveyor`: コンベアレール。

座標はテキストマップの値を使う。スクリーンショットから正確なセルの位置を推定しない。

## 出力形式

次の見出しを使い、簡潔な日本語で要約する。

```
周辺環境の要約
地形・空間:
資源:
気体・液体:
採取可能なもの・食料:
危険・制約:
拡張する方向:
次の行動の提案:
```

各見出しの内容は1〜3項目とする。座標は、行動の判断に役立つ場合にだけ示す。

## 優先して確認する点

ONI の序盤で特に重要な、次の点を優先する。

- 呼吸できる空間と、二酸化炭素が溜まっている場所
- 近くの水源や、扱いに注意が必要な液体
- 緑藻、オキシライト、有機物（固体）、金属鉱石が利用できるか
- 野生の食料や収穫可能なもの、種子を入手できる見込み
- 高温、真空、ヘドロ・汚染酸素、敵対的な動物、到達できない空洞
- 左右上下の各方向が、拠点の拡張にどの程度適しているか
- 製造ポッド付近の空間を、研究室や拠点の中心部として活用できるか

判断できない場合は、不明な点を明示し、範囲を絞った追加調査を1回提案する。広範囲を読み取るツールをむやみに使わない。

## 安全上の制約

使用してよいもの:

- 読み取り専用ツール
- `game_control domain=ui uiDomain=edit_mark action=list`
- `read_control domain=world action=area_snapshot`
- `read_control domain=world action=text_map`
- 在庫や農業の情報を読み取るツール

使用してはいけないもの:

- `orders_control`、`building_control`、`dupes_control`、`game_control` の書き込み・実行操作全般
- その他、ゲームの状態を変更する設定・書き込み・実行ツール全般

要約の後にユーザーが操作を求めた場合は、計画・建築操作を扱うスキルに切り替え、実行前に dry-run を行う。
