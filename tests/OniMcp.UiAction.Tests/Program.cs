using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OniMcp;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException(message); }
    private static BindingEntry Row(Action action, KKeyCode key = KKeyCode.A, GamepadButton button = GamepadButton.NumButtons)
    { return new BindingEntry { mAction = action, mKeyCode = key, mButton = button }; }
    private static void Reset(params BindingEntry[] entries)
    {
        GameInputMapping.Entries = entries; GameInputMapping.ThrowGet = false;
        GameInputMapping.Reads = GameInputMapping.FindCalls = GameInputMapping.MissingAsserts = GameInputMapping.CompareCalls = 0;
        KScreenManager.Instance = new KScreenManager(); ManagementMenu.Instance = new ManagementMenu();
        Components.RoleStations.Clear(); DebugHandler.InstantBuildMode = false;
        Game.Instance = new Game(); DlcManager.Cluster = false; ManagementMenu.SpaceReady = false;
        McpLocalization.LanguageProvider = () => "en";
    }
    private static string State(Action action)
    {
        var method = typeof(ToolMenuHotkeySafety).GetMethod("GetBindingState");
        Check(method != null, "product binding-state API must exist");
        return (string)method.Invoke(null, new object[] { action });
    }
    private static JObject RowInfo(Action action)
    {
        var result = UiTools.ListUiActions().Handler(new JObject());
        Check(!result.IsError, "listing must succeed");
        return (JObject)JObject.Parse(result.Value)["actions"].Single(x => (string)x["name"] == action.ToString());
    }
    private static CallToolResult Trigger(Action action)
    { return UiTools.TriggerUiAction().Handler(new JObject { ["action"] = action.ToString() }); }
    private static JObject Denied(CallToolResult result, string reason)
    {
        Check(result.IsError, "unavailable action must be an MCP error");
        var value = JObject.Parse(result.Value);
        Check((bool?)value["dispatched"] == false && (string)value["reasonCode"] == reason, "refusal retains dispatched=false and precise reason");
        Check(KScreenManager.Instance.Down == 0 && KScreenManager.Instance.Up == 0 && GameInputMapping.FindCalls == 0,
            "refusal sends no native events or asserting lookup");
        return value;
    }
    private static void BindingRegression()
    {
        Reset(Row(Action.ManageVitals));
        Check(!ToolMenuHotkeySafety.IsBoundAction(Action.BuildCategoryTiles), "missing in-range native action is unbound");
        Check(!ToolMenuHotkeySafety.SafeCompareActionKeyCodes(Action.BuildCategoryTiles, Action.ManageVitals), "missing action must not enter native comparison");
        Check(GameInputMapping.FindCalls == 0 && GameInputMapping.MissingAsserts == 0, "binding guard must use array read, never asserting FindEntry");
    }
    private static void DispatchRegression()
    {
        Reset(Row(Action.ManageVitals));
        Denied(Trigger(Action.BuildCategoryTiles), "unbound");
    }
    private static void Run()
    {
        BindingRegression(); DispatchRegression();
        Reset(Row(Action.ManageVitals), Row(Action.Escape, KKeyCode.None, GamepadButton.A));
        Check(State(Action.ManageVitals) == "bound" && State(Action.Escape) == "bound", "keyboard and controller bindings are eligible");
        Check(State(Action.BuildCategoryTiles) == "unbound", "absent array row is unbound");
        foreach (var action in new[] { Action.Invalid, Action.NumActions, (Action)(-9), (Action)999 })
            Check(State(action) == "invalid", "sentinel and out-of-range actions are invalid");
        Check(GameInputMapping.FindCalls == 0, "state inspection remains a pure native-array read");
        Reset(Row(Action.BuildCategoryTiles, KKeyCode.None));
        Check(State(Action.BuildCategoryTiles) == "unbound", "present row without key or button remains unbound");
        Denied(Trigger(Action.BuildCategoryTiles), "unbound");

        foreach (bool throws in new[] { false, true })
        {
            Reset(); GameInputMapping.Entries = null; GameInputMapping.ThrowGet = throws;
            Check(State(Action.BuildCategoryTiles) == "unknown", "null or throwing native mapping remains unknown");
            var row = RowInfo(Action.BuildCategoryTiles);
            Check((string)row["bindingState"] == "unknown" && row["available"] != null && row["available"].Type == JTokenType.Null,
                "serialized unknown availability must remain explicit JSON null");
            Denied(Trigger(Action.BuildCategoryTiles), "binding_unknown");
        }
        Reset(Row(Action.BuildCategoryTiles));
        var snapshot = (BindingEntry[])GameInputMapping.Entries.Clone();
        var info = RowInfo(Action.BuildCategoryTiles);
        Check((bool?)info["available"] == true && (string)info["readinessState"] == "ready", "bound ready whitelist row is available");
        Check(snapshot.SequenceEqual(GameInputMapping.Entries) && GameInputMapping.FindCalls == 0 && KScreenManager.Instance.Down == 0,
            "listing does not mutate bindings or dispatch UI");
        var success = Trigger(Action.BuildCategoryTiles);
        Check(!success.IsError && (bool?)JObject.Parse(success.Value)["dispatched"] == true, "success reports native dispatch only");
        Check(KScreenManager.Instance.Down == 1 && KScreenManager.Instance.Up == 1 && GameInputMapping.MissingAsserts == 0,
            "bound action delivers exactly one down/up pair");
        Check(JObject.Parse(success.Value)["opened"] == null, "dispatch result does not claim screen completion");
        GameInputMapping.Entries = new BindingEntry[0];
        KScreenManager.Instance = new KScreenManager(); GameInputMapping.FindCalls = 0;
        Denied(Trigger(Action.BuildCategoryTiles), "unbound");

        Reset(Row(Action.ManageResearch)); ManagementMenu.Instance.ThrowResearch = true;
        var unknown = RowInfo(Action.ManageResearch);
        Check((string)unknown["readinessState"] == "unknown" && unknown["available"].Type == JTokenType.Null, "unknown readiness remains explicit null");
        Denied(Trigger(Action.ManageResearch), "readiness_unknown");
        Reset(Row(Action.ManageResearch)); ManagementMenu.Instance.ResearchReady = false;
        Denied(Trigger(Action.ManageResearch), "not_ready");
        Reset(Row(Action.ManageVitals)); ManagementMenu.Instance = null;
        Check((bool?)RowInfo(Action.ManageVitals)["available"] == false, "absent management UI is unavailable");
        Denied(Trigger(Action.ManageVitals), "not_ready");
        Reset(Row(Action.ManageVitals)); KScreenManager.Instance = null;
        var missingManager = Trigger(Action.ManageVitals);
        Check(missingManager.IsError && (bool?)JObject.Parse(missingManager.Value)["dispatched"] == false && GameInputMapping.FindCalls == 0,
            "absent event manager cannot dispatch");
        Reset(Row(Action.ManageSkills));
        Denied(Trigger(Action.ManageSkills), "not_ready");
        Components.RoleStations.Add(new object());
        Check(!Trigger(Action.ManageSkills).IsError, "native skill station readiness permits dispatch");
        Reset(Row(Action.ManageStarmap)); DlcManager.Cluster = true;
        Check(!Trigger(Action.ManageStarmap).IsError, "DLC readiness branch permits dispatch");

        Reset();
        Denied(UiTools.OpenManagementScreen().Handler(new JObject { ["screen"] = "vitals" }), "unbound");
        Reset(Row(Action.ManageVitals));
        Check(!UiTools.OpenManagementScreen().Handler(new JObject { ["screen"] = "vitals" }).IsError && KScreenManager.Instance.Down == 1,
            "management dispatcher fallback shares bound-action success");
        Reset();
        Check(!UiTools.OpenManagementScreen().Handler(new JObject { ["screen"] = "research" }).IsError && ManagementMenu.Instance.DirectCalls == 1 && GameInputMapping.FindCalls == 0,
            "existing direct native management entry avoids hotkey dependency");
        Reset();
        Denied(UiTools.ControlUiAction().Handler(new JObject { ["action"] = "trigger", ["uiAction"] = "BuildCategoryTiles" }), "unbound");
        Reset(Row(Action.BuildCategoryTiles));
        Check(!UiTools.ControlUiAction().Handler(new JObject { ["action"] = "trigger", ["ui_action"] = "buildcategorytiles" }).IsError,
            "integrated route and case-insensitive action alias preserve dispatch contract");

        Reset(Row(Action.ManageVitals), Row(Action.Escape));
        Check(ToolMenuHotkeySafety.SafeCompareActionKeyCodes(Action.ManageVitals, Action.Escape) && GameInputMapping.CompareCalls == 1,
            "two bound actions preserve native comparison");
        bool compared = true;
        Check(!GameInputMapping_CompareActionKeyCodes_Patch.Prefix(Action.BuildCategoryTiles, Action.Escape, ref compared) && !compared,
            "Harmony compare prefix rejects missing rows before original invocation");
        string text = "seed";
        Check(!GameUtil_GetHotkeyString_Patch.Prefix(Action.BuildCategoryTiles, ref text) && text == "", "missing hotkey text bypasses native lookup");
        text = "seed";
        Check(!GameUtil_ReplaceHotkeyString_Patch.Prefix("x{Hotkey}{hotkey}{HOTKEY}y", Action.BuildCategoryTiles, ref text) && text == "xy",
            "unbound replacement preserves surrounding template text");
        Check(GameUtil_GetHotkeyString_Patch.Prefix(Action.ManageVitals, ref text), "bound hotkey text retains native route");
        Check(GameInputMapping.MissingAsserts == 0, "all product guards avoid native missing-action assertions");
        foreach (var language in new[] { "en", "ja", "zh-Hans" })
        {
            Reset(Row(Action.ManageVitals));
            McpLocalization.LanguageProvider = () => language;
            var denied = Denied(Trigger(Action.BuildCategoryTiles), "unbound");
            Check((string)denied["message"] == McpLocalization.GetForLanguage(language, "ui_action.unbound") && (string)denied["message"] != "ui_action.unbound",
                "localized rejection resolves in each runtime language");
        }
    }
    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--probe=bindings")) BindingRegression();
            else if (args.Contains("--probe=dispatch")) DispatchRegression();
            else Run();
            Console.WriteLine("UI action binding regression PASS: " + checks + " conditions"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.ToString()); return 1; }
    }
}
