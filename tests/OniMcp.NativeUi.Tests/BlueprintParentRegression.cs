using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class BlueprintParentRegression
{
    private static GameObject blueprint;
    private static Constructable constructable;
    private static BuildingDef Def {get{return BlueprintCompletionRegression.Definition;}}
    private static BuildTool Tool {get{return BlueprintCompletionRegression.Tool;}}
    private static int Cell {get{return BlueprintCompletionRegression.Cell;}}
    private static void Check(bool value,string reason){BlueprintCompletionRegression.Check(value,reason);}
    private static JObject Args(){return new JObject{["confirm"]=true,["instantBuild"]=true};}
    private static JObject Read(JObject args=null)
    {var result=BuildPlanningTools.CompletionForTest(Def,Cell,0,Orientation.R90,args??Args());return result==null?null:JObject.FromObject(result);}
    internal static void VerifyNoDirectFallbackOnly()
    {
        Setup();try{BuildPlanningTools.CompletionForTest(Def,Cell,0,Orientation.R90,Args());}catch(Exception){}
        Check(!blueprint.Destroyed&&blueprint.DeleteCalls==0&&Def.DirectBuildCalls==0,
            "existing blueprint path must keep the native no-op original without direct delete or direct build even when a legacy result fails");
    }
    internal static void Run()
    {TestExactExclusion();TestParentNoOpAndGuards();TestNativeCompletionObservation();}
    private static void Setup(bool hasFacade=true)
    {
        BlueprintCompletionRegression.Setup();
        blueprint=MakeObject(Def,201,Cell,Orientation.R90,hasFacade);
        constructable=new Constructable();constructable.SelectedElementsTags.Add(new Tag("FixtureRock"));blueprint.Add(constructable);
        BuildPlanningTools.FixtureBlueprints.Add(constructable);Grid.Objects[Cell,(int)ObjectLayer.Building]=blueprint;
    }
    private static GameObject MakeObject(BuildingDef def,int id,int cell,Orientation orientation,bool facade=true)
    {
        var go=new GameObject{Cell=cell,Id=id,name="Fixture object"};
        go.Add(new Building{Def=def});go.Add(new KPrefabID{InstanceID=id,PrefabTag=new Tag(def.PrefabID)});
        go.Add(new Rotatable{Orientation=orientation});
        if(facade)go.Add(new BuildingFacade{CurrentFacade="FixtureFacade"});
        NativeBuildingCellIndex.Shapes[go]=new FixtureGeometry{Origin=new BuildingGeometryCell{X=Grid.CellColumn(cell),Y=Grid.CellRow(cell),Cell=cell},
            Bounds=new[]{Grid.CellColumn(cell),Grid.CellRow(cell),Grid.CellColumn(cell),Grid.CellRow(cell)},Orientation=orientation,
            Cells=new List<BuildingGeometryCell>{new BuildingGeometryCell{X=Grid.CellColumn(cell),Y=Grid.CellRow(cell),Cell=cell}}};
        return go;
    }
    private static void NoDirectMutation(string label)
    {Check(blueprint.DeleteCalls==0&&Def.DirectBuildCalls==0,label+": adapter/caller must never delete or directly build");}
    private static void TestExactExclusion()
    {
        Setup();var own=BuildPlanningTools.ObstructionsForTest(Def,Cell,blueprint);
        Check(own.Count==0,"existing Tile blueprint is excluded from its own footprint only by exact identity");
        var other=MakeObject(Def,202,Cell,Orientation.R90);var otherConstructable=new Constructable();other.Add(otherConstructable);
        BuildPlanningTools.FixtureBlueprints.Add(otherConstructable);
        Check(BuildPlanningTools.ObstructionsForTest(Def,Cell,blueprint).Count==1,
            "different blueprint with the same prefab and occupied cell remains an obstruction");
        Setup();var building=MakeObject(Def,203,Cell,Orientation.R90);var complete=new BuildingComplete{Def=Def};building.Add(complete);
        Components.BuildingCompletes.Items.Add(complete);
        Check(BuildPlanningTools.ObstructionsForTest(Def,Cell,blueprint).Count==1,
            "different completed object with the same prefab remains an obstruction");
        Setup();Def.PrefabID="FixtureBridge";Def.BuildLocationRule=BuildLocationRule.Conduit;
        BuildPlanningTools.FixtureDefs.Clear();BuildPlanningTools.FixtureDefs[Def.PrefabID]=Def;
        BuildPlanningTools.FixtureEndpoints.Add(Tuple.Create(Cell,ObjectLayer.WireConnectors));
        Grid.Objects[Cell,(int)ObjectLayer.WireConnectors]=blueprint;
        Check(BuildPlanningTools.ObstructionsForTest(Def,Cell,blueprint).Count==0,"ignored blueprint owns its exact endpoint without blocking itself");
        var endpoint=MakeObject(Def,204,Cell,Orientation.R90);
        Grid.Objects[Cell,(int)ObjectLayer.WireConnectors]=endpoint;
        Check(BuildPlanningTools.ObstructionsForTest(Def,Cell,blueprint).Any(row=>(string)row["kind"]=="bridge_endpoint_conflict"),
            "another same-prefab object at the endpoint is rejected rather than broadly ignored");
        NoDirectMutation("read-only obstruction checks");
    }
    private static void TestParentNoOpAndGuards()
    {
        Setup();var selected=constructable.SelectedElementsTags;var result=Read();
        Check(!(bool)result["valid"]&&result["buildingCompleted"].Type==JTokenType.Boolean&&!(bool)result["buildingCompleted"]
            &&(bool)result["blueprintPreserved"]&&!(bool)result["blueprintPlaced"]&&!(bool)result["changed"]
            &&(int)result["applied"]==0&&!(bool)result["partial"]&&Tool.BuildCalls==1,
            "native void no-op preserves the existing blueprint and cannot be reported completed or applied");
        Check(ReferenceEquals(selected,constructable.SelectedElementsTags)&&selected.Single().Name=="FixtureRock"
            &&Tool.ActivatedElements.Single().Name=="FixtureRock"&&Tool.GetBuildingOrientation==Orientation.R90,
            "original selected material and actual blueprint rotation reach native UI unchanged");NoDirectMutation("native no-op");
        foreach(int guard in Enumerable.Range(0,6))
        {
            Setup();var args=Args();
            if(guard==0)args["confirm"]=false;if(guard==1)args["instantBuild"]=false;
            if(guard==2)args["dryRun"]=true;if(guard==3)args["validateOnly"]=true;
            if(guard==4)DebugHandler.InstantBuildMode=false;if(guard==5)BuildingControlTools.IsVirtualFileEditContext=false;
            Check(Read(args)==null&&Tool.Activations==0&&Tool.BuildCalls==0&&!blueprint.Destroyed,
                "virtual/confirm/instant/debug/preview authorization refuses without native UI or target mutation");NoDirectMutation("authorization guard");
        }
        foreach(int unavailable in Enumerable.Range(0,5))
        {
            Setup(unavailable!=2);
            if(unavailable==0)Def.PrefabID="FixtureUnsupported";
            if(unavailable==1)constructable.SelectedElementsTags.Add(new Tag("FixtureSecondMaterial"));
            if(unavailable==3)ClusterManager.Instance.activeWorldId=1;
            if(unavailable==4)BuildTool.Instance=null;
            BuildPlanningTools.FixtureDefs.Clear();BuildPlanningTools.FixtureDefs[Def.PrefabID]=Def;
            result=Read();
            Check(!(bool)result["valid"]&&Tool.BuildCalls==0&&!blueprint.Destroyed,
                "unsupported definition/material/facade/world/UI cannot fall back to deletion or direct build");NoDirectMutation("unavailable path");
        }
        foreach(bool backwall in new[]{false,true})
        {
            Setup();if(backwall)Grid.Objects[Cell,(int)ObjectLayer.Backwall]=new GameObject{Cell=Cell,Id=501};else Grid.Solid[Cell]=true;
            result=Read();Check(!(bool)result["valid"]&&Tool.BuildCalls==0&&!blueprint.Destroyed,
                "existing blueprint completion cannot clear natural solid terrain or a backwall");NoDirectMutation("terrain/backwall refusal");
        }
        Setup();blueprint.GetComponent<BuildingFacade>().CurrentFacade=null;blueprint.GetComponent<BuildingFacade>().IsOriginal=true;
        Tool.OnMouseMoveFixture=()=>{Tool.visualizer.GetComponent<BuildingFacade>().CurrentFacade="DEFAULT_FACADE";};
        result=Read();
        Check(Tool.BuildCalls==1&&Tool.ActivatedFacade==null&&!(bool)result["valid"]&&(bool)result["blueprintPreserved"],
            "native known original null facade is retained and allowed, distinct from a missing facade component");NoDirectMutation("known original facade");
    }
    private static GameObject CompleteNative(string material="FixtureRock",bool register=true,bool collateral=false)
    {
        // This is the substituted native UI callback, not MOD execution logic.
        blueprint.Destroyed=true;
        var go=MakeObject(Def,301,Cell,Orientation.R90);var complete=new BuildingComplete{Def=Def};go.Add(complete);
        go.Add(new PrimaryElement{ElementID=material});Components.BuildingCompletes.Items.Add(complete);
        if(register)Grid.Objects[Cell,(int)ObjectLayer.Building]=go;
        Grid.Solid[Cell]=true;
        if(collateral)Grid.Objects[Cell,(int)ObjectLayer.Wire]=null;
        return go;
    }
    private static void TestNativeCompletionObservation()
    {
        Setup();Tool.OnBuild=cell=>CompleteNative();var result=Read();
        Check((bool)result["valid"]&&(bool)result["buildingCompleted"]&&(bool)result["completedExistingBlueprint"]
            &&!(bool)result["blueprintPlaced"]&&(bool)result["changed"]&&(int)result["applied"]==1,
            "only independently observed exact, registered native completion is successful");NoDirectMutation("native completed branch");
        Setup();blueprint.GetComponent<BuildingFacade>().CurrentFacade=null;blueprint.GetComponent<BuildingFacade>().IsOriginal=true;
        Tool.OnBuild=cell=>{var completed=CompleteNative();completed.GetComponent<BuildingFacade>().CurrentFacade="DEFAULT_FACADE";completed.GetComponent<BuildingFacade>().IsOriginal=true;};
        result=Read();
        Check((bool)result["valid"]&&(bool)result["buildingCompleted"],"native default/null original facade normalization is semantically equivalent on exact completion");
        NoDirectMutation("original facade completion");
        Setup();NativeBuildingCellIndex.OnRead=(go,read)=>
        {if(ReferenceEquals(go,blueprint)&&read==2){CompleteNative();throw new InvalidOperationException("fixture before observation unavailable");}};
        result=Read();
        Check(!(bool)result["valid"]&&result["buildingCompleted"].Type==JTokenType.Null&&result["applied"].Type==JTokenType.Null&&Tool.BuildCalls==0,
            "a completed object found after unavailable before-observation cannot establish this request's completion or applied count");
        NoDirectMutation("missing before observation");
        foreach(int invalid in Enumerable.Range(0,5))
        {
            Setup();int kind=invalid;Tool.OnBuild=cell=>
            {
                if(kind==0)CompleteNative("FixtureOtherRock");if(kind==1)CompleteNative(register:false);if(kind==2)blueprint.Destroyed=true;
                if(kind==3){var hidden=CompleteNative(register:false);hidden.Active=false;var item=hidden.GetComponent<BuildingComplete>();Components.BuildingCompletes.Items.Remove(item);UnityEngine.Object.DetachedCompletes.Add(item);}
                if(kind==4){var wrong=CompleteNative();wrong.GetComponent<Rotatable>().Orientation=Orientation.Neutral;NativeBuildingCellIndex.Shapes[wrong].Orientation=Orientation.Neutral;}
            };
            result=Read();
            Check(!(bool)result["valid"]&&(result["buildingCompleted"].Type==JTokenType.Null||!(bool)result["buildingCompleted"]),
                "wrong material, missing registration or missing completion cannot be reported successful");
            if(invalid!=2)Check((bool)result["completedBuildingPresent"]&&result["buildingCompleted"].Type==JTokenType.Null,
                "raw completed candidate, including inactive Unity-only or wrong-geometry objects, stays present but unverified");
            Check((bool)result["partial"]&&result["applied"].Type==JTokenType.Null,
                "a changed target with no verified completion stays partial with unknown applied count");NoDirectMutation("invalid native completion");
        }
        Setup();var wire=new GameObject{Cell=Cell,Id=401,name="Fixture retained wire"};Grid.Objects[Cell,(int)ObjectLayer.Wire]=wire;
        Tool.OnBuild=cell=>CompleteNative(collateral:true);result=Read();
        Check((bool)result["buildingCompleted"]&&!(bool)result["valid"]&&(bool)result["partial"]
            &&(bool)result["changed"]&&result["instantCompletion"]["protectedOccupantsPreserved"].Type==JTokenType.Boolean
            &&!(bool)result["instantCompletion"]["protectedOccupantsPreserved"],
            "verified Tile completion with collateral removal is partial failure, not overall success");NoDirectMutation("collateral mutation observation");
    }
}
