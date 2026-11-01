using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    internal static class MapNativeBoundary
    {
        internal static Func<JObject,CallToolResult> GameHandler;
        internal static string Snapshot = null;
        internal static bool AllowPlacementPreview { get => ModeAllowPreview; set => ModeAllowPreview=value; }
        internal static Dictionary<string,object> LayerObservation=new Dictionary<string,object>{{"status","disabled"}};
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
        internal static readonly List<JObject> PreflightCalls = new List<JObject>();
        internal static Func<JObject, BuildPlanningRequestSupportContext, CallToolResult> PreflightHandler;
        internal static readonly List<BuildPlanningRequestSupportContext> FactoryContexts = new List<BuildPlanningRequestSupportContext>();
        internal static readonly List<JObject> BuildingCalls = new List<JObject>();
        internal static bool ModeAllowPreview;
        internal static Action<JObject> ModeObserver;
        internal static CallToolResult Building(JObject args)
        {
            BuildingCalls.Add((JObject)args.DeepClone());
            if (ModeObserver != null) ModeObserver(args);
            if (ToolUtil.GetBool(args, "dryRun", false) && !ModeAllowPreview) throw new NotSupportedException("Unexpected native placement preview.");
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
    internal static class GameControlEntryTools { internal static McpTool ControlGame() => MapNativeBoundary.GameHandler == null ? throw new NotSupportedException() : new McpTool { Handler = MapNativeBoundary.GameHandler }; }
    internal static class NavigationControlTools { internal static McpTool ControlNavigation() => throw new NotSupportedException(); }
    internal static class BuildingControlTools
    {
        internal static CallToolResult ControlBuildingFromVirtualFile(JObject args, BuildPlanningRequestSupportContext support = null)
        {
            MapNativeBoundary.FactoryContexts.Add(support);
            if (support == null) return MapNativeBoundary.Building(args);
            MapNativeBoundary.PreflightCalls.Add((JObject)args.DeepClone());
            if (MapNativeBoundary.ModeObserver != null) MapNativeBoundary.ModeObserver(args);
            return MapNativeBoundary.PreflightHandler == null
                ? CallToolResult.Text("{\"valid\":true,\"actionable\":true,\"committed\":false}")
                : MapNativeBoundary.PreflightHandler(args, support);
        }
        internal static McpTool ControlBuilding() => new McpTool { Handler = MapNativeBoundary.Building };
    }
    internal static class OrdersControlEntryTools
    {
        internal static McpTool ControlOrders() => new McpTool { Handler = MapNativeBoundary.Order };
    }
    public static partial class BuildPlanningTools
    {
        private static bool IsSupportPrefab(string id) => id == "Tile" || id == "AirflowTile";
        internal static string ValidateLayerContextOptions(JObject args) => null;
        internal static bool LayerContextRequested(JObject args) => false;
        internal static Dictionary<string,object> CaptureCompiledMapLayerContext(string prefabId, JObject args, JArray anchors = null, IEnumerable<int[]> path = null)
            => MapNativeBoundary.LayerObservation;
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
        internal const int InvalidCell = -1;
        internal static readonly int[] WorldIdx = new int[10000];
        internal static bool IsWorldValidCell(int c) => IsValidCell(c);
        internal static bool IsVisible(int c) => IsValidCell(c);
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
    internal static partial class Components
    {
        internal static class LiveMinionIdentities { internal static List<MinionIdentity> Items = new List<MinionIdentity>(); }
    }
    internal sealed class FixtureDupe { internal object gameObject => throw new NotSupportedException(); internal T GetComponent<T>() => throw new NotSupportedException(); }
    internal enum ObjectLayer { Building, FoundationTile, NumLayers }
    internal enum Orientation { Neutral, R90, R180, R270, FlipH, FlipV }
    internal enum SimHashes { CopperOre }
    internal enum BuildLocationRule { Anywhere, Tile }
    internal struct CellOffset { internal int x,y; internal CellOffset(int x,int y) { this.x=x;this.y=y; } }
    internal sealed class BuildingDef
    {
        internal bool IsFoundation;
        internal int WidthInCells=1, HeightInCells=1;
        internal ObjectLayer TileLayer=ObjectLayer.NumLayers;
        internal BuildLocationRule BuildLocationRule=BuildLocationRule.Anywhere;
        internal CellOffset[] PlacementOffsets=new[] { new CellOffset(0,0) };
        internal string PrefabID = null;
        internal string Name = null;
        internal ObjectLayer ObjectLayer = ObjectLayer.Building;
        internal NativeShape Shape = null;
    }
    internal static class Assets
    {
        internal static bool MissingDefinitionLookup;
        internal static List<BuildingDef> BuildingDefs = new List<BuildingDef>();
        internal static BuildingDef GetBuildingDef(string id) => MissingDefinitionLookup ? null : BuildingDefs.FirstOrDefault(d => d.PrefabID == id);
    }
    internal sealed class NativePoint { internal int X = 0; internal int Y = 0; internal int? Cell; }
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
        internal static NativeShape Definition(BuildingDef def, Orientation orientation) => def.Shape;
        internal static NativeShape FromLowerLeft(BuildingDef def,int x,int y,Orientation orientation)
            => new NativeShape { Cells=new List<NativePoint> {new NativePoint {X=x,Y=y,Cell=Grid.XYToCell(x,y)}} };
    }
    public static partial class WorldEditorTools
    {
        internal static CallToolResult BatchForCountTest(JObject args) => Batch(args);
        private static CallToolResult Zoom(JObject args) => throw new NotSupportedException();
        private static CallToolResult Search(JObject args) => throw new NotSupportedException();
        private static CallToolResult Grep(JObject args) => throw new NotSupportedException();
        private static CallToolResult SymbolMap(JObject args) => throw new NotSupportedException();
        private static CallToolResult Screenshot(JObject args) => throw new NotSupportedException();
        internal static CallToolResult EditForCountTest(JObject args) => Edit(args);
        internal static CallToolResult ApplyMapForCountTest(JObject args, string search, string replacement) => ApplyMapEdit(args, search, replacement);
        private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly Dictionary<string,char> UniqueCharMap = new Dictionary<string,char>();
        private static bool IsInfrastructureMapMarkdown(string relative) => false;
        private static bool IsReadableMapCell(int cell) => Grid.IsValidCell(cell);
        private static char GetUniqueChar(string id, string name) => id[0];
        private static string MapTokenPart(string token) => token;
        private static bool IsConnectionGlyph(char glyph) => "─│┼一|十●┌┐└┘┬┴├┤←→↑↓".IndexOf(glyph) >= 0;
        private static CallToolResult Read(JObject args)
        {
            DirectoryListingBoundary.FileReads++;
            return CallToolResult.Text(MapNativeBoundary.Snapshot);
        }
        private static bool IsDupeDetailMarkdown(string relative) => false;
        private static bool IsBuildingDetailMarkdown(string relative) => false;
        private static CallToolResult PreflightDupeDetailEdit(string relative, string replacement) => throw new NotSupportedException();
        private static CallToolResult PreflightBuildingDetailEdit(string relative, string search, string replacement) => throw new NotSupportedException();
        private static CallToolResult ApplyDupeDetailEdit(JObject args, string relative, string replacement) => throw new NotSupportedException();
        private static CallToolResult ApplyBuildingDetailEdit(JObject args, string relative, string search, string replacement) => throw new NotSupportedException();
    }
}
