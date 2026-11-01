using UnityEngine;
namespace OniMcp.Tools
{
    // Unchanged endpoint helper boundary only: caller's endpoint/family projection is actual source.
    internal static class LogicPortReadSemantics
    {
        internal static GameObject BuildingAtCell(int cell) => Grid.IsValidCell(cell) ? Grid.Objects[cell,(int)ObjectLayer.Building] ?? Grid.Objects[cell,(int)ObjectLayer.LogicGate] : null;
        internal static int ActualCell(LogicPorts ports,LogicPorts.Port port) => port.NativeCell;
        internal static bool TryBridgeRoute(GameObject go,out int from,out int to)
        { var route=go?.GetComponent<NativeLogicRoute>();from=route?.From ?? Grid.InvalidCell;to=route?.To ?? Grid.InvalidCell;return route?.Registered ?? false; }
    }
}
internal static class UtilitySummaryBoundary
{
    internal static int ForbiddenCalls;
    internal static Exception Forbidden(string action) { ForbiddenCalls++; return new InvalidOperationException("Unexpected utility summary native mutation: "+action); }
}
public static partial class Grid
{
    public const int InvalidCell=-1;
    public static int PosToCell(GameObject go) => go.NativeCell;
}
public sealed partial class Game { public TestCircuitManager circuitManager; }
public sealed class TestCircuitManager
{
    public readonly Dictionary<int,ushort> Values=new();
    public Func<int,ushort> Reader;
    public readonly List<int> Reads=new();
    public ushort GetCircuitID(int cell) { Reads.Add(cell);return Reader != null ? Reader(cell) : Values.TryGetValue(cell,out ushort v) ? v : ushort.MaxValue; }
}
namespace UnityEngine
{
    public sealed partial class GameObject
    {
        public int NativeCell;
        public readonly Dictionary<Type,Array> MultipleComponents=new();
        public T[] GetComponents<T>() where T:class => MultipleComponents.TryGetValue(typeof(T),out Array values) ? (T[])values : GetComponent<T>() is T one ? new[]{one} : Array.Empty<T>();
        public void AddComponent() => throw UtilitySummaryBoundary.Forbidden("AddComponent");
        public void Refresh() => throw UtilitySummaryBoundary.Forbidden("Refresh");
    }
}
public sealed class Constructable { }
public sealed class BuildingDef { public bool RequiresPowerInput,RequiresPowerOutput; }
public sealed class Building
{
    public BuildingDef Def=new();
    public int PowerInput=Grid.InvalidCell,PowerOutput=Grid.InvalidCell,UtilityInput=Grid.InvalidCell,UtilityOutput=Grid.InvalidCell;
    public int GetPowerInputCell()=>PowerInput;
    public int GetPowerOutputCell()=>PowerOutput;
    public int GetUtilityInputCell()=>UtilityInput;
    public int GetUtilityOutputCell()=>UtilityOutput;
}
public sealed class ConduitConsumer { public ConduitType ConduitType; }
public sealed class ConduitDispenser { public ConduitType ConduitType; }
public sealed class SolidConduitConsumer { }
public sealed class SolidConduitDispenser { }
public sealed class LogicPorts
{
    public struct Port { public int NativeCell; }
    public Port[] inputPortInfo,outputPortInfo;
}
public sealed class NativeLogicRoute { public int From,To;public bool Registered; }
