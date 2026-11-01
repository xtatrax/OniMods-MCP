using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using OniMcp.Support;
internal static class Program {
 static int checks,called;static bool observed;
 static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
 static BuildingDef Setup(bool nativeInstant,bool sandbox,float stock=0){
  Game.Instance=new Game{SandboxModeActive=sandbox};DebugHandler.Seed(nativeInstant);Boundary.Reset();called=0;observed=false;
  ClusterManager.Instance=new ClusterManager{World=new WorldContainer{id=0,worldInventory=new WorldInventory{NativeAmount=stock}}};
  ElementLoader.elements.Clear();ElementLoader.elements.Add(new Element{id="Cuprite",Category=new Tag("Metal")});Components.Pickupables.Items.Clear();
  return new BuildingDef{PrefabID="Fixture",MaterialCategory=new[]{"Metal"},Defaults=new List<Tag>{new Tag("Cuprite")},AvailableFacades=new[]{"FixtureFacade"}};
 }
 static JObject Request(string domain="planning",string action="preview",bool instant=false,bool dry=true)=>new JObject{{"domain",domain},{"action",action},{"dryRun",dry},{"confirm",true},{"allowSandbox",true},{"instantBuild",instant}};
 static CallToolResult ReadBoundary(){called++;observed=DebugHandler.InstantBuildMode;return CallToolResult.Text("observed");}
 static void ReadCanary(){
  Setup(true,true);var result=WorldEditorTools.HostScope(Request("world","cell_info",false,false),ReadBoundary,"read_control");
  Check(DebugHandler.Writes.Count==0,"passive read must not assign native instant-build flag");
  Check(called==1&&observed&&DebugHandler.InstantBuildMode&&!result.IsError,"passive read must preserve the native opinion during its callback");
 }
 static void ColdMaterial(){
  var def=Setup(true,true);
  Check(WorldEditorTools.ActiveConstructionRequest==null,"cold direct material reader has no active write request");
  Check(WorldEditorTools.EnforceNormalMaterialRules,"cold material reader must start with normal material policy");
  var material=BuildPlanningTools.HostMaterialJson(BuildPlanningTools.HostMaterial(def,"Cuprite",0));
  Check(!(bool)material["valid"]&&!BuildPlanningTools.HostFreeContext(),"cold direct actual material reader cannot treat Sandbox/Instant as a stock bypass");
  Check(DebugHandler.InstantBuildMode&&DebugHandler.Writes.Count==0,"cold direct material read leaves game instant mode untouched");
 }
 static void ReadCases(){
  foreach(bool sandbox in new[]{false,true})foreach(bool native in new[]{false,true})foreach(var entry in new[]{
   new[]{"read_control","world","cell_info"},new[]{"read_control","infrastructure","ports"},new[]{"read_control","buildings","specs"},
   new[]{"building_control","geometry","definition"},new[]{"building_control","planning","preview"},new[]{"building_control","planning","materials"},
   new[]{"building_control","planning","placement_candidates"},new[]{"building_control","planning","parse_plan"},new[]{"colony_control","snapshot","get"},
   new[]{"colony_control","diagnostic","diagnostics"},new[]{"game_control","sandbox","list_actions"},new[]{"server_control","diagnostics","health"}}){
   Setup(native,sandbox);var request=Request(entry[1],entry[2],false,false);
   var result=WorldEditorTools.HostScope(request,ReadBoundary,entry[0]);
   bool placement=entry[0]=="building_control"&&(entry[2]=="preview"||entry[2]=="placement_candidates");
   Check(placement&&native?called==0&&result.IsError:called==1&&!result.IsError&&observed==native,entry[0]+"/"+entry[2]+": matching current mode is observed; mismatching placement policy is unknown, never simulated");
   Check(DebugHandler.Writes.Count==0&&DebugHandler.InstantBuildMode==native&&WorldEditorTools.EnforceNormalMaterialRules,entry[0]+"/"+entry[2]+": no native assignments or policy leak");
  }
  Setup(true,false);var ui=WorldEditorTools.HostScope(Request("planning","native_ui_preview",false,false),ReadBoundary,"building_control");
  Check(!ui.IsError&&called==1&&observed&&DebugHandler.Writes.Count==0,"explicit native UI action observes current mode, not a forced normal-mode simulation");
  Setup(true,true);WorldEditorTools.CommandBoundary=args=>ReadBoundary();WorldEditorTools.HostOuter(new JObject{{"command","read"}});
  Check(called==1&&observed&&DebugHandler.Writes.Count==0,"outer world-editor dispatcher never changes game mode for file reads");
 }
 static void FuturePreflight(){
  foreach(bool sandbox in new[]{false,true})foreach(string action in new[]{"preview","build_area","placement_candidates","connect_existing"}){
   Setup(false,sandbox);var request=Request("planning",action,true,true);
   var result=WorldEditorTools.HostScope(request,ReadBoundary,"building_control");var data=JObject.Parse(result.Content[0].Text);
   Check(result.IsError&&called==0&&data["valid"].Type==JTokenType.Null&&data["availableNow"].Type==JTokenType.Null,
    action+": unestablished future instant mode is unknown, not locked or native-valid");
   Check((string)data["assessmentState"]=="unknown"&&(string)data["reasonCode"]=="construction_mode_unobserved"
    &&!(bool)data["currentInstantBuildMode"]&&DebugHandler.Writes.Count==0&&!DebugHandler.InstantBuildMode,
    action+": pure preflight never flips a native mode to validate a future request");
  }
  foreach(bool sandbox in new[]{false,true})foreach(bool omit in new[]{false,true}){
   Setup(true,sandbox);var request=Request("planning","preview",false,true);if(omit)request.Remove("instantBuild");
   var result=WorldEditorTools.HostScope(request,ReadBoundary,"building_control");var data=JObject.Parse(result.Content[0].Text);
   Check(result.IsError&&called==0&&data["valid"].Type==JTokenType.Null&&!(bool)data["requestedInstantBuild"]&&(bool)data["currentInstantBuildMode"],"future normal/OFF policy is unknown for default and explicitfalse without simulating it");
   Check(DebugHandler.Writes.Count==0&&DebugHandler.InstantBuildMode,"symmetric future OFF preflight performs zero native assignments");
  }
 }
 static void Writes(){
  foreach(bool sandbox in new[]{false,true}){
   var def=Setup(true,sandbox);var normal=WorldEditorTools.HostScope(Request("planning","build_area",false,false),()=>{
    called++;Check(!DebugHandler.InstantBuildMode&&WorldEditorTools.EnforceNormalMaterialRules,"actual ordinary construction establishes normal mode despite native initial Instant");
    Check(!(bool)BuildPlanningTools.HostMaterialJson(BuildPlanningTools.HostMaterial(def,"Cuprite",0))["valid"],"actual ordinary no-stock construction keeps normal material safeguard");return CallToolResult.Text("normal");},"building_control");
   Check(!normal.IsError&&called==1&&DebugHandler.InstantBuildMode&&WorldEditorTools.EnforceNormalMaterialRules&&DebugHandler.Writes.SequenceEqual(new[]{false,true}),"actual normal construction restores preexisting native and material policy");
   Check(WorldEditorTools.ActiveConstructionRequest==null,"ordinary construction restores exact absent typed request");

   def=Setup(false,sandbox);var instant=WorldEditorTools.HostScope(Request("planning","build_area",true,false),()=>{
    called++;Check(DebugHandler.InstantBuildMode&&!WorldEditorTools.EnforceNormalMaterialRules,"authorized actual instant construction establishes native instant context");
    Check((bool)BuildPlanningTools.HostMaterialJson(BuildPlanningTools.HostMaterial(def,"Cuprite",0))["valid"],"authorized instant construction with zero stock still reaches real material bypass");
    int before=DebugHandler.Writes.Count;var nested=WorldEditorTools.HostScope(Request("planning","build_area",true,true),()=>{
     Check(DebugHandler.InstantBuildMode&&!WorldEditorTools.EnforceNormalMaterialRules,"required internal preflight sees the authorized active write context");
     Check((bool)BuildPlanningTools.HostMaterialJson(BuildPlanningTools.HostMaterial(def,"Cuprite",0))["valid"],"authorized internal no-stock preflight remains valid");return CallToolResult.Text("internal");},"building_control");
    Check(!nested.IsError&&DebugHandler.Writes.Count==before,"internal preflight performs no additional native mode assignments");
    var generated=Request("planning","build_area",true,true);generated.Remove("instantBuild");generated.Remove("allowSandbox");generated["confirm"]=false;
    var inherited=ConstructionModeObservation.Read(generated);
    Check(inherited.MatchesRequestedPolicy&&inherited.Request.InstantBuild&&inherited.Request.Origin=="active_write_scope","generated template child inherits only the active authorized typed request");
    var child=WorldEditorTools.HostScope(generated,()=>CallToolResult.Text("generated internal"),"building_control");
    Check(!child.IsError&&DebugHandler.Writes.Count==before,"generated internal preflight remains allowed without native assignments");
    return CallToolResult.Text("instant");},"building_control");
   Check(!instant.IsError&&called==1&&!DebugHandler.InstantBuildMode&&WorldEditorTools.EnforceNormalMaterialRules&&DebugHandler.Writes.SequenceEqual(new[]{true,false}),"authorized actual instant write restores native/material context");
   Check(WorldEditorTools.ActiveConstructionRequest==null,"instant construction restores exact absent typed request");
  }
  Setup(false,false);bool threw=false;try{WorldEditorTools.HostScope(Request("planning","build_area",true,false),()=>{throw new InvalidOperationException("fixture write exception");},"building_control");}catch(InvalidOperationException){threw=true;}
  Check(threw&&!DebugHandler.InstantBuildMode&&WorldEditorTools.EnforceNormalMaterialRules&&DebugHandler.Writes.SequenceEqual(new[]{true,false}),"write exception restores both mode and material policy");
  Check(WorldEditorTools.ActiveConstructionRequest==null,"write exception restores exact absent typed request");
  Setup(false,false);WorldEditorTools.HostScope(Request("planning","build_area",true,false),()=>{
   var outerRequest=WorldEditorTools.ActiveConstructionRequest;
   Check(outerRequest!=null&&outerRequest.InstantBuild,"outer construction owns a typed write request");
   WorldEditorTools.HostScope(Request("planning","build_area",false,false),()=>{Check(!DebugHandler.InstantBuildMode&&WorldEditorTools.EnforceNormalMaterialRules,"nested normal construction temporarily overrides instant context");return CallToolResult.Text("inner");},"building_control");
   Check(DebugHandler.InstantBuildMode&&!WorldEditorTools.EnforceNormalMaterialRules,"nested construction restores enclosing authorized instant context");
   Check(object.ReferenceEquals(outerRequest,WorldEditorTools.ActiveConstructionRequest),"nested construction restores the exact enclosing typed request object");return CallToolResult.Text("outer");},"building_control");
  Check(!DebugHandler.InstantBuildMode&&WorldEditorTools.EnforceNormalMaterialRules&&DebugHandler.Writes.SequenceEqual(new[]{true,false,true,false}),"nested scopes restore the initial native state in exact order");
  Check(WorldEditorTools.ActiveConstructionRequest==null,"nested scopes restore exact absent typed request");
  foreach(string missing in new[]{"allowSandbox","confirm"}){
   Setup(false,false);var request=Request("planning","build_area",true,false);request[missing]=false;var result=WorldEditorTools.HostScope(request,ReadBoundary,"building_control");
   Check(result.IsError&&called==0&&DebugHandler.Writes.Count==0,"missing "+missing+" consent refuses actual instant construction without assignments");
  }
  Check(WorldEditorBuildModePolicy.RequiresScope("building_control",Request("planning","future_unknown_action",false,true)),"unknown planning action is not silently classified passive");
 }
 static void CoordinateCases(){
  Setup(false,true);OniToolRegistry.Tools["building_control"]=new McpTool{Handler=ReadBoundaryAdapter};
  var payload=Request("planning","connect_existing",false,false);payload["validateOnly"]=true;
  var wrapper=new JObject{{"targetTool","building_control"},{"payload",payload},{"dryRun",false},{"confirm",true},{"x",2},{"y",2}};
  string before=wrapper.ToString();var result=WorldEditorTools.HostScope(wrapper,()=>CoordinateControlTools.ControlCoordinate().Handler(wrapper),"coordinate_control");
  Check(!result.IsError&&called==1&&!observed&&DebugHandler.Writes.Count==0,"formal advertised validateOnly alias routes preflight without native mode writes");
  Check(wrapper.ToString()==before&&Boundary.UiCalls==0,"scope description/actual gateway preserve input and never move camera for read target");
  Setup(true,true);wrapper["payload"]["dryRun"]=true;
  result=WorldEditorTools.HostScope(wrapper,()=>CoordinateControlTools.ControlCoordinate().Handler(wrapper),"coordinate_control");
  Check(result.IsError&&called==0&&DebugHandler.Writes.Count==0,"conflicting coordinate dryRun remains rejected without native mode writes");
 }
 static CallToolResult ReadBoundaryAdapter(JObject args){Check((bool)args["validateOnly"]&&!(bool)args["dryRun"],"effective coordinate flags preserve advertised alias rather than rewriting dryRun");return ReadBoundary();}
 static void UnknownPreview(){
  foreach(bool native in new[]{true,false})foreach(bool alias in new[]{false,true})foreach(bool sandbox in new[]{false,true}){
   Setup(native,sandbox);var request=Request("planning","future_unknown_action",false,!alias);if(alias)request["validateOnly"]=true;
   var result=WorldEditorTools.HostScope(request,ReadBoundary,"building_control");
   Check(result.IsError&&called==0&&DebugHandler.Writes.Count==0,"unknown preview must fail before invoking router or writing native mode");
   var data=JObject.Parse(result.Content[0].Text);Check((string)data["reasonCode"]=="unsupported_preview_action"&&data["nativePlacementValid"].Type==JTokenType.Null&&(string)data["validation"]["coverage"]=="not_evaluated","unknown preview refusal never invents native eligibility or performed checks");
   Check(DebugHandler.InstantBuildMode==native&&WorldEditorTools.ActiveConstructionRequest==null,"unknown preview preserves exact native and typed context");
  }
  Setup(true,true);var wrapped=new JObject{{"targetTool","building_control"},{"payload",new JObject{{"domain","planning"},{"action","future_unknown_action"},{"validateOnly",true}}},{"dryRun",false},{"confirm",true}};
  OniToolRegistry.Tools["building_control"]=new McpTool{Name="building_control",Handler=ReadBoundaryAdapter};
  var refusal=WorldEditorTools.HostScope(wrapped,ReadBoundary,"coordinate_control");Check(refusal.IsError&&called==0&&DebugHandler.Writes.Count==0,"formal coordinate unknown validateOnly alias stops before router/native assignments");
  Setup(true,true);var normalWrite=WorldEditorTools.HostScope(Request("planning","future_unknown_action",false,false),ReadBoundary,"building_control");
  Check(called==1&&DebugHandler.Writes.Count==2&&DebugHandler.InstantBuildMode,"unknown non-preview write retains conservative normal material enforcement and restoration");
 }
 static void SandboxPreview(){
  foreach(bool native in new[]{true,false})foreach(bool sandbox in new[]{false,true})foreach(string domain in new[]{"sandbox","sandbox_tools","debug"})foreach(string kind in new[]{"map_designate","designate","search_designate","map"})foreach(bool omitted in new[]{false,true}){
   Setup(native,sandbox);var request=Request(domain,"replace",false,true);request["kind"]=kind;if(omitted)request.Remove("dryRun");
   var result=WorldEditorTools.HostScope(request,ReadBoundary,"game_control");
   Check(!result.IsError&&called==1&&observed==native&&DebugHandler.Writes.Count==0,"sandbox map preview including omitted default observes native mode without assignments");
   Check(DebugHandler.InstantBuildMode==native&&WorldEditorTools.ActiveConstructionRequest==null,"sandbox map preview preserves native/typed state");
  }
  foreach(bool native in new[]{true,false})foreach(string kind in new[]{"read","info","area"}){
   Setup(native,true);var request=Request("sandbox",kind=="area"?"flood_fill":"list_actions",false,kind=="area");request["kind"]=kind;
   var result=WorldEditorTools.HostScope(request,ReadBoundary,"game_control");Check(!result.IsError&&called==1&&observed==native&&DebugHandler.Writes.Count==0,"sandbox read/info/flood preview is not native mode simulation");
  }
  foreach(string kind in new[]{"entity","entities","area","future_unknown_kind","read"}){
   Setup(true,true);var request=Request("sandbox","future_unsupported",false,true);request["kind"]=kind;
   var result=WorldEditorTools.HostScope(request,ReadBoundary,"game_control");Check(result.IsError&&called==0&&DebugHandler.Writes.Count==0&&DebugHandler.InstantBuildMode,"unsupported sandbox preview stops before router/native assignments");
   Check((string)((JObject)result.Content[0].Meta[PreviewValidationObservation.MetadataKey])["coverage"]=="not_evaluated","unsupported sandbox preview never claims performed validation");
  }
  foreach(bool instant in new[]{false,true}){
   Setup(!instant,true);var request=Request("sandbox","paint",instant,false);request["kind"]="area";
   var result=WorldEditorTools.HostScope(request,()=>{Check(DebugHandler.InstantBuildMode==instant&&WorldEditorTools.EnforceNormalMaterialRules==!instant,"actual sandbox writer preserves requested native/material mode");return CallToolResult.Text("actual sandbox boundary");},"game_control");
   Check(!result.IsError&&DebugHandler.InstantBuildMode==!instant&&DebugHandler.Writes.Count==2&&WorldEditorTools.ActiveConstructionRequest==null,"actual sandbox write restores exact initial mode/context");
  }
 }
 static void Main(string[] args){
  if(args.Length>0&&args[0]=="--sandbox-preview-only"){SandboxPreview();Console.WriteLine("PASS sandbox preview mode isolation");return;}
  if(args.Length>0&&args[0]=="--unknown-preview-only"){UnknownPreview();Console.WriteLine("PASS unknown preview isolation canary");return;}
  if(args.Length>0&&args[0]=="--read-canary-only"){ReadCanary();Console.WriteLine("PASS passive-read native-assignment canary");return;}
  SandboxPreview();foreach(string lang in new[]{"en","ja","zh-Hans"})Check(McpLocalization.GetForLanguage(lang,"construction_mode.unsupported_sandbox_preview")!="construction_mode.unsupported_sandbox_preview","owned sandbox preview rejection resolves in "+lang);UnknownPreview();foreach(string lang in new[]{"en","ja","zh-Hans"})Check(OniMcp.Support.McpLocalization.GetForLanguage(lang,"construction_mode.unsupported_preview_action")!="construction_mode.unsupported_preview_action","owned unknown preview error resolves in "+lang);ColdMaterial();ReadCanary();ReadCases();FuturePreflight();Writes();CoordinateCases();
  ActualMaterialRegression.Run(new string[0]);Console.WriteLine("PASS "+checks+" actual scope/material/gateway read-safety checks");
 }
}
