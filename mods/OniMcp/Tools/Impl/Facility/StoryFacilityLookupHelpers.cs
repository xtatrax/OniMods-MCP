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
    public static partial class StoryFacilityTools
    {
        private static CallToolResult ListTargets(JObject args, Func<GameObject, bool> predicate, Func<GameObject, Dictionary<string, object>> selector, string payloadKey)
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

        private static GameObject FindTarget(JObject args, Func<GameObject, bool> predicate)
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

        private static GameObject FindObjectTarget(JObject args, Func<GameObject, bool> predicate)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            foreach (var go in AllCandidateObjects())
            {
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

        private static IEnumerable<GameObject> AllCandidateObjects()
        {
            var seen = new HashSet<int>();
            foreach (var kpid in UnityEngine.Object.FindObjectsByType<KPrefabID>(FindObjectsSortMode.None))
            {
                if (kpid == null || kpid.gameObject == null)
                    continue;
                int id = kpid.gameObject.GetInstanceID();
                if (seen.Add(id))
                    yield return kpid.gameObject;
            }
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

        private static Dictionary<string, McpToolParameter> StoryFacilityControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["kind"] = new McpToolParameter { Type = "string", Description = "printerceptor、poi_tech_unlock、remote_work_terminal、genetic_analysis_station のいずれか", Required = true, EnumValues = new List<string> { "printerceptor", "poi_tech_unlock", "remote_work_terminal", "genetic_analysis_station" } },
                ["action"] = new McpToolParameter { Type = "string", Description = "既定値は list。printerceptor: intercept/open_print_interface、poi_tech_unlock: start/cancel/toggle、remote_work_terminal: set_dock、genetic_analysis_station: set_seed", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で名前、prefabId、状態により絞り込む", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["pendingOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=poi_tech_unlock action=list で、解放作業がすでにあるポータルだけを返すか", Required = false },
                ["lockedOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=poi_tech_unlock action=list で、まだ解放していないポータルだけを返すか", Required = false },
                ["includeDocks"] = new McpToolParameter { Type = "boolean", Description = "kind=remote_work_terminal action=list で、同じワールドの選択可能な dock を返すか。既定値は true", Required = false },
                ["dockId"] = new McpToolParameter { Type = "integer", Description = "kind=remote_work_terminal action=set_dock の対象の dock の InstanceID", Required = false },
                ["dockName"] = new McpToolParameter { Type = "string", Description = "kind=remote_work_terminal action=set_dock の対象の dock 名", Required = false },
                ["clear"] = new McpToolParameter { Type = "boolean", Description = "kind=remote_work_terminal action=set_dock で選択を解除する", Required = false },
                ["seedId"] = new McpToolParameter { Type = "string", Description = "kind=genetic_analysis_station action=set_seed の種子の tag/prefab id", Required = false },
                ["speciesId"] = new McpToolParameter { Type = "string", Description = "kind=genetic_analysis_station action=set_seed の植物の species の prefab id", Required = false },
                ["allowed"] = new McpToolParameter { Type = "boolean", Description = "kind=genetic_analysis_station action=set_seed では true=分析用の搬入を許可、false=禁止", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "確認が必要な書き込み操作では true が必須", Required = false }
            });
        }

        private static Dictionary<string, McpToolParameter> RemoteWorkTerminalControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set_dock", Required = true, EnumValues = new List<string> { "list", "set_dock" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で端末、dock、prefabId、ワールドにより絞り込む", Required = false },
                ["includeDocks"] = new McpToolParameter { Type = "boolean", Description = "action=list で、同じワールドの選択可能な dock を返すか。既定値は true", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["dockId"] = new McpToolParameter { Type = "integer", Description = "action=set_dock の対象の RemoteWorkerDock の InstanceID", Required = false },
                ["dockName"] = new McpToolParameter { Type = "string", Description = "action=set_dock の対象の dock 名", Required = false },
                ["clear"] = new McpToolParameter { Type = "boolean", Description = "action=set_dock で選択を解除するか。既定値は false", Required = false }
            });
        }

        private static Dictionary<string, McpToolParameter> GeneticAnalysisStationControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set_seed", Required = true, EnumValues = new List<string> { "list", "set_seed" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備、種子、植物、状態により絞り込む", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["seedId"] = new McpToolParameter { Type = "string", Description = "action=set_seed の種子の tag/prefab id。例: BasicFabricPlantSeed。speciesId とのどちらかを指定する", Required = false },
                ["speciesId"] = new McpToolParameter { Type = "string", Description = "action=set_seed の植物の species の prefab id。対応する seedId に解決される", Required = false },
                ["allowed"] = new McpToolParameter { Type = "boolean", Description = "action=set_seed では true=分析用の搬入を許可、false=禁止", Required = false }
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

        private static CallToolResult JsonResult(Dictionary<string, object> payload)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
        }
    }
}
