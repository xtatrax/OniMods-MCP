using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static bool IsDryRun(JObject args)
        {
            return ToolUtil.GetBool(args, "dryRun", false) || ToolUtil.GetBool(args, "validateOnly", false);
        }

        private static Dictionary<string, McpToolParameter> BuildPlacementParameters(bool includeConfirm, bool includeArea)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["prefabId"] = new McpToolParameter { Type = "string", Description = "設備の prefabId。例: MedicalCot、Tile、Wire。省略して plan/blueprint/sequence から解析することも可能", Required = false },
                ["plan"] = new McpToolParameter { Type = "string", Description = "建築の文字列・短文を prefabId/material/query に解析。例: 粉砂岩砖@氧气、Wire-小型冰箱、用铜矿造手动发电机在电池旁", Required = false },
                ["blueprint"] = new McpToolParameter { Type = "string", Description = "plan の別名", Required = false },
                ["sequence"] = new McpToolParameter { Type = "string", Description = "plan の別名。検索と操作を一括して行う場合に文字列を渡す", Required = false },
                ["text"] = new McpToolParameter { Type = "string", Description = "plan の別名", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "lowerLeftCell anchor X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "lowerLeftCell anchor Y", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "座標省略時はオブジェクト・元素・複製人間の名前から anchor の位置を検索", Required = false },
                ["target"] = new McpToolParameter { Type = "string", Description = "query の別名。対象の名前、prefabId、元素、複製人間を検索", Required = false },
                ["search"] = new McpToolParameter { Type = "string", Description = "query の別名。対象の名前、prefabId、元素、複製人間を検索", Required = false },
                ["nearX"] = new McpToolParameter { Type = "integer", Description = "位置検索時、この X から近い順に並べ替える", Required = false },
                ["nearY"] = new McpToolParameter { Type = "integer", Description = "位置検索時、この Y から近い順に並べ替える", Required = false },
                ["offsetX"] = new McpToolParameter { Type = "integer", Description = "検索・x,y から求めた anchor に加える X オフセット。別名は dx。1回の呼び出しで検索結果からの相対位置に建築予定を配置", Required = false },
                ["offsetY"] = new McpToolParameter { Type = "integer", Description = "検索・x,y から求めた anchor に加える Y オフセット。別名は dy。1回の呼び出しで検索結果からの相対位置に建築予定を配置", Required = false },
                ["dx"] = new McpToolParameter { Type = "integer", Description = "offsetX の短い別名", Required = false },
                ["dy"] = new McpToolParameter { Type = "integer", Description = "offsetY の短い別名", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。既定は現在アクティブなワールド、または areaId に紐付くワールド", Required = false },
                ["material"] = new McpToolParameter { Type = "string", Description = "建築素材の tag。auto/default は自動選択", Required = false },
                ["facade"] = new McpToolParameter { Type = "string", Description = "任意の設備の外観・permit id", Required = false },
                ["orientation"] = new McpToolParameter { Type = "string", Description = "任意の向き。既定は Neutral", Required = false },
                ["priority"] = new McpToolParameter { Type = "integer", Description = "建築の優先度 1..9。既定は 5", Required = false },
                ["allowUnsupported"] = new McpToolParameter { Type = "boolean", Description = "既定は false。OnFloor 設備に支えがない場合は拒否", Required = false },
                ["allowUtilityTerrainDig"] = new McpToolParameter { Type = "boolean", Description = "既定false。自然地形を採掘してutilityを敷く明示許可。自動配線の追加経路にも適用。支持関係は未評価", Required = false },
                ["autoDigObstructions"] = new McpToolParameter { Type = "boolean", Description = "既定は true。設備の占有範囲に採掘可能な自然の固体がある場合、建築実行時に先に採掘を自動で指示し、同じセルへの建築予定の配置を続けて試みる", Required = false },
                ["autoUprootObstructions"] = new McpToolParameter { Type = "boolean", Description = "既定は true。設備の占有範囲に掘り起こせる植物がある場合、自動で掘り起こしを指示し、建築予定の配置を続けて試みる", Required = false },
                ["maxAutoDigCells"] = new McpToolParameter { Type = "integer", Description = "1回のコマンドで採掘・掘り起こしを自動指定するセル数の上限。既定は 100、最大 500", Required = false },
                ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "事前検証のみ。建築予定を作成しない", Required = false }
            };

            if (includeConfirm)
                parameters["confirm"] = new McpToolParameter { Type = "boolean", Description = "変更の実行には true が必須。dryRun=true の場合は省略可能", Required = false };
            if (includeArea)
            {
                parameters["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。relative=true の場合、座標を区域の rx/ry として解釈", Required = false };
                parameters["relative"] = new McpToolParameter { Type = "boolean", Description = "areaId と組み合わせて相対座標を使用", Required = false };
            }

            return parameters;
        }

        private static bool TryResolvePoint(JObject args, out int x, out int y, out string error)
        {
            x = 0;
            y = 0;
            error = null;
            int? requestedX = ToolUtil.GetInt(args, "x");
            int? requestedY = ToolUtil.GetInt(args, "y");
            if (!requestedX.HasValue || !requestedY.HasValue)
            {
                if (!ToolUtil.TryResolveSearchCell(args, out x, out y, out error))
                    return false;
                ApplyAnchorOffset(args, ref x, ref y);
                return true;
            }

            var area = WorldEditor.ResolveRelativeArea(args);
            if (area != null)
            {
                var absolute = WorldEditor.ToAbsoluteCell(requestedX.Value, requestedY.Value, area);
                x = absolute.x;
                y = absolute.y;
            }
            else
            {
                x = requestedX.Value;
                y = requestedY.Value;
            }

            ApplyAnchorOffset(args, ref x, ref y);
            return true;
        }

        private static void ApplyAnchorOffset(JObject args, ref int x, ref int y)
        {
            x += ToolUtil.GetInt(args, "offsetX") ?? ToolUtil.GetInt(args, "dx") ?? 0;
            y += ToolUtil.GetInt(args, "offsetY") ?? ToolUtil.GetInt(args, "dy") ?? 0;
        }

        private static Dictionary<string, object> BuildAnchorResolution(JObject args, List<CellCoord> anchors)
        {
            string query = args["query"]?.ToString();
            if (string.IsNullOrWhiteSpace(query))
                query = args["target"]?.ToString();
            if (string.IsNullOrWhiteSpace(query))
                query = args["search"]?.ToString();
            if (string.IsNullOrWhiteSpace(query))
                query = args["name"]?.ToString();

            int offsetX = ToolUtil.GetInt(args, "offsetX") ?? ToolUtil.GetInt(args, "dx") ?? 0;
            int offsetY = ToolUtil.GetInt(args, "offsetY") ?? ToolUtil.GetInt(args, "dy") ?? 0;
            var first = anchors != null && anchors.Count > 0
                ? new Dictionary<string, object> { ["x"] = anchors[0].x, ["y"] = anchors[0].y }
                : null;

            return new Dictionary<string, object>
            {
                ["mode"] = anchors != null && anchors.Count > 1
                    ? "anchors"
                    : string.IsNullOrWhiteSpace(query) ? "coordinate_or_area" : "search_anchor",
                ["query"] = string.IsNullOrWhiteSpace(query) ? null : query,
                ["offset"] = new Dictionary<string, int> { ["x"] = offsetX, ["y"] = offsetY },
                ["anchorCount"] = anchors?.Count ?? 0,
                ["firstAnchor"] = first,
                ["oneCallSearchAndAction"] = !string.IsNullOrWhiteSpace(query)
            };
        }

        private static List<CellCoord> ResolveAnchors(JObject args, BuildingDef def, out string error)
        {
            error = null;
            var anchors = ParseAnchorArray(args, out error);
            if (error != null)
                return anchors;
            if (anchors.Count > 0)
                return anchors;

            int x;
            int y;
            if (TryResolvePoint(args, out x, out y, out error))
                return new List<CellCoord> { new CellCoord(x, y) };
            string query = args["query"]?.ToString();
            if (string.IsNullOrWhiteSpace(query))
                query = args["target"]?.ToString();
            if (string.IsNullOrWhiteSpace(query))
                query = args["search"]?.ToString();
            if (!string.IsNullOrWhiteSpace(query))
            {
                error = $"query '{query}' did not resolve to a visible anchor; provide anchors, x/y, areaId, or x1/y1/x2/y2";
                return anchors;
            }
            error = null;

            bool hasRect = !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                || ToolUtil.GetInt(args, "x1").HasValue
                || ToolUtil.GetInt(args, "y1").HasValue
                || ToolUtil.GetInt(args, "x2").HasValue
                || ToolUtil.GetInt(args, "y2").HasValue;
            if (!hasRect)
            {
                error = "anchors, x/y, areaId, or x1/y1/x2/y2 are required";
                return anchors;
            }

            var rect = ToolUtil.GetRect(args);
            var shape = NativeBuildingFootprint.Definition(def, ParseOrientation(args["orientation"]?.ToString()));
            if (!shape.Available) { error = "Native footprint unavailable: " + shape.Error; return anchors; }
            int width = shape.Size[0];
            int height = shape.Size[1];
            bool dense = ToolUtil.GetBool(args, "dense", width == 1 && height == 1);
            int stepX = Math.Max(1, ToolUtil.GetInt(args, "stepX") ?? (dense ? 1 : width));
            int stepY = Math.Max(1, ToolUtil.GetInt(args, "stepY") ?? (dense ? 1 : height));

            for (int ay = rect["y1"]; ay <= rect["y2"]; ay += stepY)
                for (int ax = rect["x1"]; ax <= rect["x2"]; ax += stepX)
                    anchors.Add(new CellCoord(ax, ay));

            return anchors;
        }

        private static List<CellCoord> ParseAnchorArray(JObject args, out string error)
        {
            error = null;
            var result = new List<CellCoord>();
            var anchors = args["anchors"] as JArray;
            if (anchors == null)
                return result;

            var area = WorldEditor.ResolveRelativeArea(args);
            foreach (var item in anchors)
            {
                int? x = null;
                int? y = null;
                var obj = item as JObject;
                if (obj != null)
                {
                    x = ParseInt(obj["x"]);
                    y = ParseInt(obj["y"]);
                }
                else
                {
                    var pair = item as JArray;
                    if (pair != null && pair.Count >= 2)
                    {
                        x = ParseInt(pair[0]);
                        y = ParseInt(pair[1]);
                    }
                }

                if (!x.HasValue || !y.HasValue)
                {
                    error = "Each anchors item must be {x,y} or [x,y]";
                    return result;
                }

                if (area != null)
                {
                    var absolute = WorldEditor.ToAbsoluteCell(x.Value, y.Value, area);
                    result.Add(new CellCoord(absolute.x, absolute.y));
                }
                else
                {
                    result.Add(new CellCoord(x.Value, y.Value));
                }
            }

            return result;
        }

        private static int? ParseInt(JToken token)
        {
            int value;
            return token != null && int.TryParse(token.ToString(), out value) ? value : (int?)null;
        }
    }
}
