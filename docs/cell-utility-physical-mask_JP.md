# セルの接続サマリーと本体の物理マスク

`oni://world/cell/{x}/{y}` と既存の `read_control domain=world action=cell_info` は、`utilities.connectionSummary` の5系統で、占有・完成物の局所物理マスク・予定marker・未取得を区別します。隣接する設備の存在や同じ回路IDを、接続辺へ置き換えません。新しい公開toolや接続操作は追加しません。

## 返却の意味

| 項目 | 意味 |
|---|---|
| `hasLayer` | 対象系統の既存object layerに物体があるか。完成物・予定・replacementを含む占有の観測で、完成や導通とは別です。layer自体が読めなければnullです。 |
| `hasPhysical` | BuildingCompleteまたは既存KAnimGraphTileVisualizer.isPhysicalBuildingがあるか。未分類の物体だけならnull、実体なし/予定だけならfalseです。 |
| `hasPlanned` | 既存Constructable、またはphysicalでない既存visualizer markerがあるか。未分類の物体が混在し他に陽性markerが無いならnullです。完成物とreplacement予定が同居すれば、hasPhysicalとhasPlannedが両方trueになり得ます。 |
| `connectionState` | physical / planned / unknown / absent。physicalは局所physical maskの取得成功で、ネットワーク全体の確定や実流量の評価ではありません。 |
| `connectionSource` | physical取得成功時のみnative_physical。他は明示nullです。 |
| `reasonCode` | layer/state/provider/manager/maskの未取得や例外、異なるmanagerの混在等を区別します。正常なabsent/plannedはnullです。 |
| `nativePhysicalMask` | native UtilityConnectionsの整数値そのもの。取得済み0を0で保持し、未取得や予定だけの場合は明示nullです。 |
| `dirs` / `glyph` / `links` / `to` | 同じlocal physical maskのcardinal bitsから作る方向・図形・隣セル表示。隣セルの存在や同回路を根拠に追加しません。 |
| `open` | physical maskを取得できた場合だけ、そのmaskの辺に含まれない隣接layer占有を示す補助情報。接続可能・工事許可・流量を意味しません。 |

取得済みmask0はconnectionState=physical、nativePhysicalMask=0、dirs/links/toは`.`、glyphは孤立した実体の`*`です。`*`は既存UtilityGlyphの汎用記号で、mask0と単方向maskでも共用します。native UIや既存markdown mapのglyphを複製・一致させる変更ではなく、方向はdirs・linksとnativePhysicalMaskで示します。中心に対象物が無いabsentはhasLayer=false・glyph/dirs/links/to/openが`.`で、物理maskはnullです。予定だけ、または未取得は接続表示を`?`にし、取得済み0へ補いません。hasPhysicalの陽性を観測していてもproviderやmanagerが未取得なら、connectionStateはunknownのままです。

方向bitはnativeのUp/Down/Left/Rightをそのまま扱います。相手側のmaskや実体が無い場合にも、相手を生成したり相互接続済みへ読み替えたりしません。2次元のgrid外を別rowへ折り返さず、その方向のbitは保持してlinks/toをinvalidと表示します。これは読めたlocal maskと座標範囲の観測であり、双方向の有効な辺を別途検証したという意味ではありません。

`circuitId`、port/bridge情報、各layerの物体詳細は従来の独立した欄です。circuitIdの既存未取得sentinelも変更せず、同じIDやportの位置だけで接続・通電・搬送を証明しません。endpoint/bridgeの定義や経路表示も、この変更で稼働確認済みにはなりません。

## 本体へ委譲する読取範囲

既存object layerの同じGameObject重複登録は、このreader内で同じ物体として扱います。完成物またはnative physical markerのある既存物体から、IHaveUtilityNetworkMgr.GetNetworkManagerを取得し、同じ既存managerの `GetConnections(cell, is_physical_building:true)` を一度読みます。managerが異なる場合、取得できない場合、例外ではunknownと理由を返し、別layerや隣接からmaskを補いません。予定markerの存在は独立に保持します。

pinned本体のこのtrue経路は既存physicalGridのconnections fieldを読む窓口です。生成・Refresh・UpdateConnections・ネットワーク再構築・UI選択やシミュレーション進行を、この観測のために呼びません。nativeの `GetConnections(cell,false)` はvisualGridとphysicalGridの和であり、純粋な予定専用maskではありません。このサマリーではfalse経路を呼びません。既存markdown mapのplanned表示等でfalseを読む意味の整理は別の残る範囲です。

## 検証の範囲と残る親項目

ホストでは、隣接UDR/local Down反例、実体無しと隣接、取得済み0と欠測、予定/完成/replacement共存、重複登録、異managerやgetter例外、同回路でも非接続、grid端、明示nullを実サマリーとreaderへ通しました。nativeオブジェクトやmanagerはホストの入力境界です。今回の実機には取得済みmask0・予定だけ・未初期化や故障条件が無いため、それらを実機再現したとは扱いません。

開発版と公開固有IDの両方で、既存の2入力を初回と通常再読込後に停止状態で読みました。完成現物の5系統を本体のlocal physical maskと照合し、隣接から余分な辺を出す反例と、中心に当該実体が無いのに辺を出す反例を解消しました。対象identity・geometry・endpointの保持、各読取区間の反復と6項目の停止時計を別に確認しています。circuitIdはcacheの観測値として区間内の整合を確認し、再読込前後で数値が固定するとは要求していません。

両版で共通の現行native取得元として、既存markdown mapのGetConnections(cell,true)による方向・links/to/openを照合しました。開発版のlayer_snapshotは追加の同時刻証拠で、公開版にはこの機能が未搭載のため、新しいsnapshot窓口を持ち込んでいません。保全した旧snapshotは過去の比較資料であり、現在の公開版から取得した値とは扱いません。汎用glyphとnative mapのglyphは別の表記です。

両版の実ソースを連結したホスト回帰と、警告・エラー0の本体参照ビルドを別に通しました。元の導入物・対象設定・原本・共有保存・保存先選択・他MOD/DLCと元版の未ロードメニュー入口へ復帰しました。これは対象の設定と接続先・導入版を照合した範囲であり、全preferencesのbyte一致、同じ接続session IDの保持、描画された最前面UIの証明まで含みません。

親項目の、設計図・全完成実体・port・グラフ待ち/同一回路・実稼働を一体として照合する作業は残ります。実通電・流量・時間平均、全方向/派生・bridge・通常UI描画、地図の重複計上、接続や切断の書込経路、全cache非変更・他MOD全組合せは今回解消したと主張しません。
