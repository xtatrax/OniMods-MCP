using System;
using System.Collections.Generic;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static bool IsExactConnectionUtilityPrefab(string prefabId)
        {
            var prefab = Assets.GetBuildingDef(prefabId)?.BuildingComplete;
            return prefab != null && prefab.GetComponent<KAnimGraphTileVisualizer>() != null
                && (prefab.GetComponent<Wire>() != null || prefab.GetComponent<LogicWire>() != null
                    || prefab.GetComponent<Conduit>() != null || prefab.GetComponent<SolidConduit>() != null);
        }

        private static bool EnsureCompletedUtilityNetworkRegistration(
            BuildingDef def, GameObject completed, bool isolateConnections, out string error)
        {
            // Do not AddToNetworks here. Conduit registers after structure temperature
            // registration; a second registration destroys the previous object, even
            // when it is the same instance. SolidConduit is not a FlowUtilityNetwork.IItem.
            error = null;
            if (def == null || !IsExactConnectionUtilityPrefab(def.PrefabID)) return true;
            var graph = completed?.GetComponent<KAnimGraphTileVisualizer>();
            if (graph == null || !graph.isPhysicalBuilding || graph.ConnectionManager == null)
            {
                error = "completed utility is missing its graph or native manager";
                return false;
            }
            graph.connectionManager = graph.ConnectionManager;
            if (isolateConnections) graph.UpdateConnections((UtilityConnections)0);
            graph.Refresh();
            return true;
        }

        private static bool RefreshAndValidateUtilityPathNetwork(
            BuildingDef def, List<CellCoord> path, out string error)
        {
            error = null;
            if (def == null || !IsExactConnectionUtilityPrefab(def.PrefabID) || path == null || path.Count == 0)
                return true;

            IUtilityNetworkMgr manager = def.BuildingComplete
                .GetComponent<KAnimGraphTileVisualizer>()?.ConnectionManager;
            if (manager == null)
            {
                error = "utility path has no native network manager";
                return false;
            }

            var requestedConnections = new Dictionary<int, UtilityConnections>();
            foreach (var point in path)
            {
                int cell = Grid.XYToCell(point.x, point.y);
                requestedConnections[cell] = manager.GetConnections(
                    cell, is_physical_building: false);
            }

            for (int i = 1; i < path.Count; i++)
            {
                int previous = Grid.XYToCell(path[i - 1].x, path[i - 1].y);
                int current = Grid.XYToCell(path[i].x, path[i].y);
                if (!TryExpectedConnectionBits(previous, current,
                        out UtilityConnections fromBit, out UtilityConnections toBit))
                {
                    error = "utility path contains a non-adjacent segment";
                    return false;
                }
                requestedConnections[previous] |= fromBit;
                requestedConnections[current] |= toBit;
            }

            if (!ApplyRequestedUtilityPathConnections(
                    def, requestedConnections, manager, out error))
                return false;

            for (int i = 1; i < path.Count; i++)
            {
                int previous = Grid.XYToCell(path[i - 1].x, path[i - 1].y);
                int current = Grid.XYToCell(path[i].x, path[i].y);
                TryExpectedConnectionBits(previous, current,
                    out UtilityConnections fromBit, out UtilityConnections toBit);
                if ((manager.GetConnections(previous, is_physical_building: false) & fromBit) == 0
                    || (manager.GetConnections(current, is_physical_building: false) & toBit) == 0)
                {
                    error = "utility network rebuild did not register a bidirectional adjacent path segment";
                    return false;
                }
            }

            return true;
        }

        private static bool ApplyRequestedUtilityPathConnections(BuildingDef def,
            IDictionary<int, UtilityConnections> requestedConnections,
            IUtilityNetworkMgr manager, out string error)
        {
            error = null;
            var visualizers = new Dictionary<int, KAnimGraphTileVisualizer>();
            foreach (var item in requestedConnections)
            {
                if (!TryGetCompatibleUtilityVisualizer(def, item.Key, manager,
                        out KAnimGraphTileVisualizer visualizer))
                {
                    error = "utility path cell is missing its matching blueprint or completed graph";
                    return false;
                }
                foreach (UtilityConnections bit in new[] { UtilityConnections.Left, UtilityConnections.Right,
                    UtilityConnections.Up, UtilityConnections.Down })
                {
                    if ((item.Value & bit) != 0 && (manager.GetConnections(item.Key, false) & bit) == 0
                        && !manager.CanAddConnection(bit, item.Key, false, out error)) return false;
                }
                visualizers[item.Key] = visualizer;
            }

            foreach (var item in requestedConnections)
            {
                var visualizer = visualizers[item.Key];
                visualizer.connectionManager = manager;
                visualizer.UpdateConnections(item.Value);
            }
            if (!RefreshUtilityNetworkManager(manager, out error))
                return false;
            foreach (var visualizer in visualizers.Values)
                visualizer.Refresh();
            return true;
        }

        private static bool TryGetCompatibleUtilityVisualizer(BuildingDef def, int cell,
            IUtilityNetworkMgr manager, out KAnimGraphTileVisualizer visualizer)
        {
            visualizer = null;
            if (def == null || !Grid.IsValidCell(cell))
                return false;
            var go = Grid.Objects[cell, (int)def.TileLayer];
            visualizer = go?.GetComponent<KAnimGraphTileVisualizer>();
            var building = go?.GetComponent<Building>();
            return visualizer != null && EqualsIgnoreCase(building?.Def?.PrefabID, def.PrefabID)
                && ReferenceEquals(visualizer.ConnectionManager, manager);
        }

        private static bool IsCompletedUtilityPath(BuildingDef def, List<CellCoord> path)
        {
            if (def == null || !IsExactConnectionUtilityPrefab(def.PrefabID) || path == null || path.Count == 0)
                return false;
            foreach (var point in path)
            {
                int cell = Grid.XYToCell(point.x, point.y);
                var complete = Grid.IsValidCell(cell)
                    ? Grid.Objects[cell, (int)def.ObjectLayer]?.GetComponent<BuildingComplete>()
                    : null;
                if (!EqualsIgnoreCase(complete?.Def?.PrefabID, def.PrefabID))
                    return false;
            }
            return true;
        }

        private static bool IsPhysicalUtilityPathConnected(BuildingDef def, List<CellCoord> path)
        {
            if (!IsCompletedUtilityPath(def, path)) return false;
            var manager = def.BuildingComplete.GetComponent<KAnimGraphTileVisualizer>()?.ConnectionManager;
            if (manager == null) return false;
            for (int i = 1; i < path.Count; i++)
            {
                int a = Grid.XYToCell(path[i - 1].x, path[i - 1].y);
                int b = Grid.XYToCell(path[i].x, path[i].y);
                if (!TryExpectedConnectionBits(a, b, out var from, out var to)
                    || (manager.GetConnections(a, true) & from) == 0
                    || (manager.GetConnections(b, true) & to) == 0) return false;
            }
            return true;
        }

        private static bool RefreshUtilityNetworkManager(IUtilityNetworkMgr manager, out string error)
        {
            error = null;
            try
            {
                manager.ForceRebuildNetworks();
                return true;
            }
            catch (Exception ex)
            {
                error = "utility network refresh failed: " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryExpectedConnectionBits(int fromCell, int toCell,
            out UtilityConnections fromBit, out UtilityConnections toBit)
        {
            fromBit = toBit = (UtilityConnections)0;
            int dx = Grid.CellColumn(toCell) - Grid.CellColumn(fromCell);
            int dy = Grid.CellRow(toCell) - Grid.CellRow(fromCell);
            if (dx == 1 && dy == 0) { fromBit = UtilityConnections.Right; toBit = UtilityConnections.Left; }
            else if (dx == -1 && dy == 0) { fromBit = UtilityConnections.Left; toBit = UtilityConnections.Right; }
            else if (dx == 0 && dy == 1) { fromBit = UtilityConnections.Up; toBit = UtilityConnections.Down; }
            else if (dx == 0 && dy == -1) { fromBit = UtilityConnections.Down; toBit = UtilityConnections.Up; }
            return fromBit != (UtilityConnections)0;
        }
    }
}
