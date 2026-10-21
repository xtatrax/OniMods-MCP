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
public sealed class KScreenManager
{
    public static KScreenManager Instance;
    public int Down, Up;
    public void OnKeyDown(KButtonEvent e) { GameInputMapping.FindEntry(e.Action); Down++; }
    public void OnKeyUp(KButtonEvent e) { GameInputMapping.FindEntry(e.Action); Up++; }
}
public sealed class ManagementMenu
{
    public static ManagementMenu Instance;
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
