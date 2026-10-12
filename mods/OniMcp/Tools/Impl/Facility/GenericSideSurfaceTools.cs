using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static class GenericSideSurfaceTools
    {
        public static McpTool ControlSideSurface()
        {
            return new McpTool
            {
                Name = "side_surface_control",
                Group = "controls",
                Mode = "read/write",
                Risk = "high",
                Aliases = new List<string> { "generic_side_surface_control", "side_screen_control", "side_controls" },
                Tags = new List<string> { "controls", "side-screen", "user-menu", "button", "checklist", "progress", "related-entities", "options", "automation", "facility" },
                Description = "オブジェクトの surface の集約ツール。domain=generic/option/activation/automation/facility/misc/geo_tuner/user_menu/maintenance。generic では kind=button/checklist/progress/related を使い、user_menu/maintenance はオブジェクトの UserMenu 操作へ振り分ける。",
                Parameters = Params(),
                Handler = args =>
                {
                    var kind = (args["kind"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    var domain = (args["domain"]?.ToString() ?? args["surface"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(domain))
                    {
                        if (kind == "button" || kind == "buttons" || kind == "side_button" || kind == "side_buttons" ||
                            kind == "checklist" || kind == "checklists" || kind == "checkbox" || kind == "checkbox_list" ||
                            kind == "progress" || kind == "progress_bar" || kind == "progress_bars" ||
                            kind == "related" || kind == "related_entity" || kind == "related_entities")
                            domain = "generic";
                        else if (kind == "automatable" || kind == "critter_sensor")
                            domain = "automation";
                        else if (kind == "dispenser" || kind == "suit_locker" || kind == "lore_bearer" || kind == "telepad" || kind == "artifact")
                            domain = "facility";
                        else if (kind == "n_toggle" || kind == "logic_alarm" || kind == "turbo_heater" || kind == "configurable_consumer")
                            domain = "misc";
                        else if (kind == "user_menu" || kind == "context_menu" || kind == "maintenance")
                            domain = kind;
                        else
                            domain = "generic";
                    }
                    var action = (args["action"]?.ToString() ?? "list").Trim().ToLowerInvariant();

                    switch (domain)
                    {
                        case "generic":
                        case "button_surface":
                            break;
                        case "option":
                        case "options":
                        case "side_option":
                            return OptionControlTools.ControlSideOption().Handler(args);
                        case "activation":
                        case "activation_range":
                        case "activation_ranges":
                            return ActivationRangeTools.ControlActivationRange().Handler(args);
                        case "automation":
                        case "automation_side":
                            return AutomationSideScreenTools.ControlAutomationSideScreen().Handler(args);
                        case "facility":
                        case "facility_sidescreen":
                        case "facility_side":
                            return FacilitySideScreenTools.ControlFacilitySideScreen().Handler(args);
                        case "misc":
                        case "misc_sidescreen":
                        case "misc_side":
                            return MiscSideScreenTools.ControlMiscSideScreen().Handler(args);
                        case "geo_tuner":
                        case "geotuner":
                            return GeoTunerTools.ControlGeoTuner().Handler(args);
                        case "user_menu":
                        case "context_menu":
                        case "user_action":
                        case "generic_user_menu":
                            return ForwardUserAction(args, "generic");
                        case "maintenance":
                        case "maintenance_action":
                        case "service":
                            return ForwardUserAction(args, "maintenance");
                        default:
                            return CallToolResult.Error("Unsupported building_control domain=side_surface surface. Use surface=generic/option/activation/automation/facility/misc/geo_tuner/user_menu/maintenance.");
                    }

                    switch (kind)
                    {
                        case "":
                        case "button":
                        case "buttons":
                        case "side_button":
                        case "side_buttons":
                            if (action == "list")
                                return SideScreenButtonTools.ListButtons().Handler(args);
                            if (action == "press" || action == "click" || action == "execute")
                                return SideScreenButtonTools.PressButton().Handler(args);
                            break;

                        case "checklist":
                        case "checklists":
                        case "checkbox":
                        case "checkbox_list":
                            if (action == "list")
                                return ChecklistTools.ListChecklists().Handler(args);
                            break;

                        case "progress":
                        case "progress_bar":
                        case "progress_bars":
                            if (action == "list")
                                return ProgressBarTools.ListProgressBars().Handler(args);
                            break;

                        case "related":
                        case "related_entity":
                        case "related_entities":
                            if (action == "list")
                                return RelatedEntityTools.ListRelatedEntities().Handler(args);
                            if (action == "focus" || action == "select")
                                return RelatedEntityTools.FocusRelatedEntity().Handler(args);
                            break;
                    }

                    return CallToolResult.Error("Unsupported building_control domain=side_surface kind/action. Use kind=button/checklist/progress/related and action=list/press/focus.");
                }
            };
        }

        private static Dictionary<string, McpToolParameter> Params()
        {
            return new Dictionary<string, McpToolParameter>
            {
                ["domain"] = new McpToolParameter { Type = "string", Description = "generic、option、activation、automation、facility、misc、geo_tuner、user_menu、maintenance のいずれか", Required = false, EnumValues = new List<string> { "generic", "option", "activation", "automation", "facility", "misc", "geo_tuner", "user_menu", "maintenance" } },
                ["kind"] = new McpToolParameter { Type = "string", Description = "generic: button/checklist/progress/related；automation: automatable/critter_sensor；facility: dispenser/suit_locker/lore_bearer/telepad/artifact；misc: n_toggle/logic_alarm/turbo_heater/configurable_consumer；option: direction/few_option/broadcast_receiver/radbolt_direction", Required = false },
                ["action"] = new McpToolParameter { Type = "string", Description = "list。button/user_menu は press、maintenance は execute、related は focus/select、一括処理は batch に対応する", Required = false },
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の領域ハンドル。list 操作で使用できる", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の始点の X。list 操作で使用できる", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の始点の Y。list 操作で使用できる", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の終点の X。list 操作で使用できる", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の終点の Y。list 操作で使用できる", Required = false },
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID。press/focus 操作で使用できる", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X。press/focus 操作で使用できる", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y。press/focus 操作で使用できる", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "list 操作のテキストによる絞り込み", Required = false },
                ["category"] = new McpToolParameter { Type = "string", Description = "domain=user_menu action=list で分類により絞り込む。例: orders、resources、buildings、ranching、care", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "list 操作で返すオブジェクト数の上限。既定値は 100、最大 500", Required = false },
                ["actionKey"] = new McpToolParameter { Type = "string", Description = "domain=user_menu/maintenance の対象操作の key。一括処理の項目では actionKey または a を使える", Required = false },
                ["enabled"] = new McpToolParameter { Type = "boolean", Description = "domain=maintenance の set_transit_tube_wax / set_hive_harvest で設定する状態", Required = false },
                ["slotId"] = new McpToolParameter { Type = "string", Description = "domain=maintenance の unequip_dupe_equipment の装備スロット ID", Required = false },
                ["equipmentId"] = new McpToolParameter { Type = "integer", Description = "domain=maintenance の unequip_dupe_equipment の装備の KPrefabID.InstanceID", Required = false },
                ["equipmentPrefab"] = new McpToolParameter { Type = "string", Description = "domain=maintenance の unequip_dupe_equipment の装備の prefabId", Required = false },
                ["items"] = new McpToolParameter { Type = "array", Description = "batch 操作の配列", Required = false },
                ["defaults"] = new McpToolParameter { Type = "object", Description = "batch で各項目へ統合する既定の引数", Required = false },
                ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                ["interactableOnly"] = new McpToolParameter { Type = "boolean", Description = "button list でクリック可能なボタンだけを返すか", Required = false },
                ["checkedOnly"] = new McpToolParameter { Type = "boolean", Description = "checklist list でチェック済みの項目だけを返すか", Required = false },
                ["enabledOnly"] = new McpToolParameter { Type = "boolean", Description = "checklist list で有効なチェックリストだけを返すか。既定値は true", Required = false },
                ["buttonIndex"] = new McpToolParameter { Type = "integer", Description = "button press のボタンのインデックス。既定値は 0", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "button press では true が必須", Required = false },
                ["force"] = new McpToolParameter { Type = "boolean", Description = "button press で enabled/interactable のチェックを省略する", Required = false },
                ["relatedIndex"] = new McpToolParameter { Type = "integer", Description = "related focus の関連オブジェクトの番号。既定値は 0", Required = false },
                ["relatedId"] = new McpToolParameter { Type = "integer", Description = "related focus の関連オブジェクトの InstanceID", Required = false },
                ["relatedQuery"] = new McpToolParameter { Type = "string", Description = "related focus の関連オブジェクトのテキスト照合", Required = false }
            };
        }

        private static CallToolResult ForwardUserAction(JObject args, string userDomain)
        {
            var forwarded = args == null ? new JObject() : (JObject)args.DeepClone();
            forwarded["domain"] = userDomain;
            return UserMenuActionTools.ControlUserAction().Handler(forwarded);
        }
    }
}
