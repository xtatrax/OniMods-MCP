using System.Collections.Generic;

namespace OniMcp.Tools
{
    // Coordinates are relative to the native reference cell, never the lower-left anchor.
    internal static class ConstructionSupportGeometry
    {
        internal static bool IsBaseRule(string rule) => rule == "OnFloor" || rule == "OnCeiling"
            || rule == "OnFoundationRotatable";

        internal static bool IsKnownRule(string rule) => IsBaseRule(rule) || rule == "OnWall"
            || rule == "WallFloor" || rule == "InCorner" || rule == "OnBackWall";

        internal static IEnumerable<CellOffset> Offsets(string rule, int width, int height, Orientation orientation)
        {
            if (IsBaseRule(rule) || rule == "WallFloor" || rule == "InCorner")
            {
                int y = rule == "OnCeiling" || rule == "InCorner" ? height : -1;
                for (int i = 0; i < width; i++)
                    yield return Rotatable.GetRotatedCellOffset(new CellOffset(BuildingDef.GetXOffset(width) + i, y), orientation);
            }
            if (rule == "OnWall" || rule == "WallFloor" || rule == "InCorner")
            {
                // Native wall checks select a side via FlipH; they do not rotate this strip.
                int x = orientation == Orientation.FlipH ? width / 2 + 1 : BuildingDef.GetXOffset(width) - 1;
                for (int y = 0; y < height; y++) yield return new CellOffset(x, y);
            }
            if (rule == "OnBackWall")
                for (int y = 0; y < height; y++)
                    for (int i = 0; i < width; i++)
                        yield return Rotatable.GetRotatedCellOffset(new CellOffset(BuildingDef.GetXOffset(width) + i, y), orientation);
        }
    }
}
