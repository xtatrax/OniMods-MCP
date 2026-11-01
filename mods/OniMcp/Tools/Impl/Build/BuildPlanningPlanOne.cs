using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static Dictionary<string, object> TryPlanOne(string prefabId, int x, int y, JObject args, HashSet<int> plannedSupportCells = null, AutoDigContext autoDigContext = null)
        {
            string resolvedPrefabId;
            string resolveError;
            var def = ResolveBuildingDef(prefabId, out resolvedPrefabId, out resolveError);
            if (def == null)
                return ErrorResult(prefabId, x, y, resolveError);
prefabId = resolvedPrefabId;

            BuildingAvailabilityReader.Observation availability;
            string availabilityError = BuildAvailabilityError(def, args, out availability);
            if (availabilityError != null)
            {
                // Geometry remains inspectable while research/availability still prevents placement.
                var unavailablePlacement = BuildPlacementDetails(def, x, y, ToolUtil.ResolveWorldId(args),
                    ParseOrientation(args["orientation"]?.ToString()));
                return ErrorResult(prefabId, x, y, availabilityError, new Dictionary<string, object>
                {
                    ["prefabId"] = prefabId,
                    ["unlocked"] = BuildingAvailabilityReader.JsonBoolean(availability.Unlocked),
                    ["availableNow"] = BuildingAvailabilityReader.JsonBoolean(availability.Value),
                    ["availability"] = availability.ToDictionary(),
                    ["placement"] = unavailablePlacement.ToDictionary(),
                    ["footprint"] = unavailablePlacement.Footprint.Select(c => c.ToDictionary()).ToList()
                });
            }

            int cell = Grid.XYToCell(x, y);
            if (!Grid.IsValidBuildingCell(cell) || !Grid.IsVisible(cell))
                return ErrorResult(prefabId, x, y, "Invalid or not visible cell");

            int worldId = ToolUtil.ResolveWorldId(args);
            if (!ToolUtil.CellMatchesWorld(cell, worldId))
                return ErrorResult(prefabId, x, y, $"Cell is not in worldId={worldId}");

            var orientation = ParseOrientation(args["orientation"]?.ToString());
            var earlyPlacement = BuildPlacementDetails(def, x, y, worldId, orientation);
            if (!earlyPlacement.GeometryAvailable)
                return ErrorResult(prefabId, x, y, "Native footprint unavailable: " + earlyPlacement.GeometryError,
                    ValidateFootprint(earlyPlacement).ToDictionary(earlyPlacement));
            var terrainConflict = UtilityTerrainConflict(def,
                earlyPlacement.Footprint.Select(c => c.Cell), ToolUtil.ResolveWorldId(args),
                ToolUtil.GetBool(args, "allowUtilityTerrainDig", false),
                System.Math.Max(1, System.Math.Min(ToolUtil.GetInt(args, "maxAutoDigCells") ?? 100, 500)),
                ToolUtil.GetBool(args, "autoDigObstructions", true));
            if (terrainConflict != null && !GetBool(terrainConflict, "allowed"))
                return ErrorResult(prefabId, x, y, terrainConflict["error"].ToString(), terrainConflict);
            var replacementReuse = TryReuseNativeTileReplacementQueue(def, earlyPlacement, args);
            if (replacementReuse != null)
                return replacementReuse;
            var earlyExistingBuild = ExistingMatchingBuildAtPlacement(def, earlyPlacement);
            if (earlyExistingBuild != null && !IsNativeTileReplacementCandidateScope(def, earlyPlacement)
                && ExistingMaterialRequestSatisfied(def, earlyExistingBuild, args["material"]?.ToString()))
            {
                var instantRetry = TryCompleteExistingVirtualFileBlueprint(def, earlyPlacement, args, earlyExistingBuild);
                if (instantRetry != null)
                    return instantRetry;
                RegisterSupportBlueprint(prefabId, x, y, plannedSupportCells);
                return new Dictionary<string, object>
                {
                    ["planned"] = false,
                    ["blueprintPlaced"] = false,
                    ["alreadyPresent"] = true,
                    ["alreadyBlueprint"] = string.Equals(earlyExistingBuild["kind"]?.ToString(), "blueprint", StringComparison.OrdinalIgnoreCase),
                    ["alreadyBuilding"] = string.Equals(earlyExistingBuild["kind"]?.ToString(), "building", StringComparison.OrdinalIgnoreCase),
                    ["valid"] = true,
                    ["prefabId"] = prefabId,
                    ["name"] = ToolUtil.CleanName(def.Name),
                    ["x"] = x,
                    ["y"] = y,
                    ["anchor"] = AnchorDictionary(x, y, worldId),
                    ["worldId"] = worldId,
                    ["placement"] = earlyPlacement.ToDictionary(),
                    ["footprint"] = earlyPlacement.Footprint.Select(cellInfo => cellInfo.ToDictionary()).ToList(),
                    ["existing"] = earlyExistingBuild,
                    ["validationScope"] = "existing_object_reuse",
                    ["support"] = new Dictionary<string, object>
                    {
                        ["status"] = "not_evaluated",
                        ["currentValid"] = null,
                        ["nativeFoundationValid"] = null,
                        ["requiresSupportCompletion"] = null
                    }
                };
            }
            var materialResult = SelectElements(def, args["material"]?.ToString(), worldId);
            materialResult.RequiredKg = RequiredMaterialKg(def);
            if (!materialResult.Valid)
                return ErrorResult(prefabId, x, y, materialResult.Error, materialResult.ToDictionary());

            var facadeResult = ResolveFacade(def, args["facade"]?.ToString() ?? args["facadeId"]?.ToString());
            if (!facadeResult.Valid)
                return ErrorResult(prefabId, x, y, facadeResult.Error);

            var tileReplacement = TryPlanNativeTileReplacement(def, earlyPlacement, args, materialResult, facadeResult);
            if (tileReplacement != null)
                return tileReplacement;

            var supportResult = ValidateSupport(def, x, y, ToolUtil.GetBool(args, "allowUnsupported", false), plannedSupportCells, orientation);
            if (!supportResult.Valid)
                return ErrorResult(prefabId, x, y, supportResult.Error, new Dictionary<string, object>
                {
                    ["reasonCode"] = "unsupported", ["worldId"] = worldId,
                    ["support"] = supportResult.ToDictionary(),
                    ["missingSupportCells"] = supportResult.MissingSupportCells
                });

            var placement = BuildPlacementDetails(def, x, y, worldId, orientation);
            var footprintResult = ValidateFootprint(placement);
            var existingBuild = ExistingMatchingBuildAtPlacement(def, placement);
            if (existingBuild != null)
            {
                var materialMismatch = ExistingMaterialMismatchResult(
                    prefabId, x, y, existingBuild, materialResult, args["material"]?.ToString());
                if (materialMismatch != null)
                    return materialMismatch;
                var instantRetry = TryCompleteExistingVirtualFileBlueprint(def, placement, args, existingBuild);
                if (instantRetry != null)
                    return instantRetry;
                RegisterSupportBlueprint(prefabId, x, y, plannedSupportCells);
                return new Dictionary<string, object>
                {
                    ["planned"] = false,
                    ["blueprintPlaced"] = false,
                    ["alreadyPresent"] = true,
                    ["alreadyBlueprint"] = string.Equals(existingBuild["kind"]?.ToString(), "blueprint", StringComparison.OrdinalIgnoreCase),
                    ["alreadyBuilding"] = string.Equals(existingBuild["kind"]?.ToString(), "building", StringComparison.OrdinalIgnoreCase),
                    ["valid"] = true,
                    ["prefabId"] = prefabId,
                    ["name"] = ToolUtil.CleanName(def.Name),
                    ["x"] = x,
                    ["y"] = y,
                    ["anchor"] = AnchorDictionary(x, y, worldId),
                    ["worldId"] = worldId,
                    ["placement"] = placement.ToDictionary(),
                    ["footprint"] = placement.Footprint.Select(cellInfo => cellInfo.ToDictionary()).ToList(),
                    ["existing"] = existingBuild,
                    ["support"] = supportResult.ToDictionary(),
                    ["material"] = materialResult.Elements.Select(tag => tag.Name).ToList(),
                    ["materialSelection"] = materialResult.ToDictionary(),
                    ["materials"] = materialResult.ToDictionary(),
                    ["facade"] = facadeResult.ResponseId
                };
            }
            Dictionary<string, object> autoDig = null;
            if (!footprintResult.Valid)
            {
                var details = footprintResult.ToDictionary(placement);
                autoDig = TryAutoDigObstructions(placement, footprintResult, args, autoDigContext);
                if (autoDig == null)
                    return ErrorResult(prefabId, x, y, footprintResult.Error, details);
                details["autoDig"] = autoDig;
                if (!GetBool(autoDig, "available") || GetInt(autoDig, "failed") > 0 || GetInt(autoDig, "skipped") > 0)
                    return ErrorResult(prefabId, x, y, footprintResult.Error, details);
            }

            var existingUtility = ExistingMatchingUtilityAtPlacement(def, placement);
            if (existingUtility != null)
            {
                var materialMismatch = ExistingMaterialMismatchResult(
                    prefabId, x, y, existingUtility, materialResult, args["material"]?.ToString());
                if (materialMismatch != null)
                    return materialMismatch;
                RegisterSupportBlueprint(prefabId, x, y, plannedSupportCells);
                return ExistingPlacementResult(def, placement, existingUtility, utility: true);
            }

            if (IsDryRun(args))
            {
                RegisterSupportBlueprint(prefabId, x, y, plannedSupportCells);
                var powerAutoConnect = TryAutoConnectPower(def, placement.OriginX, placement.OriginY, orientation, args, plannedSupportCells, autoDigContext);
                return new Dictionary<string, object>
                {
                    ["planned"] = false,
                    ["blueprintPlaced"] = false,
                    ["actualAnchor"] = null,
                    ["valid"] = true,
                    ["dryRun"] = true,
                    ["prefabId"] = prefabId,
                    ["name"] = ToolUtil.CleanName(def.Name),
                    ["x"] = x,
                    ["y"] = y,
                    ["anchor"] = AnchorDictionary(x, y, worldId),
                    ["worldId"] = worldId,
                    ["placement"] = placement.ToDictionary(),
                    ["footprint"] = placement.Footprint.Select(cellInfo => cellInfo.ToDictionary()).ToList(),
                    ["support"] = supportResult.ToDictionary(),
                    ["material"] = materialResult.Elements.Select(tag => tag.Name).ToList(),
                    ["materialSelection"] = materialResult.ToDictionary(),
                    ["materials"] = materialResult.ToDictionary(),
                    ["facade"] = facadeResult.ResponseId,
                    ["powerAutoConnect"] = powerAutoConnect,
                    ["terrainImpact"] = terrainConflict,
                    ["autoDig"] = autoDig
                };
            }

            var executionExistingBuild = ExistingMatchingBuildAtPlacement(def, placement);
            if (executionExistingBuild != null)
            {
                var materialMismatch = ExistingMaterialMismatchResult(
                    prefabId, x, y, executionExistingBuild, materialResult, args["material"]?.ToString());
                if (materialMismatch != null)
                    return materialMismatch;
                var instantRetry = TryCompleteExistingVirtualFileBlueprint(def, placement, args, executionExistingBuild);
                if (instantRetry != null)
                    return instantRetry;
                return ExistingPlacementResult(def, placement, executionExistingBuild, utility: false);
            }

            var executionExistingUtility = ExistingMatchingUtilityAtPlacement(def, placement);
            if (executionExistingUtility != null)
            {
                var materialMismatch = ExistingMaterialMismatchResult(
                    prefabId, x, y, executionExistingUtility, materialResult, args["material"]?.ToString());
                if (materialMismatch != null)
                    return materialMismatch;
                return ExistingPlacementResult(def, placement, executionExistingUtility, utility: true);
            }

            var executionFootprintResult = ValidateFootprint(placement);
            if (HasUnsafeExecutionConflict(executionFootprintResult))
            {
                var executionDetails = executionFootprintResult.ToDictionary(placement);
                executionDetails["safety"] = "execution_pre_place_recheck";
                executionDetails["reasonCode"] = IsLinearUtilityPrefab(prefabId)
                    ? "utility_path_conflict"
                    : "placement_conflict";
                return ErrorResult(prefabId, x, y,
                    "Placement safety changed after preflight; refusing to stomp an existing building, endpoint, connector, or utility object",
                    executionDetails);
            }

            Dictionary<string, object> fallbackPlacement = null;
            Dictionary<string, object> instantCompletion = null;
            bool completedImmediately = IsAuthorizedVirtualFileInstantBuild(args);
            GameObject go;
            cell = placement.OriginCell;
            if (completedImmediately)
            {
                if (!TryBuildVirtualFileInstantBuild(def, placement, args, cell, orientation,
                    materialResult.Elements, facadeResult.ResponseId, out go, out instantCompletion))
                    return InstantCompletionFailureResult(def, placement, null, instantCompletion, placedByThisRequest: false);
            }
            else
            {
                var pos = BuildPlacementPosition(cell, def);
                go = def.TryPlace(null, pos, orientation, materialResult.Elements, facadeResult.TryPlaceId);
                if (go == null && autoDig != null)
                    go = TryPlaceWithBuildTool(def, cell, orientation, materialResult.Elements,
                        facadeResult.ResponseId, placement, args, out fallbackPlacement);
                if (go == null)
                {
                    var failureDetails = BuildPlacementFailureDetails(placement, materialResult);
                    if (autoDig != null)
                        failureDetails["autoDig"] = autoDig;
                    if (fallbackPlacement != null)
                        failureDetails["fallbackPlacement"] = fallbackPlacement;
                    return ErrorResult(prefabId, x, y, "Placement failed", failureDetails);
                }
                SetPriority(go, ToolUtil.GetInt(args, "priority") ?? 5);
            }
            NativeBuildingCellIndex.Invalidate();
            RegisterSupportBlueprint(prefabId, x, y, plannedSupportCells);
            var actualPlacement = ActualPlacementDetails(go, def, x, y);
            var placedPowerAutoConnect = TryAutoConnectPower(def, placement.OriginX, placement.OriginY, orientation, args, plannedSupportCells, autoDigContext);
            return new Dictionary<string, object>
            {
                ["planned"] = true,
                ["blueprintPlaced"] = !completedImmediately,
                ["buildingCompleted"] = completedImmediately,
                ["valid"] = true,
                ["prefabId"] = prefabId,
                ["name"] = ToolUtil.CleanName(def.Name),
                ["x"] = x,
                ["y"] = y,
                ["anchor"] = AnchorDictionary(x, y, worldId),
                ["worldId"] = worldId,
                ["placement"] = placement.ToDictionary(),
                ["footprint"] = placement.Footprint.Select(cellInfo => cellInfo.ToDictionary()).ToList(),
                ["actualPlacement"] = actualPlacement,
                ["actualAnchor"] = ActualAnchorArray(actualPlacement),
                ["placementCheck"] = ComparePlacement(placement, actualPlacement),
                ["fallbackPlacement"] = fallbackPlacement,
                ["support"] = supportResult.ToDictionary(),
                ["material"] = materialResult.Elements.Select(tag => tag.Name).ToList(),
                ["materialSelection"] = materialResult.ToDictionary(),
                ["materials"] = materialResult.ToDictionary(),
                ["facade"] = facadeResult.ResponseId,
                ["powerAutoConnect"] = placedPowerAutoConnect,
                ["terrainImpact"] = terrainConflict,
                ["autoDig"] = autoDig,
                ["instantCompletion"] = instantCompletion,
                ["id"] = go.GetComponent<KPrefabID>()?.InstanceID ?? -1
            };
        }
    }
}
