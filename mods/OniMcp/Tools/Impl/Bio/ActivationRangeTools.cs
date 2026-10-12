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
    public static class ActivationRangeTools
    {
        public static McpTool ListActivationRanges()
        {
            return new McpTool
            {
                Name = "activation_ranges_list",
                Group = "controls",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "active_range_controls_list", "activation_range_side_screens_list" },
                Tags = new List<string> { "controls", "side-screen", "activation-range", "smart-battery", "reservoir", "massage" },
                Description = "互換用の入口。building_control domain=side_surface surface=activation action=list を優先して使う。スマートバッテリー、スマート貯液庫、マッサージ台など、ActiveRangeSideScreen / IActivationRangeTarget の2つのしきい値の操作項目を一覧表示する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、見出し、タグで絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var targets = AllCandidateObjects()
                        .Where(go => MatchesTarget(go, rect, worldId))
                        .Select(TargetActivationRangeInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = targets.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["targets"] = targets
                    });
                }
            };
        }

        public static McpTool SetActivationRange()
        {
            return new McpTool
            {
                Name = "activation_range_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "active_range_set", "activation_range_side_screen_set" },
                Tags = new List<string> { "controls", "side-screen", "activation-range", "smart-battery", "reservoir", "massage" },
                Description = "互換用の入口。building_control domain=side_surface surface=activation action=set を優先して使う。ActiveRangeSideScreen / IActivationRangeTarget の2つのしきい値を設定する。自動での開始・停止や使用条件に影響するため、confirm=true が必要",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["activateValue"] = new McpToolParameter { Type = "number", Description = "作動しきい値。未指定なら変更しない", Required = false },
                    ["deactivateValue"] = new McpToolParameter { Type = "number", Description = "停止しきい値。未指定なら変更しない", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "2つのしきい値の変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for activation range changes");

                    var go = FindTarget(args);
                    if (go == null)
                        return CallToolResult.Error("Target IActivationRangeTarget not found");

                    var before = TargetActivationRangeInfo(go);
                    var error = ApplyRange(go.GetComponent<IActivationRangeTarget>(), args);
                    if (error != null)
                        return CallToolResult.Error(error);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["activationRange"] = TargetActivationRangeInfo(go)
                    });
                }
            };
        }

        public static McpTool BatchSetActivationRanges()
        {
            return new McpTool
            {
                Name = "activation_ranges_batch_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "active_ranges_batch_set", "activation_range_batch_set" },
                Tags = new List<string> { "controls", "side-screen", "activation-range", "batch" },
                Description = "互換用の入口。building_control domain=side_surface surface=activation action=batch を優先して使う。ActiveRangeSideScreen / IActivationRangeTarget の2つのしきい値を一括設定する。複数のスマートバッテリー・貯液庫の設定を揃える用途に適している。items は短縮フィールド a/d/w に対応し、defaults でしきい値・worldId を共有できる。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "配列。各項目は id または x/y/worldId を指定でき、activateValue/deactivateValue も指定可能。短縮フィールドは a=activateValue、d=deactivateValue、w=worldId", Required = true },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "各項目へ統合する既定の引数。activateValue/a、deactivateValue/d、worldId/w に対応。個別項目の引数を優先する", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "2つのしきい値の一括変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for activation range batch changes");

                    var items = args["items"] as JArray;
                    if (items == null || items.Count == 0)
                        return CallToolResult.Error("items array is required");

                    var defaults = args["defaults"] as JObject ?? args["defaultArguments"] as JObject;
                    var results = new List<Dictionary<string, object>>();
                    foreach (var token in items)
                    {
                        var rawItem = token as JObject;
                        if (rawItem == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "item must be an object" });
                            continue;
                        }

                        var item = MergeBatchDefaults(rawItem, defaults);
                        var go = FindTarget(item);
                        if (go == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "Target IActivationRangeTarget not found", ["input"] = item });
                            continue;
                        }

                        var before = TargetActivationRangeInfo(go);
                        var error = ApplyRange(go.GetComponent<IActivationRangeTarget>(), item);
                        results.Add(new Dictionary<string, object>
                        {
                            ["ok"] = error == null,
                            ["error"] = error,
                            ["target"] = TargetInfo(go),
                            ["before"] = before,
                            ["activationRange"] = TargetActivationRangeInfo(go)
                        });
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["requested"] = items.Count,
                        ["succeeded"] = results.Count(item => (bool)item["ok"]),
                        ["failed"] = results.Count(item => !(bool)item["ok"]),
                        ["results"] = results
                    });
                }
            };
        }

        public static McpTool ControlActivationRange()
        {
            return new McpTool
            {
                Name = "activation_range_control",
                Hidden = true,
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "active_range_control", "activation_range_side_screen_control" },
                Tags = new List<string> { "controls", "side-screen", "activation-range", "smart-battery", "reservoir", "batch" },
                Description = "ActiveRange の2つのしきい値の読み取り、個別設定、一括設定を統合する。action=list/set/batch。set/batch は confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、set、batch", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、見出し、タグにより絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=set の対象の KPrefabID InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=list/set の任意の領域または対象の X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=list/set の任意の領域または対象の Y", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定値は現在のワールド、または対象のセルがあるワールド", Required = false },
                    ["activateValue"] = new McpToolParameter { Type = "number", Description = "action=set の作動しきい値。未指定なら変更しない", Required = false },
                    ["deactivateValue"] = new McpToolParameter { Type = "number", Description = "action=set の停止しきい値。未指定なら変更しない", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Description = "action=batch の配列。各項目は id または x/y/worldId を指定でき、activateValue/deactivateValue も指定可能。短縮フィールドは a/d/w", Required = false },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "action=batch で各項目へ統合する既定の引数", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set/batch では true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListActivationRanges().Handler(args);
                    if (action == "set")
                        return SetActivationRange().Handler(args);
                    if (action == "batch")
                        return BatchSetActivationRanges().Handler(args);
                    return CallToolResult.Error("action must be one of list, set, batch");
                }
            };
        }

        private static string ApplyRange(IActivationRangeTarget target, JObject args)
        {
            if (target == null)
                return "Target IActivationRangeTarget not found";

            float activate = ToolUtil.GetFloat(args, "activateValue") ?? target.ActivateValue;
            float deactivate = ToolUtil.GetFloat(args, "deactivateValue") ?? target.DeactivateValue;
            activate = ClampValue(target, activate);
            deactivate = ClampValue(target, deactivate);

            if (activate < deactivate)
                return "activateValue must be greater than or equal to deactivateValue";

            target.ActivateValue = activate;
            target.DeactivateValue = deactivate;
            return null;
        }

        private static float ClampValue(IActivationRangeTarget target, float value)
        {
            float clamped = Mathf.Clamp(value, target.MinValue, target.MaxValue);
            return target.UseWholeNumbers ? Mathf.Round(clamped) : clamped;
        }

        private static Dictionary<string, object> TargetActivationRangeInfo(GameObject go)
        {
            var result = TargetInfo(go);
            var target = go.GetComponent<IActivationRangeTarget>();
            result["activationRange"] = ActivationRangeInfo(target);
            return result;
        }

        private static Dictionary<string, object> ActivationRangeInfo(IActivationRangeTarget target)
        {
            return new Dictionary<string, object>
            {
                ["component"] = target.GetType().Name,
                ["title"] = target.ActivationRangeTitleText,
                ["activateLabel"] = target.ActivateSliderLabelText,
                ["deactivateLabel"] = target.DeactivateSliderLabelText,
                ["activateValue"] = ToolUtil.SafeFloat(target.ActivateValue),
                ["deactivateValue"] = ToolUtil.SafeFloat(target.DeactivateValue),
                ["min"] = ToolUtil.SafeFloat(target.MinValue),
                ["max"] = ToolUtil.SafeFloat(target.MaxValue),
                ["wholeNumbers"] = target.UseWholeNumbers,
                ["activateTooltip"] = FormatTooltip(target.ActivateTooltip, target.ActivateValue, target.DeactivateValue),
                ["deactivateTooltip"] = FormatTooltip(target.DeactivateTooltip, target.DeactivateValue, target.ActivateValue)
            };
        }

        private static string FormatTooltip(string format, float first, float second)
        {
            if (string.IsNullOrWhiteSpace(format))
                return "";
            try
            {
                return string.Format(format, first, second);
            }
            catch
            {
                return format;
            }
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

        private static GameObject FindTarget(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);

            foreach (var go in AllCandidateObjects())
            {
                if (go.GetComponent<IActivationRangeTarget>() == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return go;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return go;
            }
            return null;
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

            CopyAlias(source, target, "activateValue", "a", overwrite);
            CopyAlias(source, target, "deactivateValue", "d", overwrite);
            CopyAlias(source, target, "worldId", "w", overwrite);
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
            return string.Equals(name, "activateValue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "a", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "deactivateValue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "d", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "worldId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "w", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesTarget(GameObject go, Dictionary<string, int> rect, int worldId)
        {
            if (go == null || go.GetComponent<IActivationRangeTarget>() == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
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
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y", Required = false },
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

        private static CallToolResult JsonResult(object payload)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
        }
    }
}
