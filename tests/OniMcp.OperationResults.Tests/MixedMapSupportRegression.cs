using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using OniMcp.Support;

internal static class MixedMapSupportRegression
{
    private const string Snapshot="百位X: 0 0\n十位X: 1 1\n个位X: 0 1\nY=21: . .\nY=20: . .";
    private const string Patch="百位X: 0 0\n十位X: 1 1\n个位X: 0 1\nY=21: Locker:5 .\nY=20: Tile:5 .";
    private static int checks;
    private static void Check(bool value,string label) { checks++;if(!value)throw new Exception("map_shared_preflight: "+label); }
    private static JObject Parent(bool partial=false)=>new JObject { ["confirm"]=true,["allowPartial"]=partial,["task"]="mixed support host",["worldId"]=0 };
    private static BuildingDef Def(string id,bool floor=false)=>new BuildingDef
    {
        PrefabID=id,Name=id,IsFoundation=floor,TileLayer=floor?ObjectLayer.FoundationTile:ObjectLayer.NumLayers,
        BuildLocationRule=floor?BuildLocationRule.Tile:BuildLocationRule.Anywhere,
        Shape=new NativeShape { Size=new[]{1,1},Bounds=new[]{0,0,0,0},Cells=new List<NativePoint>{new NativePoint()} }
    };
    private static CallToolResult Preflight(bool valid)=>CallToolResult.Text(new JObject
    { ["valid"]=valid,["actionable"]=valid,["hardFailed"]=valid?0:1,["committed"]=false }.ToString());
    private static CallToolResult Actual(string prefab,int y,bool valid=true)=>CallToolResult.Text(new JObject
    {
        ["prefabId"]=prefab,["worldId"]=0,["committed"]=valid,["failed"]=valid?0:1,["ok"]=valid,
        ["results"]=new JArray(new JObject { ["prefabId"]=prefab,["worldId"]=0,["x"]=10,["y"]=y,
            ["valid"]=valid,["planned"]=valid,["blueprintPlaced"]=valid,["id"]=valid?101:0 })
    }.ToString());
    private static void Reset()
    {
        MapNativeBoundary.Snapshot=Snapshot;MapNativeBoundary.WorldResolver=null;
        MapNativeBoundary.PreflightCalls.Clear();MapNativeBoundary.BuildingCalls.Clear();MapNativeBoundary.BuildingResults.Clear();
        MapNativeBoundary.OrderCalls.Clear();MapNativeBoundary.OrderResults.Clear();
        Assets.BuildingDefs.Clear();Assets.BuildingDefs.Add(Def("Tile",true));Assets.BuildingDefs.Add(Def("Locker"));
        MapNativeBoundary.PreflightHandler=(args,context)=>
        {
            Check(MapNativeBoundary.BuildingCalls.Count==0,"all preflights precede writes");
            Check((bool)args["dryRun"]&&(bool)args["confirm"],"explicit placement preflight only");
            if((string)args["prefabId"]=="Tile")context.Include(0,Grid.XYToCell(10,20));
            else Check(context.CopyForWorld(0).Contains(Grid.XYToCell(10,20)),"same-request future floor shared");
            Check(context.CopyForWorld(1).Count==0,"different world receives no support");
            return Preflight(true);
        };
    }
    internal static void Run()
    {
        foreach(string language in new[]{"en","ja","zh-Hans"})
            Check(McpLocalization.GetForLanguage(language,"map_request_support.preflight_failed")!="map_request_support.preflight_failed",
                "runtime localized preflight message embedded for "+language);
        Reset();
        MapNativeBoundary.BuildingResults.Enqueue(Actual("Tile",20));MapNativeBoundary.BuildingResults.Enqueue(Actual("Locker",21));
        var result=WorldEditorTools.ApplyMapForCountTest(Parent(),Snapshot,Patch);
        Check(MapNativeBoundary.PreflightCalls.Count==2,"real map adapter has a two-phase preflight");
        Check((string)MapNativeBoundary.PreflightCalls[0]["prefabId"]=="Tile","support-first despite descending map rows");
        Check((string)MapNativeBoundary.BuildingCalls[0]["prefabId"]=="Tile","actual supports first");
        Check(MapNativeBoundary.BuildingCalls.All(call=>call["dryRun"]==null||!(bool)call["dryRun"]),"actual children receive actual args");
        Check(!result.IsError,"supported mixed map accepted");

        Reset();MapNativeBoundary.PreflightHandler=(args,context)=>Preflight((string)args["prefabId"]=="Tile");
        result=WorldEditorTools.ApplyMapForCountTest(Parent(),Snapshot,Patch);
        Check(result.IsError&&MapNativeBoundary.BuildingCalls.Count==0,"default hard failure rejects whole map before writes");
        var failure=JObject.Parse(result.Content[0].Text);
        Check((bool)failure["changed"]==false&&(int)failure["executedCells"]==0,"preflight rejection reports no mutation");

        Reset();MapNativeBoundary.BuildingResults.Enqueue(Actual("Tile",20,false));MapNativeBoundary.BuildingResults.Enqueue(Actual("Locker",21,false));
        result=WorldEditorTools.ApplyMapForCountTest(Parent(true),Snapshot,Patch);
        Check(MapNativeBoundary.BuildingCalls.Count==2,"actual failure is observed without future-set forcing");
        var actualFailure=JObject.Parse(result.Content[0].Text);
        Check((int)actualFailure["executedCells"]==0&&(int)actualFailure["remainingCells"]==2,
            "actual failed supports/dependent results remain unsatisfied");

        Reset();var syntaxArgs=Parent();syntaxArgs["dryRun"]=true;
        result=WorldEditorTools.ApplyMapForCountTest(syntaxArgs,Snapshot,Patch);
        Check(MapNativeBoundary.PreflightCalls.Count==0&&MapNativeBoundary.BuildingCalls.Count==0,"outer dry-run remains syntax/shape only");

        var a=new BuildPlanningRequestSupportContext(0);a.Include(0,20);var clone=a.CopyForWorld(0);clone.Clear();
        Check(a.CopyForWorld(0).Contains(20),"copied set cannot mutate request context");
        Check(new BuildPlanningRequestSupportContext(0).CopyForWorld(0).Count==0,"no cross-request support leakage");
        MapNativeBoundary.PreflightHandler=null;MapNativeBoundary.PreflightCalls.Clear();
        Console.WriteLine("Mixed map support preflight: "+checks+" checks PASS");
    }
}
