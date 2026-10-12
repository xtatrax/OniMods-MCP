using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static class ConfigBatchTools
    {
        private static readonly HashSet<string> BuildingTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "building_config_control",
            "buildings_set_enabled",
            "buildings_set_toggle",
            "buildings_slider_set",
            "buildings_threshold_set",
            "side_option_control",
            "side_option_set",
            "direction_control_set",
            "few_option_set",
            "radbolt_direction_set",
            "state_control",
            "state_controls_list",
            "state_control_set",
            "capacity_control_set",
            "checkbox_control_set",
            "light_control",
            "lights_color_set",
            "pixel_pack_control",
            "pixel_pack_color_set",
            "pixel_pack_colors_copy",
            "doors_set_state",
            "access_control_set",
            "buildings_manual_delivery",
            "storage_system_control",
            "storage_control",
            "resources_storage_set_filter",
            "filter_control",
            "filter_set",
            "filters_single_set",
            "filters_tree_set",
            "storage_tile_selection_set",
            "receptacle_control",
            "automation_side_control",
            "user_menu_action_press",
            "maintenance_action_execute",
            "mutant_seed_control",
            "mutant_seed_control_set",
            "rocket_usage_control",
            "rocket_usage_control_set",
            "automatable_control_set",
            "valves_flow_set",
            "limit_valves_set",
            "logic_timer_set",
            "logic_ribbon_bit_set",
            "buildings_copy_settings"
        };

        private static readonly HashSet<string> AutomationTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "buildings_set_toggle",
            "buildings_threshold_set",
            "storage_system_control",
            "filter_control",
            "filter_set",
            "filters_single_set",
            "side_option_control",
            "side_option_set",
            "direction_control_set",
            "few_option_set",
            "logic_broadcast_channel_set",
            "state_control",
            "state_control_set",
            "logic_counter_set",
            "automation_side_control",
            "critter_sensor_counting_set",
            "time_range_set",
            "logic_timer_set",
            "logic_ribbon_bit_set",
            "logic_alarm_control",
            "logic_alarm_set",
            "cluster_location_sensor_set",
            "comet_detector_control",
            "comet_detector_target_set",
            "automatable_control_set",
            "buildings_slider_set",
            "valves_flow_set",
            "limit_valves_set",
            "doors_set_state"
        };

        public static McpTool BatchSetBuildingConfigs()
        {
            return BuildBatchTool(
                "buildings_config_batch_set",
                "buildings",
                "設備設定用 MCP ツールを一括呼び出し。items は {tool,args} または短縮フィールド {t,a} に対応。設備のスイッチ・slider・フィルター・通行権限・設定コピーなどの設定ツールのみ許可",
                BuildingTools,
                new List<string> { "building_settings_batch", "batch_building_configure" },
                new List<string> { "buildings", "config", "batch", "settings", "side-screen" });
        }

        public static McpTool BatchSetAutomationControls()
        {
            return BuildBatchTool(
                "automation_controls_batch_set",
                "automation",
                "自動化・電力制御用 MCP ツールを一括呼び出し。items は {tool,args} または短縮フィールド {t,a} に対応。スイッチ、閾値、センサー、タイマー、バルブ、自動化警報などの制御ツールのみ許可",
                AutomationTools,
                new List<string> { "logic_controls_batch_set", "batch_automation_configure" },
                new List<string> { "automation", "logic", "power", "batch", "controls" });
        }

        private static McpTool BuildBatchTool(string name, string group, string description, HashSet<string> allowedTools, List<string> aliases, List<string> tags)
        {
            return new McpTool
            {
                Name = name,
                Group = group,
                Mode = "execute",
                Risk = "dangerous",
                Aliases = aliases,
                Tags = tags,
                Description = $"互換用の入口。building_control domain=config action={(name == "automation_controls_batch_set" ? "batch_set_automation" : "batch_set")} を使用してください。{description}",
                Hidden = true,
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "バッチ操作の配列。各項目の tool/t はツール名、args/a はそのツールの引数オブジェクト。最大 50 項目", Required = true },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "各サブツールの args/a に結合する既定の引数。個別項目の引数を優先。confirm/worldId/閾値などの共有向け", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["stopOnError"] = new McpToolParameter { Type = "boolean", Description = "エラー発生時に以後の呼び出しを停止するか。既定は false", Required = false },
                    ["summaryOnly"] = new McpToolParameter { Type = "boolean", Description = "各項目の戻り値テキストを切り詰めるか。既定は true", Required = false },
                    ["maxTextChars"] = new McpToolParameter { Type = "integer", Description = "summaryOnly=true の場合の各項目の最大文字数。既定は 500、最大 4000", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "設定の一括変更の確認。true が必須", Required = true }
                },
                Handler = args => ExecuteBatch(args, allowedTools)
            };
        }

        private static CallToolResult ExecuteBatch(JObject args, HashSet<string> allowedTools)
        {
            if (!ToolUtil.GetBool(args, "confirm", false))
                return CallToolResult.Error("confirm=true is required for config batch operations");
            var items = args["items"] as JArray;
            if (items == null || items.Count == 0)
                return CallToolResult.Error("items array is required");
            if (items.Count > 50)
                return CallToolResult.Error("items cannot contain more than 50 operations");

            bool stopOnError = ToolUtil.GetBool(args, "stopOnError", false);
            bool summaryOnly = ToolUtil.GetBool(args, "summaryOnly", true);
            int maxTextChars = Math.Max(80, Math.Min(ToolUtil.GetInt(args, "maxTextChars") ?? 500, 4000));
            var defaults = args["defaults"] as JObject ?? args["defaultArguments"] as JObject;
            var results = new List<Dictionary<string, object>>();
            int succeeded = 0;
            int failed = 0;
            bool stopped = false;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i] as JObject;
                if (item == null)
                {
                    failed++;
                    results.Add(ErrorResult(i, null, "item must be an object"));
                    if (stopOnError)
                    {
                        stopped = i < items.Count - 1;
                        break;
                    }
                    continue;
                }

                string requestedTool = item["tool"]?.ToString() ?? item["t"]?.ToString();
                var toolArgsToken = item["args"] ?? item["a"];
                var toolArgs = MergeDefaults(toolArgsToken as JObject, defaults);
                string tool = NormalizeBatchTool(requestedTool, toolArgs);
                if (string.IsNullOrWhiteSpace(tool) || !allowedTools.Contains(tool))
                {
                    failed++;
                    results.Add(ErrorResult(i, requestedTool, "tool is not allowed for this batch surface"));
                    if (stopOnError)
                    {
                        stopped = i < items.Count - 1;
                        break;
                    }
                    continue;
                }

                var result = OniToolRegistry.CallTool(tool, toolArgs);
                bool isError = result.IsError;
                if (isError)
                    failed++;
                else
                    succeeded++;

                results.Add(new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["tool"] = tool,
                    ["requestedTool"] = requestedTool?.Trim(),
                    ["isError"] = isError,
                    ["text"] = ResultText(result, summaryOnly, maxTextChars)
                });

                if (isError && stopOnError)
                {
                    stopped = i < items.Count - 1;
                    break;
                }
            }

            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["requested"] = items.Count,
                ["executed"] = results.Count,
                ["succeeded"] = succeeded,
                ["failed"] = failed,
                ["stopped"] = stopped,
                ["results"] = results
            }, McpJsonUtil.Settings));
        }

        private static string NormalizeBatchTool(string tool, JObject args)
        {
            string normalized = tool?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return normalized;

            if (normalized.Equals("buildings_set_enabled", StringComparison.OrdinalIgnoreCase))
            {
                SetDefaultAction(args, "set_enabled");
                return "building_config_control";
            }

            if (normalized.Equals("buildings_set_toggle", StringComparison.OrdinalIgnoreCase))
            {
                SetDefaultAction(args, "set_toggle");
                return "building_config_control";
            }

            if (normalized.Equals("lights_color_set", StringComparison.OrdinalIgnoreCase))
            {
                SetDefaultAction(args, "set_color");
                return "light_control";
            }

            if (normalized.Equals("comet_detector_target_set", StringComparison.OrdinalIgnoreCase))
            {
                SetDefaultAction(args, "set_target");
                return "comet_detector_control";
            }

            return normalized;
        }

        private static void SetDefaultAction(JObject args, string action)
        {
            if (args != null && args["action"] == null)
                args["action"] = action;
        }

        private static Dictionary<string, object> ErrorResult(int index, string tool, string reason)
        {
            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["tool"] = tool,
                ["isError"] = true,
                ["text"] = reason
            };
        }

        private static JObject MergeDefaults(JObject args, JObject defaults)
        {
            var result = defaults == null ? new JObject() : (JObject)defaults.DeepClone();
            if (args == null)
                return result;

            foreach (var property in args.Properties())
                result[property.Name] = property.Value.DeepClone();
            return result;
        }

        private static string ResultText(CallToolResult result, bool summaryOnly, int maxTextChars)
        {
            string text = result.Content == null || result.Content.Count == 0 ? "" : result.Content[0].Text ?? "";
            if (summaryOnly && text.Length > maxTextChars)
                return text.Substring(0, maxTextChars) + "...";
            return text;
        }
    }
}
