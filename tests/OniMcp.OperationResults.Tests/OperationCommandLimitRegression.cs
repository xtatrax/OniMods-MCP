using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class OperationCommandLimitRegression
{
    private const string Relative = "ops/game.md";
    private const string OneCommand = "call domain=speed action=time";
    private static int checks;

    internal static void Run()
    {
        MultipleCommandsAreRejectedBeforeAnyChild();
        CommentsDoNotCountAsCommands();
        EveryBlockIsPreflightedBeforeExecution();
        CaptureExamplesStayInRelevantOperationFiles();
        LocalizedRejectionAndGuideRemainAvailable();
        Console.WriteLine("Operation command-limit contract checks passed: " + checks);
    }

    private static void MultipleCommandsAreRejectedBeforeAnyChild()
    {
        foreach (int count in new[] { 2, 5 })
        foreach (bool preview in new[] { false, true })
        foreach (bool preflight in new[] { false, true })
        {
            var parent = Parent();
            parent["dryRun"] = preview;
            parent["allowPartial"] = true;
            string before = parent.ToString(Formatting.None);
            string replacement = "# ignored comment\n## Edit Commands\n```text\n"
                + string.Join("\n# not an executable command\n", Enumerable.Repeat(OneCommand, count)) + "\n```";
            NativeBoundary.Reset("{\"changed\":true}", false);
            var response = preflight
                ? WorldEditorTools.PreflightCommandLimitForTest(parent, Relative, replacement)
                : WorldEditorTools.ApplyOperationForTest(parent, Relative, replacement);
            Require(response.IsError && NativeBoundary.Calls == 0,
                "A multi-command replacement must reject before dispatch, even with allowPartial or preview.");
            Require(parent.ToString(Formatting.None) == before, "The rejection must not alter caller policy arguments.");
            VerifyLimit(StructuredBody(response), count);
        }
    }

    private static void CommentsDoNotCountAsCommands()
    {
        const string replacement = "# call domain=speed action=pause\n## Edit Commands\n```text\n"
            + "# another comment\ncall domain=speed action=time\n```\n";
        NativeBoundary.Reset("{\"changed\":false,\"returned\":1}", false);
        var preflight = WorldEditorTools.PreflightCommandLimitForTest(Parent(), Relative, replacement);
        Require(!preflight.IsError && NativeBoundary.Calls == 0
            && StructuredBody(preflight)["commands"].Count() == 1,
            "Headings, fences and comments must not turn a single executable command into a refusal.");
        var previewParent = Parent();
        previewParent["dryRun"] = true;
        var preview = WorldEditorTools.ApplyOperationForTest(previewParent, Relative, replacement);
        Require(!preview.IsError && StructuredBody(preview)["preview"].Value<bool>() && NativeBoundary.Calls == 0,
            "The existing single-command preview gate must remain nonexecuting.");
        var executed = WorldEditorTools.ApplyOperationForTest(Parent(), Relative, replacement);
        Require(!executed.IsError && NativeBoundary.Calls == 1
            && StructuredBody(executed)["executedCommands"].Value<int>() == 1,
            "A single command must still dispatch exactly once through the actual adapter.");
        NativeBoundary.Reset("unused", false);
        var empty = WorldEditorTools.ApplyOperationForTest(Parent(), Relative, "# comment only\n```text\n```\n");
        Require(empty.IsError && NativeBoundary.Calls == 0,
            "A comment-only replacement must keep the existing empty-command refusal.");
    }

    private static void EveryBlockIsPreflightedBeforeExecution()
    {
        var parent = Parent();
        parent["path"] = "/active/" + Relative;
        parent["allowPartial"] = true;
        parent["content"] = Block(OneCommand) + Block(OneCommand + "\n" + OneCommand);
        string before = parent.ToString(Formatting.None);
        NativeBoundary.Reset("{\"changed\":true,\"succeeded\":1}", false);
        var response = WorldEditorTools.EditForCountTest(parent);
        var body = StructuredBody(response);
        Require(response.IsError && NativeBoundary.Calls == 0,
            "A later preflight refusal must leave the earlier valid block unexecuted.");
        Require(body["phase"].Value<string>() == "preflight" && body["block"].Value<int>() == 1
            && body["applied"].Value<int>() == 0 && body["appliedUnit"].Value<string>() == "edit_blocks"
            && !body["changed"].Value<bool>(), "Whole-edit zero is justified only by the outer preflight rejection.");
        VerifyLimit(JObject.Parse(body["error"].Value<string>()), 2);
        Require(parent.ToString(Formatting.None) == before, "Whole-edit preflight must retain parent arguments.");

        parent.Remove("allowPartial");
        parent["content"] = Block(OneCommand) + Block(OneCommand);
        NativeBoundary.Reset("{\"changed\":true}", false);
        Require(WorldEditorTools.EditForCountTest(parent).IsError && NativeBoundary.Calls == 0,
            "The default multi-block opt-in guard must remain intact.");
        // OutcomeRegression already covers all preflights succeeding, then a later
        // runtime error with changed=true. Its partial/outcome checks run unchanged.
    }

    private static void CaptureExamplesStayInRelevantOperationFiles()
    {
        string[] captureExamples =
        {
            "捕 小动物@(101,130):7",
            "捕 x=101 y=130 priority=7 dryRun=true",
            "抓捕 小动物@(101,130):7 dryRun=true"
        };
        var fileField = typeof(WorldEditorTools).GetField("OperationFileTools",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Require(fileField != null, "operation_capture_example_scope: actual operation-file registry is available.");
        var files = fileField.GetValue(null) as System.Collections.Generic.IDictionary<string, string>;
        Require(files != null, "operation_capture_example_scope: actual operation-file registry has the expected contract.");
        // ops/tools.md uses its separate tool-index reader before ordinary help.
        string[] normalPaths = files.Keys.Where(path => path != "ops/tools.md")
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Require(normalPaths.Length == 23 && normalPaths.Contains("ops/orders.md") && normalPaths.Contains("ops/ranching.md"),
            "operation_capture_example_scope: cover every existing ordinary operation file and both capture pages.");
        var preferred = McpLocalization.LanguageProvider;
        try
        {
            McpLocalization.LanguageProvider = () => "en";
            foreach (string relative in normalPaths)
            {
                NativeBoundary.Reset("unused", false);
                var read = WorldEditorTools.ReadOperationForTest(relative);
                Require(!read.IsError, "operation_capture_example_scope: actual reader succeeds for " + relative);
                string text = read.Content[0].Text;
                string marker = "## Edit Commands" + Environment.NewLine + "```text" + Environment.NewLine;
                string fence = "```" + Environment.NewLine;
                int start = text.IndexOf(marker, StringComparison.Ordinal);
                Require(start >= 0 && start == text.LastIndexOf(marker, StringComparison.Ordinal)
                    && text.EndsWith(fence, StringComparison.Ordinal),
                    "operation_capture_example_scope: command heading and fence remain exact for " + relative);
                string commandBody = text.Substring(start + marker.Length,
                    text.Length - start - marker.Length - fence.Length);
                string[] comments = commandBody.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                Require(comments.All(line => line.StartsWith("# ", StringComparison.Ordinal)),
                    "operation_capture_example_scope: every example remains a comment for " + relative);
                string[] commands = comments.Select(line => line.Substring(2)).ToArray();
                bool receivesCaptureExamples = relative == "ops/orders.md" || relative == "ops/ranching.md";
                foreach (string capture in captureExamples)
                    Require(commands.Count(line => line == capture) == (receivesCaptureExamples ? 1 : 0),
                        "operation_capture_example_scope: capture example count differs for " + relative + ": " + capture);
                Require(commands.Any(line => !captureExamples.Contains(line)),
                    "operation_capture_example_scope: the ordinary non-capture examples remain available for " + relative);
                if (receivesCaptureExamples)
                    Require(commands.Length >= captureExamples.Length
                        && commands.Skip(commands.Length - captureExamples.Length).SequenceEqual(captureExamples),
                        "operation_capture_example_scope: capture examples retain their exact tail order for " + relative);
                Require(NativeBoundary.Calls == 0,
                    "operation_capture_example_scope: guide reads do not dispatch a native child for " + relative);
            }
        }
        finally { McpLocalization.LanguageProvider = preferred; }
    }

    private static void LocalizedRejectionAndGuideRemainAvailable()
    {
        var preferred = McpLocalization.LanguageProvider;
        string[] helpKeys = { "operation_files.mode_hint", "operation_files.edit_tip", "operation_files.syntax_hint" };
        string[] legacyEnglish =
        {
            "- mode: edit lines under `## Edit Commands`, then submit SEARCH/REPLACE.",
            "- edit tip: use an empty SEARCH block to run fresh commands, or copy the current snippet exactly.",
            "- syntax: `call tool=<tool_name> key=value ...`; in typed files, `tool=` is optional."
        };
        var selectedHints = new string[3][];
        int languageIndex = 0;
        string toolJson = null;
        try
        {
            foreach (string language in new[] { "en", "ja", "zh-Hans" })
            {
                McpLocalization.LanguageProvider = () => language;
                NativeBoundary.Reset("unused", false);
                var response = WorldEditorTools.PreflightCommandLimitForTest(Parent(), Relative,
                    OneCommand + "\n" + OneCommand);
                var body = StructuredBody(response);
                VerifyLimit(body, 2);
                string error = body["error"].Value<string>();
                Require(error == McpLocalization.Format("operation_files.single_command_error", null, 2, 1)
                    && !error.Contains("{0}") && !error.Contains("{1}"),
                    "Existing numeric refusal placeholders and argument order remain resolved.");
                Require(NativeBoundary.Calls == 0, "Language selection must not dispatch a child operation.");
                var read = WorldEditorTools.ReadOperationForTest(Relative);
                string safety = McpLocalization.Get("operation_files.single_command_safety");
                Require(!read.IsError && !string.IsNullOrWhiteSpace(safety)
                    && safety != "operation_files.single_command_safety" && read.Content[0].Text.Contains(safety),
                    "The actual operation reader must include the selected-language single-replacement safety contract.");
                string text = read.Content[0].Text;
                string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                string[] hints = helpKeys.Select(key => McpLocalization.Get(key)).ToArray();
                selectedHints[languageIndex++] = hints;
                for (int index = 0; index < helpKeys.Length; index++)
                    Require(!string.IsNullOrWhiteSpace(hints[index]) && hints[index] != helpKeys[index]
                        && lines.Contains(hints[index]),
                        "operation_help_localization_contract: actual reader uses selected hint " + helpKeys[index] + "/" + language);
                if (language == "en")
                    Require(hints.SequenceEqual(legacyEnglish), "English help lines retain their exact legacy prose and punctuation.");
                string commands = "## Edit Commands" + Environment.NewLine + "```text" + Environment.NewLine
                    + "# call domain=speed action=pause" + Environment.NewLine + "```" + Environment.NewLine;
                Require(lines.Count(line => line == "## Edit Commands") == 1 && text.EndsWith(commands, StringComparison.Ordinal),
                    "The reserved command heading, fence and commented DSL example remain exact.");
                string[] jsonParts = text.Split(new[] { "```json" }, StringSplitOptions.None);
                Require(jsonParts.Length == 2, "The actual reader retains one Tool JSON block.");
                string metadata = jsonParts[1].Split(new[] { "```" }, StringSplitOptions.None)[0].Trim();
                if (toolJson == null) toolJson = metadata;
                else Require(metadata == toolJson, "Changing the three hints leaves the fixture Tool JSON unchanged.");
                Require(NativeBoundary.Calls == 0, "Reading localized operation help must dispatch no native child.");
            }
            for (int index = 0; index < helpKeys.Length; index++)
                Require(selectedHints.Select(hints => hints[index]).Distinct().Count() == 3,
                    "Every new help line retains three independently selected runtime translations.");
        }
        finally { McpLocalization.LanguageProvider = preferred; }
    }

    private static void VerifyLimit(JObject body, int requested)
    {
        Require(body["ok"]?.Value<bool>() == false && body["phase"]?.Value<string>() == "preflight"
            && body["code"]?.Value<string>() == "operation_replacement_command_limit",
            "A command-limit refusal must expose the stable structured preflight reason.");
        Require(body["scope"]?.Value<string>() == "operation_replacement"
            && body["file"]?.Value<string>() == Relative && body["requestedCommands"]?.Value<int>() == requested
            && body["maxCommands"]?.Value<int>() == 1, "The rejection must identify its replacement file, count and limit.");
        Require(body["executedCommands"]?.Value<int>() == 0 && body["changed"]?.Value<bool>() == false
            && body["partial"]?.Value<bool>() == false,
            "Zero-dispatch evidence must be scoped to this rejected replacement, not a rollback promise.");
        Require(!string.IsNullOrWhiteSpace(body["error"]?.Value<string>())
            && !string.IsNullOrWhiteSpace(body["guidance"]?.Value<string>())
            && body["error"].Value<string>() != "operation_files.single_command_error"
            && body["guidance"].Value<string>() != "operation_files.single_command_guidance",
            "The machine reason must retain resolved human guidance.");
    }

    private static JObject StructuredBody(CallToolResult result)
    {
        JObject body = null;
        try { body = JObject.Parse(result.Content[0].Text); }
        catch (JsonException) { }
        Require(body != null, "Command-limit rejection must expose a structured body; a plain legacy refusal is insufficient.");
        return body;
    }

    private static string Block(string replacement)
    {
        return "<<<<<<< SEARCH\n\n=======\n" + replacement + "\n>>>>>>> REPLACE\n";
    }

    private static JObject Parent() => new JObject { ["task"] = "host command-limit boundary", ["confirm"] = true };
    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
}

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static CallToolResult PreflightCommandLimitForTest(JObject parent, string relative, string replacement)
        {
            return PreflightOperationMarkdownEdit(parent, relative, replacement);
        }
    }
}
