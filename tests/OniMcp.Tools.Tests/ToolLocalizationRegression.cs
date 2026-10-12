using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class ToolLocalizationRegression
{
    internal static void Run()
    {
        var source = Fixture();
        JToken sourceBefore = JToken.FromObject(source);
        var snapshots = new Dictionary<string, List<McpToolInfo>>();
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            var localized = McpToolInfoLocalization.Localize(source, language);
            snapshots.Add(language, localized);
            Check(localized.Count == source.Count, "localization keeps every tool");
            Check(localized[0].Name == "world_editor", "tool identifiers stay canonical");
            Check(localized[0].Description == "[core/write/high] " + McpLocalization.GetForLanguage(
                language, "tools.world_editor.description", "canonical world editor description"),
                "tool description follows the selected language and retains its metadata prefix");
            Check(localized[0].InputSchema.Properties["query"].Description == McpLocalization.GetForLanguage(
                language, "tools.world_editor.parameters.query", "canonical query description"),
                "parameter descriptions follow the selected language");
            Check(localized[0].InputSchema.Properties["task"].Description == McpLocalization.GetForLanguage(
                language, "tools.task.description", "canonical task description"),
                "the common task description follows the selected language");
            Check(JToken.DeepEquals(WithoutDescriptions(sourceBefore), WithoutDescriptions(JToken.FromObject(localized))),
                "names, schema structure, values, headers, requirements and execution metadata stay unchanged");
            Check(JToken.DeepEquals(sourceBefore, JToken.FromObject(source)),
                "localization does not mutate canonical schemas");
            Check(localized[1].Description == "Player colony 八百万 and dupe ペイ use Tile / 野営トイレ.",
                "unknown prose containing user and game names is preserved verbatim");
            Check(localized[0].InputSchema.Properties["userField"].Description == "User field: カカポ",
                "unknown parameter descriptions are preserved verbatim");
            Check(localized[0].InputSchema.Properties["userField"].Properties["gameName"].Description == "砂岩 / SandStone",
                "unregistered nested descriptions preserve game names");
            Check(localized[1].InputSchema == null && localized[1].Execution == null,
                "optional null metadata remains null");
        }

        Check(snapshots.Values.Select(items => items[0].Description).Distinct().Count() == 3,
            "all three language catalogs provide distinct public descriptions");
        Check(snapshots.Values.Select(items => items[0].InputSchema.Properties["query"].Description).Distinct().Count() == 3,
            "all three language catalogs provide distinct parameter descriptions");
        Check(snapshots.Values.Select(items => items[0].InputSchema.Properties["task"].Description).Distinct().Count() == 3,
            "all three language catalogs provide distinct task descriptions");
        Check(JToken.DeepEquals(JToken.FromObject(snapshots["en"]),
            JToken.FromObject(McpToolInfoLocalization.Localize(source, "unsupported-test-locale"))),
            "unsupported locales fall back to English without changing the schema");

        AssertIndependentSnapshots(source, sourceBefore, snapshots);
        AssertLanguageChanges(source);
        Console.WriteLine("OK: localized tool metadata preserves protocol fields and isolated snapshots");
    }

    private static void AssertIndependentSnapshots(List<McpToolInfo> source, JToken sourceBefore,
        Dictionary<string, List<McpToolInfo>> snapshots)
    {
        var first = snapshots["ja"];
        var second = McpToolInfoLocalization.Localize(source, "ja");
        JToken secondBefore = JToken.FromObject(second);
        JToken englishBefore = JToken.FromObject(snapshots["en"]);
        first[0].Name = "changed tool";
        first[0].Description = "changed description";
        first[0].Execution.TaskSupport = "changed execution";
        first[0].InputSchema.Type = "changed schema type";
        first[0].InputSchema.Required.Add("changed required field");
        var payload = first[0].InputSchema.Properties["userField"];
        payload.Type = "changed property type";
        payload.McpHeader = "changed-header";
        payload.Enum.Add("changed enum list");
        ((JObject)payload.Enum[1])["nested"]["value"] = "changed enum object";
        payload.Properties["gameName"].Description = "changed nested description";
        payload.Properties["gameName"].Enum[0] = "changed nested enum";
        payload.Properties.Add("changed nested property", new SchemaProperty { Type = "boolean" });
        first[0].InputSchema.Properties.Remove("query");
        first.RemoveAt(1);

        Check(JToken.DeepEquals(sourceBefore, JToken.FromObject(source)),
            "mutating a localized snapshot does not mutate canonical input at any depth");
        Check(JToken.DeepEquals(secondBefore, JToken.FromObject(second)),
            "same-language snapshots do not share mutable metadata");
        Check(JToken.DeepEquals(englishBefore, JToken.FromObject(snapshots["en"])),
            "different-language snapshots do not share mutable metadata");
        Check(JToken.DeepEquals(secondBefore, JToken.FromObject(McpToolInfoLocalization.Localize(source, "ja"))),
            "later localized reads remain intact after a caller mutates an earlier result");
    }

    private static void AssertLanguageChanges(List<McpToolInfo> source)
    {
        var previousLanguageProvider = McpLocalization.LanguageProvider;
        var previousGameLanguageProvider = McpLocalization.GameLanguageProvider;
        try
        {
            string selected = "ja";
            McpLocalization.LanguageProvider = () => selected;
            McpLocalization.GameLanguageProvider = () => "zh-Klei";
            var japanese = McpToolInfoLocalization.Localize(source, McpLocalization.CurrentLanguage);
            selected = "en";
            var english = McpToolInfoLocalization.Localize(source, McpLocalization.CurrentLanguage);
            selected = "auto";
            var chinese = McpToolInfoLocalization.Localize(source, McpLocalization.CurrentLanguage);
            Check(japanese[0].Description != english[0].Description
                && japanese[0].Description != chinese[0].Description
                && english[0].Description != chinese[0].Description,
                "changing the selected language does not reuse the previous language's descriptions");
            selected = "ja";
            Check(JToken.DeepEquals(JToken.FromObject(japanese),
                JToken.FromObject(McpToolInfoLocalization.Localize(source, McpLocalization.CurrentLanguage))),
                "switching back restores the original language snapshot");
        }
        finally
        {
            McpLocalization.LanguageProvider = previousLanguageProvider;
            McpLocalization.GameLanguageProvider = previousGameLanguageProvider;
        }
    }

    private static List<McpToolInfo> Fixture()
    {
        return new List<McpToolInfo>
        {
            new McpToolInfo
            {
                Name = "world_editor",
                Description = "[core/write/high] canonical world editor description",
                Execution = new ToolExecution { TaskSupport = "optional" },
                InputSchema = new InputSchema
                {
                    Type = "object",
                    Required = new List<string> { "query", "task" },
                    Properties = new Dictionary<string, SchemaProperty>
                    {
                        ["query"] = new SchemaProperty { Type = "string", Description = "canonical query description" },
                        ["task"] = new SchemaProperty { Type = "string", Description = "canonical task description" },
                        ["userField"] = new SchemaProperty
                        {
                            Type = "object",
                            Description = "User field: カカポ",
                            McpHeader = "x-user-request",
                            Enum = new List<object>
                            {
                                "canonical-enum",
                                new JObject { ["nested"] = new JObject { ["value"] = "unaltered enum object" } },
                                17,
                                true,
                                null
                            },
                            Properties = new Dictionary<string, SchemaProperty>
                            {
                                ["gameName"] = new SchemaProperty
                                {
                                    Type = "string",
                                    Description = "砂岩 / SandStone",
                                    Enum = new List<object> { "SandStone", "Tile", "ja", "zh-Hans" }
                                }
                            }
                        }
                    }
                }
            },
            new McpToolInfo
            {
                Name = "unknown_user_tool",
                Description = "Player colony 八百万 and dupe ペイ use Tile / 野営トイレ."
            }
        };
    }

    private static JToken WithoutDescriptions(JToken value)
    {
        JToken clone = value.DeepClone();
        RemoveDescriptions(clone);
        return clone;
    }

    private static void RemoveDescriptions(JToken value)
    {
        var obj = value as JObject;
        if (obj != null)
        {
            obj.Property("description")?.Remove();
            foreach (JProperty property in obj.Properties().ToList())
                RemoveDescriptions(property.Value);
        }
        else if (value is JArray array)
        {
            foreach (JToken item in array)
                RemoveDescriptions(item);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Tool localization regression: " + message);
    }
}
