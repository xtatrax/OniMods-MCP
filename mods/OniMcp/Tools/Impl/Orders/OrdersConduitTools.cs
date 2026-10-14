using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class OrdersTools
{
        public static McpTool EmptyConduits()
        {
            return new McpTool
            {
                Name = "conduits_empty_area",
                Hidden = true,
                Group = "orders",
                Mode = "execute",
                Risk = "medium",
                Aliases = new List<string> { "empty_pipe_area", "orders_empty_pipe" },
                Description = "互換用の入口。orders_control domain=designation action=empty_conduits を優先して使う。領域内の気体パイプ、液体パイプ、コンベアレールの内容物を取り出すよう指定する。ゲームの Empty Pipe ツールに相当する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["type"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "空にする対象: all、gas、liquid、solid。既定値は all",
                        Required = false,
                        EnumValues = new List<string> { "all", "gas", "liquid", "solid" }
                    },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "内容物を取り出す作業の優先度 1-9。既定値は 5", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "赤色の最優先にするか。既定値は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "領域が 100 セルを超える場合は true が必須", Required = false }
                }),
                Handler = args =>
                {
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (cells > 100 && !ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required when emptying conduits in more than 100 cells");

                    var layers = GetEmptyLayers(args["type"]?.ToString());
                    if (layers.Count == 0)
                        return CallToolResult.Error("type must be all, gas, liquid or solid");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    var seen = new HashSet<GameObject>();
                    var results = new List<Dictionary<string, object>>();
                    int marked = 0;
                    for (int y = rect["y1"]; y <= rect["y2"]; y++)
                    {
                        for (int x = rect["x1"]; x <= rect["x2"]; x++)
                        {
                            int cell = Grid.XYToCell(x, y);
                            if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                                continue;

                            foreach (var layer in layers)
                            {
                                var go = Grid.Objects[cell, (int)layer];
                                if (go == null || seen.Contains(go))
                                    continue;
                                seen.Add(go);

                                var workable = go.GetComponent<IEmptyConduitWorkable>();
                                if (workable == null)
                                    continue;

                                if (DebugHandler.InstantBuildMode)
                                    workable.EmptyContents();
                                else
                                    workable.MarkForEmptying();
                                ApplyPriority(go, args);
                                marked++;
                                results.Add(ObjectResult(go, DebugHandler.InstantBuildMode ? "instant_emptied" : "marked"));
                            }
                        }
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["marked"] = marked,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["type"] = string.IsNullOrWhiteSpace(args["type"]?.ToString()) ? "all" : args["type"].ToString(),
                        ["targets"] = results.Take(200).ToList(),
                        ["truncatedTargets"] = Math.Max(0, results.Count - 200)
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool CutConduits()
        {
            return new McpTool
            {
                Name = "conduits_cut",
                Hidden = true,
                Group = "orders",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "orders_cut_conduits", "cut_conduits" },
                Description = "互換用の入口。orders_control domain=designation action=cut_conduits を優先して使う。設備を残して接続だけを切断する。矩形内の隣接辺、単一セルはdirection指定。旧解体はmode=deconstruct。dryRun対応、実行はconfirm=true",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["mode"] = new McpToolParameter { Type = "string", Description = "disconnect (default), or explicit legacy deconstruct", Required = false },
                    ["direction"] = new McpToolParameter { Type = "string", Description = "Single cell edge: up/down/left/right. Rectangle cuts internal edges only.", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "Preview without mutation", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X。矩形が未指定の場合は単一セルを処理する", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y。矩形が未指定の場合は単一セルを処理する", Required = false },
                    ["type"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "切断する種類。auto=気体・液体・固体の輸送経路、all=輸送経路+電線+自動化ワイヤー+移動チューブ。または gas/liquid/solid/wire/logic/travel_tube を指定する。既定値は auto",
                        Required = false,
                        EnumValues = new List<string> { "auto", "all", "gas", "liquid", "solid", "wire", "logic", "travel_tube" }
                    },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "mode=deconstructでの解体優先度 1-9。既定値は 5", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "赤色の最優先にするか。既定値は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "実行はtrue必須。dryRunでは省略可", Required = false }
                }),
                Handler = args =>
                {
                    if (!string.Equals(args["mode"]?.ToString(), "deconstruct", StringComparison.OrdinalIgnoreCase))
                        return DisconnectUtilityEdges(args);
                    bool dryRun = ToolUtil.GetBool(args, "dryRun", false);
                    if (!dryRun && !ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for cutting conduits");
                    if (!HasRectInput(args) && (args["x"] == null || args["y"] == null))
                        return CallToolResult.Error("areaId, x/y or x1/y1/x2/y2 are required");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (cells > 200)
                        return CallToolResult.Error("Refusing to cut more than 200 cells");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    var layers = GetCutLayers(args["type"]?.ToString());
                    if (layers.Count == 0)
                        return CallToolResult.Error("Invalid type; expected auto, all, gas, liquid, solid, wire, logic or travel_tube");
                    var seen = new HashSet<GameObject>();
                    var results = new List<Dictionary<string, object>>();
                    int queued = 0;
                    int skipped = 0;

                    for (int y = rect["y1"]; y <= rect["y2"]; y++)
                    {
                        for (int x = rect["x1"]; x <= rect["x2"]; x++)
                        {
                            int cell = Grid.XYToCell(x, y);
                            if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                                continue;

                            foreach (var go in FindCuttableObjects(cell, layers))
                            {
                                if (go == null || seen.Contains(go))
                                    continue;
                                seen.Add(go);

                                string error = null;
                                if (dryRun || QueueDeconstruct(go, args, out error))
                                {
                                    queued++;
                                    results.Add(CutResult(go, cell, dryRun ? "would_deconstruct" : "queued", null));
                                }
                                else
                                {
                                    skipped++;
                                    results.Add(CutResult(go, cell, "skipped", error));
                                }
                            }
                        }
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["queued"] = dryRun ? 0 : queued,
                        ["wouldDeconstruct"] = dryRun ? queued : 0,
                        ["mode"] = "deconstruct",
                        ["dryRun"] = dryRun,
                        ["skipped"] = skipped,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["type"] = string.IsNullOrWhiteSpace(args["type"]?.ToString()) ? "auto" : args["type"].ToString(),
                        ["targets"] = results
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static List<ObjectLayer> GetCutLayers(string value)
        {
            string type = string.IsNullOrWhiteSpace(value) ? "auto" : value.Trim().ToLowerInvariant();
            var layers = new List<ObjectLayer>();
            if (type == "auto" || type == "all" || type == "gas")
                layers.AddRange(new[] { ObjectLayer.GasConduit, ObjectLayer.GasConduitTile, ObjectLayer.ReplacementGasConduit });
            if (type == "auto" || type == "all" || type == "liquid")
                layers.AddRange(new[] { ObjectLayer.LiquidConduit, ObjectLayer.LiquidConduitTile, ObjectLayer.ReplacementLiquidConduit });
            if (type == "auto" || type == "all" || type == "solid")
                layers.AddRange(new[] { ObjectLayer.SolidConduit, ObjectLayer.SolidConduitTile, ObjectLayer.ReplacementSolidConduit });
            if (type == "all" || type == "wire")
                layers.AddRange(new[] { ObjectLayer.Wire, ObjectLayer.WireTile, ObjectLayer.ReplacementWire });
            if (type == "all" || type == "logic")
                layers.AddRange(new[] { ObjectLayer.LogicWire, ObjectLayer.LogicWireTile, ObjectLayer.ReplacementLogicWire });
            if (type == "all" || type == "travel_tube")
                layers.AddRange(new[] { ObjectLayer.TravelTubeTile, ObjectLayer.ReplacementTravelTube, ObjectLayer.Building });
            return layers;
        }

        private static List<ObjectLayer> GetEmptyLayers(string value)
        {
            string type = string.IsNullOrWhiteSpace(value) ? "all" : value.Trim().ToLowerInvariant();
            var layers = new List<ObjectLayer>();
            if (type == "all" || type == "gas")
                layers.AddRange(new[] { ObjectLayer.GasConduit, ObjectLayer.GasConduitTile });
            if (type == "all" || type == "liquid")
                layers.AddRange(new[] { ObjectLayer.LiquidConduit, ObjectLayer.LiquidConduitTile });
            if (type == "all" || type == "solid")
                layers.AddRange(new[] { ObjectLayer.SolidConduit, ObjectLayer.SolidConduitTile });
            return layers;
        }

        private static IEnumerable<GameObject> FindCuttableObjects(int cell, List<ObjectLayer> layers)
        {
            foreach (var layer in layers)
            {
                var go = Grid.Objects[cell, (int)layer];
                if (go == null)
                    continue;
                if (layer == ObjectLayer.Building && go.GetComponent<TravelTube>() == null)
                    continue;
                yield return go;
            }
        }

        private static bool QueueDeconstruct(GameObject go, Newtonsoft.Json.Linq.JObject args, out string error)
        {
            return TryQueueObjectDeconstruction(go, args, out error);
        }

        private static Dictionary<string, object> CutResult(GameObject go, int cell, string status, string error)
        {
            var building = go.GetComponent<Building>();
            var kpid = go.GetComponent<KPrefabID>();
            var result = new Dictionary<string, object>
            {
                ["status"] = status,
                ["id"] = kpid?.InstanceID ?? -1,
                ["prefabId"] = building?.Def?.PrefabID ?? kpid?.PrefabTag.Name ?? go.name,
                ["name"] = ToolUtil.CleanName(go.GetProperName()),
                ["x"] = Grid.CellColumn(cell),
                ["y"] = Grid.CellRow(cell)
            };
            if (!string.IsNullOrEmpty(error))
                result["error"] = error;
            return result;
        }
}
}
