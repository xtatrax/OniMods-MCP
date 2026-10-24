using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(name);
    }

    private static void Reset()
    {
        PlantSubSpeciesCatalog.Instance = new PlantSubSpeciesCatalog();
        DiscoveredResources.Instance = new DiscoveredResources();
        Assets.Existing.Clear();
        Assets.GetPrefabCalls = Assets.TryGetPrefabCalls = Assets.MissingPrefabWarnings = 0;
        GameTagExtensions.Names.Clear();
    }

    private static void Add(string species, string seed, string name, int subspecies = 2, bool discovered = true)
    {
        PlantSubSpeciesCatalog.Instance.Existing[species] = Enumerable.Range(0, subspecies)
            .Select(_ => new PlantSubSpeciesCatalog.SubSpeciesInfo()).ToList();
        var prefab = new UnityEngine.GameObject();
        prefab.Add(new SeedProducer { seedInfo = new SeedProducer.SeedInfo { seedId = seed } });
        Assets.Existing[species] = prefab;
        GameTagExtensions.Names[seed] = name;
        if (discovered) DiscoveredResources.Instance.Existing.Add(seed);
    }

    private static JArray Wire(List<Dictionary<string, object>> rows)
    {
        return JArray.Parse(JsonConvert.SerializeObject(rows, McpJsonUtil.Settings));
    }

    private static void NullPermissionIsPassive()
    {
        Reset();
        Add("PlantA", "SeedA", "Alpha");
        var station = new GeneticAnalysisStation.StatesInstance(null);
        var rows = StoryFacilityTools.ReadGeneticSeedOptionsForTest(station);
        Check(station.GetterCalls == 0, "mutating_getter_was_invoked");
        Check(station.CacheForTest == null && station.GetterInitializations == 0, "null_cache_was_initialized");
        Check(station.SetterCalls == 0 && station.RefreshCalls == 0, "read_invoked_native_write");
        Check(rows.Count == 1 && (string)rows[0]["seedId"] == "SeedA", "eligible_unknown_option_was_removed");
        var row = (JObject)Wire(rows)[0];
        Check(row.Property("allowed") != null && row["allowed"].Type == JTokenType.Null, "wire_allowed_unknown_was_not_explicit_null");
        Check(row.Property("forbidden") != null && row["forbidden"].Type == JTokenType.Null, "wire_forbidden_unknown_was_not_explicit_null");
        Check((string)row["permissionState"] == "unavailable" && (string)row["permissionReason"] == "forbidden_cache_uninitialized", "null_cache_reason_changed");
        Check(rows.Exists(option => new Tag((string)option["seedId"]) == new Tag("SeedA")), "unknown_permission_broke_existing_id_eligibility");
        var repeat = Wire(StoryFacilityTools.ReadGeneticSeedOptionsForTest(station));
        Check(JToken.DeepEquals(Wire(rows), repeat) && station.CacheForTest == null && station.GetterCalls == 0, "repeat_read_mutated_unknown_cache");
    }

    private static void KnownEmptyAndForbidden()
    {
        Reset();
        Add("PlantB", "SeedB", "Beta");
        Add("PlantA", "SeedA", "Alpha");
        var cache = new HashSet<Tag>();
        var station = new GeneticAnalysisStation.StatesInstance(cache);
        var empty = Wire(StoryFacilityTools.ReadGeneticSeedOptionsForTest(station));
        Check(empty.Count == 2 && (string)empty[0]["seedId"] == "SeedA", "native_name_sort_changed");
        Check((bool)empty[0]["allowed"] && !(bool)empty[0]["forbidden"], "known_empty_was_not_allowed");
        Check((string)empty[0]["permissionState"] == "observed" && empty[0]["permissionReason"].Type == JTokenType.Null, "known_empty_reason_not_explicit_null");
        Check(((JObject)empty[0]).Property("permissionReason") != null, "known_reason_key_was_omitted");
        Check(ReferenceEquals(cache, station.CacheForTest) && cache.Count == 0, "known_empty_cache_changed");
        cache.Add("SeedB");
        var before = cache.ToArray();
        var known = Wire(StoryFacilityTools.ReadGeneticSeedOptionsForTest(station));
        Check((bool)known[0]["allowed"] && !(bool)known[0]["forbidden"], "unforbidden_seed_changed");
        Check(!(bool)known[1]["allowed"] && (bool)known[1]["forbidden"], "forbidden_seed_changed");
        Check(ReferenceEquals(cache, station.CacheForTest) && cache.SetEquals(before), "forbidden_membership_changed");
        Check(station.GetterCalls == 0 && station.SetterCalls == 0 && station.RefreshCalls == 0, "known_read_invoked_native_mutator");
        Check(Assets.GetPrefabCalls == 0 && Assets.MissingPrefabWarnings == 0, "passive_read_used_warning_lookup");
        Check((string)known[0]["speciesId"] == "PlantA" && (string)known[0]["name"] == "Alpha" && (int)known[0]["subSpeciesCount"] == 2, "legacy_row_fields_changed");
    }

    private static void MissingStationRetainsEligibleRows()
    {
        Reset();
        Add("PlantA", "SeedA", "Alpha");
        var rows = Wire(StoryFacilityTools.ReadGeneticSeedOptionsForTest(null));
        Check(rows.Count == 1 && rows[0]["allowed"].Type == JTokenType.Null && rows[0]["forbidden"].Type == JTokenType.Null, "missing_station_defaulted_or_removed_option");
        Check((string)rows[0]["permissionReason"] == "station_unavailable", "missing_station_reason_changed");
    }

    private static void DiscoveryAndPrefabBoundaries()
    {
        Reset();
        Add("Valid", "SeedValid", "Valid");
        Add("OnlyOne", "SeedOne", "One", 1);
        PlantSubSpeciesCatalog.Instance.Existing["MissingSubspecies"] = null;
        Add("Undiscovered", "SeedHidden", "Hidden", 2, false);
        Add("MissingPrefab", "SeedMissing", "Missing");
        Assets.Existing.Remove("MissingPrefab");
        Add("NoProducer", "SeedNoProducer", "NoProducer");
        Assets.Existing["NoProducer"] = new UnityEngine.GameObject();
        Add("InvalidSeed", "", "Invalid");
        Add("DeadPrefab", "SeedDead", "Dead");
        var deadPrefab = Assets.Existing["DeadPrefab"];
        deadPrefab.Destroyed = true;
        Add("DeadProducer", "SeedDeadProducer", "DeadProducer");
        Assets.Existing["DeadProducer"].GetComponent<SeedProducer>().Destroyed = true;
        Add("CategoryDiscovered", "SeedCategory", "Category", 2, false);
        DiscoveredResources.Instance.ExistingCategories.Add("SeedCategory");
        var station = new GeneticAnalysisStation.StatesInstance(new HashSet<Tag>());
        var speciesBefore = PlantSubSpeciesCatalog.Instance.Existing.Keys.ToArray();
        var listReferences = PlantSubSpeciesCatalog.Instance.Existing.ToDictionary(p => p.Key, p => p.Value);
        var discoveredBefore = DiscoveredResources.Instance.Existing.ToArray();
        var prefabsBefore = Assets.Existing.ToDictionary(p => p.Key, p => p.Value);
        var rows = Wire(StoryFacilityTools.ReadGeneticSeedOptionsForTest(station));
        Check(rows.Count == 2 && rows.All(row => (string)row["seedId"] == "SeedCategory" || (string)row["seedId"] == "SeedValid"), "discovery_or_native_species_eligibility_changed");
        Check(deadPrefab.ComponentReads == 0, "destroyed_prefab_component_was_read");
        Check(Assets.GetPrefabCalls == 0 && Assets.MissingPrefabWarnings == 0, "missing_prefab_emitted_warning");
        Check(station.GetterCalls == 0 && station.SetterCalls == 0 && station.RefreshCalls == 0, "filtered_read_mutated_station");
        Check(PlantSubSpeciesCatalog.Instance.Existing.Keys.SequenceEqual(speciesBefore) && listReferences.All(p => ReferenceEquals(p.Value, PlantSubSpeciesCatalog.Instance.Existing[p.Key])), "catalog_was_changed");
        Check(DiscoveredResources.Instance.Existing.SetEquals(discoveredBefore) && Assets.Existing.Count == prefabsBefore.Count && prefabsBefore.All(p => ReferenceEquals(p.Value, Assets.Existing[p.Key])), "discovery_or_prefab_cache_changed");
    }

    private sealed class PoisonComparer : IEqualityComparer<Tag>
    {
        public bool Armed;
        public int Calls;
        public bool Equals(Tag left, Tag right)
        {
            if (Armed) { Calls++; throw new InvalidOperationException("custom_comparer_must_not_run"); }
            return left == right;
        }
        public int GetHashCode(Tag value)
        {
            if (Armed) { Calls++; throw new InvalidOperationException("custom_comparer_must_not_run"); }
            return value.GetHashCode();
        }
    }

    private static void CustomComparerIsNotInvoked()
    {
        Reset();
        Add("PlantA", "SeedA", "Alpha");
        var comparer = new PoisonComparer();
        var cache = new HashSet<Tag>(comparer) { "SeedA" };
        comparer.Armed = true;
        var station = new GeneticAnalysisStation.StatesInstance(cache);
        var rows = Wire(StoryFacilityTools.ReadGeneticSeedOptionsForTest(station));
        Check(comparer.Calls == 0 && station.GetterCalls == 0, "custom_cache_comparer_was_invoked");
        Check(ReferenceEquals(cache, station.CacheForTest) && cache.Count == 1, "custom_cache_changed");
        Check(rows.Count == 1 && rows[0]["allowed"].Type == JTokenType.Null && rows[0]["forbidden"].Type == JTokenType.Null, "custom_comparer_fabricated_permission");
        Check((string)rows[0]["permissionReason"] == "forbidden_cache_comparer_unsupported", "custom_comparer_reason_changed");
    }

    private static void ExistingMissingCatalogBehavior()
    {
        Reset();
        Add("PlantA", "SeedA", "Alpha");
        var station = new GeneticAnalysisStation.StatesInstance(null);
        PlantSubSpeciesCatalog.Instance = null;
        Check(StoryFacilityTools.ReadGeneticSeedOptionsForTest(station).Count == 0, "missing_catalog_legacy_empty_shape_changed");
        Reset();
        DiscoveredResources.Instance = null;
        Check(StoryFacilityTools.ReadGeneticSeedOptionsForTest(station).Count == 0, "missing_discovery_legacy_empty_shape_changed");
        Check(station.GetterCalls == 0 && station.CacheForTest == null && Assets.GetPrefabCalls == 0 && Assets.TryGetPrefabCalls == 0, "missing_catalog_path_did_work");
    }

    private static int Main()
    {
        NullPermissionIsPassive();
        KnownEmptyAndForbidden();
        MissingStationRetainsEligibleRows();
        DiscoveryAndPrefabBoundaries();
        CustomComparerIsNotInvoked();
        ExistingMissingCatalogBehavior();
        Console.WriteLine("PASS genetic passive seed options: " + checks + " checks");
        return 0;
    }
}
