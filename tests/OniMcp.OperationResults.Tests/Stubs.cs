using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// Only the game/registry boundary and unrelated screen/file access are substituted.
// Operation execution, command parsing and authorization are production sources.
namespace OniMcp.Tools
{
    internal static class NativeBoundary
    {
        internal static string Text;
        internal static bool Error;
        internal static int Calls;
        internal static JObject LastArguments;
        internal static CallToolResult LastResult;
        internal static string LastTool;

        internal static void Reset(string text, bool error)
        {
            Text = text;
            Error = error;
            Calls = 0;
            LastArguments = null;
            LastResult = null;
            LastTool = null;
        }

        internal static CallToolResult Call(JObject args)
        {
            Calls++;
            LastArguments = (JObject)args.DeepClone();
            LastResult = Error ? CallToolResult.Error(Text) : CallToolResult.Text(Text);
            return LastResult;
        }
    }

    public sealed class McpTool
    {
        public string Name { get; set; }
        public string Group { get; set; }
        public string Risk { get; set; }
        public string Description { get; set; }
        public string Mode { get; set; }
        public bool Hidden { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, McpToolParameter> Parameters { get; set; }
        public Func<JObject, CallToolResult> Handler { get; set; }
    }

    public sealed class McpToolParameter
    {
        public string Type { get; set; }
        public bool Required { get; set; }
        public string Description { get; set; }
        public SchemaProperty Items { get; set; }
        public List<string> EnumValues { get; set; }
        public List<object> SchemaEnumValues => EnumValues?.Cast<object>().ToList();
    }

    public static class OniToolRegistry
    {
        public static List<McpTool> GetVisibleTools() => throw new NotSupportedException();
        public static bool TryGetOperation(string name, out McpTool tool)
        {
            tool = new McpTool { Name = name, Handler = NativeBoundary.Call };
            return name == "colony_control" || name == "coordinate_control" || name == "dupes_control"
                || name == "world_editor" || name == "game_control" || name == "server_control"
                || name == "search_control";
        }

        public static bool HasCoordinateArguments(JObject args)
        {
            return args["x"] != null || args["y"] != null || args["x1"] != null || args["y1"] != null;
        }

        public static CallToolResult CallToolFromWorldEditor(string name, JObject args, bool semanticCoordinates)
        {
            NativeBoundary.LastTool = name;
            return NativeBoundary.Call(args);
        }
    }

    public static class ToolUtil
    {
        public static int? GetInt(JObject args, string key) => args?[key]?.Value<int?>();
        public static bool GetBool(JObject args, string key, bool fallback)
        {
            bool value;
            return args?[key] != null && bool.TryParse(args[key].ToString(), out value) ? value : fallback;
        }
    }

    public static class ToolCallMiddleware
    {
        public static bool TryGetTaskDescription(JObject args, out string task)
        {
            var token = args?["task"];
            task = token?.Type == JTokenType.String ? (string)token : null;
            return !string.IsNullOrWhiteSpace(task);
        }
    }

    public static class ToolMetadata
    {
        public static bool HasLegacyAliases(McpTool tool) => false;
        public static string LegacyAliasesDeprecationWarning(McpTool tool) => throw new NotSupportedException();
    }

    public static partial class ToolCatalogTools
    {
        internal static Dictionary<string, object> ManifestForTest(McpTool tool) => ToolToManifest(tool);
        private static string ExpandQuery(string query) => throw new NotSupportedException();
        private static string NormalizeDetail(string detail) => throw new NotSupportedException();
        private static string ExactVisibleToolLookup(string query) => throw new NotSupportedException();
        private static int Score(McpTool tool, string expandedQuery, string query) => throw new NotSupportedException();
    }

    public static class ManagementTools
    {
        public static McpTool ControlManagement() => throw new NotSupportedException();
    }

    public static class DuplicantTools
    {
        public static McpTool ControlDupes() => new McpTool { Handler = ManagementSkillBoundary.Call };
    }

    public static partial class WorldEditorTools
    {
        internal static JObject ToolSummaryForTest(McpTool tool) => ToolSummary(tool);
        internal static CallToolResult ReadOperationForTest(string relative) => ReadOperationMarkdown("/active/" + relative, relative);
        internal static CallToolResult ApplyOperationForTest(JObject parent, string relative, string text)
        {
            return ApplyOperationMarkdownEdit(parent, relative, text);
        }

        private static string NormalizeSearchText(string text) => (text ?? string.Empty)
            .Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        private static string SaveRelativePath(string path) => path;
        private static int? ParsePriority(string token) => throw new NotSupportedException();
        private static JObject InheritWorldEditorSandboxPolicy(JObject parent, JObject child) => child;
        private static CallToolResult RunWithWorldEditorInstantBuildScope(JObject args, Func<CallToolResult> call) => call();
        private static bool ValidateWorldEditorSandboxPolicy(JObject args, out string error)
        {
            error = null;
            return true;
        }
        private static CallToolResult ReadOperationToolIndexMarkdown(string path) => throw new NotSupportedException();
    }
}

namespace OniMcp.Support
{
    public static class McpToolInfoLocalization
    {
        public static string ParameterDescription(string tool, string key, string text) => text;
        public static string Description(string tool, string text) => text;
    }
}
