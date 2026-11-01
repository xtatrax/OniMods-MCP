using System;
using System.Collections.Generic;
using System.Linq;

// Deliberate host input boundaries. These are not ONI runtime or game rule proofs.
internal static class Boundary
{
    internal static int DbGetCalls, DbInitializationAttempts, GetOrAddCalls, UiCalls, MaterialCalls;
    internal static int UtilityGuardCalls, VirtualContextReads, MissingParentLookups, PermitReads;
    internal static bool ExactUtility, VirtualEditContext;
    internal static Action MaterialRead;
    internal static void Reset() { DbGetCalls = DbInitializationAttempts = GetOrAddCalls = UiCalls = MaterialCalls = 0; UtilityGuardCalls = VirtualContextReads = 0; ExactUtility = VirtualEditContext = false; MaterialRead = null; }
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
    public bool IsValid {get{return !string.IsNullOrEmpty(Name);}}
    public static Tag Invalid {get{return new Tag(null);}}
    public string ProperNameStripLink(){return Name;}
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
    public float[] Mass=new[]{200f};
    public List<Tag> Defaults=new List<Tag>();
    public List<Tag> DefaultElements(){return Defaults;}
    public bool IsAvailable() { AvailabilityCalls++; return AvailabilityPredicate == null ? NativeAvailable : AvailabilityPredicate(); }
}
public static class Assets
{
    public static List<BuildingDef> BuildingDefs = new List<BuildingDef>();
    public static bool MissingLookup;
    public static UnityEngine.GameObject GetPrefab(Tag tag){return null;}
    public static BuildingDef GetBuildingDef(string id)
    { return MissingLookup ? null : BuildingDefs.FirstOrDefault(d => d != null && string.Equals(d.PrefabID, id, StringComparison.OrdinalIgnoreCase)); }
}
public class ClusterManager { public static ClusterManager Instance; public int activeWorldId; public WorldContainer World; public WorldContainer GetWorld(int id){return World;} }
public class WorldContainer {public int id;public WorldInventory worldInventory;}
public class WorldInventory {public float NativeAmount=100; public int Reads;public float GetTotalAmount(Tag tag,bool includeRelatedWorlds){Reads++;return NativeAmount;}}
public class Game { public static Game Instance; public bool SandboxModeActive; }
public static class DebugHandler {
 private static bool instant;
 public static readonly List<bool> Writes=new List<bool>();
 public static bool InstantBuildMode {get{return instant;}set{Writes.Add(value);instant=value;}}
 public static void Seed(bool value){instant=value;Writes.Clear();}
}
public class Prioritizable { public void SetMasterPriority(PrioritySetting setting) { Boundary.UiCalls++; throw Boundary.Forbidden("SetMasterPriority"); } }
public class PrioritySetting { public PrioritySetting(PriorityScreen.PriorityClass p, int value) { } }
public class PriorityScreen { public enum PriorityClass { basic } }
public class Element { public string id, name; public Tag Category;public Tag GetMaterialCategoryTag(){return Category;}public bool HasTag(Tag tag){return Category==tag;} }
public static class ElementLoader { public static List<Element> elements = new List<Element>();public static Element GetElement(Tag tag){return elements.FirstOrDefault(value=>value.id==tag.Name);} }

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
        public Tech Get(string id) { var value=resources.FirstOrDefault(tech => tech.Id == id); if(value==null) Boundary.MissingParentLookups++; return value; }
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
public class PermitSet
{
    public List<Database.PermitResource> resources = new List<Database.PermitResource>();
    public FacadeSet BuildingFacades = new FacadeSet();
    public Database.PermitResource TryGet(string id) { return resources.FirstOrDefault(value=>value.Id==id); }
}
namespace Database
{
    public enum PermitRarity { Unknown, Universal, UniversalLocked, Loyalty, Common }
    public class PermitResource
    {
        public string Id; public PermitRarity Rarity=PermitRarity.Common; public bool NativeUnlocked;
        public bool IsUnlocked() { Boundary.PermitReads++; return NativeUnlocked; }
    }
}
public class FacadeSet { public List<Facade> resources=new List<Facade>(); public Facade TryGet(string id) {return resources.FirstOrDefault(value=>value.Id==id);} }
public class Facade { public string Id,Name; }
public class KleiItems
{
    public struct Inventory { public Dictionary<string,int> ItemsByType; }
    private static Inventory InventoryData;
    public static void Ready(bool ready) { InventoryData=new Inventory { ItemsByType=ready?new Dictionary<string,int>():null }; }
}

public class KPrefabID {public Tag PrefabTag;public List<Tag> Tags=new List<Tag>();public bool HasTag(Tag tag){return Tags.Contains(tag);}}
public class PrimaryElement {public float Mass;public string ElementID;}
public class Pickupable {public UnityEngine.GameObject gameObject=new UnityEngine.GameObject();public int cachedCell;public object storage;public KPrefabID KPrefabID;public PrimaryElement PrimaryElement;public T GetComponent<T>() where T:class{return null;}public int GetMyWorldId(){return 0;}}
public static class Components {public class Collection<T>{public List<T> Items=new List<T>();}public static Collection<Pickupable> Pickupables=new Collection<Pickupable>();public static Collection<BuildingComplete> BuildingCompletes=new Collection<BuildingComplete>();}
public static class Grid {public static int[] WorldIdx=new int[64];public static bool IsValidCell(int cell){return cell>=0&&cell<64;}public static bool IsWorldValidCell(int cell){return IsValidCell(cell);}}
public static class GameTags {public static Tag Stored=new Tag("Stored");}
public class DiscoveredResources {public static DiscoveredResources Instance;public List<Tag> Tags=new List<Tag>();public IEnumerable<Tag> GetDiscoveredResourcesFromTag(Tag category){return Tags;}}

public class BuildingComplete {public UnityEngine.GameObject gameObject;}
public class Constructable {public UnityEngine.GameObject gameObject;}
public enum FindObjectsSortMode {None}
namespace UnityEngine {public class Object {public static T[] FindObjectsByType<T>(FindObjectsSortMode mode){return new T[0];}}}
