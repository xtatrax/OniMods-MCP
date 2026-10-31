using System;
using System.Collections.Generic;
using System.Linq;

// Only the game/Unity boundary is substituted. The adapter and guards are production source.
namespace UnityEngine
{
    internal class Component { public GameObject gameObject; public T GetComponent<T>() where T:class { return gameObject?.GetComponent<T>(); } }
    internal sealed class GameObject
    {
        private readonly List<object> components = new List<object>();
        public int Cell;
        public int Id;
        public string name = "fixture";
        public bool Destroyed;
        public bool Active = true;
        public bool activeSelf {get{return Active;}}
        public bool activeInHierarchy {get{return Active;}}
        public int DeleteCalls;
        public bool ThrowFixtureComponentRead;
        public int GetInstanceID() { return Id; }
        public string GetProperName() { return name; }
        public void DeleteObject() { DeleteCalls++; Destroyed=true; }
        public void Add(object item)
        {
            components.Add(item);
            var component = item as Component;
            if (component != null) component.gameObject = this;
        }
        public T GetComponent<T>() where T : class
        {
            if (ThrowFixtureComponentRead) throw new InvalidOperationException("Fixture component read unavailable");
            return components.OfType<T>().FirstOrDefault();
        }
        public T[] GetComponents<T>() where T : class
        {
            if (ThrowFixtureComponentRead) throw new InvalidOperationException("Fixture component read unavailable");
            return components.OfType<T>().ToArray();
        }
    }
}
internal sealed class KPrefabID : UnityEngine.Component { public int InstanceID; public Tag PrefabTag; }
internal sealed class KSelectable : UnityEngine.Component
{
    public bool IsSelectable = true;
    public int WorldId;
    public int GetMyWorldId() { return WorldId; }
}
internal sealed class ClusterManager
{
    public static ClusterManager Instance;
    public int activeWorldId;
}
internal enum Action { NativeGameAction }
internal enum KKeyCode { Mouse0 }
internal sealed class KButton
{
    public bool isSpawned = true;
    public bool isActiveAndEnabled = true;
    public bool isInteractable = true;
    public int Clicks;
    public System.Action OnClick;
    public void SignalClick(KKeyCode key)
    {
        if (!isInteractable) throw new Exception("adapter skipped interactability guard");
        Clicks++;
        OnClick?.Invoke();
    }
}
internal interface IPlayerControlledToggle { KSelectable GetSelectable(); }
internal sealed class ToggleFixture : UnityEngine.Component, IPlayerControlledToggle
{
    public KSelectable Selectable;
    public KSelectable GetSelectable() { return Selectable; }
}
internal sealed class PlayerControlledToggleSideScreen
{
    public bool isSpawned = true;
    public IPlayerControlledToggle target;
    public bool isActiveAndEnabled = true;
    public bool Valid = true;
    public KButton toggleButton = new KButton();
    public bool IsValidForTarget(UnityEngine.GameObject go) { return Valid && go.GetComponent<IPlayerControlledToggle>() != null; }
}
internal sealed class DetailsScreen
{
    public static DetailsScreen Instance;
    public UnityEngine.GameObject target;
    public PlayerControlledToggleSideScreen[] Screens = new PlayerControlledToggleSideScreen[0];
    public int Refreshes;
    public System.Action<UnityEngine.GameObject> OnRefresh;
    public void Refresh(UnityEngine.GameObject go) { Refreshes++; OnRefresh?.Invoke(go); }
    public T[] GetComponentsInChildren<T>(bool includeInactive) { return Screens.OfType<T>().ToArray(); }
}
internal sealed class SelectTool
{
    public static SelectTool Instance;
    public KSelectable selected;
    public int Selects;
    public bool AcceptSelection = true;
    public void Select(KSelectable item) { selected = AcceptSelection ? item : null; Selects++; }
}
internal static partial class Grid
{
    public const int WidthInCells = 8;
    public const int HeightInCells = 8;
    public static readonly int[] Visible = new int[64];
    public static readonly int[] WorldIdx = new int[64];
    public static readonly bool[] Solid = new bool[64];
    public static readonly bool[] Foundation = new bool[64];
    public struct ObjectLayerIndexer
    {
        public UnityEngine.GameObject this[int cell,int layer]
        {get{UnityEngine.GameObject value;return ObjectLayers[layer].TryGetValue(cell,out value)?value:null;}set{ObjectLayers[layer][cell]=value;}}
    }
    public static Dictionary<int,UnityEngine.GameObject>[] ObjectLayers;
    public static ObjectLayerIndexer Objects = new ObjectLayerIndexer();
    public static void ResetObjectLayers()
    {ObjectLayers=Enumerable.Range(0,(int)ObjectLayer.NumLayers).Select(_=>new Dictionary<int,UnityEngine.GameObject>()).ToArray();}
    public static bool IsVisible(int cell) { return IsValidCell(cell) && Visible[cell] > 0; }
    public static UnityEngine.Vector3 CellToPosCBC(int cell,int layer) { return new UnityEngine.Vector3(CellColumn(cell),CellRow(cell),layer); }
    public static int OffsetCell(int cell,CellOffset offset) { return XYToCell(CellColumn(cell)+offset.x,CellRow(cell)+offset.y); }
    public static ObjectLayer GetObjectLayerForConduitType(ConduitType type) { return ObjectLayer.GasConduit; }
    public static int CellColumn(int cell) { return cell % WidthInCells; }
    public static int CellRow(int cell) { return cell / WidthInCells; }
    public static int PosToCell(UnityEngine.GameObject go) { return go.Cell; }
    public static int XYToCell(int x, int y) { return y * WidthInCells + x; }
    public static bool IsValidCell(int cell) { return cell >= 0 && cell < 64; }
    public static bool IsWorldValidCell(int cell) { return IsValidCell(cell) && WorldIdx[cell] >= 0; }
}
namespace OniMcp.Tools
{
    internal static partial class ToolUtil
    {
        public static bool GameObjectMatchesWorld(UnityEngine.GameObject go, int world)
        { return Grid.IsValidCell(go.Cell) && Grid.WorldIdx[go.Cell] == world; }
    }
}
