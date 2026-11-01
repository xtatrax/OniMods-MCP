using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
internal static class PreviewMaterialContextCases
{
    private static int checks;
    private static void Check(bool ok,string reason){checks++;if(!ok)throw new InvalidOperationException("Actual preview material context: "+reason);}
    private static BuildingDef Setup()
    {
        Boundary.Reset();Db.Seed(null);
        var def=new BuildingDef { PrefabID="ContextFixture",MaterialCategory=new[]{"Metal"},Mass=new[]{100f},AvailableFacades=new[]{"FixtureFacade"} };
        Boundary.AddMaterial(def,"Cuprite","Cuprite",100f);return def;
    }
    internal static void FacadeOnly()
    {
        var def=Setup();Check(!BuildPlanningTools.HostContextFacadeValid(def,"FixtureFacade"),"cold custom facade rejected without eager database getter");
        Check(Boundary.ForbiddenCalls==0,"facade resolution did not invoke lazy Db.Get or native UI");
        Check(BuildPlanningTools.HostContextFacadeValid(def,null)&&BuildPlanningTools.HostContextFacadeValid(def,"default"),"ordinary default facade needs no owned-item cache");
    }
    internal static void Run()
    {
        FacadeOnly();var def=Setup();var original=BuildPlanningTools.HostContextMaterialObject(def);
        var before=BuildPlanningTools.HostContextMaterialJson(original);
        Check((bool)before["valid"]&&before["elements"][0].ToString()=="Cuprite","real selection succeeds");
        var clone=BuildPlanningTools.HostContextClone(original);var type=clone.GetType();
        ((List<Tag>)type.GetField("Elements").GetValue(clone))[0]=new Tag("Changed");
        var selected=type.GetField("Selected").GetValue(clone);selected.GetType().GetField("AvailableKg").SetValue(selected,999f);
        var after=BuildPlanningTools.HostContextMaterialJson(original);
        Check(after["elements"][0].ToString()=="Cuprite"&&(float)after["selectedAvailableKg"]==100f,"real clone isolates material list and selected amount");
        Check((float)BuildPlanningTools.HostContextMaterialJson(BuildPlanningTools.HostContextClone(original))["selectedAvailableKg"]==100f,"later material clone retains captured stock");
        Boundary.InventoryReads=0;BuildPlanningTools.HostContextCachedMaterials(def);
        Check(Boundary.InventoryReads==1,"64 request-local selections read native inventory once");
        selected=original.GetType().GetField("Selected").GetValue(original);selected.GetType().GetField("AvailableKg").SetValue(selected,float.NaN);
        var unknown=BuildPlanningTools.HostContextMaterialJson(BuildPlanningTools.HostContextClone(original));
        Check(unknown["selectedAvailableKg"].Type==JTokenType.Null,"production serializer retains explicit unknown selected stock");
        Check((string)unknown["quantityStatus"]=="unknown","unknown stock is not fabricated as sufficient");
        foreach(string missing in new[]{"cluster_manager_missing","world_missing","world_inventory_missing"})
        {
            def=Setup();if(missing=="cluster_manager_missing")ClusterManager.Instance=null;
            else if(missing=="world_missing")ClusterManager.Instance.Worlds.Clear();else ClusterManager.Instance.Worlds[0].worldInventory=null;
            var value=BuildPlanningTools.HostContextReadiness(def);
            Check((string)value["readinessReason"]==missing&&!(bool)value["valid"],"missing native inventory remains explicit "+missing);
            Check(value["selectedAvailableKg"].Type==JTokenType.Null,"missing cache does not become zero stock "+missing);
        }
        def=Setup();Boundary.SetAllMasses(0);var zero=BuildPlanningTools.HostContextReadiness(def);
        Check(zero["readinessReason"].Type==JTokenType.Null&&Boundary.InventoryReads>0,"ready zero inventory still uses native selection");
        Console.WriteLine("Actual preview material context: "+checks+" checks PASS (full source; native catalog/inventory inputs substituted)");
    }
}
