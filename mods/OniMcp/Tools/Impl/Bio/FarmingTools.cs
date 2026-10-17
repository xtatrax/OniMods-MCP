using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class FarmingTools
    {
        private const int CancelEvent = 2127324410;

        public static McpTool ListPlanting()
        {
            return new McpTool
            {
                Name = "farming_planting_list",
                Group = "farming",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "planters_list", "plants_list" },
                Tags = new List<string> { "farming", "plants", "planters", "seeds", "harvest" },
                Description = "互換用の入口。colony_control domain=bio bioDomain=farming action=list を優先して使う。プランター・農耕タイルなどの PlantablePlot の栽培要求、現在の植物、受け入れ可能な種子の種類を一覧表示する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、要求中の種子、現在の植物で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var plots = Components.BuildingCompletes.Items
                        .Select(building => building?.GetComponent<PlantablePlot>())
                        .Where(plot => plot != null && ToolUtil.GameObjectMatchesWorld(plot.gameObject, worldId))
                        .Where(plot => rect == null || CellInRect(Grid.PosToCell(plot.gameObject), rect, worldId))
                        .Where(plot => PlantingMatches(plot, query))
                        .OrderBy(plot => TargetName(plot.gameObject))
                        .Take(limit)
                        .Select(PlotInfo)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = plots.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["plots"] = plots
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlPlanting()
        {
            return new McpTool
            {
                Name = "farming_planting_control",
                Group = "farming",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "planting_control", "planter_seed_control", "farm_plot_control" },
                Tags = new List<string> { "farming", "plants", "planters", "seeds", "harvest", "uproot", "batch" },
                Description = "栽培スロット、種子カタログ、収穫指定、領域内の掘り起こしの読み取り・設定を統合する。action=list_planting/seed_catalog/list_harvestables/set_harvestable/set_planting/batch_set_planting/uproot。list/set/batch とも互換性がある。",
                Parameters = RectParams(LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list_planting/list、seed_catalog/list_seeds/seeds、list_harvestables、set_harvestable、set_planting/set/plant、batch_set_planting/batch、uproot", Required = true, EnumValues = new List<string> { "list_planting", "list", "seed_catalog", "list_seeds", "seeds", "list_harvestables", "set_harvestable", "set_planting", "set", "plant", "batch_set_planting", "batch", "uproot" } },
                    ["seedTag"] = new McpToolParameter { Type = "string", Description = "set_planting/batch_set_planting かつ plantingAction=set の場合の種子の prefab/tag。例: BasicPlantSeed", Required = false },
                    ["mutationTag"] = new McpToolParameter { Type = "string", Description = "植物の変異・亜種の tag。変異を使わない場合は空欄にする", Required = false },
                    ["plantingAction"] = new McpToolParameter { Type = "string", Description = "栽培要求の操作: set または cancel。外側の action との衝突を避けるための引数。requestAction とも互換性がある", Required = false, EnumValues = new List<string> { "set", "cancel" } },
                    ["requestAction"] = new McpToolParameter { Type = "string", Description = "plantingAction の別名: set または cancel", Required = false, EnumValues = new List<string> { "set", "cancel" } },
                    ["harvestAction"] = new McpToolParameter { Type = "string", Description = "set_harvestable の操作: mark、when_ready、cancel。既定値は mark", Required = false, EnumValues = new List<string> { "mark", "when_ready", "cancel" } },
                    ["uprootAction"] = new McpToolParameter { Type = "string", Description = "uproot の操作: mark または cancel。既定値は mark", Required = false, EnumValues = new List<string> { "mark", "cancel" } },
                    ["readyOnly"] = new McpToolParameter { Type = "boolean", Description = "list_harvestables では収穫可能な対象だけを返す。set_harvestable mark では、現在収穫可能であることを要求するかを指定する", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "uproot の掘り起こし作業の優先度 1-9。既定値は 5", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "uproot で赤色の最優先にするか。既定値は false", Required = false },
                    ["emptyOnly"] = new McpToolParameter { Type = "boolean", Description = "action=batch で、現在植物のない栽培スロットだけを処理するか。既定値は true", Required = false },
                    ["removeOccupant"] = new McpToolParameter { Type = "boolean", Description = "植物がすでにある場合、先に OrderRemoveOccupant を呼び出して掘り起こすか。既定値は false", Required = false },
                    ["cropSeedOnly"] = new McpToolParameter { Type = "boolean", Description = "seed_catalog で CropSeed タグを持つ種子候補だけを返す。最終的な適合性は具体的な plot での検証が必要", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "栽培スロット・植物の名前、prefabId、現在の植物、要求中の種子で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "list/batch で返却・処理する件数の上限", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "set で removeOccupant=true の場合に必要。batch では true が必須。広範囲の uproot でも true が必須", Required = false }
                })),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "list" || action == "list_planting")
                        return ListPlanting().Handler(args);
                    if (action == "seed_catalog" || action == "list_seeds" || action == "seeds")
                        return ListSeedCatalog().Handler(args);
                    if (action == "list_harvestables")
                        return ListHarvestables().Handler(args);

                    var delegated = (JObject)args.DeepClone();
                    if (action == "set_harvestable")
                    {
                        string harvestAction = args["harvestAction"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(harvestAction))
                            delegated["action"] = harvestAction.Trim();
                        else
                            delegated.Remove("action");
                        return SetHarvestable().Handler(delegated);
                    }
                    if (action == "uproot")
                    {
                        string uprootAction = args["uprootAction"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(uprootAction))
                            delegated["action"] = uprootAction.Trim();
                        else
                            delegated.Remove("action");
                        return UprootArea().Handler(delegated);
                    }

                    string plantingAction = args["plantingAction"]?.ToString() ?? args["requestAction"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(plantingAction))
                        delegated["action"] = plantingAction.Trim();
                    else
                        delegated.Remove("action");

                    if (action == "set" || action == "set_planting" || action == "plant")
                        return SetPlanting().Handler(delegated);
                    if (action == "batch" || action == "batch_set_planting")
                        return BatchSetPlanting().Handler(delegated);
                    return CallToolResult.Error("action must be one of list_planting, seed_catalog, list_harvestables, set_harvestable, set_planting, batch_set_planting, uproot");
                }
            };
        }

        public static McpTool ListHarvestables()
        {
            return new McpTool
            {
                Name = "farming_harvestables_list",
                Group = "farming",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "plants_harvestables_list", "harvestables_list" },
                Tags = new List<string> { "farming", "plants", "harvest", "when-ready" },
                Description = "互換用の入口。colony_control domain=bio bioDomain=farming action=list_harvestables を優先して使う。収穫可能・自動収穫設定が可能な植物や作物の状態を一覧表示する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "名前や prefabId で絞り込む", Required = false },
                    ["readyOnly"] = new McpToolParameter { Type = "boolean", Description = "現在収穫可能な対象だけを返すか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    bool readyOnly = ToolUtil.GetBool(args, "readyOnly", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    // Count the filtered set before applying the response limit. A full
                    // page is not evidence that absent crops do not exist.
                    var matchingHarvestables = Components.HarvestDesignatables.Items
                        .Where(item => item != null && item.gameObject != null)
                        .Where(item => ToolUtil.GameObjectMatchesWorld(item.gameObject, worldId))
                        .Where(item => rect == null || CellInRect(Grid.PosToCell(item.gameObject), rect, worldId))
                        .Where(item => !readyOnly || item.CanBeHarvested())
                        .Where(item => HarvestableMatches(item, query))
                        .OrderBy(item => TargetName(item.gameObject))
                        .ToList();
                    var harvestables = matchingHarvestables
                        .Take(limit)
                        .Select(HarvestableInfo)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = harvestables.Count,
                        ["filteredTotal"] = matchingHarvestables.Count,
                        ["limit"] = limit,
                        ["truncated"] = matchingHarvestables.Count > harvestables.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["readyOnly"] = readyOnly,
                        ["harvestables"] = harvestables
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetHarvestable()
        {
            return new McpTool
            {
                Name = "farming_harvestable_set",
                Group = "farming",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "plant_harvest_set", "harvestable_set" },
                Tags = new List<string> { "farming", "plants", "harvest", "when-ready" },
                Description = "互換用の入口。colony_control domain=bio bioDomain=farming action=set_harvestable を優先して使う。単一の対象の収穫状態を設定する: mark、when_ready、cancel",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "mark、when_ready、cancel のいずれか。既定値は mark", Required = false, EnumValues = new List<string> { "mark", "when_ready", "cancel" } },
                    ["readyOnly"] = new McpToolParameter { Type = "boolean", Description = "action=mark で、現在収穫可能であることを要求するか。既定値は true", Required = false }
                }),
                Handler = args =>
                {
                    var harvestable = FindHarvestable(args);
                    if (harvestable == null)
                        return CallToolResult.Error("HarvestDesignatable target not found");

                    string action = (args["action"]?.ToString() ?? "mark").Trim().ToLowerInvariant();
                    if (action == "cancel")
                    {
                        harvestable.gameObject.Trigger(CancelEvent);
                        harvestable.SetHarvestWhenReady(false);
                    }
                    else if (action == "when_ready")
                    {
                        harvestable.SetHarvestWhenReady(true);
                    }
                    else
                    {
                        bool readyOnly = ToolUtil.GetBool(args, "readyOnly", true);
                        bool canHarvest = harvestable.CanBeHarvested();
                        if (!HarvestMarkPolicy.ShouldMarkNow(canHarvest, readyOnly))
                            return CallToolResult.Error("Target is not ready to harvest; use action=when_ready for future harvest");
                        harvestable.MarkForHarvest();
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(HarvestableInfo(harvestable), McpJsonUtil.Settings));
                }
            };
        }

    }
}
