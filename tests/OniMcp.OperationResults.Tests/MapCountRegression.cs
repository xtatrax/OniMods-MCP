using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

// Whole production adapters consume native-shaped child fixtures.
// Native fixtures are child BuildArea-shaped outcomes. Wrappers/parsing/preflight
// are production source and cannot be supplied by this fixture.
internal static partial class MapCountRegression
{
    private const string Snapshot = "百位X: 0 0 0\n十位X: 1 1 1\n个位X: 0 1 2\nY=20: . . .";
    private static int checks;
    internal static void Run()
    {
        Setup();
        MapNativeBoundary.BuildingResults.Enqueue(Native(@"{""anchorCount"":3,""attempted"":2,""succeeded"":2,""planned"":2,""failed"":1,""deferred"":0,""throttled"":false,""remainingAnchors"":[{""x"":12,""y"":20}],""preflightErrors"":[{""x"":12,""y"":20,""valid"":false}],""results"":[{""x"":10,""y"":20,""planned"":true,""blueprintPlaced"":true,""id"":101},{""x"":11,""y"":20,""planned"":true,""blueprintPlaced"":true,""id"":102}]}"));
        var same = RunMap("Tile:5 Tile:5 Tile:5");
        Require(MapNativeBoundary.BuildingCalls.Count == 1, "same-token cells reach one native call");
        Require(((JArray)MapNativeBoundary.BuildingCalls[0]["anchors"]).Count == 3, "actual footprint code derives three anchors");
        Require((int)same["executedCells"] == 2, "native two successes produce exactly two satisfied request cells");
        Require((bool)same["partial"] && (int)same["remainingCells"] == 1, "same-token partial child retains remaining request cells");
        Print("same_token_partial", same);

        Setup(); QueueMixed();
        var mixed = RunMap("Tile:5 Ladder:5 .");
        Require(MapNativeBoundary.BuildingCalls.Count == 2, "different tokens route to two native calls");
        Require((int)mixed["executedCells"] == 1 && (bool)mixed["partial"], "failed build group retains successful first token cells");
        var inner = JObject.Parse((string)mixed["results"][0]["result"]);
        Require((int)inner["applied"] == 1, "actual BuildMapEdit still records first native token progress");
        Print("mixed_token_map", mixed);

        Setup(); QueueMixed();
        var outerArgs = Parent();
        outerArgs["path"] = "/active/map/viewport.md";
        outerArgs["content"] = "<<<<<<< SEARCH\n" + Snapshot + "\n=======\n" + Replace("Tile:5 Ladder:5 .") + "\n>>>>>>> REPLACE";
        var outer = Parsed(WorldEditorTools.EditForCountTest(outerArgs));
        Require(MapNativeBoundary.BuildingCalls.Count == 2, "outer edit executes actual map and native calls");
        Require((int)outer["applied"] == 0 && (bool)outer["partial"] && (bool)outer["changed"], "outer failure retains native partial progress");
        Print("mixed_token_outer", outer);

        Setup(); QueueMixed();
        var batchArgs = Parent();
        var editStep = (JObject)outerArgs.DeepClone(); editStep["command"] = "edit";
        batchArgs["steps"] = new JArray(editStep);
        var batch = Parsed(WorldEditorTools.BatchForCountTest(batchArgs));
        Require(MapNativeBoundary.BuildingCalls.Count == 2, "batch executes actual outer edit and map native calls");
        Require((int)batch["applied"] == 0 && (bool)batch["partial"] && (bool)batch["changed"] && (int)batch["mutatedSteps"] == 1, "enclosing batch retains native partial progress");
        Print("mixed_token_batch", batch);

        Setup();
        Assets.BuildingDefs.Add(Def("Wide", 2));
        MapNativeBoundary.BuildingResults.Enqueue(Native(@"{""anchorCount"":1,""attempted"":1,""succeeded"":1,""planned"":1,""failed"":0,""deferred"":0,""throttled"":false,""remainingAnchors"":[],""preflightErrors"":[],""results"":[{""x"":10,""y"":20,""planned"":true,""blueprintPlaced"":true,""id"":103}]}"));
        var wide = RunMap("Wide:5 Wide:5 .");
        Require(((JArray)MapNativeBoundary.BuildingCalls[0]["anchors"]).Count == 1, "actual footprint mask maps two cells to one anchor");
        Require((int)wide["executedCells"] == 2 && !(bool)wide["partial"], "regression: completed two-cell mask remains two request cells");
        Print("wide_mask_complete", wide);
        AdditionalProgressCases();
        Console.WriteLine("Map count/partial regression checks passed: " + checks);
    }
    private static void QueueMixed()
    {
        MapNativeBoundary.BuildingResults.Enqueue(Native(@"{""anchorCount"":1,""attempted"":1,""succeeded"":1,""planned"":1,""failed"":0,""results"":[{""x"":10,""y"":20,""planned"":true,""blueprintPlaced"":true,""id"":101}]}"));
        MapNativeBoundary.BuildingResults.Enqueue(Native(@"{""anchorCount"":1,""committed"":false,""succeeded"":0,""planned"":0,""failed"":1,""errors"":[{""x"":11,""y"":20,""valid"":false,""planned"":false,""reasonCode"":""placement_conflict""}]}" , true));
    }
    private static JObject Parent() => new JObject { ["confirm"] = true, ["allowPartial"] = true, ["task"] = "host count probe", ["autoLayerContext"] = false, ["sourcePath"] = "/active/map/viewport.md" };
    private static JObject RunMap(string row) => Parsed(WorldEditorTools.ApplyMapForCountTest(Parent(), Snapshot, Replace(row)));
    private static string Replace(string row) => Snapshot.Substring(0, Snapshot.LastIndexOf("Y=20:", StringComparison.Ordinal)) + "Y=20: " + row;
    private static CallToolResult Native(string json, bool error = false) => error ? CallToolResult.Error(json) : CallToolResult.Text(json);
    private static JObject Parsed(CallToolResult result) => JObject.Parse(result.Content[0].Text);
    private static void Print(string name, JObject value) { Require(value["changed"] != null, name + " keeps change availability"); }
    private static void Require(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Setup()
    {
        MapNativeBoundary.Snapshot = Snapshot;
        MapNativeBoundary.BuildingResults.Clear(); MapNativeBoundary.BuildingCalls.Clear();
        MapNativeBoundary.OrderResults.Clear(); MapNativeBoundary.OrderCalls.Clear();
        Assets.BuildingDefs.Clear(); Assets.BuildingDefs.Add(Def("Tile", 1)); Assets.BuildingDefs.Add(Def("Ladder", 1));
    }
    private static FixtureBuildingDef Def(string id, int width)
    {
        var cells = new List<NativePoint>(); for (int x = 0; x < width; x++) cells.Add(new NativePoint { X = x, Y = 0 });
        return new FixtureBuildingDef { PrefabID = id, Name = id, Shape = new NativeShape { Size = new[] { width, 1 }, Bounds = new[] { 0, 0, width - 1, 0 }, Cells = cells } };
    }
}
