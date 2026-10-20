using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    // All entry points run on the existing Unity main-thread dispatcher.
    // Reflection only reads native state; confirmation uses the actual KButton.
    internal static class NativeModMismatchDialog
    {
        private const string ExecutionPath = "ConfirmDialogScreen.confirmButton.KButton.SignalClick";
        private static readonly NativeBindings Api = NativeBindings.Create();
        private static readonly List<ObservedDialog> Observed = new List<ObservedDialog>();
        [ThreadStatic] private static OriginScope origin;

        private sealed class OriginScope
        {
            internal string Title;
            internal GameObject Parent;
        }

        private sealed class ObservedDialog
        {
            internal WeakReference Screen;
            internal WeakReference ConfirmObject;
            internal string Token;
            internal string Title;
            internal string Message;
            internal bool Consumed;
        }

        internal static JObject Read()
        {
            if (Api == null)
                return Unavailable();
            try
            {
                if (!ManagerReady())
                    return Unavailable();
                List<ObservedDialog> active = ActiveDialogs();
                var dialogs = new JArray();
                foreach (ObservedDialog item in active)
                {
                    ConfirmDialogScreen screen = Screen(item);
                    string reason = Validate(item, active);
                    GameObject buttonObject = Api.ConfirmButton.GetValue(screen) as GameObject;
                    LocText label = buttonObject == null ? null : buttonObject.GetComponentInChildren<LocText>();
                    dialogs.Add(new JObject
                    {
                        ["kind"] = "mod_mismatch", ["dialogToken"] = item.Token,
                        ["title"] = Text(Api.Title, screen), ["message"] = Text(Api.Message, screen),
                        ["confirmLabel"] = label == null ? null : label.text,
                        ["canAcknowledge"] = reason == null,
                        ["reasonCode"] = reason == null ? JValue.CreateNull() : new JValue(reason)
                    });
                }
                return new JObject
                {
                    ["available"] = true, ["present"] = active.Count > 0, ["count"] = active.Count,
                    ["dialogs"] = dialogs, ["executionPath"] = ExecutionPath
                };
            }
            catch (Exception error)
            {
                JObject result = Unavailable();
                result["exceptionMessage"] = error.Message;
                return result;
            }
        }

        internal static JObject Acknowledge(string dialogToken, bool keepPaused, bool dryRun = false)
        {
            if (Api == null)
                return Failure("unavailable");
            if (string.IsNullOrWhiteSpace(dialogToken))
                return Failure("token_required");
            ObservedDialog candidate;
            ConfirmDialogScreen screen;
            KButton button;
            SpeedControlScreen speed;
            bool hadSpeed;
            try
            {
                if (!ManagerReady())
                    return Failure("unavailable");
                List<ObservedDialog> active = ActiveDialogs();
                if (active.Count == 0)
                    return Failure("not_present");
                if (active.Count != 1)
                    return Failure("ambiguous");
                candidate = active[0];
                if (!string.Equals(candidate.Token, dialogToken, StringComparison.Ordinal))
                    return Failure("token_stale");
                string reason = Validate(candidate, active);
                if (reason != null)
                    return Failure(reason);
                screen = Screen(candidate);
                button = ((GameObject)Api.ConfirmButton.GetValue(screen)).GetComponent<KButton>();
                speed = SpeedControlScreen.Instance;
                hadSpeed = speed != null;
                if (keepPaused && !hadSpeed)
                    return Failure("pause_unavailable");
                if (dryRun)
                {
                    return new JObject
                    {
                        ["ok"] = true, ["preview"] = true, ["wouldAcknowledge"] = true,
                        ["requested"] = false, ["closed"] = false, ["keepPaused"] = keepPaused,
                        ["executionPath"] = ExecutionPath
                    };
                }
                // Recheck immediately before consuming the one-use identity and invoking native UI.
                reason = Validate(candidate, ActiveDialogs());
                if (reason != null)
                    return Failure(reason);
            }
            catch (Exception error)
            {
                return Failure("unavailable", false, null, error);
            }

            candidate.Consumed = true;
            Exception clickError = null;
            Exception pauseError = null;
            try
            {
                button.SignalClick(KKeyCode.Mouse0);
            }
            catch (Exception error)
            {
                clickError = error;
            }
            finally
            {
                // A normal modal close releases its pause. Restore logical pause through
                // the ordinary native entry point, without adding duplicate pause layers.
                if (keepPaused)
                {
                    try
                    {
                        SpeedControlScreen current = SpeedControlScreen.Instance;
                        if (current == null || !ReferenceEquals(current, speed))
                            throw new InvalidOperationException("pause_unavailable");
                        if (!current.IsPaused)
                            current.Pause(playSound: false);
                    }
                    catch (Exception error)
                    {
                        pauseError = error;
                    }
                }
            }

            bool? closed = null;
            bool? pausedAfter = null;
            Exception observationError = null;
            try { closed = !IsLive(screen); }
            catch (Exception error) { observationError = error; }
            try
            {
                SpeedControlScreen current = SpeedControlScreen.Instance;
                if (!ReferenceEquals(current, speed) || ((hadSpeed || keepPaused) && current == null))
                    throw new InvalidOperationException("pause_unavailable");
                pausedAfter = current == null ? (bool?)null : current.IsPaused;
            }
            catch (Exception error) { if (pauseError == null) pauseError = error; }
            if (clickError != null || pauseError != null || observationError != null)
            {
                string reason = clickError != null ? "click_failed" : pauseError != null ? "pause_unavailable" : "unavailable";
                JObject failure = Failure(reason, true, closed, clickError ?? pauseError ?? observationError);
                failure["partial"] = true;
                failure["keepPaused"] = keepPaused;
                failure["pausedAfter"] = pausedAfter.HasValue ? new JValue(pausedAfter.Value) : JValue.CreateNull();
                return failure;
            }
            return new JObject
            {
                ["ok"] = true, ["requested"] = true, ["closed"] = closed.Value, ["partial"] = !closed.Value,
                ["keepPaused"] = keepPaused,
                ["pausedAfter"] = pausedAfter.HasValue ? new JValue(pausedAfter.Value) : JValue.CreateNull(),
                ["executionPath"] = ExecutionPath
            };
        }

        private static JObject Unavailable() => new JObject
        {
            ["available"] = false, ["present"] = JValue.CreateNull(), ["count"] = JValue.CreateNull(),
            ["dialogs"] = new JArray(), ["reasonCode"] = "unavailable", ["executionPath"] = ExecutionPath
        };

        private static JObject Failure(string reason, bool requested = false, bool? closed = null, Exception error = null)
        {
            var result = new JObject
            {
                ["ok"] = false, ["reasonCode"] = reason, ["requested"] = requested,
                ["closed"] = closed.HasValue ? new JValue(closed.Value) : JValue.CreateNull(),
                ["executionPath"] = ExecutionPath
            };
            if (error != null)
                result["exceptionMessage"] = error.Message;
            return result;
        }

        private static ConfirmDialogScreen Screen(ObservedDialog entry) => entry.Screen.Target as ConfirmDialogScreen;
        private static bool IsLive(ConfirmDialogScreen screen) => screen != null && screen.gameObject != null
            && screen.gameObject.activeInHierarchy && screen.IsActive() && screen.IsScreenActive();
        private static List<ObservedDialog> ActiveDialogs() => Observed.Where(item => IsLive(Screen(item))).ToList();
        private static string Text(FieldInfo field, ConfirmDialogScreen screen) => (field.GetValue(screen) as LocText)?.text;
        private static bool ManagerReady() => KScreenManager.Instance != null
            && Api.ScreenStack.GetValue(KScreenManager.Instance) is List<KScreen>;

        private static string Validate(ObservedDialog item, List<ObservedDialog> active)
        {
            ConfirmDialogScreen screen = Screen(item);
            if (!IsLive(screen)) return "not_present";
            if (!screen.isSpawned) return "unavailable";
            if (active.Count != 1) return "ambiguous";
            if (!ReferenceEquals(active[0], item) || item.Consumed) return "token_stale";
            if (!string.Equals(Text(Api.Title, screen), item.Title, StringComparison.Ordinal)
                || !string.Equals(Text(Api.Message, screen), item.Message, StringComparison.Ordinal)
                || !screen.deactivateOnConfirmAction
                || Api.ConfirmAction.GetValue(screen) != null || Api.CancelAction.GetValue(screen) != null
                || Api.ConfigurableAction.GetValue(screen) != null || Api.OnDeactivate.GetValue(screen) != null)
                return "unsupported_shape";
            GameObject cancel = Api.CancelButton.GetValue(screen) as GameObject;
            GameObject configurable = Api.ConfigurableButton.GetValue(screen) as GameObject;
            if ((cancel != null && cancel.activeInHierarchy) || (configurable != null && configurable.activeInHierarchy))
                return "unsupported_shape";
            KScreenManager manager = KScreenManager.Instance;
            if (manager == null) return "unavailable";
            if ((bool)Api.InputDisabled.GetValue(null)) return "blocked";
            var stack = Api.ScreenStack.GetValue(manager) as List<KScreen>;
            if (stack == null) return "unavailable";
            KScreen topModal = null;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                KScreen current = stack[i];
                if (current != null && current.IsScreenActive() && current.IsModal())
                {
                    topModal = current;
                    break;
                }
            }
            if (!ReferenceEquals(topModal, screen)) return "blocked";
            GameObject confirm = Api.ConfirmButton.GetValue(screen) as GameObject;
            if (confirm == null || !ReferenceEquals(confirm, item.ConfirmObject.Target) || !confirm.activeInHierarchy)
                return "button_unavailable";
            KButton button = confirm.GetComponent<KButton>();
            if (button == null || !button.isSpawned || !button.isActiveAndEnabled || !button.isInteractable)
                return "button_unavailable";
            var callbacks = Api.ButtonClick.GetValue(button) as System.Action;
            if (callbacks == null || callbacks.GetInvocationList().Length != 1
                || callbacks.GetInvocationList().Any(callback =>
                !ReferenceEquals(callback.Target, screen) || !callback.Method.Equals(Api.SelectOk))
                || Api.ButtonKeyClick.GetValue(button) != null)
                return "unsupported_shape";
            return null;
        }

        private static void ObservePopup(ConfirmDialogScreen screen, string title, string message)
        {
            // A generic native screen can be reused. Its old token must never survive reuse.
            Observed.RemoveAll(item => ReferenceEquals(Screen(item), screen) || Screen(item) == null);
            if (Api == null || origin == null || screen == null || !string.Equals(title, origin.Title, StringComparison.Ordinal))
                return;
            if (screen.transform.parent == null || origin.Parent == null
                || !screen.transform.IsChildOf(origin.Parent.transform))
                return;
            GameObject confirm = Api.ConfirmButton.GetValue(screen) as GameObject;
            if (confirm == null || !string.Equals(Text(Api.Title, screen), title, StringComparison.Ordinal)
                || !string.Equals(Text(Api.Message, screen), message, StringComparison.Ordinal))
                return;
            Observed.Add(new ObservedDialog
            {
                Screen = new WeakReference(screen), ConfirmObject = new WeakReference(confirm),
                Token = Guid.NewGuid().ToString("N"), Title = title, Message = message
            });
        }

        private sealed class NativeBindings
        {
            internal FieldInfo Title, Message, ConfirmButton, CancelButton, ConfigurableButton;
            internal FieldInfo ConfirmAction, CancelAction, ConfigurableAction, OnDeactivate, ScreenStack, InputDisabled, ButtonClick, ButtonKeyClick;
            internal MethodInfo Notify, Popup, SelectOk;
            private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            private static FieldInfo Field(Type type, string name, Type expected, bool isStatic = false)
            {
                FieldInfo field = type.GetField(name, isStatic ? BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic : Instance);
                return field != null && field.FieldType == expected && field.IsStatic == isStatic ? field : null;
            }

            internal static NativeBindings Create()
            {
                try
                {
                    var value = new NativeBindings
                    {
                        Title = Field(typeof(ConfirmDialogScreen), "titleText", typeof(LocText)),
                        Message = Field(typeof(ConfirmDialogScreen), "popupMessage", typeof(LocText)),
                        ConfirmButton = Field(typeof(ConfirmDialogScreen), "confirmButton", typeof(GameObject)),
                        CancelButton = Field(typeof(ConfirmDialogScreen), "cancelButton", typeof(GameObject)),
                        ConfigurableButton = Field(typeof(ConfirmDialogScreen), "configurableButton", typeof(GameObject)),
                        ConfirmAction = Field(typeof(ConfirmDialogScreen), "confirmAction", typeof(System.Action)),
                        CancelAction = Field(typeof(ConfirmDialogScreen), "cancelAction", typeof(System.Action)),
                        ConfigurableAction = Field(typeof(ConfirmDialogScreen), "configurableAction", typeof(System.Action)),
                        OnDeactivate = Field(typeof(ConfirmDialogScreen), "onDeactivateCB", typeof(System.Action)),
                        ScreenStack = Field(typeof(KScreenManager), "screenStack", typeof(List<KScreen>)),
                        InputDisabled = Field(typeof(KScreenManager), "inputDisabled", typeof(bool), true),
                        ButtonClick = Field(typeof(KButton), "onClick", typeof(System.Action)),
                        ButtonKeyClick = Field(typeof(KButton), "onBtnClick", typeof(System.Action<KKeyCode>)),
                        Notify = typeof(KMod.Manager).GetMethod("NotifyDialog", Instance, null,
                            new[] { typeof(string), typeof(string), typeof(GameObject) }, null),
                        Popup = typeof(ConfirmDialogScreen).GetMethod("PopupConfirmDialog", Instance, null,
                            new[] { typeof(string), typeof(System.Action), typeof(System.Action), typeof(string), typeof(System.Action),
                                typeof(string), typeof(string), typeof(string), typeof(Sprite) }, null),
                        SelectOk = typeof(ConfirmDialogScreen).GetMethod("OnSelect_OK", Instance, null, Type.EmptyTypes, null)
                    };
                    if (new[] { value.Title, value.Message, value.ConfirmButton, value.CancelButton, value.ConfigurableButton,
                        value.ConfirmAction, value.CancelAction, value.ConfigurableAction, value.OnDeactivate, value.ScreenStack,
                        value.InputDisabled, value.ButtonClick, value.ButtonKeyClick }.Any(field => field == null)
                        || value.Notify == null || value.Popup == null || value.SelectOk == null
                        || value.Notify.ReturnType != typeof(void) || value.Popup.ReturnType != typeof(void)
                        || value.SelectOk.ReturnType != typeof(void))
                        return null;
                    return value;
                }
                catch
                {
                    return null;
                }
            }
        }

        [HarmonyPatch]
        internal static class NotifyOriginPatch
        {
            [HarmonyPrepare] internal static bool Prepare() => Api != null;
            [HarmonyTargetMethod] internal static MethodBase TargetMethod() => Api?.Notify;
            [HarmonyPrefix]
            internal static void Prefix(KMod.Manager __instance, string title, string message_format, GameObject parent, out object __state)
            {
                __state = origin;
                origin = null;
                try
                {
                    Global global = Global.Instance;
                    if (Api != null && global != null && ReferenceEquals(global.modManager, __instance)
                        && parent != null && ReferenceEquals(parent, global.globalCanvas)
                        && string.Equals(title, (string)global::STRINGS.UI.FRONTEND.MOD_DIALOGS.SAVE_GAME_MODS_DIFFER.TITLE, StringComparison.Ordinal)
                        && string.Equals(message_format, (string)global::STRINGS.UI.FRONTEND.MOD_DIALOGS.SAVE_GAME_MODS_DIFFER.MESSAGE, StringComparison.Ordinal))
                        origin = new OriginScope { Title = title, Parent = parent };
                }
                catch { origin = null; }
            }
            [HarmonyFinalizer]
            internal static Exception Finalizer(Exception __exception, object __state)
            {
                origin = __state as OriginScope;
                return __exception;
            }
        }

        [HarmonyPatch]
        internal static class PopupCapturePatch
        {
            [HarmonyPrepare] internal static bool Prepare() => Api != null;
            [HarmonyTargetMethod] internal static MethodBase TargetMethod() => Api?.Popup;
            [HarmonyPrefix]
            internal static void Prefix(ConfirmDialogScreen __instance)
            {
                Observed.RemoveAll(item => ReferenceEquals(Screen(item), __instance));
            }
            [HarmonyPostfix]
            internal static void Postfix(ConfirmDialogScreen __instance, string text, System.Action on_confirm,
                System.Action on_cancel, string configurable_text, System.Action on_configurable_clicked,
                string title_text, string confirm_text, string cancel_text, Sprite image_sprite)
            {
                try { ObservePopup(__instance, title_text, text); }
                catch { Observed.RemoveAll(item => ReferenceEquals(Screen(item), __instance)); }
            }
        }
    }
}
