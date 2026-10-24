using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object left, Object right)
        {
            bool leftNull = ReferenceEquals(left, null) || left.Destroyed;
            bool rightNull = ReferenceEquals(right, null) || right.Destroyed;
            return leftNull || rightNull ? leftNull == rightNull : ReferenceEquals(left, right);
        }
        public static bool operator !=(Object left, Object right) { return !(left == right); }
        public override bool Equals(object other) { return ReferenceEquals(this, other); }
        public override int GetHashCode() { return base.GetHashCode(); }
    }
    public class GameObject : Object
    {
        private readonly Dictionary<Type, Object> components = new Dictionary<Type, Object>();
        public int ComponentReads;
        public void Add<T>(T component) where T : Object { components[typeof(T)] = component; }
        public T GetComponent<T>() where T : Object
        {
            ComponentReads++;
            Object component;
            return components.TryGetValue(typeof(T), out component) ? (T)component : null;
        }
    }
}

public struct Tag : IEquatable<Tag>
{
    private readonly string name;
    public Tag(string value) { name = value; }
    public string Name { get { return name; } }
    public bool IsValid { get { return !string.IsNullOrEmpty(name); } }
    public static Tag Invalid { get { return new Tag(null); } }
    public bool Equals(Tag other) { return StringComparer.Ordinal.Equals(name, other.name); }
    public override bool Equals(object other) { return other is Tag && Equals((Tag)other); }
    public override int GetHashCode() { return name == null ? 0 : StringComparer.Ordinal.GetHashCode(name); }
    public static implicit operator Tag(string value) { return new Tag(value); }
    public static bool operator ==(Tag left, Tag right) { return left.Equals(right); }
    public static bool operator !=(Tag left, Tag right) { return !left.Equals(right); }
}

public static class GameTagExtensions
{
    public static readonly Dictionary<Tag, string> Names = new Dictionary<Tag, string>();
    public static string ProperName(this Tag tag)
    {
        string value;
        return Names.TryGetValue(tag, out value) ? value : tag.Name;
    }
}

public class GeneticAnalysisStation
{
    public class StatesInstance
    {
        private HashSet<Tag> forbiddenSeeds;
        public int GetterCalls;
        public int GetterInitializations;
        public int SetterCalls;
        public int RefreshCalls;
        public StatesInstance(HashSet<Tag> existing) { forbiddenSeeds = existing; }
        public HashSet<Tag> CacheForTest { get { return forbiddenSeeds; } }
        public bool GetSeedForbidden(Tag seed)
        {
            GetterCalls++;
            if (forbiddenSeeds == null)
            {
                forbiddenSeeds = new HashSet<Tag>();
                GetterInitializations++;
            }
            return forbiddenSeeds.Contains(seed);
        }
        public void SetSeedForbidden(Tag seed, bool forbidden)
        {
            SetterCalls++;
            throw new InvalidOperationException("native_setter_must_not_run");
        }
        public void RefreshFetchTags()
        {
            RefreshCalls++;
            throw new InvalidOperationException("native_refresh_must_not_run");
        }
    }
}

public class PlantSubSpeciesCatalog
{
    public sealed class SubSpeciesInfo { }
    public static PlantSubSpeciesCatalog Instance;
    public readonly Dictionary<Tag, List<SubSpeciesInfo>> Existing = new Dictionary<Tag, List<SubSpeciesInfo>>();
    public List<Tag> GetAllDiscoveredSpecies() { return Existing.Keys.ToList(); }
    public List<SubSpeciesInfo> GetAllSubSpeciesForSpecies(Tag species)
    {
        List<SubSpeciesInfo> value;
        return Existing.TryGetValue(species, out value) ? value : null;
    }
}

public class DiscoveredResources
{
    public static DiscoveredResources Instance;
    public readonly HashSet<Tag> Existing = new HashSet<Tag>();
    public readonly HashSet<Tag> ExistingCategories = new HashSet<Tag>();
    public bool IsDiscovered(Tag seed) { return Existing.Contains(seed) || ExistingCategories.Contains(seed); }
}

public static class Assets
{
    public static readonly Dictionary<Tag, UnityEngine.GameObject> Existing = new Dictionary<Tag, UnityEngine.GameObject>();
    public static int GetPrefabCalls;
    public static int TryGetPrefabCalls;
    public static int MissingPrefabWarnings;
    public static UnityEngine.GameObject TryGetPrefab(Tag tag)
    {
        TryGetPrefabCalls++;
        UnityEngine.GameObject value;
        return Existing.TryGetValue(tag, out value) ? value : null;
    }
    public static UnityEngine.GameObject GetPrefab(Tag tag)
    {
        GetPrefabCalls++;
        var value = TryGetPrefab(tag);
        if (value == null) MissingPrefabWarnings++;
        return value;
    }
}

public class SeedProducer : UnityEngine.Object
{
    public struct SeedInfo { public string seedId; }
    public SeedInfo seedInfo;
}

namespace OniMcp.Tools
{
    public static partial class StoryFacilityTools
    {
        internal static List<Dictionary<string, object>> ReadGeneticSeedOptionsForTest(GeneticAnalysisStation.StatesInstance station)
        {
            return GetGeneticSeedOptions(station);
        }
    }
}
