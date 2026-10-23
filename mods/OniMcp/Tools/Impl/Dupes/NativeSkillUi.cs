using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEventSystem = UnityEngine.EventSystems.EventSystem;

namespace OniMcp.Tools
{
    internal static class NativeSkillUi
    {
        private static readonly FieldInfo MenuScreen = Bind(typeof(ManagementMenu), "skillsScreen", typeof(SkillsScreen));
        private static readonly FieldInfo ScreenWidgets = Bind(typeof(SkillsScreen), "skillWidgets", typeof(Dictionary<string, GameObject>));
        private static readonly FieldInfo InputDisabled = BindStatic(typeof(KScreenManager), "inputDisabled", typeof(bool));
        private static readonly FieldInfo ScreenStack = Bind(typeof(KScreenManager), "screenStack", typeof(List<KScreen>));
        private static readonly FieldInfo WidgetScreen = Bind(typeof(SkillWidget), "skillsScreen", typeof(SkillsScreen));

        internal sealed class Observation
        {
            internal ManagementMenu Menu;
            internal SkillsScreen Screen;
            internal Dictionary<string, GameObject> Widgets;
            internal bool? ScreenOpen;
            internal bool? TargetMatches;
            internal string Reason;
            internal Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["status"] = Reason == null ? "observed" : "unavailable",
                    ["reasonCode"] = (object)Reason ?? JValue.CreateNull(),
                    ["source"] = "existing_skills_screen_cache",
                    ["screenOpen"] = DuplicantSkillObservation.JsonBoolean(ScreenOpen),
                    ["targetMatches"] = DuplicantSkillObservation.JsonBoolean(TargetMatches)
                };
            }
        }

        private static FieldInfo Bind(Type type, string name, Type valueType)
        {
            try
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                return field != null && field.DeclaringType == type && field.FieldType == valueType && !field.IsStatic ? field : null;
            }
            catch { return null; }
        }

        private static FieldInfo BindStatic(Type type, string name, Type valueType)
        {
            try
            {
                var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
                return field != null && field.DeclaringType == type && field.FieldType == valueType && field.IsStatic ? field : null;
            }
            catch { return null; }
        }

        private static bool InteractionAvailable(SkillsScreen ownScreen, bool requireStackEntry, out string reason)
        {
            reason = null;
            if (InputDisabled == null || ScreenStack == null) { reason = "ui_input_binding_unavailable"; return false; }
            try
            {
                var disabled = InputDisabled.GetValue(null);
                if (!(disabled is bool value)) { reason = "ui_input_state_unavailable"; return false; }
                if (value) { reason = "ui_input_disabled"; return false; }
                var manager = KScreenManager.Instance;
                if (manager == null) { reason = "screen_manager_missing"; return false; }
                var stack = ScreenStack.GetValue(manager) as List<KScreen>;
                if (stack == null) { reason = "screen_stack_missing"; return false; }
                int ownIndex = stack.IndexOf(ownScreen);
                if (requireStackEntry && ownIndex < 0) { reason = "skills_screen_not_in_stack"; return false; }
                for (int i = stack.Count - 1; i > ownIndex; i--)
                {
                    var screen = stack[i];
                    if (screen != null && screen != ownScreen && screen.IsScreenActive() && screen.IsModal())
                    { reason = "foreground_modal_active"; return false; }
                }
                return true;
            }
            catch { reason = "ui_input_state_read_failed"; return false; }
        }

        internal static Observation Read(MinionIdentity target)
        {
            var result = new Observation();
            if (target == null) { result.Reason = "target_required"; return result; }
            if (MenuScreen == null || ScreenWidgets == null || WidgetScreen == null) { result.Reason = "ui_binding_unavailable"; return result; }
            try
            {
                result.Menu = ManagementMenu.Instance;
                if (result.Menu == null) { result.Reason = "management_menu_missing"; return result; }
                result.Screen = MenuScreen.GetValue(result.Menu) as SkillsScreen;
                if (result.Screen == null) { result.Reason = "skills_screen_missing"; return result; }
                result.ScreenOpen = result.Menu.IsScreenOpen(result.Screen);
                var selected = result.Screen.CurrentlySelectedMinion;
                MinionIdentity live = selected as MinionIdentity;
                var proxy = selected as MinionAssignablesProxy;
                if (proxy != null)
                {
                    var go = proxy.GetTargetGameObject();
                    if (go != null) live = go.GetComponent<MinionIdentity>();
                }
                result.TargetMatches = live == target;
                result.Widgets = ScreenWidgets.GetValue(result.Screen) as Dictionary<string, GameObject>;
                if (result.Widgets == null) result.Reason = "skill_widgets_missing";
                else if (result.ScreenOpen != true) result.Reason = "skills_screen_closed";
                else if (result.TargetMatches != true) result.Reason = "ui_target_mismatch";
                else if (result.Screen.gameObject == null || !result.Screen.gameObject.activeInHierarchy) result.Reason = "skills_screen_inactive";
            }
            catch { result.Reason = "ui_cache_read_failed"; }
            return result;
        }

        internal static Dictionary<string, object> ReadWidget(Observation ui, string skillId, out SkillWidget widget)
        {
            widget = null;
            var row = new Dictionary<string, object>
            {
                ["status"] = "unavailable",
                ["reasonCode"] = (object)ui?.Reason ?? JValue.CreateNull(),
                ["widgetPresent"] = JValue.CreateNull(),
                ["activeSelf"] = JValue.CreateNull(),
                ["activeInHierarchy"] = JValue.CreateNull(),
                ["parentMatches"] = JValue.CreateNull(),
                ["skillIdMatches"] = JValue.CreateNull(),
                ["visible"] = JValue.CreateNull()
            };
            if (ui == null || ui.Reason != null) return row;
            try
            {
                bool present = ui.Widgets.TryGetValue(skillId, out var go) && go != null;
                row["widgetPresent"] = present;
                if (!present) { row["status"] = "not_present"; row["reasonCode"] = "skill_widget_missing"; row["visible"] = false; return row; }
                widget = go.GetComponent<SkillWidget>();
                bool parent = widget != null && (WidgetScreen.GetValue(widget) as SkillsScreen) == ui.Screen && go.transform.IsChildOf(ui.Screen.transform);
                bool id = widget != null && widget.skillID == skillId;
                row["activeSelf"] = go.activeSelf;
                row["activeInHierarchy"] = go.activeInHierarchy;
                row["parentMatches"] = parent;
                row["skillIdMatches"] = id;
                row["visible"] = parent && id && go.activeSelf && go.activeInHierarchy;
                row["status"] = parent && id ? "observed" : "unavailable";
                row["reasonCode"] = parent && id ? (object)JValue.CreateNull() : "skill_widget_identity_mismatch";
            }
            catch { widget = null; row["reasonCode"] = "skill_widget_read_failed"; }
            return row;
        }

        private static UnityEventSystem ActiveEventSystem()
        {
            try
            {
                var events = UnityEventSystem.current;
                return events != null && events.isActiveAndEnabled ? events : null;
            }
            catch { return null; }
        }

        internal static bool PointerAvailable() => ActiveEventSystem() != null;

        internal static bool? ReadInstantBuild()
        {
            try { return DebugHandler.InstantBuildMode; }
            catch { return null; }
        }

        internal static bool TryOpen(MinionIdentity target, out Observation observed, out string reason, out bool invoked)
        {
            invoked = false;
            observed = Read(target);
            if (observed.Menu == null || observed.Screen == null)
            { reason = observed.Reason ?? "skills_screen_missing"; return false; }
            if (!InteractionAvailable(observed.Screen, false, out reason)) return false;
            try
            {
                invoked = true;
                observed.Menu.OpenSkills(target);
                observed = Read(target);
                reason = observed.Reason;
                return reason == null;
            }
            catch { observed = Read(target); reason = "native_ui_open_failed"; return false; }
        }

        internal static bool TryClick(Observation observed, MinionIdentity target, string skillId, out string reason, out bool invoked)
        {
            invoked = false;
            reason = null;
            if (ReadInstantBuild() != false) { reason = "instant_build_not_false"; return false; }
            var current = Read(target);
            if (current.Screen != observed.Screen || current.Menu != observed.Menu || current.Reason != null)
            { reason = current.Reason ?? "ui_context_changed"; return false; }
            if (!InteractionAvailable(current.Screen, true, out reason)) return false;
            var row = ReadWidget(current, skillId, out var widget);
            if (widget == null || !(row["visible"] is bool visible) || !visible)
            { reason = "skill_widget_not_visible"; return false; }
            if (target.GetComponent<MinionResume>() == null) { reason = "resume_cache_missing"; return false; }
            var events = ActiveEventSystem();
            if (events == null) { reason = "event_system_unavailable"; return false; }
            if (ReadInstantBuild() != false) { reason = "instant_build_not_false"; return false; }
            if (!InteractionAvailable(current.Screen, true, out reason)) return false;
            if (ActiveEventSystem() != events) { reason = "event_system_unavailable"; return false; }
            try
            {
                invoked = true;
                widget.OnPointerClick(new PointerEventData(events) { button = PointerEventData.InputButton.Left });
                return true;
            }
            catch { reason = "native_skill_click_failed"; return false; }
        }
    }
}
