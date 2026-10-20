using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// Native Context.CompareTo, IsPotentialSuccess, schedule and current-chore getters
// are fixture boundaries. The full production reader is compiled unchanged.
public sealed class GameObject
{
    public readonly Dictionary<Type, object> Components = new Dictionary<Type, object>();
    public int GetInstanceID() { return 101; }
    public T GetComponent<T>() where T : class
    { object value; return Components.TryGetValue(typeof(T), out value) ? value as T : null; }
}
public sealed class MinionIdentity
{
    public GameObject gameObject = new GameObject();
    public string Name = "Fixture duplicant";
    public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
    public T GetSMI<T>() where T : class { return null; }
    public bool HasTag(object value) { return false; }
}
public sealed class Game { public static Game Instance = new Game(); }
public static class GameTags { public static readonly object Dead = new object(); }
public static class Components
{
    public sealed class Collection { public List<MinionIdentity> Items = new List<MinionIdentity>(); }
    public static readonly Collection LiveMinionIdentities = new Collection();
}
public sealed class Schedulable
{
    public Schedule Schedule;
    public Schedule GetSchedule() { return Schedule; }
}
public sealed class Schedule
{
    public sealed class Block { public string name; }
    public Block Current;
    public Block GetCurrentScheduleBlock() { return Current; }
}
public sealed class ChoreConsumer
{
    public sealed class Snapshot
    {
        public readonly List<Chore.Precondition.Context> failedContexts = new List<Chore.Precondition.Context>();
        public readonly List<Chore.Precondition.Context> succeededContexts = new List<Chore.Precondition.Context>();
        private bool dirty;
        public int FlagWrites;
        public bool doFailedContextsNeedSorting { get { return dirty; } set { dirty = value; FlagWrites++; } }
        public void FixtureDirty(bool value) { dirty = value; FlagWrites = 0; }
    }
    public Snapshot Shared = new Snapshot();
    public ChoreDriver choreDriver;
    public MinionIdentity Owner;
    public int SnapshotReads;
    public Snapshot GetLastPreconditionSnapshot() { SnapshotReads++; return Shared; }
    public T GetComponent<T>() where T : class { return Owner as T; }
    public int GetPersonalPriority(ChoreGroup group) { return group.Priority; }
}
public sealed class ChoreDriver
{
    public Chore Current;
    public Chore GetCurrentChore() { return Current; }
}
public sealed class ChoreGroup { public string Id; public int Priority; }
public sealed class ChoreType { public string Id; public ChoreGroup[] groups; }
public struct PrioritySetting { public int priority_class, priority_value; }
public sealed class Chore
{
    public sealed class Target { public GameObject gameObject; }
    public sealed class Entry { public Condition condition; }
    public sealed class Condition { public string id, description; }
    public string Name;
    public Target target;
    public GameObject gameObject;
    public ChoreType choreType;
    public PrioritySetting masterPriority;
    public ChoreDriver driver;
    public string GetReportName() { return Name; }
    public readonly List<Entry> Preconditions = new List<Entry>();
    public int PreconditionReads;
    public List<Entry> GetPreconditions() { PreconditionReads++; return Preconditions; }
    public static int CompareReads, PotentialReads;
    public static class Precondition
    {
        public struct Context : IComparable<Context>
        {
            public Chore chore;
            public object data;
            public PrioritySetting masterPriority;
            public int personalPriority, priority, priorityMod, consumerPriority, cost, failedPreconditionId;
            public int FixtureOrder;
            public bool FixturePotential;
            public int CompareTo(Context other) { CompareReads++; return FixtureOrder.CompareTo(other.FixtureOrder); }
            public bool IsPotentialSuccess() { PotentialReads++; return FixturePotential; }
        }
    }
}
public static class GameUtil { public static string GetChoreName(Chore chore, object data) { return chore.Name; } }

// Unused bionic collaborators exist only to compile the original containing file.
public sealed class KPrefabID { public int InstanceID; }
public sealed class BionicUpgradeComponent
{
    public sealed class Prefab { public string Name; }
    public sealed class Assignee { public string GetProperName() { return "fixture"; } }
    public GameObject gameObject;
    public object Booster;
    public float CurrentWattage, PotentialWattage;
    public Assignee assignee;
    public T GetComponent<T>() where T : class { return null; }
    public Prefab PrefabID() { return new Prefab(); }
    public string GetProperName() { return "fixture"; }
}
public static class BionicUpgradesMonitor
{
    public sealed class Instance
    {
        public bool IsOnline, HasAnyUpgradeAssigned, HasAnyUpgradeInstalled;
        public int UnlockedSlotCount, AssignedSlotCount;
        public UpgradeComponentSlot[] upgradeComponentSlots;
    }
    public sealed class Assignable { public int ID; public bool IsUnassigning() { return false; } }
    public sealed class UpgradeComponentSlot
    {
        public bool IsLocked, HasUpgradeInstalled, HasUpgradeComponentAssigned, AssignedUpgradeMatchesInstalledUpgrade;
        public float WattageCost;
        public BionicUpgradeComponent assignedUpgradeComponent, installedUpgradeComponent;
        public Assignable GetAssignableSlotInstance() { return new Assignable(); }
    }
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
    public sealed class McpToolParameter { public string Type, Description; public bool Required; }
}
namespace OniMcp.Tools
{
    internal static class ToolUtil
    {
        internal static MinionIdentity FindDupe(JObject args) { return null; }
        internal static int ClampLimit(JObject args, int fallback, int maximum) { return fallback; }
        internal static int? GetInt(JObject args, string key) { return args?[key]?.Value<int?>(); }
        internal static bool GetBool(JObject args, string key, bool fallback) { return args?[key]?.Value<bool?>() ?? fallback; }
        internal static string CleanName(string value) { return value; }
        internal static float SafeFloat(float value) { return value; }
    }
    public static class DuplicantTools
    {
        public static Dictionary<string, object> DescribeCurrentChore(Chore chore, MinionIdentity minion)
            { return new Dictionary<string, object> { ["fixtureCurrentSummary"] = "preserved" }; }
    }
    public static partial class FacilitySideScreenTools
    {
        internal static Dictionary<string, object> TodoForTest(MinionIdentity minion, bool includeBlocked, bool potentialOnly, int limit)
            { return MinionTodoInfo(minion, includeBlocked, potentialOnly, limit); }
        private static Dictionary<string, object> TargetInfo(GameObject target)
            { return new Dictionary<string, object> { ["name"] = "Fixture duplicant", ["id"] = target.GetInstanceID() }; }
        private static bool MatchesQuery(Dictionary<string, object> info, string query) { return true; }
        private static CallToolResult JsonResult(object value) { return CallToolResult.Text(JsonConvert.SerializeObject(value)); }
    }
}
