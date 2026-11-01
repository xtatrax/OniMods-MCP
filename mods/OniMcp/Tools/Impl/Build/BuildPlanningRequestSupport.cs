using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    // A typed dependency set belonging to one explicit map execution preflight.
    // It is neither a native foundation cache nor JSON-supplied authorization.
    internal sealed class BuildPlanningRequestSupportContext
    {
        private readonly int worldId;
        private readonly HashSet<int> cells = new HashSet<int>();

        internal BuildPlanningRequestSupportContext(int worldId) { this.worldId = worldId; }

        internal HashSet<int> CopyForWorld(int world)
            => world >= 0 && world == worldId ? new HashSet<int>(cells) : new HashSet<int>();

        internal void Include(int world, int cell)
        {
            if (world >= 0 && world == worldId) cells.Add(cell);
        }
    }

    public static partial class BuildPlanningTools
    {
        internal static bool IsRequestSupportPrefab(string prefabId)
            => IsRequestSupportDefinition(Assets.GetBuildingDef(prefabId));

        private static bool IsRequestSupportDefinition(BuildingDef def)
        {
            return def != null && IsSupportPrefab(def.PrefabID) && def.IsFoundation
                && def.WidthInCells == 1 && def.HeightInCells == 1
                && def.TileLayer == ObjectLayer.FoundationTile
                && def.BuildLocationRule.ToString() == "Tile"
                && def.PlacementOffsets != null && def.PlacementOffsets.Length == 1
                && def.PlacementOffsets[0].x == 0 && def.PlacementOffsets[0].y == 0;
        }

        private static bool TryRequestSupportCell(BuildingDef def, int x, int y, int world,
            Orientation orientation, out int cell)
        {
            cell = Grid.InvalidCell;
            if (world < 0 || !IsRequestSupportDefinition(def)) return false;
            var shape = NativeBuildingFootprint.FromLowerLeft(def, x, y, orientation);
            if (!shape.Available || shape.Cells.Count != 1 || !shape.Cells[0].Cell.HasValue) return false;
            int candidate = shape.Cells[0].Cell.Value;
            if (!Grid.IsWorldValidCell(candidate) || !Grid.IsVisible(candidate)
                || Grid.WorldIdx[candidate] != world) return false;
            cell = candidate;
            return true;
        }
    }
}
