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
        private static CallToolResult DisconnectReadUnavailable(bool dryRun, string reason)
        {
            return CallToolResult.Error(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["mode"] = "disconnect", ["dryRun"] = dryRun, ["success"] = false,
                ["reasonCode"] = "utility_preflight_read_unavailable", ["readReasonCode"] = reason,
                ["wouldDisconnectEdges"] = null, ["disconnectedEdges"] = 0,
                ["connectionVerified"] = null, ["nativeCalls"] = 0, ["partial"] = false,
                ["deconstructionQueued"] = false,
                ["error"] = McpLocalization.Format("utility_read_safety.state_unavailable", null, reason)
            }, McpJsonUtil.Settings));
        }

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
            var targetCells = new Dictionary<KAnimGraphTileVisualizer, int>();
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
                    if (!Grid.IsVisible(a) || !Grid.IsVisible(b))
                        return CallToolResult.Error("Disconnect edge is not visible; no changes applied");
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
                        var firstObject = Grid.Objects[a, (int)layer];
                        var secondObject = Grid.Objects[b, (int)layer];
                        if (firstObject == null || secondObject == null) continue;
                        var first = firstObject.GetComponent<KAnimGraphTileVisualizer>();
                        var second = secondObject.GetComponent<KAnimGraphTileVisualizer>();
                        if (first == null || second == null)
                            return DisconnectReadUnavailable(dryRun, "spawned_manager_unavailable");
                        if (!seen.Add(first)) continue;
                        UtilityConnections beforeA, beforeB;
                        try
                        {
                            if (!NativeReadProviderPolicy.TryGetCachedUtilityManager(first, layer,
                                out IUtilityNetworkMgr manager, out string reason))
                                return DisconnectReadUnavailable(dryRun, reason);
                            if (!NativeReadProviderPolicy.TryGetCachedUtilityManager(second, layer,
                                out IUtilityNetworkMgr secondManager, out reason))
                                return DisconnectReadUnavailable(dryRun, reason);
                            if (!ReferenceEquals(manager, secondManager))
                                return DisconnectReadUnavailable(dryRun, "utility_manager_mismatch");
                            beforeA = requested.TryGetValue(first, out var va) ? va : manager.GetConnections(a, false);
                            beforeB = requested.TryGetValue(second, out var vb) ? vb : manager.GetConnections(b, false);
                        }
                        catch { return DisconnectReadUnavailable(dryRun, "utility_mask_read_failed"); }
                        if ((beforeA & from) == 0 && (beforeB & to) == 0) continue;
                        if ((targetCells.TryGetValue(first, out int firstCell) && firstCell != a)
                            || (targetCells.TryGetValue(second, out int secondCell) && secondCell != b))
                            return CallToolResult.Error("Disconnect target spans multiple requested cells; no changes applied");
                        targetCells[first] = a;
                        targetCells[second] = b;
                        requested[first] = beforeA & ~from;
                        requested[second] = beforeB & ~to;
                        edges.Add(new { from = new[] { x, y }, to = new[] { nx, ny },
                            layer = layer.ToString(), firstId = first.GetComponent<KPrefabID>()?.InstanceID,
                            secondId = second.GetComponent<KPrefabID>()?.InstanceID });
                    }
                }
            }
            string nativeError = null;
            int nativeCalls = 0;
            if (!dryRun && requested.Count > 0)
            {
                try
                {
                    // Resolve the exact native UI entrypoint and every target before
                    // invoking any callback. Missing UI/API never enables a direct fallback.
                    var native = NativeUtilityDisconnect.Prepare(DisconnectTool.Instance);
                    var pending = new List<System.Action>();
                    foreach (var pair in requested)
                    {
                        var graph = pair.Key;
                        int cell = targetCells[graph];
                        if (Grid.PosToCell(graph) != cell)
                            throw new InvalidOperationException("Native disconnect target moved outside its requested cell; no changes applied");
                        var target = graph.gameObject;
                        var building = target.GetComponent<Building>();
                        var utility = building?.Def?.BuildingComplete?.GetComponent<IHaveUtilityNetworkMgr>();
                        var manager = graph.ConnectionManager;
                        if (utility == null || manager == null
                            || !ReferenceEquals(graph.connectionManager, manager)
                            || !ReferenceEquals(manager, utility.GetNetworkManager()))
                            throw new InvalidOperationException("Native disconnect target is unavailable or changed; no changes applied");
                        UtilityConnections remove = manager.GetConnections(cell, false) & ~pair.Value;
                        pending.Add(() => native.Execute(cell, target, utility, remove));
                    }
                    foreach (var disconnect in pending)
                    {
                        // A callback can fail after changing native state. Report an
                        // uncertain partial result rather than repairing it ourselves.
                        nativeCalls++;
                        disconnect();
                    }
                }
                catch (Exception ex)
                {
                    nativeError = ex.Message;
                }
            }
            bool? connectionVerified = null;
            if (!dryRun)
            {
                try
                {
                    connectionVerified = requested.All(pair => pair.Key != null
                        && Grid.PosToCell(pair.Key) == targetCells[pair.Key]
                        && pair.Key.ConnectionManager != null
                        && pair.Key.ConnectionManager.GetConnections(targetCells[pair.Key], false) == pair.Value);
                    if (connectionVerified == false && nativeError == null)
                        nativeError = "Native disconnect did not produce the requested connection state";
                }
                catch (Exception ex)
                {
                    nativeError = (nativeError == null ? "" : nativeError + "; ")
                        + "Native disconnect verification failed: " + ex.Message;
                }
            }
            bool success = (dryRun || connectionVerified == true) && nativeError == null;
            var result = new Dictionary<string, object>
            {
                ["mode"] = "disconnect", ["dryRun"] = dryRun, ["worldId"] = world,
                ["rect"] = rect, ["success"] = success, ["wouldDisconnectEdges"] = edges.Count,
                ["disconnectedEdges"] = dryRun || nativeCalls == 0 ? (object)0 : success ? (object)edges.Count : null,
                ["deconstructionQueued"] = false,
                ["connectionVerified"] = connectionVerified,
                ["flowVerified"] = false,
                ["executionPath"] = NativeUtilityDisconnect.ExecutionPath,
                ["nativeCalls"] = nativeCalls,
                ["partial"] = !dryRun && nativeCalls > 0 && !success,
                ["error"] = nativeError
            };
            if (ToolUtil.GetBool(args, "detail", false)) result["edges"] = edges;
            var json = JsonConvert.SerializeObject(result, McpJsonUtil.Settings);
            return success ? CallToolResult.Text(json) : CallToolResult.Error(json);
        }
    }
}
