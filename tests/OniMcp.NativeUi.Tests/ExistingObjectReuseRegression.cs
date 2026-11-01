using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

// The entire production TryPlanOne and exact existing-object matching bodies run.
// Native geometry, support validation and unrelated services are fixture inputs.
internal static class ExistingObjectReuseRegression
{
    private static int checks;
    private static BuildingDef definition;
    private static int cell;

    private static void Check(bool value, string reason)
    {
        checks++;
        if (!value) throw new InvalidOperationException("Existing object reuse: " + reason);
    }

    private static void Setup()
    {
        BlueprintCompletionRegression.Setup();
        BuildPlanningTools.ResetPlannerFixture();
        UnityEngine.Object.ThrowEnumeration = false;
        DebugHandler.InstantBuildMode = false;
        BuildingControlTools.IsVirtualFileEditContext = false;
        cell = BlueprintCompletionRegression.Cell;
        definition = new BuildingDef {
            PrefabID = "FixtureReuseBuilding", Name = "Fixture reuse building",
            ObjectLayer = ObjectLayer.Building, TileLayer = ObjectLayer.NumLayers,
            BuildLocationRule = BuildLocationRule.OnFloor,
            ReplacementLayer = ObjectLayer.NumLayers,
            EquivalentReplacementLayers = new List<ObjectLayer>(),
            Replaceable = false, IsFoundation = false
        };
        BuildPlanningTools.FixtureDefs[definition.PrefabID] = definition;
    }

    private static JObject Preview(bool wireSerialization = false)
    {
        return BuildPlanningTools.PlanForTest(definition.PrefabID, cell,
            new JObject {
                ["worldId"] = 0, ["dryRun"] = true, ["confirm"] = false,
                ["material"] = "Cuprite", ["autoDigObstructions"] = false,
                ["autoUprootObstructions"] = false, ["autoWire"] = false
            }, wireSerialization);
    }

    private static void NoPlacementWrites(GameObject existing = null)
    {
        Check(definition.DirectBuildCalls == 0 && definition.TryPlaceCalls == 0
            && BlueprintCompletionRegression.Tool.BuildCalls == 0
            && BlueprintCompletionRegression.Tool.Activations == 0,
            "preview does not place or activate native construction");
        Check(existing == null || (!existing.Destroyed && existing.DeleteCalls == 0
            && ReferenceEquals(Grid.Objects[cell, (int)ObjectLayer.Building], existing)),
            "matching object remains registered and intact");
    }

    internal static void Run()
    {
        ReuseDoesNotClaimSupport(true);
        ReuseDoesNotClaimSupport(false);
        NewPlacementStillValidatesSupport();
        Console.WriteLine("Existing object reuse: " + checks
            + " host checks passed (full production parent/matching; native inputs substituted)");
    }

    private static void ReuseDoesNotClaimSupport(bool completed)
    {
        Setup();
        var existing = TileReplacementFixture.Make(definition, completed ? 901 : 902,
            cell, "Cuprite", completed);
        if (!completed) existing.GetComponent<Constructable>().IsReplacementTile = false;
        Grid.Objects[cell, (int)ObjectLayer.Building] = existing;
        // A false collaborator makes accidental support evaluation observable.
        BuildPlanningTools.FixtureSupportValid = false;
        var result = Preview();
        Check((bool)result["valid"] && (bool)result["alreadyPresent"]
            && !(bool)result["planned"] && !(bool)result["blueprintPlaced"]
            && (bool)result["alreadyBuilding"] == completed
            && (bool)result["alreadyBlueprint"] == !completed,
            "matching completed/blueprint reuse retains its idempotent result");
        Check((string)result["validationScope"] == "existing_object_reuse",
            "reuse explicitly identifies validation scope instead of new placement eligibility");
        var support = result["support"] as JObject;
        Check(support != null && (string)support["status"] == "not_evaluated",
            "reuse explicitly reports support as unevaluated");
        foreach (string key in new[] { "currentValid", "nativeFoundationValid", "requiresSupportCompletion" })
            Check(support[key] != null && support[key].Type == JTokenType.Null,
                "unevaluated " + key + " is present as null rather than true or false");
        // Serialize the raw production dictionary, before any JObject conversion.
        var wireSupport = Preview(true)["support"] as JObject;
        Check(wireSupport != null
            && new[] { "currentValid", "nativeFoundationValid", "requiresSupportCompletion" }
                .All(key => wireSupport[key] != null && wireSupport[key].Type == JTokenType.Null),
            "actual wire serializer preserves all three explicit null fields");
        Check(BuildPlanningTools.FixtureSupportCalls == 0 && BuildPlanningTools.FixtureSelectCalls == 0,
            "reuse neither evaluates support nor selects new construction material");
        Check(result["placement"] is JObject && result["footprint"].Count() == 1
            && (int)result["footprint"][0]["cell"] == cell
            && (bool)result["existing"]["placementCheck"]["valid"]
            && (int)result["existing"]["id"] == existing.Id,
            "reuse retains exact geometry and matching object identity");
        NoPlacementWrites(existing);
    }

    private static void NewPlacementStillValidatesSupport()
    {
        Setup();
        BuildPlanningTools.FixtureSupportValid = false;
        var result = Preview();
        Check(!(bool)result["valid"] && (string)result["details"]["reasonCode"] == "unsupported"
            && !(bool)result["details"]["support"]["valid"]
            && BuildPlanningTools.FixtureSupportCalls == 1,
            "new unsupported placement still invokes and preserves support refusal");
        Check(result["validationScope"] == null,
            "new placement is not mislabeled as existing object reuse");
        NoPlacementWrites();

        Setup();
        result = Preview();
        Check((bool)result["valid"] && !(bool)result["planned"] && !(bool)result["blueprintPlaced"]
            && (bool)result["support"]["valid"] && BuildPlanningTools.FixtureSupportCalls == 1,
            "new supported preview retains the evaluated support result");
        Check(result["validationScope"] == null,
            "evaluated support result keeps its normal placement scope");
        NoPlacementWrites();
    }
}
