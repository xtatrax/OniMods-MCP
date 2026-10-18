using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

// Exercises production registry projection, serialization and localization cloning.
// GameStubs supplies only the factory metadata fixture. This is not acceptance of
// the actual ServerTools factory; verify installed runtime tools/list separately.
internal static class CoreBatchSchemaRegression
{
    private static int checks;

    internal static void Run()
    {
        checks = 0;
        var preferred = McpLocalization.LanguageProvider;
        var game = McpLocalization.GameLanguageProvider;
        try
        {
            McpLocalization.LanguageProvider = () => "en";
            McpLocalization.GameLanguageProvider = () => "en";
            foreach (bool includeAll in new[] { false, true })
                SnapshotIsolation(includeAll);

            var unspecified = JObject.Parse(JsonConvert.SerializeObject(
                new SchemaProperty { Type = "array" },
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include }));
            Check(unspecified.Property("items") == null,
                "An unspecified item schema must remain omitted even when serializer nulls are enabled.");
            var specified = JObject.Parse(JsonConvert.SerializeObject(
                new SchemaProperty { Type = "array", Items = new SchemaProperty { Type = "object" } }));
            Check((string)specified["items"]["type"] == "object", "A non-null item schema was not serialized.");
        }
        finally
        {
            McpLocalization.LanguageProvider = preferred;
            McpLocalization.GameLanguageProvider = game;
        }
        Console.WriteLine("Batch schema projection and deep snapshot isolation: " + checks + " checks passed (factory metadata substituted).");
    }

    private static void SnapshotIsolation(bool includeAll)
    {
        McpLocalization.LanguageProvider = () => "en";
        var first = OniToolRegistry.GetToolInfos(includeAll);
        var second = OniToolRegistry.GetToolInfos(includeAll);
        Check(first.Count == (includeAll ? OniToolRegistry.GetTools().Count(tool => !tool.Hidden) : 7),
            "Schema projection changed tool visibility.");
        ValidatePublishedShape(first);
        ValidatePublishedShape(second);
        var baseline = first.ToDictionary(tool => tool.Name, tool => StripDescriptions(Serialize(tool)), StringComparer.Ordinal);
        var canonical = OniToolRegistry.GetTools().Single(tool => tool.Name == "server_control");
        var sourceCalls = canonical.Parameters["calls"].Items;
        var sourceItems = canonical.Parameters["items"].Items;
        var sourceCallsBefore = Serialize(sourceCalls);
        var sourceItemsBefore = Serialize(sourceItems);
        var firstBatch = Batch(first);
        var secondBatch = Batch(second);
        Check(!ReferenceEquals(firstBatch, secondBatch) && !ReferenceEquals(firstBatch.Items, secondBatch.Items)
            && !ReferenceEquals(firstBatch.Items, sourceCalls), "Top-level array/items snapshots share mutable schema nodes.");
        var firstFilter = FilterItem(firstBatch);
        var secondFilter = FilterItem(secondBatch);
        Check(!ReferenceEquals(firstFilter, secondFilter)
            && !ReferenceEquals(firstFilter.Properties, secondFilter.Properties)
            && !ReferenceEquals(firstFilter.Properties["mode"].Enum, secondFilter.Properties["mode"].Enum),
            "Nested array items/properties/enum collections were shallow copied.");
        Check(!ReferenceEquals(firstFilter.Properties["fixtureOptions"].Enum[0], secondFilter.Properties["fixtureOptions"].Enum[0]),
            "A nested enum JSON object was shared across snapshots.");

        McpLocalization.LanguageProvider = () => "ja";
        var japanese = OniToolRegistry.GetToolInfos(includeAll);
        McpLocalization.LanguageProvider = () => "zh-Hans";
        var chinese = OniToolRegistry.GetToolInfos(includeAll);
        foreach (var translated in new[] { japanese, chinese })
        {
            ValidatePublishedShape(translated);
            foreach (var tool in translated)
                Check(JToken.DeepEquals(baseline[tool.Name], StripDescriptions(Serialize(tool))),
                    "Localization changed protocol structure for " + tool.Name + ".");
        }

        // Corrupt nodes at each boundary, including a JSON object held by Enum.
        firstBatch.Items.Type = "caller-changed";
        firstBatch.Items.Properties["tool"].Enum.Add("caller-only");
        firstFilter.Properties["mode"].Enum[0] = "caller-changed";
        ((JArray)((JObject)firstFilter.Properties["fixtureOptions"].Enum[0])["nested"]["values"]).Add("caller-only");
        firstFilter.Properties["extra"] = new SchemaProperty { Type = "null" };
        first.Single(tool => tool.Name == "server_control").InputSchema.Properties["items"].Items.Type = "caller-changed";
        foreach (var untouched in new[] { second, japanese, chinese })
            AssertBaseline(untouched, baseline, "Changing an English snapshot poisoned another existing snapshot");
        Check(JToken.DeepEquals(sourceCallsBefore, Serialize(sourceCalls))
            && JToken.DeepEquals(sourceItemsBefore, Serialize(sourceItems)),
            "A caller's schema mutation reached registered factory parameters.");

        // A different-language mutation must not poison any cached canonical tree.
        var japaneseBatch = Batch(japanese);
        japaneseBatch.Items.Properties.Clear();
        japanese.Single(tool => tool.Name == "server_control").InputSchema.Properties["items"].Items.Type = "caller-ja";
        AssertBaseline(second, baseline, "Changing Japanese Items/Properties poisoned the English snapshot");
        AssertBaseline(chinese, baseline, "Changing Japanese Items/Properties poisoned the Chinese snapshot");
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            McpLocalization.LanguageProvider = () => language;
            var reread = OniToolRegistry.GetToolInfos(includeAll);
            AssertBaseline(reread, baseline, "A fresh " + language + " snapshot reused caller mutations");
            ValidatePublishedShape(reread);
        }
        Check(JToken.DeepEquals(sourceCallsBefore, Serialize(canonical.Parameters["calls"].Items))
            && JToken.DeepEquals(sourceItemsBefore, Serialize(canonical.Parameters["items"].Items)),
            "Cross-language mutation changed registry source metadata.");
    }

    private static void ValidatePublishedShape(List<McpToolInfo> tools)
    {
        var batch = tools.Single(tool => tool.Name == "server_control");
        var schema = Serialize(batch)["inputSchema"]["properties"];
        foreach (string parameter in new[] { "calls", "items" })
        {
            Check((string)schema[parameter]["type"] == "array"
                && (string)schema[parameter]?["items"]?["type"] == "object",
                "Published " + parameter + " did not preserve its object item schema.");
            Check(batch.InputSchema.Properties[parameter].Items != null,
                "Projection dropped " + parameter + " item metadata.");
        }
        Check(batch.InputSchema.Properties["query"].Items == null && schema["query"]["items"] == null,
            "A non-array parameter gained an invented item schema.");
        foreach (var tool in tools.Where(tool => tool.Name != "server_control"))
        {
            Check(new HashSet<string>(tool.InputSchema.Properties.Keys).SetEquals(new[] { "query", "untouchedFixture", "task" }),
                "The unrelated tool schema gained or lost fields: " + tool.Name);
            var serialized = Serialize(tool)["inputSchema"]["properties"];
            Check((string)serialized["query"]["type"] == "string" && (string)serialized["task"]["type"] == "string",
                "The unrelated tool scalar schema changed: " + tool.Name);
            Check((string)serialized["untouchedFixture"]["type"] == "array"
                && serialized["untouchedFixture"]["items"] == null
                && tool.InputSchema.Properties["untouchedFixture"].Items == null,
                "An unrelated untyped array was globally assigned object items: " + tool.Name);
            Check(tool.InputSchema.Required.SequenceEqual(new[] { "task" }),
                "Batch item metadata changed required arguments of " + tool.Name);
        }
    }

    private static SchemaProperty Batch(List<McpToolInfo> tools)
    {
        return tools.Single(tool => tool.Name == "server_control").InputSchema.Properties["calls"];
    }

    private static SchemaProperty FilterItem(SchemaProperty calls)
    {
        return calls.Items.Properties["args"].Properties["filters"].Items;
    }

    private static void AssertBaseline(List<McpToolInfo> tools, Dictionary<string, JToken> baseline, string message)
    {
        foreach (var tool in tools)
            Check(JToken.DeepEquals(baseline[tool.Name], StripDescriptions(Serialize(tool))), message + ": " + tool.Name);
    }

    private static JToken Serialize(object value)
    {
        return JToken.Parse(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
    }

    private static JToken StripDescriptions(JToken token)
    {
        var obj = token as JObject;
        if (obj != null)
        {
            obj.Remove("description");
            foreach (var property in obj.Properties()) StripDescriptions(property.Value);
        }
        var array = token as JArray;
        if (array != null)
            foreach (var child in array) StripDescriptions(child);
        return token;
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
