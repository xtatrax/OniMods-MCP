using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class BlueprintCompletionRegression
{
    private static int checks;
    internal static void Check(bool value,string reason)
    {checks++;if(!value)throw new InvalidOperationException("Blueprint completion: "+reason);}
    internal static BuildingDef Definition;
    internal static BuildTool Tool;
    internal static PlayerController Controller;
    internal static SelectTool Select;
    internal static int Cell;
    internal static void Setup()
    {
        Cell=Grid.XYToCell(2,3);
        OniMcp.Server.GameContextLifecycle.Generation=1;
        Definition=new BuildingDef{PrefabID="Tile",Name="Fixture tile"};
        Game.Instance=new Game();PlanScreen.Instance=new PlanScreen();
        World.Instance=new World{blockTileRenderer=new object()};GridCompositor.Instance=new GridCompositor();
        ResourceRemainingDisplayScreen.instance=new ResourceRemainingDisplayScreen();
        SpeedControlScreen.Instance=new SpeedControlScreen();KScreenManager.Instance=new KScreenManager();KScreenManager.inputDisabled=false;
        Select=new SelectTool();SelectTool.Instance=Select;
        Controller=new PlayerController{ActiveTool=Select};PlayerController.Instance=Controller;
        var holder=new GameObject();Tool=new BuildTool();holder.Add(Tool);holder.Add(new BuildToolHoverTextCard());BuildTool.Instance=Tool;
        ClusterManager.Instance=new ClusterManager{activeWorldId=0};
        for(int i=0;i<64;i++){Grid.Visible[i]=1;Grid.WorldIdx[i]=0;Grid.Solid[i]=false;Grid.Foundation[i]=false;}
        Grid.Objects=new Grid.ObjectLayerIndexer();Grid.ResetObjectLayers();
        DebugHandler.InstantBuildMode=true;BuildingControlTools.IsVirtualFileEditContext=true;
        BuildPlanningTools.FixtureBlueprints.Clear();BuildPlanningTools.FixtureDefs.Clear();BuildPlanningTools.FixtureDefs["Tile"]=Definition;
        NativeBuildingCellIndex.Reads=0;NativeBuildingCellIndex.OnRead=null;UnityEngine.Object.DetachedCompletes.Clear();
        BuildPlanningTools.FixtureEndpoints.Clear();Components.BuildingCompletes.Items.Clear();NativeBuildingCellIndex.Shapes.Clear();
    }
    private static NativeBlueprintInstantCompletion.Outcome RunNative(System.Action validate=null)
    {return NativeBlueprintInstantCompletion.Run(Definition,new List<Tag>{new Tag("FixtureRock")},"FixtureFacade",Cell,Orientation.R90,validate??(()=>{}));}
    internal static void Run()
    {
        TestNativeReadiness();TestNativeBinding();TestNativeFailures();
        RunParentCases();
        Console.WriteLine("Blueprint native completion: "+checks+" host checks passed (product adapter/caller and exact safety slices; native UI/geometry boundaries substituted)");
    }
    private static void TestNativeReadiness()
    {
        var failures=new System.Action[]{()=>BuildTool.Instance=null,()=>PlayerController.Instance=null,()=>SelectTool.Instance=null,
            ()=>Game.Instance=null,()=>Game.Instance.Loading=true,()=>PlanScreen.Instance=null,()=>World.Instance.blockTileRenderer=null,
            ()=>ResourceRemainingDisplayScreen.instance=null,()=>GridCompositor.Instance=null,()=>KScreenManager.Instance=null,
            ()=>SpeedControlScreen.Instance.IsPaused=false,()=>KScreenManager.inputDisabled=true,
            ()=>KScreenManager.Instance.screenStack.Add(new KScreen{Active=true,Modal=true}),
            ()=>Controller.ActiveTool=new object(),()=>Controller.Dragging=true,()=>Tool.Dragging=true};
        foreach(var fail in failures)
        {
            Setup();fail();int validations=0;var outcome=RunNative(()=>validations++);
            Check(!outcome.Available&&!outcome.ActivationAttempted&&!outcome.ActionAttempted&&outcome.ReasonCode!=null
                &&validations==0&&Tool.BuildCalls==0&&Definition.DirectBuildCalls==0,
                "unavailable/busy/unpaused native UI refuses before validation, activation or game mutation");
        }
    }
    private static void TestNativeBinding()
    {
        Setup();int validations=0;var outcome=RunNative(()=>validations++);
        Check(outcome.Available&&outcome.ActivationAttempted&&outcome.ActionAttempted&&outcome.ReasonCode==null
            &&validations==2&&Tool.BuildCalls==1&&Tool.Activations==1,
            "ready tool invokes the native void action exactly once after both target checks");
        Check(ReferenceEquals(Tool.ActivatedDef,Definition)&&Tool.ActivatedElements.Single().Name=="FixtureRock"
            &&Tool.ActivatedFacade=="FixtureFacade"&&Tool.GetBuildingOrientation==Orientation.R90&&Tool.GetLastCell==Cell,
            "exact definition, original elements, facade, cell and observed rotation reach the native UI");
        Check(outcome.UiRetargeted==true&&outcome.UiRestored==true&&ReferenceEquals(Controller.ActiveTool,Select)&&Controller.RestoreCalls==1,
            "native UI entry restores the active selection tool on its normal exit");
        var body=JObject.FromObject(outcome.ToDictionary());
        Check(body["selectionRestored"].Type==JTokenType.Null&&body["overlayRestored"].Type==JTokenType.Null
            &&body["buildingCompleted"]==null,
            "void native invocation does not claim physical completion, selection or overlay restoration");
        foreach(var change in new System.Action[]{()=>Tool.visualizer.Cell=Cell+1,()=>Tool.GetBuildingOrientation=Orientation.Neutral,
            ()=>Tool.visualizer.GetComponent<Building>().Def=new BuildingDef{PrefabID="Tile"}})
        {
            Setup();Tool.OnMouseMoveFixture=change;outcome=RunNative();
            bool ownsPreview=ReferenceEquals(Tool.visualizer.GetComponent<Building>().Def,Definition);
            Check(!outcome.ActionAttempted&&outcome.ReasonCode=="preview_unavailable"&&Tool.BuildCalls==0
                &&(ownsPreview?outcome.UiRestored==true:outcome.UiRestored==null),
                "mismatched preview cannot reach TryBuild; cleanup also requires retained preview ownership");
        }
        foreach(bool replace in new[]{false,true})
        {
            Setup();Tool.OnMouseMoveFixture=()=>
            {if(replace)Tool.ReplaceNativeMaterials(new List<Tag>{new Tag("FixtureOtherRock")});else Tool.EditNativeMaterial(new Tag("FixtureOtherRock"));};
            outcome=RunNative();
            Check(!outcome.ActionAttempted&&Tool.BuildCalls==0&&outcome.ReasonCode=="preview_unavailable",
                "native selected-elements replacement or same-list write cannot pass immutable pre-gesture material verification");
        }
        Setup();var originalMaterials=new List<Tag>{new Tag("FixtureRock")};Tool.OnActivate=()=>Tool.EditNativeMaterial(new Tag("FixtureOtherRock"));
        outcome=NativeBlueprintInstantCompletion.Run(Definition,originalMaterials,"FixtureFacade",Cell,Orientation.R90,()=>{});
        Check(originalMaterials.Single().Name=="FixtureRock"&&!outcome.ActionAttempted,
            "native activation receives a detached material list and cannot alter the caller's expected material baseline");
        Setup();int rechecks=0;
        outcome=RunNative(()=>{if(++rechecks==2)throw new NativeBlueprintInstantCompletion.Failure("target_changed");});
        Check(outcome.ActionAttempted==false&&Tool.BuildCalls==0&&rechecks==2&&outcome.UiRestored==true,
            "target changed after activation is revalidated before the native action");
    }
    private static void TestNativeFailures()
    {
        Setup();Tool.OnActivate=()=>{throw new InvalidOperationException("fixture activation failure");};var outcome=RunNative();
        Check(outcome.ActivationAttempted&&!outcome.ActionAttempted&&outcome.ExceptionType=="InvalidOperationException"
            &&outcome.UiRestored==null&&Controller.RestoreCalls==0&&Tool.BuildCalls==0,
            "partial activation failure retains attempt evidence without guessing ownership for cleanup");
        Setup();Tool.OnBuild=cell=>{throw new InvalidOperationException("fixture native failure");};outcome=RunNative();
        Check(outcome.ActionAttempted&&outcome.ReasonCode=="native_failed"&&outcome.ExceptionType=="InvalidOperationException"
            &&outcome.UiRestored==true&&Tool.BuildCalls==1,"native exception is reported after one attempt without deletion or direct fallback");
        foreach(bool atActivation in new[]{true,false})
        {
            Setup();var foreign=new object();
            if(atActivation)Tool.OnActivate=()=>Controller.ActiveTool=foreign;
            else Tool.OnBuild=cell=>Controller.ActiveTool=foreign;
            outcome=RunNative();
            Check(ReferenceEquals(Controller.ActiveTool,foreign)&&Controller.RestoreCalls==0&&outcome.UiRestored==null,
                "foreign native tool selected during callback is retained rather than overwritten by cleanup");
        }
        foreach(bool newDefinition in new[]{false,true})
        {
            Setup();GameObject retargeted=null;
            Tool.OnBuild=cell=>
            {
                Tool.Activate(newDefinition?new BuildingDef{PrefabID="Tile"}:Definition,new List<Tag>{new Tag("FixtureRock")},"FixtureFacade");
                retargeted=Tool.visualizer;
            };
            outcome=RunNative();
            Check(outcome.ActionAttempted&&outcome.UiRestored==null&&Controller.RestoreCalls==0
                &&ReferenceEquals(Controller.ActiveTool,Tool)&&ReferenceEquals(Tool.visualizer,retargeted),
                "same BuildTool singleton reactivation owns a new preview and must not be cleared by this request");
        }
        Setup();Tool.OnBuild=cell=>Controller.ActiveTool=Select;outcome=RunNative();
        Check(outcome.UiRestored==true&&Controller.RestoreCalls==0,"already restored captured SelectTool requires no cleanup write");
        Setup();Tool.OnBuild=cell=>OniMcp.Server.GameContextLifecycle.Generation++;outcome=RunNative();
        Check(outcome.ActionAttempted&&outcome.UiRestored==null&&Controller.RestoreCalls==0,
            "context changed after native action cannot restore tools in another scene generation");
        Setup();Controller.RejectRestore=true;outcome=RunNative();
        Check(outcome.ActionAttempted&&outcome.UiRestored==null&&outcome.ReasonCode=="cleanup_failed"
            &&outcome.CleanupExceptionType!=null,"cleanup failure remains explicit rather than falsely restoring UI state");
    }
    private static void RunParentCases() { BlueprintParentRegression.Run(); }
}
