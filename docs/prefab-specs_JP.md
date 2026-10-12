# 設備・アイテムの定義スペック取得

`world_editor action=read` の読み取り専用窓口。実在する設備の現在値や在庫量と、定義スペックを混同しない。

| パス | 対象 |
| --- | --- |
| `/active/buildings/specs.oni` | 設備のPrefab定義 |
| `/active/resources/specs.oni` | 設備以外のアイテム・生物・植物等のPrefab定義 |

`prefabId`（完全一致）または `query`（ID／現在言語の名前の部分一致）が必須。未指定では全定義を返さない。`specDetail=brief` が既定、`full` で詳細を要求する。`limit` は簡易版で既定10、詳細版で既定1、共通上限50。`matched`／`truncated` で取りこぼしを明示する。

## 簡易版

ID・名称、設備寸法・消費電力・過熱定義・建設質量と素材分類、および該当部品の収納容量・電池容量・発電定格・食品品質／士気／単位当たりカロリーを取得。元素を持つアイテムは元素ID・状態・比熱・熱伝導率も取得。取得対象部品がない場合は省略、読めない項目は `unavailableFields` に記録する。

```json
{ "action": "read", "path": "/active/buildings/specs.oni", "prefabId": "MineralDeoxidizer", "specDetail": "brief", "task": "酸素散布装置の主要仕様を確認" }
```

## 詳細版

簡易版の項目に、設備説明・追加の建設／熱／耐久／配置定義・ネイティブ占有形状とポート、対応済み部品の物質消費／出力・搬送設定と部品型一覧を追加する。簡易版と共通項目は同じ値を返す。

```json
{ "action": "read", "path": "/active/resources/specs.oni", "prefabId": "FieldRation", "specDetail": "full", "task": "栄養バーの定義仕様を確認" }
```

温度はK、電力W、熱kW、質量kg、時間s、食品の `caloriesKcalPerUnit` はkcal/個。`PrimaryElement` はPrefab初期値で、現物の温度・質量ではない。素材依存の実際の過熱温度・効果等は現物情報を別途読む。

詳細版も任意のMOD部品や全機能を網羅するものではなく、対応フィールドのみ取得する。未取得をゼロと扱わない。Unityオブジェクト全体や非公開状態を無制限に展開しない。定義読み取りでは新規オブジェクト生成・ゲーム進行・選択変更を行わない。

本体の建設質量は `BuildingDef.Mass`（素材分類順のkg配列）、融点は `BaseMeltingPoint`、騒音は `BaseNoisePollution`、修理時間は `BaseTimeUntilRepair` を取得する。
