using System;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
internal static class LeafScopeRegression {
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException("leaf scope: "+message);}
 internal static void Run(){
  TileReplacementFixture.Setup();var result=TileReplacementFixture.PlanRead(TileReplacementFixture.Args(true));
  Check((string)result["validation"]?["validationScope"]=="native_replacement_candidate_only"&&(string)result["validation"]["coverage"]=="complete","replacement preview preserves its exact native-candidate-only scope");
  Check(!(bool)result["validation"]["gameplayMutationRequested"]&&!(bool)result["validation"]["uiInteractionRequested"]&&result["validation"]["reportedFacts"]["tileReplacement.persistentTargetLinkVerified"].Type==JTokenType.Null&&TileReplacementFixture.Tool.BuildCalls==0,"candidate preview retains unknown persistent binding and zero native action");
  TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>TileReplacementFixture.Queue();result=TileReplacementFixture.PlanRead();
  Check((string)result["validation"]["validationScope"]=="native_replacement_reservation_observed"&&(string)result["validation"]["coverage"]=="complete"&&!(bool)result["buildingCompleted"],"observed replacement reservation never becomes completed construction");
  Check((bool)result["validation"]["gameplayMutationRequested"]&&(bool)result["validation"]["uiInteractionRequested"]&&(bool)result["validation"]["reportedFacts"]["queueVerified"],"native replacement action preserves explicit UI/write intent and actual queue fact");
  TileReplacementFixture.Setup();result=TileReplacementFixture.PlanRead();
  Check((string)result["validation"]["validationScope"]=="native_void_gesture_attempted_no_verified_reservation"&&(string)result["validation"]["coverage"]=="partial"&&!(bool)result["valid"],"native void gesture without observed queue retains partial refusal");
  ParseScopeFixture.Calls=0;var parsed=BuildPlanningTools.ParseBuildPlan().Handler(new JObject());var data=JObject.Parse(parsed.Content[0].Text);
  Check((string)data["validation"]?["validationScope"]=="plan_resolution"&&(string)data["validation"]["coverage"]=="partial"&&ParseScopeFixture.Calls==1,"complete actual parse-plan factory marks resolution, not placement validation");
  Check(!(bool)data["validation"]["gameplayMutationRequested"]&&!data["validation"]["reportedFacts"].HasValues&&(string)data["resolution"]=="boundary supplied","parse scope preserves resolver output without inventing validation facts");
  ParseScopeFixture.Throw=true;try{parsed=BuildPlanningTools.ParseBuildPlan().Handler(new JObject());Check(parsed.IsError&&(string)((JObject)parsed.Content[0].Meta[OniMcp.Tools.PreviewValidationObservation.MetadataKey])["coverage"]=="not_evaluated","failed resolution has early requested-scope metadata only");}finally{ParseScopeFixture.Throw=false;}
  var invalid=BuildPlanningTools.PreviewBuild().Handler(new JObject{{"id",0}});
  Check(invalid.IsError&&(string)((JObject)invalid.Content[0].Meta[OniMcp.Tools.PreviewValidationObservation.MetadataKey])["coverage"]=="not_evaluated","actual preview factory early ID refusal has requested-scope-only provenance and no parent validation");
  Console.WriteLine("PASS "+checks+" actual replacement/parse leaf scope checks");
 }
}
namespace OniMcp.Tools {
 internal static class ParseScopeFixture{internal static int Calls;internal static bool Throw;}
 public static partial class BuildPlanningTools {
  private sealed class ParseScopeResolution {internal System.Collections.Generic.Dictionary<string,object> ToDictionary()=>new System.Collections.Generic.Dictionary<string,object>{{"resolution","boundary supplied"}};}
  private static ParseScopeResolution ResolveBuildPlan(JObject args){ParseScopeFixture.Calls++;if(ParseScopeFixture.Throw)throw new Exception("fixture resolution unavailable");return new ParseScopeResolution();}
 }
}
