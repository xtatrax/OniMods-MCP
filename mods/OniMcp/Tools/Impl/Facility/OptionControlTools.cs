using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class OptionControlTools
    {
        public static McpTool ListOptionControls()
        {
            return new McpTool
            {
                Name = "side_options_list",
                Hidden = true,
                Group = "controls",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "building_option_controls_list", "direction_controls_list" },
                Tags = new List<string> { "controls", "side-screen", "direction", "few-option", "broadcast", "radbolt" },
                Description = "互換用の入口。building_control domain=side_surface surface=option action=list を優先して使う。slider/threshold 以外の選択式サイドパネルの操作項目を一覧表示する。作業方向、少数の選択肢、自動化信号の送信チャンネル、放射線粒子の方向が対象",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "絞り込む種類: any、direction、few_option、broadcast_receiver、radbolt_direction。既定値は any", Required = false, EnumValues = new List<string> { "any", "direction", "few_option", "broadcast_receiver", "radbolt_direction" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、選択肢の tag、送信機名で絞り込む", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "選択肢・利用可能な送信機を返すか。既定値は true", Required = false },
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
                    bool includeOptions = ToolUtil.GetBool(args, "includeOptions", true);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var results = new List<Dictionary<string, object>>();
                    foreach (var building in Components.BuildingCompletes.Items)
                    {
                        var go = building?.gameObject;
                        if (!MatchesTarget(go, rect, worldId))
                            continue;
                        var info = ControlInfo(go, includeOptions);
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

        public static McpTool SetDirection()
        {
            return new McpTool
            {
                Name = "direction_control_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "workable_direction_set" },
                Tags = new List<string> { "controls", "direction", "workable", "user-menu" },
                Description = "互換用の入口。building_control domain=side_surface surface=option action=set kind=direction を優先して使う",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["direction"] = new McpToolParameter { Type = "string", Description = "Any、Left、Right のいずれか", Required = true, EnumValues = new List<string> { "Any", "Left", "Right" } }
                }),
                Handler = args => SetOptionControl(args, "direction")
            };
        }

        public static McpTool SetOptionControl()
        {
            return new McpTool
            {
                Name = "side_option_set",
                Hidden = true,
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "option_control_set", "side_screen_option_control_set" },
                Tags = new List<string> { "controls", "side-screen", "direction", "few-option", "broadcast", "radbolt" },
                Description = "互換用の入口。building_control domain=side_surface surface=option action=set を優先して使う。kind 引数で direction、few_option、broadcast_receiver、radbolt_direction を選ぶ。",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "操作項目の種類: direction、few_option、broadcast_receiver、radbolt_direction", Required = true, EnumValues = new List<string> { "direction", "few_option", "broadcast_receiver", "radbolt_direction" } },
                    ["direction"] = new McpToolParameter { Type = "string", Description = "kind=direction では Any/Left/Right。kind=radbolt_direction では Up/UpLeft/Left/DownLeft/Down/DownRight/Right/UpRight", Required = false },
                    ["tag"] = new McpToolParameter { Type = "string", Description = "kind=few_option の対象の選択肢の tag。先に building_control domain=side_surface surface=option action=list kind=few_option で照会できる", Required = false },
                    ["broadcasterId"] = new McpToolParameter { Type = "integer", Description = "kind=broadcast_receiver の対象の LogicBroadcaster の InstanceID。clear=true では省略できる", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "kind=broadcast_receiver で現在のチャンネルを解除するか", Required = false }
                }),
                Handler = args => SetOptionControl(args, null)
            };
        }

        public static McpTool ControlSideOption()
        {
            return new McpTool
            {
                Name = "side_option_control",
                Hidden = true,
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "option_control", "side_screen_option_control" },
                Tags = new List<string> { "controls", "side-screen", "direction", "few-option", "broadcast", "radbolt" },
                Description = "選択式サイドパネルの集約ツール。action=list/set で作業方向、少数の選択肢、自動化信号の送信チャンネル、放射線粒子の方向を読み取り・設定する。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list または set", Required = true, EnumValues = new List<string> { "list", "set" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=set の対象のオブジェクトの InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=set の対象のセルの X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=set の対象のセルの Y", Required = false },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "action=list では any/direction/few_option/broadcast_receiver/radbolt_direction。action=set では direction/few_option/broadcast_receiver/radbolt_direction", Required = false, EnumValues = new List<string> { "any", "direction", "few_option", "broadcast_receiver", "radbolt_direction" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、選択肢の tag、送信機名により絞り込む", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "action=list で選択肢・利用可能な送信機を返すか。既定値は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                    ["direction"] = new McpToolParameter { Type = "string", Description = "action=set kind=direction では Any/Left/Right。kind=radbolt_direction では8方向", Required = false },
                    ["tag"] = new McpToolParameter { Type = "string", Description = "action=set kind=few_option の対象の選択肢の tag", Required = false },
                    ["broadcasterId"] = new McpToolParameter { Type = "integer", Description = "action=set kind=broadcast_receiver の対象の LogicBroadcaster の InstanceID。clear=true では省略できる", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "action=set kind=broadcast_receiver で現在のチャンネルを解除するか", Required = false }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListOptionControls().Handler(args);
                    if (action == "set")
                        return SetOptionControl().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool SetFewOption()
        {
            return new McpTool
            {
                Name = "few_option_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "side_screen_option_set" },
                Tags = new List<string> { "controls", "few-option", "side-screen", "mode" },
                Description = "互換用の入口。building_control domain=side_surface surface=option action=set kind=few_option を優先して使う",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["tag"] = new McpToolParameter { Type = "string", Description = "対象の選択肢の tag。先に building_control domain=side_surface surface=option action=list kind=few_option で照会できる", Required = true }
                }),
                Handler = args => SetOptionControl(args, "few_option")
            };
        }

        public static McpTool SetBroadcastChannel()
        {
            return new McpTool
            {
                Name = "logic_broadcast_channel_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "broadcast_receiver_set", "logic_receiver_channel_set" },
                Tags = new List<string> { "controls", "logic", "broadcast", "receiver", "automation" },
                Description = "互換用の入口。building_control domain=side_surface surface=option action=set kind=broadcast_receiver を優先して使う",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["broadcasterId"] = new McpToolParameter { Type = "integer", Description = "対象の LogicBroadcaster の InstanceID。clear=true では省略できる", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "現在のチャンネルを解除するか", Required = false }
                }),
                Handler = args => SetOptionControl(args, "broadcast_receiver")
            };
        }

        public static McpTool SetRadboltDirection()
        {
            return new McpTool
            {
                Name = "radbolt_direction_set",
                Group = "controls",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "hep_direction_set", "radiation_particle_direction_set" },
                Tags = new List<string> { "controls", "radbolt", "high-energy-particle", "direction" },
                Description = "互換用の入口。building_control domain=side_surface surface=option action=set kind=radbolt_direction を優先して使う",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["direction"] = new McpToolParameter { Type = "string", Description = "Up、UpLeft、Left、DownLeft、Down、DownRight、Right、UpRight", Required = true, EnumValues = new List<string> { "Up", "UpLeft", "Left", "DownLeft", "Down", "DownRight", "Right", "UpRight" } }
                }),
                Handler = args => SetOptionControl(args, "radbolt_direction")
            };
        }

    }
}
