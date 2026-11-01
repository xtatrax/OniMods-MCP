using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static Vector3 BuildPlacementPosition(int cell, BuildingDef def)
        {
            return Grid.CellToPosCBC(cell, def.SceneLayer);
        }

        private static Dictionary<string, object> BuildDefPlacementToDictionary(BuildingDef def)
        {
            var shape = NativeBuildingFootprint.Definition(def, Orientation.Neutral);
            int width = shape.Available ? shape.Size[0] : 0;
            int height = shape.Available ? shape.Size[1] : 0;
            return new Dictionary<string, object>
            {
                ["anchor"] = "lowerLeftCell",
                ["anchorDescription"] = "each anchor is the bottom-left of the occupied bounds; the native reference cell is returned separately",
                ["width"] = width,
                ["height"] = height,
                ["footprintCells"] = shape.Available ? (object)shape.Cells.Count : null,
                ["geometryAvailable"] = shape.Available,
                ["nativeOriginOffsetFromLowerLeft"] = shape.Available ? new[] { -shape.Bounds[0], -shape.Bounds[1] } : null,
                ["singleCellDragSafe"] = width == 1 && height == 1,
                ["operationGuide"] = BuildingOperationGuidance.MapPlacement(def.PrefabID),
                ["dragGuidance"] = BuildingOperationGuidance.Placement
            };
        }

        private static PlacementDetails BuildPlacementDetails(BuildingDef def, int x, int y, int worldId,
            Orientation orientation = Orientation.Neutral)
        {
            var shape = NativeBuildingFootprint.FromLowerLeft(def, x, y, orientation);
            int cell = shape.Origin?.Cell ?? Grid.InvalidCell;
            return new PlacementDetails
            {
                PrefabId = def.PrefabID,
                AnchorX = x,
                AnchorY = y,
                WorldId = worldId,
                Orientation = orientation,
                Width = shape.Size?[0] ?? 0,
                Height = shape.Size?[1] ?? 0,
                OriginX = shape.Origin?.X ?? x,
                OriginY = shape.Origin?.Y ?? y,
                OriginCell = cell,
                GeometryAvailable = shape.Available,
                GeometryError = shape.Error,
                FillsBounds = shape.FillsBounds,
                PlacementPoint = Grid.IsValidCell(cell) ? BuildPlacementPosition(cell, def) : Vector3.zero,
                Footprint = FootprintCells(def, x, y, worldId, orientation).ToList()
            };
        }

        private static IEnumerable<FootprintCell> FootprintCells(BuildingDef def, int x, int y, int worldId,
            Orientation orientation = Orientation.Neutral)
        {
            var shape = NativeBuildingFootprint.FromLowerLeft(def, x, y, orientation);
            foreach (var position in shape.Cells ?? new List<BuildingGeometryCell>())
            {
                int cell = position.Cell ?? Grid.InvalidCell;
                yield return new FootprintCell
                {
                    X = position.X, Y = position.Y, Cell = cell, WorldId = worldId,
                    Valid = position.Valid,
                    Visible = position.Valid && Grid.IsVisible(cell),
                    InWorld = position.Valid && ToolUtil.CellMatchesWorld(cell, worldId)
                };
            }
        }

        private static FootprintValidation ValidateFootprint(PlacementDetails placement, GameObject ignored = null, PlacementPreviewReadContext previewReads = null)
        {
            if (!placement.GeometryAvailable && placement.GeometryError != "occupied_cell_invalid")
                return FootprintValidation.Invalid("Native footprint unavailable: " + placement.GeometryError,
                    new List<Dictionary<string, object>> { new Dictionary<string, object> { ["reasonCode"] = "geometry_unavailable" } },
                    new List<Dictionary<string, object>>());
            var invalid = placement.Footprint
                .Where(cell => !cell.Valid || !cell.Visible || !cell.InWorld)
                .Select(cell => cell.ToDictionary())
                .ToList();

            var obstructions = FindFootprintObstructions(placement, ignored, previewReads);
            AddBackwallFoundationFailure(placement, obstructions);

            if (invalid.Count == 0 && obstructions.Count == 0)
                return FootprintValidation.Success();

            var backwallFailure = obstructions.FirstOrDefault(obstruction =>
                obstruction.ContainsKey("reasonCode")
                && string.Equals(obstruction["reasonCode"]?.ToString(), "backwall_required", StringComparison.Ordinal));
            string error = invalid.Count > 0
                ? "Invalid footprint: every occupied cell must be visible, valid, and inside the selected world"
                : backwallFailure != null
                    ? backwallFailure["reason"]?.ToString()
                    : "Obstructed footprint: occupied terrain, building, or blueprint overlaps the requested cells";
            return FootprintValidation.Invalid(error, invalid, obstructions);
        }

        private static void AddBackwallFoundationFailure(PlacementDetails placement, List<Dictionary<string, object>> obstructions)
        {
            var def = ResolveBuildingDefForPlacement(placement);
            if (def == null)
                return;

            string rule = def.BuildLocationRule.ToString();
            if (!string.Equals(rule, "OnBackWall", StringComparison.OrdinalIgnoreCase))
                return;

            int cell = placement.OriginCell;
            bool nativeFoundationValid = Grid.IsValidCell(cell)
                && BuildingDef.CheckFoundation(
                    cell,
                    placement.Orientation,
                    def.BuildLocationRule,
                    def.WidthInCells,
                    def.HeightInCells);
            var decision = BuildPlanningBackwallSupportPolicy.Evaluate(rule, nativeFoundationValid);
            if (decision.Valid)
                return;

            obstructions.Insert(0, new Dictionary<string, object>
            {
                ["kind"] = "missing_backwall",
                ["x"] = placement.AnchorX,
                ["y"] = placement.AnchorY,
                ["cell"] = cell,
                ["buildLocationRule"] = rule,
                ["reasonCode"] = decision.ReasonCode,
                ["reason"] = decision.Error,
                ["nativeFoundationCheck"] = true
            });
        }

        private static Dictionary<string, object> ActualPlacementDetails(GameObject go, BuildingDef def, int expectedX, int expectedY)
        {
            var shape = NativeBuildingFootprint.Instance(go, def);
            int cell = Grid.PosToCell(go);
            var result = shape.Summary();
            result["objectCell"] = cell;
            result["objectX"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1;
            result["objectY"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1;
            result["derivedAnchorX"] = shape.Available ? shape.Bounds[0] : -1;
            result["derivedAnchorY"] = shape.Available ? shape.Bounds[1] : -1;
            result["worldId"] = Grid.IsWorldValidCell(cell) ? (int)Grid.WorldIdx[cell] : -1;
            result["occupiedCells"] = shape.Available ? shape.Cells.Select(c => new[] { c.X, c.Y }).ToList() : null;
            result["note"] = "derivedAnchor is the actual occupied bounds bottom-left; origin is the native building reference cell";
            return result;
        }

        private static Dictionary<string, object> ComparePlacement(PlacementDetails expected, Dictionary<string, object> actual)
        {
            int actualX = Convert.ToInt32(actual["derivedAnchorX"]);
            int actualY = Convert.ToInt32(actual["derivedAnchorY"]);
            int actualWorld = Convert.ToInt32(actual["worldId"]);
            bool anchorMatches = actualX == expected.AnchorX && actualY == expected.AnchorY;
            bool worldMatches = actualWorld >= 0 && (expected.WorldId < 0 || actualWorld == expected.WorldId);
            bool orientationMatches = actual["orientation"]?.ToString() == expected.Orientation.ToString();
            var cells = actual["occupiedCells"] as List<int[]>;
            bool cellsMatch = expected.GeometryAvailable && cells != null
                && new HashSet<string>(expected.Footprint.Select(c => c.X + "," + c.Y))
                    .SetEquals(cells.Select(c => c[0] + "," + c[1]));
            bool valid = anchorMatches && worldMatches && orientationMatches && cellsMatch;
            return new Dictionary<string, object>
            {
                ["valid"] = valid, ["anchorMatches"] = anchorMatches, ["worldMatches"] = worldMatches,
                ["orientationMatches"] = orientationMatches, ["occupiedCellsMatch"] = cellsMatch,
                ["expectedAnchor"] = new { x = expected.AnchorX, y = expected.AnchorY },
                ["actualDerivedAnchor"] = new { x = actualX, y = actualY },
                ["expectedOrigin"] = new[] { expected.OriginX, expected.OriginY },
                ["expectedWorldId"] = expected.WorldId, ["actualWorldId"] = actualWorld,
                ["next"] = valid ? "Verify the queued/completed native footprint in the map."
                    : "Inspect origin, orientation and occupied cells before retrying."
            };
        }

        private static Dictionary<string, object> BuildPlacementFailureDetails(PlacementDetails placement, MaterialSelection materialResult)
        {
            return new Dictionary<string, object>
            {
                ["placement"] = placement.ToDictionary(),
                ["obstructions"] = FindFootprintObstructions(placement).Take(50).ToList(),
                ["materialSelection"] = materialResult.ToDictionary(),
                ["materials"] = materialResult.ToDictionary(),
                ["reasonHint"] = "TryPlace returned null after preflight; inspect obstructions/support/materialSelection for likely cause."
            };
        }
    }
}
