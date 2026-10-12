using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class MaintenanceActionTools
    {
        private static readonly FieldInfo TravelTubeUseWaxField = typeof(TravelTubeEntrance).GetField("deliverAndUseWax", BindingFlags.Instance | BindingFlags.NonPublic);

        public static McpTool ListMaintenanceActions()
        {
            return new McpTool
            {
                Name = "maintenance_actions_list",
                Group = "controls",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "focused_user_menu_actions_list", "service_actions_list" },
                Tags = new List<string> { "controls", "maintenance", "user-menu", "toilet", "desalinator", "equipment", "hive", "cargo", "travel-tube" },
                Description = "互換用エントリー。building_control domain=side_surface surface=maintenance action=list を優先して使用すること。状態機械やスロットのパラメーターを必要とする、プレイヤー向けメンテナンスの UserMenu 操作を一覧表示する。トイレの清掃、淡水化装置を空にする操作、移動チューブのワックス、ハチの巣を空にする操作、貨物室の荷下ろし、複製人間の装備解除に対応する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "オブジェクト名、prefabId、actionKey、コンポーネント、説明で絞り込む", Required = false },
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
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var targets = AllCandidateObjects()
                        .Where(go => MatchesTarget(go, rect, worldId))
                        .Select(TargetActionsInfo)
                        .Where(info => ((List<Dictionary<string, object>>)info["actions"]).Count > 0)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    var dupes = Components.LiveMinionIdentities.Items
                        .Where(dupe => dupe != null)
                        .Where(dupe => MatchesTarget(dupe.gameObject, rect, worldId))
                        .Select(DupeEquipmentInfo)
                        .Where(info => ((List<Dictionary<string, object>>)info["actions"]).Count > 0)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returnedTargets"] = targets.Count,
                        ["returnedDupes"] = dupes.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["targets"] = targets,
                        ["dupes"] = dupes,
                        ["executeTool"] = "building_control domain=side_surface surface=maintenance action=execute",
                        ["batchTool"] = "building_control domain=side_surface surface=maintenance action=batch"
                    });
                }
            };
        }

        public static McpTool ExecuteMaintenanceAction()
        {
            return new McpTool
            {
                Name = "maintenance_action_execute",
                Group = "controls",
                Mode = "write",
                Risk = "high",
                Hidden = true,
                Aliases = new List<string> { "focused_user_menu_action_execute", "service_action_execute" },
                Tags = new List<string> { "controls", "maintenance", "user-menu", "state-machine", "equipment" },
                Description = "互換用エントリー。building_control domain=side_surface surface=maintenance action=execute を優先して使用すること。プレイヤー向けメンテナンス操作を実行する。actionKey は clean_toilet、empty_desalinator、set_transit_tube_wax、set_hive_harvest、empty_cargo_bay、unequip_dupe_equipment に対応し、confirm=true が必要",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["actionKey"] = new McpToolParameter { Type = "string", Description = "メンテナンス操作の key", Required = true },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "set_transit_tube_wax / set_hive_harvest の目標状態", Required = false },
                    ["slotId"] = new McpToolParameter { Type = "string", Description = "unequip_dupe_equipment の装備スロット ID。例: Suit/Outfit/Shoes。代わりに equipmentId/equipmentPrefab/query を使用できる", Required = false },
                    ["equipmentId"] = new McpToolParameter { Type = "integer", Description = "unequip_dupe_equipment の装備 KPrefabID.InstanceID", Required = false },
                    ["equipmentPrefab"] = new McpToolParameter { Type = "string", Description = "unequip_dupe_equipment の装備 prefabId", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "unequip_dupe_equipment で装備名、prefab、スロットをあいまい検索する", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "プレイヤー操作の実行を確認するため、true が必要", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for maintenance actions");

                    var before = SnapshotForArgs(args);
                    string error = Execute(args, out Dictionary<string, object> target);
                    if (error != null)
                        return CallToolResult.Error(error);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["ok"] = true,
                        ["actionKey"] = args["actionKey"]?.ToString(),
                        ["target"] = target,
                        ["before"] = before,
                        ["after"] = SnapshotForArgs(args)
                    });
                }
            };
        }

        public static McpTool BatchExecuteMaintenanceActions()
        {
            return new McpTool
            {
                Name = "maintenance_actions_batch_execute",
                Group = "controls",
                Mode = "write",
                Risk = "high",
                Hidden = true,
                Aliases = new List<string> { "focused_user_menu_actions_batch_execute", "service_actions_batch_execute" },
                Tags = new List<string> { "controls", "maintenance", "user-menu", "batch" },
                Description = "互換用エントリー。building_control domain=side_surface surface=maintenance action=batch を優先して使用すること。プレイヤー向けメンテナンス操作を一括実行する。items は {actionKey,id/x/y/worldId/...} または短縮フィールド {a,id/x/y/w/...} に対応する。defaults で actionKey/worldId/enabled/slotId を共有できる。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "操作の配列。各項目に actionKey または a を指定する", Required = true },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "各項目にマージする既定パラメーター。actionKey/a、worldId/w、enabled/e、slotId/slot に対応し、各項目のパラメーターを優先する", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "プレイヤー操作の一括実行を確認するため、true が必要", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for maintenance action batches");
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
                        string error = Execute(item, out Dictionary<string, object> target);
                        results.Add(new Dictionary<string, object>
                        {
                            ["ok"] = error == null,
                            ["error"] = error,
                            ["actionKey"] = item["actionKey"]?.ToString(),
                            ["target"] = target
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

        public static McpTool ControlMaintenanceAction()
        {
            return new McpTool
            {
                Name = "maintenance_action_control",
                Group = "controls",
                Mode = "write",
                Risk = "high",
                Aliases = new List<string> { "focused_user_menu_action_control", "service_action_control" },
                Tags = new List<string> { "controls", "maintenance", "user-menu", "state-machine", "equipment", "batch" },
                Description = "プレイヤー向けメンテナンス操作を参照、実行、一括実行する統合ツール。action=list/execute/batch。execute/batch には confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、execute、batch", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list または unequip_dupe_equipment の場合の検索語", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list の場合に返すオブジェクト数の上限。既定値 100、最大 500", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=execute の場合の対象 KPrefabID InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象の X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象の Y", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定値は現在のワールド、または対象のセルがあるワールド", Required = false },
                    ["actionKey"] = new McpToolParameter { Type = "string", Description = "action=execute の場合のメンテナンス操作の key。一括操作の項目では actionKey または a を使用できる", Required = false },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "set_transit_tube_wax / set_hive_harvest の目標状態", Required = false },
                    ["slotId"] = new McpToolParameter { Type = "string", Description = "unequip_dupe_equipment の装備スロット ID。例: Suit/Outfit/Shoes", Required = false },
                    ["equipmentId"] = new McpToolParameter { Type = "integer", Description = "unequip_dupe_equipment の装備 KPrefabID.InstanceID", Required = false },
                    ["equipmentPrefab"] = new McpToolParameter { Type = "string", Description = "unequip_dupe_equipment の装備 prefabId", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Description = "action=batch の場合の操作配列。各項目に actionKey または a を指定する", Required = false },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "action=batch で各項目へ統合する既定の引数", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=execute/batch の場合は true が必要", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListMaintenanceActions().Handler(args);
                    if (action == "execute")
                        return ExecuteMaintenanceAction().Handler(args);
                    if (action == "batch")
                        return BatchExecuteMaintenanceActions().Handler(args);
                    return CallToolResult.Error("action must be one of list, execute, batch");
                }
            };
        }

    }
}
