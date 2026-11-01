using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new InvalidOperationException("Candidate anchor: " + reason); }
    private static Dictionary<string, object> Preview(int x, int y)
    {
        return new Dictionary<string, object> {
            ["valid"] = true,
            ["placement"] = new Dictionary<string, object> { ["width"] = 1, ["height"] = 1 },
            ["footprint"] = new List<Dictionary<string, object>> { new Dictionary<string, object> { ["x"] = x, ["y"] = y } },
            ["support"] = new Dictionary<string, object> { ["valid"] = true, ["warningOnly"] = false },
            ["x"] = x, ["y"] = y
        };
    }
    private static JObject Invoke(JObject args)
    {
        var result = BuildPlanningTools.FindPlacementCandidates().Handler(args);
        Check(!result.IsError, "handler accepts fixture inputs");
        return JObject.Parse(result.Content.Single().Text);
    }
    private static void AnchorCases(bool relative, int world)
    {
        Fixture.Rect = new Dictionary<string, int> { ["x1"] = 11, ["y1"] = 23, ["x2"] = 12, ["y2"] = 24 };
        Fixture.World = world; Fixture.Calls = 0; Fixture.Preview = Preview;
        Fixture.Area = relative ? new AreaHandle { Id = "fixture-area", X1 = 10, Y1 = 20 } : null;
        var args = new JObject { ["prefabId"] = "FixtureBuilding", ["limit"] = 4, ["worldId"] = world };
        if (relative) args["areaId"] = "fixture-area";
        var result = Invoke(args);
        Check(Fixture.Calls == 4 && (int)result["scannedAnchors"] == 4, "candidate scan count unchanged");
        var rows = (JArray)result["candidates"];
        Check(rows.Count == 4, "all fixture candidates returned");
        var expected = new[] { new[] {11,23}, new[] {12,23}, new[] {11,24}, new[] {12,24} };
        for (int i = 0; i < rows.Count; i++)
        {
            var anchor = rows[i]["anchor"];
            Check(anchor["x"] != null && anchor["y"] != null && anchor["worldId"] != null,
                "absolute anchor fields are present before optional relative decoration");
            Check((int)anchor["x"] == expected[i][0] && (int)anchor["y"] == expected[i][1], "absolute anchor and stable y/x order");
            Check((int)anchor["worldId"] == world && (string)anchor["coordRole"] == "lowerLeftCell", "world and lower-left role retained");
            Check((int)rows[i]["preview"]["x"] == (int)anchor["x"] && (int)rows[i]["preview"]["y"] == (int)anchor["y"], "anchor matches actual preview coordinates");
            if (relative)
            {
                Check((string)anchor["areaId"] == "fixture-area" && (int)anchor["rx"] == expected[i][0] - 10
                    && (int)anchor["ry"] == expected[i][1] - 20, "relative decoration preserves absolute anchor");
                Check(((JArray)anchor["origin"]).Select(v => (int)v).SequenceEqual(new[] {10,20}), "relative origin retained");
            }
            else Check(anchor["areaId"] == null && anchor["rx"] == null, "no invented relative area");
        }
        Check(JToken.DeepEquals(result["bestCandidate"], rows[0]), "bestCandidate projects exact first sorted candidate");
    }
    private static void SortProjectionCase()
    {
        // Exercise the actual projection's tie-break keys with reversed input.
        var type = typeof(BuildPlanningTools).GetNestedType("PlacementCandidate", System.Reflection.BindingFlags.NonPublic);
        var x = type.GetProperty("AnchorX"); var y = type.GetProperty("AnchorY"); var anchorField = type.GetField("Anchor");
        var values = new[] { new[] {12,24}, new[] {12,23}, new[] {11,24}, new[] {11,23} };
        var candidates = values.Select(v => { var item = Activator.CreateInstance(type, true); anchorField.SetValue(item, new Dictionary<string, object> { ["x"] = v[0], ["y"] = v[1] }); return item; }).ToList();
        var sorted = candidates.OrderBy(v => (int)y.GetValue(v)).ThenBy(v => (int)x.GetValue(v)).ToList();
        Check((int)x.GetValue(sorted[0]) == 11 && (int)y.GetValue(sorted[0]) == 23, "actual anchor projections support deterministic sort");
    }
    private static JObject BudgetInvoke(int? budget = null, int offset = 0, int limit = 1)
    {
        Fixture.Rect = new Dictionary<string, int> { ["x1"] = 11, ["y1"] = 23, ["x2"] = 30, ["y2"] = 42 };
        Fixture.World = 7; Fixture.Calls = 0; Fixture.Preview = Preview; Fixture.Area = null;
        var args = new JObject { ["prefabId"] = "FixtureBuilding", ["worldId"] = 7, ["scanOffset"] = offset, ["limit"] = limit };
        if (budget.HasValue) args["maxEvaluations"] = budget.Value;
        return Invoke(args);
    }
    private static void LocalizationCases()
    {
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            foreach (string key in new[] { "max_evaluations", "scan_offset", "limit", "max_cells", "scope_note", "next_partial", "no_viable", "offset_error", "invalid_rect", "area_too_large" })
            {
                string fullKey = "placement_candidates." + key;
                string value = OniMcp.Support.McpLocalization.GetForLanguage(language, fullKey);
                Check(value != fullKey && !string.IsNullOrWhiteSpace(value), "runtime translation present " + language + " " + fullKey);
            }
            Check(OniMcp.Support.McpLocalization.GetForLanguage(language, "tools.building_control.parameters.maxEvaluations")
                == OniMcp.Support.McpLocalization.GetForLanguage(language, "placement_candidates.max_evaluations"), "canonical aggregate evaluation description matches " + language);
            Check(OniMcp.Support.McpLocalization.GetForLanguage(language, "tools.building_control.parameters.scanOffset")
                == OniMcp.Support.McpLocalization.GetForLanguage(language, "placement_candidates.scan_offset"), "canonical aggregate cursor description matches " + language);
        }
    }
    private static void BudgetCases()
    {
        var value = BudgetInvoke();
        Check(Fixture.Calls == 64 && (int)value["scannedAnchors"] == 64,
            "default evaluation budget bounds full placement calls independently of return limit");
        Check((int)value["totalAnchors"] == 400 && (int)value["remainingAnchors"] == 336,
            "total coordinate anchors and unobserved remainder are distinct");
        Check((int)value["nextOffset"] == 64 && (bool)value["hasMore"] && !(bool)value["allAnchorsEvaluated"] && (bool)value["truncated"],
            "partial first window has an explicit continuation cursor");
        Check((string)value["countsScope"] == "evaluated_window" && (string)value["rankingScope"] == "evaluated_window",
            "ranking and counts are explicitly window-scoped");
        foreach (string key in new[] { "validCountTotal", "warningCountTotal", "rejectedCountTotal" })
            Check(value[key] != null && value[key].Type == JTokenType.Null, "incomplete total remains explicit null " + key);
        Check(((JArray)value["candidates"]).Count == 1, "returned row budget remains separate");
        var largerReturn = BudgetInvoke(limit: 50);
        Check(Fixture.Calls == 64 && ((JArray)largerReturn["candidates"]).Count == 50, "changing limit does not change native evaluation work");
        var page = BudgetInvoke(offset: 67);
        Check(Fixture.Calls == 64 && (int)page["nextOffset"] == 131 && (int)page["unobservedBefore"] == 67,
            "cursor resumes exact deterministic row-major window");
        var anchor = page["candidates"][0]["anchor"];
        Check((int)anchor["x"] == 18 && (int)anchor["y"] == 26, "non-row-aligned cursor resolves actual absolute anchor");
        Check((int)page["omittedAnchors"] == 336 && page["validCountTotal"].Type == JTokenType.Null,
            "earlier skipped anchors are not fabricated as evaluated");
        var last = BudgetInvoke(offset: 390);
        Check(Fixture.Calls == 10 && !(bool)last["hasMore"] && last["nextOffset"].Type == JTokenType.Null,
            "final window stops at exact rectangle end");
        Check(!(bool)last["allAnchorsEvaluated"] && (bool)last["truncated"] && last["validCountTotal"].Type == JTokenType.Null,
            "last page is not a globally complete snapshot");
        var empty = BudgetInvoke(offset: 400);
        Check(Fixture.Calls == 0 && ((JArray)empty["candidates"]).Count == 0 && !(bool)empty["hasMore"], "end cursor has zero work and no invented candidate");
        var clamped = BudgetInvoke(budget: 10000);
        Check(Fixture.Calls == 256 && (int)clamped["maxEvaluations"] == 256 && (int)clamped["remainingAnchors"] == 144,
            "strict evaluation maximum is 256");
        var minimum = BudgetInvoke(budget: 0);
        Check(Fixture.Calls == 1 && (int)minimum["maxEvaluations"] == 1, "evaluation budget clamps to one minimum");
        AnchorCases(false,7);
        Fixture.Calls = 0;
        var small = Invoke(new JObject { ["prefabId"] = "FixtureBuilding", ["worldId"] = 7 });
        Check((bool)small["allAnchorsEvaluated"] && !(bool)small["truncated"] && (int)small["validCountTotal"] == 4,
            "small complete scan may report exact totals");
        foreach (int invalid in new[] { -1, 401 })
        {
            Fixture.Rect = new Dictionary<string, int> { ["x1"] = 11, ["y1"] = 23, ["x2"] = 30, ["y2"] = 42 }; Fixture.Calls = 0;
            var error = BuildPlanningTools.FindPlacementCandidates().Handler(new JObject { ["prefabId"] = "FixtureBuilding", ["scanOffset"] = invalid });
            Check(error.IsError && Fixture.Calls == 0, "invalid cursor rejects before native placement work");
        }
        Fixture.Rect = new Dictionary<string, int> { ["x1"] = int.MinValue, ["y1"] = int.MinValue, ["x2"] = int.MaxValue, ["y2"] = int.MaxValue }; Fixture.Calls = 0;
        var huge = BuildPlanningTools.FindPlacementCandidates().Handler(new JObject { ["prefabId"] = "FixtureBuilding" });
        Check(huge.IsError && Fixture.Calls == 0, "rectangle guard is not bypassed by integer overflow");
    }
    private static void UnknownCases()
    {
        Fixture.RequireContext=false;Fixture.Rect=new Dictionary<string,int>{{"x1",0},{"y1",0},{"x2",3},{"y2",0}};Fixture.World=7;Fixture.Area=null;Fixture.Calls=0;
        Fixture.Preview=(x,y)=>new Dictionary<string,object>{["valid"]=x==0||x==3,["x"]=x,["y"]=y,["error"]="unknown is arbitrary prose",["support"]=x==3?new Dictionary<string,object>{{"status","not_evaluated"},{"buildLocationRule","CustomNativeRule"}}:null,["details"]=x==2?new Dictionary<string,object>{{"assessmentState","unknown"},{"reasonCode","world_inventory_missing"}}:null};
        var all=Invoke(new JObject{["prefabId"]="FixtureBuilding",["worldId"]=7,["includeRejected"]=true});var rows=(JArray)all["candidates"];
        Check(rows.Any(row=>(int)row["preview"]["x"]==3&&(string)row["status"]=="unknown"),"explicit unevaluated native support remains unknown despite MCP valid=true");
        Check((int)all["validCount"]==2&&(int)all["rejectedCount"]==2&&(int)all["unknownCount"]==2&&(int)all["knownRejectedCount"]==1,"MCP boolean compatibility counts remain distinct from known rejection and unknown");
        Check((int)all["unknownValidCount"]==1&&(int)all["unknownRejectedCount"]==1&&((JObject)all["classificationCounts"]).Properties().Sum(item=>(int)item.Value)==4,"unknown overlaps are explicitly partitioned and disjoint classification counts sum to evaluated anchors");
        Check(rows.Any(row=>(int)row["preview"]["x"]==1&&(string)row["status"]=="invalid"),"arbitrary error prose containing unknown does not change classification");
        Check(rows.Any(row=>(int)row["preview"]["x"]==2&&(string)row["status"]=="unknown"&&row["preview"]["details"]["reasonCode"].ToString()=="world_inventory_missing"),"refused unknown row retains exact evidence");
        var filtered=Invoke(new JObject{["prefabId"]="FixtureBuilding",["worldId"]=7});Check(((JArray)filtered["candidates"]).Count==1&&(string)filtered["bestCandidate"]["status"]=="valid","default prioritizes known valid rows and preserves unknown in counts");
        Fixture.Rect=new Dictionary<string,int>{{"x1",0},{"y1",0},{"x2",0},{"y2",0}};
        foreach(var supportStatus in new[]{"unknown","not_evaluated"})
        {
            Fixture.Preview=(x,y)=>new Dictionary<string,object>{{"valid",false},{"details",new Dictionary<string,object>{{"support",new Dictionary<string,object>{{"status",supportStatus},{"buildLocationRule","NativeRule"}}}}}};
            var value=Invoke(new JObject{["prefabId"]="FixtureBuilding"});Check((string)value["bestCandidate"]["status"]=="unknown"&&(int)value["unknownCountTotal"]==1,"unknown support fallback remains explicit "+supportStatus);
        }
        Fixture.Preview=(x,y)=>new Dictionary<string,object>{{"valid",false},{"details",new Dictionary<string,object>{{"availability",new Dictionary<string,object>{{"status","unknown"}}}}}};
        var unavailable=Invoke(new JObject{["prefabId"]="FixtureBuilding"});Check((string)unavailable["bestCandidate"]["status"]=="unknown","availability readiness is unknown rather than known invalid");
        Fixture.Preview=(x,y)=>new Dictionary<string,object>{{"valid",true},{"materialSelection",new Dictionary<string,object>{{"valid",true},{"quantityStatus","unknown"}}}};
        var quantity=Invoke(new JObject{["prefabId"]="FixtureBuilding"});Check((string)quantity["bestCandidate"]["status"]=="unknown","explicit unknown quantity on compatible material is retained");
        Fixture.Preview=(x,y)=>new Dictionary<string,object>{{"valid",true},{"support",new Dictionary<string,object>{{"status","not_required"},{"nativeFoundationValid",null}}}};
        var notRequired=Invoke(new JObject{["prefabId"]="FixtureBuilding"});Check((string)notRequired["bestCandidate"]["status"]=="valid"&&(int)notRequired["unknownCount"]==0,"not-required support is distinct from unknown");
        Fixture.Preview=(x,y)=>new Dictionary<string,object>{{"valid",true},{"validationScope","existing_object_reuse"},{"support",new Dictionary<string,object>{{"status","not_evaluated"},{"nativeFoundationValid",null}}}};
        var reuse=Invoke(new JObject{["prefabId"]="FixtureBuilding"});Check((string)reuse["bestCandidate"]["status"]=="valid","existing-object reuse does not invent a missing support evaluation requirement");
        Fixture.RequireContext=true;
    }
    private static void GatewayCases()
    {
        var entry=BuildingControlTools.ControlBuilding();
        foreach(string key in new[]{"maxEvaluations","scanOffset","includeRejected"})
        {
            Check(entry.Parameters.ContainsKey(key)&&entry.Parameters[key].Type==(key=="includeRejected"?"boolean":"integer")&&!entry.Parameters[key].Required,"supported outer schema advertises optional "+key);
            Check(BuildPlanningTools.ControlBuildPlanning().Parameters.ContainsKey(key),"full planning dispatcher merges candidate schema "+key);
            Check(!entry.Parameters[key].Description.StartsWith("placement_candidates.",StringComparison.Ordinal),"full outer schema runtime description resolves "+key);
        }
        Fixture.Rect=new Dictionary<string,int>{{"x1",11},{"y1",23},{"x2",30},{"y2",42}};Fixture.World=7;Fixture.Calls=0;Fixture.ContextCreations=0;Fixture.Preview=Preview;Fixture.Area=null;
        var args=new JObject{["domain"]="planning",["action"]="placement_candidates",["prefabId"]="FixtureBuilding",["worldId"]=7,["maxEvaluations"]=7,["scanOffset"]=67,["limit"]=1};
        string before=args.ToString();var call=entry.Handler(args);Check(!call.IsError,"supported outer route accepts candidate read");var result=JObject.Parse(call.Content[0].Text);
        Check(Fixture.Calls==7&&(int)result["nextOffset"]==74&&(int)result["scannedAnchors"]==7,"full outer and child dispatchers forward exact evaluation/cursor payload");
        Check(args.ToString()==before,"outer and child forwarding do not mutate caller arguments");
        Check(Fixture.ContextCreations==1&&Fixture.LastContext!=null,"real candidate handler creates and forwards exactly one context for the window");
        Check(Fixture.LastArgs["domain"]==null&&Fixture.LastArgs["action"]==null&&(int)Fixture.LastArgs["maxEvaluations"]==7&&(int)Fixture.LastArgs["scanOffset"]==67,"deep-cloned forward payload retains new keys through both wrappers");
    }
    public static void Main(string[] args)
    {
        if(args.Length>0&&args[0]=="unknown") {UnknownCases();return;}
        AnchorCases(false,7); AnchorCases(true,7); AnchorCases(false,3); SortProjectionCase(); BudgetCases(); LocalizationCases(); UnknownCases(); GatewayCases();
        Console.WriteLine("Candidate bounded-scan proposal: " + checks + " actual-handler/model/helper checks PASS; native placement supplied by fixture");
    }
}
