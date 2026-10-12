using System;
using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    public static partial class ToolCoverageTools
    {
        private static List<CoverageRow> BuildRows()
        {
            return new List<CoverageRow>
            {
                Row("game", "pause_resume_speed_sandbox_save_load_quit_dlc", "一時停止、再開、速度変更、サンドボックスモード切り替え、セーブ一覧、保存/名前を付けて保存、確認付きのセーブ読み込み、メインメニューまたはデスクトップへの終了、現在のセーブで変更可能な DLC の参照と有効化", "covered", "game_control domain=speed", "game_control domain=speed action=time", "game_control domain=state", "game_control", "game_control domain=save", "game_control domain=dlc"),
                Row("camera", "camera_navigation", "移動/フォーカス/ワールド切り替え/ビュー切り替え/スクリーンショット", "covered", "navigation_control"),
                Row("world", "inspect_world_cells", "セルの調査、元素の統計、テキストマップ", "covered", "read_control domain=world action=cell_info", "read_control domain=world action=element_summary", "read_control domain=world action=text_map"),
                Row("areas", "area_handles", "マップ範囲ハンドルの定義、参照、一覧表示、分割、連結、破棄", "covered", "read_control domain=area"),
                Row("orders", "dig_sweep_mop_disinfect_attack_priority", "採掘、掃除、モップがけ、消毒、キャンセル、収穫、攻撃、範囲の優先度", "covered", "orders_control domain=area", "orders_control domain=designation action=attack", "orders_control domain=priority"),
                Row("orders", "cancel_orders", "建築/採掘/掃除/収穫/攻撃/捕獲などの作業のキャンセル", "covered", "read_control domain=world action=text_map", "orders_control domain=area action=cancel"),
                Row("buildings", "build_plan_place", "建築可能な設備、素材、設備の外観を検索し、building_control planning で設計図をプレビューおよび配置", "covered", "building_control domain=planning"),
                Row("buildings", "deconstruct_priority_conduit_jobs", "設備の優先度設定、設備や配管の解体、配管やコンベアレールを空にする操作", "covered", "read_control domain=buildings action=list", "building_control domain=config", "orders_control domain=priority", "orders_control domain=designation action=deconstruct", "orders_control domain=designation action=cut_conduits", "orders_control domain=designation action=empty_conduits"),
                Row("buildings", "building_toggles_sliders_copy_settings", "設備の有効化、プレイヤーによる手動切り替え、汎用 slider、しきい値センサー、開始/停止の二つのしきい値、向き/少数の選択肢、容量、checkbox、照明の色、Pixel Pack の色、ドアの状態、通行権限、補給しきい値、貯蔵/元素/ツリー形式のフィルター、収納タイルの単一アイテム指定、汎用エンティティスロットの要求/取り外し（特殊なロケット貨物室を含む）、自動化運搬のみ許可、バルブ、タイマー、ribbon bit、設定のコピー、制限付き一括設定", "covered", "building_control domain=config", "building_control domain=side_surface surface=activation", "building_control domain=side_surface surface=option", "building_control domain=config", "building_control domain=config action=visual kind=light", "building_control domain=config action=visual kind=pixel_pack", "orders_control domain=designation action=manual_delivery", "building_control domain=storage", "building_control domain=filter", "building_control domain=tile_selection", "building_control domain=receptacle", "building_control domain=side_surface surface=automation"),
                Row("buildings", "geotuner_target_assignment", "GeoTuner の対象間欠泉の参照、選択可能な間欠泉の一覧、次の対象間欠泉の解除と割り当て", "covered", "building_control domain=side_surface surface=geo_tuner"),
                Row("buildings", "art_and_monument_facades", "芸術設備の外観選択、リセットして作り直し、モニュメント部品の外観と反転", "covered", "building_control domain=special kind=artable", "building_control domain=special kind=monument_part"),
                Row("buildings", "gene_shuffler_operations", "Gene Shuffler の完了ボタン、充電要求、充電キャンセル", "covered", "building_control domain=special kind=gene_shuffler"),
                Row("buildings", "liquid_heater_turbo_mode", "Liquid Tepidizer のターボモード切り替えと消費電力の状態", "covered", "building_control domain=side_surface surface=misc kind=turbo_heater"),
                Row("buildings", "remote_work_terminal_dock_selection", "RemoteWorkTerminalSidescreen の遠隔作業 dock 選択、選択解除、選択可能な dock 一覧", "covered", "building_control domain=story_facility kind=remote_work_terminal"),
                Row("buildings", "dispenser_operations", "DispenserSideScreen / IDispenser: 配布可能なアイテムの参照、アイテム選択、配布要求、配布キャンセル", "covered", "building_control domain=side_surface surface=facility"),
                Row("buildings", "suit_locker_configuration", "SuitLockerSideScreen: 初期設定、スーツ要求、要求キャンセル/スーツ不要への設定、保管済み装備の排出", "covered", "building_control domain=side_surface surface=facility"),
                Row("controls", "generic_sidescreen_buttons", "ISidescreenButtonControl の汎用ボタン: Studyable、Activatable、ExcavateButton、CryoTank、GeothermalController、POI のアンロックなど", "covered", "building_control"),
                Row("controls", "generic_user_menu_actions", "オブジェクトの UserMenu/context-menu ボタン: 建築/採掘/モップがけのキャンセル、移動/掃除、自動消毒、修理、堆肥化、空にする操作、排出、間引き、彫刻、解体、元素の放出、スーツ検問所の通行、Tinker などに対応付けられた操作", "covered", "building_control domain=side_surface surface=user_menu"),
                Row("controls", "focused_maintenance_user_menu_actions", "状態機械/スロットのパラメーターを持つ UserMenu 操作: トイレの早期清掃、淡水化装置を早期に空にする操作、移動チューブ入口のワックス有効化/キャンセル、ハチの巣を空にする操作/キャンセル、貨物室の荷下ろし、複製人間のスロットごとの装備解除", "covered", "building_control domain=side_surface surface=maintenance"),
                Row("controls", "generic_sidescreen_checklists", "ICheckboxListGroupControl の読み取り専用サイドパネル一覧: ストーリータスク、化石の発掘、地熱設備、孤独な複製人間の家などの条件/進捗一覧", "covered", "building_control"),
                Row("controls", "generic_sidescreen_progress_bars", "ProgressBarSideScreen / IProgressBarSideScreen の読み取り専用進捗バー: タイトル、ラベル、tooltip、最大値、充足率の参照", "covered", "building_control"),
                Row("controls", "related_entities_navigation", "RelatedEntitiesSideScreen / IRelatedEntities: 関連オブジェクトの一覧を参照し、プレイヤーが関連行をクリックしたときの選択とカメラのフォーカスを実行", "covered", "building_control"),
                Row("controls", "generic_n_toggle_controls", "INToggleSideScreenControl の複数選択肢を持つサイドパネルコントロール: 選択肢、現在/予約中の状態、選択の予約", "covered", "building_control domain=side_surface surface=misc kind=n_toggle"),
                Row("automation", "space_detector_targets", "宇宙スキャナー/Space Scanner の対象選択: 隕石、プレイヤーの発射物、指定したロケット", "covered", "building_control domain=space_building kind=comet_detector"),
                Row("automation", "cluster_location_sensor_filters", "星図ロケーションセンサーのフィルター: 空の宇宙、指定した星図座標/小惑星/POI", "covered", "building_control domain=space_building kind=cluster_location_sensor"),
                Row("automation", "logic_alarm_notifications", "Logic Alarm の通知名、通知文、通知の種類、作動時の一時停止、作動時のカメラ移動", "covered", "building_control domain=side_surface surface=misc kind=logic_alarm"),
                Row("production", "fabricator_recipe_queue", "製造設備/精錬/調理/製薬/衣服/粉砕/窯などの ComplexFabricator のレシピ参照、素材の種類に応じた recipeId の選択、予約、一括予約、予約の消去、無限製造、変異種子の設定", "covered", "building_control domain=production"),
                Row("production", "mutant_seed_acceptance_controls", "プレイヤーメニューの変異種子の受け入れ/拒否切り替え: ComplexFabricator、FishFeeder、SpiceGrinder", "covered", "building_control domain=production action=mutant_seed_list", "building_control domain=production action=mutant_seed_set"),
                Row("production", "configurable_consumer_options", "ConfigureConsumerSideScreen の選択式消費設備: 現在の選択肢と素材の必要量の参照、選択肢の切り替え", "covered", "building_control domain=side_surface surface=misc kind=configurable_consumer"),
                Row("storage", "storage_filters", "収納庫、StorageTile の単一アイテム選択、TreeFilterable/FlatTagFilterable のフィルター", "covered", "building_control domain=storage", "building_control domain=tile_selection", "building_control domain=filter"),
                Row("dupes", "duplicant_info_and_names", "複製人間の一覧、属性、欲求、改名", "covered", "dupes_control domain=info", "dupes_control domain=command action=rename", "dupes_control domain=command action=auto_rename"),
                Row("dupes", "duplicant_direct_commands", "ここへ移動の単体/一括指示、強制アクション、割り当て可能な対象、装備/Ownables スロットの選択、現在の装備の取り外し、スキルポイントの割り当て、個人の作業優先度、帽子、直接指示のエントリー", "covered", "dupes_control domain=side_screen", "dupes_control domain=command", "dupes_control domain=assignable", "building_control domain=side_surface surface=maintenance", "dupes_control domain=skill", "dupes_control domain=hat", "dupes_control domain=priority"),
                Row("dupes", "bionic_upgrade_slots", "BionicSideScreen のバイオニック複製人間のアップグレードスロットの参照、ロック/空/割り当て済み/装着済みの状態。スロットの割り当て/解除は dupes_control domain=assignable action=set_slot が対応", "covered", "dupes_control domain=side_screen", "dupes_control domain=assignable"),
                Row("dupes", "minion_todo_side_screen", "MinionTodoSideScreen の現在の作業、実行可能な作業、阻害されている作業、優先度、対象、現在のスケジュールブロック", "covered", "dupes_control domain=side_screen"),
                Row("schedules", "schedule_management", "スケジュール作成、ブロック変更、複製人間の割り当て", "covered", "colony_control domain=management kind=schedule"),
                Row("diet", "consumable_permissions", "食事/消費可能アイテムの許可", "covered", "colony_control domain=management kind=diet"),
                Row("research", "research_management", "研究キューの参照/検索/設定/キャンセル", "covered", "colony_control domain=management kind=research action=status", "colony_control domain=management kind=research action=list", "colony_control domain=management kind=research"),
                Row("space", "telescope_starmap_analysis", "TelescopeSideScreen の星図表示、星図上の目的地の分析対象の参照/設定/解除", "covered", "building_control domain=space_story"),
                Row("rockets", "rocket_operations", "ロケットの状態、目的地、往復/片道、発射、発射キャンセル、発射台への着陸/着陸キャンセル、操縦席の制限、ロケット内部設備に対する制御装置の制限の有効/無効、乗組員の招集/解除、割り当てグループの複製人間ごとの所属切り替え、隕石ブラスターの弾薬選択", "covered", "building_control domain=rocket rocketDomain=ops", "building_control domain=rocket rocketDomain=restriction", "building_control domain=rocket rocketDomain=usage", "building_control domain=rocket rocketDomain=crew_request", "building_control domain=rocket rocketDomain=assignment_group", "building_control domain=special kind=missile_launcher"),
                Row("rockets", "rocket_module_reordering", "ロケットモジュールの追加、置換、上下移動、取り外し指定、取り外しキャンセル", "covered", "building_control domain=rocket rocketDomain=module"),
                Row("rockets", "rocket_flight_utility_modules", "ModuleFlightUtilitySideScreen: 飛行用モジュールを空にする操作/投下、自動投下、星図上の対象選択、複製人間の選択", "covered", "building_control domain=rocket rocketDomain=flight_utility"),
                Row("rockets", "rocket_cargo_and_harvest_progress", "CargoModuleSideScreen/HarvestModuleSideScreen: 星図上の貨物室の収集進捗、容量、宇宙採掘、ダイヤモンドの在庫", "covered", "building_control domain=rocket rocketDomain=cargo_status action=collectors", "building_control domain=rocket rocketDomain=cargo_status action=harvest_modules"),
                Row("rockets", "railgun_launch_mass", "星間ランチャーの発射質量の slider/数値入力、在庫、RAD ボルトのエネルギー状態", "covered", "building_control domain=space_building kind=railgun"),
                Row("rockets", "rocket_self_destruct", "航行中のロケットの SelfDestructButtonSideScreen による自爆操作。高リスク操作の確認後に実行", "covered", "building_control domain=rocket rocketDomain=self_destruct"),
                Row("resources", "inventory_and_reports", "資源、食料、AllResourcesScreen の固定/通知切り替え、コロニーのレポート、診断設定", "covered", "read_control domain=resources", "colony_control", "colony_control"),
                Row("ui", "notifications_and_markers", "通知の参照、クリックによるフォーカス、dismiss、フローティングテキスト、マップマーカー", "covered", "colony_control", "game_control domain=ui uiDomain=feedback"),
                Row("ui", "management_screens", "管理パネルの表示/切り替え、オーバーレイ、データベース、検索、建築カテゴリの UI エントリー", "covered", "game_control domain=ui uiDomain=action", "navigation_control", "read_control domain=database action=query", "dupes_control domain=priority", "dupes_control domain=hat"),
                Row("automation", "logic_and_power_controls", "自動化スイッチ、電源スイッチ、信号のしきい値、元素フィルター/センサー、論理信号の送受信チャンネル、カウンター、Critter Sensor の動物/卵の計数、時間範囲、フィルター/バッファーの遅延、タイマー、ribbon bit、バルブ、自動化通知、星図センサー、宇宙スキャナー、自動化専用の運搬、論理ポートの状態、制限付き一括操作", "covered", "building_control domain=config", "building_control domain=filter", "building_control domain=side_surface surface=option", "building_control domain=config", "building_control domain=side_surface surface=automation", "building_control domain=side_surface surface=misc kind=logic_alarm", "building_control domain=space_building kind=cluster_location_sensor", "building_control domain=space_building kind=comet_detector"),
                Row("ranching", "critter_and_egg_operations", "動物の一覧、捕獲、解放、解放地点のフィルターと容量、孵卵器の卵要求と連続孵化、動物のおびき寄せ設備、飼育場/卵/魚に関する操作", "covered", "colony_control domain=bio bioDomain=ranching kind=critters action=critters", "orders_control domain=designation action=capture", "colony_control domain=bio bioDomain=ranching kind=dropoff", "colony_control domain=bio bioDomain=ranching kind=incubator", "building_control domain=special kind=creature_lure", "orders_control domain=designation action=attack"),
                Row("farming", "harvest_and_planting", "種子のカタログ、収穫状態、自動収穫、範囲収穫、掘り起こし、単体/一括の植え付け選択と植え付け要求", "covered", "colony_control domain=bio bioDomain=farming action=seed_catalog", "colony_control domain=bio bioDomain=farming", "orders_control domain=area action=harvest"),
                Row("farming", "genetic_analysis_seed_permissions", "GeneticAnalysisStationSideScreen / Botanical Analyzer の変異種子の分析の許可/禁止", "covered", "building_control domain=story_facility kind=genetic_analysis_station"),
                Row("medical", "medical_and_care_assignments", "患者一覧、病床、医療ベッドの単体/一括のしきい値、病床の割り当て、医務室の薬品/治療可能な病気、診療、介護、製薬に関する割り当て", "covered", "colony_control domain=management kind=medical", "dupes_control domain=assignable", "orders_control domain=designation action=manual_delivery"),
                Row("combat", "combat_targeting", "攻撃指定、攻撃キャンセル、優先度", "covered", "read_control domain=world action=text_map", "colony_control domain=bio bioDomain=ranching kind=critters action=critters", "orders_control domain=designation action=attack"),
                Row("story", "printerceptor_operations", "PrinterceptorSideScreen のプリント選択画面表示、製造ポッド候補の傍受、databank/充電状態", "covered", "building_control domain=story_facility"),
                Row("story", "poi_tech_unlock_portals", "Research Portal/研究用ポータル: アンロック作業、進捗、アンロックされる POI の研究項目の参照、アンロック研究の開始またはキャンセル", "covered", "building_control domain=story_facility"),
                Row("story", "lore_bearer_reading", "LoreBearerSideScreen の読む/調べるボタン、既読状態、tooltip、ポップアップの表示", "covered", "building_control domain=side_surface surface=facility"),
                Row("story", "telepad_side_screen", "TelepadSideScreen の移民カウントダウンの参照、移民選択/コロニー概要/スキル/研究画面の表示、勝利条件の状態", "covered", "building_control domain=side_surface surface=facility"),
                Row("story", "artifact_analysis_display", "ArtifactAnalysisSideScreen の分析済み artifact 一覧、分析設備の状態、マップ上の artifact、reveal/lore のポップアップ", "covered", "building_control domain=side_surface surface=facility"),
                Row("story", "warp_portal_side_screen", "WarpPortalSideScreen の複製人間を待ってからの転送開始、割り当て/転送準備のキャンセル、クールダウン状態の参照", "covered", "building_control domain=space_story"),
                Row("story", "temporal_tear_side_screen", "TemporalTearSideScreen の裂け目の開放/消費状態の参照、二重確認後の現在位置のロケットの消費", "covered", "building_control domain=space_story"),
                Row("diagnostics", "generic_process_conditions", "ConditionListSideScreen / IProcessConditionSet の汎用条件状態の参照。ロケットの発射/貯蔵/飛行条件を含む", "covered", "building_control domain=space_story"),
                Row("sandbox", "sandbox_tools", "サンドボックスのブラシ、塗りつぶし、サンプリング、生成、床上アイテム削除、動物削除、マップ開示、温度、ストレス、ストーリー特性の配置、Debug AutoPlumber/InstantBuild 操作", "covered", "game_control", "game_control", "game_control")
            };
        }

        private static CoverageRow Row(string group, string operation, string playerSurface, string status, params string[] tools)
        {
            return new CoverageRow
            {
                Group = group,
                Operation = operation,
                PlayerSurface = playerSurface,
                DeclaredStatus = status,
                Tools = tools.ToList()
            };
        }

        private static Dictionary<string, object> BuildHotkeySummary(HashSet<string> toolNames)
        {
            var actionNames = Enum.GetNames(typeof(global::Action))
                .Where(name => name != "NumActions")
                .OrderBy(name => name)
                .ToList();
            var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "AlternateView", "Attack", "BuildingCancel", "BuildingDeconstruct", "CameraHome",
                "Capture", "Clear", "DebugReportBug", "Dig", "Disconnect", "EmptyPipe", "Escape",
                "Harvest", "Help", "Mop", "PanDown", "PanLeft", "PanRight", "PanUp", "Prioritize",
                "RotateBuilding", "SlowDown", "SpeedUp", "TogglePause", "ZoomIn", "ZoomOut"
            };

            return new Dictionary<string, object>
            {
                ["enumCount"] = actionNames.Count,
                ["mappedExamples"] = covered.Where(actionNames.Contains).OrderBy(name => name).ToList(),
                ["unmappedCount"] = actionNames.Count(name => !covered.Contains(name)),
                ["unmappedExamples"] = actionNames.Where(name => !covered.Contains(name)).Take(80).ToList(),
                ["note"] = "Action 列挙型はキー割り当てや UI のエントリーであり、プレイヤー操作の意味全体とは一致しない。インターフェースの不足を補うための主要な監査一覧には operationSurfaces を使用する。"
            };
        }

        private static int StatusRank(string status)
        {
            switch (status)
            {
                case "missing": return 0;
                case "partial": return 1;
                case "covered": return 2;
                default: return 3;
            }
        }

        private static string NormalizeCoverageDetail(string value)
        {
            string detail = string.IsNullOrWhiteSpace(value) ? "compact" : value.Trim().ToLowerInvariant();
            if (detail == "brief" || detail == "full")
                return detail;
            return "compact";
        }

        private static int CoverageScore(CoverageRow row, string query)
        {
            if (string.IsNullOrEmpty(query))
                return 1;

            string haystack = string.Join(" ", new[]
            {
                row.Group ?? "",
                row.Operation ?? "",
                row.PlayerSurface ?? "",
                string.Join(" ", row.Tools ?? new List<string>()),
                string.Join(" ", (row.ResourceAnchors ?? new List<Dictionary<string, object>>())
                    .SelectMany(anchor => AnchorUris(anchor)))
            }).ToLowerInvariant();

            int score = 0;
            foreach (string token in ExpandCoverageQuery(query).Split(new[] { ' ', '\t', '\r', '\n', '_', '-', ',', '.', '/', ':', '，', '。', '、' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length <= 1)
                    continue;
                if (haystack.Contains(token))
                    score++;
            }
            return score;
        }

        private static IEnumerable<string> AnchorUris(Dictionary<string, object> anchor)
        {
            object uris;
            if (anchor == null || !anchor.TryGetValue("uris", out uris))
                return new string[0];

            var strings = uris as IEnumerable<string>;
            if (strings != null)
                return strings;

            var objects = uris as IEnumerable<object>;
            return objects != null ? objects.Select(item => item?.ToString() ?? "") : new string[0];
        }

        private static string ExpandCoverageQuery(string query)
        {
            var tokens = new List<string>();
            foreach (string token in query.Split(new[] { ' ', '\t', '\r', '\n', '_', '-', ',', '.', '/', ':', '，', '。', '、' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string normalized = token.ToLowerInvariant();
                tokens.Add(normalized);
                switch (normalized)
                {
                    case "挖":
                    case "挖掘":
                        tokens.AddRange(new[] { "dig", "orders", "area" });
                        break;
                    case "清扫":
                    case "打扫":
                        tokens.AddRange(new[] { "sweep", "clear", "storage", "orders" });
                        break;
                    case "建造":
                    case "建筑":
                        tokens.AddRange(new[] { "build", "building", "buildings", "plan" });
                        break;
                    case "复制人":
                    case "小人":
                        tokens.AddRange(new[] { "dupe", "duplicant", "dupes", "assign" });
                        break;
                    case "火箭":
                    case "太空":
                        tokens.AddRange(new[] { "rocket", "rockets", "space", "launch" });
                        break;
                    case "自动化":
                    case "逻辑":
                        tokens.AddRange(new[] { "automation", "logic", "sensor" });
                        break;
                    case "种植":
                    case "农场":
                        tokens.AddRange(new[] { "farming", "planting", "harvest" });
                        break;
                    case "小动物":
                    case "牧场":
                        tokens.AddRange(new[] { "ranching", "critters", "incubator" });
                        break;
                    case "地图":
                    case "地形":
                        tokens.AddRange(new[] { "world", "map", "cell", "area" });
                        break;
                }
            }
            return string.Join(" ", tokens.Distinct().ToArray());
        }

        private static bool HasConfirmParameter(McpTool tool)
        {
            return tool.Parameters != null && tool.Parameters.ContainsKey("confirm");
        }

        private static bool IsPlayerFacingTool(McpTool tool)
        {
            if (tool.Hidden)
                return false;
            switch (tool.Group)
            {
                case "tools":
                case "server":
                case "database":
                    return false;
                default:
                    return true;
            }
        }

        private static bool IsGameStateReadTool(McpTool tool)
        {
            switch (tool.Group)
            {
                case "tools":
                case "server":
                case "database":
                default:
                    return true;
            }
        }

        private static Dictionary<string, List<string>> BuildResourceUriIndex()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var info in OniResourceRegistry.GetResourceInfos())
                AddResourceUri(result, info.Name, info.Uri);
            foreach (var info in OniResourceRegistry.GetResourceTemplateInfos())
                AddResourceUri(result, info.Name, info.UriTemplate);
            return result;
        }

        private static void AddResourceUri(Dictionary<string, List<string>> index, string name, string uri)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uri))
                return;

            List<string> uris;
            if (!index.TryGetValue(name, out uris))
            {
                uris = new List<string>();
                index[name] = uris;
            }
            if (!uris.Contains(uri))
                uris.Add(uri);
        }

        private static List<Dictionary<string, object>> ResourceAnchorsForRow(CoverageRow row, List<McpTool> tools, Dictionary<string, List<string>> resourceUrisByName, bool hasGenericReadResource)
        {
            var toolByName = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
            var anchors = new List<Dictionary<string, object>>();
            foreach (string toolName in row.Tools)
            {
                McpTool tool;
                if (!toolByName.TryGetValue(toolName, out tool))
                    continue;

                List<string> uris;
                if (resourceUrisByName.TryGetValue(tool.Name, out uris))
                {
                    anchors.Add(new Dictionary<string, object>
                    {
                        ["tool"] = tool.Name,
                        ["kind"] = "semantic",
                        ["uris"] = uris
                    });
                    continue;
                }

                if (hasGenericReadResource && string.Equals(tool.Mode, "read", StringComparison.OrdinalIgnoreCase))
                {
                    anchors.Add(new Dictionary<string, object>
                    {
                        ["tool"] = tool.Name,
                        ["kind"] = "generic_read",
                        ["uris"] = new[] { "oni://tools/read/" + tool.Name + "{?...}" }
                    });
                }
            }

            return anchors;
        }

        private static Dictionary<string, object> Issue(string code, string message, object detail)
        {
            var issue = new Dictionary<string, object>
            {
                ["code"] = code,
                ["message"] = message
            };
            if (detail != null)
                issue["detail"] = detail;
            return issue;
        }

        private class CoverageRow
        {
            public string Group { get; set; }
            public string Operation { get; set; }
            public string PlayerSurface { get; set; }
            public string DeclaredStatus { get; set; }
            public string Status { get; set; }
            public List<string> Tools { get; set; }
            public List<string> MissingTools { get; set; }
            public List<Dictionary<string, object>> ResourceAnchors { get; set; }

            public CoverageRow WithRuntimeStatus(HashSet<string> toolNames)
            {
                MissingTools = Tools.Where(tool => !toolNames.Contains(SurfaceAuditUtil.ToolNameOnly(tool))).ToList();
                if (DeclaredStatus == "missing")
                    Status = "missing";
                else if (MissingTools.Count == 0 && Tools.Count > 0)
                    Status = DeclaredStatus;
                else if (MissingTools.Count < Tools.Count)
                    Status = "partial";
                else
                    Status = "missing";
                return this;
            }

            public Dictionary<string, object> ToDictionary(string detail, bool includeResources, int score)
            {
                if (detail == "brief")
                {
                    var brief = new Dictionary<string, object>
                    {
                        ["g"] = Group,
                        ["op"] = Operation,
                        ["status"] = Status,
                        ["tools"] = Tools,
                        ["score"] = score
                    };
                    if (includeResources)
                        brief["resources"] = ResourceAnchors ?? new List<Dictionary<string, object>>();
                    return brief;
                }

                var result = new Dictionary<string, object>
                {
                    ["group"] = Group,
                    ["operation"] = Operation,
                    ["playerSurface"] = PlayerSurface,
                    ["status"] = Status,
                    ["tools"] = Tools,
                    ["score"] = score
                };
                if (detail == "full")
                    result["missingTools"] = MissingTools;
                if (includeResources)
                    result["resourceAnchors"] = ResourceAnchors ?? new List<Dictionary<string, object>>();
                return result;
            }
        }
    }
}
