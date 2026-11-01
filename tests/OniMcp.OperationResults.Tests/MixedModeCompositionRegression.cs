using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

// Whole real Edit -> MapEditTools -> mixed MapBuildEdit -> typed support helper.
// Only native BuildArea terminal callbacks are substituted; this does not test
// native stock selection, actual foundation construction, or ONI runtime.
internal static class MixedModeCompositionRegression
{
    private const string Snapshot="百位X: 0 0\n十位X: 1 1\n个位X: 0 1\nY=22: . .\nY=21: . .\nY=20: . .";
    private const string Replace="百位X: 0 0\n十位X: 1 1\n个位X: 0 1\nY=22: Locker:5 .\nY=21: Locker:5 .\nY=20: Tile:5 .";
    private static int checks;
    private static void Check(bool value,string message)
    { checks++;if(!value)throw new InvalidOperationException("mixed_mode_scope: "+message); }
    private static BuildingDef Def(string id,int height,bool floor=false)
    {
        var cells=new List<NativePoint>();for(int y=0;y<height;y++)cells.Add(new NativePoint { X=0,Y=y });
        return new BuildingDef { PrefabID=id,Name=id,IsFoundation=floor,WidthInCells=1,HeightInCells=height,
            TileLayer=floor?ObjectLayer.FoundationTile:ObjectLayer.NumLayers,
            BuildLocationRule=floor?BuildLocationRule.Tile:BuildLocationRule.Anywhere,
            Shape=new NativeShape { Size=new[]{1,height},Bounds=new[]{0,0,0,height-1},Cells=cells } };
    }
    private static JObject Args(bool instant,bool nested=false,bool dry=false)
    {
        var args=new JObject { ["confirm"]=true,["allowPartial"]=false,["task"]="combined typed mixed map mode host",
            ["allowSandbox"]=true,["worldId"]=0,["path"]="/active/map/viewport.md",["dryRun"]=dry,
            ["content"]="<<<<<<< SEARCH\n"+Snapshot+"\n=======\n"+Replace+"\n>>>>>>> REPLACE" };
        // Flat false is the genuine omitted/default normal request. Nested false
        // is explicit so it overrides, then restores, an enclosing instant scope.
        if(instant||nested)args["instantBuild"]=instant;
        return args;
    }
    private static CallToolResult Actual(string prefab,int y,bool accepted)
        =>CallToolResult.Text(new JObject { ["prefabId"]=prefab,["worldId"]=0,["committed"]=accepted,
            ["ok"]=accepted,["changed"]=accepted,["failed"]=accepted?0:1,
            ["results"]=new JArray(new JObject { ["prefabId"]=prefab,["worldId"]=0,["x"]=10,["y"]=y,
                ["valid"]=accepted,["planned"]=accepted,["blueprintPlaced"]=accepted,["id"]=accepted?100+y:0 }) }.ToString());
    private static void Reset(bool current)
    {
        DebugHandler.Seed(current);MapNativeBoundary.Snapshot=Snapshot;MapNativeBoundary.WorldResolver=null;
        MapNativeBoundary.BuildingCalls.Clear();MapNativeBoundary.BuildingResults.Clear();
        MapNativeBoundary.PreflightCalls.Clear();MapNativeBoundary.PreflightHandler=null;
        MapNativeBoundary.FactoryContexts.Clear();MapNativeBoundary.ModeObserver=null;MapNativeBoundary.ModeAllowPreview=false;
        Assets.BuildingDefs.Clear();Assets.BuildingDefs.Add(Def("Tile",1,true));Assets.BuildingDefs.Add(Def("Locker",2));
    }
    internal static void Run()
    {
        try
        {
            foreach(bool instant in new[]{false,true})foreach(bool nested in new[]{false,true})
            foreach(string outcome in new[]{"success","preflight_refusal","actual_refusal","actual_partial","exception"})
            {
                Reset(!instant);
                var events=new List<string>();ConstructionModeRequest mode=null;
                BuildPlanningRequestSupportContext support=null;
                MapNativeBoundary.ModeObserver=args=>
                {
                    bool preview=ToolUtil.GetBool(args,"dryRun",false);
                    Check(DebugHandler.InstantBuildMode==instant,"global preflight/actual terminal callback must observe authorized native mode");
                    Check(WorldEditorTools.EnforceNormalMaterialRules==!instant,"global preflight/actual terminal callback must observe authorized material policy");
                    var current=WorldEditorTools.ActiveConstructionRequest;
                    Check(current!=null&&current.InstantBuild==instant,"typed active request present at native boundary");
                    if(mode==null)mode=current;
                    else Check(ReferenceEquals(mode,current),"all groups share the exact established construction request");
                    events.Add((preview?"P:":"A:")+(string)args["prefabId"]);
                    if(!preview&&outcome=="exception")throw new ApplicationException("expected substituted native failure");
                };
                MapNativeBoundary.PreflightHandler=(args,context)=>
                {
                    Check(MapNativeBoundary.BuildingCalls.Count==0,"all typed group preflights precede native actions");
                    Check((bool)args["dryRun"]&&(bool)args["confirm"],"typed preview remains explicit preflight");
                    if(support==null)support=context;else Check(ReferenceEquals(support,context),"typed support context shared only within this map");
                    Check(context.CopyForWorld(1).Count==0,"different world receives no request support");
                    bool floor=(string)args["prefabId"]=="Tile";
                    if(floor)context.Include(0,Grid.XYToCell(10,20));
                    else Check(context.CopyForWorld(0).Contains(Grid.XYToCell(10,20)),"dependent receives its same-request future floor");
                    bool valid=floor||outcome!="preflight_refusal";
                    return CallToolResult.Text(new JObject { ["valid"]=valid,["actionable"]=valid,["hardFailed"]=valid?0:1,["committed"]=false }.ToString());
                };
                MapNativeBoundary.BuildingResults.Enqueue(Actual("Tile",20,outcome!="actual_refusal"));
                MapNativeBoundary.BuildingResults.Enqueue(Actual("Locker",21,outcome!="actual_refusal"&&outcome!="actual_partial"));
                CallToolResult result=null;bool thrown=false;
                Func<CallToolResult> execute=()=>WorldEditorTools.EditForCountTest(Args(instant,nested));
                try
                {
                    if(nested)
                    {
                        var parent=new JObject { ["confirm"]=true,["allowSandbox"]=true,["instantBuild"]=!instant };
                        result=WorldEditorTools.CombinedParentScope(parent,()=>
                        {
                            var enclosing=WorldEditorTools.ActiveConstructionRequest;
                            try{return execute();}
                            finally
                            {
                                Check(ReferenceEquals(WorldEditorTools.ActiveConstructionRequest,enclosing),"exact enclosing typed request restored after inner success/refusal/exception");
                                Check(DebugHandler.InstantBuildMode==!instant&&WorldEditorTools.EnforceNormalMaterialRules==instant,
                                    "enclosing native/material mode restored after mixed inner writer");
                            }
                        });
                    }
                    else result=execute();
                }
                catch(ApplicationException error){Check(error.Message=="expected substituted native failure","only expected terminal failure propagates");thrown=true;}
                Check(thrown==(outcome=="exception"),"native exception outcome retained");
                Check(DebugHandler.InstantBuildMode==!instant&&DebugHandler.Writes.Count==2,"exact native initial mode restored with one enter/exit pair");
                Check(WorldEditorTools.ActiveConstructionRequest==null&&WorldEditorTools.EnforceNormalMaterialRules,"exact initial null request and computed normal default restored");
                Check(events.Take(2).SequenceEqual(new[]{"P:Tile","P:Locker"}),"support-first global typed preflights retain descending map order correction");
                Check(MapNativeBoundary.PreflightCalls.Count==2,"both global typed preflights executed");
                Check(MapNativeBoundary.FactoryContexts.Take(2).All(value=>ReferenceEquals(value,support)),"both factory previews receive exact request-local support object");
                Check(MapNativeBoundary.FactoryContexts.Skip(2).All(value=>value==null),"actual factories receive no future support context");
                if(outcome=="preflight_refusal")
                {
                    Check(result.IsError&&MapNativeBoundary.BuildingCalls.Count==0&&events.Count==2,"default hard failure makes zero actual native calls");
                }
                else if(outcome=="exception")Check(MapNativeBoundary.BuildingCalls.Count==1&&events.Count==3,"exception stops after first actual native boundary");
                else
                {
                    Check(MapNativeBoundary.BuildingCalls.Count==2&&events.SequenceEqual(new[]{"P:Tile","P:Locker","A:Tile","A:Locker"}),"all actual support/dependent callbacks follow global preflights");
                    Check(MapNativeBoundary.BuildingCalls.All(args=>!ToolUtil.GetBool(args,"dryRun",false)),"actual native args remain actual");
                    var root=JObject.Parse(result.Content[0].Text);var map=JObject.Parse((string)root["results"][0]["result"]);
                    var group=JObject.Parse((string)map["results"][0]["result"]);
                    int satisfied=outcome=="success"?3:outcome=="actual_partial"?1:0;
                    Check((int)map["executedCells"]==satisfied&&(int)group["remainingCells"]==3-satisfied,"full locker footprint request counts remain truthful");
                    Check((string)group["executionStatus"]==(outcome=="success"?"complete":outcome=="actual_partial"?"partial":"not_completed"),"native partial/refusal never reclassified as completed");
                    Check(result.IsError==(outcome!="success"),"actual refusal status preserved through whole edit");
                }
            }
            foreach(bool native in new[]{false,true})
            {
                Reset(native);var result=WorldEditorTools.EditForCountTest(Args(true,false,true));
                Check(!result.IsError&&MapNativeBoundary.FactoryContexts.Count==0&&MapNativeBoundary.BuildingCalls.Count==0,
                    "outer syntax-only dryRun makes no typed placement preflight or actual callback");
                Check(DebugHandler.InstantBuildMode==native&&DebugHandler.Writes.Count==0&&WorldEditorTools.ActiveConstructionRequest==null,
                    "syntax-only map preserves native/typed mode without setters");
            }
            Console.WriteLine("Mixed map + local construction-mode composition: "+checks+" checks PASS");
        }
        finally {MapNativeBoundary.ModeObserver=null;MapNativeBoundary.PreflightHandler=null;DebugHandler.Seed(false);}
    }
}
namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static CallToolResult CombinedParentScope(JObject args,Func<CallToolResult> callback)
            =>RunWithWorldEditorInstantBuildScope(args,callback);
    }
}
