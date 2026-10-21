using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public Type TargetType; public string TargetName; public Type[] Arguments;
        public HarmonyPatch(Type type, string name) { TargetType = type; TargetName = name; }
        public HarmonyPatch(Type type, string name, Type[] arguments) : this(type, name) { Arguments = arguments; }
    }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyTranspiler : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyFinalizer : Attribute { }
    public sealed class HarmonyMethod
    {
        public readonly MethodInfo method;
        public HarmonyMethod(MethodInfo value) { method = value; }
    }
    public sealed class Patch
    {
        private readonly MethodInfo method;
        public string owner;
        public bool ThrowPatchMethod;
        public Patch(MethodInfo value, string id) { method = value; owner = id; }
        public MethodInfo PatchMethod { get { if (ThrowPatchMethod) throw new InvalidOperationException("fixture metadata read failure"); return method; } }
        public MethodInfo GetMethod(MethodBase original) { Harmony.FactoryReads++; throw new InvalidOperationException("patch factory execution forbidden by this readback fixture"); }
    }
    public sealed class Patches
    {
        public ReadOnlyCollection<Patch> Prefixes = new List<Patch>().AsReadOnly();
        public ReadOnlyCollection<Patch> Postfixes = new List<Patch>().AsReadOnly();
        public ReadOnlyCollection<Patch> Transpilers = new List<Patch>().AsReadOnly();
        public ReadOnlyCollection<Patch> Finalizers = new List<Patch>().AsReadOnly();
        public ReadOnlyCollection<string> Owners
        {
            get { return new[] { Prefixes, Postfixes, Transpilers, Finalizers }.Where(x => x != null).SelectMany(x => x).Where(x => x != null).Select(x => x.owner).Distinct().ToList().AsReadOnly(); }
        }
        public void Add(string kind, Patch patch)
        {
            var current = kind == "prefix" ? Prefixes : kind == "postfix" ? Postfixes : kind == "transpiler" ? Transpilers : Finalizers;
            var next = current.ToList(); next.Add(patch);
            if (kind == "prefix") Prefixes = next.AsReadOnly(); else if (kind == "postfix") Postfixes = next.AsReadOnly();
            else if (kind == "transpiler") Transpilers = next.AsReadOnly(); else Finalizers = next.AsReadOnly();
        }
    }
    public sealed class Harmony
    {
        public readonly string Id;
        public Harmony(string id) { Id = id; }
        public static readonly List<Assembly> PatchAllAssemblies = new List<Assembly>();
        public static readonly Dictionary<MethodBase, Patches> Table = new Dictionary<MethodBase, Patches>();
        public static Func<IEnumerable<MethodBase>> AllOverride;
        public static Func<MethodBase, Patches> InfoOverride;
        public static int PatchCalls, PatchAllCalls, ParameterlessPatchAllCalls, Scans, InfoReads, FactoryReads;
        public static bool PreparationReadyAtPatchAll;
        public static void Reset()
        {
            Table.Clear(); PatchAllAssemblies.Clear(); AllOverride = null; InfoOverride = null;
            PatchCalls = PatchAllCalls = ParameterlessPatchAllCalls = Scans = InfoReads = FactoryReads = 0;
            PreparationReadyAtPatchAll = false;
        }
        public static IEnumerable<MethodBase> GetAllPatchedMethods() { Scans++; return AllOverride == null ? Table.Keys.ToArray() : AllOverride(); }
        public static Patches GetPatchInfo(MethodBase original)
        {
            InfoReads++; if (InfoOverride != null) return InfoOverride(original);
            Patches value; return original != null && Table.TryGetValue(original, out value) ? value : null;
        }
        public void PatchAll() { ParameterlessPatchAllCalls++; PatchAll(typeof(OniMcp.ModInfo).Assembly); }
        public void PatchAll(Assembly assembly)
        {
            PatchAllCalls++; PatchAllAssemblies.Add(assembly); FixtureEvents.Add("patch_all"); PreparationReadyAtPatchAll = FixtureEvents.Prepared;
            foreach (var type in assembly.GetTypes())
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length != 0) CreateClassProcessor(type).Patch();
        }
        public MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
        {
            PatchCalls++; FixtureEvents.Add("patch:" + original.Name);
            Patches list; if (!Table.TryGetValue(original, out list)) Table[original] = list = new Patches();
            if (prefix != null) list.Add("prefix", new Patch(prefix.method, Id));
            if (postfix != null) list.Add("postfix", new Patch(postfix.method, Id));
            if (transpiler != null) list.Add("transpiler", new Patch(transpiler.method, Id));
            if (finalizer != null) list.Add("finalizer", new Patch(finalizer.method, Id));
            return original as MethodInfo;
        }
        public PatchClassProcessor CreateClassProcessor(Type type) { return new PatchClassProcessor(this, type); }
    }
    public sealed class PatchClassProcessor
    {
        private readonly Harmony harmony; private readonly Type type;
        public PatchClassProcessor(Harmony owner, Type patch) { harmony = owner; type = patch; }
        public List<MethodInfo> Patch()
        {
            foreach (HarmonyPatch attr in type.GetCustomAttributes(typeof(HarmonyPatch), false))
            {
                var original = AccessTools.Method(attr.TargetType, attr.TargetName, attr.Arguments);
                harmony.Patch(original, Find("Prefix"), Find("Postfix"), Find("Transpiler"), Find("Finalizer"));
            }
            return new List<MethodInfo>();
        }
        private HarmonyMethod Find(string name)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
                method = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(m => m.GetCustomAttributes(false).Any(a => a.GetType().Name == "Harmony" + name));
            return method == null ? null : new HarmonyMethod(method);
        }
    }
    public static class AccessTools
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public static MethodInfo Method(Type type, string name, Type[] parameters = null) { return parameters == null ? type.GetMethod(name, Flags) : type.GetMethod(name, Flags, null, parameters, null); }
        public static ConstructorInfo Constructor(Type type, Type[] parameters) { return type.GetConstructor(Flags, null, parameters, null); }
        public static FieldInfo Field(Type type, string name) { return type.GetField(name, Flags); }
    }
}
