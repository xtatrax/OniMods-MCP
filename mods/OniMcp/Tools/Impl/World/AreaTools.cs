using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static class AreaTools
    {
        public static McpTool DefineArea()
        {
            return new McpTool
            {
                Name = "area_define",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "area_save", "rect_save" },
                Description = "互換用の入口。read_control domain=area action=define を優先してください。矩形区域を短い areaId として保存し、左下を origin とします。以後、areaId 対応ツールでは絶対座標の矩形の代わりに使用できます",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["label"] = new McpToolParameter { Type = "string", Description = "任意の短いラベル。例: oxygen_room_candidate", Required = false }
                }),
                Handler = args =>
                {
                    int worldId = ToolUtil.GetInt(args, "worldId") ?? ClusterManager.Instance?.activeWorldId ?? 0;
                    var handle = AreaHandleRegistry.Define(ToolUtil.GetRect(args), worldId, args["label"]?.ToString());
                    return CallToolResult.Text(JsonConvert.SerializeObject(handle.ToDictionary(), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetArea()
        {
            return new McpTool
            {
                Name = "area_get",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "area_rect" },
                Description = "互換用の入口。read_control domain=area action=get を優先してください。areaId に対応する区域の座標、origin、relativeRect、ワールド ID を取得します",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "区域ハンドル。例: a1", Required = true }
                },
                Handler = args =>
                {
                    AreaHandle handle;
                    if (!AreaHandleRegistry.TryGet(args["areaId"]?.ToString(), out handle))
                        return CallToolResult.Error("Unknown areaId");

                    return CallToolResult.Text(JsonConvert.SerializeObject(handle.ToDictionary(), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListAreas()
        {
            return new McpTool
            {
                Name = "area_list",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "areas" },
                Description = "互換用の入口。read_control domain=area action=list を優先してください。現在のセッションに保存された areaId を、最近使用したものから順に一覧表示します",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数。既定は 20、最大 100", Required = false }
                },
                Handler = args =>
                {
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 20, 100));
                    var areas = AreaHandleRegistry.List()
                        .Take(limit)
                        .Select(area => area.ToDictionary())
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = areas.Count,
                        ["areas"] = areas
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GenerateAreaBlocks()
        {
            return new McpTool
            {
                Name = "area_blocks",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "world_blocks", "area_grid", "world_area_blocks" },
                Description = "互換用の入口。read_control domain=area action=blocks を優先してください。ワールドを自動分割し、blk で始まるマップブロックのハンドル（blk1、blk2...）を作成します。各ブロックは既定で約 40x40。blockWidth/blockHeight/maxCells で調整できます。返された blk* は通常の areaId と同様に world_text_map、world_area_snapshot、areaId 対応のブロック単位ツールで使用できます。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["blockWidth"] = new McpToolParameter { Type = "integer", Description = "ブロックの幅。既定では maxCells に応じて約 40 に自動設定。推奨は 20..50", Required = false },
                    ["blockHeight"] = new McpToolParameter { Type = "integer", Description = "ブロックの高さ。既定では maxCells に応じて約 40 に自動設定。推奨は 20..50", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "ブロックごとの目標最大セル数。既定は 1600、最大 2500。幅・高さの指定がなければ、この値からブロックの大きさを求める", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "詳細を返すブロック数。既定は 200、最大 1000。全ブロックを生成するが、返す結果は省略される場合がある", Required = false },
                    ["label"] = new McpToolParameter { Type = "string", Description = "任意のラベル接頭辞。既定は world_block", Required = false }
                },
                Handler = args =>
                {
                    int worldId = ToolUtil.GetInt(args, "worldId") ?? ClusterManager.Instance?.activeWorldId ?? 0;
                    int maxCells = Math.Max(64, Math.Min(ToolUtil.GetInt(args, "maxCells") ?? 1600, 2500));
                    int defaultSide = Math.Max(8, (int)Math.Floor(Math.Sqrt(maxCells)));
                    int blockWidth = Math.Max(8, Math.Min(ToolUtil.GetInt(args, "blockWidth") ?? defaultSide, 50));
                    int blockHeight = Math.Max(8, Math.Min(ToolUtil.GetInt(args, "blockHeight") ?? Math.Max(8, maxCells / blockWidth), 50));
                    if (blockWidth * blockHeight > maxCells)
                        blockHeight = Math.Max(8, maxCells / blockWidth);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 200, 1000));
                    string label = string.IsNullOrWhiteSpace(args["label"]?.ToString()) ? "world_block" : args["label"].ToString().Trim();

                    Dictionary<string, int> bounds;
                    int validCells;
                    if (!TryGetWorldBounds(worldId, out bounds, out validCells))
                        return CallToolResult.Error("No valid cells found for worldId=" + worldId);

                    var blocks = new List<AreaHandle>();
                    int rows = 0;
                    int cols = 0;
                    for (int y = bounds["y1"]; y <= bounds["y2"]; y += blockHeight)
                    {
                        int row = rows++;
                        cols = 0;
                        for (int x = bounds["x1"]; x <= bounds["x2"]; x += blockWidth)
                        {
                            int col = cols++;
                            var rect = new Dictionary<string, int>
                            {
                                ["x1"] = x,
                                ["y1"] = y,
                                ["x2"] = Math.Min(x + blockWidth - 1, bounds["x2"]),
                                ["y2"] = Math.Min(y + blockHeight - 1, bounds["y2"])
                            };
                            blocks.Add(AreaHandleRegistry.DefineBlock(rect, worldId, col, row, blockWidth, blockHeight, label + "_" + col + "_" + row, "blk"));
                        }
                    }

                    var returned = blocks
                        .OrderBy(block => block.BlockRow ?? 0)
                        .ThenBy(block => block.BlockColumn ?? 0)
                        .Take(limit)
                        .Select(block => block.ToDictionary())
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["worldId"] = worldId,
                        ["bounds"] = bounds,
                        ["validCells"] = validCells,
                        ["blockWidth"] = blockWidth,
                        ["blockHeight"] = blockHeight,
                        ["cols"] = cols,
                        ["rows"] = rows,
                        ["generated"] = blocks.Count,
                        ["returned"] = returned.Count,
                        ["truncated"] = Math.Max(0, blocks.Count - returned.Count),
                        ["idPrefix"] = "blk",
                        ["coordRule"] = "Use any blk* as areaId for area-aware reads or whole-area operations; use world absolute x/y for build/order coordinates.",
                        ["blocks"] = returned
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool MergeAreas()
        {
            return new McpTool
            {
                Name = "area_merge",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "area_union", "area_compose", "merge_areas" },
                Description = "互換用の入口。read_control domain=area action=merge を優先してください。複数の areaId（例: blk1+blk2+blk3 または [\"blk1\",\"blk2\"]）を新しい a* 区域ハンドルに結合します。同じワールド内の外接矩形を使用します。隣接しない区域では間の空間も含み、continuity/gapCellsPercent/warning を返します。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["areaIds"] = new McpToolParameter { Type = "array", Description = "結合する区域ハンドルの配列。文字列 blk1+blk2,blk3 も使用可能", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "結合する区域ハンドルの文字列。blk1+blk2、blk1,blk2、blk1 blk2 に対応", Required = false },
                    ["label"] = new McpToolParameter { Type = "string", Description = "新しい区域のラベル。既定は merged_area", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "結合のプレビューだけを返し、新しい a* を作成しない。既定は false", Required = false }
                },
                Handler = args =>
                {
                    var ids = ReadAreaIds(args);
                    if (ids.Count == 0)
                        return CallToolResult.Error("areaIds or areaId is required");

                    List<AreaHandle> handles;
                    try
                    {
                        handles = AreaHandleRegistry.ResolveMany(string.Join("+", ids.ToArray()));
                    }
                    catch (Exception ex)
                    {
                        return CallToolResult.Error(ex.Message);
                    }

                    AreaHandle composed;
                    try
                    {
                        composed = AreaHandleRegistry.Compose(handles, args["label"]?.ToString() ?? "merged_area");
                    }
                    catch (Exception ex)
                    {
                        return CallToolResult.Error(ex.Message);
                    }

                    int sourceCells = handles.Sum(handle => (handle.X2 - handle.X1 + 1) * (handle.Y2 - handle.Y1 + 1));
                    int mergedCells = (composed.X2 - composed.X1 + 1) * (composed.Y2 - composed.Y1 + 1);
                    int gapCells = Math.Max(0, mergedCells - sourceCells);
                    double gapCellsPercent = mergedCells > 0 ? Math.Round(gapCells * 100.0 / mergedCells, 2) : 0;
                    bool dryRun = ToolUtil.GetBool(args, "dryRun", false);
                    AreaHandle result = dryRun
                        ? composed
                        : AreaHandleRegistry.Define(composed.Rect(), composed.WorldId, composed.Label);

                    var payload = new Dictionary<string, object>
                    {
                        ["dryRun"] = dryRun,
                        ["sourceAreaIds"] = ids,
                        ["sourceCount"] = handles.Count,
                        ["sourceCells"] = sourceCells,
                        ["mergedCells"] = mergedCells,
                        ["gapCells"] = gapCells,
                        ["gapCellsPercent"] = gapCellsPercent,
                        ["continuity"] = gapCells == 0,
                        ["warning"] = gapCells > 0 ? "merge uses bounding rectangle; non-adjacent or uneven regions include cells between source areas" : null,
                        ["coordRule"] = "Use the returned areaId for area-aware reads or whole-area operations; use world absolute x/y for build/order coordinates.",
                        ["area"] = result.ToDictionary()
                    };

                    if (!dryRun)
                        payload["areaId"] = result.Id;
                    return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ForgetArea()
        {
            return new McpTool
            {
                Name = "area_forget",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "互換用の入口。read_control domain=area action=forget を優先してください。不要になった areaId を削除します",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "区域ハンドル。例: a1", Required = true }
                },
                Handler = args =>
                {
                    bool removed = AreaHandleRegistry.Remove(args["areaId"]?.ToString());
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["removed"] = removed,
                        ["areaId"] = args["areaId"]?.ToString()
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlArea()
        {
            return new McpTool
            {
                Name = "area_control",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "areas_control", "area_handle_control", "world_area_control" },
                Tags = new List<string> { "area", "region", "handle", "blocks", "merge", "world" },
                Description = "区域ハンドルを一元管理します。action=define/get/list/blocks/merge/forget。座標の繰り返し指定を areaId で置き換え、blocks で blk* マップブロック、merge で再利用する区域を作成します。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: define、get、list、blocks、merge、forget", Required = true },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "action=define の区域の左下・始点 X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "action=define の区域の左下・始点 Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "action=define の区域の右上・終点 X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "action=define の区域の右上・終点 Y", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "action=get/forget/merge の区域ハンドル。merge は blk1+blk2 またはカンマ・空白区切りにも対応", Required = false },
                    ["areaIds"] = new McpToolParameter { Type = "array", Description = "action=merge で結合する区域ハンドルの配列", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "action=define/blocks のワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["label"] = new McpToolParameter { Type = "string", Description = "action=define/blocks/merge の任意のラベル", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list/blocks で返す件数の上限", Required = false },
                    ["blockWidth"] = new McpToolParameter { Type = "integer", Description = "action=blocks のブロックの幅。推奨は 20..50", Required = false },
                    ["blockHeight"] = new McpToolParameter { Type = "integer", Description = "action=blocks のブロックの高さ。推奨は 20..50", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "action=blocks のブロックごとの目標最大セル数。既定は 1600", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "action=merge では結合プレビューだけを返し、新しい areaId を作成しない", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "define")
                        return DefineArea().Handler(args);
                    if (action == "get")
                        return GetArea().Handler(args);
                    if (action == "list")
                        return ListAreas().Handler(args);
                    if (action == "blocks")
                        return GenerateAreaBlocks().Handler(args);
                    if (action == "merge")
                        return MergeAreas().Handler(args);
                    if (action == "forget")
                        return ForgetArea().Handler(args);
                    return CallToolResult.Error("action must be one of define, get, list, blocks, merge, forget");
                }
            };
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の左下・始点 X", Required = true },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の左下・始点 Y", Required = true },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の右上・終点 X", Required = true },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の右上・終点 Y", Required = true }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static List<string> ReadAreaIds(JObject args)
        {
            var result = new List<string>();
            var array = args["areaIds"] as JArray;
            if (array != null)
            {
                foreach (var item in array)
                    AddAreaIds(result, item?.ToString());
            }
            else
            {
                AddAreaIds(result, args["areaIds"]?.ToString());
            }
            AddAreaIds(result, args["areaId"]?.ToString());
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddAreaIds(List<string> ids, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            foreach (string item in value.Split(new[] { '+', ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string id = item.Trim();
                if (id.Length > 0)
                    ids.Add(id);
            }
        }

        private static bool TryGetWorldBounds(int worldId, out Dictionary<string, int> bounds, out int validCells)
        {
            bounds = null;
            validCells = 0;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int cell = 0; cell < Grid.CellCount; cell++)
            {
                if (!Grid.IsValidCell(cell) || !Grid.IsWorldValidCell(cell) || Grid.WorldIdx[cell] != worldId)
                    continue;

                int x = Grid.CellColumn(cell);
                int y = Grid.CellRow(cell);
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                validCells++;
            }

            if (validCells == 0)
                return false;

            bounds = new Dictionary<string, int>
            {
                ["x1"] = minX,
                ["y1"] = minY,
                ["x2"] = maxX,
                ["y2"] = maxY
            };
            return true;
        }
    }
}
