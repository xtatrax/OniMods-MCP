using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class ScheduleAcknowledgementRegression
{
    private static int checks;
    private static void Check(bool value, string name)
    { checks++; if (!value) throw new InvalidOperationException("schedule_ack: " + name); }
    private static void Reset()
    {
        GameInputMapping.Entries = new[] { new BindingEntry { mAction = Action.ManageSchedule, mKeyCode = KKeyCode.A, mButton = GamepadButton.NumButtons } };
        GameInputMapping.ThrowGet = false;
        GameInputMapping.Reads = GameInputMapping.FindCalls = GameInputMapping.MissingAsserts = GameInputMapping.CompareCalls = 0;
        Game.Instance = new Game(); KInputManager.isFocused = true;
        KScreenManager.Instance = new KScreenManager();
        ScheduleScreen.Instance = new ScheduleScreen { FixtureActiveAndEnabled = false };
        ManagementMenu.Instance = new ManagementMenu { scheduleScreen = ScheduleScreen.Instance };
        Components.RoleStations.Clear(); DebugHandler.InstantBuildMode = false;
        DlcManager.Cluster = false; ManagementMenu.SpaceReady = false;
        McpLocalization.LanguageProvider = () => "en";
    }
    private static JObject Args(string action = "open_management")
    { return new JObject { ["domain"] = "action", ["action"] = action, ["screen"] = "schedule", ["confirm"] = true, ["task"] = "Observe this exact schedule UI request without fallback or retry." }; }
    private static CallToolResult Call(JObject args)
    { return UiControlTools.ControlUi().Handler(args); }
    private static JObject Parse(CallToolResult value)
    { return JObject.Parse(value.Value); }
    private static void NoNative(KScreenManager manager, ManagementMenu menu, ScheduleScreen screen)
    {
        Check(manager.Down == 0 && manager.Up == 0, "no native key pair");
        Check(GameInputMapping.Reads == 0 && GameInputMapping.FindCalls == 0 && GameInputMapping.MissingAsserts == 0, "no native binding inspection or lookup");
        NoFallback(menu, screen);
    }
    private static void NoFallback(ManagementMenu menu, ScheduleScreen screen)
    {
        Check(menu.DirectCalls == 0 && menu.FixtureShowCalls == 0 && menu.FixtureActivateCalls == 0
            && screen.FixtureShowCalls == 0 && screen.FixtureActivateCalls == 0 && screen.FixtureRefreshCalls == 0,
            "no direct native open/repair/refresh fallback");
    }
    private static void OpenFixture(ManagementMenu menu, ScheduleScreen screen)
    { menu.FixtureReportedOpenScreen = screen; screen.FixtureActiveAndEnabled = true; }
    private static void ReadAndPreview()
    {
        foreach (bool open in new[] { false, true })
        {
            Reset(); var manager = KScreenManager.Instance; var menu = ManagementMenu.Instance; var screen = ScheduleScreen.Instance;
            if (open) OpenFixture(menu, screen);
            var args = Args("management_status"); args.Remove("confirm"); KInputManager.isFocused = false;
            var result = Call(args); var value = Parse(result);
            Check(!result.IsError && (string)value["status"] == "observed" && (bool?)value["observedOpen"] == open, "passive known native open/closed state");
            Check((bool?)value["dispatched"] == false && (bool?)value["stateAvailable"] == true, "status does not dispatch");
            Check((value["opened"] != null) == open, "opened field follows native observed state only");
            Check(ReferenceEquals(menu.FixtureReportedOpenScreen, open ? screen : null) && screen.FixtureActiveAndEnabled == open, "passive screen facts unchanged");
            NoNative(manager, menu, screen);
        }
        Reset(); var m = KScreenManager.Instance; var mm = ManagementMenu.Instance; var ss = ScheduleScreen.Instance;
        var preview = Args(); preview["dryRun"] = true; preview.Remove("confirm");
        var p = Parse(Call(preview)); Check((string)p["status"] == "preview" && (bool?)p["dryRun"] == true && (bool?)p["observedOpen"] == false, "preview uses native passive state only");
        NoNative(m, mm, ss);
        Reset(); m = KScreenManager.Instance; mm = ManagementMenu.Instance; ss = ScheduleScreen.Instance; OpenFixture(mm, ss); KInputManager.isFocused = false;
        p = Parse(Call(Args())); Check((bool?)p["alreadyOpen"] == true && (bool?)p["observedOpen"] == true && (string)p["opened"] == "schedule", "already-open no-op avoids toggle while unfocused");
        NoNative(m, mm, ss);
        var again = Parse(Call(Args())); Check((bool?)again["alreadyOpen"] == true && mm.FixtureReportedOpenScreen == ss, "repeated already-open request stays open"); NoNative(m, mm, ss);
    }
    private static void RefusedPreviews()
    {
        foreach (bool unfocused in new[] { false, true })
        {
            Reset(); var manager = KScreenManager.Instance; var menu = ManagementMenu.Instance; var screen = menu.scheduleScreen;
            if (unfocused) KInputManager.isFocused = false; else Game.Instance = null;
            var args = Args(); args["dryRun"] = true; args.Remove("confirm");
            string request = args.ToString();
            bool priorFocus = KInputManager.isFocused; var priorGame = Game.Instance;
            var result = Call(args); var value = Parse(result);
            Check(result.IsError && (bool?)value["dryRun"] == true && (bool?)value["dispatched"] == false,
                "refused preview retains requested dryRun flag");
            Check(value["opened"] == null && args.ToString() == request,
                "refused preview does not invent opened or rewrite request");
            Check(KInputManager.isFocused == priorFocus && ReferenceEquals(Game.Instance, priorGame)
                && menu.FixtureReportedOpenScreen == null && !screen.FixtureActiveAndEnabled,
                "refused preview leaves native-shaped focus, owner and screen facts unchanged");
            NoNative(manager, menu, screen);
        }
    }
    public static void RefusedPreviewProbe() { RefusedPreviews(); }
    private static void StrictInputs()
    {
        foreach (string action in new[] { "open_management", "management_status" })
            foreach (string flag in new[] { "confirm", "dryRun" })
            foreach (JToken invalid in new JToken[] { JValue.CreateNull(), new JValue("true"), new JValue(1), new JValue(0.5), new JObject(), new JArray() })
            {
                Reset(); var manager = KScreenManager.Instance; var menu = ManagementMenu.Instance; var screen = ScheduleScreen.Instance;
                var args = Args(action); args[flag] = invalid.DeepClone(); var result = Call(args); var value = Parse(result);
                Check(result.IsError && (string)value["reasonCode"] == "invalid_boolean" && (string)value["parameter"] == flag && (bool?)value["dispatched"] == false, "strict boolean " + flag);
                NoNative(manager, menu, screen);
            }
        foreach (bool omit in new[] { false, true })
        {
            Reset(); var m = KScreenManager.Instance; var menu = ManagementMenu.Instance; var s = ScheduleScreen.Instance;
            var args = Args(); if (omit) args.Remove("confirm"); else args["confirm"] = false;
            var result = Call(args); Check(result.IsError && (string)Parse(result)["reasonCode"] == "confirm_required", "execute requires explicit true confirm"); NoNative(m, menu, s);
        }
        foreach (JToken invalid in new JToken[] { JValue.CreateNull(), new JValue(false), new JValue(1), new JValue("research"), new JObject() })
        {
            Reset(); var m = KScreenManager.Instance; var menu = ManagementMenu.Instance; var s = ScheduleScreen.Instance;
            var args = Args("management_status"); args["screen"] = invalid.DeepClone(); var result = Call(args);
            Check(result.IsError && (string)Parse(result)["reasonCode"] == "invalid_screen", "passive status supports exact schedule string only"); NoNative(m, menu, s);
        }
        Reset(); var empty = Args("management_status"); empty.Remove("screen"); Check(Call(empty).IsError, "status missing screen rejected");
    }
    private static void PreRefusals()
    {
        System.Action[] changes = {
            () => Game.Instance = null,
            () => ManagementMenu.Instance = null,
            () => KScreenManager.Instance = null,
            () => ManagementMenu.Instance.scheduleScreen = null,
            () => ManagementMenu.Instance.scheduleScreen = new ScheduleScreen { gameObject = null },
            () => ManagementMenu.Instance.gameObject = null,
            () => ScheduleScreen.Instance.gameObject = null,
            () => ManagementMenu.Instance.FixtureActiveAndEnabled = false,
            () => ManagementMenu.Instance.gameObject.FixtureActiveInHierarchy = false,
            () => ScheduleScreen.Instance.FixtureActiveAndEnabled = true,
            () => ScheduleScreen.Instance.FixtureActiveRead = () => true,
            () => ManagementMenu.Instance.FixtureOpenRead = screen => { throw new InvalidOperationException("fixture read unavailable"); }
        };
        foreach (var change in changes)
        {
            Reset(); var m = KScreenManager.Instance; var menu = ManagementMenu.Instance; var s = ScheduleScreen.Instance;
            change(); var result = Call(Args()); var value = Parse(result);
            Check(result.IsError && (string)value["status"] == "unavailable" && value["opened"] == null && (bool?)value["dispatched"] == false && value["observedOpen"].Type == JTokenType.Null, "unavailable/foreign/inconsistent owner refuses");
            var statusResult = Call(Args("management_status")); var statusValue = Parse(statusResult);
            Check(statusResult.IsError && (bool?)statusValue["stateAvailable"] == false
                && statusValue["observedOpen"].Type == JTokenType.Null && statusValue["opened"] == null,
                "passive unavailable/foreign/inconsistent owner remains unknown, not closed");
            NoNative(m, menu, s);
        }
        Reset(); var manager = KScreenManager.Instance; var mm = ManagementMenu.Instance; var ss = ScheduleScreen.Instance; KInputManager.isFocused = false;
        var denied = Call(Args()); Check(denied.IsError && (string)Parse(denied)["reasonCode"] == "not_focused", "closed unfocused screen is refused"); NoNative(manager, mm, ss);
    }
    private static void PublicMenuScreenIsAuthoritative()
    {
        foreach (bool missingStatic in new[] { true, false })
        {
            Reset(); var manager = KScreenManager.Instance; var menu = ManagementMenu.Instance; var screen = menu.scheduleScreen;
            // Native first-init/load lifecycle may leave the static slot absent or pointing elsewhere.
            var staticSlot = missingStatic ? null : new ScheduleScreen { FixtureActiveAndEnabled = true };
            ScheduleScreen.Instance = staticSlot;
            var status = Call(Args("management_status")); var observed = Parse(status);
            Check(!status.IsError && (bool?)observed["stateAvailable"] == true && (bool?)observed["menuOwnsSchedule"] == true,
                "existing public menu screen remains available without a matching static singleton");
            Check((bool?)observed["observedOpen"] == false && observed["opened"] == null,
                "authoritative menu target is known closed rather than unknown");
            NoNative(manager, menu, screen);
            var args = Args(); args["dryRun"] = true;
            var preview = Call(args); var predicted = Parse(preview);
            Check(!preview.IsError && (string)predicted["status"] == "preview" && (bool?)predicted["dryRun"] == true
                && (bool?)predicted["observedOpen"] == false, "authoritative menu target permits a callback-free preview");
            NoNative(manager, menu, screen);
            manager.FixtureDown = e => OpenFixture(menu, screen);
            var opened = Call(Args()); var result = Parse(opened);
            Check(!opened.IsError && manager.Down == 1 && manager.Up == 1 && (bool?)result["dispatched"] == true,
                "authoritative menu target receives one ordinary native pair");
            Check((bool?)result["observedOpen"] == true && (string)result["opened"] == "schedule",
                "post native public menu target is observed open");
            Check(ReferenceEquals(menu.scheduleScreen, screen) && ReferenceEquals(ScheduleScreen.Instance, staticSlot),
                "MCP never repairs or replaces the public target or static singleton slot");
            NoFallback(menu, screen);
        }
    }
    private static void OneNativeDispatch()
    {
        foreach (bool effect in new[] { false, true })
        foreach (bool explicitFalse in new[] { false, true })
        {
            Reset(); var m = KScreenManager.Instance; var menu = ManagementMenu.Instance; var s = ScheduleScreen.Instance;
            m.FixtureDown = e => { Check(e.Action == Action.ManageSchedule && e.Type == InputEventType.KeyDown, "native exact schedule event"); if (effect) OpenFixture(menu, s); };
            var args = Args(); if (explicitFalse) args["dryRun"] = false;
            var result = Call(args); var value = Parse(result);
            Check(!result.IsError && m.Down == 1 && m.Up == 1 && GameInputMapping.FindCalls == 2, "one existing native down/up dispatch pair");
            Check((bool?)value["dispatched"] == true && (bool?)value["observedOpen"] == effect && (string)value["status"] == (effect ? "observed" : "pending"), "native dispatch distinguished from observation");
            Check((value["opened"] != null) == effect, "no-effect input does not claim opened");
            if (!effect) Check((string)value["reasonCode"] == "not_observed_open", "pending reason is explicit");
            NoFallback(menu, s);
        }
    }
    private static void PostUnknown()
    {
        System.Action[] changes = {
            () => Game.Instance = new Game(),
            () => ManagementMenu.Instance = new ManagementMenu { scheduleScreen = ScheduleScreen.Instance },
            () => ManagementMenu.Instance.scheduleScreen = new ScheduleScreen { FixtureActiveAndEnabled = false },
            () => KScreenManager.Instance = new KScreenManager(),
            () => ManagementMenu.Instance.FixtureOpenRead = screen => { throw new InvalidOperationException("post read unavailable"); },
            () => ScheduleScreen.Instance.FixtureActiveAndEnabled = true
        };
        foreach (var change in changes)
        {
            Reset(); var m = KScreenManager.Instance; var menu = ManagementMenu.Instance; var s = ScheduleScreen.Instance;
            m.FixtureDown = e => change(); var result = Call(Args()); var value = Parse(result);
            Check(result.IsError && (string)value["status"] == "partial" && (bool?)value["dispatched"] == true, "post owner/read inconsistency remains partial");
            Check(value["opened"] == null && value["observedOpen"].Type == JTokenType.Null && m.Down == 1 && m.Up == 1, "post unknown gets one pair and no invented observed open");
            NoFallback(menu, s);
        }
        foreach (bool down in new[] { false, true })
        {
            Reset(); var m = KScreenManager.Instance; var menu = ManagementMenu.Instance; var s = ScheduleScreen.Instance;
            System.Action<KButtonEvent> fail = e => { throw new InvalidOperationException("native partial dispatch"); };
            if (down) m.FixtureDown = fail; else m.FixtureUp = fail;
            var result = Call(Args()); var value = Parse(result);
            Check(result.IsError && (string)value["status"] == "partial" && (string)value["reasonCode"] == "dispatch_failed", "native exception classified partial");
            Check(value["dispatched"].Type == JTokenType.Null && value["observedOpen"].Type == JTokenType.Null && value["opened"] == null, "partial dispatch count/outcome stay unknown");
            Check(m.Down == 1 && m.Up == (down ? 0 : 1), "native exception sends no retry or fallback pair"); NoFallback(menu, s);
        }
    }
    private static void RepeatAndBindingBoundaries()
    {
        foreach (string domain in new[] { "action", "ui_action", "actions" })
        {
            Reset(); var manager = KScreenManager.Instance; var menu = ManagementMenu.Instance; var screen = menu.scheduleScreen;
            var args = Args("management_status"); args["domain"] = domain; args["screen"] = " ScHeDuLe "; args.Remove("confirm");
            string before = args.ToString();
            for (int i = 0; i < 3; i++)
            {
                var result = Call(args); var value = Parse(result);
                Check(!result.IsError && (bool?)value["observedOpen"] == false && value["opened"] == null,
                    "repeated middle-router status preserves closed observation for " + domain);
                Check(args.ToString() == before && menu.FixtureReportedOpenScreen == null && !screen.FixtureActiveAndEnabled,
                    "passive repeat leaves request and screen state intact");
                NoNative(manager, menu, screen);
            }
        }
        foreach (bool unknown in new[] { false, true })
        {
            Reset(); var manager = KScreenManager.Instance; var menu = ManagementMenu.Instance; var screen = menu.scheduleScreen;
            GameInputMapping.Entries = unknown ? null : new BindingEntry[0];
            var preview = Args(); preview["dryRun"] = true;
            var result = Call(preview); var value = Parse(result);
            Check(!result.IsError && (string)value["status"] == "preview" && (bool?)value["dispatched"] == false,
                "preview only observes current screen and does not invent binding eligibility");
            NoNative(manager, menu, screen);
            result = Call(Args()); value = Parse(result);
            Check(result.IsError && (string)value["reasonCode"] == (unknown ? "binding_unknown" : "unbound")
                && (bool?)value["dispatched"] == false && value["opened"] == null,
                "existing shared binding guard still refuses schedule dispatch");
            Check(manager.Down == 0 && manager.Up == 0 && GameInputMapping.FindCalls == 0,
                "binding refusal has no native event or asserting lookup");
            NoFallback(menu, screen);
        }
        Reset(); var m = KScreenManager.Instance; var mm = ManagementMenu.Instance; var ss = mm.scheduleScreen;
        m.FixtureDown = e => { OpenFixture(mm, ss); Game.Instance = new Game(); };
        var partial = Parse(Call(Args()));
        Check((string)partial["status"] == "partial" && partial["observedOpen"].Type == JTokenType.Null
            && partial["opened"] == null && (bool?)partial["dispatched"] == true,
            "even a native-looking open cannot prove completion after owner replacement");
        Check(m.Down == 1 && m.Up == 1, "owner replacement does not trigger a second request");
        NoFallback(mm, ss);
    }
    private static void Localization()
    {
        string[] keys = { "ui_action.management_open_description", "ui_action.control_description", "ui_action.control_action_description", "ui_action.management_screen_description", "ui_action.control_invalid_action", "ui_action.schedule_invalid_request", "ui_action.schedule_unavailable", "ui_action.schedule_not_focused", "ui_action.schedule_pending", "ui_action.schedule_partial" };
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            Reset(); McpLocalization.LanguageProvider = () => language;
            foreach (string key in keys) Check(McpLocalization.Get(key) != key && !string.IsNullOrWhiteSpace(McpLocalization.Get(key)), "actual runtime key resolves " + language + ":" + key);
            Check(UiTools.ControlUiAction().Parameters["action"].EnumValues.Contains("management_status"), "aggregate schema exposes status");
            Check(UiControlTools.ControlUi().Parameters["screen"].Description == McpLocalization.GetForLanguage(language, "ui_action.management_screen_description"), "whole middle aggregate uses actual localized screen metadata");
            Check(McpLocalization.GetForLanguage(language, "tools.game_control.parameters.screen") == McpLocalization.GetForLanguage(language, "ui_action.management_screen_description"), "public game_control screen catalog alias matches the actual canonical translation");
            var actionDescription = UiControlTools.ControlUi().Parameters["action"].Description;
            Check(actionDescription == McpLocalization.GetForLanguage(language, "ui_dialog.ui_action_description")
                && actionDescription != "ui_dialog.ui_action_description" && actionDescription.Contains("management_status"),
                "updated whole middle aggregate action description resolves from its actual runtime catalog");
            KInputManager.isFocused = false; var denied = Parse(Call(Args()));
            Check((string)denied["message"] == McpLocalization.GetForLanguage(language, "ui_action.schedule_not_focused"), "actual localized focus refusal");
        }
    }
    // Shared old API only: old whole UiTools compiles, then falsely acknowledges the no-effect pair.
    public static void OldAcknowledgementProbe()
    {
        Reset(); var manager = KScreenManager.Instance;
        var result = UiTools.OpenManagementScreen().Handler(Args()); var value = Parse(result);
        Check(!result.IsError && manager.Down == 1 && manager.Up == 1, "old probe reaches a valid native pair");
        Check(value["opened"] == null, "compiled_schedule_false_opened");
    }
    public static void Run()
    {
        ReadAndPreview(); RefusedPreviews(); StrictInputs(); PreRefusals(); PublicMenuScreenIsAuthoritative(); OneNativeDispatch(); PostUnknown(); Localization();
        RepeatAndBindingBoundaries();
        Console.WriteLine("Schedule acknowledgement regression PASS: " + checks + " conditions");
    }
}
