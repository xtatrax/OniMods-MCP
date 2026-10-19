using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private sealed class ExistingUtilityEndpoint
        {
            internal int Cell;
            internal int? Id;
            internal GameObject Target;
            internal BuildingDef Def;
            internal KAnimGraphTileVisualizer Graph;
            internal IUtilityNetworkMgr Manager;
            internal Tag Material;
            internal UtilityConnections Before;
        }

        private static ExistingUtilityEndpoint ReadExistingUtility(int cell, ObjectLayer layer)
        {
            var target = Grid.Objects[cell, (int)layer];
            var complete = target?.GetComponent<BuildingComplete>();
            var graph = target?.GetComponent<KAnimGraphTileVisualizer>();
            var element = target?.GetComponent<PrimaryElement>();
            var def = complete?.Def;
            if (def == null || graph == null || element?.Element == null
                || def.TileLayer != layer || Grid.Objects[cell, (int)def.ObjectLayer] != target || Grid.PosToCell(target) != cell
                || (def.ReplacementLayer != ObjectLayer.NumLayers
                    && Grid.Objects[cell, (int)def.ReplacementLayer] != null)
                || graph.ConnectionManager == null
                || !ReferenceEquals(graph.connectionManager, graph.ConnectionManager)
                || !ReferenceEquals(def.BuildingComplete?.GetComponent<IHaveUtilityNetworkMgr>()?.GetNetworkManager(), graph.ConnectionManager))
                throw new InvalidOperationException(McpLocalization.Get("connect_existing.target_required"));
            return new ExistingUtilityEndpoint { Cell = cell, Target = target, Def = def, Graph = graph,
                Manager = graph.ConnectionManager, Material = element.Element.tag,
                Before = graph.ConnectionManager.GetConnections(cell, false), Id = target.GetComponent<KPrefabID>()?.InstanceID };
        }

        private static CallToolResult ConnectExistingUtility(JObject args)
        {
            bool dryRun = ToolUtil.GetBool(args, "dryRun", false);
            if (!dryRun && !ToolUtil.GetBool(args, "confirm", false))
                return CallToolResult.Error(McpLocalization.Get("connect_existing.confirm_required"));
            // Deliberately a single edge. Area/path/build options must not be
            // accepted and silently interpreted as a smaller connection request.
            if (args["x"] == null || args["y"] == null || args["x1"] != null || args["y1"] != null
                || args["x2"] != null || args["y2"] != null || args["areaId"] != null || args["points"] != null
                || args["fromX"] != null || args["fromY"] != null || args["toX"] != null || args["toY"] != null
                || args["fromQuery"] != null || args["toQuery"] != null || args["prefabId"] != null
                || args["material"] != null || args["id"] != null || args["steps"] != null)
                return CallToolResult.Error(McpLocalization.Get("connect_existing.single_edge_required"));
            int x = ToolUtil.GetInt(args, "x") ?? -1, y = ToolUtil.GetInt(args, "y") ?? -1;
            int dx = 0, dy = 0;
            string direction = (args["direction"]?.ToString() ?? "").Trim().ToLowerInvariant();
            switch (direction)
            {
                case "left": dx = -1; break;
                case "right": dx = 1; break;
                case "up": dy = 1; break;
                case "down": dy = -1; break;
                default: return CallToolResult.Error(McpLocalization.Get("connect_existing.direction_required"));
            }
            ObjectLayer layer;
            switch ((args["type"]?.ToString() ?? "").ToLowerInvariant())
            {
                case "liquid": layer = ObjectLayer.LiquidConduitTile; break;
                case "gas": layer = ObjectLayer.GasConduitTile; break;
                case "solid": layer = ObjectLayer.SolidConduitTile; break;
                case "wire": layer = ObjectLayer.WireTile; break;
                case "logic": layer = ObjectLayer.LogicWireTile; break;
                default: return CallToolResult.Error(McpLocalization.Get("connect_existing.type_required"));
            }
            int nx = x + dx, ny = y + dy, world = ToolUtil.ResolveWorldId(args);
            if (x < 0 || y < 0 || nx < 0 || ny < 0 || x >= Grid.WidthInCells || nx >= Grid.WidthInCells
                || y >= Grid.HeightInCells || ny >= Grid.HeightInCells
                || ClusterManager.Instance == null || world != ClusterManager.Instance.activeWorldId)
                return CallToolResult.Error(McpLocalization.Get("connect_existing.scope_invalid"));
            int a = Grid.XYToCell(x, y), b = Grid.XYToCell(nx, ny);
            if (!ToolUtil.CellMatchesWorld(a, world) || !ToolUtil.CellMatchesWorld(b, world)
                || !Grid.IsVisible(a) || !Grid.IsVisible(b))
                return CallToolResult.Error(McpLocalization.Get("connect_existing.scope_invalid"));
            if (SpeedControlScreen.Instance == null || !SpeedControlScreen.Instance.IsPaused)
                return CallToolResult.Error(McpLocalization.Get("connect_existing.pause_required"));

            ExistingUtilityEndpoint first, second;
            try
            {
                first = ReadExistingUtility(a, layer); second = ReadExistingUtility(b, layer);
                if (first.Def != second.Def || first.Material != second.Material
                    || !ReferenceEquals(first.Manager, second.Manager))
                    return CallToolResult.Error(McpLocalization.Get("connect_existing.same_required"));
                // Limit to the linear prefabs whose native tool has been inspected.
                if (!IsLinearUtilityPrefab(first.Def.PrefabID))
                    return CallToolResult.Error(McpLocalization.Get("connect_existing.target_required"));
            }
            catch (Exception ex) { return CallToolResult.Error(ex.Message); }
            UtilityConnections from = dx == 1 ? UtilityConnections.Right : dx == -1 ? UtilityConnections.Left
                : dy == 1 ? UtilityConnections.Up : UtilityConnections.Down;
            UtilityConnections to = dx == 1 ? UtilityConnections.Left : dx == -1 ? UtilityConnections.Right
                : dy == 1 ? UtilityConnections.Down : UtilityConnections.Up;
            bool alreadyConnected = (first.Before & from) != 0 && (second.Before & to) != 0;
            NativeUtilityConnect native = null;
            string error = null;
            bool? verified = dryRun ? (bool?)null : alreadyConnected;
            if (!dryRun && !alreadyConnected)
            {
                try
                {
                    BaseUtilityBuildTool tool = layer == ObjectLayer.WireTile
                        ? (BaseUtilityBuildTool)WireBuildTool.Instance : UtilityBuildTool.Instance;
                    native = NativeUtilityConnect.Prepare(tool);
                    native.Execute(first.Def, first.Material, a, b, () =>
                    {
                        var currentA = ReadExistingUtility(a, layer);
                        var currentB = ReadExistingUtility(b, layer);
                        if (currentA.Target != first.Target || currentB.Target != second.Target
                            || currentA.Id != first.Id || currentB.Id != second.Id
                            || currentA.Def != first.Def || currentB.Def != second.Def
                            || currentA.Graph != first.Graph || currentB.Graph != second.Graph
                            || !Grid.IsVisible(a) || !Grid.IsVisible(b)
                            || !ToolUtil.CellMatchesWorld(a, world) || !ToolUtil.CellMatchesWorld(b, world)
                            || currentA.Material != first.Material || currentB.Material != second.Material
                            || !ReferenceEquals(currentA.Manager, first.Manager) || !ReferenceEquals(currentB.Manager, first.Manager)
                            || currentA.Before != first.Before || currentB.Before != second.Before
                            || !SpeedControlScreen.Instance.IsPaused || world != ClusterManager.Instance.activeWorldId)
                            throw new InvalidOperationException(McpLocalization.Get("connect_existing.target_changed"));
                    });
                }
                catch (Exception ex) { error = ex.Message; }
                try
                {
                    var afterA = ReadExistingUtility(a, layer); var afterB = ReadExistingUtility(b, layer);
                    verified = afterA.Target == first.Target && afterB.Target == second.Target
                        && afterA.Id == first.Id && afterB.Id == second.Id
                        && afterA.Def == first.Def && afterB.Def == second.Def
                        && afterA.Material == first.Material && afterB.Material == second.Material
                        && ReferenceEquals(afterA.Manager, first.Manager) && ReferenceEquals(afterB.Manager, first.Manager)
                        && afterA.Before == (first.Before | from) && afterB.Before == (second.Before | to);
                    if (verified == false && error == null) error = McpLocalization.Get("connect_existing.not_verified");
                }
                catch (Exception ex) { verified = null; error = error == null ? ex.Message : error + "; " + ex.Message; }
            }
            bool success = error == null && (dryRun || verified == true);
            var result = new Dictionary<string, object>
            {
                ["success"] = success, ["dryRun"] = dryRun, ["worldId"] = world,
                ["from"] = new[] { x, y }, ["to"] = new[] { nx, ny }, ["layer"] = layer.ToString(),
                ["prefabId"] = first.Def.PrefabID,
                ["firstId"] = first.Id,
                ["secondId"] = second.Id,
                ["alreadyConnected"] = alreadyConnected, ["wouldConnectEdges"] = alreadyConnected ? 0 : 1,
                ["uiRetargeted"] = native == null ? (object)false : native.UiRetargeted,
                ["actionAttempted"] = native?.CommitAttempted ?? false,
                ["partial"] = !success && (native?.CommitAttempted ?? false),
                ["connectedEdges"] = dryRun || alreadyConnected ? (object)0 : success ? (object)1 : null,
                ["connectionVerified"] = verified, ["flowVerified"] = false,
                ["constructionQueued"] = dryRun || alreadyConnected || success ? (object)false : null, ["executionPath"] = NativeUtilityConnect.ExecutionPath, ["error"] = error
            };
            string json = JsonConvert.SerializeObject(result, McpJsonUtil.Settings);
            return success ? CallToolResult.Text(json) : CallToolResult.Error(json);
        }
    }
}
