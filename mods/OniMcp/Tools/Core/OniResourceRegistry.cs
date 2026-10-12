using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    /// <summary>
    /// Maps stable MCP resource URIs to live ONI state snapshots.
    /// </summary>
    public static partial class OniResourceRegistry
    {
        public static List<McpResourceInfo> GetResourceInfos()
        {
            return _resources
                .Select(resource => resource.Info)
                .OrderBy(resource => resource.Uri)
                .ToList();
        }

        public static List<McpResourceTemplateInfo> GetResourceTemplateInfos()
        {
            return _templates.OrderBy(template => template.UriTemplate).ToList();
        }

        public static ReadResourceResult ReadResource(string uri)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.Scheme != "oni")
                return null;

            string resourceUri = parsed.GetLeftPart(UriPartial.Path).TrimEnd('/');
            var resource = _resources.FirstOrDefault(item => item.Info.Uri.TrimEnd('/') == resourceUri);
            if (resource != null)
            {
                var arguments = ParseQuery(parsed.Query);
                if (resource.Arguments != null)
                {
                    foreach (var property in resource.Arguments.Properties())
                        arguments[property.Name] = property.Value.DeepClone();
                }
                return ReadToolResource(uri, resource.ToolName, arguments, resource.Info.MimeType);
            }

            return ReadDynamicResource(uri);
        }



        private static ReadResourceResult ErrorResource(string uri, string message)
        {
            return new ReadResourceResult
            {
                Contents = new List<TextResourceContent>
                {
                    new TextResourceContent
                    {
                        Uri = uri,
                        MimeType = "application/json",
                        Text = JsonConvert.SerializeObject(new Dictionary<string, object>
                        {
                            ["error"] = true,
                            ["message"] = message
                        }, McpJsonUtil.Settings)
                    }
                }
            };
        }

        private static ReadResourceResult ReadToolResource(string uri, string toolName, JObject arguments, string mimeType)
        {
            NormalizeResourceArguments(toolName, arguments);
            if (!OniToolRegistry.TryGetOperation(toolName, out var operation))
                return ErrorResource(uri, "Resource operation not found: " + toolName);

            CallToolResult result;
            try
            {
                // Resource routes select their read operation. resources/read has no
                // tools/call task description and must not drain tool notifications.
                result = operation.Handler(arguments ?? new JObject());
            }
            catch (Exception ex)
            {
                return ErrorResource(uri, "Resource operation error: " + ex.Message);
            }

            if (result == null)
                return ErrorResource(uri, "Resource operation returned no result.");
            string text = ExtractText(result);
            if (result.IsError)
                return ErrorResource(uri, text);

            return new ReadResourceResult
            {
                Contents = new List<TextResourceContent>
                {
                    new TextResourceContent
                    {
                        Uri = uri,
                        MimeType = mimeType,
                        Text = text
                    }
                }
            };
        }

        private static void NormalizeResourceArguments(string toolName, JObject arguments)
        {
            if (arguments == null)
                return;

            NormalizeColonyArguments(toolName, arguments);
            NormalizeBuildingArguments(toolName, arguments);
            NormalizeGameArguments(toolName, arguments);
        }

        private static void NormalizeGameArguments(string toolName, JObject arguments)
        {
            if (!string.Equals(toolName, "game_control", StringComparison.OrdinalIgnoreCase))
                return;

            string domain = (arguments["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            switch (domain)
            {
                case "action":
                case "ui_action":
                case "actions":
                case "feedback":
                case "hint":
                case "hints":
                    arguments["uiDomain"] = domain;
                    arguments["domain"] = "ui";
                    return;
            }
        }

        private static void NormalizeColonyArguments(string toolName, JObject arguments)
        {
            if (!string.Equals(toolName, "colony_control", StringComparison.OrdinalIgnoreCase))
                return;

            string domain = (arguments["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            if (domain == "farming" || domain == "ranching")
            {
                arguments["kind"] = domain;
                arguments["domain"] = "bio";
            }
        }

        private static void NormalizeBuildingArguments(string toolName, JObject arguments)
        {
            if (!string.Equals(toolName, "building_control", StringComparison.OrdinalIgnoreCase))
                return;

            string domain = (arguments["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            if (IsSideSurfaceDomain(domain))
            {
                arguments["surface"] = domain;
                arguments["domain"] = "side_surface";
                return;
            }

            if (IsRocketDomain(domain))
            {
                arguments["rocketDomain"] = domain;
                arguments["domain"] = "rocket";
                return;
            }

            if (string.IsNullOrEmpty(domain))
            {
                string kind = (arguments["kind"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                if (IsGenericSideSurfaceKind(kind))
                    arguments["domain"] = "side_surface";
            }
        }

        private static bool IsSideSurfaceDomain(string domain)
        {
            switch (domain)
            {
                case "generic":
                case "option":
                case "activation":
                case "automation":
                case "facility":
                case "misc":
                case "geo_tuner":
                case "user_menu":
                case "maintenance":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsGenericSideSurfaceKind(string kind)
        {
            switch (kind)
            {
                case "button":
                case "buttons":
                case "checklist":
                case "checklists":
                case "progress":
                case "progress_bar":
                case "progress_bars":
                case "related":
                case "related_entity":
                case "related_entities":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsRocketDomain(string domain)
        {
            switch (domain)
            {
                case "ops":
                case "module":
                case "flight_utility":
                case "restriction":
                case "usage":
                case "crew_request":
                case "assignment_group":
                case "cargo_status":
                case "self_destruct":
                    return true;
                default:
                    return false;
            }
        }

        private static string ExtractText(CallToolResult result)
        {
            if (result == null || result.Content == null)
                return "";
            return string.Join("\n", result.Content.Where(content => content != null).Select(content => content.Text ?? "").ToArray());
        }

        private static JObject ParseQuery(string query, bool allowOperationSelectors = false)
        {
            var result = new JObject();
            if (string.IsNullOrEmpty(query))
                return result;

            string trimmed = query[0] == '?' ? query.Substring(1) : query;
            foreach (var pair in trimmed.Split('&'))
            {
                if (string.IsNullOrEmpty(pair))
                    continue;

                var parts = pair.Split(new[] { '=' }, 2);
                string key = Uri.UnescapeDataString(parts[0]);
                if (string.IsNullOrEmpty(key))
                    continue;

                // URI routes own operation selectors. Query strings supply filters;
                // they cannot redirect a read to another domain or authorize writes.
                if (key == "confirm" || key == "force" || (!allowOperationSelectors &&
                    (key == "action" || key == "domain" || key == "uiDomain" ||
                     key == "rocketDomain" || key == "bioDomain")))
                    continue;

                string value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
                if (string.IsNullOrEmpty(value))
                    continue;

                result[key] = value;
            }

            return result;
        }

        private static OniResource Resource(string uri, string toolName, string title, string description, JObject arguments)
        {
            return new OniResource
            {
                Info = new McpResourceInfo
                {
                    Uri = uri,
                    Name = toolName,
                    Title = title,
                    Description = description,
                    MimeType = "application/json"
                },
                ToolName = toolName,
                Arguments = arguments
            };
        }

        private class OniResource
        {
            public McpResourceInfo Info { get; set; }
            public string ToolName { get; set; }
            public JObject Arguments { get; set; }
        }

        private static readonly List<OniResource> _resources = new List<OniResource>
                {
                    Resource("oni://colony/status", "colony_control", "コロニーの状態", "サイクル、複製人間の数、ワールド数、速度、一時停止の状態。", new JObject { ["domain"] = "read", ["action"] = "status" }),
                    Resource("oni://colony/diagnostics", "colony_control", "コロニーの診断", "酸素不足、食料不足、オーバーヒートなど、コロニーの診断結果。", new JObject { ["domain"] = "diagnostic", ["action"] = "diagnostics" }),
                    Resource("oni://colony/alerts", "colony_control", "コロニーの警報", "現在のゲームの警報と通知。", new JObject { ["domain"] = "diagnostic", ["action"] = "alerts" }),
                    Resource("oni://colony/diagnostic-settings", "colony_control", "コロニーの診断設定", "AllDiagnosticsScreen の診断表示モード、個別条件の有効状態、Debug 通知の無効状態。", new JObject { ["domain"] = "diagnostic", ["action"] = "list_settings" }),
                    Resource("oni://colony/report", "colony_control", "コロニーレポート", "コロニーのレポート。", new JObject { ["domain"] = "report", ["action"] = "report" }),
                    Resource("oni://colony/summary", "colony_control", "コロニーの概要", "行動計画に役立つコロニーの概要。", new JObject { ["domain"] = "report", ["action"] = "summary" }),
                    Resource("oni://colony/notifications", "colony_control", "通知一覧", "現在の HUD の NotificationScreen/NotificationManager の通知、メッセージ、フォーカス対象、消去可能な状態。", new JObject { ["domain"] = "notification", ["action"] = "list" }),
                    Resource("oni://world/list", "colony_control", "ワールド一覧", "読み込み済みのワールドと、現在アクティブなワールド。", new JObject { ["domain"] = "read", ["action"] = "worlds" }),
                    Resource("oni://world/elements", "read_control", "ワールド内の元素の概要", "現在のワールドにある元素の質量と温度の概要。", new JObject { ["domain"] = "world", ["action"] = "element_summary" }),
                    Resource("oni://camera/view", "navigation_control", "カメラビュー", "現在のカメラ位置、ズーム、アクティブなワールド、画面サイズ。", new JObject { ["action"] = "get_view" }),
                    Resource("oni://resources/inventory", "read_control", "資源の在庫", "資源の在庫の概要。", new JObject { ["domain"] = "resources", ["action"] = "inventory" }),
                    Resource("oni://resources/food", "read_control", "食料の在庫", "食料の在庫と鮮度の情報。", new JObject { ["domain"] = "resources", ["action"] = "food" }),
                    Resource("oni://resources/pins", "read_control", "資源パネルの固定表示と通知", "AllResourcesScreen の資源行の固定表示と通知のオン・オフ状態。", new JObject { ["domain"] = "resources", ["action"] = "pins" }),
                    Resource("oni://diet/status", "colony_control", "消費の許可", "Consumables 管理画面の、複製人間による食料・薬・電池の消費許可と在庫。", new JObject { ["domain"] = "management", ["kind"] = "diet", ["action"] = "status" }),
                    Resource("oni://storage/list", "building_control", "収納設備一覧", "収納設備とフィルターの一覧。", new JObject { ["domain"] = "storage", ["action"] = "list" }),
                    Resource("oni://storage/tile-selections", "building_control", "収納タイルの対象アイテム", "SingleItemSelectionSideScreen / StorageTile の対象アイテムと選択可能なアイテム。", new JObject { ["domain"] = "tile_selection", ["action"] = "list" }),
                    Resource("oni://filters/controls", "building_control", "フィルターの操作項目", "気体・液体・固体の単一選択フィルター、元素センサー、ツリー表示・タイル表示の複数選択フィルター。", new JObject { ["domain"] = "filter", ["action"] = "list" }),
                    Resource("oni://controls/options", "building_control", "サイドパネルの選択肢", "作業方向、少数の選択肢、自動化信号の送信チャンネル、放射線粒子の方向の操作項目。", new JObject { ["domain"] = "option", ["action"] = "list" }),
                    Resource("oni://controls/state", "building_control", "サイドパネルの状態設定", "容量上限、単一の checkbox、自動化カウンター、時間範囲センサーの操作項目。", new JObject { ["action"] = "state_list" }),
                    Resource("oni://controls/activation-ranges", "building_control", "開始・停止のしきい値設定", "ActiveRangeSideScreen / IActivationRangeTarget の2つのしきい値の操作項目。", new JObject { ["domain"] = "activation", ["action"] = "list" }),
                    Resource("oni://controls/progress-bars", "building_control", "サイドパネルの進捗バー", "ProgressBarSideScreen / IProgressBarSideScreen の読み取り専用の進捗バーの状態。", new JObject { ["kind"] = "progress", ["action"] = "list" }),
                    Resource("oni://controls/buttons", "building_control", "サイドパネルの汎用ボタン", "ISidescreenButtonControl を実装する汎用サイドパネルボタンの操作。", new JObject { ["kind"] = "button", ["action"] = "list" }),
                    Resource("oni://controls/user-menu-actions", "building_control", "オブジェクトのユーザーメニュー操作", "オブジェクトの UserMenu/context-menu ボタンの対応表。掃除、修理、堆肥化、内容物を空にする、彫刻など、サイドパネル以外の操作。", new JObject { ["domain"] = "user_menu", ["action"] = "list" }),
                    Resource("oni://controls/maintenance-actions", "building_control", "メンテナンス用ユーザーメニュー操作", "状態機械やスロット引数が必要なメンテナンス操作。トイレの清掃、淡水化装置を空にする、移動チューブのワックス塗り、巣箱や貨物室を空にする、複製人間の装備解除。", new JObject { ["domain"] = "maintenance", ["action"] = "list" }),
                    Resource("oni://controls/checklists", "building_control", "サイドパネルのチェックリスト", "ICheckboxListGroupControl を実装するストーリータスク、条件、設備のチェックリスト。", new JObject { ["kind"] = "checklist", ["action"] = "list" }),
                    Resource("oni://controls/related-entities", "building_control", "関連オブジェクト", "IRelatedEntities を実装するサイドパネルの関連オブジェクトと、クリックで移動できる対象。", new JObject { ["kind"] = "related", ["action"] = "list" }),
                    Resource("oni://controls/n-toggles", "building_control", "サイドパネルの複数選択項目", "INToggleSideScreenControl を実装するサイドパネルの複数選択項目。", new JObject { ["domain"] = "misc", ["kind"] = "n_toggle", ["action"] = "list" }),
                    Resource("oni://automation/logic-alarms", "building_control", "自動化通知", "Logic Alarm の通知名、説明、種類、一時停止、カメラ移動の設定。", new JObject { ["domain"] = "misc", ["kind"] = "logic_alarm", ["action"] = "list" }),
                    Resource("oni://automation/automatable", "building_control", "自動化による運搬の制限", "AutomatableSideScreen の、自動化のみ許可するか、手動運搬も許可するかの状態。", new JObject { ["domain"] = "automation", ["kind"] = "automatable", ["action"] = "list" }),
                    Resource("oni://automation/critter-sensors", "building_control", "動物センサー", "CritterSensorSideScreen の動物・卵の計数のオン・オフ、しきい値、現在の数。", new JObject { ["domain"] = "automation", ["kind"] = "critter_sensor", ["action"] = "list" }),
                    Resource("oni://automation/comet-detectors", "building_control", "宇宙スキャナー", "Comet Detector/Space Scanner の現在の検知対象と選択可能な対象。", new JObject { ["domain"] = "space_building", ["kind"] = "comet_detector", ["action"] = "list" }),
                    Resource("oni://automation/cluster-location-sensors", "building_control", "星図ロケーションセンサー", "LogicClusterLocationSensor の何もない宇宙空間と小惑星・POI の座標フィルター設定。", new JObject { ["domain"] = "space_building", ["kind"] = "cluster_location_sensor", ["action"] = "list" }),
                    Resource("oni://buildings/lights", "building_control", "照明の操作項目", "照明設備、発光のパラメーター、選択可能な色のプリセット。", new JObject { ["action"] = "visual", ["kind"] = "light", ["visualAction"] = "list" }),
                    Resource("oni://buildings/turbo-heaters", "building_control", "液体ヒーターのターボモード", "Liquid Tepidizer の TurboModeSideScreen の消費電力とオン・オフ状態。", new JObject { ["domain"] = "misc", ["kind"] = "turbo_heater", ["action"] = "list" }),
                    Resource("oni://buildings/pixel-packs", "building_control", "ピクセルパックの操作項目", "Pixel Pack の現在の自動化信号値、4枚のパネルの active/standby の色、色のプリセット。", new JObject { ["action"] = "visual", ["kind"] = "pixel_pack", ["visualAction"] = "list" }),
                    Resource("oni://geotuners", "building_control", "GeoTuner", "GeoTuner の現在・今後の対象間欠泉と、調整作業の割り当て状態。", new JObject { ["domain"] = "geo_tuner", ["action"] = "list" }),
                    Resource("oni://geotuners/geysers", "building_control", "地殻チューナーの対象間欠泉", "GeoTuner で選択可能な間欠泉、分析状態、可視性、割り当て数。", new JObject { ["domain"] = "geo_tuner", ["action"] = "list_geysers" }),
                    Resource("oni://buildings/artables", "building_control", "芸術作品の外観", "芸術作品の現在の外観と、選択可能な外観の段階。", new JObject { ["domain"] = "special", ["kind"] = "artable", ["action"] = "list" }),
                    Resource("oni://buildings/monument-parts", "building_control", "記念碑の部品の外観", "記念碑の部品の現在の外観、選択可能な外観、部品の種類。", new JObject { ["domain"] = "special", ["kind"] = "monument_part", ["action"] = "list" }),
                    Resource("oni://ranching/lures", "building_control", "動物のおびき寄せ設備", "動物のおびき寄せ設備の現在の餌、選択可能な餌、在庫。", new JObject { ["domain"] = "special", ["kind"] = "creature_lure", ["action"] = "list" }),
                    Resource("oni://buildings/gene-shufflers", "building_control", "Gene Shuffler", "Gene Shuffler の割り当て、作業完了、消費、充電要求の状態。", new JObject { ["domain"] = "special", ["kind"] = "gene_shuffler", ["action"] = "list" }),
                    Resource("oni://story/printerceptors", "building_control", "Printerceptor", "Printerceptor の passcode、横取り充電、製造画面、databank の状態。", new JObject { ["domain"] = "story_facility", ["kind"] = "printerceptor", ["action"] = "list" }),
                    Resource("oni://story/poi-tech-unlocks", "building_control", "研究用ポータル", "Research Portal の解放作業、進捗、POI の技術項目。", new JObject { ["domain"] = "story_facility", ["kind"] = "poi_tech_unlock", ["action"] = "list" }),
                    Resource("oni://buildings/remote-work-terminals", "building_control", "リモート コントローラー", "Remote Work Terminal の現在・今後の dock と、同じワールドで選択可能な dock。", new JObject { ["domain"] = "story_facility", ["kind"] = "remote_work_terminal", ["action"] = "list" }),
                    Resource("oni://farming/genetic-analysis-stations", "building_control", "Botanical Analyzer", "Botanical Analyzer で分析可能な種子、許可・禁止の状態、在庫。", new JObject { ["domain"] = "story_facility", ["kind"] = "genetic_analysis_station", ["action"] = "list" }),
                    Resource("oni://buildings/dispensers", "building_control", "ディスペンサー", "DispenserSideScreen の供給可能なアイテム、現在の選択、供給要求の状態。", new JObject { ["domain"] = "facility", ["kind"] = "dispenser", ["action"] = "list" }),
                    Resource("oni://buildings/receptacles", "building_control", "展示・配置スロット", "ReceptacleSideScreen / SpecialCargoBayClusterSideScreen / SingleEntityReceptacle の汎用エンティティの要求、取り消し、取り外しの状態。", new JObject { ["domain"] = "receptacle", ["action"] = "list" }),
                    Resource("oni://buildings/suit-lockers", "building_control", "アトモスーツ ドック", "SuitLockerSideScreen の設定、要求、収納済み装備、装備を落とせるかどうかの状態。", new JObject { ["domain"] = "facility", ["kind"] = "suit_locker", ["action"] = "list" }),
                    Resource("oni://story/lore-bearers", "building_control", "LoreBearer", "LoreBearerSideScreen の閲覧可能・閲覧済みオブジェクトとボタンの状態。", new JObject { ["domain"] = "facility", ["kind"] = "lore_bearer", ["action"] = "list" }),
                    Resource("oni://story/telepads", "building_control", "Printing Pod / Telepad", "TelepadSideScreen の移民、研究、スキルのヒント、勝利条件の状態。", new JObject { ["domain"] = "facility", ["kind"] = "telepad", ["action"] = "list" }),
                    Resource("oni://story/artifacts", "building_control", "Artifact Analysis", "ArtifactAnalysisSideScreen の分析済み artifact、ワールド内の artifact、分析端末の状態。", new JObject { ["domain"] = "facility", ["kind"] = "artifact", ["action"] = "list" }),
                    Resource("oni://story/warp-portals", "building_control", "Warp Portal", "WarpPortalSideScreen のテレポート待機中、テレポート中、クールダウン、割り当ての状態。", new JObject { ["domain"] = "space_story", ["kind"] = "warp_portal", ["action"] = "list" }),
                    Resource("oni://story/temporal-tears", "building_control", "Temporal Tear", "TemporalTearSideScreen の裂け目の開放・消費状態と、進入可能なロケット。", new JObject { ["domain"] = "space_story", ["kind"] = "temporal_tear", ["action"] = "list" }),
                    Resource("oni://space/telescopes", "building_control", "Telescope", "TelescopeSideScreen の設備の状態と、現在の星図の分析対象。", new JObject { ["domain"] = "space_story", ["kind"] = "telescope", ["action"] = "list" }),
                    Resource("oni://space/analysis-targets", "building_control", "星図の分析対象", "星図の目的地の分析状態と、望遠鏡で選択可能な対象。", new JObject { ["domain"] = "space_story", ["kind"] = "starmap_analysis", ["action"] = "list" }),
                    Resource("oni://diagnostics/process-conditions", "building_control", "共通の処理条件", "ConditionListSideScreen/IProcessConditionSet の条件の状態、テキスト、tooltip。", new JObject { ["domain"] = "space_story", ["kind"] = "process_conditions", ["action"] = "list" }),
                    Resource("oni://dupes/bionic-upgrades", "dupes_control", "バイオニック複製人間のアップグレードスロット", "BionicSideScreen のバイオニック複製人間のアップグレードスロット、割り当て、装着状態。書き込みには dupes_control domain=assignable action=set_slot を使う。", new JObject { ["domain"] = "side_screen", ["action"] = "bionic_upgrades" }),
                    Resource("oni://rockets/missile-launchers", "building_control", "隕石ブラスター", "隕石ブラスターの弾薬の許可状態。", new JObject { ["domain"] = "special", ["kind"] = "missile_launcher", ["action"] = "list" }),
                    Resource("oni://rockets/modules", "building_control", "ロケットモジュール", "ロケットモジュールの順序と、取り外し・交換・追加が可能かどうかの状態。", new JObject { ["domain"] = "module", ["action"] = "list" }),
                    Resource("oni://rockets/module-defs", "building_control", "ロケットモジュールの定義", "SelectModuleSideScreen の選択可能なロケットモジュールの定義と条件の状態。", new JObject { ["domain"] = "module", ["action"] = "list_defs" }),
                    Resource("oni://rockets/launch-pads", "building_control", "ロケット発射台", "LaunchPadSideScreen の発射台、停泊中のロケット、着陸可能なロケット。", new JObject { ["domain"] = "ops", ["action"] = "list_launch_pads" }),
                    Resource("oni://rockets/flight-utilities", "building_control", "ロケット飛行モジュールの補助操作", "ModuleFlightUtilitySideScreen の内容物を空にする・投下・自動投下・対象・複製人間の選択状態。", new JObject { ["domain"] = "flight_utility", ["action"] = "list" }),
                    Resource("oni://rockets/restrictions", "building_control", "ロケット制御装置の制限", "RocketRestrictionSideScreen の地上・宇宙での使用制限の状態。", new JObject { ["domain"] = "restriction", ["action"] = "list" }),
                    Resource("oni://rockets/usage-controls", "building_control", "ロケット内部設備の使用制限", "ロケット内部の設備が RocketControlStation の制限を受けるかどうかのユーザーメニューの状態。", new JObject { ["domain"] = "usage", ["action"] = "list" }),
                    Resource("oni://rockets/crew-requests", "building_control", "ロケットの乗組員の召集", "SummonCrewSideScreen の乗組員の召集・解放状態、搭乗人数、操縦士の状態。", new JObject { ["domain"] = "crew_request", ["action"] = "list" }),
                    Resource("oni://rockets/assignment-groups", "building_control", "割り当てグループのメンバー", "AssignmentGroupControllerSideScreen の割り当てグループのメンバー状態と、複製人間の加入設定。", new JObject { ["domain"] = "assignment_group", ["action"] = "list" }),
                    Resource("oni://rockets/cargo-collectors", "building_control", "ロケットの貨物収集モジュール", "CargoModuleSideScreen の星図での貨物収集モジュールの容量、在庫、収集の進捗。", new JObject { ["domain"] = "cargo_status", ["action"] = "collectors" }),
                    Resource("oni://rockets/harvest-modules", "building_control", "ロケットの掘削モジュール", "HarvestModuleSideScreen の宇宙掘削モジュールの掘削状態とダイヤモンドの在庫。", new JObject { ["domain"] = "cargo_status", ["action"] = "harvest_modules" }),
                    Resource("oni://rockets/railguns", "building_control", "星間ランチャー", "星間ランチャーの発射質量、在庫、放射線粒子エネルギーの状態。", new JObject { ["domain"] = "space_building", ["kind"] = "railgun", ["action"] = "list" }),
                    Resource("oni://rockets/self-destruct", "building_control", "ロケットの自爆", "SelfDestructButtonSideScreen の自爆可能なロケットモジュール。", new JObject { ["domain"] = "self_destruct", ["action"] = "list" }),
                    Resource("oni://buildings/defs", "building_control", "建築可能な設備の定義", "建築メニューの設備の定義、分類、必要素材、利用可能な外観、検索結果。", new JObject { ["action"] = "search_defs" }),
                    Resource("oni://buildings/materials", "building_control", "使用可能な建築素材", "指定した設備で使用できる現在のワールドの建築素材を、在庫順に表示する。material=auto や素材の明示的な選択に使う。", new JObject { ["action"] = "materials" }),
                    Resource("oni://buildings/configurables", "building_control", "設定可能な設備", "有効化、オン・オフ、しきい値、ドアの状態、通行許可、補充を設定できる設備。", new JObject { ["action"] = "list" }),
                    Resource("oni://automation/controls", "building_control", "自動化の操作項目", "プレイヤーが設定できる自動化・電力関連の項目。ポート、スイッチ、しきい値、バルブ、タイマー、ribbon bit。", new JObject { ["action"] = "list_automation" }),
                    Resource("oni://production/fabricators", "building_control", "製造設備", "製造設備・精製設備・調理設備などのレシピキュー、現在の注文、稼働状態。", new JObject { ["domain"] = "production", ["action"] = "list_fabricators" }),
                    Resource("oni://production/recipes", "building_control", "製造レシピ", "製造設備の ComplexRecipe レシピ、素材、生産物、解放状態、キュー内の数量。", new JObject { ["domain"] = "production", ["action"] = "list_recipes" }),
                    Resource("oni://production/mutant-seed-controls", "building_control", "変異種子の受け入れ設定", "製造設備、魚用給餌機、スパイスミルの、変異種子の受け入れ・拒否を設定するユーザーメニュー。", new JObject { ["domain"] = "production", ["action"] = "mutant_seed_list" }),
                    Resource("oni://production/configurable-consumers", "building_control", "消費対象を設定できる設備", "ConfigureConsumerSideScreen の現在の選択肢、選択可能な項目、消費素材。", new JObject { ["domain"] = "misc", ["kind"] = "configurable_consumer", ["action"] = "list" }),
                    Resource("oni://rockets/status", "building_control", "ロケットの状態", "Spaced Out のロケットと基本版の宇宙船の状態。", new JObject { ["domain"] = "ops", ["action"] = "status" }),
                    Resource("oni://research/status", "colony_control", "研究の状態", "現在の研究の状態。", new JObject { ["domain"] = "management", ["kind"] = "research", ["action"] = "status" }),
                    Resource("oni://schedules", "colony_control", "スケジュール", "複製人間のスケジュール。", new JObject { ["domain"] = "management", ["kind"] = "schedule", ["action"] = "list" }),
                    Resource("oni://dupes", "colony_control", "複製人間", "複製人間の一覧と基本状態。", new JObject { ["domain"] = "read", ["action"] = "dupes" }),
                    Resource("oni://dupes/priorities", "dupes_control", "複製人間ごとの作業優先度", "Priorities/Jobs 管理画面での、各 ChoreGroup に対する複製人間ごとの作業優先度。", new JObject { ["domain"] = "priority", ["action"] = "list" }),
                    Resource("oni://dupes/priority-settings", "dupes_control", "複製人間の優先度設定", "Jobs/Priorities 管理画面の高度なモードの全体設定、既定のリセット動作、リセット後の優先度の状態。", new JObject { ["domain"] = "priority", ["action"] = "settings_get" }),
                    Resource("oni://dupes/skills", "dupes_control", "複製人間のスキル", "Skills 管理画面の複製人間のスキルポイント、習得済みスキル、習得可能なスキル。", new JObject { ["domain"] = "skill", ["action"] = "list" }),
                    Resource("oni://dupes/hats", "dupes_control", "複製人間の帽子", "Skills 管理画面の現在の帽子、変更先の帽子、選択可能な帽子の一覧。", new JObject { ["domain"] = "hat", ["action"] = "list" }),
                    Resource("oni://dupes/status-check", "dupes_control", "複製人間の状態チェック", "複製人間の位置、現在の作業、重要な欲求、周辺の到達可能なセル、閉じ込めの疑い。読み取り専用。", new JObject { ["domain"] = "info", ["action"] = "status_check" }),
                    Resource("oni://dupes/direct-commands", "dupes_control", "複製人間への直接指示", "複製人間に対して直接実行・設定できるプレイヤー操作。", new JObject { ["domain"] = "side_screen", ["action"] = "direct_commands" }),
                    Resource("oni://dupes/todos", "dupes_control", "複製人間の作業予定", "MinionTodoSideScreen の現在の作業、実行可能な作業、実行を妨げられている作業。", new JObject { ["domain"] = "side_screen", ["action"] = "todos" }),
                    Resource("oni://dupes/equipment", "dupes_control", "複製人間の装備", "複製人間の装備スロット、現在の装備、割り当て可能な装備。書き込みには dupes_control domain=assignable action=set_slot を使う。", new JObject { ["domain"] = "side_screen", ["action"] = "equipment" }),
                    Resource("oni://assignables", "dupes_control", "割り当て可能なオブジェクト", "ベッド、医療ベッド、食卓、スーツなど、割り当て可能なオブジェクトと現在の割り当て。", new JObject { ["domain"] = "assignable", ["action"] = "list" }),
                    Resource("oni://farming/planting", "colony_control", "栽培スロット", "プランター、農耕タイル、現在の植物、要求中の種子、受け入れ可能な種子。", new JObject { ["domain"] = "bio", ["bioDomain"] = "farming", ["action"] = "list_planting" }),
                    Resource("oni://farming/harvestables", "colony_control", "収穫可能な対象", "植物・作物の成熟状態、収穫指定、成熟時の自動収穫の状態。", new JObject { ["domain"] = "bio", ["bioDomain"] = "farming", ["action"] = "list_harvestables" }),
                    Resource("oni://farming/seeds", "colony_control", "種子カタログ", "栽培要求に使用できる PlantableSeed prefab。", new JObject { ["domain"] = "bio", ["bioDomain"] = "farming", ["action"] = "seed_catalog" }),
                    Resource("oni://ranching/critters", "colony_control", "動物", "捕獲可能な動物、捕獲指定、拘束状態。", new JObject { ["domain"] = "bio", ["bioDomain"] = "ranching", ["kind"] = "critters", ["action"] = "critters" }),
                    Resource("oni://ranching/dropoffs", "colony_control", "動物解放地点", "動物・魚の解放地点のフィルター、容量、数。", new JObject { ["domain"] = "bio", ["bioDomain"] = "ranching", ["kind"] = "dropoff", ["action"] = "list" }),
                    Resource("oni://ranching/incubators", "colony_control", "孵卵器", "孵卵器の卵の要求、入っている対象、進捗、連続孵化の設定。", new JObject { ["domain"] = "bio", ["bioDomain"] = "ranching", ["kind"] = "incubator", ["action"] = "list" }),
                    Resource("oni://medical/patients", "colony_control", "治療対象の患者", "治療が必要な複製人間、病気、生命力、医療ベッドの割り当て。", new JObject { ["domain"] = "management", ["kind"] = "medical", ["action"] = "patients" }),
                    Resource("oni://medical/clinics", "colony_control", "医療ベッドと診療所", "医療ベッド・診療所の治療しきい値、割り当て対象、優先度。", new JObject { ["domain"] = "management", ["kind"] = "medical", ["action"] = "clinics" }),
                    Resource("oni://medical/doctor-stations", "colony_control", "医務室", "医務室の薬の在庫と治療可能な患者。", new JObject { ["domain"] = "management", ["kind"] = "medical", ["action"] = "doctor_stations" }),
                    Resource("oni://sandbox/actions", "game_control", "サンドボックス操作", "MCP が公開するサンドボックス・Debug 操作、リスク、現在のサンドボックス状態。", new JObject { ["domain"] = "sandbox", ["kind"] = "read", ["action"] = "list_actions" }),
                    Resource("oni://sandbox/story-traits", "game_control", "サンドボックスのストーリー特性", "サンドボックスの Story Trait Tool で配置できるストーリー特性のテンプレート。", new JObject { ["domain"] = "sandbox", ["kind"] = "read", ["action"] = "list_story_traits" }),
                    Resource("oni://game/time", "game_control", "ゲーム内時刻と速度", "現在のサイクル、サイクル内の経過時間の割合、一時停止の状態、速度。", new JObject { ["domain"] = "speed", ["action"] = "time" }),
                    Resource("oni://game/red-alert", "game_control", "非常警報", "現在のワールド・全ワールドの非常警報（緊急モード）の状態。", new JObject { ["domain"] = "state", ["action"] = "red_alert_status" }),
                    Resource("oni://game/saves", "game_control", "セーブファイル", "ローカル・クラウドのセーブファイル、現在の active save、保存先のルートディレクトリ。", new JObject { ["domain"] = "save", ["action"] = "list" }),
                    Resource("oni://game/dlc", "game_control", "セーブデータの DLC 有効化状態", "一時停止メニューの DLC 有効化ボタンの状態。所有状況、現在のセーブデータでの有効状態、有効化できるかどうか。", new JObject { ["domain"] = "dlc", ["action"] = "list" }),
                    Resource("oni://mcp/sessions", "server_control", "MCP セッション", "現在の MCP session と、クライアントの sampling、elicitation、tasks の対応状況。", new JObject { ["domain"] = "diagnostics", ["action"] = "capabilities" }),
                    Resource("oni://ui/actions", "game_control", "許可されている UI Action", "安全に実行できる管理メニュー、オーバーレイ、建築分類、ナビゲーションの Action。", new JObject { ["domain"] = "ui", ["uiDomain"] = "action", ["action"] = "list" }),
                    Resource("oni://tools/manifest", "server_control", "ツール一覧", "ONI MCP のツールカタログ。", new JObject { ["domain"] = "catalog", ["action"] = "manifest" }),
                    Resource("oni://tools/guide", "server_control", "目的別のツールガイド", "プレイヤーの目標に応じてリソース、ツールの組み合わせ、一括処理の方針を提案する。", new JObject { ["domain"] = "catalog", ["action"] = "guide" }),
                    Resource("oni://tools/player-action-coverage", "server_control", "プレイヤー操作の対応範囲の監査", "プレイヤーが実行できる操作、対応する MCP ツール、未対応部分の状態。", new JObject { ["domain"] = "catalog", ["action"] = "coverage" }),
                    Resource("oni://tools/side-screen-surfaces", "server_control", "サイドパネルの surface 監査", "ランタイムの SideScreenContent 型と MCP ツール・リソースの対応範囲を照合する監査。", new JObject { ["domain"] = "catalog", ["action"] = "surface_audit", ["surface"] = "side_screen" }),
                    Resource("oni://tools/user-menu-surfaces", "server_control", "ユーザーメニューの surface 監査", "ソースコードの UserMenu/context-menu ボタンの実装元と MCP ツール・リソースの対応範囲を照合する監査。", new JObject { ["domain"] = "catalog", ["action"] = "surface_audit", ["surface"] = "user_menu" }),
                    Resource("oni://tools/management-surfaces", "server_control", "管理画面の surface 監査", "ソースコードの ManagementMenu/TableScreen/全画面管理画面と MCP ツール・リソースの対応範囲を照合する監査。", new JObject { ["domain"] = "catalog", ["action"] = "surface_audit", ["surface"] = "management" }),
                    Resource("oni://tools/tool-menu-surfaces", "server_control", "ツールバーの surface 監査", "ソースコードの ToolMenu のメインツールバー・サンドボックスツールバーと MCP ツール・リソースの対応範囲を照合する監査。", new JObject { ["domain"] = "catalog", ["action"] = "surface_audit", ["surface"] = "tool_menu" }),
                    Resource("oni://tools/ui-menu-surfaces", "server_control", "UI メニューの surface 監査", "ソースコードの OverlayMenu/PlanScreen/BuildMenu/安全な UI hotkey と MCP ツール・リソースの対応範囲を照合する監査。", new JObject { ["domain"] = "catalog", ["action"] = "surface_audit", ["surface"] = "ui_menu" }),
                    Resource("oni://tools/global-control-surfaces", "server_control", "全体操作の surface 監査", "ソースコードの SpeedControlScreen/TopLeftControlScreen/PauseScreen/Options/Locker と MCP の対応範囲を照合する監査。", new JObject { ["domain"] = "catalog", ["action"] = "surface_audit", ["surface"] = "global_control" }),
                    Resource("oni://tools/notification-surfaces", "server_control", "通知の surface 監査", "ソースコードの NotificationScreen/NotificationManager/メッセージ通知と MCP の対応範囲を照合する監査。", new JObject { ["domain"] = "catalog", ["action"] = "surface_audit", ["surface"] = "notification" }),
                    Resource("oni://tools/static-audit", "server_control", "静的インターフェース監査", "ツール登録、プレイヤー操作の対応範囲、リソースの入口、危険なツールの確認引数を静的に自己検査する。", new JObject { ["domain"] = "catalog", ["action"] = "static_audit" }),
                    Resource("oni://power/summary", "read_control", "電力の概要", "現在のワールドの電力システムの概要。発電機の定格出力、消費設備の負荷、バッテリーの容量と充電量を circuitId ごとに集計する。", new JObject { ["domain"] = "infrastructure", ["action"] = "power_summary" }),
                    Resource("oni://power/ports", "read_control", "電力ポート", "指定領域内の設備の電力ポートのセル。アンカー、入力・出力ポート、相対オフセット、配線可能な状態。", new JObject { ["domain"] = "infrastructure", ["action"] = "power_ports" }),
                    Resource("oni://rooms/types", "read_control", "部屋種別の成立条件と効果", "ゲームに読み込まれた全ての部屋種別の成立条件文と効果。未成立の部屋も調べられる。", new JObject { ["domain"] = "infrastructure", ["action"] = "room_types" }),
                    Resource("oni://rooms/list", "read_control", "部屋一覧", "部屋システムの状態。部屋の種類、広さ、境界、オブジェクト数、部屋の効果。士気を高める部屋が成立しているかの確認に適している。", new JObject { ["domain"] = "infrastructure", ["action"] = "rooms" }),
                    Resource("oni://thermal/overheat-risk", "read_control", "オーバーヒートリスクの走査", "設備のオーバーヒートリスクを走査する。現在のセル温度と設備のオーバーヒート温度の差で並べ、過熱間近または過熱中の設備を見つける。", new JObject { ["domain"] = "world", ["action"] = "thermal_overheat_risk" }),
                    Resource("oni://world/search", "read_control", "マップ検索", "query/条件に従ってマップ上の元素セル、設備、地面のアイテム、複製人間を検索する。領域での絞り込みと近い順の並べ替えに対応する。", new JObject { ["domain"] = "world", ["action"] = "search" }),
                    Resource("oni://world/coordinate-screenshot", "navigation_control", "座標付きスクリーンショット", "ONI のワールド座標のグリッドと座標テキストを重ねたスクリーンショットを保存し、ローカルパスと HTTP URL を返す。視覚モデルで座標を直接読み取る用途に適している。", new JObject { ["action"] = "coordinate_screenshot" }),
                    Resource("oni://world/layout-candidates", "read_control", "配置候補", "用途に応じて領域を走査し、部屋・足場の候補矩形、評価点、必要な採掘・タイル設置、危険なセル、接続性を返す。", new JObject { ["domain"] = "world", ["action"] = "layout_candidates" })
                };

                private static readonly List<McpResourceTemplateInfo> _templates = BuildResourceTemplates();

        private static List<McpResourceTemplateInfo> BuildResourceTemplates()
        {
            var templates = new List<McpResourceTemplateInfo>();
            AddWorldAndColonyResourceTemplates(templates);
            AddBuildingAndSpaceResourceTemplates(templates);
            AddDuplicantAndUiResourceTemplates(templates);
            return templates;
        }

        private static ReadResourceResult ReadDynamicResource(string uri)
        {
            Uri parsed;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out parsed) || parsed.Scheme != "oni")
                return null;

            ReadResourceResult result;
            result = ReadWorldAndColonyResourceRoutes(uri, parsed);
            if (result != null)
                return result;

            result = ReadBuildingAndSpaceResourceRoutes(uri, parsed);
            if (result != null)
                return result;

            result = ReadDuplicantAndUiResourceRoutes(uri, parsed);
            if (result != null)
                return result;

            return null;
        }
    }
}
