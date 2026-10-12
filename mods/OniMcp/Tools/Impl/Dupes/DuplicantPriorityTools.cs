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
        public static McpTool ListPersonalPriorities()
        {
            return new McpTool
            {
                Name = "dupes_priorities_list",
                Hidden = true,
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "duplicant_priorities_list", "jobs_priorities_list", "personal_priorities_list" },
                Tags = new List<string> { "dupes", "priorities", "jobs", "management", "chore-groups" },
                Description = "互換用の入口。個人の優先度を読み取る。新しい呼び出しには dupes_control domain=priority action=list を使う",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。空欄なら全員を返す", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。空欄なら全員を返す", Required = false },
                    ["choreGroup"] = new McpToolParameter { Type = "string", Description = "任意の ChoreGroup の ID/名前による絞り込み。例: Dig、Build、Cook", Required = false },
                    ["includeNonUserPrioritizable"] = new McpToolParameter { Type = "boolean", Description = "UI でユーザーが設定できないグループを含めるか。既定は false", Required = false }
                },
                Handler = args =>
                {
                    var selected = ToolUtil.FindDupe(args);
                    var dupes = selected != null
                        ? new List<MinionIdentity> { selected }
                        : Components.LiveMinionIdentities.Items.Where(dupe => dupe != null).OrderBy(dupe => dupe.GetProperName()).ToList();
                    bool includeNonUserPrioritizable = ToolUtil.GetBool(args, "includeNonUserPrioritizable", false);
                    string choreGroupQuery = args["choreGroup"]?.ToString();
                    var groups = GetChoreGroups(choreGroupQuery, includeNonUserPrioritizable).ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returnedDupes"] = dupes.Count,
                        ["returnedChoreGroups"] = groups.Count,
                        ["priorityRange"] = "0..5",
                        ["dupes"] = dupes.Select(dupe => PersonalPrioritiesInfo(dupe, groups)).ToList()
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlPersonalPriority()
        {
            return new McpTool
            {
                Name = "dupes_priority_control",
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "duplicant_priority_control", "jobs_priority_control", "personal_priority_control", "dupes_priorities_control" },
                Tags = new List<string> { "dupes", "priorities", "jobs", "management", "chore-groups", "batch", "settings" },
                Description = "Jobs/Priorities の個人の優先度の読み取り、設定、一括設定、構成を統合。action=list/set/batch/settings_get/settings_set/reset。書き込み操作には confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、set、batch、settings_get、settings_set、reset", Required = true, EnumValues = new List<string> { "list", "set", "batch", "settings_get", "settings_set", "reset" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。list/set で使用", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。list/set で使用", Required = false },
                    ["choreGroup"] = new McpToolParameter { Type = "string", Description = "ChoreGroup の ID/名前。list では絞り込み、set/batch では対象または既定値として使用可能", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "個人の優先度 0-5。set/batch で使用", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Description = "batch の設定配列。各項目は id/name/choreGroup/priority に対応", Required = false },
                    ["includeNonUserPrioritizable"] = new McpToolParameter { Type = "boolean", Description = "list で UI 上ユーザーが設定できないグループを含めるか。既定は false", Required = false },
                    ["advancedPersonalPriorities"] = new McpToolParameter { Type = "boolean", Description = "settings_set で高度な個人優先度モードを切り替える。空欄なら変更しない", Required = false },
                    ["reset"] = new McpToolParameter { Type = "boolean", Description = "settings_set で reset を実行するか。action=reset では自動的に true になる", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "書き込み操作の確認。set/batch/settings_set/reset では true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = args["action"]?.ToString()?.Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                            return ListPersonalPriorities().Handler(args);
                        case "set":
                            return SetPersonalPriority().Handler(args);
                        case "batch":
                            return BatchSetPersonalPriorities().Handler(args);
                        case "settings_get":
                            return GetPersonalPrioritySettings().Handler(args);
                        case "settings_set":
                            return SetPersonalPrioritySettings().Handler(args);
                        case "reset":
                            args["reset"] = true;
                            return SetPersonalPrioritySettings().Handler(args);
                        default:
                            return CallToolResult.Error("Unsupported action; use list, set, batch, settings_get, settings_set, or reset");
                    }
                }
            };
        }

        public static McpTool SetPersonalPriority()
        {
            return new McpTool
            {
                Name = "dupes_priority_set",
                Hidden = true,
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "duplicant_priority_set", "jobs_priority_set", "personal_priority_set", "set_personal_priority" },
                Tags = new List<string> { "dupes", "priorities", "jobs", "management", "chore-groups" },
                Description = "互換用の入口。1人の複製人間の個人優先度を設定する。新しい呼び出しには dupes_control domain=priority action=set を使う。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false },
                    ["choreGroup"] = new McpToolParameter { Type = "string", Description = "ChoreGroup の ID/名前。例: Dig、Build、Cook", Required = true },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "個人の優先度 0-5", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "複製人間の個人優先度変更の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");

                    var group = FindChoreGroup(args["choreGroup"]?.ToString(), userPrioritizableOnly: true);
                    if (group == null)
                        return CallToolResult.Error("ChoreGroup not found or not user-prioritizable");

                    var consumer = dupe.GetComponent<ChoreConsumer>();
                    if (consumer == null)
                        return CallToolResult.Error("Duplicant has no ChoreConsumer");
                    if (consumer.IsChoreGroupDisabled(group))
                        return CallToolResult.Error($"ChoreGroup is disabled for {dupe.GetProperName()}: {group.Id}");

                    int priority = Math.Max(0, Math.Min(ToolUtil.GetInt(args, "priority") ?? 3, 5));
                    var before = PersonalPriorityInfo(dupe, group);
                    consumer.SetPersonalPriority(group, priority);
                    var after = PersonalPriorityInfo(dupe, group);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changed"] = !Equals(before["priority"], after["priority"]),
                        ["dupe"] = DupeRef(dupe),
                        ["before"] = before,
                        ["after"] = after
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool BatchSetPersonalPriorities()
        {
            return new McpTool
            {
                Name = "dupes_priorities_batch_set",
                Hidden = true,
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "duplicant_priorities_batch_set", "jobs_priorities_batch_set" },
                Tags = new List<string> { "dupes", "priorities", "jobs", "management", "batch" },
                Description = "互換用の入口。個人の優先度を一括設定する。新しい呼び出しには dupes_control domain=priority action=batch を使う。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "優先度の設定配列。各項目は id/name/choreGroup/priority に対応", Required = true },
                    ["choreGroup"] = new McpToolParameter { Type = "string", Description = "既定の ChoreGroup", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "既定の優先度 0-5", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "一括変更の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    var items = args["items"] as JArray;
                    if (items == null || items.Count == 0)
                        return CallToolResult.Error("items array is required");

                    string defaultGroup = args["choreGroup"]?.ToString();
                    int? defaultPriority = ToolUtil.GetInt(args, "priority");
                    var results = new List<Dictionary<string, object>>();
                    foreach (var token in items)
                    {
                        var item = token as JObject;
                        if (item == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "item must be an object" });
                            continue;
                        }
                        if (item["choreGroup"] == null && !string.IsNullOrWhiteSpace(defaultGroup))
                            item["choreGroup"] = defaultGroup;
                        if (item["priority"] == null && defaultPriority.HasValue)
                            item["priority"] = defaultPriority.Value;
                        results.Add(SetPersonalPriorityItem(item));
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["requested"] = items.Count,
                        ["succeeded"] = results.Count(result => (bool)result["ok"]),
                        ["failed"] = results.Count(result => !(bool)result["ok"]),
                        ["results"] = results
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetPersonalPrioritySettings()
        {
            return new McpTool
            {
                Name = "dupes_priority_settings_get",
                Hidden = true,
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "jobs_priority_settings_get", "personal_priority_settings_get" },
                Tags = new List<string> { "dupes", "priorities", "jobs", "management", "settings" },
                Description = "互換用の入口。Jobs/Priorities の全体設定を読み取る。新しい呼び出しには dupes_control domain=priority action=settings_get を使う",
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args => CallToolResult.Text(JsonConvert.SerializeObject(PersonalPrioritySettingsInfo(), McpJsonUtil.Settings))
            };
        }

        public static McpTool SetPersonalPrioritySettings()
        {
            return new McpTool
            {
                Name = "dupes_priority_settings_set",
                Hidden = true,
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "jobs_priority_settings_set", "personal_priority_settings_set", "dupes_priorities_reset" },
                Tags = new List<string> { "dupes", "priorities", "jobs", "management", "settings" },
                Description = "互換用の入口。Jobs/Priorities の全体設定または reset を実行する。新しい呼び出しには dupes_control domain=priority action=settings_set/reset を使う。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["advancedPersonalPriorities"] = new McpToolParameter { Type = "boolean", Description = "高度な個人優先度モードを有効にするか。空欄なら変更しない", Required = false },
                    ["reset"] = new McpToolParameter { Type = "boolean", Description = "Jobs 画面の Reset ボタンと同じ処理を行うか。高度なモードでは Immigration の既定値を適用し、通常モードでは設定可能なすべてのグループを 3 にする", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "全体の優先度設定の変更、または複製人間の優先度リセットの確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var before = PersonalPrioritySettingsInfo();
                    bool changedMode = false;
                    bool reset = ToolUtil.GetBool(args, "reset", false);

                    if (args["advancedPersonalPriorities"] != null)
                    {
                        bool current = Game.Instance != null && Game.Instance.advancedPersonalPriorities;
                        bool advanced = ToolUtil.GetBool(args, "advancedPersonalPriorities", current);
                        changedMode = current != advanced;
                        if (Game.Instance != null)
                            Game.Instance.advancedPersonalPriorities = advanced;
                    }

                    Dictionary<string, object> resetResult = null;
                    if (reset)
                        resetResult = ResetPersonalPrioritiesLikeJobsScreen();

                    var after = PersonalPrioritySettingsInfo();
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changedMode"] = changedMode,
                        ["reset"] = resetResult,
                        ["before"] = before,
                        ["after"] = after
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static IEnumerable<ChoreGroup> GetChoreGroups(string query, bool includeNonUserPrioritizable)
        {
            var groups = Db.Get()?.ChoreGroups?.resources ?? new List<ChoreGroup>();
            return groups
                .Where(group => group != null)
                .Where(group => includeNonUserPrioritizable || group.userPrioritizable)
                .Where(group => string.IsNullOrWhiteSpace(query) || ChoreGroupMatches(group, query))
                .OrderByDescending(group => group.DefaultPersonalPriority)
                .ThenBy(group => group.Name);
        }

        private static ChoreGroup FindChoreGroup(string query, bool userPrioritizableOnly)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;
            string q = query.Trim();
            var groups = Db.Get()?.ChoreGroups?.resources ?? new List<ChoreGroup>();
            return groups.FirstOrDefault(group => group != null
                                                 && (!userPrioritizableOnly || group.userPrioritizable)
                                                 && (string.Equals(group.Id, q, StringComparison.OrdinalIgnoreCase)
                                                     || string.Equals(group.Name, q, StringComparison.OrdinalIgnoreCase)))
                   ?? groups.FirstOrDefault(group => group != null
                                                     && (!userPrioritizableOnly || group.userPrioritizable)
                                                     && ChoreGroupMatches(group, q));
        }

        private static bool ChoreGroupMatches(ChoreGroup group, string query)
        {
            string q = query.Trim();
            return Contains(group.Id, q)
                   || Contains(group.Name, q)
                   || Contains(group.description, q)
                   || Contains(group.attribute?.Id, q)
                   || Contains(group.attribute?.Name, q);
        }

        private static Dictionary<string, object> PersonalPrioritiesInfo(MinionIdentity dupe, List<ChoreGroup> groups)
        {
            return new Dictionary<string, object>
            {
                ["dupe"] = DupeRef(dupe),
                ["priorities"] = groups.Select(group => PersonalPriorityInfo(dupe, group)).ToList()
            };
        }

        private static Dictionary<string, object> PersonalPriorityInfo(MinionIdentity dupe, ChoreGroup group)
        {
            var consumer = dupe.GetComponent<ChoreConsumer>();
            bool disabled = consumer == null || consumer.IsChoreGroupDisabled(group);
            return new Dictionary<string, object>
            {
                ["choreGroupId"] = group.Id,
                ["choreGroupName"] = group.Name,
                ["description"] = group.description,
                ["userPrioritizable"] = group.userPrioritizable,
                ["defaultPriority"] = group.DefaultPersonalPriority,
                ["priority"] = consumer == null ? -1 : consumer.GetPersonalPriority(group),
                ["disabled"] = disabled,
                ["associatedSkillLevel"] = consumer == null ? 0 : consumer.GetAssociatedSkillLevel(group),
                ["attributeId"] = group.attribute?.Id,
                ["attributeName"] = group.attribute?.Name
            };
        }

        private static Dictionary<string, object> SetPersonalPriorityItem(JObject item)
        {
            var dupe = ToolUtil.FindDupe(item);
            if (dupe == null)
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "Duplicant not found", ["input"] = item };

            var group = FindChoreGroup(item["choreGroup"]?.ToString(), userPrioritizableOnly: true);
            if (group == null)
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "ChoreGroup not found or not user-prioritizable", ["dupe"] = DupeRef(dupe), ["input"] = item };

            var consumer = dupe.GetComponent<ChoreConsumer>();
            if (consumer == null)
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "Duplicant has no ChoreConsumer", ["dupe"] = DupeRef(dupe), ["input"] = item };
            if (consumer.IsChoreGroupDisabled(group))
                return new Dictionary<string, object> { ["ok"] = false, ["error"] = "ChoreGroup is disabled for duplicant", ["dupe"] = DupeRef(dupe), ["choreGroup"] = group.Id };

            int priority = Math.Max(0, Math.Min(ToolUtil.GetInt(item, "priority") ?? 3, 5));
            int before = consumer.GetPersonalPriority(group);
            consumer.SetPersonalPriority(group, priority);
            int after = consumer.GetPersonalPriority(group);
            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["changed"] = before != after,
                ["dupe"] = DupeRef(dupe),
                ["choreGroup"] = group.Id,
                ["before"] = before,
                ["after"] = after
            };
        }

        private static Dictionary<string, object> PersonalPrioritySettingsInfo()
        {
            var groups = GetChoreGroups(null, false).ToList();
            return new Dictionary<string, object>
            {
                ["advancedPersonalPriorities"] = Game.Instance != null && Game.Instance.advancedPersonalPriorities,
                ["resetBehavior"] = Game.Instance != null && Game.Instance.advancedPersonalPriorities
                    ? "apply Immigration default personal priorities to all live duplicants"
                    : "set every user-prioritizable ChoreGroup to priority 3 for all live duplicants",
                ["liveDuplicants"] = Components.LiveMinionIdentities.Items.Count(dupe => dupe != null),
                ["userPrioritizableChoreGroups"] = groups.Count,
                ["choreGroups"] = groups.Select(group => new Dictionary<string, object>
                {
                    ["id"] = group.Id,
                    ["name"] = group.Name,
                    ["defaultPriority"] = group.DefaultPersonalPriority,
                    ["attributeId"] = group.attribute?.Id,
                    ["attributeName"] = group.attribute?.Name
                }).ToList()
            };
        }

        private static Dictionary<string, object> ResetPersonalPrioritiesLikeJobsScreen()
        {
            int dupes = 0;
            int assignments = 0;
            bool advanced = Game.Instance != null && Game.Instance.advancedPersonalPriorities;
            var groups = GetChoreGroups(null, false).ToList();

            if (advanced)
            {
                if (Immigration.Instance != null)
                    Immigration.Instance.ResetPersonalPriorities();

                foreach (var dupe in Components.LiveMinionIdentities.Items)
                {
                    if (dupe == null)
                        continue;
                    dupes++;
                    if (Immigration.Instance != null)
                    {
                        Immigration.Instance.ApplyDefaultPersonalPriorities(dupe.gameObject);
                        assignments += groups.Count;
                    }
                }
            }
            else
            {
                foreach (var dupe in Components.LiveMinionIdentities.Items)
                {
                    if (dupe == null)
                        continue;
                    var consumer = dupe.GetComponent<ChoreConsumer>();
                    if (consumer == null)
                        continue;
                    dupes++;
                    foreach (var group in groups)
                    {
                        consumer.SetPersonalPriority(group, 3);
                        assignments++;
                    }
                }
            }

            return new Dictionary<string, object>
            {
                ["advancedPersonalPriorities"] = advanced,
                ["duplicantsTouched"] = dupes,
                ["priorityAssignmentsTouched"] = assignments
            };
        }
}
}
