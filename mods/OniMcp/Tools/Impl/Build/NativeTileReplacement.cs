using System;
using OniMcp.Server;
using UnityEngine;

namespace OniMcp.Tools
{
    internal static partial class NativeTileReplacement
    {
        internal static Outcome Run(BuildingDef def, Tag material, string facadeId, int cell,
            int priority, System.Action revalidate)
        {
            var result = new Outcome();
            BuildTool tool = null;
            PlayerController controller = null;
            SelectTool select = null;
            PlanScreen plan = null;
            Game game = null;
            int generation = -1;
            GameObject ownedPreview = null;
            ProductInfoScreen ownedProduct = null;
            MaterialSelectionPanel panel = null, originalPanel = null, ownedPanel = null;
            PriorityScreen priorityScreen = null, originalScreen = null;
            PrioritySetting? originalPriority = null;
            var requestedPriority = new PrioritySetting(PriorityScreen.PriorityClass.basic, priority);
            try
            {
                tool = BuildTool.Instance; controller = PlayerController.Instance;
                select = SelectTool.Instance; plan = PlanScreen.Instance;
                RequireReady(tool, controller, select, plan, result);
                if (def == null || !material.IsValid || priority < 1 || priority > 9 || revalidate == null)
                    throw new Failure("ui_unavailable");
                if (!ReferenceEquals(controller.ActiveTool, select) || controller.IsDragging() || tool.Dragging)
                    throw new Failure("ui_busy");
                revalidate();
                game = Game.Instance; generation = GameContextLifecycle.CaptureGeneration();
                // A priority from a different panel is not the old state of a newly created panel.
                originalPanel = plan.ProductInfoScreen?.materialSelectionPanel;
                originalScreen = originalPanel?.PriorityScreen;
                if (originalPanel != null && TryGetReadyPriority(originalScreen, out PrioritySetting previous))
                    originalPriority = previous;
                result.Available = true;
                result.ActivationAttempted = true;
                plan.CopyBuildingOrder(def, facadeId);
                ownedProduct = plan.ProductInfoScreen;
                ownedPanel = ownedProduct?.materialSelectionPanel;
                ownedPreview = CaptureOwnedPreview(tool, controller, select, plan, def, game, generation);
                panel = RequireMaterialUi(plan, def, material, result);
                RequireMaterialOwnership(plan, ownedProduct, ownedPanel, def);
                priorityScreen = panel.PriorityScreen;
                if (!ReferenceEquals(panel, originalPanel) || !ReferenceEquals(priorityScreen, originalScreen)) originalPriority = null;
                panel.ForceSelectPrimaryTag(material);
                ownedPreview = CaptureOwnedPreview(tool, controller, select, plan, def, game, generation);
                if (!ReferenceEquals(RequireMaterialUi(plan, def, material, result), panel)
                    || !ReferenceEquals(panel.PriorityScreen, priorityScreen)) throw new Failure("ui_busy");
                result.PrioritySetAttempted = true;
                priorityScreen.SetScreenPriority(requestedPriority, play_sound: false);
                RequireOwnedPreview(tool, controller, select, plan, def, game, generation, ownedPreview);
                tool.SetToolOrientation(Orientation.Neutral);
                RequireOwnedPreview(tool, controller, select, plan, def, game, generation, ownedPreview);
                tool.OnMouseMove(Grid.CellToPosCBC(cell, def.SceneLayer));
                RequireOwnedPreview(tool, controller, select, plan, def, game, generation, ownedPreview);
                if (tool.GetLastCell != cell || tool.GetBuildingOrientation != Orientation.Neutral
                    || Grid.PosToCell(ownedPreview) != cell
                    || !NativeBlueprintInstantCompletion.FacadeEquivalent(ownedPreview.GetComponent<BuildingFacade>(), facadeId)
                    || !ReferenceEquals(RequireMaterialUi(plan, def, material, result), panel)
                    || !SelectedMaterialMatches(tool, material) || panel.CurrentSelectedElement != material
                    || !SamePriority(priorityScreen.GetLastSelectedPriority(), requestedPriority))
                    throw new Failure("preview_unavailable");
                revalidate();
                RequireMaterialOwnership(plan, ownedProduct, ownedPanel, def);
                RequireOwnedPreview(tool, controller, select, plan, def, game, generation, ownedPreview);
                if (!ReferenceEquals(RequireMaterialUi(plan, def, material, result), panel)
                    || panel.CurrentSelectedElement != material || !SelectedMaterialMatches(tool, material)
                    || !ReferenceEquals(panel.PriorityScreen, priorityScreen)
                    || !SamePriority(priorityScreen.GetLastSelectedPriority(), requestedPriority))
                    throw new Failure("preview_unavailable");
                result.ActionAttempted = true;
                TryBuild.Invoke(tool, new object[] { cell });
            }
            catch (Exception error)
            {
                var actual = (error as System.Reflection.TargetInvocationException)?.InnerException ?? error;
                result.ReasonCode = (actual as Failure)?.Reason ?? "native_failed";
                result.UiInitializing = result.ReasonCode == "ui_initializing";
                result.ExceptionType = actual.GetType().Name;
            }
            finally
            {
                if (result.PrioritySetAttempted && originalPriority.HasValue)
                {
                    try
                    {
                        RequireMaterialOwnership(plan, ownedProduct, ownedPanel, def);
                        RequireOwnedPreview(tool, controller, select, plan, def, game, generation, ownedPreview, cleanup: true);
                        if (panel == null || !ReferenceEquals(plan.ProductInfoScreen?.materialSelectionPanel, panel)
                            || !ReferenceEquals(panel.PriorityScreen, priorityScreen)
                            || !SamePriority(priorityScreen.GetLastSelectedPriority(), requestedPriority))
                            throw new Failure("priority_restore_failed");
                        if (!SamePriority(originalPriority.Value, requestedPriority))
                            priorityScreen.SetScreenPriority(originalPriority.Value, play_sound: false);
                        result.PriorityRestored = SamePriority(priorityScreen.GetLastSelectedPriority(), originalPriority.Value);
                        if (result.PriorityRestored != true) throw new Failure("priority_restore_failed");
                    }
                    catch (Exception error)
                    {
                        result.PriorityRestored = null;
                        result.CleanupExceptionType = error.GetType().Name;
                        if (result.ReasonCode == null) result.ReasonCode = "priority_restore_failed";
                    }
                }
                if (result.ActivationAttempted)
                {
                    try
                    {
                        RequireContext(controller, select, plan, game, generation);
                        if (ReferenceEquals(controller.ActiveTool, select)) result.UiRestored = true;
                        else
                        {
                            RequireMaterialOwnership(plan, ownedProduct, ownedPanel, def);
                            RequireOwnedPreview(tool, controller, select, plan, def, game, generation, ownedPreview, cleanup: true);
                            controller.ActivateTool(select);
                            result.UiRestored = ReferenceEquals(controller.ActiveTool, select);
                            if (result.UiRestored != true) throw new Failure("cleanup_failed");
                        }
                    }
                    catch (Exception error)
                    {
                        result.UiRestored = null;
                        if (result.CleanupExceptionType == null) result.CleanupExceptionType = error.GetType().Name;
                        if (result.ReasonCode == null) result.ReasonCode = "cleanup_failed";
                    }
                }
                if (result.PriorityRestored == true && originalPriority.HasValue)
                {
                    try
                    {
                        RequireContext(controller, select, plan, game, generation);
                        if (!ReferenceEquals(plan.ProductInfoScreen, ownedProduct)
                            || !ReferenceEquals(ownedProduct.materialSelectionPanel, panel)
                            || !ReferenceEquals(panel.PriorityScreen, priorityScreen)) throw new Failure("priority_restore_failed");
                        result.PriorityRestored = SamePriority(priorityScreen.GetLastSelectedPriority(), originalPriority.Value);
                        if (result.PriorityRestored != true && result.ReasonCode == null) result.ReasonCode = "priority_restore_failed";
                    }
                    catch (Exception error)
                    {
                        result.PriorityRestored = null;
                        if (result.CleanupExceptionType == null) result.CleanupExceptionType = error.GetType().Name;
                        if (result.ReasonCode == null) result.ReasonCode = "priority_restore_failed";
                    }
                }
            }
            return result;
        }

        private static void RequireMaterialOwnership(PlanScreen plan, ProductInfoScreen product, MaterialSelectionPanel panel, BuildingDef def)
        {
            if (product == null || !ReferenceEquals(plan.ProductInfoScreen, product)
                || !ReferenceEquals(product.materialSelectionPanel, panel) || !ReferenceEquals(product.currentDef, def))
                throw new Failure("cleanup_failed");
        }

        private static void RequireContext(PlayerController controller, SelectTool select, PlanScreen plan, Game game, int generation)
        {
            if (game == null || game.IsLoading() || !ReferenceEquals(Game.Instance, game)
                || generation != GameContextLifecycle.CaptureGeneration()
                || controller == null || !ReferenceEquals(PlayerController.Instance, controller)
                || select == null || !ReferenceEquals(SelectTool.Instance, select)
                || plan == null || !ReferenceEquals(PlanScreen.Instance, plan)) throw new Failure("cleanup_failed");
        }

        private static GameObject CaptureOwnedPreview(BuildTool tool, PlayerController controller, SelectTool select,
            PlanScreen plan, BuildingDef def, Game game, int generation)
        {
            RequireContext(controller, select, plan, game, generation);
            RequireReady(tool, controller, select, plan);
            if (!ReferenceEquals(BuildTool.Instance, tool) || !ReferenceEquals(controller.ActiveTool, tool)
                || controller.IsDragging() || tool.Dragging || tool.visualizer == null
                || !ReferenceEquals(tool.visualizer.GetComponent<Building>()?.Def, def)) throw new Failure("ui_busy");
            return tool.visualizer;
        }

        private static void RequireOwnedPreview(BuildTool tool, PlayerController controller, SelectTool select,
            PlanScreen plan, BuildingDef def, Game game, int generation, GameObject ownedPreview, bool cleanup = false)
        {
            if (cleanup)
            {
                RequireContext(controller, select, plan, game, generation);
                if (tool == null || !ReferenceEquals(BuildTool.Instance, tool) || !ReferenceEquals(controller.ActiveTool, tool)
                    || ownedPreview == null || !ReferenceEquals(tool.visualizer, ownedPreview)
                    || !ReferenceEquals(ownedPreview.GetComponent<Building>()?.Def, def)) throw new Failure("cleanup_failed");
                return;
            }
            var current = CaptureOwnedPreview(tool, controller, select, plan, def, game, generation);
            if (ownedPreview == null || !ReferenceEquals(current, ownedPreview)) throw new Failure("ui_busy");
        }
    }
}
