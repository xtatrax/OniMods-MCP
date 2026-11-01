using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class PassiveReaderCases
{
    private static int checks;
    private static void Check(bool value,string reason) { if(!value)throw new InvalidOperationException("Passive reader: "+reason);checks++; }
    private sealed class CustomDefinition:BuildingDef
    {
        public new string Category {get{throw new InvalidOperationException("untrusted runtime Category property executed");}}
        public object BuildMenuCategory {get{throw new InvalidOperationException("untrusted runtime property executed");}}
    }
    internal static void Category()
    {
        var def=new CustomDefinition();((BuildingDef)def).Category="NativeField";def.MaterialCategory=new[]{"RawMineral"};
        var method=typeof(BuildPlanningTools).GetMethod("BuildingCategories",BindingFlags.NonPublic|BindingFlags.Static);
        var value=(List<string>)method.Invoke(null,new object[]{def});
        Check(value.Contains("NativeField")&&value.Contains("RawMineral"),"known native fields/material category remain readable");
        Check(value.Count==2,"runtime properties are never evaluated or fabricated as categories");
        Db.Seed(null);var row=BuildPlanningTools.HostCatalog(def);
        Check((string)row["categoryScope"]=="native_definition_fields_and_material_categories","category output identifies supported native field scope");
        Check((string)row["runtimeCategoryProperties"]=="not_evaluated","omitted runtime property extensions are not claimed as evaluated");
    }
    internal static void MissingParent()
    {
        DebugHandler.InstantBuildMode=false;Game.Instance=new Game();Research.Instance=new Research();Boundary.MissingParentLookups=0;
        var db=new Db{Techs=new Database.Techs(),TechItems=new Database.TechItems()};Db.Seed(db);
        db.TechItems.resources.Add(new TechItem{Id="Fixture",parentTechId="MissingParent"});
        var value=BuildingAvailabilityReader.Read(new BuildingDef{PrefabID="Fixture"});
        Check(Boundary.MissingParentLookups==0,"missing parent native Get must not run or log");
        Check(db.TechItems.PredicateCalls==0,"native completion not called before matching parent exists");
        Check(value.Unlocked==null&&value.Value==null&&value.ReasonCode=="tech_item_parent_missing","missing parent is unknown, not locked or default unlocked");
        db.Techs.resources.Add(new Database.Tech{Id="MissingParent"});
        value=BuildingAvailabilityReader.Read(new BuildingDef{PrefabID="Fixture"});
        Check(value.Unlocked==false&&db.TechItems.PredicateCalls==1,"known incomplete parent uses native completion once");
        var nativeParent=db.Techs.resources[0];Research.Instance.Existing.Add(new TechInstance{tech=nativeParent,Complete=true});
        value=BuildingAvailabilityReader.Read(new BuildingDef{PrefabID="Fixture"});
        Check(value.Unlocked==true&&db.TechItems.PredicateCalls==2,"completed parent uses native completion");
        db.TechItems.resources.Clear();
        value=BuildingAvailabilityReader.Read(new BuildingDef{PrefabID="Unlisted"});
        Check(value.Unlocked==true,"unlisted item preserves native unlocked semantics");
    }
    internal static void Facades()
    {
        Boundary.DbGetCalls=Boundary.PermitReads=0;Db.Seed(null);KleiItems.Ready(false);
        var value=BuildingFacadeReader.Read("FixtureFacade");
        Check(value.Unlocked==null&&value.ReasonCode=="database_cache_missing"&&Boundary.DbGetCalls==0,"cold database is unknown without initializer");
        var db=new Db{Permits=new PermitSet()};Db.Seed(db);
        value=BuildingFacadeReader.Read("FixtureFacade");Check(value.Unlocked==null&&value.ReasonCode=="permit_resource_missing","missing permit stays unknown");
        var permit=new Database.PermitResource{Id="FixtureFacade",NativeUnlocked=true};db.Permits.resources.Add(permit);
        db.Permits.BuildingFacades.resources.Add(new Facade{Id="FixtureFacade",Name="NativeLocalizedName"});
        value=BuildingFacadeReader.Read("FixtureFacade");
        Check(value.Unlocked==null&&value.Name=="NativeLocalizedName"&&Boundary.PermitReads==0,"cold owned inventory is unknown without eligibility callback");
        KleiItems.Ready(true);value=BuildingFacadeReader.Read("FixtureFacade");
        Check(value.Unlocked==true&&Boundary.PermitReads==1&&Boundary.DbGetCalls==0,"ready cache uses native eligibility without lazy database getter");
        permit.NativeUnlocked=false;value=BuildingFacadeReader.Read("FixtureFacade");Check(value.Unlocked==false&&value.Status=="locked","native locked remains known false");
        KleiItems.Ready(false);permit.Rarity=Database.PermitRarity.Universal;permit.NativeUnlocked=true;value=BuildingFacadeReader.Read("FixtureFacade");
        Check(value.Unlocked==true,"native universal permits need no owned inventory cache");
    }
    internal static void Run() {Category();MissingParent();Facades();
        foreach(string language in new[]{"en","ja","zh-Hans"})
            foreach(string key in new[]{"building_facade.default_name","building_facade.not_available","building_facade.unknown","building_facade.locked","placement_candidates.discovery_unknown","placement_candidates.material_inventory_unknown","placement_candidates.include_rejected","tools.building_control.parameters.includeRejected"})
                Check(OniMcp.Support.McpLocalization.GetForLanguage(language,key)!=key,"owned runtime translation resolves "+language+" "+key);
Console.WriteLine("Passive reader guards: "+checks+" checks PASS");}
}
