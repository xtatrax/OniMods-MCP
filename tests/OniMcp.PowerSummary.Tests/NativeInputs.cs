using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Inputs only: no aggregation, eligibility, power simulation or production rules.
namespace UnityEngine
{
    internal sealed class GameObject
    {
        internal readonly Dictionary<Type, object> ExistingComponents = new Dictionary<Type, object>();
        internal int Cell { get; set; }
        internal int World { get; set; }
        internal int Identity { get; set; }
        internal string Name { get; set; }
        internal string name { get { return Name; } }
        internal T GetComponent<T>() where T : class
        { object value; return ExistingComponents.TryGetValue(typeof(T), out value) ? value as T : null; }
        internal int GetInstanceID() { return Identity; }
        internal string GetProperName() { return Name; }
    }
}
internal sealed class Game { internal static Game Instance { get; set; } }
internal sealed class ClusterManager
{ internal static ClusterManager Instance { get; set; } internal int activeWorldId { get; set; } }
internal sealed class Registry<T> { internal readonly List<T> Items = new List<T>(); }
internal static class Components
{
    internal static readonly Registry<Battery> Batteries = new Registry<Battery>();
    internal static readonly Registry<Generator> Generators = new Registry<Generator>();
    internal static readonly Registry<EnergyConsumer> EnergyConsumers = new Registry<EnergyConsumer>();
}
internal sealed class Operational
{
    internal bool Value { get; set; }
    internal int Reads { get; private set; }
    internal bool IsOperational { get { Reads++; return Value; } }
    internal bool IsActive { get { throw new InvalidOperationException("IsActive is not the selected predicate"); } }
}
internal sealed class Battery
{
    internal GameObject gameObject { get; set; }
    internal ushort CircuitID { get; set; }
    internal float Capacity { get; set; }
    internal float JoulesAvailable { get; set; }
    internal float WattsUsed { get; set; }
}
internal sealed class Generator
{
    internal GameObject gameObject { get; set; }
    internal ushort CircuitID { get; set; }
    internal float Capacity { get; set; }
    internal float JoulesAvailable { get; set; }
    internal float WattageRating { get; set; }
    internal bool IsEmpty { get; set; }
    internal bool IsProducingPower() { throw new InvalidOperationException("Production predicate is outside this count"); }
}
internal sealed class EnergyConsumer
{
    internal GameObject gameObject { get; set; }
    internal ushort CircuitID { get; set; }
    internal float BaseWattageRating { get; set; }
    internal float WattsNeededWhenActive { get; set; }
    internal float WattsUsed { get; set; }
    internal bool IsPowered { get; set; }
}
internal sealed class KPrefabID { internal int InstanceID { get; set; } internal Tag PrefabTag { get; set; } }
internal sealed class Tag { internal string Name { get; set; } }
internal sealed class Building { internal BuildingDef Def { get; set; } }
internal sealed class BuildingDef
{
    internal string PrefabID { get; set; }
    internal bool RequiresPowerInput { get; set; }
    internal bool RequiresPowerOutput { get; set; }
}
internal static class Grid
{
    internal const int InvalidCell = -1;
    internal static readonly Dictionary<int, int> WorldIdx = new Dictionary<int, int>();
    internal static bool IsValidCell(int cell) { return cell >= 0 && cell < 10000; }
    internal static bool IsWorldValidCell(int cell) { return WorldIdx.ContainsKey(cell); }
    internal static int PosToCell(GameObject go) { return go.Cell; }
    internal static void CellToXY(int cell, out int x, out int y) { x = cell % 100; y = cell / 100; }
}
namespace OniMcp.Tools
{
    internal static class ToolUtil
    {
        internal static int? GetInt(JObject args, string name) { return args[name]?.Value<int?>(); }
        internal static bool GetBool(JObject args, string name, bool fallback)
        { bool value; return args[name] != null && bool.TryParse(args[name].ToString(), out value) ? value : fallback; }
        internal static int ClampLimit(JObject args, int fallback, int maximum)
        { return Math.Max(1, Math.Min(maximum, GetInt(args, "limit") ?? fallback)); }
        internal static bool GameObjectMatchesWorld(GameObject go, int world) { return world < 0 || go.World == world; }
        internal static float SafeFloat(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0 : value; }
        internal static string CleanName(string value) { return value; }
    }
    public static partial class PowerAndRoomTools
    {
        // Unrelated diagnostic identity/port boundary; aggregate decisions are real source.
        private static Dictionary<string, object> TargetInfo(GameObject go)
        { return new Dictionary<string, object> { ["id"] = go.Identity, ["cell"] = go.Cell }; }
        private static Dictionary<string, object> BuildingPowerPortInfo(GameObject go, BuildingDef def, bool input, bool output)
        { throw new InvalidOperationException("Native port geometry is not part of this count fixture"); }
    }
}
