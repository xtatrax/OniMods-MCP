using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static Klei.AI.Amount definition;
    private static void Main()
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        McpLocalization.LanguageProvider=()=>"en";
        DiagnosticScope();
        MissingAndFaults(); ValuesAndBoundaries(); PartialAndGlobalScope(); CriticalAndUnrelatedPreservation(); EmptyAndRepeat(); LocalesAndRoutes();
        Console.WriteLine("Diagnostic/survival stress: "+checks+" host checks passed (native boundaries substituted)");
    }
    private static void DiagnosticScope()
    {
        foreach(string language in new[]{"en","ja","zh-Hans"})
        {
            Reset();Add(0);McpLocalization.LanguageProvider=()=>language;
            var diagnostic=Diag();var scope=diagnostic["alertCoverage"];
            Check(scope!=null,"diagnostic_scope_contract: diagnostics exposes source coverage");
            Check((string)scope["source"]=="mcp_rule_checks"&&(string)scope["model"]=="diagnostics","diagnostics identifies its own rule model");
            Check((string)scope["populationScope"]=="all_live_worlds"&&(string)scope["buildingScope"]=="all_completed_worlds","legacy global population and building scope remain explicit");
            Check((int)scope["sampledCycle"]==(int)diagnostic["cycle"],"scope records the same evaluated cycle");
            Null(scope,"sampledTimeOfDayPercent");Null(scope,"hudWarningCount");Null(scope,"dailyOxygenBalanceKg");
            Check(!(bool)scope["overallComplete"]&&(bool)scope["evaluated"],"successful MCP checks do not imply HUD coverage");
            Check((string)scope["oxygenCellCheck"]=="visible_stock_only","visible oxygen inventory is distinct from daily balance");
            Check(Alerts().Count==0,"legacy empty array is retained without inventing native warnings");
            string description=DiagnosticsTools.GetColonyAlerts().Description;
            Check(description==McpLocalization.Get("diagnostic_scope.alerts.description")&&!description.Contains("diagnostic_scope."),"legacy alert description is localized and defines the source");
            CleanCanaries();
        }
        McpLocalization.LanguageProvider=()=>"en";
    }
    private static void Check(bool c,string m) { checks++;if(!c)throw new InvalidOperationException("Diagnostic stress: "+m); }
    private static void Null(JToken p,string k) { Check(p[k]!=null&&p[k].Type==JTokenType.Null,k+" remains explicit serialized null"); }
    private static void Reset()
    {
        Components.LiveMinionIdentities.Items.Clear();Components.BuildingCompletes.Items.Clear();Components.Edibles.Items.Clear();Components.HarvestDesignatables.Items.Clear();
        NativeCanaries.ForbiddenCalls=0;Db.GetCalls=0;FoodAvailabilityReader.Calls=0;
        Game.Instance=new Game();Grid.Visible=true;ClusterManager.Instance=new ClusterManager();
        for(int i=0;i<Grid.CellCount;i++){Grid.WorldIdx[i]=7;Grid.Element[i]=new Element{id=SimHashes.Oxygen};Grid.Mass[i]=2;Grid.Solid[i]=false;Grid.Foundation[i]=false;}
        definition=new Klei.AI.Amount();Db.FixtureSet(new Db{Amounts=new Database.Amounts{Stress=definition}});
        FoodAvailabilityReader.Fixture=new FoodAccessBreakdown();
        FoodAvailabilityReader.Fixture.Add(new FoodAccessObservation{CaloriesKcal=100000,Reachable=true,PermittedReachableConsumer=true,Fetchable=true,StoredPrivate=false,BeingConsumed=false,AutomationOnlyStorage=false,FetchTotalAmount=1,UnreservedFetchAmount=1});
        Components.Edibles.Items.Add(new Edible{Calories=100000000});
        foreach(string id in new[]{"OxygenDiffuser","Outhouse","ResearchCenter","Battery","Bed","Bed"})
            Components.BuildingCompletes.Items.Add(new BuildingComplete{Def=new BuildingDef{PrefabID=id},transform={Position=new Position{x=Components.BuildingCompletes.Items.Count,y=1}}});
    }
    private static MinionIdentity Add(float? value,int world=7)
    {
        var d=new MinionIdentity{World=world};Components.LiveMinionIdentities.Items.Add(d);
        if(value.HasValue)d.NativeAmounts[definition]=new Klei.AI.AmountInstance{amount=definition,value=value.Value};
        return d;
    }
    private static JToken Data(CallToolResult result)
    { Check(!result.IsError,"real read handler succeeds");return JToken.Parse(result.Value); }
    private static JObject Diag() { return (JObject)Data(DiagnosticsTools.GetColonyDiagnostics().Handler(new JObject())); }
    private static JArray Alerts() { return (JArray)Data(DiagnosticsTools.GetColonyAlerts().Handler(new JObject())); }
    private static JObject Survival() { return (JObject)Data(SurvivalPlanTools.ControlSurvivalPlan().Handler(new JObject())); }
    private static bool Has(JToken rows,string c,string severity=null)
    { return rows.Any(x=>(string)x["category"]==c&&(severity==null||(string)x["severity"]==severity)); }
    private static void Coverage(JToken c,int observed,int unknown,string status)
    {
        Check((int)c["observed"]==observed&&(int)c["unknown"]==unknown,"coverage keeps independent observed and unknown counts");
        Check((bool)c["complete"]==(unknown==0)&&(string)c["status"]==status,"coverage keeps explicit completion/status");
    }
    private static void BothCoverage(JObject diag,JObject survival,int observed,int unknown,string status)
    { Coverage(diag["stressCoverage"],observed,unknown,status);Coverage(survival["metrics"]["stressCoverage"],observed,unknown,status); }
    private static void CleanCanaries()
    {
        Check(Db.GetCalls==0,"no eager/lazy Db.Get fallback");
        Check(Components.LiveMinionIdentities.Items.Where(x=>x!=null).All(x=>x.InvalidAmountComponentReads==0),"invalid GetComponent<Amounts> is never consulted");
        Check(NativeCanaries.ForbiddenCalls==0,"settings/refresh/disinfect/navigation side routes remain untouched");
    }
    private static void ValuesAndBoundaries()
    {
        foreach(float value in new[]{0f,17.25f,-3f,40f,40.1f,59.9f,60f,85.26f})
        {
            Reset();var d=Add(value);int writes=d.NativeAmounts[definition].Writes;
            var diag=Diag();var survival=Survival();double expected=Math.Round(value,1);
            Check((double)diag["maxStress"]==expected&&(double)survival["metrics"]["maxStress"]==expected,"finite native value and one-place rounding survive without clamp or kcal/percent conversion");
            BothCoverage(diag,survival,1,0,"complete");
            Check(Has(diag["alerts"],"stress","warning")== (value>40),"diagnostic threshold is strictly above forty");
            Check(Has(survival["blockers"],"stress","warning")== (value>=60),"survival threshold includes sixty");
            Check((bool)survival["canAttemptLongRun"]&&(string)survival["decision"]=="long_run_allowed_with_monitoring","complete known noncritical state preserves long-run result");
            Check(!Has(Alerts(),"stress_assessment_incomplete"),"alerts-only read does not fabricate incomplete info for known0/finite data");
            Check(d.NativeAmounts[definition].Writes==writes,"native amount setters are not called");CleanCanaries();
        }
        Reset();var fixedDupe=Add(18);var decoy=new Klei.AI.Amount{Id="Stress",Name="Stress"};fixedDupe.NativeAmounts[decoy]=new Klei.AI.AmountInstance{amount=decoy,value=99};
        Check((double)Diag()["maxStress"]==18&&definition.Lookups>0,"fixed native definition wins over text-match decoy");CleanCanaries();
    }
    private static void MissingAndFaults()
    {
        foreach(string mode in new[]{"cache","definitions","definition","lookup","value","nan","positive_infinity","negative_infinity","instance","null_live","definition_throw"})
        {
            Reset();var d=Add(20);
            if(mode=="cache")Db.FixtureSet(null);
            if(mode=="definitions")Db.FixtureCurrent.Amounts=null;
            if(mode=="definition")Db.FixtureCurrent.Amounts.Stress=null;
            if(mode=="lookup")definition.ThrowLookup=true;
            if(mode=="value")d.NativeAmounts[definition].ThrowValue=true;
            if(mode=="nan")d.NativeAmounts[definition].value=float.NaN;
            if(mode=="positive_infinity")d.NativeAmounts[definition].value=float.PositiveInfinity;
            if(mode=="negative_infinity")d.NativeAmounts[definition].value=float.NegativeInfinity;
            if(mode=="instance")d.NativeAmounts.Clear();
            if(mode=="null_live"){Components.LiveMinionIdentities.Items.Clear();Components.LiveMinionIdentities.Items.Add(null);}
            if(mode=="definition_throw")Db.FixtureCurrent.Amounts.ThrowStress=true;
            var diag=Diag();var survival=Survival();Null(diag,"maxStress");Null(survival["metrics"],"maxStress");BothCoverage(diag,survival,0,1,"unknown");
            Null(survival,"canAttemptLongRun");Check((string)survival["decision"]=="assessment_incomplete","unknown noncritical population is not declared ready or classified as known critical");
            var alerts=Alerts();Check(Has(alerts,"stress_assessment_incomplete","info"),"alerts-only shape carries localized unknown stress info for "+mode);
            Check(!Has(alerts,"stress","warning")&&!Has(survival["blockers"],"stress","warning"),"missing data does not manufacture numeric stress warning");CleanCanaries();
        }
    }
    private static void PartialAndGlobalScope()
    {
        foreach(float known in new[]{0f,45f,80f})
        {
            Reset();Add(null);Add(known,99);var diag=Diag();var survival=Survival();
            Null(diag,"maxStress");Null(survival["metrics"],"maxStress");BothCoverage(diag,survival,1,1,"partial");
            Check(Has(diag["alerts"],"stress","warning")== (known>40),"partial known-high diagnostic warning survives");
            Check(Has(survival["blockers"],"stress","warning")== (known>=60),"partial known-high survival warning survives");
            var a=Alerts();Check(Has(a,"stress_assessment_incomplete","info")&&Has(a,"stress","warning")== (known>40),"alerts-only incomplete info and known warning coexist");
            Null(survival,"canAttemptLongRun");CleanCanaries();
        }
        Reset();Add(2);Add(90,99);var full=Diag();var plan=Survival();BothCoverage(full,plan,2,0,"complete");
        Check((double)full["maxStress"]==90&&(double)plan["metrics"]["maxStress"]==90,"legacy global population is not narrowed to the active world's oxygen scope");
        Reset();Add(-8);Add(-3);Check((double)Diag()["maxStress"]== -3,"finite negative maximum is not silently clamped to zero");
    }
    private static void CriticalAndUnrelatedPreservation()
    {
        Reset();Add(0);var knownD=Diag();var knownS=Survival();
        Components.LiveMinionIdentities.Items[0].NativeAmounts.Clear();var unknownD=Diag();var unknownS=Survival();
        foreach(string key in new[]{"cycle","duplicants","foodKcal","foodScope","foodAccess","visibleOnly","oxygenKgVisible","pollutedOxygenKgVisible","breathableCellsVisible","buildings"})
            Check(JToken.DeepEquals(knownD[key],unknownD[key]),"unknown stress preserves diagnostics "+key);
        foreach(string key in new[]{"cycle","dupes","foodKcal","foodAccess","foodPolicy","visibleOnly","foodNeedKcal","oxygenProducers","toilets","beds","readyHarvestables","reachableHarvestables"})
            Check(JToken.DeepEquals(knownS["metrics"][key],unknownS["metrics"][key]),"unknown stress preserves survival "+key);
        Check(FoodAvailabilityReader.Calls==4,"real callers retain one independent native food capture each");
        FoodAvailabilityReader.Fixture.Available=0;FoodAvailabilityReader.Fixture.UnknownAvailabilityItems=1;Components.BuildingCompletes.Items.Clear();
        for(int i=0;i<Grid.CellCount;i++)Grid.Element[i]=null;
        var badD=Diag();var badS=Survival();
        foreach(string category in new[]{"food","food_assessment_incomplete","oxygen","hygiene","sleep"})
        { Check(Has(badD["alerts"],category),"diagnostics preserves existing "+category+" alert");Check(Has(badS["blockers"],category),"survival preserves existing "+category+" blocker"); }
        Check(!(bool)badS["canAttemptLongRun"]&&(string)badS["decision"]=="fix_critical_before_100_cycle_run","known critical food assessment overrides unknown stress with original false/critical decision");
        Null(badS["metrics"],"maxStress");Check(Has(Alerts(),"stress_assessment_incomplete","info"),"known critical does not hide diagnostics data-quality info");CleanCanaries();
    }
    private static void EmptyAndRepeat()
    {
        Reset();bool nullSetRejected=false;try{DupeStressSummary.Read(null);}catch(ArgumentNullException){nullSetRejected=true;}Check(nullSetRejected,"missing population enumerable is an explicit failure, not a fabricated unknown person");var d=Diag();var s=Survival();Null(d,"maxStress");Null(s["metrics"],"maxStress");BothCoverage(d,s,0,0,"empty");
        Check(!(bool)s["canAttemptLongRun"]&&Has(s["blockers"],"dupes","critical"),"empty population preserves existing no-dupe critical");
        Check(!Has(Alerts(),"stress_assessment_incomplete"),"empty complete set is distinct from missing people");
        var dupe=Add(33);Check((double)Diag()["maxStress"]==33,"read obtains current successful value");dupe.NativeAmounts.Clear();Null(Diag(),"maxStress");
        dupe.NativeAmounts[definition]=new Klei.AI.AmountInstance{amount=definition,value=0};Check((double)Diag()["maxStress"]==0,"later observed0 is independent from prior failure");CleanCanaries();
    }
    private static void LocalesAndRoutes()
    {
        foreach(string language in new[]{"en","ja","zh-Hans"})
        {
            Reset();Add(null);Add(80);McpLocalization.LanguageProvider=()=>language;
            var diag=Diag();var s=Survival();var a=Alerts();
            string partial=McpLocalization.Format("diagnostic_stress.warning.partial",null,80d);
            string incomplete=McpLocalization.Get("diagnostic_stress.assessment_incomplete");
            Check(!partial.Contains("diagnostic_stress.")&&!incomplete.Contains("diagnostic_stress."),"new text is resolved in "+language);
            Check((string)diag["alerts"].First(x=>(string)x["category"]=="stress")["message"]==partial&&(string)s["blockers"].First(x=>(string)x["category"]=="stress")["message"]==partial,"both real callers use localized partial warning in "+language);
            Check((string)a.First(x=>(string)x["category"]=="stress_assessment_incomplete")["message"]==incomplete,"alerts-only localized assessment info in "+language);
            Components.LiveMinionIdentities.Items.RemoveAt(0);string complete=McpLocalization.Format("diagnostic_stress.warning.complete",null,80d);
            Check((string)Diag()["alerts"].First(x=>(string)x["category"]=="stress")["message"]==complete,"complete warning uses its separate localized meaning in "+language);
        }
        McpLocalization.LanguageProvider=()=>"en";Reset();Add(0);
        var alias=(JObject)Data(DiagnosticsTools.ControlDiagnostics().Handler(new JObject{["action"]="summary"}));Check((double)alias["maxStress"]==0,"real diagnostics control alias reaches production read caller");
        Check(Data(DiagnosticsTools.ControlDiagnostics().Handler(new JObject{["action"]="alerts"})) is JArray,"real alerts route retains array shape");
        Game.Instance=null;Check(DiagnosticsTools.GetColonyDiagnostics().Handler(new JObject()).IsError&&SurvivalPlanTools.ControlSurvivalPlan().Handler(new JObject()).IsError,"existing game-not-initialized guards retained");CleanCanaries();
    }
}
