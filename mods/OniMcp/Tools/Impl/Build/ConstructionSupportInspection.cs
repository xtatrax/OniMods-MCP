using System.Collections.Generic;

namespace OniMcp.Tools
{
    // Read-only support coordinates; no hypothetical changes to native state.
    internal static class ConstructionSupportInspection
    {
        internal static bool Observable(int cell, int worldId) => Grid.IsValidCell(cell)
            && Grid.IsVisible(cell) && ToolUtil.CellMatchesWorld(cell, worldId);

        internal static int Offset(int origin, CellOffset offset)
        {
            if (!Grid.IsValidCell(origin)) return Grid.InvalidCell;
            var p = NativeBuildingFootprint.AbsoluteCell(Grid.CellColumn(origin) + offset.x, Grid.CellRow(origin) + offset.y);
            return p.Valid && p.Cell.HasValue ? p.Cell.Value : Grid.InvalidCell;
        }

        internal static Dictionary<string, object> Cell(int cell) => new Dictionary<string, object>
        {
            ["cell"] = cell, ["x"] = Grid.CellColumn(cell), ["y"] = Grid.CellRow(cell)
        };

    }
}
