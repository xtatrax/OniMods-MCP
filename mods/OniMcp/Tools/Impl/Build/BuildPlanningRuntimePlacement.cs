using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static GameObject TryPlaceWithBuildTool(BuildingDef def, int cell, Orientation orientation, IList<Tag> selectedElements, string facadeId, PlacementDetails placement, JObject args, out Dictionary<string, object> details)
        {
            details = new Dictionary<string, object>
            {
                ["attempted"] = true,
                ["path"] = "BuildTool.TryBuild",
                ["reason"] = "direct BuildingDef.TryPlace returned null while natural solid footprint cells were auto-dig queued"
            };

            if (!ToolUtil.GetBool(args, "allowNativeBuildTool", false))
            {
                details["attempted"] = false;
                details["available"] = false;
                details["error"] = "native BuildTool fallback disabled; it depends on PlanScreen UI state and can throw PlanScreen.GetBuildingPriority NullReferenceException";
                details["next"] = "Let queued auto-dig finish, then repeat the same build request. Set allowNativeBuildTool=true only for manual debugging.";
                return null;
            }

            if (BuildTool.Instance == null)
            {
                details["available"] = false;
                details["error"] = "BuildTool.Instance is not initialized";
                return null;
            }

            try
            {
                BuildTool.Instance.Activate(def, selectedElements, facadeId);
                BuildTool.Instance.SetToolOrientation(orientation);
                var tryBuild = typeof(BuildTool).GetMethod("TryBuild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (tryBuild == null)
                {
                    details["available"] = false;
                    details["error"] = "BuildTool.TryBuild method was not found";
                    return null;
                }

                tryBuild.Invoke(BuildTool.Instance, new object[] { cell });
                var placed = FindConstructableAtPlacement(def, placement);
                details["available"] = true;
                details["placed"] = placed != null;
                if (placed != null)
                {
                    details["id"] = placed.GetComponent<KPrefabID>()?.InstanceID ?? -1;
                    details["actualPlacement"] = ActualPlacementDetails(placed, def, placement.AnchorX, placement.AnchorY);
                }
                return placed;
            }
            catch (Exception ex)
            {
                details["available"] = false;
                details["error"] = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }

        private static GameObject FindConstructableAtPlacement(BuildingDef def, PlacementDetails placement)
        {
            if (def == null || placement == null)
                return null;

            foreach (var constructable in FindConstructables(placement.WorldId))
            {
                var go = constructable?.gameObject;
                if (go == null)
                    continue;

                var building = go.GetComponent<Building>();
                string prefabId = building?.Def?.PrefabID ?? go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name;
                if (!EqualsIgnoreCase(prefabId, def.PrefabID))
                    continue;

                var actual = ActualPlacementDetails(go, def, placement.AnchorX, placement.AnchorY);
                var check = ComparePlacement(placement, actual);
                if (GetBool(check, "valid"))
                    return go;
            }

            return null;
        }

        private static Dictionary<string, object> ExistingMatchingBuildAtPlacement(BuildingDef def, PlacementDetails placement)
        {
            if (def == null || placement == null)
                return null;

            foreach (var complete in Components.BuildingCompletes.Items)
            {
                var go = complete?.gameObject;
                if (go == null || !ToolUtil.GameObjectMatchesWorld(go, placement.WorldId))
                    continue;

                var building = go.GetComponent<Building>();
                string existingPrefabId = building?.Def?.PrefabID ?? go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name;
                if (!EqualsIgnoreCase(existingPrefabId, def.PrefabID))
                    continue;

                var actual = ActualPlacementDetails(go, def, placement.AnchorX, placement.AnchorY);
                var check = ComparePlacement(placement, actual);
                if (!GetBool(check, "valid"))
                    continue;
                int cell = placement.OriginCell;
                var rotatable = go.GetComponent<Rotatable>();
                Orientation orientation = building != null ? building.Orientation : rotatable == null ? Orientation.Neutral : rotatable.GetOrientation();
                if (!IsCompletedBuildFullyRegistered(def, placement, cell, orientation, go, out _))
                    continue;

                return new Dictionary<string, object>
                {
                    ["kind"] = "building",
                    ["prefabId"] = existingPrefabId,
                    ["id"] = go.GetComponent<KPrefabID>()?.InstanceID ?? -1,
                    ["material"] = ExistingConstructionMaterialTag(go),
                    ["actualPlacement"] = actual,
                    ["placementCheck"] = check
                };
            }

            var blueprint = FindConstructableAtPlacement(def, placement);
            if (blueprint == null)
                return null;

            var blueprintBuilding = blueprint.GetComponent<Building>();
            return new Dictionary<string, object>
            {
                ["kind"] = "blueprint",
                ["prefabId"] = blueprintBuilding?.Def?.PrefabID ?? blueprint.GetComponent<KPrefabID>()?.PrefabTag.Name ?? blueprint.name,
                ["id"] = blueprint.GetComponent<KPrefabID>()?.InstanceID ?? -1,
                ["material"] = ExistingConstructionMaterialTag(blueprint),
                ["actualPlacement"] = ActualPlacementDetails(blueprint, def, placement.AnchorX, placement.AnchorY),
                ["placementCheck"] = ComparePlacement(placement, ActualPlacementDetails(blueprint, def, placement.AnchorX, placement.AnchorY))
            };
        }

        private static Dictionary<string, object> ExistingMatchingUtilityAtPlacement(BuildingDef def, PlacementDetails placement)
        {
            if (def == null || placement == null || !IsLinearUtilityPrefab(def.PrefabID))
                return null;

            var layers = UtilityLayersForPrefab(def.PrefabID);
            if (layers.Count == 0)
                return null;

            var existing = new List<Dictionary<string, object>>();
            foreach (var cellInfo in placement.Footprint)
            {
                if (!cellInfo.Valid || !cellInfo.Visible || !cellInfo.InWorld)
                    return null;

                var found = ExistingUtilityAtCell(cellInfo, layers);
                if (found == null)
                    return null;
                if (!EqualsIgnoreCase(found["id"]?.ToString(), def.PrefabID))
                    return null;
                existing.Add(found);
            }

            return new Dictionary<string, object>
            {
                ["prefabId"] = def.PrefabID,
                ["mode"] = "reuse_existing_same_utility_layer",
                // A linear utility occupies one cell; an unexpected shape stays unknown.
                ["material"] = existing.Count == 1 ? existing[0]["material"] : null,
                ["cells"] = existing,
                ["note"] = "A matching wire/pipe already exists on this utility layer, so this cell is treated as connected instead of placing a duplicate blueprint."
            };
        }

        private static Dictionary<string, object> ExistingUtilityAtCell(FootprintCell cellInfo, List<ObjectLayer> layers)
        {
            foreach (var layer in layers)
            {
                var go = Grid.Objects[cellInfo.Cell, (int)layer];
                if (go == null)
                    continue;

                var building = go.GetComponent<Building>();
                string prefabId = building?.Def?.PrefabID ?? go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name;
                return new Dictionary<string, object>
                {
                    ["x"] = cellInfo.X,
                    ["y"] = cellInfo.Y,
                    ["cell"] = cellInfo.Cell,
                    ["layer"] = layer.ToString(),
                    ["id"] = prefabId,
                    ["name"] = ToolUtil.CleanName(go.GetProperName()),
                    ["material"] = ExistingConstructionMaterialTag(go)
                };
            }

            return null;
        }

        private static IEnumerable<Dictionary<string, object>> ExistingObjectFootprint(GameObject go, BuildingDef def, string kind, HashSet<int> footprintCells)
        {
            if (go == null) yield break;
            var shape = NativeBuildingCellIndex.Geometry(go);
            if (!shape.Available) yield break;
            var id = def?.PrefabID ?? go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name;
            foreach (var position in shape.Cells.Where(c => footprintCells.Contains(c.Cell.Value)))
                yield return new Dictionary<string, object>
                {
                    ["kind"] = kind, ["id"] = id, ["name"] = ToolUtil.CleanName(go.GetProperName()),
                    ["x"] = position.X, ["y"] = position.Y, ["cell"] = position.Cell.Value,
                    ["objectX"] = shape.Origin.X, ["objectY"] = shape.Origin.Y,
                    ["anchorX"] = shape.Bounds[0], ["anchorY"] = shape.Bounds[1],
                    ["orientation"] = shape.Orientation.ToString(), ["geometrySource"] = NativeBuildingFootprint.NativeSource,
                    ["reasonCode"] = "occupied_by_" + kind,
                    ["reason"] = "requested native occupied cells overlap an existing " + kind + " footprint"
                };
        }

        private static IEnumerable<Constructable> FindConstructables(int worldId)
        {
            Constructable[] constructables;
            try
            {
                constructables = UnityEngine.Object.FindObjectsByType<Constructable>(FindObjectsSortMode.None);
            }
            catch
            {
                yield break;
            }

            foreach (var constructable in constructables)
            {
                if (constructable == null || constructable.gameObject == null)
                    continue;
                if (!ToolUtil.GameObjectMatchesWorld(constructable.gameObject, worldId))
                    continue;
                yield return constructable;
            }
        }

        private static BuildDragPolicyResult BuildDragPolicy(BuildingDef def, JObject args)
        {
            int width = Math.Max(1, def.WidthInCells);
            int height = Math.Max(1, def.HeightInCells);
            bool singleCell = width == 1 && height == 1;
            bool allowFootprintDrag = ToolUtil.GetBool(args, "allowFootprintDrag", false);
            if (singleCell || allowFootprintDrag)
                return BuildDragPolicyResult.Allow(def.PrefabID, width, height, singleCell, allowFootprintDrag);
            return BuildDragPolicyResult.Reject(def.PrefabID, width, height);
        }

        private static void RegisterSupportBlueprint(string prefabId, int x, int y, HashSet<int> plannedSupportCells)
        {
            if (plannedSupportCells == null || !IsSupportPrefab(prefabId))
                return;

            int cell = Grid.XYToCell(x, y);
            if (Grid.IsValidCell(cell))
                plannedSupportCells.Add(cell);
        }

        private static bool IsSupportPrefab(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId))
                return false;

            return EqualsIgnoreCase(prefabId, "Tile")
                || EqualsIgnoreCase(prefabId, "MeshTile")
                || EqualsIgnoreCase(prefabId, "GasPermeableMembrane")
                || EqualsIgnoreCase(prefabId, "AirflowTile")
                || EqualsIgnoreCase(prefabId, "BunkerTile")
                || EqualsIgnoreCase(prefabId, "GlassTile")
                || EqualsIgnoreCase(prefabId, "InsulationTile")
                || EqualsIgnoreCase(prefabId, "PlasticTile")
                || EqualsIgnoreCase(prefabId, "MetalTile")
                || EqualsIgnoreCase(prefabId, "CarpetTile");
        }

        private static bool IsUtilityPrefab(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId))
                return false;

            string id = prefabId.Trim();
            return id.IndexOf("Wire", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("Conduit", StringComparison.OrdinalIgnoreCase) >= 0
                || id.IndexOf("TravelTube", StringComparison.OrdinalIgnoreCase) >= 0
                || EqualsIgnoreCase(id, "GasConduit")
                || EqualsIgnoreCase(id, "LiquidConduit")
                || EqualsIgnoreCase(id, "SolidConduit")
                || EqualsIgnoreCase(id, "SolidConduitBridge")
                || EqualsIgnoreCase(id, "WireBridge")
                || EqualsIgnoreCase(id, "LogicWire")
                || EqualsIgnoreCase(id, "LogicWireBridge");
        }

        private static bool IsLinearUtilityPrefab(string prefabId)
        {
            if (!IsUtilityPrefab(prefabId))
                return false;
            string id = prefabId.Trim();
            return id.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) < 0
                && id.IndexOf("TravelTube", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static List<ObjectLayer> UtilityLayersForPrefab(string prefabId)
        {
            var layers = new List<ObjectLayer>();
            if (string.IsNullOrWhiteSpace(prefabId))
                return layers;

            string id = prefabId.Trim();
            if (id.IndexOf("GasConduit", StringComparison.OrdinalIgnoreCase) >= 0)
                layers.AddRange(new[] { ObjectLayer.GasConduit, ObjectLayer.GasConduitTile, ObjectLayer.ReplacementGasConduit });
            else if (id.IndexOf("LiquidConduit", StringComparison.OrdinalIgnoreCase) >= 0)
                layers.AddRange(new[] { ObjectLayer.LiquidConduit, ObjectLayer.LiquidConduitTile, ObjectLayer.ReplacementLiquidConduit });
            else if (id.IndexOf("SolidConduit", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("Conveyor", StringComparison.OrdinalIgnoreCase) >= 0)
                layers.AddRange(new[] { ObjectLayer.SolidConduit, ObjectLayer.SolidConduitTile, ObjectLayer.ReplacementSolidConduit });
            else if (id.IndexOf("LogicWire", StringComparison.OrdinalIgnoreCase) >= 0)
                layers.AddRange(new[] { ObjectLayer.LogicWire, ObjectLayer.LogicWireTile, ObjectLayer.ReplacementLogicWire });
            else if (id.IndexOf("Wire", StringComparison.OrdinalIgnoreCase) >= 0)
                layers.AddRange(new[] { ObjectLayer.Wire, ObjectLayer.WireTile, ObjectLayer.ReplacementWire });

            return layers;
        }

        private static bool HasSupportBlueprint(int cell)
        {
            if (!Grid.IsValidCell(cell))
                return false;

            for (int layer = 0; layer < (int)ObjectLayer.NumLayers; layer++)
            {
                var go = Grid.Objects[cell, layer];
                if (go == null)
                    continue;

                var building = go.GetComponent<Building>();
                if (building != null && building.Def != null && building.Def.IsFoundation
                    && (go.GetComponent<BuildingUnderConstruction>() != null || go.GetComponent<Constructable>() != null))
                    return true;
            }

            return false;
        }
    }
}
