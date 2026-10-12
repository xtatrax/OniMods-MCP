using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class ReceptacleTools
    {
        public static McpTool ListStorageTileSelections()
        {
            return new McpTool
            {
                Name = "storage_tile_selections_list",
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "single_item_selection_list", "storage_tiles_target_items_list" },
                Tags = new List<string> { "resources", "storage", "side-screen", "single-item", "tile" },
                Description = "互換用の入口。building_control domain=tile_selection action=list を優先してください。SingleItemSelectionSideScreen / StorageTile の対象アイテム選択、容量、現在の内容、選択可能なアイテムを列挙します",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、対象アイテム、選択可能なアイテムで絞り込み", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "選択可能なアイテムを返すか。既定は true", Required = false },
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

                    var rows = AllStorageTiles()
                        .Where(item => MatchesTarget(item.gameObject, rect, worldId))
                        .Select(item => StorageTileInfo(item, includeOptions))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = rows.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["storageTiles"] = rows
                    });
                }
            };
        }

        public static McpTool SetStorageTileSelection()
        {
            return new McpTool
            {
                Name = "storage_tile_selection_set",
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "single_item_selection_set", "storage_tile_target_item_set" },
                Tags = new List<string> { "resources", "storage", "side-screen", "single-item", "tile" },
                Description = "互換用の入口。building_control domain=tile_selection action=set を優先してください。SingleItemSelectionSideScreen / StorageTile の対象アイテムを設定します。clear=true は None を選択。confirm=true が必要です",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["itemTag"] = new McpToolParameter { Type = "string", Description = "対象アイテムの tag。clear=true の場合は省略可能", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "None / GameTags.Void を選択するか", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "対象アイテムの変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for storage tile target changes");

                    var tile = FindStorageTile(args);
                    if (tile == null)
                        return CallToolResult.Error("StorageTile target not found");

                    var before = StorageTileInfo(tile, includeOptions: false);
                    var error = ApplyStorageTileSelection(tile, args);
                    if (error != null)
                        return CallToolResult.Error(error);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(tile.gameObject),
                        ["before"] = before,
                        ["storageTile"] = StorageTileInfo(tile, includeOptions: true)
                    });
                }
            };
        }

        public static McpTool BatchSetStorageTileSelections()
        {
            return new McpTool
            {
                Name = "storage_tile_selections_batch_set",
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "single_item_selections_batch_set" },
                Tags = new List<string> { "resources", "storage", "side-screen", "single-item", "batch" },
                Description = "互換用の入口。building_control domain=tile_selection action=batch を優先してください。StorageTile の対象アイテムを一括設定します。items は短縮フィールド i/c/w に対応し、defaults で itemTag/clear/worldId を共有可能。confirm=true が必要です",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "配列。各項目は id または x/y/worldId と itemTag または clear=true を指定。短縮フィールドは i=itemTag、c=clear、w=worldId", Required = true },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "各項目に結合する既定の引数。itemTag/i、clear/c、worldId/w に対応。個別項目の引数を優先", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "対象アイテムの一括変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for storage tile target batch changes");

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

                        var item = MergeStorageTileDefaults(rawItem, defaults);
                        var tile = FindStorageTile(item);
                        if (tile == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "StorageTile target not found", ["input"] = item });
                            continue;
                        }

                        var before = StorageTileInfo(tile, includeOptions: false);
                        var error = ApplyStorageTileSelection(tile, item);
                        results.Add(new Dictionary<string, object>
                        {
                            ["ok"] = error == null,
                            ["error"] = error,
                            ["target"] = TargetInfo(tile.gameObject),
                            ["before"] = before,
                            ["storageTile"] = StorageTileInfo(tile, includeOptions: false)
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

        public static McpTool ControlStorageTileSelection()
        {
            return new McpTool
            {
                Name = "storage_tile_selection_control",
                Hidden = true,
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "single_item_selection_control", "storage_tile_target_item_control" },
                Tags = new List<string> { "resources", "storage", "side-screen", "single-item", "tile", "batch" },
                Description = "StorageTile の対象アイテムの読み取り、個別設定、一括設定を統合。action=list/set/batch。set/batch は confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、set、batch", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、対象アイテム、選択可能なアイテムによる絞り込み", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "action=list で選択可能なアイテムを返すか。既定は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定は 100、最大 500", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=set の対象の KPrefabID InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=list/set の任意の区域または対象の X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=list/set の任意の区域または対象の Y", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "action=list で絞り込む矩形の始点 X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "action=list で絞り込む矩形の始点 Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "action=list で絞り込む矩形の終点 X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "action=list で絞り込む矩形の終点 Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在のワールドまたは対象セルのあるワールド", Required = false },
                    ["itemTag"] = new McpToolParameter { Type = "string", Description = "action=set の対象アイテムの tag。clear=true の場合は省略可能", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "action=set で None / GameTags.Void を選択するか", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Description = "action=batch の配列。各項目は id または x/y/worldId と itemTag または clear=true を指定。短縮フィールドは i/c/w", Required = false },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "action=batch で各項目に結合する既定の引数", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set/batch では true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListStorageTileSelections().Handler(args);
                    if (action == "set")
                        return SetStorageTileSelection().Handler(args);
                    if (action == "batch")
                        return BatchSetStorageTileSelections().Handler(args);
                    return CallToolResult.Error("action must be one of list, set, batch");
                }
            };
        }

    }
}
