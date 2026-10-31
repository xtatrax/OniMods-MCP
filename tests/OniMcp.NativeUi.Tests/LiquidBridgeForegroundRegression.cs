using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class LiquidBridgeForegroundRegression
{
    private static int checks;
    private static BuildingDef bridge, foregroundDef;
    private static GameObject foreground;
    private static int[] cells;
    private static Orientation placementOrientation;
    private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException("Liquid bridge foreground: "+reason);}
    private static void Setup()
    {
        TileReplacementFixture.Setup();
        placementOrientation = Orientation.R270;
        Components.BuildingCompletes.Items.Clear();BuildPlanningTools.FixtureBlueprints.Clear();NativeBuildingCellIndex.Shapes.Clear();
        BuildPlanningTools.FixtureEndpoints.Clear();BuildPlanningTools.FixtureLiquidBridgePlantCells.Clear();
        for(int cell=0;cell<Grid.Visible.Length;cell++)
        {
            Grid.Solid[cell]=false;Grid.Foundation[cell]=false;
            for(int layer=0;layer<(int)ObjectLayer.NumLayers;layer++)Grid.Objects[cell,layer]=null;
        }
        cells=new[]{Grid.XYToCell(2,3),Grid.XYToCell(2,4),Grid.XYToCell(2,5)};
        bridge=new BuildingDef{PrefabID="LiquidConduitBridge",Name="fixture liquid bridge",BuildLocationRule=BuildLocationRule.Conduit,
            ObjectLayer=ObjectLayer.LiquidConduitConnection,TileLayer=ObjectLayer.NumLayers,ReplacementLayer=ObjectLayer.NumLayers,
            WidthInCells=3,HeightInCells=1,PlacementOffsets=new[]{new CellOffset{x=-1},new CellOffset(),new CellOffset{x=1}},
            InputConduitType=ConduitType.Liquid,OutputConduitType=ConduitType.Liquid,UtilityInputOffset=new CellOffset{x=-1},
            UtilityOutputOffset=new CellOffset{x=1},PermittedRotations=PermittedRotations.R360,BuildingComplete=new GameObject()};
        foregroundDef=new BuildingDef{PrefabID="LiquidReservoir",Name="fixture reservoir",BuildLocationRule=BuildLocationRule.OnFloor,
            ObjectLayer=ObjectLayer.Building,TileLayer=ObjectLayer.NumLayers,ReplacementLayer=ObjectLayer.NumLayers,
            WidthInCells=2,HeightInCells=3,BuildingComplete=new GameObject()};
        foreground=TileReplacementFixture.Make(foregroundDef,601,Grid.XYToCell(2,2),"Granite",true);
        var shape=NativeBuildingCellIndex.Shapes[foreground];shape.Bounds=new[]{2,2,3,4};shape.Cells.Clear();
        for(int x=2;x<=3;x++)for(int y=2;y<=4;y++)
        {int cell=Grid.XYToCell(x,y);shape.Cells.Add(new BuildingGeometryCell{Cell=cell,X=x,Y=y});Grid.Objects[cell,(int)ObjectLayer.Building]=foreground;}
        Grid.Objects[Grid.XYToCell(2,2),(int)ObjectLayer.LiquidConduitConnection]=foreground;
        Grid.Objects[Grid.XYToCell(3,4),(int)ObjectLayer.LiquidConduitConnection]=foreground;
        BuildPlanningTools.FixtureEndpoints.Add(Tuple.Create(cells[0],ObjectLayer.LiquidConduitConnection));
        BuildPlanningTools.FixtureEndpoints.Add(Tuple.Create(cells[2],ObjectLayer.LiquidConduitConnection));
    }
    private static JObject Read(bool geometry=true)
    {BuildPlanningTools.FixtureDefs[bridge.PrefabID]=bridge;return BuildPlanningTools.BridgeClassifierFootprintForTest(bridge,cells,geometry,placementOrientation);}
    private static bool Has(JObject value,string kind)
    {return value["obstructions"].Any(item=>item["kind"]?.Value<string>()==kind);}
    private static void NoMutation()
    {Check(bridge.TryPlaceCalls==0&&bridge.DirectBuildCalls==0&&TileReplacementFixture.Tool.BuildCalls==0
        &&TileReplacementFixture.Plan.CopyCalls==0&&foreground.DeleteCalls==0,"classification is passive; no native UI, placement, deletion or fallback");}
    internal static void PositiveOnly()
    {
        Setup();var result=Read();
        Check(result["valid"].Value<bool>()&&!result["obstructions"].Any(),
            "liquid_foreground_contract: canonical native3cell liquid bridge may cross known completed foreground with empty own endpoint layers");
        NoMutation();
    }
    // Canonical metadata and literal endpoint order come from the pinned native
    // LadderConfig/CreateLadderDef and Rotatable static inspection. Geometry and
    // registration remain supplied fixture facts; no native game DLL is loaded.
    private static void SetupLadder(Orientation orientation, int position, bool completed = true)
    {
        Setup();
        Components.BuildingCompletes.Items.Clear();
        NativeBuildingCellIndex.Shapes.Clear();
        for (int cell = 0; cell < Grid.Visible.Length; cell++)
            for (int layer = 0; layer < (int)ObjectLayer.NumLayers; layer++)
                Grid.Objects[cell, layer] = null;
        placementOrientation = orientation;
        if (orientation == Orientation.Neutral)
            cells = new[] { Grid.XYToCell(2, 3), Grid.XYToCell(3, 3), Grid.XYToCell(4, 3) };
        else if (orientation == Orientation.R90)
            cells = new[] { Grid.XYToCell(3, 4), Grid.XYToCell(3, 3), Grid.XYToCell(3, 2) };
        else if (orientation == Orientation.R180)
            cells = new[] { Grid.XYToCell(4, 3), Grid.XYToCell(3, 3), Grid.XYToCell(2, 3) };
        else if (orientation == Orientation.R270)
            cells = new[] { Grid.XYToCell(3, 2), Grid.XYToCell(3, 3), Grid.XYToCell(3, 4) };
        else
            throw new ArgumentOutOfRangeException("orientation");
        BuildPlanningTools.FixtureEndpoints.Clear();
        BuildPlanningTools.FixtureEndpoints.Add(Tuple.Create(cells[0], ObjectLayer.LiquidConduitConnection));
        BuildPlanningTools.FixtureEndpoints.Add(Tuple.Create(cells[2], ObjectLayer.LiquidConduitConnection));
        foregroundDef = new BuildingDef
        {
            PrefabID = "Ladder", Name = "fixture native ladder", BuildLocationRule = BuildLocationRule.Anywhere,
            ObjectLayer = ObjectLayer.Building, TileLayer = ObjectLayer.LadderTile,
            ReplacementLayer = ObjectLayer.ReplacementLadder, IsTilePiece = true, DragBuild = true,
            WidthInCells = 1, HeightInCells = 1, PlacementOffsets = new[] { new CellOffset() },
            PermittedRotations = PermittedRotations.Unrotatable, BuildingComplete = new GameObject()
        };
        foreground = TileReplacementFixture.Make(foregroundDef, 610, cells[position], "SandStone", completed);
        Grid.Objects[cells[position], (int)ObjectLayer.Building] = foreground;
        Grid.Objects[cells[position], (int)(completed ? ObjectLayer.LadderTile : ObjectLayer.ReplacementLadder)] = foreground;
    }

    private static JObject ReadLadder(bool geometry = true)
    {
        var objects = Grid.ObjectLayers.Select(layer => layer.OrderBy(entry => entry.Key).ToArray()).ToArray();
        var solid = (bool[])Grid.Solid.Clone();
        var foundation = (bool[])Grid.Foundation.Clone();
        var completes = Components.BuildingCompletes.Items.ToArray();
        var blueprints = BuildPlanningTools.FixtureBlueprints.ToArray();
        float mass = foreground.GetComponent<PrimaryElement>().Mass;
        var result = Read(geometry);
        Check(Grid.ObjectLayers.Select((layer, index) => layer.OrderBy(entry => entry.Key).SequenceEqual(objects[index])).All(same => same)
            && Grid.Solid.SequenceEqual(solid) && Grid.Foundation.SequenceEqual(foundation)
            && Components.BuildingCompletes.Items.SequenceEqual(completes)
            && BuildPlanningTools.FixtureBlueprints.SequenceEqual(blueprints)
            && foreground.GetComponent<PrimaryElement>().Mass == mass,
            "ladder classification preserves exact layer entries, terrain, registries and material mass");
        NoMutation();
        return result;
    }

    internal static void LadderPositiveOnly()
    {
        SetupLadder(Orientation.Neutral, 1);
        var result = ReadLadder();
        Check(result["valid"].Value<bool>() && !result["obstructions"].Any(),
            "liquid_ladder_contract: canonical completed ladder may share standard bridge footprint");
    }

    private static void LadderRegression()
    {
        LadderPositiveOnly();
        var orientations = new[] { Orientation.Neutral, Orientation.R90, Orientation.R180, Orientation.R270 };
        foreach (var orientation in orientations)
        {
            for (int position = 0; position < 3; position++)
            {
                SetupLadder(orientation, position);
                var result = ReadLadder();
                Check(result["valid"].Value<bool>() && !result["obstructions"].Any(),
                    "completed ladder shares bridge input, middle and output in each orientation: " + orientation + "/" + position);
            }
            foreach (int endpoint in new[] { 0, 2 })
            {
                SetupLadder(orientation, 1);
                Grid.Objects[cells[endpoint], (int)ObjectLayer.LiquidConduitConnection] = foreground;
                var result = ReadLadder();
                Check(!result["valid"].Value<bool>() && Has(result, "bridge_endpoint_conflict"),
                    "liquid endpoint occupancy remains blocked beside ladder: " + orientation + "/" + endpoint);
            }
        }
        SetupLadder(Orientation.Neutral, 1);
        var own = TileReplacementFixture.Make(bridge, 611, cells[0], "SandStone", true);
        Grid.Objects[cells[0], (int)ObjectLayer.LiquidConduitConnection] = own;
        var same = ReadLadder();
        Check(!same["valid"].Value<bool>() && same["obstructions"].Any(item =>
            item["kind"]?.Value<string>() == "bridge_endpoint_conflict" && item["samePrefab"]?.Value<bool>() == true),
            "same-prefab bridge endpoint remains blocked beside ladder");
        SetupLadder(Orientation.Neutral, 1, false);
        var planned = ReadLadder();
        Check(!planned["valid"].Value<bool>() && Has(planned, "blueprint"),
            "ladder blueprint does not receive completed-object exemption");
        foreach (string mutation in new[] { "prefab", "object_layer", "tile_layer", "replacement_layer", "tile_piece",
            "drag", "location", "rotation", "width", "height", "offset", "missing_offsets", "prefab_missing",
            "definition_missing", "complete_definition", "constructable", "building_registration", "ladder_registration",
            "different_ladder_registration", "geometry_missing", "origin_missing", "origin_mismatch", "extra_geometry" })
        {
            SetupLadder(Orientation.Neutral, 1);
            if (mutation == "prefab") foregroundDef.PrefabID = "RenamedLadder";
            else if (mutation == "object_layer") foregroundDef.ObjectLayer = ObjectLayer.Backwall;
            else if (mutation == "tile_layer") foregroundDef.TileLayer = ObjectLayer.FoundationTile;
            else if (mutation == "replacement_layer") foregroundDef.ReplacementLayer = ObjectLayer.ReplacementTile;
            else if (mutation == "tile_piece") foregroundDef.IsTilePiece = false;
            else if (mutation == "drag") foregroundDef.DragBuild = false;
            else if (mutation == "location") foregroundDef.BuildLocationRule = BuildLocationRule.OnFloor;
            else if (mutation == "rotation") foregroundDef.PermittedRotations = PermittedRotations.R360;
            else if (mutation == "width") foregroundDef.WidthInCells = 2;
            else if (mutation == "height") foregroundDef.HeightInCells = 2;
            else if (mutation == "offset") foregroundDef.PlacementOffsets[0] = new CellOffset { x = 1 };
            else if (mutation == "missing_offsets") foregroundDef.PlacementOffsets = null;
            else if (mutation == "prefab_missing") foregroundDef.BuildingComplete = null;
            else if (mutation == "definition_missing") foreground.GetComponent<Building>().Def = null;
            else if (mutation == "complete_definition") foreground.GetComponent<BuildingComplete>().Def = bridge;
            else if (mutation == "constructable") foreground.Add(new Constructable());
            else if (mutation == "building_registration") Grid.Objects[cells[1], (int)ObjectLayer.Building] = null;
            else if (mutation == "ladder_registration") Grid.Objects[cells[1], (int)ObjectLayer.LadderTile] = null;
            else if (mutation == "different_ladder_registration") Grid.Objects[cells[1], (int)ObjectLayer.LadderTile] = new GameObject();
            else if (mutation == "geometry_missing") NativeBuildingCellIndex.Shapes[foreground].Available = false;
            else if (mutation == "origin_missing") NativeBuildingCellIndex.Shapes[foreground].Origin = null;
            else if (mutation == "origin_mismatch") NativeBuildingCellIndex.Shapes[foreground].Origin.Cell = cells[0];
            else NativeBuildingCellIndex.Shapes[foreground].Cells.Add(new BuildingGeometryCell { Cell = cells[0] });
            if (mutation == "origin_missing")
            {
                // Native factories cannot emit Available=true with a null Origin.
                // Exercise the actual classifier on that malformed raw boundary,
                // then use the factory-consistent unavailable state for fallback.
                Check(!BuildPlanningTools.BridgeClassifierFootprintForTest(bridge, cells, true,
                    placementOrientation, foreground)["canShare"].Value<bool>(),
                    "actual classifier rejects a missing native origin without inferring a shape");
                NativeBuildingCellIndex.Shapes[foreground].Available = false;
            }
            Check(!ReadLadder()["valid"].Value<bool>(), "unknown or altered ladder stays conservative: " + mutation);
        }
        foreach (string kind in new[] { "GasConduitBridge", "WireBridge", "SolidConduitBridge", "RenamedLiquidBridge" })
        {
            SetupLadder(Orientation.Neutral, 1);
            bridge.PrefabID = kind;
            Check(!ReadLadder()["valid"].Value<bool>(), "ladder exemption does not extend to other requested bridge kind: " + kind);
        }
        SetupLadder(Orientation.Neutral, 1);
        Check(!ReadLadder(false)["valid"].Value<bool>(), "unknown requested geometry cannot inherit ladder exemption");
    }

    internal static void Run()
    {
        PositiveOnly();
        LadderRegression();
        try
        {
            foreach(int endpoint in new[]{0,2})
            {
                Setup();Grid.Objects[cells[endpoint],(int)ObjectLayer.LiquidConduitConnection]=foreground;var value=Read();
                Check(!value["valid"].Value<bool>()&&Has(value,"bridge_endpoint_conflict"),"the same liquid endpoint layer remains blocked at both ends");NoMutation();
            }
            Setup();var own=TileReplacementFixture.Make(bridge,602,cells[0],"Granite",true);
            Grid.Objects[cells[0],(int)ObjectLayer.LiquidConduitConnection]=own;var same=Read();
            Check(!same["valid"].Value<bool>()&&same["obstructions"].Any(item=>item["kind"]?.Value<string>()=="bridge_endpoint_conflict"&&item["samePrefab"]?.Value<bool>()==true),"own-bridge endpoint registration is never ignored");
            Setup();TileReplacementFixture.Make(foregroundDef,603,cells[1],"Granite",false);var queued=Read();
            Check(!queued["valid"].Value<bool>()&&Has(queued,"blueprint"),"pending foreground blueprints retain the existing obstruction rule");
            foreach(string kind in new[]{"GasConduitBridge","WireBridge","SolidConduitBridge","RenamedLiquidBridge"})
            {Setup();bridge.PrefabID=kind;Check(!Read()["valid"].Value<bool>(),"other/unknown bridge kind is conservative: "+kind);}
            foreach(string mutation in new[]{"width","offset","layer","replacement","power","logic"})
            {
                Setup();if(mutation=="width")bridge.WidthInCells=4;
                else if(mutation=="offset")bridge.UtilityInputOffset=new CellOffset();
                else if(mutation=="layer")bridge.ObjectLayer=ObjectLayer.Building;
                else if(mutation=="replacement")bridge.ReplacementLayer=ObjectLayer.ReplacementTile;
                else if(mutation=="power")bridge.RequiresPowerInput=true;
                else bridge.BuildingComplete.Add(new LogicPorts());
                Check(!Read()["valid"].Value<bool>(),"nonstandard canonical metadata cannot share foreground: "+mutation);
            }
            foreach(string mutation in new[]{"terrain","plant","hidden","world","foreground_tile","foreground_unknown","foreground_geometry"})
            {
                Setup();if(mutation=="terrain")Grid.Solid[cells[1]]=true;
                else if(mutation=="plant")BuildPlanningTools.FixtureLiquidBridgePlantCells.Add(cells[1]);
                else if(mutation=="hidden")Grid.Visible[cells[1]]=0;
                else if(mutation=="world")Grid.WorldIdx[cells[1]]=1;
                else if(mutation=="foreground_tile")foregroundDef.TileLayer=ObjectLayer.FoundationTile;
                else if(mutation=="foreground_unknown")foreground.GetComponent<Building>().Def=null;
                else NativeBuildingCellIndex.Shapes[foreground].Available=false;
                var value=Read();Check(!value["valid"].Value<bool>(),"existing safety or unknown foreground remains conservative: "+mutation);
                if(mutation=="terrain")Check(Has(value,"solid_cell"),"terrain obstruction retained");
                if(mutation=="plant")Check(Has(value,"uprootable"),"plant obstruction retained");
                NoMutation();
            }
            Setup();Check(!Read(false)["valid"].Value<bool>(),"unknown native request geometry is never converted to a shareable shape");
        }
        finally{BuildPlanningTools.FixtureLiquidBridgePlantCells.Clear();}
        Console.WriteLine("Liquid bridge foreground: "+checks+" actual-source host checks passed (native geometry/layers supplied as fixture facts)");
    }
}
namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        internal static readonly HashSet<int> FixtureLiquidBridgePlantCells=new HashSet<int>();
        internal static IEnumerable<Dictionary<string,object>> LiquidBridgePlantsForFixture(int cell)
        {return FixtureLiquidBridgePlantCells.Contains(cell)?new[]{new Dictionary<string,object>{["kind"]="uprootable",["canUproot"]=true}}:Enumerable.Empty<Dictionary<string,object>>();}
        internal static JObject BridgeClassifierFootprintForTest(BuildingDef def,int[] cells,bool geometryAvailable,Orientation orientation=Orientation.R270,GameObject classifierForeground=null)
        {
            int origin=cells[1];
            var placement=new PlacementDetails{PrefabId=def.PrefabID,AnchorX=cells.Min(cell=>Grid.CellColumn(cell)),AnchorY=cells.Min(cell=>Grid.CellRow(cell)),
                OriginX=Grid.CellColumn(origin),OriginY=Grid.CellRow(origin),OriginCell=origin,WorldId=0,Orientation=orientation,
                GeometryAvailable=geometryAvailable,GeometryError=geometryAvailable?null:"fixture_geometry_missing",FillsBounds=geometryAvailable,
                Footprint=cells.Select(cell=>new FootprintCell{Cell=cell,X=Grid.CellColumn(cell),Y=Grid.CellRow(cell),WorldId=0,
                    Valid=Grid.IsValidCell(cell),Visible=Grid.IsVisible(cell),InWorld=ToolUtil.CellMatchesWorld(cell,0)}).ToList()};
            if (classifierForeground != null)
                return new JObject { ["canShare"] = CanLiquidBridgeShareCompletedForeground(def, placement, classifierForeground) };
            return JObject.FromObject(ValidateFootprint(placement).ToDictionary(placement));
        }
    }
}
