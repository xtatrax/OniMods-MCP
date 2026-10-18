using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static partial class Program
{
    private static void SearchResultsRemainAvailable()
    {
        int before = checks;
        SearchDomainPayloadsRemainExact();
        SearchDetailAndToolScopeStaySeparate();
        SearchFailuresRemainCompact();
        SearchExecutionGuardsRemainUnchanged();
        OperationGuideDescribesResultScope();
        Console.WriteLine("Search operation result regression checks passed: " + (checks - before));
    }

    private static void SearchDomainPayloadsRemainExact()
    {
        var world = JObject.Parse("{v:1,tool:'search_control',domain:'world',searchResult:{matched:170,returned:2,returnMode:'clusters',clusters:[{id:'cluster1',cells:100,bbox:[1,2,3,4]},{id:'cluster2',cells:70,bbox:[5,6,7,8]}]}}");
        var resources = JObject.Parse("{v:1,tool:'search_control',domain:'resources',searchResult:{matched:7,returned:1,truncated:6,items:[{id:123,prefabId:'Dirt',massKg:42}]}}");
        var buildings = JObject.Parse("{v:1,tool:'search_control',domain:'buildings',searchResult:{returned:1,buildings:[{prefabId:'Ladder',name:'梯子'}]}}");
        var pattern = JObject.Parse("{v:1,tool:'search_control',domain:'world',searchResult:{matched:2,returned:2,truncated:true,pattern:'abc',items:[{id:'pattern1'},{id:'pattern2'}]}}");
        foreach (var child in new[] { world, resources, buildings, pattern })
        {
            string raw = child.ToString(Formatting.None);
            string domain = (string)child["domain"];
            var result = Invoke(raw, false, Parent(), "call domain=" + domain + " query=ExactId worldId=7 visibleOnly=true limit=2", relative: "ops/search.md");
            var item = Item(result);
            Require((string)item["result"] == raw, "default search retains exact native domain envelope and all metadata");
            var parsed = JObject.Parse((string)item["result"]);
            Require(JToken.DeepEquals(parsed, child), "domain-specific counts, truncation types and missing fields are not reinterpreted");
            Require(item["arguments"] == null, "default search does not duplicate argument text");
            Require((int)Body(result)["applied"] == 0, "search hit counts are never counted as game changes");
            Require((string)NativeBoundary.LastTool == "search_control", "typed search executes the canonical search tool");
            Require((string)NativeBoundary.LastArguments["query"] == "ExactId" && (int)NativeBoundary.LastArguments["limit"] == 2
                && (int)NativeBoundary.LastArguments["worldId"] == 7 && (bool)NativeBoundary.LastArguments["visibleOnly"], "query, limit, world and visibility reach the child unchanged");
            Require(NativeBoundary.LastArguments["detail"] == null, "default search does not force a native detail setting");
        }
        foreach (string raw in new[]
        {
            new JObject { ["searchResult"] = new JObject { ["payload"] = new string('x', 16000), ["tail"] = "search-end" } }.ToString(Formatting.None),
            new JArray(Enumerable.Range(0, 1600).Select(index => new JObject { ["id"] = index, ["name"] = "酸素😀" })).ToString(Formatting.None),
            new JObject { ["payload"] = string.Concat(Enumerable.Repeat("酸素😀\n\"\\", 2400)), ["tail"] = "unicode-search-end" }.ToString(Formatting.None),
            "[]", "null", "0", "true", "plain search response", ""
        })
        {
            var result = Invoke(raw, false, Parent(), "call domain=world query=ExactId limit=1", relative: "ops/search.md");
            Require((string)Item(result)["result"] == raw, "default search preserves long JSON, Unicode, scalar and non-JSON results without string cuts");
            Require(Item(result)["arguments"] == null, "complete default search results do not require argument echo");
        }
    }

    private static void SearchDetailAndToolScopeStaySeparate()
    {
        string raw = "{\"searchResult\":{\"returned\":1,\"items\":[{\"id\":123}]}}";
        foreach (string detail in new[] { "brief", "compact", "full" })
        {
            var parent = Parent(); parent["detail"] = false; parent["query"] = "parent query";
            var result = Invoke(raw, false, parent, "call domain=world query=ExactId limit=1 detail=" + detail, relative: "ops/search.md");
            Require((string)Item(result)["result"] == raw && Item(result)["arguments"] == null, "native detail enums retain child results without enabling argument echo");
            Require(NativeBoundary.LastArguments["detail"].Type == JTokenType.String
                && (string)NativeBoundary.LastArguments["detail"] == detail, "native detail enum is not converted to wrapper bool");
            Require((string)NativeBoundary.LastArguments["query"] == "ExactId" && (int)NativeBoundary.LastArguments["limit"] == 1, "explicit search scope wins over unrelated parent arguments");
        }
        string large = new JObject { ["payload"] = new string('x', 40000) }.ToString(Formatting.None);
        var compact = Invoke(large, false, Parent(), "call domain=world query=ExactId detail=false", relative: "ops/search.md");
        Require(Item(compact)["result"] == null && Item(compact)["arguments"] == null, "legacy explicit boolean false opts out of search result output");
        Require(compact.Content[0].Text.Length < 1000 && (string)Item(compact)["summary"] == "ok", "explicit compact search remains bounded");
        Require(NativeBoundary.LastArguments["detail"].Type == JTokenType.Boolean && !(bool)NativeBoundary.LastArguments["detail"], "legacy false reaches native unchanged without becoming an enum");
        var detailed = Invoke(raw, false, Parent(), "call domain=world query=ExactId detail=true", relative: "ops/search.md");
        Require((string)Item(detailed)["result"] == raw && (bool)Item(detailed)["arguments"]["detail"], "legacy true retains full result and argument echo");

        var generic = Invoke(raw, false, Parent(), "call tool=search_control domain=world query=ExactId limit=1", relative: "ops/any.md");
        Require((string)Item(generic)["result"] == raw && (string)Item(generic)["tool"] == "search_control", "canonical search retention covers generic operation files");
        Require(NativeBoundary.LastTool == "search_control", "generic search dispatches through canonical name");
        // RegisterInternal currently indexes only the canonical operation name.
        // Alias metadata does not register these names in the native registry.
        foreach (string alias in new[] { "find_control", "search_action_control" })
        {
            var rejected = Invoke("unused", false, Parent(), "call tool=" + alias + " domain=world query=ExactId limit=1", 0, relative: "ops/any.md");
            Require(rejected.IsError && (string)Body(rejected)["phase"] == "preflight", "unregistered internal aliases remain rejected before native dispatch");
        }
        var alternateFile = Invoke(raw, false, Parent(), "call tool=search_control domain=world query=ExactId limit=1", relative: "ops/colony.md");
        Require((string)Item(alternateFile)["result"] == raw, "search result retention follows the canonical tool rather than file name");
        foreach (string tool in new[] { "colony_control", "game_control" })
        {
            var result = Invoke(large, false, Parent(), "call tool=" + tool + " domain=management action=list", relative: "ops/search.md");
            Require(Item(result)["result"] == null && Item(result)["arguments"] == null, "typed search tool overrides do not expand non-search results");
            Require(result.Content[0].Text.Length < 1000, "other tools retain compact defaults even under the search path");
        }
    }

    private static void SearchFailuresRemainCompact()
    {
        string error = new JObject { ["ok"] = false, ["error"] = new string('e', 16000), ["tail"] = "search-error-end" }.ToString(Formatting.None);
        foreach (bool nativeError in new[] { false, true })
            foreach (string suffix in new[] { "", " detail=false", " detail=brief", " detail=compact", " detail=full", " detail=true" })
            {
                var result = Invoke(error, nativeError, Parent(), "call domain=world query=ExactId" + suffix, relative: "ops/search.md");
                var item = Item(result);
                Require(result.IsError && !(bool)item["ok"] && (bool)item["isError"], "native and logical search failures preserve their failure status");
                Require(((string)item["error"]).Length == 4003 && ((string)item["error"]).EndsWith("...", StringComparison.Ordinal), "search failure previews retain their existing bound");
                if (suffix == " detail=true")
                    Require((string)item["result"] == error && item["arguments"] != null, "explicit boolean true retains complete search error as before");
                else
                    Require(item["result"] == null && item["arguments"] == null, "failed searches retain compact defaults despite native detail enums");
            }
    }

    private static void SearchExecutionGuardsRemainUnchanged()
    {
        foreach (var parent in new[]
        {
            JObject.Parse("{task:'host test',confirm:true,dryRun:true}"),
            JObject.Parse("{task:'host test',confirm:false}"),
            JObject.Parse("{task:'host test'}")
        })
        {
            var result = Invoke("unused", false, parent, "call domain=world query=ExactId confirm=true", 0, relative: "ops/search.md");
            Require(Body(result)["preview"]?.Value<bool>() == true, "search parent execution gates remain previews");
        }
        foreach (string suffix in new[] { " confirm=false", " dryRun=true" })
        {
            var result = Invoke("unused", false, Parent(), "call domain=world query=ExactId" + suffix, 0, relative: "ops/search.md");
            Require(Item(result)["preview"]?.Value<bool>() == true && Item(result)["result"] == null, "search child execution gates do not manufacture results");
        }
        var missingTask = Invoke("unused", false, new JObject { ["confirm"] = true }, "call domain=world query=ExactId", 0, relative: "ops/search.md");
        Require(missingTask.IsError && (string)Body(missingTask)["phase"] == "preflight", "search task remains required");
        var two = Invoke("unused", false, Parent(), "call domain=world query=ExactId\ncall domain=world query=OtherId", 0, false, "ops/search.md");
        Require(two.IsError, "search remains one executable command per edit");
        foreach (string line in new[]
        {
            "call domain=world query=ExactId x=2 y=3",
            "call tool=world_editor command=search domain=world query=ExactId",
            "call tool=server_control domain=batch action=batch",
            "call tool=server_control domain=program action=run",
            "call tool=server_control domain=diagnostics action=flow"
        })
        {
            var result = Invoke("unused", false, Parent(), line, 0, relative: "ops/search.md");
            Require(result.IsError && (string)Body(result)["phase"] == "preflight", "coordinate and recursive capability restrictions remain before dispatch");
        }
    }

    private static void OperationGuideDescribesResultScope()
    {
        var languageProvider = McpLocalization.LanguageProvider;
        var gameLanguageProvider = McpLocalization.GameLanguageProvider;
        try
        {
            foreach (string language in new[] { "en", "ja", "zh-Hans" })
            {
                McpLocalization.LanguageProvider = () => language;
                McpLocalization.GameLanguageProvider = () => "en";
                NativeBoundary.Reset("unused", false);
                string guide = WorldEditorTools.ReadOperationForTest("ops/search.md").Content[0].Text;
                Require(guide.Contains(McpLocalization.Get("operation_files.result_contract")), "generated guide uses the selected result contract translation");
                Require(guide.Contains("detail=brief|compact|full") && guide.Contains("detail=false") && guide.Contains("detail=true"), "guide distinguishes native detail strings from legacy booleans");
                Require(guide.Contains("call domain=buildings query=\"ladder\" limit=3"), "search example has a small explicit limit");
                Require(!guide.Contains("operation_files.result_contract") && !guide.Contains("each command returns `ok`, exact arguments"), "guide does not expose a missing key or old unconditional raw-result promise");
                Require(NativeBoundary.Calls == 0, "reading operation guidance does not execute its examples");
            }
        }
        finally
        {
            McpLocalization.LanguageProvider = languageProvider;
            McpLocalization.GameLanguageProvider = gameLanguageProvider;
        }
    }
}
