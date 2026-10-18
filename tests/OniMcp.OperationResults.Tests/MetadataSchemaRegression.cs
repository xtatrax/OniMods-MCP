using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class MetadataSchemaRegression
{
    internal static void Run()
    {
        var serializer = JsonSerializer.Create(McpJsonUtil.Settings);
        var item = new SchemaProperty
        {
            Type = "object",
            Properties = new Dictionary<string, SchemaProperty>
            {
                ["arguments"] = new SchemaProperty { Type = "object" },
                ["nested"] = new SchemaProperty
                {
                    Type = "array", Items = new SchemaProperty { Type = "string" }
                }
            }
        };
        var tool = new McpTool
        {
            Name = "metadata_fixture", Group = "tools", Mode = "read", Risk = "none", Description = "fixture",
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["calls"] = new McpToolParameter { Type = "array", Items = item },
                ["items"] = new McpToolParameter { Type = "array", Items = item },
                ["other"] = new McpToolParameter { Type = "array" },
                ["limit"] = new McpToolParameter { Type = "integer" }
            }
        };
        var before = JToken.FromObject(tool.Parameters, serializer);
        var expected = JToken.FromObject(item, serializer);
        var manifest = JObject.FromObject(ToolCatalogTools.ManifestForTest(tool), serializer);
        var ops = WorldEditorTools.ToolSummaryForTest(tool);
        int checks = 0;
        foreach (var output in new[] { manifest, ops })
        {
            foreach (string key in new[] { "calls", "items" })
            {
                Require(JToken.DeepEquals(output["parameters"]?[key]?["items"], expected),
                    "manual metadata exports retain the complete optional item schema");
                checks++;
            }
            Require(output["parameters"]?["other"]?["items"] == null,
                "unspecified array Items must be absent, not JSON null"); checks++;
            Require(output["parameters"]?["limit"]?["items"] == null,
                "scalar metadata must not acquire an Items schema"); checks++;
            Require((string)output["parameters"]?["calls"]?["type"] == "array",
                "the existing array type remains unchanged"); checks++;
        }
        Require(JToken.DeepEquals(before, JToken.FromObject(tool.Parameters, serializer)),
            "manual metadata serialization must not mutate source tool parameters"); checks++;
        manifest["parameters"]["calls"]["items"]["type"] = "changed";
        ops["parameters"]["calls"]["items"]["properties"]["arguments"]["type"] = "changed";
        Require(JToken.DeepEquals(before, JToken.FromObject(tool.Parameters, serializer)),
            "detached serialized manual metadata must not share source state"); checks++;
        Console.WriteLine("Manual metadata item-schema regression checks passed: " + checks);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
