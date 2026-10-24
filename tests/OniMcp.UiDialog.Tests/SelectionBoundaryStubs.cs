using System;
using System.Collections.Generic;
using UnityEngine;

// Native declarations and controlled event boundaries only. Product source owns
// selector resolution, guards, reflection, UI cache reads and postconditions.
internal static class SelectionNativeCalls
{
    internal static int Select, Close, Refresh, Format, Focus, Activate, DbGet;
    internal static int PointerEvents, BaseScroll, KScroll;
    internal static void Reset()
    { Select = Close = Refresh = Format = Focus = Activate = DbGet = PointerEvents = BaseScroll = KScroll = 0; }
}
internal sealed class KPrefabID : Component { public int InstanceID; }
internal sealed class MinionIdentity : Component
{
    public int FixtureCell, FixtureWorld;
    public string FixtureName = "Native fixture duplicant";
    public T GetComponent<T>() where T : class { return gameObject.GetComponent<T>(); }
    public int GetMyWorldId() { return FixtureWorld; }
    public string GetProperName() { return FixtureName; }
}
internal sealed class KSelectable : Component
{
    public bool IsSelectable = true;
    public int FixtureWorld;
    public int GetMyWorldId() { return FixtureWorld; }
}
internal sealed class FixtureLiveMinions
{
    public List<MinionIdentity> Items = new List<MinionIdentity>();
}
internal static class Components
{
    public static FixtureLiveMinions LiveMinionIdentities = new FixtureLiveMinions();
}
internal static partial class Grid
{
    public static int[] Visible = new int[16], WorldIdx = new int[16];
    public static bool[] Valid = new bool[16], WorldValid = new bool[16];
    public static Action<int> FixtureOnPositionRead;
    public static int PositionReads;
    public static int PosToCell(Component value)
    {
        PositionReads++; FixtureOnPositionRead?.Invoke(PositionReads);
        return ((MinionIdentity)value).FixtureCell;
    }
    public static bool IsValidCell(int cell) { return cell >= 0 && cell < CellCount && Valid[cell]; }
    public static bool IsWorldValidCell(int cell) { return cell >= 0 && cell < CellCount && WorldValid[cell]; }
}
internal sealed partial class ClusterManager
{
    public int activeWorldId;
}
internal sealed partial class PlayerController
{
    public static PlayerController Instance;
    public bool Dragging;
    public bool IsDragging() { return Dragging; }
    public object ActiveTool;
    public void ActivateTool(object value) { SelectionNativeCalls.Activate++; throw new InvalidOperationException("forbidden_activate_tool"); }
}
internal sealed class SelectTool : Component
{
    public static SelectTool Instance;
    public KSelectable selected, LastArgument;
    public Action<KSelectable> FixtureNativeSelect;
    public SelectTool() { var go = new GameObject(); go.Add(this); }
    public void Select(KSelectable value)
    {
        SelectionNativeCalls.Select++; LastArgument = value;
        FixtureNativeSelect?.Invoke(value);
    }
    public void SelectAndFocus(object position, KSelectable value)
    { SelectionNativeCalls.Focus++; throw new InvalidOperationException("forbidden_focus"); }
}
internal sealed class ManagementMenu
{
    internal sealed class ScreenData { }
    public static ManagementMenu Instance;
    private ScreenData activeScreen;
    public void FixtureSetScreen(ScreenData value) { activeScreen = value; }
    public ScreenData FixtureScreen { get { return activeScreen; } }
}
internal class TargetPanel : KScreen
{
    private GameObject selectedTarget;
    public void FixtureTarget(GameObject value) { selectedTarget = value; }
    public GameObject FixtureSelected { get { return selectedTarget; } }
    public void SetTarget(GameObject value)
    { SelectionNativeCalls.Refresh++; throw new InvalidOperationException("forbidden_set_target"); }
}
internal sealed class SimpleInfoScreen : TargetPanel
{
    private MinionVitalsPanel vitalsPanel;
    public void FixtureVitals(MinionVitalsPanel value) { vitalsPanel = value; }
    public MinionVitalsPanel FixturePanel { get { return vitalsPanel; } }
    public void Refresh() { SelectionNativeCalls.Refresh++; throw new InvalidOperationException("forbidden_info_refresh"); }
}
internal sealed class ToolTip : Component
{
    public string FixtureTooltip = "Native cached tooltip";
    public string toolTip { set { FixtureTooltip = value; } }
    public string GetToolTip() { SelectionNativeCalls.Format++; throw new InvalidOperationException("forbidden_tooltip_format"); }
}
namespace Klei.AI
{
    internal sealed class Amount
    {
        public string Id;
        public string GetDescription() { SelectionNativeCalls.Format++; throw new InvalidOperationException("forbidden_amount_format"); }
    }
}
internal sealed class FixtureAmounts { public Klei.AI.Amount Breath; }
internal sealed class Db
{
    private static Db _Instance;
    public FixtureAmounts Amounts = new FixtureAmounts();
    public static void FixtureSet(Db value) { _Instance = value; }
    public static Db FixtureExisting { get { return _Instance; } }
    public static Db Get() { SelectionNativeCalls.DbGet++; throw new InvalidOperationException("forbidden_database_initialization"); }
}
internal sealed class MinionVitalsPanel : Component
{
    public struct AmountLine
    {
        public Klei.AI.Amount amount;
        public GameObject go;
        public LocText locText;
        public ToolTip toolTip;
    }
    private GameObject lastSelectedEntity;
    public List<AmountLine> amountsLines = new List<AmountLine>();
    public void FixtureTarget(GameObject value) { lastSelectedEntity = value; }
    public GameObject FixtureSelected { get { return lastSelectedEntity; } }
    public void Refresh() { SelectionNativeCalls.Refresh++; throw new InvalidOperationException("forbidden_vitals_refresh"); }
    public string FormatAmount() { SelectionNativeCalls.Format++; throw new InvalidOperationException("forbidden_vitals_format"); }
}
internal sealed class DetailsScreen : KScreen
{
    public static DetailsScreen Instance;
    public bool FixtureInitialized = true;
    public bool IsInitialized() { return FixtureInitialized; }
    public GameObject target;
    private KButton CloseButton;
    public KScreen FixtureTab;
    public Action FixtureNativeClose;
    public DetailsScreen()
    {
        Modal = false;
        var closeGo = new GameObject(); CloseButton = new KButton(); closeGo.Add(CloseButton);
        closeGo.transform.parent = transform; CloseButton.onClick += DeselectAndClose;
    }
    public KScreen GetActiveTab() { return FixtureTab; }
    public KButton FixtureCloseButton { get { return CloseButton; } }
    public void FixtureReplaceButton(KButton value) { CloseButton = value; }
    public void DeselectAndClose() { SelectionNativeCalls.Close++; FixtureNativeClose?.Invoke(); }
    public void Refresh(GameObject value)
    { SelectionNativeCalls.Refresh++; throw new InvalidOperationException("forbidden_details_refresh"); }
}

internal static class App
{
    public static bool IsExiting;
    public static bool isLoading;
}

namespace UnityEngine
{
    internal struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    internal sealed class RectTransform : Transform
    {
        public RectTransform(GameObject value) : base(value) { }
    }
}
namespace UnityEngine.EventSystems
{
    internal sealed class EventSystem : Component
    {
        private static EventSystem fixtureCurrent;
        public static EventSystem current { get { return fixtureCurrent; } }
        public static void FixtureSetCurrent(EventSystem value) { fixtureCurrent = value; }
    }
    internal sealed class PointerEventData
    {
        public static bool FixtureThrowOnConstruction;
        public readonly EventSystem FixtureEvents;
        public Vector2 scrollDelta { get; set; }
        public PointerEventData(EventSystem events)
        {
            SelectionNativeCalls.PointerEvents++;
            if (FixtureThrowOnConstruction) throw new InvalidOperationException("fixture_pointer_event_failed");
            FixtureEvents = events;
        }
    }
}
namespace UnityEngine.UI
{
    internal class ScrollRect : Component
    {
        public bool vertical { get; set; } = true;
        public bool horizontal { get; set; }
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool FixtureActive = true;
        public UnityEngine.EventSystems.PointerEventData FixtureLastEvent;
        public Action<UnityEngine.EventSystems.PointerEventData> FixtureNativeScroll;
        public virtual bool IsActive() { return FixtureActive; }
        public virtual void OnScroll(UnityEngine.EventSystems.PointerEventData data)
        {
            SelectionNativeCalls.BaseScroll++; FixtureLastEvent = data;
            FixtureNativeScroll?.Invoke(data);
        }
    }
}
internal sealed class KScrollRect : UnityEngine.UI.ScrollRect
{
    public bool allowVerticalScrollWheel = true;
    public bool scrollIsHorizontalOnly;
    public bool isDragging;
    public override void OnScroll(UnityEngine.EventSystems.PointerEventData data)
    {
        SelectionNativeCalls.KScroll++; FixtureLastEvent = data;
        FixtureNativeScroll?.Invoke(data);
    }
}
internal sealed class FixtureUnknownScrollRect : UnityEngine.UI.ScrollRect { }
