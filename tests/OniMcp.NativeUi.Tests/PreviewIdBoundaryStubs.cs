using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Metadata, query/area lookup and passive layer presentation are explicit
// boundaries. The actual preview handler and both anchor resolvers are sliced;
// the existing harness still links the actual TryPlanOne and native adapters.

internal static partial class Grid { public const int InvalidCell = -1; }
namespace OniMcp.Tools
{
    public sealed class McpToolParameter { public string Type, Description; public bool Required; }
    public sealed class McpTool
    {
        public string Name, Group, Mode, Risk, Description;
        public bool Hidden;
        public List<string> Aliases, Tags;
        public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, OniMcp.Core.CallToolResult> Handler;
    }
    internal sealed class PreviewFixtureArea { internal int X,Y; internal string Id {get{return "fixture-area";}} internal int X1 {get{return X;}} internal int Y1 {get{return Y;}} }
    internal static class WorldEditor
    {
        internal sealed class AbsoluteCell { internal int x, y; }
        internal static PreviewFixtureArea Area;
        internal static int ResolveCalls;
        internal static PreviewFixtureArea ResolveRelativeArea(JObject args) { ResolveCalls++; return Area; }
        internal static AbsoluteCell ToAbsoluteCell(int x, int y, PreviewFixtureArea area)
        { return new AbsoluteCell { x = x + area.X, y = y + area.Y }; }
    }
    internal static partial class ToolUtil
    {
        internal static int PreviewQueryCalls, PreviewQueryX, PreviewQueryY;
        internal static bool PreviewQuerySuccess;
        internal static JObject PreviewQueryArgs;
        internal static string PreviewQueryError;
        internal static bool TryResolveSearchCell(JObject args, out int x, out int y, out string error)
        {
            PreviewQueryCalls++; PreviewQueryArgs = args;
            x = PreviewQueryX; y = PreviewQueryY; error = PreviewQuerySuccess ? null : PreviewQueryError;
            return PreviewQuerySuccess;
        }
    }
    public static partial class BuildPlanningTools
    {
        private sealed class CellCoord
        {
            internal int x, y;
            internal CellCoord(int first, int second) { x = first; y = second; }
        }
        private static Dictionary<string, McpToolParameter> BuildPlacementParameters(bool includeConfirm, bool includeArea)
        { return new Dictionary<string, McpToolParameter>(); }
        private static string ValidateLayerContextOptions(JObject args) { return null; }
        private static Dictionary<string, object> PlanOneWithoutLayerContext(string prefab, int x, int y,
            JObject args, HashSet<int> supports = null, AutoDigContext dig = null, PlacementPreviewReadContext previewReads = null)
        { return TryPlanOne(prefab, x, y, args, supports, dig, previewReads); }
        private static Dictionary<string, object> CaptureLayerContext(BuildingDef def, JObject args,
            IEnumerable<CellCoord> anchors = null, IEnumerable<CellCoord> path = null,
            IEnumerable<Dictionary<string, object>> previews = null)
        { return null; }
        private static Dictionary<string, object> AttachLayerContext(Dictionary<string, object> result,
            Dictionary<string, object> context)
        { return result; }
    }
}
