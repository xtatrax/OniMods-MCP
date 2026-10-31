using System;
using System.Collections.Generic;
using System.Linq;

// Deliberate host input boundaries. These are not ONI runtime or game rule proofs.
internal static class Boundary
{
    internal static int DbGetCalls, DbInitializationAttempts, GetOrAddCalls, UiCalls, MaterialCalls;
    internal static Action MaterialRead;
    internal static void Reset() { DbGetCalls = DbInitializationAttempts = GetOrAddCalls = UiCalls = MaterialCalls = 0; MaterialRead = null; }
    internal static InvalidOperationException Forbidden(string operation)
    { return new InvalidOperationException("Unexpected native mutation/init/UI boundary: " + operation); }
}

namespace UnityEngine
{
    public class GameObject
    {
        public T GetComponent<T>() where T : class { Boundary.UiCalls++; throw Boundary.Forbidden("GetComponent"); }
    }
}
public struct Tag : IEquatable<Tag>
{
    public string Name;
    public Tag(string name) { Name = name; }
    public bool Equals(Tag other) { return Name == other.Name; }
    public override bool Equals(object other) { return other is Tag && Equals((Tag)other); }
    public override int GetHashCode() { return Name == null ? 0 : Name.GetHashCode(); }
    public static bool operator ==(Tag a, object b) { return b is Tag && a.Equals((Tag)b); }
    public static bool operator !=(Tag a, object b) { return !(a == b); }
    public override string ToString() { return Name; }
}
public enum Orientation { Neutral, R90, R180, R270, FlipH, FlipV, NumRotations }
public enum BuildLocationRule { Anywhere }
public class BuildingDef
{
    public string PrefabID, Name, Desc, Category;
    public string[] SearchTerms = new string[0], MaterialCategory = new string[0], AvailableFacades;
    public int WidthInCells = 1, HeightInCells = 1;
    public bool RequiresPowerInput;
    public float EnergyConsumptionWhenActive;
    public BuildLocationRule BuildLocationRule;
    public bool NativeAvailable = true;
    public int AvailabilityCalls;
    public Func<bool> AvailabilityPredicate;
    public bool IsAvailable() { AvailabilityCalls++; return AvailabilityPredicate == null ? NativeAvailable : AvailabilityPredicate(); }
}
public static class Assets
{
    public static List<BuildingDef> BuildingDefs = new List<BuildingDef>();
    public static bool MissingLookup;
    public static BuildingDef GetBuildingDef(string id)
    { return MissingLookup ? null : BuildingDefs.FirstOrDefault(d => d != null && string.Equals(d.PrefabID, id, StringComparison.OrdinalIgnoreCase)); }
}
public class ClusterManager { public static ClusterManager Instance; public int activeWorldId; }
public class Game { public static Game Instance; public bool SandboxModeActive; }
public static class DebugHandler { public static bool InstantBuildMode; }
public class Prioritizable { public void SetMasterPriority(PrioritySetting setting) { Boundary.UiCalls++; throw Boundary.Forbidden("SetMasterPriority"); } }
public class PrioritySetting { public PrioritySetting(PriorityScreen.PriorityClass p, int value) { } }
public class PriorityScreen { public enum PriorityClass { basic } }
public class Element { public string id, name; }
public static class ElementLoader { public static List<Element> elements = new List<Element>(); }

public class Db
{
    // Same typed cache as the reader; Get permits cached reads but guards initialization.
    private static Db _Instance;
    internal static Db CachedForFixture { get { return _Instance; } }
    public Database.Techs Techs;
    public Database.TechItems TechItems;
    public PermitSet Permits;
    public static void Seed(Db db) { _Instance = db; }
    public static Db Get()
    {
        Boundary.DbGetCalls++;
        if (_Instance != null) return _Instance;
        Boundary.DbInitializationAttempts++; throw Boundary.Forbidden("Db.Get initialization");
    }
    public static FacadeSet GetBuildingFacades() { Boundary.DbGetCalls++; throw Boundary.Forbidden("Db.GetBuildingFacades"); }
}
namespace Database
{
    public class Techs
    {
        public List<Tech> resources = new List<Tech>();
        public Func<string, bool> Predicate;
        public int PredicateCalls;
        public string LastPrefabId;
        public Tech Get(string id) { return resources.FirstOrDefault(tech => tech.Id == id); }
        public bool IsTechItemComplete(string prefabId)
        {
            PredicateCalls++; LastPrefabId = prefabId;
            if (Predicate != null) return Predicate(prefabId);
            // Fixture boundary follows the native public contract: no listed tech => true;
            // required research without a started instance => false. Never creates research.
            foreach (var tech in resources)
                foreach (var item in tech.unlockedItems)
                    if (item.Id == prefabId) return tech.IsComplete();
            return true;
        }
    }
    public class Tech
    {
        public string Id;
        public List<string> unlockedItemIDs = new List<string>();
        public List<TechItem> unlockedItems = new List<TechItem>();
        public bool IsComplete() { return Research.Instance != null && (Research.Instance.Get(this)?.IsComplete() ?? false); }
    }
    public class TechItems
    {
        public List<TechItem> resources = new List<TechItem>();
        public Func<string, bool> Predicate;
        public int PredicateCalls;
        public string LastPrefabId;
        public bool IsTechItemComplete(string prefabId)
        {
            PredicateCalls++; LastPrefabId = prefabId;
            if (Predicate != null) return Predicate(prefabId);
            foreach (var item in resources)
                if (item.Id == prefabId) return item.IsComplete();
            return true;
        }
    }
}
public class TechItem
{
    public string Id, parentTechId;
    public bool isPOIUnlock;
    public Database.Tech ParentTech { get { return Db.Get().Techs.Get(parentTechId); } }
    // Native input boundary includes the POI distinction that Techs alone omits.
    public bool IsComplete()
    {
        if (ParentTech.IsComplete()) return true;
        if (!isPOIUnlock) return false;
        var existing = Research.Instance.Get(ParentTech);
        return existing != null && existing.UnlockedPOITechIds.Contains(Id);
    }
}
public class TechInstance
{
    public Database.Tech tech;
    public bool Complete;
    public List<string> UnlockedPOITechIds = new List<string>();
    public bool IsComplete() { return Complete; }
}
public class Research
{
    public static Research Instance;
    public List<TechInstance> Existing = new List<TechInstance>();
    public int GetCalls;
    public TechInstance Get(Database.Tech tech) { GetCalls++; return Existing.FirstOrDefault(x => ReferenceEquals(x.tech, tech)); }
    public TechInstance GetOrAdd(Database.Tech tech) { Boundary.GetOrAddCalls++; throw Boundary.Forbidden("Research.GetOrAdd"); }
}
public class PermitSet { public Permit TryGet(string id) { Boundary.UiCalls++; throw Boundary.Forbidden("Permit lookup"); } }
public class Permit { public bool IsUnlocked() { Boundary.UiCalls++; throw Boundary.Forbidden("Permit eligibility"); } }
public class FacadeSet { public Facade TryGet(string id) { Boundary.UiCalls++; throw Boundary.Forbidden("Facade lookup"); } }
public class Facade { public string Name; }
