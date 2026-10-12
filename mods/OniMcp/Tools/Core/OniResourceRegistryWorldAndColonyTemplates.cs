using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class OniResourceRegistry
    {
        private static void AddWorldAndColonyResourceTemplates(List<McpResourceTemplateInfo> templates)
        {
            templates.AddRange(new[]
            {
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://world/cell/{x}/{y}",
                                        Name = "read_control",
                                        Title = "ワールドのセル",
                                        Description = "read_control domain=world action=cell_info で、指定したマップセルの元素、質量、温度、病原菌、可視性を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://world/search{?query,kinds,areaId,x1,y1,x2,y2,worldId,visibleOnly,state,solid,minMassKg,maxMassKg,minTempC,maxTempC,nearX,nearY,sort,limit,maxCells}",
                                        Name = "read_control",
                                        Title = "マップ検索",
                                        Description = "read_control domain=world action=search で、query/条件に従い、マップ上の元素セル、設備、地面のアイテム、複製人間を検索する。areaId/矩形/ワールド、温度/質量/状態による絞り込みと、nearX/nearY から近い順の並べ替えに対応する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://world/coordinate-screenshot{?areaId,x1,y1,x2,y2,worldId,view,focusCamera,paddingCells,showGrid,showCoordinates,includeCellLabels,step,waitFrames,filename}",
                                        Name = "navigation_control",
                                        Title = "座標付きスクリーンショット",
                                        Description = "指定領域へカメラを移動し、ワールド座標のグリッド線と座標テキストを重ねてスクリーンショットを取得する。視覚モデルで座標を直接読み取れるよう、screenshot.url/latestUrl を返す。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://world/text-map{?areaId,x1,y1,x2,y2,worldId,visibleOnly,view,sparse,includeBuildings,includeItems,includeDupes,includeElements,includeSummary,detail,encoding,profile,format,elementLimit,objectLimit,maxCells}",
                                        Name = "read_control",
                                        Title = "ワールドのテキストマップ",
                                        Description = "read_control domain=world action=text_map で、指定した矩形領域や areaId のテキストマップを取得する。トークン消費の少ない走査や、画像を認識できない場合の代替手段として使う。画像と座標の対応が必要なら、oni://world/coordinate-screenshot を優先する。",
                                        MimeType = "text/plain"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://power/summary{?worldId,includeDetails,limit}",
                                        Name = "read_control",
                                        Title = "電力の概要",
                                        Description = "read_control domain=infrastructure action=power_summary で、指定ワールドの電力システムの概要を取得する。ワールド ID、詳細表示のオン・オフ、件数上限による絞り込みに対応する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://power/ports{?worldId,areaId,x1,y1,x2,y2,query,limit}",
                                        Name = "read_control",
                                        Title = "電力ポート",
                                        Description = "read_control domain=infrastructure action=power_ports で、指定領域内の設備の電力ポートのセルを取得する。ワールド ID、領域、キーワードによる絞り込みに対応する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rooms/list{?worldId,type,includeBuildings,includeCriteria,includeNeutral,limit}",
                                        Name = "read_control",
                                        Title = "部屋一覧",
                                        Description = "read_control domain=infrastructure action=rooms で、部屋システムの状態を取得する。ワールド ID、部屋の種類、設備の詳細、条件テキストの取得に対応する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                {
                    UriTemplate = "oni://rooms/types{?type,query,includeNeutral,limit}",
                    Name = "read_control",
                    Title = "部屋種別の成立条件と効果",
                    Description = "read_control domain=infrastructure action=room_types で、未成立の部屋種別も含めたゲーム定義の条件文と効果を取得する。部屋種別の id/名前で検索可能。既存の部屋の判定とは別。",
                    MimeType = "application/json"
                },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://thermal/overheat-risk{?worldId,marginC,includeNonOverheatable,minTempC,limit}",
                                        Name = "read_control",
                                        Title = "オーバーヒートリスクの走査",
                                        Description = "read_control domain=world action=thermal_overheat_risk で、設備のオーバーヒートリスクの走査結果を取得する。ワールド ID、温度差のしきい値、オーバーヒートしない設備、件数上限による絞り込みに対応する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/manifest{?query,group,mode,risk,detail,limit}",
                                        Name = "server_control",
                                        Title = "ツール一覧",
                                        Description = "server_control domain=catalog action=manifest で、全ツールまたは絞り込み後の一覧を取得する。detail=brief/compact はトークン消費を抑えた一覧に使う。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/search{?query,group,mode,risk,detail,limit}",
                                        Name = "server_control",
                                        Title = "ツール検索",
                                        Description = "server_control domain=catalog action=search で、キーワード、グループ、モード、リスクに従って、トークン消費を抑えてツールを検索する。detail=brief は最小限の結果を返す。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/guide{?goal,detail}",
                                        Name = "server_control",
                                        Title = "目的別のツールガイド",
                                        Description = "server_control domain=catalog action=guide で、目標に応じたトークン消費の少ないツール利用ガイドを生成し、リソース、検索語、ツールの組み合わせ、一括処理の方針、計画用 harness の手順を提案する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/player-action-coverage{?query,group,status,detail,includeResources,includeHotkeys,limit}",
                                        Name = "server_control",
                                        Title = "プレイヤー操作の対応範囲",
                                        Description = "プレイヤーの操作ごとに、MCP ツール・リソースの対応範囲を検索する。detail=brief はトークン消費の少ない照会に適し、detail=full はリソースのアンカーを返す。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/static-audit{?includeWarnings}",
                                        Name = "server_control",
                                        Title = "静的インターフェース監査",
                                        Description = "ツール登録、対応表、リソースの入口、危険なツールの確認引数の静的な自己検査結果を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://game/saves{?type,limit}",
                                        Name = "game_control",
                                        Title = "セーブファイル",
                                        Description = "ローカル・クラウドのセーブファイルを取得する。game_control domain=save action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://game/dlc{?includeCosmetic}",
                                        Name = "game_control",
                                        Title = "セーブデータの DLC 有効化状態",
                                        Description = "一時停止メニューの DLC 有効化ボタンの状態を取得する。game_control domain=dlc action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://game/red-alert{?worldId,allWorlds}",
                                        Name = "game_control",
                                        Title = "非常警報の状態",
                                        Description = "game_control domain=state action=red_alert_status で、現在・指定・全ワールドの非常警報（緊急モード）の状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/side-screen-surfaces{?query,status,includeNoAction,limit}",
                                        Name = "server_control",
                                        Title = "サイドパネルの surface 対応範囲",
                                        Description = "server_control domain=catalog action=surface_audit surface=side_screen で、ランタイムの SideScreenContent 型と補助サイドパネルの KScreen ごとに、MCP ツール・リソースの対応範囲を取得する。status=review で未対応部分を探せる。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/user-menu-surfaces{?query,status,detail,limit}",
                                        Name = "server_control",
                                        Title = "ユーザーメニューの surface 対応範囲",
                                        Description = "server_control domain=catalog action=surface_audit surface=user_menu で、ソースコードの UserMenu/context-menu ボタンの実装元ごとに、MCP ツール・リソースの対応範囲を取得する。detail=brief はトークン消費の少ない照会に適している。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/management-surfaces{?query,status,detail,limit}",
                                        Name = "server_control",
                                        Title = "管理画面の surface 対応範囲",
                                        Description = "server_control domain=catalog action=surface_audit surface=management で、ManagementMenu/TableScreen/全画面管理画面ごとに、MCP ツール・リソースの対応範囲を取得する。detail=brief はトークン消費の少ない照会に適している。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/tool-menu-surfaces{?query,status,detail,limit}",
                                        Name = "server_control",
                                        Title = "ツールバーの surface 対応範囲",
                                        Description = "server_control domain=catalog action=surface_audit surface=tool_menu で、ToolMenu のメインツールバー・サンドボックスツールバーごとに、MCP ツール・リソースの対応範囲を取得する。detail=brief はトークン消費の少ない照会に適している。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/ui-menu-surfaces{?query,status,detail,limit}",
                                        Name = "server_control",
                                        Title = "UI メニューの surface 対応範囲",
                                        Description = "server_control domain=catalog action=surface_audit surface=ui_menu で、OverlayMenu/BuildCategory/安全な UI hotkey ごとに、MCP ツール・リソースの対応範囲を取得する。detail=brief はトークン消費の少ない照会に適している。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://colony/notifications{?query,includePending,limit}",
                                        Name = "colony_control",
                                        Title = "通知一覧",
                                        Description = "現在の HUD の通知、メッセージ、フォーカス対象、消去可能な状態を取得する。colony_control domain=notification action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://resources/pins{?worldId,query,includeUnpinned,limit}",
                                        Name = "read_control",
                                        Title = "資源パネルの固定表示と通知",
                                        Description = "AllResourcesScreen の資源行の固定表示と通知のオン・オフ状態を取得する。read_control domain=resources action=pins と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/global-control-surfaces{?query,status,detail,limit}",
                                        Name = "server_control",
                                        Title = "全体操作の surface 対応範囲",
                                        Description = "server_control domain=catalog action=surface_audit surface=global_control で、速度、最上位の HUD、一時停止メニュー、設定、外部アカウントのメニューごとに、MCP の対応範囲を取得する。detail=brief はトークン消費の少ない照会に適している。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/notification-surfaces{?query,status,detail,limit}",
                                        Name = "server_control",
                                        Title = "通知の surface 対応範囲",
                                        Description = "server_control domain=catalog action=surface_audit surface=notification で、NotificationScreen/NotificationManager/MessageNotification ごとに、MCP の対応範囲を取得する。detail=brief はトークン消費の少ない照会に適している。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://tools/read/{name}{?...}",
                                        Name = "tools_read_resource",
                                        Title = "汎用の読み取り専用ツールリソース",
                                        Description = "登録済みの任意の read ツールを MCP resource として読み取る。クエリ文字列がツールの引数になる。複雑な引数では、引き続き用途別のリソースか、ツールの直接呼び出しを推奨する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/defs{?query,category,includeUnavailable,limit}",
                                        Name = "building_control",
                                        Title = "建築可能な設備の定義",
                                        Description = "キーワード、分類、利用可否に従って、建築可能な設備の定義を検索する。建築メニュー、素材・外観の選択、建築予定の計画に使う。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/materials{?prefabId,worldId,includeUnavailable,limit}",
                                        Name = "building_control",
                                        Title = "使用可能な建築素材",
                                        Description = "指定した設備について、現在・指定ワールドの使用可能な素材候補と在庫を取得する。既定では利用可能な素材だけを返し、先頭の項目が material=auto で選ばれる素材になる。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/configurables{?areaId,x1,y1,x2,y2,worldId,capability,query,limit}",
                                        Name = "building_control",
                                        Title = "設定可能な設備",
                                        Description = "領域、機能、キーワードに従って、プレイヤーがサイドパネルで設定できる設備を取得する。building_control action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/lights{?areaId,x1,y1,x2,y2,worldId,query,configurableOnly,limit}",
                                        Name = "building_control",
                                        Title = "照明の操作項目",
                                        Description = "building_control action=visual kind=light visualAction=list で、領域とキーワードに従って照明設備と色のプリセットを取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/pixel-packs{?areaId,x1,y1,x2,y2,worldId,query,includePresets,limit}",
                                        Name = "building_control",
                                        Title = "ピクセルパックの操作項目",
                                        Description = "building_control action=visual kind=pixel_pack visualAction=list で、領域とキーワードに従って Pixel Pack の色パネルとプリセットを取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://geotuners{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "GeoTuner",
                                        Description = "領域とキーワードに従って GeoTuner の割り当て状態を取得する。building_control domain=side_surface surface=geo_tuner action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://geotuners/geysers{?id,x,y,worldId,query,includeUnstudied,limit}",
                                        Name = "building_control",
                                        Title = "地殻チューナーの対象間欠泉",
                                        Description = "GeoTuner と同じワールドで選択可能な間欠泉と、割り当ての制約を取得する。building_control domain=side_surface surface=geo_tuner action=list_geysers と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/artables{?areaId,x1,y1,x2,y2,worldId,query,includeOptions,limit}",
                                        Name = "building_control",
                                        Title = "芸術作品の外観",
                                        Description = "building_control domain=special kind=artable action=list で、領域とキーワードに従って芸術作品の外観の選択肢を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/monument-parts{?areaId,x1,y1,x2,y2,worldId,query,includeOptions,limit}",
                                        Name = "building_control",
                                        Title = "記念碑の部品の外観",
                                        Description = "building_control domain=special kind=monument_part action=list で、領域とキーワードに従って記念碑の部品の外観の選択肢を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://ranching/lures{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "動物のおびき寄せ設備",
                                        Description = "building_control domain=special kind=creature_lure action=list で、領域とキーワードに従って動物のおびき寄せ設備の餌の選択肢を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/gene-shufflers{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "Gene Shuffler",
                                        Description = "building_control domain=special kind=gene_shuffler action=list で、領域とキーワードに従って Gene Shuffler の状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://story/printerceptors{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "Printerceptor",
                                        Description = "領域とキーワードに従って PrinterceptorSideScreen の状態を取得する。building_control domain=story_facility kind=printerceptor action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/remote-work-terminals{?areaId,x1,y1,x2,y2,worldId,query,includeDocks,limit}",
                                        Name = "building_control",
                                        Title = "リモート コントローラー",
                                        Description = "領域とキーワードに従って RemoteWorkTerminalSidescreen の dock の選択肢を取得する。building_control domain=story_facility kind=remote_work_terminal action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://farming/genetic-analysis-stations{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "Botanical Analyzer",
                                        Description = "領域とキーワードに従って Botanical Analyzer の種子の許可・禁止状態を取得する。building_control domain=story_facility kind=genetic_analysis_station action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/dispensers{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "ディスペンサー",
                                        Description = "領域とキーワードに従って DispenserSideScreen の状態を取得する。building_control domain=side_surface surface=facility kind=dispenser action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/receptacles{?areaId,x1,y1,x2,y2,worldId,query,includeOptions,limit}",
                                        Name = "building_control",
                                        Title = "展示・配置スロット",
                                        Description = "領域とキーワードに従って ReceptacleSideScreen / SpecialCargoBayClusterSideScreen / SingleEntityReceptacle の状態と要求可能なエンティティを取得する。building_control domain=receptacle action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/suit-lockers{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "アトモスーツ ドック",
                                        Description = "領域とキーワードに従って SuitLockerSideScreen の状態を取得する。building_control domain=side_surface surface=facility kind=suit_locker action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://story/poi-tech-unlocks{?areaId,x1,y1,x2,y2,worldId,query,pendingOnly,lockedOnly,limit}",
                                        Name = "building_control",
                                        Title = "研究用ポータル",
                                        Description = "領域とキーワードに従って Research Portal の解放作業、進捗、解放項目を取得する。building_control domain=story_facility kind=poi_tech_unlock action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://story/lore-bearers{?areaId,x1,y1,x2,y2,worldId,query,interactableOnly,limit}",
                                        Name = "building_control",
                                        Title = "LoreBearer",
                                        Description = "領域とキーワードに従って閲覧可能・閲覧済みの LoreBearer を取得する。building_control domain=side_surface surface=facility kind=lore_bearer action=list と同等。",
                                        MimeType = "application/json"
                                    }
            });
        }
    }
}
