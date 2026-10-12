using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
{
        public static McpTool ControlAssignable()
        {
            return new McpTool
            {
                Name = "assignable_control",
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Description = "割り当て可能なオブジェクトの集約入口: action=list/set/set_slot",
                Parameters = AssignableControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "list").Trim().ToLowerInvariant();
                    if (action == "list" || action == "status")
                        return ListAssignables().Handler(args);

                    if (action == "set" || action == "assign")
                    {
                        var forwarded = (JObject)args.DeepClone();
                        forwarded["action"] = args["assignmentAction"] ?? args["assignAction"] ?? "assign";
                        return SetAssignable().Handler(forwarded);
                    }

                    if (action == "set_slot" || action == "slot")
                    {
                        var forwarded = (JObject)args.DeepClone();
                        forwarded["action"] = args["slotAction"] ?? args["itemAction"] ?? "assign";
                        return SetAssignableSlotItem().Handler(forwarded);
                    }

                    return CallToolResult.Error("action must be list, set, or set_slot");
                }
            };
        }

        public static McpTool ListAssignables()
        {
            return new McpTool
            {
                Name = "assignables_list",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "beds_list", "medical_assignables_list" },
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=assignable action=list を使用すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["slotId"] = new McpToolParameter { Type = "string", Description = "割り当てスロットで絞り込む。例: Bed、MedicalBed、SuitLocker。空欄も可", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "オブジェクト名/prefab/現在の割り当て対象で検索する", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定では絞り込まない", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数。既定は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    string slotId = args["slotId"]?.ToString();
                    string query = args["query"]?.ToString();
                    int worldId = ToolUtil.GetInt(args, "worldId") ?? -1;
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var items = Components.AssignableItems.Items
                        .Where(item => item != null && ToolUtil.GameObjectMatchesWorld(item.gameObject, worldId))
                        .Where(item => string.IsNullOrWhiteSpace(slotId) || string.Equals(item.slotID, slotId, StringComparison.OrdinalIgnoreCase))
                        .Where(item => AssignableMatches(item, query))
                        .OrderBy(item => item.slotID)
                        .ThenBy(item => item.GetProperName())
                        .Take(limit)
                        .Select(AssignableToDictionary)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = items.Count,
                        ["assignables"] = items
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetAssignable()
        {
            return new McpTool
            {
                Name = "assignables_set",
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "bed_assign", "medical_bed_assign", "assignable_set" },
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=assignable action=set と assignmentAction=assign/unassign/public を使用すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["targetId"] = new McpToolParameter { Type = "integer", Description = "割り当て可能なオブジェクトの InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "割り当て可能なオブジェクトのセル X。targetId が空の場合に座標で検索する", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "割り当て可能なオブジェクトのセル Y。targetId が空の場合に座標で検索する", Required = false },
                    ["slotId"] = new McpToolParameter { Type = "string", Description = "任意の割り当てスロットによる絞り込み。同じセルの複数オブジェクトからの誤選択を防ぐ", Required = false },
                    ["dupeId"] = new McpToolParameter { Type = "integer", Description = "割り当てる複製人間の InstanceID。action=assign では dupeName とどちらか一方を指定", Required = false },
                    ["dupeName"] = new McpToolParameter { Type = "string", Description = "割り当てる複製人間の名前。action=assign では dupeId とどちらか一方を指定", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "assign、unassign、public。既定は assign", Required = false, EnumValues = new List<string> { "assign", "unassign", "public" } },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "割り当て変更の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    var assignable = FindAssignable(args);
                    if (assignable == null)
                        return CallToolResult.Error("Assignable target not found");

                    string action = (args["action"]?.ToString() ?? "assign").Trim().ToLowerInvariant();
                    if (action == "unassign")
                    {
                        assignable.Unassign();
                    }
                    else if (action == "public")
                    {
                        if (!assignable.canBePublic)
                            return CallToolResult.Error("Target cannot be assigned to public group");
                        assignable.Assign(Game.Instance.assignmentManager.assignment_groups["public"]);
                    }
                    else
                    {
                        var dupeArgs = new Newtonsoft.Json.Linq.JObject();
                        if (args["dupeId"] != null)
                            dupeArgs["id"] = args["dupeId"];
                        if (args["dupeName"] != null)
                            dupeArgs["name"] = args["dupeName"];
                        var dupe = ToolUtil.FindDupe(dupeArgs);
                        if (dupe == null)
                            return CallToolResult.Error("Duplicant not found");
                        assignable.Assign(dupe);
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(AssignableToDictionary(assignable), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetAssignableSlotItem()
        {
            return new McpTool
            {
                Name = "assignable_slot_item_set",
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "equipment_slot_set", "ownables_slot_set", "bionic_upgrade_slot_set" },
                Tags = new List<string> { "dupes", "equipment", "assignables", "ownables", "bionic", "side-screen" },
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=assignable action=set_slot と slotAction=assign/unassign/none を使用すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。dupeId も使用可能", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。dupeName も使用可能", Required = false },
                    ["dupeId"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。id より優先する", Required = false },
                    ["dupeName"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。name より優先する", Required = false },
                    ["slotId"] = new McpToolParameter { Type = "string", Description = "スロットの種類 ID。例: Suit、Hat、BionicUpgrade。同じ種類のスロットが複数ある場合は slotInstanceId または slotIndex の併記を推奨", Required = false },
                    ["slotInstanceId"] = new McpToolParameter { Type = "string", Description = "AssignableSlotInstance.ID。例: BionicUpgrade2。assignableSlotId も使用可能", Required = false },
                    ["assignableSlotId"] = new McpToolParameter { Type = "string", Description = "slotInstanceId の別名。dupes_control domain=side_screen action=bionic_upgrades の戻り値のフィールドを直接使用しやすくする", Required = false },
                    ["slotIndex"] = new McpToolParameter { Type = "integer", Description = "slotId に一致するスロット一覧内の 0 始まりの番号。複数スロットがある場合に使用可能", Required = false },
                    ["assignableId"] = new McpToolParameter { Type = "integer", Description = "割り当てる Assignable/Equippable の InstanceID。action=assign で使用可能", Required = false },
                    ["itemId"] = new McpToolParameter { Type = "integer", Description = "assignableId の別名", Required = false },
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "割り当てるアイテムの prefabId。assignableId が空の場合に使用可能", Required = false },
                    ["itemName"] = new McpToolParameter { Type = "string", Description = "割り当てるアイテムの名前/キーワード。assignableId と prefabId が空の場合に使用可能", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "itemName の別名。名前、prefabId、slotId で候補アイテムを検索する", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "assign または unassign/none。既定は assign", Required = false, EnumValues = new List<string> { "assign", "unassign", "none" } },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "スロットの割り当て変更の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var dupe = FindDupeForAssignableSlot(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");

                    string slotError;
                    var slot = ResolveAssignableSlot(dupe, args, out slotError);
                    if (slot == null)
                        return CallToolResult.Error(slotError ?? "Assignable slot not found");

                    var ownerIdentity = slot.assignables?.GetComponent<IAssignableIdentity>();
                    if (ownerIdentity == null)
                        return CallToolResult.Error("Slot owner identity not found");

                    var before = AssignableSlotToDictionary(slot);
                    string action = (args["action"]?.ToString() ?? "assign").Trim().ToLowerInvariant();
                    if (action == "unassign" || action == "none")
                    {
                        slot.Unassign();
                    }
                    else
                    {
                        string itemError;
                        var item = FindAssignableForSlot(slot, ownerIdentity, args, out itemError);
                        if (item == null)
                            return CallToolResult.Error(itemError ?? "Assignable item not found");

                        if (item.IsAssigned())
                            item.Unassign();
                        item.Assign(ownerIdentity, slot);
                    }

                    var after = AssignableSlotToDictionary(slot);
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changed"] = JsonConvert.SerializeObject(before) != JsonConvert.SerializeObject(after),
                        ["dupe"] = DupeRef(dupe),
                        ["action"] = action,
                        ["before"] = before,
                        ["after"] = after
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static Dictionary<string, McpToolParameter> AssignableControlParams()
        {
            return new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list/status、set/assign、set_slot/slot のいずれか。既定は list", Required = false },
                ["slotId"] = new McpToolParameter { Type = "string", Description = "action=list/set/set_slot で使用可能。スロットの種類で絞り込み、または特定する", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list では割り当て可能なオブジェクト、action=set_slot では候補アイテムを検索する", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "action=list でワールドにより絞り込む", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数。既定は 100、最大 500", Required = false },
                ["targetId"] = new McpToolParameter { Type = "integer", Description = "action=set の割り当て可能なオブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "action=set の割り当て可能なオブジェクトのセル X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "action=set の割り当て可能なオブジェクトのセル Y", Required = false },
                ["dupeId"] = new McpToolParameter { Type = "integer", Description = "action=set/set_slot の複製人間の InstanceID。set では割り当て対象、set_slot ではスロットを所有する複製人間", Required = false },
                ["dupeName"] = new McpToolParameter { Type = "string", Description = "action=set/set_slot の複製人間の名前", Required = false },
                ["id"] = new McpToolParameter { Type = "integer", Description = "action=set_slot の複製人間の InstanceID。dupeId も使用可能", Required = false },
                ["name"] = new McpToolParameter { Type = "string", Description = "action=set_slot の複製人間の名前。dupeName も使用可能", Required = false },
                ["assignmentAction"] = new McpToolParameter { Type = "string", Description = "action=set の内部操作: assign、unassign、public。既定は assign", Required = false, EnumValues = new List<string> { "assign", "unassign", "public" } },
                ["assignAction"] = new McpToolParameter { Type = "string", Description = "assignmentAction の短い別名", Required = false, EnumValues = new List<string> { "assign", "unassign", "public" } },
                ["slotAction"] = new McpToolParameter { Type = "string", Description = "action=set_slot の内部操作: assign、unassign、none。既定は assign", Required = false, EnumValues = new List<string> { "assign", "unassign", "none" } },
                ["itemAction"] = new McpToolParameter { Type = "string", Description = "slotAction の別名", Required = false, EnumValues = new List<string> { "assign", "unassign", "none" } },
                ["slotInstanceId"] = new McpToolParameter { Type = "string", Description = "action=set_slot の AssignableSlotInstance.ID。例: BionicUpgrade2", Required = false },
                ["assignableSlotId"] = new McpToolParameter { Type = "string", Description = "slotInstanceId の別名。dupes_control domain=side_screen action=bionic_upgrades の戻り値のフィールドを使用しやすくする", Required = false },
                ["slotIndex"] = new McpToolParameter { Type = "integer", Description = "action=set_slot で同じ種類のスロットを指定する 0 始まりの番号", Required = false },
                ["assignableId"] = new McpToolParameter { Type = "integer", Description = "action=set_slot で割り当てる Assignable/Equippable の InstanceID", Required = false },
                ["itemId"] = new McpToolParameter { Type = "integer", Description = "assignableId の別名", Required = false },
                ["prefabId"] = new McpToolParameter { Type = "string", Description = "action=set_slot で prefabId により候補アイテムを選ぶ", Required = false },
                ["itemName"] = new McpToolParameter { Type = "string", Description = "action=set_slot で名前/キーワードにより候補アイテムを選ぶ", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set/set_slot では true が必須", Required = false }
            };
        }
        }
}
