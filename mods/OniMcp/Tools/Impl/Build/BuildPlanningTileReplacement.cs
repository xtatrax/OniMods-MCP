using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static Dictionary<string, object> TryReuseNativeTileReplacementQueue(BuildingDef def, PlacementDetails placement, JObject args)
        {
            if (!IsNativeTileReplacementCandidateScope(def, placement)
                || Grid.Objects[placement.OriginCell, (int)ObjectLayer.ReplacementTile] == null) return null;
            var queue = Grid.Objects[placement.OriginCell, (int)ObjectLayer.ReplacementTile];
            var elements = queue.GetComponent<Constructable>()?.SelectedElementsTags;
            Tag material = elements != null && elements.Count == 1 ? elements[0] : Tag.Invalid;
            var facade = ResolveFacade(def, args["facade"]?.ToString() ?? args["facadeId"]?.ToString());
            if (!facade.Valid) return ErrorResult(def.PrefabID, placement.AnchorX, placement.AnchorY, facade.Error);
            bool materialMatches = material.IsValid && ExistingMaterialRequestSatisfied(def,
                new Dictionary<string, object> { ["material"] = material.Name }, args["material"]?.ToString());
            return ProcessNativeTileReplacement(def, placement, args, material, facade.TryPlaceId,
                requireExisting: true, existingMaterialMatches: materialMatches);
        }

        private static Dictionary<string, object> TryPlanNativeTileReplacement(BuildingDef def, PlacementDetails placement,
            JObject args, MaterialSelection material, FacadeSelection facade)
        {
            if (!IsNativeTileReplacementCandidateScope(def, placement)) return null;
            Tag selected = material.Elements != null && material.Elements.Count == 1 ? material.Elements[0] : Tag.Invalid;
            var result = ProcessNativeTileReplacement(def, placement, args, selected, facade.TryPlaceId,
                requireExisting: false, existingMaterialMatches: true);
            result["materialSelection"] = material.ToDictionary();
            return result;
        }

        private static Dictionary<string, object> ProcessNativeTileReplacement(BuildingDef def, PlacementDetails placement,
            JObject args, Tag material, string facade, bool requireExisting, bool existingMaterialMatches)
        {
            var outcome = new NativeTileReplacement.Outcome();
            TileReplacementTarget target = null;
            TileReplacementObservation before = null;
            Dictionary<string, object> mode = null;
            string reason = null;
            bool dryRun = IsDryRun(args), reused = false, previewValidated = false;
            try
            {
                int priority = TileReplacementPriority(args);
                target = CaptureTileReplacementTarget(def, placement, material, facade, priority);
                before = ObserveTileReplacement(target);
                mode = NativeTileReplacement.RequireNormalMode();
                if (ToolUtil.GetBool(args, "instantBuild", false)) throw new NativeTileReplacement.Failure("scope_unavailable");
                RevalidateTileReplacementTarget(target, args, execute: !dryRun);
                if (!material.IsValid) throw new NativeTileReplacement.Failure("pending_conflict");
                if (before.Queue != null)
                {
                    if (!existingMaterialMatches || !MatchingTileReplacementQueue(target, before.Queue))
                        throw new NativeTileReplacement.Failure("pending_conflict");
                    reused = true;
                }
                else if (requireExisting) throw new NativeTileReplacement.Failure("target_changed");
                previewValidated = true;
                if (!dryRun && !reused)
                {
                    System.Action revalidate = () =>
                    {
                        RevalidateTileReplacementTarget(target, args, execute: true);
                        var current = ObserveTileReplacement(target);
                        if (current.Queue != null) throw new NativeTileReplacement.Failure("pending_conflict");
                        if (!TileReplacementProtectedSame(before, current)) throw new NativeTileReplacement.Failure("protected_state_changed");
                    };
                    outcome = NativeTileReplacement.Run(def, material, facade, placement.OriginCell, priority, revalidate);
                    reason = outcome.ReasonCode;
                }
            }
            catch (Exception error)
            {
                reason = (error as NativeTileReplacement.Failure)?.Reason ?? "observation_unavailable";
                outcome.ReasonCode = reason;
                outcome.ExceptionType = error.GetType().Name;
            }
            if (outcome.ActionAttempted) NativeBuildingCellIndex.Invalidate();
            bool? changed = null, oldPreserved = null, protectedPreserved = null, queuePresent = null;
            bool queueVerified = false;
            GameObject queue = null;
            try
            {
                if (target != null)
                {
                    var after = ObserveTileReplacement(target);
                    oldPreserved = OldTilePreserved(target);
                    queuePresent = after.Queue != null || after.CandidateIds.Count > 0;
                    queue = after.Queue;
                    queueVerified = queue != null && MatchingTileReplacementQueue(target, queue)
                        && oldPreserved == true && ReferenceEquals(def.GetReplacementCandidate(placement.OriginCell), target.Old);
                    if (before != null)
                    {
                        protectedPreserved = TileReplacementProtectedSame(before, after);
                        changed = !TileReplacementObservationSame(before, after) || oldPreserved == false;
                    }
                }
            }
            catch (Exception error)
            {
                changed = null; oldPreserved = null; protectedPreserved = null; queuePresent = null; queueVerified = false;
                if (reason == null) reason = "observation_unavailable";
                if (outcome.ExceptionType == null) outcome.ExceptionType = error.GetType().Name;
            }
            bool success = reason == null && oldPreserved == true && protectedPreserved == true
                && (dryRun ? previewValidated : queueVerified && (!outcome.ActivationAttempted || outcome.UiRestored == true));
            if (!success && reason == null) reason = oldPreserved == false || protectedPreserved == false
                ? "protected_state_changed" : "queue_not_observed";
            bool newQueue = queueVerified && before != null && !ReferenceEquals(before.Queue, queue);
            bool partial = !success && (changed != false || outcome.CleanupExceptionType != null || newQueue);
            var details = outcome.ToDictionary();
            details["reasonCode"] = reason == null ? (object)JValue.CreateNull() : reason;
            details["nativeCandidateValidated"] = previewValidated;
            details["nativePlacementValidated"] = outcome.ActionAttempted && queueVerified;
            details["validationScope"] = queueVerified ? "native_replacement_reservation_observed"
                : outcome.ActionAttempted ? "native_void_gesture_attempted_no_verified_reservation"
                : previewValidated ? "native_replacement_candidate_only" : "unverified";
            details["instantMode"] = mode == null ? (object)JValue.CreateNull() : mode;
            details["queueVerified"] = queueVerified;
            details["queuePresent"] = TileReplacementValue(queuePresent);
            details["oldTargetPreserved"] = TileReplacementValue(oldPreserved);
            details["protectedOccupantsPreserved"] = TileReplacementValue(protectedPreserved);
            details["persistentTargetLinkVerified"] = JValue.CreateNull();
            details["targetBindingScope"] = "native_candidate_matches_original_at_observation_only";
            details["priority"] = target == null ? (object)JValue.CreateNull() : target.Priority;
            details["oldTargetId"] = target == null ? (object)JValue.CreateNull() : target.OldId;
            details["placement"] = placement.ToDictionary();
            details["changed"] = TileReplacementValue(changed);
            details["changedScope"] = "target_tile_occupied_map_layers_solid_foundation_and_replacement_candidate_ids_only";
            details["uiStateChanged"] = JValue.CreateNull();
            details["partial"] = partial;
            details["applied"] = newQueue ? (object)1 : changed == false ? 0 : (object)JValue.CreateNull();
            var result = success ? new Dictionary<string, object>() : ErrorResult(def.PrefabID, placement.AnchorX,
                placement.AnchorY, McpLocalization.Get("tile_replacement." + reason), details);
            result["planned"] = success && !dryRun && newQueue;
            result["alreadyPresent"] = success && reused;
            result["blueprintPlaced"] = newQueue;
            result["buildingCompleted"] = oldPreserved == true ? (object)false : JValue.CreateNull();
            result["replacementQueued"] = queueVerified ? (object)true : queuePresent == false ? false : (object)JValue.CreateNull();
            result["queueVerified"] = queueVerified;
            result["alreadyBlueprint"] = reused && queueVerified;
            result["alreadyBuilding"] = false;
            result["valid"] = success; result["dryRun"] = dryRun;
            result["changed"] = details["changed"]; result["changedScope"] = details["changedScope"];
            result["uiStateChanged"] = JValue.CreateNull();
            result["partial"] = partial; result["applied"] = details["applied"];
            result["prefabId"] = def.PrefabID; result["name"] = ToolUtil.CleanName(def.Name);
            result["x"] = placement.AnchorX; result["y"] = placement.AnchorY; result["worldId"] = placement.WorldId;
            result["anchor"] = AnchorDictionary(placement.AnchorX, placement.AnchorY, placement.WorldId);
            result["placement"] = placement.ToDictionary(); result["tileReplacement"] = details;
            result["material"] = material.IsValid ? (object)new[] { material.Name } : JValue.CreateNull();
            if (queueVerified)
            {
                var actual = ActualPlacementDetails(queue, def, placement.AnchorX, placement.AnchorY);
                result["id"] = queue.GetComponent<KPrefabID>().InstanceID;
                result["actualPlacement"] = actual; result["actualAnchor"] = ActualAnchorArray(actual);
                result["placementCheck"] = ComparePlacement(placement, actual);
            }
            return result;
        }
    }
}
