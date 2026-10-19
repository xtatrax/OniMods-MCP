using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using OniMcp.Support;
using UnityEngine;

internal static class NativeConnectRegression
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static JObject Request(string type = "liquid", string direction = "right") => new JObject
        { ["x"] = 2, ["y"] = 2, ["type"] = type, ["direction"] = direction, ["confirm"] = true };
    private static JObject Data(OniMcp.Core.CallToolResult result) => JObject.Parse(result.TextValue);
    private static void Reject(ConnectFixture f, JObject request, string key)
    {
        var result = BuildPlanningTools.ConnectForTest(request);
        Check(result.IsError && result.TextValue.Contains(McpLocalization.Get("connect_existing." + key)), key);
        Check(f.Tool.Events.Count == 0, "Preflight rejection must not activate UI");
        Check(f.Manager.Bits[f.A.Cell] == f.BeforeA && f.Manager.Bits[f.B.Cell] == f.BeforeB, "Preflight is read only");
    }
    internal static void Run()
    {
        McpLocalization.LanguageProvider = () => "en";
        foreach (var type in new[] { "liquid", "gas", "solid", "wire", "logic" })
        foreach (var direction in new[] { "right", "left", "up", "down" })
        {
            var f = new ConnectFixture(type, direction);
            f.Tool.OnRelease = () => { f.Manager.Bits[f.A.Cell] = f.BeforeA | f.AddA; f.Manager.Bits[f.B.Cell] = f.BeforeB | f.AddB; };
            var result = BuildPlanningTools.ConnectForTest(Request(type, direction)); var data = Data(result);
            Check(!result.IsError && (bool)data["connectionVerified"], type + "/" + direction + " native result");
            Check(f.Tool.Events.SequenceEqual(new[] { "activate", "down", "drag", "up", "deactivate" }), "Dispatch the native gesture in order and leave the build tool");
            Check(f.Tool.First == f.A.Cell && f.Tool.Second == f.B.Cell && f.Tool.Material == f.Material, "Exact endpoints and original material");
            Check((bool)data["actionAttempted"] && (int)data["connectedEdges"] == 1 && !(bool)data["flowVerified"], "Acceptance, edge verification and flow remain separate");
            Check(f.Unrelated.GetComponent<KPrefabID>().InstanceID == 999 && f.OtherManager.Bits[f.A.Cell] == UtilityConnections.Down, "Co-located other layer remains unchanged");
            Check(!f.Tool.Dragging && PlayerController.Instance.ActiveTool == SelectTool.Instance, "No lingering drag or armed construction");
            int count = f.Tool.Events.Count;
            data = Data(BuildPlanningTools.ConnectForTest(Request(type, direction)));
            Check((bool)data["alreadyConnected"] && !(bool)data["actionAttempted"] && (int)data["connectedEdges"] == 0 && f.Tool.Events.Count == count, "Repeated connection is a native no-op");
        }
        var fixture = new ConnectFixture(); var request = Request(); request["dryRun"] = true; request.Remove("confirm");
        var preview = Data(BuildPlanningTools.ConnectForTest(request));
        Check((bool)preview["success"] && preview["connectionVerified"].Type == JTokenType.Null && !(bool)preview["actionAttempted"], "Preview does not claim physical completion");
        Check(fixture.Tool.Events.Count == 0, "Preview must not select or activate native UI");
        fixture = new ConnectFixture(); request = Request(); request.Remove("confirm"); Reject(fixture, request, "confirm_required");
        foreach (var key in new[] { "x1", "y1", "x2", "y2", "areaId", "points", "id", "prefabId", "material", "steps", "fromX", "toY", "fromQuery" })
        {
            fixture = new ConnectFixture(); request = Request(); request[key] = 1; Reject(fixture, request, "single_edge_required");
        }
        fixture = new ConnectFixture(); request = Request(); request["type"] = "all"; Reject(fixture, request, "type_required");
        fixture = new ConnectFixture(); request = Request(); request["direction"] = "diagonal"; Reject(fixture, request, "direction_required");
        fixture = new ConnectFixture(); request = Request(); request["x"] = 7; Reject(fixture, request, "scope_invalid");
        fixture = new ConnectFixture(); Grid.Worlds[fixture.B.Cell] = 1; Reject(fixture, Request(), "scope_invalid");
        fixture = new ConnectFixture(); Grid.Visible[fixture.B.Cell] = false; Reject(fixture, Request(), "scope_invalid");
        fixture = new ConnectFixture(); ClusterManager.Instance.activeWorldId = 1; Reject(fixture, Request(), "scope_invalid");
        fixture = new ConnectFixture(); SpeedControlScreen.Instance.IsPaused = false; Reject(fixture, Request(), "pause_required");
        fixture = new ConnectFixture(); Grid.Objects[fixture.B.Cell, (int)fixture.Layer] = null; Reject(fixture, Request(), "target_required");
        fixture = new ConnectFixture(); Grid.Objects[fixture.B.Cell, (int)fixture.Def.ReplacementLayer] = new GameObject(); Reject(fixture, Request(), "target_required");
        fixture = new ConnectFixture(); fixture.B.GetComponent<PrimaryElement>().Element.tag = new Tag(2); Reject(fixture, Request(), "same_required");
        fixture = new ConnectFixture(); fixture.B.GetComponent<BuildingComplete>().Def = new BuildingDef { BuildingComplete = fixture.Def.BuildingComplete, ObjectLayer = fixture.Def.ObjectLayer, TileLayer = fixture.Layer }; Reject(fixture, Request(), "same_required");
        fixture = new ConnectFixture(); fixture.B.ConnectionManager = fixture.OtherManager; Reject(fixture, Request(), "target_required");
        fixture = new ConnectFixture(); fixture.B.connectionManager = null; Reject(fixture, Request(), "target_required");
        fixture = new ConnectFixture(); fixture.Def.BuildingComplete = new GameObject(); Reject(fixture, Request(), "target_required");
        fixture = new ConnectFixture(); fixture.Tool.Dragging = true; PlayerController.Instance.ActiveTool = fixture.Tool; Reject(fixture, Request(), "ui_dragging");
        fixture = new ConnectFixture(); UtilityBuildTool.Instance = null; Reject(fixture, Request(), "ui_unavailable");

        fixture = new ConnectFixture(); fixture.Tool.AcceptPath = false;
        var result2 = BuildPlanningTools.ConnectForTest(Request()); var report = Data(result2);
        Check(result2.IsError && !(bool)report["actionAttempted"] && !(bool)report["partial"], "Rejected native path must not be released");
        Check(!fixture.Tool.Events.Contains("up") && !fixture.Tool.Dragging && fixture.Tool.Events.Last() == "deactivate", "Cancel native preview after path refusal");
        fixture = new ConnectFixture(); var captured = fixture;
        fixture.Tool.OnDrag = () => Grid.Objects[captured.B.Cell, (int)captured.Layer] = null;
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["success"] && !(bool)report["actionAttempted"] && !fixture.Tool.Events.Contains("up"), "Recheck targets after activation hooks, before release");
        fixture = new ConnectFixture(); fixture.Tool.OnRelease = () => throw new Exception("native refusal");
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["success"] && (bool)report["partial"] && report["connectedEdges"].Type == JTokenType.Null, "Failed callback reports uncertainty, not completion");
        Check(fixture.Manager.Bits[fixture.A.Cell] == fixture.BeforeA && !fixture.Tool.Dragging, "No mutation fallback after exception; native drag cleared");
        fixture = new ConnectFixture(); captured = fixture;
        fixture.Tool.OnRelease = () => { captured.Manager.Bits[captured.A.Cell] |= captured.AddA; throw new Exception("partial native write"); };
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["success"] && (bool)report["partial"] && !(bool)report["connectionVerified"], "One-sided native failure cannot be reported as connected");
        Check(fixture.Manager.Bits[fixture.B.Cell] == fixture.BeforeB, "No reciprocal auto-repair after partial native operation");
        fixture = new ConnectFixture(); report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["success"] && !(bool)report["connectionVerified"], "Native no-op must fail independent mask verification");
        fixture = new ConnectFixture(); captured = fixture;
        fixture.Tool.OnRelease = () => { captured.Manager.Bits[captured.A.Cell] = captured.AddA; captured.Manager.Bits[captured.B.Cell] = captured.AddB; };
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["success"], "Connecting requested edge while dropping other bits must fail verification");
        fixture = new ConnectFixture(); PlayerController.Instance.ActiveTool = fixture.Tool;
        Reject(fixture, Request(), "ui_dragging");
        fixture = new ConnectFixture(); captured = fixture;
        fixture.Tool.OnRelease = () => captured.Manager.ReadError = true;
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["success"] && (bool)report["partial"] && report["connectionVerified"].Type == JTokenType.Null, "Read failure keeps verification unknown");
        fixture = new ConnectFixture(); captured = fixture;
        fixture.Tool.OnDrag = () => Grid.Visible[captured.B.Cell] = false;
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["actionAttempted"] && !fixture.Tool.Events.Contains("up"), "Visibility drift before release rejects the commit");
        fixture = new ConnectFixture(); captured = fixture;
        fixture.Tool.OnDrag = () => ClusterManager.Instance.activeWorldId = 1;
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["actionAttempted"] && !fixture.Tool.Events.Contains("up"), "World drift before release rejects the commit");
        fixture = new ConnectFixture(); captured = fixture;
        fixture.Tool.OnRelease = () => { captured.Manager.Bits[captured.A.Cell] |= captured.AddA; captured.Manager.Bits[captured.B.Cell] |= captured.AddB; };
        PlayerController.Instance.ThrowAfterDeactivate = true;
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["success"] && (bool)report["connectionVerified"] && (bool)report["partial"], "UI cleanup failure remains an error even if physical edge is verified");
        Check(report["connectedEdges"].Type == JTokenType.Null && !fixture.Tool.Dragging, "Do not report full completion on cleanup failure");
        fixture = new ConnectFixture(); captured = fixture;
        fixture.Tool.OnRelease = () => { captured.Manager.Bits[captured.A.Cell] |= captured.AddA; captured.Manager.Bits[captured.B.Cell] |= captured.AddB; captured.B.GetComponent<KPrefabID>().InstanceID++; };
        report = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)report["connectionVerified"], "Changed endpoint ID must fail verification even when masks match");
        foreach (var lang in new[] { "en", "ja", "zh-Hans" })
            Check(McpLocalization.GetForLanguage(lang, "connect_existing.same_required") != "connect_existing.same_required", "Runtime translation loaded: " + lang);
        Console.WriteLine($"Native connect production-boundary tests passed: {checks} checks");
    }
}

internal sealed class ConnectFixture
{
    internal readonly Manager Manager = new Manager(), OtherManager = new Manager();
    internal readonly KAnimGraphTileVisualizer A, B;
    internal readonly GameObject Unrelated;
    internal readonly BaseUtilityBuildTool Tool;
    internal readonly BuildingDef Def;
    internal readonly ObjectLayer Layer;
    internal readonly Tag Material = new Tag(1);
    internal readonly UtilityConnections BeforeA, BeforeB, AddA, AddB;
    internal ConnectFixture(string type = "liquid", string direction = "right")
    {
        Grid.Objects = new GameObject[64, (int)ObjectLayer.NumLayers]; Grid.Worlds = new int[64]; Grid.Visible = Enumerable.Repeat(true, 64).ToArray();
        ClusterManager.Instance = new ClusterManager(); SpeedControlScreen.Instance = new SpeedControlScreen { IsPaused = true };
        PlayerController.Instance = new PlayerController(); SelectTool.Instance = new SelectTool();
        UtilityBuildTool.Instance = new UtilityBuildTool(); WireBuildTool.Instance = new WireBuildTool();
        Layer = type switch { "gas" => ObjectLayer.GasConduitTile, "solid" => ObjectLayer.SolidConduitTile, "wire" => ObjectLayer.WireTile, "logic" => ObjectLayer.LogicWireTile, _ => ObjectLayer.LiquidConduitTile };
        Tool = type == "wire" ? WireBuildTool.Instance : UtilityBuildTool.Instance;
        var prefab = new GameObject(); prefab.Add(new UtilityItem(Manager));
        Def = new BuildingDef { BuildingComplete = prefab, ObjectLayer = (ObjectLayer)((int)Layer - 5), TileLayer = Layer, ReplacementLayer = (ObjectLayer)((int)Layer + 5), PrefabID = type };
        int dx = direction == "right" ? 1 : direction == "left" ? -1 : 0;
        int dy = direction == "up" ? 1 : direction == "down" ? -1 : 0;
        AddA = dx == 1 ? UtilityConnections.Right : dx == -1 ? UtilityConnections.Left : dy == 1 ? UtilityConnections.Up : UtilityConnections.Down;
        AddB = dx == 1 ? UtilityConnections.Left : dx == -1 ? UtilityConnections.Right : dy == 1 ? UtilityConnections.Down : UtilityConnections.Up;
        BeforeA = dx == 0 ? UtilityConnections.Left : UtilityConnections.Up;
        BeforeB = dx == 0 ? UtilityConnections.Right : UtilityConnections.Down;
        A = Put(2, 2, BeforeA); B = Put(2 + dx, 2 + dy, BeforeB);
        Unrelated = new GameObject(); Unrelated.Add(new KPrefabID { InstanceID = 999 });
        var otherLayer = Layer == ObjectLayer.WireTile ? ObjectLayer.GasConduit : ObjectLayer.Wire;
        Grid.Objects[A.Cell, (int)otherLayer] = Unrelated; OtherManager.Bits[A.Cell] = UtilityConnections.Down;
    }
    private KAnimGraphTileVisualizer Put(int x, int y, UtilityConnections mask)
    {
        int cell = Grid.XYToCell(x, y); var target = new GameObject();
        var graph = new KAnimGraphTileVisualizer { Cell = cell, ConnectionManager = Manager, connectionManager = Manager };
        target.Add(graph); target.Add(new BuildingComplete { Def = Def }); target.Add(new PrimaryElement { Element = new Element { tag = Material } });
        target.Add(new KPrefabID { InstanceID = cell }); Grid.Objects[cell, (int)Layer] = target; Grid.Objects[cell, (int)Def.ObjectLayer] = target; Manager.Bits[cell] = mask; return graph;
    }
}
namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        public static OniMcp.Core.CallToolResult ConnectForTest(JObject args) => ConnectExistingUtility(args);
        private static bool IsLinearUtilityPrefab(string id) => new[] { "liquid", "gas", "solid", "wire", "logic" }.Contains(id);
    }
}
namespace UnityEngine { public struct Vector3 { public int Cell; } }
public struct Tag
{
    private int value;
    public Tag(int value) { this.value = value; }
    public static bool operator ==(Tag a, Tag b) => a.value == b.value;
    public static bool operator !=(Tag a, Tag b) => !(a == b);
    public override bool Equals(object o) => o is Tag other && this == other;
    public override int GetHashCode() => value;
}
public sealed class Element { public Tag tag; }
public sealed class PrimaryElement : Component { public Element Element; }
public sealed class ClusterManager { public static ClusterManager Instance; public int activeWorldId; }
public sealed class SpeedControlScreen { public static SpeedControlScreen Instance; public bool IsPaused; }
public class InterfaceTool { }
public sealed class SelectTool : InterfaceTool { public static SelectTool Instance; }
public sealed class PlayerController
{
    public static PlayerController Instance;
    public InterfaceTool ActiveTool;
    public bool ThrowAfterDeactivate;
    public bool IsDragging() => ActiveTool is BaseUtilityBuildTool tool && tool.Dragging;
    public void ActivateTool(InterfaceTool next) { if (ActiveTool is BaseUtilityBuildTool old) old.Deactivate(); ActiveTool = next; if (ThrowAfterDeactivate) throw new Exception("cleanup failed"); }
}
public class BaseUtilityBuildTool : InterfaceTool
{
    protected struct PathNode { public int cell; public bool valid; }
    protected List<PathNode> path = new List<PathNode>();
    public readonly List<string> Events = new List<string>();
    public bool Dragging, AcceptPath = true;
    public int First, Second; public Tag Material;
    public System.Action OnRelease, OnDrag;
    public void Activate(BuildingDef def, IList<Tag> materials) { Events.Add("activate"); Material = materials[0]; PlayerController.Instance.ActiveTool = this; }
    public void OnLeftClickDown(Vector3 pos) { Events.Add("down"); First = pos.Cell; Dragging = true; path.Clear(); path.Add(new PathNode { cell = First, valid = AcceptPath }); }
    protected void OnDragTool(int cell, int distance) { Events.Add("drag"); Second = cell; path.Add(new PathNode { cell = cell, valid = AcceptPath }); OnDrag?.Invoke(); }
    public void OnLeftClickUp(Vector3 pos) { Events.Add("up"); OnRelease?.Invoke(); Dragging = false; path.Clear(); }
    public void Deactivate() { Events.Add("deactivate"); Dragging = false; path.Clear(); }
}
public sealed class UtilityBuildTool : BaseUtilityBuildTool { public static UtilityBuildTool Instance; }
public sealed class WireBuildTool : BaseUtilityBuildTool { public static WireBuildTool Instance; }
