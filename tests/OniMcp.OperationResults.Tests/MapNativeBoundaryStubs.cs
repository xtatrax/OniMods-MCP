using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    internal static class MapNativeBoundary
    {
        internal static string Snapshot = null;
        internal static Func<JObject, int> WorldResolver;
        internal static readonly List<JObject> WorldResolutionCalls = new List<JObject>();
        internal static readonly Queue<CallToolResult> BuildingResults = new Queue<CallToolResult>();
        internal static readonly Queue<CallToolResult> OrderResults = new Queue<CallToolResult>();
        internal static readonly List<JObject> OrderCalls = new List<JObject>();
        internal static CallToolResult Order(JObject args)
        {
            OrderCalls.Add((JObject)args.DeepClone());
            if (OrderResults.Count == 0) throw new NotSupportedException();
            return OrderResults.Dequeue();
        }
        internal static readonly List<JObject> BuildingCalls = new List<JObject>();
        internal static CallToolResult Building(JObject args)
        {
            BuildingCalls.Add((JObject)args.DeepClone());
            if (ToolUtil.GetBool(args, "dryRun", false)) throw new NotSupportedException("Unexpected native placement preview.");
            if (BuildingResults.Count == 0) throw new NotSupportedException("No native building result fixture.");
            return BuildingResults.Dequeue();
        }
    }
    public static partial class ToolUtil
    {
        // World ownership is a game boundary. Tests supply its resolved value
        // and observe whether source area selectors still exist at this call.
        public static int ResolveWorldId(JObject args)
        {
            MapNativeBoundary.WorldResolutionCalls.Add((JObject)args.DeepClone());
            return MapNativeBoundary.WorldResolver == null
                ? GetInt(args, "worldId") ?? 0 : MapNativeBoundary.WorldResolver(args);
        }
    }
    internal static class GameControlEntryTools { internal static McpTool ControlGame() => throw new NotSupportedException(); }
    internal static class NavigationControlTools { internal static McpTool ControlNavigation() => throw new NotSupportedException(); }
    internal static class BuildingControlTools
    {
        internal static CallToolResult ControlBuildingFromVirtualFile(JObject args) => MapNativeBoundary.Building(args);
        internal static McpTool ControlBuilding() => new McpTool { Handler = MapNativeBoundary.Building };
    }
    internal static class OrdersControlEntryTools
    {
        internal static McpTool ControlOrders() => new McpTool { Handler = MapNativeBoundary.Order };
    }
    internal static class BuildPlanningTools
    {
        internal static string ValidateLayerContextOptions(JObject args) => null;
        internal static bool LayerContextRequested(JObject args) => false;
        internal static Dictionary<string,object> CaptureCompiledMapLayerContext(string prefabId, JObject args, JArray anchors = null, IEnumerable<int[]> path = null)
            => new Dictionary<string, object> { ["status"] = "disabled" };
    }
    internal sealed class KPrefabID
    {
        internal int InstanceID => throw new NotSupportedException();
        internal FixtureTag PrefabTag => throw new NotSupportedException();
    }
    internal sealed class FixtureTag { internal string Name => throw new NotSupportedException(); }
    internal sealed class FixtureGameObject { internal T GetComponent<T>() => throw new NotSupportedException(); }
    internal static class Grid
    {
        internal const int WidthInCells = 100;
        internal const int HeightInCells = 100;
        internal static int PosToCell(object position) => throw new NotSupportedException();
        internal static bool IsValidCell(int cell) => cell >= 0 && cell < WidthInCells * HeightInCells;
        internal static int CellColumn(int cell) => cell % WidthInCells;
        internal static int CellRow(int cell) => cell / WidthInCells;
        internal static int XYToCell(int x, int y) => x + y * WidthInCells;
        internal static readonly bool[] Solid = new bool[WidthInCells * HeightInCells];
        internal static readonly FixtureGameObject[,] Objects = new FixtureGameObject[WidthInCells * HeightInCells, (int)ObjectLayer.NumLayers];
    }
    internal static class Components
    {
        internal static class LiveMinionIdentities { internal static List<FixtureDupe> Items = new List<FixtureDupe>(); }
    }
    internal sealed class FixtureDupe { internal object gameObject => throw new NotSupportedException(); internal T GetComponent<T>() => throw new NotSupportedException(); }
    internal enum ObjectLayer { Building, NumLayers }
    internal enum Orientation { Neutral, R90, R180, R270, FlipH, FlipV }
    internal enum SimHashes { CopperOre }
    internal sealed class FixtureBuildingDef
    {
        internal string PrefabID = null;
        internal string Name = null;
        internal ObjectLayer ObjectLayer = ObjectLayer.Building;
        internal NativeShape Shape = null;
    }
    internal static class Assets
    {
        internal static List<FixtureBuildingDef> BuildingDefs = new List<FixtureBuildingDef>();
        internal static FixtureBuildingDef GetBuildingDef(string id) => BuildingDefs.FirstOrDefault(d => d.PrefabID == id);
    }
    internal sealed class NativePoint { internal int X = 0; internal int Y = 0; }
    internal sealed class NativeShape
    {
        internal bool Available = true;
        internal string Error = null;
        internal int[] Size = null;
        internal int[] Bounds = null;
        internal List<NativePoint> Cells = null;
    }
    internal static class NativeBuildingFootprint
    {
        internal static NativeShape Definition(FixtureBuildingDef def, Orientation orientation) => def.Shape;
    }
    public static partial class WorldEditorTools
    {
        internal static CallToolResult BatchForCountTest(JObject args) => Batch(args);
        private static CallToolResult HandleWorldEditorScoped(JObject args) => HandleWorldEditorCommand(args);
        private static CallToolResult Cd(JObject args) => throw new NotSupportedException();
        private static CallToolResult Ls(JObject args) => throw new NotSupportedException();
        private static CallToolResult Zoom(JObject args) => throw new NotSupportedException();
        private static CallToolResult Search(JObject args) => throw new NotSupportedException();
        private static CallToolResult Grep(JObject args) => throw new NotSupportedException();
        private static CallToolResult SymbolMap(JObject args) => throw new NotSupportedException();
        private static CallToolResult ForwardSandbox(JObject args) => throw new NotSupportedException();
        private static CallToolResult Screenshot(JObject args) => throw new NotSupportedException();
        private static CallToolResult BlueprintCommand(JObject args) => throw new NotSupportedException();
        internal static CallToolResult EditForCountTest(JObject args) => Edit(args);
        internal static CallToolResult ApplyMapForCountTest(JObject args, string search, string replacement) => ApplyMapEdit(args, search, replacement);
        private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly Dictionary<string,char> UniqueCharMap = new Dictionary<string,char>();
        private static string SaveRelativePath(string path) => path.StartsWith("/active/", StringComparison.Ordinal) ? path.Substring(8) : path;
        private static bool IsInfrastructureMapMarkdown(string relative) => false;
        private static bool IsReadableMapCell(int cell) => Grid.IsValidCell(cell);
        private static char GetUniqueChar(string id, string name) => id[0];
        private static string MapTokenPart(string token) => token;
        private static bool IsConnectionGlyph(char glyph) => "─│┼一|十●┌┐└┘┬┴├┤←→↑↓".IndexOf(glyph) >= 0;
        private static CallToolResult Read(JObject args) => CallToolResult.Text(MapNativeBoundary.Snapshot);
        private static bool IsBlueprintMarkdown(string relative) => false;
        private static bool IsDupeDetailMarkdown(string relative) => false;
        private static bool IsBuildingDetailMarkdown(string relative) => false;
        private static CallToolResult PreflightBlueprintMarkdownEdit(string relative, string search, string replacement) => throw new NotSupportedException();
        private static CallToolResult PreflightDupeDetailEdit(string relative, string replacement) => throw new NotSupportedException();
        private static CallToolResult PreflightBuildingDetailEdit(string relative, string search, string replacement) => throw new NotSupportedException();
        private static CallToolResult ApplyBlueprintMarkdownEdit(JObject args, string relative, string search, string replacement) => throw new NotSupportedException();
        private static CallToolResult ApplyDupeDetailEdit(JObject args, string relative, string replacement) => throw new NotSupportedException();
        private static CallToolResult ApplyBuildingDetailEdit(JObject args, string relative, string search, string replacement) => throw new NotSupportedException();
    }
}
