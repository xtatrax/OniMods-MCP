using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class RanchingTools
    {
        private const int MaxAreaCells = 1000;

        public static McpTool ControlRanching()
        {
            return new McpTool
            {
                Name = "ranching_control",
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "critters_control", "creature_ranching_control" },
                Tags = new List<string> { "ranching", "critters", "dropoff", "incubator", "eggs", "batch" },
                Description = "牧畜の統合エントリーポイント。kind=critters/dropoff/incubator。action を対応する旧 control へそのまま渡す。",
                Parameters = RectParams(LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "牧畜の対象の種類: critters、dropoff、incubator", Required = true, EnumValues = new List<string> { "critters", "dropoff", "incubator" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "critters では critters を使う。dropoff/incubator は list/configure/batch に対応する", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "名前、prefabId、フィルターの tag、要求中の卵、中にいる対象で絞り込む", Required = false },
                    ["capturableOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=critters で現在捕獲可能な対象だけを返すか。既定値は false", Required = false },
                    ["wrangledOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=critters で拘束済みの対象だけを返すか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返却・処理する件数の上限。既定値は 100、最大 500", Required = false },
                    ["capacity"] = new McpToolParameter { Type = "integer", Description = "kind=dropoff で設定する容量。未指定なら変更しない", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "kind=dropoff のフィルター変更モード: replace、add、remove、clear。既定値は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove", "clear" } },
                    ["critterTags"] = new McpToolParameter { Type = "array", Description = "kind=dropoff の動物の prefab/tag のリスト。例: Hatch、Drecko。mode=clear では空欄にできる", Required = false },
                    ["incubatorAction"] = new McpToolParameter { Type = "string", Description = "kind=incubator では set、cancel、remove_occupant のいずれか。既定値は set", Required = false, EnumValues = new List<string> { "set", "cancel", "remove_occupant" } },
                    ["eggTag"] = new McpToolParameter { Type = "string", Description = "kind=incubator かつ incubatorAction=set の場合の卵の prefab/tag", Required = false },
                    ["autoReplace"] = new McpToolParameter { Type = "boolean", Description = "kind=incubator で連続孵化を行うか。未指定なら変更しない", Required = false },
                    ["emptyOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=incubator の batch + set で空の孵卵器だけを処理するか。既定値は true", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "内部の一括操作、フィルターの消去、容量を 0 に設定する操作、remove_occupant では true が必要", Required = false }
                })),
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (kind)
                    {
                        case "critters":
                        case "critter":
                            {
                                var forwarded = args != null ? (JObject)args.DeepClone() : new JObject();
                                forwarded["action"] = "critters";
                                return ReadRanchingControl().Handler(forwarded);
                            }
                        case "dropoff":
                        case "dropoffs":
                            return ControlDropOff().Handler(args);
                        case "incubator":
                        case "incubators":
                            return ControlIncubator().Handler(args);
                        default:
                            return CallToolResult.Error("kind must be critters, dropoff, or incubator");
                    }
                }
            };
        }

        public static McpTool ReadRanchingControl()
        {
            return new McpTool
            {
                Name = "ranching_read_control",
                Group = "ranching",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "ranching_control", "critters_read_control" },
                Tags = new List<string> { "ranching", "critters", "creatures", "capture", "wrangle" },
                Description = "牧畜の読み取り専用の集約ツール。action=critters で、マップ上の動物・捕獲可能な対象の状態を一覧表示する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "critters", Required = true, EnumValues = new List<string> { "critters" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=critters で名前や prefabId により絞り込む", Required = false },
                    ["capturableOnly"] = new McpToolParameter { Type = "boolean", Description = "action=critters で現在捕獲可能な対象だけを返すか。既定値は false", Required = false },
                    ["wrangledOnly"] = new McpToolParameter { Type = "boolean", Description = "action=critters で拘束済みの対象だけを返すか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=critters で返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "critters")
                        return ListCritters().Handler(args);
                    return CallToolResult.Error("action must be critters");
                }
            };
        }

        public static McpTool ListCritters()
        {
            return new McpTool
            {
                Name = "critters_list",
                Group = "ranching",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "ranching_critters_list", "creatures_list" },
                Tags = new List<string> { "ranching", "critters", "creatures", "capture", "wrangle" },
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=bio bioDomain=ranching action=critters を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "名前や prefabId で絞り込む", Required = false },
                    ["capturableOnly"] = new McpToolParameter { Type = "boolean", Description = "現在捕獲可能な対象だけを返すか。既定値は false", Required = false },
                    ["wrangledOnly"] = new McpToolParameter { Type = "boolean", Description = "拘束済みの対象だけを返すか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    bool capturableOnly = ToolUtil.GetBool(args, "capturableOnly", false);
                    bool wrangledOnly = ToolUtil.GetBool(args, "wrangledOnly", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var critters = Components.Capturables.Items
                        .Where(item => item != null && item.gameObject != null)
                        .Where(item => ToolUtil.GameObjectMatchesWorld(item.gameObject, worldId))
                        .Where(item => rect == null || CellInRect(Grid.PosToCell(item.gameObject), rect, worldId))
                        .Where(item => !capturableOnly || item.IsCapturable())
                        .Where(item => !wrangledOnly || (item.GetComponent<Baggable>()?.wrangled ?? false))
                        .Where(item => CritterMatches(item.gameObject, query))
                        .OrderBy(item => TargetName(item.gameObject))
                        .Take(limit)
                        .Select(CritterInfo)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = critters.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["critters"] = critters
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListDropOffs()
        {
            return new McpTool
            {
                Name = "critters_dropoff_list",
                Group = "ranching",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "ranching_dropoffs_list", "critter_dropoffs" },
                Tags = new List<string> { "ranching", "critters", "dropoff", "filters", "capacity" },
                Description = "互換用の入口。動物解放地点・魚解放地点を一覧表示する。新しい呼び出しでは colony_control domain=bio bioDomain=ranching kind=dropoff action=list を使う。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "名前、prefabId、フィルターの tag で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var items = Components.BuildingCompletes.Items
                        .Select(building => building?.GetComponent<CreatureDeliveryPoint>())
                        .Where(point => point != null && ToolUtil.GameObjectMatchesWorld(point.gameObject, worldId))
                        .Where(point => rect == null || CellInRect(Grid.PosToCell(point.gameObject), rect, worldId))
                        .Where(point => Matches(point, query))
                        .OrderBy(point => TargetName(point.gameObject))
                        .Take(limit)
                        .Select(DropOffInfo)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = items.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["dropOffs"] = items
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlDropOff()
        {
            return new McpTool
            {
                Name = "critters_dropoff_control",
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "critter_dropoff_control", "ranching_dropoff_control" },
                Tags = new List<string> { "ranching", "critters", "dropoff", "filters", "capacity", "batch" },
                Description = "動物解放地点・魚解放地点の読み取りと設定を統合する。action=list/configure/batch。configure は id/x/y による個別変更、batch は areaId または矩形による一括変更。",
                Parameters = RectParams(LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、configure、batch のいずれか。既定値は list", Required = false, EnumValues = new List<string> { "list", "configure", "batch" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "名前、prefabId、フィルターの tag で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返却・処理する件数の上限。既定値は 100、最大 500", Required = false },
                    ["capacity"] = new McpToolParameter { Type = "integer", Description = "設定する容量。未指定なら変更しない", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "フィルターの変更モード: replace、add、remove、clear。既定値は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove", "clear" } },
                    ["critterTags"] = new McpToolParameter { Type = "array", Description = "動物の prefab/tag のリスト。例: Hatch、Drecko。mode=clear では空欄にできる", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "batch、フィルターの消去、容量を 0 に設定する場合は true が必須", Required = false }
                })),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "list").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListDropOffs().Handler(args);
                    if (action == "configure" || action == "set")
                        return ConfigureDropOff().Handler(args);
                    if (action == "batch")
                        return BatchConfigureDropOffs().Handler(args);
                    return CallToolResult.Error("action must be list, configure, or batch");
                }
            };
        }

        public static McpTool ListIncubators()
        {
            return new McpTool
            {
                Name = "incubators_list",
                Group = "ranching",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "egg_incubators_list", "ranching_incubators_list" },
                Tags = new List<string> { "ranching", "eggs", "incubator", "receptacle" },
                Description = "互換用の入口。孵卵器の卵の要求、中にある卵・幼体、進捗、連続孵化の設定を一覧表示する。新しい呼び出しでは colony_control domain=bio bioDomain=ranching kind=incubator action=list を使う。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、要求中の卵、現在入っている対象で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var incubators = Components.BuildingCompletes.Items
                        .Select(building => building?.GetComponent<EggIncubator>())
                        .Where(incubator => incubator != null && ToolUtil.GameObjectMatchesWorld(incubator.gameObject, worldId))
                        .Where(incubator => rect == null || CellInRect(Grid.PosToCell(incubator.gameObject), rect, worldId))
                        .Where(incubator => IncubatorMatches(incubator, query))
                        .OrderBy(incubator => TargetName(incubator.gameObject))
                        .Take(limit)
                        .Select(IncubatorInfo)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = incubators.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["incubators"] = incubators
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlIncubator()
        {
            return new McpTool
            {
                Name = "incubator_control",
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "incubators_control", "egg_incubator_control", "ranching_incubator_control" },
                Tags = new List<string> { "ranching", "eggs", "incubator", "receptacle", "batch" },
                Description = "孵卵器の読み取りと設定を統合する。action=list/configure/batch。incubatorAction=set/cancel/remove_occupant で卵の要求に関する操作を指定する。",
                Parameters = RectParams(LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、configure、batch のいずれか。既定値は list", Required = false, EnumValues = new List<string> { "list", "configure", "batch" } },
                    ["incubatorAction"] = new McpToolParameter { Type = "string", Description = "set、cancel、remove_occupant のいずれか。既定値は set。旧個別ツールでは引き続き action でこの値を指定する", Required = false, EnumValues = new List<string> { "set", "cancel", "remove_occupant" } },
                    ["eggTag"] = new McpToolParameter { Type = "string", Description = "卵の prefab/tag。incubatorAction=set では必須", Required = false },
                    ["autoReplace"] = new McpToolParameter { Type = "boolean", Description = "連続で孵化するか。未指定なら変更しない", Required = false },
                    ["emptyOnly"] = new McpToolParameter { Type = "boolean", Description = "batch + set で空の孵卵器だけを処理するか。既定値は true", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "孵卵器、要求中の卵、現在入っている対象で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返却・処理する件数の上限。既定値は 100、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "batch または remove_occupant では true が必須", Required = false }
                })),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "list").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListIncubators().Handler(args);
                    if (action == "configure" || action == "set" || action == "cancel" || action == "remove_occupant")
                        return ConfigureIncubator().Handler(IncubatorDispatchArgs(args, action));
                    if (action == "batch")
                        return BatchConfigureIncubators().Handler(IncubatorDispatchArgs(args, action));
                    return CallToolResult.Error("action must be list, configure, or batch");
                }
            };
        }

    }
}
