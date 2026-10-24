using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// Inputs only. The four product files, their actual factories/callers and serializer
// are compiled unchanged. No native formatter or new opt-in decision is copied here.
internal static class NativeCanary
{
    internal static int ForbiddenCalls;
    internal static T Forbidden<T>() { ForbiddenCalls++; throw new InvalidOperationException("forbidden native fixture"); }
}
public sealed class Game { public static Game Instance = new Game(); }
public struct Position { public float x, y; }
public sealed class Transform { public Position Position; public Position GetPosition() { return Position; } }
public struct Tag { public string Name; public override string ToString() { return Name; } }
public sealed class KPrefabID { public int InstanceID; public Tag PrefabTag; }
public sealed class MinionIdentity
{
    public string Name = "normal native fixture";
    public int World = 3, Cell = 123;
    public readonly Transform transform = new Transform();
    public readonly Dictionary<Type, object> Parts = new Dictionary<Type, object>();
    public readonly Dictionary<Klei.AI.Amount, Klei.AI.AmountInstance> NativeAmounts = new Dictionary<Klei.AI.Amount, Klei.AI.AmountInstance>();
    public Klei.AI.Amounts Amounts = new Klei.AI.Amounts();
    public Klei.AI.Attributes Attributes = new Klei.AI.Attributes();
    public int InvalidComponentReads, SmiReads;
    public T GetComponent<T>() where T : class
    {
        if (typeof(T) == typeof(Klei.AI.Amounts)) { InvalidComponentReads++; return NativeCanary.Forbidden<T>(); }
        object v; return Parts.TryGetValue(typeof(T), out v) ? v as T : null;
    }
    public T GetSMI<T>() where T : class { SmiReads++; return null; }
    public Klei.AI.Amounts GetAmounts() { return Amounts; }
    public Klei.AI.Attributes GetAttributes() { return Attributes; }
    public string GetProperName() { return Name; }
    public int GetMyWorldId() { return World; }
}
namespace Klei.AI
{
    public enum Units { Flat, Percentage, Calories, Kelvin }
    public sealed class Amount
    {
        public string Id = "Breath", Name = "native localized breath";
        public Units StoredUnits = Units.Flat;
        public bool ThrowLookup, ThrowUnits;
        public int Lookups, UnitReads;
        public Units units { get { UnitReads++; if (ThrowUnits) throw new InvalidOperationException("units fixture"); return StoredUnits; } }
        public AmountInstance Lookup(MinionIdentity dupe)
        {
            Lookups++; if (ThrowLookup) throw new InvalidOperationException("lookup fixture");
            AmountInstance v; return dupe != null && dupe.NativeAmounts.TryGetValue(this, out v) ? v : null;
        }
    }
    public sealed class AmountInstance
    {
        public Amount amount;
        public float Raw = 41.125f, Maximum = 200, Minimum = 0, Delta = 0;
        public string Display = "native-marker-41";
        public bool ThrowValue, ThrowMaximum, ThrowDisplay;
        public int ValueReads, MaxReads, DisplayReads, MinReads, DeltaReads;
        public float value { get { ValueReads++; if (ThrowValue) throw new InvalidOperationException("value fixture"); return Raw; } }
        public float GetMax() { MaxReads++; if (ThrowMaximum) throw new InvalidOperationException("max fixture"); return Maximum; }
        public string GetValueString() { DisplayReads++; if (ThrowDisplay) throw new InvalidOperationException("display fixture"); return Display; }
        public float GetMin() { MinReads++; return Minimum; }
        public float GetDelta() { DeltaReads++; return Delta; }
    }
    public sealed class Amounts { public readonly List<AmountInstance> ModifierList = new List<AmountInstance>(); }
    public sealed class AttributeInstance
    {
        public bool hide; public string Id = "unused", Name = "unused";
        public float GetTotalValue() { return 0; } public float GetBaseValue() { return 0; }
    }
    public sealed class Profession { public string Name = "fixture profession"; }
    public sealed class Attributes : IEnumerable<AttributeInstance>
    {
        public Profession GetProfession() { return new Profession(); }
        public IEnumerator<AttributeInstance> GetEnumerator() { return new List<AttributeInstance>().GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}
namespace Database
{
    public sealed class Amounts { public Klei.AI.Amount Breath, Stress; }
}
public sealed class Db
{
    private static Db _Instance;
    public Database.Amounts Amounts;
    public static int GetCalls;
    public static void FixtureSet(Db value) { _Instance = value; }
    public static Db Get() { GetCalls++; return NativeCanary.Forbidden<Db>(); }
}
public sealed class MinionResume
{
    public int AvailableSkillpoints;
    public readonly Dictionary<string, bool> MasteryBySkillID = new Dictionary<string, bool>();
    public readonly Dictionary<string, float> AptitudeBySkillGroup = new Dictionary<string, float>();
}
public sealed class Schedulable { public Schedule GetSchedule() { return null; } }
public sealed class Schedule { public string name; public ScheduleBlock GetCurrentScheduleBlock() { return null; } }
public sealed class ScheduleBlock { public string GroupId; }
public sealed class Navigator { public T GetSMI<T>() where T : class, new() { return new T(); } }
public sealed class MoveToLocationMonitor { public sealed class Instance { } }
public sealed class ClusterManager { public static ClusterManager Instance; public int activeWorldId; }
public static class Grid
{
    public static int PosToCell(MinionIdentity d) { return d.Cell; }
    public static int CellColumn(int cell) { return cell % 100; }
    public static int CellRow(int cell) { return cell / 100; }
    public static int XYToCell(int x, int y) { return x + y * 100; }
    public static bool IsValidCell(int cell) { return cell >= 0; }
    public static bool IsWorldValidCell(int cell) { return cell >= 0; }
    public static bool IsVisible(int cell) { return true; }
}
public static class Components
{
    public sealed class MinionCollection { public readonly List<MinionIdentity> Items = new List<MinionIdentity>(); }
    public static readonly MinionCollection LiveMinionIdentities = new MinionCollection();
}
public sealed class BionicOilMonitor
{
    public const float OIL_REFILL_TRESHOLD = .3f;
    public static bool WantsOilChange(Instance m) { return false; }
    public sealed class Instance { public float CurrentOilMass, CurrentOilPercentage; public Klei.AI.AmountInstance oilAmount; public bool HasOil, IsOnline; }
}
public sealed class Storage { public float capacityKg; }
public sealed class BionicOxygenTankMonitor
{
    public const float SAFE_TRESHOLD = .5f, CRITICAL_TRESHOLD = .2f;
    public static bool AreOxygenLevelsSafe(Instance m) { return false; }
    public static bool AreOxygenLevelsCritical(Instance m) { return false; }
    public sealed class Instance { public Storage storage; public float AvailableOxygen, OxygenPercentage; public bool IsEmpty, isRecoveringFromSuffocation; }
}
public sealed class GunkMonitor
{
    public static bool IsGunkLevelsOverMildUrgeThreshold(Instance m) { return false; }
    public static bool IsGunkLevelsOverCriticalUrgeThreshold(Instance m) { return false; }
    public sealed class Def { public float SeekForGunkToiletTreshold_InSchedule, DesperetlySeekForGunkToiletTreshold; }
    public sealed class Instance { public Def def; public float CurrentGunkMass, CurrentGunkPercentage; public bool IsGunkBuildupAtMax, DoesCurrentScheduleAllowsGunkToilet; }
}
public sealed class BionicBatteryMonitor
{
    public static bool IsCriticallyLow(Instance m) { return false; }
    public static bool ChargeIsBelowNotificationThreshold(Instance m) { return false; }
    public static bool DoesNotHaveCharge(Instance m) { return false; }
    public sealed class Instance { public float CurrentCharge, Wattage; public int ChargedElectrobankCount, DepletedElectrobankCount, ElectrobankCountCapacity; public bool IsOnline; }
}
namespace OniMcp.Core
{
    public sealed class McpTool
    {
        public string Name, Group, Mode, Risk, Description;
        public bool Hidden;
        public List<string> Aliases, Tags;
        public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, CallToolResult> Handler;
    }
    public sealed class McpToolParameter { public string Type, Description; public bool Required; public List<string> EnumValues; }
}
namespace OniMcp.Tools
{
    internal static class ToolUtil
    {
        internal static MinionIdentity Selected;
        internal static MinionIdentity FindDupe(JObject args) { return Selected; }
        internal static int? GetInt(JObject args, string key) { return args[key] == null ? (int?)null : (int)args[key]; }
        internal static bool GetBool(JObject args, string key, bool fallback) { return args[key] == null ? fallback : (bool)args[key]; }
        internal static bool CellMatchesWorld(int cell, int world) { return true; }
        internal static string CleanName(string value) { return value; }
        internal static float SafeFloat(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0 : value; }
    }
    internal static class AmbientTemperaturePolicy
    {
        internal static string Status(string state, double temperature) { return "fixture_neutral"; }
        internal static bool WarnForDupe(string state, double temperature) { return false; }
    }
    public static partial class DuplicantTools
    {
        private static string GuessRole(MinionIdentity dupe) { return "fixture-role"; }
        private static Dictionary<string, McpToolParameter> DupeLookupParams() { return new Dictionary<string, McpToolParameter>(); }
        private static ReachabilitySummary ScanReachableNearby(Navigator n, int x, int y, int world, int radius, bool samples) { return new ReachabilitySummary { ReachableCells = 10, VisibleCells = 10, SolidCells = 0 }; }
        private static bool SafeCanReach(Navigator n, int cell) { return true; }
        private static KeyNeeds KeyNeedValues(MinionIdentity dupe)
        {
            var result = new KeyNeeds();
            foreach (string field in new[] { "stamina", "calories", "stress", "bladder", "breath", "bodyTemperature" })
                result[field].Read(() => new RawNeedSample { Value = 100, Unit = "Flat" });
            return result;
        }
        private static Dictionary<string, object> CellEnvironment(int cell) { return new Dictionary<string, object> { ["state"] = "gas", ["temperatureC"] = 20d }; }
        private static Dictionary<string, object> CurrentChoreSummary(MinionIdentity dupe) { return null; }
        private static Dictionary<string, object> IdleDiagnostics(MinionIdentity d, ReachabilitySummary r, KeyNeeds n, bool canMove) { return null; }
    }
}
