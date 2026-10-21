using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// Native boundaries only. All snapshot projection, needs reader, aggregation,
// alert/watch/delta and localization implementations are linked source.
namespace UnityEngine
{
    public struct Vector3 { public float x, y; }
    public sealed class Transform { public Vector3 Position; public Vector3 GetPosition() { return Position; } }
    public sealed class GameObject { public string Name = "sample"; public string GetProperName() { return Name; } }
    public static class Time { public static float timeScale; }
}
internal static class Grid
{
    internal static int[] WorldIdx { get; } = new int[1000];
    internal static bool IsValidCell(int cell) { return cell >= 0 && cell < WorldIdx.Length; }
    internal static bool IsWorldValidCell(int cell) { return IsValidCell(cell); }
    internal static int CellColumn(int cell) { return cell % 100; }
    internal static int CellRow(int cell) { return cell / 100; }
    internal static int PosToCell(MinionIdentity dupe) { return dupe.Cell; }
    internal static int PosToCell(Edible edible) { return edible.Cell; }
}
internal sealed class ClusterManager
{
    internal static ClusterManager Instance { get; set; }
    internal int activeWorldId { get; set; }
    internal int worldCount { get; set; }
    internal WorldContainer GetWorld(int id) { return null; }
}
internal sealed class WorldContainer { internal AlertManager AlertManager { get; set; } }
internal sealed class AlertManager
{
    internal bool IsRedAlertToggledOn() { return false; }
    internal bool IsRedAlert() { return false; }
    internal bool IsYellowAlert() { return false; }
    internal bool IsOn() { return false; }
}
internal static class Components
{
    internal static MinionCollection LiveMinionIdentities { get; } = new MinionCollection();
    internal static EdibleCollection Edibles { get; } = new EdibleCollection();
    internal static BuildingCollection BuildingCompletes { get; } = new BuildingCollection();
}
internal sealed class MinionCollection
{
    internal int Count { get; set; }
    internal List<MinionIdentity> Items { get; } = new List<MinionIdentity>();
}
internal sealed class EdibleCollection { internal List<Edible> Items { get; } = new List<Edible>(); }
internal sealed class BuildingCollection { internal List<BuildingComplete> Items { get; } = new List<BuildingComplete>(); }
internal sealed class BuildingComplete
{
    internal BuildingDef Def = null;
    internal string name = null;
    internal readonly UnityEngine.Transform transform = new UnityEngine.Transform();
    internal int GetMyWorldId() { return 0; }
}
internal sealed class BuildingDef { internal string PrefabID = null; }
internal sealed class Edible
{
    internal readonly UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    internal string name = null;
    internal int Cell = 0;
    internal float Calories = 0;
    internal T GetComponent<T>() where T : class { return null; }
    internal int GetMyWorldId() { return 0; }
    internal int GetQuality() { return 0; }
    internal string GetProperName() { return name; }
}
internal sealed class Pickupable { }
internal struct Tag { internal string Name { get; set; } }
internal sealed class KPrefabID { internal int InstanceID; internal Tag PrefabTag = default(Tag); }
internal sealed class MinionResume { internal int AvailableSkillpoints; }
internal sealed class MinionIdentity
{
    internal string Name;
    internal int WorldId, Cell, InvalidAmountComponentReads;
    internal readonly Dictionary<Type,object> Components = new Dictionary<Type,object>();
    internal readonly Dictionary<Klei.AI.Amount,Klei.AI.AmountInstance> NativeAmounts = new Dictionary<Klei.AI.Amount,Klei.AI.AmountInstance>();
    internal T GetComponent<T>() where T : class
    {
        if (typeof(T) == typeof(Klei.AI.Amounts)) { InvalidAmountComponentReads++; return null; }
        object value; return Components.TryGetValue(typeof(T), out value) ? value as T : null;
    }
    internal int GetMyWorldId() { return WorldId; }
    internal string GetProperName() { return Name; }
}
namespace Klei.AI
{
    internal enum Units { Percentage, Calories }
    internal sealed class Amount
    {
        internal string Id, Name;
        private Units storedUnits;
        internal Units units { get { if (ThrowUnits) throw new InvalidOperationException("native units fixture"); return storedUnits; } set { storedUnits=value; } }
        internal bool ThrowLookup, ThrowUnits;
        internal int Lookups;
        internal AmountInstance Lookup(MinionIdentity dupe)
        {
            Lookups++;
            if (ThrowLookup) throw new InvalidOperationException("native amount lookup fixture");
            AmountInstance instance;
            return dupe != null && dupe.NativeAmounts.TryGetValue(this, out instance) ? instance : null;
        }
    }
    internal sealed class AmountInstance
    {
        private float stored;
        internal bool ThrowValue;
        internal int Reads, Writes;
        internal Amount amount;
        internal float value { get { Reads++; if (ThrowValue) throw new InvalidOperationException("native value fixture"); return stored; } set { stored=value; Writes++; } }
    }
    internal sealed class Amounts
    {
        internal readonly List<AmountInstance> ModifierList = new List<AmountInstance>();
        internal AmountInstance Get(Amount definition) { throw new InvalidOperationException("component Amounts reader must not be used"); }
    }
}
namespace Database
{
    internal sealed class Amounts { internal Klei.AI.Amount Stress, Stamina, Calories; }
}
internal sealed class Db
{
    private static Db _Instance;
    internal Database.Amounts Amounts;
    internal static int GetCalls;
    internal static void FixtureSet(Db value) { _Instance=value; }
    internal static Db FixtureCurrent { get { return _Instance; } }
    internal static Db Get() { GetCalls++; throw new InvalidOperationException("lazy Db.Get forbidden"); }
}
internal sealed class GameClock
{
    internal static GameClock Instance = null;
    internal float GetCurrentCycleAsPercentage() { return 0; }
}
internal static class GameUtil { internal static int GetCurrentCycle() { return 42; } }
internal sealed class SpeedControlScreen
{
    internal static SpeedControlScreen Instance = null;
    internal bool IsPaused = false;
    internal int GetSpeed() { return 0; }
}
internal sealed class SaveLoader { internal static SaveLoader Instance = null; internal GameInfo GameInfo = null; }
internal sealed class GameInfo { internal string baseName = null; }
namespace OniMcp.Tools
{
    internal static class ToolUtil
    {
        internal static string CleanName(string value) { return value; }
        internal static bool GetBool(JObject args, string key, bool fallback) { return args[key] == null ? fallback : (bool)args[key]; }
        internal static float SafeFloat(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0 : value; }
        internal static int PickupableCell(Pickupable pickup) { throw new NotSupportedException("food boundary outside needs cases"); }
        internal static bool VisibleCellAllowed(int cell,bool visibleOnly) { throw new NotSupportedException("food boundary outside needs cases"); }
    }
    internal sealed class FoodAvailabilityReader
    {
        internal FoodAccessObservation Read(Edible edible,Pickupable pickup,int world,out Dictionary<string,object> evidence) { throw new NotSupportedException("food boundary outside needs cases"); }
        internal static Dictionary<string,object> NativeRations(int? world) { throw new NotSupportedException("food boundary outside needs cases"); }
    }
}
