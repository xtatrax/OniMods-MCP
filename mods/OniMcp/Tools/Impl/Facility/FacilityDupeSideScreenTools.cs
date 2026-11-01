using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;
using OniMcp.Support;

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
                Description = McpLocalization.Get("minion_todo_read.description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("minion_todo_read.id"), Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("minion_todo_read.name"), Required = false },
                    ["includeBlocked"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("minion_todo_read.include_blocked"), Required = false },
                    ["includePotentialOnly"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("minion_todo_read.include_potential_only"), Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("minion_todo_read.query"), Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("minion_todo_read.limit"), Required = false },
                    ["taskLimit"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("minion_todo_read.task_limit"), Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error(McpLocalization.Get("errors.game_not_initialized"));
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
                result["note"] = McpLocalization.Get("minion_todo_read.consumer_missing");
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
                    .Where(ctx => ctx.chore != null)
                    .Select(ctx => new { Context = ctx, Observation = NativeChorePreconditionObservation.Read(ctx) })
                    .Where(row => !potentialOnly || row.Observation.PotentialSuccess != false)
                    .Select(row => ChoreContextInfo(row.Context, consumer, success: false, observation: row.Observation))
                    .Take(taskLimit)
                    .ToList()
                : new List<Dictionary<string, object>>();

            int unknownPotential = succeeded.Count(info => info["potentialSuccess"] == null)
                + blocked.Count(info => info["potentialSuccess"] == null);
            result["current"] = CurrentChoreInfo(consumer);
            result["tasks"] = succeeded;
            result["blocked"] = blocked;
            result["counts"] = new Dictionary<string, object>
            {
                ["succeeded"] = snapshot.succeededContexts.Count,
                ["blocked"] = snapshot.failedContexts.Count,
                ["returnedTasks"] = succeeded.Count,
                ["returnedBlocked"] = blocked.Count,
                ["returnedUnknownPotential"] = unknownPotential
            };
            if (unknownPotential > 0)
                result["note"] = McpLocalization.Get("minion_todo_read.unknown_note");
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

        private static Dictionary<string, object> ChoreContextInfo(Chore.Precondition.Context context, ChoreConsumer consumer, bool success,
            NativeChorePreconditionObservation.Observation observation = null)
        {
            observation = observation ?? NativeChorePreconditionObservation.Read(context);
            var info = ChoreInfo(context.chore, consumer, context.data);
            info["success"] = success;
            info["potentialSuccess"] = observation.PotentialSuccess.HasValue ? (object)observation.PotentialSuccess.Value : null;
            info["preconditionState"] = observation.State;
            info["personalPriority"] = context.personalPriority;
            info["typePriority"] = context.priority;
            info["priorityMod"] = context.priorityMod;
            info["consumerPriority"] = context.consumerPriority;
            info["cost"] = context.cost;
            info["failedPrecondition"] = observation.FailedPrecondition;
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
