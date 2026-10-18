using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityEngine
{
    public sealed class GameObject
    {
        private readonly List<object> components = new List<object>();
        public void Add(object component) { components.Add(component); }
        public T GetComponent<T>() where T : class { return components.OfType<T>().FirstOrDefault(); }
        public T[] GetComponents<T>() where T : class { return components.OfType<T>().ToArray(); }
    }
}

public enum ObjectLayer { Building, Power, Liquid, Gas, Logic, Conveyor }
public enum ConduitType { Gas, Liquid }
public sealed class BuildingDef { public bool RequiresPowerInput; public bool RequiresPowerOutput; }
public sealed class Building
{
    public BuildingDef Def = new BuildingDef();
    public int PowerInputCell = Grid.InvalidCell;
    public int PowerOutputCell = Grid.InvalidCell;
    public int UtilityInputCell = Grid.InvalidCell;
    public int UtilityOutputCell = Grid.InvalidCell;
    public int GetPowerInputCell() { return PowerInputCell; }
    public int GetPowerOutputCell() { return PowerOutputCell; }
    public int GetUtilityInputCell() { return UtilityInputCell; }
    public int GetUtilityOutputCell() { return UtilityOutputCell; }
}
public sealed class ConduitConsumer { public ConduitType ConduitType; }
public sealed class ConduitDispenser { public ConduitType ConduitType; }
public sealed class SolidConduitConsumer { public bool IsConnected; }
public sealed class SolidConduitDispenser { public bool IsConnected; }
public sealed class LogicPorts
{
    public struct Port { public int id; public int ActualCell; public bool requiresConnection; }
    public Port[] inputPortInfo;
    public Port[] outputPortInfo;
}
public static class Grid
{
    public const int InvalidCell = -1;
    public static bool IsValidCell(int cell) { return cell >= 0 && cell < 10000; }
    public static int CellColumn(int cell) { return cell % 100; }
    public static int CellRow(int cell) { return cell / 100; }
}
public sealed class BridgeFixture { public int From; public int To; }

namespace OniMcp.Support
{
    // Localization is substituted only at its formatting boundary. Production catalog
    // loading and fallback have their own host suite; this suite tests the real adapter.
    public static class McpLocalization
    {
        public static string Format(string key, string fallback, params object[] args)
        { return key + ": " + string.Join(" | ", args.Select(arg => arg == null ? "<null>" : arg.ToString())); }
    }
}

namespace OniMcp.Tools
{
    internal static class LogicPortReadSemantics
    {
        internal static readonly HashSet<int> ConnectedCells = new HashSet<int>();
        // ActualCell is a native boundary: a fixture supplies the already transformed
        // cell, including rotations and reused native port IDs.
        internal static int ActualCell(LogicPorts ports, LogicPorts.Port port) { return port.ActualCell; }
        internal static bool TryBridgeRoute(UnityEngine.GameObject go, out int from, out int to)
        {
            var bridge = go.GetComponent<BridgeFixture>();
            from = bridge == null ? Grid.InvalidCell : bridge.From;
            to = bridge == null ? Grid.InvalidCell : bridge.To;
            return bridge != null;
        }
        internal static bool ConnectedAtCell(int cell) { return ConnectedCells.Contains(cell); }
    }

    public static partial class WorldEditorTools
    {
        internal static UnityEngine.GameObject FixtureBuilding;
        internal static readonly HashSet<string> FixtureLayers = new HashSet<string>();
        private static readonly ObjectLayer[] PowerLayers = { ObjectLayer.Power };
        private static readonly ObjectLayer[] LiquidLayers = { ObjectLayer.Liquid };
        private static readonly ObjectLayer[] GasLayers = { ObjectLayer.Gas };
        private static readonly ObjectLayer[] LogicLayers = { ObjectLayer.Logic };
        private static readonly ObjectLayer[] ConveyorLayers = { ObjectLayer.Conveyor };
        private static UnityEngine.GameObject CellBuildingObject(int cell) { return FixtureBuilding; }
        private static bool HasLayer(int cell, ObjectLayer[] layers)
        { return layers.Any(layer => FixtureLayers.Contains(cell + ":" + layer)); }
        private static string LocalZoomCall(int x, int y, string layers) { return "zoom(" + x + "," + y + ";" + layers + ")"; }
        private static string DecisionCellRef(int cell) { return "@(" + Grid.CellColumn(cell) + "," + Grid.CellRow(cell) + ")"; }
        internal static string RenderFixture(out bool wrote)
        {
            var text = new StringBuilder();
            wrote = AppendPortDecisionHint(text, 25, 25, 2525);
            return text.ToString();
        }
    }
}
