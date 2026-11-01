using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
namespace OniMcp.Tools
{
 public static partial class WorldEditorTools
 {
  internal static CallToolResult PreflightMapForValidation(JObject args,string search,string replace){return PreflightMapEdit(args,search,replace);}
  internal static CallToolResult PreflightBuildForValidation(JObject args,string file,string text){return PreflightBuildEdit(args,file,text);}
 }
}
internal static class EditorValidationScopeRegression
{
 private static int checks;
 private const string Snapshot="百位X: 0 0 0\n十位X: 1 1 1\n个位X: 0 1 2\nY=20: . . .";
 private const string Replacement="百位X: 0 0 0\n十位X: 1 1 1\n个位X: 0 1 2\nY=20: Tile:5 . .";
 private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException("Editor validation scope: "+reason);}
 private static JObject Parent(bool preview=true){return new JObject{["task"]="Host validation metadata",["dryRun"]=preview,["confirm"]=!preview,["sourcePath"]="/active/map/viewport.md"};}
 private static JObject Parsed(CallToolResult result){return JObject.Parse(result.Content[0].Text);}
 private static JObject Scope(JObject result,string scope,string coverage)
 {
  Check(result["validation"] is JObject,"actual editor source must provide canonical scope");var value=(JObject)result["validation"];
  Check((string)value["validationScope"]==scope&&(string)value["coverage"]==coverage,"expected exact editor validation scope/coverage");return value;
 }
 private static void Setup()
 {
  DebugHandler.Seed(false);
  Check(WorldEditorTools.ActiveConstructionRequest==null,"metadata fixture starts without leaked construction request");
  MapNativeBoundary.ModeObserver=null;MapNativeBoundary.PreflightHandler=null;MapNativeBoundary.PreflightCalls.Clear();MapNativeBoundary.FactoryContexts.Clear();
  MapNativeBoundary.Snapshot=Snapshot;MapNativeBoundary.BuildingCalls.Clear();MapNativeBoundary.BuildingResults.Clear();MapNativeBoundary.OrderCalls.Clear();MapNativeBoundary.OrderResults.Clear();MapNativeBoundary.WorldResolutionCalls.Clear();MapNativeBoundary.WorldResolver=null;MapNativeBoundary.AllowPlacementPreview=false;
  Assets.MissingDefinitionLookup=false;MapNativeBoundary.LayerObservation=new Dictionary<string,object>{{"status","disabled"}};Assets.BuildingDefs.Clear();Assets.BuildingDefs.Add(new BuildingDef{PrefabID="Tile",Name="Tile",Shape=new NativeShape{Size=new[]{1,1},Bounds=new[]{0,0,0,0},Cells=new List<NativePoint>{new NativePoint{X=0,Y=0}}}});NativeBoundary.Reset("{}",false);
 }
 internal static void MapCases()
 {
  Setup();var args=Parent();var before=args.DeepClone();var result=Parsed(WorldEditorTools.ApplyMapForCountTest(args,Snapshot,Replacement));var scope=Scope(result,"syntax_and_footprint_shape","partial");
  Check((int)result["childCalls"]==0&&!(bool)result["placementValidated"]&&MapNativeBoundary.BuildingCalls.Count==0&&MapNativeBoundary.OrderCalls.Count==0,"outer map preview performs syntax/shape only with no child dispatch");
  Check(JToken.DeepEquals(args,before),"map metadata does not mutate request flags/payload");
  Setup();MapNativeBoundary.LayerObservation=new Dictionary<string,object>{{"status","observed"},{"additionalRead",true}};
  result=Parsed(WorldEditorTools.PreflightMapForValidation(Parent(),Snapshot,Replacement));Scope(result,"syntax_and_footprint_shape",
#if PUBLIC_EDITOR
"partial"
#else
"complete"
#endif
);Check((int)result["childCalls"]==0&&!(bool)result["placementValidated"],"fully observed narrow map shape scope still does not validate placement");
  Setup();result=Parsed(WorldEditorTools.PreflightMapForValidation(Parent(),Snapshot,Replacement));Scope(result,"syntax_and_footprint_shape","partial");
  Setup();Assets.MissingDefinitionLookup=true;MapNativeBoundary.LayerObservation=new Dictionary<string,object>{{"status","observed"},{"additionalRead",true}};
  result=Parsed(WorldEditorTools.PreflightMapForValidation(Parent(),Snapshot,Replacement));Scope(result,"syntax_and_footprint_shape","partial");Check((int)result["childCalls"]==0&&!(bool)result["placementValidated"],"missing native definition never becomes completed footprint coverage from observed layers");
  Setup();args=Parent();args["path"]="/active/map/viewport.md";args["content"]="<<<<<<< SEARCH\n"+Snapshot+"\n=======\n"+Replacement+"\n>>>>>>> REPLACE";
  result=Parsed(WorldEditorTools.EditForCountTest(args));Scope(result,"syntax_and_footprint_shape","partial");Check((int)result["childCalls"]==0&&!(bool)result["placementValidated"]&&MapNativeBoundary.BuildingCalls.Count==0,"outer search/replace preserves map preflight scope rather than claiming placement");
 }
 internal static void OperationCases()
 {
  Setup();const string line="call domain=skill action=list detail=true";var args=Parent();var result=Parsed(WorldEditorTools.ApplyOperationForTest(args,"ops/dupes.md",line));Scope(result,"syntax_and_capability_only","complete");Check((int)result["childCalls"]==0&&NativeBoundary.Calls==0,"outer operation preview parses/capability-checks without child invocation");
  Setup();args=Parent(false);result=Parsed(WorldEditorTools.ApplyOperationForTest(args,"ops/dupes.md",line+" dryRun=true"));Scope(result,"child_not_invoked","not_evaluated");Check((int)result["childCalls"]==0&&(int)result["executedCommands"]==0&&NativeBoundary.Calls==0,"per-line preview is skipped rather than actual native validation");
  Check((string)result["results"][0]["validation"]["validationScope"]=="child_not_invoked","skipped row carries its exact source scope");
  Setup();args=Parent(false);var native=Leaf(true);string raw=native.Content[0].Text;string source=JsonConvert.SerializeObject(native);NativeBoundary.Results.Enqueue(native);
  result=Parsed(WorldEditorTools.ApplyOperationForTest(args,"ops/dupes.md",line));Scope(result,"placement_preflight","partial");Check((int)result["childCalls"]==1&&NativeBoundary.Calls==1,"actual ops callback is counted only inside mode wrapper lambda");Check((string)result["results"][0]["result"]==raw&&JsonConvert.SerializeObject(native)==source,"actual raw child text/content/meta/policy remain unchanged");
 }
 private static CallToolResult Leaf(bool valid)
 {
  var value=new PreviewValidationObservation("placement","placement_preflight","partial",false,false,"valid","support.nativeFoundationValid").Attach(CallToolResult.Text(new JObject{["valid"]=valid,["dryRun"]=true,["support"]=new JObject{["nativeFoundationValid"]=JValue.CreateNull()}}.ToString(Formatting.None)));
  value.Content[0].Meta["fixture.extra"]="retained";var policy=ToolContent.CreateText("owned policy fixture",ToolContent.PrintingPodPolicyKind);policy.IsOwnedPrintingPodPolicy=true;value.Content.Add(policy);return value;
 }
 internal static void OniCases()
 {
  Setup();MapNativeBoundary.AllowPlacementPreview=true;var args=Parent();var child=Leaf(true);var original=JsonConvert.SerializeObject(child);MapNativeBoundary.BuildingResults.Enqueue(child);
  var actual=WorldEditorTools.PreflightBuildForValidation(args,"buildings/plans.oni","fixture plan");var payload=Parsed(actual);Scope(payload,"placement_preflight","partial");
  Check(MapNativeBoundary.BuildingCalls.Count==1&&(bool)MapNativeBoundary.BuildingCalls[0]["dryRun"]&&!(bool)MapNativeBoundary.BuildingCalls[0]["confirm"],"normal .oni preflight really invokes the child with preview policy");Check(JsonConvert.SerializeObject(child)==original&&actual.Content.Count==2&&actual.Content[1].IsOwnedPrintingPodPolicy,".oni wrapper retains source, all content, metadata and owned policy");
  Setup();MapNativeBoundary.AllowPlacementPreview=true;args=Parent();args["path"]="/active/buildings/plans.oni";args["content"]="<<<<<<< SEARCH\n=======\nfixture plan\n>>>>>>> REPLACE";MapNativeBoundary.BuildingResults.Enqueue(Leaf(true));
  payload=Parsed(WorldEditorTools.EditForCountTest(args));Scope(payload,"placement_preflight","partial");Check((int)payload["childCalls"]==1,"outer .oni preview preserves actual child validation instead of syntax-only label");
  Setup();MapNativeBoundary.AllowPlacementPreview=true;child=Leaf(false);original=JsonConvert.SerializeObject(child);MapNativeBoundary.BuildingResults.Enqueue(child);actual=WorldEditorTools.PreflightBuildForValidation(Parent(),"buildings/plans.oni","fixture plan");
  Check(actual.IsError&&!child.IsError&&actual.Content.Count==2,"failure promotion changes only the clone error bit and retains policy content");Check(actual.Content[0].Text==child.Content[0].Text&&(string)actual.Content[0].Meta["fixture.extra"]=="retained"&&JsonConvert.SerializeObject(child)==original,"failure promotion preserves exact child text/meta/source");
  MapNativeBoundary.AllowPlacementPreview=false;
 }
 internal static void Run(){MapCases();OperationCases();OniCases();MapNativeBoundary.LayerObservation=new Dictionary<string,object>{{"status","disabled"}};Console.WriteLine("Editor validation scope: "+checks+" actual-source checks PASS (actual mode/metadata bodies; native fields/actions are supplied boundaries)");}
}
