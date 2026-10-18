using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

internal static partial class MapCountRegression
{
    private static void AdditionalProgressCases()
    {
        foreach (bool completedExisting in new[] { false, true })
        {
            var row = new JObject { ["x"] = 10, ["y"] = 20, ["planned"] = false, ["alreadyPresent"] = true,
                ["buildingCompleted"] = completedExisting, ["id"] = 123 };
            var native = NativeRows(row); native["planned"] = 0; native["succeeded"] = 1; native["alreadyPresent"] = 1;
            var value = Single(native);
            Require((int)value["executedCells"] == 1, "already-present native row satisfies the request cell");
            Require((bool)value["changed"] == completedExisting, "existing blueprint completion differs from already-present no-op");
            Require((string)BuildGroup(value)["appliedUnit"] == "requested_map_cells", "map building applied unit is explicit");
            var outcome = BuildGroup(value)["results"][0]["nativeOutcome"];
            Require((int?)outcome["counters"]["planned"] == 0 && (int)outcome["counters"]["succeeded"] == 1,
                "zero planned is preserved separately from one satisfied native row");
        }
        string[] unknown =
        {
            "{}", "not-json", "[]", "null", "true",
            "{\"planned\":1,\"succeeded\":1}",
            "{\"committed\":false,\"alreadyPresent\":1}",
            NativeRows(new JObject { ["x"] = 10, ["y"] = 20, ["planned"] = "true" }).ToString(),
            NativeRows(Placed(10), Placed(10)).ToString(),
            NativeRows(Placed(11)).ToString(),
            NativeRows(new JObject { ["x"] = "10", ["y"] = 20, ["planned"] = true }).ToString(),
            NativeRows(new JObject { ["x"] = 10, ["y"] = 20, ["planned"] = true, ["prefabId"] = "Other" }).ToString(),
            NativeRows(new JObject { ["x"] = 10, ["y"] = 20, ["planned"] = true, ["id"] = "123" }).ToString(),
            "{\"results\":null}", "{\"results\":{}}"
        };
        foreach (string refusalFlag in new[] { "none", "partial", "deferred", "throttled" })
        {
            Setup();
            var refusal = new JObject { ["committed"] = false, ["failed"] = 1, ["hardFailed"] = 1,
                ["errors"] = new JArray(new JObject { ["x"] = 10, ["y"] = 20, ["planned"] = false, ["valid"] = false }) };
            if (refusalFlag == "partial") refusal["partial"] = true;
            if (refusalFlag == "deferred") refusal["deferred"] = 1;
            if (refusalFlag == "throttled") refusal["throttled"] = true;
            MapNativeBoundary.BuildingResults.Enqueue(Native(refusal.ToString()));
            var args = Parent(); args["command"] = "edit"; args["path"] = "/active/map/viewport.md";
            args["content"] = "<<<<<<< SEARCH\n" + Snapshot + "\n=======\n" + Replace("Tile:5 . .") + "\n>>>>>>> REPLACE";
            var batchArgs = Parent(); batchArgs["steps"] = new JArray(args);
            var batch = Parsed(WorldEditorTools.BatchForCountTest(batchArgs));
            Require(!(bool)batch["ok"] && !(bool)batch["changed"] && (int)batch["mutatedSteps"] == 0,
                "known all-refused native result remains failed and unchanged");
            Require((bool)batch["partial"] == (refusalFlag != "none") && (bool)batch["results"][0]["partial"] == (refusalFlag != "none"),
                "all-refused is not partial success; explicit native partial/deferred/throttled is retained");
            // The compact text may be truncated; use a second queued native fixture for the direct outer adapter.
            MapNativeBoundary.BuildingResults.Enqueue(Native(refusal.ToString()));
            var outer = Parsed(WorldEditorTools.EditForCountTest(args));
            var map = JObject.Parse((string)outer["results"][0]["result"]);
            Require((bool)outer["partial"] == (refusalFlag != "none") && (bool)map["partial"] == (refusalFlag != "none"),
                "all-refused partial contract survives map and outer edit");
            Require((int)map["executedCells"] == 0 && (int)map["remainingCells"] == 1 && !(bool)map["changed"],
                "known refusal has zero satisfied cells without erasing remaining request scope");
            Require((int)BuildGroup(map)["failed"] == 1 && (int)outer["applied"] == 0,
                "refusal failure count stays separate from applied edit blocks");
        }
        foreach (string raw in unknown)
        {
            Setup(); MapNativeBoundary.BuildingResults.Enqueue(Native(raw));
            var value = RunMap("Tile:5 . .");
            Require(value["executedCells"].Type == JTokenType.Null && value["remainingCells"].Type == JTokenType.Null,
                "missing, ambiguous or invalid native evidence is not zero or full satisfaction");
            Require((int)value["cellProgress"]["unknown"] == 1 && (int)value["cellProgress"]["knownSatisfied"] == 0,
                "unknown request cells remain explicit");
            Require(!(bool)value["completionKnown"] && (string)value["next"] != "complete", "unknown evidence cannot claim completion");
            Require((string)BuildGroup(value)["results"][0]["result"] == raw, "native result text remains exact");
        }
        var mixedRows = NativeRows(Placed(10), new JObject { ["x"] = 11, ["y"] = 20, ["planned"] = "invalid" });
        var mixedUnknown = Single(mixedRows, "Tile:5 Tile:5 .");
        Require(mixedUnknown["executedCells"].Type == JTokenType.Null && (int)mixedUnknown["cellProgress"]["knownSatisfied"] == 1,
            "known subset survives an unknown second anchor");
        Require((int)mixedUnknown["cellProgress"]["unknown"] == 1 && (bool)mixedUnknown["changed"], "known mutation survives an unknown neighbor");

        var deferredNative = NativeRows(Placed(10));
        deferredNative["remainingAnchors"] = new JArray(new JObject { ["x"] = 11, ["y"] = 20 });
        deferredNative["deferred"] = 1; deferredNative["throttled"] = true;
        var deferred = Single(deferredNative, "Tile:5 Tile:5 .");
        Require((int)deferred["executedCells"] == 1 && (int)deferred["remainingCells"] == 1 && (bool)deferred["partial"],
            "deferred native anchors are not treated as satisfied cells");

        Setup(); MapNativeBoundary.BuildingResults.Enqueue(Native(NativeRows(Placed(10)).ToString()));
        var budgetArgs = Parent(); budgetArgs["maxWriteCells"] = 1;
        var budget = Parsed(WorldEditorTools.ApplyMapForCountTest(budgetArgs, Snapshot, Replace("Tile:5 Tile:5 .")));
        Require(MapNativeBoundary.BuildingCalls.Count == 1 && ((JArray)MapNativeBoundary.BuildingCalls[0]["anchors"]).Count == 1,
            "write budget preserves native call scope");
        Require((int)budget["executedCells"] == 1 && (int)budget["remainingCells"] == 1 && (bool)budget["partial"],
            "unexecuted budget cells stay pending without becoming unknown");

        Setup(); Assets.BuildingDefs.Add(Def("Wide", 2));
        var fullMaskNative = NativeRows(Placed(10)); fullMaskNative["prefabId"] = "Wide";
        MapNativeBoundary.BuildingResults.Enqueue(Native(fullMaskNative.ToString()));
        var fullMaskArgs = Parent(); fullMaskArgs["maxWriteCells"] = 1;
        var fullMaskBudget = Parsed(WorldEditorTools.ApplyMapForCountTest(fullMaskArgs, Snapshot, Replace("Wide:5 Wide:5 .")));
        Require(((JArray)MapNativeBoundary.BuildingCalls[0]["anchors"]).Count == 1,
            "full-mask budget preserves the original single native anchor call");
        Require((int)fullMaskBudget["executedCells"] == 2 && (int)fullMaskBudget["remainingCells"] == 0,
            "same executed anchor satisfies every original requested mask cell");
        Require(!(bool)fullMaskBudget["partial"] && (string)fullMaskBudget["next"] == "complete",
            "budget clipping alone does not invent pending cells after the same native building satisfies the whole mask");

        var failedAfterPlacement = NativeRows(new JObject { ["x"] = 10, ["y"] = 20, ["planned"] = false,
            ["valid"] = false, ["blueprintPlaced"] = true, ["id"] = 123 });
        failedAfterPlacement["failed"] = 1;
        Setup(); MapNativeBoundary.BuildingResults.Enqueue(Native(failedAfterPlacement.ToString(), true));
        var incident = RunMap("Tile:5 . .");
        Require(!(bool)incident["ok"] && (bool)incident["changed"] && (bool)incident["partial"],
            "native error after a confirmed effect keeps the error and partial effect");

        Setup(); Assets.BuildingDefs.Add(Def("Wide", 2));
        var wideNative = NativeRows(Placed(10)); wideNative["prefabId"] = "Wide";
        MapNativeBoundary.BuildingResults.Enqueue(Native(wideNative.ToString()));
        var marker = RunMap("Wide:5 . .");
        Require((int)marker["executedCells"] == 1 && (int)marker["cellProgress"]["requested"] == 1,
            "wide native footprint requested by one marker counts one request cell");

        foreach (JToken detail in new JToken[] { null, false, true, "true" })
        {
            Setup(); QueueMixed();
            var step = Parent(); step["command"] = "edit"; step["path"] = "/active/map/viewport.md";
            step["content"] = "<<<<<<< SEARCH\n" + Snapshot + "\n=======\n" + Replace("Tile:5 Ladder:5 .") + "\n>>>>>>> REPLACE";
            if (detail != null) step["detail"] = detail;
            var args = Parent(); args["detail"] = true; args["steps"] = new JArray(step);
            var value = Parsed(WorldEditorTools.BatchForCountTest(args));
            var item = value["results"][0];
            bool explicitDetail = detail?.Type == JTokenType.Boolean && detail.Value<bool>();
            Require((item["result"] != null) == explicitDetail, "batch full child result requires the step's strict Boolean detail=true");
            Require((bool)value["partial"] && (bool)item["partial"] && (bool)item["nativeOutcome"]["changed"], "compact and detailed batch retain partial effect evidence");
            if (explicitDetail)
            {
                var child = JObject.Parse((string)item["result"]);
                var map = JObject.Parse((string)child["results"][0]["result"]);
                var build = BuildGroup(map);
                var native = JObject.Parse((string)build["results"][0]["result"]);
                Require((int)native["results"][0]["id"] == 101, "batch explicit detail retains independently parseable successful native IDs");
                var failed = JObject.Parse((string)build["results"][1]["result"]);
                Require((int)failed["errors"][0]["x"] == 11, "batch explicit detail retains unresolved native anchor evidence");
                Require(((string)item["text"]).Length == 903 && ((string)item["result"]).Length > 900,
                    "existing bounded batch text is independent of explicit complete child result");
            }
        }

        foreach (var gate in new[] { new JObject { ["dryRun"] = true }, new JObject { ["confirm"] = false } })
        {
            Setup(); var args = Parent(); args.Merge(gate);
            var preview = Parsed(WorldEditorTools.ApplyMapForCountTest(args, Snapshot, Replace("Tile:5 . .")));
            Require((bool)preview["preview"] && MapNativeBoundary.BuildingCalls.Count == 0, "preview and confirm gates do not call native mutation");
        }
        Setup();
        var executionFailure = NativeRows(Placed(10), Placed(11), new JObject { ["x"] = 12, ["y"] = 20,
            ["planned"] = false, ["valid"] = false, ["reasonCode"] = "placement_conflict" });
        executionFailure["planned"] = 2; executionFailure["succeeded"] = 2; executionFailure["failed"] = 1;
        executionFailure["remainingAnchors"] = new JArray(new JObject { ["x"] = 12, ["y"] = 20 });
        executionFailure["remainingAction"] = new JObject { ["tool"] = "building_control", ["arguments"] = new JObject {
            ["domain"] = "planning", ["action"] = "build_area", ["anchors"] = executionFailure["remainingAnchors"].DeepClone() } };
        MapNativeBoundary.BuildingResults.Enqueue(Native(executionFailure.ToString(), true));
        var failureStep = Parent(); failureStep["command"] = "edit"; failureStep["path"] = "/active/map/viewport.md";
        failureStep["detail"] = true;
        failureStep["content"] = "<<<<<<< SEARCH\n" + Snapshot + "\n=======\n" + Replace("Tile:5 Tile:5 Tile:5") + "\n>>>>>>> REPLACE";
        var failureBatchArgs = Parent(); failureBatchArgs["steps"] = new JArray(failureStep);
        var failureBatch = Parsed(WorldEditorTools.BatchForCountTest(failureBatchArgs));
        var failureOuter = JObject.Parse((string)failureBatch["results"][0]["result"]);
        var failureMap = JObject.Parse((string)failureOuter["results"][0]["result"]);
        var failureNative = JObject.Parse((string)BuildGroup(failureMap)["results"][0]["result"]);
        Require(!(bool)failureBatch["ok"] && (bool)failureBatch["partial"] && (bool)failureBatch["changed"],
            "failed native execution retains the partial effect through actual outer batch");
        Require((int)failureMap["executedCells"] == 2 && (int)failureNative["results"][1]["id"] == 111,
            "failed child retains successful native IDs and satisfied request-cell count");
        Require(JToken.DeepEquals(failureNative["remainingAction"], executionFailure["remainingAction"])
            && JToken.DeepEquals(failureNative["remainingAnchors"], executionFailure["remainingAnchors"]),
            "explicit batch detail preserves exact native remaining action and anchors after failure");

        Setup(); MapNativeBoundary.BuildingResults.Enqueue(Native(NativeRows(Placed(10)).ToString()));
        MapNativeBoundary.OrderResults.Enqueue(Native("{\"marked\":1,\"changed\":true}"));
        var mixedUnits = RunMap("Tile:5 dig:5 .");
        Require(MapNativeBoundary.BuildingCalls.Count == 1 && MapNativeBoundary.OrderCalls.Count == 1, "build and order each retain their native call");
        Require(mixedUnits["executedCells"].Type == JTokenType.Null && (int)mixedUnits["cellProgress"]["knownSatisfied"] == 1
            && (int)mixedUnits["cellProgress"]["unknown"] == 1, "order counters of unproven units are not added to building request cells");
        Require((bool)mixedUnits["changed"], "explicit order/native effects survive independently of unknown cell quantity");

        Setup();
        var readBatch = Parent(); readBatch["steps"] = new JArray(new JObject { ["command"] = "read", ["path"] = "/active/map/viewport.md", ["detail"] = true });
        var readBatchValue = Parsed(WorldEditorTools.BatchForCountTest(readBatch));
        Require(readBatchValue["results"][0]["result"] == null, "unrelated batch reads do not gain full raw output through the new detail gate");
        Require(MapNativeBoundary.BuildingCalls.Count == 0, "batch read remains read-only");
        Setup();
        var invalidPatch = WorldEditorTools.ApplyMapForCountTest(Parent(), Snapshot, Replace("Tile:5 Tile:0 ."));
        Require(invalidPatch.IsError && MapNativeBoundary.BuildingCalls.Count == 0, "all map tokens are validated before any native placement");

        foreach (bool order in new[] { false, true })
        {
            Setup();
            if (order) MapNativeBoundary.OrderResults.Enqueue(null);
            else MapNativeBoundary.BuildingResults.Enqueue(null);
            var absent = RunMap(order ? "dig:5 . ." : "Tile:5 . .");
            Require(!(bool)absent["ok"] && absent["changed"].Type == JTokenType.Null && !(bool)absent["completionKnown"],
                "null native building/order result is failed and unknown without an exception");
            Require(absent["executedCells"].Type == JTokenType.Null && (int)absent["cellProgress"]["unknown"] == 1,
                "null native result does not manufacture zero satisfied cells");
            Require(MapNativeBoundary.BuildingCalls.Count + MapNativeBoundary.OrderCalls.Count == 1,
                "null native result is never retried");
        }

        Setup(); var worldArgs = Parent(); worldArgs["worldId"] = 0;
        var wrongWorld = NativeRows(new JObject { ["x"] = 10, ["y"] = 20, ["worldId"] = 1, ["planned"] = true, ["id"] = 123 });
        MapNativeBoundary.BuildingResults.Enqueue(Native(wrongWorld.ToString()));
        var foreign = Parsed(WorldEditorTools.ApplyMapForCountTest(worldArgs, Snapshot, Replace("Tile:5 . .")));
        Require((int)foreign["cellProgress"]["unknown"] == 1, "native world mismatch cannot prove local satisfaction");
    }
    private static JObject Placed(int x) => new JObject { ["x"] = x, ["y"] = 20, ["planned"] = true, ["blueprintPlaced"] = true, ["id"] = 100 + x };
    private static JObject NativeRows(params JObject[] rows) => new JObject
    {
        ["prefabId"] = "Tile", ["committed"] = true, ["planned"] = rows.Length, ["succeeded"] = rows.Length,
        ["failed"] = 0, ["results"] = new JArray(rows), ["remainingAnchors"] = new JArray(), ["preflightErrors"] = new JArray()
    };
    private static JObject Single(JObject native, string row = "Tile:5 . .")
    {
        Setup(); MapNativeBoundary.BuildingResults.Enqueue(Native(native.ToString())); return RunMap(row);
    }
    private static JObject BuildGroup(JObject map) => JObject.Parse((string)map["results"][0]["result"]);
}
