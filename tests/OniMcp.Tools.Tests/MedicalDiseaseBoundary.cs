using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;

// Only native inputs and unrelated routes are substituted. Actual medical callers are linked whole.
internal static class MedicalBoundary
{
    internal static bool InRead;
    internal static int ForbiddenCalls, SicknessReads, NameReads, SymptomReads, ExposureReads;
    internal static Exception Forbidden(string name) { ForbiddenCalls++; return new InvalidOperationException("Unexpected medical native mutation/factory/unrelated route: " + name); }
    internal static void Reset() { ForbiddenCalls = SicknessReads = NameReads = SymptomReads = ExposureReads = 0; }
}
internal partial class MinionIdentity
{
    internal T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
    internal string GetProperName() { return gameObject.GetProperName(); }
    internal int GetMyWorldId() { return gameObject.GetMyWorldId(); }
    internal bool IsNull() { return false; }
}
namespace UnityEngine
{
    internal partial class GameObject
    {
        internal Klei.AI.Sicknesses ExistingSicknesses;
        internal Func<Klei.AI.Sicknesses> SicknessReader;
        internal int MedicalSicknessReads;
        internal string name { get { return NativeName; } }
        internal int GetInstanceID() { return GetComponent<KPrefabID>()?.InstanceID ?? -1; }
    }
    internal static class Mathf { internal static float Clamp(float v,float min,float max) { return Math.Max(min,Math.Min(max,v)); } }
}
namespace Klei.AI
{
    internal static class MedicalSicknessExtensions
    {
        internal static Sicknesses GetSicknesses(this GameObject go)
        { MedicalBoundary.SicknessReads++; go.MedicalSicknessReads++; return go.SicknessReader == null ? go.ExistingSicknesses : go.SicknessReader(); }
    }
    internal sealed class Sicknesses
    {
        internal Sicknesses() { if (MedicalBoundary.InRead) throw MedicalBoundary.Forbidden("Sicknesses constructor"); }
        internal List<SicknessInstance> ExistingList = new List<SicknessInstance>();
        internal Func<List<SicknessInstance>> ListReader;
        internal List<SicknessInstance> ModifierList { get { return ListReader == null ? ExistingList : ListReader(); } }
        internal void Add(SicknessInstance item) { throw MedicalBoundary.Forbidden("infection Add"); }
        internal void Cleanup() { throw MedicalBoundary.Forbidden("sickness Cleanup"); }
    }
    internal sealed class SicknessInstance
    {
        internal SicknessInstance() { if (MedicalBoundary.InRead) throw MedicalBoundary.Forbidden("SicknessInstance constructor"); }
        internal Sickness ExistingSickness;
        internal Func<Sickness> SicknessReader;
        internal Sickness Sickness { get { return SicknessReader == null ? ExistingSickness : SicknessReader(); } }
        internal SicknessExposureInfo ExistingExposure = new SicknessExposureInfo { sourceInfo = "Observed exposure" };
        internal Func<SicknessExposureInfo> ExposureReader;
        internal SicknessExposureInfo ExposureInfo { get { MedicalBoundary.ExposureReads++; return ExposureReader == null ? ExistingExposure : ExposureReader(); } }
        internal void Cure() { throw MedicalBoundary.Forbidden("Cure"); }
    }
    internal abstract class Sickness
    {
        internal HashedString id;
        private StringKey name;
        private StringKey descriptiveSymptoms;
        protected Sickness(HashedString id, StringKey nameKey, StringKey symptomKey)
        { if (MedicalBoundary.InRead) throw MedicalBoundary.Forbidden("Sickness constructor"); this.id = id; name = nameKey; descriptiveSymptoms = symptomKey; }
        internal string Name { get { MedicalBoundary.NameReads++; throw MedicalBoundary.Forbidden("Sickness.Name formatting " + name.Key); } }
        internal Func<StringKey> SymptomKeyReader;
        internal StringKey DescriptiveSymptoms { get { MedicalBoundary.SymptomReads++; return SymptomKeyReader == null ? descriptiveSymptoms : SymptomKeyReader(); } }
    }
    internal sealed class MedicalFixtureSickness : Sickness
    { internal MedicalFixtureSickness(HashedString id,StringKey name,StringKey symptoms) : base(id,name,symptoms) { } }
    internal sealed class Amounts { internal AmountInstance Get(Amount definition) { throw MedicalBoundary.Forbidden("unrelated legacy Amounts.Get"); } }
}
internal sealed class Health { internal float hitPoints { get; set; } = 100; internal float maxHitPoints { get; set; } = 100; }
internal static partial class Components
{
    internal static class Clinics { internal static readonly List<Clinic> Items = new List<Clinic>(); }
    internal static class AssignableItems { internal static readonly List<Assignable> Items = new List<Assignable>(); }
}
internal partial class Db { internal NativeAssignableSlots AssignableSlots { get { throw MedicalBoundary.Forbidden("unrelated bed Db slots"); } } }
internal sealed class NativeAssignableSlots { internal NativeAssignableSlot MedicalBed { get { throw MedicalBoundary.Forbidden("medical slot"); } } }
internal sealed class NativeAssignableSlot { internal string Id { get { throw MedicalBoundary.Forbidden("medical slot ID"); } } }
namespace Database { internal sealed partial class Amounts { internal Klei.AI.Amount Stress { get; set; } internal Klei.AI.Amount RadiationBalance { get; set; } } }
internal sealed partial class Game { internal static bool IsDlcActiveForCurrentSave(string id) { return false; } }
internal interface ISliderControl
{
    float GetSliderMin(int index); float GetSliderMax(int index); void SetSliderValue(float value,int index); float GetSliderValue(int index); string GetSliderTooltip(int index);
}
internal sealed class Clinic : ISliderControl
{
    internal GameObject gameObject { get; set; }
    internal T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
    internal float MedicalAttentionMinimum { get { throw MedicalBoundary.Forbidden("clinic minimum"); } }
    public float GetSliderMin(int i) { throw MedicalBoundary.Forbidden("clinic slider minimum"); }
    public float GetSliderMax(int i) { throw MedicalBoundary.Forbidden("clinic slider maximum"); }
    public void SetSliderValue(float v,int i) { throw MedicalBoundary.Forbidden("clinic slider write"); }
    public float GetSliderValue(int i) { throw MedicalBoundary.Forbidden("clinic slider value"); }
    public string GetSliderTooltip(int i) { throw MedicalBoundary.Forbidden("clinic tooltip"); }
}
internal sealed class Assignable
{
    internal GameObject gameObject { get; set; }
    internal string slotID { get; set; }
    internal MinionIdentity assignee { get; set; }
    internal void Assign(MinionIdentity dupe) { throw MedicalBoundary.Forbidden("Assign"); }
    internal void Unassign() { throw MedicalBoundary.Forbidden("Unassign"); }
}
internal struct PrioritySetting { internal object priority_class { get; set; } internal int priority_value { get; set; } }
internal sealed class Prioritizable { internal PrioritySetting GetMasterPriority() { throw MedicalBoundary.Forbidden("unrelated priority"); } }
internal sealed class DoctorStation
{
    internal GameObject gameObject { get; set; }
    internal NativeMedicalStorage storage { get; set; }
    internal bool IsTreatmentAvailable(GameObject go) { throw MedicalBoundary.Forbidden("treatment evaluation"); }
    internal bool IsDoctorAvailable(GameObject go) { throw MedicalBoundary.Forbidden("doctor evaluation"); }
}
internal sealed class NativeMedicalStorage { internal List<GameObject> items { get; set; } = new List<GameObject>(); }
internal sealed class MedicinalPill { internal NativePillInfo info { get; set; } }
internal sealed class NativePillInfo { internal List<string> curedSicknesses { get; set; } }
namespace OniMcp.Tools
{
    public static partial class ToolUtil
    {
        internal static float? GetFloat(JObject args,string key) { return args[key]?.Value<float?>(); }
        internal static int ResolveWorldId(JObject args) { return GetInt(args,"worldId") ?? 0; }
        internal static Dictionary<string,int> GetRect(JObject args) { throw MedicalBoundary.Forbidden("unrelated rect resolution"); }
        internal static bool CellMatchesWorld(int cell,int world) { throw MedicalBoundary.Forbidden("unrelated cell world"); }
        internal static MinionIdentity FindDupe(JObject args) { throw MedicalBoundary.Forbidden("unrelated assignment target"); }
    }
    internal static class ScheduleTools { internal static McpTool ControlSchedule() { throw MedicalBoundary.Forbidden("schedule route"); } }
    internal static class DietTools { internal static McpTool ControlDiet() { throw MedicalBoundary.Forbidden("diet route"); } }
    internal static class ResearchTools { internal static McpTool ControlResearch() { throw MedicalBoundary.Forbidden("research route"); } }
}

internal struct SicknessExposureInfo { public string sicknessID; public string sourceInfo; }
internal struct HashedString
{
    internal int RawHash;
    internal HashedString(int value) { RawHash = value; }
    public bool IsValid { get { return RawHash != 0; } }
    public int HashValue { get { return RawHash; } set { RawHash = value; } }
    public override string ToString() { return "0x" + RawHash.ToString("X"); }
}
internal struct StringKey
{
    internal readonly string Key;
    internal StringKey(string key) { if (MedicalBoundary.InRead) throw MedicalBoundary.Forbidden("StringKey constructor"); Key = key; }
    public override string ToString() { throw MedicalBoundary.Forbidden("StringKey.ToString formatting"); }
}
internal sealed class StringEntry { public string String; }
internal static class Strings
{
    internal static readonly Dictionary<string,StringEntry> Entries = new Dictionary<string,StringEntry>();
    internal static Func<StringKey,StringEntry> EntryReader;
    internal static int TryGetReads, GetReads;
    public static bool TryGet(StringKey key,out StringEntry result)
    {
        TryGetReads++;
        if (EntryReader != null) { result = EntryReader(key); return result != null; }
        return Entries.TryGetValue(key.Key,out result);
    }
    internal static StringEntry Get(StringKey key) { GetReads++; throw MedicalBoundary.Forbidden("Strings.Get formatter/fallback"); }
}

internal static partial class Grid { internal static int[] WorldIdx { get { throw MedicalBoundary.Forbidden("unrelated world grid"); } } }

internal partial class BuildingComplete { internal T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); } }
