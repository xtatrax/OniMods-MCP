using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class TileReplacementParentRegression
{
    private static void Check(bool value,string reason){TileReplacementRegression.Check(value,reason);}
    internal static void Run(){PreviewAndGuards();QueueObservation();IdempotentQueue();ChangedTargets();UnknownObservations();}
    private static bool IsTrue(JObject value,string key){return value[key]?.Type==JTokenType.Boolean&&value[key].Value<bool>();}
    private static bool IsFalse(JObject value,string key){return value[key]?.Type==JTokenType.Boolean&&!value[key].Value<bool>();}
    private static void PreviewAndGuards()
    {
        TileReplacementFixture.Setup();var result=TileReplacementFixture.PlanRead(TileReplacementFixture.Args(true));
        Check(IsTrue(result,"valid")&&IsFalse(result,"planned")&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Plan.CopyCalls==0
            &&TileReplacementFixture.Materials.MaterialSelectCalls==0&&TileReplacementFixture.Priority.SetCalls==1
            &&result["tileReplacement"]?["initialOptionalBuildMenuPriorityState"]?.Value<string>()=="not_observed",
            "read-only preview uses real parent route, allows only the exact native-compatible floor and performs no native UI writes");
        TileReplacementFixture.AssertOriginalAndWire("preview keeps original floor, support flags and colocated wire");
        foreach(string key in new[]{"confirm","worldId","priority","orientation"})
        {
            TileReplacementFixture.Setup();var args=TileReplacementFixture.Args();
            if(key=="confirm")args[key]=false;else if(key=="worldId")args[key]=1;else if(key=="priority")args[key]=10;else args[key]="R90";
            result=TileReplacementFixture.PlanRead(args);
            Check(!IsTrue(result,"valid")&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Plan.CopyCalls==0,
                "parent rejects mismatching explicit "+key+" before preparation");
            TileReplacementFixture.AssertOriginalAndWire("guard rejection never deconstructs original or changes wire");
        }
        TileReplacementFixture.Setup();BuildPlanningTools.FixtureMaterialValid=false;result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&TileReplacementFixture.Plan.CopyCalls==0,"material service rejection remains before UI entry");
        TileReplacementFixture.Setup();BuildPlanningTools.FixtureAvailabilityError="fixture research unavailable";result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&TileReplacementFixture.Plan.CopyCalls==0,"research/availability guard is retained");
        foreach(string pending in new[]{"deconstruct","reconstruct","dig"})
        {
            TileReplacementFixture.Setup();
            if(pending=="deconstruct")TileReplacementFixture.Original.GetComponent<Deconstructable>().Marked=true;
            else if(pending=="reconstruct")TileReplacementFixture.Original.Add(new Reconstructable{ReconstructRequested=true});
            else Grid.Objects[TileReplacementFixture.Cell,(int)ObjectLayer.DigPlacer]=new GameObject();
            result=TileReplacementFixture.PlanRead();
            Check(!IsTrue(result,"valid")&&TileReplacementFixture.Plan.CopyCalls==0,"existing marked work "+pending+" is not cancelled or replaced by this request");
        }
        TileReplacementFixture.Setup();BuildingControlTools.IsVirtualFileEditContext=false;result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&TileReplacementFixture.Plan.CopyCalls==0,"non-map execution cannot enter the normal replacement adapter");
        TileReplacementFixture.Setup();Grid.Visible[TileReplacementFixture.Cell]=0;result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&TileReplacementFixture.Plan.CopyCalls==0,"hidden target is rejected before UI entry");
        foreach(bool sandbox in new[]{false,true})
        {
            TileReplacementFixture.Setup();if(sandbox){Game.Instance.SandboxModeActive=true;SandboxToolParameterMenu.instance.settings.InstantBuild=true;}else DebugHandler.InstantBuildMode=true;
            result=TileReplacementFixture.PlanRead();
            Check(!IsTrue(result,"valid")&&TileReplacementFixture.Tool.BuildCalls==0,"neither native instant flag can use the ordinary replacement path");
            TileReplacementFixture.AssertOriginalAndWire("instant refusal is no-delete/no-direct-build");
        }
    }
    private static void QueueObservation()
    {
        TileReplacementFixture.Setup();GameObject queue=null;TileReplacementFixture.Tool.OnBuild=cell=>queue=TileReplacementFixture.Queue();
        var result=TileReplacementFixture.PlanRead();
        Check(IsTrue(result,"valid")&&IsTrue(result,"planned")&&IsFalse(result,"buildingCompleted")&&TileReplacementFixture.Tool.BuildCalls==1,
            "native exact replacement queue is accepted as queued construction, never completed building");
        Check(queue!=null&&queue.GetComponent<Constructable>().IsReplacementTile&&queue.GetComponent<Constructable>().SelectedElementsTags.Single().Name=="Cuprite"
            &&queue.GetComponent<Prioritizable>().GetMasterPriority().priority_value==7,"requested new material and explicit native priority are observed on the queue");
        TileReplacementFixture.AssertOriginalAndWire("accepted queue retains floor/support and colocated wire without prior demolition");
        TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>TileReplacementFixture.Queue(priority:5);
        result=TileReplacementFixture.PlanRead(TileReplacementFixture.Args(priority:null));
        Check(IsTrue(result,"valid")&&TileReplacementFixture.Tool.BuildCalls==1,"omitted priority retains the existing basic-five default");
        TileReplacementFixture.Setup();result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&!IsTrue(result,"planned")&&IsFalse(result,"buildingCompleted")&&TileReplacementFixture.Tool.BuildCalls==1,
            "native void no-op is not fabricated into a queue or completed object");
        TileReplacementFixture.AssertOriginalAndWire("native no-op has no fallback or deconstruction");
        foreach(bool wrongPriority in new[]{false,true})
        {
            TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>TileReplacementFixture.Queue(wrongPriority?"Cuprite":"GoldAmalgam",wrongPriority?6:7);
            result=TileReplacementFixture.PlanRead();
            Check(!IsTrue(result,"valid")&&TileReplacementFixture.Tool.BuildCalls==1&&!IsTrue(result,"buildingCompleted"),
                "native queue with wrong material or priority is not accepted or retried");
        }
        TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>{TileReplacementFixture.Queue();Grid.Objects[cell,(int)ObjectLayer.Wire]=null;};
        result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&IsTrue(result,"partial")&&TileReplacementFixture.Tool.BuildCalls==1,
            "a correct queue plus lost colocated wire is partial failure rather than successful replacement");
        foreach(bool overwriteMaterial in new[]{false,true})
        {
            TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>
            {TileReplacementFixture.Queue();if(overwriteMaterial)TileReplacementFixture.Original.GetComponent<PrimaryElement>().ElementID="Cuprite";else Grid.Foundation[cell]=false;};
            result=TileReplacementFixture.PlanRead();
            Check(!IsTrue(result,"valid")&&IsTrue(result,"partial")&&IsTrue(result,"changed"),
                "original material and constructed-foundation metadata are preserved independently of queue acceptance");
        }
        TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>{TileReplacementFixture.Queue();throw new InvalidOperationException("fixture exception after queue");};
        result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&TileReplacementFixture.Tool.BuildCalls==1&&!IsTrue(result,"buildingCompleted"),
            "exception after native queue retains evidence without success or fallback retry");
    }
    private static void IdempotentQueue()
    {
        TileReplacementFixture.Setup();var queue=TileReplacementFixture.Queue();BuildPlanningTools.FixtureMaterialValid=false;
        var result=TileReplacementFixture.PlanRead();
        Check(IsTrue(result,"valid")&&IsFalse(result,"planned")&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Plan.CopyCalls==0
            &&BuildPlanningTools.FixtureSelectCalls==0&&ReferenceEquals(Grid.Objects[TileReplacementFixture.Cell,(int)ObjectLayer.ReplacementTile],queue),
            "exact existing queue is reusable before inventory selection even if no new material can be selected");
        foreach(string difference in new[]{"priority","material","replacement","geometry","differentPrefab"})
        {
            TileReplacementFixture.Setup();queue=TileReplacementFixture.Queue();
            if(difference=="priority")queue.GetComponent<Prioritizable>().Observed=new PrioritySetting(PriorityScreen.PriorityClass.basic,6);
            else if(difference=="material")queue.GetComponent<Constructable>().SelectedElementsTags=new List<Tag>{new Tag("GoldAmalgam")};
            else if(difference=="replacement")queue.GetComponent<Constructable>().IsReplacementTile=false;
            else if(difference=="geometry"){NativeBuildingCellIndex.Shapes[queue].Cells[0].Cell=TileReplacementFixture.Cell+1;NativeBuildingCellIndex.Shapes[queue].Cells[0].X++;}
            else {queue.GetComponent<Building>().Def=new BuildingDef{PrefabID="FixtureOtherTile"};}
            result=TileReplacementFixture.PlanRead();
            Check(!IsTrue(result,"valid")&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Plan.CopyCalls==0,
                "existing queue mismatch "+difference+" cannot escape through generic early already-present reuse");
        }
    }
    private static void UnknownObservations()
    {
        TileReplacementFixture.Setup();UnityEngine.Object.ThrowEnumeration=true;
        var result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&result["changed"].Type==JTokenType.Null&&result["applied"].Type==JTokenType.Null
            &&TileReplacementFixture.Plan.CopyCalls==0,"missing initial observation cannot claim no change, queue, completion or native availability");
        TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>{TileReplacementFixture.Queue();UnityEngine.Object.ThrowEnumeration=true;};
        result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&IsTrue(result,"partial")&&result["changed"].Type==JTokenType.Null
            &&result["buildingCompleted"].Type==JTokenType.Null&&result["applied"].Type==JTokenType.Null,
            "unreadable post-action state stays nullable and partial even when the fixture callback created a queue");
    }
    private static void ChangedTargets()
    {
        TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnMouseMoveFixture=()=>
        {Grid.Objects[TileReplacementFixture.Cell,(int)ObjectLayer.FoundationTile]=TileReplacementFixture.Make(TileReplacementFixture.OldDefinition,404,TileReplacementFixture.Cell,"FixtureRock",true);};
        var result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&TileReplacementFixture.Tool.BuildCalls==0,"same-prefab replacement of original identity during UI setup is rejected before gesture");
        TileReplacementFixture.Setup();Grid.Objects[TileReplacementFixture.Cell,(int)ObjectLayer.ReplacementLadder]=new GameObject();
        result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&TileReplacementFixture.Plan.CopyCalls==0,"equivalent native replacement-layer occupancy refuses before UI preparation");
        TileReplacementFixture.Setup();var third=TileReplacementFixture.Make(TileReplacementFixture.OldDefinition,505,TileReplacementFixture.Cell,"FixtureRock",true);
        result=TileReplacementFixture.PlanRead();
        Check(!IsTrue(result,"valid")&&TileReplacementFixture.Plan.CopyCalls==0&&third.DeleteCalls==0,
            "broad old-target exclusion cannot hide a second same-prefab physical building at the target cell");
    }
}
