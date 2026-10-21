using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static class UiTools
    {
        private static readonly Dictionary<string, global::Action> ManagementScreens = new Dictionary<string, global::Action>(StringComparer.OrdinalIgnoreCase)
        {
            ["vitals"] = global::Action.ManageVitals,
            ["consumables"] = global::Action.ManageConsumables,
            ["priorities"] = global::Action.ManagePriorities,
            ["schedule"] = global::Action.ManageSchedule,
            ["skills"] = global::Action.ManageSkills,
            ["research"] = global::Action.ManageResearch,
            ["starmap"] = global::Action.ManageStarmap,
            ["report"] = global::Action.ManageReport,
            ["database"] = global::Action.ManageDatabase,
            ["codex"] = global::Action.ManageDatabase
        };

        public static McpTool ListUiActions()
        {
            return new McpTool
            {
                Name = "ui_actions_list",
                Hidden = true,
                Group = "ui",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "ui_management_list", "ui_hotkeys_list" },
                Tags = new List<string> { "ui", "management", "screen", "hotkey", "action" },
                Description = "互換用の窓口: game_control domain=ui uiDomain=action action=list を使用してください。MCP から安全に開く/実行できる UI ページと Action を一覧表示する。管理メニュー、オーバーレイ、建築カテゴリ、キャンセル/閉じるを含む",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "絞り込みタイプ: all、management、overlay、build、navigation。既定値は all", Required = false, EnumValues = new List<string> { "all", "management", "overlay", "build", "navigation" } }
                },
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "all").Trim().ToLowerInvariant();
                    var actions = SafeActions()
                        .Select(item => ActionInfo(item.Key, item.Value))
                        .Where(item => kind == "all" || (string)item["kind"] == kind)
                        .OrderBy(item => item["kind"].ToString())
                        .ThenBy(item => item["name"].ToString())
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = actions.Count,
                        ["managementScreens"] = ManagementScreens.Keys.OrderBy(name => name).ToList(),
                        ["actions"] = actions
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool OpenManagementScreen()
        {
            return new McpTool
            {
                Name = "ui_management_open",
                Hidden = true,
                Group = "ui",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "management_open", "ui_open_screen" },
                Tags = new List<string> { "ui", "management", "screen", "codex", "research", "skills" },
                Description = "互換用の窓口: game_control domain=ui uiDomain=action action=open_management を使用してください。指定した管理ページを開く: vitals、consumables、priorities、schedule、skills、research、starmap、report、database/codex",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["screen"] = new McpToolParameter { Type = "string", Description = "ページ名", Required = true, EnumValues = ManagementScreens.Keys.OrderBy(name => name).ToList() },
                    ["codexId"] = new McpToolParameter { Type = "string", Description = "screen=database/codex の場合、指定した Codex entry id を直接開ける", Required = false },
                    ["researchId"] = new McpToolParameter { Type = "string", Description = "screen=research の場合、指定した tech id にズームできる", Required = false },
                    ["reportDay"] = new McpToolParameter { Type = "integer", Description = "screen=report の場合、指定したサイクルのレポートを開く。既定値は現在のサイクル", Required = false }
                },
                Handler = args =>
                {
                    var menu = ManagementMenu.Instance;
                    if (menu == null)
                        return CallToolResult.Error("ManagementMenu is not available");

                    string screen = (args["screen"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (!ManagementScreens.TryGetValue(screen, out var action))
                        return CallToolResult.Error("Unknown management screen");

                    if ((screen == "database" || screen == "codex") && !string.IsNullOrWhiteSpace(args["codexId"]?.ToString()))
                        menu.OpenCodexToEntry(args["codexId"].ToString().Trim());
                    else if (screen == "research")
                        menu.OpenResearch(string.IsNullOrWhiteSpace(args["researchId"]?.ToString()) ? null : args["researchId"].ToString().Trim());
                    else if (screen == "skills")
                        menu.ToggleSkills();
                    else if (screen == "starmap")
                        OpenStarmap(menu);
                    else if (screen == "priorities")
                        menu.TogglePriorities();
                    else if (screen == "report")
                        menu.OpenReports(ToolUtil.GetInt(args, "reportDay") ?? GameUtil.GetCurrentCycle());
                    else if (screen == "database" || screen == "codex")
                        menu.ToggleCodex();
                    else
                    {
                        var dispatchFailure = DispatchUiAction(action);
                        if (dispatchFailure != null) return dispatchFailure;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["opened"] = screen,
                        ["action"] = action.ToString()
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool TriggerUiAction()
        {
            return new McpTool
            {
                Name = "ui_action_trigger",
                Hidden = true,
                Group = "ui",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "ui_hotkey_trigger", "game_action_trigger" },
                Tags = new List<string> { "ui", "hotkey", "action", "management", "build", "overlay" },
                Description = "互換用の窓口: game_control domain=ui uiDomain=action action=trigger を使用してください。許可リスト内の安全な UI Action を実行し、画面、オーバーレイ、建築カテゴリを開閉する。debug や破壊的なゲーム操作は実行しない",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "Action 列挙型の名前。例: ManageVitals、Overlay1、BuildCategoryTiles、Escape", Required = true }
                },
                Handler = args =>
                {
                    string name = args["action"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                        return CallToolResult.Error("action is required");
                    var safe = SafeActions();
                    var match = safe.Keys.FirstOrDefault(action => string.Equals(action.ToString(), name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (!safe.ContainsKey(match))
                        return CallToolResult.Error("Action is not in the safe UI whitelist");

                    var dispatchFailure = DispatchUiAction(match);
                    if (dispatchFailure != null) return dispatchFailure;
                    var info = ActionInfo(match, safe[match]);
                    info["dispatched"] = true;
                    return CallToolResult.Text(JsonConvert.SerializeObject(info, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlUiAction()
        {
            return new McpTool
            {
                Name = "ui_action_control",
                Group = "ui",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "ui_control", "ui_hotkey_control", "game_action_control" },
                Tags = new List<string> { "ui", "hotkey", "action", "management", "build", "overlay" },
                Description = "UI Action の統合ツール: action=list/trigger/open_management。安全な UI Action の許可リストを読み取り、許可されたアクションを実行、または管理ページを開く。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、trigger、open_management のいずれか", Required = true, EnumValues = new List<string> { "list", "trigger", "open_management" } },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "action=list の場合の絞り込みタイプ: all、management、overlay、build、navigation。既定値は all", Required = false, EnumValues = new List<string> { "all", "management", "overlay", "build", "navigation" } },
                    ["uiAction"] = new McpToolParameter { Type = "string", Description = "action=trigger の場合の Action 列挙型の名前。例: ManageVitals、Overlay1、BuildCategoryTiles、Escape", Required = false },
                    ["screen"] = new McpToolParameter { Type = "string", Description = "action=open_management の場合のページ名", Required = false, EnumValues = ManagementScreens.Keys.OrderBy(name => name).ToList() },
                    ["codexId"] = new McpToolParameter { Type = "string", Description = "action=open_management screen=database/codex の場合、指定した Codex entry id を直接開ける", Required = false },
                    ["researchId"] = new McpToolParameter { Type = "string", Description = "action=open_management screen=research の場合、指定した tech id にズームできる", Required = false },
                    ["reportDay"] = new McpToolParameter { Type = "integer", Description = "action=open_management screen=report の場合、指定したサイクルのレポートを開く。既定値は現在のサイクル", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListUiActions().Handler(args);
                    if (action == "trigger")
                    {
                        var forwarded = (Newtonsoft.Json.Linq.JObject)args.DeepClone();
                        forwarded["action"] = args["uiAction"] ?? args["ui_action"] ?? args["name"];
                        return TriggerUiAction().Handler(forwarded);
                    }
                    if (action == "open_management")
                        return OpenManagementScreen().Handler(args);
                    return CallToolResult.Error("action must be list, trigger, or open_management");
                }
            };
        }

        private static Dictionary<global::Action, string> SafeActions()
        {
            var result = new Dictionary<global::Action, string>();
            foreach (var item in ManagementScreens.Values.Distinct())
                result[item] = "management";
            AddRange(result, "overlay", "Overlay", 1, 15);
            AddRange(result, "build", "BuildCategory", new[]
            {
                "Ladders", "Tiles", "Doors", "Storage", "Generators", "Wires", "PowerControl",
                "PlumbingStructures", "Pipes", "VentilationStructures", "Tubes", "TravelTubes",
                "Conveyance", "LogicWiring", "LogicGates", "LogicSwitches", "LogicConduits",
                "Cooking", "Farming", "Ranching", "Research", "Hygiene", "Medical", "Recreation",
                "Furniture", "Decor", "Oxygen", "Utilities", "Refining", "Equipment", "Rocketry"
            });
            AddRange(result, "build", "BuildMenuKey", Enumerable.Range('A', 26).Select(code => ((char)code).ToString()).ToArray());
            result[global::Action.Escape] = "navigation";
            result[global::Action.Find] = "navigation";
            result[global::Action.Help] = "navigation";
            result[global::Action.ToggleScreenshotMode] = "navigation";
            return result;
        }

        private static void AddRange(Dictionary<global::Action, string> actions, string kind, string prefix, int from, int to)
        {
            for (int i = from; i <= to; i++)
            {
                if (Enum.TryParse(prefix + i, out global::Action action))
                    actions[action] = kind;
            }
        }

        private static void AddRange(Dictionary<global::Action, string> actions, string kind, string prefix, IEnumerable<string> suffixes)
        {
            foreach (string suffix in suffixes)
            {
                if (Enum.TryParse(prefix + suffix, out global::Action action))
                    actions[action] = kind;
            }
        }

        private static Dictionary<string, object> ActionInfo(global::Action action, string kind)
        {
            string bindingState = ToolMenuHotkeySafety.GetBindingState(action);
            string readinessState = ReadinessState(action);
            bool? available = bindingState == "unbound" || bindingState == "invalid" || readinessState == "unavailable"
                ? false : bindingState == "unknown" || readinessState == "unknown" ? (bool?)null : true;
            return new Dictionary<string, object>
            {
                ["name"] = action.ToString(),
                ["kind"] = kind,
                ["ordinal"] = (int)action,
                ["available"] = available.HasValue ? (object)available.Value : Newtonsoft.Json.Linq.JValue.CreateNull(),
                ["bindingState"] = bindingState,
                ["readinessState"] = readinessState
            };
        }

        private static string ReadinessState(global::Action action)
        {
            try
            {
                var menu = ManagementMenu.Instance;
                if (menu == null || KScreenManager.Instance == null) return "unavailable";
                if (action == global::Action.ManageResearch)
                    return menu.CheckHasResearchCenter() ? "ready" : "unavailable";
                if (action == global::Action.ManageSkills)
                    return Components.RoleStations.Count > 0 || DebugHandler.InstantBuildMode
                        || (Game.Instance != null && Game.Instance.SandboxModeActive) ? "ready" : "unavailable";
                if (action == global::Action.ManageStarmap)
                    return ManagementMenu.StarmapAvailable() || DlcManager.FeatureClusterSpaceEnabled() ? "ready" : "unavailable";
                return "ready";
            }
            catch
            {
                return "unknown";
            }
        }

        private static void OpenStarmap(ManagementMenu menu)
        {
            if (DlcManager.FeatureClusterSpaceEnabled())
                menu.OpenClusterMap();
            else
                menu.OpenStarmap();
        }

        private static CallToolResult DispatchUiAction(global::Action action)
        {
            // Re-read current bindings at the shared dispatch boundary, including management fallbacks.
            var info = ActionInfo(action, SafeActions()[action]);
            if (!(info["available"] is bool available) || !available)
            {
                string bindingState = (string)info["bindingState"];
                string readinessState = (string)info["readinessState"];
                string reason = bindingState == "unknown" ? "binding_unknown"
                    : bindingState != "bound" ? "unbound"
                    : readinessState == "unknown" ? "readiness_unknown" : "not_ready";
                info["dispatched"] = false;
                info["reasonCode"] = reason;
                info["message"] = McpLocalization.Get("ui_action." + reason);
                return CallToolResult.Error(JsonConvert.SerializeObject(info, McpJsonUtil.Settings));
            }
            var manager = KScreenManager.Instance;
            if (manager == null)
                return CallToolResult.Error(McpLocalization.Get("ui_action.not_ready"));
            var down = new KButtonEvent(null, InputEventType.KeyDown, action);
            manager.OnKeyDown(down);
            var up = new KButtonEvent(null, InputEventType.KeyUp, action);
            manager.OnKeyUp(up);
            return null;
        }
    }
}
