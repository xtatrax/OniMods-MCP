using System;
using System.Collections.Generic;
using System.Reflection;
using OniMcp.Tools;
internal static class ActualMaterialRegression
{
 private static int checks;private static void Check(bool value,string reason){checks++;if(!value)throw new InvalidOperationException("Actual materials: "+reason);}
 private static BuildingDef Setup(){Game.Instance=new Game();DebugHandler.InstantBuildMode=false;ClusterManager.Instance=new ClusterManager{World=new WorldContainer{id=0,worldInventory=new WorldInventory()}};ElementLoader.elements.Clear();ElementLoader.elements.Add(new Element{id="Cuprite",Category=new Tag("Metal")});Components.Pickupables.Items.Clear();return new BuildingDef{PrefabID="Fixture",MaterialCategory=new[]{"Metal"},Defaults=new List<Tag>{new Tag("Cuprite")},AvailableFacades=new[]{"FixtureFacade"}};}
 private static void FacadeCase(){var def=Setup();Db.Seed(null);Boundary.DbGetCalls=0;Check(!BuildPlanningTools.HostFacadeValid(def,"FixtureFacade"),"cold custom facade rejected without eager Db.Get");Check(Boundary.DbGetCalls==0,"facade selection never calls lazy getter");Check(BuildPlanningTools.HostFacadeValid(def,null)&&BuildPlanningTools.HostFacadeValid(def,"default"),"default facade requires no cached ownership");}
 internal static void Run(string[] args)
 {
  OniMcp.Support.McpLocalization.LanguageProvider=()=>"en";FacadeCase();if(args.Length>0)return;
  var def=Setup();var value=BuildPlanningTools.HostMaterial(def,"auto",0);var before=BuildPlanningTools.HostMaterialJson(value);Check((bool)before["valid"]&&before["elements"][0].ToString()=="Cuprite","real material selection succeeds");
  var clone=BuildPlanningTools.HostClone(value);var type=clone.GetType();((List<Tag>)type.GetField("Elements").GetValue(clone))[0]=new Tag("Changed");
  var info=type.GetField("Selected").GetValue(clone);info.GetType().GetField("AvailableKg").SetValue(info,999f);
  var after=BuildPlanningTools.HostMaterialJson(value);Check(after["elements"][0].ToString()=="Cuprite"&&(float)after["selectedAvailableKg"]==100f,"real material clone keeps source lists and selected values isolated");
  var another=BuildPlanningTools.HostClone(value);Check((float)BuildPlanningTools.HostMaterialJson(another)["selectedAvailableKg"]==100f,"later snapshot retains captured stock");
  var stock=ClusterManager.Instance.World.worldInventory;stock.Reads=0;BuildPlanningTools.HostCachedMaterial(def);
  Check(stock.Reads==3,"64 context selections capture the three native quantity views once");
  var selected=value.GetType().GetField("Selected").GetValue(value);selected.GetType().GetField("AvailableKg").SetValue(selected,float.NaN);
  var unknown=BuildPlanningTools.HostClone(value);Check(BuildPlanningTools.HostMaterialJson(unknown)["selectedAvailableKg"].Type==Newtonsoft.Json.Linq.JTokenType.Null,"unknown stock remains explicit null through real snapshot and quantity serializer");
  Check(BuildPlanningTools.HostMaterialJson(unknown)["quantityStatus"].ToString()=="unknown","unknown stock is not fabricated as sufficient");
  foreach(string missing in new[]{"cluster_manager_missing","world_missing","world_inventory_missing"})
  {
   def=Setup();if(missing=="cluster_manager_missing")ClusterManager.Instance=null;else if(missing=="world_missing")ClusterManager.Instance.World=null;else ClusterManager.Instance.World.worldInventory=null;
   var readiness=BuildPlanningTools.HostMaterialReadiness(def);Check((string)readiness["readinessReason"]==missing&&!(bool)readiness["valid"],"missing native inventory readiness remains explicit "+missing);
   Check(readiness["selectedAvailableKg"].Type==Newtonsoft.Json.Linq.JTokenType.Null,"missing cache does not invent zero stock "+missing);
  }
  def=Setup();ClusterManager.Instance.World.worldInventory.NativeAmount=0;var zero=BuildPlanningTools.HostMaterialReadiness(def);
  Check(zero["readinessReason"].Type==Newtonsoft.Json.Linq.JTokenType.Null&&ClusterManager.Instance.World.worldInventory.Reads>0,"ready actual zero inventory executes native selection rather than unknown guard");
  Console.WriteLine("Actual material/facade: "+checks+" checks PASS (full real source, native catalog/inventory inputs substituted)");
 }
}
