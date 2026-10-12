using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class StateControlTools
    {
        public static McpTool ListStateControls()
        {
            return new McpTool
            {
                Name = "state_controls_list",
                Hidden = true,
                Group = "controls",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "numeric_checkbox_controls_list", "capacity_counter_controls_list" },
                Tags = new List<string> { "controls", "side-screen", "capacity", "checkbox", "counter", "time-range" },
                Description = "互換用の入口。game_control domain=state action=list を優先して使う。容量上限、単一の checkbox、カウンター、時間範囲センサーなど、数値・状態を扱うサイドパネルの操作項目を一覧表示する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "絞り込む種類: any、capacity、checkbox、counter、time_range。既定値は any", Required = false, EnumValues = new List<string> { "any", "capacity", "checkbox", "counter", "time_range" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、checkbox のテキスト、単位で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string kind = (args["kind"]?.ToString() ?? "any").Trim().ToLowerInvariant();
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var results = new List<Dictionary<string, object>>();
                    foreach (var building in Components.BuildingCompletes.Items)
                    {
                        var go = building?.gameObject;
                        if (!MatchesTarget(go, rect, worldId))
                            continue;

                        var info = ControlInfo(go);
                        var kinds = (List<string>)info["controlKinds"];
                        if (kinds.Count == 0)
                            continue;
                        if (kind != "any" && !kinds.Contains(kind))
                            continue;
                        if (!MatchesQuery(info, query))
                            continue;

                        results.Add(info);
                        if (results.Count >= limit)
                            break;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = results.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["kind"] = kind,
                        ["controls"] = results
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetCapacity()
        {
            return new McpTool
            {
                Name = "capacity_control_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "user_capacity_set", "storage_capacity_set" },
                Tags = new List<string> { "controls", "capacity", "side-screen", "storage" },
                Description = "互換用の入口。game_control domain=state action=set kind=capacity を優先して使う",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["capacity"] = new McpToolParameter { Type = "number", Description = "設定する容量。対象の min/max の範囲に収める。WholeValues の対象では整数に丸める", Required = true }
                }),
                Handler = args => SetStateControl(args, "capacity")
            };
        }

        public static McpTool SetStateControl()
        {
            return new McpTool
            {
                Name = "state_control_set",
                Hidden = true,
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "state_side_control_set", "numeric_checkbox_control_set", "capacity_counter_control_set" },
                Tags = new List<string> { "controls", "side-screen", "capacity", "checkbox", "counter", "time-range" },
                Description = "互換用の入口。game_control domain=state action=set を優先して使う。kind 引数で capacity、checkbox、counter、time_range を選ぶ。",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "操作項目の種類: capacity、checkbox、counter、time_range", Required = true, EnumValues = new List<string> { "capacity", "checkbox", "counter", "time_range" } },
                    ["capacity"] = new McpToolParameter { Type = "number", Description = "kind=capacity で設定する容量。対象の min/max の範囲に収める。WholeValues の対象では整数に丸める", Required = false },
                    ["value"] = new McpToolParameter { Type = "boolean", Description = "kind=checkbox で設定する値", Required = false },
                    ["maxCount"] = new McpToolParameter { Type = "integer", Description = "kind=counter で設定する最大カウント数 1-10。未指定なら変更しない", Required = false },
                    ["advancedMode"] = new McpToolParameter { Type = "boolean", Description = "kind=counter で高度なモードを有効にするか。未指定なら変更しない", Required = false },
                    ["reset"] = new McpToolParameter { Type = "boolean", Description = "kind=counter で Reset Counter を実行するか。既定値は false", Required = false },
                    ["start"] = new McpToolParameter { Type = "number", Description = "kind=time_range の開始時刻。サイクル内の割合 0-1 で指定する。未指定なら変更しない", Required = false },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "kind=time_range の継続時間。サイクル内の割合 0-1 で指定する。未指定なら変更しない", Required = false }
                }),
                Handler = args => SetStateControl(args, null)
            };
        }

        public static McpTool ControlState()
        {
            return new McpTool
            {
                Name = "state_control",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "state_side_control", "numeric_checkbox_control", "capacity_counter_control" },
                Tags = new List<string> { "controls", "side-screen", "capacity", "checkbox", "counter", "time-range" },
                Description = "数値・状態を扱うサイドパネルの集約ツール。action=list/set で容量上限、checkbox、カウンター、時間範囲センサーを読み取り・設定する。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list または set", Required = true, EnumValues = new List<string> { "list", "set" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=set の対象のオブジェクトの InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=set の対象のセルの X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=set の対象のセルの Y", Required = false },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "action=list では any/capacity/checkbox/counter/time_range。action=set では capacity/checkbox/counter/time_range", Required = false, EnumValues = new List<string> { "any", "capacity", "checkbox", "counter", "time_range" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、checkbox のテキスト、単位により絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                    ["capacity"] = new McpToolParameter { Type = "number", Description = "action=set kind=capacity で設定する容量", Required = false },
                    ["value"] = new McpToolParameter { Type = "boolean", Description = "action=set kind=checkbox で設定する値", Required = false },
                    ["maxCount"] = new McpToolParameter { Type = "integer", Description = "action=set kind=counter で設定する最大カウント数 1-10。未指定なら変更しない", Required = false },
                    ["advancedMode"] = new McpToolParameter { Type = "boolean", Description = "action=set kind=counter で高度なモードを有効にするか。未指定なら変更しない", Required = false },
                    ["reset"] = new McpToolParameter { Type = "boolean", Description = "action=set kind=counter で Reset Counter を実行するか。既定値は false", Required = false },
                    ["start"] = new McpToolParameter { Type = "number", Description = "action=set kind=time_range の開始時刻。サイクル内の割合 0-1 で指定する。未指定なら変更しない", Required = false },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "action=set kind=time_range の継続時間。サイクル内の割合 0-1 で指定する。未指定なら変更しない", Required = false }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListStateControls().Handler(args);
                    if (action == "set")
                        return SetStateControl().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool SetCheckbox()
        {
            return new McpTool
            {
                Name = "checkbox_control_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "single_checkbox_set" },
                Tags = new List<string> { "controls", "checkbox", "side-screen", "toggle" },
                Description = "互換用の入口。game_control domain=state action=set kind=checkbox を優先して使う",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["value"] = new McpToolParameter { Type = "boolean", Description = "checkbox に設定する値", Required = true }
                }),
                Handler = args => SetStateControl(args, "checkbox")
            };
        }

        public static McpTool SetCounter()
        {
            return new McpTool
            {
                Name = "logic_counter_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "counter_control_set" },
                Tags = new List<string> { "controls", "logic", "counter", "automation" },
                Description = "互換用の入口。game_control domain=state action=set kind=counter を優先して使う",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["maxCount"] = new McpToolParameter { Type = "integer", Description = "設定する最大カウント数 1-10。未指定なら変更しない", Required = false },
                    ["advancedMode"] = new McpToolParameter { Type = "boolean", Description = "高度なモードを有効にするか。未指定なら変更しない", Required = false },
                    ["reset"] = new McpToolParameter { Type = "boolean", Description = "Reset Counter を実行するか。既定値は false", Required = false }
                }),
                Handler = args => SetStateControl(args, "counter")
            };
        }

        public static McpTool SetTimeRange()
        {
            return new McpTool
            {
                Name = "time_range_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "time_of_day_sensor_set" },
                Tags = new List<string> { "controls", "logic", "time-range", "automation" },
                Description = "互換用の入口。game_control domain=state action=set kind=time_range を優先して使う",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["start"] = new McpToolParameter { Type = "number", Description = "開始時刻。サイクル内の割合 0-1 で指定する。未指定なら変更しない", Required = false },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "継続時間。サイクル内の割合 0-1 で指定する。未指定なら変更しない", Required = false }
                }),
                Handler = args => SetStateControl(args, "time_range")
            };
        }

    }
}
