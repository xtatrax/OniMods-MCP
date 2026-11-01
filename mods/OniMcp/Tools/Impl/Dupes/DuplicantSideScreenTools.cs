using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
{
        public static McpTool ListDirectCommands()
        {
            return new McpTool
            {
                Name = "dupes_direct_commands_list",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "dupe_actions_list", "duplicant_commands_list" },
                Tags = new List<string> { "dupes", "commands", "move", "assignables", "skills", "equipment" },
                Description = McpLocalization.Get("dupe_move_guidance.description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("dupe_move_guidance.id"), Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("dupe_move_guidance.name"), Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("dupe_move_guidance.limit"), Required = false }
                },
                Handler = args =>
                {
                    var selected = ToolUtil.FindDupe(args);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));
                    var dupes = selected != null
                        ? new List<MinionIdentity> { selected }
                        : Components.LiveMinionIdentities.Items.Where(dupe => dupe != null).OrderBy(dupe => dupe.GetProperName()).Take(limit).ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = dupes.Count,
                        ["dupes"] = dupes.Select(DirectCommandInfo).ToList(),
                        ["moveGuidance"] = DirectMoveGuidance(),
                        ["tools"] = new[]
                        {
                            "world_editor command=read path=/active/ops/dupes.md",
                            "world_editor command=read path=/active/ops/coordinate.md",
                            "dupes_skill_control",
                            "assignable_control",
                            "dupes_control domain=side_screen action=equipment",
                            "colony_control domain=management kind=diet",
                            "colony_control domain=management kind=schedule"
                        }
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListEquipment()
        {
            return new McpTool
            {
                Name = "dupes_equipment_list",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "equipment_slots_list", "dupe_equipment_list" },
                Tags = new List<string> { "dupes", "equipment", "suits", "assignables" },
                Description = "互換用の入口。dupes_control domain=side_screen action=equipment を使用すること。複製人間の装備スロット、現在の装備、割り当て可能な Assignable/Equippable を一覧表示する。スロットの選択/解除には dupes_control domain=assignable action=set_slot を使う",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。空欄なら全員を返す", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。空欄なら全員を返す", Required = false },
                    ["slotId"] = new McpToolParameter { Type = "string", Description = "装備スロット ID で絞り込む。例: Suit、Hat。空欄も可", Required = false },
                    ["includeAvailable"] = new McpToolParameter { Type = "boolean", Description = "未割り当ての使用可能な装備一覧を付けるか。既定は true", Required = false }
                },
                Handler = args =>
                {
                    var selected = ToolUtil.FindDupe(args);
                    string slotId = args["slotId"]?.ToString();
                    bool includeAvailable = ToolUtil.GetBool(args, "includeAvailable", true);
                    var dupes = selected != null
                        ? new List<MinionIdentity> { selected }
                        : Components.LiveMinionIdentities.Items.Where(dupe => dupe != null).OrderBy(dupe => dupe.GetProperName()).ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = dupes.Count,
                        ["dupes"] = dupes.Select(dupe => EquipmentInfo(dupe, slotId, includeAvailable)).ToList()
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlDupeSideScreens()
        {
            return new McpTool
            {
                Name = "dupes_side_screen_control",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "dupe_side_screen_control", "duplicant_side_screen_control" },
                Tags = new List<string> { "dupes", "side-screen", "commands", "equipment", "todo", "bionic" },
                Description = "複製人間のサイド画面を読み取り専用で扱う集約ツール: action=direct_commands/equipment/todos/bionic_upgrades",
                Parameters = DupeSideScreenControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "direct_commands":
                        case "commands":
                            return ListDirectCommands().Handler(args);
                        case "equipment":
                            return ListEquipment().Handler(args);
                        case "todos":
                        case "todo":
                            return FacilitySideScreenTools.ListMinionTodos().Handler(args);
                        case "bionic_upgrades":
                        case "bionic":
                            return FacilitySideScreenTools.ListBionicUpgrades().Handler(args);
                        default:
                            return CallToolResult.Error("action must be direct_commands, equipment, todos, or bionic_upgrades");
                    }
                }
            };
        }

        private static Dictionary<string, McpToolParameter> DupeSideScreenControlParams()
        {
            return new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "direct_commands、equipment、todos、bionic_upgrades のいずれか", Required = true, EnumValues = new List<string> { "direct_commands", "equipment", "todos", "bionic_upgrades" } },
                ["id"] = new McpToolParameter { Type = "integer", Description = "任意の複製人間の InstanceID", Required = false },
                ["name"] = new McpToolParameter { Type = "string", Description = "任意の複製人間の名前", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=todos/bionic_upgrades で複製人間、作業、対象、アップグレード名、状態により絞り込む", Required = false },
                ["slotId"] = new McpToolParameter { Type = "string", Description = "action=equipment で装備スロット ID により絞り込む。例: Suit、Hat", Required = false },
                ["includeAvailable"] = new McpToolParameter { Type = "boolean", Description = "action=equipment で未割り当ての使用可能な装備一覧を付けるか。既定は true", Required = false },
                ["includeBlocked"] = new McpToolParameter { Type = "boolean", Description = "action=todos で失敗/停滞した作業を含めるか。既定は true", Required = false },
                ["includePotentialOnly"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("minion_todo_read.include_potential_only"), Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "返す最大件数。各 action は元の一覧ツールの既定値と上限を引き継ぐ", Required = false },
                ["taskLimit"] = new McpToolParameter { Type = "integer", Description = "action=todos で複製人間1人あたりに返す作業の最大件数。既定は 30、最大 100", Required = false }
            };
        }

        private static Dictionary<string, object> DirectMoveGuidance()
        {
            return new Dictionary<string, object>
            {
                ["tool"] = "world_editor",
                ["readCommand"] = "read",
                ["editCommand"] = "edit",
                ["contentTemplate"] = "<<<<<<< SEARCH\n=======\n<commandLine>\n>>>>>>> REPLACE",
                ["taskRequired"] = true,
                ["preview"] = new Dictionary<string, object> { ["dryRun"] = true, ["confirm"] = false },
                ["execute"] = new Dictionary<string, object> { ["dryRun"] = false, ["confirm"] = true },
                ["previewScope"] = "syntax_and_capability_only",
                ["instructions"] = McpLocalization.Get("dupe_move_guidance.instructions"),
                ["single"] = new Dictionary<string, object>
                {
                    ["path"] = "/active/ops/dupes.md",
                    ["operation"] = "dupes_control",
                    ["lineTemplate"] = "call domain=command action=move_to id=<dupeId> query=<targetQueryToken> worldId=<worldId> confirm=true"
                },
                ["batch"] = new Dictionary<string, object>
                {
                    ["path"] = "/active/ops/coordinate.md",
                    ["operation"] = "coordinate_control",
                    ["targetTool"] = "dupes_control",
                    ["lineTemplate"] = "call targetTool=dupes_control payload={\"domain\":\"command\",\"action\":\"move_batch_to\",\"items\":<itemsJson>,\"confirm\":true} confirm=true",
                    ["itemsTemplate"] = "[{\"id\":<dupeId>,\"x\":<x>,\"y\":<y>,\"worldId\":<worldId>}]"
                }
            };
        }

        private static Dictionary<string, object> DirectCommandInfo(MinionIdentity dupe)
        {
            var navigator = dupe.GetComponent<Navigator>();
            var moveMonitor = navigator?.GetSMI<MoveToLocationMonitor.Instance>();
            var resume = dupe.GetComponent<MinionResume>();
            return new Dictionary<string, object>
            {
                ["dupe"] = DupeRef(dupe),
                ["canMoveTo"] = navigator != null && moveMonitor != null,
                ["availableSkillPoints"] = resume?.AvailableSkillpoints ?? 0,
                ["equipmentSlots"] = EquipmentSlots(dupe, null),
                ["assignedObjects"] = AssignedObjectsForDupe(dupe),
                ["recommendedLookup"] = new Dictionary<string, object>
                {
                    ["move"] = "#/moveGuidance",
                    ["skills"] = "dupes_control domain=skill action=list/learn",
                    ["assignables"] = "dupes_control domain=assignable action=list/set",
                    ["equipment"] = "dupes_control domain=side_screen action=equipment then dupes_control domain=assignable action=set_slot",
                    ["schedule"] = "colony_control domain=management kind=schedule action=assign_dupe",
                    ["diet"] = "colony_control domain=management kind=diet action=set"
                }
            };
        }

        private static Dictionary<string, object> EquipmentInfo(MinionIdentity dupe, string slotId, bool includeAvailable)
        {
            var slots = EquipmentSlots(dupe, slotId);
            var result = new Dictionary<string, object>
            {
                ["dupe"] = DupeRef(dupe),
                ["slots"] = slots
            };
            if (includeAvailable)
            {
                var wantedSlots = slots.Select(slot => slot["slotId"].ToString()).ToHashSet();
                result["available"] = Components.AssignableItems.Items
                    .Where(item => item != null && item is Equippable)
                    .Where(item => wantedSlots.Contains(item.slotID))
                    .Where(item => !item.IsAssigned())
                    .OrderBy(item => item.slotID)
                    .ThenBy(item => ToolUtil.CleanName(item.GetProperName()))
                    .Take(200)
                    .Select(AssignableToDictionary)
                    .ToList();
            }
            return result;
        }

        private static List<Dictionary<string, object>> EquipmentSlots(MinionIdentity dupe, string slotId)
        {
            var equipment = dupe.assignableProxy.Get()?.GetComponent<Equipment>();
            if (equipment == null)
                return new List<Dictionary<string, object>>();

            return equipment.Slots
                .Where(slot => slot != null && (string.IsNullOrWhiteSpace(slotId) || string.Equals(slot.slot.Id, slotId, StringComparison.OrdinalIgnoreCase)))
                .Select(slot =>
                {
                    var equippable = slot.assignable as Equippable;
                    return new Dictionary<string, object>
                    {
                        ["slotId"] = slot.slot.Id,
                        ["slotName"] = slot.slot.Name,
                        ["assigned"] = slot.IsAssigned(),
                        ["assignable"] = slot.assignable == null ? null : AssignableToDictionary(slot.assignable),
                        ["isEquipped"] = equippable?.isEquipped ?? false
                    };
                })
                .ToList();
        }

        private static List<Dictionary<string, object>> AssignedObjectsForDupe(MinionIdentity dupe)
        {
            return Components.AssignableItems.Items
                .Where(item => item != null && item.assignee != null && !item.assignee.IsNull())
                .Where(item => string.Equals(item.assignee.GetProperName(), dupe.GetProperName(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.slotID)
                .Select(AssignableToDictionary)
                .ToList();
        }
}
}
