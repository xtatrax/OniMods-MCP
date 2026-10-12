using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class AutomationSideScreenTools
    {
        public static McpTool ControlAutomationSideScreen()
        {
            return new McpTool
            {
                Name = "automation_side_control",
                Hidden = true,
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "automation_side_screen_control", "side_automation_control" },
                Tags = new List<string> { "automation", "side-screen", "automatable", "critter-sensor", "batch" },
                Description = "自動化サイドパネルの読み取り、個別設定、一括設定を統合する。kind=automatable/critter_sensor。action=list/set/batch。set/batch は confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "操作項目の種類: automatable または critter_sensor", Required = true },
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、set、batch", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名や prefabId により絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "action=list の領域ハンドル。x1/y1/x2/y2 とのどちらかを指定する", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の Y", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=set の対象の KPrefabID.InstanceID。指定を推奨する", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=set の対象のセルの X。id が未指定の場合に使う", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=set の対象のセルの Y。id が未指定の場合に使う", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。list では絞り込みに使え、set で座標から探す場合は指定を推奨する", Required = false },
                    ["allowManual"] = new McpToolParameter { Type = "boolean", Description = "kind=automatable action=set/batch では true で手動運搬を許可する", Required = false },
                    ["automationOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=automatable action=set/batch で、自動化による運搬だけを許可するかを直接設定する", Required = false },
                    ["countCritters"] = new McpToolParameter { Type = "boolean", Description = "kind=critter_sensor action=set/batch で、動物を数に含めるか", Required = false },
                    ["countEggs"] = new McpToolParameter { Type = "boolean", Description = "kind=critter_sensor action=set/batch で、卵を数に含めるか", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Description = "action=batch の配列。各項目は id または x/y/worldId を指定でき、対応する kind の設定フィールドを渡す", Required = false },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "action=batch で各項目へ統合する既定の引数", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set/batch では true が必須", Required = false }
                },
                Handler = args =>
                {
                    string kind = NormalizeKind(args["kind"]?.ToString());
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();

                    if (kind == "automatable")
                    {
                        if (action == "list")
                            return ListAutomatableControls().Handler(args);
                        if (action == "set")
                            return SetAutomatableControl().Handler(args);
                        if (action == "batch")
                            return BatchSetAutomatableControls().Handler(args);
                    }
                    else if (kind == "critter_sensor")
                    {
                        if (action == "list")
                            return ListCritterSensors().Handler(args);
                        if (action == "set")
                            return SetCritterSensorCounting().Handler(args);
                        if (action == "batch")
                            return BatchSetCritterSensors().Handler(args);
                    }

                    return CallToolResult.Error("kind must be automatable or critter_sensor; action must be list, set, or batch");
                }
            };
        }

        private static string NormalizeKind(string value)
        {
            string kind = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_");
            if (kind == "manual" || kind == "automatable_control" || kind == "automatable_controls")
                return "automatable";
            if (kind == "critter" || kind == "critter_sensor_counting" || kind == "critter_sensors" || kind == "logic_critter_sensor")
                return "critter_sensor";
            return kind;
        }

        private static void ApplyAutomatable(Automatable automatable, JObject args)
        {
            bool automationOnly = ToolUtil.GetBool(args, "automationOnly", automatable.GetAutomationOnly());
            if (args["allowManual"] != null)
                automationOnly = !ToolUtil.GetBool(args, "allowManual", !automationOnly);
            automatable.SetAutomationOnly(automationOnly);
        }

        private static void ApplyCritterSensor(LogicCritterCountSensor sensor, JObject args)
        {
            if (args["countCritters"] != null)
                sensor.countCritters = ToolUtil.GetBool(args, "countCritters", sensor.countCritters);
            if (args["countEggs"] != null)
                sensor.countEggs = ToolUtil.GetBool(args, "countEggs", sensor.countEggs);
        }

        private static JObject MergeDefaults(JObject item, JObject defaults)
        {
            var result = defaults == null ? new JObject() : (JObject)defaults.DeepClone();
            if (item == null)
                return result;

            foreach (var property in item.Properties())
                result[property.Name] = property.Value.DeepClone();
            return result;
        }

        private static Dictionary<string, object> AutomatableInfo(GameObject go)
        {
            var automatable = go.GetComponent<Automatable>();
            var result = TargetInfo(go);
            result["automationOnly"] = automatable.GetAutomationOnly();
            result["allowManual"] = !automatable.GetAutomationOnly();
            result["component"] = automatable.GetType().Name;
            return result;
        }

        private static Dictionary<string, object> CritterSensorInfo(GameObject go)
        {
            var sensor = go.GetComponent<LogicCritterCountSensor>();
            var result = TargetInfo(go);
            result["countCritters"] = sensor.countCritters;
            result["countEggs"] = sensor.countEggs;
            result["currentCount"] = sensor.currentCount;
            result["threshold"] = sensor.countThreshold;
            result["activateAboveThreshold"] = sensor.activateOnGreaterThan;
            result["isSwitchedOn"] = sensor.IsSwitchedOn;
            result["rangeMin"] = ToolUtil.SafeFloat(sensor.RangeMin);
            result["rangeMax"] = ToolUtil.SafeFloat(sensor.RangeMax);
            return result;
        }

        private static GameObject FindTarget(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);

            foreach (var building in Components.BuildingCompletes.Items)
            {
                var go = building?.gameObject;
                if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                if (id.HasValue)
                {
                    var kpid = go.GetComponent<KPrefabID>();
                    if (kpid != null && kpid.InstanceID == id.Value)
                        return go;
                }
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return go;
            }
            return null;
        }

        private static Dictionary<string, object> TargetInfo(GameObject go)
        {
            int cell = Grid.PosToCell(go);
            var kpid = go.GetComponent<KPrefabID>();
            var building = go.GetComponent<Building>();
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
            return string.IsNullOrWhiteSpace(query)
                || JsonConvert.SerializeObject(info).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
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
            parameters["id"] = new McpToolParameter { Type = "integer", Description = "対象の KPrefabID.InstanceID。指定を推奨する", Required = false };
            parameters["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X。id が未指定の場合に使う", Required = false };
            parameters["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y。id が未指定の場合に使う", Required = false };
            parameters["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標から探す場合は指定を推奨する", Required = false };
            return parameters;
        }

        private static CallToolResult JsonResult(object payload)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
        }
    }
}
