using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x;
        public float y;
    }
    public sealed class Transform
    {
        public Vector3 Position;
        public Vector3 GetPosition() { return Position; }
    }
    public sealed class GameObject
    {
        public string name;
        public int Id;
        public bool ThrowProperName;
        public int WorldId;
        public readonly Transform transform = new Transform();
        public readonly Dictionary<Type, object> Components = new Dictionary<Type, object>();
        public readonly HashSet<Type> ThrowGetComponent = new HashSet<Type>();
        public readonly List<object> ExtraComponents = new List<object>();
        public readonly List<object> ChildComponents = new List<object>();
        public int ComponentEnumerationReads, ChildEnumerationReads;
        public bool ThrowComponentEnumeration, ReturnNullComponentEnumeration;
        public Func<Type, object[]> ComponentResults;
        public T[] GetComponents<T>() where T : class
        {
            ComponentEnumerationReads++;
            if (ThrowComponentEnumeration) throw new InvalidOperationException("fixture component enumeration failure");
            if (ReturnNullComponentEnumeration) return null;
            if (ComponentResults != null) { var values = ComponentResults(typeof(T)); return values == null ? null : values.Cast<T>().ToArray(); }
            return Components.Values.Concat(ExtraComponents).OfType<T>().ToArray();
        }
        public T[] GetComponentsInChildren<T>() where T : class
        {
            ChildEnumerationReads++;
            return Components.Values.Concat(ExtraComponents).Concat(ChildComponents).OfType<T>().ToArray();
        }
        public T GetComponent<T>() where T : class
        {
            if (ThrowGetComponent.Contains(typeof(T))) throw new InvalidOperationException("fixture component read failure");
            object component;
            return Components.TryGetValue(typeof(T), out component) ? component as T : null;
        }
        public int GetInstanceID() { return Id; }
        public int GetMyWorldId() { return WorldId; }
        public string GetProperName() { if (ThrowProperName) throw new InvalidOperationException("fixture item name failure"); return name; }
    }
    public static class Mathf
    {
        public static int RoundToInt(float value) { return (int)Math.Round(value); }
    }
}

public struct Tag : IEquatable<Tag>
{
    private readonly string nativeName;
    private readonly bool initialized;
    public static readonly Tag Invalid = default(Tag);
    public static bool ThrowInvalidName;
    public static int InvalidNameReads;
    public string Name { get { if (!initialized) { InvalidNameReads++; if (ThrowInvalidName) throw new InvalidOperationException("fixture invalid tag Name read forbidden"); } return nativeName; } }
    public Tag(string name) { nativeName = name; initialized = true; }
    public bool Equals(Tag other) { return initialized == other.initialized && string.Equals(nativeName, other.nativeName, StringComparison.Ordinal); }
    public override bool Equals(object other) { return other is Tag && Equals((Tag)other); }
    public override int GetHashCode() { return nativeName == null ? (initialized ? 1 : 0) : nativeName.GetHashCode(); }
    public static bool operator ==(Tag first, Tag second) { return first.Equals(second); }
    public static bool operator !=(Tag first, Tag second) { return !first.Equals(second); }
}

public sealed class TreeFilterable
{
    private HashSet<Tag> nativeTags = new HashSet<Tag>();
    public int Updates;
    public int Reads;
    public Action<TreeFilterable> OnUpdated;
    public IEnumerable<Tag> GetTags() { Reads++; return nativeTags.ToArray(); }
    public void FixtureSet(params string[] tags) { nativeTags = new HashSet<Tag>(tags.Select(tag => new Tag(tag))); }
    public void UpdateFilters(HashSet<Tag> tags)
    {
        Updates++;
        nativeTags = new HashSet<Tag>(tags);
        if (OnUpdated != null) OnUpdated(this);
    }
}

public interface IStorage { }
public sealed class OtherStorageFixture : IStorage { }
public sealed class Storage : IStorage
{
    private float capacity, nativeMass;
    private bool show = true;
    private Tag id;
    private List<Tag> filters;
    private List<UnityEngine.GameObject> storedItems = new List<UnityEngine.GameObject>();
    public int MassReads, Writes;
    public readonly Dictionary<string, int> Reads = new Dictionary<string, int>();
    public readonly HashSet<string> ThrowOn = new HashSet<string>();
    private void Read(string field)
    {
        Reads[field] = Reads.ContainsKey(field) ? Reads[field] + 1 : 1;
        if (ThrowOn.Contains(field)) throw new InvalidOperationException("fixture storage field read failure");
    }
    public float capacityKg { get { Read("capacityKg"); return capacity; } set { Writes++; capacity = value; } }
    public bool showInUI { get { Read("showInUI"); return show; } set { Writes++; show = value; } }
    public Tag storageID { get { Read("storageID"); return id; } set { Writes++; id = value; } }
    public List<Tag> storageFilters { get { Read("storageFilters"); return filters; } set { Writes++; filters = value; } }
    public List<UnityEngine.GameObject> items { get { Read("items"); return storedItems; } set { Writes++; storedItems = value; } }
    public void FixtureMass(float value) { nativeMass = value; }
    public float MassStored() { MassReads++; Read("MassStored"); return nativeMass; }
}

public sealed class StorageLocker { public float UserMaxCapacity; }
public sealed class KPrefabID
{
    private int instanceId;
    public bool ThrowInstanceId;
    public int InstanceID { get { if (ThrowInstanceId) throw new InvalidOperationException("fixture identity read failure"); return instanceId; } set { instanceId = value; } }
    public Tag PrefabTag;
}
public enum SimHashes { Water, DirtyWater, Dirt, Oxygen }
public sealed class PrimaryElement
{
    private float mass, temperature;
    private byte diseaseIdx;
    private int diseaseCount;
    private SimHashes elementId;
    public int Writes;
    public readonly HashSet<string> ThrowOn = new HashSet<string>();
    public readonly Dictionary<string, int> Reads = new Dictionary<string, int>();
    private void BeforeRead(string field)
    {
        Reads[field] = Reads.ContainsKey(field) ? Reads[field] + 1 : 1;
        if (ThrowOn.Contains(field)) throw new InvalidOperationException("fixture primary read failure");
    }
    public float Mass { get { BeforeRead("Mass"); return mass; } set { Writes++; mass = value; } }
    public float Temperature { get { BeforeRead("Temperature"); return temperature; } set { Writes++; temperature = value; } }
    public byte DiseaseIdx { get { BeforeRead("DiseaseIdx"); return diseaseIdx; } set { Writes++; diseaseIdx = value; } }
    public int DiseaseCount { get { BeforeRead("DiseaseCount"); return diseaseCount; } set { Writes++; diseaseCount = value; } }
    public SimHashes ElementID { get { BeforeRead("ElementID"); return elementId; } set { Writes++; elementId = value; } }
}
namespace Klei.AI
{
    public sealed class Disease
    {
        private string id, name;
        public bool ThrowId, ThrowName;
        public string Id { get { if (ThrowId) throw new InvalidOperationException("fixture disease ID read failure"); return id; } set { id = value; } }
        public string Name { get { if (ThrowName) throw new InvalidOperationException("fixture disease name read failure"); return name; } set { name = value; } }
    }
}
namespace Database { public sealed class Diseases { public List<Klei.AI.Disease> resources = new List<Klei.AI.Disease>(); } }
public sealed class Db
{
    private static Db _Instance;
    public static int LazyGetCalls;
    public Database.Diseases Diseases;
    public static void FixtureSet(Db value) { _Instance = value; }
    public static Db FixtureCurrent { get { return _Instance; } }
    public static Db Get() { LazyGetCalls++; throw new InvalidOperationException("lazy database initialization forbidden in readback"); }
}
public sealed class BuildingComplete { public UnityEngine.GameObject gameObject; }
public sealed class BuildingCollection { public readonly List<BuildingComplete> Items = new List<BuildingComplete>(); }
public static class Components { public static readonly BuildingCollection BuildingCompletes = new BuildingCollection(); }
public static class Grid
{
    public static int XYToCell(int x, int y) { return x < 0 || y < 0 || x >= 100 || y >= 100 ? -1 : y * 100 + x; }
    public static int PosToCell(UnityEngine.Vector3 position) { return XYToCell((int)position.x, (int)position.y); }
    public static bool IsValidCell(int cell) { return cell >= 0 && cell < 10000; }
    public static int CellColumn(int cell) { return cell % 100; }
    public static int CellRow(int cell) { return cell / 100; }
}

namespace OniMcp.Core
{
    public sealed class McpToolParameter
    {
        public string Type;
        public string Description;
        public bool Required;
        public List<string> EnumValues;
    }
    public sealed class McpTool
    {
        public string Name;
        public bool Hidden;
        public string Group;
        public string Mode;
        public string Risk;
        public List<string> Aliases;
        public List<string> Tags;
        public string Description;
        public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, CallToolResult> Handler;
    }
    public sealed class CallToolResult
    {
        public bool IsError;
        public string Value;
        public static CallToolResult Text(string value) { return new CallToolResult { Value = value }; }
        public static CallToolResult Error(string value) { return new CallToolResult { Value = value, IsError = true }; }
    }
}

namespace OniMcp.Tools
{
    // Unrelated sibling routes are not exercised by these storage-handler tests.
    public static class ReceptacleTools
    {
        public static OniMcp.Core.McpTool ControlStorageTileSelection() { throw new NotSupportedException(); }
        public static OniMcp.Core.McpTool ControlReceptacle() { throw new NotSupportedException(); }
    }
    public static class FilterTools
    {
        public static OniMcp.Core.McpTool ControlFilter() { throw new NotSupportedException(); }
    }
    public static class ToolUtil
    {
        public static int? GetInt(JObject args, string key) { return (int?)args[key]; }
        public static int ResolveWorldId(JObject args) { return GetInt(args, "worldId") ?? 0; }
        public static int ClampLimit(JObject args, int fallback, int maximum) { return Math.Max(1, Math.Min(maximum, GetInt(args, "limit") ?? fallback)); }
        public static string CleanName(string value) { return value; }
        public static float SafeFloat(float value) { return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value; }
    }
}

namespace OniMcp { public static class McpLocalization { public static string Get(string key) { return key; } } }
