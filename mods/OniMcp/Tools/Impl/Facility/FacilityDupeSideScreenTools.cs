using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class FacilitySideScreenTools
    {
        public static McpTool ListBionicUpgrades()
        {
            return new McpTool
            {
                Name = "bionic_upgrades_list",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "bionic_slots_list", "bionic_side_screen_list" },
                Tags = new List<string> { "dupe", "bionic", "upgrade", "assignable", "side-screen" },
                Description = "互換用の入口。dupes_control domain=side_screen action=bionic_upgrades を使う。BionicSideScreen のアップグレードスロットのロック・空き・割り当て済み・装着済みの状態、アップグレード部品、消費電力を一覧表示する。スロットへの割り当て・解除には assignable_slot_item_set を使う",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "任意のバイオニック複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "任意のバイオニック複製人間の名前", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "複製人間、アップグレード名、prefabId、状態で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 100, 500);
                    MinionIdentity specific = ToolUtil.FindDupe(args);
                    var dupes = Components.LiveMinionIdentities.Items
                        .Where(minion => minion != null && minion.GetSMI<BionicUpgradesMonitor.Instance>() != null)
                        .Where(minion => specific == null || minion == specific)
                        .Select(BionicInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();
                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = dupes.Count,
                        ["assignmentTool"] = "assignable_slot_item_set",
                        ["bionics"] = dupes
                    });
                }
            };
        }

        public static McpTool ListMinionTodos()
        {
            return new McpTool
            {
                Name = "minion_todos_list",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "dupe_todos_list", "minion_chore_queue_list" },
                Tags = new List<string> { "dupe", "todo", "chore", "priority", "side-screen" },
                Description = "互換用の入口。dupes_control domain=side_screen action=todos を使う。MinionTodoSideScreen の現在の作業、実行可能な作業、実行を妨げられている作業、優先度、対象の位置を取得する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "任意の複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "任意の複製人間の名前", Required = false },
                    ["includeBlocked"] = new McpToolParameter { Type = "boolean", Description = "失敗・実行を妨げられている作業を含めるか。既定値は true", Required = false },
                    ["includePotentialOnly"] = new McpToolParameter { Type = "boolean", Description = "実行を妨げられている作業のうち IsPotentialSuccess のものだけを返すか。既定値は true", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "複製人間、作業、対象、グループ、妨げられている理由で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す複製人間の人数の上限。既定値は 50、最大 200", Required = false },
                    ["taskLimit"] = new McpToolParameter { Type = "integer", Description = "複製人間1人あたりの作業の返却件数の上限。既定値は 30、最大 100", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    MinionIdentity specific = ToolUtil.FindDupe(args);
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 50, 200);
                    int taskLimit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "taskLimit") ?? 30, 100));
                    bool includeBlocked = ToolUtil.GetBool(args, "includeBlocked", true);
                    bool potentialOnly = ToolUtil.GetBool(args, "includePotentialOnly", true);
                    var dupes = Components.LiveMinionIdentities.Items
                        .Where(minion => minion != null && !minion.HasTag(GameTags.Dead))
                        .Where(minion => specific == null || minion == specific)
                        .Select(minion => MinionTodoInfo(minion, includeBlocked, potentialOnly, taskLimit))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();
                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = dupes.Count,
                        ["dupes"] = dupes
                    });
                }
            };
        }


        private static Dictionary<string, object> BionicInfo(MinionIdentity minion)
        {
            var monitor = minion.GetSMI<BionicUpgradesMonitor.Instance>();
            var result = TargetInfo(minion.gameObject);
            result["online"] = monitor.IsOnline;
            result["unlockedSlotCount"] = monitor.UnlockedSlotCount;
            result["assignedSlotCount"] = monitor.AssignedSlotCount;
            result["hasAnyUpgradeAssigned"] = monitor.HasAnyUpgradeAssigned;
            result["hasAnyUpgradeInstalled"] = monitor.HasAnyUpgradeInstalled;
            result["slots"] = (monitor.upgradeComponentSlots ?? new BionicUpgradesMonitor.UpgradeComponentSlot[0])
                .Select((slot, index) => BionicSlotInfo(slot, index))
                .ToList();
            return result;
        }

        private static Dictionary<string, object> BionicSlotInfo(BionicUpgradesMonitor.UpgradeComponentSlot slot, int index)
        {
            var assigned = slot?.assignedUpgradeComponent;
            var installed = slot?.installedUpgradeComponent;
            string state = "empty";
            if (slot == null)
                state = "missing";
            else if (slot.IsLocked)
                state = "locked";
            else if (slot.HasUpgradeInstalled)
                state = "installed";
            else if (slot.HasUpgradeComponentAssigned && !slot.GetAssignableSlotInstance().IsUnassigning())
                state = "assigned";
            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["state"] = state,
                ["locked"] = slot == null || slot.IsLocked,
                ["assignableSlotId"] = slot?.GetAssignableSlotInstance()?.ID,
                ["assignedUpgrade"] = assigned == null ? null : BionicUpgradeInfo(assigned),
                ["installedUpgrade"] = installed == null ? null : BionicUpgradeInfo(installed),
                ["assignedMatchesInstalled"] = slot != null && slot.AssignedUpgradeMatchesInstalledUpgrade,
                ["wattageCost"] = slot == null ? (object)null : Math.Round(ToolUtil.SafeFloat(slot.WattageCost), 3)
            };
        }

        private static Dictionary<string, object> BionicUpgradeInfo(BionicUpgradeComponent upgrade)
        {
            return new Dictionary<string, object>
            {
                ["id"] = upgrade.GetComponent<KPrefabID>()?.InstanceID ?? upgrade.gameObject.GetInstanceID(),
                ["prefabId"] = upgrade.PrefabID().Name,
                ["name"] = ToolUtil.CleanName(upgrade.GetProperName()),
                ["boosterType"] = upgrade.Booster.ToString(),
                ["currentWattage"] = Math.Round(ToolUtil.SafeFloat(upgrade.CurrentWattage), 3),
                ["potentialWattage"] = Math.Round(ToolUtil.SafeFloat(upgrade.PotentialWattage), 3),
                ["assignee"] = upgrade.assignee == null ? null : ToolUtil.CleanName(upgrade.assignee.GetProperName())
            };
        }

        private static Dictionary<string, object> MinionTodoInfo(MinionIdentity minion, bool includeBlocked, bool potentialOnly, int taskLimit)
        {
            var consumer = minion.GetComponent<ChoreConsumer>();
            var result = TargetInfo(minion.gameObject);
            var schedulable = minion.GetComponent<Schedulable>();
            var scheduleBlock = schedulable?.GetSchedule()?.GetCurrentScheduleBlock();
            result["currentScheduleBlock"] = scheduleBlock?.name;
            if (consumer == null)
            {
                result["current"] = null;
                result["tasks"] = new List<Dictionary<string, object>>();
                result["blocked"] = new List<Dictionary<string, object>>();
                result["note"] = "No ChoreConsumer on target";
                return result;
            }

            var snapshot = consumer.GetLastPreconditionSnapshot();
            var failedContexts = snapshot.failedContexts;
            if (includeBlocked && snapshot.doFailedContextsNeedSorting)
            {
                failedContexts = new List<Chore.Precondition.Context>(failedContexts);
                failedContexts.Sort();
            }

            var succeeded = snapshot.succeededContexts
                .Where(ctx => ctx.chore != null)
                .OrderByDescending(ChoreSortScore)
                .Select(ctx => ChoreContextInfo(ctx, consumer, success: true))
                .Take(taskLimit)
                .ToList();
            var blocked = includeBlocked
                ? failedContexts
                    .Where(ctx => ctx.chore != null && (!potentialOnly || ctx.IsPotentialSuccess()))
                    .Select(ctx => ChoreContextInfo(ctx, consumer, success: false))
                    .Take(taskLimit)
                    .ToList()
                : new List<Dictionary<string, object>>();

            result["current"] = CurrentChoreInfo(consumer);
            result["tasks"] = succeeded;
            result["blocked"] = blocked;
            result["counts"] = new Dictionary<string, object>
            {
                ["succeeded"] = snapshot.succeededContexts.Count,
                ["blocked"] = snapshot.failedContexts.Count,
                ["returnedTasks"] = succeeded.Count,
                ["returnedBlocked"] = blocked.Count
            };
            return result;
        }

        private static Dictionary<string, object> CurrentChoreInfo(ChoreConsumer consumer)
        {
            var driver = consumer.choreDriver;
            var current = driver?.GetCurrentChore();
            if (current == null)
                return null;
            var info = ChoreInfo(current, consumer, null);
            var summary = DuplicantTools.DescribeCurrentChore(current, consumer.GetComponent<MinionIdentity>());
            foreach (var item in summary)
                info[item.Key] = item.Value;
            return info;
        }

        private static Dictionary<string, object> ChoreContextInfo(Chore.Precondition.Context context, ChoreConsumer consumer, bool success)
        {
            var info = ChoreInfo(context.chore, consumer, context.data);
            info["success"] = success;
            info["potentialSuccess"] = context.IsPotentialSuccess();
            info["personalPriority"] = context.personalPriority;
            info["typePriority"] = context.priority;
            info["priorityMod"] = context.priorityMod;
            info["consumerPriority"] = context.consumerPriority;
            info["cost"] = context.cost;
            info["failedPrecondition"] = FailedPreconditionInfo(context);
            return info;
        }

        private static int ChoreSortScore(Chore.Precondition.Context context)
        {
            return ((int)context.masterPriority.priority_class * 100000)
                + (context.personalPriority * 10000)
                + (context.masterPriority.priority_value * 1000)
                + context.priority
                + context.priorityMod
                + context.consumerPriority
                - context.cost;
        }

        private static Dictionary<string, object> ChoreInfo(Chore chore, ChoreConsumer consumer, object data)
        {
            var target = chore.target?.gameObject;
            var choreGameObject = chore.gameObject;
            var priority = chore.masterPriority;
            return new Dictionary<string, object>
            {
                ["name"] = SafeChoreName(chore, data),
                ["reportName"] = Safe(() => chore.GetReportName(), chore.GetType().Name),
                ["type"] = chore.choreType?.Id ?? chore.GetType().Name,
                ["groups"] = chore.choreType?.groups?.Select(group => group.Id).ToList() ?? new List<string>(),
                ["bestGroup"] = BestChoreGroup(chore, consumer)?.Id,
                ["priorityClass"] = priority.priority_class.ToString(),
                ["priorityValue"] = priority.priority_value,
                ["isCurrent"] = chore.driver == consumer.choreDriver,
                ["target"] = target == null ? null : TargetInfo(target),
                ["provider"] = choreGameObject == null ? null : TargetInfo(choreGameObject)
            };
        }

        private static string SafeChoreName(Chore chore, object data)
        {
            return Safe(() => GameUtil.GetChoreName(chore, data), Safe(() => chore.GetReportName(), chore.GetType().Name));
        }

        private static ChoreGroup BestChoreGroup(Chore chore, ChoreConsumer consumer)
        {
            ChoreGroup best = null;
            var groups = chore.choreType?.groups;
            if (groups == null || groups.Length == 0)
                return null;
            foreach (var group in groups)
            {
                if (best == null || consumer.GetPersonalPriority(best) < consumer.GetPersonalPriority(group))
                    best = group;
            }
            return best;
        }

        private static Dictionary<string, object> FailedPreconditionInfo(Chore.Precondition.Context context)
        {
            if (context.failedPreconditionId < 0)
                return null;
            var preconditions = context.chore.GetPreconditions();
            if (context.failedPreconditionId >= preconditions.Count)
            {
                return new Dictionary<string, object>
                {
                    ["index"] = context.failedPreconditionId,
                    ["id"] = "out_of_range"
                };
            }
            var precondition = preconditions[context.failedPreconditionId].condition;
            return new Dictionary<string, object>
            {
                ["index"] = context.failedPreconditionId,
                ["id"] = precondition.id,
                ["description"] = precondition.description
            };
        }

        private static T Safe<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch
            {
                return fallback;
            }
        }
    }
}
