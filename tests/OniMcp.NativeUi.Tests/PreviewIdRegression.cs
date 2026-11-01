using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;

internal static class PreviewIdRegression
{
    private static int checks;
    private static int origin;
    private static BuildingDef previewDef;

    private static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Preview ID: " + reason);
    }
    private static void Setup()
    {
        BlueprintCompletionRegression.Setup(); BuildPlanningTools.ResetPlannerFixture();
        UnityEngine.Object.ThrowEnumeration = false;
        DebugHandler.InstantBuildMode = false; BuildingControlTools.IsVirtualFileEditContext = false;
        Db.Seed(null); Db.GetCalls = 0; Research.Instance = null;
        origin = BlueprintCompletionRegression.Cell;
        previewDef = new BuildingDef { PrefabID = "Wire", Name = "Fixture preview wire",
            ObjectLayer = ObjectLayer.Wire, TileLayer = ObjectLayer.NumLayers,
            ReplacementLayer = ObjectLayer.NumLayers, Replaceable = false, IsFoundation = false,
            BuildLocationRule = BuildLocationRule.Anywhere };
        BuildPlanningTools.FixtureDefs[previewDef.PrefabID] = previewDef;
        ToolUtil.PreviewQueryCalls = 0; ToolUtil.PreviewQueryArgs = null;
        ToolUtil.PreviewQuerySuccess = false; ToolUtil.PreviewQueryError = "fixture legacy query unavailable";
        ToolUtil.PreviewQueryX = 4; ToolUtil.PreviewQueryY = 4;
        WorldEditor.Area = null; WorldEditor.ResolveCalls = 0;
    }
    private static GameObject Anchor(int canonicalId, int unityId = 901, bool hasPrefabId = true,
        int? cell = null, bool completed = true)
    {
        int at = cell ?? origin;
        var def = new BuildingDef { PrefabID = "FixtureAnchor", Name = "Fixture anchor",
            ObjectLayer = ObjectLayer.Building, TileLayer = ObjectLayer.NumLayers };
        var go = new GameObject { Cell = at, Id = unityId };
        go.Add(new Building { Def = def });
        if (hasPrefabId) go.Add(new KPrefabID { InstanceID = canonicalId, PrefabTag = new Tag(def.PrefabID) });
        go.Add(new PrimaryElement { ElementID = "FixtureRock" });
        if (completed)
        {
            var complete = new BuildingComplete { Def = def }; go.Add(complete);
            Components.BuildingCompletes.Items.Add(complete);
        }
        else
        {
            var blueprint = new Constructable(); go.Add(blueprint);
            BuildPlanningTools.FixtureBlueprints.Add(blueprint);
        }
        NativeBuildingCellIndex.Shapes[go] = new FixtureGeometry {
            Origin = new BuildingGeometryCell { Cell = at, X = Grid.CellColumn(at), Y = Grid.CellRow(at) },
            Bounds = new[] { Grid.CellColumn(at), Grid.CellRow(at), Grid.CellColumn(at), Grid.CellRow(at) },
            Cells = new List<BuildingGeometryCell> {
                new BuildingGeometryCell { Cell = at, X = Grid.CellColumn(at), Y = Grid.CellRow(at) } }
        };
        return go;
    }
    private static JObject Args()
    {
        return new JObject { ["prefabId"] = previewDef.PrefabID, ["worldId"] = 0,
            ["material"] = "Cuprite", ["dryRun"] = false, ["confirm"] = false,
            ["autoDigObstructions"] = false, ["autoUprootObstructions"] = false,
            ["autoWire"] = false };
    }
    private static CallToolResult Invoke(JObject args)
    {
        var request = args.DeepClone();
        var completed = Components.BuildingCompletes.Items.ToArray();
        var blueprints = BuildPlanningTools.FixtureBlueprints.ToArray();
        var solid = Grid.Solid.ToArray(); var foundation = Grid.Foundation.ToArray();
        var layers = Grid.ObjectLayers.Select(layer => new Dictionary<int, GameObject>(layer)).ToArray();
        var tool = BuildPlanningTools.PreviewBuild();
        Check(tool.Mode == "read" && tool.Risk == "none", "actual preview descriptor remains read-only");
        var result = tool.Handler(args);
        Check(JToken.DeepEquals(request, args), "actual handler keeps input arguments unchanged while forcing child dry-run");
        Check(completed.SequenceEqual(Components.BuildingCompletes.Items)
            && blueprints.SequenceEqual(BuildPlanningTools.FixtureBlueprints), "completed/blueprint registries are not written");
        Check(solid.SequenceEqual(Grid.Solid) && foundation.SequenceEqual(Grid.Foundation), "terrain flags are not written");
        Check(layers.Select((layer, index) => layer.Count == Grid.ObjectLayers[index].Count
            && layer.All(pair => ReferenceEquals(pair.Value, Grid.ObjectLayers[index][pair.Key]))).All(value => value),
            "native object layer registrations are not written");
        Check(completed.All(item => item == null || item.gameObject == null
            || (!item.gameObject.Destroyed && item.gameObject.DeleteCalls == 0)), "native completed objects are not deleted");
        Check(previewDef.DirectBuildCalls == 0 && previewDef.TryPlaceCalls == 0
            && BlueprintCompletionRegression.Tool.BuildCalls == 0
            && BlueprintCompletionRegression.Tool.Activations == 0
            && BlueprintCompletionRegression.Controller.RestoreCalls == 0
            && BlueprintCompletionRegression.Select.Selects == 0
            && BuildPlanningTools.FixtureFallbackCalls == 0, "preview invokes no native UI/build action or direct-write fallback");
        return result;
    }
    private static JObject Success(JObject args, int x, int y)
    {
        var result = Invoke(args);
        Check(!result.IsError, "ID preview resolver must accept the exact visible completed instance");
        var body = JObject.Parse(result.Content[0].Text);
        Check(body["valid"]?.Value<bool>() == true && body["dryRun"]?.Value<bool>() == true
            && body["planned"]?.Value<bool>() == false && body["blueprintPlaced"]?.Value<bool>() == false,
            "actual parent/child force dry-run and never claim construction");
        Check(body["x"]?.Value<int>() == x && body["y"]?.Value<int>() == y,
            "actual child receives the resolved native reference or explicitly offset anchor");
        return body;
    }
    private static void Refusal(JObject args, string reason, string displayId)
    {
        var result = Invoke(args);
        Check(result.IsError && result.Content[0].Text.Contains(reason) && result.Content[0].Text.Contains(displayId)
            && result.Content[0].Text != "build.preview_anchor.unavailable", "localized refusal retains canonical reason/ID without fallback");
        Check(BuildPlanningTools.FixtureSelectCalls == 0 && ToolUtil.PreviewQueryCalls == 0,
            "invalid explicit ID cannot reach name search or child material/planning work");
    }
    internal static void Run()
    {
        var original = McpLocalization.LanguageProvider;
        try
        {
            McpLocalization.LanguageProvider = () => "en";
            ExactCompletedId();
            RejectedIdsAndTargets();
            OffsetGuards();
            LegacyResolverUnchanged();
            ActualAvailabilityInputs();
        }
        finally { McpLocalization.LanguageProvider = original; }
        Console.WriteLine("Preview ID: " + checks
            + " host checks passed (actual preview/anchor/parent source; native inputs substituted)");
    }
    private static void ExactCompletedId()
    {
        // First positive is the behavioral negative-control contract. Complete
        // prechange Anchors + Placement files must compile and fail this case.
        Setup(); Anchor(701); var args = Args(); args["id"] = 701;
        Success(args, Grid.CellColumn(origin), Grid.CellRow(origin));
        Check(ToolUtil.PreviewQueryCalls == 0 && BuildPlanningTools.FixtureSelectCalls == 1,
            "exact ID works without any query and planned prefab is distinct from anchor prefab");
        Setup(); var go = Anchor(701); var shape = NativeBuildingCellIndex.Shapes[go];
        shape.Bounds[0]--; shape.Bounds[1]--;
        args = Args(); args["id"] = 701; args["query"] = "other matching fixture";
        ToolUtil.PreviewQuerySuccess = true;
        Success(args, Grid.CellColumn(origin), Grid.CellRow(origin));
        Check(ToolUtil.PreviewQueryCalls == 0, "explicit canonical ID wins over query and uses native reference, not inferred lower-left");
        Setup(); Anchor(0, -701, false); args = Args(); args["id"] = -701;
        Success(args, Grid.CellColumn(origin), Grid.CellRow(origin));
        Setup(); Anchor(701, 901); args = Args(); args["id"] = 901; args["query"] = "fallback";
        ToolUtil.PreviewQuerySuccess = true;
        Refusal(args, "not_found", "901");
        Setup(); Anchor(0, 901); args = Args(); args["id"] = 901;
        Refusal(args, "not_found", "901");
    }
    private static void RejectedIdsAndTargets()
    {
        var invalid = new JToken[] { JValue.CreateNull(), new JValue(0), new JValue("701"),
            new JValue(701.0), new JValue(true), new JArray(701), new JObject { ["value"] = 701 },
            new JValue((long)int.MaxValue + 1), new JValue((long)int.MinValue - 1) };
        foreach (var id in invalid)
        {
            Setup(); Anchor(701); ToolUtil.PreviewQuerySuccess = true;
            var args = Args(); args["id"] = id; args["query"] = "otherwise valid";
            Refusal(args, "invalid_id", "?");
        }
        Setup(); Anchor(701); ToolUtil.PreviewQuerySuccess = true;
        var request = Args(); request["id"] = 702; request["query"] = "otherwise valid";
        Refusal(request, "not_found", "702");
        Setup(); Anchor(701, completed: false); request = Args(); request["id"] = 701;
        Refusal(request, "not_found", "701");
        Setup(); Anchor(701); Anchor(701, 902); request = Args(); request["id"] = 701;
        Refusal(request, "ambiguous", "701");
        Setup(); Anchor(701); Anchor(701, 902, cell: origin + 1); Grid.WorldIdx[origin + 1] = 1;
        request = Args(); request["id"] = 701;
        Refusal(request, "ambiguous", "701");
        Setup(); Anchor(701); Grid.Visible[origin] = 0; request = Args(); request["id"] = 701;
        Refusal(request, "not_found", "701");
        Setup(); Anchor(701); request = Args(); request["id"] = 701; request["worldId"] = 1;
        Refusal(request, "not_found", "701");
        Setup(); Anchor(701, cell: -1); request = Args(); request["id"] = 701;
        Refusal(request, "not_found", "701");
        Setup(); var go = Anchor(701); go.GetComponent<BuildingComplete>().Def = null;
        request = Args(); request["id"] = 701; Refusal(request, "unreadable", "701");
        Setup(); go = Anchor(701); go.ThrowFixtureComponentRead = true;
        request = Args(); request["id"] = 701; Refusal(request, "unreadable", "701");
        Setup(); Anchor(701); Game.Instance = null;
        request = Args(); request["id"] = 701; Refusal(request, "unreadable", "701");
        Setup(); Anchor(701); Game.Instance.Loading = true;
        request = Args(); request["id"] = 701; Refusal(request, "unreadable", "701");
    }
    private static void OffsetGuards()
    {
        Setup(); Anchor(701); var args = Args(); args["id"] = 701; args["dx"] = 1;
        Success(args, Grid.CellColumn(origin) + 1, Grid.CellRow(origin));
        Setup(); Anchor(701); args = Args(); args["id"] = 701; args["dx"] = 2; args["offsetX"] = 1;
        Success(args, Grid.CellColumn(origin) + 1, Grid.CellRow(origin));
        Setup(); Anchor(701); Grid.Visible[origin + 1] = 0;
        args = Args(); args["id"] = 701; args["dx"] = 1; Refusal(args, "not_found", "701");
        Setup(); Anchor(701); Grid.WorldIdx[origin + 1] = 1;
        args = Args(); args["id"] = 701; args["dx"] = 1; Refusal(args, "not_found", "701");
        foreach (var offset in new[] { -9, 9, int.MaxValue, int.MinValue })
        {
            Setup(); Anchor(701); args = Args(); args["id"] = 701; args["offsetX"] = offset;
            Refusal(args, "not_found", "701");
        }
    }
    private static void LegacyResolverUnchanged()
    {
        Setup(); var args = Args(); args["query"] = "legacy query"; ToolUtil.PreviewQuerySuccess = true;
        Success(args, 4, 4);
        Check(ToolUtil.PreviewQueryCalls == 1 && ReferenceEquals(ToolUtil.PreviewQueryArgs, args),
            "absent ID delegates unchanged legacy query resolver with the original request");
        Setup(); args = Args(); args["x"] = 1; args["y"] = 1; args["offsetX"] = 1; args["dy"] = 2;
        Success(args, 2, 3);
        Check(WorldEditor.ResolveCalls == 1 && ToolUtil.PreviewQueryCalls == 0,
            "absent ID preserves coordinate/area and offset alias path");
        Setup(); WorldEditor.Area = new PreviewFixtureArea { X = 2, Y = 2 };
        args = Args(); args["x"] = 1; args["y"] = 1;
        Success(args, 3, 3);
        Setup(); args = Args(); args["x"] = 7; args["query"] = "legacy partial coordinate fallback";
        ToolUtil.PreviewQuerySuccess = true; Success(args, 4, 4);
        Check(ToolUtil.PreviewQueryCalls == 1, "absent ID keeps legacy partial-coordinate query behavior");
        Setup(); args = Args(); var result = Invoke(args);
        Check(result.IsError && result.Content[0].Text == ToolUtil.PreviewQueryError && ToolUtil.PreviewQueryCalls == 1,
            "absent ID retains exact legacy error rather than new ID text");
    }
    private static void ActualAvailabilityInputs()
    {
        Setup(); var observed = BuildingAvailabilityReader.Read(previewDef);
        Check(observed.Value == null && observed.ReasonCode == "database_cache_missing" && Db.GetCalls == 0,
            "linked actual availability dependency preserves missing native research cache");
        previewDef.FixtureNativeAvailable = false; observed = BuildingAvailabilityReader.Read(previewDef);
        Check(observed.Value == false && observed.ReasonCode == "definition_unavailable",
            "linked actual reader preserves supplied unavailable native definition");
        Setup(); var db = new Db(); Db.Seed(db); Research.Instance = new Research();
        db.TechItems.Complete = false; observed = BuildingAvailabilityReader.Read(previewDef);
        Check(observed.Value == false && observed.ReasonCode == "research_locked", "linked actual reader preserves cached research denial");
        db.TechItems.Complete = true; observed = BuildingAvailabilityReader.Read(previewDef);
        Check(observed.Value == true && Db.GetCalls == 0, "linked actual reader combines supplied known native inputs without initialization");
        Db.Seed(null); Research.Instance = null;
    }
}
