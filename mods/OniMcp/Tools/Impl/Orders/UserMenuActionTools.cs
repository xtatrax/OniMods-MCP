using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class UserMenuActionTools
    {
        private static readonly List<ActionSpec> Specs = new List<ActionSpec>
        {
            Spec<Constructable>("cancel_construction", "Cancel construction", "OnPressCancel", "orders"),
            Spec<Diggable>("cancel_dig", "Cancel dig order", "OnCancel", "orders"),
            Spec<Moppable>("cancel_mop", "Cancel mop order", "OnCancel", "orders"),
            Spec<Deconstructable>("toggle_deconstruct", "Mark/cancel deconstruction", "OnDeconstruct", "orders"),
            Spec<CancellableMove>("cancel_move_delivery", "Cancel grouped pickupable move delivery", "CancelAll", "orders"),
            Spec<Clearable>("toggle_clear", "Mark/cancel sweep-to-storage", "OnClickClear", "orders"),
            Spec<Clearable>("cancel_clear", "Cancel sweep-to-storage", "OnClickCancel", "orders"),
            Spec<Movable>("toggle_move_pickupable", "Move/cancel pickupable move", "OnClickMove", "orders"),
            Spec<Movable>("cancel_move_pickupable", "Cancel pickupable move", "OnClickCancel", "orders"),
            Spec<Navigator>("toggle_navigation_paths", "Show/hide navigation paths for selected navigator", "OnDrawPaths", "navigation"),
            Spec<Navigator>("follow_navigator", "Toggle camera follow for selected navigator", "OnFollowCam", "navigation"),
            Spec<AutoDisinfectable>("enable_auto_disinfect", "Enable auto-disinfect", "EnableAutoDisinfect", "care"),
            Spec<AutoDisinfectable>("disable_auto_disinfect", "Disable auto-disinfect", "DisableAutoDisinfect", "care"),
            Spec<Repairable>("allow_auto_repair", "Allow auto-repair", "AllowRepair", "maintenance"),
            Spec<Repairable>("cancel_auto_repair", "Disable/cancel auto-repair", "CancelRepair", "maintenance"),
            Spec<Compostable>("toggle_compost", "Mark/cancel compost", "OnToggleCompost", "resources"),
            Spec<Dumpable>("toggle_dump", "Dump/cancel dump", "ToggleDumping", "resources"),
            Spec<DropAllWorkable>("toggle_empty_storage", "Empty/cancel empty storage", "DropAll", "resources"),
            Spec<SubstanceChunk>("release_element", "Release element chunk to world", "OnRelease", "resources"),
            Spec<Butcherable>("butcher", "Meatify critter", "OnClickButcher", "ranching"),
            Spec<Butcherable>("cancel_butcher", "Cancel meatify", "OnClickCancel", "ranching"),
            Spec<Carvable>("carve", "Carve object", "OnClickCarve", "orders"),
            Spec<Carvable>("cancel_carve", "Cancel carve", "OnClickCancelCarve", "orders"),
            Spec<Uprootable>("uproot", "Mark plant/object for uproot", "OnClickUproot", "farming"),
            Spec<Uprootable>("cancel_uproot", "Cancel plant/object uproot", "OnClickCancelUproot", "farming"),
            Spec<Demolishable>("toggle_demolish", "Mark/cancel demolition", "OnDemolish", "orders"),
            Spec<Demolishable>("cancel_demolish", "Cancel demolition", "CancelDemolition", "orders"),
            Spec<BottleEmptier>("toggle_manual_pump_delivery", "Allow/deny manual pump delivery", "OnChangeAllowManualPumpingStationFetching", "buildings"),
            Spec<SuitMarker>("only_traverse_when_suit_available", "Require suit dock availability before traversal", "OnEnableTraverseIfUnequipAvailable", "buildings"),
            Spec<SuitMarker>("always_allow_traversal", "Always allow suit checkpoint traversal", "OnDisableTraverseIfUnequipAvailable", "buildings"),
            Spec<Tinkerable>("toggle_tinker", "Allow/disallow tinker operation", "OnClickToggleTinker", "buildings"),
            Spec<ConnectionManager>("toggle_geothermal_connection", "Reconnect/disconnect geothermal controller", "OnMenuToggle", "story")
        };

        public static McpTool ListUserMenuActions()
        {
            return new McpTool
            {
                Name = "user_menu_actions_list",
                Group = "controls",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "context_menu_actions_list", "object_user_buttons_list" },
                Tags = new List<string> { "controls", "user-menu", "context-menu", "buttons", "actions" },
                Description = "互換用の入口。building_control domain=side_surface surface=user_menu action=list を優先して使う。対応付け済みのオブジェクトの UserMenu ボタン操作を一覧表示する。掃除・移動・修理・堆肥化・内容物を空にする・彫刻など、サイドパネル以外のボタンが対象",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "オブジェクト名、prefabId、actionKey、説明、コンポーネントの種類で絞り込む", Required = false },
                    ["category"] = new McpToolParameter { Type = "string", Description = "分類で絞り込む。例: orders、resources、buildings、ranching、care", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返すオブジェクト数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    string category = (args["category"]?.ToString() ?? "").Trim();
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var rows = AllCandidateObjects()
                        .Where(go => MatchesTarget(go, rect, worldId))
                        .Select(go => TargetActionsInfo(go, category))
                        .Where(info => ((List<Dictionary<string, object>>)info["actions"]).Count > 0)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = rows.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["category"] = string.IsNullOrWhiteSpace(category) ? "any" : category,
                        ["targets"] = rows
                    });
                }
            };
        }

        public static McpTool PressUserMenuAction()
        {
            return new McpTool
            {
                Name = "user_menu_action_press",
                Group = "controls",
                Mode = "write",
                Risk = "high",
                Hidden = true,
                Aliases = new List<string> { "context_menu_action_press", "object_user_button_press" },
                Tags = new List<string> { "controls", "user-menu", "context-menu", "buttons", "actions" },
                Description = "互換用の入口。building_control domain=side_surface surface=user_menu action=press を優先して使う。対応付け済みのオブジェクトの UserMenu ボタン操作を実行する。サイドパネル以外のボタン用。先に action=list で actionKey を照会する必要があり、confirm=true も必要",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["actionKey"] = new McpToolParameter { Type = "string", Description = "実行する actionKey。例: toggle_compost、toggle_dump、allow_auto_repair", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "オブジェクトの UserMenu 操作の実行を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for user menu actions");

                    var go = FindTarget(args);
                    if (go == null)
                        return CallToolResult.Error("Target not found");

                    string actionKey = args["actionKey"]?.ToString();
                    var spec = FindSpec(go, actionKey);
                    if (spec == null)
                        return CallToolResult.Error("actionKey is not available on target");

                    var before = TargetActionsInfo(go, "");
                    string error = InvokeSpec(go, spec);
                    if (error != null)
                        return CallToolResult.Error(error);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["pressed"] = ActionInfo(spec),
                        ["before"] = before,
                        ["after"] = TargetActionsInfo(go, "")
                    });
                }
            };
        }

        public static McpTool BatchPressUserMenuActions()
        {
            return new McpTool
            {
                Name = "user_menu_actions_batch_press",
                Group = "controls",
                Mode = "write",
                Risk = "high",
                Hidden = true,
                Aliases = new List<string> { "context_menu_actions_batch_press" },
                Tags = new List<string> { "controls", "user-menu", "context-menu", "batch" },
                Description = "互換用の入口。building_control domain=side_surface surface=user_menu action=batch を優先して使う。対応付け済みのオブジェクトの UserMenu ボタン操作を一括実行する。items は {actionKey|a,id/x/y/worldId|w} に対応し、defaults で actionKey/worldId を共有できる。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "配列。各項目は id または x/y/worldId を指定でき、actionKey または短縮フィールド a を指定する", Required = true },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "各項目にマージする既定パラメーター。actionKey/a、worldId/w に対応し、各項目のパラメーターを優先する", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "オブジェクトの UserMenu 操作を一括実行することを確認するため、true が必要", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for user menu batch actions");
                    var items = args["items"] as JArray;
                    if (items == null || items.Count == 0)
                        return CallToolResult.Error("items array is required");

                    var defaults = args["defaults"] as JObject ?? args["defaultArguments"] as JObject;
                    var results = new List<Dictionary<string, object>>();
                    foreach (var token in items)
                    {
                        var rawItem = token as JObject;
                        if (rawItem == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "item must be an object" });
                            continue;
                        }
                        var item = MergeBatchDefaults(rawItem, defaults);
                        var go = FindTarget(item);
                        if (go == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "Target not found", ["input"] = item });
                            continue;
                        }
                        var spec = FindSpec(go, item["actionKey"]?.ToString());
                        if (spec == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "actionKey is not available on target", ["target"] = TargetInfo(go), ["input"] = item });
                            continue;
                        }
                        string error = InvokeSpec(go, spec);
                        results.Add(new Dictionary<string, object>
                        {
                            ["ok"] = error == null,
                            ["error"] = error,
                            ["target"] = TargetInfo(go),
                            ["pressed"] = ActionInfo(spec)
                        });
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["requested"] = items.Count,
                        ["succeeded"] = results.Count(item => (bool)item["ok"]),
                        ["failed"] = results.Count(item => !(bool)item["ok"]),
                        ["results"] = results
                    });
                }
            };
        }

        public static McpTool ControlUserMenuAction()
        {
            return new McpTool
            {
                Name = "user_menu_action_control",
                Group = "controls",
                Mode = "write",
                Risk = "high",
                Aliases = new List<string> { "context_menu_action_control", "object_user_button_control" },
                Tags = new List<string> { "controls", "user-menu", "context-menu", "buttons", "actions", "batch" },
                Description = "オブジェクトの UserMenu ボタン操作を参照、実行、一括実行する統合ツール。action=list/press/batch。press/batch には confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、press、batch", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list の場合、オブジェクト名、prefabId、actionKey、説明、コンポーネント型で絞り込む", Required = false },
                    ["category"] = new McpToolParameter { Type = "string", Description = "action=list の場合、orders、resources、buildings、ranching、care などの分類で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list の場合に返すオブジェクト数の上限。既定値 100、最大 500", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=press の場合の対象 KPrefabID InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象の X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象の Y", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定値は現在のワールド、または対象のセルがあるワールド", Required = false },
                    ["actionKey"] = new McpToolParameter { Type = "string", Description = "action=press の場合に実行する actionKey。一括操作の項目では actionKey または a を使用できる", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Description = "action=batch の場合の配列。各項目は id または x/y/worldId を指定でき、actionKey または短縮フィールド a を指定する", Required = false },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "action=batch で各項目へ統合する既定の引数", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=press/batch の場合は true が必要", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListUserMenuActions().Handler(args);
                    if (action == "press")
                        return PressUserMenuAction().Handler(args);
                    if (action == "batch")
                        return BatchPressUserMenuActions().Handler(args);
                    return CallToolResult.Error("action must be one of list, press, batch");
                }
            };
        }

    }
}
