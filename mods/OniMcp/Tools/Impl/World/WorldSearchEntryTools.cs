using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldSearchTools
    {
        private const int MaxSearchCells = 20000;

        public static McpTool SearchWorld()
        {
            return new McpTool
            {
                Name = "world_search",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "map_search", "world_find", "find_on_map" },
                Tags = new List<string> { "world", "map", "search", "find", "elements", "buildings", "items", "dupes", "地图", "搜索", "查找" },
                Description = "非推奨: 旧ツールは 0.3.0 で削除予定です。read_control domain=world action=search に切り替えてください。自然言語・query で同じ区域内のセルの元素、設備、アイテム、複製人間を検索します。nearX/nearY による近い順の並べ替えに対応。",
                Parameters = CommonSearchParameters(includeKinds: true),
                Handler = args =>
                {
                    if (Game.Instance == null)
                    return CallToolResult.Error("Game not initialized");

                var request = SearchRequest.From(args, "clusters");
                if (request.HasPattern)
                    return CallToolResult.Text(JsonConvert.SerializeObject(BuildPatternResult(request), McpJsonUtil.Settings));

                var results = new List<SearchHit>();
                    if (request.IncludeKind("cells") || request.IncludeKind("elements"))
                        results.AddRange(SearchCells(request));
                    if (request.IncludeKind("buildings"))
                        results.AddRange(SearchBuildings(request));
                    if (request.IncludeKind("items") || request.IncludeKind("resources"))
                        results.AddRange(SearchItems(request));
                    if (request.IncludeKind("dupes") || request.IncludeKind("duplicants"))
                        results.AddRange(SearchDupes(request));

                    return CallToolResult.Text(JsonConvert.SerializeObject(BuildResult("world_search", request, results), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SearchCells()
        {
            return new McpTool
            {
                Name = "world_search_cells",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "map_search_cells", "world_find_cells", "element_cells_search" },
                Tags = new List<string> { "world", "map", "search", "cells", "elements", "temperature", "mass", "地图", "格子", "元素" },
                Description = "互換用の入口。world_search kinds=cells/elements を優先してください。",
                Parameters = CellSearchParameters(),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    var request = SearchRequest.From(args, "hits");
                    return CallToolResult.Text(JsonConvert.SerializeObject(BuildResult("world_search_cells", request, SearchCells(request)), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SearchObjects()
        {
            return new McpTool
            {
                Name = "world_search_objects",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "map_search_objects", "world_find_objects", "object_search" },
                Tags = new List<string> { "world", "map", "search", "buildings", "items", "dupes", "objects", "地图", "对象", "建筑", "物品" },
                Description = "互換用の入口。world_search kinds=buildings/items/dupes を優先してください。",
                Parameters = ObjectSearchParameters(),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    var request = SearchRequest.From(args, "hits");
                    var results = new List<SearchHit>();
                    if (request.IncludeKind("buildings"))
                        results.AddRange(SearchBuildings(request));
                    if (request.IncludeKind("items") || request.IncludeKind("resources"))
                        results.AddRange(SearchItems(request));
                    if (request.IncludeKind("dupes") || request.IncludeKind("duplicants"))
                        results.AddRange(SearchDupes(request));

                    return CallToolResult.Text(JsonConvert.SerializeObject(BuildResult("world_search_objects", request, results), McpJsonUtil.Settings));
                }
            };
        }

        private static Dictionary<string, McpToolParameter> CommonSearchParameters(bool includeKinds)
        {
            var parameters = BaseAreaParameters();
            parameters["query"] = new McpToolParameter { Type = "string", Description = "検索語。元素 ID・名前、設備名・prefabId、アイテム名・prefabId、複製人間名に一致。省略時は条件に応じて返す", Required = false };
            parameters["pattern"] = new McpToolParameter { Type = "string", Description = "連続するセルの並びを検索。例: 粉砂岩-泥土-氧气、SiltStone>Dirt>Oxygen、砂岩|粉砂岩-* -氧气、Dirt{3}。通常の query より優先", Required = false };
            parameters["sequence"] = new McpToolParameter { Type = "string", Description = "pattern の別名。連続する元素・状態の並び。* は任意のセル、A|B は選択肢、term{N} は繰り返し", Required = false };
            parameters["direction"] = new McpToolParameter { Type = "string", Description = "pattern の検索方向: horizontal、vertical または both。既定は both。順方向・逆方向の両方に一致", Required = false, EnumValues = new List<string> { "horizontal", "vertical", "both" } };
            parameters["matchMode"] = new McpToolParameter { Type = "string", Description = "pattern/query の一致モード: exact は完全一致、smart は正規化・部分一致、fuzzy は少量の綴りの違いを許容。既定は smart", Required = false, EnumValues = new List<string> { "exact", "smart", "fuzzy" } };
            if (includeKinds)
                parameters["kinds"] = new McpToolParameter { Type = "array", Description = "検索種別の配列またはカンマ区切り文字列: cells,elements,buildings,items,resources,dupes。既定は all", Required = false };
            parameters["returnMode"] = new McpToolParameter { Type = "string", Description = "戻り値の形式: clusters は連結区域・オブジェクト群、summary は統計のみ、hits は個別の一致結果。既定は clusters", Required = false, EnumValues = new List<string> { "clusters", "summary", "hits" } };
            AddCellFilters(parameters);
            AddSortFilters(parameters);
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> CellSearchParameters()
        {
            var parameters = BaseAreaParameters();
            parameters["query"] = new McpToolParameter { Type = "string", Description = "元素 ID・名前の検索語。例: Water/Oxygen/Cuprite。省略時は条件に応じて返す", Required = false };
            parameters["returnMode"] = new McpToolParameter { Type = "string", Description = "互換用の入口の既定は hits。clusters または summary で圧縮した結果を取得可能", Required = false, EnumValues = new List<string> { "hits", "clusters", "summary" } };
            AddCellFilters(parameters);
            AddSortFilters(parameters);
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> ObjectSearchParameters()
        {
            var parameters = BaseAreaParameters();
            parameters["query"] = new McpToolParameter { Type = "string", Description = "オブジェクトの検索語。設備名・prefabId、アイテム名・prefabId・元素、複製人間名に一致", Required = false };
            parameters["kinds"] = new McpToolParameter { Type = "array", Description = "オブジェクト種別の配列またはカンマ区切り文字列: buildings,items,resources,dupes。既定は buildings,items,dupes", Required = false };
            parameters["returnMode"] = new McpToolParameter { Type = "string", Description = "互換用の入口の既定は hits。clusters または summary で圧縮した結果を取得可能", Required = false, EnumValues = new List<string> { "hits", "clusters", "summary" } };
            AddSortFilters(parameters);
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> BaseAreaParameters()
        {
            return new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。指定すると x1/y1/x2/y2 を省略可能", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の X。省略時は現在のカメラ付近", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の Y。省略時は現在のカメラ付近", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の X。省略時は現在のカメラ付近", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の Y。省略時は現在のカメラ付近", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド。-1 で全ワールドを検索（オブジェクト検索で使用可能）", Required = false },
                ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "発見済みのセルだけを検索するか。既定は true", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "返す結果数の上限。既定は 50、最大 300", Required = false },
                ["maxCells"] = new McpToolParameter { Type = "integer", Description = "走査セル数の上限。既定は 2500、最大 20000", Required = false }
            };
        }

        private static void AddCellFilters(Dictionary<string, McpToolParameter> parameters)
        {
            parameters["state"] = new McpToolParameter { Type = "string", Description = "元素の状態フィルター: any/gas/liquid/solid/vacuum", Required = false, EnumValues = new List<string> { "any", "gas", "liquid", "solid", "vacuum" } };
            parameters["solid"] = new McpToolParameter { Type = "boolean", Description = "固体・非固体のどちらのセルだけを返すか。省略時は絞り込まない", Required = false };
            parameters["minMassKg"] = new McpToolParameter { Type = "number", Description = "最低質量 kg", Required = false };
            parameters["maxMassKg"] = new McpToolParameter { Type = "number", Description = "最高質量 kg", Required = false };
            parameters["minTempC"] = new McpToolParameter { Type = "number", Description = "最低温度 C", Required = false };
            parameters["maxTempC"] = new McpToolParameter { Type = "number", Description = "最高温度 C", Required = false };
        }

        private static void AddSortFilters(Dictionary<string, McpToolParameter> parameters)
        {
            parameters["nearX"] = new McpToolParameter { Type = "integer", Description = "この X から近い順に並べ替える。nearY も必要", Required = false };
            parameters["nearY"] = new McpToolParameter { Type = "integer", Description = "この Y から近い順に並べ替える。nearX も必要", Required = false };
            parameters["sort"] = new McpToolParameter { Type = "string", Description = "並べ替え: nearest、mass、temperature、kind。既定は nearX/nearY がある場合 nearest、それ以外は kind", Required = false, EnumValues = new List<string> { "nearest", "mass", "temperature", "kind" } };
        }
    }
}
