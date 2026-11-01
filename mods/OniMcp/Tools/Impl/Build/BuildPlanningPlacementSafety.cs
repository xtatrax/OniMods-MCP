using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private sealed class UtilityPathSafety
        {
            public readonly List<Dictionary<string, object>> Conflicts = new List<Dictionary<string, object>>();
            public int ExistingSamePrefabCells;
            public bool HasRepeatedCells;
            public bool Valid => Conflicts.Count == 0;
            public bool RequiresCellFallback => ExistingSamePrefabCells > 0 || HasRepeatedCells;
        }

        private static List<Dictionary<string, object>> FindUtilityLayerConflicts(
            BuildingDef def, PlacementDetails placement, GameObject ignored = null)
        {
            var conflicts = new List<Dictionary<string, object>>();
            if (def == null || placement == null)
                return conflicts;
            if (UsesNativeBridgeEndpointRegistration(def))
                return FindBridgeEndpointLayerConflicts(def, placement, ignored);
            if (!IsLinearUtilityPrefab(def.PrefabID))
                return conflicts;

            var seen = new HashSet<int>();
            var layers = UtilityLayersForPrefab(def.PrefabID);
            foreach (var cellInfo in placement.Footprint)
            {
                if (!cellInfo.Valid || !cellInfo.InWorld)
                    continue;

                foreach (var layer in layers)
                {
                    var existing = Grid.Objects[cellInfo.Cell, (int)layer];
                    if (existing == null || !seen.Add(existing.GetInstanceID()))
                        continue;

                    string actualPrefabId = PlacementObjectPrefabId(existing);
                    if (EqualsIgnoreCase(actualPrefabId, def.PrefabID))
                        continue;

                    conflicts.Add(new Dictionary<string, object>
                    {
                        ["kind"] = "utility_layer_conflict",
                        ["reasonCode"] = "utility_layer_occupied_by_different_prefab",
                        ["reason"] = "target utility layer is occupied by a different object; placement was rejected before ONI could stomp the existing endpoint or connector",
                        ["expectedPrefabId"] = def.PrefabID,
                        ["actualPrefabId"] = actualPrefabId,
                        ["actualName"] = ToolUtil.CleanName(existing.GetProperName()),
                        ["layer"] = layer.ToString(),
                        ["x"] = cellInfo.X,
                        ["y"] = cellInfo.Y,
                        ["cell"] = cellInfo.Cell
                    });
                }
            }
            return conflicts;
        }

        private sealed class BridgeEndpointTarget
        {
            public int Cell;
            public ObjectLayer Layer;
            public string Role;
        }

        private static bool UsesNativeBridgeEndpointRegistration(BuildingDef def)
        {
            return def != null && (def.BuildLocationRule == BuildLocationRule.Conduit
                || def.BuildLocationRule == BuildLocationRule.WireBridge
                || def.BuildLocationRule == BuildLocationRule.LogicBridge);
        }

        private static List<Dictionary<string, object>> FindBridgeEndpointLayerConflicts(
            BuildingDef def, PlacementDetails placement, GameObject ignored)
        {
            var conflicts = new List<Dictionary<string, object>>();
            var seen = new HashSet<string>();
            var targets = NativeBridgeEndpointTargets(def, placement).ToList();
            if (targets.Count == 0)
            {
                conflicts.Add(new Dictionary<string, object>
                {
                    ["kind"] = "bridge_endpoint_conflict",
                    ["reasonCode"] = "bridge_endpoint_metadata_missing",
                    ["reason"] = "native bridge endpoint metadata is unavailable; placement failed closed before mutation",
                    ["expectedPrefabId"] = def.PrefabID
                });
                return conflicts;
            }
            foreach (var target in targets)
            {
                string targetKey = target.Cell + "|" + (int)target.Layer;
                if (!seen.Add(targetKey))
                    continue;
                if (!Grid.IsValidCell(target.Cell) || !Grid.IsVisible(target.Cell)
                    || !ToolUtil.CellMatchesWorld(target.Cell, placement.WorldId))
                {
                    conflicts.Add(new Dictionary<string, object>
                    {
                        ["kind"] = "bridge_endpoint_conflict",
                        ["reasonCode"] = "bridge_endpoint_invalid",
                        ["reason"] = "native bridge endpoint must be visible, valid, and inside the selected world",
                        ["expectedPrefabId"] = def.PrefabID,
                        ["role"] = target.Role,
                        ["layer"] = target.Layer.ToString(),
                        ["cell"] = target.Cell
                    });
                    continue;
                }

                var existing = Grid.Objects[target.Cell, (int)target.Layer];
                if (existing == null || existing == ignored)
                    continue;
                string actualPrefabId = PlacementObjectPrefabId(existing);
                conflicts.Add(new Dictionary<string, object>
                {
                    ["kind"] = "bridge_endpoint_conflict",
                    ["reasonCode"] = "bridge_endpoint_layer_occupied",
                    ["reason"] = "native bridge endpoint layer is already occupied; placement was rejected before BuildingDef.MarkArea could overwrite or invalidate a different object",
                    ["expectedPrefabId"] = def.PrefabID,
                    ["actualPrefabId"] = actualPrefabId,
                    ["samePrefab"] = EqualsIgnoreCase(actualPrefabId, def.PrefabID),
                    ["actualName"] = ToolUtil.CleanName(existing.GetProperName()),
                    ["role"] = target.Role,
                    ["layer"] = target.Layer.ToString(),
                    ["x"] = Grid.CellColumn(target.Cell),
                    ["y"] = Grid.CellRow(target.Cell),
                    ["cell"] = target.Cell
                });
            }
            return conflicts;
        }

        private static Dictionary<string, object> ExistingBlueprintCompletionSafetyFailure(
            BuildingDef def, PlacementDetails placement, GameObject blueprint)
        {
            var validation = ValidateFootprint(placement, blueprint);
            var backwalls = placement.Footprint.Where(cell => cell.Valid
                && Grid.Objects[cell.Cell, (int)ObjectLayer.Backwall] != null).Select(cell => cell.Cell).ToList();
            // The instant native tool can clear terrain or vegetation. This existing-blueprint
            // scope authorizes neither, including when ordinary auto-dig options are enabled.
            if (placement.GeometryAvailable && validation.InvalidCells.Count == 0
                && validation.Obstructions.Count == 0 && backwalls.Count == 0)
                return null;
            return new Dictionary<string, object>
            {
                ["reasonCode"] = "safety_blocked",
                ["safety"] = "existing_blueprint_pre_completion_recheck",
                ["invalidCells"] = validation.InvalidCells,
                ["obstructions"] = validation.Obstructions,
                ["backwallCells"] = backwalls
            };
        }

        private static IEnumerable<BridgeEndpointTarget> NativeBridgeEndpointTargets(
            BuildingDef def, PlacementDetails placement)
        {
            int anchorCell = placement.OriginCell;
            if (def.BuildLocationRule == BuildLocationRule.Conduit)
            {
                if (def.InputConduitType != ConduitType.None)
                    yield return BridgeEndpointTargetForOffset(anchorCell, placement.Orientation,
                        def.UtilityInputOffset, Grid.GetObjectLayerForConduitType(def.InputConduitType), "input");
                if (def.OutputConduitType != ConduitType.None)
                    yield return BridgeEndpointTargetForOffset(anchorCell, placement.Orientation,
                        def.UtilityOutputOffset, Grid.GetObjectLayerForConduitType(def.OutputConduitType), "output");
                yield break;
            }

            if (def.BuildLocationRule == BuildLocationRule.WireBridge)
            {
                var link = def.BuildingComplete?.GetComponent<UtilityNetworkLink>();
                if (link == null)
                    yield break;
                link.GetCells(anchorCell, placement.Orientation, out int linkedCellOne, out int linkedCellTwo);
                yield return new BridgeEndpointTarget { Cell = linkedCellOne, Layer = ObjectLayer.WireConnectors, Role = "link1" };
                yield return new BridgeEndpointTarget { Cell = linkedCellTwo, Layer = ObjectLayer.WireConnectors, Role = "link2" };
                yield break;
            }

            var ports = def.BuildingComplete?.GetComponent<LogicPorts>();
            if (ports?.inputPortInfo == null)
                yield break;
            foreach (var port in ports.inputPortInfo)
                yield return BridgeEndpointTargetForOffset(anchorCell, placement.Orientation,
                    port.cellOffset, def.ObjectLayer, "logic_input");
        }

        private static BridgeEndpointTarget BridgeEndpointTargetForOffset(
            int anchorCell, Orientation orientation, CellOffset offset, ObjectLayer layer, string role)
        {
            var rotated = Rotatable.GetRotatedCellOffset(offset, orientation);
            return new BridgeEndpointTarget
            {
                Cell = Grid.OffsetCell(anchorCell, rotated),
                Layer = layer,
                Role = role
            };
        }

        private static IEnumerable<FootprintCell> PlacementSafetyFootprint(BuildingDef def, PlacementDetails placement)
        {
            return placement.Footprint;
        }

        private static string PlacementObjectPrefabId(GameObject go)
        {
            if (go == null)
                return string.Empty;
            var building = go.GetComponent<Building>();
            return building?.Def?.PrefabID ?? go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name;
        }

        // A native-confirmed standard liquid bridge uses its connection layer;
        // only completed, known ordinary foreground or a canonical ladder may
        // share its visible cells.
        private static bool CanLiquidBridgeShareCompletedForeground(BuildingDef requested, PlacementDetails placement, GameObject foreground)
        {
            if (requested == null || placement == null || foreground == null
                || requested.PrefabID != "LiquidConduitBridge" || placement.PrefabId != requested.PrefabID
                || requested.BuildLocationRule != BuildLocationRule.Conduit
                || requested.ObjectLayer != ObjectLayer.LiquidConduitConnection
                || requested.TileLayer != ObjectLayer.NumLayers || requested.ReplacementLayer != ObjectLayer.NumLayers
                || requested.IsTilePiece || requested.DragBuild || requested.WidthInCells != 3 || requested.HeightInCells != 1
                || requested.PlacementOffsets == null || requested.PlacementOffsets.Length != 3
                || requested.PlacementOffsets.Any(offset => offset.y != 0 || offset.x < -1 || offset.x > 1)
                || !requested.PlacementOffsets.Any(offset => offset.x == -1)
                || !requested.PlacementOffsets.Any(offset => offset.x == 0)
                || !requested.PlacementOffsets.Any(offset => offset.x == 1)
                || requested.InputConduitType != ConduitType.Liquid || requested.OutputConduitType != ConduitType.Liquid
                || requested.UtilityInputOffset.x != -1 || requested.UtilityInputOffset.y != 0
                || requested.UtilityOutputOffset.x != 1 || requested.UtilityOutputOffset.y != 0
                || requested.RequiresPowerInput || requested.RequiresPowerOutput
                || requested.PermittedRotations != PermittedRotations.R360 || requested.BuildingComplete == null
                || requested.BuildingComplete.GetComponent<LogicPorts>() != null
                || requested.BuildingComplete.GetComponents<ISecondaryInput>().Length != 0
                || requested.BuildingComplete.GetComponents<ISecondaryOutput>().Length != 0
                || !placement.GeometryAvailable || placement.FillsBounds != true || placement.WorldId < 0
                || placement.Footprint == null || placement.Footprint.Count != 3
                || placement.Footprint.Select(cell => cell.Cell).Distinct().Count() != 3
                || !placement.Footprint.Any(cell => cell.Cell == placement.OriginCell)
                || (placement.Orientation != Orientation.Neutral && placement.Orientation != Orientation.R90
                    && placement.Orientation != Orientation.R180 && placement.Orientation != Orientation.R270)
                || placement.Footprint.Any(cell => !cell.Valid || !cell.Visible || !cell.InWorld
                    || !Grid.IsValidCell(cell.Cell) || !Grid.IsVisible(cell.Cell) || !ToolUtil.CellMatchesWorld(cell.Cell, placement.WorldId)))
                return false;
            var def = foreground.GetComponent<Building>()?.Def;
            var complete = foreground.GetComponent<BuildingComplete>();
            bool nativeLadder = def != null && def.PrefabID == "Ladder"
                && def.ObjectLayer == ObjectLayer.Building && def.TileLayer == ObjectLayer.LadderTile
                && def.ReplacementLayer == ObjectLayer.ReplacementLadder && def.IsTilePiece && def.DragBuild
                && def.BuildLocationRule == BuildLocationRule.Anywhere
                && def.PermittedRotations == PermittedRotations.Unrotatable
                && def.WidthInCells == 1 && def.HeightInCells == 1
                && def.PlacementOffsets != null && def.PlacementOffsets.Length == 1
                && def.PlacementOffsets[0].x == 0 && def.PlacementOffsets[0].y == 0;
            if (def == null || string.IsNullOrWhiteSpace(def.PrefabID) || def.PrefabID == requested.PrefabID
                || def.ObjectLayer != ObjectLayer.Building
                || (!nativeLadder && (def.TileLayer != ObjectLayer.NumLayers
                    || def.ReplacementLayer != ObjectLayer.NumLayers || def.IsTilePiece))
                || def.BuildingComplete == null
                || UsesNativeBridgeEndpointRegistration(def) || complete == null || !ReferenceEquals(complete.Def, def)
                || foreground.GetComponent<Constructable>() != null || !ToolUtil.GameObjectMatchesWorld(foreground, placement.WorldId)
                || !placement.Footprint.Any(cell => ReferenceEquals(Grid.Objects[cell.Cell, (int)ObjectLayer.Building], foreground)))
                return false;
            var shape = NativeBuildingCellIndex.Geometry(foreground);
            return shape.Available && shape.Origin != null && shape.Origin.Cell.HasValue && shape.Cells != null
                && shape.Cells.Count > 0 && shape.Cells.All(cell => cell.Cell.HasValue && Grid.IsValidCell(cell.Cell.Value))
                && shape.Cells.Any(cell => placement.Footprint.Any(target => target.Cell == cell.Cell.Value))
                && (!nativeLadder || (shape.Cells.Count == 1 && shape.Cells[0].Cell == shape.Origin.Cell
                    && ReferenceEquals(Grid.Objects[shape.Origin.Cell.Value, (int)ObjectLayer.LadderTile], foreground)));
        }

        private static List<Dictionary<string, object>> FindBuildingLayerConflicts(BuildingDef def, PlacementDetails placement)
        {
            var conflicts = new List<Dictionary<string, object>>();
            if (def == null || placement == null
                || (IsUtilityPrefab(def.PrefabID) && !UsesNativeBridgeEndpointRegistration(def)))
                return conflicts;

            var seen = new HashSet<int>();
            foreach (var cellInfo in PlacementSafetyFootprint(def, placement))
            {
                if (!cellInfo.Valid)
                    continue;
                var existing = Grid.Objects[cellInfo.Cell, (int)ObjectLayer.Building];
                if (existing == null || !seen.Add(existing.GetInstanceID()))
                    continue;
                string actualPrefabId = PlacementObjectPrefabId(existing);
                if (EqualsIgnoreCase(actualPrefabId, def.PrefabID))
                    continue;
                if (CanLiquidBridgeShareCompletedForeground(def, placement, existing))
                    continue;
                conflicts.Add(new Dictionary<string, object>
                {
                    ["kind"] = "building_layer_conflict",
                    ["reasonCode"] = "building_layer_occupied_by_different_prefab",
                    ["reason"] = "requested physical footprint is occupied by a different building object; placement was rejected before construction could replace it",
                    ["expectedPrefabId"] = def.PrefabID,
                    ["actualPrefabId"] = actualPrefabId,
                    ["actualName"] = ToolUtil.CleanName(existing.GetProperName()),
                    ["x"] = cellInfo.X,
                    ["y"] = cellInfo.Y,
                    ["cell"] = cellInfo.Cell
                });
            }
            return conflicts;
        }

        private static List<Dictionary<string, object>> FindLogicEndpointConflicts(BuildingDef def, PlacementDetails placement, PlacementPreviewReadContext previewReads = null)
        {
            var conflicts = new List<Dictionary<string, object>>();
            if (def?.BuildingComplete?.GetComponent<LogicPorts>() == null || placement == null)
                return conflicts;
            if (UsesNativeBridgeEndpointRegistration(def))
                return conflicts;

            var footprintCells = new HashSet<int>(placement.Footprint.Where(item => item.Valid).Select(item => item.Cell));
            var seenObjects = new HashSet<int>();
            foreach (var complete in previewReads == null ? Components.BuildingCompletes.Items : previewReads.CompletedBuildings())
                AddLogicEndpointConflicts(complete?.gameObject, def, footprintCells, seenObjects, conflicts);
            foreach (var constructable in previewReads == null ? FindConstructables(placement.WorldId) : previewReads.Constructables())
                AddLogicEndpointConflicts(constructable?.gameObject, def, footprintCells, seenObjects, conflicts);
            return conflicts;
        }

        private static void AddLogicEndpointConflicts(
            GameObject existing,
            BuildingDef expected,
            HashSet<int> footprintCells,
            HashSet<int> seenObjects,
            List<Dictionary<string, object>> conflicts)
        {
            if (existing == null || !seenObjects.Add(existing.GetInstanceID()))
                return;
            string actualPrefabId = PlacementObjectPrefabId(existing);
            if (EqualsIgnoreCase(actualPrefabId, expected.PrefabID))
                return;

            var ports = existing.GetComponent<LogicPorts>();
            if (ports == null)
                return;
            AddLogicPortGroupConflicts(ports, ports.inputPortInfo, "input", actualPrefabId, footprintCells, conflicts);
            AddLogicPortGroupConflicts(ports, ports.outputPortInfo, "output", actualPrefabId, footprintCells, conflicts);
        }

        private static void AddLogicPortGroupConflicts(
            LogicPorts ports,
            LogicPorts.Port[] portInfos,
            string direction,
            string actualPrefabId,
            HashSet<int> footprintCells,
            List<Dictionary<string, object>> conflicts)
        {
            if (portInfos == null)
                return;
            foreach (var port in portInfos)
            {
                int cell = LogicPortReadSemantics.ActualCell(ports, port);
                if (!Grid.IsValidCell(cell) || !footprintCells.Contains(cell))
                    continue;
                conflicts.Add(new Dictionary<string, object>
                {
                    ["kind"] = "utility_endpoint_conflict",
                    ["reasonCode"] = "logic_endpoint_occupied_by_different_prefab",
                    ["reason"] = "requested logic-building footprint overlaps an existing logic endpoint; placement was rejected before endpoint registration could stomp it",
                    ["actualPrefabId"] = actualPrefabId,
                    ["portDirection"] = direction,
                    ["portId"] = port.id.ToString(),
                    ["x"] = Grid.CellColumn(cell),
                    ["y"] = Grid.CellRow(cell),
                    ["cell"] = cell
                });
            }
        }

        private static UtilityPathSafety ValidateUtilityPathSafety(BuildingDef def, List<CellCoord> path, int worldId)
        {
            var result = new UtilityPathSafety();
            var seenCells = new HashSet<int>();
            if (def == null || path == null)
                return result;

            foreach (var point in path)
            {
                int cell = Grid.XYToCell(point.x, point.y);
                if (!seenCells.Add(cell))
                {
                    result.HasRepeatedCells = true;
                    continue;
                }

                var placement = BuildPlacementDetails(def, point.x, point.y, worldId);
                var invalid = placement.Footprint
                    .Where(item => !item.Valid || !item.Visible || !item.InWorld)
                    .Select(item => item.ToDictionary())
                    .ToList();
                if (invalid.Count > 0)
                {
                    result.Conflicts.Add(new Dictionary<string, object>
                    {
                        ["kind"] = "invalid_utility_path_cell",
                        ["reasonCode"] = "invalid_utility_path_cell",
                        ["reason"] = "utility path cell must be visible, valid, and inside the selected world",
                        ["x"] = point.x,
                        ["y"] = point.y,
                        ["invalidCells"] = invalid
                    });
                    continue;
                }

                var conflicts = FindUtilityLayerConflicts(def, placement);
                if (conflicts.Count > 0)
                {
                    result.Conflicts.AddRange(conflicts);
                    continue;
                }

                if (ExistingMatchingUtilityAtPlacement(def, placement) != null)
                    result.ExistingSamePrefabCells++;
            }
            return result;
        }

        private static Dictionary<string, object> UtilityPathConflictResult(
            BuildingDef def,
            List<CellCoord> path,
            UtilityPathSafety safety,
            string phase)
        {
            return new Dictionary<string, object>
            {
                ["success"] = false,
                ["committed"] = false,
                ["reasonCode"] = "utility_path_conflict",
                ["reason"] = "utility path safety rejected an occupied or invalid cell before path commit",
                ["phase"] = phase,
                ["prefabId"] = def?.PrefabID,
                ["pathCells"] = path?.Count ?? 0,
                ["conflicts"] = safety?.Conflicts.Take(50).ToList() ?? new List<Dictionary<string, object>>()
            };
        }

        private static Dictionary<string, object> ExistingPlacementResult(
            BuildingDef def,
            PlacementDetails placement,
            Dictionary<string, object> existing,
            bool utility)
        {
            return new Dictionary<string, object>
            {
                ["planned"] = false,
                ["blueprintPlaced"] = false,
                ["alreadyPresent"] = !utility,
                ["alreadyConnected"] = utility,
                ["valid"] = true,
                ["prefabId"] = def.PrefabID,
                ["name"] = ToolUtil.CleanName(def.Name),
                ["x"] = placement.AnchorX,
                ["y"] = placement.AnchorY,
                ["anchor"] = AnchorDictionary(placement.AnchorX, placement.AnchorY, placement.WorldId),
                ["worldId"] = placement.WorldId,
                ["placement"] = placement.ToDictionary(),
                ["footprint"] = placement.Footprint.Select(cellInfo => cellInfo.ToDictionary()).ToList(),
                [utility ? "existingUtility" : "existing"] = existing,
                ["safety"] = "same_prefab_idempotent_reuse"
            };
        }

        private static bool HasUnsafeExecutionConflict(FootprintValidation validation)
        {
            if (validation == null || validation.Valid)
                return false;
            if (validation.InvalidCells.Count > 0)
                return true;

            return validation.Obstructions.Any(item =>
            {
                string kind = item.TryGetValue("kind", out object value) ? value?.ToString() : null;
                return !EqualsIgnoreCase(kind, "solid_cell") && !EqualsIgnoreCase(kind, "uprootable");
            });
        }

        private static Dictionary<string, object> PlannedFootprintOverlap(
            BuildingDef def,
            int x,
            int y,
            int worldId,
            HashSet<int> reservedCells, Orientation orientation)
        {
            if (def == null || reservedCells == null)
                return null;
            var overlaps = FootprintCells(def, x, y, worldId, orientation)
                .Where(item => item.Valid && reservedCells.Contains(item.Cell))
                .Select(item => item.ToDictionary())
                .ToList();
            if (overlaps.Count == 0)
                return null;
            return new Dictionary<string, object>
            {
                ["reasonCode"] = "planned_footprint_overlap",
                ["reason"] = "two requested anchors overlap within the same batch; refusing a plan that could instantiate duplicate buildings or utility connectors",
                ["overlaps"] = overlaps
            };
        }

        private static void ReservePlannedFootprint(
            BuildingDef def,
            int x,
            int y,
            int worldId,
            HashSet<int> reservedCells, Orientation orientation)
        {
            if (def == null || reservedCells == null)
                return;
            foreach (var item in FootprintCells(def, x, y, worldId, orientation).Where(item => item.Valid))
                reservedCells.Add(item.Cell);
        }
    }
}
