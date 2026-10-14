using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class FarmingTools
    {
        public static McpTool UprootArea()
        {
            return new McpTool
            {
                Name = "plants_uproot_area",
                Group = "farming",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "farming_uproot_area", "plants_cancel_uproot" },
                Tags = new List<string> { "farming", "plants", "uproot", "harvest" },
                Description = "互換用の入口。colony_control domain=bio bioDomain=farming action=uproot を優先して使う。領域内の植物の掘り起こしを指定または取り消す",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "mark または cancel。既定値は mark", Required = false, EnumValues = new List<string> { "mark", "cancel" } },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "掘り起こし作業の優先度 1-9。既定値は 5", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "赤色の最優先にするか。既定値は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "領域が 100 セルを超える場合は true が必須。mark 操作では true の指定を推奨する", Required = false }
                }),
                Handler = args =>
                {
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (cells > 100 && !ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required when changing uproot orders in more than 100 cells");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    bool mark = (args["action"]?.ToString() ?? "mark").Trim().ToLowerInvariant() != "cancel";
                    int changed = 0;
                    var results = new List<Dictionary<string, object>>();
                    foreach (var uprootable in Components.Uprootables.Items)
                    {
                        var go = uprootable?.gameObject;
                        if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                            continue;
                        int cell = Grid.PosToCell(go);
                        if (!CellInRect(cell, rect, worldId))
                            continue;

                        if (mark)
                        {
                            if (!uprootable.CanUproot())
                                continue;
                            uprootable.MarkForUproot();
                            ApplyPriority(go, args);
                            results.Add(TargetInfo(go, "marked"));
                        }
                        else
                        {
                            if (!uprootable.IsMarkedForUproot)
                                continue;
                            uprootable.ForceCancelUproot();
                            // Cancel only uproot; generic Cancel also clears Harvestable chores/settings.
                            results.Add(TargetInfo(go, "cancelled"));
                        }
                        changed++;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["action"] = mark ? "mark" : "cancel",
                        ["changed"] = changed,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["targets"] = results.Take(200).ToList(),
                        ["truncatedTargets"] = Math.Max(0, results.Count - 200)
                    }, McpJsonUtil.Settings));
                }
            };
        }
    }
}
