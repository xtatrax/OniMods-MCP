using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class RanchingTools
    {
        public static McpTool ConfigureIncubator()
        {
            return new McpTool
            {
                Name = "incubator_configure",
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "egg_incubator_set", "ranching_incubator_configure" },
                Tags = new List<string> { "ranching", "eggs", "incubator", "receptacle" },
                Description = "互換用の入口。孵卵器の卵の要求、連続孵化、中にいる対象の取り出しを設定する。新しい呼び出しでは colony_control domain=bio bioDomain=ranching kind=incubator action=configure を使う。",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["eggTag"] = new McpToolParameter { Type = "string", Description = "卵の prefab/tag。action=set では必須", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "set、cancel、remove_occupant のいずれか。既定値は set", Required = false, EnumValues = new List<string> { "set", "cancel", "remove_occupant" } },
                    ["autoReplace"] = new McpToolParameter { Type = "boolean", Description = "連続で孵化するか。未指定なら変更しない", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "remove_occupant では true が必須", Required = false }
                }),
                Handler = args =>
                {
                    var incubator = FindIncubator(args);
                    if (incubator == null)
                        return CallToolResult.Error("EggIncubator target not found");

                    if (args["autoReplace"] != null)
                        incubator.autoReplaceEntity = ToolUtil.GetBool(args, "autoReplace", false);

                    string action = (args["action"]?.ToString() ?? "set").Trim().ToLowerInvariant();
                    if (action == "cancel")
                    {
                        incubator.CancelActiveRequest();
                    }
                    else if (action == "remove_occupant")
                    {
                        if (!ToolUtil.GetBool(args, "confirm", false))
                            return CallToolResult.Error("confirm=true is required for remove_occupant");
                        incubator.OrderRemoveOccupant();
                    }
                    else
                    {
                        string eggName = args["eggTag"]?.ToString();
                        if (string.IsNullOrWhiteSpace(eggName))
                            return CallToolResult.Error("eggTag is required for action=set");
                        var eggTag = TagManager.Create(eggName.Trim());
                        var eggPrefab = Assets.GetPrefab(eggTag);
                        if (eggPrefab == null || !eggPrefab.HasTag(GameTags.Egg))
                            return CallToolResult.Error("eggTag is not an egg prefab");
                        if (!incubator.HasDepositTag(eggTag) || !incubator.IsValidEntity(eggPrefab))
                            return CallToolResult.Error("Egg is not valid for this incubator");
                        incubator.CancelActiveRequest();
                        incubator.CreateOrder(eggTag, Tag.Invalid);
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(IncubatorInfo(incubator), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ConfigureDropOff()
        {
            return new McpTool
            {
                Name = "critters_dropoff_configure",
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "ranching_dropoff_set", "critter_dropoff_filter_set" },
                Tags = new List<string> { "ranching", "critters", "dropoff", "filters", "capacity" },
                Description = "互換用の入口。動物解放地点・魚解放地点の種別フィルターと容量上限を設定する。新しい呼び出しでは colony_control domain=bio bioDomain=ranching kind=dropoff action=configure を使う。",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["capacity"] = new McpToolParameter { Type = "integer", Description = "設定する容量。未指定なら変更しない", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "フィルターの変更モード: replace、add、remove、clear。既定値は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove", "clear" } },
                    ["critterTags"] = new McpToolParameter { Type = "array", Description = "動物の prefab/tag のリスト。例: Hatch、Drecko。mode=clear では空欄にできる", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "フィルターを空にする場合や容量を 0 にする場合は true が必須", Required = false }
                }),
                Handler = args =>
                {
                    var point = FindDropOff(args);
                    if (point == null)
                        return CallToolResult.Error("CreatureDeliveryPoint target not found");

                    var error = ApplyDropOffConfig(point, args);
                    if (error != null)
                        return CallToolResult.Error(error);
                    return CallToolResult.Text(JsonConvert.SerializeObject(DropOffInfo(point), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool BatchConfigureDropOffs()
        {
            return new McpTool
            {
                Name = "critters_dropoff_batch_configure",
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "ranching_dropoffs_batch_set", "critter_dropoffs_batch_configure" },
                Tags = new List<string> { "ranching", "critters", "dropoff", "filters", "capacity", "batch" },
                Description = "互換用の入口。領域内の動物・魚の解放地点の容量と種別フィルターを一括設定する。新しい呼び出しでは colony_control domain=bio bioDomain=ranching kind=dropoff action=batch を使う。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["capacity"] = new McpToolParameter { Type = "integer", Description = "設定する容量。未指定なら変更しない", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "フィルターの変更モード: replace、add、remove、clear。既定値は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove", "clear" } },
                    ["critterTags"] = new McpToolParameter { Type = "array", Description = "動物の prefab/tag のリスト。例: Hatch、Drecko。mode=clear では空欄にできる", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "解放地点の名前、prefabId、フィルターの tag で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "処理する件数の上限。既定値は 100、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "true が必須。フィルターを空にする場合や容量を 0 にする場合も true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to batch configure critter drop-offs");

                    var rect = ToolUtil.GetRect(args);
                    int worldId = ToolUtil.ResolveWorldId(args);
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));
                    var changed = new List<Dictionary<string, object>>();
                    foreach (var point in Components.BuildingCompletes.Items
                                 .Select(building => building?.GetComponent<CreatureDeliveryPoint>())
                                 .Where(point => point != null && ToolUtil.GameObjectMatchesWorld(point.gameObject, worldId))
                                 .Where(point => CellInRect(Grid.PosToCell(point.gameObject), rect, worldId))
                                 .Where(point => Matches(point, query))
                                 .Take(limit))
                    {
                        var error = ApplyDropOffConfig(point, args);
                        if (error != null)
                            return CallToolResult.Error(error);
                        changed.Add(DropOffInfo(point));
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changed"] = changed.Count,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["dropOffs"] = changed
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool BatchConfigureIncubators()
        {
            return new McpTool
            {
                Name = "incubators_batch_configure",
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "egg_incubators_batch_set", "ranching_incubators_batch_configure" },
                Tags = new List<string> { "ranching", "eggs", "incubator", "receptacle", "batch" },
                Description = "互換用の入口。領域内の孵卵器の卵の要求、連続孵化、要求の取り消しを一括設定する。新しい呼び出しでは colony_control domain=bio bioDomain=ranching kind=incubator action=batch を使う。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["eggTag"] = new McpToolParameter { Type = "string", Description = "卵の prefab/tag。action=set では必須", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "set、cancel、remove_occupant のいずれか。既定値は set", Required = false, EnumValues = new List<string> { "set", "cancel", "remove_occupant" } },
                    ["autoReplace"] = new McpToolParameter { Type = "boolean", Description = "連続で孵化するか。未指定なら変更しない", Required = false },
                    ["emptyOnly"] = new McpToolParameter { Type = "boolean", Description = "action=set で空の孵卵器だけを処理するか。既定値は true", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "孵卵器、要求中の卵、現在入っている対象で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "処理する件数の上限。既定値は 100、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "true が必須。remove_occupant でも true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to batch configure incubators");

                    string action = (args["action"]?.ToString() ?? "set").Trim().ToLowerInvariant();
                    if (action != "set" && action != "cancel" && action != "remove_occupant")
                        return CallToolResult.Error("action must be set, cancel, or remove_occupant");
                    Tag eggTag = Tag.Invalid;
                    GameObject eggPrefab = null;
                    if (action == "set")
                    {
                        string eggName = args["eggTag"]?.ToString();
                        if (string.IsNullOrWhiteSpace(eggName))
                            return CallToolResult.Error("eggTag is required for action=set");
                        eggTag = TagManager.Create(eggName.Trim());
                        eggPrefab = Assets.GetPrefab(eggTag);
                        if (eggPrefab == null || !eggPrefab.HasTag(GameTags.Egg))
                            return CallToolResult.Error("eggTag is not an egg prefab");
                    }

                    var rect = ToolUtil.GetRect(args);
                    int worldId = ToolUtil.ResolveWorldId(args);
                    string query = args["query"]?.ToString();
                    bool emptyOnly = ToolUtil.GetBool(args, "emptyOnly", true);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));
                    var changed = new List<Dictionary<string, object>>();
                    foreach (var incubator in Components.BuildingCompletes.Items
                                 .Select(building => building?.GetComponent<EggIncubator>())
                                 .Where(incubator => incubator != null && ToolUtil.GameObjectMatchesWorld(incubator.gameObject, worldId))
                                 .Where(incubator => CellInRect(Grid.PosToCell(incubator.gameObject), rect, worldId))
                                 .Where(incubator => IncubatorMatches(incubator, query))
                                 .Take(limit))
                    {
                        if (args["autoReplace"] != null)
                            incubator.autoReplaceEntity = ToolUtil.GetBool(args, "autoReplace", false);
                        if (action == "cancel")
                        {
                            incubator.CancelActiveRequest();
                        }
                        else if (action == "remove_occupant")
                        {
                            incubator.OrderRemoveOccupant();
                        }
                        else
                        {
                            if (emptyOnly && incubator.Occupant != null)
                                continue;
                            if (!incubator.HasDepositTag(eggTag) || !incubator.IsValidEntity(eggPrefab))
                                continue;
                            incubator.CancelActiveRequest();
                            incubator.CreateOrder(eggTag, Tag.Invalid);
                        }
                        changed.Add(IncubatorInfo(incubator));
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["action"] = action,
                        ["eggTag"] = eggTag.IsValid ? eggTag.Name : null,
                        ["changed"] = changed.Count,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["incubators"] = changed
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static JObject IncubatorDispatchArgs(JObject args, string controlAction)
        {
            var dispatched = (JObject)args.DeepClone();
            string incubatorAction = args["incubatorAction"]?.ToString();
            if (string.IsNullOrWhiteSpace(incubatorAction))
            {
                incubatorAction = controlAction == "cancel" || controlAction == "remove_occupant"
                    ? controlAction
                    : "set";
            }
            dispatched["action"] = incubatorAction;
            return dispatched;
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標で検索する場合の既定値は現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の領域ハンドル。指定すると x1/y1/x2/y2 を省略できる", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の左下・始点の X。areaId を使う場合は省略できる", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の左下・始点の Y。areaId を使う場合は省略できる", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の右上・終点の X。areaId を使う場合は省略できる", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の右上・終点の Y。areaId を使う場合は省略できる", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定値は areaId に関連付けられたワールド、または現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

    }
}
