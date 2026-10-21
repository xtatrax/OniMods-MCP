using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

internal static partial class Program
{
    private static int checks;

    private static void Main()
    {
        MetadataSchemaRegression.Run();
        DetailedPayloadsRemainExact();
        DefaultsRemainCompact();
        ErrorSummaryRemainsBounded();
        ExecutionGatesDoNotCallTheChild();
        CallArgumentsKeepTheirScope();
        Console.WriteLine("Operation result regression checks passed: " + checks);
        SearchResultsRemainAvailable();
        ColonyReadResultsRemainAvailable();
        ManagementSkillReadRegression.Run();
        MapCountRegression.Run();
        OutcomeRegression.Run();
        CommandParsingRegression.Run();
    }

    private static void DetailedPayloadsRemainExact()
    {
        string unicode = string.Concat(Enumerable.Repeat("酸素😀\n\"\\", 2400));
        string[] json =
        {
            new JObject { ["payload"] = new string('x', 16000), ["tail"] = "object-end" }.ToString(Formatting.None),
            new JArray(Enumerable.Range(0, 1600).Select(index => new JObject
                { ["index"] = index, ["value"] = "array-entry" })).ToString(Formatting.None),
            new JObject { ["payload"] = unicode, ["tail"] = "unicode-end" }.ToString(Formatting.None),
            JsonConvert.SerializeObject(new string('s', 14000)),
            "{\"count\":1,\"tail\":\"short-end\"}",
            "[]", "null", "42", "true"
        };
        foreach (string raw in json)
        {
            var response = Invoke(raw, false, Parent(), "call domain=management action=list detail=true");
            var item = Item(response);
            Require((string)item["result"] == raw, "explicit detail must preserve the exact raw JSON text");
            Require(JToken.DeepEquals(JToken.Parse((string)item["result"]), JToken.Parse(raw)),
                "detailed JSON must remain complete and independently parseable");
            Require(item["arguments"]?["detail"]?.Value<bool>() == true, "detail arguments are reported");
        }
        foreach (string raw in new[] { "plain response", "", new string('n', 12000), new string('n', 12001), unicode })
        {
            var response = Invoke(raw, false, Parent(), "call domain=management action=list detail=true");
            Require((string)Item(response)["result"] == raw, "non-JSON, empty and boundary-length text must remain exact");
        }
        var absent = Invoke(null, false, Parent(), "call domain=management action=list detail=true");
        Require((string)Item(absent)["result"] == string.Empty, "absent native content uses the existing empty-string behavior");
    }

    private static void DefaultsRemainCompact()
    {
        string large = new JObject { ["payload"] = new string('x', 40000) }.ToString(Formatting.None);
        foreach (string line in new[]
        {
            "call domain=management action=list",
            "call domain=management action=list detail=false"
        })
        {
            var response = Invoke(large, false, Parent(), line);
            var item = Item(response);
            Require(item["result"] == null && item["arguments"] == null, "default and detail=false must omit full results and arguments");
            Require((string)item["summary"] == "ok", "default JSON summary remains compact");
            Require(response.Content[0].Text.Length < 1000, "a long native JSON response must not expand the default reply");
        }
        var parentDetail = Parent();
        parentDetail["detail"] = true;
        var parentOnly = Invoke(large, false, parentDetail, "call domain=management action=list");
        Require(Item(parentOnly)["result"] == null, "parent detail must not silently enable child detail");
        var nonJson = Invoke(new string('t', 40000), false, Parent(), "call domain=management action=list");
        Require(((string)Item(nonJson)["summary"]).Length == 503, "non-JSON summaries retain their 500-character prefix limit");
        Require(Item(nonJson)["result"] == null, "bounded non-JSON summaries do not expose full detail by default");
    }

    private static void ErrorSummaryRemainsBounded()
    {
        string raw = new JObject { ["error"] = new string('e', 16000), ["tail"] = "error-end" }.ToString(Formatting.None);
        foreach (bool detail in new[] { false, true })
        {
            var response = Invoke(raw, true, Parent(), "call domain=management action=list detail=" + detail.ToString().ToLowerInvariant());
            var item = Item(response);
            Require(response.IsError && !(bool)item["ok"] && (bool)item["isError"], "native error state must survive detailed and compact replies");
            Require(((string)item["error"]).Length == 4003 && ((string)item["error"]).EndsWith("...", StringComparison.Ordinal),
                "the error preview must retain its existing 4000-character prefix limit");
            Require(item["summary"] == null, "error responses must not be relabelled as success summaries");
            if (detail)
            {
                Require((string)item["result"] == raw, "explicit detail must retain a complete long error result");
                Require(JToken.DeepEquals(JToken.Parse((string)item["result"]), JToken.Parse(raw)), "complete error JSON remains parseable");
            }
            else
                Require(item["result"] == null, "compact errors do not return full payloads");
        }
        var plainError = Invoke(new string('e', 14000), true, Parent(), "call domain=management action=list detail=true");
        Require(((string)Item(plainError)["result"]).Length == 14000, "detailed non-JSON errors remain complete");
    }

    private static void ExecutionGatesDoNotCallTheChild()
    {
        foreach (var parent in new[]
        {
            JObject.Parse("{task:'host test',confirm:true,dryRun:true}"),
            JObject.Parse("{task:'host test',confirm:false}"),
            JObject.Parse("{task:'host test'}")
        })
        {
            var response = Invoke("unused", false, parent, "call domain=management action=list detail=true confirm=true", 0);
            Require(Body(response)["preview"]?.Value<bool>() == true, "withheld confirmation and parent dry-run remain previews");
        }
        foreach (string line in new[]
        {
            "call domain=management action=list detail=true confirm=false",
            "call domain=management action=list detail=true dryRun=true"
        })
        {
            var response = Invoke("unused", false, Parent(), line, 0);
            Require(Item(response)["preview"]?.Value<bool>() == true && Item(response)["result"] == null,
                "child dry-run and child refusal must not execute or produce a native result");
        }
        var missingTask = Invoke("unused", false, new JObject { ["confirm"] = true }, "call domain=management action=list detail=true", 0);
        Require(missingTask.IsError && (string)Body(missingTask)["phase"] == "preflight", "missing task fails before execution");
        var twoCommands = Invoke("unused", false, Parent(), "call domain=management action=list detail=true\ncall domain=management action=list detail=true", 0, false);
        Require(twoCommands.IsError, "multiple executable operations remain rejected without invoking either child");
    }

    private static void CallArgumentsKeepTheirScope()
    {
        var parent = Parent();
        parent["detail"] = false;
        parent["query"] = "parent scope";
        var response = Invoke("{\"count\":1}", false, parent,
            "call tool=colony_control domain=management action=list detail=true query=ExactId limit=1");
        var args = NativeBoundary.LastArguments;
        Require((string)args["query"] == "ExactId" && (int)args["limit"] == 1,
            "explicit child query and limit keep their native retrieval scope");
        Require((string)args["task"] == "host test" && (bool)args["confirm"], "task and confirmation pass through the existing policy");
        Require((bool)args["detail"] && Item(response)["result"] != null, "only explicit child detail controls full result inclusion");
    }

    private static CallToolResult Invoke(string raw, bool error, JObject parent, string line, int calls = 1, bool parseBody = true, string relative = "ops/colony.md")
    {
        string before = parent.ToString(Formatting.None);
        NativeBoundary.Reset(raw, error);
        var response = WorldEditorTools.ApplyOperationForTest(parent, relative, line);
        Require(NativeBoundary.Calls == calls, "native tool must be called exactly once for an executed operation, zero times for preview or rejection");
        Require(parent.ToString(Formatting.None) == before, "operation result handling must not mutate parent arguments");
        if (calls != 0)
        {
            Require(NativeBoundary.LastResult.Content[0].Text == raw, "result formatting must not mutate native result content");
            Require(NativeBoundary.LastArguments != null, "executed operations retain their parsed argument snapshot");
        }
        if (parseBody)
            Require(Body(response) != null, "the complete operation envelope remains valid JSON");
        return response;
    }

    private static JObject Parent() => new JObject { ["task"] = "host test", ["confirm"] = true };
    private static JObject Body(CallToolResult response) => JObject.Parse(response.Content[0].Text);
    private static JObject Item(CallToolResult response) => (JObject)Body(response)["results"][0];

    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
