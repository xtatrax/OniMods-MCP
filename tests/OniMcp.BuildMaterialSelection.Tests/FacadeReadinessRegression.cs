using System;using Newtonsoft.Json.Linq;using OniMcp.Tools;
// Native catalog/eligibility results are supplied data, not copied ownership rules.
internal static class FacadeReadinessInputs{internal static int Lookups,EligibilityCalls;}
internal static class FacadeReadinessRegression{
 private static int checks;private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException("Actual facade readiness: "+reason);}
 private static BuildingDef Setup(){Boundary.Reset();Db.Seed(null);FacadeReadinessInputs.Lookups=FacadeReadinessInputs.EligibilityCalls=0;return new BuildingDef{PrefabID="FacadeFixture",MaterialCategory=new[]{"Metal"},Mass=new[]{100f},AvailableFacades=new[]{"FixtureFacade"}};}
 internal static void Run(){OniMcp.Support.McpLocalization.LanguageProvider=()=>"en";
 foreach(string missing in new[]{"database_cache_missing","permit_catalog_missing","permit_resource_missing","owned_inventory_cache_missing"}){
  var def=Setup();var db=new Db{Permits=new PermitSet()};db.Permits.resources.Add(new Permit{Id="FixtureFacade",NativeUnlocked=false});KleiItems.Seed(true);
  if(missing=="database_cache_missing")Db.Seed(null);else{Db.Seed(db);if(missing=="permit_catalog_missing")db.Permits=null;else if(missing=="permit_resource_missing")db.Permits.resources.Clear();else KleiItems.Seed(false);}
  var value=BuildPlanningTools.HostContextFacadeRecord(def,"FixtureFacade");Check(!(bool)value["Valid"]&&(string)value["AssessmentState"]=="unknown"&&(string)value["ReasonCode"]==missing,"missing native readiness retains structured unknown: "+missing);
  Check(Boundary.ForbiddenCalls==0&&FacadeReadinessInputs.EligibilityCalls==0&&FacadeReadinessInputs.Lookups==(missing=="database_cache_missing"||missing=="permit_catalog_missing"?0:1),"unknown cached state does not call lazy Db/native eligibility; only an existing catalog may be looked up");}
 foreach(bool unlocked in new[]{false,true}){var def=Setup();var db=new Db{Permits=new PermitSet()};db.Permits.resources.Add(new Permit{Id="FixtureFacade",NativeUnlocked=unlocked});Db.Seed(db);KleiItems.Seed(true);var value=BuildPlanningTools.HostContextFacadeRecord(def,"FixtureFacade");Check((bool)value["Valid"]==unlocked&&(string)value["AssessmentState"]=="known"&&FacadeReadinessInputs.EligibilityCalls==1,"ready locked/unlocked use native eligibility result, including true with an empty supplied ownership cache");}
 Db.Seed(null);KleiItems.Seed(false);Boundary.Reset();Console.WriteLine("Actual facade readiness: "+checks+" checks PASS (full Materials and passive reader; native catalog/eligibility inputs supplied)");}
}
