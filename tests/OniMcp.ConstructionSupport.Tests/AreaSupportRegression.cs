using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class AreaSupportRegression
{
    private static int checks;
    private static int Cell(int x,int y)=>Grid.XYToCell(x,y);
    private static void Check(bool value,string label){checks++;if(!value)throw new Exception("area_request_support: "+label);}
    private static JObject Args(string prefab,bool dry=true,int budget=32,bool partial=false,params int[] xs)=>new JObject
    {
        ["prefabId"]=prefab,["worldId"]=0,["dryRun"]=dry,["confirm"]=true,["maxCommitAnchors"]=budget,["allowPartial"]=partial,
        ["anchors"]=new JArray(xs.Select(x=>new JObject{["x"]=x,["y"]=prefab=="Locker"?11:10}))
    };
    private static JObject Run(JObject a,BuildPlanningRequestSupportContext ctx=null)
        =>JObject.Parse(BuildPlanningTools.BuildArea(ctx).Handler(a).Content[0].Text);
    internal static void Run()
    {
        AreaFixture.Reset();var ctx=new BuildPlanningRequestSupportContext(0);
        Run(Args("Tile",true,1,false,10,11),ctx);
        Check(ctx.CopyForWorld(0).SetEquals(new[]{Cell(10,10)}),"only budget-selected valid support exported");
        Check(AreaFixture.Mutations==0&&BuildPlanningTools.Blueprints.Count==0,"shared preflight writes no game fixture");
        var dependent=Run(Args("Locker",true,32,true,10,11),ctx);
        Check((int)dependent["validAnchors"]==1&&(int)dependent["hardFailed"]==1,"uncommitted deferred floor cannot support second equipment");
        var pending=(JObject)dependent["previews"][0]["support"];
        Check((bool)pending["currentValid"]==false&&(bool)pending["nativeFoundationValid"]==false,"future support never changes native current validity");
        Check((bool)pending["requiresSupportCompletion"]&&pending["supportCells"][0]["state"].ToString()=="planned_in_request","planned dependency and completion requirement visible");

        AreaFixture.Reset();ctx=new BuildPlanningRequestSupportContext(0);AreaFixture.DigOnly.Add(Cell(10,10));
        Run(Args("Tile",true,1,true,10,11),ctx);
        Check(ctx.CopyForWorld(0).Count==0,"auto-dig-only first anchor consumes budget without exporting support");
        Check(AreaFixture.Mutations==0,"auto-dig preview never queues dig");

        AreaFixture.Reset();ctx=new BuildPlanningRequestSupportContext(0);AreaFixture.Blocked.Add(Cell(11,10));
        Run(Args("Tile",true,32,false,10,11),ctx);
        Check(ctx.CopyForWorld(0).Count==0,"default hard-failed support group exports nothing");
        Run(Args("Tile",true,1,true,11,10),ctx);
        Check(ctx.CopyForWorld(0).SetEquals(new[]{Cell(10,10)}),"allowPartial exports selected successful subset only");

        AreaFixture.Reset();ctx=new BuildPlanningRequestSupportContext(0);
        Run(Args("Tile",true,32,false,10,10),ctx);
        Check(ctx.CopyForWorld(0).Count==0,"overlapping support anchors export nothing");
        foreach(var change in new Action<BuildingDef>[] { d=>d.IsFoundation=false,d=>d.WidthInCells=2,d=>d.TileLayer=ObjectLayer.Building,d=>d.BuildLocationRule=BuildLocationRule.Anywhere,d=>d.PlacementOffsets=new[]{new CellOffset(1,0)} })
        {
            AreaFixture.Reset();change(Assets.Definitions["Tile"]);ctx=new BuildPlanningRequestSupportContext(0);
            Run(Args("Tile",true,32,false,10),ctx);Check(ctx.CopyForWorld(0).Count==0,"unknown or special support definition not exported");
        }
        AreaFixture.Reset();ctx=new BuildPlanningRequestSupportContext(1);
        Run(Args("Tile",true,32,false,10),ctx);Check(ctx.CopyForWorld(1).Count==0,"world mismatch cannot export support");

        AreaFixture.Reset();ctx=new BuildPlanningRequestSupportContext(0);
        Run(Args("Tile",true,32,false,10),ctx);
        var real=Run(Args("Locker",false,32,false,10));
        Check((int)real["hardFailed"]==1&&AreaFixture.Mutations==0,"actual execution without future context rereads missing native supports");
        AreaFixture.ActualFailed.Add(Cell(10,10));Run(Args("Tile",false,32,false,10));
        real=Run(Args("Locker",false,32,false,10));
        Check((int)real["hardFailed"]==1&&BuildPlanningTools.Blueprints.Count==0,"failed actual floor cannot authorize dependent placement");
        AreaFixture.ActualFailed.Clear();Run(Args("Tile",false,32,false,10));real=Run(Args("Locker",false,32,false,10));
        var observed=(JObject)real["results"][0]["support"];
        Check((int)real["planned"]==1&&(bool)observed["currentValid"]==false&&(bool)observed["requiresSupportCompletion"],"actual support blueprint permits sequencing but remains physically invalid");
        Check(observed["supportCells"][0]["state"].ToString()=="support_blueprint","actual child observes real blueprint rather than prior request forecast");
        AreaFixture.Reset();Console.WriteLine("Request support budget/native boundary: "+checks+" checks PASS");
    }
}
