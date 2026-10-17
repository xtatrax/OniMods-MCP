using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static JObject Result(CallToolResult result) => JObject.Parse(result.TextValue);
    private static JObject Request() => new JObject { ["x"] = 2, ["y"] = 2,
        ["direction"] = "right", ["type"] = "wire", ["confirm"] = true, ["worldId"] = 0 };
    private static void Rejected(Fixture f, JObject request, string expected)
    {
        var before = f.Manager.Bits.ToDictionary(p => p.Key, p => p.Value);
        var result = OrdersTools.Disconnect(request);
        Check(result.IsError && result.TextValue.Contains(expected, StringComparison.OrdinalIgnoreCase), expected + ": " + result.TextValue);
        Check(f.Tool.Calls.Count == 0, "Rejected requests must not invoke the native action");
        Check(before.All(p => f.Manager.Bits[p.Key] == p.Value), "Rejected requests must not change connection state");
    }
    public static void Main()
    {
        // The native callback is a substituted boundary. Tests supply observed
        // outcomes rather than reimplementing the game's connection algorithm.
        var f = new Fixture();
        f.Tool.OnDisconnect = call => f.Manager.Bits[call.Cell] = f.Expected[call.Cell];
        var result = OrdersTools.Disconnect(Request()); var data = Result(result);
        Check(!result.IsError && (bool)data["success"], "Native dispatch must produce verified success");
        Check(f.Tool.Calls.Count == 2 && (int)data["nativeCalls"] == 2, "Invoke each selected native object exactly once");
        Check(f.Tool.Calls[0].Cell == f.A.Cell && ReferenceEquals(f.Tool.Calls[0].Target, f.A.gameObject)
            && f.Tool.Calls[0].Remove == UtilityConnections.Right, "Forward the exact first edge endpoint");
        Check(f.Tool.Calls[1].Cell == f.B.Cell && ReferenceEquals(f.Tool.Calls[1].Target, f.B.gameObject)
            && f.Tool.Calls[1].Remove == UtilityConnections.Left, "Forward the reciprocal endpoint");
        Check(f.Tool.Calls.All(call => ReferenceEquals(call.Utility.GetNetworkManager(), f.Manager)), "Forward the native utility component");
        Check(f.Manager.Bits[f.Unrelated.Cell] == UtilityConnections.Left, "Unrequested adjacent objects remain outside invocation scope");
        Check((string)data["executionPath"] == NativeUtilityDisconnect.ExecutionPath
            && (int)data["disconnectedEdges"] == 1 && (bool)data["connectionVerified"], "Report native execution with verified edge count");

        f = new Fixture(); var dry = Request(); dry["dryRun"] = true; dry.Remove("confirm");
        data = Result(OrdersTools.Disconnect(dry));
        Check((bool)data["success"] && (int)data["wouldDisconnectEdges"] == 1, "Dry-run keeps exact edge preview");
        Check(f.Tool.Calls.Count == 0 && (int)data["nativeCalls"] == 0
            && data["connectionVerified"].Type == JTokenType.Null, "Dry-run must not invoke native callbacks or claim verification");
        Check(f.Manager.Bits[f.A.Cell] == (UtilityConnections.Up | UtilityConnections.Right), "Dry-run state stays unchanged");

        f = new Fixture(); var rejected = Request(); rejected.Remove("confirm"); Rejected(f, rejected, "confirm=true");
        f = new Fixture(); rejected = Request(); rejected["direction"] = "diagonal"; Rejected(f, rejected, "direction must");
        f = new Fixture(); rejected = Request(); rejected.Remove("direction"); Rejected(f, rejected, "requires direction");
        f = new Fixture(); rejected = Request(); rejected["type"] = "building"; Rejected(f, rejected, "Invalid utility type");
        f = new Fixture(); rejected = Request(); rejected["x"] = 7; Rejected(f, rejected, "outside grid");
        f = new Fixture(); Grid.Worlds[f.B.Cell] = 1; Rejected(f, Request(), "world boundary");
        f = new Fixture(); Grid.Visible[f.B.Cell] = false; Rejected(f, Request(), "not visible");
        f = new Fixture(); Grid.Visible[f.A.Cell] = false; dry = Request(); dry["dryRun"] = true;
        Rejected(f, dry, "not visible");
        f = new Fixture(); rejected = new JObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 200, ["y2"] = 0,
            ["confirm"] = true, ["type"] = "wire" }; Rejected(f, rejected, "1..200");

        f = new Fixture(); DisconnectTool.Instance = null; Rejected(f, Request(), "not initialized");
        f = new Fixture(); DisconnectTool.Instance = new WrongSignature(); Rejected(f, Request(), "unavailable");
        f = new Fixture(); DisconnectTool.Instance = new WrongReturn(); Rejected(f, Request(), "unavailable");
        f = new Fixture(); f.B.gameObject.GetComponent<Building>().Def.BuildingComplete = new GameObject();
        Rejected(f, Request(), "target is unavailable");
        f = new Fixture(); f.B.connectionManager = null; Rejected(f, Request(), "target is unavailable");
        f = new Fixture(); f.B.Cell = Grid.XYToCell(4, 2); Rejected(f, Request(), "moved outside");

        f = new Fixture(); f.Tool.OnDisconnect = _ => throw new InvalidOperationException("game refused the operation");
        result = OrdersTools.Disconnect(Request()); data = Result(result);
        Check(result.IsError && (string)data["error"] == "Native disconnect UI action failed: game refused the operation",
            "Native exception cause must remain visible");
        Check((bool)data["partial"] && (int)data["nativeCalls"] == 1 && data["disconnectedEdges"].Type == JTokenType.Null,
            "An interrupted callback has an uncertain partial result, not a completed count");
        Check(f.Manager.Bits[f.A.Cell] == (UtilityConnections.Right | UtilityConnections.Up), "No direct mutation fallback after native rejection");

        f = new Fixture(); f.Tool.OnDisconnect = call => { f.Manager.Bits[call.Cell] = f.Expected[call.Cell]; throw new Exception("after native write"); };
        result = OrdersTools.Disconnect(Request()); data = Result(result);
        Check(result.IsError && (bool)data["partial"] && !(bool)data["connectionVerified"], "Native failure after a write must report partial");
        Check(f.Tool.Calls.Count == 1 && f.Manager.Bits[f.B.Cell] == (UtilityConnections.Left | UtilityConnections.Down),
            "Stop native dispatch at its first error; do not repair other endpoints");
        Check(data["disconnectedEdges"].Type == JTokenType.Null, "Never count all edges as disconnected after partial execution");

        f = new Fixture(); result = OrdersTools.Disconnect(Request()); data = Result(result);
        Check(result.IsError && !(bool)data["connectionVerified"], "A native no-op must fail result verification");
        Check(f.Manager.Bits[f.A.Cell] == (UtilityConnections.Right | UtilityConnections.Up), "Verification must not fix game state");

        f = new Fixture(); f.Tool.OnDisconnect = call => { f.Manager.ReadError = true; throw new Exception("before verification"); };
        result = OrdersTools.Disconnect(Request()); data = Result(result);
        Check(result.IsError && (bool)data["partial"] && data["connectionVerified"].Type == JTokenType.Null,
            "A verification exception must retain structured partial state and an unknown reading");
        Check(((string)data["error"]).Contains("verification failed") && (int)data["nativeCalls"] == 1,
            "Report the verification error together with native call count");

        f = new Fixture(); f.Tool.OnDisconnect = call => { f.A.Cell = Grid.XYToCell(5, 2); throw new Exception("target moved during callback"); };
        result = OrdersTools.Disconnect(Request()); data = Result(result);
        Check(result.IsError && (bool)data["partial"] && !(bool)data["connectionVerified"],
            "Post-call verification must use the recorded requested cell rather than a moved target");

        // A box affects only internal edges, invokes each object once and never
        // crosses onto a co-located utility on a different requested layer.
        f = new Fixture(); var c = f.Put(2, 3, UtilityConnections.Right | UtilityConnections.Down | UtilityConnections.Up);
        var d = f.Put(3, 3, UtilityConnections.Left | UtilityConnections.Down | UtilityConnections.Right);
        f.Manager.Bits[f.A.Cell] = UtilityConnections.Right | UtilityConnections.Up | UtilityConnections.Left;
        f.Manager.Bits[f.B.Cell] = UtilityConnections.Left | UtilityConnections.Up | UtilityConnections.Down;
        f.Expected[f.A.Cell] = UtilityConnections.Left; f.Expected[f.B.Cell] = UtilityConnections.Down;
        f.Expected[c.Cell] = UtilityConnections.Up; f.Expected[d.Cell] = UtilityConnections.Right;
        var otherManager = new Manager(); var other = f.Put(2, 2, UtilityConnections.Right, ObjectLayer.GasConduit, otherManager);
        f.Tool.OnDisconnect = call => f.Manager.Bits[call.Cell] = f.Expected[call.Cell];
        var box = new JObject { ["x1"] = 2, ["y1"] = 2, ["x2"] = 3, ["y2"] = 3,
            ["type"] = "wire", ["confirm"] = true, ["worldId"] = 0 };
        result = OrdersTools.Disconnect(box); data = Result(result);
        Check(!result.IsError && f.Tool.Calls.Count == 4 && (int)data["wouldDisconnectEdges"] == 4,
            "Box scope coalesces native invocations while retaining internal edge count");
        Check(f.Tool.Calls.Single(call => call.Cell == f.A.Cell).Remove == (UtilityConnections.Right | UtilityConnections.Up),
            "Coalesce only the two explicitly selected internal edges");
        Check(f.Tool.Calls.All(call => ReferenceEquals(call.Utility.GetNetworkManager(), f.Manager))
            && otherManager.Bits[other.Cell] == UtilityConnections.Right, "Layer selection must constrain native target scope");

        Console.WriteLine($"Native disconnect production-boundary tests passed: {checks} checks");
    }
}

internal sealed class Fixture
{
    internal readonly Manager Manager = new Manager();
    internal readonly DisconnectTool Tool = new DisconnectTool();
    internal readonly Dictionary<int, UtilityConnections> Expected = new Dictionary<int, UtilityConnections>();
    internal readonly KAnimGraphTileVisualizer A, B, Unrelated;
    internal Fixture()
    {
        Grid.Objects = new GameObject[64, 2]; Grid.Worlds = new int[64];
        Grid.Visible = Enumerable.Repeat(true, 64).ToArray(); DisconnectTool.Instance = Tool;
        A = Put(2, 2, UtilityConnections.Right | UtilityConnections.Up);
        B = Put(3, 2, UtilityConnections.Left | UtilityConnections.Down);
        Unrelated = Put(4, 2, UtilityConnections.Left);
        Expected[A.Cell] = UtilityConnections.Up; Expected[B.Cell] = UtilityConnections.Down;
    }
    internal KAnimGraphTileVisualizer Put(int x, int y, UtilityConnections bits,
        ObjectLayer layer = ObjectLayer.Wire, Manager manager = null)
    {
        manager ??= Manager;
        int cell = Grid.XYToCell(x, y); var go = new GameObject();
        var graph = new KAnimGraphTileVisualizer { Cell = cell, ConnectionManager = manager, connectionManager = manager };
        go.Add(graph); var prefab = new GameObject(); prefab.Add(new UtilityItem(manager));
        go.Add(new Building { Def = new BuildingDef { BuildingComplete = prefab } });
        go.Add(new KPrefabID { InstanceID = cell + (int)layer * 64 });
        Grid.Objects[cell, (int)layer] = go; manager.Bits[cell] = bits; return graph;
    }
}
public sealed class DisconnectCall
{
    public int Cell; public GameObject Target; public IHaveUtilityNetworkMgr Utility; public UtilityConnections Remove;
}
public sealed class DisconnectTool
{
    public static object Instance;
    public readonly List<DisconnectCall> Calls = new List<DisconnectCall>();
    public System.Action<DisconnectCall> OnDisconnect;
    private void DisconnectCellsAction(int cell, GameObject target, IHaveUtilityNetworkMgr utility, UtilityConnections remove)
    {
        var call = new DisconnectCall { Cell = cell, Target = target, Utility = utility, Remove = remove };
        Calls.Add(call); OnDisconnect?.Invoke(call);
    }
}
internal sealed class WrongSignature
{
    private void DisconnectCellsAction(int cell, object target, IHaveUtilityNetworkMgr utility, UtilityConnections remove)
        => throw new Exception("Wrong overload must never run");
}
internal sealed class WrongReturn
{
    private int DisconnectCellsAction(int cell, GameObject target, IHaveUtilityNetworkMgr utility, UtilityConnections remove)
        => throw new Exception("Wrong return contract must never run");
}
namespace OniMcp.Tools
{
    public static partial class OrdersTools
    {
        public static CallToolResult Disconnect(JObject args) => DisconnectUtilityEdges(args);
        private static bool HasRectInput(JObject args) => args["x1"] != null;
        private static int RectCellCount(Dictionary<string, int> r) => (r["x2"] - r["x1"] + 1) * (r["y2"] - r["y1"] + 1);
        private static List<ObjectLayer> GetCutLayers(string type) => type == "wire" ? new List<ObjectLayer> { ObjectLayer.Wire }
            : type == "gas" ? new List<ObjectLayer> { ObjectLayer.GasConduit } : new List<ObjectLayer>();
    }
    public static class ToolUtil
    {
        public static bool GetBool(JObject args, string key, bool fallback) => (bool?)args[key] ?? fallback;
        public static int ResolveWorldId(JObject args) => (int?)args["worldId"] ?? 0;
        public static bool CellMatchesWorld(int cell, int world) => cell >= 0 && cell < 64 && Grid.Worlds[cell] == world;
        public static Dictionary<string, int> GetRect(JObject args)
        {
            int x = (int?)args["x"] ?? 0, y = (int?)args["y"] ?? 0;
            return new Dictionary<string, int> { ["x1"] = (int?)args["x1"] ?? x, ["x2"] = (int?)args["x2"] ?? x,
                ["y1"] = (int?)args["y1"] ?? y, ["y2"] = (int?)args["y2"] ?? y };
        }
    }
}
namespace OniMcp.Core
{
    public sealed class CallToolResult
    {
        public bool IsError; public string TextValue;
        public static CallToolResult Text(string value) => new CallToolResult { TextValue = value };
        public static CallToolResult Error(string value) => new CallToolResult { TextValue = value, IsError = true };
    }
}
namespace OniMcp.Support { public static class McpJsonUtil { public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings(); } }
namespace UnityEngine
{
    public class Component
    {
        public GameObject gameObject;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public sealed class GameObject
    {
        private readonly List<object> components = new List<object>();
        public void Add(object value) { components.Add(value); if (value is Component component) component.gameObject = this; }
        public T GetComponent<T>() where T : class => components.OfType<T>().FirstOrDefault();
    }
}
[Flags] public enum UtilityConnections { Left = 1, Right = 2, Up = 4, Down = 8 }
public enum ObjectLayer { Wire, GasConduit }
public sealed class BuildingDef { public GameObject BuildingComplete; }
public sealed class Building : Component { public BuildingDef Def; }
public sealed class KPrefabID : Component { public int InstanceID; }
public interface IHaveUtilityNetworkMgr { IUtilityNetworkMgr GetNetworkManager(); }
public sealed class UtilityItem : IHaveUtilityNetworkMgr
{
    private readonly IUtilityNetworkMgr manager;
    public UtilityItem(IUtilityNetworkMgr manager) { this.manager = manager; }
    public IUtilityNetworkMgr GetNetworkManager() => manager;
}
public interface IUtilityNetworkMgr { UtilityConnections GetConnections(int cell, bool physical); }
public sealed class Manager : IUtilityNetworkMgr
{
    public readonly Dictionary<int, UtilityConnections> Bits = new Dictionary<int, UtilityConnections>();
    public bool ReadError;
    public UtilityConnections GetConnections(int cell, bool physical) => ReadError ? throw new Exception("native read failed") : Bits.GetValueOrDefault(cell);
}
public sealed class KAnimGraphTileVisualizer : Component
{
    public int Cell; public IUtilityNetworkMgr ConnectionManager, connectionManager;
    public void UpdateConnections(UtilityConnections value) => throw new Exception("MOD must not write connection masks directly");
    public void Refresh() => throw new Exception("MOD must not refresh native disconnect state directly");
}
public static class Grid
{
    public const int WidthInCells = 8, HeightInCells = 8;
    public static GameObject[,] Objects = new GameObject[64, 2]; public static int[] Worlds = new int[64];
    public static bool[] Visible = Enumerable.Repeat(true, 64).ToArray();
    public static bool IsVisible(int cell) => Visible[cell];
    public static int XYToCell(int x, int y) => y * 8 + x;
    public static int PosToCell(KAnimGraphTileVisualizer graph) => graph.Cell;
}

// ONI also defines a global Action enum; production delegates must be qualified.
public enum Action { None }
