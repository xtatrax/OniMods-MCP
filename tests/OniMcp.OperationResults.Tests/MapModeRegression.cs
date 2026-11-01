using System;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
internal static partial class MapCountRegression {
 private static void AdditionalModeCases(){
  try{foreach(bool instant in new[]{false,true}){
   Setup();DebugHandler.Seed(!instant);
   MapNativeBoundary.ModeObserver=forwarded=>Require(DebugHandler.InstantBuildMode==instant&&WorldEditorTools.EnforceNormalMaterialRules==!instant,"actual map writer establishes requested mode before native construction callback");
   MapNativeBoundary.BuildingResults.Enqueue(Native("{\"anchorCount\":1,\"attempted\":1,\"succeeded\":1,\"planned\":1,\"failed\":0,\"results\":[{\"x\":10,\"y\":20,\"planned\":true,\"blueprintPlaced\":true,\"id\":103}]}"));
   var args=Parent();args["instantBuild"]=instant;args["allowSandbox"]=true;
   var result=Parsed(WorldEditorTools.ApplyMapForCountTest(args,Snapshot,Replace("Tile:5 . .")));
   Require((int)result["executedCells"]==1&&MapNativeBoundary.BuildingCalls.Count==1,"actual map construction retains satisfied request-cell accounting");
   Require(DebugHandler.InstantBuildMode==!instant&&DebugHandler.Writes.Count==2&&WorldEditorTools.EnforceNormalMaterialRules&&WorldEditorTools.ActiveConstructionRequest==null,"actual map write restores exact native and typed construction context");
  }}finally{MapNativeBoundary.ModeObserver=null;DebugHandler.Seed(false);}
 }
}
