using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class MiscSideScreenTools
    {
        private const int RocketSelfDestructEvent = -1061799784;

        public static McpTool ListConfigurableConsumers()
        {
            return new McpTool
            {
                Name = "configurable_consumers_list",
                Hidden = true,
                Group = "production",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "spice_grinders_list", "consumer_options_list" },
                Tags = new List<string> { "production", "side-screen", "configurable-consumer", "spice" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=misc kind=configurable_consumer action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備、prefabId、選択肢の名前、optionId で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args => ListTargets(args, go => go.GetComponent<IConfigurableConsumer>() != null, go => ConfigurableConsumerInfo(go), "consumers")
            };
        }

        public static McpTool SetConfigurableConsumerOption()
        {
            return new McpTool
            {
                Name = "configurable_consumer_option_set",
                Hidden = true,
                Group = "production",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "spice_grinder_option_set", "consumer_option_set" },
                Tags = new List<string> { "production", "side-screen", "configurable-consumer", "spice" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=misc kind=configurable_consumer action=set_option を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["optionIndex"] = new McpToolParameter { Type = "integer", Description = "GetSettingOptions() の選択肢の番号", Required = false },
                    ["optionId"] = new McpToolParameter { Type = "string", Description = "IConfigurableConsumerOption.GetID().Name", Required = false },
                    ["optionName"] = new McpToolParameter { Type = "string", Description = "表示名。大文字・小文字を区別しない", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindTarget(args, target => target.GetComponent<IConfigurableConsumer>() != null);
                    if (go == null)
                        return CallToolResult.Error("Target configurable consumer not found");
                    var consumer = go.GetComponent<IConfigurableConsumer>();
                    var options = consumer.GetSettingOptions() ?? new IConfigurableConsumerOption[0];
                    var option = FindConsumerOption(args, options);
                    if (option == null)
                        return CallToolResult.Error("optionIndex, optionId, or optionName must match an available option");

                    var before = ConfigurableConsumerInfo(go);
                    consumer.SetSelectedOption(option);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["consumer"] = ConfigurableConsumerInfo(go)
                    });
                }
            };
        }

        public static McpTool ControlConfigurableConsumer()
        {
            return new McpTool
            {
                Name = "configurable_consumer_control",
                Group = "production",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "spice_grinder_control", "consumer_option_control" },
                Tags = new List<string> { "production", "side-screen", "configurable-consumer", "spice" },
                Description = "消費対象を設定できる設備の集約ツール。action=list で選択肢を持つ消費設備を照会し、action=set_option で現在の選択肢を設定する",
                Parameters = ConfigurableConsumerControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListConfigurableConsumers().Handler(args);
                    if (action == "set_option" || action == "set")
                        return SetConfigurableConsumerOption().Handler(args);
                    return CallToolResult.Error("action must be list or set_option");
                }
            };
        }

        public static McpTool ControlMiscSideScreen()
        {
            return new McpTool
            {
                Name = "misc_sidescreen_control",
                Hidden = true,
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "misc_side_screen_control", "small_sidescreen_control" },
                Tags = new List<string> { "controls", "side-screen", "toggle", "logic", "alarm", "turbo", "configurable-consumer" },
                Description = "小規模なサイドパネル操作の統合エントリーポイント。kind=n_toggle/logic_alarm/turbo_heater/configurable_consumer。action を対応する旧 control へそのまま渡す。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "サイドパネルの操作項目の種類: n_toggle、logic_alarm、turbo_heater、configurable_consumer", Required = true, EnumValues = new List<string> { "n_toggle", "logic_alarm", "turbo_heater", "configurable_consumer" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作。通常は list/set。configurable_consumer は set_option に対応する", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備、prefabId、見出し、選択肢、警報情報により絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "action=list の領域ハンドル。x1/y1/x2/y2 とのどちらかを指定する", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の始点の Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "action=list の絞り込み矩形の終点の Y", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=set/set_option の対象の KPrefabID.InstanceID。指定を推奨する", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=set/set_option の対象のセルの X。id が未指定の場合に使う", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=set/set_option の対象のセルの Y。id が未指定の場合に使う", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。list では絞り込みに使え、set で座標から探す場合は指定を推奨する", Required = false },
                    ["optionIndex"] = new McpToolParameter { Type = "integer", Description = "n_toggle/configurable_consumer の対象の選択肢の番号", Required = false },
                    ["optionId"] = new McpToolParameter { Type = "string", Description = "configurable_consumer の選択肢の ID", Required = false },
                    ["optionName"] = new McpToolParameter { Type = "string", Description = "n_toggle/configurable_consumer の選択肢の表示名", Required = false },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "turbo_heater action=set では true=有効、false=無効", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "logic_alarm action=set の警報名。最大 30 文字", Required = false },
                    ["tooltip"] = new McpToolParameter { Type = "string", Description = "logic_alarm action=set の警報の説明。最大 90 文字", Required = false },
                    ["type"] = new McpToolParameter { Type = "string", Description = "logic_alarm action=set の通知の種類: bad、neutral、duplicant_threatening のいずれか", Required = false, EnumValues = new List<string> { "bad", "neutral", "duplicant_threatening" } },
                    ["pauseOnNotify"] = new McpToolParameter { Type = "boolean", Description = "logic_alarm action=set で、発動後に一時停止するか", Required = false },
                    ["zoomOnNotify"] = new McpToolParameter { Type = "boolean", Description = "logic_alarm action=set で、発動後にカメラを移動するか", Required = false }
                },
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (kind)
                    {
                        case "n_toggle":
                        case "multi_toggle":
                            return ControlNToggle().Handler(args);
                        case "logic_alarm":
                        case "alarm":
                            return ControlLogicAlarm().Handler(args);
                        case "turbo_heater":
                        case "turbo":
                            return ControlTurboHeater().Handler(args);
                        case "configurable_consumer":
                        case "consumer":
                            return ControlConfigurableConsumer().Handler(args);
                        default:
                            return CallToolResult.Error("kind must be n_toggle, logic_alarm, turbo_heater, or configurable_consumer");
                    }
                }
            };
        }

        public static McpTool ControlNToggle()
        {
            return new McpTool
            {
                Name = "n_toggle_control",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "multi_toggle_control", "n_toggle_option_control" },
                Tags = new List<string> { "controls", "side-screen", "toggle", "multi-option" },
                Description = "複数選択サイドパネルの集約ツール。action=list で INToggleSideScreenControl を照会し、action=set で選択肢の変更をキューに入れる",
                Parameters = NToggleControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListNToggles().Handler(args);
                    if (action == "set")
                        return SetNToggle().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool ListNToggles()
        {
            return new McpTool
            {
                Name = "n_toggles_list",
                Hidden = true,
                Group = "controls",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "multi_toggle_controls_list", "n_toggle_controls_list" },
                Tags = new List<string> { "controls", "side-screen", "toggle", "multi-option" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=misc kind=n_toggle action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備、prefabId、見出し、選択肢で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args => ListTargets(args, go => go.GetComponent<INToggleSideScreenControl>() != null, go => NToggleInfo(go), "toggles")
            };
        }

        public static McpTool SetNToggle()
        {
            return new McpTool
            {
                Name = "n_toggle_set",
                Hidden = true,
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "multi_toggle_set", "n_toggle_option_set" },
                Tags = new List<string> { "controls", "side-screen", "toggle", "multi-option" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=misc kind=n_toggle action=set を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["optionIndex"] = new McpToolParameter { Type = "integer", Description = "対象の選択肢の番号", Required = false },
                    ["optionName"] = new McpToolParameter { Type = "string", Description = "対象の選択肢の表示テキスト。大文字・小文字を区別しない", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindTarget(args, target => target.GetComponent<INToggleSideScreenControl>() != null);
                    if (go == null)
                        return CallToolResult.Error("Target INToggleSideScreenControl not found");
                    var toggle = go.GetComponent<INToggleSideScreenControl>();
                    int index = FindToggleOption(args, toggle);
                    if (index < 0 || index >= toggle.Options.Count)
                        return CallToolResult.Error("optionIndex or optionName must match an available option");

                    var before = NToggleInfo(go);
                    toggle.QueueSelectedOption(index);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["toggle"] = NToggleInfo(go)
                    });
                }
            };
        }

    }
}
