using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
internal static class UtilityConnectionScopeRegression
{
 private static int checks;
 private static void Check(bool value,string reason){checks++;if(!value)throw new Exception("Utility connection scope: "+reason);}
 private static JObject Request(bool preview=true)=>new JObject{["x"]=2,["y"]=2,["direction"]="right",["type"]="liquid",["worldId"]=0,["dryRun"]=preview,["confirm"]=!preview};
 private static JObject Result(OniMcp.Core.CallToolResult value)=>JObject.Parse(value.Content[0].Text);
 private static JObject Scope(JObject data,string expected,string coverage="partial")
 {Check(data["validation"] is JObject,"actual leaf supplies an authoritative scope");var scope=(JObject)data["validation"];Check((string)scope["validationScope"]==expected&&(string)scope["coverage"]==coverage,"scope follows exact source stage, independent of success");return scope;}
 internal static void Run()
 {
  var f=new ConnectFixture();var args=Request();var before=args.DeepClone();var report=Result(BuildPlanningTools.ConnectForTest(args));var scope=Scope(report,"existing_utility_target_preflight");
  Check((bool)report["success"]&&!(bool)report["actionAttempted"]&&report["connectionVerified"].Type==JTokenType.Null,"target preview success retains unknown native operation result");
  Check(scope["reportedFacts"]["connectionVerified"].Type==JTokenType.Null&&!(bool)scope["gameplayMutationRequested"]&&!(bool)scope["uiInteractionRequested"],"native null and passive requested intent survive actual serialization");
  Check(f.Tool.Events.Count==0&&f.Manager.Bits[f.A.Cell]==f.BeforeA&&f.Manager.Bits[f.B.Cell]==f.BeforeB&&JToken.DeepEquals(args,before),"scope adds no UI/native/args mutation");
  f=new ConnectFixture();UtilityBuildTool.Instance=null;report=Result(BuildPlanningTools.ConnectForTest(Request()));Scope(report,"existing_utility_target_preflight");Check((bool)report["success"]&&!(bool)report["actionAttempted"],"missing UI is deliberately not evaluated by target preview, never complete");
  f=new ConnectFixture();args=Request();args["validateOnly"]=true;args["dryRun"]=false;args["confirm"]=true;report=Result(BuildPlanningTools.ConnectForTest(args));Scope(report,"existing_utility_target_preflight");Check(f.Tool.Events.Count==0&&(bool)report["dryRun"],"newest provider IsDryRun alias remains effective and passive");
  f=new ConnectFixture();args=Request();args["direction"]="diagonal";var rejected=BuildPlanningTools.ConnectForTest(args);Check(rejected.IsError,"invalid request stays an error");var meta=rejected.Content[0].Meta[PreviewValidationObservation.MetadataKey] as JObject;Check((string)meta["validationScope"]=="request_rejected"&&(string)meta["coverage"]=="not_evaluated"&&f.Tool.Events.Count==0,"plain request error text remains plain with request-only Meta");
  f=new ConnectFixture();var capture=f;f.Tool.OnRelease=()=>{capture.Manager.Bits[capture.A.Cell]|=capture.AddA;capture.Manager.Bits[capture.B.Cell]|=capture.AddB;};report=Result(BuildPlanningTools.ConnectForTest(Request(false)));scope=Scope(report,"existing_utility_connection_execution_observation");Check((bool)report["connectionVerified"]&&(bool)report["actionAttempted"]&&!(bool)report["flowVerified"]&&(bool)scope["uiInteractionRequested"],"verified native edge remains partial scope with flow unobserved");
  f=new ConnectFixture();f.Tool.OnRelease=()=>throw new Exception("fixture native failure");var failed=BuildPlanningTools.ConnectForTest(Request(false));report=Result(failed);Scope(report,"existing_utility_connection_execution_observation");Check(failed.IsError&&(bool)report["partial"]&&report["connectedEdges"].Type==JTokenType.Null,"native failure/partial/null facts remain exact rather than become complete");
  Console.WriteLine("Utility connection scope: "+checks+" actual source checks PASS (native provider/UI boundary supplied)");
 }
}
