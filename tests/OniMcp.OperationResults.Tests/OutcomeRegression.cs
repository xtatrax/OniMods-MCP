using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

internal static class OutcomeRegression
{
    private static int checks;
    internal static void Run()
    {
        foreach (bool changed in new[] { false, true })
        foreach (bool error in new[] { false, true })
        {
            string raw = new JObject { ["changed"] = changed, ["planned"] = 0, ["succeeded"] = 1, ["alreadyPresent"] = 1 }.ToString();
            NativeBoundary.Reset(raw, error);
            var result = WorldEditorTools.ApplyOperationForTest(Parent(), "ops/dupes.md", "call domain=skill action=learn skillId=Fixture force=false");
            var value = Parse(result); var item = value["results"][0]; var outcome = item["nativeOutcome"];
            Require(result.IsError == error && (bool)item["isError"] == error, "native error must not become success");
            Require((int)value["executedCommands"] == 1 && NativeBoundary.Calls == 1, "commands count native calls including failures");
            Require((string)value["appliedUnit"] == "legacy_child_counter", "legacy count is explicitly a separate unit");
            Require((bool)outcome["observed"] && (bool)outcome["changed"] == changed && (bool)value["changed"] == changed,
                "true and false native changed states survive compact results");
            Require((int?)outcome["counters"]["planned"] == 0 && (int)outcome["counters"]["succeeded"] == 1,
                "native zero is not replaced with another field");
            Require(outcome["countUnit"].Type == JTokenType.Null && item["result"] == null,
                "unknown generic units and existing compact detail gate remain unchanged");
            Require(NativeBoundary.LastResult.Content[0].Text == raw, "native text is not rewritten");
            Require((int)value["applied"] == (error ? 0 : 1), "legacy counter selection remains compatible");
            if (error) Require((bool)value["partial"] == changed, "failed one-command result distinguishes confirmed partial effect from unchanged refusal");
        }
        foreach (string raw in new[] { "{}", "{\"changed\":null}", "{\"changed\":\"false\"}", "{\"changed\":0}", "{\"planned\":0}" })
        {
            var value = Invoke(raw); var evidence = value["results"][0]["nativeOutcome"];
            Require(evidence["changed"].Type == JTokenType.Null && value["changed"].Type == JTokenType.Null, "missing or mistyped changed stays unknown");
            Require((bool)evidence["observed"], "valid object is observed even if change field is absent");
        }
        foreach (string raw in new[] { "", "not-json", "[]", "null", "42", "true" })
        {
            var value = Invoke(raw); var evidence = value["results"][0]["nativeOutcome"];
            Require(!(bool)evidence["observed"] && evidence["changed"].Type == JTokenType.Null, "non-object native result is unavailable");
            Require(((JObject)evidence["counters"]).Count == 0 && (string)evidence["reasonCode"] == "native_result_unavailable",
                "unavailable counters are not manufactured as zero");
        }
        foreach (JToken invalid in new JToken[] { "1", true, -1, 1.0, JValue.CreateNull(), JToken.Parse("9223372036854775808") })
        {
            var raw = new JObject { ["changed"] = false, ["planned"] = invalid, ["marked"] = 0 }.ToString();
            var outcome = Invoke(raw)["results"][0]["nativeOutcome"];
            Require(outcome["counters"]["planned"] == null && (int?)outcome["counters"]["marked"] == 0, "invalid count is omitted while a typed zero remains");
            Require((string)outcome["reasonCode"] == "invalid_native_fields" && !(bool)outcome["changed"], "invalid counters cannot erase an explicit changed false");
        }
        NativeBoundary.Reset(null, false); NativeBoundary.Results.Enqueue(null);
        var absentResult = Parse(WorldEditorTools.ApplyOperationForTest(Parent(), "ops/dupes.md", "call domain=skill action=list"));
        var absentEvidence = absentResult["results"][0]["nativeOutcome"];
        Require(!(bool)absentResult["ok"] && (int)absentResult["executedCommands"] == 1 && NativeBoundary.Calls == 1,
            "null native result remains one failed observed call without a retry");
        Require(!(bool)absentEvidence["observed"] && absentResult["changed"].Type == JTokenType.Null,
            "actual null result is unavailable rather than unchanged");

        foreach (var gate in new[] { new JObject { ["dryRun"] = true }, new JObject { ["confirm"] = false } })
        {
            NativeBoundary.Reset("{\"changed\":true}", false); var parent = Parent(); parent.Merge(gate);
            var value = Parse(WorldEditorTools.ApplyOperationForTest(parent, "ops/dupes.md", "call domain=skill action=list"));
            Require((bool)value["preview"] && (int)value["executedCommands"] == 0 && NativeBoundary.Calls == 0, "operation preview has zero real commands");
            Require(!(bool)value["changed"], "adapter knows no command was executed in preview");
        }
        NativeBoundary.Reset("{\"changed\":true}", false);
        var rejected = WorldEditorTools.ApplyOperationForTest(Parent(), "ops/dupes.md", "call domain=skill action=list\ncall domain=skill action=list");
        Require(rejected.IsError && NativeBoundary.Calls == 0, "single-operation guard is retained");

        NativeBoundary.Reset(null, false);
        NativeBoundary.Results.Enqueue(CallToolResult.Text("{\"changed\":false,\"returned\":9}"));
        NativeBoundary.Results.Enqueue(CallToolResult.Error("{\"changed\":true,\"planned\":3}"));
        var outerArgs = Parent(); outerArgs["allowPartial"] = true; outerArgs["path"] = "/active/ops/dupes.md";
        string block = "<<<<<<< SEARCH\n\n=======\ncall domain=skill action=list\n>>>>>>> REPLACE\n";
        outerArgs["content"] = block + block;
        var outer = Parse(WorldEditorTools.EditForCountTest(outerArgs));
        Require(NativeBoundary.Calls == 2 && !(bool)outer["ok"], "two permitted blocks preserve native error");
        Require((int)outer["applied"] == 1 && (string)outer["appliedUnit"] == "edit_blocks", "outer failure does not add failed native counters to block count");
        Require((bool)outer["partial"] && (bool)outer["changed"], "outer failure preserves confirmed change in failed block");
        Require(JObject.Parse((string)outer["results"][1]["result"])["results"][0]["nativeOutcome"]["changed"].Value<bool>(),
            "failed child native outcome remains independently accessible");

        NativeBoundary.Reset("{\"changed\":false,\"returned\":9}", false);
        outerArgs["content"] = block;
        var readOuter = Parse(WorldEditorTools.EditForCountTest(outerArgs));
        Require((int)readOuter["applied"] == 1 && !(bool)readOuter["changed"], "one successfully executed block is not a mutation count");
        Require((int)JObject.Parse((string)readOuter["results"][0]["result"])["executedCommands"] == 1,
            "native returned quantity does not overwrite actual command count");
        Console.WriteLine("Native outcome/unit regression checks passed: " + checks);
    }
    private static JObject Parent() => new JObject { ["confirm"] = true, ["task"] = "host typed native outcome" };
    private static JObject Invoke(string raw)
    {
        NativeBoundary.Reset(raw, false);
        return Parse(WorldEditorTools.ApplyOperationForTest(Parent(), "ops/dupes.md", "call domain=skill action=list"));
    }
    private static JObject Parse(CallToolResult result) => JObject.Parse(result.Content[0].Text);
    private static void Require(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
}
