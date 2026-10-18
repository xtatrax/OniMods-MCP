using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

internal static class ManagementSkillReadRegression
{
    private static int checks;

    internal static void Run()
    {
        StringQueriesRenderNativeSkillRows();
        ForwardingStaysDetachedAndReadOnly();
        FormatSelectionPreservesLegacyPrecedence();
        ScalarAndContainerQueriesDoNotThrow();
        JsonPayloadsRemainExact();
        MissingValuesAndEmptyListsStayMissingAndEmpty();
        Console.WriteLine("Management skill Markdown regression checks passed: " + checks);
    }

    private static void StringQueriesRenderNativeSkillRows()
    {
        foreach (bool target in new[] { false, true })
        {
            var args = new JObject { ["query"] = "Mining", ["limit"] = 4 };
            if (target) args["id"] = 17;
            string text = Read(args, Fixture(target));
            Require(text.StartsWith("# Duplicant Skill Tree", StringComparison.Ordinal), "a scalar skill query must render Markdown");
            Require(text.Contains("| Mining | 0 | `Mining1` | Hard\\|Dig Field | 0 |  |"), "native zero, escaped names and empty prerequisites survive");
            Require(text.Contains("| Mining | 1 | `Mining2` | Deep Dig | 2 | Mining1, Mining|Prereq |".Replace("Mining|Prereq", "Mining\\|Prereq")),
                "prerequisite arrays remain scalar IDs and are escaped");
            Require(text.IndexOf("`Mining1`", StringComparison.Ordinal) < text.IndexOf("`Mining2`", StringComparison.Ordinal),
                "the native group/tier/ID ordering remains deterministic");
            Require(text.Contains("Mastered | Can learn |") == target, "target-only columns follow the observed native dupe object");
            Require(text.Contains("- dupe:") == target, "global lists must not invent a target");
            if (target)
            {
                Require(text.Contains("Dupe\\|Name Line"), "target display names escape table separators and newlines");
                Require(text.Contains("| 0 |  | False | True |"), "false mastered and true canMaster remain visible");
                Require(text.Contains("Mining\\|Prereq | True | False |"), "true mastered and false canMaster remain visible");
            }
            Require(text.Contains("## Edit Commands") && text.Contains("# learn_skill"), "existing commented write examples remain comments");
        }
    }

    private static void ForwardingStaysDetachedAndReadOnly()
    {
        foreach (var scope in new[]
        {
            new JObject(),
            new JObject { ["id"] = 17 },
            new JObject { ["name"] = "Dupe Name" },
            new JObject { ["id"] = 17, ["name"] = "Dupe Name" }
        })
        {
            scope["query"] = "Research";
            scope["limit"] = 1;
            scope["confirm"] = true;
            scope["force"] = true;
            scope["skillId"] = "MustNotLearn";
            Read(scope, Fixture(scope["id"] != null || scope["name"] != null));
            var expected = new JObject { ["domain"] = "skill", ["action"] = "list" };
            foreach (string key in new[] { "id", "name", "query", "limit" })
                if (scope[key] != null) expected[key] = scope[key].DeepClone();
            Require(JToken.DeepEquals(ManagementSkillBoundary.Arguments, expected), "only id/name/query/limit are forwarded to the native list");
        }

        var nested = new JObject { ["id"] = 17, ["query"] = new JObject { ["format"] = "json" }, ["limit"] = new JArray(1) };
        string raw = Fixture(true);
        Require(Read(nested, raw, true) == raw, "child mutations cannot change the parent's selected format");
        Require(!ReferenceEquals(ManagementSkillBoundary.ReceivedReference["query"], nested["query"]), "nested query is detached from caller state");
        Require(!ReferenceEquals(ManagementSkillBoundary.ReceivedReference["limit"], nested["limit"]), "forwarded container values are deep cloned");
    }

    private static void FormatSelectionPreservesLegacyPrecedence()
    {
        Format(new JObject(), false);
        Format(new JObject { ["query"] = "Mining" }, false);
        Format(new JObject { ["format"] = " JSON ", ["query"] = "Mining" }, true);
        Format(new JObject { ["query"] = new JObject { ["format"] = " JSON " } }, true);
        Format(new JObject { ["format"] = "markdown", ["query"] = new JObject { ["format"] = "json" } }, false);
        Format(new JObject { ["format"] = "", ["query"] = new JObject { ["format"] = "json" } }, false);
        Format(new JObject { ["format"] = JValue.CreateNull(), ["query"] = new JObject { ["format"] = "json" } }, false);
        Format(new JObject { ["format"] = "markdown", ["path"] = "/active/management/skills.md?format=json" }, true);
        Format(new JObject { ["format"] = "", ["path"] = "/active/management/skills.md?FORMAT=JSON" }, true);
        Format(new JObject { ["query"] = new JObject { ["format"] = "markdown" }, ["path"] = "/active/management/skills.md?format=json" }, true);
        Format(new JObject { ["path"] = "/active/management/skills.md?format=markdown" }, false);

        string raw = Fixture(false);
        foreach (var args in new[]
        {
            new JObject { ["query"] = "Mining", ["format"] = "json" },
            new JObject { ["query"] = new JObject { ["format"] = "json" } },
            new JObject { ["query"] = "Mining", ["format"] = "markdown", ["path"] = "/active/management/skills.md?format=json" }
        })
            Require(Read(args, raw) == raw, "the actual skills reader honors the existing JSON selectors");
    }

    private static void ScalarAndContainerQueriesDoNotThrow()
    {
        foreach (var query in new JToken[]
        {
            new JValue("Mining"), new JValue(""), new JValue("format=json"), new JValue(0), new JValue(false),
            JValue.CreateNull(), new JArray("Mining"), new JArray(new JObject { ["format"] = "json" }),
            new JObject(), new JObject { ["other"] = "json" }, new JObject { ["format"] = JValue.CreateNull() }
        })
        {
            var args = new JObject { ["query"] = query.DeepClone(), ["limit"] = 1 };
            Format(args, false);
            string text = Read(args, Fixture(false));
            Require(text.StartsWith("# Duplicant Skill Tree", StringComparison.Ordinal), "non-object queries must not be treated as nested format objects");
            Require(JToken.DeepEquals(ManagementSkillBoundary.Arguments["query"], query), "format inspection must not rewrite the native query token");
        }
    }

    private static void JsonPayloadsRemainExact()
    {
        foreach (string raw in new[] { Fixture(true), Fixture(false), "null", "[]", "  {\n\"skills\": []\n}  ", "raw native text", "" })
            Require(Read(new JObject { ["format"] = "json", ["query"] = "Mining" }, raw) == raw,
                "JSON mode retains the exact first native text without parsing, adding counts or changing fields");
    }

    private static void MissingValuesAndEmptyListsStayMissingAndEmpty()
    {
        var root = JObject.Parse(Fixture(true));
        var first = (JObject)root["skills"][1];
        first.Remove("mastered");
        first["canMaster"] = JValue.CreateNull();
        string text = Read(new JObject { ["query"] = "Mining" }, root.ToString(Formatting.None));
        Require(text.Contains("| 0 |  |  |  |"), "missing mastery values remain blank, not false, zero or true");
        foreach (bool target in new[] { false, true })
        {
            root = JObject.Parse(Fixture(target));
            root["skills"] = new JArray();
            root["returned"] = 0;
            text = Read(new JObject { ["query"] = "NoSuchNativeSkill", ["limit"] = 1 }, root.ToString(Formatting.None));
            Require(!text.Contains("`Mining1`") && !text.Contains("`Mining2`"), "zero-result native arrays must not manufacture rows");
            Require(text.Contains("Mastered | Can learn |") == target, "empty target and global lists retain their own column contract");
        }
    }

    private static string Fixture(bool target)
    {
        var root = new JObject
        {
            ["dupe"] = target ? (JToken)new JObject { ["id"] = 17, ["name"] = "Dupe|Name\nLine" } : JValue.CreateNull(),
            ["returned"] = 2,
            ["skills"] = new JArray(
                new JObject { ["id"] = "Mining2", ["name"] = "Deep Dig", ["skillGroup"] = "Mining", ["tier"] = 1,
                    ["moraleExpectation"] = 2, ["priorSkills"] = new JArray("Mining1", "Mining|Prereq") },
                new JObject { ["id"] = "Mining1", ["name"] = "Hard|Dig\nField", ["skillGroup"] = "Mining", ["tier"] = 0,
                    ["moraleExpectation"] = 0, ["priorSkills"] = new JArray() })
        };
        if (target)
        {
            root["skills"][0]["mastered"] = true;
            root["skills"][0]["canMaster"] = false;
            root["skills"][1]["mastered"] = false;
            root["skills"][1]["canMaster"] = true;
        }
        return root.ToString(Formatting.None);
    }

    private static void Format(JObject args, bool expected)
    {
        string before = args.ToString(Formatting.None);
        Require(WorldEditorTools.WantsManagementJsonForTest(args) == expected, "format selection preserves explicit/nested/path precedence");
        Require(args.ToString(Formatting.None) == before, "format selection is a read-only inspection");
    }

    private static string Read(JObject args, string raw, bool mutateArguments = false)
    {
        string before = args.ToString(Formatting.None);
        ManagementSkillBoundary.Reset(raw, mutateArguments);
        CallToolResult result = WorldEditorTools.ReadManagementForTest(args);
        Require(ManagementSkillBoundary.Calls == 1, "every skills read calls the native list exactly once");
        Require(args.ToString(Formatting.None) == before, "the actual reader preserves caller arguments");
        Require(!result.IsError, "valid native skill text remains a successful read");
        Require(ManagementSkillBoundary.Text == raw, "rendering does not modify native source text");
        return result.Content.Single().Text;
    }

    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
