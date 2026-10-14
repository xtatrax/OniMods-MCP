using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static bool TryGetUtilityConnections(int cell, ObjectLayer[] layers, out UtilityConnections connections)
        {
            connections = (UtilityConnections)0;
            if (!IsReadableMapCell(cell))
                return false;
            foreach (var layer in layers)
            {
                var go = Grid.Objects[cell, (int)layer];
                if (go == null)
                    continue;

                var provider = UtilityNetworkProvider(go);
                if (provider == null)
                    continue;

                var manager = provider.GetNetworkManager();
                if (manager == null)
                    continue;

                connections = manager.GetConnections(cell, is_physical_building: true);
                return true;
            }

            return false;
        }

        private static IHaveUtilityNetworkMgr UtilityNetworkProvider(GameObject go)
        {
            if (go == null)
                return null;
            var visualizer = go.GetComponent<KAnimGraphTileVisualizer>();
            if (go.GetComponent<BuildingComplete>() == null
                && (visualizer == null || !visualizer.isPhysicalBuilding))
                return null;
            return go.GetComponent<IHaveUtilityNetworkMgr>();
        }

    }
}
