using System;
using UnityEngine;

namespace OniMcp.Tools
{
    // Physical port locations only. A blueprint or an unconnected bridge does not
    // establish network connectivity merely because these coordinates are known.
    internal sealed class NativeBridgeRead
    {
        internal string Kind, PrefabId, Source;
        internal int From = Grid.InvalidCell, Via = Grid.InvalidCell, To = Grid.InvalidCell;
        internal bool Planned;
        internal bool Available => Grid.IsValidCell(From) && Grid.IsValidCell(To) && From != To;

        internal static NativeBridgeRead Read(GameObject go)
        {
            var building = go == null ? null : go.GetComponent<Building>();
            var def = building?.Def;
            if (def == null) return null;
            bool planned = go.GetComponent<Constructable>() != null;
            // Planned instances may contain visualization-only components. Use the
            // complete prefab offsets and the placed blueprint's actual orientation.
            var source = planned ? def.BuildingComplete : go;
            var wire = source == null ? null : source.GetComponent<WireUtilityNetworkLink>();
            var logic = source == null ? null : source.GetComponent<LogicUtilityNetworkLink>();
            UtilityNetworkLink link = wire != null ? (UtilityNetworkLink)wire : logic;
            string kind = wire != null ? "power" : logic != null ? "logic" : null;
            if (kind == null && def.PrefabID.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (def.ObjectLayer == ObjectLayer.WireConnectors) kind = "power";
                else if (def.BuildLocationRule == BuildLocationRule.LogicBridge) kind = "logic";
                else if (def.InputConduitType == def.OutputConduitType)
                    kind = def.InputConduitType == ConduitType.Liquid ? "liquid"
                        : def.InputConduitType == ConduitType.Gas ? "gas"
                        : def.InputConduitType == ConduitType.Solid ? "solid" : null;
            }
            if (kind == null) return null;
            var result = new NativeBridgeRead
            {
                Kind = kind, PrefabId = def.PrefabID, Planned = planned,
                Via = Grid.PosToCell(go), Source = "unavailable"
            };
            if (!Grid.IsValidCell(result.Via)) return result;
            CellOffset from, to;
            if (link != null)
            {
                from = link.link1; to = link.link2;
                result.Source = planned ? "complete_prefab.UtilityNetworkLink" : "instance.UtilityNetworkLink";
            }
            else if (kind == "liquid" || kind == "gas" || kind == "solid")
            {
                from = def.UtilityInputOffset; to = def.UtilityOutputOffset;
                result.Source = "BuildingDef.UtilityOffsets";
            }
            else return result;
            result.From = OffsetCell(result.Via, from, building.Orientation);
            result.To = OffsetCell(result.Via, to, building.Orientation);
            return result;
        }

        private static int OffsetCell(int origin, CellOffset offset, Orientation orientation)
        {
            var rotated = Rotatable.GetRotatedCellOffset(offset, orientation);
            int x = Grid.CellColumn(origin) + rotated.x;
            int y = Grid.CellRow(origin) + rotated.y;
            // Do not allow linear cell offsets to wrap across a map edge.
            if (x < 0 || y < 0 || x >= Grid.WidthInCells || y >= Grid.HeightInCells)
                return Grid.InvalidCell;
            int cell = Grid.XYToCell(x, y);
            return Grid.IsValidCell(cell) && Grid.WorldIdx[cell] == Grid.WorldIdx[origin]
                ? cell : Grid.InvalidCell;
        }
    }
}
