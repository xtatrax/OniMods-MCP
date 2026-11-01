using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
namespace OniMcp.Tools
{
 public static partial class WorldEditorTools
 {
  internal static ConstructionModeRequest FixtureModeRequest;
  internal static ConstructionModeRequest ActiveConstructionRequest {get{return FixtureModeRequest;}}
  internal static bool EnforceNormalMaterialRules {get{return FixtureModeRequest==null||!FixtureModeRequest.InstantBuild;}}
 }
 public static partial class BuildPlanningTools
 {
  internal static JObject ParentForMode(JObject args){return JObject.Parse(JsonConvert.SerializeObject(TryPlanOne("ModeFixture",0,0,args),OniMcp.Support.McpJsonUtil.Settings));}
 }
}
internal static class ModeObservationRegression
{
 private static int checks;private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException("Construction mode observation: "+reason);}
 private static BuildingDef Setup(bool current,bool? active)
 {
  BlueprintCompletionRegression.Setup();BuildPlanningTools.ResetPlannerFixture();DebugHandler.InstantBuildMode=current;WorldEditorTools.FixtureModeRequest=active.HasValue?ConstructionModeRequest.Resolve(new JObject{["instantBuild"]=active.Value},null):null;WorldEditor.Area=null;UnityEngine.Object.ThrowEnumeration=false;
  var def=new BuildingDef{PrefabID="ModeFixture",Name="Mode fixture",BuildLocationRule=BuildLocationRule.Anywhere};BuildPlanningTools.FixtureDefs[def.PrefabID]=def;BuildPlanningTools.FixtureResolveCalls=0;return def;
 }
 private static JObject Args(bool? requested){var args=new JObject{["dryRun"]=true,["worldId"]=0,["material"]="Cuprite"};if(requested.HasValue)args["instantBuild"]=requested.Value;return args;}
 private static void Unknown(JObject value,bool current,bool requested)
 {
  Check(!(bool)value["valid"]&&(string)value["details"]["assessmentState"]=="unknown"&&(string)value["details"]["reasonCode"]=="construction_mode_unobserved","mode mismatch is MCP refusal with explicit unknown native assessment");
  Check((bool)value["details"]["currentInstantBuildMode"]==current&&(bool)value["details"]["requestedInstantBuild"]==requested,"observed native/requested booleans remain exact");
  Check(value["details"]["nativePlacementValid"].Type==JTokenType.Null&&value["details"]["availableNow"].Type==JTokenType.Null,"native eligibility/availability remain explicit null, not false");
  Check((string)value["validation"]["validationScope"]=="placement_mode_observation"&&(string)value["validation"]["coverage"]=="partial","source stage identifies mode observation rather than placement validation");
  Check(value["validation"]["reportedFacts"]["details.nativePlacementValid"].Type==JTokenType.Null&&value["validation"]["reportedFacts"]["details.availableNow"].Type==JTokenType.Null,"scope facts preserve unknown native values");
 }
 internal static void Run()
 {
  foreach(var scenario in new[]{new{Current=true,Requested=(bool?)null},new{Current=true,Requested=(bool?)false},new{Current=false,Requested=(bool?)true}})
  {
   var def=Setup(scenario.Current,null);var args=Args(scenario.Requested);var before=args.DeepClone();var active=WorldEditorTools.FixtureModeRequest;
   var value=BuildPlanningTools.ParentForMode(args);Unknown(value,scenario.Current,scenario.Requested??false);
   Check(def.FixtureAvailabilityReads==0&&BuildPlanningTools.FixtureResolveCalls==0&&BuildPlanningTools.FixtureSelectCalls==0&&BuildPlanningTools.FixtureSupportCalls==0,"direct guard runs before definition/eligibility/material/support reads");
   Check(DebugHandler.InstantBuildMode==scenario.Current&&ReferenceEquals(active,WorldEditorTools.FixtureModeRequest)&&JToken.DeepEquals(args,before),"read guard does not set native mode, typed context or arguments");
   def=Setup(scenario.Current,null);args=Args(scenario.Requested);args["domain"]="planning";args["action"]="placement_candidates";args["prefabId"]=def.PrefabID;args["x1"]=0;args["y1"]=0;args["x2"]=3;args["y2"]=0;args["maxEvaluations"]=2;args["scanOffset"]=1;args["limit"]=2;
   var response=BuildPlanningTools.FindPlacementCandidates().Handler(args);var window=JObject.Parse(response.Content[0].Text);
   Check(!response.IsError&&(int)window["unknownCount"]==2&&(int)window["knownRejectedCount"]==0&&(string)window["validation"]["coverage"]=="partial","actual public inline candidate leaf keeps mode mismatch rows unknown within bounded window");
   Check(def.FixtureAvailabilityReads==0&&BuildPlanningTools.FixtureResolveCalls==1&&BuildPlanningTools.FixtureSelectCalls==0&&BuildPlanningTools.FixtureSupportCalls==0,"candidate header resolves cached identity once; no parent eligibility/material read follows mismatch");
   foreach(var row in (JArray)window["candidates"]){Unknown((JObject)row["preview"],scenario.Current,scenario.Requested??false);Check((string)row["status"]=="unknown","candidate classification preserves mode unknown");}
   Check(def.DirectBuildCalls==0&&def.TryPlaceCalls==0&&BuildTool.Instance.BuildCalls==0&&DebugHandler.InstantBuildMode==scenario.Current,"mismatch candidate observation invokes no game/UI action or native mode write");
  }
  var matching=Setup(false,null);var normal=BuildPlanningTools.ParentForMode(Args(null));Check((string)normal["validation"]["validationScope"]=="placement_preflight"&&matching.FixtureAvailabilityReads==1,"already matching normal policy enters ordinary preflight");
  matching=Setup(true,true);var inherited=BuildPlanningTools.ParentForMode(Args(null));Check((string)inherited["validation"]["validationScope"]=="placement_preflight"&&matching.FixtureAvailabilityReads==1,"authorized active write request can supply matching inherited instant mode");
  matching=Setup(true,true);var explicitNormal=BuildPlanningTools.ParentForMode(Args(false));Unknown(explicitNormal,true,false);Check((string)explicitNormal["details"]["requestModeOrigin"]=="explicit"&&matching.FixtureAvailabilityReads==0,"explicit false overrides active instant request without reads");
  matching=Setup(true,null);var policyMismatch=BuildPlanningTools.ParentForMode(Args(true));Unknown(policyMismatch,true,true);Check((bool)policyMismatch["details"]["modeMatches"]&&!(bool)policyMismatch["details"]["materialPolicyMatches"],"matching native flag alone cannot manufacture matching material policy");
  Console.WriteLine("Construction mode observation: "+checks+" full actual helper/parent/candidate checks PASS (active-policy/native inputs substituted; full policy write/restore proof belongs to mode owner)");
 }
}
