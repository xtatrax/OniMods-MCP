using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static partial class Program
{
    private const string ColonySeedRead = "call domain=bio bioDomain=farming action=seed_catalog";
    private const string ColonyPlantingRead = "call domain=bio bioDomain=farming action=list_planting";
    private const string ColonyResearchRead = "call domain=management kind=research action=list";

    private static void ColonyReadResultsRemainAvailable()
    {
        int before = checks;
        ColonyReadPayloadsRemainExact();
        ColonyReadAliasesMatchExistingSelectors();
        ColonyReadSelectorPrecedenceStaysNarrow();
        ColonyReadDetailAndScopeStaySeparate();
        ColonyReadFailuresRemainCompact();
        ColonyReadExecutionGuardsRemainUnchanged();
        ColonyReadGuideUsesRuntimeCatalog();
        Console.WriteLine("Colony read result regression checks passed: " + (checks - before));
    }

    private static CallToolResult ColonyRetained(string raw, string line, JObject parent = null, string relative = "ops/colony.md")
    {
        var response = Invoke(raw, false, parent ?? Parent(), line, relative: relative);
        var item = Item(response);
        Require(!response.IsError && (string)item["result"] == raw, "selected colony read keeps the exact first child result");
        Require(item["arguments"] == null, "default colony read does not echo arguments or force detailed output");
        Require(NativeBoundary.LastTool == "colony_control", "retention follows the canonical colony child tool");
        return response;
    }

    private static void ColonyCompact(string line, string relative = "ops/colony.md")
    {
        string large = new JObject { ["payload"] = new string('x', 18000) }.ToString(Formatting.None);
        var response = Invoke(large, false, Parent(), line, relative: relative);
        Require(Item(response)["result"] == null && Item(response)["arguments"] == null,
            "non-target or explicit compact result remains omitted");
        Require(response.Content[0].Text.Length < 1000, "non-target long results keep their compact response bound");
    }

    private static void ColonyReadPayloadsRemainExact()
    {
        string[] lines = { ColonySeedRead, ColonyPlantingRead, ColonyResearchRead };
        string[] bodies =
        {
            "{\"returned\":2,\"truncated\":7,\"seeds\":[{\"id\":\"SeedA\",\"cropSeedOnly\":true},{\"id\":\"SeedB\"}]}",
            "[{\"id\":101,\"selectedSeed\":null},{\"id\":102,\"selectedSeed\":\"SeedA\"}]",
            "{\"count\":2,\"research\":[{\"id\":\"TechA\",\"complete\":false},{\"id\":\"TechB\",\"complete\":true}]}"
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var response = ColonyRetained(bodies[i], lines[i] + " query=ExactId limit=2 worldId=7");
            Require(JToken.DeepEquals(JToken.Parse((string)Item(response)["result"]), JToken.Parse(bodies[i])),
                "child arrays, nulls, quantities and completion flags are not projected or rewritten");
            Require((string)NativeBoundary.LastArguments["query"] == "ExactId"
                && (int)NativeBoundary.LastArguments["limit"] == 2 && (int)NativeBoundary.LastArguments["worldId"] == 7,
                "native query/limit/world filter arguments pass through unchanged");
            Require(NativeBoundary.LastArguments["detail"] == null && (int)Body(response)["applied"] == 0,
                "retention does not force native detail or count read results as game changes");
            ColonyRetained("[]", lines[i] + " query=NoSuchId limit=1");
        }
        string unicode = string.Concat(Enumerable.Repeat("酸素😀\n\"\\", 2300));
        string longJson = new JObject { ["research"] = new JArray(new JObject { ["text"] = unicode }), ["tail"] = "complete-tail" }.ToString(Formatting.None);
        var longResult = ColonyRetained(longJson, ColonyResearchRead + " query=ExactTech limit=1");
        Require((string)JObject.Parse((string)Item(longResult)["result"])["tail"] == "complete-tail",
            "default colony read preserves long Unicode JSON through its final field");
        foreach (string raw in new[] { "null", "42", "true", "plain native response", "", new string('n', 12001) })
            ColonyRetained(raw, ColonySeedRead + " limit=1");

        NativeBoundary.Reset(bodies[0], false);
        var primary = CallToolResult.Text(bodies[0]);
        primary.Content.Add(CallToolResult.Text("{\"printingPodSelection\":{\"fixture\":true}}").Content[0]);
        NativeBoundary.Results.Enqueue(primary);
        var firstOnly = WorldEditorTools.ApplyOperationForTest(Parent(), "ops/colony.md", ColonySeedRead);
        Require(NativeBoundary.Calls == 1 && (string)Item(firstOnly)["result"] == bodies[0]
            && NativeBoundary.LastResult.Content.Count == 2, "retention keeps first child content only and never merges extra policy blocks");
    }

    private static void ColonyReadAliasesMatchExistingSelectors()
    {
        foreach (string line in new[]
        {
            "call domain=life bioDomain=farm action=list_seeds",
            "call domain=biology bioDomain=planting action=seeds",
            "call domain=Bio bioDomain=PLANTS action=SEED_CATALOG",
            "call domain=bio kind=farming action=seed_catalog",
            "call domain=bio bioDomain=\"\" kind=farming action=list_planting",
            "call domain=life bioDomain=plants action=list",
            "call domain=bio bioDomain=farming kind=ranching action=list_planting",
            "call domain=manage managementDomain=tech action=list",
            "call domain=screen kind=tech managementDomain=medical action=list",
            "call domain=screens kind=research action=LIST"
        })
            ColonyRetained("{\"items\":[{\"id\":\"AliasRead\"}]}", line);
    }

    private static void ColonyReadSelectorPrecedenceStaysNarrow()
    {
        foreach (string line in new[]
        {
            "call domain=read action=status", "call domain=snapshot action=get",
            "call domain=diagnostic action=alerts", "call domain=notification action=list",
            "call domain=bio bioDomain=ranching action=list", "call domain=bio bioDomain=farming action=set_planting",
            "call domain=bio bioDomain=farming action=list_harvestables",
            "call domain=bio bioDomain=unknown kind=farming action=seed_catalog",
            "call domain=bio bioDomain=ranching kind=farming action=seed_catalog",
            "call domain=bio kind=FARMING action=seed_catalog",
            "call domain=bio kind=plants action=seed_catalog",
            "call domain=bio bioDomain=farming",
            "call domain=management kind=research action=status",
            "call domain=management kind=research action=set",
            "call domain=management kind=research action=clear",
            "call domain=management kind=diet action=list",
            "call domain=management kind=medical managementDomain=research action=list",
            "call domain=management kind=\"\" managementDomain=research action=list",
            "call domain=management managementDomain=research"
        })
            ColonyCompact(line);

        // The DSL preserves scalar null as the string "null". Exercise actual
        // JValue-null selector precedence directly on the production predicate;
        // this is not a model of native handlers or a second routing algorithm.
        var selector = typeof(WorldEditorTools).GetMethod("ShouldRetainColonyReadResult", BindingFlags.Static | BindingFlags.NonPublic);
        Require(selector != null, "host links the actual colony retention predicate source");
        var args = new JObject { ["domain"] = "management", ["managementDomain"] = "research", ["action"] = "list" };
        string before = args.ToString(Formatting.None);
        Require((bool)selector.Invoke(null, new object[] { "colony_control", args }), "missing kind allows native managementDomain fallback");
        Require(args.ToString(Formatting.None) == before, "selector read never rewrites the child arguments");
        args["kind"] = JValue.CreateNull(); before = args.ToString(Formatting.None);
        Require(!(bool)selector.Invoke(null, new object[] { "colony_control", args }), "JValue-null kind shadows managementDomain just like the native router");
        Require(args.ToString(Formatting.None) == before && args["kind"].Type == JTokenType.Null,
            "typed-null selector remains null after the production predicate reads it");
        args = new JObject { ["domain"] = "bio", ["bioDomain"] = JValue.CreateNull(), ["kind"] = "farming", ["action"] = "seeds" };
        Require((bool)selector.Invoke(null, new object[] { "colony_control", args }), "empty-string native bioDomain null can use exact farming kind fallback");
        args["kind"] = "FARMING";
        Require(!(bool)selector.Invoke(null, new object[] { "colony_control", args }), "native bio kind fallback retains its existing case-sensitive eligibility");
    }

    private static void ColonyReadDetailAndScopeStaySeparate()
    {
        string raw = "{\"items\":[{\"id\":\"ScopedRead\"}]}";
        var parent = Parent(); parent["detail"] = true; parent["query"] = "wrong parent query";
        ColonyRetained(raw, ColonySeedRead + " query=ExactChild limit=1", parent);
        Require(NativeBoundary.LastArguments["detail"] == null && (string)NativeBoundary.LastArguments["query"] == "ExactChild",
            "parent detail never promotes child detail and child filter keeps precedence");
        foreach (string line in new[] { ColonySeedRead, ColonyPlantingRead, ColonyResearchRead })
        {
            ColonyCompact(line + " detail=false");
            var detailed = Invoke(raw, false, Parent(), line + " detail=true");
            Require((string)Item(detailed)["result"] == raw && (bool)Item(detailed)["arguments"]["detail"],
                "explicit boolean detail true retains raw result and exact argument echo");
        }
        foreach (string detail in new[] { "brief", "compact", "full" })
        {
            ColonyRetained(raw, ColonySeedRead + " detail=" + detail);
            Require(NativeBoundary.LastArguments["detail"].Type == JTokenType.String
                && (string)NativeBoundary.LastArguments["detail"] == detail, "string detail passes to native unchanged without wrapper argument echo");
        }
        foreach (string value in new[] { "[]", "[false]" })
        {
            ColonyRetained(raw, ColonySeedRead + " detail=" + value);
            Require(NativeBoundary.LastArguments["detail"].Type == JTokenType.Array
                && JToken.DeepEquals(NativeBoundary.LastArguments["detail"], JArray.Parse(value)),
                "nonboolean array detail is preserved and never reinterpreted as compact false");
        }
        foreach (string relative in new[] { "ops/any.md", "ops/search.md" })
            ColonyRetained(raw, "call tool=colony_control domain=bio bioDomain=farming action=seed_catalog limit=1", relative: relative);
        ColonyCompact("call tool=game_control domain=bio bioDomain=farming action=seed_catalog");
        var explicitOther = Invoke(raw, false, Parent(), "call domain=management kind=research action=set detail=true");
        Require((string)Item(explicitOther)["result"] == raw && Item(explicitOther)["arguments"] != null,
            "non-target commands preserve the pre-existing explicit-detail contract");
    }

    private static void ColonyReadFailuresRemainCompact()
    {
        string error = new JObject { ["ok"] = false, ["error"] = new string('e', 16000), ["tail"] = "colony-error-end" }.ToString(Formatting.None);
        foreach (bool nativeError in new[] { false, true })
        foreach (string suffix in new[] { "", " detail=false", " detail=full", " detail=true" })
        {
            var response = Invoke(error, nativeError, Parent(), ColonyResearchRead + suffix);
            var item = Item(response);
            Require(response.IsError && !(bool)item["ok"] && (bool)item["isError"], "native or logical refusal remains an error");
            Require(((string)item["error"]).Length == 4003 && ((string)item["error"]).EndsWith("...", StringComparison.Ordinal),
                "failed colony reads retain the existing bounded error preview");
            Require(item["summary"] == null, "refused colony read never becomes a success summary");
            if (suffix == " detail=true")
                Require((string)item["result"] == error && item["arguments"] != null, "only explicit true keeps complete long errors");
            else
                Require(item["result"] == null && item["arguments"] == null, "default/false/string-detail failures do not acquire raw output");
        }
    }

    private static void ColonyReadExecutionGuardsRemainUnchanged()
    {
        foreach (var parent in new[]
        {
            JObject.Parse("{task:'host test',confirm:true,dryRun:true}"),
            JObject.Parse("{task:'host test',confirm:false}"), JObject.Parse("{task:'host test'}")
        })
        {
            var result = Invoke("unused", false, parent, ColonySeedRead + " confirm=true", 0);
            Require(Body(result)["preview"]?.Value<bool>() == true, "parent confirmation and preview gates still avoid native execution");
        }
        foreach (string suffix in new[] { " confirm=false", " dryRun=true" })
        {
            var result = Invoke("unused", false, Parent(), ColonySeedRead + suffix, 0);
            Require(Item(result)["preview"]?.Value<bool>() == true && Item(result)["result"] == null,
                "child withheld execution never manufactures a retained read result");
        }
        var noTask = Invoke("unused", false, new JObject { ["confirm"] = true }, ColonySeedRead, 0);
        Require(noTask.IsError && (string)Body(noTask)["phase"] == "preflight", "task requirement remains before child execution");
        var two = Invoke("unused", false, Parent(), ColonySeedRead + "\n" + ColonyResearchRead, 0, false);
        Require(two.IsError, "one-command guard remains before either read call");
        foreach (string line in new[]
        {
            ColonySeedRead + " x=1 y=2",
            "call tool=world_editor command=read path=/active/colony/status.oni",
            "call tool=server_control domain=batch action=batch",
            "call tool=server_control domain=program action=run",
            "call tool=server_control domain=diagnostics action=flow",
            "call tool=unknown_colony_alias domain=bio bioDomain=farming action=seed_catalog"
        })
        {
            var result = Invoke("unused", false, Parent(), line, 0);
            Require(result.IsError && (string)Body(result)["phase"] == "preflight", "coordinates/recursive/unregistered capabilities remain refused");
        }
    }

    private static void ColonyReadGuideUsesRuntimeCatalog()
    {
        var language = McpLocalization.LanguageProvider;
        try
        {
            foreach (string selected in new[] { "en", "ja", "zh-Hans" })
            {
                McpLocalization.LanguageProvider = () => selected;
                NativeBoundary.Reset("unused", false);
                string guide = WorldEditorTools.ReadOperationForTest("ops/colony.md").Content[0].Text;
                Require(guide.Contains(McpLocalization.Get("operation_files.result_contract"))
                    && !guide.Contains("operation_files.result_contract"), "colony result guide resolves the runtime catalog in the selected language");
                Require(guide.Contains("detail=false") && guide.Contains("detail=true"), "colony guide keeps explicit compact/full controls visible");
                Require(guide.Contains("seed_catalog") && guide.Contains("list_planting") && guide.Contains("research"),
                    "colony guide identifies the selected read families rather than promising every colony result");
                Require(NativeBoundary.Calls == 0, "reading the result contract does not execute its examples");
            }
        }
        finally { McpLocalization.LanguageProvider = language; }
    }
}
