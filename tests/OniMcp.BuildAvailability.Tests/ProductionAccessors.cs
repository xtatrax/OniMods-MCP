using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    internal static class ToolUtil
    {
        internal static int ResolveWorldId(JObject args) { return GetInt(args, "worldId") ?? 0; }
        internal static int? GetInt(JObject args, string key) { return args[key]?.Type == JTokenType.Integer ? (int?)args[key] : null; }
        internal static bool GetBool(JObject args, string key, bool fallback) { return args[key]?.Type == JTokenType.Boolean ? (bool)args[key] : fallback; }
        internal static string CleanName(string text) { return text; }
        internal static float SafeFloat(float value) { throw Boundary.Forbidden("material SafeFloat"); }
    }
    internal static class BuildingControlTools
    {
        internal static bool IsVirtualFileEditContext { get { Boundary.UiCalls++; throw Boundary.Forbidden("virtual edit context"); } }
    }
    public static partial class BuildPlanningTools
    {
        // Thin test entry points invoke full production private callers; no selection logic is copied.
        internal static JObject HostPlan(JObject args) { return HostJson(ResolveBuildPlan(args).ToDictionary()); }
        internal static JArray HostCandidates(string prefab, string text, bool includeUnavailable)
        { return (JArray)JToken.Parse(JsonConvert.SerializeObject(ResolveBuildingCandidates(prefab, text, SplitPlanTerms(text).ToList(), includeUnavailable).Select(x => x.ToDictionary()).ToList(), McpJsonUtil.Settings)); }
        internal static JObject HostCatalog(BuildingDef def) { return HostJson(BuildingDefToDictionary(def)); }
        internal static JObject HostJson(object value) { return JObject.Parse(JsonConvert.SerializeObject(value, McpJsonUtil.Settings)); }

        // Material, placement and utility-write collaborators are outside this bounded read contract.
        // Ordinary empty material inputs are permitted; any UI or write collaboration is a canary.
        private static IEnumerable<BuildMaterialInfo> AvailableMaterials(BuildingDef def, int worldId, bool includeUnavailable)
        { Boundary.MaterialCalls++; Boundary.MaterialRead?.Invoke(); return new BuildMaterialInfo[0]; }
        private static List<Tag> DefaultBuildElements(BuildingDef def) { return new List<Tag>(); }
        private static IEnumerable<Tag> MaterialCategoryTags(BuildingDef def) { return new Tag[0]; }
        private static object BuildDefPlacementToDictionary(BuildingDef def) { return new Dictionary<string, object>(); }
        private static bool IsExactConnectionUtilityPrefab(string id) { Boundary.UiCalls++; throw Boundary.Forbidden("utility write guard"); }
        private static bool Contains(string value, string query) { return value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0; }
        private sealed class BuildMaterialInfo
        {
            internal Tag Tag = new Tag(null);
            internal string Name = null;
            internal List<Tag> Categories = null;
            internal float AvailableKg = 0;
            internal bool ValidForBuilding = false;
            internal Dictionary<string, object> ToDictionary() { throw Boundary.Forbidden("nonempty material result"); }
        }
    }
}
