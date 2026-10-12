# 地図差分予行のレイヤー観測

`world_editor edit /active/map/viewport.md dryRun=true` のpreflightは構文・足形を確認する。配置可否、建材配送、支持、掘削、ネットワーク接続の完全な検証ではない。`placementValidated=false`を必ず読む。

建設差分の`cellDetails`に要求Prefabと定義ObjectLayer、現在のGridレイヤー別Prefab/現物ID、複製人間ID、固体地形を返す。先頭64セルを表示し、残りがある場合は`cellDetailsTruncated=true`。同一設備がWireとWireTileなど複数レイヤーに現れることがある。Gridリストは拾得可能物の全スタックを列挙するAPIではない。

定義ObjectLayerは完成設備の本体レイヤーで、建設予約・置換・橋の端点を含む全書込先の保証ではない。別レイヤーの既存設備が表示されても、上書き可能と解釈しない。

## 配置検証と自動掘り起こし

地図編集のdryRunは構文・形状の予行。`building_control domain=planning action=preview` は内部でdryRun=trueにし、TryPlanOneによる配置予行を実行する。座標は `/active/ops/coordinate.md` の `targetTool=building_control` で指定する。外側のops実行と内側の配置予行を区別する。

別レイヤーという観測だけでは植物の自動撤去を防げない。通常utilityでは植物障害物の列挙を除外し、ブリッジではendpoint/footprint判定を維持する。予行の成功と、実際の建設完了・接続を区別する。
