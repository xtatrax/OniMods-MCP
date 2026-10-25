using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class ScheduleTools
    {
        private static readonly FieldInfo ScheduleDbCache = BindScheduleCache(typeof(Db), "_Instance", typeof(Db), true);
        private static readonly FieldInfo ScheduleAssignedCache = BindScheduleCache(typeof(Schedule), "assigned", typeof(List<Ref<Schedulable>>), false);

        private sealed class ScheduleReadContext
        {
            internal Game Game;
            internal ScheduleManager Manager;
            internal Db CachedDatabase;
            internal GameClock Clock;
            internal List<Schedule> NativeSchedules;
            internal List<ScheduleGroup> NativeGroups;
            internal List<ScheduleGroup> GroupEntries;
            internal Database.ScheduleGroups Groups;
            internal List<Schedule> Schedules;
        }

        private static FieldInfo BindScheduleCache(Type owner, string name, Type type, bool isStatic)
        {
            try
            {
                var field = owner.GetField(name, BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
                return field != null && field.DeclaringType == owner && field.FieldType == type && field.IsStatic == isStatic ? field : null;
            }
            catch { return null; }
        }

        private static bool TryScheduleContext(out ScheduleReadContext context, out string reason)
        {
            context = null; reason = null;
            try
            {
                var game = Game.Instance;
                var manager = ScheduleManager.Instance;
                if (game == null || game.IsLoading()) { reason = "game_unavailable"; return false; }
                if (manager == null) { reason = "manager_unavailable"; return false; }
                if (ScheduleDbCache == null) { reason = "db_binding_unavailable"; return false; }
                var db = ScheduleDbCache.GetValue(null) as Db;
                if (db?.ScheduleGroups?.allGroups == null) { reason = "group_cache_unavailable"; return false; }
                var schedules = manager.GetSchedules();
                if (schedules == null) { reason = "schedule_collection_unavailable"; return false; }
                context = new ScheduleReadContext { Game = game, Manager = manager, CachedDatabase = db, Clock = GameClock.Instance, Groups = db.ScheduleGroups, NativeSchedules = schedules, Schedules = schedules.ToList(), NativeGroups = db.ScheduleGroups.allGroups, GroupEntries = db.ScheduleGroups.allGroups.ToList() };
                return true;
            }
            catch { reason = "context_read_failed"; return false; }
        }

        private static bool SameScheduleContext(ScheduleReadContext context)
        {
            try
            {
                return ReferenceEquals(Game.Instance, context.Game) && !context.Game.IsLoading()
                    && ReferenceEquals(ScheduleManager.Instance, context.Manager)
                    && ReferenceEquals(ScheduleDbCache.GetValue(null), context.CachedDatabase)
                    && ReferenceEquals(context.CachedDatabase.ScheduleGroups, context.Groups)
                    && ReferenceEquals(GameClock.Instance, context.Clock)
                    && ReferenceEquals(context.Manager.GetSchedules(), context.NativeSchedules)
                    && context.NativeSchedules.Count == context.Schedules.Count
                    && context.NativeSchedules.Where((schedule, index) => !ReferenceEquals(schedule, context.Schedules[index])).Count() == 0
                    && ReferenceEquals(context.Groups.allGroups, context.NativeGroups)
                    && context.NativeGroups.Count == context.GroupEntries.Count
                    && context.NativeGroups.Where((group, index) => !ReferenceEquals(group, context.GroupEntries[index])).Count() == 0;
            }
            catch { return false; }
        }

        private static CallToolResult ScheduleUnavailable(string reason, string parameter = null, bool nativeInvoked = false, bool? accepted = false)
        {
            var value = new Dictionary<string, object>
            {
                ["ok"] = false, ["available"] = false, ["status"] = nativeInvoked ? "partial" : "unavailable",
                ["reasonCode"] = reason, ["nativeInvoked"] = nativeInvoked, ["accepted"] = accepted, ["requestedStateObserved"] = nativeInvoked ? null : (object)false, ["changed"] = nativeInvoked ? null : (object)false,
                ["message"] = McpLocalization.Format("schedule_timetables.unavailable", null, reason)
            };
            if (!nativeInvoked) value["applied"] = 0;
            if (parameter != null) value["parameter"] = parameter;
            var result = ScheduleResult(value); result.IsError = true; return result;
        }

        private static CallToolResult ScheduleResult(object value)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
        }

        private static bool ScheduleInteger(JObject args, string key, bool required, out int value)
        {
            value = 0;
            var token = args[key];
            if (token == null) return !required;
            return token.Type == JTokenType.Integer && int.TryParse(token.ToString(Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool ScheduleBoolean(JObject args, string key, out bool value)
        {
            value = false;
            var token = args[key];
            if (token == null) return true;
            if (token.Type != JTokenType.Boolean) return false;
            value = token.Value<bool>(); return true;
        }

        private static bool ScheduleName(JObject args, bool required, out string name)
        {
            name = null;
            if (args["schedule"] == null) return !required;
            if (args["schedule"].Type != JTokenType.String) return false;
            name = args["schedule"].Value<string>();
            return !string.IsNullOrWhiteSpace(name);
        }

        private static List<string> ReadCachedAssigned(Schedule schedule, out string reason)
        {
            return ReadCachedAssigned(schedule, out reason, out _);
        }

        private static List<string> ReadCachedAssigned(Schedule schedule, out string reason, out List<Schedulable> targets)
        {
            reason = null; targets = null;
            if (ScheduleAssignedCache == null) { reason = "assigned_binding_unavailable"; return null; }
            try
            {
                var assigned = ScheduleAssignedCache.GetValue(schedule) as List<Ref<Schedulable>>;
                if (assigned == null) { reason = "assigned_cache_unavailable"; return null; }
                var result = new List<string>();
                var seen = new List<Schedulable>();
                foreach (var reference in assigned)
                {
                    if (ReferenceEquals(reference, null)) { reason = "assigned_reference_missing"; return null; }
                    var schedulable = reference.Get();
                    if (schedulable == null || schedulable.gameObject == null) { reason = "assigned_target_unavailable"; return null; }
                    var components = schedulable.gameObject.GetComponents<Schedulable>();
                    if (components.Length != 1 || !ReferenceEquals(components[0], schedulable)) { reason = "assigned_owner_mismatch"; return null; }
                    if (seen.Any(item => ReferenceEquals(item, schedulable))) { reason = "assigned_duplicate"; return null; }
                    seen.Add(schedulable);
                    var identity = schedulable.GetComponent<MinionIdentity>();
                    string name = identity != null ? identity.GetProperName() : schedulable.GetProperName();
                    if (string.IsNullOrWhiteSpace(name)) { reason = "assigned_name_unavailable"; return null; }
                    result.Add(name);
                }
                targets = seen;
                return result;
            }
            catch { reason = "assigned_read_failed"; return null; }
        }

        private static List<Dictionary<string, object>> ScheduleRow(List<ScheduleBlock> blocks, int row, Dictionary<string, object> reasons)
        {
            if (blocks == null || blocks.Count % 24 != 0 || row < 0 || row >= blocks.Count / 24)
            { reasons["blocks"] = "timetable_unavailable"; return null; }
            var result = new List<Dictionary<string, object>>();
            for (int hour = 0; hour < 24; hour++)
            {
                var block = blocks[row * 24 + hour];
                var item = new Dictionary<string, object> { ["hour"] = hour, ["group"] = block?.GroupId, ["name"] = block?.name };
                if (block == null || string.IsNullOrEmpty(block.GroupId))
                { item["available"] = false; item["reasonCode"] = "block_unavailable"; reasons["blocks"] = "block_unavailable"; }
                result.Add(item);
            }
            return result;
        }

        private static Dictionary<string, object> ReadScheduleTimetable(Schedule schedule, int row, bool explicitRow, bool includeTimetables)
        {
            var reasons = new Dictionary<string, object>();
            var result = new Dictionary<string, object>
            {
                ["name"] = schedule?.name, ["alarmActivated"] = schedule == null ? null : (object)schedule.alarmActivated,
                ["assigned"] = null, ["blocks"] = null, ["blocksScope"] = explicitRow ? "selected_timetable" : "first_timetable",
                ["blocksTimetableIndex"] = null, ["totalBlockCount"] = null, ["timetableCount"] = null,
                ["currentTimetableIndex"] = null, ["currentBlockIndex"] = null, ["currentGroup"] = null,
                ["metadataSource"] = "Schedule.GetBlocks / ProgressTimetableIdx / GetCurrentBlockIdx / ScheduleBlock.GroupId"
            };
            if (schedule == null) { reasons["schedule"] = "schedule_missing"; }
            else
            {
                if (schedule.name == null) reasons["name"] = "schedule_name_unavailable";
                result["assigned"] = ReadCachedAssigned(schedule, out string assignedReason);
                if (assignedReason != null) reasons["assigned"] = assignedReason;
                List<ScheduleBlock> blocks = null;
                try { blocks = schedule.GetBlocks()?.ToList(); }
                catch { reasons["totalBlockCount"] = "blocks_read_failed"; }
                if (blocks == null) { reasons["totalBlockCount"] = "blocks_unavailable"; }
                else
                {
                    result["totalBlockCount"] = blocks.Count;
                    if (blocks.Count % 24 != 0) reasons["timetableCount"] = "block_count_not_multiple_of_24";
                    else result["timetableCount"] = blocks.Count / 24;
                    var selected = ScheduleRow(blocks, row, reasons);
                    result["blocks"] = selected;
                    if (selected != null) result["blocksTimetableIndex"] = row;
                    try
                    {
                        int currentRow = schedule.ProgressTimetableIdx;
                        if (blocks.Count == 0 || blocks.Count % 24 != 0 || currentRow < 0 || currentRow >= blocks.Count / 24)
                            reasons["currentTimetableIndex"] = "current_timetable_out_of_range";
                        else
                        {
                            result["currentTimetableIndex"] = currentRow;
                            if (GameClock.Instance == null) reasons["currentBlockIndex"] = "clock_unavailable";
                            else
                            {
                                try
                                {
                                    int index = schedule.GetCurrentBlockIdx();
                                    if (index < currentRow * 24 || index >= (currentRow + 1) * 24 || index >= blocks.Count)
                                        reasons["currentBlockIndex"] = "current_block_out_of_range";
                                    else
                                    {
                                        result["currentBlockIndex"] = index;
                                        if (blocks[index] == null || string.IsNullOrEmpty(blocks[index].GroupId)) reasons["currentGroup"] = "current_block_unavailable";
                                        else result["currentGroup"] = blocks[index].GroupId;
                                    }
                                }
                                catch { reasons["currentBlockIndex"] = "current_block_read_failed"; }
                            }
                        }
                    }
                    catch { reasons["currentTimetableIndex"] = "current_timetable_read_failed"; }
                    if (includeTimetables)
                    {
                        result["timetables"] = null;
                        if (blocks.Count % 24 == 0)
                        {
                            var timetables = new List<Dictionary<string, object>>();
                            for (int index = 0; index < blocks.Count / 24; index++)
                                timetables.Add(new Dictionary<string, object> { ["timetableIndex"] = index, ["blocks"] = ScheduleRow(blocks, index, reasons) });
                            result["timetables"] = timetables;
                        }
                        else reasons["timetables"] = "block_count_not_multiple_of_24";
                    }
                }
            }
            if (includeTimetables && !result.ContainsKey("timetables")) result["timetables"] = null;
            result["available"] = reasons.Count == 0;
            result["unavailableFields"] = reasons;
            return result;
        }

        private static CallToolResult ReadScheduleTimetables(JObject args)
        {
            if (!ScheduleName(args, false, out string name)) return ScheduleUnavailable("invalid_schedule", "schedule");
            if (!ScheduleInteger(args, "timetableIndex", false, out int row) || row < 0) return ScheduleUnavailable("invalid_timetable_index", "timetableIndex");
            if (!ScheduleBoolean(args, "includeTimetables", out bool includeTimetables)) return ScheduleUnavailable("invalid_boolean", "includeTimetables");
            if (!TryScheduleContext(out var context, out string reason)) return ScheduleUnavailable(reason);
            var schedules = context.Schedules;
            if (name != null)
            {
                if (schedules.Any(schedule => schedule == null)) return ScheduleUnavailable("schedule_collection_partial");
                schedules = schedules.Where(schedule => string.Equals(schedule.name, name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (schedules.Count != 1) return ScheduleUnavailable(schedules.Count == 0 ? "schedule_not_found" : "schedule_ambiguous", "schedule");
            }
            var groups = new List<Dictionary<string, object>>();
            bool partialGroups = false;
            foreach (var group in context.GroupEntries)
            {
                bool available = group != null && !string.IsNullOrEmpty(group.Id);
                partialGroups |= !available;
                var item = new Dictionary<string, object> { ["id"] = group?.Id, ["name"] = group?.Name, ["defaultSegments"] = group == null ? null : (object)group.defaultSegments, ["alarm"] = group == null ? null : (object)group.alarm };
                if (!available) { item["available"] = false; item["reasonCode"] = "group_unavailable"; }
                groups.Add(item);
            }
            var snapshots = schedules.Select(schedule => ReadScheduleTimetable(schedule, row, args["timetableIndex"] != null, includeTimetables)).ToList();
            if (name != null && ((Dictionary<string, object>)snapshots[0]["unavailableFields"]).ContainsKey("blocks"))
                return ScheduleUnavailable("timetable_or_block_unavailable", "timetableIndex");
            object currentHour = null;
            try
            {
                if (GameClock.Instance != null)
                {
                    int hour = ScheduleManager.GetCurrentHour();
                    if (hour >= 0 && hour < 24) currentHour = hour;
                }
            }
            catch { }
            if (!SameScheduleContext(context)) return ScheduleUnavailable("owner_changed");
            return ScheduleResult(new Dictionary<string, object>
            {
                ["currentHour"] = currentHour, ["groups"] = groups, ["schedules"] = snapshots,
                ["partial"] = partialGroups || currentHour == null || snapshots.Any(snapshot => !(bool)snapshot["available"])
            });
        }

        private static CallToolResult SetScheduleTimetableBlock(JObject args)
        {
            if (!ScheduleName(args, true, out string name)) return ScheduleUnavailable("invalid_schedule", "schedule");
            if (!ScheduleInteger(args, "hour", true, out int hour) || hour < 0 || hour > 23) return ScheduleUnavailable("invalid_hour", "hour");
            if (!ScheduleInteger(args, "timetableIndex", false, out int row) || row < 0) return ScheduleUnavailable("invalid_timetable_index", "timetableIndex");
            if (args["group"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(args["group"].Value<string>())) return ScheduleUnavailable("invalid_group", "group");
            if (!ScheduleBoolean(args, "dryRun", out bool dryRun) || !ScheduleBoolean(args, "confirm", out bool confirm)) return ScheduleUnavailable("invalid_boolean");
            foreach (string flag in new[] { "force", "apply", "includeTimetables", "query", "id", "name", "worldId", "allWorlds", "allowUnsupported", "allowUtilityTerrainDig" })
                if (args[flag] != null) return ScheduleUnavailable("unsupported_parameter", flag);
            if (args["task"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(args["task"].Value<string>())) return ScheduleUnavailable("task_required", "task");
            if (!dryRun && !confirm) return ScheduleUnavailable("confirm_required", "confirm");
            foreach (string flag in new[] { "allowSandbox", "instantBuild", "allowForce", "allowTerrainMutation", "allowEntitySpawn", "allowDestroy" })
                if (args[flag] != null && (args[flag].Type != JTokenType.Boolean || args[flag].Value<bool>())) return ScheduleUnavailable("unsupported_flag", flag);
            if (!TryScheduleContext(out var context, out string reason)) return ScheduleUnavailable(reason);
            if (context.Clock == null) return ScheduleUnavailable("clock_unavailable");
            if (context.Schedules.Any(item => item == null)) return ScheduleUnavailable("schedule_collection_partial");
            var matches = context.Schedules.Where(item => string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1) return ScheduleUnavailable(matches.Count == 0 ? "schedule_not_found" : "schedule_ambiguous", "schedule");
            var schedule = matches[0];
            var groupName = args["group"].Value<string>();
            if (context.GroupEntries.Any(group => group == null || string.IsNullOrEmpty(group.Id))) return ScheduleUnavailable("group_cache_partial");
            var groups = context.GroupEntries.Where(group => string.Equals(group.Id, groupName, StringComparison.OrdinalIgnoreCase) || string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (groups.Count != 1) return ScheduleUnavailable(groups.Count == 0 ? "group_not_found" : "group_ambiguous", "group");
            var targetGroup = groups[0];
            var assigned = ReadCachedAssigned(schedule, out string assignedReason, out var assignedTargets);
            if (assignedReason != null) return ScheduleUnavailable(assignedReason);
            List<ScheduleBlock> blocks;
            try { blocks = schedule.GetBlocks(); }
            catch { return ScheduleUnavailable("blocks_read_failed"); }
            if (blocks == null || blocks.Count == 0 || blocks.Count % 24 != 0 || blocks.Any(block => block == null || string.IsNullOrEmpty(block.GroupId))) return ScheduleUnavailable("blocks_unavailable");
            long absolute = (long)row * 24 + hour;
            if (absolute >= blocks.Count) return ScheduleUnavailable("timetable_out_of_range", "timetableIndex");
            int index = (int)absolute;
            int count = blocks.Count;
            string before = blocks[index].GroupId;
            string desired = targetGroup.Id;
            var result = new Dictionary<string, object>
            {
                ["ok"] = true, ["status"] = dryRun ? "preview" : "observed", ["schedule"] = schedule.name,
                ["timetableIndex"] = row, ["hour"] = hour, ["blockIndex"] = index, ["totalBlockCount"] = count,
                ["previousGroup"] = before, ["requestedGroup"] = desired, ["observedGroup"] = before,
                ["assigned"] = assigned, ["dryRun"] = dryRun, ["nativeInvoked"] = false, ["applied"] = 0, ["changed"] = false, ["accepted"] = !dryRun, ["requestedStateObserved"] = !dryRun && string.Equals(before, desired, StringComparison.Ordinal),
                ["nativePath"] = "Schedule.SetBlockGroup"
            };
            if (!SameScheduleContext(context)) return ScheduleUnavailable("owner_changed");
            if (dryRun || string.Equals(before, desired, StringComparison.Ordinal))
            {
                result["message"] = McpLocalization.Get(dryRun ? "schedule_timetables.preview" : "schedule_timetables.no_change");
                return ScheduleResult(result);
            }
            try { schedule.SetBlockGroup(index, targetGroup); }
            catch { return ScheduleUnavailable("native_call_failed", null, true, null); }
            result["nativeInvoked"] = true;
            try
            {
                if (!SameScheduleContext(context)) return ScheduleUnavailable("owner_changed", null, true, true);
                var live = context.Manager.GetSchedules();
                if (live == null || live.Any(item => item == null) || live.Count(item => item != null && string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase)) != 1
                    || !live.Any(item => ReferenceEquals(item, schedule)) || !ReferenceEquals(schedule.GetBlocks(), blocks)
                    || blocks.Count != count || blocks[index] == null || !string.Equals(targetGroup.Id, desired, StringComparison.Ordinal)
                    || context.Groups.allGroups.Count(group => group != null && string.Equals(group.Id, desired, StringComparison.Ordinal)) != 1
                    || !context.Groups.allGroups.Any(group => ReferenceEquals(group, targetGroup)))
                    return ScheduleUnavailable("owner_or_target_changed", null, true, true);
                var afterAssigned = ReadCachedAssigned(schedule, out string afterAssignedReason, out var afterTargets);
                if (afterAssignedReason != null || !afterAssigned.SequenceEqual(assigned) || afterTargets.Count != assignedTargets.Count
                    || afterTargets.Where((target, position) => !ReferenceEquals(target, assignedTargets[position])).Count() != 0)
                    return ScheduleUnavailable("assigned_targets_changed", null, true, true);
                result["observedGroup"] = blocks[index].GroupId;
                if (!string.Equals(blocks[index].GroupId, desired, StringComparison.Ordinal)) return ScheduleUnavailable("native_result_unverified", null, true, true);
            }
            catch { return ScheduleUnavailable("post_read_failed", null, true, true); }
            if (!SameScheduleContext(context)) return ScheduleUnavailable("owner_changed", null, true, true);
            result["applied"] = 1;
            result["changed"] = true;
            result["accepted"] = true;
            result["requestedStateObserved"] = true;
            result["message"] = McpLocalization.Get("schedule_timetables.applied");
            return ScheduleResult(result);
        }
    }
}
