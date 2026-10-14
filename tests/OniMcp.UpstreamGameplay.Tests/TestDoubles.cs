using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
namespace OniMcp.Tools
{
    public sealed class McpTool
    {
        public string Name { get; set; }
        public string Group { get; set; }
        public string Mode { get; set; }
        public string Risk { get; set; }
        public string Description { get; set; }
        public bool Hidden { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, McpToolParameter> Parameters { get; set; }
        public Func<JObject, CallToolResult> Handler { get; set; }
    }

    public sealed class McpToolParameter
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public List<string> EnumValues { get; set; }
    }

    public static partial class ToolUtil
    {
        public static bool GetBool(JObject args, string name, bool fallback)
        {
            bool value;
            return args[name] != null && bool.TryParse(args[name].ToString(), out value) ? value : fallback;
        }
        public static int? GetInt(JObject args, string name) => args[name]?.Value<int?>();
    }
}
internal enum SimHashes { TestElement }
internal static partial class Components
{
    internal static class LiveMinionIdentities
    {
        internal static readonly List<MinionIdentity> Items = new List<MinionIdentity>();
    }
}
internal sealed class MinionIdentity { internal UnityEngine.GameObject gameObject; }
