using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Newtonsoft.Json.Linq;

internal static class AuditRegression
{
    internal static void Original() { }
    internal static void Original(bool value) { }
    internal static void Complex(ref Dictionary<string, int[,]>[] values) { }
    internal static void Hook() { }
    private static class HookA { public static void Postfix() { } }
    private static class HookB { public static void Postfix() { } }
    internal static MethodInfo OriginalMethod { get { return typeof(AuditRegression).GetMethod("Original", BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null); } }
    internal static MethodInfo HookMethod { get { return typeof(AuditRegression).GetMethod("Hook", BindingFlags.Static | BindingFlags.NonPublic); } }
    internal static void Add(MethodBase original, string kind, MethodInfo hook, string owner)
    {
        Patches list;
        if (!Harmony.Table.TryGetValue(original, out list)) Harmony.Table[original] = list = new Patches();
        list.Add(kind, new Patch(hook, owner));
    }
    private static void Missing(string expected)
    {
        var data = Program.ReadAudit();
        Program.Check(!(bool)data["available"] && (string)data["reasonCode"] == expected, "uncertain metadata has explicit unavailable reason " + expected);
        foreach (string key in new[] { "totalEntries", "distinctGroups", "duplicateGroups", "owners", "returned", "rows", "truncated" })
            Program.Check(data.Property(key) != null && data[key].Type == JTokenType.Null, "unknown " + key + " is explicit null, not normal zero/empty");
        Program.Check((int)data["limit"] == 128 && (string)data["source"] == "Harmony.GetAllPatchedMethods/GetPatchInfo" && (string)data["scope"] == "patch_methods_from_this_assembly", "unavailable metadata retains source/scope/fixed limit");
    }
    private static IEnumerable<MethodBase> PartialEnumeration()
    {
        yield return OriginalMethod;
        throw new InvalidOperationException("fixture enumeration failure");
    }
    private static MethodInfo[] ExternalOriginals(int count)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SyntheticExternalTargets" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("fixture").DefineType("ExternalOriginals", TypeAttributes.Public);
        for (int i = 0; i < count; i++)
        {
            var method = type.DefineMethod("Cell_" + i.ToString("D3"), MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
            method.GetILGenerator().Emit(OpCodes.Ret);
        }
        return type.CreateTypeInfo().AsType().GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
    }
    internal static void Run()
    {
        Harmony.Reset();
        var data = Program.ReadAudit();
        Program.Check((bool)data["available"] && (int)data["totalEntries"] == 0 && (int)data["distinctGroups"] == 0 && (int)data["duplicateGroups"] == 0 && !((JArray)data["rows"]).Any(), "a successful empty global enumeration is known zero");
        Program.Check((int)data["returned"] == 0 && (int)data["limit"] == 128 && !(bool)data["truncated"], "known empty read has stable bounded row shape");

        Add(OriginalMethod, "postfix", typeof(HookA).GetMethod("Postfix"), "owner-z");
        Add(OriginalMethod, "postfix", typeof(HookB).GetMethod("Postfix"), "owner-a");
        data = Program.ReadAudit();
        Program.Check((int)data["totalEntries"] == 2 && (int)data["distinctGroups"] == 2 && (int)data["duplicateGroups"] == 0, "distinct same-name postfix method identities on one original are legitimate groups");
        Program.Check(data["owners"].Values<string>().SequenceEqual(new[] { "owner-a", "owner-z" }), "owners are deterministic distinct metadata");

        Harmony.Reset(); Add(OriginalMethod, "postfix", HookMethod, "owner-z"); Add(OriginalMethod, "postfix", HookMethod, "owner-a");
        data = Program.ReadAudit();
        Program.Check((int)data["totalEntries"] == 2 && (int)data["distinctGroups"] == 1 && (int)data["duplicateGroups"] == 1, "the exact same original/kind/hook is duplicate even under two owners");
        Program.Check((int)data["rows"][0]["count"] == 2 && data["rows"][0]["owners"].Values<string>().SequenceEqual(new[] { "owner-a", "owner-z" }), "duplicate row preserves both distinct owner claims");
        Add(OriginalMethod, "postfix", HookMethod, "owner-a");
        data = Program.ReadAudit();
        Program.Check((int)data["totalEntries"] == 3 && (int)data["rows"][0]["count"] == 3 && ((JArray)data["rows"][0]["owners"]).Count == 2, "same-owner repetition increases hook count without duplicating owner names");

        Harmony.Reset();
        foreach (string kind in new[] { "prefix", "postfix", "transpiler", "finalizer" }) Add(OriginalMethod, kind, HookMethod, "owner");
        data = Program.ReadAudit();
        Program.Check((int)data["totalEntries"] == 4 && (int)data["distinctGroups"] == 4 && (int)data["duplicateGroups"] == 0, "the four native patch kinds are separate groups");
        Program.Check(((JArray)data["rows"]).Select(row => (string)row["kind"]).OrderBy(k => k).SequenceEqual(new[] { "finalizer", "postfix", "prefix", "transpiler" }), "all four native patch collections are read");
        Add(typeof(AuditRegression).GetMethod("Original", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null), "postfix", HookMethod, "owner");
        data = Program.ReadAudit();
        Program.Check((int)data["distinctGroups"] == 5 && (int)data["duplicateGroups"] == 0, "overloaded original method identities remain separate");
        Program.Check(((JArray)data["rows"]).Any(row => (string)row["original"]["signature"] == "System.Void(System.Boolean)"), "canonical original parameter signature is returned");
        Add(typeof(AuditRegression).GetMethod("Complex", BindingFlags.Static | BindingFlags.NonPublic), "postfix", HookMethod, "owner");
        data = Program.ReadAudit();
        Program.Check(((JArray)data["rows"]).Any(row => (string)row["original"]["signature"] == "System.Void(System.Collections.Generic.Dictionary<System.String,System.Int32[,]>[]&)"), "generic/multidimensional array/byref metadata is canonical");

        Harmony.Reset();
        Add(OriginalMethod, "postfix", typeof(Console).GetMethod("WriteLine", new[] { typeof(string) }), null);
        data = Program.ReadAudit();
        Program.Check((bool)data["available"] && (int)data["totalEntries"] == 0 && ((JArray)data["owners"]).Count == 0, "other-assembly patch methods are excluded without owner-prefix assumptions");
        Add(OriginalMethod, "postfix", HookMethod, "arbitrary-owner");
        data = Program.ReadAudit();
        Program.Check((int)data["totalEntries"] == 1 && (string)data["owners"][0] == "arbitrary-owner", "this assembly's hook counts even with an unrelated owner string");
        Program.Check(Harmony.FactoryReads == 0 && Harmony.PatchCalls == 0, "readback never calls patch factories or installs patches");

        Harmony.Reset();
        var originals = ExternalOriginals(130);
        foreach (var original in originals.Reverse()) Add(original, "postfix", HookMethod, "owner-a");
        Add(originals[129], "postfix", HookMethod, "owner-z");
        var tableSnapshot = Harmony.Table.ToDictionary(kv => kv.Key, kv => kv.Value);
        data = Program.ReadAudit();
        Program.Check((int)data["totalEntries"] == 131 && (int)data["distinctGroups"] == 130 && (int)data["duplicateGroups"] == 1, "global counts include duplicate groups beyond returned rows");
        Program.Check((int)data["returned"] == 128 && ((JArray)data["rows"]).Count == 128 && (int)data["limit"] == 128 && (bool)data["truncated"], "rows obey the fixed cap while reporting truncation");
        Program.Check((string)data["rows"][0]["original"]["name"] == "Cell_000" && (string)data["rows"][127]["original"]["name"] == "Cell_127", "rows are deterministic despite reverse input enumeration");
        Program.Check(data["owners"].Values<string>().SequenceEqual(new[] { "owner-a", "owner-z" }), "global owner inventory includes truncated tail registrations");
        Program.Check(Harmony.Table.Count == tableSnapshot.Count && tableSnapshot.All(kv => ReferenceEquals(Harmony.Table[kv.Key], kv.Value)) && Harmony.PatchCalls == 0 && Harmony.FactoryReads == 0, "audit preserves native patch table identity and performs no registration/factory calls");

        Harmony.Reset(); Harmony.AllOverride = () => null; Missing("method_list_unavailable");
        Harmony.Reset(); Harmony.AllOverride = () => new MethodBase[] { null }; Missing("original_method_unavailable");
        Harmony.Reset(); Add(OriginalMethod, "postfix", HookMethod, "owner"); Harmony.AllOverride = () => new[] { OriginalMethod, OriginalMethod }; Missing("original_method_duplicate");
        Harmony.Reset(); Harmony.Table[OriginalMethod] = null; Missing("patch_info_unavailable");
        foreach (string kind in new[] { "prefix", "postfix", "transpiler", "finalizer" })
        {
            Harmony.Reset(); var list = new Patches(); Harmony.Table[OriginalMethod] = list;
            if (kind == "prefix") list.Prefixes = null; else if (kind == "postfix") list.Postfixes = null; else if (kind == "transpiler") list.Transpilers = null; else list.Finalizers = null;
            Missing("patch_list_unavailable");
        }
        Harmony.Reset(); Harmony.Table[OriginalMethod] = new Patches(); Harmony.Table[OriginalMethod].Add("prefix", null); Missing("patch_entry_unavailable");
        Harmony.Reset(); Add(OriginalMethod, "postfix", null, "owner"); Missing("patch_method_unavailable");
        foreach (string owner in new string[] { null, "" })
        {
            Harmony.Reset(); Add(OriginalMethod, "postfix", HookMethod, owner); Missing("owner_unavailable");
        }
        Harmony.Reset(); Harmony.AllOverride = () => { throw new InvalidOperationException("fixture global API failure"); }; Missing("scan_failed");
        Harmony.Reset(); Add(OriginalMethod, "postfix", HookMethod, "owner"); Harmony.InfoOverride = method => { throw new InvalidOperationException("fixture patch info failure"); }; Missing("scan_failed");
        Harmony.Reset(); Add(OriginalMethod, "postfix", HookMethod, "owner"); Harmony.AllOverride = PartialEnumeration; Missing("scan_failed");
        Harmony.Reset(); var bad = new Patch(HookMethod, "owner") { ThrowPatchMethod = true }; Harmony.Table[OriginalMethod] = new Patches(); Harmony.Table[OriginalMethod].Add("postfix", bad); Missing("scan_failed");
        Harmony.Reset();
        var dynamicHook = new DynamicMethod("NoDeclaringType", typeof(void), Type.EmptyTypes, typeof(AuditRegression).Module);
        Add(OriginalMethod, "postfix", dynamicHook, "owner"); Missing("declaring_type_unavailable");
        Program.Check(Harmony.FactoryReads == 0, "even unavailable metadata does not execute a patch factory");
    }
}
