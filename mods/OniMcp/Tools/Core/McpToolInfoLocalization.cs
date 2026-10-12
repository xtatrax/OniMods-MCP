using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    /// <summary>Localizes presentation text on a detached schema, never the operation contract.</summary>
    internal static class McpToolInfoLocalization
    {
        internal static List<McpToolInfo> Localize(IEnumerable<McpToolInfo> tools, string language)
        {
            return tools.Select(tool => new McpToolInfo
            {
                Name = tool.Name,
                Description = LocalizeDescription(tool.Name, tool.Description, language),
                Execution = tool.Execution == null ? null : new ToolExecution { TaskSupport = tool.Execution.TaskSupport },
                InputSchema = tool.InputSchema == null ? null : new InputSchema
                {
                    Type = tool.InputSchema.Type,
                    Required = tool.InputSchema.Required == null ? null : new List<string>(tool.InputSchema.Required),
                    Properties = CloneProperties(tool.InputSchema.Properties, tool.Name, language, true)
                }
            }).ToList();
        }

        internal static string Description(string toolName, string fallback, string language = null)
        {
            return McpLocalization.GetForLanguage(language ?? McpLocalization.CurrentLanguage,
                "tools." + toolName + ".description", fallback);
        }

        internal static string ParameterDescription(string toolName, string parameter, string fallback, string language = null)
        {
            return McpLocalization.GetForLanguage(language ?? McpLocalization.CurrentLanguage,
                parameter == "task" ? "tools.task.description" : "tools." + toolName + ".parameters." + parameter,
                fallback);
        }

        private static string LocalizeDescription(string name, string description, string language)
        {
            // Group/mode/risk is machine-readable metadata prepended by ToolMetadata.
            int end = description != null && description.StartsWith("[", StringComparison.Ordinal)
                ? description.IndexOf("] ", StringComparison.Ordinal) : -1;
            string prefix = end >= 0 ? description.Substring(0, end + 2) : string.Empty;
            string prose = end >= 0 ? description.Substring(end + 2) : description;
            return prefix + Description(name, prose, language);
        }

        private static Dictionary<string, SchemaProperty> CloneProperties(
            Dictionary<string, SchemaProperty> properties, string tool, string language, bool topLevel)
        {
            if (properties == null) return null;
            var copy = new Dictionary<string, SchemaProperty>(properties.Comparer);
            foreach (var entry in properties)
            {
                var value = entry.Value;
                copy.Add(entry.Key, value == null ? null : new SchemaProperty
                {
                    Type = value.Type,
                    Description = topLevel
                        ? ParameterDescription(tool, entry.Key, value.Description, language) : value.Description,
                    Enum = value.Enum == null ? null : value.Enum.Select(CloneEnumValue).ToList(),
                    Properties = CloneProperties(value.Properties, tool, language, false),
                    McpHeader = value.McpHeader
                });
            }
            return copy;
        }

        private static object CloneEnumValue(object value)
        {
            var token = value as JToken;
            return token == null ? value : token.DeepClone();
        }
    }
}
