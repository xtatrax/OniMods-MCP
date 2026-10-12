using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class SandboxTools
    {
        private const int MaxSandboxCells = 1000;

        public static McpTool ControlSandbox()
        {
            return new McpTool
            {
                Name = "sandbox_control",
                Group = "sandbox",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "sandbox", "sandbox_debug_control", "map_edit_control" },
                Tags = new List<string> { "sandbox", "debug", "map", "designate", "area", "entity", "search" },
                Description = "サンドボックスとマップ編集の統合エントリー。kind=read/area/entity/map_designate。マップ編集では search/designate のテキスト断片で位置を自動特定し、座標オフセットの手計算を不要にする。危険な書き込み操作では confirm/force/サンドボックスモードの検証を維持する。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "read、area、entity または map_designate。既定値 read", Required = false, EnumValues = new List<string> { "read", "area", "entity", "map_designate" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "kind=read: list_actions/sample_cell/list_story_traits；kind=area: paint/flood_fill/temperature/reveal/clear_floor/clear_critters/destroy/stress；kind=entity: spawn_entity/story_trait_stamp/auto_plumb_building/set_building_enabled_debug", Required = false },
                    ["search"] = new McpToolParameter { Type = "string", Description = "kind=map_designate の場合に検索するテキストマップ断片", Required = false },
                    ["designate"] = new McpToolParameter { Type = "string", Description = "kind=map_designate の場合の指定用断片。_、same、keep は元のセルを保持する", Required = false },
                    ["replace"] = new McpToolParameter { Type = "string", Description = "旧パラメーターとの互換用。designate を使用すること", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "始点または対象セルの X。action に応じて解釈する", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "始点または対象セルの Y。action に応じて解釈する", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "矩形または検索範囲の左下 X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "矩形または検索範囲の左下 Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "矩形または検索範囲の右上 X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "矩形または検索範囲の右上 Y", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の範囲ハンドル", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールド ID。既定値は現在のアクティブワールド", Required = false },
                    ["element"] = new McpToolParameter { Type = "string", Description = "paint/flood_fill または map_designate の既定の元素", Required = false },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "set_building_enabled_debug 専用。デュプ作業を省略して直接変更するデバッグ値", Required = false },
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "kind=entity action=spawn_entity の場合の Prefab ID", Required = false },
                    ["storyId"] = new McpToolParameter { Type = "string", Description = "kind=entity action=story_trait_stamp の場合のストーリー ID", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象オブジェクトの InstanceID。action に応じて解釈する", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "kind=read action=list_story_traits の場合の検索語", Required = false },
                    ["massKg"] = new McpToolParameter { Type = "number", Description = "元素の質量 kg。action に応じて解釈する", Required = false },
                    ["temperatureK"] = new McpToolParameter { Type = "number", Description = "温度 K。action に応じて解釈する", Required = false },
                    ["disease"] = new McpToolParameter { Type = "string", Description = "病原菌 ID。既定値はなし", Required = false },
                    ["diseaseCount"] = new McpToolParameter { Type = "integer", Description = "セルごとの病原菌数。既定値 0", Required = false },
                    ["matchMode"] = new McpToolParameter { Type = "string", Description = "kind=map_designate の一致処理: unique/first/all。既定値 unique", Required = false, EnumValues = new List<string> { "unique", "first", "all" } },
                    ["matchIndex"] = new McpToolParameter { Type = "integer", Description = "kind=map_designate で複数の一致がある場合に選択するインデックス。0 始まり", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "範囲または検索の安全上限", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "kind=map_designate でプレビューのみ行い、変更しない。既定値 true", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=map_designate の検索で未発見セルを unk とみなすか。既定値 false", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "対応する下位ツールのサンドボックスモードまたは InstantBuild の要件を回避することを許可する。既定値 false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な書き込み操作の確認", Required = false }
                },
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "read").Trim().ToLowerInvariant();
                    switch (kind)
                    {
                        case "read":
                        case "info":
                            return ReadControl().Handler(args);
                        case "area":
                            return AreaControl().Handler(args);
                        case "entity":
                        case "entities":
                            return EntityControl().Handler(args);
                        case "map_designate":
                        case "designate":
                        case "search_designate":
                        case "map":
                            return ReplaceMapPattern().Handler(args);
                        default:
                            return CallToolResult.Error("kind must be read, area, entity, or map_designate");
                    }
                }
            };
        }

        public static McpTool ListSandboxActions()
        {
            return new McpTool
            {
                Name = "sandbox_actions_list",
                Group = "sandbox",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "sandbox_tools_list", "debug_actions_list" },
                Tags = new List<string> { "sandbox", "debug", "actions", "tools" },
                Description = "互換用エントリー。MCP が公開するサンドボックス/Debug 操作、リスク、現在のサンドボックスモードの状態を一覧表示する。新規の呼び出しでは game_control domain=sandbox kind=read action=list_actions を使用すること。",
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args =>
                {
                    var actions = new List<Dictionary<string, object>>
                    {
                        ActionInfo("game_control", "read", "none", "kind=read action=sample_cell: セルの元素、質量、温度、病原菌を参照し、paint/flood にそのまま使用できるパラメーターを返す。"),
                        ActionInfo("game_control", "execute", "dangerous", "kind=map_designate: search/designate のテキストマップ断片で検索してセルの元素を指定し、座標オフセットの手計算を不要にする。"),
                        ActionInfo("game_control", "execute", "dangerous", "kind=area：action=paint/flood_fill/temperature/reveal/clear_floor/clear_critters/destroy/stress。"),
                        ActionInfo("game_control", "execute", "dangerous", "kind=entity: action=spawn_entity/story_trait_stamp/auto_plumb_building/set_building_enabled_debug。auto_plumb_building では plumbAction=auto_plumb/power/pipes/solids/spawn_minion を使用する。"),
                        ActionInfo("game_control", "read", "none", "kind=read action=list_story_traits: サンドボックスの Story Trait Tool で配置できるストーリー特性のテンプレートを一覧表示する。"),
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["sandboxModeActive"] = Game.Instance?.SandboxModeActive ?? false,
                        ["maxAreaCells"] = MaxSandboxCells,
                        ["actions"] = actions,
                        ["safety"] = "Dangerous sandbox write operations require confirm=true and sandbox mode unless force=true."
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool AreaControl()
        {
            return new McpTool
            {
                Name = "sandbox_area_control",
                Group = "sandbox",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "sandbox_area", "debug_sandbox_area_control" },
                Tags = new List<string> { "sandbox", "area", "paint", "flood", "temperature", "reveal", "destroy", "stress" },
                Description = "サンドボックスの範囲操作を統合する。action=paint/flood_fill/temperature/reveal/clear_floor/clear_critters/destroy/stress。危険な書き込み操作では、既存の handler が引き続き confirm、force、サンドボックスモードを検証する。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "paint、flood_fill、temperature、reveal、clear_floor、clear_critters、destroy、stress", Required = true, EnumValues = new List<string> { "paint", "flood_fill", "temperature", "reveal", "clear_floor", "clear_critters", "destroy", "stress" } },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=flood_fill の場合の始点 X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=flood_fill の場合の始点 Y", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=stress の場合の複製人間 InstanceID。指定時は範囲を省略できる", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "action=stress の場合の複製人間の名前。指定時は範囲を省略できる", Required = false },
                    ["element"] = new McpToolParameter { Type = "string", Description = "action=paint/flood_fill の場合の対象元素 ID", Required = false },
                    ["massKg"] = new McpToolParameter { Type = "number", Description = "action=paint/flood_fill の場合のセルごとの質量 kg", Required = false },
                    ["temperatureK"] = new McpToolParameter { Type = "number", Description = "action=paint/flood_fill/temperature の場合の温度 K", Required = false },
                    ["disease"] = new McpToolParameter { Type = "string", Description = "action=paint/flood_fill の場合の病原菌 ID", Required = false },
                    ["diseaseCount"] = new McpToolParameter { Type = "integer", Description = "action=paint/flood_fill の場合のセルごとの病原菌数", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "action=flood_fill の場合の安全上限。既定値/最大値 1000", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "action=temperature の場合は set/add。既定値 set", Required = false, EnumValues = new List<string> { "set", "add" } },
                    ["deltaK"] = new McpToolParameter { Type = "number", Description = "action=temperature かつ mode=add の場合の温度増分 K", Required = false },
                    ["delta"] = new McpToolParameter { Type = "number", Description = "action=stress の場合のストレス変化量", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "サンドボックスモード以外での実行を許可する。既定値 false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。true が必須", Required = true }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "paint":
                            return PaintElement().Handler(args);
                        case "flood_fill":
                            return FloodFillElement().Handler(args);
                        case "temperature":
                            return SetTemperatureArea().Handler(args);
                        case "reveal":
                            return RevealArea().Handler(args);
                        case "clear_floor":
                            return ClearFloorArea().Handler(args);
                        case "clear_critters":
                            return ClearCrittersArea().Handler(args);
                        case "destroy":
                            return DestroyArea().Handler(args);
                        case "stress":
                            return StressArea().Handler(args);
                        default:
                            return CallToolResult.Error("action must be paint, flood_fill, temperature, reveal, clear_floor, clear_critters, destroy, or stress");
                    }
                }
            };
        }

        public static McpTool ReadControl()
        {
            return new McpTool
            {
                Name = "sandbox_read_control",
                Group = "sandbox",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "sandbox_info", "sandbox_read" },
                Tags = new List<string> { "sandbox", "debug", "actions", "sample", "story", "traits" },
                Description = "サンドボックス情報の参照を統合する。action=list_actions/sample_cell/list_story_traits。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list_actions、sample_cell、list_story_traits。既定値 list_actions", Required = false, EnumValues = new List<string> { "list_actions", "sample_cell", "list_story_traits" } },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=sample_cell の場合の対象セル X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=sample_cell の場合の対象セル Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "action=sample_cell の場合の対象ワールド ID。既定値は現在のアクティブワールド", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list_story_traits の場合、storyId、trait 名、テンプレート ID で検索する", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "list_actions").Trim().ToLowerInvariant();
                    if (action == "list_actions")
                        return ListSandboxActions().Handler(args);
                    if (action == "sample_cell")
                        return SampleCell().Handler(args);
                    if (action == "list_story_traits")
                        return ListStoryTraits().Handler(args);
                    return CallToolResult.Error("action must be list_actions, sample_cell, or list_story_traits");
                }
            };
        }

        public static McpTool EntityControl()
        {
            return new McpTool
            {
                Name = "sandbox_entity_control",
                Group = "sandbox",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "sandbox_entities", "sandbox_debug_entity_control" },
                Tags = new List<string> { "sandbox", "entity", "spawn", "story", "trait", "auto-plumber", "debug" },
                Description = "エンティティ、ストーリー、Debug 設備の操作を統合する。action=spawn_entity/story_trait_stamp/auto_plumb_building/set_building_enabled_debug。auto_plumb_building では plumbAction=auto_plumb/power/pipes/solids/spawn_minion を使用し、元の confirm、Sandbox/InstantBuild/force の検証を維持する。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "spawn_entity、story_trait_stamp または auto_plumb_building", Required = true, EnumValues = new List<string> { "spawn_entity", "story_trait_stamp", "auto_plumb_building", "set_building_enabled_debug" } },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "set_building_enabled_debug 専用。デュプ作業を省略して直接変更するデバッグ値", Required = false },
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "action=spawn_entity の場合の Prefab ID。例: Hatch、BasicPlantBar、Tile、OxygenDiffuser", Required = false },
                    ["storyId"] = new McpToolParameter { Type = "string", Description = "action=story_trait_stamp の場合のストーリー ID。例: MegaBrainTank、CreatureManipulator、LonelyMinion", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=auto_plumb_building の場合の対象設備 InstanceID。spawn_minion もこの設備で位置を特定する", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象セル X。spawn_entity/story_trait_stamp では必須。auto_plumb_building では座標で設備を検索できる", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象セル Y。spawn_entity/story_trait_stamp では必須。auto_plumb_building では座標で設備を検索できる", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールド ID。既定値は現在のアクティブワールド", Required = false },
                    ["massKg"] = new McpToolParameter { Type = "number", Description = "action=spawn_entity で ElementChunk を生成する場合の質量 kg。既定値 100", Required = false },
                    ["temperatureK"] = new McpToolParameter { Type = "number", Description = "action=spawn_entity で ElementChunk を生成する場合の温度 K。既定値は元素の既定温度", Required = false },
                    ["allowExisting"] = new McpToolParameter { Type = "boolean", Description = "action=story_trait_stamp の場合、同じストーリーのインスタンスが既に存在していても配置を許可する。既定値 false", Required = false },
                    ["plumbAction"] = new McpToolParameter { Type = "string", Description = "action=auto_plumb_building の場合に実行する auto_plumb、power、pipes、solids または spawn_minion", Required = false, EnumValues = new List<string> { "auto_plumb", "power", "pipes", "solids", "spawn_minion" } },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "対応する下位ツールのサンドボックスモードまたは InstantBuild の要件を回避することを許可する。既定値 false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "spawn_entity":
                            return SpawnEntity().Handler(args);
                        case "story_trait_stamp":
                            return StampStoryTrait().Handler(args);
                        case "set_building_enabled_debug":
                            return SetBuildingEnabledDebug(args);
                        case "auto_plumb_building":
                            var forwardArgs = (JObject)args.DeepClone();
                            string plumbAction = (forwardArgs["plumbAction"]?.ToString() ?? "").Trim();
                            forwardArgs["action"] = plumbAction;
                            return AutoPlumbBuilding().Handler(forwardArgs);
                        default:
                            return CallToolResult.Error("action must be spawn_entity, story_trait_stamp, auto_plumb_building, or set_building_enabled_debug");
                    }
                }
            };
        }

    }
}
