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
        public static McpTool MopArea()
        {
            return new McpTool
            {
                Name = "orders_mop_area",
                Group = "orders",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "mop_area", "liquids_mop_area", "water_mop_area", "mop_liquid_area", "mop_water_area" },
                Tags = new List<string> { "orders", "mop", "liquid", "water", "polluted-water", "floor", "spill", "地上的水", "拖地", "液体", "水" },
                Description = "矩形範囲内の地面にある水、汚染水、その他のモップがけ可能な液体セルにモップがけ指示を出す。固体のがれきの掃除ではない。ゲームの制限に従い、下に地面があり、液体の質量がモップがけ可能な上限以下である必要がある",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "モップがけ作業の優先度 1-9。既定値 5", Required = false },
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
                        return CallToolResult.Error("confirm=true is required when mopping more than 100 cells");

                    var prefab = Assets.GetPrefab(new Tag("MopPlacer"));
                    if (prefab == null)
                        return CallToolResult.Error("MopPlacer prefab is not available");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    int marked = 0;
                    int skipped = 0;
                    var results = new List<Dictionary<string, object>>();
                    var targetCells = new List<int>();
                    var executionSkipped = new Dictionary<string, int>();
                    for (int y = rect["y1"]; y <= rect["y2"]; y++)
                    {
                        for (int x = rect["x1"]; x <= rect["x2"]; x++)
                        {
                            int cell = Grid.XYToCell(x, y);
                            if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                                continue;
                            if (Grid.Solid[cell] || !Grid.Element[cell].IsLiquid)
                                continue;
                        if (Grid.Objects[cell, (int)ObjectLayer.MopPlacer] != null)
                        {
                            skipped++;
                            IncrementSkip(executionSkipped, "already_queued");
                            continue;
                        }
                            bool onFloor = Grid.IsValidCell(Grid.CellBelow(cell)) && Grid.Solid[Grid.CellBelow(cell)];
                            bool smallEnough = Grid.Mass[cell] <= MopTool.maxMopAmt;
                        if (!onFloor || !smallEnough)
                        {
                            skipped++;
                            IncrementSkip(executionSkipped, onFloor ? "too_much_liquid" : "no_floor");
                            results.Add(CellResult(cell, onFloor ? "skipped_too_much_liquid" : "skipped_no_floor"));
                            continue;
                        }

                            if (DebugHandler.InstantBuildMode)
                            {
                            Moppable.MopCell(cell, 1000000f, null);
                            marked++;
                            targetCells.Add(cell);
                            results.Add(CellResult(cell, "instant_mopped"));
                            continue;
                        }

                            var placer = Util.KInstantiate(prefab);
                            Grid.Objects[cell, (int)ObjectLayer.MopPlacer] = placer;
                            var position = Grid.CellToPosCBC(cell, Grid.SceneLayer.Move);
                            position.z -= 0.15f;
                            placer.transform.SetPosition(position);
                            placer.SetActive(true);
                        ApplyPriority(placer, args);
                        marked++;
                        targetCells.Add(cell);
                        results.Add(CellResult(cell, "marked"));
                        }
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["marked"] = marked,
                        ["skipped"] = skipped,
                        ["execution"] = CellExecutionMetadata("mop", worldId, targetCells, executionSkipped, false, 200),
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["targets"] = results.Take(200).ToList(),
                        ["truncatedTargets"] = Math.Max(0, results.Count - 200)
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool DisinfectArea()
        {
            return new McpTool
            {
                Name = "orders_disinfect_area",
                Group = "orders",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "disinfect_area", "germs_disinfect_area" },
                Description = "互換用エントリー。orders_control domain=area action=disinfect を優先して使用すること。矩形範囲内の、病原菌が付着した消毒可能なオブジェクトを指定する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "消毒作業の優先度 1-9。既定値 5", Required = false },
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
                        return CallToolResult.Error("confirm=true is required when disinfecting more than 100 cells");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    var seen = new HashSet<GameObject>();
                    int marked = 0;
                    for (int y = rect["y1"]; y <= rect["y2"]; y++)
                    {
                        for (int x = rect["x1"]; x <= rect["x2"]; x++)
                        {
                            int cell = Grid.XYToCell(x, y);
                            if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                                continue;

                            for (int layer = 0; layer < (int)ObjectLayer.NumLayers; layer++)
                            {
                                var go = Grid.Objects[cell, layer];
                                if (go == null || seen.Contains(go))
                                    continue;
                                seen.Add(go);

                                var disinfectable = go.GetComponent<Disinfectable>();
                                var element = go.GetComponent<PrimaryElement>();
                                if (disinfectable == null || element == null || element.DiseaseCount <= 0)
                                    continue;

                                disinfectable.MarkForDisinfect();
                                ApplyPriority(go, args);
                                marked++;
                            }
                        }
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["marked"] = marked,
                        ["worldId"] = worldId,
                        ["rect"] = rect
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static Dictionary<string, object> ScanLiquidCells(Dictionary<string, int> rect, int worldId, int sampleLimit)
        {
            int count = 0;
            var samples = new List<Dictionary<string, object>>();

            for (int y = rect["y1"]; y <= rect["y2"]; y++)
            {
                for (int x = rect["x1"]; x <= rect["x2"]; x++)
                {
                    int cell = Grid.XYToCell(x, y);
                    if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                        continue;
                    if (Grid.Solid[cell] || !Grid.Element[cell].IsLiquid)
                        continue;

                    count++;
                    if (samples.Count < sampleLimit)
                        samples.Add(CellResult(cell, "liquid_in_sweep_area"));
                }
            }

            return new Dictionary<string, object>
            {
                ["count"] = count,
                ["samples"] = samples
            };
        }

        private static List<Dictionary<string, object>> SweepPreviewRisks(Dictionary<string, object> liquidScan)
        {
            var risks = new List<Dictionary<string, object>>();
            int liquidCount = liquidScan != null && liquidScan.ContainsKey("count") ? Convert.ToInt32(liquidScan["count"]) : 0;
            if (liquidCount > 0)
            {
                risks.Add(new Dictionary<string, object>
                {
                    ["type"] = "liquid_in_area",
                    ["severity"] = "info",
                    ["count"] = liquidCount,
                    ["samples"] = liquidScan.ContainsKey("samples") ? liquidScan["samples"] : null,
                    ["message"] = "Sweep ignores liquid cells; use orders_control domain=area action=mop for floor liquids."
                });
            }
            return risks;
        }
}
}
