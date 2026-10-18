using System;
using System.Collections.Generic;
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

        internal static void Reset(string text, bool error)
        {
            Text = text;
            Error = error;
            Calls = 0;
            LastArguments = null;
            LastResult = null;
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
        public Dictionary<string, McpToolParameter> Parameters { get; set; }
        public Func<JObject, CallToolResult> Handler { get; set; }
    }

    public sealed class McpToolParameter
    {
        public string Type { get; set; }
        public bool Required { get; set; }
        public string Description { get; set; }
    }

    public static class OniToolRegistry
    {
        public static bool TryGetOperation(string name, out McpTool tool)
        {
            tool = new McpTool { Name = name, Handler = NativeBoundary.Call };
            return name == "colony_control" || name == "coordinate_control" || name == "dupes_control"
                || name == "world_editor" || name == "game_control" || name == "server_control";
        }

        public static bool HasCoordinateArguments(JObject args)
        {
            return args["x"] != null || args["y"] != null || args["x1"] != null || args["y1"] != null;
        }

        public static CallToolResult CallToolFromWorldEditor(string name, JObject args, bool semanticCoordinates)
        {
            return NativeBoundary.Call(args);
        }
    }

    public static class ToolUtil
    {
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

    public static class ManagementTools
    {
        public static McpTool ControlManagement() => throw new NotSupportedException();
    }

    public static class DuplicantTools
    {
        public static McpTool ControlDupes() => throw new NotSupportedException();
    }

    public static partial class WorldEditorTools
    {
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
        private static string ReadManagementIndexMarkdown(string path) => throw new NotSupportedException();
        private static CallToolResult ReadScheduleManagementMarkdown(JObject args, string path) => throw new NotSupportedException();
        private static CallToolResult ReadPrioritiesManagementMarkdown(JObject args, string path) => throw new NotSupportedException();
        private static CallToolResult ReadDupesManagementMarkdown(JObject args, string path) => throw new NotSupportedException();
        private static CallToolResult ReadFoodManagementMarkdown(JObject args, string path) => throw new NotSupportedException();
        private static CallToolResult ReadSkillsManagementMarkdown(JObject args, string path) => throw new NotSupportedException();
        private static CallToolResult ReadResearchManagementMarkdown(JObject args, string path) => throw new NotSupportedException();
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
