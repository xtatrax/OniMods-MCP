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
    public static partial class BuildingConfigTools
    {
        public static McpTool CopySettings()
        {
            return new McpTool
            {
                Name = "buildings_copy_settings",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "copy_building_settings", "settings_copy" },
                Tags = new List<string> { "buildings", "copy", "settings", "side-screen", "batch" },
                Description = "互換用の入口。building_control domain=config action=copy_settings を使用してください",
                Hidden = true,
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["sourceId"] = new McpToolParameter { Type = "integer", Description = "コピー元設備の InstanceID。sourceX/sourceY より優先", Required = false },
                    ["sourceX"] = new McpToolParameter { Type = "integer", Description = "コピー元設備のセル X", Required = false },
                    ["sourceY"] = new McpToolParameter { Type = "integer", Description = "コピー元設備のセル Y", Required = false },
                    ["targetId"] = new McpToolParameter { Type = "integer", Description = "単一のコピー先設備の InstanceID。指定時は区域を無視", Required = false },
                    ["targetX"] = new McpToolParameter { Type = "integer", Description = "単一のコピー先設備のセル X。targetY も必要", Required = false },
                    ["targetY"] = new McpToolParameter { Type = "integer", Description = "単一のコピー先設備のセル Y。targetX も必要", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "区域が 100 セルを超える場合は true が必須", Required = false }
                }),
                Handler = args =>
                {
                    var sourceArgs = new JObject();
                    if (args["sourceId"] != null)
                        sourceArgs["id"] = args["sourceId"];
                    if (args["sourceX"] != null)
                        sourceArgs["x"] = args["sourceX"];
                    if (args["sourceY"] != null)
                        sourceArgs["y"] = args["sourceY"];
                    if (args["worldId"] != null)
                        sourceArgs["worldId"] = args["worldId"];

                    var source = FindTarget(sourceArgs);
                    if (source == null)
                        return CallToolResult.Error("Source building not found; provide sourceId or sourceX/sourceY");
                    var sourceId = source.GetComponent<KPrefabID>();
                    var sourceSettings = source.GetComponent<CopyBuildingSettings>();
                    if (sourceId == null || sourceSettings == null)
                        return CallToolResult.Error("Source building does not support Copy Settings");

                    var targets = new List<KPrefabID>();
                    if (args["targetId"] != null || (args["targetX"] != null && args["targetY"] != null))
                    {
                        var targetArgs = new JObject();
                        if (args["targetId"] != null)
                            targetArgs["id"] = args["targetId"];
                        if (args["targetX"] != null)
                            targetArgs["x"] = args["targetX"];
                        if (args["targetY"] != null)
                            targetArgs["y"] = args["targetY"];
                        if (args["worldId"] != null)
                            targetArgs["worldId"] = args["worldId"];
                        var target = FindTarget(targetArgs);
                        if (target == null)
                            return CallToolResult.Error("Target building not found");
                        var targetId = target.GetComponent<KPrefabID>();
                        if (targetId != null)
                            targets.Add(targetId);
                    }
                    else
                    {
                        if (!HasRectInput(args))
                            return CallToolResult.Error("targetId, targetX/targetY, areaId or x1/y1/x2/y2 are required");

                        var rect = ToolUtil.GetRect(args);
                        int cells = (rect["x2"] - rect["x1"] + 1) * (rect["y2"] - rect["y1"] + 1);
                        if (cells > 100 && !ToolUtil.GetBool(args, "confirm", false))
                            return CallToolResult.Error("confirm=true is required when copying settings across more than 100 cells");

                        int worldId = ToolUtil.ResolveWorldId(args);
                        ObjectLayer layer = CopyBuildingSettings.ResolveLayer(source);
                        var seen = new HashSet<KPrefabID>();
                        for (int y = rect["y1"]; y <= rect["y2"]; y++)
                        {
                            for (int x = rect["x1"]; x <= rect["x2"]; x++)
                            {
                                int cell = Grid.XYToCell(x, y);
                                if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                                    continue;
                                var targetId = CopyBuildingSettings.ResolveTarget(layer, cell);
                                if (targetId != null && targetId.gameObject != source && seen.Add(targetId))
                                    targets.Add(targetId);
                            }
                        }
                    }

                    int applied = 0;
                    int skipped = 0;
                    var results = new List<Dictionary<string, object>>();
                    foreach (var target in targets)
                    {
                        if (target == null || target.gameObject == null || target.gameObject == source)
                        {
                            skipped++;
                            continue;
                        }

                        bool copied = CopyBuildingSettings.ApplyCopy(target, source, sourceId, sourceSettings);
                        if (copied)
                            applied++;
                        else
                            skipped++;
                        var info = TargetInfo(target.gameObject);
                        info["status"] = copied ? "copied" : "skipped_incompatible";
                        results.Add(info);
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["source"] = TargetInfo(source),
                        ["matched"] = targets.Count,
                        ["applied"] = applied,
                        ["skipped"] = skipped,
                        ["targets"] = results.Take(200).ToList(),
                        ["truncatedTargets"] = Math.Max(0, results.Count - 200)
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象オブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。座標による検索では現在アクティブなワールドが既定", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> BuildingConfigControlParams()
        {
            return LookupParams(RectParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list/list_automation/set_enabled/set_toggle/set_threshold/set_slider/set_valve_flow/set_limit_valve/set_logic_timer/set_logic_ribbon_bit/set_door_state/get_access/set_access/copy_settings/batch_set/batch_set_automation/state_list/state_set/visual。operation とも互換", Required = false },
                ["operation"] = new McpToolParameter { Type = "string", Description = "旧引数との互換用。action を優先", Required = false },
                ["items"] = new McpToolParameter { Type = "array", Description = "action=batch_set/batch_set_automation のバッチ操作配列", Required = false },
                ["defaults"] = new McpToolParameter { Type = "object", Description = "action=batch_set/batch_set_automation で各項目に結合する既定の引数", Required = false },
                ["capability"] = new McpToolParameter { Type = "string", Description = "action=list で設定可能な機能による絞り込み", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list/list_automation で名前または prefabId による絞り込み", Required = false },
                ["limit"] = new McpToolParameter { Type = "number", Description = "action=list/list_automation では戻り値の件数上限、action=set_limit_valve では目標通過量の上限", Required = false },
                ["enabled"] = new McpToolParameter { Type = "boolean", Description = "action=set_enabled の目標状態。デュプのネイティブ切替作業を予約・取消する（即時変更ではない）", Required = false },
                ["on"] = new McpToolParameter { Type = "boolean", Description = "action=set_toggle で true はプレイヤーの手動スイッチをオン、false はオフ", Required = false },
                ["component"] = new McpToolParameter { Type = "string", Description = "閾値・slider の対象コンポーネントを型名で絞り込み", Required = false },
                ["threshold"] = new McpToolParameter { Type = "number", Description = "action=set_threshold の閾値", Required = false },
                ["activateAbove"] = new McpToolParameter { Type = "boolean", Description = "action=set_threshold で閾値を上回ると作動", Required = false },
                ["value"] = new McpToolParameter { Type = "number", Description = "action=set_slider の slider 目標値。action=state_set kind=checkbox では boolean", Required = false },
                ["index"] = new McpToolParameter { Type = "integer", Description = "action=set_slider の slider インデックス", Required = false },
                ["flowKgPerSecond"] = new McpToolParameter { Type = "number", Description = "action=set_valve_flow の流量 kg/s", Required = false },
                ["resetAmount"] = new McpToolParameter { Type = "boolean", Description = "action=set_limit_valve で通過済みカウントをリセットするか", Required = false },
                ["onSeconds"] = new McpToolParameter { Type = "number", Description = "action=set_logic_timer のグリーン信号の継続秒数", Required = false },
                ["offSeconds"] = new McpToolParameter { Type = "number", Description = "action=set_logic_timer のレッド信号の継続秒数", Required = false },
                ["displayCyclesMode"] = new McpToolParameter { Type = "boolean", Description = "action=set_logic_timer でサイクル単位で表示するか", Required = false },
                ["reset"] = new McpToolParameter { Type = "boolean", Description = "action=set_logic_timer でタイマーをリセットするか", Required = false },
                ["bitIndex"] = new McpToolParameter { Type = "integer", Description = "action=set_logic_ribbon_bit の bit インデックス 0..3", Required = false },
                ["state"] = new McpToolParameter { Type = "string", Description = "action=set_door_state：auto/opened/locked", Required = false },
                ["kind"] = new McpToolParameter { Type = "string", Description = "action=state_list/state_set では any/capacity/checkbox/counter/time_range、action=visual では light/pixel_pack", Required = false },
                ["visualAction"] = new McpToolParameter { Type = "string", Description = "action=visual のサブ操作: light は list/set_color、pixel_pack は list/set_color/copy_colors に対応", Required = false },
                ["colorIndex"] = new McpToolParameter { Type = "integer", Description = "action=visual で色を設定する際の対象色プリセットのインデックス", Required = false },
                ["colorName"] = new McpToolParameter { Type = "string", Description = "action=visual で色を設定する際の対象色プリセットの名前", Required = false },
                ["panel"] = new McpToolParameter { Type = "string", Description = "action=visual kind=pixel_pack の対象パネル", Required = false },
                ["sourcePanel"] = new McpToolParameter { Type = "string", Description = "action=visual kind=pixel_pack visualAction=copy_colors のコピー元パネル", Required = false },
                ["sourceState"] = new McpToolParameter { Type = "string", Description = "action=visual kind=pixel_pack visualAction=copy_colors のコピー元の状態", Required = false },
                ["targetPanel"] = new McpToolParameter { Type = "string", Description = "action=visual kind=pixel_pack visualAction=copy_colors のコピー先パネル", Required = false },
                ["targetState"] = new McpToolParameter { Type = "string", Description = "action=visual kind=pixel_pack visualAction=copy_colors のコピー先の状態", Required = false },
                ["capacity"] = new McpToolParameter { Type = "number", Description = "action=state_set kind=capacity の目標容量", Required = false },
                ["maxCount"] = new McpToolParameter { Type = "integer", Description = "action=state_set kind=counter の目標最大カウント", Required = false },
                ["advancedMode"] = new McpToolParameter { Type = "boolean", Description = "action=state_set kind=counter で高度なモードを有効にするか", Required = false },
                ["start"] = new McpToolParameter { Type = "number", Description = "action=state_set kind=time_range の開始時刻。サイクルに対する割合 0-1", Required = false },
                ["duration"] = new McpToolParameter { Type = "number", Description = "action=state_set kind=time_range の継続時間。サイクルに対する割合 0-1", Required = false },
                ["includeDupes"] = new McpToolParameter { Type = "boolean", Description = "action=get_access で複製人間の通行権限を含めるか", Required = false },
                ["scope"] = new McpToolParameter { Type = "string", Description = "action=set_access：default_standard/default_bionic/default_robot/dupe", Required = false },
                ["permission"] = new McpToolParameter { Type = "string", Description = "action=set_access：both/go_left/go_right/neither", Required = false },
                ["dupeId"] = new McpToolParameter { Type = "integer", Description = "action=set_access scope=dupe の複製人間または proxy の InstanceID", Required = false },
                ["dupeName"] = new McpToolParameter { Type = "string", Description = "action=set_access scope=dupe の複製人間の名前", Required = false },
                ["clear"] = new McpToolParameter { Type = "boolean", Description = "action=set_access scope=dupe で個別権限を解除", Required = false },
                ["sourceId"] = new McpToolParameter { Type = "integer", Description = "action=copy_settings のコピー元設備の InstanceID", Required = false },
                ["sourceX"] = new McpToolParameter { Type = "integer", Description = "action=copy_settings のコピー元設備の X", Required = false },
                ["sourceY"] = new McpToolParameter { Type = "integer", Description = "action=copy_settings のコピー元設備の Y", Required = false },
                ["targetId"] = new McpToolParameter { Type = "integer", Description = "action=copy_settings の単一のコピー先設備の InstanceID", Required = false },
                ["targetX"] = new McpToolParameter { Type = "integer", Description = "action=copy_settings の単一のコピー先設備の X", Required = false },
                ["targetY"] = new McpToolParameter { Type = "integer", Description = "action=copy_settings の単一のコピー先設備の Y", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=copy_settings で広い区域にコピーする場合、true が必要になることがある", Required = false }
            }));
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。指定すると x1/y1/x2/y2 を省略可能", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の左下・始点 X。areaId を使用する場合は省略可能", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の左下・始点 Y。areaId を使用する場合は省略可能", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の右上・終点 X。areaId を使用する場合は省略可能", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の右上・終点 Y。areaId を使用する場合は省略可能", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。既定は areaId に紐付くワールドまたは現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
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

            if (id.HasValue)
            {
                foreach (var prioritizable in Components.Prioritizables.Items)
                {
                    var go = prioritizable?.gameObject;
                    if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                        continue;
                    var kpid = go.GetComponent<KPrefabID>();
                    if (kpid != null && kpid.InstanceID == id.Value)
                        return go;
                }
            }

            return null;
        }

        private static IThresholdSwitch FindThresholdSwitch(GameObject go, string componentName)
        {
            var matches = go.GetComponents<Component>()
                .OfType<IThresholdSwitch>()
                .ToList();
            if (matches.Count == 0)
                return null;
            if (string.IsNullOrWhiteSpace(componentName))
                return matches[0];

            return matches.FirstOrDefault(item => item.GetType().Name.IndexOf(componentName.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static ISliderControl FindSliderControl(GameObject go, string componentName)
        {
            var matches = SliderControls(go)
                .Select(item => item.Control)
                .Where(item => item != null)
                .Distinct()
                .ToList();
            if (matches.Count == 0)
                return null;
            if (string.IsNullOrWhiteSpace(componentName))
                return matches[0];

            return matches.FirstOrDefault(item => item.GetType().Name.IndexOf(componentName.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static Dictionary<string, object> BuildConfigInfo(GameObject go)
        {
            var capabilities = new List<string>();
            var thresholds = go.GetComponents<Component>().OfType<IThresholdSwitch>().Select(ThresholdInfo).ToList();
            var sliders = SliderControlInfos(go);
            if (go.GetComponent<BuildingEnabledButton>() != null)
                capabilities.Add("enabled");
            if (go.GetComponents<Component>().OfType<IPlayerControlledToggle>().Any())
                capabilities.Add("toggle");
            if (thresholds.Count > 0)
                capabilities.Add("threshold");
            if (sliders.Count > 0)
                capabilities.Add("slider");
            if (go.GetComponent<DirectionControl>() != null)
                capabilities.Add("direction");
            if (go.GetComponent<FewOptionSideScreen.IFewOptionSideScreen>() != null)
                capabilities.Add("few_option");
            if (go.GetComponent<LogicBroadcastReceiver>() != null)
                capabilities.Add("broadcast_receiver");
            if (go.GetComponent<IHighEnergyParticleDirection>() != null)
                capabilities.Add("radbolt_direction");
            var capacity = go.GetComponent<IUserControlledCapacity>() ?? go.GetSMI<IUserControlledCapacity>();
            if (capacity != null && capacity.ControlEnabled())
                capabilities.Add("capacity");
            if (go.GetComponent<ICheckboxControl>() != null || go.GetSMI<ICheckboxControl>() != null)
                capabilities.Add("checkbox");
            if (go.GetComponent<LogicCounter>() != null)
                capabilities.Add("counter");
            if (go.GetComponent<LogicTimeOfDaySensor>() != null)
                capabilities.Add("time_range");
            if (go.GetComponent<LightColorMenu>() != null)
                capabilities.Add("light_color");
            if (go.GetComponent<Door>() != null)
                capabilities.Add("door");
            if (go.GetComponent<AccessControl>() != null)
                capabilities.Add("access");
            if (go.GetComponent<ManualDeliveryKG>() != null)
                capabilities.Add("manual_delivery");
            if (go.GetComponent<Filterable>() != null)
                capabilities.Add("filterable");
            if (go.GetComponent<TreeFilterable>() != null)
                capabilities.Add("tree_filter");
            if (go.GetComponent<FlatTagFilterable>() != null)
                capabilities.Add("flat_filter");
            if (go.GetComponent<Valve>() != null)
                capabilities.Add("valve");
            if (go.GetComponent<LimitValve>() != null)
                capabilities.Add("limit_valve");
            if (go.GetComponent<LogicTimerSensor>() != null)
                capabilities.Add("timer");
            if (go.GetComponent<ILogicRibbonBitSelector>() != null)
                capabilities.Add("ribbon_bit");
            if (go.GetComponent<LogicPorts>() != null)
                capabilities.Add("logic_ports");
            if (IsAutomationControl(go))
                capabilities.Add("automation");

            if (go.GetComponent<IActivationRangeTarget>() != null)
                capabilities.Add("activation_range");
            var result = TargetInfo(go);
            result["capabilities"] = capabilities;
            var enabledButton = go.GetComponent<BuildingEnabledButton>();
            if (enabledButton != null)
            {
                result["enabled"] = enabledButton.IsEnabled;
                try { result["enabledControl"] = NativeBuildingToggle.Read(enabledButton); }
                catch (Exception ex) { result["enabledControlError"] = ex.GetBaseException().Message; }
            }
            var playerToggle = go.GetComponents<Component>().OfType<IPlayerControlledToggle>().FirstOrDefault();
            if (playerToggle != null)
            {
                result["toggle"] = playerToggle.ToggledOn();
                result["toggleRequested"] = playerToggle.ToggleRequested;
                result["requestedToggle"] = playerToggle.ToggleRequested ? !playerToggle.ToggledOn() : playerToggle.ToggledOn();
                result["toggleRequiresDuplicantWork"] = false;
            }
            if (thresholds.Count > 0)
                result["thresholds"] = thresholds;
            if (sliders.Count > 0)
                result["sliders"] = sliders;
            var door = go.GetComponent<Door>();
            if (door != null)
                result["door"] = DoorInfo(door);
            var access = go.GetComponent<AccessControl>();
            if (access != null)
                result["access"] = AccessDefaults(access);
            var valve = go.GetComponent<Valve>();
            if (valve != null)
                result["valve"] = ValveInfo(valve);
            var limitValve = go.GetComponent<LimitValve>();
            if (limitValve != null)
                result["limitValve"] = LimitValveInfo(limitValve);
            var timer = go.GetComponent<LogicTimerSensor>();
            if (timer != null)
                result["timer"] = TimerInfo(timer);
            var ribbon = go.GetComponent<ILogicRibbonBitSelector>();
            if (ribbon != null)
                result["ribbonBit"] = RibbonBitInfo(ribbon);
            var ports = go.GetComponent<LogicPorts>();
            if (ports != null)
                result["logicPorts"] = LogicPortsInfo(ports);
            var included = new HashSet<string>();
            foreach (var entry in new[] { "enabled", "toggle", "thresholds", "sliders", "door", "access", "valve", "limitValve", "timer", "ribbonBit", "logicPorts" })
                if (result.ContainsKey(entry)) included.Add(entry);
            var fields = new Dictionary<string, string> { ["threshold"] = "thresholds", ["slider"] = "sliders", ["limit_valve"] = "limitValve", ["ribbon_bit"] = "ribbonBit", ["logic_ports"] = "logicPorts" };
            result["configurationReads"] = capabilities.Select(cap => BuildingOperationGuidance.ConfigurationRead(cap,
                included.Contains(fields.ContainsKey(cap) ? fields[cap] : cap),
                go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name,
                Grid.IsValidCell(Grid.PosToCell(go)) ? Grid.WorldIdx[Grid.PosToCell(go)] : -1, go.GetComponent<KPrefabID>()?.InstanceID ?? go.GetInstanceID())).ToList();
            return result;
        }

        internal static Dictionary<string, object> SnapshotConfig(GameObject go)
        {
            return BuildConfigInfo(go);
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

        private static Dictionary<string, object> ThresholdInfo(IThresholdSwitch threshold)
        {
            float? current = ThresholdReading.Current(threshold);
            return new Dictionary<string, object>
            {
                ["component"] = threshold.GetType().Name,
                ["title"] = threshold.Title.ToString(),
                ["valueName"] = threshold.ThresholdValueName.ToString(),
                ["threshold"] = ToolUtil.SafeFloat(threshold.Threshold),
                ["currentValue"] = current,
                ["samplePending"] = !current.HasValue,
                ["formattedThreshold"] = threshold.Format(threshold.Threshold, true),
                ["formattedCurrentValue"] = current.HasValue ? threshold.Format(current.Value, true) : null,
                ["activateAbove"] = threshold.ActivateAboveThreshold,
                ["rangeMin"] = ToolUtil.SafeFloat(threshold.RangeMin),
                ["rangeMax"] = ToolUtil.SafeFloat(threshold.RangeMax),
                ["inputMin"] = ToolUtil.SafeFloat(threshold.GetRangeMinInputField()),
                ["inputMax"] = ToolUtil.SafeFloat(threshold.GetRangeMaxInputField()),
                ["units"] = threshold.ThresholdValueUnits().ToString()
            };
        }

    }
}
