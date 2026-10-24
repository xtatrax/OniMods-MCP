using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

// The collection/classification/footprint/parent bodies come from production.
// Native geometry, support, material selection and UI inputs remain deliberate
// fixture boundaries. These checks do not establish native UI build eligibility.
internal static class BackgroundWallRegression
{
    private static int checks;
    private static BuildingDef target;
    private static int cell;

    private static void Check(bool value, string reason)
    {
        checks++;
        if (!value) throw new InvalidOperationException("Background wall: " + reason);
    }

    private static void Setup()
    {
        BlueprintCompletionRegression.Setup();
        BuildPlanningTools.ResetPlannerFixture();
        UnityEngine.Object.ThrowEnumeration = false;
        DebugHandler.InstantBuildMode = false;
        BuildingControlTools.IsVirtualFileEditContext = false;
        Db.Seed(null); Db.GetCalls = 0; Research.Instance = null;
        cell = BlueprintCompletionRegression.Cell;
        // Do not change the Tile replacement fixtures' FoundationTile default.
        target = new BuildingDef {
            PrefabID = "FixtureForeground", Name = "Fixture foreground",
            ObjectLayer = ObjectLayer.Building, TileLayer = ObjectLayer.NumLayers,
            BuildLocationRule = BuildLocationRule.OnFloor,
            ReplacementLayer = ObjectLayer.NumLayers,
            EquivalentReplacementLayers = new List<ObjectLayer>(),
            Replaceable = false, IsFoundation = false
        };
        BuildPlanningTools.FixtureDefs[target.PrefabID] = target;
    }

    private static BuildingDef BackgroundDefinition()
    {
        return new BuildingDef {
            PrefabID = "FixtureBackground", Name = "Fixture background",
            ObjectLayer = ObjectLayer.Backwall, TileLayer = ObjectLayer.NumLayers,
            BuildLocationRule = BuildLocationRule.Anywhere,
            ReplacementLayer = ObjectLayer.NumLayers,
            Replaceable = false, IsFoundation = false
        };
    }

    private static GameObject Place(BuildingDef definition, bool completed, int id = 701, int? at = null)
    {
        return TileReplacementFixture.Make(definition, id, at ?? cell, "FixtureRock", completed);
    }

    private static JObject Footprint(BuildingDef definition = null, int[] occupied = null,
        bool geometryAvailable = true, GameObject ignored = null)
    {
        return BuildPlanningTools.BackwallFootprintForTest(definition ?? target,
            occupied ?? new[] { cell }, geometryAvailable, ignored);
    }

    private static bool Has(JObject result, string reasonCode)
    {
        return result["obstructions"].Any(item => (string)item["reasonCode"] == reasonCode);
    }

    private static void NoWrites(string context)
    {
        Check(target.DirectBuildCalls == 0 && target.TryPlaceCalls == 0
            && BlueprintCompletionRegression.Tool.BuildCalls == 0
            && BlueprintCompletionRegression.Tool.Activations == 0,
            context + ": no native UI gesture or direct build");
        Check(Components.BuildingCompletes.Items.All(item => !item.gameObject.Destroyed
            && item.gameObject.DeleteCalls == 0), context + ": completed occupants retained");
    }

    internal static void Run()
    {
        CompletedBackgroundCoexists();
        RetainedObstructions();
        MissingMetadataAndGeometry();
        ExistingGuardsAndParent();
        ActualAvailabilityReaderInputs();
        Console.WriteLine("Background wall: " + checks
            + " host checks passed (actual production slices/parent; native inputs substituted)");
    }

    private static void CompletedBackgroundCoexists()
    {
        // Keep this positive case first: the exact prechange Obstructions source
        // must compile and fail here, not fail only a grep or ABI/compile check.
        Setup(); var wall = Place(BackgroundDefinition(), true);
        var result = Footprint();
        Check((bool)result["valid"] && result["obstructions"].Count() == 0,
            "ordinary Building / NumLayers / OnFloor must coexist with completed Backwall / NumLayers");
        Check(wall.GetComponent<Building>().Def.ObjectLayer == ObjectLayer.Backwall,
            "fixture has the supplied native Backwall metadata");
        NoWrites("completed background co-location");

        Setup(); wall = Place(BackgroundDefinition(), true);
        var shape = NativeBuildingCellIndex.Shapes[wall];
        shape.Cells.Add(new BuildingGeometryCell {
            X = Grid.CellColumn(cell + 1), Y = Grid.CellRow(cell + 1), Cell = cell + 1 });
        shape.Bounds[2]++;
        shape.Orientation = Orientation.R90;
        wall.GetComponent<Rotatable>().Orientation = Orientation.R90;
        result = Footprint(occupied: new[] { cell, cell + 1 });
        Check((bool)result["valid"] && result["obstructions"].Count() == 0,
            "compatible completed background remains nonblocking across native supplied rotated occupied cells");
        NoWrites("multi-cell fixture geometry");
    }

    private static void RetainedObstructions()
    {
        Setup(); var foreground = new BuildingDef {
            PrefabID = "FixtureOtherForeground", ObjectLayer = ObjectLayer.Building,
            TileLayer = ObjectLayer.NumLayers, BuildLocationRule = BuildLocationRule.OnFloor };
        Place(foreground, true);
        Check(Has(Footprint(), "occupied_by_building"), "same-layer completed foreground remains blocked");
        Setup(); Place(target, false);
        Check(Has(Footprint(), "occupied_by_blueprint"), "same-layer foreground blueprint remains blocked");
        Setup(); Place(BackgroundDefinition(), false);
        Check(Has(Footprint(), "occupied_by_blueprint"), "background blueprint stays conservatively blocked");
        Setup(); target.ObjectLayer = ObjectLayer.Backwall; Place(BackgroundDefinition(), true);
        Check(Has(Footprint(), "occupied_by_building"), "Backwall target never uses foreground exemption");
        foreach (var layer in new[] { ObjectLayer.FoundationTile, ObjectLayer.Building, (ObjectLayer)999 })
        {
            Setup(); target.TileLayer = layer; Place(BackgroundDefinition(), true);
            Check(Has(Footprint(), "occupied_by_building"), "target auxiliary or unknown TileLayer remains blocked");
            Setup(); var wallDef = BackgroundDefinition(); wallDef.TileLayer = layer; Place(wallDef, true);
            Check(Has(Footprint(), "occupied_by_building"), "background auxiliary or unknown TileLayer remains blocked");
        }
        foreach (var layer in new[] { ObjectLayer.Other, (ObjectLayer)999 })
        {
            Setup(); target.ObjectLayer = layer; Place(BackgroundDefinition(), true);
            Check(Has(Footprint(), "occupied_by_building"), "unknown or nonstandard target object layer remains blocked");
            Setup(); var existingDef = BackgroundDefinition(); existingDef.ObjectLayer = layer; Place(existingDef, true);
            Check(Has(Footprint(), "occupied_by_building"), "unknown or nonbackground existing object layer remains blocked");
        }
        foreach (var rule in new[] { BuildLocationRule.Other, BuildLocationRule.Anywhere,
            BuildLocationRule.Tile, BuildLocationRule.Conduit, BuildLocationRule.WireBridge,
            BuildLocationRule.LogicBridge, (BuildLocationRule)999 })
        {
            Setup(); target.BuildLocationRule = rule; Place(BackgroundDefinition(), true);
            Check(Has(Footprint(), "occupied_by_building"), "non-OnFloor rule retains original XY obstruction");
        }
        Setup(); Place(BackgroundDefinition(), true); Place(foreground, true, 702);
        var result = Footprint();
        Check(Has(result, "occupied_by_building") && result["obstructions"].Count() == 1
            && (string)result["obstructions"][0]["id"] == foreground.PrefabID,
            "excluding background cannot exclude a simultaneous foreground occupant");
        Setup(); var own = Place(foreground, true); Place(foreground, true, 702);
        result = Footprint(ignored: own);
        Check(result["obstructions"].Count() == 1 && Has(result, "occupied_by_building"),
            "exact ignored identity excludes self only, not a second same-prefab object");
        NoWrites("retained complete collision");
    }

    private static void MissingMetadataAndGeometry()
    {
        Setup(); Place(BackgroundDefinition(), true); BuildPlanningTools.FixtureDefs.Clear();
        Check(Has(Footprint(), "occupied_by_building"), "unresolved target metadata retains old XY obstruction");
        Setup(); var wall = Place(BackgroundDefinition(), true);
        wall.GetComponent<BuildingComplete>().Def = null;
        wall.GetComponent<Building>().Def = null;
        Check(Has(Footprint(), "occupied_by_building"),
            "missing completed definition with available supplied shape cannot establish compatibility");
        Setup(); Place(BackgroundDefinition(), true);
        var result = Footprint(geometryAvailable: false);
        Check(!(bool)result["valid"] && (string)result["invalidCells"][0]["reasonCode"] == "geometry_unavailable",
            "target geometry unavailable retains existing explicit invalid result");
        Setup(); wall = Place(BackgroundDefinition(), true);
        NativeBuildingCellIndex.Shapes[wall].Available = false;
        target.BuildLocationRule = BuildLocationRule.Other;
        result = Footprint();
        Check(result["obstructions"].Count() == 0,
            "unavailable existing shape was already omitted by original geometry enumerator; this is unchanged, not improved rejection");
        Setup(); wall = Place(BackgroundDefinition(), true, at: cell + 1);
        target.BuildLocationRule = BuildLocationRule.Other;
        Check(Footprint()["obstructions"].Count() == 0,
            "nonintersecting native occupied cells keep original exclusion");
        NoWrites("metadata/geometry boundaries");
    }

    private static JObject ParentPreview()
    {
        return BuildPlanningTools.PlanForTest(target.PrefabID, cell,
            new JObject { ["worldId"] = 0, ["dryRun"] = true, ["confirm"] = false,
                ["material"] = "Cuprite", ["autoDigObstructions"] = false,
                ["autoUprootObstructions"] = false, ["autoWire"] = false });
    }

    private static void ExistingGuardsAndParent()
    {
        Setup(); Place(BackgroundDefinition(), true); Grid.Solid[cell] = true;
        Check(Has(Footprint(), "solid_cell"), "compatible background never hides solid terrain");
        Setup(); Place(BackgroundDefinition(), true);
        var other = new GameObject { Cell = cell, Id = 802 };
        other.Add(new KPrefabID { PrefabTag = new Tag("FixtureOtherForeground") });
        Grid.Objects[cell, (int)ObjectLayer.Building] = other;
        Check(Has(Footprint(), "building_layer_occupied_by_different_prefab"),
            "actual Building grid collision is still checked independently");
        Setup(); Place(BackgroundDefinition(), true); Grid.Visible[cell] = 0;
        Check(!(bool)Footprint()["valid"], "invisible target remains invalid");
        Setup(); Place(BackgroundDefinition(), true); Grid.WorldIdx[cell] = 1;
        Check(!(bool)Footprint()["valid"], "target outside requested world remains invalid");
        Setup(); Place(BackgroundDefinition(), true); BuildPlanningTools.FixtureSupportValid = false;
        var result = ParentPreview();
        Check(!(bool)result["valid"] && (string)result["details"]["reasonCode"] == "unsupported",
            "actual parent retains support collaborator refusal before co-location acceptance");
        NoWrites("parent support refusal");
        Setup(); Place(BackgroundDefinition(), true); result = ParentPreview();
        Check((bool)result["valid"] && !(bool)result["planned"] && !(bool)result["blueprintPlaced"],
            "actual parent read-only preview accepts compatible background without claiming construction");
        NoWrites("parent preview");
    }

    private static void ActualAvailabilityReaderInputs()
    {
        Setup(); var observation = BuildingAvailabilityReader.Read(target);
        Check(observation.Value == null && observation.ReasonCode == "database_cache_missing"
            && target.FixtureAvailabilityReads == 1 && Db.GetCalls == 0,
            "actual availability reader retains unknown missing research cache without initialization");
        target.FixtureNativeAvailable = false;
        observation = BuildingAvailabilityReader.Read(target);
        Check(observation.Value == false && observation.ReasonCode == "definition_unavailable",
            "actual reader preserves supplied native definition unavailability");
        Setup(); var db = new Db(); Db.Seed(db); Research.Instance = new Research();
        db.TechItems.Complete = false; observation = BuildingAvailabilityReader.Read(target);
        Check(observation.Value == false && observation.ReasonCode == "research_locked"
            && db.TechItems.Reads == 1 && db.TechItems.LastPrefabId == target.PrefabID,
            "actual reader preserves supplied cached native research denial");
        db.TechItems.Complete = true; observation = BuildingAvailabilityReader.Read(target);
        Check(observation.Value == true && db.TechItems.Reads == 2 && Db.GetCalls == 0,
            "actual reader combines supplied available/unlocked states through existing native cache");
        Db.Seed(null); Research.Instance = null;
    }
}

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        internal static JObject BackwallFootprintForTest(BuildingDef def, int[] cells,
            bool geometryAvailable, GameObject ignored)
        {
            int origin = cells[0];
            var placement = new PlacementDetails {
                PrefabId = def == null ? "FixtureMissingDefinition" : def.PrefabID,
                AnchorX = Grid.CellColumn(origin), AnchorY = Grid.CellRow(origin),
                OriginX = Grid.CellColumn(origin), OriginY = Grid.CellRow(origin),
                OriginCell = origin, WorldId = 0, GeometryAvailable = geometryAvailable,
                GeometryError = geometryAvailable ? null : "fixture_geometry_missing",
                Footprint = cells.Select(value => new FootprintCell {
                    Cell = value, X = Grid.CellColumn(value), Y = Grid.CellRow(value), WorldId = 0,
                    Valid = Grid.IsValidCell(value), Visible = Grid.IsVisible(value),
                    InWorld = ToolUtil.CellMatchesWorld(value, 0)
                }).ToList()
            };
            return JObject.FromObject(ValidateFootprint(placement, ignored).ToDictionary(placement));
        }
    }
}
