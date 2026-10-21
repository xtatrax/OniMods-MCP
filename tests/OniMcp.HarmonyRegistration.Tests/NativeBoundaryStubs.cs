using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

internal static class FixtureEvents
{
    internal static readonly List<string> Events = new List<string>();
    internal static int BaseOnLoadCalls, RegistryInitializations, RuntimeReadyCalls, OverlayCalls, CoordinateCalls;
    internal static void Add(string name) { Events.Add(name); }
    internal static bool Prepared { get { return new[] { "paths", "options", "language_provider", "game_language_provider", "locale_refresh", "plib", "translation", "options_ui" }.All(Events.Contains); } }
}
namespace KMod
{
    public class UserMod2
    {
        public string path = "/fixture/mod";
        public Assembly assembly;
        public virtual void OnLoad(Harmony harmony)
        {
            FixtureEvents.BaseOnLoadCalls++; FixtureEvents.Add("base_on_load");
            harmony.PatchAll(assembly ?? GetType().Assembly);
        }
    }
}
namespace UnityEngine
{
    public sealed class GameObject
    {
        private readonly string name;
        public GameObject(string value) { name = value; FixtureEvents.Add("object:" + name); }
        public T AddComponent<T>() where T : new() { FixtureEvents.Add("component:" + typeof(T).Name); return new T(); }
    }
    public static class Debug { public static void LogWarning(object message) { FixtureEvents.Add("warning"); } }
}
namespace PeterHan.PLib.Core { public static class PUtil { public static void InitLibrary() { FixtureEvents.Add("plib"); } } }
namespace PeterHan.PLib.Options
{
    public sealed class POptions { public void RegisterOptions(KMod.UserMod2 mod, Type type) { FixtureEvents.Add("options_ui"); } }
}
public static class Localization
{
    public sealed class Locale { public string Code { get { return "ja"; } } }
    public static object GetLocale() { FixtureEvents.Add("locale_refresh"); return new Locale(); }
    public static void Initialize() { }
    public static void RegisterForTranslation(Type type) { FixtureEvents.Add("translation"); }
}
public static class STRINGS { }
// The installed game also declares a global generic Tuple; production must qualify System.Tuple.
public sealed class Tuple<T1, T2, T3> { }
public sealed class Db { public void Initialize() { } }
public enum Action { FixtureAction }
public enum InputEventType { KeyDown, KeyUp }
public sealed class KInputController { }
public sealed class KButtonEvent
{
    public KButtonEvent(KInputController input, InputEventType type, bool[] actions) { }
    public KButtonEvent(KInputController input, InputEventType type, Action action) { }
    public bool IsAction(Action action) { return false; }
}
public sealed class ToolMenu { public void OnKeyDown(KButtonEvent button) { } }
public sealed class AutoDisinfectable
{
    private bool enableAutoDisinfect = true;
    public bool FixtureEnabled { get { return enableAutoDisinfect; } }
    public void DisableAutoDisinfect() { enableAutoDisinfect = false; }
    public void EnableAutoDisinfect() { enableAutoDisinfect = true; }
}
public sealed class AutoDisinfectableManager
{
    public static AutoDisinfectableManager Instance;
    private readonly List<AutoDisinfectable> autoDisinfectables = new List<AutoDisinfectable>();
    public void AddAutoDisinfectable(AutoDisinfectable item) { autoDisinfectables.Add(item); }
    public int FixtureCount { get { return autoDisinfectables.Count; } }
}
public sealed class SpeedControlScreen
{
    public void Pause(bool value, bool second) { }
    public void Unpause(bool value) { }
    public void SetSpeed(int value) { }
    public void ToggleRidiculousSpeed() { }
    private void OnChanged() { }
}
namespace OniMcp.Config
{
    public sealed class OniMcpOptions
    {
        public static readonly OniMcpOptions Current = new OniMcpOptions();
        public string Language = "ja";
        public bool GlobalAutoDisinfectDisabled;
        public static string ConfigPath { get { return "fixture-config.json"; } }
        public static void Reload() { FixtureEvents.Add("options"); }
        public static void Save(OniMcpOptions options) { throw new InvalidOperationException("configuration writes are outside this fixture"); }
    }
}
namespace OniMcp
{
    public static class McpLocalization
    {
        private static Func<string> language, gameLanguage;
        public static Func<string> LanguageProvider { get { return language; } set { language = value; FixtureEvents.Add("language_provider"); } }
        public static Func<string> GameLanguageProvider { get { return gameLanguage; } set { gameLanguage = value; FixtureEvents.Add("game_language_provider"); } }
        public static string Get(string key) { return key; }
    }
    public static class OniMcpVersion { public static string Display { get { return "fixture-version"; } } }
    public static class ToolMenu_OnKeyDown_Patch { public static bool Prefix() { return true; } }
    public static class KButtonEvent_IsAction_Patch { public static bool Prefix() { return true; } }
    public static class KButtonEvent_BoolArrayCtor_Patch { public static void Postfix() { } }
    public static class KButtonEvent_ActionCtor_Patch { public static void Postfix() { } }
}
namespace OniMcp.Support
{
    public static class OniMcpPaths { public static void Initialize(string path, Assembly assembly) { FixtureEvents.Add("paths"); } }
    public static class OniMcpLog
    {
        public static void Debug(string message) { if (message.Contains("Input safety patch check at OnLoad")) FixtureEvents.Add("ensure_input"); if (message.Contains("Auto-disinfect policy patch check at OnLoad")) FixtureEvents.Add("ensure_auto"); }
        public static void Warning(string message) { }
    }
}
namespace OniMcp.Server
{
    public sealed class MainThreadBridge { }
    public static class GameRestartCoordinator { public static void EnsureCreated() { FixtureEvents.Add("restart_coordinator"); } }
    public sealed class McpHttpServer
    {
        public static McpHttpServer Instance;
        public string EndpointUrl = "fixture-endpoint";
        public int Port = 1234;
        public object GetSessionSummaries() { return new object[0]; }
    }
    public static class ToolCallMiddleware
    {
        public static Dictionary<string, object> QueueNotification(string message, string level) { throw new NotSupportedException(); }
        public static Dictionary<string, object> Status() { throw new NotSupportedException(); }
        public static Dictionary<string, object> Clear() { throw new NotSupportedException(); }
    }
}
namespace OniMcp.Tools
{
    // Tool descriptor DTOs are a registry boundary; real handlers and result/serializer types are linked.
    public sealed class McpTool
    {
        public string Name, Description, Group, Mode, Risk;
        public bool Hidden;
        public List<string> Aliases, Tags;
        public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, CallToolResult> Handler;
    }
    public sealed class McpToolParameter
    {
        public string Type, Description;
        public bool Required;
        public List<string> EnumValues;
        public SchemaProperty Items;
    }
    public static class OniToolRegistry
    {
        public static void Initialize() { FixtureEvents.RegistryInitializations++; FixtureEvents.Add("registry"); }
        public static List<McpTool> GetVisibleTools() { return new List<McpTool>(); }
        public static int GetDefaultToolInfoCount() { return 7; }
        public static bool TryGetTool(string name, out McpTool tool) { tool = ServerTools.ControlServer(); return true; }
    }
    public static class WorldEditorTools { public static void MarkRuntimeDatabaseReady() { FixtureEvents.RuntimeReadyCalls++; } }
    public static class ToolCallSpeechOverlay { public static void EnsureInstance() { FixtureEvents.OverlayCalls++; } }
    public static class CoordinateGridOverlay { public static void EnsureInstance() { FixtureEvents.CoordinateCalls++; } }
    public static class ToolCatalogTools { public static McpTool ControlToolCatalog() { throw new NotSupportedException(); } }
    public static class ToolBatchTools { public static McpTool CallMany() { throw new NotSupportedException(); } }
    public static class AgentProgramTools { public static McpTool ExecuteProgram() { throw new NotSupportedException(); } }
    public static class SpeedTransitionTrace
    {
        internal static readonly string[] Methods = { "Pause", "Unpause", "SetSpeed", "ToggleRidiculousSpeed", "OnChanged" };
        internal sealed class Call { }
        internal static readonly Dictionary<string, string> Coverage = new Dictionary<string, string>();
        internal static void SetCoverage(string name, string state) { Coverage[name] = state; if (name == "OnChanged") FixtureEvents.Add("ensure_speed"); }
        internal static Call Begin(SpeedControlScreen screen, MethodBase method) { return new Call(); }
        internal static void End(Call call, SpeedControlScreen screen, Exception error) { }
    }
}
