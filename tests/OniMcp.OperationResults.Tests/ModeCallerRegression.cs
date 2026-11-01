using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
internal static class ModeCallerRegression {
 internal static bool Enabled;private static int calls,checks;private static bool observed;
 private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
 internal static bool TryGetOperation(string name,out McpTool tool){
  tool=new McpTool{Name=name,Parameters=new Dictionary<string,McpToolParameter>(),Handler=Leaf};
  if(name=="coordinate_control")tool=CoordinateControlTools.ControlCoordinate();
  return name=="read_control"||name=="building_control"||name=="coordinate_control";
 }
 private static CallToolResult Leaf(JObject args){calls++;observed=DebugHandler.InstantBuildMode;return CallToolResult.Text(new JObject{{"observedInstantBuild",observed},{"cache",true}}.ToString());}
 internal static CallToolResult Dispatch(string name,JObject args){McpTool tool;if(!TryGetOperation(name,out tool))throw new InvalidOperationException(name);return tool.Handler(args);}
 private static JObject Parent(bool dry=false,bool instant=false)=>new JObject{{"dryRun",dry},{"confirm",true},{"allowSandbox",true},{"instantBuild",instant},{"task","mode caller host verification"}};
 private static void Reset(bool native){DebugHandler.Seed(native);calls=0;observed=false;MapNativeBoundary.BuildingCalls.Clear();MapNativeBoundary.BuildingResults.Clear();MapNativeBoundary.PreflightCalls.Clear();MapNativeBoundary.PreflightHandler=null;MapNativeBoundary.FactoryContexts.Clear();MapNativeBoundary.ModeObserver=null;MapNativeBoundary.ModeAllowPreview=false;}
 private static void BlueprintCases(){
  try{foreach(bool instant in new[]{false,true})foreach(bool rejected in new[]{false,true}){
   Reset(!instant);BlueprintModeBoundary.Uses=0;BlueprintModeBoundary.FailUse=rejected;BlueprintModeBoundary.Observer=()=>Check(DebugHandler.InstantBuildMode==instant&&WorldEditorTools.EnforceNormalMaterialRules==!instant,"full UseBlueprint establishes requested mode at third-party native boundary");
   var args=Parent(false,instant);args["name"]="fixture";args["x"]=2;args["y"]=3;
   var result=WorldEditorTools.HostBlueprintMode(args);
   Check(result.IsError==rejected&&BlueprintModeBoundary.Uses==1&&DebugHandler.InstantBuildMode==!instant&&DebugHandler.Writes.Count==2&&WorldEditorTools.ActiveConstructionRequest==null,"full UseBlueprint restores exact context after native success/refusal");
  }
  Reset(true);BlueprintModeBoundary.Uses=0;var preview=Parent(true);preview["name"]="fixture";preview["x"]=2;preview["y"]=3;var read=WorldEditorTools.HostBlueprintMode(preview);
  Check(!read.IsError&&BlueprintModeBoundary.Uses==0&&DebugHandler.Writes.Count==0&&DebugHandler.InstantBuildMode,"blueprint syntax/bounds preview performs no game mode assignments or third-party use");
  }finally{BlueprintModeBoundary.Observer=null;BlueprintModeBoundary.FailUse=false;}
 }
 internal static void Run(){
  Enabled=true;try{
   Reset(true);var read=WorldEditorTools.ControlWorldEditor().Handler(new JObject{{"command","read"},{"path","/active/ops/coordinate.md"},{"task","read advertised operation docs"}});
   Check(!read.IsError&&DebugHandler.Writes.Count==0&&DebugHandler.InstantBuildMode,"actual outer read command must not change native mode");
   Reset(true);var operation=WorldEditorTools.ApplyOperationForTest(Parent(),"ops/read.md","call domain=world action=current detail=true");
   Check(!operation.IsError&&calls==1&&observed&&DebugHandler.Writes.Count==0,"actual ops read adapter observes native mode with zero mode assignments");
   Reset(false);var payload=new JObject{{"domain","planning"},{"action","connect_existing"},{"validateOnly",true}}.ToString(Newtonsoft.Json.Formatting.None);
   operation=WorldEditorTools.ApplyOperationForTest(Parent(),"ops/coordinate.md","call targetTool=building_control payload="+payload+" x=2 y=2 detail=true");
   Check(!operation.IsError&&calls==1&&!observed&&DebugHandler.Writes.Count==0,"actual ops and coordinate gateway preserve advertised alias preflight mode (leaf boundary substituted)");

   Reset(false);MapNativeBoundary.ModeAllowPreview=true;MapNativeBoundary.BuildingResults.Enqueue(CallToolResult.Text("{\"valid\":true}"));
   MapNativeBoundary.ModeObserver=args=>{Check(!DebugHandler.InstantBuildMode&&WorldEditorTools.EnforceNormalMaterialRules,"matching normal .oni preflight observes current native mode and normal material policy");};
   var preview=WorldEditorTools.HostModePreflight(Parent(true),"buildings/plans.oni","Tile");
   Check(!preview.IsError&&MapNativeBoundary.BuildingCalls.Count==1&&DebugHandler.Writes.Count==0,"actual .oni preflight reaches child without changing native mode");
   Reset(false);MapNativeBoundary.ModeAllowPreview=true;
   var future=WorldEditorTools.HostModePreflight(Parent(true,true),"buildings/plans.oni","Tile");
   Check(future.IsError&&MapNativeBoundary.BuildingCalls.Count==0&&DebugHandler.Writes.Count==0
    &&JObject.Parse(future.Content[0].Text)["assessmentState"].Value<string>()=="unknown","actual .oni future-mode preflight returns unknown before child or mode assignments");

   foreach(bool instant in new[]{false,true}){
    Reset(!instant);MapNativeBoundary.BuildingResults.Enqueue(CallToolResult.Text("{\"ok\":true,\"planned\":1}"));
    MapNativeBoundary.ModeObserver=args=>{Check(DebugHandler.InstantBuildMode==instant&&WorldEditorTools.EnforceNormalMaterialRules==!instant,"actual .oni write establishes requested authorized material/build mode");};
    var written=WorldEditorTools.HostModeApply(Parent(false,instant),"buildings/plans.oni","Tile");
    Check(!written.IsError&&MapNativeBoundary.BuildingCalls.Count==1&&DebugHandler.InstantBuildMode==!instant&&WorldEditorTools.EnforceNormalMaterialRules
     &&DebugHandler.Writes.Count==2,"actual .oni write restores preexisting native mode");
   }
   BlueprintCases();
   foreach(string lang in new[]{"en","ja","zh-Hans"})foreach(string key in new[]{"construction_mode.future_preflight_unavailable","construction_mode.instant_consent_required"})
    Check(OniMcp.Support.McpLocalization.GetForLanguage(lang,key)!=key,"new scope locale embedded: "+lang+"/"+key);
   Console.WriteLine("PASS "+checks+" complete world-editor caller mode-scope checks");
  }finally{Enabled=false;MapNativeBoundary.ModeObserver=null;MapNativeBoundary.ModeAllowPreview=false;}
 }
}
namespace OniMcp.Tools {
 public static partial class WorldEditorTools{
  internal static CallToolResult HostModePreflight(JObject args,string relative,string replacement){return PreflightBuildEdit(args,relative,replacement);}
  internal static CallToolResult HostModeApply(JObject args,string relative,string replacement){return ApplyBuildEdit(args,relative,replacement);}
 }
}
