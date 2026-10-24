using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// Native/input boundaries only; caller/helper/threshold/decision/serialization code is linked production.
internal static class NativeCanaries
{
    internal static int ForbiddenCalls;
    internal static T Forbidden<T>() { ForbiddenCalls++; throw new InvalidOperationException("unrelated native boundary invoked"); }
}
public sealed class Game { public static Game Instance = new Game(); }
public struct Position { public float x, y; }
public sealed class Transform { public Position Position; public Position GetPosition() { return Position; } }
public class GameObject
{
    public string name = "fixture";
    public int Cell, World = 7;
    public readonly Dictionary<Type,object> Parts = new Dictionary<Type,object>();
    public T GetComponent<T>() where T : class { object v; return Parts.TryGetValue(typeof(T),out v) ? v as T : null; }
    public int GetMyWorldId() { return World; }
    public string GetProperName() { return name; }
}
public sealed class MinionIdentity
{
    public readonly Dictionary<Klei.AI.Amount,Klei.AI.AmountInstance> NativeAmounts = new Dictionary<Klei.AI.Amount,Klei.AI.AmountInstance>();
    public int InvalidAmountComponentReads, World = 7;
    public T GetComponent<T>() where T : class { if (typeof(T)==typeof(Klei.AI.Amounts)) InvalidAmountComponentReads++; return null; }
    public int GetMyWorldId() { return World; }
}
namespace Klei.AI
{
    public sealed class Amount
    {
        public string Id = "unrelated_native_id", Name = "unrelated localized label";
        public bool ThrowLookup;
        public int Lookups;
        public AmountInstance Lookup(MinionIdentity d) { Lookups++; if (ThrowLookup) throw new InvalidOperationException("lookup fixture"); AmountInstance v; return d != null && d.NativeAmounts.TryGetValue(this,out v) ? v : null; }
    }
    public sealed class AmountInstance
    {
        private float stored;
        public Amount amount;
        public bool ThrowValue;
        public int Reads, Writes;
        public float value { get { Reads++; if (ThrowValue) throw new InvalidOperationException("value fixture"); return stored; } set { stored=value; Writes++; } }
    }
    public sealed class Amounts
    {
        public readonly List<AmountInstance> ModifierList = new List<AmountInstance>();
        public AmountInstance Get(Amount a) { return NativeCanaries.Forbidden<AmountInstance>(); }
    }
}
namespace Database
{
    public sealed class Amounts
    {
        public Klei.AI.Amount Breath = null;
        private Klei.AI.Amount stress;
        public bool ThrowStress;
        public Klei.AI.Amount Stress { get { if (ThrowStress) throw new InvalidOperationException("definition fixture"); return stress; } set { stress=value; } }
    }
}
public sealed class Db
{
    private static Db _Instance;
    public Database.Amounts Amounts;
    public static int GetCalls;
    public static void FixtureSet(Db value) { _Instance=value; }
    public static Db FixtureCurrent { get { return _Instance; } }
    public static Db Get() { GetCalls++; throw new InvalidOperationException("lazy database access forbidden"); }
}
public sealed class NativeCollection<T> { public readonly List<T> Items = new List<T>(); public int Count { get { return Items.Count; } } }
public static class Components
{
    public static readonly NativeCollection<MinionIdentity> LiveMinionIdentities = new NativeCollection<MinionIdentity>();
    public static readonly NativeCollection<Edible> Edibles = new NativeCollection<Edible>();
    public static readonly NativeCollection<BuildingComplete> BuildingCompletes = new NativeCollection<BuildingComplete>();
    public static readonly NativeCollection<HarvestDesignatable> HarvestDesignatables = new NativeCollection<HarvestDesignatable>();
}
public sealed class Edible { public GameObject gameObject = new GameObject(); public float Calories; }
public sealed class BuildingDef { public string PrefabID = "fixture"; public string BuildLocationRule = "OnFloor"; public int WidthInCells = 1; }
public sealed class BuildingComplete
{
    public string name = "fixture";
    public BuildingDef Def = new BuildingDef();
    public readonly Transform transform = new Transform();
    public int GetMyWorldId() { return 7; }
    public T GetComponent<T>() where T : class { return NativeCanaries.Forbidden<T>(); }
}
public sealed class Building { public int GetBottomLeftCell() { return NativeCanaries.Forbidden<int>(); } }
public sealed class Navigator
{
    public readonly GameObject gameObject = new GameObject();
    public bool CanReach(int cell) { return NativeCanaries.Forbidden<bool>(); }
}
public sealed class KPrefabID { public int InstanceID; public Tag PrefabTag; }
public struct Tag { public string Name; }
public sealed class HarvestDesignatable
{
    public GameObject gameObject;
    public bool MarkedForHarvest;
    public bool CanBeHarvested() { return NativeCanaries.Forbidden<bool>(); }
}
public enum SimHashes { Oxygen, ContaminatedOxygen, Other }
public sealed class Element { public SimHashes id; public bool IsLiquid; }
public static class Grid
{
    public const int CellCount = 80;
    public static readonly int[] WorldIdx = new int[CellCount];
    public static readonly Element[] Element = new Element[CellCount];
    public static readonly float[] Mass = new float[CellCount];
    public static readonly bool[] Solid = new bool[CellCount], Foundation = new bool[CellCount];
    public static bool Visible = true;
    public static bool IsValidCell(int c) { return c>=0 && c<CellCount; }
    public static bool IsWorldValidCell(int c) { return IsValidCell(c); }
    public static bool IsVisible(int c) { return Visible && IsValidCell(c); }
    public static int PosToCell(Edible e) { return e.gameObject.Cell; }
    public static int PosToCell(GameObject o) { return o.Cell; }
    public static int PosToCell(Navigator n) { return n.gameObject.Cell; }
    public static int PosToCell(BuildingComplete b) { return 0; }
    public static int CellColumn(int c) { return c%10; }
    public static int CellRow(int c) { return c/10; }
    public static Position CellToXY(int c) { return new Position { x=CellColumn(c),y=CellRow(c) }; }
    public static int XYToCell(int x,int y) { return x+y*10; }
}
public sealed class ClusterManager { public static ClusterManager Instance = new ClusterManager(); public int activeWorldId = 7; }
public static class GameUtil { public static int GetCurrentCycle() { return 12; } }
public static class DebugHandler
{
    public static bool NotificationsDisabled { get { return NativeCanaries.Forbidden<bool>(); } }
    public static void ToggleDisableNotifications() { NativeCanaries.Forbidden<object>(); }
}
public sealed class ColonyDiagnosticScreen
{
    public static ColonyDiagnosticScreen Instance;
    public void RefreshAll() { NativeCanaries.Forbidden<object>(); }
}
public sealed class ColonyDiagnostic
{
    public string id = "unused",name = "unused";
    public DiagnosticResult LatestResult = new DiagnosticResult();
    public IEnumerable<DiagnosticCriteria> GetCriteria() { return NativeCanaries.Forbidden<IEnumerable<DiagnosticCriteria>>(); }
}
public sealed class DiagnosticCriteria { public string id = "unused",name = "unused"; }
public sealed class DiagnosticResult { public string opinion = "unused",Message = "unused"; }
public sealed class ColonyDiagnosticUtility
{
    public enum DisplaySetting { Always, AlertOnly, Never }
    public static ColonyDiagnosticUtility Instance;
    public readonly Dictionary<int,Dictionary<string,DisplaySetting>> diagnosticDisplaySettings = new Dictionary<int,Dictionary<string,DisplaySetting>>();
    public readonly Dictionary<int,Dictionary<string,bool>> diagnosticCriteriaDisabled = new Dictionary<int,Dictionary<string,bool>>();
    public bool IsDiagnosticTutorialDisabled(string id) { return NativeCanaries.Forbidden<bool>(); }
    public void ClearDiagnosticTutorialSetting(string id) { NativeCanaries.Forbidden<object>(); }
    public ColonyDiagnostic GetDiagnostic(string id,int world) { return NativeCanaries.Forbidden<ColonyDiagnostic>(); }
    public void SetCriteriaEnabled(int w,string d,string c,bool e) { NativeCanaries.Forbidden<object>(); }
    public string GetDiagnosticName(string id) { return NativeCanaries.Forbidden<string>(); }
    public bool IsCriteriaEnabled(int w,string d,string c) { return NativeCanaries.Forbidden<bool>(); }
}
namespace OniMcp.Core
{
    public sealed class McpToolParameter { public string Type,Description; public bool Required; public List<string> EnumValues; }
    public sealed class McpTool
    {
        public string Name,Group,Mode,Risk,Description;
        public bool Hidden;
        public List<string> Aliases,Tags;
        public Dictionary<string,McpToolParameter> Parameters;
        public Func<JObject,CallToolResult> Handler;
    }
    public sealed class CallToolResult
    {
        public bool IsError;
        public string Value;
        public static CallToolResult Text(string v) { return new CallToolResult { Value=v }; }
        public static CallToolResult Error(string v) { return new CallToolResult { Value=v,IsError=true }; }
    }
}
namespace OniMcp.Tools
{
    internal static class ToolUtil
    {
        internal static bool GetBool(JObject a,string k,bool f) { return a[k]==null ? f : (bool)a[k]; }
        internal static int? GetInt(JObject a,string k) { return a[k]==null ? (int?)null : (int)a[k]; }
        internal static float? GetFloat(JObject a,string k) { return a[k]==null ? (float?)null : (float)a[k]; }
        internal static int ClampLimit(JObject a,int d,int m) { return Math.Max(0,Math.Min(GetInt(a,"limit")??d,m)); }
        internal static float SafeFloat(float v) { return float.IsNaN(v)||float.IsInfinity(v) ? 0 : v; }
        internal static bool VisibleCellAllowed(int c,bool only) { return Grid.IsValidCell(c)&&(!only||Grid.IsVisible(c)); }
        internal static bool CellMatchesWorld(int c,int w) { return Grid.IsValidCell(c)&&(w<0||Grid.WorldIdx[c]==w); }
        internal static string CleanName(string s) { return s; }
    }
    internal static class FoodAvailabilityReader
    {
        internal static FoodAccessBreakdown Fixture = new FoodAccessBreakdown();
        internal static int Calls;
        internal static bool LastVisibleOnly;
        internal static FoodAccessBreakdown CaptureAccess(bool visibleOnly) { Calls++;LastVisibleOnly=visibleOnly;return Fixture; }
    }
    internal static class AutoDisinfectPolicy
    {
        internal static object Status() { return NativeCanaries.Forbidden<object>(); }
        internal static void SetDisabled(bool d,bool persist) { NativeCanaries.Forbidden<object>(); }
        internal static int ApplyToExisting() { return NativeCanaries.Forbidden<int>(); }
    }
}
