using System;
using System.Collections.Generic;
using System.Reflection;
using Klei.AI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UiEventSystem = UnityEngine.EventSystems.EventSystem;
using UiPointerEventData = UnityEngine.EventSystems.PointerEventData;

namespace OniMcp.Tools
{
    // Observe existing native UI caches. Only Select, Close and Scroll invoke native UI callbacks.
    internal static class NativeDuplicantSelectionUi
    {
        private static readonly FieldInfo ExistingDb = Bind(typeof(Db), "_Instance", typeof(Db), true);
        private static readonly FieldInfo InputDisabled = Bind(typeof(KScreenManager), "inputDisabled", typeof(bool), true);
        private static readonly FieldInfo ScreenStack = Bind(typeof(KScreenManager), "screenStack", typeof(List<KScreen>));
        private static readonly FieldInfo ActiveManagement = Bind(typeof(ManagementMenu), "activeScreen", typeof(ManagementMenu.ScreenData));
        private static readonly FieldInfo SimpleTarget = Bind(typeof(TargetPanel), "selectedTarget", typeof(GameObject));
        private static readonly FieldInfo VitalsPanel = Bind(typeof(SimpleInfoScreen), "vitalsPanel", typeof(MinionVitalsPanel));
        private static readonly FieldInfo VitalsTarget = Bind(typeof(MinionVitalsPanel), "lastSelectedEntity", typeof(GameObject));
        private static readonly FieldInfo CloseButton = Bind(typeof(DetailsScreen), "CloseButton", typeof(KButton));
        private static readonly FieldInfo ButtonClick = Bind(typeof(KButton), "onClick", typeof(System.Action));
        private static readonly FieldInfo ButtonKeyClick = Bind(typeof(KButton), "onBtnClick", typeof(Action<KKeyCode>));
        private static readonly MethodInfo CloseCallback = BindCloseCallback();

        private sealed class Context
        {
            internal Game Game;
            internal MinionIdentity Target;
            internal KSelectable Selectable;
            internal SelectTool Select;
            internal PlayerController Controller;
            internal ClusterManager Cluster;
            internal KScreenManager Manager;
            internal DetailsScreen Details;
            internal SimpleInfoScreen Simple;
            internal List<KScreen> Screens;
            internal KButton Close;
            internal int WorldId;
            internal int Cell;
        }

        private sealed class ScrollContext
        {
            internal Context Ui;
            internal MinionVitalsPanel Panel;
            internal GameObject Line;
            internal LocText Label;
            internal ScrollRect Scroll;
            internal RectTransform Content;
            internal RectTransform Viewport;
            internal UiEventSystem Events;
        }

        private static FieldInfo Bind(Type owner, string name, Type valueType, bool isStatic = false)
        {
            try
            {
                var field = owner.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                    | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
                return field != null && field.DeclaringType == owner && field.FieldType == valueType
                    && field.IsStatic == isStatic ? field : null;
            }
            catch { return null; }
        }

        private static MethodInfo BindCloseCallback()
        {
            try
            {
                var method = typeof(DetailsScreen).GetMethod("DeselectAndClose",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                return method != null && method.DeclaringType == typeof(DetailsScreen)
                    && method.ReturnType == typeof(void) && !method.IsStatic ? method : null;
            }
            catch { return null; }
        }

        private static JObject Base(int id) => new JObject
        {
            ["ok"] = true, ["status"] = "unavailable", ["available"] = false,
            ["id"] = id, ["requested"] = false, ["nativeInvoked"] = false,
            ["source"] = "existing_native_duplicant_selection_ui",
            ["frame"] = Time.frameCount
        };

        private static JObject Missing(int id, string reason, bool pending = false)
        {
            var result = Base(id);
            result["status"] = pending ? "pending" : "unavailable";
            result["reasonCode"] = reason;
            return result;
        }

        private static bool Resolve(int id, out MinionIdentity target, out string reason)
        {
            target = null;
            reason = null;
            try
            {
                var items = Components.LiveMinionIdentities?.Items;
                if (items == null) { reason = "target_cache_unavailable"; return false; }
                int matches = 0;
                foreach (var item in items)
                {
                    if (item == null) continue;
                    var identity = item.GetComponent<KPrefabID>();
                    if (identity == null || identity.InstanceID == 0)
                    { reason = "target_identity_unreadable"; return false; }
                    if (identity.InstanceID == id) { target = item; matches++; }
                }
                if (matches != 1)
                { target = null; reason = matches == 0 ? "target_not_found" : "target_ambiguous"; return false; }
                return true;
            }
            catch { target = null; reason = "target_read_failed"; return false; }
        }

        private static Context Inspect(int id, out string reason)
        {
            reason = null;
            try
            {
                var game = Game.Instance;
                if (game == null || App.IsExiting || App.isLoading || game.IsLoading() || Grid.CellCount <= 0)
                { reason = "game_unavailable"; return null; }
                if (!Resolve(id, out var target, out reason)) return null;
                var selectable = target.GetComponent<KSelectable>();
                if (target.gameObject == null || !target.gameObject.activeInHierarchy || !target.isSpawned
                    || selectable == null || !ReferenceEquals(selectable.gameObject, target.gameObject)
                    || !ReferenceEquals(target.GetComponent<MinionIdentity>(), target) || !selectable.IsSelectable)
                { reason = "target_not_selectable"; return null; }
                int cell = Grid.PosToCell(target);
                if (!Grid.IsValidCell(cell) || !Grid.IsWorldValidCell(cell) || Grid.Visible[cell] <= 0)
                { reason = "target_not_visible"; return null; }
                var cluster = ClusterManager.Instance;
                if (cluster == null || target.GetMyWorldId() != cluster.activeWorldId
                    || selectable.GetMyWorldId() != cluster.activeWorldId || Grid.WorldIdx[cell] != cluster.activeWorldId)
                { reason = "target_wrong_world"; return null; }
                var select = SelectTool.Instance;
                var controller = PlayerController.Instance;
                var details = DetailsScreen.Instance;
                var manager = KScreenManager.Instance;
                if (select == null || controller == null || details == null || manager == null)
                { reason = "ui_unavailable"; return null; }
                if (!select.isSpawned)
                { reason = "selection_tool_not_spawned"; return null; }
                if (!select.isActiveAndEnabled)
                { reason = "selection_tool_inactive"; return null; }
                // Native selection opens the initially hidden panel and lets Start spawn it.
                if (!details.IsInitialized())
                { reason = "details_screen_not_initialized"; return null; }
                if (InputDisabled == null || ScreenStack == null)
                { reason = "ui_binding_unavailable"; return null; }
                if (SpeedControlScreen.Instance == null || !SpeedControlScreen.Instance.IsPaused
                    || !ReferenceEquals(controller.ActiveTool, select) || controller.IsDragging())
                { reason = "ui_busy"; return null; }
                if (!(InputDisabled.GetValue(null) is bool disabled))
                { reason = "ui_state_unreadable"; return null; }
                if (disabled) { reason = "ui_busy"; return null; }
                var screens = ScreenStack.GetValue(manager) as List<KScreen>;
                if (screens == null) { reason = "ui_state_unreadable"; return null; }
                foreach (var screen in screens)
                    if (screen != null && screen.IsScreenActive() && screen.IsModal())
                    { reason = "ui_busy"; return null; }
                if (ManagementMenu.Instance != null)
                {
                    if (ActiveManagement == null) { reason = "ui_state_unreadable"; return null; }
                    if (ActiveManagement.GetValue(ManagementMenu.Instance) != null)
                    { reason = "ui_busy"; return null; }
                }
                var simple = details.GetActiveTab() as SimpleInfoScreen;
                if (simple == null || !simple.transform.IsChildOf(details.transform))
                { reason = "simple_info_tab_unavailable"; return null; }
                return new Context { Game = game, Target = target, Selectable = selectable,
                    Select = select, Controller = controller, Cluster = cluster, Manager = manager,
                    Details = details, Simple = simple, Screens = screens, WorldId = cluster.activeWorldId, Cell = cell };
            }
            catch { reason = "ui_state_unreadable"; return null; }
        }

        private static bool Same(Context first, Context next) => first != null && next != null
            && ReferenceEquals(first.Game, next.Game) && ReferenceEquals(first.Target, next.Target)
            && ReferenceEquals(first.Selectable, next.Selectable) && ReferenceEquals(first.Select, next.Select)
            && ReferenceEquals(first.Controller, next.Controller) && ReferenceEquals(first.Details, next.Details)
            && ReferenceEquals(first.Cluster, next.Cluster) && ReferenceEquals(first.Manager, next.Manager)
            && ReferenceEquals(first.Simple, next.Simple) && first.WorldId == next.WorldId && first.Cell == next.Cell;

        internal static JObject Read(int id)
        {
            var context = Inspect(id, out string reason);
            if (context == null) return Missing(id, reason);
            var result = Base(id);
            bool selected = ReferenceEquals(context.Select.selected, context.Selectable);
            bool detailsTarget = ReferenceEquals(context.Details.target, context.Target.gameObject);
            result["selectionMatches"] = selected;
            result["detailsTargetMatches"] = detailsTarget;
            result["selectionEmpty"] = context.Select.selected == null;
            result["detailsTargetEmpty"] = context.Details.target == null;
            result["detailsScreenActive"] = context.Details.IsScreenActive();
            result["detailsScreenSpawned"] = context.Details.isSpawned;
            result["detailsScreenInitialized"] = context.Details.IsInitialized();
            if (!selected) return WithReason(result, "target_not_selected");
            if (!detailsTarget) return WithReason(result, "details_target_pending", true);
            try
            {
                result["name"] = context.Target.GetProperName();
                result["worldId"] = context.Target.GetMyWorldId();
                if (!context.Details.isSpawned || !context.Details.isActiveAndEnabled || !context.Details.IsScreenActive()
                    || !context.Simple.isActiveAndEnabled || !context.Simple.gameObject.activeInHierarchy
                    || !context.Screens.Contains(context.Details))
                    return WithReason(result, "details_not_displayed", true);
                if (SimpleTarget == null || VitalsPanel == null || VitalsTarget == null || ExistingDb == null)
                    return WithReason(result, "ui_binding_unavailable");
                bool simpleTarget = ReferenceEquals(SimpleTarget.GetValue(context.Simple), context.Target.gameObject);
                result["simpleInfoTargetMatches"] = simpleTarget;
                if (!simpleTarget) return WithReason(result, "simple_info_target_pending", true);
                var panel = VitalsPanel.GetValue(context.Simple) as MinionVitalsPanel;
                if (panel == null || !panel.transform.IsChildOf(context.Simple.transform))
                    return WithReason(result, "vitals_panel_unavailable");
                bool vitalsTarget = ReferenceEquals(VitalsTarget.GetValue(panel), context.Target.gameObject);
                result["vitalsTargetMatches"] = vitalsTarget;
                if (!vitalsTarget) return WithReason(result, "vitals_target_pending", true);
                if (!panel.isActiveAndEnabled || !panel.gameObject.activeInHierarchy)
                    return WithReason(result, "vitals_panel_inactive");
                var db = ExistingDb.GetValue(null) as Db;
                Amount breath = db?.Amounts?.Breath;
                if (breath == null || breath.Id != "Breath" || panel.amountsLines == null)
                    return WithReason(result, "breath_definition_unavailable");
                int matches = 0;
                MinionVitalsPanel.AmountLine line = default(MinionVitalsPanel.AmountLine);
                foreach (var candidate in panel.amountsLines)
                    if (ReferenceEquals(candidate.amount, breath)) { matches++; line = candidate; }
                if (matches != 1) return WithReason(result, matches == 0 ? "breath_line_missing" : "breath_line_ambiguous");
                if (line.go == null || line.locText == null
                    || !line.go.transform.IsChildOf(panel.transform)
                    || !line.locText.transform.IsChildOf(line.go.transform)
                    || !line.go.activeInHierarchy || !line.locText.isActiveAndEnabled)
                    return WithReason(result, "breath_line_unavailable");
                string label = line.locText.text;
                if (string.IsNullOrEmpty(label)) return WithReason(result, "breath_text_pending", true);
                result["status"] = "observed";
                result["available"] = true;
                result["breath"] = new JObject
                {
                    ["amountId"] = "Breath", ["amountIdentityMatches"] = true,
                    ["label"] = label,
                    ["source"] = "owned_MinionVitalsPanel.amountsLines.LocText.text",
                    ["pixelVisibilityVerified"] = false
                };
                return result;
            }
            catch { return WithReason(result, "ui_cache_read_failed"); }
        }

        private static JObject WithReason(JObject value, string reason, bool pending = false)
        {
            value["status"] = pending ? "pending" : "unavailable";
            value["reasonCode"] = reason;
            return value;
        }

        internal static JObject Select(int id, bool dryRun)
        {
            var context = Inspect(id, out string reason);
            if (context == null) return Missing(id, reason);
            var next = Inspect(id, out reason);
            if (!Same(context, next)) return Missing(id, reason ?? "target_changed");
            if (dryRun) return Preview(id, "select", "SelectTool.Select");
            try { context.Select.Select(context.Selectable); }
            catch (Exception error) { return InvocationFailed(id, "select", "SelectTool.Select", error); }
            var after = Inspect(id, out reason);
            if (!Same(context, after)) return ObservationLost(id, "select", "SelectTool.Select", reason);
            var result = Read(id);
            result["action"] = "select";
            result["requested"] = true;
            result["nativeInvoked"] = true;
            result["executionPath"] = "SelectTool.Select";
            result["selectionAccepted"] = ReferenceEquals(context.Select.selected, context.Selectable);
            result["partial"] = !(bool)result["available"];
            if (!(bool)result["selectionAccepted"])
            { result["ok"] = false; result["reasonCode"] = "selection_not_accepted"; }
            return result;
        }

        private static ScrollContext ReadyScroll(int id, Context context, out string reason)
        {
            reason = null;
            try
            {
                // Reuse the complete rendered-cache proof without requiring the row to be unclipped.
                var observed = Read(id);
                if (observed["available"]?.Value<bool>() != true)
                { reason = observed["reasonCode"]?.Value<string>() ?? "scroll_target_unavailable"; return null; }
                var current = Inspect(id, out reason);
                if (!Same(context, current))
                { reason = reason ?? "target_changed"; return null; }
                var panel = VitalsPanel.GetValue(context.Simple) as MinionVitalsPanel;
                var db = ExistingDb.GetValue(null) as Db;
                if (panel == null || !ReferenceEquals(VitalsTarget.GetValue(panel), context.Target.gameObject)
                    || !ReferenceEquals(SimpleTarget.GetValue(context.Simple), context.Target.gameObject)
                    || db?.Amounts?.Breath == null || panel.amountsLines == null)
                { reason = "scroll_target_unavailable"; return null; }
                int lines = 0;
                MinionVitalsPanel.AmountLine line = default(MinionVitalsPanel.AmountLine);
                foreach (var candidate in panel.amountsLines)
                    if (ReferenceEquals(candidate.amount, db.Amounts.Breath)) { lines++; line = candidate; }
                if (lines != 1 || line.go == null || line.locText == null
                    || !line.go.activeInHierarchy || !line.locText.isActiveAndEnabled
                    || !line.go.transform.IsChildOf(panel.transform)
                    || !line.locText.transform.IsChildOf(line.go.transform)
                    || !panel.transform.IsChildOf(context.Simple.transform))
                { reason = "scroll_target_unavailable"; return null; }
                var ancestors = line.go.GetComponentsInParent<ScrollRect>(false);
                if (ancestors == null || ancestors.Length != 1)
                { reason = ancestors != null && ancestors.Length > 1 ? "scroll_region_ambiguous" : "scroll_region_unavailable"; return null; }
                var scroll = ancestors[0];
                if (scroll == null || (scroll.GetType() != typeof(ScrollRect) && scroll.GetType() != typeof(KScrollRect))
                    || !scroll.isActiveAndEnabled || !scroll.gameObject.activeInHierarchy || !scroll.IsActive()
                    || !scroll.vertical || scroll.horizontal || !scroll.transform.IsChildOf(context.Details.transform))
                { reason = "scroll_region_unavailable"; return null; }
                var content = scroll.content;
                // Match native viewRect fallback by reading the own RectTransform, never populating a cache.
                var viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
                if (content == null || viewport == null || ReferenceEquals(content, viewport)
                    || !viewport.IsChildOf(scroll.transform) || !content.IsChildOf(viewport)
                    || !context.Simple.transform.IsChildOf(content) || !line.go.transform.IsChildOf(content))
                { reason = "scroll_owner_mismatch"; return null; }
                var native = scroll as KScrollRect;
                if (native != null && (!native.allowVerticalScrollWheel || native.isDragging))
                { reason = "scroll_region_busy"; return null; }
                var events = UiEventSystem.current;
                if (events == null || !events.isActiveAndEnabled || !events.gameObject.activeInHierarchy)
                { reason = "scroll_input_unavailable"; return null; }
                return new ScrollContext { Ui = current, Panel = panel, Line = line.go, Label = line.locText,
                    Scroll = scroll, Content = content, Viewport = viewport, Events = events };
            }
            catch { reason = "scroll_state_unreadable"; return null; }
        }

        private static bool SameScroll(ScrollContext first, ScrollContext next) => first != null && next != null
            && Same(first.Ui, next.Ui) && ReferenceEquals(first.Panel, next.Panel)
            && ReferenceEquals(first.Line, next.Line) && ReferenceEquals(first.Label, next.Label)
            && ReferenceEquals(first.Scroll, next.Scroll) && ReferenceEquals(first.Content, next.Content)
            && ReferenceEquals(first.Viewport, next.Viewport) && ReferenceEquals(first.Events, next.Events);

        internal static JObject Scroll(int id, bool down, bool dryRun)
        {
            string action = down ? "scroll_down" : "scroll_up";
            const string path = "ScrollRect.OnScroll";
            var context = Inspect(id, out string reason);
            if (context == null) return Missing(id, reason);
            var first = ReadyScroll(id, context, out reason);
            if (first == null) return Missing(id, reason);
            var next = ReadyScroll(id, context, out reason);
            if (!SameScroll(first, next)) return Missing(id, reason ?? "scroll_target_changed");
            float delta = down ? -1f : 1f;
            if (dryRun)
            {
                var preview = Preview(id, action, path);
                preview["wheelDeltaY"] = delta;
                return preview;
            }
            UiPointerEventData data;
            try { data = new UiPointerEventData(first.Events) { scrollDelta = new Vector2(0f, delta) }; }
            catch { return Missing(id, "scroll_event_unavailable"); }
            try { first.Scroll.OnScroll(data); }
            catch (Exception error) { return InvocationFailed(id, action, path, error); }
            var after = ReadyScroll(id, context, out reason);
            if (!SameScroll(first, after)) return ObservationLost(id, action, path, reason ?? "scroll_target_changed");
            var result = Base(id);
            result["action"] = action; result["requested"] = true; result["nativeInvoked"] = true;
            result["executionPath"] = path; result["wheelDeltaY"] = delta;
            result["status"] = "pending"; result["partial"] = true;
            result["reasonCode"] = "scroll_render_pending";
            return result;
        }

        private static bool ReadyClose(Context context, out string reason)
        {
            reason = null;
            try
            {
                if (!ReferenceEquals(context.Select.selected, context.Selectable)
                    || !ReferenceEquals(context.Details.target, context.Target.gameObject))
                { reason = "close_target_mismatch"; return false; }
                if (!context.Details.isSpawned || !context.Details.isActiveAndEnabled || !context.Details.IsScreenActive()
                    || !context.Simple.isActiveAndEnabled || !context.Simple.gameObject.activeInHierarchy
                    || !context.Screens.Contains(context.Details))
                { reason = "details_not_displayed"; return false; }
                if (SimpleTarget == null || !ReferenceEquals(SimpleTarget.GetValue(context.Simple), context.Target.gameObject))
                { reason = "simple_info_target_pending"; return false; }
                if (CloseButton == null || ButtonClick == null || ButtonKeyClick == null || CloseCallback == null
                    || CloseCallback.ReturnType != typeof(void) || CloseCallback.IsStatic)
                { reason = "close_binding_unavailable"; return false; }
                var button = CloseButton.GetValue(context.Details) as KButton;
                if (button == null || !button.isSpawned || !button.isActiveAndEnabled || !button.isInteractable
                    || !button.gameObject.activeInHierarchy || !button.transform.IsChildOf(context.Details.transform))
                { reason = "close_button_unavailable"; return false; }
                var callbacks = ButtonClick.GetValue(button) as System.Action;
                if (callbacks == null || callbacks.GetInvocationList().Length != 1
                    || !ReferenceEquals(callbacks.Target, context.Details) || !callbacks.Method.Equals(CloseCallback)
                    || ButtonKeyClick.GetValue(button) != null)
                { reason = "close_callback_mismatch"; return false; }
                context.Close = button;
                return true;
            }
            catch { reason = "close_state_unreadable"; return false; }
        }

        internal static JObject Close(int id, bool dryRun)
        {
            var context = Inspect(id, out string reason);
            if (context == null || !ReadyClose(context, out reason)) return Missing(id, reason);
            var next = Inspect(id, out reason);
            if (!Same(context, next) || !ReadyClose(next, out reason)
                || !ReferenceEquals(context.Close, next.Close)) return Missing(id, reason ?? "target_changed");
            if (dryRun) return Preview(id, "close", "DetailsScreen.CloseButton.onClick->DetailsScreen.DeselectAndClose");
            try { context.Details.DeselectAndClose(); }
            catch (Exception error) { return InvocationFailed(id, "close", "DetailsScreen.DeselectAndClose", error); }
            try
            {
                var after = Inspect(id, out reason);
                if (!Same(context, after)) return ObservationLost(id, "close", "DetailsScreen.DeselectAndClose", reason);
                bool closed = ReferenceEquals(DetailsScreen.Instance, context.Details)
                    && ReferenceEquals(SelectTool.Instance, context.Select)
                    && context.Select.selected == null && context.Details.target == null
                    && !context.Details.IsScreenActive();
                var result = Base(id);
                result["action"] = "close"; result["requested"] = true; result["nativeInvoked"] = true;
                result["executionPath"] = "DetailsScreen.CloseButton.onClick->DetailsScreen.DeselectAndClose";
                result["closed"] = closed; result["partial"] = !closed;
                result["status"] = closed ? "observed" : "pending";
                result["available"] = closed;
                if (!closed) result["reasonCode"] = "close_pending";
                return result;
            }
            catch (Exception error) { return InvocationFailed(id, "close", "DetailsScreen.DeselectAndClose", error); }
        }

        private static JObject Preview(int id, string action, string path)
        {
            var result = Base(id);
            result["action"] = action; result["status"] = "preview"; result["available"] = true;
            result["dryRun"] = true; result["wouldInvoke"] = true; result["executionPath"] = path;
            return result;
        }

        private static JObject InvocationFailed(int id, string action, string path, Exception error)
        {
            var result = Missing(id, "native_invocation_failed");
            result["ok"] = false; result["action"] = action;
            result["requested"] = true; result["nativeInvoked"] = true; result["partial"] = true;
            result["executionPath"] = path; result["exceptionType"] = error.GetType().FullName;
            return result;
        }

        private static JObject ObservationLost(int id, string action, string path, string observationReason)
        {
            var result = Missing(id, "postcondition_unavailable");
            result["ok"] = false; result["action"] = action;
            result["requested"] = true; result["nativeInvoked"] = true; result["partial"] = true;
            result["executionPath"] = path;
            if (observationReason != null) result["observationReasonCode"] = observationReason;
            return result;
        }
    }
}
