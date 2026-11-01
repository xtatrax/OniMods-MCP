using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Support;
#pragma warning disable CS0649

// Input/cache shapes only. SelectElements/AvailableMaterials and their nested
// production records are never replaced in this boundary.
internal static class Boundary
{
    internal static int InventoryReads, DiscoveryReads, PickupEnumerations, ForbiddenCalls;
    internal static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal);

    internal static void Reset()
    {
        InventoryReads = DiscoveryReads = PickupEnumerations = ForbiddenCalls = 0;
        Names.Clear();
        ElementLoader.Elements.Clear();
        Assets.Prefabs.Clear();
        Components.Pickupables.Items.Reset();
        DiscoveredResources.Instance = new DiscoveredResources();
        ClusterManager.Instance = new ClusterManager { activeWorldId = 0 };
        ClusterManager.Instance.Worlds[0] = new WorldContainer { id = 0, worldInventory = new WorldInventory() };
        Game.Instance = new Game();
        DebugHandler.InstantBuildMode = false;
        OniMcp.Tools.WorldEditorTools.EnforceNormalMaterialRules = false;
        for (int i = 0; i < Grid.WorldIdx.Length; i++) Grid.WorldIdx[i] = -1;
        Grid.WorldIdx[2] = 0;
        Grid.WorldIdx[3] = 1;
    }

    internal static InvalidOperationException Forbidden(string operation)
    {
        ForbiddenCalls++;
        return new InvalidOperationException("Forbidden native/UI/write boundary: " + operation);
    }

    internal static void AddMaterial(BuildingDef def, string id, string name, float mass, string category = "Metal")
    {
        Tag tag = new Tag(id);
        def.Defaults.Add(tag);
        Names[id] = name;
        ElementLoader.Elements[id] = new Element { Category = new Tag(category) };
        ClusterManager.Instance.Worlds[0].worldInventory.Amounts[id] = mass;
    }

    internal static BuildingDef BaseDefinition()
    {
        var def = new BuildingDef { PrefabID = "FixtureBuilding", MaterialCategory = new[] { "Metal" }, Mass = new[] { 100f } };
        AddMaterial(def, "MatA", "Alpha", 200f);
        AddMaterial(def, "MatB", "Beta", 100f);
        AddMaterial(def, "MatZ", "Zero", 0f);
        return def;
    }

    internal static void SetAllMasses(float value)
    {
        var inventory = ClusterManager.Instance.Worlds[0].worldInventory;
        foreach (string key in inventory.Amounts.Keys.ToArray()) inventory.Amounts[key] = value;
    }

    internal static Pickupable Pickup(string id, float mass, bool stored, bool storedTag, int cell)
    {
        var prefab = new KPrefabID { PrefabTag = new Tag(id) };
        prefab.Tags.Add(new Tag(id));
        prefab.Tags.Add(new Tag("Metal"));
        if (storedTag) prefab.Tags.Add(GameTags.Stored);
        var primary = new PrimaryElement { ElementID = id, Mass = mass };
        var gameObject = new UnityEngine.GameObject();
        gameObject.Add(prefab).Add(primary);
        var item = new Pickupable { gameObject = gameObject, KPrefabID = prefab, PrimaryElement = primary, cachedCell = cell,
            storage = stored ? new object() : null, World = Grid.WorldIdx[cell] };
        Components.Pickupables.Items.Add(item);
        return item;
    }

    internal static string SnapshotInputs(BuildingDef def)
    {
        // Reads fixture backing stores directly; never calls the counted getter/enumerator.
        var worlds = ClusterManager.Instance == null ? null : ClusterManager.Instance.Worlds.OrderBy(p => p.Key).Select(p => new {
            id = p.Key, amounts = p.Value.worldInventory == null ? null : p.Value.worldInventory.Amounts.OrderBy(a => a.Key).ToArray()
        }).ToArray();
        var items = Components.Pickupables.Items.Raw.Select(p => new {
            cell = p.cachedCell, world = p.World, stored = p.storage != null,
            tag = p.KPrefabID == null ? null : p.KPrefabID.PrefabTag.Name,
            tags = p.KPrefabID == null ? null : p.KPrefabID.Tags.Select(t => t.Name).OrderBy(t => t).ToArray(),
            element = p.PrimaryElement == null ? null : p.PrimaryElement.ElementID,
            mass = p.PrimaryElement == null ? (float?)null : p.PrimaryElement.Mass
        }).ToArray();
        return JsonConvert.SerializeObject(new {
            defaults = def.Defaults.Select(t => t.Name).ToArray(), categories = def.MaterialCategory, masses = def.Mass,
            names = Names.OrderBy(p => p.Key).ToArray(),
            elements = ElementLoader.Elements.OrderBy(p => p.Key).Select(p => new { id = p.Key, category = p.Value.Category.Name, tags = p.Value.ExtraTags.Select(t => t.Name).OrderBy(t => t).ToArray() }).ToArray(),
            prefabTags = Assets.Prefabs.OrderBy(p => p.Key.Name).Select(p => new { id = p.Key.Name, tags = p.Value.GetComponent<KPrefabID>().Tags.Select(t => t.Name).OrderBy(t => t).ToArray() }).ToArray(),
            discovery = DiscoveredResources.Instance == null ? null : DiscoveredResources.Instance.Existing.OrderBy(p => p.Key).Select(p => new { key = p.Key, tags = p.Value.Select(t => t.Name).ToArray() }).ToArray(),
            worlds, items, grid = Grid.WorldIdx.ToArray(),
            activeWorld = ClusterManager.Instance == null ? (int?)null : ClusterManager.Instance.activeWorldId,
            discoveryThrows = DiscoveredResources.Instance != null && DiscoveredResources.Instance.ThrowOnRead,
            sandbox = Game.Instance != null && Game.Instance.SandboxModeActive,
            debug = DebugHandler.InstantBuildMode, normal = OniMcp.Tools.WorldEditorTools.EnforceNormalMaterialRules
        }, McpJsonUtil.Settings);
    }
}

namespace UnityEngine
{
    public sealed class GameObject
    {
        private readonly Dictionary<Type, object> components = new Dictionary<Type, object>();
        public GameObject Add<T>(T value) where T : class { components[typeof(T)] = value; return this; }
        public T GetComponent<T>() where T : class
        { object value; return components.TryGetValue(typeof(T), out value) ? (T)value : null; }
    }
}
public struct Tag : IEquatable<Tag>
{
    public string Name;
    public Tag(string name) { Name = name; }
    public bool IsValid { get { return !string.IsNullOrEmpty(Name); } }
    public static Tag Invalid { get { return new Tag(null); } }
    public bool Equals(Tag other) { return string.Equals(Name, other.Name, StringComparison.Ordinal); }
    public override bool Equals(object other) { return other is Tag && Equals((Tag)other); }
    public override int GetHashCode() { return Name == null ? 0 : StringComparer.Ordinal.GetHashCode(Name); }
    public static bool operator ==(Tag a, Tag b) { return a.Equals(b); }
    public static bool operator !=(Tag a, Tag b) { return !a.Equals(b); }
    public string ProperNameStripLink() { string value; return Boundary.Names.TryGetValue(Name, out value) ? value : Name; }
}
public sealed class BuildingDef
{
    public string PrefabID;
    public string[] MaterialCategory, AvailableFacades;
    public float[] Mass;
    public readonly List<Tag> Defaults = new List<Tag>();
    public List<Tag> DefaultElements() { return Defaults; }
}
public sealed class Element
{
    public Tag Category;
    public readonly HashSet<Tag> ExtraTags = new HashSet<Tag>();
    public Tag GetMaterialCategoryTag() { return Category; }
    public bool HasTag(Tag tag) { return ExtraTags.Contains(tag); }
}
public static class ElementLoader
{
    internal static readonly Dictionary<string, Element> Elements = new Dictionary<string, Element>(StringComparer.Ordinal);
    public static Element GetElement(Tag tag) { Element result; return tag.IsValid && Elements.TryGetValue(tag.Name, out result) ? result : null; }
}
public static class Assets
{
    internal static readonly Dictionary<Tag, UnityEngine.GameObject> Prefabs = new Dictionary<Tag, UnityEngine.GameObject>();
    public static UnityEngine.GameObject GetPrefab(Tag tag) { UnityEngine.GameObject result; return Prefabs.TryGetValue(tag, out result) ? result : null; }
}
public sealed class DiscoveredResources
{
    public static DiscoveredResources Instance;
    public readonly Dictionary<string, List<Tag>> Existing = new Dictionary<string, List<Tag>>(StringComparer.Ordinal);
    public bool ThrowOnRead;
    public IEnumerable<Tag> GetDiscoveredResourcesFromTag(Tag category)
    {
        Boundary.DiscoveryReads++;
        if (ThrowOnRead) throw new InvalidOperationException("fixture unavailable discovery cache");
        List<Tag> tags;
        return Existing.TryGetValue(category.Name, out tags) ? tags : new List<Tag>();
    }
}
public sealed class KPrefabID
{
    public Tag PrefabTag;
    public readonly HashSet<Tag> Tags = new HashSet<Tag>();
    public bool HasTag(Tag tag) { return Tags.Contains(tag); }
}
public sealed class PrimaryElement { public string ElementID; public float Mass; }
public sealed class Pickupable
{
    public UnityEngine.GameObject gameObject;
    public KPrefabID KPrefabID;
    public PrimaryElement PrimaryElement;
    public object storage;
    public int cachedCell, World;
    public int GetMyWorldId() { return World; }
    public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
}
internal sealed class CountingPickupables : IEnumerable<Pickupable>
{
    internal readonly List<Pickupable> Raw = new List<Pickupable>();
    internal void Add(Pickupable item) { Raw.Add(item); }
    internal void Reset() { Raw.Clear(); }
    public IEnumerator<Pickupable> GetEnumerator() { Boundary.PickupEnumerations++; return Raw.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
}
internal static partial class Components
{
    internal static class Pickupables { internal static readonly CountingPickupables Items = new CountingPickupables(); }
}
public static class GameTags { public static readonly Tag Stored = new Tag("Stored"); }
public static class Grid
{
    public static readonly int[] WorldIdx = new int[16];
    public static bool IsValidCell(int cell) { return cell >= 0 && cell < WorldIdx.Length; }
    public static bool IsWorldValidCell(int cell) { return IsValidCell(cell) && WorldIdx[cell] >= 0; }
}
public sealed class WorldInventory
{
    public readonly Dictionary<string, float> Amounts = new Dictionary<string, float>(StringComparer.Ordinal);
    public float GetTotalAmount(Tag tag, bool includeRelatedWorlds)
    {
        Boundary.InventoryReads++;
        if (!includeRelatedWorlds) throw Boundary.Forbidden("unexpected inventory scope flag");
        float amount; return Amounts.TryGetValue(tag.Name, out amount) ? amount : 0f;
    }
}
public sealed class WorldContainer { public int id; public WorldInventory worldInventory; }
public sealed class ClusterManager
{
    public static ClusterManager Instance;
    public int activeWorldId;
    public readonly Dictionary<int, WorldContainer> Worlds = new Dictionary<int, WorldContainer>();
    public WorldContainer GetWorld(int id) { WorldContainer world; return Worlds.TryGetValue(id, out world) ? world : null; }
}
public sealed class Game { public static Game Instance; public bool SandboxModeActive; }
public static class DebugHandler { public static bool InstantBuildMode; }
public sealed class Db
{
    private static Db _Instance;
    internal static void Seed(Db db){_Instance=db;}
    public PermitSet Permits;
    public static Db Get() { throw Boundary.Forbidden("Db.Get / facade initialization"); }
}
public sealed class PermitSet { public List<Permit> resources=new List<Permit>(); public FacadeSet BuildingFacades=new FacadeSet(); public Permit TryGet(string id) { FacadeReadinessInputs.Lookups++;return resources.FirstOrDefault(value=>value.Id==id); } }
public sealed class Permit { public string Id;public Database.PermitRarity Rarity;public bool NativeUnlocked; public bool IsUnlocked() { FacadeReadinessInputs.EligibilityCalls++;return NativeUnlocked; } }
