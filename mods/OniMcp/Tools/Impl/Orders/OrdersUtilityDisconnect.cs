using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class OrdersTools
    {
        private static CallToolResult DisconnectUtilityEdges(JObject args)
        {
            string mode = args["mode"]?.ToString() ?? "disconnect";
            if (!string.Equals(mode, "disconnect", StringComparison.OrdinalIgnoreCase))
                return CallToolResult.Error("mode must be disconnect or deconstruct");
            bool dryRun = ToolUtil.GetBool(args, "dryRun", false);
            if (!dryRun && !ToolUtil.GetBool(args, "confirm", false))
                return CallToolResult.Error("confirm=true is required for disconnecting utilities");
            if (!HasRectInput(args) && (args["x"] == null || args["y"] == null))
                return CallToolResult.Error("areaId, x/y, or rectangle is required");
            var rect = ToolUtil.GetRect(args);
            int count = RectCellCount(rect);
            if (count < 1 || count > 200) return CallToolResult.Error("Disconnect area must contain 1..200 cells");
            var layers = GetCutLayers(args["type"]?.ToString());
            if (layers.Count == 0) return CallToolResult.Error("Invalid utility type");
            int world = ToolUtil.ResolveWorldId(args);
            string direction = (args["direction"]?.ToString() ?? "").Trim().ToLowerInvariant();
            int dx = 0, dy = 0;
            if (direction == "left") dx = -1;
            else if (direction == "right") dx = 1;
            else if (direction == "up") dy = 1;
            else if (direction == "down") dy = -1;
            else if (direction.Length > 0) return CallToolResult.Error("direction must be up/down/left/right");
            if (count == 1 && direction.Length == 0)
                return CallToolResult.Error("Single-cell disconnection requires direction; no building is removed");
            if (count > 1 && direction.Length > 0)
                return CallToolResult.Error("direction is only supported for a single cell");

            var requested = new Dictionary<KAnimGraphTileVisualizer, UtilityConnections>();
            var edges = new List<object>();
            for (int y = rect["y1"]; y <= rect["y2"]; y++)
            for (int x = rect["x1"]; x <= rect["x2"]; x++)
            {
                var offsets = count == 1 ? new[] { new[] { dx, dy } }
                    : new[] { new[] { 1, 0 }, new[] { 0, 1 } };
                foreach (var offset in offsets)
                {
                    int nx = x + offset[0], ny = y + offset[1];
                    if (count > 1 && (nx > rect["x2"] || ny > rect["y2"])) continue;
                    if (x < 0 || nx < 0 || y < 0 || ny < 0 || x >= Grid.WidthInCells
                        || nx >= Grid.WidthInCells || y >= Grid.HeightInCells || ny >= Grid.HeightInCells)
                        return CallToolResult.Error("Disconnect edge is outside grid; no changes applied");
                    int a = Grid.XYToCell(x, y), b = Grid.XYToCell(nx, ny);
                    if (!ToolUtil.CellMatchesWorld(a, world) || !ToolUtil.CellMatchesWorld(b, world))
                        return CallToolResult.Error("Disconnect edge crosses world boundary; no changes applied");
                    UtilityConnections from = offset[0] == 1 ? UtilityConnections.Right
                        : offset[0] == -1 ? UtilityConnections.Left
                        : offset[1] == 1 ? UtilityConnections.Up : UtilityConnections.Down;
                    UtilityConnections to = from == UtilityConnections.Right ? UtilityConnections.Left
                        : from == UtilityConnections.Left ? UtilityConnections.Right
                        : from == UtilityConnections.Up ? UtilityConnections.Down : UtilityConnections.Up;
                    // All objects are inspected before any update. Bridges and unrelated
                    // building-layer objects have no graph and are never deconstructed.
                    var seen = new HashSet<KAnimGraphTileVisualizer>();
                    foreach (var layer in layers)
                    {
                        var first = Grid.Objects[a, (int)layer]?.GetComponent<KAnimGraphTileVisualizer>();
                        var second = Grid.Objects[b, (int)layer]?.GetComponent<KAnimGraphTileVisualizer>();
                        if (first == null || second == null || !seen.Add(first)) continue;
                        var manager = first.ConnectionManager;
                        if (manager == null || !ReferenceEquals(manager, second.ConnectionManager)) continue;
                        var beforeA = requested.TryGetValue(first, out var va) ? va : manager.GetConnections(a, false);
                        var beforeB = requested.TryGetValue(second, out var vb) ? vb : manager.GetConnections(b, false);
                        if ((beforeA & from) == 0 && (beforeB & to) == 0) continue;
                        requested[first] = beforeA & ~from;
                        requested[second] = beforeB & ~to;
                        edges.Add(new { from = new[] { x, y }, to = new[] { nx, ny },
                            layer = layer.ToString(), firstId = first.GetComponent<KPrefabID>()?.InstanceID,
                            secondId = second.GetComponent<KPrefabID>()?.InstanceID });
                    }
                }
            }
            if (!dryRun)
            {
                foreach (var pair in requested)
                {
                    pair.Key.connectionManager = pair.Key.ConnectionManager;
                    pair.Key.UpdateConnections(pair.Value);
                }
                foreach (var manager in requested.Keys.Select(g => g.ConnectionManager).Distinct())
                    manager.ForceRebuildNetworks();
                foreach (var graph in requested.Keys) graph.Refresh();
            }
            bool verified = dryRun || requested.All(pair =>
                pair.Key.ConnectionManager.GetConnections(Grid.PosToCell(pair.Key), false) == pair.Value);
            var result = new Dictionary<string, object>
            {
                ["mode"] = "disconnect", ["dryRun"] = dryRun, ["worldId"] = world,
                ["rect"] = rect, ["success"] = verified, ["wouldDisconnectEdges"] = edges.Count,
                ["disconnectedEdges"] = dryRun ? 0 : edges.Count, ["deconstructionQueued"] = false,
                ["connectionVerified"] = dryRun ? (object)null : verified,
                ["flowVerified"] = false
            };
            if (ToolUtil.GetBool(args, "detail", false)) result["edges"] = edges;
            var json = JsonConvert.SerializeObject(result, McpJsonUtil.Settings);
            return verified ? CallToolResult.Text(json) : CallToolResult.Error(json);
        }
    }
}
