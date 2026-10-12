using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldAnalysisTools
    {
        private static List<Dictionary<string, object>> CellLayerObjects(int cell)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (ObjectLayer layer in Enum.GetValues(typeof(ObjectLayer)))
            {
                int index = (int)layer;
                if (index < 0)
                    continue;
                GameObject go = null;
                try
                {
                    go = Grid.Objects[cell, index];
                }
                catch
                {
                    continue;
                }

                if (go == null)
                    continue;
                result.Add(ObjectInfo(go, layer.ToString(), cell));
            }
            return result;
        }

        private static List<Dictionary<string, object>> CellBuildings(int cell, int worldId)
        {
            return NativeBuildingCellIndex.At(cell)
                .Where(go => go.GetComponent<BuildingComplete>() != null && MatchesWorld(go, worldId))
                .Select(go => ObjectInfo(go, "building", cell)).ToList();
        }

        private static List<Dictionary<string, object>> CellBlueprints(int cell, int worldId)
        {
            return NativeBuildingCellIndex.At(cell)
                .Where(go => go.GetComponent<Constructable>() != null && MatchesWorld(go, worldId))
                .Select(go => ObjectInfo(go, "blueprint", cell)).ToList();
        }

        private static List<Dictionary<string, object>> CellPickupables(int cell, int worldId)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var pickupable in Components.Pickupables.Items)
            {
                if (pickupable == null || pickupable.gameObject == null)
                    continue;
                int itemCell = pickupable.cachedCell;
                if (itemCell != cell || !MatchesWorld(pickupable.gameObject, worldId))
                    continue;
                var info = ObjectInfo(pickupable.gameObject, "pickupable", cell);
                var primary = pickupable.PrimaryElement ?? pickupable.GetComponent<PrimaryElement>();
                if (primary != null)
                {
                    info["element"] = primary.ElementID.ToString();
                    info["massKg"] = Math.Round(SafeFloat(primary.Mass), 3);
                    info["temperatureC"] = Math.Round(SafeFloat(primary.Temperature) - 273.15f, 2);
                }
                info["stored"] = pickupable.storage != null || pickupable.KPrefabID != null && pickupable.KPrefabID.HasTag(GameTags.Stored);
                result.Add(info);
            }
            return result;
        }

        private static List<Dictionary<string, object>> CellDupes(int cell, int worldId)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var dupe in Components.LiveMinionIdentities.Items)
            {
                if (dupe == null || dupe.gameObject == null || !MatchesWorld(dupe.gameObject, worldId))
                    continue;
                int dupeCell = Grid.PosToCell(dupe.gameObject);
                if (dupeCell != cell)
                    continue;
                result.Add(ObjectInfo(dupe.gameObject, "dupe", cell));
            }
            return result;
        }

        private static List<Dictionary<string, object>> CellPlants(int cell, int worldId)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var uprootable in Components.Uprootables.Items)
            {
                var go = uprootable?.gameObject;
                if (go == null || !MatchesWorld(go, worldId))
                    continue;
                int plantCell = Grid.PosToCell(go);
                if (plantCell != cell)
                    continue;
                var info = ObjectInfo(go, "plant_or_uprootable", cell);
                info["canUproot"] = uprootable.CanUproot();
                info["markedForUproot"] = uprootable.IsMarkedForUproot;
                result.Add(info);
            }
            return result;
        }

        private static Dictionary<string, object> CellUtilities(int cell)
        {
            var layers = new[]
            {
                ObjectLayer.Wire,
                ObjectLayer.WireTile,
                ObjectLayer.ReplacementWire,
                ObjectLayer.LiquidConduit,
                ObjectLayer.LiquidConduitTile,
                ObjectLayer.ReplacementLiquidConduit,
                ObjectLayer.GasConduit,
                ObjectLayer.GasConduitTile,
                ObjectLayer.ReplacementGasConduit,
                ObjectLayer.SolidConduit,
                ObjectLayer.SolidConduitTile,
                ObjectLayer.ReplacementSolidConduit,
                ObjectLayer.LogicWire,
                ObjectLayer.LogicWireTile,
                ObjectLayer.ReplacementLogicWire
            };
            var result = new Dictionary<string, object>();
            foreach (var layer in layers)
            {
                var go = Grid.Objects[cell, (int)layer];
                if (go != null)
                    result[layer.ToString()] = ObjectInfo(go, layer.ToString(), cell);
            }
            result["connectionSummary"] = CellUtilityConnectionSummary(cell);
            return result;
        }

        private static Dictionary<string, object> CellBuildability(int cell, int worldId)
        {
            bool naturalSolid = Grid.IsValidCell(cell)
                && Grid.IsVisible(cell)
                && ToolUtil.CellMatchesWorld(cell, worldId)
                && Grid.Solid[cell]
                && !Grid.Foundation[cell];
            bool hasUprootable = CellPlants(cell, worldId).Any(item => item.ContainsKey("canUproot") && item["canUproot"] is bool && (bool)item["canUproot"]);
            return new Dictionary<string, object>
            {
                ["naturalSolid"] = naturalSolid,
                ["canAutoDig"] = naturalSolid,
                ["plantPresent"] = CellPlants(cell, worldId).Count > 0,
                ["canAutoUproot"] = hasUprootable,
                ["notes"] = "Build planner can auto-mark natural solid dig and uprootable plants when autoDigObstructions/autoUprootObstructions are enabled."
            };
        }

        private static Dictionary<string, object> ObjectInfo(GameObject go, string kind, int selectedCell)
        {
            var kpid = go.GetComponent<KPrefabID>();
            int objectCell = Grid.PosToCell(go);
            var info = new Dictionary<string, object>
            {
                ["kind"] = kind,
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["prefabId"] = kpid?.PrefabTag.Name ?? go.name,
                ["name"] = ToolUtil.CleanName(go.GetProperName()),
                ["cell"] = objectCell,
                ["x"] = Grid.IsValidCell(objectCell) ? Grid.CellColumn(objectCell) : -1,
                ["y"] = Grid.IsValidCell(objectCell) ? Grid.CellRow(objectCell) : -1,
                ["selectedCell"] = selectedCell
            };
            if (Grid.IsWorldValidCell(objectCell))
                info["worldId"] = Grid.WorldIdx[objectCell];
            if (go.GetComponent<Building>() != null)
            {
                var shape = NativeBuildingCellIndex.Geometry(go);
                foreach (var item in shape.Summary()) info[item.Key] = item.Value;
                info["operational"] = go.GetComponent<Operational>()?.IsOperational;
                info["anchorX"] = shape.Available ? (object)shape.Bounds[0] : null;
                info["anchorY"] = shape.Available ? (object)shape.Bounds[1] : null;
                info["anchorCell"] = shape.Available ? (object)Grid.XYToCell(shape.Bounds[0], shape.Bounds[1]) : null;
                info["anchorRole"] = "occupied_bounds_bottom_left";
            }
            return info;
        }

        private static bool ObjectFootprintContains(GameObject go, BuildingDef def, int targetCell)
        {
            return go != null && Grid.IsValidCell(targetCell)
                && NativeBuildingCellIndex.Geometry(go).Contains(targetCell);
        }

        private static bool MatchesWorld(GameObject go, int worldId)
        {
            return worldId < 0 || ToolUtil.GameObjectMatchesWorld(go, worldId);
        }
    }
}
