# 設備形状の簡易版・完全版

読み取り専用。簡易版（既定の `compact=true`）と完全版（`compact=false`）は、同じゲーム側の占有・接続計算を参照する。建設予約、回転変更、ゲーム進行、カメラ操作は行わない。

## 使用例

```json
{"command":"read","path":"/active/buildings/geometry.md","query":"発電機","compact":true,"limit":10,"task":"設備の基準点と占有範囲を確認"}
{"command":"read","path":"/active/buildings/geometry.md","id":12345,"compact":false,"task":"選択した設備の全占有セルと接続点を確認"}
{"command":"read","path":"/active/buildings/geometry.md","action":"definition","prefabId":"Generator","orientation":"R90","compact":false,"task":"未配置設備の回転後の相対形状を確認"}
```

公開 `building_control` からも `domain=geometry action=inspect/definition` と同じ引数で取得できる。`inspect` は完成設備が対象。未完成設備を含めた一覧ではない。IDと完全Prefab IDは厳密一致、queryは名称またはPrefab IDの部分一致。既定は現在ワールド、`worldId=-1` は全ワールド。既定10件、最大100件で、打切りは `matched` と `truncated` に示す。

## 返却項目

- 共通: ID、Prefab、対象種別、座標系、向き・許可された回転、`origin`、`bottomLeft`、`bounds`、回転後の `size`、`occupiedCount`、`fillsBounds`、接続点、取得元、取得失敗理由。
- 完全版のみ: `occupiedCells`、回転前の `definitionOffsets`・回転後の `orientedOffsets`、定義寸法、配置規則、オブジェクト／タイルのレイヤー、ポートの元オフセットと取得元、座標契約。
- `bounds` は `[xMin,yMin,xMax,yMax]`（両端を含む）。疎な形状の場合も外接矩形であり、`fillsBounds=false` なら矩形内の全セルが占有されるとは解釈しない。占有一覧が必要なら完全版を使う。
- 個体は絶対セル座標。定義は回転後の相対オフセットであり、実ワールドの設置位置ではない。定義に指定した向きの形状は取得できるが、`permittedRotations` に従う実配置可否の検証を意味しない。Y+は上（地表方向）、Y-は下（地下方向）。
- `available=false` のとき範囲・占有数はnull。ポートの取得失敗は `portsComplete=false` と `errors` に示す。空配列だけで全取得成功と判断しない。

## ネイティブ取得と注意点

個体の占有は `BuildingDef.RunOnArea(Grid.PosToCell(Building), Building.Orientation, callback)`、定義は `PlacementOffsets` と `Rotatable.GetRotatedCellOffset`。`GetBottomLeftCell()` の名称を根拠に左下とみなさず、占有座標の最小値から実際の左下を求める。幅3設備の標準オフセットは横に-1/0/+1となる。

ポートは定義の電力・配管・高エネルギー粒子オフセット、個体／定義の自動化ポート、ブリッジの `UtilityNetworkLink.link1/link2`、および `ISecondaryInput` / `ISecondaryOutput` を参照する。独自MODがこれらの契約を使わない独自端子を追加した場合まで、網羅取得を保証するものではない。登録レイヤーの実占有や配線接続成立は別の情報であり、ブリッジなどで占有形状と `Grid.Objects` の登録セルが異なる場合がある。

2026-10-08から、設備形状窓口、セルJSON／Markdown、テキスト／人間用地図、建設予行・実配置は共通のネイティブ占有計算を使う。完成設備と設計図の占有セルを索引化し、`Grid.Objects`の基準レイヤーに登録されないブリッジも全セルで取得する。通常の電線・配管は既存の専用レイヤー表示を維持する。

建設入力の`x/y`は占有範囲の左下（`lowerLeftCell`）の契約を維持する。回転後オフセットの最小値からネイティブ`origin`へ変換し、予行・実配置・接続点計算へ同じ基準点を渡す。応答で要求アンカー、ネイティブ基準点、実左下、回転後の寸法、全占有セルを分離する。既存設備の再利用はアンカー・ワールド・向き・全占有セルの一致を確認する。

地図の建設マスクは回転後の占有セルと厳密に照合し、外接矩形内の穴を埋めない。1セルだけの指定は左下アンカーの略記。占有が不連続な定義の複数セルマスクは、複数アンカーとの区別がつかないため拒否し、1編集につき1点のアンカー指定を使う。占有定義の欠測・無効な向き・マップ端の行をまたぐ無効オフセットは未取得／無効として返し、矩形の推測で代用しない。形状の取得成功は研究・素材・支えなどの施工条件の成功を意味しない。
