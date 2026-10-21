using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class StorageItemDetailsRegression
{
    private static int checks;
    private static GameObject storageObject;
    private static Storage storage;
    private static TreeFilterable filter;
    private static Klei.AI.Disease definition;

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception("Storage item detail: " + name);
    }
    private static JObject Detail(int route = 0)
    {
        var args = new JObject { ["id"] = 41, ["worldId"] = 0, ["action"] = "detail", ["domain"] = "storage" };
        var result = route == 0 ? StorageTools.GetStorageDetail().Handler(args)
            : route == 1 ? StorageTools.ControlStorage().Handler(args) : StorageTools.ControlStorageSystem().Handler(args);
        Check(!result.IsError, "actual storage detail handler succeeds");
        return JObject.Parse(result.Value);
    }
    private static JObject Item(GameObject item)
    {
        storage.items.Clear();
        storage.items.Add(item);
        return (JObject)Detail()["items"][0];
    }
    private static GameObject MakeItem(int id, PrimaryElement primary)
    {
        var item = new GameObject { Id = id + 1000, name = "Liquid fixture", WorldId = 0 };
        item.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = id, PrefabTag = new Tag("Water") };
        if (primary != null) item.Components[typeof(PrimaryElement)] = primary;
        return item;
    }
    private static PrimaryElement NativeValues()
    {
        return new PrimaryElement { Mass = 1.23456f, Temperature = 293.25f, ElementID = SimHashes.Water, DiseaseIdx = 0, DiseaseCount = 4321 };
    }
    private static void NativeDatabase()
    {
        definition = new Klei.AI.Disease { Id = "FixtureGerm", Name = "Localized fixture germ" };
        Db.FixtureSet(new Db { Diseases = new Database.Diseases() });
        Db.FixtureCurrent.Diseases.resources.Add(definition);
    }
    private static void Null(JObject item, params string[] fields)
    {
        foreach (string field in fields)
            Check(item.Property(field) != null && item[field].Type == JTokenType.Null, field + " stays explicit JSON null under actual serializer settings");
    }
    private static void Reason(JObject item, string field, string reason = null)
    {
        var failures = (JArray)item["unavailableFields"];
        Check(failures != null && failures.OfType<JObject>().Any(row => (string)row["field"] == field && !string.IsNullOrWhiteSpace((string)row["reason"]) && (reason == null || (string)row["reason"] == reason)), field + " has a specific unavailable reason");
    }
    private static void NoSafetyInference(JObject item)
    {
        Check(item["safe"] == null && item["drinkable"] == null && item["sterile"] == null && item["disinfected"] == null, "raw properties do not assert liquid treatment or safety");
    }
    internal static int Run()
    {
        Components.BuildingCompletes.Items.Clear();
        storageObject = new GameObject { Id = 41, name = "Reservoir fixture", WorldId = 0 };
        storage = new Storage { capacityKg = 5000f };
        filter = new TreeFilterable();
        filter.FixtureSet("Water");
        storageObject.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = 41, PrefabTag = new Tag("LiquidReservoir") };
        storageObject.Components[typeof(Storage)] = storage;
        storageObject.Components[typeof(TreeFilterable)] = filter;
        Components.BuildingCompletes.Items.Add(new BuildingComplete { gameObject = storageObject });
        NativeDatabase();
        Db.LazyGetCalls = 0;

        var primary = NativeValues();
        var actual = MakeItem(130, primary);
        int primaryWrites = primary.Writes;
        JObject observed = Item(actual);
        Check(observed["id"] != null && observed["id"].Type == JTokenType.Integer && (int)observed["id"] == 130 && (string)observed["name"] == actual.name && (string)observed["prefabId"] == "Water", "item identity uses native KPrefabID and preserves existing fields");
        Check((double)observed["massKg"] == 1.235 && (string)observed["elementId"] == "Water", "native mass retains three-place projection and native element ID");
        Check((double)observed["temperatureK"] == 293.25 && (double)observed["temperatureC"] == 20.10, "Kelvin is raw while Celsius retains two-place conversion");
        Check((int)observed["diseaseIndex"] == 0 && (int)observed["diseaseCount"] == 4321, "raw germ index and count are reported separately");
        Check((string)observed["diseaseId"] == "FixtureGerm" && (string)observed["diseaseName"] == "Localized fixture germ" && (string)observed["diseaseDefinitionStatus"] == "resolved", "definition comes from existing native database index");
        Check((string)observed["propertiesSource"] == "PrimaryElement" && (bool)observed["primaryElementPresent"] && (string)observed["diseaseDefinitionSource"] == "Db.Diseases.resources", "property and definition provenance are explicit");
        Check(((JArray)observed["unavailableFields"]).Count == 0 && Db.LazyGetCalls == 0, "valid cached definition requires no lazy database initialization");
        NoSafetyInference(observed);
        Check(JToken.DeepEquals(Detail()["items"], Detail(1)["items"]) && JToken.DeepEquals(Detail()["items"], Detail(2)["items"]), "both existing detail routing layers preserve the same item data");

        Check(primary.Writes == primaryWrites, "direct and both routed detail reads do not write primary native properties");

        primary = new PrimaryElement { Mass = 0, Temperature = 0, DiseaseIdx = 0, DiseaseCount = 0, ElementID = SimHashes.Water };
        observed = Item(MakeItem(0, primary));
        Check((int)observed["id"] == 0 && (double)observed["massKg"] == 0 && (double)observed["temperatureK"] == 0 && (double)observed["temperatureC"] == -273.15 && (int)observed["diseaseCount"] == 0, "observed numeric zeros are not treated as unavailable");
        Check((string)observed["diseaseId"] == "FixtureGerm" && (string)observed["diseaseDefinitionStatus"] == "resolved", "count zero does not erase the observed native index definition");
        NoSafetyInference(observed);

        primary = NativeValues(); primary.DiseaseIdx = byte.MaxValue; primary.DiseaseCount = 99;
        observed = Item(MakeItem(131, primary));
        Check((int)observed["diseaseIndex"] == 255 && (int)observed["diseaseCount"] == 99 && (string)observed["diseaseDefinitionStatus"] == "native_none_index", "native none index and even inconsistent positive count stay raw");
        Null(observed, "diseaseId", "diseaseName"); NoSafetyInference(observed);
        primary.DiseaseIdx = 5;
        observed = Item(MakeItem(131, primary));
        Check((int)observed["diseaseIndex"] == 5 && (int)observed["diseaseCount"] == 99 && (string)observed["diseaseDefinitionStatus"] == "unresolved_index", "out-of-range mapping cannot erase raw germ values");
        Null(observed, "diseaseId", "diseaseName"); Reason(observed, "diseaseId"); Reason(observed, "diseaseName");

        foreach (int missing in new[] { 0, 1, 2, 3 })
        {
            NativeDatabase();
            if (missing == 0) Db.FixtureSet(null);
            if (missing == 1) Db.FixtureCurrent.Diseases = null;
            if (missing == 2) Db.FixtureCurrent.Diseases.resources = null;
            if (missing == 3) Db.FixtureCurrent.Diseases.resources[0] = null;
            observed = Item(MakeItem(132, NativeValues()));
            Check((int)observed["diseaseIndex"] == 0 && (int)observed["diseaseCount"] == 4321, "missing database stage preserves raw disease values");
            Null(observed, "diseaseId", "diseaseName"); Reason(observed, "diseaseId"); Reason(observed, "diseaseName");
            Check((string)observed["diseaseDefinitionStatus"] != "resolved" && Db.LazyGetCalls == 0, "missing cache remains unresolved without Db.Get initialization");
        }
        NativeDatabase(); definition.ThrowName = true;
        observed = Item(MakeItem(133, NativeValues()));
        Check((string)observed["diseaseId"] == "FixtureGerm", "failed localized germ name does not erase independent definition ID");
        Null(observed, "diseaseName"); Reason(observed, "diseaseName");
        NativeDatabase(); definition.ThrowId = true;
        observed = Item(MakeItem(133, NativeValues()));
        Check((string)observed["diseaseName"] == "Localized fixture germ", "failed definition ID does not erase independent localized name");
        Null(observed, "diseaseId"); Reason(observed, "diseaseId");
        NativeDatabase();

        observed = Item(MakeItem(134, null));
        Check(observed["primaryElementPresent"].Type == JTokenType.Boolean && !(bool)observed["primaryElementPresent"], "normal absent component is false rather than unknown");
        Null(observed, "massKg", "elementId", "temperatureK", "temperatureC", "diseaseIndex", "diseaseCount", "diseaseId", "diseaseName");
        foreach (string field in new[] { "massKg", "elementId", "temperatureK", "temperatureC", "diseaseIndex", "diseaseCount" }) Reason(observed, field, "primary_element_missing");
        var componentFailure = MakeItem(135, NativeValues()); componentFailure.ThrowGetComponent.Add(typeof(PrimaryElement));
        observed = Item(componentFailure);
        Null(observed, "primaryElementPresent", "massKg", "temperatureK", "diseaseCount");
        Reason(observed, "massKg", "primary_element_read_failed");
        Check((int)observed["id"] == 135, "failed PrimaryElement lookup does not erase independently observed identity");

        storage.items.Clear(); storage.items.Add(componentFailure); storage.items.Add(MakeItem(136, NativeValues()));
        var independentRows = (JArray)Detail()["items"];
        Check(independentRows.Count == 2 && independentRows[0]["massKg"].Type == JTokenType.Null && (int)independentRows[1]["diseaseCount"] == 4321, "component read failure in one item does not discard later native items");

        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            primary = NativeValues(); primary.Mass = invalid; primary.Temperature = invalid;
            observed = Item(MakeItem(136, primary));
            Null(observed, "massKg", "temperatureK", "temperatureC");
            Reason(observed, "massKg", "nonfinite"); Reason(observed, "temperatureK", "nonfinite"); Reason(observed, "temperatureC", "temperature_unavailable");
            Check((int)observed["diseaseCount"] == 4321 && (string)observed["elementId"] == "Water", "invalid floats do not poison other native properties");
        }
        foreach (string getter in new[] { "Mass", "Temperature", "DiseaseIdx", "DiseaseCount", "ElementID" })
        {
            primary = NativeValues(); primary.ThrowOn.Add(getter);
            observed = Item(MakeItem(137, primary));
            string field = getter == "Mass" ? "massKg" : getter == "Temperature" ? "temperatureK" : getter == "DiseaseIdx" ? "diseaseIndex" : getter == "DiseaseCount" ? "diseaseCount" : "elementId";
            Null(observed, field); Reason(observed, field, "read_failed");
            if (getter != "Mass") Check((double)observed["massKg"] == 1.235, "independent mass survives another getter exception");
            if (getter != "Temperature") Check((double)observed["temperatureK"] == 293.25, "independent temperature survives another getter exception");
            if (getter != "DiseaseCount") Check((int)observed["diseaseCount"] == 4321, "independent raw count survives another getter exception");
            if (getter == "Temperature") Null(observed, "temperatureC");
            if (getter == "DiseaseIdx") Check((string)observed["diseaseDefinitionStatus"] == "raw_unavailable", "missing raw index is distinct from native none and unknown database definition");
            else Check((string)observed["diseaseId"] == "FixtureGerm", "independent index mapping survives unrelated getter exception");
        }

        var noId = MakeItem(138, NativeValues()); noId.Components.Remove(typeof(KPrefabID));
        observed = Item(noId); Null(observed, "id"); Reason(observed, "id", "prefab_id_missing");
        Check((string)observed["prefabId"] == noId.name && (int)observed["diseaseCount"] == 4321, "missing native identity preserves name fallback and observed properties");
        var failedId = MakeItem(139, NativeValues()); ((KPrefabID)failedId.Components[typeof(KPrefabID)]).ThrowInstanceId = true;
        observed = Item(failedId); Null(observed, "id"); Reason(observed, "id");
        Check((string)observed["prefabId"] == "Water", "ID getter failure preserves independently read prefab tag");

        actual = MakeItem(140, NativeValues());
        storage.items.Clear(); storage.items.Add(null); storage.items.Add(actual); storage.items.Add(actual); storage.items.Add(MakeItem(141, NativeValues()));
        var beforeItems = storage.items.ToArray();
        var rows = (JArray)Detail()["items"];
        Check(rows.Count == 3 && (int)rows[0]["id"] == 140 && (int)rows[1]["id"] == 140 && (int)rows[2]["id"] == 141, "null entries are skipped while native duplicate references and item order are preserved");
        Check(storage.items.SequenceEqual(beforeItems) && Components.BuildingCompletes.Items.Count == 1 && filter.Updates == 0 && Db.LazyGetCalls == 0, "detail never writes native inventory, filter, registry or lazy database");
        var solid = NativeValues(); solid.ElementID = SimHashes.Dirt;
        observed = Item(MakeItem(142, solid));
        Check((string)observed["elementId"] == "Dirt" && (int)observed["diseaseCount"] == 4321, "same item detail schema covers stored solids without inferring liquid-specific safety");
        storage.items.Clear();
        Check(((JArray)Detail()["items"]).Count == 0, "empty detail emits an empty item array");
        actual.ThrowGetComponent.Add(typeof(PrimaryElement)); storage.items.Add(actual);
        var list = StorageTools.GetStorageList().Handler(new JObject { ["worldId"] = 0 });
        Check(!list.IsError && JObject.Parse(list.Value)["storages"][0]["items"] == null, "compact list does not add item projection or read item components");
        var write = StorageTools.SetStorageFilter().Handler(new JObject { ["id"] = 41, ["worldId"] = 0, ["tags"] = new JArray("Water") });
        Check(!write.IsError && JObject.Parse(write.Value)["items"] == null && filter.Updates == 1, "existing filter write stays compact and delegates only its requested update");
        Check(storage.items.Count == 1 && ReferenceEquals(storage.items[0], actual) && Db.LazyGetCalls == 0, "readback additions do not simulate inventory movement or database initialization");
        return checks;
    }
}
