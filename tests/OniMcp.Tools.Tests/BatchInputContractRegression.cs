using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

internal static class BatchInputContractRegression
{
    private const string ReadTool = "batch_contract_read";
    private static int checks;
    private static int childCalls;
    private static JObject lastChild;

    internal static void Run()
    {
        checks = 0;
        McpTool oldRead;
        McpTool oldGame;
        bool hadRead = OniToolRegistry.Tools.TryGetValue(ReadTool, out oldRead);
        bool hadGame = OniToolRegistry.Tools.TryGetValue("game_control", out oldGame);
        try
        {
            OniToolRegistry.Tools[ReadTool] = FakeTool(ReadTool, "read", "low");
            // Keep the real aggregate's conservative metadata and arguments. This
            // suite must not make a dangerous aggregate artificially read-only.
            OniToolRegistry.Tools["game_control"] = FakeTool("game_control", "execute", "dangerous");
            CheckObjectItemMetadata();
            CheckEntryShapesAndAliases();
            CheckLimitsAndDryRun();
            CheckConfirmationScope();
        }
        finally
        {
            Restore(ReadTool, hadRead, oldRead);
            Restore("game_control", hadGame, oldGame);
        }
        Console.WriteLine("Batch input and confirmation contract: " + checks + " host checks passed");
    }

    private static McpTool FakeTool(string name, string mode, string risk)
    {
        return new McpTool
        {
            Name = name,
            Group = "test",
            Mode = mode,
            Risk = risk,
            Handler = args =>
            {
                childCalls++;
                lastChild = (JObject)args.DeepClone();
                return CallToolResult.Text("{\"cycle\":42,\"paused\":true}");
            }
        };
    }

    private static void CheckObjectItemMetadata()
    {
        var tool = ToolBatchTools.CallMany();
        foreach (string name in new[] { "calls", "items" })
        {
            var parameter = tool.Parameters[name];
            Check(parameter.Type == "array", name + " remains an array");
            Check(parameter.Items != null && parameter.Items.Type == "object",
                name + " declares the existing object-entry runtime contract");
        }
    }

    private static void CheckEntryShapesAndAliases()
    {
        foreach (string arrayName in new[] { "calls", "items" })
            foreach (string nameKey in new[] { "name", "tool", "t" })
                foreach (string argsKey in new[] { "arguments", "args", "a" })
                {
                    var input = new JObject
                    {
                        [arrayName] = new JArray(new JObject
                        {
                            [nameKey] = ReadTool,
                            [argsKey] = new JObject { ["marker"] = "exact-child", ["flag"] = false }
                        })
                    };
                    var body = Invoke(input, false, 1);
                    Check((int)body["executed"] == 1 && (int)body["succeeded"] == 1 && (int)body["failed"] == 0,
                        "supported array/name/arguments aliases retain runtime success counts");
                    Check((string)lastChild["marker"] == "exact-child" && lastChild["flag"].Type == JTokenType.Boolean
                        && !(bool)lastChild["flag"], "child object values retain their types through all supported aliases");
                }

        foreach (JToken invalid in new JToken[] { new JValue("text"), new JValue(7), JValue.CreateNull(), new JArray() })
        {
            var invalidCalls = new JArray();
            invalidCalls.Add(invalid.DeepClone());
            var body = Invoke(new JObject { ["calls"] = invalidCalls }, true, 0);
            Check(!(bool)body["valid"] && (int)body["failed"] == 1 && (int)body["executed"] == 0,
                "non-object entries fail preflight without execution");
            Check(((string)body["results"][0]["text"]).Contains("must be an object"),
                "invalid entry is rejected for its shape");
        }
        foreach (JToken invalidArgs in new JToken[] { new JValue("text"), new JValue(7), new JValue(false), new JArray() })
        {
            var body = Invoke(new JObject { ["calls"] = new JArray(new JObject
                { ["name"] = ReadTool, ["arguments"] = invalidArgs.DeepClone() }) }, true, 0);
            Check(((string)body["results"][0]["text"]).Contains("arguments must be an object"),
                "scalar and array arguments remain rejected");
        }
        // Missing/null arguments are an existing supported empty-object alias,
        // distinct from a null call entry or malformed scalar argument.
        foreach (bool explicitNull in new[] { false, true })
        {
            var call = new JObject { ["name"] = ReadTool };
            if (explicitNull) call["arguments"] = JValue.CreateNull();
            Invoke(new JObject { ["calls"] = new JArray(call) }, false, 1);
            Check(lastChild.Count == 0, "omitted/null arguments retain the existing empty object contract");
        }
    }

    private static void CheckLimitsAndDryRun()
    {
        var twenty = new JArray();
        for (int i = 0; i < 20; i++) twenty.Add(new JObject
            { ["name"] = ReadTool, ["args"] = new JObject { ["index"] = i } });
        var accepted = Invoke(new JObject { ["calls"] = twenty }, false, 20);
        Check((int)accepted["requested"] == 20 && (int)accepted["executed"] == 20,
            "twenty child calls remain accepted");
        var twentyOne = (JArray)twenty.DeepClone();
        twentyOne.Add(new JObject { ["name"] = ReadTool });
        Invoke(new JObject { ["calls"] = twentyOne }, true, 0, false);
        Invoke(new JObject { ["calls"] = new JArray() }, true, 0, false);

        var preview = Invoke(new JObject { ["calls"] = twenty.DeepClone(), ["dryRun"] = true }, false, 0);
        Check((bool)preview["dryRun"] && (bool)preview["valid"] && (int)preview["executed"] == 0,
            "batch dry-run validates valid entries without calling tools");
    }

    private static void CheckConfirmationScope()
    {
        var absent = Invoke(GameInput(), true, 0);
        Check(HasConfirmFailure(absent), "game_control speed/time keeps the existing dangerous aggregate gate");

        var outerOnly = GameInput();
        outerOnly["confirm"] = true;
        Check(HasConfirmFailure(Invoke(outerOnly, true, 0)), "outer confirmation is not silently inherited by children");

        var childTrue = GameInput(true);
        var success = Invoke(childTrue, false, 1);
        Check((bool)lastChild["confirm"] && (string)lastChild["domain"] == "speed"
            && (string)lastChild["action"] == "time" && (int)success["executed"] == 1,
            "explicit child confirmation reaches the unchanged aggregate arguments exactly once");

        var defaultsTrue = GameInput();
        defaultsTrue["defaults"] = new JObject { ["confirm"] = true, ["marker"] = "default" };
        Invoke(defaultsTrue, false, 1);
        Check((bool)lastChild["confirm"] && (string)lastChild["marker"] == "default",
            "existing explicit defaults confirmation still applies");

        var childFalse = GameInput(false);
        childFalse["defaults"] = new JObject { ["confirm"] = true };
        Check(HasConfirmFailure(Invoke(childFalse, true, 0)), "child refusal overrides defaults confirmation");

        var defaultsAlias = GameInput();
        defaultsAlias["defaultArguments"] = new JObject { ["confirm"] = true };
        Invoke(defaultsAlias, false, 1);
        Check((bool)lastChild["confirm"], "defaultArguments keeps the existing defaults alias");

        var previewMissingConfirm = GameInput();
        previewMissingConfirm["dryRun"] = true;
        var rejectedPreview = Invoke(previewMissingConfirm, true, 0);
        Check((bool)rejectedPreview["dryRun"] && HasConfirmFailure(rejectedPreview),
            "outer dry-run does not invent missing child consent");
        var previewConfirmed = GameInput(true);
        previewConfirmed["dryRun"] = true;
        var validPreview = Invoke(previewConfirmed, false, 0);
        Check((bool)validPreview["valid"] && (int)validPreview["executed"] == 0,
            "confirmed dangerous calls are only validated during outer dry-run");

        var childPreview = GameInput();
        childPreview["calls"][0]["arguments"]["dryRun"] = true;
        var childPreviewResult = Invoke(childPreview, false, 1);
        // Native changes are governed by the child's dry-run implementation.
        // Generic batch executed counts handler calls, including child previews;
        // only the outer batch dryRun skips dispatch entirely.
        Check((bool)lastChild["dryRun"] && lastChild["confirm"] == null
            && (int)childPreviewResult["executed"] == 1,
            "child dry-run bypasses the confirmation gate and is dispatched once without invented consent");
    }

    private static JObject GameInput(bool? confirm = null)
    {
        var arguments = new JObject { ["domain"] = "speed", ["action"] = "time", ["task"] = "host confirmation regression" };
        if (confirm.HasValue) arguments["confirm"] = confirm.Value;
        return new JObject { ["calls"] = new JArray(new JObject { ["name"] = "game_control", ["arguments"] = arguments }) };
    }

    private static JObject Invoke(JObject input, bool expectError, int expectedCalls, bool json = true)
    {
        childCalls = 0;
        lastChild = null;
        string before = input.ToString(Formatting.None);
        CallToolResult result = ToolBatchTools.CallMany().Handler(input);
        Check(result.IsError == expectError, "batch preserves expected success/error state");
        Check(childCalls == expectedCalls, "batch invokes exactly the authorized number of child calls");
        Check(input.ToString(Formatting.None) == before, "validation/default merging/execution leave caller input unchanged");
        return json ? JObject.Parse(result.Content[0].Text) : null;
    }

    private static bool HasConfirmFailure(JObject body)
    {
        return !(bool)body["valid"] && (int)body["executed"] == 0
            && ((string)body["results"][0]["text"]).Contains("requires arguments.confirm=true");
    }

    private static void Restore(string name, bool existed, McpTool value)
    {
        if (existed) OniToolRegistry.Tools[name] = value;
        else OniToolRegistry.Tools.Remove(name);
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
