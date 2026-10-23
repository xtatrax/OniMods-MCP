using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Native input boundaries only. Product reader, routing and serializer are linked whole.
internal static class FreshnessBoundary
{
    internal static bool GuardNativeFactory, InRead;
    internal static int ForbiddenCalls, SmiLookups, AmountLookups, FormattingCalls;
    internal static InvalidOperationException Forbidden(string operation)
    { ForbiddenCalls++; return new InvalidOperationException("Unexpected native write/factory/formatter: " + operation); }
    internal static void ResetCounters() { ForbiddenCalls = SmiLookups = AmountLookups = FormattingCalls = 0; }
}
internal struct Tag : IEquatable<Tag>
{
    internal string Name { get; set; }
    internal Tag(string name) { Name = name; }
    public bool Equals(Tag other) { return Name == other.Name; }
    public override bool Equals(object other) { return other is Tag && Equals((Tag)other); }
    public override int GetHashCode() { return Name == null ? 0 : Name.GetHashCode(); }
    public override string ToString() { return Name; }
}
internal static class GameTags
{
    internal static readonly Tag Preserved = new Tag("Preserved"), Entombed = new Tag("Entombed");
    internal static class RotModifierTags
    { internal static readonly Tag Fresh = new Tag("Fresh"), Stale = new Tag("Stale"), Refrigerated = new Tag("Refrigerated"), DeepFrozen = new Tag("DeepFrozen"); }
}
internal sealed class KPrefabID
{
    internal int InstanceID { get; set; }
    internal Tag PrefabTag { get; set; }
    internal GameObject gameObject { get; set; }
    internal HashSet<Tag> Tags = new HashSet<Tag>();
    internal Func<Tag, bool> TagReader;
    internal bool HasTag(Tag tag) { return TagReader == null ? Tags.Contains(tag) : TagReader(tag); }
}
namespace UnityEngine
{
    internal partial class GameObject
    {
        internal readonly Dictionary<Type, object> ExistingComponents = new Dictionary<Type, object>();
        internal Func<Type, object> ComponentReader = null;
        internal object ExistingSmi;
        internal Func<object> SmiReader;
        internal Klei.AI.Amount BoundAmountDefinition;
        internal Klei.AI.AmountInstance BoundAmount;
        internal Func<Klei.AI.AmountInstance> AmountReader;
        internal int SmiLookupReads, AmountLookupReads, EdibleLookupReads;
        internal int WorldId { get; set; }
        internal string NativeName { get; set; } = "Fixture entity";
        internal T ExistingComponent<T>() where T : class
        {
            if (typeof(T) == typeof(Edible)) EdibleLookupReads++;
            if (ComponentReader != null) return ComponentReader(typeof(T)) as T;
            object component;
            return ExistingComponents.TryGetValue(typeof(T), out component) ? component as T : null;
        }
        internal T[] GetComponents<T>() where T : class
        { var one = GetComponent<T>(); return one == null ? new T[0] : new[] { one }; }
        internal T GetSMI<T>() where T : class
        { SmiLookupReads++; FreshnessBoundary.SmiLookups++; return (SmiReader == null ? ExistingSmi : SmiReader()) as T; }
        internal string GetProperName() { return NativeName; }
        internal int GetMyWorldId() { return WorldId; }
        internal void AddComponent<T>() { throw FreshnessBoundary.Forbidden("AddComponent"); }
        internal void RequireComponent<T>() { throw FreshnessBoundary.Forbidden("RequireComponent"); }
    }
}
namespace OniMcp.Tools
{
    public static partial class ToolUtil
    { internal static bool GameObjectMatchesWorld(GameObject go, int world) { return world < 0 || go.GetMyWorldId() == world; } }
    internal static class DuplicantTools
    { internal static object ReadSectionCurrentChore(MinionIdentity dupe) { throw FreshnessBoundary.Forbidden("unrelated chore read"); } }
}
internal partial class BuildingDef { public string Desc = null, Effect = null; }
internal partial class BuildingComplete { internal double creationTime { get; set; } }
internal partial class MinionIdentity
{
    internal string personalityResourceId { get; set; } = "Fixture";
    internal string gender { get; set; } = "Fixture";
    internal Tag model { get; set; } = new Tag("Fixture");
    internal double arrivalTime { get; set; }
}
internal sealed class Operational
{
    internal bool IsFunctional { get; set; }
    internal bool IsOperational { get; set; }
    internal bool IsActive { get; set; }
    internal float GetCurrentCycleUptime() { return 0; }
    internal float GetLastCycleUptime() { return 0; }
    internal float GetUptimeOverCycles(int count) { return 0; }
}
internal sealed class Battery { internal float JoulesAvailable { get; set; } internal float Capacity { get; set; } }
internal sealed class GameClock
{ internal static GameClock Instance { get; set; } = new GameClock(); internal double GetTime() { return 0; } }
internal class Workable
{
    internal GameObject worker { get; set; }
    internal float WorkTimeRemaining { get; set; }
    internal float WorkTimeValue { get; set; }
    internal float PercentCompleteValue { get; set; }
    internal float GetWorkTime() { return WorkTimeValue; }
    internal float GetPercentComplete() { return PercentCompleteValue; }
}
internal sealed class Studyable : Workable
{
    internal bool StudiedValue;
    internal Func<bool> StudiedReader;
    internal int StudiedReads;
    internal bool Studied { get { StudiedReads++; return StudiedReader == null ? StudiedValue : StudiedReader(); } }
}
internal sealed class Descriptor { public string text = null, tooltipText = null; public int type = 0, indent = 0; }
internal sealed class KSelectable { internal List<StatusEntry> GetStatusItemGroup() { return new List<StatusEntry>(); } }
internal sealed class StatusEntry { internal StatusItem item { get; set; } internal object data { get; set; } }
internal sealed class StatusItem
{ internal string Id { get; set; } internal string GetName(object data) { return Id; } internal string GetTooltip(object data) { return Id; } }
internal static class GameUtil
{
    internal static List<Descriptor> GetAllDescriptors(GameObject go, bool flag) { FreshnessBoundary.FormattingCalls++; return new List<Descriptor>(); }
    internal static List<Descriptor> GetRequirementDescriptors(List<Descriptor> source) { return source; }
    internal static List<Descriptor> GetEffectDescriptors(List<Descriptor> source) { return source; }
}
internal sealed class PrimaryElement
{
    internal float Mass { get; set; }
    internal SimHashes ElementID { get; set; }
    internal byte DiseaseIdx { get; set; }
    internal int DiseaseCount { get; set; }
    internal NativeFoodElement Element { get; set; } = new NativeFoodElement();
    internal float TemperatureValue = 280f;
    internal Func<float> TemperatureReader;
    internal int TemperatureReads;
    internal float Temperature { get { TemperatureReads++; return TemperatureReader == null ? TemperatureValue : TemperatureReader(); } }
}
internal sealed class NativeFoodElement
{ public float specificHeatCapacity = 1, thermalConductivity = 1, highTemp = 1000, radiationAbsorptionFactor = 0; }
internal sealed class Edible
{
    internal EdiblesManager.FoodInfo ExistingFood;
    internal Func<EdiblesManager.FoodInfo> FoodReader;
    internal int FoodReads;
    internal EdiblesManager.FoodInfo FoodInfo { get { FoodReads++; return FoodReader == null ? ExistingFood : FoodReader(); } }
}
internal static class EdiblesManager { internal sealed class FoodInfo { internal bool CanRot { get; set; } } }
internal sealed class Rottable
{
    internal sealed class Instance
    {
        internal Instance() { if (FreshnessBoundary.InRead) throw FreshnessBoundary.Forbidden("Rottable.Instance constructor"); }
        internal float FractionValue = 0.75f;
        internal bool RunningValue = true;
        internal Func<float> FractionReader = null;
        internal Func<bool> RunningReader;
        internal int FractionReads, RunningReads;
        internal float RotConstitutionPercentage { get { FractionReads++; return FractionReader == null ? FractionValue : FractionReader(); } }
        internal bool IsRunning() { RunningReads++; return RunningReader == null ? RunningValue : RunningReader(); }
        internal void RefreshModifiers() { throw FreshnessBoundary.Forbidden("RefreshModifiers"); }
        internal void Rot() { throw FreshnessBoundary.Forbidden("Rot"); }
        internal void GoTo() { throw FreshnessBoundary.Forbidden("GoTo"); }
        internal string StateString() { throw FreshnessBoundary.Forbidden("StateString formatter"); }
    }
}
internal sealed class SpeedControlScreen
{
    internal static SpeedControlScreen Instance { get; set; }
    internal bool PausedValue = true;
    internal Func<bool> PausedReader;
    internal bool IsPaused { get { return PausedReader == null ? PausedValue : PausedReader(); } }
}
internal sealed partial class Db
{
    private static Db _Instance;
    internal Database.Amounts Amounts { get; set; }
}
namespace Database { internal sealed partial class Amounts { internal Klei.AI.Amount Rot { get; set; } } }
namespace Klei.AI
{
    internal sealed class Traits { internal List<Trait> TraitList { get; set; } = new List<Trait>(); }
    internal sealed partial class AttributeModifier { internal bool UIOnly { get; set; } }
    internal sealed class Amount
    {
        internal Amount() { if (FreshnessBoundary.InRead) throw FreshnessBoundary.Forbidden("Amount constructor"); }
        internal enum Units { Flat }
        internal Units units = Units.Flat;
        internal AmountInstance Lookup(GameObject go)
        {
            FreshnessBoundary.AmountLookups++; go.AmountLookupReads++;
            if (go.AmountReader != null) return go.AmountReader();
            return ReferenceEquals(this, go.BoundAmountDefinition) ? go.BoundAmount : null;
        }
        internal void Add() { throw FreshnessBoundary.Forbidden("Amount.Add"); }
    }
    internal sealed class AmountInstance
    {
        internal AmountInstance() { if (FreshnessBoundary.InRead) throw FreshnessBoundary.Forbidden("AmountInstance constructor"); }
        internal Amount amount;
        internal AttributeInstance deltaAttribute;
        internal float RawValue = 30, MinValue = 0, MaxValue = 100;
        internal bool PausedValue;
        internal Func<float> ValueReader, MinReader, MaxReader, DeltaReader;
        internal Func<bool> PausedReader;
        internal int ValueReads, MinReads, MaxReads, DeltaReads, PausedReads;
        internal float value { get { ValueReads++; return ValueReader == null ? RawValue : ValueReader(); } }
        internal float GetMin() { MinReads++; return MinReader == null ? MinValue : MinReader(); }
        internal float GetMax() { MaxReads++; return MaxReader == null ? MaxValue : MaxReader(); }
        internal float GetDelta() { DeltaReads++; return DeltaReader == null ? deltaAttribute.GetTotalValue() : DeltaReader(); }
        internal bool paused { get { PausedReads++; return PausedReader == null ? PausedValue : PausedReader(); } }
        internal void SetValue(float value) { throw FreshnessBoundary.Forbidden("SetValue"); }
        internal void ApplyDelta(float value) { throw FreshnessBoundary.Forbidden("ApplyDelta"); }
        internal void Activate() { throw FreshnessBoundary.Forbidden("Activate"); }
    }
    internal sealed class AttributeInstance
    {
        internal float ActualValue = -2, DisplayValue = 20;
        internal List<AttributeModifier> ExistingModifiers = new List<AttributeModifier>();
        internal Func<List<AttributeModifier>> ModifierReader;
        internal int ModifierReads, ActualReads, DisplayReads;
        internal List<AttributeModifier> Modifiers { get { ModifierReads++; return ModifierReader == null ? ExistingModifiers : ModifierReader(); } }
        internal float GetTotalValue() { ActualReads++; return ActualValue; }
        internal float GetTotalDisplayValue() { DisplayReads++; return DisplayValue; }
        internal void ClearModifiers() { throw FreshnessBoundary.Forbidden("ClearModifiers"); }
        internal void Add(AttributeModifier modifier) { throw FreshnessBoundary.Forbidden("modifier Add"); }
    }
}
