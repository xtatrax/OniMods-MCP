using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Server;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private sealed class TileReplacementTarget
        {
            internal Game Game;
            internal int Generation, OldId, Priority;
            internal GameObject Old;
            internal BuildingDef Def, OldDef;
            internal PlacementDetails Placement, OldPlacement;
            internal Tag Material;
            internal string Facade, OldMaterial, OldFacade;
            internal float OldMass;
            internal bool OldSolid, OldFoundation;
            internal NativeReplacementOpinion NativeCandidate;
        }

        private sealed class TileReplacementObservation
        {
            internal readonly List<GameObject> Objects = new List<GameObject>();
            internal readonly List<bool> Solid = new List<bool>();
            internal readonly List<bool> Foundation = new List<bool>();
            internal readonly HashSet<int> CandidateIds = new HashSet<int>();
            internal GameObject Queue;
        }

        private static bool IsNativeTileReplacementCandidateScope(BuildingDef def, PlacementDetails placement)
        {
            if (def == null || placement == null || !def.IsFoundation
                || def.TileLayer != ObjectLayer.FoundationTile || def.ReplacementLayer != ObjectLayer.ReplacementTile
                || !Grid.IsValidCell(placement.OriginCell)) return false;
            var old = Grid.Objects[placement.OriginCell, (int)ObjectLayer.FoundationTile];
            var oldDef = old?.GetComponent<BuildingComplete>()?.Def;
            // Same-prefab reuse/material mismatch keeps the established generic contract.
            return oldDef != null && !string.Equals(oldDef.PrefabID, def.PrefabID, StringComparison.Ordinal);
        }

        private sealed class NativeReplacementOpinion
        {
            internal string AssessmentState = "unknown", ExceptionType;
            internal bool? OldReplaceable, CanReplace, CandidatePresent, CandidateMatchesOriginal;
            internal int? CandidateId;
            internal Dictionary<string, object> ToDictionary() => new Dictionary<string, object>
            {
                ["assessmentState"] = AssessmentState, ["oldReplaceable"] = TileReplacementValue(OldReplaceable),
                ["canReplace"] = TileReplacementValue(CanReplace), ["candidatePresent"] = TileReplacementValue(CandidatePresent),
                ["candidateMatchesOriginal"] = TileReplacementValue(CandidateMatchesOriginal),
                ["candidateId"] = CandidateId.HasValue ? (object)CandidateId.Value : JValue.CreateNull(),
                ["exceptionType"] = ExceptionType == null ? (object)JValue.CreateNull() : ExceptionType,
                ["scope"] = "last_native_replacement_guard_read_not_final_placement_or_completion"
            };
        }

        private static bool SingleCellMaterialTileDefinition(BuildingDef def)
            => def != null && def.IsFoundation && def.TileLayer == ObjectLayer.FoundationTile
                && def.ReplacementLayer == ObjectLayer.ReplacementTile && def.WidthInCells == 1 && def.HeightInCells == 1
                && def.Mass != null && def.Mass.Length == 1
                && def.MaterialCategory != null && def.MaterialCategory.Length == 1;

        private static void RequireNativeTileReplacementCandidate(TileReplacementTarget target)
        {
            var opinion = new NativeReplacementOpinion();
            target.NativeCandidate = opinion;
            try
            {
                opinion.OldReplaceable = target.OldDef.Replaceable;
                opinion.CanReplace = target.Def.CanReplace(target.Old);
                var candidate = target.Def.GetReplacementCandidate(target.Placement.OriginCell);
                opinion.CandidatePresent = candidate != null;
                opinion.CandidateMatchesOriginal = ReferenceEquals(candidate, target.Old);
                int? id = candidate?.GetComponent<KPrefabID>()?.InstanceID;
                if (id.HasValue && id.Value > 0) opinion.CandidateId = id;
                opinion.AssessmentState = "known";
            }
            catch (Exception error)
            {
                opinion.ExceptionType = error.GetType().Name;
                throw new NativeTileReplacement.Failure("observation_unavailable");
            }
            if (opinion.OldReplaceable != true || opinion.CanReplace != true || opinion.CandidateMatchesOriginal != true)
                throw new NativeTileReplacement.Failure("candidate_rejected");
        }

        private static int TileReplacementPriority(JObject args)
        {
            if (args["priority"] == null) return 5;
            if (args["priority"].Type != JTokenType.Integer) throw new NativeTileReplacement.Failure("priority_unavailable");
            long value = args["priority"].Value<long>();
            if (value < 1 || value > 9) throw new NativeTileReplacement.Failure("priority_unavailable");
            return (int)value;
        }

        private static TileReplacementTarget CaptureTileReplacementTarget(BuildingDef def, PlacementDetails placement,
            Tag material, string facade, int priority)
        {
            if (!placement.GeometryAvailable || placement.Orientation != Orientation.Neutral || placement.Footprint.Count != 1
                || placement.Footprint.Any(cell => !cell.Valid || !cell.Visible || !cell.InWorld)
                || !SingleCellMaterialTileDefinition(def))
                throw new NativeTileReplacement.Failure("scope_unavailable");
            var old = Grid.Objects[placement.OriginCell, (int)ObjectLayer.FoundationTile];
            var oldDef = old?.GetComponent<BuildingComplete>()?.Def;
            var identity = old?.GetComponent<KPrefabID>();
            var primary = old?.GetComponent<PrimaryElement>();
            var oldFacade = old?.GetComponent<BuildingFacade>();
            if (old == null || identity == null || identity.InstanceID <= 0 || primary == null || oldFacade == null
                || oldDef == null || !ReferenceEquals(old.GetComponent<Building>()?.Def, oldDef))
                throw new NativeTileReplacement.Failure("target_unverifiable");
            if (!SingleCellMaterialTileDefinition(oldDef)) throw new NativeTileReplacement.Failure("scope_unavailable");
            var oldPlacement = BuildPlacementDetails(oldDef, placement.AnchorX, placement.AnchorY, placement.WorldId, Orientation.Neutral);
            if (!oldPlacement.GeometryAvailable || oldPlacement.Footprint.Count != 1
                || oldPlacement.OriginCell != placement.OriginCell
                || !GetBool(ComparePlacement(oldPlacement, ActualPlacementDetails(old, oldDef, placement.AnchorX, placement.AnchorY)), "valid")
                || (old.GetComponent<Rotatable>()?.GetOrientation() ?? Orientation.Neutral) != Orientation.Neutral
                || !IsCompletedBuildFullyRegistered(oldDef, oldPlacement, oldPlacement.OriginCell, Orientation.Neutral, old, out _))
                throw new NativeTileReplacement.Failure("target_unverifiable");
            return new TileReplacementTarget
            {
                Game = Game.Instance, Generation = GameContextLifecycle.CaptureGeneration(), Old = old, OldId = identity.InstanceID,
                OldDef = oldDef, Def = def, Placement = placement, OldPlacement = oldPlacement,
                Material = material, Facade = facade, Priority = priority,
                OldMaterial = primary.ElementID.ToString(), OldMass = primary.Mass, OldFacade = oldFacade.CurrentFacade,
                OldSolid = Grid.Solid[placement.OriginCell], OldFoundation = Grid.Foundation[placement.OriginCell]
            };
        }

        private static void RevalidateTileReplacementTarget(TileReplacementTarget target, JObject args, bool execute)
        {
            NativeTileReplacement.RequireNormalMode();
            if (target.Game == null || !ReferenceEquals(Game.Instance, target.Game)
                || target.Generation != GameContextLifecycle.CaptureGeneration() || !OldTilePreserved(target))
                throw new NativeTileReplacement.Failure("target_changed");
            if (ClusterManager.Instance == null || ClusterManager.Instance.activeWorldId != target.Placement.WorldId
                || SpeedControlScreen.Instance == null || !SpeedControlScreen.Instance.IsPaused)
                throw new NativeTileReplacement.Failure("scope_unavailable");
            if (execute && (!BuildingControlTools.IsVirtualFileEditContext || IsDryRun(args)
                || !ToolUtil.GetBool(args, "confirm", false))) throw new NativeTileReplacement.Failure("permission_required");
            if (!SingleCellMaterialTileDefinition(target.Def) || !SingleCellMaterialTileDefinition(target.OldDef))
                throw new NativeTileReplacement.Failure("scope_unavailable");
            RequireNativeTileReplacementCandidate(target);
            if (Grid.Solid[target.Placement.OriginCell] && !Grid.Foundation[target.Placement.OriginCell])
                throw new NativeTileReplacement.Failure("terrain_unverifiable");
            if (target.Def.EquivalentReplacementLayers != null)
                foreach (var layer in target.Def.EquivalentReplacementLayers)
                {
                    if ((int)layer < 0 || (int)layer >= (int)ObjectLayer.NumLayers) throw new NativeTileReplacement.Failure("scope_unavailable");
                    if (layer != ObjectLayer.ReplacementTile && Grid.Objects[target.Placement.OriginCell, (int)layer] != null)
                        throw new NativeTileReplacement.Failure("pending_conflict");
                }
            if (Grid.Objects[target.Placement.OriginCell, (int)ObjectLayer.ReplacementTile] == null
                && target.Def.IsReplacementLayerOccupied(target.Placement.OriginCell))
                throw new NativeTileReplacement.Failure("pending_conflict");
            var existingQueue = Grid.Objects[target.Placement.OriginCell, (int)ObjectLayer.ReplacementTile];
            if (existingQueue != null)
            {
                if (!MatchingTileReplacementQueue(target, existingQueue)) throw new NativeTileReplacement.Failure("pending_conflict");
                RequireReplacementReusePhysicalScope(target, existingQueue);
            }
            else foreach (var obstruction in FindFootprintObstructions(target.Placement, target.Old))
            {
                if (!ExpectedOldTileObstruction(target, obstruction))
                    throw new NativeTileReplacement.Failure("protected_state_changed");
            }
            var deconstruct = target.Old.GetComponent<Deconstructable>();
            if (deconstruct == null) throw new NativeTileReplacement.Failure("target_unverifiable");
            if (deconstruct.IsMarkedForDeconstruction() || target.Old.GetComponent<Reconstructable>()?.ReconstructRequested == true
                || target.Placement.Footprint.Any(cell => Grid.Objects[cell.Cell, (int)ObjectLayer.DigPlacer] != null))
                throw new NativeTileReplacement.Failure("marked_work_conflict");
        }

        private static bool ExpectedOldTileObstruction(TileReplacementTarget target, Dictionary<string, object> obstruction)
        {
            if (!obstruction.TryGetValue("kind", out object kind) || !obstruction.TryGetValue("cell", out object cell)
                || !(cell is int oldCell) || oldCell != target.Placement.OriginCell) return false;
            if ((string)kind == "solid_cell") return Grid.Foundation[oldCell]
                && ReferenceEquals(Grid.Objects[oldCell, (int)ObjectLayer.FoundationTile], target.Old);
            return (string)kind == "building_layer_conflict"
                && ReferenceEquals(Grid.Objects[oldCell, (int)ObjectLayer.Building], target.Old);
        }

        private static void RequireReplacementReusePhysicalScope(TileReplacementTarget target, GameObject allowedQueue)
        {
            var cells = new HashSet<int>(target.Placement.Footprint.Select(cell => cell.Cell));
            var objects = UnityEngine.Object.FindObjectsByType<BuildingComplete>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(item => item?.gameObject).Concat(UnityEngine.Object.FindObjectsByType<Constructable>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Select(item => item?.gameObject));
            foreach (var go in objects)
            {
                if (go == null || ReferenceEquals(go, target.Old) || ReferenceEquals(go, allowedQueue)
                    || !ToolUtil.GameObjectMatchesWorld(go, target.Placement.WorldId)) continue;
                var def = go.GetComponent<Building>()?.Def;
                if (def != null && IsUtilityPrefab(def.PrefabID)) continue;
                if (ExistingObjectFootprint(go, def, "building", cells).Any())
                    throw new NativeTileReplacement.Failure("protected_state_changed");
            }
            if (FindBuildingLayerConflicts(target.Def, target.Placement).Any(item => !ExpectedOldTileObstruction(target, item))
                || target.Placement.Footprint.Any(cell => UprootableObstructionsAtCell(cell.Cell, target.Placement.WorldId).Any()))
                throw new NativeTileReplacement.Failure("protected_state_changed");
        }

        private static bool OldTilePreserved(TileReplacementTarget target)
        {
            var old = target.Old;
            var primary = old == null ? null : old.GetComponent<PrimaryElement>();
            return old != null && old.GetComponent<KPrefabID>()?.InstanceID == target.OldId
                && ReferenceEquals(old.GetComponent<BuildingComplete>()?.Def, target.OldDef)
                && ReferenceEquals(old.GetComponent<Building>()?.Def, target.OldDef)
                && primary != null && primary.ElementID.ToString() == target.OldMaterial && primary.Mass == target.OldMass
                && old.GetComponent<BuildingFacade>() != null
                && string.Equals(old.GetComponent<BuildingFacade>().CurrentFacade, target.OldFacade, StringComparison.Ordinal)
                && ToolUtil.GameObjectMatchesWorld(old, target.Placement.WorldId)
                && Grid.Solid[target.Placement.OriginCell] == target.OldSolid
                && Grid.Foundation[target.Placement.OriginCell] == target.OldFoundation
                && GetBool(ComparePlacement(target.OldPlacement,
                    ActualPlacementDetails(old, target.OldDef, target.Placement.AnchorX, target.Placement.AnchorY)), "valid")
                && IsCompletedBuildFullyRegistered(target.OldDef, target.OldPlacement, target.OldPlacement.OriginCell,
                    Orientation.Neutral, old, out _);
        }

        private static bool MatchingTileReplacementQueue(TileReplacementTarget target, GameObject queue)
        {
            if (queue == null || queue.GetComponent<KPrefabID>() == null || queue.GetComponent<KPrefabID>().InstanceID <= 0
                || !ReferenceEquals(queue.GetComponent<Building>()?.Def, target.Def)
                || !ToolUtil.GameObjectMatchesWorld(queue, target.Placement.WorldId)) return false;
            var constructable = queue.GetComponent<Constructable>();
            var elements = constructable?.SelectedElementsTags;
            var prioritizable = queue.GetComponent<Prioritizable>();
            if (constructable == null || !constructable.IsReplacementTile || elements == null || elements.Count != 1
                || elements[0] != target.Material || prioritizable == null
                || !NativeBlueprintInstantCompletion.FacadeEquivalent(queue.GetComponent<BuildingFacade>(), target.Facade)) return false;
            var priority = prioritizable.GetMasterPriority();
            return priority.priority_class == PriorityScreen.PriorityClass.basic && priority.priority_value == target.Priority
                && (queue.GetComponent<Rotatable>()?.GetOrientation() ?? Orientation.Neutral) == Orientation.Neutral
                && GetBool(ComparePlacement(target.Placement,
                    ActualPlacementDetails(queue, target.Def, target.Placement.AnchorX, target.Placement.AnchorY)), "valid")
                && target.Placement.Footprint.All(cell => ReferenceEquals(Grid.Objects[cell.Cell, (int)ObjectLayer.ReplacementTile], queue));
        }

        private static TileReplacementObservation ObserveTileReplacement(TileReplacementTarget target)
        {
            if (!ReferenceEquals(Game.Instance, target.Game) || target.Generation != GameContextLifecycle.CaptureGeneration()
                || Game.Instance == null || Game.Instance.IsLoading()) throw new NativeTileReplacement.Failure("observation_unavailable");
            var observation = new TileReplacementObservation
            {
                Queue = Grid.Objects[target.Placement.OriginCell, (int)ObjectLayer.ReplacementTile]
            };
            foreach (var cell in target.Placement.Footprint)
            {
                if (!cell.Valid) throw new NativeTileReplacement.Failure("observation_unavailable");
                observation.Solid.Add(Grid.Solid[cell.Cell]);
                observation.Foundation.Add(Grid.Foundation[cell.Cell]);
                for (int layer = 0; layer < (int)ObjectLayer.NumLayers; layer++) observation.Objects.Add(Grid.Objects[cell.Cell, layer]);
            }
            foreach (var item in UnityEngine.Object.FindObjectsByType<Constructable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = item == null ? null : item.gameObject;
                if (go != null && ReferenceEquals(go.GetComponent<Building>()?.Def, target.Def)
                    && ToolUtil.GameObjectMatchesWorld(go, target.Placement.WorldId)
                    && target.Placement.Footprint.Any(cell => cell.Cell == Grid.PosToCell(go)))
                    observation.CandidateIds.Add(go.GetInstanceID());
            }
            return observation;
        }

        private static bool TileReplacementObservationSame(TileReplacementObservation before, TileReplacementObservation after)
        {
            if (!before.Solid.SequenceEqual(after.Solid) || !before.Foundation.SequenceEqual(after.Foundation) || before.Objects.Count != after.Objects.Count
                || !before.CandidateIds.SetEquals(after.CandidateIds)) return false;
            for (int i = 0; i < before.Objects.Count; i++) if (!ReferenceEquals(before.Objects[i], after.Objects[i])) return false;
            return true;
        }

        private static bool TileReplacementProtectedSame(TileReplacementObservation before, TileReplacementObservation after)
        {
            if (!before.Solid.SequenceEqual(after.Solid) || !before.Foundation.SequenceEqual(after.Foundation) || before.Objects.Count != after.Objects.Count) return false;
            for (int i = 0; i < before.Objects.Count; i++)
                if (i % (int)ObjectLayer.NumLayers != (int)ObjectLayer.ReplacementTile
                    && !ReferenceEquals(before.Objects[i], after.Objects[i])) return false;
            return true;
        }

        private static object TileReplacementValue(bool? value) => value.HasValue ? (object)value.Value : JValue.CreateNull();
    }
}
