using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class MaintenanceActionTools
    {
        private static IEnumerable<GameObject> AllCandidateObjects()
        {
            var seen = new HashSet<int>();
            foreach (var kpid in UnityEngine.Object.FindObjectsByType<KPrefabID>(FindObjectsSortMode.None))
            {
                if (kpid == null || kpid.gameObject == null)
                    continue;
                if (seen.Add(kpid.gameObject.GetInstanceID()))
                    yield return kpid.gameObject;
            }
        }

        private static GameObject FindTarget(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            string query = args["query"]?.ToString();
            foreach (var go in AllCandidateObjects())
            {
                if (!ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return go;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return go;
                if (!string.IsNullOrWhiteSpace(query) && MatchesQuery(TargetInfo(go), query))
                    return go;
            }
            return null;
        }

        private static MinionIdentity FindDupeTarget(JObject args)
        {
            var dupe = ToolUtil.FindDupe(args);
            if (dupe != null)
                return dupe;
            int? dupeId = ToolUtil.GetInt(args, "dupeId");
            string dupeName = args["dupeName"]?.ToString();
            if (dupeId.HasValue || !string.IsNullOrWhiteSpace(dupeName))
            {
                foreach (var candidate in Components.LiveMinionIdentities.Items)
                {
                    var kpid = candidate?.GetComponent<KPrefabID>();
                    if (dupeId.HasValue && kpid != null && kpid.InstanceID == dupeId.Value)
                        return candidate;
                    if (!string.IsNullOrWhiteSpace(dupeName) && string.Equals(candidate?.GetProperName(), dupeName.Trim(), StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
            }
            return null;
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

        private static bool MatchesTarget(GameObject go, Dictionary<string, int> rect, int worldId)
        {
            if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                return false;
            if (rect == null)
                return true;
            int cell = Grid.PosToCell(go);
            return Grid.IsValidCell(cell)
                   && ToolUtil.CellMatchesWorld(cell, worldId)
                   && Grid.CellColumn(cell) >= rect["x1"]
                   && Grid.CellColumn(cell) <= rect["x2"]
                   && Grid.CellRow(cell) >= rect["y1"]
                   && Grid.CellRow(cell) <= rect["y2"];
        }

        private static bool MatchesQuery(Dictionary<string, object> info, string query)
        {
            return MatchesMaintenanceQueryWithToiletAlias(info, query);
        }

        private static bool HasRectInput(JObject args)
        {
            return !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                   || (args["x1"] != null && args["y1"] != null && args["x2"] != null && args["y2"] != null);
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> parameters)
        {
            parameters["areaId"] = new McpToolParameter { Type = "string", Description = "領域ハンドル。x1/y1/x2/y2 とのどちらかを指定する", Required = false };
            parameters["x1"] = new McpToolParameter { Type = "integer", Description = "矩形の左下・左上の X", Required = false };
            parameters["y1"] = new McpToolParameter { Type = "integer", Description = "矩形の左下・左上の Y", Required = false };
            parameters["x2"] = new McpToolParameter { Type = "integer", Description = "矩形の右上・右下の X", Required = false };
            parameters["y2"] = new McpToolParameter { Type = "integer", Description = "矩形の右上・右下の Y", Required = false };
            parameters["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。省略するとワールドを限定しない", Required = false };
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> parameters)
        {
            parameters["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトまたは複製人間の KPrefabID.InstanceID。指定を推奨する", Required = false };
            parameters["dupeId"] = new McpToolParameter { Type = "integer", Description = "unequip_dupe_equipment の複製人間の InstanceID。id が未指定の場合に使う", Required = false };
            parameters["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。unequip_dupe_equipment で使用できる", Required = false };
            parameters["dupeName"] = new McpToolParameter { Type = "string", Description = "複製人間の名前を指定する別名の引数。unequip_dupe_equipment で使用できる", Required = false };
            parameters["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X。id が未指定の場合に使う", Required = false };
            parameters["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y。id が未指定の場合に使う", Required = false };
            parameters["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標から探す場合は指定を推奨する", Required = false };
            return parameters;
        }

        private static JObject MergeBatchDefaults(JObject item, JObject defaults)
        {
            var result = new JObject();
            CopyBatchAliases(defaults, result, overwrite: false);
            CopyNonBatchAliases(defaults, result, overwrite: false);
            CopyBatchAliases(item, result, overwrite: true);
            CopyNonBatchAliases(item, result, overwrite: true);
            return result;
        }

        private static void CopyBatchAliases(JObject source, JObject target, bool overwrite)
        {
            if (source == null)
                return;

            CopyAlias(source, target, "actionKey", "a", overwrite);
            CopyAlias(source, target, "worldId", "w", overwrite);
            CopyAlias(source, target, "enabled", "e", overwrite);
            CopyAlias(source, target, "slotId", "slot", overwrite);
        }

        private static void CopyAlias(JObject source, JObject target, string longKey, string shortKey, bool overwrite)
        {
            var token = source[longKey] ?? source[shortKey];
            if (token != null && (overwrite || target[longKey] == null))
                target[longKey] = token.DeepClone();
        }

        private static void CopyNonBatchAliases(JObject source, JObject target, bool overwrite)
        {
            if (source == null)
                return;

            foreach (var property in source.Properties())
            {
                if (IsBatchAlias(property.Name))
                    continue;
                if (overwrite || target[property.Name] == null)
                    target[property.Name] = property.Value.DeepClone();
            }
        }

        private static bool IsBatchAlias(string name)
        {
            return string.Equals(name, "actionKey", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "a", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "worldId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "w", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "enabled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "e", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "slotId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "slot", StringComparison.OrdinalIgnoreCase);
        }

        private static CallToolResult JsonResult(object payload)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
        }
    }
}
