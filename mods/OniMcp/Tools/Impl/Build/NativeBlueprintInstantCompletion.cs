using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Server;
using UnityEngine;

namespace OniMcp.Tools
{
    // Only the native tool writes game state. No deletion, direct build or retry path.
    internal static class NativeBlueprintInstantCompletion
    {
        internal const string ExecutionPath = "BuildTool.Activate/SetToolOrientation/OnMouseMove/TryBuild";
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo TryBuild = typeof(BuildTool).GetMethod("TryBuild", Instance, null, new[] { typeof(int) }, null);
        private static readonly FieldInfo SelectedElements = typeof(BuildTool).GetField("selectedElements", Instance);
        private static readonly FieldInfo ScreenStack = typeof(KScreenManager).GetField("screenStack", Instance);
        private static readonly FieldInfo InputDisabled = typeof(KScreenManager).GetField("inputDisabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        internal sealed class Failure : InvalidOperationException
        {
            internal readonly string Reason;
            internal Failure(string reason) : base(McpLocalization.Get("blueprint_completion." + reason)) { Reason = reason; }
        }

        internal sealed class Outcome
        {
            internal bool Available, ActivationAttempted, ActionAttempted;
            internal bool? UiRetargeted = false, UiRestored;
            internal string ReasonCode, ExceptionType, CleanupExceptionType;
            internal Dictionary<string, object> ToDictionary() => new Dictionary<string, object>
            {
                ["requested"] = true, ["executionPath"] = ExecutionPath,
                ["available"] = Available, ["activationAttempted"] = ActivationAttempted,
                ["actionAttempted"] = ActionAttempted,
                ["uiRetargeted"] = Nullable(UiRetargeted), ["uiRestored"] = Nullable(UiRestored),
                ["uiRestorationScope"] = "active_tool_only",
                ["selectionRestored"] = JValue.CreateNull(), ["overlayRestored"] = JValue.CreateNull(),
                ["reasonCode"] = ReasonCode == null ? (object)JValue.CreateNull() : ReasonCode,
                ["exceptionType"] = ExceptionType == null ? (object)JValue.CreateNull() : ExceptionType,
                ["cleanupExceptionType"] = CleanupExceptionType == null ? (object)JValue.CreateNull() : CleanupExceptionType
            };
            private static object Nullable(bool? value) => value.HasValue ? (object)value.Value : JValue.CreateNull();
        }

        internal static Outcome Run(BuildingDef def, IList<Tag> elements, string facadeId,
            int cell, Orientation orientation, System.Action revalidate)
        {
            var result = new Outcome();
            BuildTool tool = null;
            PlayerController controller = null;
            SelectTool select = null;
            GameObject ownedPreview = null;
            BuildingDef ownedPreviewDef = null;
            Game game = null;
            int generation = -1;
            try
            {
                tool = BuildTool.Instance;
                controller = PlayerController.Instance;
                select = SelectTool.Instance;
                RequireReady(tool, controller, select);
                RequireIdle(tool, controller, select);
                if (TryBuild == null || TryBuild.ReturnType != typeof(void) || TryBuild.IsStatic
                    || SelectedElements?.FieldType != typeof(IList<Tag>) || SelectedElements.IsStatic
                    || def == null || elements == null || elements.Count == 0 || revalidate == null)
                    throw new Failure("ui_unavailable");
                var expectedElements = new List<Tag>(elements);
                revalidate();
                game = Game.Instance;
                generation = GameContextLifecycle.CaptureGeneration();
                result.Available = true;
                result.ActivationAttempted = true;
                result.UiRetargeted = null;
                tool.Activate(def, new List<Tag>(expectedElements), facadeId);
                ownedPreview = tool.visualizer;
                ownedPreviewDef = ownedPreview == null ? null : ownedPreview.GetComponent<Building>()?.Def;
                RequireActive(tool, controller, select);
                result.UiRetargeted = ReferenceEquals(controller.ActiveTool, tool);
                tool.SetToolOrientation(orientation);
                RequireActive(tool, controller, select);
                tool.OnMouseMove(Grid.CellToPosCBC(cell, def.SceneLayer));
                RequireActive(tool, controller, select);
                if (tool.visualizer == null || Grid.PosToCell(tool.visualizer) != cell
                    || tool.GetLastCell != cell || tool.GetBuildingOrientation != orientation
                    || !ReferenceEquals(tool.visualizer.GetComponent<Building>()?.Def, def)
                    || !FacadeEquivalent(tool.visualizer.GetComponent<BuildingFacade>(), facadeId))
                    throw new Failure("preview_unavailable");
                revalidate();
                if (!SelectedElementsMatch(tool, expectedElements)) throw new Failure("preview_unavailable");
                // This is a void entry point. Completion is inspected separately by the caller.
                result.ActionAttempted = true;
                TryBuild.Invoke(tool, new object[] { cell });
            }
            catch (Exception error)
            {
                var invocation = error as TargetInvocationException;
                Exception actual = invocation?.InnerException ?? error;
                result.ReasonCode = (actual as Failure)?.Reason ?? "native_failed";
                result.ExceptionType = actual.GetType().Name;
            }
            finally
            {
                if (result.ActivationAttempted)
                {
                    try
                    {
                        if (controller == null || select == null || game == null || game.IsLoading()
                            || !ReferenceEquals(Game.Instance, game)
                            || generation != GameContextLifecycle.CaptureGeneration()
                            || !ReferenceEquals(PlayerController.Instance, controller)
                            || !ReferenceEquals(SelectTool.Instance, select))
                            throw new Failure("cleanup_failed");
                        if (ReferenceEquals(controller.ActiveTool, select))
                            result.UiRestored = true;
                        else if (ReferenceEquals(controller.ActiveTool, tool) && ReferenceEquals(BuildTool.Instance, tool)
                            && ownedPreview != null && ReferenceEquals(tool.visualizer, ownedPreview)
                            && ReferenceEquals(ownedPreviewDef, def)
                            && ReferenceEquals(ownedPreview.GetComponent<Building>()?.Def, ownedPreviewDef))
                        {
                            controller.ActivateTool(select);
                            result.UiRestored = ReferenceEquals(controller.ActiveTool, select);
                        }
                        else throw new Failure("cleanup_failed");
                        if (result.UiRestored != true) throw new Failure("cleanup_failed");
                    }
                    catch (Exception error)
                    {
                        result.UiRestored = null;
                        result.CleanupExceptionType = error.GetType().Name;
                        if (result.ReasonCode == null) result.ReasonCode = "cleanup_failed";
                    }
                }
            }
            return result;
        }

        private static bool SelectedElementsMatch(BuildTool tool, IList<Tag> expected)
        {
            try
            {
                var actual = SelectedElements.GetValue(tool) as IList<Tag>;
                if (actual == null || actual.Count != expected.Count) return false;
                for (int i = 0; i < expected.Count; i++) if (actual[i] != expected[i]) return false;
                return true;
            }
            catch { return false; }
        }

        internal static bool FacadeEquivalent(BuildingFacade actual, string expectedId)
        {
            if (actual == null) return false;
            bool expectedOriginal = string.IsNullOrWhiteSpace(expectedId)
                || string.Equals(expectedId, "DEFAULT_FACADE", StringComparison.Ordinal);
            if (expectedOriginal) return actual.IsOriginal
                || string.Equals(actual.CurrentFacade, "DEFAULT_FACADE", StringComparison.Ordinal);
            return !actual.IsOriginal && string.Equals(actual.CurrentFacade, expectedId, StringComparison.Ordinal);
        }

        private static void RequireReady(BuildTool tool, PlayerController controller, SelectTool select)
        {
            if (tool == null || controller == null || select == null || Game.Instance == null
                || Game.Instance.IsLoading() || PlanScreen.Instance == null || World.Instance == null
                || ResourceRemainingDisplayScreen.instance == null || GridCompositor.Instance == null
                || World.Instance.blockTileRenderer == null || tool.GetComponent<BuildToolHoverTextCard>() == null
                || KScreenManager.Instance == null || ScreenStack?.FieldType != typeof(List<KScreen>) || ScreenStack.IsStatic
                || InputDisabled?.FieldType != typeof(bool) || !InputDisabled.IsStatic)
                throw new Failure("ui_unavailable");
            if (SpeedControlScreen.Instance == null || !SpeedControlScreen.Instance.IsPaused)
                throw new Failure("pause_required");
            if ((bool)InputDisabled.GetValue(null)) throw new Failure("ui_busy");
            var stack = ScreenStack.GetValue(KScreenManager.Instance) as List<KScreen>;
            if (stack == null) throw new Failure("ui_unavailable");
            foreach (KScreen screen in stack)
                if (screen != null && screen.IsScreenActive() && screen.IsModal())
                    throw new Failure("ui_busy");
        }

        private static void RequireIdle(BuildTool tool, PlayerController controller, SelectTool select)
        {
            if (!ReferenceEquals(controller.ActiveTool, select) || controller.IsDragging() || tool.Dragging)
                throw new Failure("ui_busy");
        }

        private static void RequireActive(BuildTool tool, PlayerController controller, SelectTool select)
        {
            RequireReady(tool, controller, select);
            if (!ReferenceEquals(BuildTool.Instance, tool) || !ReferenceEquals(PlayerController.Instance, controller)
                || !ReferenceEquals(SelectTool.Instance, select) || !ReferenceEquals(controller.ActiveTool, tool)
                || controller.IsDragging() || tool.Dragging)
                throw new Failure("ui_busy");
        }
    }
}
