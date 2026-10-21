using UnityEngine;
// Cached native inputs only. Group identity, aggregation, selection within the
// target method, ordering, truncation and payload assembly are production code.
public readonly struct Tag : IEquatable<Tag>
{
    public string Name { get; }
    public bool IsValid { get; }
    public Tag(string name,bool valid=true) { Name=name;IsValid=valid; }
    public bool Equals(Tag other)=>StringComparer.Ordinal.Equals(Name,other.Name);
    public override bool Equals(object other)=>other is Tag tag&&Equals(tag);
    public override int GetHashCode()=>Name==null?0:StringComparer.Ordinal.GetHashCode(Name);
}
public sealed partial class KPrefabID
{
    public Tag PrefabTag;
    public readonly HashSet<Tag> Tags=new();
    public bool HasTag(Tag tag)=>Tags.Contains(tag);
    // Native KPrefabID equality compares prefab tags, not entity identity.
    public override bool Equals(object other)=>other is KPrefabID prefab&&PrefabTag.Equals(prefab.PrefabTag);
    public override int GetHashCode()=>PrefabTag.GetHashCode();
}
public static class GameTags { public static readonly Tag Stored=new("Stored"); }
public sealed class PrimaryElement { public SimHashes ElementID;public float Mass; }
public sealed class Pickupable
{
    public GameObject gameObject;
    public int cachedCell;
    public object storage;
    public KPrefabID KPrefabID=>gameObject?.GetComponent<KPrefabID>();
    public PrimaryElement PrimaryElement=>gameObject?.GetComponent<PrimaryElement>();
    public T GetComponent<T>() where T:class=>gameObject?.GetComponent<T>();
    public string GetProperName()=>gameObject.GetProperName();
    public void Refresh()=>throw new InvalidOperationException("Unexpected pickupable refresh");
}
public static class Components
{
    public static class Pickupables { public static readonly List<Pickupable> Items=new(); }
}
namespace UnityEngine { public sealed partial class GameObject { public int WorldId; } }
namespace OniMcp.Tools
{
    public static partial class ToolUtil
    {
        // Inputs in this fixture are already cleaned; markup cleaning is outside
        // this grouping test's boundary and is not independently proven.
        public static string CleanName(string value)=>value;
    }
    public static partial class WorldAnalysisTools
    {
        // External cell/world selector and finite-value helper boundaries only.
        // The production touch-cell OR and grouping remain in the linked source.
        private static bool MatchesWorld(GameObject go,int worldId)=>worldId<0||go.WorldId==worldId;
        private static float SafeFloat(float value)=>float.IsNaN(value)||float.IsInfinity(value)?0f:value;
        internal static Dictionary<string,object> PickupableSummaryForTests(int cell,int worldId)=>CellPickupableSummary(cell,worldId);
        internal static void RunPickupableGroupingRegressions()=>PickupableGroupingRegression.Run();
    }
}
