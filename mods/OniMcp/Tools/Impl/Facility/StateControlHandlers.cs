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
    public static partial class StateControlTools
    {
        private static CallToolResult SetStateControl(JObject args, string forcedKind)
        {
            var go = FindTarget(args);
            if (go == null)
                return CallToolResult.Error("Target not found");

            string kind = forcedKind ?? (args["kind"]?.ToString() ?? "").Trim().ToLowerInvariant();
            switch (kind)
            {
                case "capacity":
                    return SetCapacityControl(go, args);
                case "checkbox":
                    return SetCheckboxControl(go, args);
                case "counter":
                    return SetCounterControl(go, args);
                case "time_range":
                case "time-range":
                case "timerange":
                    return SetTimeRangeControl(go, args);
                default:
                    return CallToolResult.Error("kind must be capacity, checkbox, counter, or time_range");
            }
        }

        private static CallToolResult SetCapacityControl(GameObject go, JObject args)
        {
            var capacity = GetCapacity(go);
            if (capacity == null || !capacity.ControlEnabled())
                return CallToolResult.Error("Target does not expose an enabled IUserControlledCapacity");

            float? requested = ToolUtil.GetFloat(args, "capacity");
            if (!requested.HasValue)
                return CallToolResult.Error("capacity is required");

            float before = capacity.UserMaxCapacity;
            float next = Mathf.Clamp(requested.Value, capacity.MinCapacity, capacity.MaxCapacity);
            if (capacity.WholeValues)
                next = Mathf.Round(next);
            capacity.UserMaxCapacity = next;

            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["target"] = TargetInfo(go),
                ["kind"] = "capacity",
                ["before"] = ToolUtil.SafeFloat(before),
                ["capacity"] = CapacityInfo(capacity),
                ["changed"] = Math.Abs(before - capacity.UserMaxCapacity) > 0.0001f
            }, McpJsonUtil.Settings));
        }

        private static CallToolResult SetCheckboxControl(GameObject go, JObject args)
        {
            var checkbox = GetCheckbox(go);
            if (checkbox == null)
                return CallToolResult.Error("Target does not expose ICheckboxControl");

            bool before = checkbox.GetCheckboxValue();
            bool value = ToolUtil.GetBool(args, "value", before);
            checkbox.SetCheckboxValue(value);

            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["target"] = TargetInfo(go),
                ["kind"] = "checkbox",
                ["before"] = before,
                ["checkbox"] = CheckboxInfo(checkbox),
                ["changed"] = before != checkbox.GetCheckboxValue()
            }, McpJsonUtil.Settings));
        }

        private static CallToolResult SetCounterControl(GameObject go, JObject args)
        {
            var counter = go.GetComponent<LogicCounter>();
            if (counter == null)
                return CallToolResult.Error("Target does not expose LogicCounter");

            var before = CounterInfo(counter);
            int? maxCount = ToolUtil.GetInt(args, "maxCount");
            if (maxCount.HasValue)
            {
                counter.maxCount = Mathf.Clamp(maxCount.Value, 1, 10);
                if (counter.currentCount > counter.maxCount)
                    counter.currentCount = counter.maxCount;
            }
            if (args["advancedMode"] != null)
                counter.advancedMode = ToolUtil.GetBool(args, "advancedMode", counter.advancedMode);
            if (ToolUtil.GetBool(args, "reset", false))
                counter.ResetCounter();
            else
            {
                counter.SetCounterState();
                counter.UpdateLogicCircuit();
                counter.UpdateVisualState(force: true);
                counter.UpdateMeter();
            }

            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["target"] = TargetInfo(go),
                ["kind"] = "counter",
                ["before"] = before,
                ["counter"] = CounterInfo(counter)
            }, McpJsonUtil.Settings));
        }

        private static CallToolResult SetTimeRangeControl(GameObject go, JObject args)
        {
            var sensor = go.GetComponent<LogicTimeOfDaySensor>();
            if (sensor == null)
                return CallToolResult.Error("Target does not expose LogicTimeOfDaySensor");

            var before = TimeRangeInfo(sensor);
            float? start = ToolUtil.GetFloat(args, "start");
            float? duration = ToolUtil.GetFloat(args, "duration");
            if (!start.HasValue && !duration.HasValue)
                return CallToolResult.Error("start or duration is required");
            if (start.HasValue)
                sensor.startTime = Mathf.Clamp01(start.Value);
            if (duration.HasValue)
                sensor.duration = Mathf.Clamp01(duration.Value);

            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["target"] = TargetInfo(go),
                ["kind"] = "time_range",
                ["before"] = before,
                ["timeRange"] = TimeRangeInfo(sensor)
            }, McpJsonUtil.Settings));
        }

        private static Dictionary<string, object> ControlInfo(GameObject go)
        {
            var result = TargetInfo(go);
            var kinds = new List<string>();

            var capacity = GetCapacity(go);
            if (capacity != null && capacity.ControlEnabled())
            {
                kinds.Add("capacity");
                result["capacity"] = CapacityInfo(capacity);
            }

            var checkbox = GetCheckbox(go);
            if (checkbox != null)
            {
                kinds.Add("checkbox");
                result["checkbox"] = CheckboxInfo(checkbox);
            }

            var counter = go.GetComponent<LogicCounter>();
            if (counter != null)
            {
                kinds.Add("counter");
                result["counter"] = CounterInfo(counter);
            }

            var timeRange = go.GetComponent<LogicTimeOfDaySensor>();
            if (timeRange != null)
            {
                kinds.Add("time_range");
                result["timeRange"] = TimeRangeInfo(timeRange);
            }

            result["controlKinds"] = kinds.OrderBy(kind => kind).ToList();
            return result;
        }

        internal static Dictionary<string, object> SnapshotState(GameObject go)
        {
            return ControlInfo(go);
        }

        private static IUserControlledCapacity GetCapacity(GameObject go)
        {
            return go.GetComponent<IUserControlledCapacity>() ?? go.GetSMI<IUserControlledCapacity>();
        }

        private static ICheckboxControl GetCheckbox(GameObject go)
        {
            return go.GetComponent<ICheckboxControl>() ?? go.GetSMI<ICheckboxControl>();
        }

        private static Dictionary<string, object> CapacityInfo(IUserControlledCapacity capacity)
        {
            return new Dictionary<string, object>
            {
                ["userMaxCapacity"] = ToolUtil.SafeFloat(capacity.UserMaxCapacity),
                ["amountStored"] = ToolUtil.SafeFloat(capacity.AmountStored),
                ["min"] = ToolUtil.SafeFloat(capacity.MinCapacity),
                ["max"] = ToolUtil.SafeFloat(capacity.MaxCapacity),
                ["wholeValues"] = capacity.WholeValues,
                ["units"] = capacity.CapacityUnits.ToString(),
                ["enabled"] = capacity.ControlEnabled()
            };
        }

        private static Dictionary<string, object> CheckboxInfo(ICheckboxControl checkbox)
        {
            return new Dictionary<string, object>
            {
                ["titleKey"] = checkbox.CheckboxTitleKey,
                ["label"] = ToolUtil.CleanName(checkbox.CheckboxLabel),
                ["tooltip"] = ToolUtil.CleanName(checkbox.CheckboxTooltip),
                ["value"] = checkbox.GetCheckboxValue()
            };
        }

        private static Dictionary<string, object> CounterInfo(LogicCounter counter)
        {
            return new Dictionary<string, object>
            {
                ["currentCount"] = counter.currentCount,
                ["maxCount"] = counter.maxCount,
                ["advancedMode"] = counter.advancedMode,
                ["resetCountAtMax"] = counter.resetCountAtMax,
                ["receivedFirstSignal"] = counter.receivedFirstSignal
            };
        }

        private static Dictionary<string, object> TimeRangeInfo(LogicTimeOfDaySensor sensor)
        {
            return new Dictionary<string, object>
            {
                ["start"] = ToolUtil.SafeFloat(sensor.startTime),
                ["duration"] = ToolUtil.SafeFloat(sensor.duration),
                ["startPercent"] = Math.Round(ToolUtil.SafeFloat(sensor.startTime) * 100f, 2),
                ["durationPercent"] = Math.Round(ToolUtil.SafeFloat(sensor.duration) * 100f, 2)
            };
        }

        private static bool MatchesTarget(GameObject go, Dictionary<string, int> rect, int worldId)
        {
            if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                return false;
            int cell = Grid.PosToCell(go);
            return rect == null || CellInRect(cell, rect, worldId);
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
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return go;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return go;
            }
            return null;
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
                || (args["x1"] != null && args["y1"] != null && args["x2"] != null && args["y2"] != null);
        }

        private static bool CellInRect(int cell, Dictionary<string, int> rect, int worldId)
        {
            if (!Grid.IsValidCell(cell)) return false;
            if (!ToolUtil.CellMatchesWorld(cell, worldId)) return false;
            int x = Grid.CellColumn(cell);
            int y = Grid.CellRow(cell);
            return x >= rect["x1"] && x <= rect["x2"] && y >= rect["y1"] && y <= rect["y2"];
        }
    }
}
