using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KMod;
using Newtonsoft.Json.Linq;
using OniMcp;
using OniMcp.Support;
using OniMcp.Tools;
using NativeLabel = KMod.Label;

internal static class SavedModFootprintRegression
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Saved MOD footprint: " + message);
        checks++;
    }
    private static JObject Read(JObject args = null)
    {
        var result = SavedModFootprint.Read(args ?? new JObject());
        Check(!result.IsError && result.Content.Count == 1, "actual read returns one structured wire payload");
        return JObject.Parse(result.Content[0].Text);
    }
    private static List<NativeLabel> Labels(int count = 2)
    {
        return Enumerable.Range(0, count).Select(i => new NativeLabel { id = "label-" + i,
            title = "Saved title " + i, version = 10 + i,
            distribution_platform = NativeLabel.DistributionPlatform.Local }).ToList();
    }
    private static object Hook(string name, params object[] args)
    {
        return typeof(SavedModFootprintPatch).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }
    private static SavedModFootprint.LoadCall File(string name = "loaded.sav", SaveLoader loader = null)
    {
        object[] args = { loader ?? SaveLoader.Instance, name, null };
        Hook("FilePrefix", args);
        return (SavedModFootprint.LoadCall)args[2];
    }
    private static SavedModFootprint.BodyCall Body(SaveLoader loader = null)
    {
        object[] args = { loader ?? SaveLoader.Instance, null };
        Hook("BodyPrefix", args);
        return (SavedModFootprint.BodyCall)args[1];
    }
    private static void End(SavedModFootprint.LoadCall file, SavedModFootprint.BodyCall body,
        bool inner = true, bool outer = true, Exception bodyError = null, Exception fileError = null, System.Action afterBody = null)
    {
        Check(ReferenceEquals(Hook("BodyFinalizer", body, inner, bodyError), bodyError), "body finalizer preserves native exception");
        if (afterBody != null) afterBody();
        Check(ReferenceEquals(Hook("FileFinalizer", file, outer, fileError), fileError), "file finalizer preserves native exception");
    }
    private static void Success(List<NativeLabel> labels = null)
    {
        var file = File(); var body = Body(); SavedModFootprint.Observe(labels ?? Labels()); End(file, body);
    }
    private static void Setup()
    {
        Game.ThrowOnRead = false; Game.Instance = new Game();
        SaveLoader.Instance = new SaveLoader(); SaveLoader.Active = "active.sav"; Grid.CellCount = 1;
        SaveGame.Instance = new SaveGame { BaseName = "Native colony" };
        ClusterManager.Instance = new ClusterManager();
        ClusterManager.Instance.WorldContainers.Add(new WorldContainer { id = 7, IsDiscovered = true,
            Entity = new AsteroidGridEntity { RawName = "Raw cluster name" } });
        ClusterManager.Instance.WorldContainers.Add(new WorldContainer { id = 9, IsDiscovered = false,
            Entity = new AsteroidGridEntity { RawName = "Undiscovered raw name" } });
        ClusterManager.Instance.WorldContainers.Add(new WorldContainer { id = 12, IsDiscovered = true,
            IsModuleInterior = true, Entity = new AsteroidGridEntity { RawName = "Interior raw name" } });

    }
    private static List<CodeInstruction> Site()
    {
        return new List<CodeInstruction> {
            new CodeInstruction(OpCodes.Ldloc_0), new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Ldfld, typeof(Klei.SaveFileRoot).GetField("active_mods")),
            new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)31),
            new CodeInstruction(OpCodes.Callvirt, typeof(Manager).GetMethod("MatchFootprint")),
            new CodeInstruction(OpCodes.Brtrue_S, new object()) };
    }
    internal static void Run()
    {
        Setup();
        Check(!(bool)Read()["available"], "uninstalled observer is unknown, never an empty known list");
        var before = Harmony.Registrations.Count;
        SavedModFootprintPatch.EnsureInstalled(new Harmony("fixture-save-observer"));
        Check(Harmony.Registrations.Count == before + 2, "only exact outer and body native loads receive patches");
        var registrations = Harmony.Registrations.Skip(before).ToArray();
        Check(registrations.All(x => x.Prefix != null && x.Finalizer != null) && registrations[0].Transpiler == null &&
            registrations[1].Transpiler != null, "success/exception wrappers and one scoped transpiler are registered");
        SavedModFootprintPatch.EnsureInstalled(new Harmony("fixture-save-observer"));
        Check(Harmony.Registrations.Count == before + 2, "repeated installation does not duplicate hooks");
        foreach (string fault in new[] { "missing", "mask", "field", "call", "duplicate", "label", "block", "already_observed" })
        {
            var site = Site();
            if (fault == "missing") site.RemoveAt(4);
            if (fault == "mask") site[3].operand = (sbyte)7;
            if (fault == "field") site[2].operand = typeof(SaveLoader).GetField("Active");
            if (fault == "call") site[4].opcode = OpCodes.Call;
            if (fault == "duplicate") site.AddRange(Site());
            if (fault == "label") site[3].labels.Add(default(System.Reflection.Emit.Label));
            if (fault == "block") site[2].blocks.Add(new ExceptionBlock());
            if (fault == "already_observed") site.Add(new CodeInstruction(OpCodes.Call,
                typeof(SavedModFootprint).GetMethod("Observe", BindingFlags.Static | BindingFlags.NonPublic)));
            var untouched = site.ToArray(); var result = SavedModFootprintPatch.Transpiler(site).ToArray();
            Check(result.Length == untouched.Length && result.Zip(untouched, ReferenceEquals).All(x => x) &&
                SavedModFootprint.BindingStatus != "installed", "unsupported site leaves original IL objects unchanged: " + fault);
        }
        var valid = Site(); var patched = SavedModFootprintPatch.Transpiler(valid).ToArray();
        Check(patched.Length == valid.Count + 2 && patched[3].opcode == OpCodes.Dup && patched[4].opcode == OpCodes.Call &&
            patched[4].operand.Equals(typeof(SavedModFootprint).GetMethod("Observe", BindingFlags.Static | BindingFlags.NonPublic)),
            "one exact native active_mods argument is duplicated for the observer");
        Check(patched.Where(x => x.opcode == OpCodes.Callvirt).Count() == 1 &&
            valid.All(x => patched.Contains(x)) && SavedModFootprint.BindingStatus == "installed",
            "native evaluator remains exactly once with original instructions retained");

        Setup(); Game.Instance = null;
        var bornFile = File(); var bornBody = Body(); SavedModFootprint.Observe(Labels()); Game.Instance = new Game(); End(bornFile, bornBody);
        Check((bool)Read()["available"], "native prefab load may establish Game after file prefix");
        Setup();
        var file = File(); var body = Body(); var source = Labels();
        SavedModFootprint.Observe(source);
        var changed = source[0]; changed.id = "mutated"; source[0] = changed; source.Clear();
        End(file, body);
        var read = Read();
        Check((bool)read["available"] && (int)read["total"] == 2 && (string)read["savedLabels"][0]["id"] == "label-0",
            "successful inner and outer hooks publish detached scalar copies through actual wire producer");
        Check((string)read["loadedSourceFilename"] == "loaded.sav" && (string)read["activeSaveAtLoad"] == "active.sav" &&
            !read["savedLabels"][0].Children<JProperty>().Any(p => p.Name == "staticID"), "source path and label ID are not relabeled as active file or staticID");
        var repeated = Read(); Check(JToken.DeepEquals(read, repeated), "repeat read is stable");
        var limited = Read(new JObject { ["limit"] = 1 });
        Check((int)limited["returned"] == 1 && (int)limited["matched"] == 2 && (bool)limited["truncated"], "default cache supports bounded response");
        var filtered = Read(new JObject { ["query"] = "LABEL-1", ["limit"] = 1 });
        Check((int)filtered["matched"] == 1 && (int)filtered["savedLabels"][0]["index"] == 1 && !(bool)filtered["truncated"], "query applies before limit and retains native order index");
        var zero = Read(new JObject { ["query"] = "absent" });
        Check((bool)zero["available"] && (int)zero["matched"] == 0 && ((JArray)zero["savedLabels"]).Count == 0, "known query zero is separate from unknown");
        Check(!(bool)Read(new JObject { ["query"] = new string('x', SavedModFootprint.StringLimit + 1) })["available"], "over-budget query does not manufacture results");
        SaveLoader.Active = "save-as.sav"; read = Read();
        Check((string)read["loadedSourceFilename"] == "loaded.sav" && (string)read["activeSaveAtLoad"] == "active.sav" &&
            (string)read["activeSaveFile"] == "save-as.sav" && (bool)read["activeSaveChangedSinceLoad"], "save-as pointer cannot relabel old loaded footprint");
        Game.Instance.Loading = true; Check(!(bool)Read()["available"], "transient loading is unavailable");
        Game.Instance.Loading = false; Grid.CellCount = 0; Check(!(bool)Read()["available"], "transient empty Grid is unavailable");
        Grid.CellCount = 1; Check((bool)Read()["available"], "same-owner accepted snapshot recovers after readiness without recapture");
        Game.Instance = new Game(); Check(!(bool)Read()["available"], "world owner replacement invalidates snapshot");
        Success(new List<NativeLabel>()); read = Read();
        Check((bool)read["available"] && (int)read["total"] == 0 && ((JArray)read["savedLabels"]).Count == 0, "successful empty list is known zero");
        Success(new List<NativeLabel> { new NativeLabel { id = null, title = null, version = -2, distribution_platform = (NativeLabel.DistributionPlatform)42 },
            new NativeLabel { id = "duplicate", title = "A" }, new NativeLabel { id = "duplicate", title = "B" } });
        read = Read(); Check(read["savedLabels"][0]["id"].Type == JTokenType.Null && (long)read["savedLabels"][0]["version"] == -2 &&
            (int)read["savedLabels"][0]["distributionPlatformValue"] == 42 && (int)read["returned"] == 3, "native nulls, raw scalar values and duplicates are preserved");

        foreach (string fault in new[] { "inner_false", "outer_false", "inner_exception", "outer_exception", "missing_body", "missing_observation",
            "null", "count_limit", "string_limit", "multiple_observation", "multiple_body", "duplicate_body_end", "wrong_loader", "changed_game" })
        {
            Setup(); file = File(); body = fault == "missing_body" ? null : Body(fault == "wrong_loader" ? new SaveLoader() : null);
            var labels = Labels(fault == "count_limit" ? SavedModFootprint.CaptureLimit + 1 : 2);
            if (fault == "string_limit") { var row = labels[0]; row.title = new string('x', SavedModFootprint.StringLimit + 1); labels[0] = row; }
            if (fault != "missing_observation") SavedModFootprint.Observe(fault == "null" ? null : labels);
            if (fault == "multiple_observation") SavedModFootprint.Observe(labels);
            if (fault == "multiple_body") { var other = Body(); Hook("BodyFinalizer", other, true, null); }
            if (fault == "duplicate_body_end") Hook("BodyFinalizer", body, true, null);
            End(file, body, fault != "inner_false", fault != "outer_false",
                fault == "inner_exception" ? new InvalidOperationException("native body fixture") : null,
                fault == "outer_exception" ? new InvalidOperationException("native post-try fixture") : null,
                fault == "changed_game" ? (System.Action)(() => Game.Instance = new Game()) : null);
            read = Read(); Check(!(bool)read["available"] && read["savedLabels"].Type == JTokenType.Null && read["total"].Type == JTokenType.Null,
                "failed/ambiguous/over-budget load never becomes known empty or revives old data: " + fault);
        }
        Setup(); file = File(); body = Body(); SavedModFootprint.Observe(Labels()); End(file, body);
        Hook("FileFinalizer", file, true, null); Check(!(bool)Read()["available"], "duplicate closed file finalizer cannot republish capture");
        Setup(); var outer = File("outer.sav"); var nested = File("nested.sav");
        Hook("FileFinalizer", outer, true, null);
        body = Body(); SavedModFootprint.Observe(Labels()); End(nested, body);
        Hook("FileFinalizer", outer, true, null); Check(!(bool)Read()["available"], "nested and out-of-order frames fail closed without replacing current TLS");
        Setup(); file = File("auto-save.sav"); body = Body(); SavedModFootprint.Observe(Labels()); SaveLoader.Active = "normalized-original.sav"; End(file, body);
        read = Read(); Check((string)read["loadedSourceFilename"] == "auto-save.sav" && (string)read["activeSaveAtLoad"] == "normalized-original.sav" &&
            !(bool)read["activeSaveChangedSinceLoad"], "native autosave normalization is separate from actual loaded source");
        SaveLoader.Instance = new SaveLoader(); Check(!(bool)Read()["available"], "loader replacement invalidates footprint");
        Success(); Game.Instance = null; Check(!(bool)Read()["available"], "menu/unload never exposes old labels");
        Check(SaveLoader.NativeCalls == 0 && Manager.MatchCalls == 0, "all reads and observer hooks perform no extra native save load or matcher evaluation");
        Setup(); Success();
        AsteroidGridEntity.NameReads = 0;
        Check(Read()["currentSaveContext"] == null && AsteroidGridEntity.NameReads == 0 &&
            ClusterManager.Instance.WorldContainers.All(world => world.ComponentLookups == 0), "default response does not enumerate current world context");
        var context = (JObject)Read(new JObject { ["includeSaveContext"] = true })["currentSaveContext"];
        Check((bool)context["available"] && (string)context["colonyName"] == "Native colony" && (int)context["worldCount"] == 3,
            "explicit context reads bounded current native fields, separate from saved labels");
        Check((int)context["worlds"][0]["worldId"] == 7 && (bool)context["worlds"][0]["isDiscovered"] &&
            !(bool)context["worlds"][0]["isModuleInterior"] && (string)context["worlds"][0]["clusterName"] == "Raw cluster name" &&
            !(bool)context["worlds"][1]["isDiscovered"] && (bool)context["worlds"][2]["isModuleInterior"], "raw IDs, native flags and exact asteroid names remain unfiltered");
        Check(new[] { 1, 2 }.All(index => !(bool)context["worlds"][index]["clusterNameAvailable"] &&
            context["worlds"][index]["clusterName"].Type == JTokenType.Null &&
            context["worlds"][index]["clusterNameSource"].Type == JTokenType.Null &&
            ClusterManager.Instance.WorldContainers[index].ComponentLookups == 0) &&
            ClusterManager.Instance.WorldContainers[0].ComponentLookups == 1 && AsteroidGridEntity.NameReads == 1,
            "undiscovered and interior rows keep flags but never read components or names");
        foreach (int index in new[] { 1, 2 })
            ((AsteroidGridEntity)ClusterManager.Instance.WorldContainers[index].Entity).ThrowOnName = true;
        context = (JObject)Read(new JObject { ["includeSaveContext"] = true })["currentSaveContext"];
        Check((bool)context["available"] && AsteroidGridEntity.NameReads == 2 &&
            new[] { 1, 2 }.All(index => ClusterManager.Instance.WorldContainers[index].ComponentLookups == 0),
            "excluded throwing Name getters cannot invalidate current context");
        ClusterManager.Instance.WorldContainers[1].IsDiscovered = true;
        ClusterManager.Instance.WorldContainers[2].IsModuleInterior = false;
        foreach (var world in ClusterManager.Instance.WorldContainers)
        {
            world.ComponentLookups = 0;
            ((AsteroidGridEntity)world.Entity).ThrowOnName = false;
        }
        AsteroidGridEntity.NameReads = 0;
        context = (JObject)Read(new JObject { ["includeSaveContext"] = true })["currentSaveContext"];
        Check((bool)context["available"] && (string)context["worlds"][1]["clusterName"] == "Undiscovered raw name" &&
            (string)context["worlds"][2]["clusterName"] == "Interior raw name" && AsteroidGridEntity.NameReads == 3 &&
            ClusterManager.Instance.WorldContainers.All(world => world.ComponentLookups == 1),
            "known names require explicitly discovered noninterior fixture worlds");
        Setup(); Success(); ClusterManager.Instance.WorldContainers[0].Entity = null;
        context = (JObject)Read(new JObject { ["includeSaveContext"] = true })["currentSaveContext"];
        Check((bool)context["available"] && !(bool)context["worlds"][0]["clusterNameAvailable"] &&
            context["worlds"][0]["clusterName"].Type == JTokenType.Null && context["worlds"][0]["clusterNameSource"].Type == JTokenType.Null,
            "missing component on a discovered noninterior world is explicit with no UI-name fallback");
        ClusterManager.Instance.WorldContainers[0].Entity = new UnsupportedAsteroid();
        context = (JObject)Read(new JObject { ["includeSaveContext"] = true })["currentSaveContext"];
        Check((bool)context["available"] && !(bool)context["worlds"][0]["clusterNameAvailable"], "unsupported Name override is never invoked or guessed");
        ClusterManager.Instance.WorldContainers[0].Entity = new UnsupportedClusterEntity();
        ClusterManager.Instance.WorldContainers[0].AdditionalAsteroid = new AsteroidGridEntity { RawName = "Wrong secondary component" };
        context = (JObject)Read(new JObject { ["includeSaveContext"] = true })["currentSaveContext"];
        Check((bool)context["available"] && !(bool)context["worlds"][0]["clusterNameAvailable"], "same base-component lookup as native migration cannot substitute a secondary asteroid component");
        foreach (string fault in new[] { "manager", "save_game", "worlds", "null_world", "count", "name_length", "cluster_name_length", "name_throw" })
        {
            Setup(); Success();
            if (fault == "manager") ClusterManager.Instance = null;
            if (fault == "save_game") SaveGame.Instance = null;
            if (fault == "worlds") ClusterManager.Instance.WorldContainers = null;
            if (fault == "null_world") ClusterManager.Instance.WorldContainers.Add(null);
            if (fault == "count") for (int i = 3; i < 129; i++) ClusterManager.Instance.WorldContainers.Add(new WorldContainer { id = i });
            if (fault == "name_length") SaveGame.Instance.BaseName = new string('x', 2049);
            if (fault == "cluster_name_length") ((AsteroidGridEntity)ClusterManager.Instance.WorldContainers[0].Entity).RawName = new string('x', 2049);
            if (fault == "name_throw") ((AsteroidGridEntity)ClusterManager.Instance.WorldContainers[0].Entity).ThrowOnName = true;
            var outerRead = Read(new JObject { ["includeSaveContext"] = true }); context = (JObject)outerRead["currentSaveContext"];
            Check((bool)outerRead["available"] && !(bool)context["available"] && context["worlds"].Type == JTokenType.Null &&
                context["worldCount"].Type == JTokenType.Null, "optional current context failure does not discard accepted saved labels or invent zero: " + fault);
        }
        Setup(); Success(); ClusterManager.Instance.WorldContainers.Clear();
        context = (JObject)Read(new JObject { ["includeSaveContext"] = true })["currentSaveContext"];
        Check((bool)context["available"] && (int)context["worldCount"] == 0 && ((JArray)context["worlds"]).Count == 0, "known empty current list is distinct from unavailable");
        Check(!(bool)Read(new JObject { ["includeSaveContext"] = "true" })["available"], "context opt-in requires actual boolean");
        Setup(); Success();
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            McpLocalization.LanguageProvider = () => language;
            Check((string)Read()["message"] != "save_status.observed" &&
                McpLocalization.Get("tools.game_save_control.description") != "tools.game_save_control.description", "affected runtime prose resolves: " + language);
        }
        McpLocalization.LanguageProvider = () => "en";
        Console.WriteLine("Saved MOD footprint: " + checks + " host checks (actual cache/patch/reader; native boundary substituted)");
    }
}
