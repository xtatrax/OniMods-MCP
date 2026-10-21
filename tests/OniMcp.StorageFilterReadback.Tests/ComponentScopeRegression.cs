using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using UnityEngine;

internal static class ComponentScopeRegression
{
    private static int checks;
    private sealed class Fixture
    {
        internal GameObject Target;
        internal Storage Primary, Secondary, Child, Neighbor;
        internal TreeFilterable Filter;
    }
    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception("Storage component scope: " + name);
    }
    private static GameObject Item(int id, SimHashes element = SimHashes.Water)
    {
        var item = new GameObject { Id = id + 1000, name = "Stored fixture", WorldId = 0 };
        item.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = id, PrefabTag = new Tag(element.ToString()) };
        item.Components[typeof(PrimaryElement)] = new PrimaryElement { Mass = 2, Temperature = 293.25f, ElementID = element, DiseaseIdx = 255, DiseaseCount = 0 };
        return item;
    }
    private static Storage NativeStorage(float capacity, float mass, bool shown, params string[] tags)
    {
        var storage = new Storage { capacityKg = capacity, storageID = new Tag("Native.Shared"), showInUI = shown, storageFilters = tags.Select(tag => new Tag(tag)).ToList() };
        storage.FixtureMass(mass);
        return storage;
    }
    private static Fixture Make()
    {
        Components.BuildingCompletes.Items.Clear();
        var f = new Fixture
        {
            Target = new GameObject { Id = 51, name = "Scoped fixture", WorldId = 0 },
            Primary = NativeStorage(500, 11.5f, true, "RawB", "RawA", "RawB"),
            Secondary = NativeStorage(200, 3, false, "SecondaryOnly"),
            Child = NativeStorage(999, 99, true, "ChildOnly"),
            Neighbor = NativeStorage(888, 88, true, "NeighborOnly"), Filter = new TreeFilterable()
        };
        f.Filter.FixtureSet("HeaderFilter");
        f.Primary.items.Add(Item(511)); f.Secondary.items.Add(Item(512, SimHashes.Dirt));
        f.Target.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = 51, PrefabTag = new Tag("ScopedStorageFixture") };
        f.Target.Components[typeof(Storage)] = f.Primary;
        f.Target.Components[typeof(TreeFilterable)] = f.Filter;
        f.Target.ExtraComponents.Add(f.Secondary); f.Target.ExtraComponents.Add(new OtherStorageFixture());
        f.Target.ChildComponents.Add(f.Child);
        Components.BuildingCompletes.Items.Add(new BuildingComplete { gameObject = f.Target });
        var neighbor = new GameObject { Id = 52, name = "Neighbor fixture", WorldId = 0 };
        neighbor.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = 52, PrefabTag = new Tag("NeighborStorageFixture") };
        neighbor.Components[typeof(Storage)] = f.Neighbor;
        Components.BuildingCompletes.Items.Add(new BuildingComplete { gameObject = neighbor });
        foreach (var storage in new[] { f.Primary, f.Secondary, f.Child, f.Neighbor }) { storage.Reads.Clear(); storage.MassReads = 0; }
        return f;
    }
    private static CallToolResult Call(int route, string action, JToken scope, bool specified = true, JObject extra = null)
    {
        var args = new JObject { ["id"] = 51, ["worldId"] = 0, ["action"] = action, ["domain"] = "storage", ["tags"] = new JArray("Requested") };
        if (specified) args["storageScope"] = scope == null ? JValue.CreateNull() : scope.DeepClone();
        if (extra != null) foreach (var property in extra.Properties()) args[property.Name] = property.Value.DeepClone();
        if (route == 0) return (action == "detail" ? StorageTools.GetStorageDetail() : action == "list" ? StorageTools.GetStorageList() : StorageTools.SetStorageFilter()).Handler(args);
        return (route == 1 ? StorageTools.ControlStorage() : route == 2 ? StorageTools.ControlStorageSystem() : BuildingControlTools.ControlBuilding()).Handler(args);
    }
    private static JObject Detail(string scope = "all_components", int route = 0, bool specified = true)
    {
        var result = Call(route, "detail", new JValue(scope), specified);
        Check(!result.IsError, "actual detail handler and route succeed");
        return JObject.Parse(result.Value);
    }
    private static JObject Row(JObject response, int index = 1) { return (JObject)response["storageComponents"][index]; }
    private static void Null(JObject row, string field)
    {
        Check(row.Property(field) != null && row[field].Type == JTokenType.Null, field + " is explicit JSON null");
    }
    private static void Reason(JObject row, string field, string reason)
    {
        Check(((JArray)row["unavailableFields"]).OfType<JObject>().Any(entry => (string)entry["field"] == field && (string)entry["reason"] == reason), field + " preserves " + reason);
    }
    private static void SmallScope(JObject response)
    {
        Check((string)response["storageScope"] == "primary" && (string)response["summaryScope"] == "primary" && (string)response["componentScope"] == "same_game_object", "primary scope and summary provenance are explicit");
        Check(!(bool)response["includesChildStorages"] && !(bool)response["includesOtherIStorageImplementations"] && (string)response["primarySelectionSource"] == "GameObject.GetComponent<Storage>", "component boundary excludes descendants and other interfaces");
        Check(response["storageComponents"] == null && response["storageComponentCount"] == null && response["componentEnumerationSource"] == null, "primary omits component enumeration payload");
    }
    private static void DefaultAndAll()
    {
        var f = Make();
        foreach (string field in new[] { "capacityKg", "MassStored", "items", "storageID", "showInUI", "storageFilters" }) f.Secondary.ThrowOn.Add(field);
        JObject omitted = Detail("primary", 0, false), explicitPrimary = Detail("primary");
        Check(JToken.DeepEquals(omitted, explicitPrimary), "missing scope and explicit primary produce the same detail");
        SmallScope(omitted);
        Check((int)omitted["id"] == 51 && (int)omitted["worldId"] == 0 && (double)omitted["capacityKg"] == 500 && (double)omitted["massStoredKg"] == 11.5 && (int)omitted["items"][0]["id"] == 511, "primary identity and legacy item/capacity/mass projection remain compatible");
        Check(f.Target.ComponentEnumerationReads == 0 && f.Secondary.Reads.Count == 0 && f.Child.Reads.Count == 0 && f.Neighbor.Reads.Count == 0, "default never enumerates or reads nonprimary storage data");
        f.Secondary.ThrowOn.Clear();
        var beforeItems = f.Primary.items.ToArray(); int writes = f.Primary.Writes + f.Secondary.Writes;
        JObject all = Detail();
        Check(all.Property("items") == null && (int)all["storageComponentCount"] == 2 && ((JArray)all["storageComponents"]).Count == 2, "all components emits item payload once in component rows");
        Check((string)all["componentEnumerationStatus"] == "observed" && (string)all["componentEnumerationSource"] == "GameObject.GetComponents<Storage>", "enumeration is a same-object native read");
        Check((double)all["capacityKg"] == 500 && (double)all["massStoredKg"] == 11.5 && (string)all["summaryScope"] == "primary", "top summary remains primary rather than an aggregate");
        for (int index = 0; index < 2; index++)
        {
            JObject row = Row(all, index);
            Check((int)row["componentIndex"] == index && (bool)row["isPrimary"] == (index == 0), "component indexes preserve enumeration order and exact primary identity");
            Check((string)row["storageIdTag"] == "Native.Shared" && (string)row["storageIdStatus"] == "resolved" && (string)row["storageIdKind"] == "native_storage_tag_not_instance_id", "duplicate native tags are retained without fabricated instance identities");
            Check((int)row["items"][0]["id"] == 511 + index && !(bool)row["partial"], "each component retains its own healthy inventory");
        }
        Check((bool)Row(all, 0)["showInUI"] && !(bool)Row(all)["showInUI"] && (double)Row(all)["capacityKg"] == 200 && (double)Row(all)["massStoredKg"] == 3, "hidden secondary component has independent capacity and mass");
        Check((string)Row(all, 0)["storageFiltersStatus"] == "read" && (string)Row(all)["storageFiltersStatus"] == "read" && Row(all, 0)["storageFilters"].Values<string>().SequenceEqual(new[] { "RawB", "RawA", "RawB" }) && Row(all)["storageFilters"].Values<string>().SequenceEqual(new[] { "SecondaryOnly" }) && all["acceptedTags"].Values<string>().SequenceEqual(new[] { "HeaderFilter" }), "raw per-component filter order and duplicates differ from shared TreeFilterable tags");
        Check(f.Target.ComponentEnumerationReads == 1 && f.Target.ChildEnumerationReads == 0 && f.Child.Reads.Count == 0 && f.Neighbor.Reads.Count == 0, "same-object concrete Storage enumeration excludes children, neighbors and other IStorage");
        Check(f.Primary.Writes + f.Secondary.Writes == writes && f.Primary.items.SequenceEqual(beforeItems) && f.Filter.Updates == 0 && !(bool)all["partial"], "detailed reads preserve native fields, item references and filters");
        for (int route = 1; route <= 3; route++) Check(JToken.DeepEquals(all, Detail("all_components", route)), "full production aggregate and canonical routes retain the same result");
        f.Target.ComponentResults = type => new object[] { f.Secondary, f.Primary };
        JObject reordered = Detail();
        Check(!(bool)Row(reordered, 0)["isPrimary"] && (bool)Row(reordered)["isPrimary"] && (int)Row(reordered, 0)["items"][0]["id"] == 512 && (int)Row(reordered)["items"][0]["id"] == 511, "primary identity follows exact component reference rather than enumeration index");
        Check((double)reordered["capacityKg"] == 500 && (double)reordered["massStoredKg"] == 11.5, "reordered enumeration does not substitute the first row for the primary summary");
        f.Target.ComponentResults = null;
        f.Secondary.capacityKg = 0; f.Secondary.FixtureMass(0); f.Secondary.items.Clear(); f.Secondary.storageFilters.Clear();
        JObject zero = Row(Detail());
        Check((double)zero["capacityKg"] == 0 && (double)zero["massStoredKg"] == 0 && ((JArray)zero["items"]).Count == 0 && ((JArray)zero["storageFilters"]).Count == 0 && !(bool)zero["showInUI"] && !(bool)zero["partial"], "observed zeros, empty lists and false stay known values");
    }
    private static void MissingAndIndependent()
    {
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var f = Make(); f.Secondary.capacityKg = invalid; f.Secondary.FixtureMass(invalid);
            JObject all = Detail(), row = Row(all);
            Null(row, "capacityKg"); Null(row, "massStoredKg"); Reason(row, "capacityKg", "nonfinite"); Reason(row, "massStoredKg", "nonfinite");
            Check((double)all["capacityKg"] == 500 && (int)row["items"][0]["id"] == 512 && (bool)all["partial"], "invalid secondary floats preserve independent data and primary summary");
        }
        foreach (string field in new[] { "capacityKg", "MassStored", "storageID", "showInUI", "storageFilters", "items" })
        {
            var f = Make(); f.Secondary.ThrowOn.Add(field);
            JObject all = Detail(), row = Row(all);
            string output = field == "MassStored" ? "massStoredKg" : field == "storageID" ? "storageIdTag" : field;
            Null(row, output); Reason(row, output, "read_failed");
            Check((bool)row["partial"] && (bool)all["partial"] && (double)all["capacityKg"] == 500 && (double)all["massStoredKg"] == 11.5, "single getter failure remains partial without poisoning primary summary");
            if (field != "items") Check((int)row["items"][0]["id"] == 512, "independent inventory survives another component getter failure");
            if (field != "capacityKg") Check((double)row["capacityKg"] == 200, "independent capacity survives another component getter failure");
        }
        var missing = Make(); missing.Secondary.items = null; missing.Secondary.storageFilters = null; missing.Secondary.storageID = new Tag(null);
        JObject partial = Row(Detail());
        Null(partial, "items"); Reason(partial, "items", "items_unavailable");
        Null(partial, "storageFilters"); Check((string)partial["storageFiltersStatus"] == "native_null", "successful native null filter value is distinct from an unavailable read");
        Null(partial, "storageIdTag"); Reason(partial, "storageIdTag", "tag_unavailable");
        Check((string)partial["storageIdStatus"] == "unresolved", "valid tag with unknown name is unresolved rather than native unassigned");
        var nativeNullFilter = Make(); nativeNullFilter.Secondary.storageFilters = null;
        JObject rawNullFilter = Row(Detail());
        Null(rawNullFilter, "storageFilters");
        Check((string)rawNullFilter["storageFiltersStatus"] == "native_null" && !(bool)rawNullFilter["partial"] && ((JArray)rawNullFilter["unavailableFields"]).Count == 0, "normal native null filters preserve raw null without declaring unavailable or partial");
        Check(rawNullFilter["acceptsAll"] == null && rawNullFilter["unrestricted"] == null && rawNullFilter["filteringDisabled"] == null, "native null filters do not infer all-resource acceptance or disabled filtering");
        nativeNullFilter.Secondary.ThrowOn.Add("storageFilters");
        JObject failedFilter = Row(Detail());
        Null(failedFilter, "storageFilters"); Reason(failedFilter, "storageFilters", "read_failed");
        Check((string)failedFilter["storageFiltersStatus"] == "read_failed" && (bool)failedFilter["partial"], "failed filter read remains unavailable rather than normal native null");
        var nativeUnassigned = Make(); nativeUnassigned.Secondary.storageID = Tag.Invalid;
        Tag.InvalidNameReads = 0; Tag.ThrowInvalidName = true;
        JObject unassigned;
        try { unassigned = Row(Detail()); } finally { Tag.ThrowInvalidName = false; }
        Null(unassigned, "storageIdTag");
        Check((string)unassigned["storageIdStatus"] == "native_unassigned" && !(bool)unassigned["partial"] && ((JArray)unassigned["unavailableFields"]).Count == 0 && Tag.InvalidNameReads == 0, "native Invalid is known unassigned with no name lookup, fabricated ID or partial failure");
        nativeUnassigned.Secondary.ThrowOn.Add("storageID");
        JObject failedId = Row(Detail());
        Null(failedId, "storageIdTag"); Reason(failedId, "storageIdTag", "read_failed");
        Check((string)failedId["storageIdStatus"] == "read_failed" && (bool)failedId["partial"], "storage tag read failure stays distinct from native unassigned");
        missing = Make(); missing.Secondary.items = null;
        missing.Secondary.storageFilters = new List<Tag> { default(Tag), new Tag("Raw") };
        partial = Row(Detail());
        Check(partial["storageFilters"][0].Type == JTokenType.Null && (string)partial["storageFilters"][1] == "Raw", "unknown raw tag positions are preserved");
        Reason(partial, "storageFilters[0]", "tag_unavailable");
        foreach (bool all in new[] { false, true })
        {
            var f = Make(); f.Primary.capacityKg = float.NaN; f.Primary.ThrowOn.Add("MassStored"); f.Primary.items = null;
            JObject result = Detail(all ? "all_components" : "primary");
            Null(result, "capacityKg"); Null(result, "massStoredKg"); Reason(result, "capacityKg", "nonfinite"); Reason(result, "massStoredKg", "read_failed");
            JObject primary = all ? Row(result, 0) : result;
            Null(primary, "items"); Reason(primary, "items", "items_unavailable");
            Check((bool)result["partial"], "unknown primary values never become normal zeros or empty inventory");
        }
        var ordered = Make(); var item = Item(513); var broken = Item(514); broken.ThrowProperName = true;
        ordered.Secondary.items = new List<GameObject> { null, item, item, broken, Item(515) };
        JObject raw = Row(Detail()); var rows = (JArray)raw["items"];
        Check(rows.Count == 5 && rows[0].Type == JTokenType.Null && (int)rows[1]["id"] == 513 && (int)rows[2]["id"] == 513 && rows[3].Type == JTokenType.Null && (int)rows[4]["id"] == 515, "all component rows preserve raw null indexes, duplicate refs and later successful items");
        Reason(raw, "items[0]", "item_missing"); Reason(raw, "items[3]", "read_failed");
        Check(ordered.Secondary.items.Count == 5 && ReferenceEquals(ordered.Secondary.items[1], ordered.Secondary.items[2]), "partial item projection does not repair or deduplicate native contents");
    }
    private static void EnumerationFailures()
    {
        foreach (string failure in new[] { "throw", "null", "null_component", "duplicate", "missing_primary", "empty" })
        {
            var f = Make(); string reason;
            if (failure == "throw") { f.Target.ThrowComponentEnumeration = true; reason = "component_enumeration_failed"; }
            else if (failure == "null") { f.Target.ReturnNullComponentEnumeration = true; reason = "component_enumeration_unavailable"; }
            else
            {
                object[] entries = failure == "null_component" ? new object[] { f.Primary, null } : failure == "duplicate" ? new object[] { f.Primary, f.Secondary, f.Secondary } : failure == "empty" ? new object[0] : new object[] { f.Secondary };
                f.Target.ComponentResults = type => entries;
                reason = failure == "null_component" ? "component_missing" : failure == "duplicate" ? "duplicate_component_reference" : "primary_component_unresolved";
            }
            JObject result = Detail();
            Null(result, "storageComponentCount"); Null(result, "storageComponents");
            Reason(result, "storageComponentCount", reason); Reason(result, "storageComponents", reason);
            Check((string)result["componentEnumerationStatus"] == "unavailable" && (bool)result["partial"] && result.Property("items") == null, "failed enumeration is unknown rather than zero normal components");
            Check((double)result["capacityKg"] == 500 && (double)result["massStoredKg"] == 11.5 && f.Secondary.Reads.Count == 0, "failed enumeration retains independent primary summary without reading partial rows");
        }
    }
    private static void RoutesAndWriteBoundary()
    {
        var f = Make();
        foreach (JToken invalid in new JToken[] { new JValue(""), new JValue("everything"), new JValue("Primary"), new JValue(false), new JValue(0), new JArray(), new JObject(), JValue.CreateNull() })
        for (int route = 0; route <= 3; route++)
        {
            var rejected = Call(route, "detail", invalid);
            Check(rejected.IsError && rejected.Value == "storage_components.invalid_scope", "unknown or wrongly typed detail scope is rejected at every entry");
        }
        Check(f.Target.ComponentEnumerationReads == 0 && f.Filter.Updates == 0, "invalid detail scopes cause neither component reads nor writes");
        foreach (string action in new[] { "list", "set_filter" })
        foreach (JToken scope in new JToken[] { new JValue("primary"), new JValue("all_components"), JValue.CreateNull(), new JValue(false) })
        for (int route = 0; route <= 3; route++)
        {
            var rejected = Call(route, action, scope);
            Check(rejected.IsError && rejected.Value == "storage_components.detail_only", "explicit scope on list/write refuses hidden, legacy, aggregate and canonical entries");
        }
        Check(f.Filter.Updates == 0 && f.Target.ComponentEnumerationReads == 0 && f.Secondary.Reads.Count == 0, "scope-bearing list/write never mutates or expands to secondary storage");
        foreach (string domain in new[] { "filter", "tile_selection", "receptacle", "config", "geometry" })
        {
            var rejected = Call(3, "set", new JValue("all_components"), true, new JObject { ["domain"] = domain });
            Check(rejected.IsError && rejected.Value == "storage_components.detail_only", "canonical non-storage domains refuse scope before unrelated routing");
        }
        foreach (string domain in new[] { "filter", "receptacle", "tile_selection" })
        {
            var rejected = Call(2, "list", new JValue("primary"), true, new JObject { ["domain"] = domain });
            Check(rejected.IsError && rejected.Value == "storage_components.detail_only", "aggregate non-storage domains refuse scope before dispatch");
        }
        foreach (var extra in new[] { new JObject { ["kind"] = "filter" }, new JObject { ["storageDomain"] = "filter" } })
        {
            var rejected = Call(3, "set", new JValue("all_components"), true, extra);
            Check(rejected.IsError && rejected.Value == "storage_components.detail_only", "legacy nested storage-domain forwarding cannot turn component scope into a write");
        }
        var exactTarget = Call(3, "detail", new JValue("all_components"), true, new JObject { ["worldId"] = 7 });
        Check(exactTarget.IsError && f.Target.ComponentEnumerationReads == 0, "world mismatch preserves target scope before enumeration");
        foreach (int route in new[] { 0, 1, 2, 3 })
        {
            f.Filter.OnUpdated = ignored => f.Secondary.FixtureMass(1 + route);
            int updates = f.Filter.Updates;
            var applied = Call(route, "set_filter", null, false);
            Check(!applied.IsError && f.Filter.Updates == updates + 1, "scope-free write delegates once to the selected shared TreeFilterable");
            JObject result = JObject.Parse(applied.Value);
            Check(result["items"] == null && result["storageComponents"] == null && result["storageScope"] == null, "scope-free write retains compact primary summary");
            Check(f.Secondary.MassStored() == 1 + route, "fixture native filter callback may affect secondary storage; adapter does not claim primary-only native effects");
        }
        f.Filter.OnUpdated = null;
        Check(f.Filter.Updates == 4 && f.Child.Reads.Count == 0 && f.Neighbor.Reads.Count == 0, "write delegation does not add per-component loops or change another target");
        var list = Call(3, "list", null, false);
        Check(!list.IsError && (int)JObject.Parse(list.Value)["returned"] == 2 && JObject.Parse(list.Value)["storages"][0]["items"] == null, "scope-free canonical list remains compact and unchanged");
        var alias = Detail("all_components", 3);
        Check(JToken.DeepEquals(alias, JObject.Parse(Call(3, "detail", new JValue("all_components"), true, new JObject { ["domain"] = "stores", ["kind"] = "building" }).Value)), "canonical storage alias preserves detail scope");
        foreach (var tool in new[] { StorageTools.GetStorageDetail(), StorageTools.ControlStorage(), StorageTools.ControlStorageSystem(), BuildingControlTools.ControlBuilding() })
        {
            McpToolParameter parameter = tool.Parameters["storageScope"];
            Check(parameter.Type == "string" && !parameter.Required && parameter.EnumValues.SequenceEqual(new[] { "primary", "all_components" }), "actual detail and routing schemas expose only the optional supported scope enum");
        }
    }
    internal static int Run()
    {
        DefaultAndAll(); MissingAndIndependent(); EnumerationFailures(); RoutesAndWriteBoundary();
        return checks;
    }
}
