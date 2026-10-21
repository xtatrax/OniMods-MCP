using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string description)
    {
        assertions++;
        if (!condition) throw new Exception(description);
    }
    private sealed class Fixture
    {
        internal GameObject Object;
        internal TreeFilterable Filter;
        internal Storage Storage;
    }
    private static Fixture Make(int id, int world, params string[] tags)
    {
        var fixture = new Fixture
        {
            Object = new GameObject { Id = id, name = "Storage fixture", WorldId = world },
            Filter = new TreeFilterable(),
            Storage = new Storage { capacityKg = 20000f }
        };
        fixture.Object.transform.Position = new Vector3 { x = id, y = world + 10 };
        fixture.Object.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = id, PrefabTag = new Tag("StorageLocker") };
        fixture.Object.Components[typeof(Storage)] = fixture.Storage;
        fixture.Object.Components[typeof(TreeFilterable)] = fixture.Filter;
        fixture.Object.Components[typeof(StorageLocker)] = new StorageLocker { UserMaxCapacity = 500f };
        fixture.Filter.FixtureSet(tags);
        Components.BuildingCompletes.Items.Add(new BuildingComplete { gameObject = fixture.Object });
        return fixture;
    }
    private static JObject Detail(int id, int world)
    {
        var result = StorageTools.GetStorageDetail().Handler(new JObject { ["id"] = id, ["worldId"] = world });
        Check(!result.IsError, "independent native detail succeeds");
        return JObject.Parse(result.Value);
    }
    private static JObject Apply(Fixture fixture, string mode, JToken tags, int route, params string[] expected)
    {
        int beforeUpdates = fixture.Filter.Updates;
        var args = new JObject { ["id"] = fixture.Object.Id, ["worldId"] = fixture.Object.WorldId, ["tags"] = tags };
        if (mode != null) args["mode"] = mode;
        if (route > 0) args["action"] = "set_filter";
        if (route == 2) args["domain"] = "storage";
        var result = route == 0 ? StorageTools.SetStorageFilter().Handler(args)
            : route == 1 ? StorageTools.ControlStorage().Handler(args) : StorageTools.ControlStorageSystem().Handler(args);
        Check(!result.IsError, "actual filter handler accepts valid operation");
        var response = JObject.Parse(result.Value);
        Check(response["acceptedTags"].Values<string>().SequenceEqual(expected), "write response observes the new sorted native filter");
        var detail = Detail(fixture.Object.Id, fixture.Object.WorldId);
        Check(JToken.DeepEquals(response["acceptedTags"], detail["acceptedTags"]), "write tags equal independent immediate detail");
        Check((double)response["massStoredKg"] == (double)detail["massStoredKg"], "write mass equals immediate native mass read");
        Check(fixture.Filter.Updates == beforeUpdates + 1, "each valid request delegates exactly one native filter update");
        Check((int)response["id"] == fixture.Object.Id && (int)response["worldId"] == fixture.Object.WorldId, "write response retains exact target identity");
        Check(response["items"] == null, "write response retains compact item omission");
        Check((float)response["capacityKg"] == 20000f && (float)response["userMaxCapacityKg"] == 500f, "capacity projection remains compatible");
        return response;
    }

    private static void Main()
    {
        Components.BuildingCompletes.Items.Clear();
        var target = Make(11, 0, "SandStone");
        var unrelated = Make(12, 0, "Iron");
        var remote = Make(13, 7, "Cuprite");
        Check(Detail(11, 0)["acceptedTags"].Values<string>().SequenceEqual(new[] { "SandStone" }), "pre-update detail records old native filter");
        Apply(target, "replace", new JArray("Dirt"), 0, "Dirt");
        Apply(target, "add", new JArray("Algae", "Dirt", "Algae"), 1, "Algae", "Dirt");
        Apply(target, "remove", new JArray("Dirt"), 2, "Algae");
        Apply(target, "remove", new JArray("Algae"), 0);
        Check(!Detail(11, 0)["acceptedTags"].HasValues, "nonempty remove request can restore initially empty filter");
        Apply(target, "remove", new JArray("SandStone"), 1);
        Apply(target, null, new JArray("Dirt", "Algae", "Dirt"), 2, "Algae", "Dirt");
        Apply(target, "replace", new JValue(" SandStone, Dirt, SandStone "), 0, "Dirt", "SandStone");
        Check(unrelated.Filter.Updates == 0 && remote.Filter.Updates == 0, "explicit target writes never update another storage");
        Check(Detail(12, 0)["acceptedTags"].Values<string>().SequenceEqual(new[] { "Iron" }), "neighbor storage filter is unchanged");
        Check(Detail(13, 7)["acceptedTags"].Values<string>().SequenceEqual(new[] { "Cuprite" }), "other-world storage filter is unchanged");

        // Simulate an authoritative native post-state different from the requested set.
        // This is a boundary test, not an assertion about ONI tag eligibility rules.
        target.Filter.OnUpdated = filter => filter.FixtureSet("NativeAccepted");
        Apply(target, "replace", new JArray("Dirt"), 0, "NativeAccepted");
        target.Filter.OnUpdated = null;

        var item = new GameObject { Id = 100, name = "Inventory fixture", WorldId = 0 };
        item.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = 100, PrefabTag = new Tag("Dirt") };
        item.Components[typeof(PrimaryElement)] = new PrimaryElement { Mass = 12.5f };
        target.Storage.items.Add(item);
        target.Storage.FixtureMass(12.5f);
        int massReads = target.Storage.MassReads;
        var withInventory = Apply(target, "replace", new JArray("Dirt"), 0, "Dirt");
        Check((float)withInventory["massStoredKg"] == 12.5f && target.Storage.MassReads >= massReads + 2, "filter response reads current native inventory mass");
        Check(target.Storage.items.Count == 1 && ReferenceEquals(target.Storage.items[0], item), "MCP filter projection does not force inventory ejection or movement");
        var inventory = Detail(11, 0)["items"];
        Check(inventory.Count() == 1 && (float)inventory[0]["massKg"] == 12.5f && (string)inventory[0]["prefabId"] == "Dirt", "independent detail retains actual item inventory");

        // An independent native callback can change mass during UpdateFilters.
        // The adapter must read after the call, without manufacturing movement.
        target.Filter.OnUpdated = filter => target.Storage.FixtureMass(4f);
        var afterNativeMass = Apply(target, "add", new JArray("Algae"), 0, "Algae", "Dirt");
        Check((float)afterNativeMass["massStoredKg"] == 4f, "response uses post-update native mass");
        Check(target.Storage.items.Count == 1 && ((PrimaryElement)item.Components[typeof(PrimaryElement)]).Mass == 12.5f, "MCP does not reconcile or simulate native inventory");
        target.Filter.OnUpdated = null;

        int beforeUpdates = target.Filter.Updates;
        foreach (JToken empty in new JToken[] { new JArray(), new JArray("", " ", JValue.CreateNull()), new JValue(" , ") })
        {
            var result = StorageTools.SetStorageFilter().Handler(new JObject { ["id"] = 11, ["worldId"] = 0, ["mode"] = "replace", ["tags"] = empty });
            Check(result.IsError, "empty tag input is rejected");
            Check(target.Filter.Updates == beforeUpdates, "rejected empty input does not clear or update native filter");
        }
        Check(Detail(11, 0)["acceptedTags"].Values<string>().SequenceEqual(new[] { "Algae", "Dirt" }), "empty-input rejection preserves prior native tags");

        var rejectedWorld = StorageTools.SetStorageFilter().Handler(new JObject { ["id"] = 11, ["worldId"] = 7, ["tags"] = new JArray("Algae") });
        Check(rejectedWorld.IsError && target.Filter.Updates == beforeUpdates, "conflicting explicit world never mutates target");
        var rejectedSelector = StorageTools.SetStorageFilter().Handler(new JObject { ["id"] = 11, ["worldId"] = 0, ["query"] = "NotThisStorage", ["tags"] = new JArray("Algae") });
        Check(rejectedSelector.IsError && target.Filter.Updates == beforeUpdates, "conflicting semantic selector never mutates target");
        var ambiguous = StorageTools.SetStorageFilter().Handler(new JObject { ["query"] = "Storage fixture", ["worldId"] = 0, ["tags"] = new JArray("Algae") });
        Check(ambiguous.IsError && target.Filter.Updates == beforeUpdates && unrelated.Filter.Updates == 0, "ambiguous query never selects one storage arbitrarily");

        target.Object.Components.Remove(typeof(TreeFilterable));
        var noFilter = StorageTools.SetStorageFilter().Handler(new JObject { ["id"] = 11, ["tags"] = new JArray("Algae") });
        Check(noFilter.IsError && target.Filter.Updates == beforeUpdates, "missing native filter refuses mutation");
        target.Object.Components[typeof(TreeFilterable)] = target.Filter;

        int nativeBeforeRead = target.Filter.Updates;
        var listed = StorageTools.GetStorageList().Handler(new JObject { ["worldId"] = 0 });
        Check(!listed.IsError && (int)JObject.Parse(listed.Value)["returned"] == 2, "unchanged list route preserves explicit world scope");
        Check(target.Filter.Updates == nativeBeforeRead && unrelated.Filter.Updates == 0 && remote.Filter.Updates == 0, "list and independent detail reads never perform filter writes");
        Check(Components.BuildingCompletes.Items.Count == 3 && target.Storage.items.Count == 1, "adapter preserves world objects and item inventory");
        Console.WriteLine("Storage filter readback adapter: " + assertions + " assertions passed");
        Console.WriteLine("Storage item detail adapter: " + StorageItemDetailsRegression.Run() + " assertions passed");
    }
}
