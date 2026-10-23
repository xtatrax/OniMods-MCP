using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// All native predicates below return supplied fixture opinions. No skill rule,
// model-filter algorithm, eligibility calculation or UI lifecycle is duplicated.
namespace OniMcp.Tools
{
    public sealed class McpTool
    {
        public string Name, Group, Mode, Risk, Description;
        public bool Hidden;
        public List<string> Aliases, Tags;
        public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, CallToolResult> Handler;
    }
    public sealed class McpToolParameter
    {
        public string Type, Description;
        public bool Required;
        public List<string> EnumValues;
    }
    public static class ToolUtil
    {
        public static int? GetInt(JObject a, string key) { int n; return int.TryParse(a?[key]?.ToString(), out n) ? n : (int?)null; }
        public static bool GetBool(JObject a, string key, bool fallback) { bool b; return bool.TryParse(a?[key]?.ToString(), out b) ? b : fallback; }
        public static string CleanName(string value) { return value; }
        public static MinionIdentity FindDupe(JObject args)
        {
            int? id = GetInt(args, "id");
            return Components.LiveMinionIdentities.Items.FirstOrDefault(d => d != null && (id.HasValue ? d.Id == id.Value : d.Name == (string)args["name"]));
        }
    }
    public static partial class DuplicantTools
    {
        private static Dictionary<string, object> DupeRef(MinionIdentity dupe) { return new Dictionary<string, object> { ["id"] = dupe.Id, ["name"] = dupe.Name }; }
        private static bool Contains(string value, string query) { return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; }
    }
}

public sealed class Db
{
    private static Db _Instance;
    public static void SetFixture(Db value) { _Instance = value; }
    public static int GetCalls;
    public SkillSet Skills = new SkillSet();
    public PersonalitySet Personalities = new PersonalitySet();
    public static Db Get() { GetCalls++; return _Instance; }
}
public sealed class SkillSet
{
    public List<Database.Skill> resources = new List<Database.Skill>();
    public Database.Skill TryGet(string id) { return resources.FirstOrDefault(s => s != null && s.Id == id); }
}
namespace Database
{
    public sealed class Skill
    {
        public string Id, Name, description, skillGroup, hat, requiredDuplicantModel;
        public int tier, Morale;
        public bool deprecated;
        public List<string> priorSkills = new List<string>();
        public int GetMoraleExpectation() { return Morale; }
    }
}
public sealed class Personality { public string Id; public Tag model; }
public sealed class PersonalitySet { public List<Personality> resources = new List<Personality>(); }
// Native Tag is a value type. Hash values here are supplied boundary IDs,
// not an implementation or test of Klei's string hash algorithm.
public struct Tag
{
    public readonly string Name;
    public readonly int Hash;
    public Tag(string value)
    {
        Name = value;
        Hash = string.IsNullOrEmpty(value) ? 0 : string.Equals(value, "Minion", StringComparison.OrdinalIgnoreCase) ? 11 : string.Equals(value, "BionicMinion", StringComparison.OrdinalIgnoreCase) ? 22 : string.Equals(value, "ThirdPartyModel", StringComparison.OrdinalIgnoreCase) ? 33 : throw new NotSupportedException("Unsupplied native Tag hash fixture");
    }
    public bool IsValid { get { return Hash != 0; } }
    public static readonly Tag Invalid = default(Tag);
    public static implicit operator Tag(string value) { return new Tag(value); }
    public static bool operator ==(Tag a, Tag b) { return a.Hash == b.Hash; }
    public static bool operator !=(Tag a, Tag b) { return !(a == b); }
    public override bool Equals(object other) { return other is Tag && this == (Tag)other; }
    public override int GetHashCode() { return Hash; }
    public override string ToString() { return Name; }
}
public sealed class KPrefabID { public int InstanceID; }
public sealed class MinionIdentity
{
    public int Id;
    public string Name, personalityResourceId;
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public Personality Personality;
    public MinionResume Resume;
    public Personality GetPersonality() { return Personality; }
    public string GetProperName() { return Name; }
    public T GetComponent<T>() where T : class
    {
        if (typeof(T) == typeof(MinionResume)) return Resume as T;
        if (typeof(T) == typeof(KPrefabID)) return new KPrefabID { InstanceID = Id } as T;
        return null;
    }
}
public static class Components
{
    public static MinionCollection LiveMinionIdentities = new MinionCollection();
}
public sealed class MinionCollection { public readonly List<MinionIdentity> Items = new List<MinionIdentity>(); }
public sealed class MinionResume
{
    public enum SkillMasteryConditions { InsufficientPoints, MissingPrerequisite, JobRestriction }
    public readonly Dictionary<string, SkillMasteryConditions[]> Conditions = new Dictionary<string, SkillMasteryConditions[]>();
    public readonly Dictionary<SkillMasteryConditions[], bool> NativeOpinions = new Dictionary<SkillMasteryConditions[], bool>();
    public Dictionary<string, bool> MasteryBySkillID = new Dictionary<string, bool>();
    public readonly HashSet<string> Mastered = new HashSet<string>();
    public readonly HashSet<string> Granted = new HashSet<string>();
    public int AvailableSkillpoints = 5, ConditionCalls, CanMasterCalls, DirectMasterCalls, GrantCalls;
    public bool ThrowConditions, ThrowMastered;
    public SkillMasteryConditions[] GetSkillMasteryConditions(string id)
    {
        ConditionCalls++;
        if (ThrowConditions) throw new InvalidOperationException("native conditions unavailable fixture");
        SkillMasteryConditions[] result;
        return Conditions.TryGetValue(id, out result) ? result : null;
    }
    public bool CanMasterSkill(SkillMasteryConditions[] conditions) { CanMasterCalls++; return NativeOpinions[conditions]; }
    public bool HasMasteredSkill(string id) { if (ThrowMastered) throw new InvalidOperationException("native mastery unavailable fixture"); return Mastered.Contains(id); }
    public bool HasBeenGrantedSkill(string id) { return Granted.Contains(id); }
    public void MasterSkill(string id) { DirectMasterCalls++; throw new InvalidOperationException("Direct native mutation bypassed normal UI"); }
    public void GrantSkill(string id) { GrantCalls++; Granted.Add(id); Mastered.Add(id); }
    public string CurrentHat, TargetHat;
    public IEnumerable<HatRecord> GetAllHats() { throw new NotSupportedException("unrelated hat path"); }
    public bool OwnsHat(string hat) { throw new NotSupportedException("unrelated hat path"); }
    public void SetHats(string current, string target) { throw new NotSupportedException("unrelated hat path"); }
    public void ApplyTargetHat() { throw new NotSupportedException("unrelated hat path"); }
}
public sealed class HatRecord { public string Source, Hat; public int count; }

namespace UnityEngine
{
    public sealed class GameObject
    {
        public bool activeSelf = true, activeInHierarchy = true;
        public readonly Transform transform = new Transform();
        public readonly Dictionary<Type, object> Components = new Dictionary<Type, object>();
        public T GetComponent<T>() where T : class { object result; return Components.TryGetValue(typeof(T), out result) ? result as T : null; }
    }
    public sealed class Transform
    {
        public Transform Parent;
        public bool IsChildOf(Transform other)
        {
            for (var item = this; item != null; item = item.Parent) if (ReferenceEquals(item, other)) return true;
            return false;
        }
    }
}
namespace UnityEngine.EventSystems
{
    public sealed class EventSystem { public static EventSystem current; public bool isActiveAndEnabled = true; }
    public sealed class PointerEventData
    {
        public enum InputButton { Left, Right, Middle }
        public readonly EventSystem System;
        public InputButton button;
        public PointerEventData(EventSystem events) { System = events; }
    }
}
public static class DebugHandler
{
    public static bool InstantValue;
    public static bool ThrowInstant;
    public static int InstantReads;
    public static bool InstantBuildMode { get { InstantReads++; if (ThrowInstant) throw new InvalidOperationException("instant mode unavailable fixture"); return InstantValue; } }
}
public sealed class MinionAssignablesProxy
{
    public UnityEngine.GameObject Target;
    public UnityEngine.GameObject GetTargetGameObject() { return Target; }
}
public sealed class ManagementMenu
{
    public static ManagementMenu Instance;
    private SkillsScreen skillsScreen;
    public int OpenCalls, IsOpenReads;
    public bool Open = true;
    public Action<MinionIdentity> OnOpen;
    public ManagementMenu(SkillsScreen screen) { skillsScreen = screen; }
    public bool IsScreenOpen(SkillsScreen screen) { IsOpenReads++; return ReferenceEquals(screen, skillsScreen) && Open; }
    public void OpenSkills(MinionIdentity target) { OpenCalls++; if (OnOpen != null) OnOpen(target); }
    public SkillsScreen FixtureScreen() { return skillsScreen; }
}
public sealed class SkillsScreen : KScreen
{
    private Dictionary<string, UnityEngine.GameObject> skillWidgets = new Dictionary<string, UnityEngine.GameObject>();
    public object CurrentlySelectedMinion;
    public UnityEngine.Transform transform { get { return gameObject.transform; } }
    public void Add(string slot, SkillWidget widget)
    {
        widget.gameObject.Components[typeof(SkillWidget)] = widget;
        widget.gameObject.transform.Parent = transform;
        skillWidgets[slot] = widget.gameObject;
    }
    public void SetWidgetsMissing() { skillWidgets = null; }
}
public sealed class SkillWidget
{
    private SkillsScreen skillsScreen;
    public readonly UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public string skillID;
    public int ClickCalls;
    public UnityEngine.EventSystems.PointerEventData LastEvent;
    public Action OnClick;
    public SkillWidget(SkillsScreen owner, string id) { skillsScreen = owner; skillID = id; }
    public SkillsScreen FixtureOwner() { return skillsScreen; }
    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData events) { ClickCalls++; LastEvent = events; if (OnClick != null) OnClick(); }
}

public class KScreen
{
    public bool Active = true, Modal;
    public bool ThrowActive;
    public readonly UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public bool IsScreenActive() { if (ThrowActive) throw new InvalidOperationException("native screen state unavailable fixture"); return Active; }
    public bool IsModal() { return Modal; }
}
public sealed class KScreenManager
{
    public static KScreenManager Instance;
    private static bool inputDisabled;
    private List<KScreen> screenStack;
    public KScreenManager(params KScreen[] screens) { screenStack = new List<KScreen>(screens); }
    public static void SetFixtureInputDisabled(bool value) { inputDisabled = value; }
    public static bool FixtureInputDisabled() { return inputDisabled; }
    public void SetStackMissing() { screenStack = null; }
    public List<KScreen> FixtureStack() { return screenStack; }
}

// The installed game also defines a global EventSystem. Deliberately retain
// that type-name collision without implementing its unrelated event mechanism.
public sealed class EventSystem { }
