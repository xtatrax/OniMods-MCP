using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class OniResourceRegistry
    {
        private static void AddBuildingAndSpaceResourceTemplates(List<McpResourceTemplateInfo> templates)
        {
            templates.AddRange(new[]
            {
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://story/telepads{?areaId,x1,y1,x2,y2,worldId,query,includeVictory,limit}",
                                        Name = "building_control",
                                        Title = "Printing Pod / Telepad",
                                        Description = "領域とキーワードに従って TelepadSideScreen の移民、研究、スキル、勝利条件の状態を取得する。building_control domain=side_surface surface=facility kind=telepad action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://story/artifacts{?areaId,x1,y1,x2,y2,worldId,query,includeStations,includeWorldArtifacts,limit}",
                                        Name = "building_control",
                                        Title = "Artifact Analysis",
                                        Description = "分析済み artifact、ワールド内の artifact、Artifact Analysis Station の状態を取得する。building_control domain=side_surface surface=facility kind=artifact action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://story/warp-portals{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "Warp Portal",
                                        Description = "領域とキーワードに従って WarpPortalSideScreen の状態を取得する。building_control domain=space_story kind=warp_portal action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://story/temporal-tears{?query,limit}",
                                        Name = "building_control",
                                        Title = "Temporal Tear",
                                        Description = "時間の裂け目の状態と、進入可能・消費可能なロケットを取得する。building_control domain=space_story kind=temporal_tear action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://space/telescopes{?areaId,x1,y1,x2,y2,worldId,query,includeTargets,limit}",
                                        Name = "building_control",
                                        Title = "Telescope",
                                        Description = "領域とキーワードに従って望遠鏡と星図の分析対象を取得する。building_control domain=space_story kind=telescope action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://space/analysis-targets{?query,includeComplete,limit}",
                                        Name = "building_control",
                                        Title = "星図の分析対象",
                                        Description = "望遠鏡で分析できる星図の目的地を取得する。building_control domain=space_story kind=starmap_analysis action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://diagnostics/process-conditions{?id,x,y,worldId,conditionType,query,showHidden,limit}",
                                        Name = "building_control",
                                        Title = "共通の処理条件",
                                        Description = "IProcessConditionSet/ConditionListSideScreen の条件一覧を取得する。building_control domain=space_story kind=process_conditions action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://dupes/bionic-upgrades{?id,name,query,limit}",
                                        Name = "dupes_control",
                                        Title = "バイオニック複製人間のアップグレードスロット",
                                        Description = "BionicSideScreen のアップグレードスロット、割り当て、装着状態を取得する。dupes_control domain=side_screen action=bionic_upgrades と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/missile-launchers{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "隕石ブラスター",
                                        Description = "building_control domain=special kind=missile_launcher action=list で、領域とキーワードに従って隕石ブラスターの弾薬の許可状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/modules{?areaId,x1,y1,x2,y2,worldId,query,includeOptions,limit}",
                                        Name = "building_control",
                                        Title = "ロケットモジュール",
                                        Description = "building_control domain=rocket rocketDomain=module action=list で、領域とキーワードに従って並べ替え可能なロケットモジュールと操作機能を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/module-defs{?id,x,y,worldId,mode,query,limit}",
                                        Name = "building_control",
                                        Title = "ロケットモジュールの定義",
                                        Description = "building_control domain=rocket rocketDomain=module action=list_defs で、SelectModuleSideScreen のロケットモジュールの定義と add/replace の条件を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/launch-pads{?query,limit}",
                                        Name = "building_control",
                                        Title = "ロケット発射台",
                                        Description = "building_control domain=rocket rocketDomain=ops action=list_launch_pads で、LaunchPadSideScreen の発射台、停泊中のロケット、着陸可能なロケットを取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/flight-utilities{?rocketId,rocketName,query,includeDupes,limit}",
                                        Name = "building_control",
                                        Title = "ロケット飛行モジュールの補助操作",
                                        Description = "building_control domain=rocket rocketDomain=flight_utility action=list で、ModuleFlightUtilitySideScreen の内容物を空にする・投下・自動投下・対象・複製人間の選択状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/restrictions{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "ロケット制御装置の制限",
                                        Description = "building_control domain=rocket rocketDomain=restriction action=list で、領域とキーワードに従って RocketRestrictionSideScreen の状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/usage-controls{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "ロケット内部設備の使用制限",
                                        Description = "building_control domain=rocket rocketDomain=usage action=list で、領域とキーワードに従ってロケット内部の設備が制御装置の制限を受けるかを取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/crew-requests{?rocketId,rocketName,query,limit}",
                                        Name = "building_control",
                                        Title = "ロケットの乗組員の召集",
                                        Description = "building_control domain=rocket rocketDomain=crew_request action=list で、SummonCrewSideScreen の乗組員の召集・解放状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/assignment-groups{?groupId,controllerId,query,includeDupes,limit}",
                                        Name = "building_control",
                                        Title = "割り当てグループのメンバー",
                                        Description = "building_control domain=rocket rocketDomain=assignment_group action=list で、AssignmentGroupControllerSideScreen の割り当てグループと、複製人間の加入設定の状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/cargo-collectors{?rocketId,rocketName,query,limit}",
                                        Name = "building_control",
                                        Title = "ロケットの貨物収集モジュール",
                                        Description = "building_control domain=rocket rocketDomain=cargo_status action=collectors で、CargoModuleSideScreen の収集モジュールの容量、在庫、進捗を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/harvest-modules{?rocketId,rocketName,query,limit}",
                                        Name = "building_control",
                                        Title = "ロケットの掘削モジュール",
                                        Description = "building_control domain=rocket rocketDomain=cargo_status action=harvest_modules で、HarvestModuleSideScreen の掘削状態とダイヤモンドの在庫を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://automation/controls{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "自動化の操作項目",
                                        Description = "領域とキーワードに従って、プレイヤーが設定できる自動化・電力関連の操作項目を取得する。building_control action=list_automation と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://automation/automatable{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "自動化による運搬の制限",
                                        Description = "building_control domain=side_surface surface=automation kind=automatable action=list で、領域とキーワードに従って AutomatableSideScreen の手動運搬の許可状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://automation/critter-sensors{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "動物センサー",
                                        Description = "building_control domain=side_surface surface=automation kind=critter_sensor action=list で、領域とキーワードに従って CritterSensorSideScreen の動物・卵の計数のオン・オフ状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://storage/list{?resource,worldId,limit}",
                                        Name = "building_control",
                                        Title = "収納設備一覧",
                                        Description = "資源、ワールド、件数に従って収納設備の一覧を取得する。building_control domain=storage action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://storage/tile-selections{?areaId,x1,y1,x2,y2,worldId,query,includeOptions,limit}",
                                        Name = "building_control",
                                        Title = "収納タイルの対象アイテム",
                                        Description = "領域とキーワードに従って SingleItemSelectionSideScreen / StorageTile の対象アイテムの選択肢を取得する。building_control domain=tile_selection action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://filters/controls{?areaId,x1,y1,x2,y2,worldId,kind,query,includeOptions,limit}",
                                        Name = "building_control",
                                        Title = "フィルターの操作項目",
                                        Description = "領域、種類、キーワードに従って、単一選択・複数選択フィルターの操作項目を取得する。building_control domain=filter action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/options{?areaId,x1,y1,x2,y2,worldId,kind,query,includeOptions,limit}",
                                        Name = "building_control",
                                        Title = "サイドパネルの選択肢",
                                        Description = "領域、種類、キーワードに従って、方向、少数の選択肢、送信チャンネル、放射線粒子の方向の操作項目を取得する。building_control domain=side_surface surface=option action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/state{?areaId,x1,y1,x2,y2,worldId,kind,query,limit}",
                                        Name = "building_control",
                                        Title = "サイドパネルの状態設定",
                                        Description = "領域、種類、キーワードに従って、容量、checkbox、カウンター、時間範囲の操作項目を取得する。building_control action=state_list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/activation-ranges{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "開始・停止のしきい値設定",
                                        Description = "領域とキーワードに従って ActiveRangeSideScreen / IActivationRangeTarget の2つのしきい値の操作項目を取得する。building_control domain=side_surface surface=activation action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/progress-bars{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "サイドパネルの進捗バー",
                                        Description = "領域とキーワードに従って ProgressBarSideScreen / IProgressBarSideScreen の読み取り専用の進捗バーの状態を取得する。building_control domain=side_surface kind=progress action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/buttons{?areaId,x1,y1,x2,y2,worldId,query,interactableOnly,limit}",
                                        Name = "building_control",
                                        Title = "サイドパネルの汎用ボタン",
                                        Description = "領域とキーワードに従って ISidescreenButtonControl の汎用サイドパネルボタンを取得する。building_control domain=side_surface kind=button action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/user-menu-actions{?areaId,x1,y1,x2,y2,worldId,category,query,limit}",
                                        Name = "building_control",
                                        Title = "オブジェクトのユーザーメニュー操作",
                                        Description = "領域、分類、キーワードに従って、オブジェクトの UserMenu/context-menu ボタンの対応表を取得する。building_control domain=side_surface surface=user_menu action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/maintenance-actions{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "メンテナンス用ユーザーメニュー操作",
                                        Description = "building_control domain=side_surface surface=maintenance action=list で、領域とキーワードに従って、状態機械やスロット引数が必要なプレイヤーのメンテナンス操作を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/checklists{?areaId,x1,y1,x2,y2,worldId,query,checkedOnly,enabledOnly,limit}",
                                        Name = "building_control",
                                        Title = "サイドパネルのチェックリスト",
                                        Description = "領域とキーワードに従って ICheckboxListGroupControl のストーリータスク、条件、設備のチェックリストを取得する。building_control domain=side_surface kind=checklist action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/related-entities{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "関連オブジェクト",
                                        Description = "領域とキーワードに従って IRelatedEntities のサイドパネルの関連オブジェクトと、クリックで移動できる対象を取得する。building_control domain=side_surface kind=related action=list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://controls/n-toggles{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "サイドパネルの複数選択項目",
                                        Description = "building_control domain=side_surface surface=misc kind=n_toggle action=list で、領域とキーワードに従って INToggleSideScreenControl の複数選択項目を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://automation/logic-alarms{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "自動化通知",
                                        Description = "building_control domain=side_surface surface=misc kind=logic_alarm action=list で、領域とキーワードに従って Logic Alarm の通知設定を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://automation/comet-detectors{?areaId,x1,y1,x2,y2,worldId,query,includeOptions,limit}",
                                        Name = "building_control",
                                        Title = "宇宙スキャナー",
                                        Description = "building_control domain=space_building kind=comet_detector action=list で、領域とキーワードに従って宇宙スキャナーの現在の対象と選択可能な対象を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://automation/cluster-location-sensors{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "星図ロケーションセンサー",
                                        Description = "building_control domain=space_building kind=cluster_location_sensor action=list で、領域とキーワードに従って星図ロケーションセンサーのフィルター設定を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/railguns{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "星間ランチャー",
                                        Description = "building_control domain=space_building kind=railgun action=list で、領域とキーワードに従って星間ランチャーの発射質量、在庫、エネルギーの状態を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://buildings/turbo-heaters{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "液体ヒーターのターボモード",
                                        Description = "building_control domain=side_surface surface=misc kind=turbo_heater action=list で、領域とキーワードに従って Liquid Tepidizer の TurboModeSideScreen を取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://rockets/self-destruct{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "ロケットの自爆",
                                        Description = "building_control domain=rocket rocketDomain=self_destruct action=list で、領域とキーワードに従って自爆可能なロケットモジュールを取得する。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://production/fabricators{?areaId,x1,y1,x2,y2,worldId,query,queuedOnly,includeRecipes,limit}",
                                        Name = "building_control",
                                        Title = "製造設備",
                                        Description = "領域とキーワードに従って製造設備のキュー、注文、稼働状態を取得する。building_control domain=production action=list_fabricators と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://production/recipes{?id,x,y,areaId,x1,y1,x2,y2,worldId,recipeId,categoryId,query,queuedOnly,includeLocked,limit}",
                                        Name = "building_control",
                                        Title = "製造レシピ",
                                        Description = "製造設備、領域、キーワードに従って、レシピの素材、生産物、キュー内の数量を取得する。building_control domain=production action=list_recipes と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://production/mutant-seed-controls{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "変異種子の受け入れ設定",
                                        Description = "領域とキーワードに従って、変異種子の受け入れ・拒否を設定するユーザーメニューを取得する。building_control domain=production action=mutant_seed_list と同等。",
                                        MimeType = "application/json"
                                    },
                new McpResourceTemplateInfo
                                    {
                                        UriTemplate = "oni://production/configurable-consumers{?areaId,x1,y1,x2,y2,worldId,query,limit}",
                                        Name = "building_control",
                                        Title = "消費対象を設定できる設備",
                                        Description = "building_control domain=side_surface surface=misc kind=configurable_consumer action=list で、領域とキーワードに従って ConfigureConsumerSideScreen の選択肢を取得する。",
                                        MimeType = "application/json"
                                    }
            });
        }
    }
}
