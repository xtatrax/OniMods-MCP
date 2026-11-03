using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// Only native input/UI and unrelated MCP metadata boundaries are substitutes.
// Product action selection, binding inspection, guards and serialization are linked unchanged.
public enum Action { Invalid = -1, ManageVitals, ManageConsumables, ManagePriorities, ManageSchedule,
    ManageSkills, ManageResearch, ManageStarmap, ManageReport, ManageDatabase, Overlay1,
    BuildCategoryTiles, BuildMenuKeyA, Escape, Find, Help, ToggleScreenshotMode, NumActions }
public enum KKeyCode { None, A, B }
public enum GamepadButton { A, B, NumButtons }
public struct BindingEntry { public Action mAction; public KKeyCode mKeyCode; public GamepadButton mButton; }
public static class GameInputMapping
{
    public static BindingEntry[] Entries;
    public static bool ThrowGet;
    public static int Reads, FindCalls, MissingAsserts, CompareCalls;
    public static BindingEntry[] GetBindingEntries()
    { Reads++; if (ThrowGet) throw new InvalidOperationException("fixture binding read failure"); return Entries; }
    public static BindingEntry FindEntry(Action action)
    {
        FindCalls++;
        if (Entries != null) foreach (var entry in Entries) if (entry.mAction == action) return entry;
        MissingAsserts++; throw new InvalidOperationException("fixture native FindEntry asserts on an absent action");
    }
    public static bool CompareActionKeyCodes(Action a, Action b)
    { CompareCalls++; return FindEntry(a).mKeyCode == FindEntry(b).mKeyCode; }
}
public enum InputEventType { KeyDown, KeyUp }
public sealed class KButtonEvent
{
    public readonly Action Action; public readonly InputEventType Type;
    public KButtonEvent(object input, InputEventType type, Action action) { Action = action; Type = type; }
}
namespace UnityEngine
{
    public sealed class GameObject
    {
        public bool FixtureActiveInHierarchy = true;
        public bool activeInHierarchy { get { return FixtureActiveInHierarchy; } }
    }
}
// Native-shaped public reads; fixture inputs are independent of MCP eligibility.
public class KScreen
{
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
    public bool FixtureActiveAndEnabled = true;
    public Func<bool> FixtureActiveRead;
    public int FixtureActiveReads, FixtureShowCalls, FixtureActivateCalls;
    public bool isActiveAndEnabled { get { return FixtureActiveAndEnabled; } }
    public virtual bool IsScreenActive()
    { FixtureActiveReads++; return FixtureActiveRead == null ? isActiveAndEnabled : FixtureActiveRead(); }
    public virtual void Show(bool show = true)
    { FixtureShowCalls++; throw new InvalidOperationException("forbidden fixture native Show"); }
    public void Activate()
    { FixtureActivateCalls++; throw new InvalidOperationException("forbidden fixture native Activate"); }
}
public sealed class ScheduleScreen : KScreen
{
    public static ScheduleScreen Instance;
    public int FixtureRefreshCalls;
    public void RefreshAllPaintButtons()
    { FixtureRefreshCalls++; throw new InvalidOperationException("forbidden fixture native refresh"); }
}
public static class KInputManager { public static bool isFocused = true; }
public sealed class KScreenManager
{
    public static KScreenManager Instance;
    public int Down, Up;
    public System.Action<KButtonEvent> FixtureDown, FixtureUp;
    public void OnKeyDown(KButtonEvent e)
    { GameInputMapping.FindEntry(e.Action); Down++; if (FixtureDown != null) FixtureDown(e); }
    public void OnKeyUp(KButtonEvent e)
    { GameInputMapping.FindEntry(e.Action); Up++; if (FixtureUp != null) FixtureUp(e); }
}
public sealed class ManagementMenu : KScreen
{
    public static ManagementMenu Instance;
    public ScheduleScreen scheduleScreen;
    public KScreen FixtureReportedOpenScreen;
    public Func<KScreen, bool> FixtureOpenRead;
    public int FixtureOpenReads;
    public bool IsScreenOpen(KScreen screen)
    { FixtureOpenReads++; return FixtureOpenRead == null ? ReferenceEquals(FixtureReportedOpenScreen, screen) : FixtureOpenRead(screen); }
    public bool ResearchReady = true;
    public bool ThrowResearch;
    public static bool SpaceReady;
    public int DirectCalls;
    public bool CheckHasResearchCenter() { if (ThrowResearch) throw new InvalidOperationException("fixture readiness failure"); return ResearchReady; }
    public static bool StarmapAvailable() { return SpaceReady; }
    public void OpenCodexToEntry(string id) { DirectCalls++; }
    public void OpenResearch(string id) { DirectCalls++; }
    public void ToggleSkills() { DirectCalls++; }
    public void OpenClusterMap() { DirectCalls++; }
    public void OpenStarmap() { DirectCalls++; }
    public void TogglePriorities() { DirectCalls++; }
    public void OpenReports(int day) { DirectCalls++; }
    public void ToggleCodex() { DirectCalls++; }
}
public static class Components { public static readonly List<object> RoleStations = new List<object>(); }
public static class DebugHandler { public static bool InstantBuildMode; }
public sealed class Game { public static Game Instance; public bool SandboxModeActive; }
public static class DlcManager { public static bool Cluster; public static bool FeatureClusterSpaceEnabled() { return Cluster; } }
public static class GameUtil
{
    public static int GetCurrentCycle() { return 3; }
    public static string ReplaceHotkeyString(string template, Action action) { return template; }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    { public HarmonyPatch(Type type, string name, Type[] signature) { } }
}
namespace OniMcp.Support
{
    public static class KButtonEventSafety
    { public static bool TryGetActionArray(KButtonEvent item, out bool[] value) { value = null; return false; } }
    public static class OniMcpLog { public static void Debug(string text) { } }
}
namespace OniMcp.Core
{
    public sealed class CallToolResult
    {
        public bool IsError; public string Value;
        public static CallToolResult Text(string value) { return new CallToolResult { Value = value }; }
        public static CallToolResult Error(string value) { return new CallToolResult { Value = value, IsError = true }; }
    }
}
namespace OniMcp.Tools
{
    public sealed class McpTool
    {
        public string Name, Group, Mode, Risk, Description; public bool Hidden;
        public List<string> Aliases, Tags; public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, OniMcp.Core.CallToolResult> Handler;
    }
    public sealed class McpToolParameter
    { public string Type, Description; public bool Required; public List<string> EnumValues; }
    public static class ToolUtil { public static int? GetInt(JObject args, string key) { return (int?)args[key]; } }
}

namespace OniMcp.Tools
{
    public static class UiDialogTools
    { public static OniMcp.Core.CallToolResult Control(JObject args) { throw new InvalidOperationException("unrelated dialog route"); } }
    public static class UiSelectionTools
    { public static OniMcp.Core.CallToolResult Control(JObject args) { throw new InvalidOperationException("unrelated selection route"); } }
    public static class UiHintTools
    { public static McpTool ControlUiFeedback() { throw new InvalidOperationException("unrelated feedback route"); } }
}
