using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;
namespace OniMcp.Tools
{
 public static partial class BuildPlanningTools
 {
  internal static JObject ScopePlan(string id,int cell,JObject args,bool cached)
  {
   PlacementPreviewReadContext context=null;
   if(cached&&FixtureDefs.ContainsKey(id))context=new PlacementPreviewReadContext(FixtureDefs[id],args,0);
   var result=TryPlanOne(id,Grid.CellColumn(cell),Grid.CellRow(cell),args,previewReads:context);
   return JObject.Parse(JsonConvert.SerializeObject(result,OniMcp.Support.McpJsonUtil.Settings));
  }
 }
}
internal static class ScopeParentRegression
{
 private static int checks;
 private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException("Scope parent: "+reason);}
 private static BuildingDef Setup()
 {
  BlueprintCompletionRegression.Setup();DebugHandler.InstantBuildMode=false;WorldEditorTools.FixtureModeRequest=null;BuildPlanningTools.ResetPlannerFixture();UnityEngine.Object.ThrowEnumeration=false;WorldEditor.Area=null;
  var def=new BuildingDef{PrefabID="ScopeFixture",Name="Scope fixture",BuildLocationRule=BuildLocationRule.Anywhere};BuildPlanningTools.FixtureDefs[def.PrefabID]=def;return def;
 }
 private static JObject Args(){return new JObject{["worldId"]=0,["dryRun"]=true,["material"]="Cuprite"};}
 private static JObject Scope(JObject value,string scope,string coverage)
 {
  Check(value["validation"] is JObject,"full actual parent must report source-stage scope");var result=(JObject)value["validation"];
  Check((string)result["requestedScope"]=="placement"&&(string)result["validationScope"]==scope&&(string)result["coverage"]==coverage,"exact source-stage scope/coverage");
  Check(!(bool)result["gameplayMutationRequested"]&&!(bool)result["uiInteractionRequested"],"passive intent is distinct from performed gameplay/UI action");
  Check(result["performed"]==null,"no manufactured performed bool");return result;
 }
 internal static void Run()
 {
  var def=Setup();var result=BuildPlanningTools.ScopePlan(def.PrefabID,0,Args(),true);var scope=Scope(result,"placement_preflight","partial");
  Check((bool)result["valid"]&&(bool)result["dryRun"]&&!(bool)result["planned"]&&!(bool)result["blueprintPlaced"],"compatible preview is not performed construction");
  Check((bool)scope["reportedFacts"]["valid"]&&!(bool)scope["reportedFacts"]["planned"],"reported facts repeat existing booleans");
  Check(scope["reportedFacts"]["support.nativeFoundationValid"]==null,"absent support observation does not become false");
  def=Setup();Db.Seed(null);Research.Instance=null;DebugHandler.InstantBuildMode=false;BuildPlanningTools.FixtureAvailabilityError="fixture availability unknown";
  result=BuildPlanningTools.ScopePlan(def.PrefabID,0,Args(),true);scope=Scope(result,"placement_preflight","partial");
  Check(!(bool)result["valid"]&&scope["reportedFacts"]["details.availableNow"].Type==JTokenType.Null,"unknown availability remains an explicit fact null despite refused preview");
  Check(scope["reportedFacts"]["details.availability.status"].ToString()=="unknown","known availability status is retained");
  def=Setup();DebugHandler.InstantBuildMode=false;ClusterManager.Instance=null;
  result=BuildPlanningTools.ScopePlan(def.PrefabID,0,Args(),true);scope=Scope(result,"placement_preflight","partial");
  Check((string)scope["reportedFacts"]["details.assessmentState"]=="unknown"&&(string)scope["reportedFacts"]["details.reasonCode"]=="cluster_manager_missing","material readiness facts carry exact source assessment/reason");
  Check(scope["reportedFacts"]["details.selectedAvailableKg"].Type==JTokenType.Null&&scope["reportedFacts"]["details.satisfied"].Type==JTokenType.Null,"unknown stock/sufficiency survive production wire serialization and scope metadata");
  Check(BuildPlanningTools.FixtureSelectCalls==0,"unknown readiness does not execute selection collaborator");
  def=Setup();int cell=Grid.XYToCell(2,1);var go=TileReplacementFixture.Make(def,600,cell,"Cuprite",true);Grid.Objects[cell,(int)ObjectLayer.Building]=go;
  result=BuildPlanningTools.ScopePlan(def.PrefabID,cell,Args(),true);scope=Scope(result,"existing_object_reuse","complete");
  Check((bool)result["alreadyPresent"]&&scope["reportedFacts"]["support.nativeFoundationValid"].Type==JTokenType.Null,"exact existing-object scope does not fabricate native support validity");
  def=Setup();result=BuildPlanningTools.ScopePlan("MissingDefinition",0,Args(),false);scope=Scope(result,"request_rejected","not_evaluated");
  Check(!(bool)result["valid"]&&scope["reportedFacts"]["details.availableNow"]==null,"request rejection omits unobserved availability fact");
  Check(def.DirectBuildCalls==0&&def.TryPlaceCalls==0&&BuildTool.Instance.BuildCalls==0,"scope instrumentation issues no native placement/UI action");
  Console.WriteLine("Scope parent: "+checks+" actual-source checks PASS");
 }
}
