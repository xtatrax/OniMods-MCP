using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class ScheduleTools
    {
        public static McpTool ControlSchedule()
        {
            return new McpTool
            {
                Name = "schedule_control",
                Group = "schedules",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "schedules_control", "schedule_management_control" },
                Tags = new List<string> { "schedule", "schedules", "shift", "timetable", "dupe", "日程", "作息", "轮班" },
                Description = McpLocalization.Get("schedule_timetables.description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("schedule_timetables.action"), Required = true, EnumValues = new List<string> { "list", "create", "set_block", "assign_dupe", "optimize" } },
                    ["schedule"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("schedule_timetables.schedule"), Required = false },
                    ["hour"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("schedule_timetables.hour"), Required = false },
                    ["group"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("schedule_timetables.group"), Required = false },
                    ["timetableIndex"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("schedule_timetables.timetable_index"), Required = false },
                    ["includeTimetables"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("schedule_timetables.include_timetables"), Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("schedule_timetables.dry_run"), Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("schedule_timetables.confirm"), Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "action=create では新しいスケジュール名、action=assign_dupe では複製人間の名前", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=assign_dupe の複製人間の InstanceID", Required = false },
                    ["sleepStart"] = new McpToolParameter { Type = "integer", Description = "action=create の睡眠開始時刻 0-23。既定は 20", Required = false },
                    ["alarmOn"] = new McpToolParameter { Type = "boolean", Description = "action=create でスケジュールのベルを有効にするか。既定は true", Required = false },
                    ["replaceExisting"] = new McpToolParameter { Type = "boolean", Description = "action=create で同名のスケジュールがある場合に時間枠を上書きするか。既定は true", Required = false },
                    ["shifts"] = new McpToolParameter { Type = "integer", Description = "action=optimize のシフト数。既定では人数に応じて自動設定し、最大 4 シフト", Required = false },
                    ["baseSleepStart"] = new McpToolParameter { Type = "integer", Description = "action=optimize の最初のシフトの睡眠開始時刻。既定は 20", Required = false },
                    ["prefix"] = new McpToolParameter { Type = "string", Description = "action=optimize のスケジュール名の接頭辞。既定は AI轮班。names/namePattern を渡さない場合に使う", Required = false },
                    ["names"] = new McpToolParameter { Type = "array", Description = "action=optimize のカスタムスケジュール名の一覧。カンマ区切り文字列も使用可能", Required = false },
                    ["namePattern"] = new McpToolParameter { Type = "string", Description = "action=optimize のスケジュール名テンプレート。{prefix}/{index}/{number}/{i}/{sleepStart}/{hour} に対応", Required = false },
                    ["startIndex"] = new McpToolParameter { Type = "integer", Description = "action=optimize のテンプレートと既定の命名に使用する開始番号。既定は 1", Required = false },
                    ["separator"] = new McpToolParameter { Type = "string", Description = "action=optimize の既定の命名に使う prefix/番号の区切り文字。既定は -", Required = false },
                    ["apply"] = new McpToolParameter { Type = "boolean", Description = "action=optimize で実際に作成・割り当てを行うか。既定は false でプレビューのみ", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return GetSchedules().Handler(args);
                    if (action == "create")
                        return CreateSchedule().Handler(args);
                    if (action == "set_block" || action == "set")
                        return SetScheduleBlock().Handler(args);
                    if (action == "assign_dupe" || action == "assign")
                        return AssignDupeSchedule().Handler(args);
                    if (action == "optimize")
                        return OptimizeSchedules().Handler(args);
                    return CallToolResult.Error("action must be list, create, set_block, assign_dupe, or optimize");
                }
            };
        }

        public static McpTool GetSchedules()
        {
            return new McpTool
            {
                Name = "schedule_list",
                Hidden = true,
                Group = "schedules",
                Mode = "read",
                Risk = "none",
                Description = McpLocalization.Get("schedule_timetables.list_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["schedule"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("schedule_timetables.schedule"), Required = false },
                    ["timetableIndex"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("schedule_timetables.timetable_index"), Required = false },
                    ["includeTimetables"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("schedule_timetables.include_timetables"), Required = false }
                },
                Handler = args => ReadScheduleTimetables(args)
            };
        }

        public static McpTool SetScheduleBlock()
        {
            return new McpTool
            {
                Name = "schedule_set_block",
                Hidden = true,
                Group = "schedules",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "set_schedule_block", "schedule_block_set", "change_schedule_block" },
                Tags = new List<string> { "schedule", "schedules", "shift", "timetable", "blocks", "日程", "作息", "轮班", "时间段" },
                Description = McpLocalization.Get("schedule_timetables.set_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["schedule"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("schedule_timetables.schedule"), Required = true },
                    ["hour"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("schedule_timetables.hour"), Required = true },
                    ["group"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("schedule_timetables.group"), Required = true },
                    ["timetableIndex"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("schedule_timetables.timetable_index"), Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("schedule_timetables.dry_run"), Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("schedule_timetables.confirm"), Required = false }
                },
                Handler = args => SetScheduleTimetableBlock(args)
            };
        }

        public static McpTool CreateSchedule()
        {
            return new McpTool
            {
                Name = "schedule_create",
                Hidden = true,
                Group = "schedules",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "create_schedule", "schedule_add", "add_schedule" },
                Tags = new List<string> { "schedule", "schedules", "shift", "timetable", "create", "日程", "作息", "轮班", "创建" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=management kind=schedule action=create を使用すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["name"] = new McpToolParameter { Type = "string", Description = "新しいスケジュール名", Required = true },
                    ["sleepStart"] = new McpToolParameter { Type = "integer", Description = "睡眠開始時刻 0-23。既定は 20", Required = false },
                    ["alarmOn"] = new McpToolParameter { Type = "boolean", Description = "スケジュールのベルを有効にするか。既定は true", Required = false },
                    ["replaceExisting"] = new McpToolParameter { Type = "boolean", Description = "同名のスケジュールがある場合に時間枠を上書きするか。既定は true", Required = false }
                },
                Handler = args =>
                {
                    if (ScheduleManager.Instance == null)
                        return CallToolResult.Error("ScheduleManager not initialized");

                    string name = args["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                        return CallToolResult.Error("name is required");

                    int sleepStart = NormalizeHour(ToolUtil.GetInt(args, "sleepStart") ?? 20);
                    bool alarmOn = ToolUtil.GetBool(args, "alarmOn", true);
                    bool replaceExisting = ToolUtil.GetBool(args, "replaceExisting", true);
                    var schedule = FindSchedule(name);

                    if (schedule == null)
                        schedule = ScheduleManager.Instance.AddSchedule(Db.Get().ScheduleGroups.allGroups, name.Trim(), alarmOn);
                    else if (!replaceExisting)
                        return CallToolResult.Error("Schedule already exists");

                    schedule.alarmActivated = alarmOn;
                    ApplyShiftTemplate(schedule, sleepStart);
                    return CallToolResult.Text(JsonConvert.SerializeObject(ScheduleToDictionary(schedule), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool AssignDupeSchedule()
        {
            return new McpTool
            {
                Name = "schedule_assign_dupe",
                Hidden = true,
                Group = "schedules",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "assign_dupe_schedule", "schedule_assign_duplicant", "assign_duplicant_schedule" },
                Tags = new List<string> { "schedule", "schedules", "dupe", "duplicant", "assign", "shift", "日程", "复制人", "分配", "轮班" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=management kind=schedule action=assign_dupe を使用すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false },
                    ["schedule"] = new McpToolParameter { Type = "string", Description = "対象のスケジュール名", Required = true }
                },
                Handler = args =>
                {
                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");
                    var schedule = FindSchedule(args["schedule"]?.ToString());
                    if (schedule == null)
                        return CallToolResult.Error("Schedule not found");

                    var schedulable = dupe.GetComponent<Schedulable>();
                    if (schedulable == null)
                        return CallToolResult.Error("Duplicant is not schedulable");

                    var current = schedulable.GetSchedule();
                    current?.Unassign(schedulable);
                    schedule.Assign(schedulable);
                    return CallToolResult.Text($"Assigned {dupe.GetProperName()} to schedule {schedule.name}");
                }
            };
        }

    }
}
