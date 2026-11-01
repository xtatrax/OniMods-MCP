using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static Db seeded;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Build availability: " + message);
    }
    private static void Equal<T>(T actual, T expected, string label)
    { Check(EqualityComparer<T>.Default.Equals(actual, expected), label + " expected=" + expected + " actual=" + actual); }
    private static bool? NullableBool(JToken value)
    { return value == null || value.Type == JTokenType.Null ? (bool?)null : (bool)value; }
    private static void NullProperty(JObject value, string key, string label)
    { Check(value.Property(key) != null, label + " property exists"); Equal(value[key]?.Type, (JTokenType?)JTokenType.Null, label + " explicit null"); }
    private static BuildingDef Def(string id = "Tile", string name = null)
    {
        var def = new BuildingDef { PrefabID = id, Name = name ?? id, Desc = "Fixture definition", Category = "Base", SearchTerms = new[] { id } };
        Assets.BuildingDefs.Add(def); return def;
    }
    private static void Reset(bool cached = true)
    {
        Boundary.Reset(); Assets.BuildingDefs = new List<BuildingDef>(); Assets.MissingLookup = false;
        ElementLoader.elements = new List<Element>();
        DebugHandler.InstantBuildMode = false; Game.Instance = new Game();
        ClusterManager.Instance = new ClusterManager { activeWorldId = 7 };
        Research.Instance = new Research();
        seeded = new Db { Techs = new Database.Techs(), TechItems = new Database.TechItems(), Permits = new PermitSet() }; Db.Seed(cached ? seeded : null);
    }
    private static void NoMutation(string label)
    {
        Equal(Boundary.DbInitializationAttempts, 0, label + " no database initialization attempt");
        Equal(seeded?.Techs?.PredicateCalls ?? 0, 0, label + " no legacy Techs eligibility predicate");
        Equal(Boundary.GetOrAddCalls, 0, label + " no GetOrAdd");
        Equal(Boundary.UiCalls, 0, label + " no UI/write");
        Equal(Boundary.UtilityGuardCalls, 0, label + " no utility bypass lookup");
        Equal(Boundary.VirtualContextReads, 0, label + " no virtual write context lookup");
    }
    private static void GateDidNotMutate(string label)
    {
        Equal(Boundary.DbInitializationAttempts, 0, label + " no database initialization attempt");
        Equal(seeded?.Techs?.PredicateCalls ?? 0, 0, label + " no legacy Techs eligibility predicate");
        Equal(Boundary.GetOrAddCalls, 0, label + " no GetOrAdd");
        Equal(Boundary.UiCalls, 0, label + " no UI/write");
    }
    private static void AvailabilityGateCases()
    {
        // This first case is the semantic old-full-Catalog negative control:
        // its old Db.Get() attempts initialization and throws after compilation.
        Reset(false); var def = Def();
        Equal(BuildPlanningTools.HostAvailabilityError(def),
            "Building availability is unknown for Tile: database_cache_missing", "missing cache stays unknown");
        Equal(Boundary.DbGetCalls, 0, "missing cache gate never calls lazy getter");
        Check(Db.CachedForFixture == null, "gate does not create or replace database cache");
        Equal(def.AvailabilityCalls, 1, "gate captures native definition once");
        GateDidNotMutate("missing cache gate");

        Reset(false); def = Def();
        var captured = BuildPlanningTools.HostCapturedAvailabilityError(def);
        NullProperty(captured, "unlocked", "unknown gate research");
        NullProperty(captured, "availableNow", "unknown gate availability");
        Equal((bool)captured["definitionAvailable"], true, "independent known definition retained");
        Equal((string)captured["availability"]["reasonCode"], "database_cache_missing", "captured missing-cache provenance");
        Equal(def.AvailabilityCalls, 1, "metadata reuses original gate capture");
        GateDidNotMutate("captured missing cache");

        Reset(); def = Def(); seeded.TechItems.Predicate = _ => false;
        Equal(BuildPlanningTools.HostAvailabilityError(def), "Building is locked by research: Tile", "known locked gate");
        Equal(seeded.TechItems.PredicateCalls, 1, "gate uses native TechItems once");
        Equal(Boundary.UtilityGuardCalls, 0, "ordinary locked gate avoids utility bypass lookup");
        GateDidNotMutate("known locked gate");
        Reset(); def = Def(); Required("Tile", false); seeded.TechItems.resources[0].isPOIUnlock = true;
        Research.Instance.Existing[0].UnlockedPOITechIds.Add("Tile");
        Equal(BuildPlanningTools.HostAvailabilityError(def), null, "native individual POI unlock permits gate");
        GateDidNotMutate("individual POI gate");
        Reset(); def = Def();
        Equal(BuildPlanningTools.HostAvailabilityError(def), null, "known available gate");
        Equal(def.AvailabilityCalls, 1, "available gate one native definition read");
        GateDidNotMutate("known available gate");

        Reset(); def = Def(); def.NativeAvailable = false;
        Equal(BuildPlanningTools.HostAvailabilityError(def), "Building is not available in current context: Tile", "definition unavailable gate");
        GateDidNotMutate("definition unavailable gate");
        Reset(false); def = Def(); def.NativeAvailable = false;
        captured = BuildPlanningTools.HostCapturedAvailabilityError(def);
        Equal((string)captured["error"], "Building is not available in current context: Tile", "known native refusal is sufficient despite unknown research");
        NullProperty(captured, "unlocked", "native refusal retains independent unknown research");
        Equal((bool)captured["availableNow"], false, "known native refusal retains false aggregate");
        GateDidNotMutate("independent definition false/research unknown gate");
        Reset(); def = Def(); Research.Instance = null;
        Equal(BuildPlanningTools.HostAvailabilityError(def), "Building availability is unknown for Tile: research_controller_missing", "missing controller distinct from locked");
        GateDidNotMutate("missing controller gate");
        Reset(); def = Def(); def.AvailabilityPredicate = () => { throw new InvalidOperationException("fixture definition unavailable"); };
        Equal(BuildPlanningTools.HostAvailabilityError(def), "Building availability is unknown for Tile: definition_read_failed", "definition exception distinct from false");
        Equal(def.AvailabilityCalls, 1, "failed getter not retried");
        GateDidNotMutate("definition exception gate");
        Reset();
        Equal(BuildPlanningTools.HostAvailabilityError(null), "Building def is missing", "missing definition gate");
        GateDidNotMutate("missing definition gate");

        foreach (bool sandbox in new[] { false, true })
        {
            Reset(false); def = Def(); Research.Instance = null;
            if (sandbox) Game.Instance.SandboxModeActive = true; else DebugHandler.InstantBuildMode = true;
            Equal(BuildPlanningTools.HostAvailabilityError(def), null, "authorized free-build context bypasses missing research");
            Equal(seeded.TechItems.PredicateCalls, 0, "free-build gate does not evaluate research");
            def.NativeAvailable = false;
            Equal(BuildPlanningTools.HostAvailabilityError(def), "Building is not available in current context: Tile", "free-build still requires native definition");
            GateDidNotMutate("free-build gate");
        }
        for (int mask = 0; mask < 16; mask++)
        {
            Reset(); def = Def("Wire"); seeded.TechItems.Predicate = _ => false;
            Boundary.VirtualEditContext = (mask & 4) != 0; Boundary.ExactUtility = (mask & 8) != 0;
            var args = new JObject { ["instantBuild"] = (mask & 1) != 0, ["allowSandbox"] = (mask & 2) != 0 };
            Equal(BuildPlanningTools.HostAvailabilityError(def, args), mask == 15 ? null : "Building is locked by research: Wire", "exact utility bypass guard " + mask);
            Equal(Boundary.VirtualContextReads, (mask & 3) == 3 ? 1 : 0, "consent before context read " + mask);
            Equal(Boundary.UtilityGuardCalls, (mask & 7) == 7 ? 1 : 0, "context before native utility read " + mask);
            GateDidNotMutate("utility bypass " + mask);
        }
        Reset(false); def = Def("Wire"); Boundary.VirtualEditContext = Boundary.ExactUtility = true;
        captured = BuildPlanningTools.HostCapturedAvailabilityError(def, new JObject { ["instantBuild"] = true, ["allowSandbox"] = true });
        NullProperty(captured, "error", "explicit utility bypass allowed");
        NullProperty(captured, "unlocked", "bypass does not fabricate research true");
        NullProperty(captured, "availableNow", "bypass preserves unknown aggregate");
        Equal((string)captured["availability"]["status"], "unknown", "bypassed missing cache still unknown");
        GateDidNotMutate("unknown utility bypass");
    }
    private static JObject Observe(BuildingDef def, bool? definition, bool? unlocked, bool? available, string status, string label)
    {
        var result = BuildingAvailabilityReader.Read(def);
        Equal(result.DefinitionAvailable, definition, label + " definition");
        Equal(result.Unlocked, unlocked, label + " unlocked");
        Equal(result.Value, available, label + " available");
        var meta = BuildPlanningTools.HostJson(result.ToDictionary());
        Equal((string)meta["scope"], "definition_and_research", label + " scope");
        Equal((string)meta["status"], status, label + " status");
        Check(meta.Properties().All(x => new[] { "status", "scope", "reasonCode", "researchSource" }.Contains(x.Name)), label + " compact metadata");
        NoMutation(label); return meta;
    }
    private static Database.Tech Required(string prefabId, bool? complete)
    {
        var tech = new Database.Tech { Id = "FixtureTech" };
        tech.unlockedItemIDs.Add(prefabId);
        var item = new TechItem { Id = prefabId, parentTechId = tech.Id };
        tech.unlockedItems.Add(item); seeded.TechItems.resources.Add(item);
        seeded.Techs.resources.Add(tech);
        if (complete.HasValue) Research.Instance.Existing.Add(new TechInstance { tech = tech, Complete = complete.Value });
        return tech;
    }
    private static void NativeUiPoiCases()
    {
        Reset(); var def = Def(); Required("Tile", false);
        seeded.TechItems.resources[0].isPOIUnlock = true;
        Research.Instance.Existing[0].UnlockedPOITechIds.Add("Tile");
        Equal(seeded.Techs.IsTechItemComplete("Tile"), false, "parent research remains incomplete");
        seeded.Techs.PredicateCalls = 0;
        var meta = Observe(def, true, true, true, "available", "POI item unlocked despite incomplete parent research");
        Equal((string)meta["researchSource"], "native_tech_items", "POI delegates normal UI predicate");
        Check(Boundary.DbGetCalls > 0, "native parent lookup uses permitted cached Db.Get");
        Equal(Research.Instance.Existing.Count, 1, "POI read creates no research instance");
        Equal(Research.Instance.Existing[0].UnlockedPOITechIds.Count, 1, "POI read leaves individual unlock cache unchanged");
        Research.Instance.Existing[0].UnlockedPOITechIds.Clear();
        Observe(def, true, false, false, "unavailable", "same POI item not individually unlocked");

        Reset(); def = Def(); Required("Tile", null); seeded.TechItems.resources[0].isPOIUnlock = true;
        Observe(def, true, false, false, "unavailable", "POI without started native instance");
        Equal(Research.Instance.Existing.Count, 0, "POI no instance remains uncreated");
        Reset(); def = Def(); Required("Tile", false); seeded.TechItems.resources[0].isPOIUnlock = true;
        Research.Instance.Existing[0].UnlockedPOITechIds = null;
        meta = Observe(def, true, null, null, "unknown", "POI unlock collection missing");
        Equal((string)meta["reasonCode"], "research_read_failed", "POI cache fault stays unknown");
        Check(Research.Instance.Existing[0].UnlockedPOITechIds == null, "POI missing collection not repaired");
        Reset(); def = Def(); Required("Tile", true); seeded.TechItems.resources[0].isPOIUnlock = true;
        Research.Instance.Existing[0].UnlockedPOITechIds = null;
        Observe(def, true, true, true, "available", "complete parent does not require POI unlock list");
        Reset(); def = Def(); Required("Tile", false); seeded.Techs.resources.Clear();
        Observe(def, true, null, null, "unknown", "required parent tech missing");
    }
    private static void ReaderCases()
    {
        Reset(); var def = Def();
        var meta = Observe(def, true, true, true, "available", "complete");
        Equal((string)meta["researchSource"], "native_tech_items", "normal native provenance");
        NullProperty(meta, "reasonCode", "available reason");
        Equal(seeded.TechItems.PredicateCalls, 1, "native predicate once");
        Equal(seeded.TechItems.LastPrefabId, "Tile", "exact native identity");
        Equal(def.AvailabilityCalls, 1, "native definition once");

        Reset(); def = Def(); Required("Tile", null);
        Observe(def, true, false, false, "unavailable", "unstarted research");
        Equal(Research.Instance.Existing.Count, 0, "unstarted remains uncreated");
        Equal(seeded.TechItems.PredicateCalls, 1, "unstarted native final predicate");
        Equal(Research.Instance.GetCalls, 1, "unstarted native lookup only");

        foreach (bool complete in new[] { false, true })
        {
            Reset(); def = Def(); Required("Tile", complete);
            Observe(def, true, complete, complete, complete ? "available" : "unavailable", "cached research " + complete);
            Equal(Research.Instance.Existing.Count, 1, "cached research count unchanged");
        }

        Reset(); def = Def(); seeded.TechItems.Predicate = _ => false;
        Observe(def, true, false, false, "unavailable", "native override false without mapping");
        Equal(seeded.TechItems.resources.Count, 0, "empty catalog legitimate native false");
        Equal(seeded.TechItems.PredicateCalls, 1, "empty catalog delegates");
        Reset(); def = Def(); Required("Tile", false); seeded.TechItems.Predicate = _ => true;
        Observe(def, true, true, true, "available", "native override true despite fixture cache");
        Equal(Research.Instance.GetCalls, 0, "no independent custom research lookup");

        Reset(false); def = Def();
        meta = Observe(def, true, null, null, "unknown", "missing Db cache");
        Check(!string.IsNullOrWhiteSpace((string)meta["reasonCode"]), "missing cache reason");
        Equal(seeded.TechItems.PredicateCalls, 0, "missing Db no native predicate");
        Reset(); def = Def(); Required("Tile", false); seeded.Techs = null;
        Observe(def, true, null, null, "unknown", "missing required parent Techs cache");
        Reset(); def = Def(); seeded.TechItems = null;
        meta = Observe(def, true, null, null, "unknown", "missing TechItems cache");
        Equal((string)meta["reasonCode"], "tech_item_catalog_missing", "missing UI catalog reason");
        Reset(); def = Def(); Required("Tile", false); seeded.Techs.resources = null;
        meta = Observe(def, true, null, null, "unknown", "missing parent tech resource collection");
        Equal((string)meta["reasonCode"], "tech_resources_missing", "missing parent resources reason");
        Equal(seeded.TechItems.PredicateCalls, 0, "missing parent resources before native predicate");
        Reset(); def = Def(); seeded.TechItems.resources = null;
        meta = Observe(def, true, null, null, "unknown", "missing resource collection");
        Equal((string)meta["reasonCode"], "tech_item_resources_missing", "missing UI resources reason");
        Equal(seeded.TechItems.PredicateCalls, 0, "missing resources no predicate");
        Reset(); def = Def(); Research.Instance = null;
        Observe(def, true, null, null, "unknown", "missing research controller");
        Equal(seeded.TechItems.PredicateCalls, 0, "missing research no predicate");
        Reset(); def = Def(); seeded.TechItems.Predicate = _ => { throw new InvalidOperationException("fixture native lookup fault"); };
        Observe(def, true, null, null, "unknown", "native getter fault");
        Equal(seeded.TechItems.PredicateCalls, 1, "fault no retry");

        Reset(); def = Def(); def.NativeAvailable = false;
        Observe(def, false, true, false, "unavailable", "definition unavailable");
        Reset(); def = Def(); def.NativeAvailable = false; Research.Instance = null;
        meta = Observe(def, false, null, false, "unavailable", "definition false with research unknown");
        Equal((string)meta["reasonCode"], "definition_unavailable", "definition false reason preserved");
        Reset(); def = Def(); def.AvailabilityPredicate = () => { throw new InvalidOperationException("fixture definition fault"); };
        Observe(def, null, true, null, "unknown", "definition getter fault");
        Reset(); def = Def(); def.AvailabilityPredicate = () => { throw new InvalidOperationException("fixture definition fault"); }; seeded.TechItems.Predicate = _ => false;
        Observe(def, null, false, false, "unavailable", "unknown definition with known locked research");
        Reset(); def = Def("");
        meta = Observe(def, true, null, null, "unknown", "missing prefab identity");
        Equal((string)meta["reasonCode"], "prefab_id_missing", "missing identity reason");
        Equal(seeded.TechItems.PredicateCalls, 0, "missing identity no native predicate");
        Reset(); def = Def(); seeded.TechItems.resources.Add(null);
        meta = Observe(def, true, null, null, "unknown", "malformed native resource entry");
        Equal((string)meta["reasonCode"], "research_read_failed", "native resource fault remains unknown");
        Equal(seeded.TechItems.resources.Count, 1, "malformed resource not repaired");
        Reset(); meta = Observe(null, null, null, null, "unknown", "missing definition");
        Equal((string)meta["reasonCode"], "definition_missing", "missing definition reason");
        NullProperty(meta, "researchSource", "missing definition has no research provenance");

        foreach (string mode in new[] { "sandbox", "instant_build" })
        {
            Reset(false); def = Def(); Research.Instance = null;
            if (mode == "sandbox") Game.Instance.SandboxModeActive = true;
            else DebugHandler.InstantBuildMode = true;
            meta = Observe(def, true, true, true, "available", mode);
            Equal((string)meta["researchSource"], mode, mode + " provenance");
            Equal(seeded.TechItems.PredicateCalls, 0, mode + " no research evaluation");
            def.NativeAvailable = false;
            Observe(def, false, true, false, "unavailable", mode + " cannot bypass definition");
        }
        Reset(false); def = Def(); Game.Instance.SandboxModeActive = true; DebugHandler.InstantBuildMode = true;
        meta = Observe(def, true, true, true, "available", "both bypass modes");
        Equal((string)meta["researchSource"], "instant_build", "instant provenance priority");
    }
    private static void CandidateCases()
    {
        foreach (bool? known in new bool?[] { true, false, null })
        {
            Reset(); var def = Def("Tile");
            if (!known.HasValue) Research.Instance = null; else seeded.TechItems.Predicate = _ => known.Value;
            var exact = BuildPlanningTools.HostCandidates("tile", "", true);
            Equal(exact.Count, 1, "exact candidate count");
            var row = (JObject)exact[0];
            Equal((string)row["prefabId"], "Tile", "exact canonical prefab identity");
            Equal((string)row["matchKind"], "prefabId", "exact factory");
            Equal(NullableBool(row["availableNow"]), known, "exact nullable availability " + known);
            if (!known.HasValue) NullProperty(row, "availableNow", "exact unknown");
            Equal((string)row["availability"]?["scope"], "definition_and_research", "exact metadata scope");
            Equal((string)row["availability"]?["status"], !known.HasValue ? "unknown" : known.Value ? "available" : "unavailable", "exact status");
            Equal(def.AvailabilityCalls, 1, "exact captures once"); NoMutation("exact " + known);
            var plan = BuildPlanningTools.HostPlan(new JObject { ["prefabId"] = "tile" });
            Equal((string)plan["resolved"]?["prefabId"], "Tile", "full parser exact canonical identity");
            var planRow = (JObject)plan["buildingCandidates"][0];
            Equal(NullableBool(planRow["availableNow"]), known, "full parser exact nullable availability");
            Equal((string)planRow["availability"]?["status"], !known.HasValue ? "unknown" : known.Value ? "available" : "unavailable", "full parser exact metadata");
            var filteredExact = BuildPlanningTools.HostCandidates("tile", "", false);
            Equal(filteredExact.Count, known == true ? 1 : 0, "exact false filter is observed-true only");
            NoMutation("full and filtered exact " + known);
        }

        Reset(); Def("Battery", "Localized Battery");
        var alias = BuildPlanningTools.HostCandidates(null, "电池", true);
        Equal((string)alias[0]["prefabId"], "Battery", "alias canonical identity");
        Equal(NullableBool(alias[0]["availableNow"]), true, "alias availability");
        Check(alias[0]["availability"] != null, "alias metadata"); NoMutation("alias");

        Reset(); var available = Def("ZAvailable", "Shared Unit");
        var unavailable = Def("ALocked", "Shared Unit");
        var unknown = Def("BUnknown", "Shared Unit");
        var nonmatching = Def("IrrelevantDeepSpaceCatalog", "Different Catalog Family");
        nonmatching.AvailabilityPredicate = () => { throw Boundary.Forbidden("nonmatching native definition"); };
        unknown.AvailabilityPredicate = () => { throw new InvalidOperationException("fixture definition absent"); };
        seeded.TechItems.Predicate = id => id != "ALocked";
        var all = BuildPlanningTools.HostCandidates(null, "Shared Unit", true);
        Equal(nonmatching.AvailabilityCalls, 0, "include-unavailable nonmatching definition never read");
        Equal(all.Count, 3, "all retains three statuses");
        Equal((string)all[0]["prefabId"], "ZAvailable", "same score available sorts first");
        Equal((string)all[1]["prefabId"], "ALocked", "same score false before null");
        Equal((string)all[2]["prefabId"], "BUnknown", "same score unknown retained last");
        var filtered = BuildPlanningTools.HostCandidates(null, "Shared Unit", false);
        Equal(nonmatching.AvailabilityCalls, 0, "available-only nonmatching definition never read");
        Equal(filtered.Count, 1, "read filter excludes false and unknown");
        Equal((string)filtered[0]["prefabId"], "ZAvailable", "read filter exact true");
        NoMutation("candidate filter");
        Equal(available.AvailabilityCalls, 2, "available candidate once each read");
        Equal(unavailable.AvailabilityCalls, 2, "unavailable candidate once each read");
        Equal(unknown.AvailabilityCalls, 2, "unknown candidate once each read");

        foreach (bool missingDefinition in new[] { false, true })
        {
            Reset(); Def("Tile"); Def("Wire"); Assets.MissingLookup = missingDefinition;
            var plan = BuildPlanningTools.HostPlan(new JObject { ["plan"] = "Tile-Wire" });
            var first = (JObject)plan["buildingCandidates"][0];
            Equal((string)first["matchKind"], "sequence_first", "sequence first factory");
            Equal((string)first["prefabId"], "Tile", "sequence first identity");
            Equal(NullableBool(first["availableNow"]), missingDefinition ? (bool?)null : true, "sequence missing definition not fabricated true");
            if (missingDefinition) NullProperty(first, "availableNow", "sequence first missing def");
            Equal((string)first["availability"]?["status"], missingDefinition ? "unknown" : "available", "sequence first status");
            Check(((JArray)plan["sequenceItems"]).All(item => ((JArray)item["buildingCandidates"]).All(row => row["availability"] != null)), "all sequence candidate metadata");
            NoMutation("sequence " + missingDefinition);
        }
        Reset(); Def("Tile");
        var single = BuildPlanningTools.HostPlan(new JObject { ["plan"] = "Tile" });
        Equal((string)single["buildingCandidates"][0]["matchKind"], "sequence", "single sequence promotion factory");
        Check(single["buildingCandidates"][0]["availability"] != null, "single sequence promotion metadata");

        foreach (int requestedLimit in new[] { 0, 3, 99 })
        {
            Reset();
            for (int i = 0; i < 23; i++) Def("Shared" + i.ToString("D2"), "Shared Unit");
            var plan = BuildPlanningTools.HostPlan(new JObject { ["plan"] = "SharedUnit", ["limit"] = requestedLimit });
            Equal(((JArray)plan["buildingCandidates"]).Count, Math.Max(1, Math.Min(requestedLimit, 20)), "candidate limit clamps " + requestedLimit);
            NoMutation("limits");
        }
    }
    private static void CatalogCases()
    {
        foreach (bool? known in new bool?[] { true, false, null })
        {
            Reset(); var def = Def();
            if (!known.HasValue) Research.Instance = null; else seeded.TechItems.Predicate = _ => known.Value;
            var row = BuildPlanningTools.HostCatalog(def);
            Equal(NullableBool(row["availableNow"]), known, "catalog availability " + known);
            Equal(NullableBool(row["unlocked"]), known, "catalog research " + known);
            if (!known.HasValue) { NullProperty(row, "availableNow", "catalog unknown availability"); NullProperty(row, "unlocked", "catalog unknown unlocked"); }
            Equal((string)row["availability"]?["scope"], "definition_and_research", "catalog scope");
            Equal(def.AvailabilityCalls, 1, "catalog native definition captured once");
            Equal(seeded.TechItems.PredicateCalls, known.HasValue ? 1 : 0, "catalog research captured once");
            Equal((string)row["autoMaterialReason"], !known.HasValue ? "building_availability_unknown" : known.Value ? "no_currently_available_material" : "building_locked_by_research", "catalog auto-material reason");
            NoMutation("catalog " + known);
        }
        Reset(); var unavailable = Def(); unavailable.NativeAvailable = false; Research.Instance = null;
        var unavailableRow = BuildPlanningTools.HostCatalog(unavailable);
        Equal(NullableBool(unavailableRow["availableNow"]), false, "catalog definition false known");
        NullProperty(unavailableRow, "unlocked", "catalog definition false independent unknown research");
        Equal((string)unavailableRow["autoMaterialReason"], "building_not_available_in_current_context", "definition false auto-material reason");
        NoMutation("catalog independent false/null");

        Reset(false); var defAfter = Def();
        Boundary.MaterialRead = () => Db.Seed(seeded);
        var after = BuildPlanningTools.HostCatalog(defAfter);
        NullProperty(after, "availableNow", "catalog captures before material callback");
        NullProperty(after, "unlocked", "catalog research captures before callback");
        Equal(seeded.TechItems.PredicateCalls, 0, "catalog does not reread after later cache initialization");
        Equal(defAfter.AvailabilityCalls, 1, "catalog definition no reread");
        NoMutation("catalog capture order");

        Reset(); var cosmetic = Def(); cosmetic.AvailableFacades = new[] { "fixture-facade" };
        bool cosmeticCanary = false;
        try { BuildPlanningTools.HostCatalog(cosmetic); }
        catch (InvalidOperationException error) { cosmeticCanary = error.Message.Contains("Permit lookup"); }
        Check(cosmeticCanary, "nonempty cosmetic catalog branch acknowledged as existing eager boundary");
        Equal(Boundary.DbGetCalls, 1, "cosmetic cached Db.Get observed once");
        Equal(Boundary.DbInitializationAttempts, 0, "cosmetic cached Get is not initialization");
        Equal(cosmetic.AvailabilityCalls, 1, "availability captured before cosmetic boundary");
    }
    private static void LocalizationCases()
    {
        const string key = "building_availability.include_unavailable";
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            string text = McpLocalization.GetForLanguage(language, key);
            Check(text != key && !string.IsNullOrWhiteSpace(text), language + " runtime localization resolved");
            Check(text.Contains("false"), language + " preserves default false");
            Check(text.Contains(language == "en" ? "unknown" : language == "ja" ? "未取得" : "未知"), language + " distinguishes unknown");
            Check(text.Contains(language == "en" ? "materials" : language == "ja" ? "素材" : "材料"), language + " preserves limited scope");
        }
        Check(McpLocalization.GetForLanguage("en", key) != McpLocalization.GetForLanguage("ja", key), "independent language resources");
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            foreach (string ownedKey in new[] { "definition_missing", "research_locked", "definition_unavailable", "unknown" })
            {
                string fullKey = "building_availability.error." + ownedKey;
                string value = McpLocalization.GetForLanguage(language, fullKey);
                Check(value != fullKey && !string.IsNullOrWhiteSpace(value), language + " gate error resolves " + ownedKey);
                Check((ownedKey == "definition_missing") == !value.Contains("{0}"), language + " prefab placeholder scope " + ownedKey);
                Check((ownedKey == "unknown") == value.Contains("{1}"), language + " unknown reason placeholder scope " + ownedKey);
            }
            McpLocalization.LanguageProvider = () => language;
            Reset(false); var def = Def();
            string unknown = BuildPlanningTools.HostAvailabilityError(def);
            Check(unknown.Contains("Tile") && unknown.Contains("database_cache_missing"), language + " gate formats prefab and exact reason code");
            Check(unknown.Contains(language == "en" ? "unknown" : language == "ja" ? "未取得" : "未知"), language + " error distinguishes unknown");
            GateDidNotMutate(language + " localized gate");
        }
        McpLocalization.LanguageProvider = () => "en";
    }
    private static void Main()
    {
        McpLocalization.LanguageProvider = () => "en";
        AvailabilityGateCases();
        NativeUiPoiCases(); ReaderCases(); CandidateCases(); CatalogCases(); LocalizationCases();
        Console.WriteLine("Build availability: " + checks + " checks PASS");
    }
}
