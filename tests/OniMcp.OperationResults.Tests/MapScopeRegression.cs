using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

// Exercises the production map parser, footprint compiler and build adapter.
// The native boundary records child requests; it does not implement selection,
// interpolation, placement or the game's world-resolution rules.
internal static partial class MapCountRegression
{
    private const string ScopeSnapshot = "百位X: 0 0 0 0 0\n十位X: 1 1 1 1 1\n个位X: 0 1 2 3 4\nY=22: . . . . .\nY=21: . . . . .\nY=20: . . . . .";

    private static readonly string[] ScopeSelectors =
    {
        "x", "y", "x1", "y1", "x2", "y2", "cell", "cells", "id",
        "points", "fromX", "fromY", "toX", "toY", "fromId", "toId",
        "fromQuery", "toQuery", "sourceQuery", "targetQuery", "query",
        "target", "search", "name", "nearX", "nearY", "areaId", "relative",
        "offsetX", "offsetY", "dx", "dy", "plan", "blueprint", "sequence",
        "text", "steps", "items", "dense", "stepX", "stepY"
    };

    private static void AdditionalScopeCases()
    {
        int start = checks;
        var cases = new[]
        {
            new { Name = "one-cell viewport", Prefab = "Wire", Cells = new[] { Point(12, 21) } },
            new { Name = "sparse same row", Prefab = "Wire", Cells = new[] { Point(10, 21), Point(14, 21) } },
            new { Name = "diagonal cells", Prefab = "LiquidConduit", Cells = new[] { Point(10, 20), Point(14, 22) } },
            new { Name = "multiple rows", Prefab = "Wire", Cells = new[] { Point(10, 22), Point(11, 22), Point(14, 21), Point(10, 20), Point(12, 20) } }
        };
        foreach (var fixture in cases)
        {
            ScopeSetup();
            var args = ScopeParent();
            args["x1"] = 0; args["y1"] = 0; args["x2"] = 90; args["y2"] = 90;
            var before = args.DeepClone();
            QueueScopeResult(fixture.Prefab, fixture.Cells);
            var result = Parsed(WorldEditorTools.ApplyMapForCountTest(args, ScopeSnapshot,
                ScopeReplacement(fixture.Prefab + ":5", fixture.Cells)));
            Require((bool)result["ok"], fixture.Name + " reaches the native boundary successfully");
            Require(MapNativeBoundary.BuildingCalls.Count == 1, fixture.Name + " dispatches exactly once");
            AssertScope(MapNativeBoundary.BuildingCalls[0], fixture.Cells, 0, fixture.Name);
            Require((int)result["executedCells"] == fixture.Cells.Length, fixture.Name + " counts only requested cells");
            Require(JToken.DeepEquals(args, before), fixture.Name + " leaves parent bounds and payload unchanged");
        }

        // A payload may contain old path endpoints or a relative placement plan.
        // None may reselect or shift the absolute cells compiled from the map.
        foreach (bool outerSelectors in new[] { false, true })
        {
            ScopeSetup();
            var args = ScopeParent();
            var selectors = new JObject
            {
                ["x"] = 91, ["y"] = 92, ["x1"] = 80, ["y1"] = 81, ["x2"] = 93, ["y2"] = 94,
                ["cell"] = 9090, ["cells"] = new JArray(9090, 9091), ["id"] = 999,
                ["anchors"] = new JArray(Point(90, 90)), ["points"] = new JArray(Point(80, 80), Point(90, 90)),
                ["fromX"] = 80, ["fromY"] = 80, ["toX"] = 90, ["toY"] = 90,
                ["fromId"] = 998, ["toId"] = 999,
                ["fromQuery"] = "old source", ["toQuery"] = "old destination",
                ["sourceQuery"] = "old source", ["targetQuery"] = "old destination",
                ["query"] = "old query", ["target"] = "old target", ["search"] = "old search", ["name"] = "old name",
                ["nearX"] = 88, ["nearY"] = 88, ["areaId"] = "fixture-area", ["relative"] = true,
                ["offsetX"] = 50, ["offsetY"] = 60, ["dx"] = 7, ["dy"] = 8,
                ["plan"] = "old plan", ["blueprint"] = "old blueprint", ["sequence"] = "old sequence",
                ["text"] = "old text", ["steps"] = new JArray("old step"), ["items"] = new JArray("old item"),
                ["dense"] = true, ["stepX"] = 5, ["stepY"] = 6
            };
            args["payload"] = selectors;
            if (outerSelectors)
                foreach (var property in selectors.Properties().ToList()) args[property.Name] = property.Value.DeepClone();
            args["worldId"] = 7;
            args["orientation"] = "R90";
            args["rotation"] = 90;
            args["material"] = "CopperOre";
            args["facade"] = "fixture-facade";
            args["allowUtilityTerrainDig"] = true;
            args["autoDigObstructions"] = false;
            args["maxAutoDigCells"] = 3;
            args["autoConnectPower"] = false;
            args["nativePathPlacement"] = false;
            args["maxCommitAnchors"] = 2;
            var before = args.DeepClone();
            var cells = new[] { Point(12, 21) };
            QueueScopeResult("Wire", cells);
            var result = Parsed(WorldEditorTools.ApplyMapForCountTest(args, ScopeSnapshot, ScopeReplacement("Wire:5", cells)));
            Require((bool)result["ok"] && MapNativeBoundary.BuildingCalls.Count == 1, "selector-bearing map dispatches once");
            var child = MapNativeBoundary.BuildingCalls[0];
            AssertScope(child, cells, 7, outerSelectors ? "outer selectors" : "nested payload selectors");
            foreach (string key in new[] { "orientation", "rotation", "material", "facade", "task", "confirm", "dryRun",
                "allowUtilityTerrainDig", "autoDigObstructions", "maxAutoDigCells", "autoConnectPower", "nativePathPlacement", "maxCommitAnchors" })
                Require(JToken.DeepEquals(child[key], args[key]), "scope cleanup preserves " + key);
            Require((int)child["priority"] == 5 && (string)child["prefabId"] == "Wire", "map token controls prefab and priority");
            Require((string)child["domain"] == "planning" && (string)child["action"] == "build_area", "map retains exact-placement route");
            Require(MapNativeBoundary.WorldResolutionCalls.Count == 1
                && (string)MapNativeBoundary.WorldResolutionCalls[0]["areaId"] == "fixture-area",
                "world resolution receives the area before its selector is discarded");
            Require(JToken.DeepEquals(args, before), "scope cleanup never mutates nested payload or parent selectors");
        }

        ScopeSetup();
        MapNativeBoundary.WorldResolver = args =>
        {
            Require((string)args["areaId"] == "fixture-area" && (bool)args["relative"], "area world is resolved with source context intact");
            return 9;
        };
        var areaArgs = ScopeParent();
        areaArgs["payload"] = new JObject { ["areaId"] = "fixture-area", ["relative"] = true, ["offsetX"] = 80 };
        var areaBefore = areaArgs.DeepClone();
        QueueScopeResult("Wire", new[] { Point(10, 20) });
        var areaResult = Parsed(WorldEditorTools.ApplyMapForCountTest(areaArgs, ScopeSnapshot, ScopeReplacement("Wire:5", new[] { Point(10, 20) })));
        Require((bool)areaResult["ok"], "area-derived world result reaches the child");
        AssertScope(MapNativeBoundary.BuildingCalls.Single(), new[] { Point(10, 20) }, 9, "area-derived world");
        Require(JToken.DeepEquals(areaArgs, areaBefore), "area resolution leaves parent payload unchanged");

        ScopeSetup();
        Assets.BuildingDefs.Add(Def("Wide", 2));
        var wideArgs = ScopeParent(); wideArgs["x1"] = 0; wideArgs["y1"] = 0; wideArgs["x2"] = 90; wideArgs["y2"] = 90;
        var wideMask = new[] { Point(10, 21), Point(11, 21) };
        QueueScopeResult("Wide", new[] { Point(10, 21) });
        var wide = Parsed(WorldEditorTools.ApplyMapForCountTest(wideArgs, ScopeSnapshot, ScopeReplacement("Wide:5", wideMask)));
        Require((bool)wide["ok"] && (int)wide["executedCells"] == 2, "two occupied cells remain one complete building request");
        AssertScope(MapNativeBoundary.BuildingCalls.Single(), new[] { Point(10, 21) }, 0, "multi-cell footprint");

        foreach (bool confirm in new[] { false, true })
        foreach (bool dryRun in new[] { false, true })
        {
            if (confirm && !dryRun) continue;
            ScopeSetup();
            var args = ScopeParent(); args["confirm"] = confirm; args["dryRun"] = dryRun;
            args["payload"] = new JObject { ["confirm"] = true, ["dryRun"] = false, ["points"] = new JArray(Point(90, 90)) };
            var before = args.DeepClone();
            var result = Parsed(WorldEditorTools.ApplyMapForCountTest(args, ScopeSnapshot, ScopeReplacement("Wire:5", new[] { Point(12, 21) })));
            Require((bool)result["preview"] && !(bool)result["changed"], "parent confirmation and preview gate stay non-mutating");
            // This public preview predates the optional layer-context metadata.
            Require(result["details"]?["placementValidated"]?.Value<bool>() != true,
                "syntax preview does not claim native placement acceptance");
            Require(MapNativeBoundary.BuildingCalls.Count == 0 && MapNativeBoundary.WorldResolutionCalls.Count == 0,
                "unconfirmed or preview map never enters building dispatch or execution-world resolution");
            Require(JToken.DeepEquals(args, before), "preview leaves source arguments unchanged");
        }

        ScopeSetup();
        var noChangeArgs = ScopeParent();
        var noChangeBefore = noChangeArgs.DeepClone();
        var noChange = WorldEditorTools.ApplyMapForCountTest(noChangeArgs, ScopeSnapshot, ScopeSnapshot);
        Require(noChange.IsError && MapNativeBoundary.BuildingCalls.Count == 0, "unchanged map never dispatches construction");
        Require(JToken.DeepEquals(noChangeArgs, noChangeBefore), "unchanged map preserves parent arguments");
        Setup();
        Console.WriteLine("Map scope regression checks passed: " + (checks - start));
    }

    private static JObject Point(int x, int y) => new JObject { ["x"] = x, ["y"] = y };
    private static JObject ScopeParent()
    {
        var args = Parent();
        args["dryRun"] = false;
        return args;
    }
    private static void ScopeSetup()
    {
        Setup();
        MapNativeBoundary.Snapshot = ScopeSnapshot;
        Assets.BuildingDefs.Add(Def("Wire", 1));
        Assets.BuildingDefs.Add(Def("LiquidConduit", 1));
    }
    private static string ScopeReplacement(string token, IEnumerable<JObject> cells)
    {
        var selected = new HashSet<string>(cells.Select(CellKey));
        var rows = new List<string> { "百位X: 0 0 0 0 0", "十位X: 1 1 1 1 1", "个位X: 0 1 2 3 4" };
        for (int y = 22; y >= 20; y--)
            rows.Add("Y=" + y + ": " + string.Join(" ", Enumerable.Range(10, 5).Select(x => selected.Contains(x + "," + y) ? token : ".")));
        return string.Join("\n", rows);
    }
    private static string CellKey(JToken cell) => (int)cell["x"] + "," + (int)cell["y"];
    private static void AssertScope(JObject child, IEnumerable<JObject> expected, int worldId, string name)
    {
        var leaked = ScopeSelectors.Where(key => child[key] != null).ToArray();
        Require(leaked.Length == 0, name + " must not pass alternate selectors: " + string.Join(", ", leaked));
        var anchors = child["anchors"] as JArray;
        var wanted = new HashSet<string>(expected.Select(CellKey));
        Require(anchors != null && anchors.Count == wanted.Count && wanted.SetEquals(anchors.Select(CellKey)),
            name + " forwards exactly the compiled absolute anchors, including sparse gaps");
        Require((int?)child["worldId"] == worldId, name + " preserves the resolved world explicitly");
    }
    private static void QueueScopeResult(string prefab, IEnumerable<JObject> anchors)
    {
        var rows = anchors.Select((anchor, index) => new JObject
        {
            ["x"] = anchor["x"].DeepClone(), ["y"] = anchor["y"].DeepClone(),
            ["planned"] = true, ["blueprintPlaced"] = true, ["id"] = 700 + index
        }).ToArray();
        var native = NativeRows(rows); native["prefabId"] = prefab;
        MapNativeBoundary.BuildingResults.Enqueue(Native(native.ToString()));
    }
}
