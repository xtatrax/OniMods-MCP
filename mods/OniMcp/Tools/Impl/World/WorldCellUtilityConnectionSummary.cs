using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldAnalysisTools
    {
        private static Dictionary<string, object> CellUtilityConnectionSummary(int cell)
        {
            return new Dictionary<string, object>
            {
                ["power"] = UtilityNetworkSummary(cell, "power", PowerUtilityLayers(), power: true),
                ["liquid"] = UtilityNetworkSummary(cell, "liquid", LiquidUtilityLayers(), power: false),
                ["gas"] = UtilityNetworkSummary(cell, "gas", GasUtilityLayers(), power: false),
                ["logic"] = UtilityNetworkSummary(cell, "logic", LogicUtilityLayers(), power: false),
                ["rail"] = UtilityNetworkSummary(cell, "rail", RailUtilityLayers(), power: false)
            };
        }

        private static Dictionary<string, object> UtilityNetworkSummary(int cell, string kind, ObjectLayer[] layers, bool power)
        {
            var observed = WorldUtilityPhysicalConnectionRead.Read(cell, layers);
            bool known = observed.Mask.HasValue;
            bool absent = observed.State == "absent";
            var dirs = known ? UtilityMaskDirs(cell, observed.Mask.Value) : new List<KeyValuePair<string, int>>();
            string emptyOrUnknown = absent ? "." : "?";
            var result = new Dictionary<string, object>
            {
                ["hasLayer"] = UtilityObservedValue(observed.HasLayer),
                ["hasPhysical"] = UtilityObservedValue(observed.HasPhysical),
                ["hasPlanned"] = UtilityObservedValue(observed.HasPlanned),
                ["connectionState"] = observed.State,
                ["connectionSource"] = UtilityObservedValue(observed.Source),
                ["reasonCode"] = UtilityObservedValue(observed.ReasonCode),
                ["nativePhysicalMask"] = UtilityObservedValue(known ? (object)(int)observed.Mask.Value : null),
                ["glyph"] = known ? UtilityGlyph(dirs).ToString() : emptyOrUnknown,
                ["dirs"] = known ? dirs.Count == 0 ? "." : string.Join("", dirs.Select(item => item.Key).ToArray()) : emptyOrUnknown,
                ["links"] = known ? UtilityLinkText(dirs) : emptyOrUnknown,
                ["open"] = known ? UtilityOpenText(cell, layers, dirs) : emptyOrUnknown,
                ["to"] = known ? dirs.Count == 0 ? "." : string.Join(",", dirs.Select(item => CellCoordText(item.Value)).ToArray()) : emptyOrUnknown
            };

            if (power)
                result["circuitId"] = UtilityPowerCircuitId(cell);

            AddUtilityEndpointSummary(result, cell, kind);
            return result;
        }

        private static object UtilityObservedValue(object value) => value ?? Newtonsoft.Json.Linq.JValue.CreateNull();

        private static void AddUtilityEndpointSummary(Dictionary<string, object> result, int cell, string kind)
        {
            var go = LogicPortReadSemantics.BuildingAtCell(cell);
            var building = go != null ? go.GetComponent<Building>() : null;
            if (building == null || building.Def == null)
                return;

            var endpoints = new Dictionary<string, object>();
            if (kind == "power")
            {
                if (building.Def.RequiresPowerInput)
                    endpoints["input"] = PortCell("⊗", building.GetPowerInputCell());
                if (building.Def.RequiresPowerOutput)
                    endpoints["output"] = PortCell("⊙", building.GetPowerOutputCell());
            }
            else if (kind == "logic")
            {
                var ports = go.GetComponent<LogicPorts>();
                if (LogicPortReadSemantics.TryBridgeRoute(go, out int from, out int to))
                {
                    endpoints["input"] = PortCell("⊗", from);
                    endpoints["output"] = PortCell("⊙", to);
                }
                else
                {
                    AddLogicEndpoint(endpoints, ports, "input", true, ports?.inputPortInfo);
                    AddLogicEndpoint(endpoints, ports, "output", false, ports?.outputPortInfo);
                }
            }
            else if (kind == "rail")
            {
                if (go.GetComponents<SolidConduitConsumer>().Any())
                    endpoints["input"] = PortCell("⊗", building.GetUtilityInputCell());
                if (go.GetComponents<SolidConduitDispenser>().Any())
                    endpoints["output"] = PortCell("⊙", building.GetUtilityOutputCell());
            }
            else
            {
                ConduitType type = kind == "gas" ? ConduitType.Gas : ConduitType.Liquid;
                if (go.GetComponents<ConduitConsumer>().Any(item => item.ConduitType == type))
                    endpoints["input"] = PortCell("⊗", building.GetUtilityInputCell());
                if (go.GetComponents<ConduitDispenser>().Any(item => item.ConduitType == type))
                    endpoints["output"] = PortCell("⊙", building.GetUtilityOutputCell());
            }

            if (endpoints.Count > 0)
                result["endpoints"] = endpoints;
            string bridge = UtilityBridgeRoute(go, building, kind);
            if (!string.IsNullOrEmpty(bridge))
                result["bridge"] = bridge;
        }

        private static void AddLogicEndpoint(Dictionary<string, object> endpoints, LogicPorts ports, string key, bool input, LogicPorts.Port[] info)
        {
            if (ports == null || info == null || info.Length == 0)
                return;
            int cell = Grid.InvalidCell;
            foreach (var port in info)
            {
                cell = LogicPortReadSemantics.ActualCell(ports, port);
                if (Grid.IsValidCell(cell))
                    break;
            }
            if (Grid.IsValidCell(cell))
                endpoints[key] = PortCell(input ? "⊗" : "⊙", cell, info.Length);
        }

        private static Dictionary<string, object> PortCell(string symbol, int cell, int count = 1)
        {
            var result = new Dictionary<string, object>
            {
                ["symbol"] = symbol,
                ["cell"] = cell,
                ["coord"] = CellCoordText(cell)
            };
            if (count > 1)
                result["count"] = count;
            return result;
        }

        private static string UtilityBridgeRoute(GameObject go, Building building, string kind)
        {
            if (go == null || building == null || go.name.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) < 0)
                return string.Empty;
            int input = Grid.InvalidCell;
            int output = Grid.InvalidCell;
            if (kind == "power")
            {
                input = building.GetPowerInputCell();
                output = building.GetPowerOutputCell();
            }
            else if (kind == "logic")
            {
                if (!LogicPortReadSemantics.TryBridgeRoute(go, out input, out output))
                    return string.Empty;
            }
            else
            {
                input = building.GetUtilityInputCell();
                output = building.GetUtilityOutputCell();
            }
            return Grid.IsValidCell(input) && Grid.IsValidCell(output)
                ? "from:" + CellCoordText(input) + " via:" + CellCoordText(Grid.PosToCell(go))
                    + "⌒ to:" + CellCoordText(output)
                : "⌒";
        }

        private static int FirstLogicCell(LogicPorts ports, LogicPorts.Port[] info)
        {
            if (ports == null || info == null)
                return Grid.InvalidCell;
            foreach (var port in info)
            {
                int cell = LogicPortReadSemantics.ActualCell(ports, port);
                if (Grid.IsValidCell(cell))
                    return cell;
            }
            return Grid.InvalidCell;
        }

        private static List<KeyValuePair<string, int>> UtilityMaskDirs(int cell, UtilityConnections mask)
        {
            var result = new List<KeyValuePair<string, int>>();
            AddUtilityMaskDir(result, cell, "U", 0, 1, (mask & UtilityConnections.Up) != 0);
            AddUtilityMaskDir(result, cell, "D", 0, -1, (mask & UtilityConnections.Down) != 0);
            AddUtilityMaskDir(result, cell, "L", -1, 0, (mask & UtilityConnections.Left) != 0);
            AddUtilityMaskDir(result, cell, "R", 1, 0, (mask & UtilityConnections.Right) != 0);
            return result;
        }

        private static void AddUtilityMaskDir(List<KeyValuePair<string, int>> result, int cell, string dir, int dx, int dy, bool connected)
        {
            if (connected)
                result.Add(new KeyValuePair<string, int>(dir, NeighborCell(cell, dx, dy)));
        }

        private static string UtilityOpenText(int cell, ObjectLayer[] layers, List<KeyValuePair<string, int>> dirs)
        {
            var linked = new HashSet<string>(dirs.Select(item => item.Key));
            var open = new List<KeyValuePair<string, int>>();
            AddOpenUtility(open, linked, cell, "U", 0, 1, layers);
            AddOpenUtility(open, linked, cell, "D", 0, -1, layers);
            AddOpenUtility(open, linked, cell, "L", -1, 0, layers);
            AddOpenUtility(open, linked, cell, "R", 1, 0, layers);
            return UtilityLinkText(open);
        }

        private static void AddOpenUtility(List<KeyValuePair<string, int>> open, HashSet<string> linked, int cell, string dir, int dx, int dy, ObjectLayer[] layers)
        {
            if (linked.Contains(dir))
                return;
            int neighbor = NeighborCell(cell, dx, dy);
            if (Grid.IsValidCell(neighbor) && HasAnyUtilityLayer(neighbor, layers))
                open.Add(new KeyValuePair<string, int>(dir, neighbor));
        }

        private static string UtilityLinkText(List<KeyValuePair<string, int>> dirs)
        {
            return dirs.Count == 0
                ? "."
                : string.Join(",", dirs.Select(item => item.Key + ":" + CellCoordText(item.Value)).ToArray());
        }

        private static char UtilityGlyph(List<KeyValuePair<string, int>> dirs)
        {
            bool up = dirs.Any(item => item.Key == "U");
            bool down = dirs.Any(item => item.Key == "D");
            bool left = dirs.Any(item => item.Key == "L");
            bool right = dirs.Any(item => item.Key == "R");
            int value = (up ? 8 : 0) | (down ? 4 : 0) | (left ? 2 : 0) | (right ? 1 : 0);
            switch (value)
            {
                case 3: return '─';
                case 5: return '┌';
                case 6: return '┐';
                case 7: return '┬';
                case 9: return '└';
                case 10: return '┘';
                case 11: return '┴';
                case 12: return '│';
                case 13: return '├';
                case 14: return '┤';
                case 15: return '┼';
                default: return '*';
            }
        }

        private static bool HasAnyUtilityLayer(int cell, ObjectLayer[] layers)
        {
            if (!Grid.IsValidCell(cell))
                return false;
            return layers.Any(layer => Grid.Objects[cell, (int)layer] != null);
        }

        private static ushort UtilityPowerCircuitId(int cell)
        {
            try
            {
                return Grid.IsValidCell(cell) && Game.Instance?.circuitManager != null
                    ? Game.Instance.circuitManager.GetCircuitID(cell)
                    : ushort.MaxValue;
            }
            catch
            {
                return ushort.MaxValue;
            }
        }

        private static int NeighborCell(int cell, int dx, int dy)
        {
            if (!Grid.IsValidCell(cell)) return Grid.InvalidCell;
            int x = Grid.CellColumn(cell) + dx, y = Grid.CellRow(cell) + dy;
            return x >= 0 && x < Grid.WidthInCells && y >= 0 && y < Grid.HeightInCells
                ? Grid.XYToCell(x, y) : Grid.InvalidCell;
        }

        private static string CellCoordText(int cell)
        {
            return Grid.IsValidCell(cell)
                ? "(" + Grid.CellColumn(cell) + "," + Grid.CellRow(cell) + ")"
                : "invalid";
        }

        private static ObjectLayer[] PowerUtilityLayers() => new[] { ObjectLayer.Wire, ObjectLayer.WireTile, ObjectLayer.ReplacementWire };
        private static ObjectLayer[] LiquidUtilityLayers() => new[] { ObjectLayer.LiquidConduit, ObjectLayer.LiquidConduitTile, ObjectLayer.ReplacementLiquidConduit };
        private static ObjectLayer[] GasUtilityLayers() => new[] { ObjectLayer.GasConduit, ObjectLayer.GasConduitTile, ObjectLayer.ReplacementGasConduit };
        private static ObjectLayer[] LogicUtilityLayers() => new[] { ObjectLayer.LogicWire, ObjectLayer.LogicWireTile, ObjectLayer.ReplacementLogicWire };
        private static ObjectLayer[] RailUtilityLayers() => new[] { ObjectLayer.SolidConduit, ObjectLayer.SolidConduitTile, ObjectLayer.ReplacementSolidConduit };
    }
}
