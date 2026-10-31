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
    private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException("Liquid bridge foreground: "+reason);}
    private static void Setup()
    {
        TileReplacementFixture.Setup();
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
    {BuildPlanningTools.FixtureDefs[bridge.PrefabID]=bridge;return BuildPlanningTools.BridgeClassifierFootprintForTest(bridge,cells,geometry);}
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
    internal static void Run()
    {
        PositiveOnly();
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
        internal static JObject BridgeClassifierFootprintForTest(BuildingDef def,int[] cells,bool geometryAvailable)
        {
            int origin=cells[1];
            var placement=new PlacementDetails{PrefabId=def.PrefabID,AnchorX=Grid.CellColumn(cells[0]),AnchorY=Grid.CellRow(cells[0]),
                OriginX=Grid.CellColumn(origin),OriginY=Grid.CellRow(origin),OriginCell=origin,WorldId=0,Orientation=Orientation.R270,
                GeometryAvailable=geometryAvailable,GeometryError=geometryAvailable?null:"fixture_geometry_missing",FillsBounds=geometryAvailable,
                Footprint=cells.Select(cell=>new FootprintCell{Cell=cell,X=Grid.CellColumn(cell),Y=Grid.CellRow(cell),WorldId=0,
                    Valid=Grid.IsValidCell(cell),Visible=Grid.IsVisible(cell),InWorld=ToolUtil.CellMatchesWorld(cell,0)}).ToList()};
            return JObject.FromObject(ValidateFootprint(placement).ToDictionary(placement));
        }
    }
}
