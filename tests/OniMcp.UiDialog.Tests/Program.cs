using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static readonly List<ConfirmDialogScreen> PreviousScreens = new List<ConfirmDialogScreen>();
    private static readonly string NativeTitle = STRINGS.UI.FRONTEND.MOD_DIALOGS.SAVE_GAME_MODS_DIFFER.TITLE;
    private static readonly string NativeFormat = STRINGS.UI.FRONTEND.MOD_DIALOGS.SAVE_GAME_MODS_DIFFER.MESSAGE;
    private const string NativeMessage = "Native fixture: enabled Example Mod; disabled Earlier Mod";

    private static void Main()
    {
        McpLocalization.LanguageProvider = () => "en";
        TestBindingsAndAbsent();
        TestBirthIdentification();
        TestReadAndRouter();
        TestConsentAndPreview();
        TestTokenGeneration();
        TestNativeGuards();
        TestNativeClickAndPauseLayers();
        TestPartialFailures();
        TestLocalization();
        PauseTraceRegression.Run();
        SavedModFootprintRegression.Run();
        Console.WriteLine("MOD mismatch dialog: " + checks + " host checks passed (actual product adapter/handler/routing; native UI boundary substituted)");
        SelectionRegression.Run();
    }

    private static void Setup(int pauses = 1)
    {
        foreach (var screen in PreviousScreens) { screen.Destroyed = true; screen.gameObject.Destroyed = true; }
        PreviousScreens.Clear();
        KScreenManager.Instance = new KScreenManager(); KScreenManager.Instance.DisableInput(false);
        Global.Instance = new Global(); SpeedControlScreen.Instance = new SpeedControlScreen { PauseCount = pauses };
        UnrelatedDispatchBoundary.Calls = 0;
        Check(NativeModMismatchDialog.NotifyOriginPatch.Prepare() && NativeModMismatchDialog.PopupCapturePatch.Prepare(),
            "installed native method/field shape is available at host boundary");
    }

    private static ConfirmDialogScreen Screen()
    {
        var screen = new ConfirmDialogScreen(); screen.transform.parent = Global.Instance.globalCanvas.transform;
        KScreenManager.Instance.Add(screen); PreviousScreens.Add(screen); return screen;
    }

    private static void Popup(ConfirmDialogScreen screen, string title = null, string message = null,
        System.Action confirm = null, System.Action cancel = null, System.Action third = null)
    {
        string t = title ?? NativeTitle, m = message ?? NativeMessage;
        NativeModMismatchDialog.PopupCapturePatch.Prefix(screen);
        screen.PopupConfirmDialog(m, confirm, cancel, null, third, t, null, null, null);
        NativeModMismatchDialog.PopupCapturePatch.Postfix(screen, m, confirm, cancel, null, third, t, null, null, null);
    }

    private static ConfirmDialogScreen Notify(KMod.Manager manager = null, string title = null, string format = null,
        GameObject parent = null, bool detached = false, System.Action confirm = null, System.Action cancel = null,
        System.Action third = null)
    {
        object state;
        NativeModMismatchDialog.NotifyOriginPatch.Prefix(manager ?? Global.Instance.modManager,
            title ?? NativeTitle, format ?? NativeFormat, parent ?? Global.Instance.globalCanvas, out state);
        try
        {
            var screen = Screen();
            if (detached) screen.transform.parent = new GameObject().transform;
            Popup(screen, title, NativeMessage, confirm, cancel, third); return screen;
        }
        finally { Check(NativeModMismatchDialog.NotifyOriginPatch.Finalizer(null, state) == null, "native notify finalizer preserves success"); }
    }

    private static JObject Body(CallToolResult value)
    { Check(value != null && value.Content.Count == 1, "structured result uses one content block"); return JObject.Parse(value.Content[0].Text); }
    private static JObject Read() { return Body(UiDialogTools.Control(new JObject { ["action"] = "status" })); }
    private static JObject Row(JObject result)
    { Check((int)result["count"] == 1 && ((JArray)result["dialogs"]).Count == 1, "one exact recognized dialog"); return (JObject)result["dialogs"][0]; }
    private static string Token() { return Row(Read())["dialogToken"].Value<string>(); }
    private static JObject Args(string token, bool? confirm = true, bool dryRun = false, bool? keepPaused = null)
    {
        var a = new JObject { ["action"] = "acknowledge", ["dialog"] = "mod_mismatch", ["dialogToken"] = token, ["task"] = "dialog regression fixture" };
        if (confirm.HasValue) a["confirm"] = confirm.Value;
        if (dryRun) a["dryRun"] = true;
        if (keepPaused.HasValue) a["keepPaused"] = keepPaused.Value;
        return a;
    }
    private static void NoWrite(ConfirmDialogScreen screen, int pauseCount, string reason)
    {
        Check((screen.FixtureButton == null || screen.FixtureButton.SignalCalls == 0) && screen.NativeOkCalls == 0 && screen.DeactivateCalls == 0,
            reason + ": no native click, callback or direct close");
        Check(SpeedControlScreen.Instance.PauseCount == pauseCount && SpeedControlScreen.Instance.PauseCalls == 0
            && SpeedControlScreen.Instance.UnpauseCalls == 0, reason + ": no pause mutation");
        Check(KScreenManager.Instance == null || KScreenManager.Instance.Refreshes == 0, reason + ": no native stack refresh");
    }
    private static JObject Reject(JObject args, string expected)
    {
        var response = UiDialogTools.Control(args); var body = Body(response);
        Check(response.IsError && body["ok"].Value<bool>() == false && body["reasonCode"].Value<string>() == expected,
            "expected localized structured refusal: " + expected);
        Check(body["requested"].Value<bool>() == false, "refused request never claims native click");
        return body;
    }

    private static void TestBindingsAndAbsent()
    {
        Setup();
        Check(NativeModMismatchDialog.NotifyOriginPatch.TargetMethod().Name == "NotifyDialog"
            && NativeModMismatchDialog.PopupCapturePatch.TargetMethod().Name == "PopupConfirmDialog", "hooks bind exact native methods");
        var body = Read();
        Check(body["available"].Value<bool>() && !body["present"].Value<bool>() && body["count"].Value<int>() == 0,
            "known absent is distinct from unavailable");
        Reject(Args("no-such-screen"), "not_present");
        Check(SpeedControlScreen.Instance.PauseCalls == 0 && SpeedControlScreen.Instance.UnpauseCalls == 0,
            "absent reads and refusal do not change simulation pause");
        KScreenManager.Instance = null;
        body = Read();
        Check(body["available"].Value<bool>() == false && body["present"].Type == JTokenType.Null,
            "missing native manager remains unavailable, not a fabricated absent result");
    }

    private static void TestBirthIdentification()
    {
        foreach (string mismatch in new[] { "manager", "title", "format", "parent", "detached", "generic" })
        {
            Setup(); ConfirmDialogScreen screen;
            if (mismatch == "generic") { screen = Screen(); Popup(screen); }
            else screen = Notify(manager: mismatch == "manager" ? new KMod.Manager() : null,
                title: mismatch == "title" ? "Other native notification" : null,
                format: mismatch == "format" ? "Other native format {0}" : null,
                parent: mismatch == "parent" ? new GameObject() : null, detached: mismatch == "detached");
            Check(!Read()["present"].Value<bool>(), "a matching caption alone cannot identify origin: " + mismatch);
            NoWrite(screen, 1, "unrecognized origin " + mismatch);
        }
        Setup(); object outer, inner;
        NativeModMismatchDialog.NotifyOriginPatch.Prefix(Global.Instance.modManager, NativeTitle, NativeFormat, Global.Instance.globalCanvas, out outer);
        NativeModMismatchDialog.NotifyOriginPatch.Prefix(new KMod.Manager(), NativeTitle, NativeFormat, Global.Instance.globalCanvas, out inner);
        var ignored = Screen(); Popup(ignored);
        Check(!Read()["present"].Value<bool>(), "nested foreign notify masks an outer recognized origin");
        var exception = new InvalidOperationException("native fixture notify failed");
        Check(ReferenceEquals(NativeModMismatchDialog.NotifyOriginPatch.Finalizer(exception, inner), exception),
            "native finalizer preserves an original exception");
        var recognized = Screen(); Popup(recognized);
        Check(Row(Read())["canAcknowledge"].Value<bool>(), "inner finalizer restores the outer native origin");
        NativeModMismatchDialog.NotifyOriginPatch.Finalizer(null, outer);
        var later = Screen(); Popup(later);
        Check(Read()["count"].Value<int>() == 1, "finalizer clears origin so later generic popup is not captured");
    }

    private static void TestReadAndRouter()
    {
        Setup(); var screen = Notify();
        var before = NativeModMismatchDialog.Read(); var again = NativeModMismatchDialog.Read();
        Check(JToken.DeepEquals(before, again), "repeated reads preserve the same token and complete snapshot");
        var row = Row(before); Guid identity;
        Check(Guid.TryParseExact(row["dialogToken"].Value<string>(), "N", out identity), "generation token is opaque identity, not a caption or instance index");
        Check(row["title"].Value<string>() == NativeTitle && row["message"].Value<string>() == NativeMessage
            && row["confirmLabel"].Value<string>() == "OK", "native localized display text is returned without copied prose or formatting rewrite");
        var input = new JObject { ["domain"] = "ui", ["uiDomain"] = "dialog", ["action"] = "status", ["dialog"] = "mod_mismatch", ["task"] = "fixture" };
        string unchanged = input.ToString();
        var routed = Body(GameControlTools.ControlGame().Handler(input));
        Check(JToken.DeepEquals(routed, Read()) && input.ToString() == unchanged && UnrelatedDispatchBoundary.Calls == 0,
            "actual public game/UI routing preserves child result and never mutates caller arguments");
        var kindAlias = new JObject { ["domain"] = "ui", ["kind"] = "dialog", ["action"] = "status", ["task"] = "fixture" };
        Check(JToken.DeepEquals(Body(GameControlTools.ControlGame().Handler(kindAlias)), Read()), "existing ui-domain-from-kind forwarding remains compatible");
        var tool = GameControlTools.ControlGame();
        Check(tool.Parameters["uiDomain"].EnumValues.Contains("dialog") && tool.Parameters["dialog"].EnumValues.SequenceEqual(new[] { "mod_mismatch" })
            && tool.Parameters["dialogToken"].Type == "string" && tool.Parameters["keepPaused"].Type == "boolean", "public metadata advertises the narrow supported dialog and typed controls");
        NoWrite(screen, 1, "readonly public route");
    }

    private static void TestConsentAndPreview()
    {
        Setup(); var screen = Notify(); string token = Token();
        Reject(Args(token, false), "confirm_required"); Reject(Args(token, null), "confirm_required");
        var missing = Args(token); missing.Remove("dialogToken"); Reject(missing, "token_required");
        foreach (JToken bad in new JToken[] { 17, JValue.CreateNull(), new JArray("token"), new JObject(), " " })
        { var a = Args(token); a["dialogToken"] = bad; Reject(a, "token_required"); }
        foreach (string flag in new[] { "confirm", "dryRun", "keepPaused" })
        {
            foreach (JToken bad in new JToken[] { "true", 1, JValue.CreateNull(), new JArray(true), new JObject() })
            { var a = Args(token); a[flag] = bad; var failure = Reject(a, "invalid_boolean"); Check(failure["parameter"].Value<string>() == flag, "invalid boolean identifies its exact parameter"); }
        }
        var action = Args(token); action["action"] = "close_all"; Reject(action, "unsupported_action");
        var kind = Args(token); kind["dialog"] = "generic"; Reject(kind, "unsupported_dialog");
        foreach (bool keep in new[] { true, false })
        {
            var response = UiDialogTools.Control(Args(token, false, true, keep)); var preview = Body(response);
            Check(!response.IsError && preview["preview"].Value<bool>() && preview["wouldAcknowledge"].Value<bool>()
                && !preview["requested"].Value<bool>() && !preview["closed"].Value<bool>(), "confirmed target preview does not impersonate actual acknowledgment");
        }
        NoWrite(screen, 1, "all withheld/invalid/preview controls");
        var outer = Args(token, false); outer["domain"] = "ui"; outer["uiDomain"] = "dialog";
        Check(GameControlTools.ControlGame().Handler(outer).IsError, "actual public forwarding cannot drop withheld confirmation");
        outer["dryRun"] = true; outer["confirm"] = true;
        Check(!GameControlTools.ControlGame().Handler(outer).IsError, "actual public forwarding retains dryRun even when confirmed");
        NoWrite(screen, 1, "outer execution-policy retention");
    }

    private static void TestTokenGeneration()
    {
        Setup(); var first = Notify(); string old = Token();
        first.FixtureButton.ClearOnClick(); object state;
        NativeModMismatchDialog.NotifyOriginPatch.Prefix(Global.Instance.modManager, NativeTitle, NativeFormat, Global.Instance.globalCanvas, out state);
        Popup(first); NativeModMismatchDialog.NotifyOriginPatch.Finalizer(null, state);
        string fresh = Token(); Check(old != fresh, "reusing the same native instance creates a new observation generation");
        Reject(Args(old), "token_stale"); NoWrite(first, 1, "stale generation");
        first.FixtureButton.ClearOnClick(); Popup(first);
        Check(!Read()["present"].Value<bool>(), "foreign reuse invalidates its formerly recognized token");
        Reject(Args(fresh), "not_present"); NoWrite(first, 1, "foreign reused screen");
        Setup(); first = Notify(); old = Token();
        NativeModMismatchDialog.PopupCapturePatch.Prefix(first);
        Check(!Read()["present"].Value<bool>(), "reuse start invalidates previous identity even if native Popup later throws");
        Reject(Args(old), "not_present"); NoWrite(first, 1, "incomplete popup reuse");
        Setup(); first = Notify(); old = Token(); first.Deactivate(); SpeedControlScreen.Instance.UnpauseCalls = 0; var second = Notify();
        Reject(Args(old), "token_stale"); NoWrite(second, 0, "stale closed screen cannot acknowledge replacement");
    }

    private static void TestNativeGuards()
    {
        foreach (string name in new[] { "inactive", "inactive_go", "destroyed", "unspawned_screen", "unspawned_button", "disabled_button", "inactive_button",
            "inactive_button_go", "title", "body", "action", "cancel", "third", "deactivate", "binding", "blocking_modal", "input_disabled", "missing_manager", "missing_speed", "button_replaced", "deactivate_callback", "extra_click_callback", "extra_key_callback", "duplicate_native_callback" })
        {
            Setup(); var screen = Notify(); string token = Token(); string expected;
            switch (name)
            {
                case "inactive": screen.Active = false; expected = "not_present"; break;
                case "inactive_go": screen.gameObject.SetActive(false); expected = "not_present"; break;
                case "destroyed": screen.Destroyed = true; expected = "not_present"; break;
                case "unspawned_screen": screen.isSpawned = false; expected = "unavailable"; break;
                case "unspawned_button": screen.FixtureButton.isSpawned = false; expected = "button_unavailable"; break;
                case "disabled_button": screen.FixtureButton.isInteractable = false; expected = "button_unavailable"; break;
                case "inactive_button": screen.FixtureButton.isActiveAndEnabled = false; expected = "button_unavailable"; break;
                case "inactive_button_go": screen.FixtureConfirmObject.SetActive(false); expected = "button_unavailable"; break;
                case "title": screen.FixtureTitle = "Different notification"; expected = "unsupported_shape"; break;
                case "body": screen.FixtureMessage = "Different body"; expected = "unsupported_shape"; break;
                case "action": screen.FixtureSetAction(() => { throw new Exception("foreign action must never run"); }); expected = "unsupported_shape"; break;
                case "cancel": screen.FixtureCancelObject.SetActive(true); expected = "unsupported_shape"; break;
                case "third": screen.FixtureConfigObject.SetActive(true); expected = "unsupported_shape"; break;
                case "deactivate": screen.deactivateOnConfirmAction = false; expected = "unsupported_shape"; break;
                case "binding": screen.FixtureButton.ClearOnClick(); expected = "unsupported_shape"; break;
                case "blocking_modal": KScreenManager.Instance.Add(new KScreen()); expected = "blocked"; break;
                case "input_disabled": KScreenManager.Instance.DisableInput(true); expected = "blocked"; break;
                case "missing_manager": KScreenManager.Instance = null; expected = "unavailable"; break;
                case "missing_speed": SpeedControlScreen.Instance = null; expected = "pause_unavailable"; break;
                case "deactivate_callback": screen.onDeactivateCB = () => { throw new Exception("foreign deactivate callback must never run"); }; expected = "unsupported_shape"; break;
                case "extra_click_callback": screen.FixtureButton.onClick += () => { throw new Exception("foreign click callback must never run"); }; expected = "unsupported_shape"; break;
                case "extra_key_callback": screen.FixtureButton.onBtnClick += key => { throw new Exception("foreign key callback must never run"); }; expected = "unsupported_shape"; break;
                case "duplicate_native_callback": screen.FixtureButton.onClick += screen.OnSelect_OK; expected = "unsupported_shape"; break;
                case "button_replaced": typeof(ConfirmDialogScreen).GetField("confirmButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(screen, new GameObject()); expected = "button_unavailable"; break;
                default: throw new Exception(name);
            }
            var result = Reject(Args(token), expected);
            Check(screen.FixtureButton == null || screen.FixtureButton.SignalCalls == 0, name + ": no native button attempt");
            Check(screen.NativeOkCalls == 0 && screen.DeactivateCalls == 0, name + ": no manual close or fallback");
            if (SpeedControlScreen.Instance != null) NoWrite(screen, 1, name);
            if (name != "missing_manager" && name != "missing_speed")
            { var preview = Args(token, false, true); Reject(preview, expected); }
        }
        Setup(); var a = Notify(); var b = Notify();
        var status = Read(); Check(status["count"].Value<int>() == 2 && status["dialogs"].All(d => !d["canAcknowledge"].Value<bool>()), "multiple recognized modals remain ambiguous");
        Reject(Args(status["dialogs"][0]["dialogToken"].Value<string>()), "ambiguous");
        NoWrite(a, 1, "ambiguous first"); NoWrite(b, 1, "ambiguous second");
        Setup(); a = Notify(); KScreenManager.Instance.Add(new KScreen { Modal = false });
        Check(Row(Read())["canAcknowledge"].Value<bool>(), "nonmodal foreground display does not become an invented blocking modal");
    }

    private static void TestNativeClickAndPauseLayers()
    {
        foreach (int initial in new[] { 1, 2, 3 })
        foreach (bool keep in new[] { true, false })
        {
            Setup(initial); var screen = Notify(); string token = Token();
            var response = UiDialogTools.Control(Args(token, true, false, keep)); var result = Body(response);
            int expectedPauseCount = keep ? Math.Max(1, initial - 1) : initial - 1;
            Check(!response.IsError && result["requested"].Value<bool>() && result["closed"].Value<bool>() && !result["partial"].Value<bool>(),
                "actual normal native close is independently observed complete");
            Check(screen.FixtureButton.SignalCalls == 1 && screen.NativeOkCalls == 1 && screen.DeactivateCalls == 1,
                "exact native button and registered OnSelect_OK run once");
            Check(SpeedControlScreen.Instance.PauseCount == expectedPauseCount
                && SpeedControlScreen.Instance.PauseCalls == (keep && initial == 1 ? 1 : 0)
                && SpeedControlScreen.Instance.UnpauseCalls == 1, "logical pause retention does not leak or forcibly remove native pause layers");
            Check(result["pausedAfter"].Value<bool>() == (expectedPauseCount > 0), "pause result reports observed native pause state");
            Check(!Read()["present"].Value<bool>(), "separate status read observes native dialog removal");
            Reject(Args(token), "not_present");
            Check(screen.FixtureButton.SignalCalls == 1, "repeat acknowledgment never replays the native click");
        }
    }

    private static void TestPartialFailures()
    {
        Setup(); var screen = Notify(); screen.IgnoreClose = true; string token = Token();
        var pending = Body(UiDialogTools.Control(Args(token)));
        Check(pending["requested"].Value<bool>() && !pending["closed"].Value<bool>() && pending["partial"].Value<bool>(),
            "accepted click is not claimed as closure when native UI remains active");
        Check(screen.FixtureButton.SignalCalls == 1 && screen.NativeOkCalls == 1, "pending case invokes only normal native callback");
        Check(!Row(Read())["canAcknowledge"].Value<bool>(), "one-use token remains consumed while native close is pending");
        Reject(Args(token), "token_stale"); Check(screen.FixtureButton.SignalCalls == 1, "pending click is not silently retried");

        Setup(); screen = Notify(); token = Token();
        screen.FixtureAfterNativeOk = () => { throw new InvalidOperationException("native fixture close failure"); };
        var response = UiDialogTools.Control(Args(token)); var result = Body(response);
        Check(response.IsError && result["requested"].Value<bool>() && result["closed"].Value<bool>()
            && result["partial"].Value<bool>() && result["reasonCode"].Value<string>() == "click_failed",
            "failure after native close retains requested/observed completion and reports partial error");
        Check(screen.NativeOkCalls == 1 && screen.FixtureButton.SignalCalls == 1 && SpeedControlScreen.Instance.IsPaused,
            "native exception never falls back to another close and still preserves pause");

        Setup(); screen = Notify(); token = Token(); SpeedControlScreen.Instance.ThrowOnPause = true;
        response = UiDialogTools.Control(Args(token)); result = Body(response);
        Check(response.IsError && result["requested"].Value<bool>() && result["closed"].Value<bool>()
            && result["reasonCode"].Value<string>() == "pause_unavailable" && !result["pausedAfter"].Value<bool>(),
            "pause-retention failure preserves real close and does not fabricate paused success");
        Check(screen.NativeOkCalls == 1 && SpeedControlScreen.Instance.PauseCalls == 1, "no pause or click fallback retry after partial failure");

        foreach (string change in new[] { "replaced", "missing", "read_failed" })
        foreach (bool keep in new[] { true, false })
        {
            Setup(); screen = Notify(); token = Token();
            var oldSpeed = SpeedControlScreen.Instance;
            var replacement = new SpeedControlScreen { PauseCount = 0 };
            screen.FixtureAfterNativeOk = () =>
            {
                if (change == "replaced") SpeedControlScreen.Instance = replacement;
                else if (change == "missing") SpeedControlScreen.Instance = null;
                else oldSpeed.ThrowOnPauseRead = true;
            };
            response = UiDialogTools.Control(Args(token, true, false, keep)); result = Body(response);
            Check(response.IsError && result["requested"].Value<bool>() && result["closed"].Value<bool>()
                && result["partial"].Value<bool>() && result["reasonCode"].Value<string>() == "pause_unavailable"
                && result["pausedAfter"].Type == JTokenType.Null,
                "post-click native pause " + change + " is unknown and partial, keepPaused=" + keep);
            Check(screen.NativeOkCalls == 1 && screen.FixtureButton.SignalCalls == 1 && screen.DeactivateCalls == 1,
                "pause observation uncertainty never retries or bypasses native click");
            Check(oldSpeed.PauseCalls == 0 && oldSpeed.UnpauseCalls == 1 && replacement.PauseCalls == 0
                && replacement.UnpauseCalls == 0, "changed or unreadable pause controller is never silently mutated");
        }
    }

    private static void TestLocalization()
    {
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            McpLocalization.LanguageProvider = () => language;
            var a = Args("any", false); var result = Body(UiDialogTools.Control(a));
            string message = result["reasonMessage"].Value<string>();
            Check(message == McpLocalization.Get("ui_dialog.confirm_required") && !message.StartsWith("ui_dialog.", StringComparison.Ordinal),
                "confirmation refusal resolves an embedded catalog in " + language);
            a["confirm"] = "true"; result = Body(UiDialogTools.Control(a));
            Check(result["reasonMessage"].Value<string>().Contains("confirm") && !result["reasonMessage"].Value<string>().Contains("{0}"),
                "typed-boolean error resolves and preserves its placeholder in " + language);
        }
        McpLocalization.LanguageProvider = () => "en";
    }

    private static void Check(bool ok, string message)
    { checks++; if (!ok) throw new InvalidOperationException(message); }
}
