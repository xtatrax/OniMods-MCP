using System.Collections.Generic;
using System.Reflection.Emit;

internal interface IReader { }
internal sealed class SaveLoader : UnityEngine.Object
{
    public static SaveLoader Instance;
    public static string Active = "fixture.sav";
    public static int NativeCalls;
    public bool Load(string filename) { NativeCalls++; return true; }
    private bool Load(IReader reader) { NativeCalls++; return true; }
    public static string GetActiveSaveFilePath() { return Active; }
}
internal static class Grid { public static int CellCount = 1; }
namespace Klei
{
    internal sealed class SaveFileRoot { public List<KMod.Label> active_mods = null; }
}
namespace KMod
{
    [System.Flags]
    internal enum Content : byte { LayerableFiles = 1, Strings = 2, DLL = 4, Translation = 8, Animation = 16 }
    internal struct Label
    {
        internal enum DistributionPlatform { Local, Steam, Epic, Rail, Dev }
        public string id, title;
        public long version;
        public DistributionPlatform distribution_platform;
    }
    internal sealed partial class Manager
    {
        public static int MatchCalls;
        public bool MatchFootprint(List<Label> labels, Content content) { MatchCalls++; return true; }
    }
}
namespace HarmonyLib
{
    internal sealed class ExceptionBlock { }
    internal sealed class CodeInstruction
    {
        public OpCode opcode;
        public object operand;
        public List<System.Reflection.Emit.Label> labels = new List<System.Reflection.Emit.Label>();
        public List<ExceptionBlock> blocks = new List<ExceptionBlock>();
        public CodeInstruction(OpCode code, object value = null) { opcode = code; operand = value; }
    }
}

internal sealed class SaveGame : UnityEngine.Object
{
    public static SaveGame Instance;
    public string BaseName { get; set; }
}
internal sealed class ClusterManager
{
    public static ClusterManager Instance;
    public List<WorldContainer> WorldContainers = new List<WorldContainer>();
}
internal sealed class WorldContainer : UnityEngine.Object
{
    public int id;
    public bool IsDiscovered { get; set; }
    public bool IsModuleInterior { get; set; }
    public ClusterGridEntity Entity;
    public AsteroidGridEntity AdditionalAsteroid;
    public int ComponentLookups;
    public T GetComponent<T>() where T : class
    {
        ComponentLookups++;
        if (typeof(T) == typeof(AsteroidGridEntity) && AdditionalAsteroid != null) return AdditionalAsteroid as T;
        return Entity as T;
    }
    public string GetProperName() { throw new System.Exception("UI-name fallback must not run"); }
}
internal abstract class ClusterGridEntity : UnityEngine.Object { public abstract string Name { get; } }
internal class AsteroidGridEntity : ClusterGridEntity
{
    public string RawName;
    public bool ThrowOnName;
    public static int NameReads;
    public override string Name { get { NameReads++; if (ThrowOnName) throw new System.Exception("name unavailable"); return RawName; } }
}
internal sealed class UnsupportedAsteroid : AsteroidGridEntity
{
    public override string Name { get { throw new System.Exception("unknown override must not run"); } }
}

internal sealed class UnsupportedClusterEntity : ClusterGridEntity
{
    public override string Name { get { throw new System.Exception("unknown cluster override must not run"); } }
}
