using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    internal static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new Exception("Harmony registration: " + description);
    }
    internal static JObject ReadAudit()
    {
        return JObject.Parse(JsonConvert.SerializeObject(OwnedHarmonyPatchAudit.Read(), McpJsonUtil.Settings));
    }
    private static void Registration()
    {
        Harmony.Reset(); FixtureEvents.Events.Clear();
        string owner = (string)typeof(InputSafetyPatchVerifier).GetField("HarmonyId", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        string autoOwner = (string)typeof(AutoDisinfectPolicy).GetField("HarmonyId", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
        Check(!string.IsNullOrWhiteSpace(owner) && owner == autoOwner, "matching loader owner comes from the actual variant's unchanged Ensure constants");
        var mod = new ModInfo { assembly = typeof(ModInfo).Assembly };
        mod.OnLoad(new Harmony(owner));
        Check(FixtureEvents.BaseOnLoadCalls == 1 && Harmony.PatchAllCalls == 1 && Harmony.ParameterlessPatchAllCalls == 0, "native base is the only attribute patch registration path");
        Check(Harmony.PatchAllAssemblies.Single() == mod.assembly, "native base registers the supplied mod assembly");
        Check(Harmony.PreparationReadyAtPatchAll, "paths/options/locale/PLib/translation/options UI are prepared before native registration");
        Check(FixtureEvents.Events.IndexOf("options_ui") < FixtureEvents.Events.IndexOf("base_on_load"), "base initialization follows complete options preparation");
        Check(McpLocalization.LanguageProvider() == "ja" && McpLocalization.GameLanguageProvider() == "ja", "both language providers and cached native locale are ready");
        int patchAll = FixtureEvents.Events.IndexOf("patch_all"), speed = FixtureEvents.Events.IndexOf("ensure_speed"), input = FixtureEvents.Events.IndexOf("ensure_input"), auto = FixtureEvents.Events.IndexOf("ensure_auto"), registry = FixtureEvents.Events.IndexOf("registry");
        Check(patchAll < speed && speed < input && input < auto && auto < registry, "manual Ensure order and registry initialization remain after native PatchAll");
        Check(FixtureEvents.RegistryInitializations == 1 && FixtureEvents.Events.Count(x => x == "component:MainThreadBridge") == 1 && FixtureEvents.Events.Count(x => x == "component:McpHttpServer") == 1, "existing bridge/server startup still runs once");
        Check(SpeedTransitionTrace.Coverage.Count == 5 && SpeedTransitionTrace.Coverage.Values.All(x => x == "installed"), "all five real manual speed-patch bindings remain installed");
        var audit = ReadAudit();
        Check((bool)audit["available"] && (int)audit["duplicateGroups"] == 0, "the represented startup has no duplicate hook registration");
        var dbRows = ((JArray)audit["rows"]).OfType<JObject>().Where(row => (string)row["original"]["type"] == "Db" && (string)row["original"]["name"] == "Initialize" && (string)row["kind"] == "postfix").ToList();
        Check(dbRows.Count == 2 && dbRows.All(row => (int)row["count"] == 1), "distinct legitimate Db.Initialize postfixes stay separate, not duplicates");
        int registered = Harmony.PatchCalls, scans = Harmony.PatchAllCalls, reads = Harmony.InfoReads;
        Db_Initialize_Patch.Postfix(); Db_Initialize_Patch.Postfix();
        Check(Harmony.PatchCalls == registered && Harmony.PatchAllCalls == scans && Harmony.InfoReads > reads, "twice-executed real Db callbacks preserve the matching-owner Ensure guards");
        Check(FixtureEvents.RuntimeReadyCalls == 2 && FixtureEvents.OverlayCalls == 2 && FixtureEvents.CoordinateCalls == 2, "both real Db callback bodies execute without claiming overlay instance semantics");
        var after = ReadAudit();
        Check(JToken.DeepEquals(audit, after) && Harmony.FactoryReads == 0, "repeat callbacks leave exact registration metadata and factory state unchanged");
    }
    private static void Main()
    {
        Registration();
        int registrationChecks = checks;
        AuditRegression.Run();
        int auditChecks = checks - registrationChecks;
        RouteRegression.Run();
        Console.WriteLine("Harmony registration bootstrap: " + registrationChecks + " assertions passed");
        Console.WriteLine("Harmony registration audit: " + auditChecks + " assertions passed");
        Console.WriteLine("Harmony registration status routes: " + (checks - registrationChecks - auditChecks) + " assertions passed");
        Console.WriteLine("Harmony registration total: " + checks + " assertions passed");
    }
}
