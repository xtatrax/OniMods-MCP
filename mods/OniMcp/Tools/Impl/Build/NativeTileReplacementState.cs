using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OniMcp.Server;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    internal static partial class NativeTileReplacement
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo TryBuild = typeof(BuildTool).GetMethod("TryBuild", Instance, null, new[] { typeof(int) }, null);
        private static readonly FieldInfo SelectedElements = typeof(BuildTool).GetField("selectedElements", Instance);
        private static readonly FieldInfo BuildMenuProduct = typeof(BuildMenu).GetField("productInfoScreen", Instance);
        private static readonly FieldInfo MaterialOnClick = typeof(KToggle).GetField("onClick", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo Selectors = typeof(MaterialSelectionPanel).GetField("materialSelectors", Instance);
        private static readonly FieldInfo ActiveRecipe = typeof(MaterialSelectionPanel).GetField("activeRecipe", Instance);
        private static readonly FieldInfo PriorityButtons = typeof(PriorityScreen).GetField("buttons_basic", Instance);
        private static readonly FieldInfo PriorityHigh = typeof(PriorityScreen).GetField("button_toggleHigh", Instance);
        private static readonly FieldInfo PriorityEmergency = typeof(PriorityScreen).GetField("button_emergency", Instance);
        private static readonly FieldInfo ScreenStack = typeof(KScreenManager).GetField("screenStack", Instance);
        private static readonly FieldInfo InputDisabled = typeof(KScreenManager).GetField("inputDisabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        internal sealed class Failure : InvalidOperationException
        {
            internal readonly string Reason;
            internal Failure(string reason) : base(McpLocalization.Get("tile_replacement." + reason)) { Reason = reason; }
        }

        internal sealed class Outcome
        {
            internal bool Available, UiInitializing, ActivationAttempted, ActionAttempted, PrioritySetAttempted;
            internal bool? UiRestored, PriorityRestored;
            internal string ReasonCode, ExceptionType, CleanupExceptionType;
            internal string InitialOptionalBuildMenuPriorityState = "not_observed";
            internal Dictionary<string, object> MaterialUiReadiness;
            internal Dictionary<string, object> ToDictionary() => new Dictionary<string, object>
            {
                ["executionPath"] = "PlanScreen.CopyBuildingOrder/MaterialSelectionPanel.ForceSelectPrimaryTag/PriorityScreen.SetScreenPriority/BuildTool.TryBuild",
                ["available"] = Available, ["uiInitializing"] = UiInitializing,
                ["initialOptionalBuildMenuPriorityState"] = InitialOptionalBuildMenuPriorityState,
                ["materialUiReadiness"] = MaterialUiReadiness == null ? (object)JValue.CreateNull() : MaterialUiReadiness,
                ["activationAttempted"] = ActivationAttempted, ["actionAttempted"] = ActionAttempted,
                ["prioritySetAttempted"] = PrioritySetAttempted, ["uiRestored"] = Value(UiRestored),
                ["priorityRestored"] = Value(PriorityRestored), ["uiRestorationScope"] = "owned_active_tool_and_known_screen_priority_only",
                ["selectionRestored"] = JValue.CreateNull(), ["overlayRestored"] = JValue.CreateNull(),
                ["historyRestored"] = JValue.CreateNull(), ["reasonCode"] = ReasonCode == null ? (object)JValue.CreateNull() : ReasonCode,
                ["exceptionType"] = ExceptionType == null ? (object)JValue.CreateNull() : ExceptionType,
                ["cleanupExceptionType"] = CleanupExceptionType == null ? (object)JValue.CreateNull() : CleanupExceptionType
            };
        }

        internal static Dictionary<string, object> RequireNormalMode()
        {
            if (Game.Instance == null || Game.Instance.IsLoading()) throw new Failure("target_changed");
            bool debug = DebugHandler.InstantBuildMode;
            bool sandbox = Game.Instance.SandboxModeActive;
            bool? rawSandboxInstant = null;
            if (sandbox)
            {
                if (SandboxToolParameterMenu.instance == null || SandboxToolParameterMenu.instance.settings == null)
                    throw new Failure("ui_unavailable");
                rawSandboxInstant = SandboxToolParameterMenu.instance.settings.InstantBuild;
            }
            if (debug || rawSandboxInstant == true) throw new Failure("instant_mode_enabled");
            return new Dictionary<string, object>
            {
                ["debugInstantBuild"] = debug, ["sandboxActive"] = sandbox,
                ["sandboxInstantBuild"] = Value(rawSandboxInstant),
                ["sandboxInstantState"] = sandbox ? "observed" : "not_applicable", ["effectiveInstantBuild"] = false
            };
        }

        private static void RequireReady(BuildTool tool, PlayerController controller, SelectTool select, PlanScreen plan, Outcome initialObservation = null)
        {
            RequireNormalMode();
            if (tool == null || controller == null || select == null || plan == null
                || ResourceRemainingDisplayScreen.instance == null || GridCompositor.Instance == null
                || World.Instance?.blockTileRenderer == null || tool.GetComponent<BuildToolHoverTextCard>() == null
                || TryBuild == null || TryBuild.ReturnType != typeof(void) || TryBuild.IsStatic
                || SelectedElements?.FieldType != typeof(IList<Tag>) || SelectedElements.IsStatic
                || BuildMenuProduct?.FieldType != typeof(ProductInfoScreen) || BuildMenuProduct.IsStatic
                || MaterialOnClick?.FieldType != typeof(System.Action) || MaterialOnClick.IsStatic
                || Selectors?.FieldType != typeof(List<MaterialSelector>) || Selectors.IsStatic
                || ActiveRecipe?.FieldType != typeof(Recipe) || ActiveRecipe.IsStatic
                || !PriorityBindingsAvailable()
                || KScreenManager.Instance == null || ScreenStack?.FieldType != typeof(List<KScreen>) || ScreenStack.IsStatic
                || InputDisabled?.FieldType != typeof(bool) || !InputDisabled.IsStatic)
                throw new Failure("ui_unavailable");
            var buildMenu = BuildMenu.Instance;
            if (buildMenu == null)
            {
                if (initialObservation != null) initialObservation.InitialOptionalBuildMenuPriorityState = "not_applicable";
            }
            else
            {
                if (initialObservation != null) initialObservation.InitialOptionalBuildMenuPriorityState = "unknown";
                var buildProduct = BuildMenuProduct.GetValue(buildMenu) as ProductInfoScreen;
                var buildPanel = buildProduct?.materialSelectionPanel;
                var buildPriority = buildPanel?.PriorityScreen;
                // This optional native branch only reads cached priority; no spawn/click is required.
                if (buildProduct == null || buildPanel == null || buildPriority == null)
                {
                    if (initialObservation != null) initialObservation.InitialOptionalBuildMenuPriorityState = "unavailable";
                    throw new Failure("ui_initializing");
                }
                if (!KnownPriority(buildPriority.GetLastSelectedPriority())) throw new Failure("priority_unavailable");
                if (initialObservation != null) initialObservation.InitialOptionalBuildMenuPriorityState = "observed";
            }
            if (SpeedControlScreen.Instance == null || !SpeedControlScreen.Instance.IsPaused) throw new Failure("ui_busy");
            if ((bool)InputDisabled.GetValue(null)) throw new Failure("ui_busy");
            var screens = ScreenStack.GetValue(KScreenManager.Instance) as List<KScreen>;
            if (screens == null) throw new Failure("ui_unavailable");
            foreach (var screen in screens)
                if (screen != null && screen.IsScreenActive() && screen.IsModal()) throw new Failure("ui_busy");
        }

        private static MaterialSelectionPanel RequireMaterialUi(PlanScreen plan, BuildingDef def, Tag material, Outcome observation = null)
        {
            var product = plan.ProductInfoScreen;
            var panel = product?.materialSelectionPanel;
            var priority = panel?.PriorityScreen;
            ObserveMaterialUi(product, panel, priority, def, observation);
            if (product == null || panel == null || priority == null || !TryGetReadyPriority(priority, out _))
                throw new Failure("ui_initializing");
            if (product.gameObject == null || panel.gameObject == null
                || !product.gameObject.activeInHierarchy || !panel.gameObject.activeInHierarchy)
                throw new Failure("preview_unavailable");
            if (!ReferenceEquals(product.currentDef, def) || def.CraftRecipe == null
                || !ReferenceEquals(ActiveRecipe.GetValue(panel), def.CraftRecipe))
                throw new Failure("preview_unavailable");
            var selectors = Selectors.GetValue(panel) as List<MaterialSelector>;
            if (selectors == null || selectors.Count != 3 || selectors[0] == null)
                throw new Failure("ui_initializing");
            if (selectors[0].gameObject == null || !selectors[0].gameObject.activeInHierarchy) throw new Failure("preview_unavailable");
            for (int i = 1; i < selectors.Count; i++)
                if (selectors[i] == null || selectors[i].gameObject.activeSelf) throw new Failure("preview_unavailable");
            if (!selectors[0].ElementToggles.TryGetValue(material, out KToggle toggle) || toggle == null
                || !toggle.gameObject.activeInHierarchy || !toggle.IsInteractable()
                || !(MaterialOnClick.GetValue(toggle) is System.Action))
                throw new Failure("preview_unavailable");
            return panel;
        }

        private static bool PriorityBindingsAvailable()
            => PriorityButtons?.FieldType == typeof(List<PriorityButton>) && !PriorityButtons.IsStatic
                && PriorityHigh?.FieldType == typeof(KToggle) && !PriorityHigh.IsStatic
                && PriorityEmergency?.FieldType == typeof(PriorityButton) && !PriorityEmergency.IsStatic;

        private static bool TryGetReadyPriority(PriorityScreen priority, out PrioritySetting value)
        {
            value = default(PrioritySetting);
            if (priority == null || !PriorityBindingsAvailable()) return false;
            var buttons = PriorityButtons.GetValue(priority) as List<PriorityButton>;
            var high = PriorityHigh.GetValue(priority) as KToggle;
            var emergency = PriorityEmergency.GetValue(priority) as PriorityButton;
            if (buttons == null || buttons.Count != 9 || high == null || emergency == null || emergency.toggle == null)
                return false;
            foreach (var button in buttons)
                if (button == null || button.toggle == null || button.tooltip == null) return false;
            value = priority.GetLastSelectedPriority();
            return KnownPriority(value);
        }

        private static void ObserveMaterialUi(ProductInfoScreen product, MaterialSelectionPanel panel,
            PriorityScreen priority, BuildingDef def, Outcome observation)
        {
            if (observation == null) return;
            var state = new Dictionary<string, object>
            {
                ["productPresent"] = product != null, ["productIsSpawned"] = JValue.CreateNull(),
                ["panelPresent"] = Value(product == null ? (bool?)null : panel != null), ["panelIsSpawned"] = JValue.CreateNull(),
                ["priorityPresent"] = Value(panel == null ? (bool?)null : priority != null), ["priorityIsSpawned"] = JValue.CreateNull(),
                ["priorityCacheReady"] = JValue.CreateNull(),
                ["firstSelectorPresent"] = JValue.CreateNull(), ["firstSelectorIsSpawned"] = JValue.CreateNull(),
                ["panelActive"] = JValue.CreateNull(), ["recipeMatches"] = JValue.CreateNull(), ["selectorCount"] = JValue.CreateNull()
            };
            observation.MaterialUiReadiness = state;
            try
            {
                if (product != null) state["productIsSpawned"] = product.isSpawned;
                if (panel != null)
                {
                    state["panelIsSpawned"] = panel.isSpawned;
                    if (panel.gameObject != null) state["panelActive"] = panel.gameObject.activeSelf;
                }
                if (priority != null) state["priorityIsSpawned"] = priority.isSpawned;
                if (priority != null && PriorityBindingsAvailable()) state["priorityCacheReady"] = TryGetReadyPriority(priority, out _);
                if (panel != null && def != null && ActiveRecipe?.FieldType == typeof(Recipe) && !ActiveRecipe.IsStatic)
                    state["recipeMatches"] = def.CraftRecipe != null && ReferenceEquals(ActiveRecipe.GetValue(panel), def.CraftRecipe);
                if (panel != null && Selectors?.FieldType == typeof(List<MaterialSelector>) && !Selectors.IsStatic)
                {
                    var selectors = Selectors.GetValue(panel) as List<MaterialSelector>;
                    if (selectors != null)
                    {
                        state["selectorCount"] = selectors.Count;
                        var first = selectors.Count == 0 ? null : selectors[0];
                        state["firstSelectorPresent"] = first != null;
                        if (first != null) state["firstSelectorIsSpawned"] = first.isSpawned;
                    }
                }
            }
            catch (Exception)
            {
                // Diagnostic read failure leaves unobserved values null and never changes execution guards.
            }
        }

        private static bool SamePriority(PrioritySetting a, PrioritySetting b)
            => a.priority_class == b.priority_class && a.priority_value == b.priority_value;
        private static bool KnownPriority(PrioritySetting setting)
            => setting.priority_class == PriorityScreen.PriorityClass.basic && setting.priority_value >= 1 && setting.priority_value <= 9
                || setting.priority_class == PriorityScreen.PriorityClass.topPriority && setting.priority_value == 1;
        private static bool SelectedMaterialMatches(BuildTool tool, Tag material)
        {
            var selected = SelectedElements.GetValue(tool) as IList<Tag>;
            return selected != null && selected.Count == 1 && selected[0] == material;
        }
        private static object Value(bool? value) => value.HasValue ? (object)value.Value : JValue.CreateNull();
    }
}
