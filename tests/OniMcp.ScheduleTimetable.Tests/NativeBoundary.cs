using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// Native declarations and independent fixture inputs only. Production row parsing,
// validation, lookup uniqueness, owner guards and response policy are linked whole.
namespace UnityEngine
{
    public class Object
    {
        public bool FixtureDestroyed;
        private static bool Missing(Object item) { return ReferenceEquals(item, null) || item.FixtureDestroyed; }
        public static bool operator ==(Object left, Object right)
        { return Missing(left) ? Missing(right) : !Missing(right) && ReferenceEquals(left, right); }
        public static bool operator !=(Object left, Object right) { return !(left == right); }
        public static implicit operator bool(Object value) { return !Missing(value); }
        public override bool Equals(object value) { return ReferenceEquals(this, value); }
        public override int GetHashCode() { return base.GetHashCode(); }
    }
    public class Component : Object
    {
        private GameObject fixtureOwner;
        public GameObject gameObject { get { return fixtureOwner; } }
        public void FixtureSetOwner(GameObject owner) { fixtureOwner = owner; }
        public T GetComponent<T>() where T : class { return gameObject == null ? null : gameObject.GetComponent<T>(); }
        public T[] GetComponents<T>() where T : class { return gameObject == null ? new T[0] : gameObject.GetComponents<T>(); }
        public string GetProperName() { return gameObject == null ? null : gameObject.FixtureName; }
    }
    public sealed class GameObject : Object
    {
        public string FixtureName;
        private readonly List<Component> fixtureComponents = new List<Component>();
        public void FixtureAdd(Component component) { component.FixtureSetOwner(this); fixtureComponents.Add(component); }
        public T GetComponent<T>() where T : class { return fixtureComponents.OfType<T>().FirstOrDefault(); }
        public T[] GetComponents<T>() where T : class { return fixtureComponents.OfType<T>().ToArray(); }
        public string GetProperName() { return FixtureName; }
    }
    public static class Time { public static float timeScale; }
}
public class KMonoBehaviour : UnityEngine.Component { }
public sealed class KPrefabID : KMonoBehaviour { public int InstanceID; }
public sealed class MinionIdentity : KMonoBehaviour { }
public sealed class Schedulable : KMonoBehaviour
{
    public Schedule FixtureSchedule;
    public Schedule GetSchedule() { return FixtureSchedule; }
}
public sealed class Ref<T> where T : class
{
    public T FixtureTarget;
    public bool FixtureThrow;
    public int FixtureReads;
    public T Get()
    { FixtureReads++; if (FixtureThrow) throw new InvalidOperationException("fixture ref read"); return FixtureTarget; }
}
public sealed class Game : KMonoBehaviour
{
    public static Game Instance;
    public bool FixtureLoading;
    public bool IsLoading() { return FixtureLoading; }
}
public sealed class GameClock : KMonoBehaviour { public static GameClock Instance; }
public sealed class Db
{
    private static Db _Instance = null;
    public Database.ScheduleGroups ScheduleGroups;
    public static int FixtureGetCalls;
    public static bool FixtureThrowGet;
    public static Db Get()
    { FixtureGetCalls++; if (FixtureThrowGet) throw new InvalidOperationException("forbidden Db.Get"); return _Instance; }
    public static void FixtureSetExisting(Db value) { _Instance = value; }
}
namespace Database
{
    public sealed class ScheduleGroups
    {
        public List<ScheduleGroup> allGroups;
        public ScheduleGroup Worktime, Hygene, Recreation, Sleep;
    }
}
public sealed class ScheduleGroup
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public int defaultSegments { get; private set; }
    public bool alarm { get; private set; }
    public ScheduleGroup(string id, string name, int segments = 0, bool alarmValue = false)
    { Id = id; Name = name; defaultSegments = segments; alarm = alarmValue; }
}
public sealed class ScheduleBlock
{
    public string GroupId { get; private set; }
    public string name;
    public ScheduleBlock(string groupId, string displayName) { GroupId = groupId; name = displayName; }
    public void FixtureNativeSet(ScheduleGroup group) { GroupId = group.Id; name = group.Name; }
}
public sealed class Schedule
{
    private List<Ref<Schedulable>> assigned = null;
    public string name;
    public bool alarmActivated;
    private int fixtureProgress;
    public int FixtureProgressWrites;
    public int ProgressTimetableIdx { get { return fixtureProgress; } set { FixtureProgressWrites++; fixtureProgress = value; } }
    public List<ScheduleBlock> FixtureBlocks;
    public int FixtureCurrentBlockIndex;
    public ScheduleBlock FixtureCurrentBlock;
    public int FixtureGetBlocksCalls, FixtureCurrentIndexCalls, FixtureCurrentGroupCalls, FixtureGetAssignedCalls;
    public bool FixtureThrowBlocks, FixtureThrowCurrentIndex, FixtureThrowCurrentGroup, FixtureThrowGetAssigned;
    public System.Action FixtureOnBlocksRead, FixtureBeforeSet, FixtureAfterSet;
    public int FixtureSetCalls, FixtureNotifications, FixtureAssignCalls, FixtureUnassignCalls;
    public int FixtureLastSetIndex = -1;
    public ScheduleGroup FixtureLastSetGroup;
    public bool FixtureThrowSetBeforeMutation, FixtureThrowSetAfterMutation;
    public void FixtureSeed(List<ScheduleBlock> blocks, int progress, int currentIndex,
        ScheduleBlock currentBlock, List<Ref<Schedulable>> references)
    { FixtureBlocks = blocks; fixtureProgress = progress; FixtureCurrentBlockIndex = currentIndex;
      FixtureCurrentBlock = currentBlock; assigned = references; FixtureProgressWrites = 0; }
    public void FixtureSetAssigned(List<Ref<Schedulable>> references) { assigned = references; }
    public List<Ref<Schedulable>> FixtureAssigned { get { return assigned; } }
    public List<ScheduleBlock> GetBlocks()
    { FixtureGetBlocksCalls++; var hook = FixtureOnBlocksRead; if (hook != null) { FixtureOnBlocksRead = null; hook(); }
      if (FixtureThrowBlocks) throw new InvalidOperationException("fixture blocks read"); return FixtureBlocks; }
    public int GetCurrentBlockIdx()
    { FixtureCurrentIndexCalls++; if (FixtureThrowCurrentIndex) throw new InvalidOperationException("fixture index read"); return FixtureCurrentBlockIndex; }
    public ScheduleBlock GetCurrentScheduleBlock()
    { FixtureCurrentGroupCalls++; if (FixtureThrowCurrentGroup) throw new InvalidOperationException("fixture current group read"); return FixtureCurrentBlock; }
    public List<Ref<Schedulable>> GetAssigned()
    { FixtureGetAssignedCalls++; if (FixtureThrowGetAssigned) throw new InvalidOperationException("forbidden GetAssigned"); return assigned; }
    public void SetBlockGroup(int index, ScheduleGroup group)
    {
        FixtureSetCalls++; FixtureLastSetIndex = index; FixtureLastSetGroup = group;
        var before = FixtureBeforeSet; if (before != null) before();
        if (FixtureThrowSetBeforeMutation) throw new InvalidOperationException("fixture native set before");
        // This native boundary changes exactly the supplied list element. It does
        // not derive a timetable row or validate product request eligibility.
        FixtureBlocks[index].FixtureNativeSet(group); FixtureNotifications++;
        var after = FixtureAfterSet; if (after != null) after();
        if (FixtureThrowSetAfterMutation) throw new InvalidOperationException("fixture native set after");
    }
    public void Assign(Schedulable value) { FixtureAssignCalls++; }
    public void Unassign(Schedulable value) { FixtureUnassignCalls++; }
}
public sealed class ScheduleManager
{
    public static ScheduleManager Instance;
    public List<Schedule> FixtureSchedules;
    public static int FixtureCurrentHour;
    public int FixtureReads, FixtureAdds;
    public List<Schedule> GetSchedules() { FixtureReads++; return FixtureSchedules; }
    public static int GetCurrentHour() { return FixtureCurrentHour; }
    public Schedule AddSchedule(List<ScheduleGroup> groups, string name, bool alarmOn)
    { FixtureAdds++; throw new InvalidOperationException("out of scope native schedule creation"); }
}
public sealed class FixtureComponents<T>
{
    public readonly List<T> Items = new List<T>();
    public int Count { get { return Items.Count; } }
}
public static class Components
{
    public static readonly FixtureComponents<MinionIdentity> LiveMinionIdentities = new FixtureComponents<MinionIdentity>();
}
public static class GameUtil { public static int GetCurrentCycle() { return 1; } }
public sealed class WorldContainer
{
    public int id, Width, Height; public string worldName;
    public string GetProperName() { return worldName; }
}
public sealed class ClusterManager
{
    public static ClusterManager Instance;
    public int worldCount, activeWorldId;
    public List<WorldContainer> WorldContainers;
}
public sealed class FixtureGameInfo { public string baseName; }
public sealed class SaveLoader { public static SaveLoader Instance; public FixtureGameInfo GameInfo; }
public struct Tag { public string Name; }
public sealed class DiscoveredResources
{
    public static DiscoveredResources Instance;
    public List<Tag> FixtureDiscovered;
    public List<Tag> GetDiscovered() { return FixtureDiscovered; }
}
namespace OniMcp.Support
{
    public static class McpLocalization
    {
        public static string Get(string key, string fallback = null) { return key; }
        public static string Format(string key, string fallback, params object[] values) { return key; }
    }
}
namespace OniMcp.Tools
{
    public sealed class McpTool
    {
        public string Name, Group, Mode, Risk, Description; public bool Hidden;
        public List<string> Aliases, Tags; public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, CallToolResult> Handler;
    }
    public sealed class McpToolParameter
    { public string Type, Description; public bool Required; public List<string> EnumValues; }
    public static class ToolUtil
    {
        public static int? GetInt(JObject args, string key) { return (int?)args[key]; }
        public static bool GetBool(JObject args, string key, bool fallback) { return (bool?)args[key] ?? fallback; }
        public static MinionIdentity FindDupe(JObject args) { throw new InvalidOperationException("non-target FindDupe"); }
    }
    public static class FixtureRoutes
    {
        public static int Calls;
        public static McpTool Forbidden(string name)
        { return new McpTool { Handler = args => { Calls++; throw new InvalidOperationException("non-target route " + name); } }; }
    }
    public static partial class ColonyTools
    { public static McpTool GetDuplicants() { return FixtureRoutes.Forbidden("dupes"); } }
    public static class SnapshotTools
    { public static McpTool GetColonyStateSnapshot() { return FixtureRoutes.Forbidden("snapshot"); } }
    public static class ColonyReportTools
    { public static McpTool ControlColonyReport() { return FixtureRoutes.Forbidden("report"); } }
    public static class DiagnosticsTools
    { public static McpTool ControlDiagnostics() { return FixtureRoutes.Forbidden("diagnostics"); } }
    public static class SurvivalPlanTools
    { public static McpTool ControlSurvivalPlan() { return FixtureRoutes.Forbidden("survival"); } }
    public static class NotificationTools
    { public static McpTool ControlNotification() { return FixtureRoutes.Forbidden("notifications"); } }
    public static class BioControlTools
    { public static McpTool ControlBio() { return FixtureRoutes.Forbidden("bio"); } }
    public static class DietTools
    { public static McpTool ControlDiet() { return FixtureRoutes.Forbidden("diet"); } }
    public static class ResearchTools
    { public static McpTool ControlResearch() { return FixtureRoutes.Forbidden("research"); } }
    public static class MedicalTools
    { public static McpTool ControlMedical() { return FixtureRoutes.Forbidden("medical"); } }
}

namespace OniMcp.Tools
{
    // Uncalled compile canaries for unrelated methods in the linked whole
    // execution-policy file. The actual outcome parser/classifier is not stubbed.
    public static partial class WorldEditorTools
    {
        private static JObject InheritWorldEditorSandboxPolicy(JObject parent, JObject child)
        { throw new InvalidOperationException("out-of-scope sandbox inheritance"); }
        private static CallToolResult JsonResult(JToken value)
        { throw new InvalidOperationException("out-of-scope world-editor result writer"); }
    }
}
