using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class MiscSideScreenTools
    {
        public static McpTool ListLogicAlarms()
        {
            return new McpTool
            {
                Name = "logic_alarms_list",
                Group = "automation",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "alarm_side_screens_list" },
                Tags = new List<string> { "automation", "logic", "alarm", "notification", "side-screen" },
                Description = "互換用の入口。building_control domain=side_surface surface=misc kind=logic_alarm action=list を優先して使う。LogicAlarmSideScreen の名前、説明、通知の種類、一時停止、カメラ移動の設定を一覧表示する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備、prefabId、警報名、説明、種類で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args => ListTargets(args, go => go.GetComponent<LogicAlarm>() != null, go => LogicAlarmInfo(go.GetComponent<LogicAlarm>()), "alarms")
            };
        }

        public static McpTool SetLogicAlarm()
        {
            return new McpTool
            {
                Name = "logic_alarm_set",
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "alarm_side_screen_set" },
                Tags = new List<string> { "automation", "logic", "alarm", "notification", "side-screen" },
                Description = "互換用の入口。building_control domain=side_surface surface=misc kind=logic_alarm action=set を優先して使う。LogicAlarmSideScreen の通知名、説明、種類、発動時の一時停止とカメラ移動を設定する",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["name"] = new McpToolParameter { Type = "string", Description = "警報名。最大 30 文字。未指定なら変更しない", Required = false },
                    ["tooltip"] = new McpToolParameter { Type = "string", Description = "警報の説明。最大 90 文字。未指定なら変更しない", Required = false },
                    ["type"] = new McpToolParameter { Type = "string", Description = "bad、neutral、duplicant_threatening のいずれか。未指定なら変更しない", Required = false, EnumValues = new List<string> { "bad", "neutral", "duplicant_threatening" } },
                    ["pauseOnNotify"] = new McpToolParameter { Type = "boolean", Description = "発動時に一時停止するか。未指定なら変更しない", Required = false },
                    ["zoomOnNotify"] = new McpToolParameter { Type = "boolean", Description = "発動時にカメラを移動するか。未指定なら変更しない", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindTarget(args, target => target.GetComponent<LogicAlarm>() != null);
                    if (go == null)
                        return CallToolResult.Error("Target LogicAlarm not found");
                    var alarm = go.GetComponent<LogicAlarm>();
                    var before = LogicAlarmInfo(alarm);

                    if (args["name"] != null)
                        alarm.notificationName = Truncate(args["name"].ToString(), 30);
                    if (args["tooltip"] != null)
                        alarm.notificationTooltip = Truncate(args["tooltip"].ToString(), 90);
                    if (args["type"] != null)
                    {
                        NotificationType type;
                        if (!TryParseNotificationType(args["type"].ToString(), out type))
                            return CallToolResult.Error("type must be bad, neutral, or duplicant_threatening");
                        alarm.notificationType = type;
                    }
                    if (args["pauseOnNotify"] != null)
                        alarm.pauseOnNotify = ToolUtil.GetBool(args, "pauseOnNotify", alarm.pauseOnNotify);
                    if (args["zoomOnNotify"] != null)
                        alarm.zoomOnNotify = ToolUtil.GetBool(args, "zoomOnNotify", alarm.zoomOnNotify);
                    alarm.UpdateNotification(clear: true);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["alarm"] = LogicAlarmInfo(alarm)
                    });
                }
            };
        }

        public static McpTool ControlLogicAlarm()
        {
            return new McpTool
            {
                Name = "logic_alarm_control",
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "logic_alarm_side_control", "alarm_side_screen_control" },
                Tags = new List<string> { "automation", "logic", "alarm", "notification", "side-screen" },
                Description = "LogicAlarmSideScreen の通知設定の読み取りと設定を統合する。action=list/set。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list または set", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備、prefabId、警報名、説明、種類により絞り込む", Required = false },
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
                    ["name"] = new McpToolParameter { Type = "string", Description = "action=set の警報名。最大 30 文字。未指定なら変更しない", Required = false },
                    ["tooltip"] = new McpToolParameter { Type = "string", Description = "action=set の警報の説明。最大 90 文字。未指定なら変更しない", Required = false },
                    ["type"] = new McpToolParameter { Type = "string", Description = "action=set の通知の種類: bad、neutral、duplicant_threatening のいずれか。未指定なら変更しない", Required = false, EnumValues = new List<string> { "bad", "neutral", "duplicant_threatening" } },
                    ["pauseOnNotify"] = new McpToolParameter { Type = "boolean", Description = "action=set で、発動後に一時停止するか。未指定なら変更しない", Required = false },
                    ["zoomOnNotify"] = new McpToolParameter { Type = "boolean", Description = "action=set で、発動後にカメラを移動するか。未指定なら変更しない", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListLogicAlarms().Handler(args);
                    if (action == "set")
                        return SetLogicAlarm().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool ControlTurboHeater()
        {
            return new McpTool
            {
                Name = "turbo_heater_control",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "liquid_tepidizer_turbo_control", "turbo_mode_control" },
                Tags = new List<string> { "building", "heater", "liquid", "turbo", "side-screen" },
                Description = "Liquid Tepidizer のターボモードの集約ツール。action=list で状態を照会し、action=set でターボモードを切り替える",
                Parameters = TurboHeaterControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListTurboHeaters().Handler(args);
                    if (action == "set")
                        return SetTurboHeater().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool ListTurboHeaters()
        {
            return new McpTool
            {
                Name = "turbo_heaters_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "liquid_tepidizers_list", "turbo_mode_heaters_list" },
                Tags = new List<string> { "building", "heater", "liquid", "turbo", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=misc kind=turbo_heater action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備や prefabId で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args => ListTargets(args, IsTurboHeater, go => TurboHeaterInfo(go.GetComponent<SpaceHeater>()), "heaters")
            };
        }

        public static McpTool SetTurboHeater()
        {
            return new McpTool
            {
                Name = "turbo_heater_set",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "liquid_tepidizer_turbo_set", "turbo_mode_set" },
                Tags = new List<string> { "building", "heater", "liquid", "turbo", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=misc kind=turbo_heater action=set を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "true=最大消費電力、false=最小消費電力", Required = true }
                }),
                Handler = args =>
                {
                    var go = FindTarget(args, IsTurboHeater);
                    if (go == null)
                        return CallToolResult.Error("Target turbo-capable SpaceHeater not found");
                    var heater = go.GetComponent<SpaceHeater>();
                    bool enabled = ToolUtil.GetBool(args, "enabled", heater.UserSliderSetting > 0f);
                    var before = TurboHeaterInfo(heater);
                    heater.SetUserSpecifiedPowerConsumptionValue(enabled ? heater.maxPower : heater.minPower);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["heater"] = TurboHeaterInfo(heater)
                    });
                }
            };
        }

    }
}
