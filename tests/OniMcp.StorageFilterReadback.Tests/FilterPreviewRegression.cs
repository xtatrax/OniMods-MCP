using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using UnityEngine;

internal static class FilterPreviewRegression
{
    private static int checks;
    private static void Check(bool value, string description)
    {
        checks++;
        if (!value) throw new InvalidOperationException("Filter preview guard: " + description);
    }
    private sealed class Fixture
    {
        internal string Kind;
        internal GameObject Object, Neighbor, Item;
        internal TreeFilterable Tree, NeighborTree;
        internal Filterable Single;
        internal FlatTagFilterable Flat;
        internal Storage Storage;
        internal List<GameObject> Contents;
    }
    private static Fixture Make(string kind)
    {
        Components.BuildingCompletes.ThrowRead = false;
        Components.BuildingCompletes.Items.Clear();
        Game.Instance = new Game();
        var value = new Fixture
        {
            Kind = kind,
            Object = new GameObject { Id = 31, name = "Target fixture", WorldId = 0 },
            Neighbor = new GameObject { Id = 32, name = "Neighbor fixture", WorldId = 0 },
            Item = new GameObject { Id = 41, name = "Contents fixture", WorldId = 0 },
            Tree = new TreeFilterable(), NeighborTree = new TreeFilterable(),
            Single = new Filterable(), Storage = new Storage { capacityKg = 20000f, storageFilters = new List<Tag>() }
        };
        value.Object.transform.Position = new Vector3 { x = 31, y = 20 };
        value.Neighbor.transform.Position = new Vector3 { x = 32, y = 20 };
        value.Object.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = 31, PrefabTag = new Tag("StorageLocker") };
        value.Neighbor.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = 32, PrefabTag = new Tag("StorageLocker") };
        value.Object.Components[typeof(Storage)] = value.Storage;
        value.Object.Components[typeof(StorageLocker)] = new StorageLocker { UserMaxCapacity = 500f };
        value.Contents = new List<GameObject> { value.Item };
        value.Storage.items = value.Contents;
        value.Storage.FixtureMass(12f);
        value.Tree.FixtureSet("Original");
        value.NeighborTree.FixtureSet("NeighborOnly");
        value.Tree.FilterStorage = value.Storage;
        value.Single.FixtureSet("Original");
        value.Single.Options["Fixture"] = new List<Tag> { new Tag("Original"), new Tag("Next"), new Tag("Other") };
        if (kind == "single") value.Object.Components[typeof(Filterable)] = value.Single;
        else value.Object.Components[typeof(TreeFilterable)] = value.Tree;
        if (kind == "flat")
        {
            value.Flat = new FlatTagFilterable { Owner = value.Object };
            value.Flat.FixtureSet("Original");
            value.Flat.tagOptions.AddRange(value.Single.Options["Fixture"]);
            value.Object.Components[typeof(FlatTagFilterable)] = value.Flat;
        }
        value.Neighbor.Components[typeof(TreeFilterable)] = value.NeighborTree;
        Components.BuildingCompletes.Items.Add(new BuildingComplete { gameObject = value.Object });
        Components.BuildingCompletes.Items.Add(new BuildingComplete { gameObject = value.Neighbor });
        value.Storage.Writes = value.Object.ComponentReads = value.Neighbor.ComponentReads = 0;
        Components.BuildingCompletes.Reads = 0;
        return value;
    }
    private static JObject Args(Fixture fixture, JToken dryRun)
    {
        var result = new JObject
        {
            ["id"] = fixture.Object.Id, ["worldId"] = 0, ["kind"] = fixture.Kind,
            ["tag"] = "Next", ["tags"] = new JArray("Next"), ["mode"] = "add"
        };
        if (dryRun != null) result["dryRun"] = dryRun.DeepClone();
        return result;
    }
    private static CallToolResult Invoke(Fixture fixture, JObject args, int route)
    {
        bool storage = fixture.Kind == "storage";
        args["action"] = storage ? "set_filter" : "set";
        args["domain"] = storage ? "storage" : "filter";
        if (route == 0) return BuildingControlTools.ControlBuilding().Handler(args);
        if (route == 1) return StorageTools.ControlStorageSystem().Handler(args);
        if (storage) return route == 2 ? StorageTools.ControlStorage().Handler(args) : StorageTools.SetStorageFilter().Handler(args);
        if (route == 2) return FilterTools.ControlFilter().Handler(args);
        if (route == 3) return FilterTools.SetFilter().Handler(args);
        return fixture.Kind == "single" ? FilterTools.SetSingleFilter().Handler(args) : FilterTools.SetTreeFilter().Handler(args);
    }
    private static void Preserved(Fixture fixture)
    {
        Check(fixture.Tree.Updates == 0, "preview must not update native tags");
        Check(fixture.Single.Writes == 0 && (fixture.Flat == null || fixture.Flat.SelectCalls == 0), "preview does not select a single or flat native tag");
        Check(fixture.Tree.Reads == 0 && fixture.Single.Reads == 0 && fixture.Single.OptionReads == 0,
            "preview rejects before native selected-tag or option reads");
        Check(Components.BuildingCompletes.Reads == 0 && fixture.Object.ComponentReads == 0 && fixture.Neighbor.ComponentReads == 0,
            "preview rejects before lookup and component resolution");
        Check(fixture.Tree.FixtureTags().SequenceEqual(new[] { "Original" }) && fixture.Single.FixtureTag() == "Original",
            "preview preserves independently observed filter values");
        Check(fixture.Flat == null || fixture.Flat.selectedTags.Select(tag => tag.Name).SequenceEqual(new[] { "Original" }),
            "preview preserves flat selection values");
        Check(fixture.Storage.Writes == 0 && fixture.Contents.Count == 1 && ReferenceEquals(fixture.Contents[0], fixture.Item),
            "preview preserves stored object references and performs no storage writes");
        Check(fixture.NeighborTree.Updates == 0 && fixture.NeighborTree.FixtureTags().SequenceEqual(new[] { "NeighborOnly" }),
            "preview preserves neighboring filters");
    }
    private static void Rejected(string kind, JToken flag, int route, string reason, bool clear = false)
    {
        var fixture = Make(kind);
        var args = Args(fixture, flag);
        if (clear) { args.Remove("tag"); args.Remove("tags"); args["clear"] = true; args.Remove("mode"); }
        var response = Invoke(fixture, args, route);
        // Keep this first: the old whole-source negative must fail because a
        // real handler changed the native substitute, not a metadata assertion.
        Preserved(fixture);
        Check(response.IsError, "unsupported/invalid preview is an error");
        var data = JObject.Parse(response.Value);
        Check((string)data["reasonCode"] == reason && (string)data["parameter"] == "dryRun", "rejection is machine readable");
        Check((bool)data["previewSupported"] == false && (bool)data["nativeInvoked"] == false && (bool)data["changed"] == false,
            "error does not claim preview validation or native execution");
        Check((string)data["message"] == "filter_preview.error." + reason, "message resolves the dedicated localization key");
    }
    private static void PreviewRoutes()
    {
        foreach (string kind in new[] { "tree", "single", "flat", "storage" })
        {
            int routes = kind == "storage" ? 4 : 5;
            for (int route = 0; route < routes; route++) Rejected(kind, new JValue(true), route, "unsupported_preview");
            if (kind != "storage") Rejected(kind, new JValue(true), 4, "unsupported_preview", clear: true);
        }
    }
    private static void InvalidFlags()
    {
        foreach (string kind in new[] { "single", "tree", "flat", "storage" })
            foreach (JToken flag in new JToken[] { new JValue("true"), new JValue("false"), new JValue(1), JValue.CreateNull(), new JArray(), new JObject { ["privateFixture"] = "do_not_echo" } })
                Rejected(kind, flag, 0, "invalid_boolean");
        var fixture = Make("tree");
        Components.BuildingCompletes.ThrowRead = true;
        try
        {
            var args = Args(fixture, new JValue(true)); args["id"] = -999;
            var result = Invoke(fixture, args, 0);
            Check(result.IsError && (string)JObject.Parse(result.Value)["reasonCode"] == "unsupported_preview",
                "unsupported preview refuses even when target lookup would throw");
            Preserved(fixture);
        }
        finally { Components.BuildingCompletes.ThrowRead = false; }
    }
    private static void UnrelatedPreserved(Fixture fixture)
    {
        Check(fixture.NeighborTree.Updates == 0 && fixture.NeighborTree.FixtureTags().SequenceEqual(new[] { "NeighborOnly" }),
            "normal write retains exact target scope");
        Check(fixture.Storage.Writes == 0 && fixture.Contents.Count == 1 && ReferenceEquals(fixture.Contents[0], fixture.Item),
            "adapter does not introduce storage or inventory mutation");
    }
    private static void NormalWrites()
    {
        var single = Make("single");
        Check(!Invoke(single, Args(single, null), 0).IsError && single.Single.Writes == 1 && single.Single.FixtureTag() == "Next",
            "omitted dryRun still delegates a valid single selection once");
        var clearSingle = Args(single, new JValue(false)); clearSingle["clear"] = true; clearSingle.Remove("tag");
        Check(!Invoke(single, clearSingle, 4).IsError && single.Single.Writes == 2 && single.Single.FixtureTag() == GameTags.Void.Name,
            "false dryRun preserves native single clear execution");
        UnrelatedPreserved(single);
        foreach (string kind in new[] { "tree", "flat" })
        {
            var fixture = Make(kind);
            var add = Invoke(fixture, Args(fixture, null), 0);
            Check(!add.IsError && fixture.Tree.Updates == 1 && fixture.Tree.FixtureTags().SequenceEqual(new[] { "Next", "Original" }),
                "omitted dryRun preserves native add " + kind);
            var remove = Args(fixture, new JValue(false)); remove["mode"] = "remove"; remove["tags"] = new JArray("Original");
            Check(!Invoke(fixture, remove, 1).IsError && fixture.Tree.Updates == 2 && fixture.Tree.FixtureTags().SequenceEqual(new[] { "Next" }),
                "false dryRun preserves native remove " + kind);
            var replace = Args(fixture, null); replace["mode"] = "replace"; replace["tags"] = new JArray("Other");
            Check(!Invoke(fixture, replace, 4).IsError && fixture.Tree.Updates == 3 && fixture.Tree.FixtureTags().SequenceEqual(new[] { "Other" }),
                "legacy leaf preserves replace " + kind);
            var clear = Args(fixture, new JValue(false)); clear["clear"] = true; clear.Remove("mode"); clear.Remove("tags");
            Check(!Invoke(fixture, clear, 3).IsError && fixture.Tree.Updates == 4 && fixture.Tree.FixtureTags().Length == 0,
                "false dryRun preserves clear alias " + kind);
            Check(kind != "flat" || fixture.Flat.SelectCalls > 0, "flat normal writes retain native SelectTag delegation");
            UnrelatedPreserved(fixture);
        }
        var storage = Make("storage");
        var applied = Invoke(storage, Args(storage, new JValue(false)), 0);
        Check(!applied.IsError && storage.Tree.Updates == 1 && storage.Tree.FixtureTags().SequenceEqual(new[] { "Next", "Original" }),
            "false dryRun preserves independent storage filter writer");
        UnrelatedPreserved(storage);
    }
    private static void ReadAndMetadata()
    {
        foreach (string kind in new[] { "single", "tree", "flat" })
        {
            var fixture = Make(kind);
            var args = new JObject { ["domain"] = "filter", ["action"] = "list", ["kind"] = kind,
                ["worldId"] = 0, ["includeOptions"] = true, ["dryRun"] = true };
            var listed = BuildingControlTools.ControlBuilding().Handler(args);
            Check(!listed.IsError && (int)JObject.Parse(listed.Value)["returned"] >= 1, "dryRun does not disable filter list " + kind);
            Check(fixture.Tree.Updates == 0 && fixture.Single.Writes == 0 && (fixture.Flat == null || fixture.Flat.SelectCalls == 0),
                "filter list remains read only " + kind);
            UnrelatedPreserved(fixture);
        }
        foreach (McpTool tool in new[] { FilterTools.SetSingleFilter(), FilterTools.SetTreeFilter(), FilterTools.SetFilter(),
            FilterTools.ControlFilter(), StorageTools.SetStorageFilter(), StorageTools.ControlStorage(), StorageTools.ControlStorageSystem() })
            Check(tool.Parameters.ContainsKey("dryRun") && tool.Parameters["dryRun"].Type == "boolean"
                && tool.Parameters["dryRun"].Description == "filter_preview.parameter.dry_run" && !tool.Parameters["dryRun"].Required,
                "write metadata advertises unsupported preview contract " + tool.Name);
    }
    internal static int Run()
    {
        PreviewRoutes();
        InvalidFlags();
        NormalWrites();
        ReadAndMetadata();
        Components.BuildingCompletes.ThrowRead = false;
        Components.BuildingCompletes.Items.Clear();
        return checks;
    }
}
