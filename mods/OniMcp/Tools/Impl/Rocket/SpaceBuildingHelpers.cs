using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class SpaceBuildingTools
    {
        private static Dictionary<string, object> CometDetectorInfo(GameObject go, bool includeOptions)
        {
            var result = TargetInfo(go);
            result["dlcClusterMode"] = DlcManager.IsExpansion1Active();
            if (DlcManager.IsExpansion1Active())
            {
                var detector = go.GetSMI<ClusterCometDetector.Instance>();
                var craft = detector?.GetClustercraftTarget();
                result["currentTarget"] = detector == null ? null : new Dictionary<string, object>
                {
                    ["type"] = DetectorStateToTargetType(detector.GetDetectorState()),
                    ["rocket"] = craft == null ? null : ClustercraftInfo(craft)
                };
                result["options"] = includeOptions ? ClusterDetectorOptions() : new List<Dictionary<string, object>>();
            }
            else
            {
                var detector = go.GetSMI<CometDetector.Instance>();
                var target = detector?.GetTargetCraft();
                result["currentTarget"] = new Dictionary<string, object>
                {
                    ["type"] = target == null ? "meteor_shower" : "rocket",
                    ["rocket"] = target == null ? null : BaseSpacecraftInfo(SpacecraftManager.instance.GetSpacecraftFromLaunchConditionManager(target))
                };
                result["options"] = includeOptions ? BaseDetectorOptions() : new List<Dictionary<string, object>>();
            }
            return result;
        }

        private static string DetectorStateToTargetType(ClusterCometDetector.Instance.ClusterCometDetectorState state)
        {
            if (state == ClusterCometDetector.Instance.ClusterCometDetectorState.MeteorShower)
                return "meteor_shower";
            if (state == ClusterCometDetector.Instance.ClusterCometDetectorState.BallisticObject)
                return "ballistic_object";
            return "rocket";
        }

        private static List<Dictionary<string, object>> ClusterDetectorOptions()
        {
            var options = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["targetType"] = "meteor_shower", ["name"] = "Comets" },
                new Dictionary<string, object> { ["targetType"] = "ballistic_object", ["name"] = "Dupe-made Ballistics" }
            };
            options.AddRange(Components.Clustercrafts.Items.Where(craft => craft != null).Select(craft => new Dictionary<string, object>
            {
                ["targetType"] = "rocket",
                ["rocket"] = ClustercraftInfo(craft)
            }));
            return options;
        }

        private static List<Dictionary<string, object>> BaseDetectorOptions()
        {
            var options = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["targetType"] = "meteor_shower", ["name"] = "Comets" }
            };
            options.AddRange(SpacecraftManager.instance.GetSpacecraft().Select(craft => new Dictionary<string, object>
            {
                ["targetType"] = "rocket",
                ["rocket"] = BaseSpacecraftInfo(craft)
            }));
            return options;
        }

        private static Dictionary<string, object> ClusterLocationSensorInfo(LogicClusterLocationSensor sensor)
        {
            var result = TargetInfo(sensor.gameObject);
            result["activeInSpace"] = sensor.ActiveInSpace;
            result["locations"] = ClusterLocations()
                .Select(location => new Dictionary<string, object>
                {
                    ["q"] = location.location.q,
                    ["r"] = location.location.r,
                    ["name"] = location.name,
                    ["visible"] = location.visible,
                    ["selected"] = sensor.CheckLocationSelected(location.location)
                })
                .ToList();
            return result;
        }

        private static IEnumerable<(AxialI location, string name, bool visible)> ClusterLocations()
        {
            if (ClusterManager.Instance == null)
                yield break;
            foreach (var world in ClusterManager.Instance.WorldContainers)
            {
                if (world == null || world.IsModuleInterior)
                    continue;
                var location = world.GetMyWorldLocation();
                bool visible = ClusterGrid.Instance == null || ClusterGrid.Instance.GetCellRevealLevel(location) == ClusterRevealLevel.Visible;
                yield return (location, ToolUtil.CleanName(world.GetProperName()), visible);
            }
        }

        private static Dictionary<string, object> RailGunInfo(RailGun railGun)
        {
            var result = TargetInfo(railGun.gameObject);
            result["launchMassKg"] = Math.Round(ToolUtil.SafeFloat(railGun.launchMass), 3);
            result["minLaunchMassKg"] = Math.Round(ToolUtil.SafeFloat(railGun.MinLaunchMass), 3);
            result["maxLaunchMassKg"] = Math.Round(ToolUtil.SafeFloat(railGun.MaxLaunchMass), 3);
            result["storedMassKg"] = Math.Round(ToolUtil.SafeFloat(railGun.resourceStorage?.MassStored() ?? 0f), 3);
            result["currentEnergy"] = Math.Round(ToolUtil.SafeFloat(railGun.CurrentEnergy), 3);
            result["requiredEnergy"] = Math.Round(ToolUtil.SafeFloat(railGun.EnergyCost), 3);
            result["hasLogicWire"] = railGun.HasLogicWire;
            result["logicActive"] = railGun.IsLogicActive;
            return result;
        }

        private static bool IsCometDetector(GameObject go)
        {
            if (go == null)
                return false;
            return DlcManager.IsExpansion1Active()
                ? go.GetSMI<ClusterCometDetector.Instance>() != null
                : go.GetSMI<CometDetector.Instance>() != null;
        }

        private static Clustercraft FindClustercraft(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "rocketId");
            string name = args["rocketName"]?.ToString();
            foreach (var craft in Components.Clustercrafts.Items)
            {
                if (craft == null)
                    continue;
                var kpid = craft.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return craft;
                if (!string.IsNullOrWhiteSpace(name) && string.Equals(craft.Name, name, StringComparison.OrdinalIgnoreCase))
                    return craft;
            }
            return null;
        }

        private static LaunchConditionManager FindBaseRocketTarget(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "rocketId");
            string name = args["rocketName"]?.ToString();
            foreach (var craft in SpacecraftManager.instance.GetSpacecraft())
            {
                if (craft == null)
                    continue;
                if (id.HasValue && craft.GetHashCode() == id.Value)
                    return craft.launchConditions;
                if (!string.IsNullOrWhiteSpace(name) && string.Equals(craft.GetRocketName(), name, StringComparison.OrdinalIgnoreCase))
                    return craft.launchConditions;
            }
            return null;
        }

        private static Dictionary<string, object> ClustercraftInfo(Clustercraft craft)
        {
            var kpid = craft.GetComponent<KPrefabID>();
            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? craft.GetInstanceID(),
                ["name"] = craft.Name,
                ["worldId"] = craft.GetMyWorldId()
            };
        }

        private static Dictionary<string, object> BaseSpacecraftInfo(Spacecraft craft)
        {
            if (craft == null)
                return null;
            return new Dictionary<string, object>
            {
                ["id"] = craft.GetHashCode(),
                ["name"] = craft.GetRocketName(),
                ["state"] = craft.state.ToString()
            };
        }

        private static string NormalizeTargetType(string value)
        {
            string normalized = (value ?? "").Trim().ToLowerInvariant();
            if (normalized == "comets" || normalized == "meteor" || normalized == "meteors")
                return "meteor_shower";
            if (normalized == "dupe_made" || normalized == "ballistic")
                return "ballistic_object";
            if (normalized == "meteor_shower" || normalized == "ballistic_object" || normalized == "rocket")
                return normalized;
            return null;
        }

        private static CallToolResult ListBuildingComponent(JObject args, Func<GameObject, bool> predicate, Func<GameObject, Dictionary<string, object>> selector, string payloadKey)
        {
            if (Game.Instance == null)
                return CallToolResult.Error("Game not initialized");
            bool hasRect = HasRectInput(args);
            var rect = hasRect ? ToolUtil.GetRect(args) : null;
            int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
            string query = args["query"]?.ToString();
            int limit = ToolUtil.ClampLimit(args, 100, 500);
            var items = Components.BuildingCompletes.Items
                .Select(building => building?.gameObject)
                .Where(go => MatchesTarget(go, rect, worldId))
                .Where(predicate)
                .Select(selector)
                .Where(info => MatchesQuery(info, query))
                .OrderBy(info => info["name"].ToString())
                .Take(limit)
                .ToList();
            return JsonResult(new Dictionary<string, object>
            {
                ["returned"] = items.Count,
                ["worldId"] = worldId >= 0 ? (object)worldId : null,
                [payloadKey] = items
            });
        }

        private static GameObject FindBuildingTarget(JObject args, Func<GameObject, bool> predicate)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            foreach (var building in Components.BuildingCompletes.Items)
            {
                var go = building?.gameObject;
                if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId) || !predicate(go))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return go;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return go;
            }
            return null;
        }

        private static bool MatchesTarget(GameObject go, Dictionary<string, int> rect, int worldId)
        {
            if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                return false;
            int cell = Grid.PosToCell(go);
            return rect == null || CellInRect(cell, rect, worldId);
        }

        private static bool MatchesQuery(Dictionary<string, object> info, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            return JsonConvert.SerializeObject(info).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, object> TargetInfo(GameObject go)
        {
            int cell = Grid.PosToCell(go);
            var building = go.GetComponent<Building>();
            var kpid = go.GetComponent<KPrefabID>();
            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["prefabId"] = building?.Def?.PrefabID ?? kpid?.PrefabTag.Name ?? go.name,
                ["name"] = ToolUtil.CleanName(go.GetProperName()),
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["worldId"] = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : -1
            };
        }

        private static CallToolResult JsonResult(Dictionary<string, object> payload)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象の設備の InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象の設備のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象の設備のセルの Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標で検索する場合の既定値は現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の領域ハンドル。指定すると x1/y1/x2/y2 を省略できる", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の左下・始点の X。areaId を使う場合は省略できる", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の左下・始点の Y。areaId を使う場合は省略できる", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の右上・終点の X。areaId を使う場合は省略できる", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の右上・終点の Y。areaId を使う場合は省略できる", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定値は areaId に関連付けられたワールド、または現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> AreaLookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = RectParams(new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象の設備の InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象の設備のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象の設備のセルの Y", Required = false }
            });
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> CometDetectorControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set_target", Required = true, EnumValues = new List<string> { "list", "set_target" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、対象の種類、ロケット名により絞り込む", Required = false },
                ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "action=list で選択可能な対象を返すか。既定値は true", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["targetType"] = new McpToolParameter { Type = "string", Description = "action=set_target では meteor_shower、ballistic_object、rocket のいずれか", Required = false, EnumValues = new List<string> { "meteor_shower", "ballistic_object", "rocket" } },
                ["rocketId"] = new McpToolParameter { Type = "integer", Description = "action=set_target かつ targetType=rocket の場合の、対象の Clustercraft/Spacecraft の id", Required = false },
                ["rocketName"] = new McpToolParameter { Type = "string", Description = "action=set_target かつ targetType=rocket の場合に、ロケット名で対象を指定する", Required = false }
            });
        }

        private static Dictionary<string, McpToolParameter> ClusterLocationSensorControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set", Required = true, EnumValues = new List<string> { "list", "set" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、小惑星名、座標により絞り込む", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["target"] = new McpToolParameter { Type = "string", Description = "action=set では space または location", Required = false, EnumValues = new List<string> { "space", "location" } },
                ["q"] = new McpToolParameter { Type = "integer", Description = "action=set かつ target=location の場合の星図の q 座標", Required = false },
                ["r"] = new McpToolParameter { Type = "integer", Description = "action=set かつ target=location の場合の星図の r 座標", Required = false },
                ["enabled"] = new McpToolParameter { Type = "boolean", Description = "action=set でこの対象を有効にするか", Required = false }
            });
        }

        private static Dictionary<string, McpToolParameter> RailGunControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set_launch_mass", Required = true, EnumValues = new List<string> { "list", "set_launch_mass" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名や prefabId により絞り込む", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["massKg"] = new McpToolParameter { Type = "number", Description = "action=set_launch_mass で設定する発射質量（kg）。星間ランチャーの min/max の範囲に収める", Required = false }
            });
        }

        private static bool HasRectInput(JObject args)
        {
            return !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                   || ToolUtil.GetInt(args, "x1").HasValue
                   || ToolUtil.GetInt(args, "y1").HasValue
                   || ToolUtil.GetInt(args, "x2").HasValue
                   || ToolUtil.GetInt(args, "y2").HasValue;
        }

        private static bool CellInRect(int cell, Dictionary<string, int> rect, int worldId)
        {
            return Grid.IsValidCell(cell)
                   && ToolUtil.CellMatchesWorld(cell, worldId)
                   && Grid.CellColumn(cell) >= rect["x1"]
                   && Grid.CellColumn(cell) <= rect["x2"]
                   && Grid.CellRow(cell) >= rect["y1"]
                   && Grid.CellRow(cell) <= rect["y2"];
        }
    }
}
