using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

// Full production planner and native adapter; native opinion, UI and geometry
// are explicit fixtures. False flag combinations are contract cases, not proof
// that a named game prefab uses those flags in the running game.
internal static class TileReplacementGeneralizationRegression
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new InvalidOperationException("General floor replacement: " + message);
    }

    private static void Setup(string oldId = "Tile", string nextId = "MetalTile", bool solid = true, bool foundation = true)
    {
        TileReplacementFixture.Setup();
        TileReplacementFixture.OldDefinition.PrefabID = oldId;
        TileReplacementFixture.Original.GetComponent<KPrefabID>().PrefabTag = new Tag(oldId);
        TileReplacementFixture.Definition.PrefabID = nextId;
        TileReplacementFixture.Definition.CraftRecipe.Result = new Tag(nextId);
        BuildPlanningTools.FixtureDefs[oldId] = TileReplacementFixture.OldDefinition;
        BuildPlanningTools.FixtureDefs[nextId] = TileReplacementFixture.Definition;
        Grid.Solid[TileReplacementFixture.Cell] = solid;
        Grid.Foundation[TileReplacementFixture.Cell] = foundation;
    }

    private static JObject Opinion(JObject result)
        => result["tileReplacement"]?["nativeCandidate"] as JObject;
    private static string Reason(JObject result)
        => result["details"]?["reasonCode"]?.Value<string>();
    private static void NoGesture(string message)
    {
        Check(TileReplacementFixture.Plan.CopyCalls == 0 && TileReplacementFixture.Tool.BuildCalls == 0
            && TileReplacementFixture.Materials.MaterialSelectCalls == 0, message + " stays before native UI");
        Check(TileReplacementFixture.Definition.DirectBuildCalls == 0 && TileReplacementFixture.Definition.TryPlaceCalls == 0
            && BuildPlanningTools.FixtureFallbackCalls == 0 && BuildPlanningTools.FixtureAutoDigCalls == 0,
            message + " has no direct placement, dig or fallback");
        Check(TileReplacementFixture.Original.DeleteCalls == 0 && TileReplacementFixture.Wire.DeleteCalls == 0,
            message + " never removes the original floor or wire");
    }

    private static void Preserved(bool solid, bool foundation, string message)
    {
        int cell = TileReplacementFixture.Cell;
        Check(ReferenceEquals(Grid.Objects[cell, (int)ObjectLayer.Building], TileReplacementFixture.Original)
            && ReferenceEquals(Grid.Objects[cell, (int)ObjectLayer.FoundationTile], TileReplacementFixture.Original)
            && ReferenceEquals(Grid.Objects[cell, (int)ObjectLayer.Wire], TileReplacementFixture.Wire)
            && Grid.Solid[cell] == solid && Grid.Foundation[cell] == foundation,
            message + " preserves the exact occupants and observed flags");
        Check(TileReplacementFixture.Original.GetComponent<PrimaryElement>().ElementID == "FixtureRock"
            && TileReplacementFixture.Original.GetComponent<PrimaryElement>().Mass == 100f
            && TileReplacementFixture.Original.GetComponent<BuildingFacade>().CurrentFacade == null
            && TileReplacementFixture.Original.DeleteCalls == 0 && TileReplacementFixture.Wire.DeleteCalls == 0,
            message + " preserves original material, mass, facade and wire");
    }

    internal static void Run()
    {
        NativeCompatiblePairs();
        NativeRefusalAndUnknown();
        ExactIdentityAndScope();
        ObservedFlagsAndTerrainProtection();
        SamePrefabContract();
        Console.WriteLine("General floor replacement: " + checks
            + " host checks passed (real planner/adapter; native replies and physical flags substituted)");
    }

    private static void NativeCompatiblePairs()
    {
        foreach (var pair in new[] {
            new[] { "Tile", "MetalTile" }, new[] { "MetalTile", "Tile" },
            new[] { "GasPermeableMembrane", "CarpetTile" }, new[] { "MeshTile", "MetalTile" }
        })
        {
            Setup(pair[0], pair[1]);
            var preview = TileReplacementFixture.PlanRead(TileReplacementFixture.Args(true));
            Check(preview["valid"].Value<bool>() && !preview["planned"].Value<bool>()
                && preview["tileReplacement"]["validationScope"].Value<string>() == "native_replacement_candidate_only",
                pair[0] + " to " + pair[1] + " uses native-compatible candidate preview");
            Check(Opinion(preview)?["assessmentState"].Value<string>() == "known"
                && Opinion(preview)["oldReplaceable"].Value<bool>() && Opinion(preview)["canReplace"].Value<bool>()
                && Opinion(preview)["candidateMatchesOriginal"].Value<bool>()
                && Opinion(preview)["candidateId"].Value<int>() == 101,
                "preview reports exact native facts rather than fabricated placement success");
            NoGesture("passive generalized preview");
            Preserved(true, true, "passive generalized preview");

            Setup(pair[0], pair[1]);
            GameObject queue = null;
            TileReplacementFixture.Tool.OnBuild = cell => queue = TileReplacementFixture.Queue();
            var result = TileReplacementFixture.PlanRead();
            Check(result["valid"].Value<bool>() && result["planned"].Value<bool>()
                && !result["buildingCompleted"].Value<bool>() && result["queueVerified"].Value<bool>(),
                pair[0] + " to " + pair[1] + " is a verified reservation, not completion");
            Check(queue != null && ReferenceEquals(queue.GetComponent<Building>().Def, TileReplacementFixture.Definition)
                && queue.GetComponent<Constructable>().IsReplacementTile
                && queue.GetComponent<Constructable>().SelectedElementsTags.Single().Name == "Cuprite"
                && queue.GetComponent<Prioritizable>().GetMasterPriority().priority_value == 7
                && TileReplacementFixture.Tool.BuildCalls == 1 && TileReplacementFixture.Plan.CopyCalls == 1,
                "the ordinary native gesture yields the exact ordered material, priority and definition");
            Check(TileReplacementFixture.Definition.DirectBuildCalls == 0 && TileReplacementFixture.Definition.TryPlaceCalls == 0,
                "expanded pairs do not use low-level replacement");
            Preserved(true, true, "generalized native reservation");

            BuildPlanningTools.FixtureMaterialValid = false;
            int selects = BuildPlanningTools.FixtureSelectCalls;
            result = TileReplacementFixture.PlanRead();
            Check(result["valid"].Value<bool>() && result["alreadyPresent"].Value<bool>()
                && !result["planned"].Value<bool>() && TileReplacementFixture.Tool.BuildCalls == 1
                && BuildPlanningTools.FixtureSelectCalls == selects,
                "the exact expanded reservation is reusable without a second gesture or new inventory selection");
        }
    }

    private static void NativeRefusalAndUnknown()
    {
        foreach (string name in new[] { "FloorSwitch", "FarmTile", "FixtureNativeRejectingFloor" })
        {
            Setup(nextId: name);
            TileReplacementFixture.Definition.NativeCanReplace = false;
            var result = TileReplacementFixture.PlanRead(TileReplacementFixture.Args(true));
            Check(!result["valid"].Value<bool>() && Reason(result) == "candidate_rejected"
                && Opinion(result)?["assessmentState"].Value<string>() == "known"
                && Opinion(result)["canReplace"].Value<bool>() == false
                && Opinion(result)["candidateMatchesOriginal"].Value<bool>(),
                "native rejection for " + name + " retains its actual false opinion and is not generic obstruction");
            NoGesture("native rejection");
        }
        Setup(); TileReplacementFixture.OldDefinition.Replaceable = false;
        var rejected = TileReplacementFixture.PlanRead();
        Check(Reason(rejected) == "candidate_rejected" && Opinion(rejected)["oldReplaceable"].Value<bool>() == false,
            "the original native Replaceable=false remains an independent refusal");
        NoGesture("nonreplaceable original");

        foreach (bool absent in new[] { true, false })
        {
            Setup();
            var other = absent ? null : TileReplacementFixture.Make(TileReplacementFixture.OldDefinition, 404,
                TileReplacementFixture.Cell + 1, "FixtureRock", true);
            TileReplacementFixture.Definition.ReplacementCandidateOverride = cell => other;
            var result = TileReplacementFixture.PlanRead();
            Check(Reason(result) == "candidate_rejected"
                && Opinion(result)["candidatePresent"].Value<bool>() == !absent
                && Opinion(result)["candidateMatchesOriginal"].Value<bool>() == false
                && (absent ? Opinion(result)["candidateId"].Type == JTokenType.Null
                    : Opinion(result)["candidateId"].Value<int>() == 404),
                "absent or foreign native candidate is observed without using it as the requested target");
            NoGesture("wrong native candidate");
        }
        foreach (bool candidateGetter in new[] { false, true })
        {
            Setup();
            TileReplacementFixture.Definition.ThrowCanReplace = !candidateGetter;
            TileReplacementFixture.Definition.ThrowReplacementCandidate = candidateGetter;
            var result = TileReplacementFixture.PlanRead();
            Check(Reason(result) == "observation_unavailable"
                && Opinion(result)["assessmentState"].Value<string>() == "unknown"
                && Opinion(result)["oldReplaceable"].Value<bool>()
                && Opinion(result)["candidatePresent"].Type == JTokenType.Null
                && Opinion(result)["candidateMatchesOriginal"].Type == JTokenType.Null
                && (candidateGetter ? Opinion(result)["canReplace"].Value<bool>()
                    : Opinion(result)["canReplace"].Type == JTokenType.Null),
                "unreadable native reply preserves known facts and nulls rather than reporting native false");
            NoGesture("unavailable native opinion");
        }
    }

    private static void ExactIdentityAndScope()
    {
        foreach (string scope in new[] { "rotation", "width", "height", "slots", "categories", "old_width" })
        {
            Setup(); var args = TileReplacementFixture.Args();
            if (scope == "rotation") args["orientation"] = "R90";
            else if (scope == "width") TileReplacementFixture.Definition.WidthInCells = 2;
            else if (scope == "height") TileReplacementFixture.Definition.HeightInCells = 2;
            else if (scope == "slots") TileReplacementFixture.Definition.Mass = new[] { 100f, 50f };
            else if (scope == "categories") TileReplacementFixture.Definition.MaterialCategory = new[] { "AllMetals", "RawMinerals" };
            else TileReplacementFixture.OldDefinition.WidthInCells = 2;
            var result = TileReplacementFixture.PlanRead(args);
            Check(!result["valid"].Value<bool>() && Reason(result) == "scope_unavailable"
                && result["tileReplacement"]["nativeCandidate"].Type == JTokenType.Null,
                "unsupported " + scope + " does not claim an evaluated native opinion");
            NoGesture("unsupported generalized scope");
        }
        Setup(); TileReplacementFixture.Tool.OnMouseMoveFixture = () =>
        {
            var replacement = TileReplacementFixture.Make(TileReplacementFixture.OldDefinition, 505,
                TileReplacementFixture.Cell, "FixtureRock", true);
            Grid.Objects[TileReplacementFixture.Cell, (int)ObjectLayer.FoundationTile] = replacement;
        };
        var changed = TileReplacementFixture.PlanRead();
        Check(!changed["valid"].Value<bool>() && TileReplacementFixture.Tool.BuildCalls == 0,
            "same-prefab original identity replacement during UI preparation never reaches build");
        Setup(); TileReplacementFixture.Tool.OnMouseMoveFixture = () => TileReplacementFixture.Definition.NativeCanReplace = false;
        changed = TileReplacementFixture.PlanRead();
        Check(!changed["valid"].Value<bool>() && TileReplacementFixture.Tool.BuildCalls == 0
            && Opinion(changed)["canReplace"].Value<bool>() == false,
            "a native eligibility change during preview is re-read before the gesture");
    }

    private static void ObservedFlagsAndTerrainProtection()
    {
        foreach (bool foundation in new[] { false, true })
        {
            Setup("FixturePermeableFloor", "MetalTile", solid: false, foundation: foundation);
            TileReplacementFixture.Tool.OnBuild = cell => TileReplacementFixture.Queue();
            var result = TileReplacementFixture.PlanRead();
            Check(result["valid"].Value<bool>() && result["queueVerified"].Value<bool>()
                && !result["tileReplacement"]["originalCellFlags"]["solid"].Value<bool>()
                && result["tileReplacement"]["originalCellFlags"]["foundation"].Value<bool>() == foundation,
                "native-compatible exact registration can retain observed false flags without assuming all floors are solid");
            Preserved(false, foundation, "observed-flag reservation");
        }
        Setup(solid: true, foundation: false);
        var blocked = TileReplacementFixture.PlanRead(TileReplacementFixture.Args(true));
        Check(!blocked["valid"].Value<bool>() && Reason(blocked) == "terrain_unverifiable"
            && Opinion(blocked)["canReplace"].Value<bool>()
            && Grid.Solid[TileReplacementFixture.Cell] && !Grid.Foundation[TileReplacementFixture.Cell],
            "solid-without-foundation remains terrain-protected even when native replacement tags allow the pair");
        NoGesture("natural-solid provenance protection");
        foreach (bool afterGesture in new[] { false, true })
        {
            Setup(solid: false);
            if (afterGesture) TileReplacementFixture.Tool.OnBuild = cell =>
            { TileReplacementFixture.Queue(); Grid.Solid[cell] = true; };
            else TileReplacementFixture.Tool.OnMouseMoveFixture = () => Grid.Solid[TileReplacementFixture.Cell] = true;
            var result = TileReplacementFixture.PlanRead();
            Check(!result["valid"].Value<bool>() && result["partial"].Value<bool>()
                && result["tileReplacement"]["oldTargetPreserved"].Value<bool>() == false
                && TileReplacementFixture.Tool.BuildCalls == (afterGesture ? 1 : 0),
                "a changed observed raw flag is detected independently of the original object identity and native queue");
        }
    }

    private static void SamePrefabContract()
    {
        foreach (bool differentMaterial in new[] { false, true })
        {
            Setup();
            TileReplacementFixture.Definition = TileReplacementFixture.OldDefinition;
            TileReplacementFixture.Original.GetComponent<PrimaryElement>().ElementID = "Cuprite";
            TileReplacementFixture.Definition.CraftRecipe.Result = new Tag("Tile");
            TileReplacementFixture.Materials.SetFixtureSelectors(TileReplacementFixture.Selectors,
                TileReplacementFixture.Definition.CraftRecipe);
            BuildPlanningTools.FixtureDefs["Tile"] = TileReplacementFixture.Definition;
            var args = TileReplacementFixture.Args(); if (differentMaterial) args["material"] = "GoldAmalgam";
            var result = TileReplacementFixture.PlanRead(args);
            Check(differentMaterial ? !result["valid"].Value<bool>() && Reason(result) == "existing_material_mismatch"
                : result["valid"].Value<bool>() && result["alreadyPresent"].Value<bool>()
                    && !result["planned"].Value<bool>() && BuildPlanningTools.FixtureSelectCalls == 0,
                "same-prefab matching reuse and explicit material mismatch retain the prior contract");
            Check(result["tileReplacement"] == null, "same-prefab request is not recast as an expanded replacement");
            NoGesture("same-prefab contract");
        }
    }
}
