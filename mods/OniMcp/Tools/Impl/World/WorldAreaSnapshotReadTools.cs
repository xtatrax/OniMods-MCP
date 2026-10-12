using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldAnalysisTools
    {
        public static McpTool GetWorldAreaSnapshot()
        {
            return new McpTool
            {
                Name = "world_area_snapshot",
                Group = "world",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "area_snapshot", "world_snapshot_area" },
                Tags = new List<string> { "map", "snapshot", "text", "grid", "screenshot", "overlay", "地图", "截图", "快照" },
                Hidden = true,
                Description = "非推奨: 旧ツールは 0.3.0 で削除予定です。次へ切り替えてください: read_control domain=world action=area_snapshot",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。結果に origin/relativeRect とワールドの絶対座標範囲を含める", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の X。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の Y。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の X。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の Y。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "発見済みのセルだけを出力するか。既定は true", Required = false },
                    ["preset"] = new McpToolParameter { Type = "string", Description = "スナップショットのプリセット: terrain=地形のみ、construction=地形+電力、utilities=地形+全インフラレイヤー、planning=utilities+配置計画の要約、all=utilities+スクリーンショット。既定は construction", Required = false, EnumValues = new List<string> { "terrain", "construction", "utilities", "planning", "all" } },
                    ["overlays"] = new McpToolParameter { Type = "array", Description = "任意の overlay/analysis 一覧またはカンマ区切り文字列: power、gas_conduits、liquid_conduits、solid_conveyor、logic、temperature。preset の既定値を上書き", Required = false },
                    ["includeBase"] = new McpToolParameter { Type = "boolean", Description = "基本地形のテキストマップを含めるか。既定は true", Required = false },
                    ["includeBuildings"] = new McpToolParameter { Type = "boolean", Description = "基本マップに設備オブジェクトを含めるか。既定は true", Required = false },
                    ["includeItems"] = new McpToolParameter { Type = "boolean", Description = "基本マップに落ちているアイテムを含めるか。既定は false", Required = false },
                    ["includeDupes"] = new McpToolParameter { Type = "boolean", Description = "基本マップに複製人間を含めるか。既定は true", Required = false },
                    ["includeElements"] = new McpToolParameter { Type = "boolean", Description = "基本マップに元素の統計を含めるか。既定は false", Required = false },
                    ["includeScreenshot"] = new McpToolParameter { Type = "boolean", Description = "現在のスクリーンショットを保存し、そのパスを含めるか。既定は false。呼び出し前にカメラを移動していない場合、指定区域全体が映るとは限らない", Required = false },
                    ["encoding"] = new McpToolParameter { Type = "string", Description = "マップ行のエンコード: plain/rle/both。既定は plain", Required = false, EnumValues = new List<string> { "plain", "rle", "both" } },
                    ["profile"] = new McpToolParameter { Type = "string", Description = "マップ出力の詳細度: standard/minimal/scan。既定は standard", Required = false, EnumValues = new List<string> { "standard", "minimal", "scan" } },
                    ["objectLimit"] = new McpToolParameter { Type = "integer", Description = "返すオブジェクト・疎形式セルの件数上限。既定は 120、最大 500", Required = false },
                    ["label"] = new McpToolParameter { Type = "string", Description = "任意の区域の短いラベル。返される areaId にこのラベルを保存する", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "出力セル数の上限。既定は 1600、厳格な上限は 2500", Required = false },
                    ["chunksOnly"] = new McpToolParameter { Type = "boolean", Description = "分割計画だけを返し、マップは展開しない。広範囲の走査向け", Required = false },
                    ["includeChunks"] = new McpToolParameter { Type = "boolean", Description = "広い区域を分割する際、areaId 一覧だけでなく各ブロックの base 内容の短いプレビューも埋め込む。既定は false", Required = false },
                    ["chunkMaxCells"] = new McpToolParameter { Type = "integer", Description = "ブロックごとの目標最大セル数。既定では maxCells を引き継ぎ、厳格な上限は 2500", Required = false },
                    ["chunkLimit"] = new McpToolParameter { Type = "integer", Description = "詳細を返すブロック数の上限。既定は 200、最大 1000", Required = false },
                    ["compact"] = new McpToolParameter { Type = "boolean", Description = "コンパクトに出力するか。既定は false。有効時はオブジェクトの null・空配列フィールドと、空の凡例を省略", Required = false },
                    ["includeRows"] = new McpToolParameter { Type = "boolean", Description = "マップの行データを含めるか。terrain/construction の既定は true、utilities/planning/all の既定は false", Required = false },
                    ["includeObjects"] = new McpToolParameter { Type = "boolean", Description = "オブジェクト一覧を含めるか。既定は true", Required = false },
                    ["includeAreaDescription"] = new McpToolParameter { Type = "boolean", Description = "自然言語による区域の説明と、主要な地形・液体の区間を含めるか。既定は true", Required = false }
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

                    int maxCells = Math.Max(1, Math.Min(TryGetInt(args, "maxCells", 1600), MaxTextMapCells));
                    bool chunksOnly = TryGetBool(args, "chunksOnly", false);
                    bool includeChunks = TryGetBool(args, "includeChunks", false);
                    int chunkMaxCells = Math.Max(64, Math.Min(TryGetInt(args, "chunkMaxCells", maxCells), MaxTextMapCells));
                    int chunkLimit = Math.Max(1, Math.Min(TryGetInt(args, "chunkLimit", 200), 1000));
                    var rect = ResolveTextMapRect(args, maxCells);
                    int worldId = TryGetInt(args, "worldId", requestedArea?.WorldId ?? ClusterManager.Instance?.activeWorldId ?? 0);
                    int width = rect["x2"] - rect["x1"] + 1;
                    int height = rect["y2"] - rect["y1"] + 1;
                    int cells = width * height;
                    var area = AreaHandleRegistry.Define(rect, worldId, args["label"]?.ToString());
                    if (chunksOnly || cells > maxCells)
                    {
                        var chunkPlan = BuildChunkPlan(area, rect, worldId, width, height, cells, maxCells, chunkMaxCells, chunkLimit);
                        if (includeChunks)
                            AddChunkPreviews(chunkPlan, worldId, "base", TryGetBool(args, "visibleOnly", true), TryGetBool(args, "includeBuildings", true), TryGetBool(args, "includeItems", false), TryGetBool(args, "includeDupes", true));
                        return CallToolResult.Text(JsonConvert.SerializeObject(chunkPlan, McpJsonUtil.Settings));
                    }

                    string preset = NormalizeSnapshotPreset(args["preset"]?.ToString());
                    bool includeBase = TryGetBool(args, "includeBase", true);
                    bool includeScreenshot = TryGetBool(args, "includeScreenshot", preset == "all");
                    bool visibleOnly = TryGetBool(args, "visibleOnly", true);
                    bool includeBuildings = TryGetBool(args, "includeBuildings", true);
                    bool includeItems = TryGetBool(args, "includeItems", false);
                    bool includeDupes = TryGetBool(args, "includeDupes", true);
                    bool includeElements = TryGetBool(args, "includeElements", false);
                    string encoding = NormalizeEncoding(args["encoding"]?.ToString(), "plain");
                    string profile = NormalizeProfile(args["profile"]?.ToString());
                    if (profile == "scan" && args["encoding"] == null)
                        encoding = "rle";
                    int objectLimit = ClampInt(args, "objectLimit", 120, 0, 500);
                    bool compact = TryGetBool(args, "compact", false);
                    bool includeRows = TryGetBool(args, "includeRows", preset == "terrain" || preset == "construction");
                    bool includeObjects = TryGetBool(args, "includeObjects", true);
                    bool includeAreaDescription = TryGetBool(args, "includeAreaDescription", true);

                    var overlayViews = ResolveSnapshotOverlays(args["overlays"], preset);
                    bool sparseOverlays = profile == "scan";
                    var maps = BuildSnapshotMapsSinglePass(area, rect, worldId, width, height, includeBase, overlayViews, sparseOverlays, visibleOnly, includeBuildings, includeItems, includeDupes, includeElements, encoding, objectLimit, compact, includeRows, includeObjects);
                    var snapshotSummary = BuildAreaSnapshotSummary(maps);

                    var result = new Dictionary<string, object>
                    {
                        ["v"] = 1,
                        ["areaId"] = area.Id,
                        ["worldId"] = worldId,
                        ["rect"] = new[] { rect["x1"], rect["y1"], rect["x2"], rect["y2"] },
                        ["size"] = new[] { width, height },
                        ["cells"] = cells,
                        ["visibleOnly"] = visibleOnly,
                        ["preset"] = preset,
                        ["summary"] = snapshotSummary,
                        ["maps"] = maps
                    };

                    if (includeAreaDescription)
                        result["areaDescription"] = BuildAreaDescription(rect, worldId, visibleOnly);

                    if (!compact)
                    {
                        result["comparison"] = new Dictionary<string, object>
                        {
                            ["textMapStrength"] = "coordinate-accurate terrain, elements, buildings, dupes, wires and conduits; use for planning and validation",
                            ["screenshotStrength"] = "human visual confirmation of current camera view, UI overlays, decor/room/crop visuals; not reliable for exact coordinates by itself",
                            ["defaultRule"] = "plan from maps; use screenshot only as supplementary visual evidence"
                        };
                    }

                    if (preset == "planning")
                    {
                        result["planning"] = BuildPlanningSummary(rect, worldId, visibleOnly, purpose: "generic", limit: 12);
                    }

                    if (includeScreenshot)
                    {
                        var screenshot = CameraTools.TakeScreenshot().Handler(new JObject());
                        result["screenshot"] = ToolResultToToken(screenshot);
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

    }
}
