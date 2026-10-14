using System;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        // Planned graph bits describe construction intent, never physical connectivity.
        private static string UtilityConnectionReadState(int cell, ObjectLayer[] layers)
        {
            if (!IsReadableMapCell(cell)) return "connectionState=unknown";
            foreach (var layer in layers)
            {
                var go = Grid.Objects[cell, (int)layer];
                if (go == null) continue;
                var graph = go.GetComponent<KAnimGraphTileVisualizer>();
                if (go.GetComponent<BuildingComplete>() != null || (graph != null && graph.isPhysicalBuilding))
                    return TryGetUtilityConnections(cell, layers, out _)
                        ? "connectionState=complete connectionSource=native_physical flowVerified=false"
                        : "connectionState=complete connectionSource=unknown physicalConnection=unknown flowVerified=false";
                if (graph == null || graph.ConnectionManager == null)
                    return "connectionState=planned plannedMask=? physicalConnection=not_built";
                try
                {
                    var bits = graph.ConnectionManager.GetConnections(cell, is_physical_building: false);
                    string dirs = ((bits & UtilityConnections.Up) != 0 ? "U" : "")
                        + ((bits & UtilityConnections.Down) != 0 ? "D" : "")
                        + ((bits & UtilityConnections.Left) != 0 ? "L" : "")
                        + ((bits & UtilityConnections.Right) != 0 ? "R" : "");
                    return "connectionState=planned plannedDirs=" + (dirs.Length == 0 ? "." : dirs)
                        + " plannedGlyph=" + GetConnectionGlyph((bits & UtilityConnections.Up) != 0,
                            (bits & UtilityConnections.Down) != 0, (bits & UtilityConnections.Left) != 0,
                            (bits & UtilityConnections.Right) != 0)
                        + " physicalConnection=not_built";
                }
                catch (Exception)
                {
                    return "connectionState=planned plannedMask=? physicalConnection=not_built";
                }
            }
            return "connectionState=absent";
        }
    }
}
