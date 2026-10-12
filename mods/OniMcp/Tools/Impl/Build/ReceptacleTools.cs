using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class ReceptacleTools
    {
        public static McpTool ListReceptacles()
        {
            return new McpTool
            {
                Name = "receptacles_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "single_entity_receptacles_list", "receptacle_side_screens_list" },
                Tags = new List<string> { "buildings", "side-screen", "receptacle", "single-entity", "pedestal", "rocket", "cargo" },
                Description = "互換用の入口。building_control domain=receptacle action=list を優先してください。特殊なロケット貨物室を含む ReceptacleSideScreen / SingleEntityReceptacle の汎用エンティティ要求コントロールを列挙します。プランターと孵卵器は含みません",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、要求対象、現在の occupant で絞り込み", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "要求可能なエンティティの選択肢を返すか。既定は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    bool includeOptions = ToolUtil.GetBool(args, "includeOptions", true);
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var rows = AllReceptacles()
                        .Where(item => MatchesTarget(item.gameObject, rect, worldId))
                        .Select(item => ReceptacleInfo(item, includeOptions))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = rows.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["receptacles"] = rows
                    });
                }
            };
        }

        public static McpTool ControlReceptacle()
        {
            return new McpTool
            {
                Name = "receptacle_control",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "single_entity_receptacle_control", "receptacle_side_screen_control" },
                Tags = new List<string> { "buildings", "side-screen", "receptacle", "single-entity", "rocket", "cargo" },
                Description = "ReceptacleSideScreen の読み取り、個別操作、一括操作を統合。action=list/request/cancel_request/remove_occupant/cancel_remove/batch。書き込み操作には confirm=true が必要です。",
                Parameters = ReceptacleControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(action) || action == "list")
                        return ListReceptacles().Handler(args);
                    if (action == "batch")
                        return BatchControlReceptacles().Handler(args);

                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for receptacle changes");

                    var receptacle = FindReceptacle(args);
                    if (receptacle == null)
                        return CallToolResult.Error("SingleEntityReceptacle target not found");

                    var before = ReceptacleInfo(receptacle, includeOptions: false);
                    var error = ApplyReceptacleAction(receptacle, args);
                    if (error != null)
                        return CallToolResult.Error(error);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(receptacle.gameObject),
                        ["before"] = before,
                        ["receptacle"] = ReceptacleInfo(receptacle, includeOptions: true)
                    });
                }
            };
        }

        public static McpTool BatchControlReceptacles()
        {
            return new McpTool
            {
                Name = "receptacles_batch_control",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "single_entity_receptacles_batch_control" },
                Tags = new List<string> { "buildings", "side-screen", "receptacle", "batch" },
                Description = "互換用の入口。building_control domain=receptacle action=batch を優先してください。ReceptacleSideScreen 操作を一括実行します。items は短縮フィールド a/tag/w に対応し、defaults で action/entityTag/worldId を共有可能。confirm=true が必要です",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "配列。各項目は id または x/y/worldId と action、entityTag/additionalTag などの引数を指定。短縮フィールドは a=action、tag=entityTag、w=worldId", Required = true },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "各項目に結合する既定の引数。action/a、entityTag/tag、worldId/w に対応。個別項目の引数を優先", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "エンティティ要求の一括変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for receptacle batch changes");

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

                        var item = MergeReceptacleDefaults(rawItem, defaults);
                        var receptacle = FindReceptacle(item);
                        if (receptacle == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "SingleEntityReceptacle target not found", ["input"] = item });
                            continue;
                        }

                        var before = ReceptacleInfo(receptacle, includeOptions: false);
                        var error = ApplyReceptacleAction(receptacle, item);
                        results.Add(new Dictionary<string, object>
                        {
                            ["ok"] = error == null,
                            ["error"] = error,
                            ["target"] = TargetInfo(receptacle.gameObject),
                            ["before"] = before,
                            ["receptacle"] = ReceptacleInfo(receptacle, includeOptions: false)
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

    }
}
