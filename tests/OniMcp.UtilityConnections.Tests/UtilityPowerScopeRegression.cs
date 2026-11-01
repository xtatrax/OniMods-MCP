using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using OniMcp.Support;
internal static class UtilityPowerScopeRegression
{
 private static int checks;private static void Check(bool value,string reason){checks++;if(!value)throw new Exception("Utility power scope: "+reason);}
 private static JObject Wire(Dictionary<string,object> value)=>JObject.Parse(JsonConvert.SerializeObject(value,McpJsonUtil.Settings));
 private static void Setup(){BuildPlanningTools.AutoPowerChildCalls=0;BuildPlanningTools.AutoPowerWouldMutate=0;BuildPlanningTools.AutoPowerSourceAvailable=true;BuildPlanningTools.AutoPowerWire=new BuildingDef{PrefabID="Wire"};BuildPlanningTools.AutoPowerTerrain=null;BuildPlanningTools.AutoPowerChildResult=CallToolResult.Text("{}");}
 private static JObject Scope(JObject data,string expected,string coverage="partial")
 {Check(data["validation"] is JObject,"full actual power adapter supplies scope");var scope=(JObject)data["validation"];Check((string)scope["validationScope"]==expected&&(string)scope["coverage"]==coverage,"power observation source stage is exact");return scope;}
 internal static void Run()
 {
  Setup();var args=new JObject{["validateOnly"]=true};var before=args.DeepClone();var def=new BuildingDef{RequiresPowerInput=true};var data=Wire(BuildPlanningTools.AutoPowerForTest(def,args));var scope=Scope(data,"automatic_power_wiring_child_dispatch");
  Check((string)data["status"]=="unknown"&&data["planned"].Type==JTokenType.Null&&data["childSuccess"].Type==JTokenType.Null,"empty successful child cannot establish known placement/counts");Check(scope["reportedFacts"]["planned"].Type==JTokenType.Null&&scope["reportedFacts"]["childSuccess"].Type==JTokenType.Null,"selected unknown child facts stay explicit null");Check(BuildPlanningTools.AutoPowerChildCalls==1&&BuildPlanningTools.AutoPowerWouldMutate==0&&JToken.DeepEquals(args,before),"passive alias/parent arguments preserved without UI/native action");
  Setup();BuildPlanningTools.AutoPowerChildResult=CallToolResult.Text("plain native response");data=Wire(BuildPlanningTools.AutoPowerForTest(def,new JObject{["dryRun"]=true}));Scope(data,"automatic_power_wiring_child_dispatch");Check((string)data["status"]=="connect_result_unparseable"&&data["childComplete"].Type==JTokenType.Null,"opaque child remains unparseable/partial instead of complete");
  Setup();BuildPlanningTools.AutoPowerSourceAvailable=false;data=Wire(BuildPlanningTools.AutoPowerForTest(def,new JObject{["dryRun"]=true}));Scope(data,"automatic_power_wiring_selection");Check(BuildPlanningTools.AutoPowerChildCalls==0&&(string)data["status"]=="no_connected_power_source","selection-only source refusal does not imply child preflight");
  Setup();data=Wire(BuildPlanningTools.AutoPowerForTest(def,new JObject{["dryRun"]=true,["autoConnectPower"]=false}));scope=Scope(data,"automatic_power_wiring_not_invoked","not_evaluated");Check(!(bool)data["enabled"]&&BuildPlanningTools.AutoPowerChildCalls==0&&!(bool)scope["gameplayMutationRequested"]&&!(bool)scope["uiInteractionRequested"],"disabled wiring observes no child scope or wiring mutation request");
  Setup();data=Wire(BuildPlanningTools.AutoPowerForTest(def,new JObject{["dryRun"]=false,["autoConnectPower"]=false}));scope=Scope(data,"automatic_power_wiring_not_invoked","not_evaluated");Check(!(bool)scope["gameplayMutationRequested"]&&!(bool)scope["uiInteractionRequested"]&&BuildPlanningTools.AutoPowerChildCalls==0,"actual parent with wiring disabled does not request wiring gameplay/UI mutation");
  Console.WriteLine("Utility power scope: "+checks+" full actual adapter/projector checks PASS (child/native boundary supplied)");
 }
}
