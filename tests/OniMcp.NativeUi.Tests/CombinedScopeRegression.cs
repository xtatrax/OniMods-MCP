using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
namespace OniMcp.Tools
{
 internal static partial class ToolUtil
 {
  internal static Dictionary<string,int> GetRect(JObject args){return new Dictionary<string,int>{{"x1",GetInt(args,"x1")??0},{"y1",GetInt(args,"y1")??0},{"x2",GetInt(args,"x2")??3},{"y2",GetInt(args,"y2")??0}};}
 }
}
internal static class CombinedScopeRegression
{
 private static int checks;private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException("Combined candidate scope: "+reason);}
 private static BuildingDef Setup()
 {
  BlueprintCompletionRegression.Setup();DebugHandler.InstantBuildMode=false;WorldEditorTools.FixtureModeRequest=null;BuildPlanningTools.ResetPlannerFixture();WorldEditor.Area=null;UnityEngine.Object.ThrowEnumeration=false;UnityEngine.Object.EnumerationCalls=0;
  var def=new BuildingDef{PrefabID="CombinedScopeFixture",Name="Combined scope fixture",BuildLocationRule=BuildLocationRule.Anywhere};BuildPlanningTools.FixtureDefs[def.PrefabID]=def;return def;
 }
 private static JObject Read(BuildingDef def)
 {
  var call=BuildPlanningTools.FindPlacementCandidates().Handler(new JObject{["domain"]="planning",["action"]="placement_candidates",["prefabId"]=def.PrefabID,["worldId"]=0,["x1"]=0,["y1"]=0,["x2"]=3,["y2"]=0,["maxEvaluations"]=2,["scanOffset"]=1,["limit"]=2,["material"]="Cuprite"});
  Check(!call.IsError,"actual public inline candidate leaf accepted");return JObject.Parse(call.Content[0].Text);
 }
 internal static void Run()
 {
  var def=Setup();var value=Read(def);var scope=value["validation"];
  Check((int)value["scannedAnchors"]==2&&(int)value["nextOffset"]==3&&!(bool)value["allAnchorsEvaluated"],"actual candidate budget/cursor survives full parent integration");
  Check((string)scope["validationScope"]=="candidate_scan_window"&&(string)scope["coverage"]=="complete","top scope is only the known finite scan window");
  Check(def.FixtureAvailabilityReads==1&&BuildPlanningTools.FixtureSelectCalls==1&&UnityEngine.Object.EnumerationCalls==1&&BuildPlanningTools.FixtureSupportCalls==2,"actual candidate context captures native reads once and validates support per anchor");
  foreach(var row in (JArray)value["candidates"])
  {
   var child=row["preview"];Check(child["validation"] is JObject,"actual parent child must report its own source-stage scope");Check((bool)child["valid"]&&(string)child["validation"]["validationScope"]=="placement_preflight"&&(string)child["validation"]["coverage"]=="partial","actual row parent scope is distinct from window scope even on valid previews");
   Check(!(bool)child["validation"]["gameplayMutationRequested"]&&!(bool)child["validation"]["uiInteractionRequested"],"real row records passive request intent");
  }
  def=Setup();DebugHandler.InstantBuildMode=false;ClusterManager.Instance=null;value=Read(def);scope=value["validation"];
  Check((string)scope["coverage"]=="partial"&&(int)value["unknownCount"]==2&&(int)value["knownRejectedCount"]==0,"actual missing material cache yields partial unknown candidate window");
  Check(BuildPlanningTools.FixtureSelectCalls==0&&def.FixtureAvailabilityReads==1,"missing cache refuses before selection while reusing availability capture");
  foreach(var row in (JArray)value["candidates"])
  {
   var child=row["preview"];Check(child["validation"] is JObject,"unknown actual parent child must preserve its source-stage scope");Check((string)row["status"]=="unknown"&&(string)child["details"]["assessmentState"]=="unknown","actual parent unknown details reach actual candidate classification");
   Check((string)child["validation"]["reportedFacts"]["details.reasonCode"]=="cluster_manager_missing"&&child["validation"]["reportedFacts"]["details.selectedAvailableKg"].Type==JTokenType.Null,"actual scoped parent preserves unknown reason/stock fact");
  }
  Check(def.DirectBuildCalls==0&&def.TryPlaceCalls==0&&BuildTool.Instance.BuildCalls==0,"full supported candidate/cache/scope path performs no native game/UI action");
  Console.WriteLine("Combined candidate scope: "+checks+" actual public inline candidate/context/scope checks PASS");
 }
}
