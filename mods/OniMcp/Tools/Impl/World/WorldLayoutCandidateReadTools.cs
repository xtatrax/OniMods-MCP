using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldAnalysisTools
    {
        public static McpTool GetLayoutCandidates()
        {
            return new McpTool
            {
                Name = "layout_candidates",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "world_layout_candidates", "base_layout_candidates", "room_candidates" },
                Tags = new List<string> { "layout", "planning", "room", "base", "map", "平面", "规划", "房间" },
                Hidden = true,
                Description = "非推奨: 旧ツールは 0.3.0 で削除予定です。次へ切り替えてください: read_control domain=world action=layout_candidates",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。指定すると x1/y1/x2/y2/worldId を省略可能", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の X。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の Y。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の X。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の Y。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["purpose"] = new McpToolParameter { Type = "string", Description = "用途: generic、lab、barracks、bathroom、power、farm。既定は generic", Required = false, EnumValues = new List<string> { "generic", "lab", "barracks", "bathroom", "power", "farm" } },
                    ["width"] = new McpToolParameter { Type = "integer", Description = "対象の部屋の幅。省略時は purpose の既定値を使用", Required = false },
                    ["height"] = new McpToolParameter { Type = "integer", Description = "対象の部屋の高さ。省略時は purpose の既定値を使用", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "発見済みのセルだけを対象にするか。既定は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す候補数の上限。既定は 10、最大 50", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "走査セル数の上限。既定は 2500、厳格な上限は 2500", Required = false },
                    ["detailHazards"] = new McpToolParameter { Type = "boolean", Description = "セルごとの危険の詳細を返すか。既定は false で、座標と元素数だけを返す", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    AreaHandle requestedArea = null;
                    if (!string.IsNullOrWhiteSpace(args["areaId"]?.ToString()))
                    {
                        if (!AreaHandleRegistry.TryGet(args["areaId"].ToString(), out requestedArea))
                            return CallToolResult.Error("Unknown areaId");
                    }

                    int maxCells = Math.Max(1, Math.Min(TryGetInt(args, "maxCells", MaxTextMapCells), MaxTextMapCells));
                    var rect = ResolveTextMapRect(args, maxCells);
                    int worldId = TryGetInt(args, "worldId", requestedArea?.WorldId ?? ClusterManager.Instance?.activeWorldId ?? 0);
                    int width = rect["x2"] - rect["x1"] + 1;
                    int height = rect["y2"] - rect["y1"] + 1;
                    int cells = width * height;
                    if (cells > maxCells)
                        return CallToolResult.Error($"Area too large: {width}x{height}={cells} cells, maxCells={maxCells}");

                    string purpose = NormalizeLayoutPurpose(args["purpose"]?.ToString());
                    bool visibleOnly = TryGetBool(args, "visibleOnly", true);
                    int limit = ClampInt(args, "limit", 10, 1, 50);
                    var defaults = LayoutDefaults(purpose);
                    int candidateWidth = ClampInt(args, "width", defaults.Width, 4, Math.Min(64, width));
                    int candidateHeight = ClampInt(args, "height", defaults.Height, 3, Math.Min(16, height));
                    var area = AreaHandleRegistry.Define(rect, worldId, args["label"]?.ToString());
                    bool detailHazards = TryGetBool(args, "detailHazards", false);
                    var summary = BuildPlanningSummary(rect, worldId, visibleOnly, purpose, limit, candidateWidth, candidateHeight, detailHazards);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["v"] = 1,
                        ["areaId"] = area.Id,
                        ["worldId"] = worldId,
                        ["rect"] = new[] { rect["x1"], rect["y1"], rect["x2"], rect["y2"] },
                        ["purpose"] = purpose,
                        ["candidateSize"] = new[] { candidateWidth, candidateHeight },
                        ["planning"] = summary
                    }, McpJsonUtil.Settings));
                }
            };
        }

    }
}
