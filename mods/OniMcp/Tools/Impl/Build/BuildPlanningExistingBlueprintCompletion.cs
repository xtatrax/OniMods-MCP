using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Server;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private sealed class ExistingBlueprintTarget
        {
            internal Game Game;
            internal int Generation, InstanceId;
            internal GameObject Blueprint;
            internal Constructable Constructable;
            internal List<Tag> Elements;
            internal string Facade;
            internal bool HasFacade, FacadeOriginal;
            internal PlacementDetails Placement;
        }

        private sealed class ExistingBlueprintObservation
        {
            internal GameObject Blueprint;
            internal readonly List<GameObject> Objects = new List<GameObject>();
            internal readonly List<bool> Solid = new List<bool>();
            internal readonly HashSet<int> CompleteIds = new HashSet<int>();
        }

        private static Dictionary<string, object> TryCompleteExistingVirtualFileBlueprint(
            BuildingDef def, PlacementDetails placement, JObject args, Dictionary<string, object> existing)
        {
            if (!IsAuthorizedVirtualFileInstantBuild(args) || existing == null
                || !existing.TryGetValue("kind", out object kind)
                || !string.Equals(kind?.ToString(), "blueprint", StringComparison.OrdinalIgnoreCase))
                return null;

            var outcome = new NativeBlueprintInstantCompletion.Outcome();
            ExistingBlueprintTarget target = null;
            ExistingBlueprintObservation before = null;
            Dictionary<string, object> safety = null;
            string reason = null;
            try
            {
                var blueprint = FindConstructableAtPlacement(def, placement);
                if (blueprint == null) throw new NativeBlueprintInstantCompletion.Failure("target_changed");
                var constructable = blueprint.GetComponent<Constructable>();
                var selected = constructable?.SelectedElementsTags;
                var identity = blueprint.GetComponent<KPrefabID>();
                var building = blueprint.GetComponent<Building>();
                if (identity == null || identity.InstanceID <= 0 || !ReferenceEquals(building?.Def, def)
                    || selected == null || selected.Count == 0 || selected.Any(tag => !tag.IsValid))
                    throw new NativeBlueprintInstantCompletion.Failure("target_unverifiable");
                Orientation orientation = blueprint.GetComponent<Rotatable>()?.GetOrientation() ?? Orientation.Neutral;
                var facade = blueprint.GetComponent<BuildingFacade>();
                target = new ExistingBlueprintTarget
                {
                    Game = Game.Instance, Generation = GameContextLifecycle.CaptureGeneration(),
                    Blueprint = blueprint, Constructable = constructable, InstanceId = identity.InstanceID,
                    Elements = new List<Tag>(selected), HasFacade = facade != null, Facade = facade?.CurrentFacade,
                    FacadeOriginal = facade != null && facade.IsOriginal,
                    Placement = BuildPlacementDetails(def, placement.AnchorX, placement.AnchorY, placement.WorldId, orientation)
                };
                placement = target.Placement;
                before = ObserveExistingBlueprintTarget(def, target);
                if (!string.Equals(def.PrefabID, "Tile", StringComparison.Ordinal))
                    throw new NativeBlueprintInstantCompletion.Failure("definition_unavailable");
                if (target.Elements.Count != 1)
                    throw new NativeBlueprintInstantCompletion.Failure("material_unverifiable");
                if (!target.HasFacade)
                    throw new NativeBlueprintInstantCompletion.Failure("facade_unverifiable");
                System.Action revalidate = () =>
                {
                    RevalidateExistingBlueprintTarget(def, target, args);
                    safety = ExistingBlueprintCompletionSafetyFailure(def, target.Placement, target.Blueprint);
                    if (safety != null) throw new NativeBlueprintInstantCompletion.Failure("safety_blocked");
                };
                outcome = NativeBlueprintInstantCompletion.Run(def, new List<Tag>(target.Elements), target.Facade,
                    placement.OriginCell, placement.Orientation, revalidate);
                reason = outcome.ReasonCode;
            }
            catch (Exception error)
            {
                reason = (error as NativeBlueprintInstantCompletion.Failure)?.Reason ?? "observation_unavailable";
                outcome.ReasonCode = reason;
                outcome.ExceptionType = error.GetType().Name;
            }

            if (outcome.ActionAttempted) NativeBuildingCellIndex.Invalidate();
            bool? changed = null, preserved = null, leftover = null, completedPresent = null, protectedPreserved = null;
            GameObject completed = null;
            bool verified = false;
            try
            {
                if (target != null && ReferenceEquals(Game.Instance, target.Game)
                    && target.Generation == GameContextLifecycle.CaptureGeneration()
                    && Game.Instance != null && !Game.Instance.IsLoading())
                {
                    var after = ObserveExistingBlueprintTarget(def, target);
                    leftover = after.Blueprint != null;
                    preserved = ReferenceEquals(after.Blueprint, target.Blueprint)
                        && ExistingBlueprintFieldsMatch(def, target);
                    if (before != null) changed = !SameExistingBlueprintObservation(before, after) || preserved == false;
                    completed = FindCompletedBuildAtPlacement(def, placement);
                    completedPresent = after.CompleteIds.Count > 0;
                    verified = before != null && completed != null && after.Blueprint == null
                        && !before.CompleteIds.Contains(completed.GetInstanceID())
                        && VerifyNativeExistingBlueprintCompletion(def, target, completed);
                    if (before != null)
                        protectedPreserved = ProtectedExistingBlueprintObjectsPreserved(before, after, target.Blueprint,
                            completed);
                }
            }
            catch (Exception error)
            {
                changed = null; preserved = null; leftover = null; completedPresent = null; protectedPreserved = null; verified = false;
                if (reason == null) reason = "observation_unavailable";
                if (outcome.ExceptionType == null) outcome.ExceptionType = error.GetType().Name;
            }
            bool success = verified && protectedPreserved == true && outcome.ReasonCode == null && outcome.UiRestored == true;
            if (success) changed = true;
            if (!success && reason == null) reason = protectedPreserved == false ? "protected_state_changed"
                : verified ? "cleanup_failed" : "completion_not_observed";
            outcome.ReasonCode = reason;
            var details = outcome.ToDictionary();
            details["verified"] = verified;
            details["verificationScope"] = "exact_def_geometry_orientation_single_primary_material_facade_registration";
            details["changeObservationScope"] = "target_blueprint_occupied_grid_layers_and_completed_ids";
            details["changed"] = BlueprintNullable(changed);
            details["blueprintPreserved"] = BlueprintNullable(preserved);
            details["leftoverBlueprint"] = BlueprintNullable(leftover);
            details["blueprintId"] = target == null ? (object)JValue.CreateNull() : target.InstanceId;
            details["placement"] = placement.ToDictionary();
            details["completedBuildingPresent"] = BlueprintNullable(completedPresent);
            details["protectedOccupantsPreserved"] = BlueprintNullable(protectedPreserved);
            details["safety"] = safety == null ? (object)JValue.CreateNull() : safety;
            details["requestedMaterialTags"] = target == null ? (object)JValue.CreateNull() : target.Elements.Select(tag => tag.Name).ToArray();
            details["reasonCode"] = reason == null ? (object)JValue.CreateNull() : reason;
            details["applied"] = verified ? (object)1 : changed == false ? 0 : (object)JValue.CreateNull();
            details["partial"] = !success && (changed != false || verified || outcome.CleanupExceptionType != null);
            var result = success ? new Dictionary<string, object>()
                : ErrorResult(def.PrefabID, placement.AnchorX, placement.AnchorY,
                    McpLocalization.Get("blueprint_completion." + reason), details);
            result["planned"] = false;
            result["blueprintPlaced"] = false;
            object completion = verified ? (object)true : completedPresent == false ? false : (object)JValue.CreateNull();
            result["buildingCompleted"] = completion;
            result["completedExistingBlueprint"] = completion;
            result["alreadyPresent"] = success;
            result["alreadyBlueprint"] = BlueprintNullable(leftover);
            result["alreadyBuilding"] = completion;
            result["completedBuildingPresent"] = BlueprintNullable(completedPresent);
            result["blueprintPreserved"] = BlueprintNullable(preserved);
            result["leftoverBlueprint"] = BlueprintNullable(leftover);
            result["valid"] = success;
            result["changed"] = details["changed"];
            result["applied"] = details["applied"];
            result["partial"] = details["partial"];
            result["prefabId"] = def.PrefabID;
            result["name"] = ToolUtil.CleanName(def.Name);
            result["x"] = placement.AnchorX; result["y"] = placement.AnchorY;
            result["worldId"] = placement.WorldId;
            result["anchor"] = AnchorDictionary(placement.AnchorX, placement.AnchorY, placement.WorldId);
            result["placement"] = placement.ToDictionary();
            result["instantCompletion"] = details;
            if (verified)
            {
                var actual = ActualPlacementDetails(completed, def, placement.AnchorX, placement.AnchorY);
                result["actualPlacement"] = actual;
                result["actualAnchor"] = ActualAnchorArray(actual);
                result["placementCheck"] = ComparePlacement(placement, actual);
                result["id"] = completed.GetComponent<KPrefabID>()?.InstanceID ?? -1;
            }
            return result;
        }

        private static void RevalidateExistingBlueprintTarget(BuildingDef def, ExistingBlueprintTarget target, JObject args)
        {
            if (!IsAuthorizedVirtualFileInstantBuild(args) || !ReferenceEquals(Game.Instance, target.Game)
                || Game.Instance == null || Game.Instance.IsLoading()
                || target.Generation != GameContextLifecycle.CaptureGeneration())
                throw new NativeBlueprintInstantCompletion.Failure("target_changed");
            if (ClusterManager.Instance == null || ClusterManager.Instance.activeWorldId != target.Placement.WorldId)
                throw new NativeBlueprintInstantCompletion.Failure("world_inactive");
            if (SpeedControlScreen.Instance == null || !SpeedControlScreen.Instance.IsPaused)
                throw new NativeBlueprintInstantCompletion.Failure("pause_required");
            if (!ExistingBlueprintFieldsMatch(def, target))
                throw new NativeBlueprintInstantCompletion.Failure("target_changed");
        }

        private static bool ExistingBlueprintFieldsMatch(BuildingDef def, ExistingBlueprintTarget target)
        {
            var blueprint = target.Blueprint;
            if (blueprint == null || !ReferenceEquals(FindConstructableAtPlacement(def, target.Placement), blueprint)
                || !ReferenceEquals(blueprint.GetComponent<Constructable>(), target.Constructable)
                || !ReferenceEquals(blueprint.GetComponent<Building>()?.Def, def)
                || blueprint.GetComponent<KPrefabID>()?.InstanceID != target.InstanceId
                || !ToolUtil.GameObjectMatchesWorld(blueprint, target.Placement.WorldId)) return false;
            var elements = target.Constructable.SelectedElementsTags;
            if (elements == null || elements.Count != target.Elements.Count) return false;
            for (int i = 0; i < elements.Count; i++) if (elements[i] != target.Elements[i]) return false;
            var facade = blueprint.GetComponent<BuildingFacade>();
            if ((facade != null) != target.HasFacade || facade != null
                && (facade.IsOriginal != target.FacadeOriginal || !string.Equals(facade.CurrentFacade, target.Facade, StringComparison.Ordinal))) return false;
            var orientation = blueprint.GetComponent<Rotatable>()?.GetOrientation() ?? Orientation.Neutral;
            return orientation == target.Placement.Orientation
                && GetBool(ComparePlacement(target.Placement,
                    ActualPlacementDetails(blueprint, def, target.Placement.AnchorX, target.Placement.AnchorY)), "valid");
        }

        private static bool VerifyNativeExistingBlueprintCompletion(BuildingDef def, ExistingBlueprintTarget target, GameObject completed)
        {
            var identity = completed.GetComponent<KPrefabID>();
            if (identity == null || identity.InstanceID <= 0 || !target.HasFacade
                || !ReferenceEquals(completed.GetComponent<BuildingComplete>()?.Def, def)
                || !ReferenceEquals(completed.GetComponent<Building>()?.Def, def)
                || !string.Equals(ExistingConstructionMaterialTag(completed), target.Elements[0].Name, StringComparison.Ordinal)) return false;
            var facade = completed.GetComponent<BuildingFacade>();
            if (!NativeBlueprintInstantCompletion.FacadeEquivalent(facade, target.Facade)) return false;
            return (completed.GetComponent<Rotatable>()?.GetOrientation() ?? Orientation.Neutral) == target.Placement.Orientation;
        }

        private static ExistingBlueprintObservation ObserveExistingBlueprintTarget(BuildingDef def, ExistingBlueprintTarget target)
        {
            var result = new ExistingBlueprintObservation { Blueprint = FindConstructableAtPlacement(def, target.Placement) };
            foreach (var cell in target.Placement.Footprint)
            {
                if (!cell.Valid) throw new NativeBlueprintInstantCompletion.Failure("target_unverifiable");
                result.Solid.Add(Grid.Solid[cell.Cell]);
                for (int layer = 0; layer < (int)ObjectLayer.NumLayers; layer++) result.Objects.Add(Grid.Objects[cell.Cell, layer]);
            }
            var candidates = Components.BuildingCompletes.Items.Select(item => item?.gameObject)
                .Concat(UnityEngine.Object.FindObjectsByType<BuildingComplete>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Select(item => item?.gameObject));
            foreach (var candidate in candidates)
                if (IsTargetCompletedObject(def, target.Placement, candidate))
                    result.CompleteIds.Add(candidate.GetInstanceID());
            return result;
        }

        private static bool ProtectedExistingBlueprintObjectsPreserved(ExistingBlueprintObservation before,
            ExistingBlueprintObservation after, GameObject blueprint, GameObject completed)
        {
            if (before.Objects.Count != after.Objects.Count) return false;
            for (int i = 0; i < before.Objects.Count; i++)
            {
                GameObject expected = completed != null && ReferenceEquals(before.Objects[i], blueprint)
                    ? completed : before.Objects[i];
                if (!ReferenceEquals(expected, after.Objects[i])) return false;
            }
            return true;
        }

        private static bool SameExistingBlueprintObservation(ExistingBlueprintObservation before, ExistingBlueprintObservation after)
        {
            if (!ReferenceEquals(before.Blueprint, after.Blueprint) || !before.Solid.SequenceEqual(after.Solid)
                || before.Objects.Count != after.Objects.Count || !before.CompleteIds.SetEquals(after.CompleteIds)) return false;
            for (int i = 0; i < before.Objects.Count; i++) if (!ReferenceEquals(before.Objects[i], after.Objects[i])) return false;
            return true;
        }

        private static object BlueprintNullable(bool? value) => value.HasValue ? (object)value.Value : JValue.CreateNull();
    }
}
