using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        // Native utility placement can queue digs even though utility footprints ignore solids.
        // Reject the entire route before placement unless allowUtilityTerrainDig is explicit.
        // confirm alone is insufficient; consent does not imply evaluated support safety.
        private static Dictionary<string, object> UtilityTerrainConflict(
            BuildingDef def, IEnumerable<int> cells, int worldId, bool allowTerrainDig = false,
            int maxDigCells = 100, bool autoDigEnabled = true)
        {
            if (def == null || !IsUtilityPrefab(def.PrefabID))
                return null;
            var blocked = new List<Dictionary<string, object>>();
            foreach (int cell in cells.Distinct())
            {
                if (!Grid.IsValidCell(cell) || !Grid.IsVisible(cell)
                    || !ToolUtil.CellMatchesWorld(cell, worldId)
                    || !Grid.Solid[cell] || Grid.Foundation[cell])
                    continue; // Visibility/world validation remains the placement validator's job.
                var visualizer = Game.Instance == null ? null : Game.Instance.GetComponent<EntombedItemVisualizer>();
                blocked.Add(new Dictionary<string, object>
                {
                    ["cell"] = cell, ["x"] = Grid.CellColumn(cell), ["y"] = Grid.CellRow(cell),
                    ["buriedMarker"] = visualizer == null ? "unavailable"
                        : visualizer.IsEntombedItem(cell) ? "present" : "absent",
                    ["alreadyMarkedForDig"] = Grid.Objects[cell, (int)ObjectLayer.DigPlacer] != null
                });
            }
            if (blocked.Count == 0) return null;
            if (allowTerrainDig && autoDigEnabled && blocked.Count <= maxDigCells)
                return new Dictionary<string, object>
                {
                    ["allowed"] = true, ["explicitTerrainDig"] = true,
                    ["naturalCellCount"] = blocked.Count, ["naturalCells"] = blocked,
                    ["supportAnalysis"] = "not_evaluated",
                    ["terrainPolicy"] = "explicit_utility_dig_cell_placement"
                };
            return new Dictionary<string, object>
            {
                ["allowed"] = false,
                ["explicitTerrainDig"] = allowTerrainDig,
                ["maxAutoDigCells"] = maxDigCells,
                ["autoDigEnabled"] = autoDigEnabled,
                ["reasonCode"] = "utility_natural_terrain_blocked",
                ["error"] = "Utility placement may queue native digging in natural terrain; route rejected before placement. Inspect supports and markers, then reroute, dig separately, or explicitly set allowUtilityTerrainDig=true with autoDigObstructions enabled and a sufficient maxAutoDigCells budget.",
                ["prefabId"] = def.PrefabID, ["worldId"] = worldId,
                ["success"] = false, ["valid"] = false, ["committed"] = false,
                ["planned"] = 0, ["failed"] = 1, ["autoDigQueued"] = 0,
                ["blockedCellCount"] = blocked.Count, ["blockedCells"] = blocked,
                ["supportAnalysis"] = "not_evaluated",
                ["terrainPolicy"] = "reject_natural_solids_before_utility_placement"
            };
        }
    }
}
