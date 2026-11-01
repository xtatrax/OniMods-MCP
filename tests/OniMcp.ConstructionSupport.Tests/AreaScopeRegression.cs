using System;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using OniMcp.Core;
internal static class AreaScopeRegression {
 static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception("area scope: "+message);}
 static JObject Args(string prefab,bool dry=true)=>new JObject{{"prefabId",prefab},{"worldId",0},{"dryRun",dry},{"confirm",true},{"anchors",new JArray(new JObject{{"x",10},{"y",prefab=="Locker"?11:10}})}};
 internal static void Run(){
  AreaFixture.Reset();var result=BuildPlanningTools.BuildArea().Handler(Args("Tile"));var data=JObject.Parse(result.Content[0].Text);
  Check((string)data["validation"]?["validationScope"]=="batch_placement_preflight","real typed BuildArea leaf declares batch preflight");
  Check((string)data["validation"]["coverage"]=="partial"&&!(bool)data["validation"]["gameplayMutationRequested"]&&!(bool)data["validation"]["uiInteractionRequested"]&&AreaFixture.Mutations==0,"batch scope does not claim complete native rules or actions");
  Check((int)data["validation"]["reportedFacts"]["anchorCount"]==1&&data["validation"]["reportedFacts"]["availableNow"]==null,"only actual bounded payload scalar facts are copied");
  AreaFixture.Reset();result=BuildPlanningTools.BuildArea().Handler(Args("Locker"));data=JObject.Parse(result.Content[0].Text);
  Check((int)data["hardFailed"]==1&&(string)data["validation"]["coverage"]=="partial"&&(int)data["validation"]["reportedFacts"]["hardFailed"]==1,"known support refusal preserves real failure and partial unobserved stages");
  AreaFixture.Reset();result=BuildPlanningTools.BuildArea().Handler(Args("Missing"));
  Check(result.IsError&&(string)((JObject)result.Content[0].Meta[PreviewValidationObservation.MetadataKey])["coverage"]=="not_evaluated"&&AreaFixture.Mutations==0,"missing definition is an early request refusal, not completed native validation");
  AreaFixture.Reset();var context=new BuildPlanningRequestSupportContext(0);BuildPlanningTools.BuildArea(context).Handler(Args("Tile"));
  data=JObject.Parse(BuildPlanningTools.BuildArea(context).Handler(Args("Locker")).Content[0].Text);
  Check((string)data["previews"][0]["support"]["supportCells"][0]["state"]=="planned_in_request"&&(string)data["validation"]["coverage"]=="partial","typed mixed-prefab forecast survives scope metadata without becoming native support");
  AreaFixture.Reset();result=BuildPlanningTools.BuildArea().Handler(Args("Tile",false));data=JObject.Parse(result.Content[0].Text);
  Check((string)data["validation"]["validationScope"]=="batch_execution_observation"&&(bool)data["validation"]["gameplayMutationRequested"]&&AreaFixture.Mutations==1,"actual dispatch scope carries write intent and retains native-boundary outcome");
  AreaFixture.Reset();Console.WriteLine("PASS "+checks+" actual typed batch leaf scope checks");
 }
}
