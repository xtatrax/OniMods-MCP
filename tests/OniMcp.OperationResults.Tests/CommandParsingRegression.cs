using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

// The production raw-ops, semantic-ops and management-edit entry points are linked
// into this project. Only their terminal native handler boundary is substituted.
internal static class CommandParsingRegression
{
    private static int checks;

    internal static void Run()
    {
        ArraysRetainTheirTypesAtEveryEntry();
        ScalarPayloadAndArrowCompatibility();
        MalformedInputsNeverReachAChild();
        ExistingExecutionGatesRemainIntact();
        Console.WriteLine("Command parser entry-point regression checks passed: " + checks);
    }

    private static void ArraysRetainTheirTypesAtEveryEntry()
    {
        var nested = new JArray(new JArray(1, 2), new JObject
        {
            ["name"] = "Ada \"Quoted\" Name",
            ["literal"] = "brackets [ ] { } and key=value",
            ["flags"] = new JArray(true, false, JValue.CreateNull())
        }, new JArray(), "C:\\path with spaces");
        foreach (var fixture in new[]
        {
            new ArrayFixture("[\"power\"]", new JArray("power")),
            new ArrayFixture("[\"power\",\"logic\"]", new JArray("power", "logic")),
            new ArrayFixture("[]", new JArray()),
            new ArrayFixture("[\"2026-10-20T00:00:00Z\"]", new JArray("2026-10-20T00:00:00Z")),
            new ArrayFixture("[ 1,\t 2, 3 ]", new JArray(1, 2, 3)),
            new ArrayFixture(nested.ToString(Formatting.None), nested),
            new ArrayFixture("[ [ 1, 2 ], { \"name\": \"A B\" }, [] ]",
                new JArray(new JArray(1, 2), new JObject { ["name"] = "A B" }, new JArray())),
            new ArrayFixture(new JArray("a \"quoted\" value", "x\\y", "[literal]").ToString(Formatting.None),
                new JArray("a \"quoted\" value", "x\\y", "[literal]"))
        })
        {
            var raw = Raw("call domain=management action=list values=" + fixture.Text + " limit=7 enabled=false", Parent(), true);
            ArrayEquals(raw["values"], fixture.Value, "raw ops preserves complete typed JSON arrays");
            Require((int)raw["limit"] == 7 && raw["enabled"].Type == JTokenType.Boolean && !(bool)raw["enabled"],
                "tokens after array remain independent and typed");
            foreach (var parent in new[] { PreviewParent(), RefusedParent() })
            {
                var preview = Raw("call domain=management action=list values=" + fixture.Text + " limit=7", parent, false);
                ArrayEquals(preview["values"], fixture.Value, "raw syntax preview retains typed arrays without child execution");
            }
            var semantic = Raw("dig areaId=fixture_area type=" + fixture.Text + " priority=7 confirm=true", Parent(), true, "ops/orders.md");
            ArrayEquals(semantic["type"], fixture.Value, "semantic production path retains the typed value it forwards");
            Require((string)semantic["domain"] == "area" && (string)semantic["action"] == "dig"
                && (string)semantic["areaId"] == "fixture_area" && (int)semantic["priority"] == 7,
                "semantic command retains its requested target and action");
            foreach (var parent in new[] { PreviewParent(), RefusedParent() })
            {
                var management = ManagementPreview("priority id=17 values=" + fixture.Text + " name=\"Ada Lovelace\"", parent);
                ArrayEquals(management["values"], fixture.Value, "management edit production path preserves typed arrays");
                Require((int)management["id"] == 17 && (string)management["name"] == "Ada Lovelace",
                    "management array token does not absorb following scope/name arguments");
            }
            Reset();
            var preflight = WorldEditorTools.PreflightManagementCommandForParsingTest("management/priorities.md",
                "priority id=17 values=" + fixture.Text);
            Require(!preflight.IsError && NativeBoundary.Calls == 0 && ManagementSkillBoundary.Calls == 0,
                "management preflight parses a valid array without invoking any child");
        }
    }

    private static void ScalarPayloadAndArrowCompatibility()
    {
        var scalar = Raw("call domain=management action=list enabled=true count=-3 quotedCount=\"7\" name=\"Ada Lovelace\" query=\"[1,2]\"", Parent(), true);
        Require(scalar["enabled"].Type == JTokenType.Boolean && (bool)scalar["enabled"], "existing boolean inference remains");
        Require(scalar["count"].Type == JTokenType.Integer && (int)scalar["count"] == -3, "existing signed integer inference remains");
        Require(scalar["quotedCount"].Type == JTokenType.Integer && (int)scalar["quotedCount"] == 7, "legacy quoted scalar inference remains");
        Require((string)scalar["name"] == "Ada Lovelace", "quoted names keep internal whitespace");
        Require(scalar["query"].Type == JTokenType.String && (string)scalar["query"] == "[1,2]",
            "explicitly quoted array-looking text remains a string");
        string escaped = "Ada \\\"Quoted\\\" Name";
        var quoted = Raw("call domain=management action=list name=\"" + escaped + "\" limit=4", Parent(), true);
        Require((string)quoted["name"] == escaped && (int)quoted["limit"] == 4,
            "escaped quote does not terminate a token or introduce scalar unescaping");
        string escapedEnd = "Ada \\\"";
        var endQuote = Raw("call domain=management action=list name=\"" + escapedEnd + "\" limit=5", Parent(), true);
        Require((string)endQuote["name"] == escapedEnd && (int)endQuote["limit"] == 5,
            "only the outer quote pair is removed; a trailing escaped content quote is retained");
        var bracketScalar = Raw("call domain=management action=list suffix=] literal=ordinary[bracket equation=one=two", Parent(), true);
        Require((string)bracketScalar["suffix"] == "]" && (string)bracketScalar["literal"] == "ordinary[bracket" && (string)bracketScalar["equation"] == "one=two",
            "bare bracket scalar compatibility does not become structured JSON inference");
        string payload = "{\"domain\":\"world\", \"query\":\"a \\\"quoted\\\" value\", \"ids\":[1, 2], \"nested\":{\"enabled\":true}}";
        var objectArgs = Raw("call targetTool=read_control payload=" + payload + " detail=true", Parent(), true, "ops/coordinate.md");
        Require(objectArgs["payload"].Type == JTokenType.String && (string)objectArgs["payload"] == payload,
            "valid object payload stays the original complete string for coordinate-wrapper compatibility");
        Require(JToken.DeepEquals(JObject.Parse((string)objectArgs["payload"]), JObject.Parse(payload)),
            "object payload nested arrays, whitespace and escaped quotes remain parseable and exact");
        foreach (string arrow in new[] { "->", "=>" })
        {
            var move = Raw("move 人@\"Ada Lovelace\" " + arrow + " \"Printing Pod\" worldId=3 confirm=true", Parent(), true, "ops/dupes.md");
            Require(NativeBoundary.LastTool == "dupes_control" && (string)move["domain"] == "command"
                && (string)move["action"] == "move_to" && (string)move["name"] == "Ada Lovelace"
                && (string)move["query"] == "Printing Pod" && (int)move["worldId"] == 3,
                "semantic arrows, quoted dupe name and quoted target preserve the existing operation");
        }
        var management = ManagementPreview("priority id=17 query=\"[1,2]\" count=2 enabled=false name=\"Ada Lovelace\"", PreviewParent());
        Require(management["query"].Type == JTokenType.String && (string)management["query"] == "[1,2]"
            && (int)management["count"] == 2 && !(bool)management["enabled"],
            "management scalar and explicitly quoted string compatibility remains");
    }

    private static void MalformedInputsNeverReachAChild()
    {
        foreach (string suffix in new[]
        {
            "values=[", "values=[1,2", "values=[1,2}", "values=[broken]",
            "values=[1]suffix", "payload={\"name\":\"Ada\"", "payload={\"name\":1]}",
            "payload={broken}", "name=\"unterminated", "name=\"escaped\\\""
        })
        {
            foreach (var parent in new[] { Parent(), PreviewParent(), RefusedParent() })
            {
                RejectOperation("call domain=management action=list " + suffix, parent, "ops/colony.md");
                RejectOperation("dig areaId=fixture_area " + suffix, parent, "ops/orders.md");
                Reset();
                var management = WorldEditorTools.ApplyManagementCommandForParsingTest(parent,
                    "management/priorities.md", "priority id=17 " + suffix);
                Reject(management, "malformed management command fails even under preview/refused/execution policy");
            }
            Reset();
            Reject(WorldEditorTools.PreflightManagementCommandForParsingTest("management/priorities.md",
                "priority id=17 " + suffix), "management preflight rejects malformed JSON/quoting");
        }
    }

    private static void ExistingExecutionGatesRemainIntact()
    {
        foreach (var parent in new[] { PreviewParent(), RefusedParent(), new JObject { ["task"] = "parser fixture" } })
        {
            Raw("call domain=management action=list values=[1,2] confirm=true", parent, false);
            Raw("dig areaId=fixture_area type=[1,2] confirm=true", parent, false, "ops/orders.md");
        }
        foreach (string childGate in new[] { "dryRun=true", "confirm=false" })
            Raw("call domain=management action=list values=[1,2] " + childGate, Parent(), false);
        foreach (var parent in new[] { Parent(), PreviewParent() })
        {
            Reset();
            var multiple = WorldEditorTools.ApplyOperationForTest(parent, "ops/colony.md",
                "call domain=management action=list values=[1]\ncall domain=management action=list values=[2]");
            Reject(multiple, "raw ops single-command guard remains intact with typed arrays");
            Reset();
            var management = WorldEditorTools.ApplyManagementCommandForParsingTest(parent, "management/priorities.md",
                "priority id=17 values=[1]\npriority id=18 values=[2]");
            Reject(management, "management single-command guard remains intact with typed arrays");
        }
    }

    private static JObject Raw(string line, JObject parent, bool executed, string relative = "ops/colony.md")
    {
        Reset();
        string before = parent.ToString(Formatting.None);
        var response = WorldEditorTools.ApplyOperationForTest(parent, relative, line);
        Require(!response.IsError, "valid production operation command must parse: " + line);
        Require(parent.ToString(Formatting.None) == before, "parser and execution policy do not mutate caller arguments");
        Require(NativeBoundary.Calls == (executed ? 1 : 0) && ManagementSkillBoundary.Calls == 0,
            "only explicitly executed valid operation invokes exactly one child");
        if (executed) return NativeBoundary.LastArguments;
        var body = BodyWithoutDateInference(response);
        return (JObject)(body["details"]?[0]?["arguments"] ?? body["results"]?[0]?["arguments"]);
    }

    private static JObject ManagementPreview(string line, JObject parent)
    {
        Reset();
        string before = parent.ToString(Formatting.None);
        var response = WorldEditorTools.ApplyManagementCommandForParsingTest(parent, "management/priorities.md", line);
        Require(!response.IsError && NativeBoundary.Calls == 0 && ManagementSkillBoundary.Calls == 0,
            "management preview parses with zero child calls");
        Require(parent.ToString(Formatting.None) == before, "management parser does not mutate caller arguments");
        return (JObject)BodyWithoutDateInference(response)["results"][0]["arguments"];
    }

    private static JObject BodyWithoutDateInference(CallToolResult response)
    {
        using (var reader = new JsonTextReader(new StringReader(response.Content[0].Text)))
        {
            reader.DateParseHandling = DateParseHandling.None;
            return JObject.Load(reader);
        }
    }

    private static void RejectOperation(string line, JObject parent, string relative)
    {
        Reset();
        Reject(WorldEditorTools.ApplyOperationForTest(parent, relative, line), "malformed operation must reject before child invocation");
    }

    private static void Reject(CallToolResult response, string message)
    {
        Require(response.IsError, message);
        Require(NativeBoundary.Calls == 0 && ManagementSkillBoundary.Calls == 0,
            "malformed/single-command rejection makes zero native handler calls");
    }

    private static void Reset()
    {
        NativeBoundary.Reset("{\"count\":1}", false);
        ManagementSkillBoundary.Reset("{}");
    }

    private static JObject Parent() => new JObject { ["task"] = "parser fixture", ["confirm"] = true };
    private static JObject PreviewParent() => new JObject { ["task"] = "parser fixture", ["confirm"] = true, ["dryRun"] = true };
    private static JObject RefusedParent() => new JObject { ["task"] = "parser fixture", ["confirm"] = false };

    private static void ArrayEquals(JToken actual, JArray expected, string message)
    {
        Require(actual != null && actual.Type == JTokenType.Array, message + " (native argument type)");
        Require(JToken.DeepEquals(actual, expected), message + " (complete values)");
    }

    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ArrayFixture
    {
        internal readonly string Text;
        internal readonly JArray Value;
        internal ArrayFixture(string text, JArray value) { Text = text; Value = value; }
    }
}

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static CallToolResult ApplyManagementCommandForParsingTest(JObject args, string relative, string text)
            => ApplyManagementMarkdownEdit(args, relative, text);
        internal static CallToolResult PreflightManagementCommandForParsingTest(string relative, string text)
            => PreflightManagementMarkdownEdit(relative, text);
    }
}
