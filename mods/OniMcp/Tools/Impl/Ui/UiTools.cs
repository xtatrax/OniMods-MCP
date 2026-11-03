using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                Description = McpLocalization.Get("ui_action.management_open_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["screen"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("ui_action.management_screen_description"), Required = true, EnumValues = ManagementScreens.Keys.OrderBy(name => name).ToList() },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.confirm"), Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.dryRun"), Required = false },
                    ["codexId"] = new McpToolParameter { Type = "string", Description = "screen=database/codex の場合、指定した Codex entry id を直接開ける", Required = false },
                    ["researchId"] = new McpToolParameter { Type = "string", Description = "screen=research の場合、指定した tech id にズームできる", Required = false },
                    ["reportDay"] = new McpToolParameter { Type = "integer", Description = "screen=report の場合、指定したサイクルのレポートを開く。既定値は現在のサイクル", Required = false }
                },
                Handler = args =>
                {
                    string screen = (args["screen"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (screen == "schedule")
                        return OpenScheduleManagement(args);

                    var menu = ManagementMenu.Instance;
                    if (menu == null)
                        return CallToolResult.Error("ManagementMenu is not available");
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
                Description = McpLocalization.Get("ui_action.control_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("ui_action.control_action_description"), Required = true, EnumValues = new List<string> { "list", "trigger", "open_management", "management_status" } },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "action=list の場合の絞り込みタイプ: all、management、overlay、build、navigation。既定値は all", Required = false, EnumValues = new List<string> { "all", "management", "overlay", "build", "navigation" } },
                    ["uiAction"] = new McpToolParameter { Type = "string", Description = "action=trigger の場合の Action 列挙型の名前。例: ManageVitals、Overlay1、BuildCategoryTiles、Escape", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.confirm"), Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.dryRun"), Required = false },
                    ["screen"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("ui_action.management_screen_description"), Required = false, EnumValues = ManagementScreens.Keys.OrderBy(name => name).ToList() },
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
                    if (action == "management_status")
                        return ReadScheduleManagementStatus(args);
                    return CallToolResult.Error(McpLocalization.Get("ui_action.control_invalid_action"));
                }
            };
        }

        // Schedule acknowledgement observes existing public native state; it never repairs or refreshes it.
        private sealed class ScheduleUiState
        {
            internal Game Game;
            internal ManagementMenu Menu;
            internal ScheduleScreen Screen;
            internal KScreenManager Manager;
            internal bool Available;
            internal bool? MenuOwnsSchedule, MenuReportsOpen, ScreenActive, ScreenEnabled, ScreenInHierarchy, Focused;
            internal string Reason = "ui_unavailable";
        }

        private static ScheduleUiState ReadScheduleUiState()
        {
            var state = new ScheduleUiState();
            try
            {
                state.Game = Game.Instance;
                state.Menu = ManagementMenu.Instance;
                state.Manager = KScreenManager.Instance;
                if (state.Game == null || state.Menu == null || state.Manager == null)
                    return state;
                state.Screen = state.Menu.scheduleScreen;
                state.MenuOwnsSchedule = state.Screen != null;
                if (state.MenuOwnsSchedule != true || state.Menu.gameObject == null || state.Screen.gameObject == null)
                    return state;
                state.MenuReportsOpen = state.Menu.IsScreenOpen(state.Screen);
                state.ScreenActive = state.Screen.IsScreenActive();
                state.ScreenEnabled = state.Screen.isActiveAndEnabled;
                state.ScreenInHierarchy = state.Screen.gameObject.activeInHierarchy;
                state.Focused = KInputManager.isFocused;
                if (!state.Menu.IsScreenActive() || !state.Menu.gameObject.activeInHierarchy)
                {
                    state.Reason = "menu_inactive";
                    return state;
                }
                if (state.ScreenActive != state.ScreenEnabled
                    || state.MenuReportsOpen != (state.ScreenActive == true && state.ScreenInHierarchy == true))
                {
                    state.Reason = "state_inconsistent";
                    return state;
                }
                state.Available = true;
                state.Reason = null;
            }
            catch { state.Reason = "state_read_failed"; }
            return state;
        }

        private static bool ReadScheduleFlags(JObject args, out bool dryRun, out string invalidParameter)
        {
            dryRun = false;
            invalidParameter = null;
            foreach (string flag in new[] { "confirm", "dryRun" })
                if (args[flag] != null && args[flag].Type != JTokenType.Boolean)
                {
                    invalidParameter = flag;
                    return false;
                }
            dryRun = args["dryRun"]?.Value<bool>() ?? false;
            return true;
        }

        private static CallToolResult ReadScheduleManagementStatus(JObject args)
        {
            bool dryRun;
            string invalidParameter;
            if (!ReadScheduleFlags(args, out dryRun, out invalidParameter))
                return ScheduleUiResult(null, false, "unavailable", false, "invalid_boolean", invalidParameter);
            if (args["screen"]?.Type != JTokenType.String
                || !string.Equals(args["screen"].Value<string>().Trim(), "schedule", StringComparison.OrdinalIgnoreCase))
                return ScheduleUiResult(null, false, "unavailable", false, "invalid_screen", "screen");
            var state = ReadScheduleUiState();
            return ScheduleUiResult(state, state.Available, state.Available ? "observed" : "unavailable",
                false, state.Reason);
        }

        private static CallToolResult OpenScheduleManagement(JObject args)
        {
            bool dryRun;
            string invalidParameter;
            if (!ReadScheduleFlags(args, out dryRun, out invalidParameter))
                return ScheduleUiResult(null, false, "unavailable", false, "invalid_boolean", invalidParameter);
            if (!dryRun && !(args["confirm"]?.Value<bool>() ?? false))
                return ScheduleUiResult(null, false, "unavailable", false, "confirm_required", "confirm");
            // The unchanged registry middleware enforces task before either public UI route invokes this handler.
            var before = ReadScheduleUiState();
            if (!before.Available)
                return ScheduleUiResult(before, false, "unavailable", false, before.Reason, dryRun: dryRun);
            if (before.MenuReportsOpen == true)
                return ScheduleUiResult(before, true, dryRun ? "preview" : "observed", false,
                    null, null, dryRun, true);
            if (before.Focused != true)
                return ScheduleUiResult(before, false, "unavailable", false, "not_focused", dryRun: dryRun);
            if (dryRun)
                return ScheduleUiResult(before, true, "preview", false, null, null, true);
            try
            {
                var dispatchFailure = DispatchUiAction(global::Action.ManageSchedule);
                if (dispatchFailure != null) return dispatchFailure;
            }
            catch
            {
                // The existing dispatcher may have sent part of the pair; do not infer an invocation count.
                return ScheduleUiResult(ReadScheduleUiState(), false, "partial", null, "dispatch_failed",
                    null, false, false, false);
            }
            var after = ReadScheduleUiState();
            if (!after.Available || !ReferenceEquals(before.Game, after.Game)
                || !ReferenceEquals(before.Menu, after.Menu) || !ReferenceEquals(before.Screen, after.Screen)
                || !ReferenceEquals(before.Manager, after.Manager))
                return ScheduleUiResult(after, false, "partial", true, "owner_state_unknown",
                    null, false, false, false);
            return ScheduleUiResult(after, true, after.MenuReportsOpen == true ? "observed" : "pending",
                true, after.MenuReportsOpen == true ? null : "not_observed_open");
        }

        private static CallToolResult ScheduleUiResult(ScheduleUiState state, bool ok, string status,
            bool? dispatched, string reason, string parameter = null, bool dryRun = false,
            bool alreadyOpen = false, bool allowObservation = true)
        {
            bool? observed = state != null && state.Available && allowObservation ? state.MenuReportsOpen : null;
            var value = new Dictionary<string, object>
            {
                ["ok"] = ok, ["requestedScreen"] = "schedule", ["action"] = global::Action.ManageSchedule.ToString(),
                ["status"] = status, ["dispatched"] = dispatched, ["observedOpen"] = observed,
                ["stateAvailable"] = state != null && state.Available, ["dryRun"] = dryRun,
                ["alreadyOpen"] = alreadyOpen, ["menuOwnsSchedule"] = state?.MenuOwnsSchedule,
                ["menuReportsOpen"] = state?.MenuReportsOpen, ["screenIsScreenActive"] = state?.ScreenActive,
                ["screenActiveAndEnabled"] = state?.ScreenEnabled, ["screenActiveInHierarchy"] = state?.ScreenInHierarchy,
                ["focused"] = state?.Focused
            };
            if (status == "observed" && observed == true) value["opened"] = "schedule";
            if (reason != null)
            {
                value["reasonCode"] = reason;
                if (parameter != null) value["parameter"] = parameter;
                value["message"] = McpLocalization.Get(reason == "not_focused" ? "ui_action.schedule_not_focused"
                    : status == "partial" ? "ui_action.schedule_partial"
                    : status == "pending" ? "ui_action.schedule_pending"
                    : reason == "invalid_boolean" || reason == "confirm_required" || reason == "invalid_screen"
                    ? "ui_action.schedule_invalid_request" : "ui_action.schedule_unavailable");
            }
            var result = CallToolResult.Text(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
            result.IsError = !ok;
            return result;
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
