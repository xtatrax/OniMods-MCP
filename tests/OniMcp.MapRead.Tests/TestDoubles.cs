using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public readonly record struct HashedString(string Value) { public override string ToString() => Value; }
public static class OverlayModes
{
    public static class None { public static HashedString ID = new("default"); }
    public static class Power { public static HashedString ID = new("power"); }
    public static class LiquidConduits { public static HashedString ID = new("liquid_conduits"); }
    public static class GasConduits { public static HashedString ID = new("gas_conduits"); }
    public static class Logic { public static HashedString ID = new("logic"); }
    public static class SolidConveyor { public static HashedString ID = new("solid_conveyor"); }
    public static class Temperature { public static HashedString ID = new("temperature"); }
    public static class Oxygen { public static HashedString ID = new("oxygen"); }
    public static class Light { public static HashedString ID = new("light"); }
    public static class Decor { public static HashedString ID = new("decor"); }
    public static class Disease { public static HashedString ID = new("disease"); }
    public static class Radiation { public static HashedString ID = new("radiation"); }
    public static class TileMode { public static HashedString ID = new("materials"); }
    public static class Crop { public static HashedString ID = new("crop"); }
    public static class Harvest { public static HashedString ID = new("harvest"); }
    public static class Rooms { public static HashedString ID = new("rooms"); }
}
public sealed class OverlayScreen
{
    public static OverlayScreen Instance = new(); public HashedString mode = OverlayModes.None.ID;
    public int Changes; public void ToggleOverlay(HashedString next, bool sound) { mode = next; Changes++; }
}
public sealed class CameraController
{
    public static CameraController Instance = new(); public int Moves;
    public void SnapTo(Vector3 point, float zoom) { Moves++; }
}
public sealed partial class Game { public static Game Instance = new(); public void ForceOverlayUpdate(bool value) { } }
public sealed class World { public Vector2Int WorldOffset = new(10, 20), WorldSize = new(64, 64); }
public sealed class ClusterManager
{
    public static ClusterManager Instance = new(); public int activeWorldId;
    public World world = new(); public World GetWorld(int id) => id == activeWorldId ? world : null;
}
public static partial class Grid
{
    public static int WidthInCells = 128, HeightInCells = 128;
    public static int[] WorldIdx = new int[16384]; public static bool[] Visible = Enumerable.Repeat(true, 16384).ToArray();
    public static GameObject[,] Objects = new GameObject[16384, 2];
    public static Element[] Element = new Element[16384];
    public static float[] Mass = new float[16384], Temperature = new float[16384];
    public static bool[] Solid = new bool[16384], Foundation = new bool[16384];
    public static bool[] WorldValid = Enumerable.Repeat(true, 16384).ToArray();
    public static int CellColumn(int cell) => cell % WidthInCells;
    public static int CellRow(int cell) => cell / WidthInCells;
    public static int CellAbove(int cell) => cell + WidthInCells;
    public static int CellBelow(int cell) => cell - WidthInCells;
    public static int CellLeft(int cell) => cell - 1;
    public static int CellRight(int cell) => cell + 1;
    public static int XYToCell(int x, int y) => y * WidthInCells + x;
    public static bool IsValidCell(int cell) => cell >= 0 && cell < 16384;
    public static bool IsWorldValidCell(int cell) => IsValidCell(cell) && WorldValid[cell];
    public static bool IsVisible(int cell) => Visible[cell];
}
public enum ObjectLayer { Wire, Minion, WireTile, ReplacementWire, LiquidConduit, LiquidConduitTile, ReplacementLiquidConduit, GasConduit, GasConduitTile, ReplacementGasConduit, SolidConduit, SolidConduitTile, ReplacementSolidConduit, LogicWire, LogicWireTile, ReplacementLogicWire, Building, LogicGate }
[Flags] public enum UtilityConnections { Left = 1, Right = 2, Up = 4, Down = 8 }
public sealed class BuildingComplete { }
public sealed class KAnimGraphTileVisualizer
{
    public bool isPhysicalBuilding; public IUtilityNetworkMgr ConnectionManager;
}
public interface IHaveUtilityNetworkMgr { IUtilityNetworkMgr GetNetworkManager(); }
public interface IUtilityNetworkMgr { UtilityConnections GetConnections(int cell, bool is_physical_building); }
public sealed class TestProvider : IHaveUtilityNetworkMgr
{
    public IUtilityNetworkMgr Manager;
    public Func<IUtilityNetworkMgr> ManagerReader;
    public int Reads;
    public IUtilityNetworkMgr GetNetworkManager() { Reads++; return ManagerReader == null ? Manager : ManagerReader(); }
}
public sealed class TestManager : IUtilityNetworkMgr
{
    public UtilityConnections Mask;
    public UtilityConnections? PhysicalRead, NonPhysicalRead;
    public bool RequestedPhysical;
    public readonly List<(int Cell,bool Physical)> Reads = new();
    public Func<int,bool,UtilityConnections> Reader;
    public UtilityConnections GetConnections(int cell,bool is_physical_building)
    {
        RequestedPhysical=is_physical_building; Reads.Add((cell,is_physical_building));
        return Reader != null ? Reader(cell,is_physical_building) : is_physical_building ? PhysicalRead ?? Mask : NonPhysicalRead ?? Mask;
    }
    public void GetOrAdd() => throw UtilitySummaryBoundary.Forbidden("GetOrAdd");
    public void Update() => throw UtilitySummaryBoundary.Forbidden("Update");
    public void Rebuild() => throw UtilitySummaryBoundary.Forbidden("Rebuild");
    public void AddToNetworks() => throw UtilitySummaryBoundary.Forbidden("AddToNetworks");
}
public enum SimHashes { TestElement, OtherElement, Oxygen, ContaminatedOxygen, Water, PollutedWater }
public sealed class Element { public SimHashes id; public string name; public bool IsLiquid, IsGas; }
public sealed partial class KPrefabID { public int InstanceID; }
public sealed class TestBuildingDef { public string PrefabID, Name; }
public static class Assets { public static List<TestBuildingDef> BuildingDefs = new(); }
namespace UnityEngine
{
    public readonly record struct Vector2Int(int x, int y);
    public readonly record struct Vector3(float x, float y, float z);
    public sealed class Transform { public Vector3 position = new(20, 20, 0); }
    public sealed class Camera { public static Camera main; public Transform transform = new(); public float orthographicSize = 2, aspect = 1; }
    public static class Screen { public static int width = 1920, height = 1080; }
    public static class Mathf { public static int RoundToInt(float x) => (int)Math.Round(x); public static int Clamp(int x, int min, int max) => Math.Clamp(x, min, max); public static float Clamp(float x, float min, float max) => Math.Clamp(x, min, max); }
    public sealed partial class GameObject
    {
        public Dictionary<Type, object> Components = new();
        public string name;
        public int ComponentReads, NameReads;
        public Func<Type,object> ComponentReader;
        public T GetComponent<T>() where T : class { ComponentReads++; if (ComponentReader != null) return ComponentReader(typeof(T)) as T; return Components.TryGetValue(typeof(T), out var component) ? (T)component : null; }
        public string GetProperName() { NameReads++; return name; }
    }
}
namespace OniMcp.Core
{
    public sealed class CallToolResult { public string Value; public bool IsError; public static CallToolResult Text(string text) => new() { Value = text }; public static CallToolResult Error(string text) => new() { Value = text, IsError = true }; }
}
namespace OniMcp.Tools
{
    public static partial class ToolUtil
    {
        public static int? GetInt(JObject a, string key) => int.TryParse(a?[key]?.ToString(), out int v) ? v : null;
        public static float? GetFloat(JObject a, string key) => float.TryParse(a?[key]?.ToString(), out float v) ? v : null;
        public static bool GetBool(JObject a, string key, bool fallback) => bool.TryParse(a?[key]?.ToString(), out bool v) ? v : fallback;
    }
    public static partial class WorldEditorTools
    {
        private const int VirtualMapLayerSize = 32;
        private static readonly string _cwd = "/active/";
        private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly Dictionary<string, char> UniqueCharMap = new();
        private sealed class MapEditCell { public int X { get; set; } public int Y { get; set; } public string FromToken { get; set; } public string ToToken { get; set; } }
        private static string MapTokenPart(string value) => value?.Replace(" ", "") ?? "";
        private static char GetUniqueChar(string id, string name) => UniqueCharMap.TryGetValue(id, out char value) ? value : id[0];
        private static string GetOverlayViewName(HashedString mode) => mode.ToString();
        private static string ReadFileDirectly(string path) => "HTML:" + path;
        private static string NormalizePath(string path, string cwd) => path ?? "";
        private static string SaveRelativePath(string path) => path.Replace("/active/", "");
        private static string Text(JObject args, params string[] keys) => keys.Select(k => args?[k]?.ToString()).FirstOrDefault(x => x != null) ?? "";
        private static char GetConnectionGlyph(bool up, bool down, bool left, bool right) => MapTextReadPolicy.ConnectionGlyph(true, (up ? 8 : 0) | (down ? 4 : 0) | (left ? 2 : 0) | (right ? 1 : 0));
        private static string GetMapMd(string title, int xMin, int xMax, int yMin, int yMax, HashedString mode, bool compact = true)
        { Rendered.Add((xMin, xMax, yMin, yMax, mode, compact)); return mode.ToString(); }
        internal static List<(int xMin, int xMax, int yMin, int yMax, HashedString mode, bool compact)> Rendered = new();
    }
}
