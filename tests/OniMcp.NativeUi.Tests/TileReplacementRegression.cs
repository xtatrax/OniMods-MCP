using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class TileReplacementFixture
{
    internal static BuildingDef Definition,OldDefinition;
    internal static GameObject Original,Wire;
    internal static BuildTool Tool;
    internal static PlayerController Controller;
    internal static PlanScreen Plan;
    internal static ProductInfoScreen Product;
    internal static MaterialSelectionPanel Materials;
    internal static PriorityScreen Priority;
    internal static List<MaterialSelector> Selectors;
    internal static int Cell;
    internal static void Setup()
    {
        BlueprintCompletionRegression.Setup();BuildPlanningTools.ResetPlannerFixture();UnityEngine.Object.ThrowEnumeration=false;
        Cell=BlueprintCompletionRegression.Cell;Tool=BlueprintCompletionRegression.Tool;Controller=BlueprintCompletionRegression.Controller;
        DebugHandler.InstantBuildMode=false;Game.Instance.SandboxModeActive=false;
        SandboxToolParameterMenu.instance=new SandboxToolParameterMenu{settings=new SandboxToolParameterMenu.Settings()};
        OldDefinition=BlueprintCompletionRegression.Definition;OldDefinition.BuildLocationRule=BuildLocationRule.Tile;
        Definition=new BuildingDef{PrefabID="GasPermeableMembrane",Name="Fixture airflow",BuildLocationRule=BuildLocationRule.Tile};
        BuildPlanningTools.FixtureDefs[Definition.PrefabID]=Definition;
        Original=Make(OldDefinition,101,Cell,"FixtureRock",true);
        Grid.Objects[Cell,(int)ObjectLayer.Building]=Original;Grid.Objects[Cell,(int)ObjectLayer.FoundationTile]=Original;
        Grid.Solid[Cell]=true;Grid.Foundation[Cell]=true;
        Wire=new GameObject{Cell=Cell,Id=202};Wire.Add(new KPrefabID{InstanceID=202,PrefabTag=new Tag("Wire")});
        Grid.Objects[Cell,(int)ObjectLayer.Wire]=Wire;
        Plan=new PlanScreen{isSpawned=true};PlanScreen.Instance=Plan;
        Product=new ProductInfoScreen{isSpawned=true};new GameObject().Add(Product);
        Materials=new MaterialSelectionPanel{isSpawned=true};new GameObject().Add(Materials);
        Priority=new PriorityScreen{isSpawned=true};new GameObject().Add(Priority);
        Priority.SetScreenPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic,3));Materials.FixturePriority=Priority;
        Selectors=new List<MaterialSelector>();
        for(int i=0;i<3;i++)
        {
            var selector=new MaterialSelector{isSpawned=true,selectorIndex=i,CurrentSelectedElement=new Tag("GoldAmalgam")};
            new GameObject{Active=i==0}.Add(selector);
            foreach(string tag in new[]{"Cuprite","GoldAmalgam"})
            {var toggle=new KToggle();toggle.FixtureSetCallback(true);new GameObject().Add(toggle);selector.ElementToggles[new Tag(tag)]=toggle;}
            Selectors.Add(selector);
        }
        Definition.CraftRecipe.Result=new Tag(Definition.PrefabID);Materials.SetFixtureSelectors(Selectors,Definition.CraftRecipe);
        Product.materialSelectionPanel=Materials;Plan.SetFixtureProduct(Product);
        var menuPriority=new PriorityScreen{isSpawned=true};new GameObject().Add(menuPriority);
        var menuPanel=new MaterialSelectionPanel{isSpawned=true,FixturePriority=menuPriority};new GameObject().Add(menuPanel);
        var menuProduct=new ProductInfoScreen{isSpawned=true,materialSelectionPanel=menuPanel};new GameObject().Add(menuProduct);
        BuildMenu.Instance=new BuildMenu{isSpawned=true};BuildMenu.Instance.SetFixtureProduct(menuProduct);
        Controller.OnRestore=()=>{if(Plan.ProductInfoScreen!=null)Plan.ProductInfoScreen.currentDef=null;};
        Materials.OnSelect=tag=>Tool.Activate(Product.currentDef,new List<Tag>(Materials.GetSelectedElementAsList),"DEFAULT_FACADE");
    }
    internal static GameObject Make(BuildingDef def,int id,int cell,string material,bool complete)
    {
        var go=new GameObject{Id=id,Cell=cell};go.Add(new Building{Def=def});go.Add(new KPrefabID{InstanceID=id,PrefabTag=new Tag(def.PrefabID)});
        go.Add(new PrimaryElement{ElementID=material});go.Add(new BuildingFacade{CurrentFacade=null,IsOriginal=true});go.Add(new Rotatable{Orientation=Orientation.Neutral});
        go.Add(new Prioritizable());go.Add(new Deconstructable());
        if(complete){var component=new BuildingComplete{Def=def};go.Add(component);Components.BuildingCompletes.Items.Add(component);}
        else {var component=new Constructable{IsReplacementTile=true,SelectedElementsTags=new List<Tag>{new Tag(material)}};go.Add(component);BuildPlanningTools.FixtureBlueprints.Add(component);}
        NativeBuildingCellIndex.Shapes[go]=new FixtureGeometry{Orientation=Orientation.Neutral,Origin=new BuildingGeometryCell{Cell=cell,X=Grid.CellColumn(cell),Y=Grid.CellRow(cell)},Bounds=new[]{Grid.CellColumn(cell),Grid.CellRow(cell),Grid.CellColumn(cell),Grid.CellRow(cell)},Cells=new List<BuildingGeometryCell>{new BuildingGeometryCell{Cell=cell,X=Grid.CellColumn(cell),Y=Grid.CellRow(cell)}}};
        return go;
    }
    internal static GameObject Queue(string material="Cuprite",int priority=7)
    {
        var go=Make(Definition,303,Cell,material,false);
        go.GetComponent<Prioritizable>().Observed=new PrioritySetting(PriorityScreen.PriorityClass.basic,priority);
        Grid.Objects[Cell,(int)ObjectLayer.ReplacementTile]=go;return go;
    }
    internal static JObject Args(bool preview=false,int? priority=7)
    {var args=new JObject{["worldId"]=0,["material"]="Cuprite",["confirm"]=true,["dryRun"]=preview};if(priority.HasValue)args["priority"]=priority.Value;return args;}
    internal static JObject PlanRead(JObject args=null){return BuildPlanningTools.PlanForTest(Definition.PrefabID,Cell,args??Args());}
    internal static NativeTileReplacement.Outcome Native(System.Action validate=null,int priority=7)
    {return NativeTileReplacement.Run(Definition,new Tag("Cuprite"),"DEFAULT_FACADE",Cell,priority,validate??(()=>{}));}
    internal static void AssertOriginalAndWire(string reason)
    {
        TileReplacementRegression.Check(ReferenceEquals(Grid.Objects[Cell,(int)ObjectLayer.Building],Original)
            &&ReferenceEquals(Grid.Objects[Cell,(int)ObjectLayer.FoundationTile],Original)&&ReferenceEquals(Grid.Objects[Cell,(int)ObjectLayer.Wire],Wire)
            &&Original.DeleteCalls==0&&Wire.DeleteCalls==0&&Grid.Solid[Cell]&&Grid.Foundation[Cell]
            &&Original.GetComponent<PrimaryElement>().ElementID=="FixtureRock"&&Definition.DirectBuildCalls==0&&Definition.TryPlaceCalls==0,reason);
    }
}
internal static class TileReplacementRegression
{
    private static int checks;
    internal static void Check(bool condition,string reason){checks++;if(!condition)throw new InvalidOperationException("Tile replacement: "+reason);}
    internal static void Run()
    {
        NativeReadiness();NativePreparationAndOwnership();MaterialUiDiagnostics();PriorityCacheReadiness();TileReplacementParentRegression.Run();TileReplacementGeneralizationRegression.Run();
        Console.WriteLine("Tile replacement: "+checks+" host checks passed (real adapter and parent route; native UI/geometry boundaries substituted)");
    }
    private static void NativeReadiness()
    {
        var invalid=new System.Action[]{()=>DebugHandler.InstantBuildMode=true,
            ()=>{Game.Instance.SandboxModeActive=true;SandboxToolParameterMenu.instance.settings.InstantBuild=true;},
            ()=>{Game.Instance.SandboxModeActive=true;SandboxToolParameterMenu.instance=null;},
            ()=>Game.Instance.Loading=true,()=>PlanScreen.Instance=null,()=>SpeedControlScreen.Instance.IsPaused=false,
            ()=>TileReplacementFixture.Controller.ActiveTool=new object(),()=>TileReplacementFixture.Controller.Dragging=true,
            ()=>TileReplacementFixture.Tool.Dragging=true,()=>KScreenManager.inputDisabled=true,
            ()=>KScreenManager.Instance.screenStack.Add(new KScreen{Active=true,Modal=true})};
        foreach(var change in invalid)
        {
            TileReplacementFixture.Setup();change();var outcome=TileReplacementFixture.Native();
            Check(!outcome.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Plan.CopyCalls==0,
                "normal-mode/readiness/idle guards reject before native preparation and gesture");
        }
        TileReplacementFixture.Setup();BuildMenu.Instance=null;var optional=TileReplacementFixture.Native();
        Check(optional.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==1
            &&JObject.FromObject(optional.ToDictionary())["initialOptionalBuildMenuPriorityState"]?.Value<string>()=="not_applicable",
            "absent optional legacy BuildMenu does not block the selected PlanScreen native route");
        TileReplacementFixture.Setup();BuildMenu.Instance.isSpawned=false;
        var cachedProduct=BuildMenu.Instance.FixtureProduct;var cachedPanel=cachedProduct.materialSelectionPanel;var cachedPriority=cachedPanel.PriorityScreen;
        cachedProduct.isSpawned=false;cachedPanel.isSpawned=false;cachedPriority.isSpawned=false;
        cachedProduct.gameObject.Active=false;cachedPanel.gameObject.Active=false;cachedPriority.gameObject.Active=false;
        optional=TileReplacementFixture.Native();
        Check(optional.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==1
            &&JObject.FromObject(optional.ToDictionary())["initialOptionalBuildMenuPriorityState"]?.Value<string>()=="observed",
            "hidden unspawned legacy UI with valid typed cached priority is sufficient for its pure native getter");
        foreach(string unavailableCache in new[]{"product","panel","priority","unknown_priority"})
        {
            TileReplacementFixture.Setup();cachedProduct=BuildMenu.Instance.FixtureProduct;cachedPanel=cachedProduct.materialSelectionPanel;
            if(unavailableCache=="product")BuildMenu.Instance.SetFixtureProduct(null);
            else if(unavailableCache=="panel")cachedProduct.materialSelectionPanel=null;
            else if(unavailableCache=="priority")cachedPanel.FixturePriority=null;
            else cachedPanel.PriorityScreen.SetScreenPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic,-1));
            optional=TileReplacementFixture.Native();
            Check(!optional.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Plan.CopyCalls==0
                &&JObject.FromObject(optional.ToDictionary())["initialOptionalBuildMenuPriorityState"]?.Value<string>()==(unavailableCache=="unknown_priority"?"unknown":"unavailable"),
                "present optional legacy menu with invalid cached "+unavailableCache+" refuses before native preparation");
        }
        TileReplacementFixture.Setup();TileReplacementFixture.Materials.isSpawned=false;var pending=TileReplacementFixture.Native();
        Check(!pending.UiInitializing&&pending.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==1,
            "material UI caches initialized synchronously before OnSpawn support the normal native gesture");
        TileReplacementFixture.Setup();TileReplacementFixture.Selectors[0].isSpawned=false;pending=TileReplacementFixture.Native();
        Check(!pending.UiInitializing&&pending.ActionAttempted,"a configured active selector does not require an unrelated spawn flag");
    }
    private static void NativePreparationAndOwnership()
    {
        foreach(string unavailable in new[]{"hidden","disabled","missing_callback","extra_active_selector","wrong_recipe"})
        {
            TileReplacementFixture.Setup();var toggle=TileReplacementFixture.Selectors[0].ElementToggles[new Tag("Cuprite")];
            if(unavailable=="hidden")toggle.gameObject.Active=false;
            else if(unavailable=="disabled")toggle.FixtureInteractable=false;
            else if(unavailable=="missing_callback")toggle.FixtureSetCallback(false);
            else if(unavailable=="extra_active_selector")TileReplacementFixture.Selectors[1].gameObject.Active=true;
            else TileReplacementFixture.Materials.SetFixtureSelectors(TileReplacementFixture.Selectors,new Recipe());
            var unavailableOutcome=TileReplacementFixture.Native();
            Check(!unavailableOutcome.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Materials.MaterialSelectCalls==0,
                "native UI material not selectable: "+unavailable+" is rejected before ForceSelect and gesture");
        }
        TileReplacementFixture.Setup();int validations=0;var outcome=TileReplacementFixture.Native(()=>validations++);
        Check(outcome.ActionAttempted&&TileReplacementFixture.Plan.CopyCalls==1&&TileReplacementFixture.Tool.BuildCalls==1&&validations>=2,
            "three native selectors with one active slot permit one exact native gesture after revalidation");
        Check(TileReplacementFixture.Tool.ActivatedDef==TileReplacementFixture.Definition
            &&TileReplacementFixture.Tool.ActivatedElements.Single().Name=="Cuprite"&&TileReplacementFixture.Materials.RememberedSelections==1,
            "new material reaches native UI and records its ordinary selection history");
        Check(outcome.UiRestored==true&&outcome.PriorityRestored==true&&TileReplacementFixture.Priority.GetLastSelectedPriority().priority_value==3,
            "owned native UI priority and active tool restore through their native entries");
        var dto=JObject.FromObject(outcome.ToDictionary());
        Check(dto["historyRestored"].Type==JTokenType.Null&&dto["selectionRestored"].Type==JTokenType.Null,
            "native history and selection restoration remain explicitly unknown");
        foreach(bool replace in new[]{false,true})
        {
            TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnMouseMoveFixture=()=>
            {if(replace)TileReplacementFixture.Tool.ReplaceNativeMaterials(new List<Tag>{new Tag("GoldAmalgam")});else TileReplacementFixture.Tool.EditNativeMaterial(new Tag("GoldAmalgam"));};
            outcome=TileReplacementFixture.Native();
            Check(!outcome.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==0,"swapped native material list or in-place write cannot reach construction");
        }
        TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnMouseMoveFixture=()=>TileReplacementFixture.Priority.SetScreenPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic,8));
        outcome=TileReplacementFixture.Native();
        Check(!outcome.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Priority.GetLastSelectedPriority().priority_value==8,
            "foreign priority change is rejected and not overwritten during cleanup");
        TileReplacementFixture.Setup();var foreign=new object();TileReplacementFixture.Tool.OnBuild=cell=>TileReplacementFixture.Controller.ActiveTool=foreign;
        outcome=TileReplacementFixture.Native();Check(outcome.UiRestored==null&&ReferenceEquals(TileReplacementFixture.Controller.ActiveTool,foreign),"foreign active tool is not clobbered");
        TileReplacementFixture.Setup();GameObject foreignPreview=null;TileReplacementFixture.Tool.OnBuild=cell=>
        {TileReplacementFixture.Tool.Activate(TileReplacementFixture.Definition,new List<Tag>{new Tag("Cuprite")},"DEFAULT_FACADE");foreignPreview=TileReplacementFixture.Tool.visualizer;};
        outcome=TileReplacementFixture.Native();Check(outcome.UiRestored==null&&ReferenceEquals(TileReplacementFixture.Tool.visualizer,foreignPreview),"same singleton new preview remains owned by its new activation");
        TileReplacementFixture.Setup();var foreignProduct=new ProductInfoScreen{isSpawned=true,currentDef=new BuildingDef{PrefabID="FixtureForeignDef"},materialSelectionPanel=new MaterialSelectionPanel()};
        int rechecks=0;outcome=TileReplacementFixture.Native(()=>{if(++rechecks==2)TileReplacementFixture.Plan.SetFixtureProduct(foreignProduct);});
        Check(!outcome.ActionAttempted&&TileReplacementFixture.Controller.RestoreCalls==0&&ReferenceEquals(TileReplacementFixture.Plan.ProductInfoScreen,foreignProduct)
            &&foreignProduct.currentDef!=null,"foreign product/panel after final revalidation prevents gesture and cleanup that would clear it");
        TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>OniMcp.Server.GameContextLifecycle.Generation++;
        outcome=TileReplacementFixture.Native();Check(outcome.UiRestored==null&&TileReplacementFixture.Controller.RestoreCalls==0,"changed scene generation prevents cleanup writes");
        TileReplacementFixture.Setup();TileReplacementFixture.Tool.OnBuild=cell=>{throw new InvalidOperationException("fixture native failure");};
        outcome=TileReplacementFixture.Native();Check(outcome.ActionAttempted&&outcome.ExceptionType=="InvalidOperationException"&&TileReplacementFixture.Tool.BuildCalls==1,"native exception retains one-attempt evidence without fallback");
    }
    private static void MaterialUiDiagnostics()
    {
        Check(JObject.FromObject(new NativeTileReplacement.Outcome().ToDictionary())["materialUiReadiness"].Type==JTokenType.Null,
            "material UI diagnostics remain null before the guarded path is observed");
        TileReplacementFixture.Setup();var outcome=TileReplacementFixture.Native();
        var state=(JObject)JObject.FromObject(outcome.ToDictionary())["materialUiReadiness"];
        Check(new[]{"productPresent","productIsSpawned","panelPresent","panelIsSpawned","priorityPresent","priorityIsSpawned","priorityCacheReady",
                "firstSelectorPresent","firstSelectorIsSpawned","panelActive","recipeMatches"}.All(key=>state[key]?.Value<bool>()==true)
            &&state["selectorCount"]?.Value<int>()==3&&outcome.ActionAttempted,
            "ready typed native caches are observed without changing the successful gesture");
        foreach(string component in new[]{"product","panel","priority","firstSelector"})
        {
            TileReplacementFixture.Setup();
            if(component=="product")TileReplacementFixture.Product.isSpawned=false;
            else if(component=="panel")TileReplacementFixture.Materials.isSpawned=false;
            else if(component=="priority")TileReplacementFixture.Priority.isSpawned=false;
            else TileReplacementFixture.Selectors[0].isSpawned=false;
            outcome=TileReplacementFixture.Native();state=(JObject)JObject.FromObject(outcome.ToDictionary())["materialUiReadiness"];
            Check(state[component+"Present"]?.Value<bool>()==true&&state[component+"IsSpawned"]?.Value<bool>()==false
                &&state["selectorCount"]?.Value<int>()==3&&state["recipeMatches"]?.Value<bool>()==true
                &&!outcome.UiInitializing&&outcome.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==1
                &&TileReplacementFixture.Materials.MaterialSelectCalls==1,
                "unspawned "+component+" remains diagnosed without rejecting its initialized native operation caches");
        }
        TileReplacementFixture.Setup();TileReplacementFixture.Materials.FixturePriority=null;outcome=TileReplacementFixture.Native();
        state=(JObject)JObject.FromObject(outcome.ToDictionary())["materialUiReadiness"];
        Check(state["priorityPresent"]?.Value<bool>()==false&&state["priorityIsSpawned"].Type==JTokenType.Null&&state["priorityCacheReady"].Type==JTokenType.Null
            &&state["firstSelectorPresent"]?.Value<bool>()==true&&outcome.UiInitializing&&!outcome.ActionAttempted,
            "an observed missing priority differs from its unobserved spawn state");
        TileReplacementFixture.Setup();TileReplacementFixture.Plan.OnCopy=(def,facade)=>
        {
            TileReplacementFixture.Product.currentDef=def;
            TileReplacementFixture.Tool.Activate(def,new List<Tag>{new Tag("GoldAmalgam")},facade);
            TileReplacementFixture.Product.materialSelectionPanel=null;
        };
        outcome=TileReplacementFixture.Native();state=(JObject)JObject.FromObject(outcome.ToDictionary())["materialUiReadiness"];
        Check(state["productPresent"]?.Value<bool>()==true&&state["panelPresent"]?.Value<bool>()==false
            &&new[]{"panelIsSpawned","priorityPresent","priorityIsSpawned","priorityCacheReady","firstSelectorPresent","firstSelectorIsSpawned",
                "panelActive","recipeMatches","selectorCount"}.All(key=>state[key].Type==JTokenType.Null)
            &&outcome.UiInitializing&&!outcome.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==0,
            "a missing parent panel keeps descendant cache observations null without enabling construction");
    }
    private static void PriorityCacheReadiness()
    {
        TileReplacementFixture.Setup();
        TileReplacementFixture.Product.isSpawned=false;TileReplacementFixture.Materials.isSpawned=false;
        TileReplacementFixture.Priority.isSpawned=false;foreach(var selector in TileReplacementFixture.Selectors)selector.isSpawned=false;
        var outcome=TileReplacementFixture.Native();
        var state=(JObject)JObject.FromObject(outcome.ToDictionary())["materialUiReadiness"];
        Check(outcome.ActionAttempted&&outcome.UiRestored==true&&outcome.PriorityRestored==true
            &&TileReplacementFixture.Priority.GetLastSelectedPriority().priority_value==3
            &&new[]{"productIsSpawned","panelIsSpawned","priorityIsSpawned","firstSelectorIsSpawned"}.All(key=>state[key]?.Value<bool>()==false),
            "all unspawned UI components can use initialized native caches and restore the original known priority");
        foreach(string part in new[]{"list","count","button","toggle","tooltip","high","emergency","emergency_toggle","priority"})
        {
            TileReplacementFixture.Setup();TileReplacementFixture.Priority.FixtureBreakCache(part);
            int priorWrites=TileReplacementFixture.Priority.SetCalls;outcome=TileReplacementFixture.Native();
            Check(outcome.UiInitializing&&!outcome.ActionAttempted&&!outcome.PrioritySetAttempted
                &&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Materials.MaterialSelectCalls==0
                &&TileReplacementFixture.Priority.SetCalls==priorWrites
                &&JObject.FromObject(outcome.ToDictionary())["materialUiReadiness"]?["priorityCacheReady"]?.Value<bool>()==false,
                "missing native priority cache "+part+" refuses before material/priority writes and construction");
        }
        foreach(string part in new[]{"product","panel","selector"})
        {
            TileReplacementFixture.Setup();
            if(part=="product")TileReplacementFixture.Product.gameObject.Active=false;
            else if(part=="panel")TileReplacementFixture.Materials.gameObject.Active=false;
            else TileReplacementFixture.Selectors[0].gameObject.Active=false;
            outcome=TileReplacementFixture.Native();
            Check(!outcome.ActionAttempted&&TileReplacementFixture.Tool.BuildCalls==0&&TileReplacementFixture.Materials.MaterialSelectCalls==0,
                "inactive native "+part+" is not bypassed by initialized caches");
        }
        TileReplacementFixture.Setup();TileReplacementFixture.Priority.gameObject.Active=false;outcome=TileReplacementFixture.Native();
        Check(outcome.ActionAttempted&&outcome.PriorityRestored==true,
            "native cached priority operations do not require the priority screen itself to be visible");
    }
}
