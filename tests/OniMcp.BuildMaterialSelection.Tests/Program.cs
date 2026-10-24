using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static readonly JArray observations = new JArray();
    private static readonly JArray countBudgets = new JArray();
    private static readonly string[] OutputKeys = {
        "valid", "mode", "requested", "requirementKnown", "requirementSource", "quantityScope", "requiredKg", "selectedAvailableKg",
        "satisfied", "shortageKg", "quantityStatus", "inventoryScope", "selected", "elements", "availableMaterials", "candidateMaterials",
        "fallbackMaterial", "next", "suggestion", "error"
    };

    private static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            Cases();
            int behaviorChecks = checks;
            // All fixed-input behavior cases run before the baseline-negative budget check.
            foreach (JObject budget in countBudgets)
                budget["passed"] = (int)budget["inventoryReads"] == (int)budget["expectedInventoryReads"]
                    && (int)budget["discoveryReads"] == (int)budget["expectedDiscoveryReads"]
                    && (int)budget["pickupEnumerations"] == (int)budget["expectedPickupEnumerations"];
            bool budgetsPass = countBudgets.All(b => (bool)b["passed"]);
            var report = new JObject {
                ["status"] = budgetsPass ? "HOST_FIXED_INPUT_BEHAVIOR_AND_REUSE_BUDGETS_PASS" : "HOST_FIXED_INPUT_BEHAVIOR_PASS_REUSE_BUDGET_FAILED",
                ["nativeAcceptance"] = false, ["parentTodoComplete"] = false, ["stableAnchor"] = "todo-03",
                ["behaviorChecks"] = behaviorChecks, ["behaviorCases"] = observations.Count,
                ["sourceFiles"] = SourceHashes(), ["observations"] = observations, ["callCountBudgets"] = countBudgets,
                ["negativeContract"] = "Compile the exact frozen before file with the same runner; behavior must pass before the reuse budgets fail."
            };
            Console.WriteLine(report.ToString(Formatting.Indented));
            return budgetsPass ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine(new JObject { ["status"] = "HOST_BEHAVIOR_OR_BOUNDARY_FAILURE", ["checksBeforeFailure"] = checks,
                ["error"] = ex.ToString(), ["observations"] = observations }.ToString(Formatting.Indented));
            return 2;
        }
    }

    private static void Cases()
    {
        Run("explicit_id", Base, "MatA", r => {
            Success(r, "explicit", "MatA", "MatA"); Sequence(r, "availableMaterials", "tag", "MatA", "MatB");
            Equal(r["requiredKg"], 100d, "required mass"); Equal(r["selectedAvailableKg"], 200d, "available mass");
            Equal(r["quantityStatus"], "sufficient", "quantity status"); Equal(r["shortageKg"], 0d, "surplus shortage");
        }, 2);
        Run("explicit_case_trim", Base, "  mAtB  ", r => Success(r, "explicit", "mAtB", "MatB"), 2);
        Run("explicit_native_name", Base, "Beta", r => Success(r, "explicit", "Beta", "MatB"), 2);
        Run("explicit_name_substring", Base, "et", r => Success(r, "explicit", "et", "MatB"), 2);
        Run("explicit_localized_native_name", () => { var d = Base(); Boundary.Names["MatB"] = "銅鉱石"; return d; }, "銅鉱石",
            r => Success(r, "explicit", "銅鉱石", "MatB"), 2);
        Run("first_sorted_partial_match_precedes_later_exact_match", () => {
            var d = EmptyDefinition(); Boundary.AddMaterial(d, "Cuprite", "Exact", 10f); Boundary.AddMaterial(d, "CupriteRich", "Earlier", 200f); return d;
        }, "Cuprite", r => Success(r, "explicit", "Cuprite", "CupriteRich"));
        Run("equal_mass_tag_order", () => {
            var d = Base(); d.Defaults.Reverse(); Boundary.SetAllMasses(10f); return d;
        }, null, r => { Success(r, "auto", "auto", "MatA"); Sequence(r, "availableMaterials", "tag", "MatA", "MatB", "MatZ"); }, 1);
        Run("unknown_first_twenty_candidates", Many, "NoSuchMaterial", r => {
            Invalid(r, "Material 'NoSuchMaterial' is not valid for FixtureBuilding");
            Sequence(r, "candidateMaterials", "tag", Enumerable.Range(1, 20).Select(i => "Mat" + i.ToString("D2")).ToArray());
            Check(((List<Dictionary<string, object>>)r["availableMaterials"]).Count == 20, "available output stays capped at twenty");
        });
        Run("match_after_first_twenty", Many, "Mat25", r => Success(r, "explicit", "Mat25", "Mat25"));
        Run("unknown_with_fallback", Base, "NoSuchMaterial", r => {
            Invalid(r, "Material 'NoSuchMaterial' is not valid for FixtureBuilding");
            Sequence(r, "candidateMaterials", "tag", "MatA", "MatB", "MatZ"); Equal(r["fallbackMaterial"], "MatA", "fallback id");
        }, 2);
        Run("zero_stock_explicit_keeps_selected", Base, "MatZ", r => {
            Invalid(r, "Material 'MatZ' is valid for FixtureBuilding, but none is currently available");
            Equal(Selected(r)["tag"], "MatZ", "zero stock selected row kept"); Equal(r["selectedAvailableKg"], 0d, "known zero stock");
            Equal(r["satisfied"], false, "zero stock is insufficient"); Sequence(r, "candidateMaterials", "tag", "MatA", "MatB", "MatZ");
        }, 2);
        foreach (string request in new string[] { null, "", "   ", "auto", "AuTo", "DeFaUlT" })
        {
            string captured = request;
            string requested = string.IsNullOrWhiteSpace(captured) ? "auto" : captured.Trim();
            Run("auto_alias_" + (captured == null ? "null" : captured.Replace(" ", "_")), Base, captured,
                r => Success(r, "auto", requested, "MatA"), 1);
        }
        Run("auto_no_inventory", () => { var d = Base(); Boundary.SetAllMasses(0f); return d; }, null, r => {
            Invalid(r, "No available build material in current world inventory");
            Sequence(r, "availableMaterials", "tag"); Sequence(r, "candidateMaterials", "tag", "MatA", "MatB", "MatZ");
        }, 2);
        Run("category_success", Base, "Metal", r => Success(r, "category:Metal", "Metal", "MatA"), 1);
        Run("category_case_preserves_mode", Base, "mEtAl", r => Success(r, "category:Metal", "mEtAl", "MatA"), 1);
        Run("empty_category_falls_through", () => { var d = Base(); Boundary.SetAllMasses(0f); return d; }, "Metal", r => {
            Invalid(r, "Material 'Metal' is not valid for FixtureBuilding"); Sequence(r, "candidateMaterials", "tag", "MatA", "MatB", "MatZ");
        }, 2);
        Run("sandbox_auto_defaults_and_free_quantity", () => {
            var d = Base(); Boundary.SetAllMasses(0f); Game.Instance.SandboxModeActive = true; return d;
        }, null, r => {
            Equal(r["valid"], true, "sandbox auto valid"); Equal(r["mode"], "default_no_inventory_in_debug", "sandbox default mode");
            Check(r["selected"] == null, "free default does not invent selected material");
            SequenceStrings(r, "elements", "MatA", "MatB", "MatZ"); Equal(r["quantityStatus"], "not_required", "free quantity");
        }, 1);
        Run("normal_rules_override_sandbox", () => {
            var d = Base(); Boundary.SetAllMasses(0f); Game.Instance.SandboxModeActive = true; WorldEditorTools.EnforceNormalMaterialRules = true; return d;
        }, null, r => Invalid(r, "No available build material in current world inventory"), 2);
        Run("debug_auto_defaults", () => {
            var d = Base(); Boundary.SetAllMasses(0f); DebugHandler.InstantBuildMode = true; return d;
        }, "default", r => { Equal(r["valid"], true, "debug auto valid"); Equal(r["mode"], "default_no_inventory_in_debug", "debug default mode"); }, 1);
        Run("free_explicit_zero", () => { var d = Base(); Game.Instance.SandboxModeActive = true; return d; }, "MatZ", r => {
            Success(r, "explicit", "MatZ", "MatZ"); Equal(r["quantityStatus"], "not_required", "free explicit quantity");
        }, 2);
        Run("free_empty_defaults", () => { Game.Instance.SandboxModeActive = true; return EmptyDefinition(); }, null,
            r => Invalid(r, "No material selected."));
        Run("normal_empty_catalog_auto", EmptyDefinition, null, r => {
            Invalid(r, "No available build material in current world inventory"); Sequence(r, "candidateMaterials", "tag");
            Check(r["fallbackMaterial"] == null && r["selected"] == null, "empty auto catalog does not invent fallback or selected");
        });
        Run("normal_empty_catalog_explicit", EmptyDefinition, "Unknown", r => {
            Invalid(r, "Material 'Unknown' is not valid for FixtureBuilding"); Sequence(r, "candidateMaterials", "tag");
            Check(r["fallbackMaterial"] == null, "empty explicit catalog does not invent fallback");
        });
        Run("missing_element_validation_kept", () => {
            var d = EmptyDefinition(); d.Defaults.Add(new Tag("Ghost")); Boundary.Names["Ghost"] = "Ghost";
            ClusterManager.Instance.Worlds[0].worldInventory.Amounts["Ghost"] = 20f;
            var kpid = new KPrefabID { PrefabTag = new Tag("Ghost") }; kpid.Tags.Add(new Tag("Metal"));
            Assets.Prefabs[new Tag("Ghost")] = new UnityEngine.GameObject().Add(kpid); return d;
        }, "Ghost", r => Invalid(r, "Material 'Ghost' is not a valid build material."));
        Run("cluster_unavailable_remains_zero_stock", () => { var d = Base(); ClusterManager.Instance = null; return d; }, "MatA",
            r => Invalid(r, "Material 'MatA' is valid for FixtureBuilding, but none is currently available"));
        Run("multiple_slots_stay_unknown", () => { var d = Base(); d.Mass = new[] { 100f, 200f }; return d; }, "MatA", r => {
            Success(r, "explicit", "MatA", "MatA"); Equal(r["requirementKnown"], false, "multi-slot requirement is not asserted");
            Check(r["requiredKg"] == null && r["requirementSource"] == null && r["satisfied"] == null && r["shortageKg"] == null, "multi-slot nulls kept");
            Equal(r["quantityStatus"], "unknown", "multi-slot status");
        }, 2);
        Run("single_slot_shortage_does_not_reject_compatible_selection", () => {
            var d = Base(); ClusterManager.Instance.Worlds[0].worldInventory.Amounts["MatA"] = 25f; return d;
        }, "MatA", r => { Success(r, "explicit", "MatA", "MatA"); Equal(r["quantityStatus"], "insufficient", "shortage status"); Equal(r["shortageKg"], 75d, "shortage amount"); }, 2);
        Run("single_slot_exact_stock", () => {
            var d = Base(); ClusterManager.Instance.Worlds[0].worldInventory.Amounts["MatA"] = 100f; return d;
        }, "MatA", r => { Success(r, "explicit", "MatA", "MatA"); Equal(r["satisfied"], true, "exact stock satisfies"); Equal(r["shortageKg"], 0d, "exact stock shortage"); }, 2);
        Run("empty_mass_stays_unknown", () => { var d = Base(); d.Mass = new float[0]; return d; }, "MatA", r => {
            Success(r, "explicit", "MatA", "MatA"); Check(r["requiredKg"] == null, "empty mass is unknown"); Equal(r["quantityStatus"], "unknown", "empty mass status");
        }, 2);
        Run("existing_world_and_loose_amount_semantics", () => {
            var d = Base(); ClusterManager.Instance.Worlds[0].worldInventory.Amounts["MatA"] = 10f;
            Boundary.Pickup("MatA", 7f, false, false, 2); Boundary.Pickup("MatA", 11f, true, false, 2);
            Boundary.Pickup("MatA", 13f, false, true, 2); Boundary.Pickup("MatA", 17f, false, false, 3); return d;
        }, "MatA", r => { Success(r, "explicit", "MatA", "MatA"); Equal(Selected(r)["availableKg"], 17d, "world/storage filtering and existing extra loose amount kept"); });
        Run("ineligible_category_not_returned", () => {
            var d = Base(); Boundary.AddMaterial(d, "GasOnly", "GasOnly", 500f, "Gas");
            DiscoveredResources.Instance.Existing["Metal"] = new List<Tag> { new Tag("GasOnly") }; return d;
        }, "GasOnly", r => { Invalid(r, "Material 'GasOnly' is not valid for FixtureBuilding"); Sequence(r, "candidateMaterials", "tag", "MatA", "MatB", "MatZ"); });
        Run("discovery_failure_keeps_defaults", () => { var d = Base(); DiscoveredResources.Instance.ThrowOnRead = true; return d; }, "MatA",
            r => Success(r, "explicit", "MatA", "MatA"), 2);
        Run("no_category_default_elements", () => { var d = Base(); d.MaterialCategory = null; return d; }, "MatA",
            r => Success(r, "explicit", "MatA", "MatA"));
        RequestIsolation();
    }

    private static void RequestIsolation()
    {
        Boundary.Reset(); var d = Base();
        Inspect("request_scope_first", d, null, r => Success(r, "auto", "auto", "MatA"));
        ClusterManager.Instance.Worlds[0].worldInventory.Amounts["MatB"] = 300f;
        Boundary.Names["MatB"] = "Renamed";
        Inspect("request_scope_new_inventory", d, null, r => Success(r, "auto", "auto", "MatB"));
        Inspect("request_scope_new_native_name", d, "Renamed", r => Success(r, "explicit", "Renamed", "MatB"));
        ElementLoader.Elements["MatNew"] = new Element { Category = new Tag("Metal") };
        Boundary.Names["MatNew"] = "Discovered";
        ClusterManager.Instance.Worlds[0].worldInventory.Amounts["MatNew"] = 500f;
        DiscoveredResources.Instance.Existing["Metal"] = new List<Tag> { new Tag("MatNew") };
        Inspect("request_scope_new_discovery", d, null, r => Success(r, "auto", "auto", "MatNew"));
    }

    private static BuildingDef Base() { return Boundary.BaseDefinition(); }
    private static BuildingDef EmptyDefinition() { return new BuildingDef { PrefabID = "FixtureBuilding", MaterialCategory = new[] { "Metal" }, Mass = new[] { 100f } }; }
    private static BuildingDef Many()
    {
        var d = EmptyDefinition();
        for (int i = 25; i >= 1; i--) Boundary.AddMaterial(d, "Mat" + i.ToString("D2"), "Name" + i.ToString("D2"), 26 - i);
        return d;
    }
    private static void Run(string name, Func<BuildingDef> setup, string request, Action<Dictionary<string, object>> assertion, int? expectedLists = null)
    {
        Boundary.Reset(); BuildingDef def = setup();
        Inspect(name, def, request, assertion);
        if (expectedLists.HasValue)
            countBudgets.Add(new JObject { ["case"] = name, ["expectedLists"] = expectedLists.Value,
                ["inventoryReads"] = Boundary.InventoryReads, ["expectedInventoryReads"] = expectedLists.Value * 3,
                ["discoveryReads"] = Boundary.DiscoveryReads, ["expectedDiscoveryReads"] = expectedLists.Value,
                ["pickupEnumerations"] = Boundary.PickupEnumerations, ["expectedPickupEnumerations"] = expectedLists.Value * 4 });
    }
    private static void Inspect(string name, BuildingDef def, string request, Action<Dictionary<string, object>> assertion)
    {
        string before = Boundary.SnapshotInputs(def);
        var result = BuildPlanningTools.HostSelect(def, request, 0);
        assertion(result);
        Check(OutputKeys.OrderBy(x => x).SequenceEqual(result.Keys.OrderBy(x => x)), name + ": full output keys unchanged");
        Check(before == Boundary.SnapshotInputs(def), name + ": native fixture inputs unchanged");
        Check(Boundary.ForbiddenCalls == 0, name + ": no UI/facade/init/write boundary");
        JToken wire = JToken.Parse(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
        Check((bool)wire["valid"] == (bool)result["valid"], name + ": runtime JSON settings keep valid flag");
        observations.Add(new JObject { ["case"] = name, ["output"] = wire });
    }
    private static void Success(Dictionary<string, object> r, string mode, string requested, string tag)
    {
        Equal(r["valid"], true, "selection valid"); Equal(r["mode"], mode, "mode"); Equal(r["requested"], requested, "requested");
        Equal(Selected(r)["tag"], tag, "selected tag"); SequenceStrings(r, "elements", tag);
        Sequence(r, "candidateMaterials", "tag"); Check(r["error"] == null, "success error stays null");
    }
    private static void Invalid(Dictionary<string, object> r, string error)
    { Equal(r["valid"], false, "selection invalid"); Equal(r["mode"], "invalid", "invalid mode"); Equal(r["error"], error, "exact legacy error"); SequenceStrings(r, "elements"); }
    private static Dictionary<string, object> Selected(Dictionary<string, object> r)
    { var selected = r["selected"] as Dictionary<string, object>; Check(selected != null, "selected row is present"); return selected; }
    private static void Sequence(Dictionary<string, object> r, string key, string field, params string[] expected)
    {
        var actual = ((List<Dictionary<string, object>>)r[key]).Select(x => (string)x[field]).ToArray();
        Check(actual.SequenceEqual(expected), key + ": expected " + string.Join(",", expected) + "; actual " + string.Join(",", actual));
    }
    private static void SequenceStrings(Dictionary<string, object> r, string key, params string[] expected)
    { Check(((List<string>)r[key]).SequenceEqual(expected), key + " sequence"); }
    private static void Equal(object actual, object expected, string message)
    {
        if (actual == null || expected == null)
            Check(object.Equals(actual, expected), message + ": null is distinct from a known zero");
        else if (actual is double || actual is float || expected is double || expected is float)
            Check(IsNumber(actual) && IsNumber(expected)
                && Convert.ToDouble(actual, CultureInfo.InvariantCulture) == Convert.ToDouble(expected, CultureInfo.InvariantCulture), message);
        else Check(object.Equals(actual, expected), message + ": expected=" + expected + "; actual=" + actual);
    }
    private static bool IsNumber(object value)
    { return value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint
        || value is long || value is ulong || value is float || value is double || value is decimal; }
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidOperationException(message); }
    private static JObject SourceHashes()
    {
        var result = new JObject();
        foreach (var metadata in Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (metadata.Key != "BuildMaterialsSource" && metadata.Key != "MaterialQuantitySource" && metadata.Key != "McpJsonSource") continue;
            byte[] bytes = File.ReadAllBytes(metadata.Value);
            using (SHA256 sha = SHA256.Create()) result[metadata.Key] = new JObject { ["path"] = metadata.Value,
                ["bytes"] = bytes.Length, ["sha256"] = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() };
        }
        return result;
    }
}
