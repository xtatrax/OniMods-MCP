using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class AutoPowerResultRegression
{
    private static int checks;
    private static readonly string[] CountNames = { "planned", "reusedExisting", "failed", "digNewlyQueued", "digAlreadyQueued" };
    private static readonly string[] TopCountNames = { "planned", "reused", "failed", "autoDigQueued", "autoDigAlreadyQueued" };
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new InvalidOperationException("Auto power result: " + reason); }
    private static JObject Complete(bool preview = false) => new JObject
    {
        ["success"] = true, ["complete"] = true, ["dryRun"] = preview, ["committed"] = !preview,
        ["planned"] = preview ? 0 : 2, ["reusedExisting"] = preview ? 3 : 1, ["failed"] = 0,
        ["digNewlyQueued"] = 0, ["digAlreadyQueued"] = 0,
        ["connectionPlanApplied"] = !preview, ["networkConnected"] = JValue.CreateNull(),
        ["constructionComplete"] = false, ["flowVerified"] = false,
        ["results"] = new JArray(new JObject { ["fixture"] = "preserved child result" })
    };
    private static CallToolResult Child(JObject payload, bool error = false)
    {
        var value = CallToolResult.Text(payload.ToString(Formatting.None)); value.IsError = error; return value;
    }
    private static JObject Wire(Dictionary<string, object> value)
        => JObject.Parse(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
    private static JObject Project(JObject payload, bool preview = false, bool error = false)
    {
        var child = Child(payload, error); string before = JsonConvert.SerializeObject(child);
        var result = Wire(AutoConnectPowerResultProjection.Project(child, preview));
        Check(JsonConvert.SerializeObject(child) == before, "projection does not change the child envelope or original text");
        Check(JToken.DeepEquals(result["connectResult"], payload), "primary child payload is retained losslessly");
        return result;
    }
    private static void Null(JObject result, string field, string why)
        => Check(result.Property(field) != null && result[field].Type == JTokenType.Null, why);
    internal static void Run()
    {
        TestRefusalAndIncomplete();
        TestPreviewAndExecution();
        TestTypedCountsAndFlags();
        TestPrimaryAndMalformed();
        TestActualAdapterBoundary();
        Console.WriteLine("Auto power result: " + checks + " host checks passed (linked projector and full parent adapter; child/native boundaries substituted)");
    }
    private static void TestRefusalAndIncomplete()
    {
        // Exact early-rejection shape: the original parent called this connected_or_planned.
        var early = new JObject { ["success"] = false, ["committed"] = false,
            ["reasonCode"] = "utility_path_conflict", ["phase"] = "path_preflight",
            ["pathCells"] = 3, ["conflicts"] = new JArray(new JObject { ["cell"] = 23, ["prefabId"] = "WireRefined" }) };
        foreach (bool error in new[] { false, true })
        {
            var rejected = Project(early, error: error);
            Check((string)rejected["status"] == "rejected" && (bool)rejected["childIsError"] == error,
                "MCP preflight refusal is rejected with or without envelope error flag");
            foreach (string field in TopCountNames) Null(rejected, field, "unreported early-refusal counts are not invented zeros: " + field);
            Check(((JArray)rejected["unknownCountFields"]).Count == 5, "all missing child counts remain explicitly unknown");
        }
        var failed = Complete(); failed["planned"] = 0; failed["reusedExisting"] = 0; failed["committed"] = false;
        var outerError = Project(failed, error: true);
        Check((string)outerError["status"] == "rejected" && !(bool)outerError["childCommitted"], "IsError wins over optimistic success/complete flags");
        var envelopeOnly=Project(Complete(),error:true);
        Check((string)envelopeOnly["status"]=="partially_planned" && (bool)envelopeOnly["childIsError"],
            "envelope error cannot be ignored when all payload success/count flags are optimistic");
        failed["success"] = false;
        Check((string)Project(failed)["status"] == "rejected", "explicit unsuccessful uncommitted child is rejected");
        var partial = Complete(); partial["success"] = false; partial["complete"] = false; partial["failed"] = 1;
        var progress = Project(partial, error: true);
        Check((string)progress["status"] == "partially_planned" && (int)progress["planned"] == 2
            && (int)progress["failed"] == 1, "actual wire progress and refusal are reported separately");
        var diggingOnly = (JObject)partial.DeepClone(); diggingOnly["planned"] = 0; diggingOnly["reusedExisting"] = 0; diggingOnly["digNewlyQueued"] = 2;
        var diggingResult=Project(diggingOnly,error:true);
        Check((string)diggingResult["status"] == "rejected" && (bool)diggingResult["childCommitted"]
            && (int)diggingResult["autoDigQueued"]==2, "queued digging alone is not partial wire planning or a no-change guarantee");
        var missing = Complete(); missing.Remove("failed");
        var unknown = Project(missing); Null(unknown,"failed","missing failed count is JSON null under real serialization settings");
        Check((string)unknown["status"] == "unknown", "success flags cannot turn missing failed into zero failures");
        Check((string)Project(new JObject())["status"] == "unknown", "empty object does not establish successful placement");
    }
    private static void TestPreviewAndExecution()
    {
        var planned = Project(Complete());
        Check((string)planned["status"] == "planned_or_reused" && (string)planned["countsScope"] == "observed"
            && (int)planned["planned"] == 2 && (int)planned["reused"] == 1 && (bool)planned["childCommitted"],
            "healthy observed child reports reservation or reuse without claiming physical connection");
        Check(planned.Property("committed")==null,"parent exposes typed childCommitted instead of an inferred parent commit claim");
        foreach (string field in new[] { "networkConnected", "flowVerified", "constructionComplete" })
            Check(planned.Property(field) == null && planned["connectResult"][field] != null,
                "native connection/completion/flow evidence stays in child rather than being inferred: " + field);
        var reused = Complete(); reused["planned"] = 0; reused["reusedExisting"] = 3;
        Check((string)Project(reused)["status"] == "planned_or_reused", "known existing utility reuse remains a successful observed outcome");
        var preview = Project(Complete(true), true);
        Check((string)preview["status"] == "preview_valid" && (bool)preview["dryRun"] && !(bool)preview["childCommitted"]
            && (string)preview["countsScope"] == "preview" && (int)preview["planned"] == 0 && (int)preview["reused"] == 3,
            "valid preview distinguishes validation and observed existing cells from new reservations");
        Check(preview.Property("previewPlanned") == null && preview.Property("previewReused") == null,
            "preview does not duplicate count families");
        var rejected = Complete(true); rejected["success"] = false; rejected["complete"] = false; rejected["failed"] = 1;
        Check((string)Project(rejected,true,error:true)["status"] == "preview_rejected", "failed preview is distinct from executed refusal");
        var claim = Complete(true); claim["planned"] = 3;
        var contradiction = Project(claim,true);
        Check((string)contradiction["status"] == "unknown", "preview child claiming new reservations cannot be reported valid or executed");
        Null(contradiction,"planned","contradictory preview reservation count becomes null while raw child is retained");
        Check((int)contradiction["connectResult"]["planned"] == 3 && (int)contradiction["reused"] == 3,
            "original contradictory claim and separately observed reuse remain inspectable");
        Check(((JArray)contradiction["unknownCountFields"]).Values<string>().Contains("planned"),"semantically invalidated preview count is listed unknown");
        var committedPreview=Complete(true);committedPreview["committed"]=true;
        var childClaim=Project(committedPreview,true);
        Check((string)childClaim["status"]=="unknown" && (bool)childClaim["childCommitted"] && childClaim.Property("committed")==null,
            "contradictory child commit claim stays attributed to the child rather than parent execution");
        var newDigs=Complete(true);newDigs["digNewlyQueued"]=2;
        var diggingClaim=Project(newDigs,true);
        Check((string)diggingClaim["status"]=="unknown", "preview newly-queued digging claim cannot establish valid non-mutating preview");
        Null(diggingClaim,"autoDigQueued","preview new-dig claim is projected unknown rather than executed digging");
        Check(((JArray)diggingClaim["unknownCountFields"]).Values<string>().Contains("digNewlyQueued")
            && (int)diggingClaim["connectResult"]["digNewlyQueued"]==2,
            "contradictory new-dig claim remains named unknown with raw child preserved");
        var existingDigs=Complete(true);existingDigs["digAlreadyQueued"]=2;
        var observedExisting=Project(existingDigs,true);
        Check((string)observedExisting["status"]=="preview_valid" && (int)observedExisting["autoDigAlreadyQueued"]==2
            && (int)observedExisting["reused"]==3, "previously queued digs and reused cells remain valid existing observations in preview");
        var wrongMode = Complete(true);
        Check((string)Project(wrongMode)["status"] == "unknown", "child preview cannot establish parent execution");
        Check((string)Project(Complete(),true)["status"] == "unknown", "executed child cannot establish parent preview");
    }
    private static void TestTypedCountsAndFlags()
    {
        foreach (int index in Enumerable.Range(0,CountNames.Length))
        {
            string field = CountNames[index], top = TopCountNames[index];
            foreach (JToken invalid in new JToken[] { JValue.CreateNull(), new JValue(true), new JValue(false),
                new JValue("2"), new JValue("0"), new JValue(1.5), new JValue(-1), new JValue((long)int.MaxValue+1),
                JObject.Parse("{n:9223372036854775808}")["n"], new JObject(), new JArray(0) })
            {
                var payload=Complete(); payload[field]=invalid.DeepClone(); var result=Project(payload);
                Null(result,top,"non-count type/range remains null: "+field+"/"+invalid.Type);
                Check(((JArray)result["unknownCountFields"]).Values<string>().Contains(field), "invalid child count is named unknown: "+field);
                Check((string)result["status"] != "planned_or_reused", "invalid count never becomes known complete planning: "+field);
            }
            var absent=Complete();absent.Remove(field);var missing=Project(absent);
            Null(missing,top,"absent count remains null: "+field);
            var zero=Complete();zero[field]=0L;var valid=Project(zero);
            Check(valid[top].Type==JTokenType.Integer && (int)valid[top]==0,"explicit integer zero remains known: "+field);
        }
        foreach (string field in new[] { "success", "complete", "committed", "dryRun" })
        {
            string top=field=="success"?"childSuccess":field=="complete"?"childComplete":field=="dryRun"?"childDryRun":field=="committed"?"childCommitted":field;
            foreach (JToken invalid in new JToken[] { JValue.CreateNull(), new JValue(1), new JValue("true"), new JArray(true) })
            {
                var payload=Complete();payload[field]=invalid;var result=Project(payload);
                Null(result,top,"non-boolean flag is unknown: "+field);
                Check(((JArray)result["unknownFlagFields"]).Values<string>().Contains(field),"unknown flag named explicitly: "+field);
                Check((string)result["status"]=="unknown","truthy coercion cannot establish success: "+field);
            }
        }
    }
    private static void TestPrimaryAndMalformed()
    {
        foreach (string text in new[] { "", "  ", "not JSON", "[]", "null", "17", "{broken" })
        {
            var child=CallToolResult.Error(text);child.Content.Add(new ToolContent { Text=Complete().ToString() });
            var result=Wire(AutoConnectPowerResultProjection.Project(child,false));
            Check((string)result["status"]=="connect_result_unparseable" && (bool)result["childIsError"],
                "malformed first primary body is never rescued by later optimistic content");
            Null(result,"connectResult","unparseable child payload is retained as unknown rather than an empty success");
            foreach (string count in TopCountNames) Null(result,count,"unparseable result has no fabricated count");
            Check((string)result["error"]==text,"primary error text remains available without replacement");
        }
        foreach (CallToolResult child in new[] { null, new CallToolResult(), new CallToolResult { Content=new List<ToolContent>() },
            new CallToolResult { Content=new List<ToolContent> { null, new ToolContent { Text=Complete().ToString() } } } })
        {
            var result=Wire(AutoConnectPowerResultProjection.Project(child,false));
            Check((string)result["status"]=="connect_result_unparseable","missing first primary content stays unparseable");
            Null(result,"connectResult","missing payload is serialized JSON null");
            if(child==null) Null(result,"childIsError","missing envelope error state is unknown rather than false");
        }
        var nonText=Child(Complete());nonText.Content[0].Type="image";
        Check((string)Wire(AutoConnectPowerResultProjection.Project(nonText,false))["status"]=="connect_result_unparseable",
            "non-text first content cannot establish a parsed primary JSON result");
        var original=Child(Complete());var resultOne=AutoConnectPowerResultProjection.Project(original,false);
        ((JObject)resultOne["connectResult"])["results"][0]["fixture"]="changed detached projection";
        Check((string)JObject.Parse(original.Content[0].Text)["results"][0]["fixture"]=="preserved child result",
            "mutable returned child payload does not alias input text");
    }
    private static void Setup()
    {
        BuildPlanningTools.AutoPowerChildCalls=0;BuildPlanningTools.AutoPowerWouldMutate=0;
        BuildPlanningTools.AutoPowerChildArgs=null;BuildPlanningTools.AutoPowerSourceAvailable=true;
        BuildPlanningTools.AutoPowerWire=new BuildingDef { PrefabID="Wire" };
        BuildPlanningTools.AutoPowerTerrain=null;
        BuildPlanningTools.AutoPowerChildResult=Child(Complete());
    }
    private static BuildingDef Consumer() => new BuildingDef { PrefabID="FixtureConsumer",RequiresPowerInput=true,PowerInputOffset=new CellOffset(0,0) };
    private static void TestActualAdapterBoundary()
    {
        foreach(bool preview in new[] { false,true })
        {
            Setup();BuildPlanningTools.AutoPowerChildResult=Child(Complete(preview));
            var args=new JObject { ["dryRun"]=preview,["material"]="Cuprite",["worldId"]=0,["maxCells"]=20,
                ["maxAutoDigCells"]=6,["points"]=new JArray(new JArray(9,9)),["autoConnectPower"]=true };
            string original=args.ToString();var value=Wire(BuildPlanningTools.AutoPowerForTest(Consumer(),args));
            Check(args.ToString()==original,"actual parent adapter preserves caller's path/material/options");
            Check(BuildPlanningTools.AutoPowerChildCalls==1,"actual parent dispatches exactly one child, no retries");
            Check(BuildPlanningTools.AutoPowerWouldMutate==(preview?0:1),"parent preview forwards dryRun before child mutation gate");
            var child=BuildPlanningTools.AutoPowerChildArgs;
            Check((bool)child["dryRun"]==preview && (bool)child["confirm"] && !(bool)child["nativePathPlacement"],
                "actual adapter preserves counted validation/placement child entry path");
            Check(!(bool)child["autoConnectPower"] && !(bool)child["autoUprootObstructions"] && !(bool)child["autoDigObstructions"],
                "child recursion/uproot/implicit terrain digging stay disabled");
            Check((string)child["prefabId"]=="Wire" && (string)child["material"]=="Cuprite" && (int)child["maxCells"]==20
                && ((JArray)child["points"]).Count==3 && (int)child["points"][0][0]==2 && (int)child["points"][2][0]==4,
                "adapter uses its guarded route and keeps wire material/budget rather than caller viewport path");
            Check((string)value["status"]==(preview?"preview_valid":"planned_or_reused") && (bool)value["dryRun"]==preview,
                "serialized actual parent distinguishes preview from observed planning");
            Check((int)value["input"]["cell"]==24 && (int)value["source"]["cell"]==22 && (int)value["pathCells"]==3,
                "adapter attaches exact route context around projected child outcome");
        }
        Setup();var typicalPreview=Complete(true);typicalPreview["planned"]=3;
        BuildPlanningTools.AutoPowerChildResult=Child(typicalPreview);
        var typical=Wire(BuildPlanningTools.AutoPowerForTest(Consumer(),new JObject { ["dryRun"]=true }));
        Check((string)typical["status"]=="unknown" && BuildPlanningTools.AutoPowerWouldMutate==0,
            "actual adapter does not turn child predicted planned cells into successful execution");
        Null(typical,"planned","actual adapter serializes contradictory preview counts as null");
        Setup();BuildPlanningTools.AutoPowerChildResult=Child(Complete(true));
        var alias=Wire(BuildPlanningTools.AutoPowerForTest(Consumer(),new JObject { ["validateOnly"]=true }));
        Check((string)alias["status"]=="preview_valid" && (bool)BuildPlanningTools.AutoPowerChildArgs["dryRun"]
            && BuildPlanningTools.AutoPowerWouldMutate==0, "validateOnly alias also remains a child preview at the actual adapter boundary");
        Setup();BuildPlanningTools.AutoPowerChildResult=Child(new JObject { ["success"]=false,["committed"]=false,["reasonCode"]="utility_path_conflict" },true);
        var failure=Wire(BuildPlanningTools.AutoPowerForTest(Consumer(),new JObject()));
        Check((string)failure["status"]=="rejected","actual parent no longer calls early child refusal connected_or_planned");
        Null(failure,"failed","actual parent does not supplement missing child failed with zero");
        Setup();var requested=new JObject { ["dryRun"]=true,["allowUtilityTerrainDig"]=true,["autoDigObstructions"]=true,["maxAutoDigCells"]=4 };
        BuildPlanningTools.AutoPowerChildResult=Child(Complete(true));
        BuildPlanningTools.AutoPowerForTest(Consumer(),requested);
        Check(BuildPlanningTools.AutoPowerTerrainConsent && BuildPlanningTools.AutoPowerTerrainAutoDig && BuildPlanningTools.AutoPowerTerrainBudget==4
            && (bool)BuildPlanningTools.AutoPowerChildArgs["allowUtilityTerrainDig"] && (bool)BuildPlanningTools.AutoPowerChildArgs["autoDigObstructions"],
            "explicit terrain consent and budget retain both preflight and child route");
        Check(BuildPlanningTools.AutoPowerWouldMutate==0,"explicit terrain consent cannot turn preview into mutation");
        Setup();BuildPlanningTools.AutoPowerTerrain=new Dictionary<string,object> { ["allowed"]=false,["success"]=false,["committed"]=false,["reasonCode"]="utility_natural_terrain_blocked" };
        var terrain=Wire(BuildPlanningTools.AutoPowerForTest(Consumer(),new JObject { ["dryRun"]=true }));
        Check((string)terrain["status"]=="blocked_natural_terrain" && BuildPlanningTools.AutoPowerChildCalls==0
            && BuildPlanningTools.AutoPowerWouldMutate==0,"terrain preflight refusal prevents child invocation entirely");
        foreach(int early in Enumerable.Range(0,6))
        {
            Setup();var def=Consumer();var args=new JObject();
            if(early==0)args["autoConnectPower"]=false;
            if(early==1)def.RequiresPowerInput=false;
            if(early==2)def.PowerInputOffset=new CellOffset(100,0);
            if(early==3)BuildPlanningTools.AutoPowerSourceAvailable=false;
            if(early==4)BuildPlanningTools.AutoPowerWire=null;
            if(early==5)args["maxCells"]=1;
            BuildPlanningTools.AutoPowerForTest(def,args);
            Check(BuildPlanningTools.AutoPowerChildCalls==0 && BuildPlanningTools.AutoPowerWouldMutate==0,
                "unchanged pre-child guard/disable does not place or call child: "+early);
        }
    }
}
