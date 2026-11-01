using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static bool TryGetUtilityConnections(int cell, ObjectLayer[] layers, out UtilityConnections connections)
        {
            connections = (UtilityConnections)0;
            if (!IsReadableMapCell(cell)) return false;
            foreach (var layer in layers)
            {
                var go = Grid.Objects[cell, (int)layer];
                if (go == null) continue;
                var graph = go.GetComponent<KAnimGraphTileVisualizer>();
                if (go.GetComponent<BuildingComplete>() == null && (graph == null || !graph.isPhysicalBuilding))
                    continue;
                try
                {
                    if (!NativeReadProviderPolicy.TryGetUtilityManager(graph, go, layer,
                        out IUtilityNetworkMgr manager, out _)) return false;
                    connections = manager.GetConnections(cell, is_physical_building: true);
                    return true;
                }
                catch { return false; }
            }
            return false;
        }
    }
}
