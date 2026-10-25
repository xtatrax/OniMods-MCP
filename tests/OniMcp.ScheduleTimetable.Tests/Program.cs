using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--dry-run-probe")) DryRunProbe();
            else { MetadataAndRoutes(); ReadProfiles(); MissingAndPartial(); AssignedBoundaries();
                StrictRequests(); Writes(); OwnerAndNativeFailures(); OutcomeIntegration(); }
            Console.WriteLine(JsonConvert.SerializeObject(new { status = "pass", checks,
                wholeProductFiles = 10, sourceExtraction = false, substitutedNativeBoundary = true }));
            return 0;
        }
        catch (Exception error)
        { Console.Error.WriteLine(error.GetType().Name + ": " + error.Message); return 1; }
    }
    private static void Check(bool value, string name)
    { checks++; if (!value) throw new InvalidOperationException(name); }
    private static JObject Body(CallToolResult result)
    {
        Check(result.Content != null, "actual_result_content_present");
        var parts = result.Content.Where(part => part.Type == "text" && part.Meta != null
            && part.Meta.ContainsKey(ToolContent.ContentKindMetadataKey)
            && (string)part.Meta[ToolContent.ContentKindMetadataKey] == ToolContent.ToolResultKind).ToList();
        Check(parts.Count == 1, "one_actual_business_content");
        return JObject.Parse(parts[0].Text);
    }
    private static JObject Good(CallToolResult result)
    { Check(!result.IsError, "expected_success_not_error"); return Body(result); }
    private static JObject Bad(CallToolResult result, string reason = null)
    {
        Check(result.IsError, "invalid_request_is_error"); var body = Body(result);
        Check((bool?)body["nativeInvoked"] == false, "invalid_request_native_not_invoked");
        Check(body["applied"]?.Type == JTokenType.Integer && (int)body["applied"] == 0, "pre_refusal_known_counter_zero");
        if (reason != null) Check((string)body["reasonCode"] == reason, "expected_reason_" + reason);
        return body;
    }
    private static JObject ReadArgs(bool named = false)
    {
        var args = new JObject { ["domain"] = "management", ["kind"] = "schedule", ["action"] = "list" };
        if (named) args["schedule"] = "FixturePlan"; return args;
    }
    private static JObject WriteArgs(bool dry = false)
    {
        return new JObject { ["domain"] = "management", ["kind"] = "schedule", ["action"] = "set_block",
            ["schedule"] = "FixturePlan", ["hour"] = 16, ["group"] = "Hygene", ["timetableIndex"] = 1,
            ["task"] = "fixture exact timetable change", ["confirm"] = true, ["dryRun"] = dry };
    }
    private static CallToolResult Colony(JObject args) { return ColonyTools.ControlColony().Handler(args); }
    private static JObject Only(JObject body)
    { var values = body["schedules"] as JArray; Check(values != null && values.Count == 1, "one_native_schedule"); return (JObject)values[0]; }
    private static void ExplicitNull(JObject body, string key)
    { Check(body.Property(key) != null && body[key].Type == JTokenType.Null, "explicit_null_" + key); }
    private static string Blocks(Schedule schedule)
    { return JsonConvert.SerializeObject(schedule.FixtureBlocks == null ? null : schedule.FixtureBlocks.Select(b => b == null ? null : new[] { b.GroupId, b.name }).ToArray()); }
    private static void NoWrites(Fixture f, string blocks = null)
    {
        Check(f.Schedule.FixtureSetCalls == 0, "no_native_SetBlockGroup");
        Check(f.Schedule.FixtureNotifications == 0, "no_native_notification");
        Check(f.Schedule.FixtureAssignCalls == 0 && f.Schedule.FixtureUnassignCalls == 0, "no_assignment_mutation");
        Check(f.Schedule.FixtureProgressWrites == 0, "no_native_progress_write");
        Check(f.Schedule.FixtureGetAssignedCalls == 0, "no_lazy_GetAssigned");
        Check(f.Schedule.FixtureCurrentGroupCalls == 0, "no_derived_current_block_fallback");
        Check(Db.FixtureGetCalls == 0, "no_lazy_Db_Get"); Check(FixtureRoutes.Calls == 0, "no_sibling_route");
        if (blocks != null) Check(Blocks(f.Schedule) == blocks, "all_supplied_blocks_unchanged");
    }
    private static void MetadataAndRoutes()
    {
        foreach (var tool in new[] { ScheduleTools.ControlSchedule(), ManagementTools.ControlManagement(), ColonyTools.ControlColony() })
            foreach (string name in new[] { "includeTimetables", "timetableIndex" })
                Check(tool.Parameters.ContainsKey(name), "actual_factory_declares_" + tool.Name + "_" + name);
        foreach (int route in new[] { 0, 1, 2 })
        {
            var f = Seed(); var args = ReadArgs(true); args["includeTimetables"] = true;
            if (route == 1) { args["domain"] = "schedule"; args.Remove("kind"); }
            if (route == 2) { args.Remove("domain"); args.Remove("kind"); }
            string original = args.ToString(Formatting.None), blocks = Blocks(f.Schedule);
            var result = route == 0 ? Colony(args) : route == 1 ? ManagementTools.ControlManagement().Handler(args) : ScheduleTools.ControlSchedule().Handler(args);
            Check(((JArray)Only(Good(result))["timetables"]).Count == 3, "whole_router_keeps_expansion");
            Check(args.ToString(Formatting.None) == original, "router_does_not_mutate_caller"); NoWrites(f, blocks);
        }
        Check(ScheduleTools.SetScheduleBlock().Tags.Contains("轮班"), "legacy_set_block_tag_preserved");
        Seed(); Check(Colony(new JObject { ["domain"] = "not-a-domain", ["action"] = "list" }).IsError, "bad_colony_domain");
        Check(ManagementTools.ControlManagement().Handler(new JObject { ["domain"] = "not-a-domain", ["action"] = "list" }).IsError, "bad_management_domain");
    }
    private static void ReadProfiles()
    {
        var f = Seed(); string blocks = Blocks(f.Schedule); var body = Good(Colony(ReadArgs())); var item = Only(body);
        Check((int)body["currentHour"] == 16, "native_current_hour"); Check((int)item["totalBlockCount"] == 72, "native_total_72");
        Check((int)item["timetableCount"] == 3, "native_three_rows"); Check((int)item["currentTimetableIndex"] == 1, "independent_native_progress_row1");
        Check((int)item["currentBlockIndex"] == 40 && (string)item["currentGroup"] == "Worktime", "actual_current_group_not_firstrow");
        Check((string)item["blocksScope"] == "first_timetable" && (int)item["blocksTimetableIndex"] == 0, "legacy_firstrow_scope");
        Check(((JArray)item["blocks"]).Count == 24 && (string)item["blocks"][16]["group"] == "Hygene", "firstrow_still_24");
        Check(item.Property("timetables") == null, "default_no_all_rows_expansion");
        Check(((JArray)item["assigned"]).Count == 1 && (string)item["assigned"][0] == "Fixture minion", "passive_assigned_name");
        Check((bool)item["available"] && !(bool)body["partial"], "complete_native_snapshot"); NoWrites(f, blocks);
        var args = ReadArgs(true); args["timetableIndex"] = 1; item = Only(Good(Colony(args)));
        Check((string)item["blocksScope"] == "selected_timetable" && (int)item["blocksTimetableIndex"] == 1, "explicit_row_scope");
        Check((string)item["blocks"][16]["group"] == "Worktime" && item.Property("timetables") == null, "selected_row_not_all_rows"); NoWrites(f, blocks);
        args["includeTimetables"] = true; item = Only(Good(Colony(args))); var rows = (JArray)item["timetables"];
        Check(rows.Count == 3, "explicit_three_rows");
        for (int row = 0; row < 3; row++)
        { Check((int)rows[row]["timetableIndex"] == row, "row_index"); Check(((JArray)rows[row]["blocks"]).Count == 24, "row_24_slots"); }
        Check((string)rows[0]["blocks"][16]["group"] == "Hygene" && (string)rows[1]["blocks"][16]["group"] == "Worktime", "independent_rows_kept"); NoWrites(f, blocks);
        f = Seed(24, 0, 0); body = Good(Colony(ReadArgs())); item = Only(body);
        Check((int)item["totalBlockCount"] == 24 && (int)item["timetableCount"] == 1, "one_row_compat");
        Check((int)item["currentTimetableIndex"] == 0 && (int)item["currentBlockIndex"] == 0, "real_native_index_zero_kept"); NoWrites(f);
        f = Seed(); f.Manager.FixtureSchedules.Clear(); body = Good(Colony(ReadArgs()));
        Check(((JArray)body["schedules"]).Count == 0 && !(bool)body["partial"], "known_empty_schedules"); NoWrites(f);
    }
    private static void MissingAndPartial()
    {
        foreach (int variant in Enumerable.Range(0, 6))
        {
            var f = Seed();
            if (variant == 0) Game.Instance = null;
            if (variant == 1) Game.Instance.FixtureLoading = true;
            if (variant == 2) ScheduleManager.Instance = null;
            if (variant == 3) Db.FixtureSetExisting(null);
            if (variant == 4) f.Groups.allGroups = null;
            if (variant == 5) f.Manager.FixtureSchedules = null;
            Bad(Colony(ReadArgs())); NoWrites(f);
        }
        var x = Seed(); x.Schedule.FixtureBlocks = null; var body = Good(Colony(ReadArgs())); var item = Only(body);
        ExplicitNull(item, "totalBlockCount"); ExplicitNull(item, "blocks"); ExplicitNull(item, "currentTimetableIndex"); Check((bool)body["partial"], "null_blocks_partial"); NoWrites(x);
        x = Seed(); x.Schedule.FixtureBlocks = x.Schedule.FixtureBlocks.Take(25).ToList(); item = Only(Good(Colony(ReadArgs())));
        Check((int)item["totalBlockCount"] == 25, "partial_native_count_not_zero"); ExplicitNull(item, "timetableCount"); ExplicitNull(item, "blocks"); NoWrites(x);
        x = Seed(); x.Schedule.FixtureBlocks.Clear(); item = Only(Good(Colony(ReadArgs())));
        Check((int)item["totalBlockCount"] == 0 && (int)item["timetableCount"] == 0, "real_empty_block_counts"); ExplicitNull(item, "currentTimetableIndex"); NoWrites(x);
        x = Seed(); x.Schedule.FixtureBlocks[3] = null; item = Only(Good(Colony(ReadArgs())));
        Check(!(bool)item["blocks"][3]["available"], "null_slot_unavailable"); ExplicitNull((JObject)item["blocks"][3], "group"); NoWrites(x);
        foreach (int progress in new[] { -1, 3 })
        { x = Seed(72, progress, 40); item = Only(Good(Colony(ReadArgs()))); ExplicitNull(item, "currentTimetableIndex"); NoWrites(x); }
        foreach (int index in new[] { -1, 16, 72 })
        { x = Seed(72, 1, index); item = Only(Good(Colony(ReadArgs()))); ExplicitNull(item, "currentBlockIndex"); ExplicitNull(item, "currentGroup"); NoWrites(x); }
        x = Seed(); GameClock.Instance = null; body = Good(Colony(ReadArgs())); ExplicitNull(body, "currentHour"); ExplicitNull(Only(body), "currentBlockIndex"); NoWrites(x);
        Bad(Colony(WriteArgs()), "clock_unavailable"); NoWrites(x);
        x = Seed(); x.Schedule.FixtureThrowCurrentIndex = true; item = Only(Good(Colony(ReadArgs()))); ExplicitNull(item, "currentBlockIndex"); NoWrites(x);
        foreach (int hour in new[] { -1, 24 })
        { x = Seed(); ScheduleManager.FixtureCurrentHour = hour; body = Good(Colony(ReadArgs())); ExplicitNull(body, "currentHour"); Check((bool)body["partial"], "invalid_native_hour_partial"); NoWrites(x); }
        x = Seed(); ScheduleManager.FixtureCurrentHour = 0; body = Good(Colony(ReadArgs())); Check((int)body["currentHour"] == 0, "actual_native_hour_zero"); NoWrites(x);
        x = Seed(); x.Schedule.FixtureThrowBlocks = true; item = Only(Good(Colony(ReadArgs()))); ExplicitNull(item, "blocks"); ExplicitNull(item, "totalBlockCount"); Bad(Colony(WriteArgs()), "blocks_read_failed"); NoWrites(x);
        x = Seed(); x.Groups.allGroups.Add(null); body = Good(Colony(ReadArgs())); Check((bool)body["partial"], "null_group_partial"); ExplicitNull((JObject)body["groups"][4], "id"); Bad(Colony(WriteArgs()), "group_cache_partial"); NoWrites(x);
        x = Seed(); x.Schedule.name = null; item = Only(Good(Colony(ReadArgs()))); ExplicitNull(item, "name"); Check((string)item["unavailableFields"]["name"] == "schedule_name_unavailable", "missing_name_not_available"); NoWrites(x);
        x = Seed(); x.Manager.FixtureSchedules.Add(null); body = Good(Colony(ReadArgs()));
        Check(((JArray)body["schedules"]).Count == 2 && (bool)body["partial"], "partial_schedule_preserved"); ExplicitNull((JObject)body["schedules"][1], "name"); NoWrites(x);
    }
    private static void AssignedBoundaries()
    {
        var f = Seed(); var original = f.Schedule.FixtureAssigned; var first = original[0]; string blocks = Blocks(f.Schedule);
        Good(Colony(ReadArgs())); Check(ReferenceEquals(f.Schedule.FixtureAssigned, original) && original.Count == 1 && ReferenceEquals(original[0], first), "passive_assigned_list_and_refs_preserved"); NoWrites(f, blocks);
        f = Seed(); f.Schedule.FixtureSetAssigned(null); var item = Only(Good(Colony(ReadArgs()))); ExplicitNull(item, "assigned"); NoWrites(f);
        f = Seed(); f.Schedule.FixtureSetAssigned(new List<Ref<Schedulable>>()); item = Only(Good(Colony(ReadArgs()))); Check(((JArray)item["assigned"]).Count == 0, "real_empty_assigned"); NoWrites(f);
        foreach (int variant in Enumerable.Range(0, 6))
        {
            f = Seed(); var list = f.Schedule.FixtureAssigned;
            if (variant == 0) list[0] = null;
            if (variant == 1) list[0].FixtureTarget = null;
            if (variant == 2) list[0].FixtureThrow = true;
            if (variant == 3) list.Add(new Ref<Schedulable> { FixtureTarget = f.Schedulable });
            if (variant == 4) f.Schedulable.FixtureSetOwner(new UnityEngine.GameObject { FixtureName = "Foreign" });
            if (variant == 5) f.Owner.FixtureName = " ";
            var refs = list.ToArray(); item = Only(Good(Colony(ReadArgs()))); ExplicitNull(item, "assigned");
            Check(ReferenceEquals(f.Schedule.FixtureAssigned, list) && list.SequenceEqual(refs), "malformed_assigned_not_cleaned");
            Bad(Colony(WriteArgs())); NoWrites(f);
        }
    }
    private static void StrictRequests()
    {
        var invalidRows = new JToken[] { JValue.CreateNull(), new JValue("1"), new JValue(1.0), new JValue(true), new JValue(-1), new JValue(2147483648L) };
        foreach (JToken token in invalidRows)
        { var f = Seed(); var a = ReadArgs(); a["timetableIndex"] = token.DeepClone(); Bad(Colony(a)); a = WriteArgs(); a["timetableIndex"] = token.DeepClone(); Bad(Colony(a)); NoWrites(f); }
        foreach (JToken token in new JToken[] { JValue.CreateNull(), new JValue("true"), new JValue(1), new JArray() })
        { var f = Seed(); var a = ReadArgs(); a["includeTimetables"] = token.DeepClone(); Bad(Colony(a)); NoWrites(f); }
        foreach (string key in new[] { "hour", "schedule", "group", "task", "confirm", "dryRun" })
        {
            var f = Seed(); var a = WriteArgs(); a[key] = JValue.CreateNull(); Bad(Colony(a)); NoWrites(f);
            f = Seed(); a = WriteArgs(); a[key] = key == "hour" ? (JToken)new JValue("16") : new JValue(7); Bad(Colony(a)); NoWrites(f);
        }
        foreach (int hour in new[] { -1, 24 })
        { var f = Seed(); var a = WriteArgs(); a["hour"] = hour; Bad(Colony(a), "invalid_hour"); NoWrites(f); }
        foreach (string key in new[] { "schedule", "group", "task" })
        { var f = Seed(); var a = WriteArgs(); a.Remove(key); Bad(Colony(a)); a = WriteArgs(); a[key] = " "; Bad(Colony(a)); NoWrites(f); }
        var x = Seed(); var args = WriteArgs(); args["confirm"] = false; Bad(Colony(args), "confirm_required"); NoWrites(x);
        args = WriteArgs(); args["timetableIndex"] = 3; Bad(Colony(args), "timetable_out_of_range"); NoWrites(x);
        args = ReadArgs(true); args["timetableIndex"] = 3; Bad(Colony(args), "timetable_or_block_unavailable"); NoWrites(x);
        foreach (string flag in new[] { "force", "apply", "includeTimetables", "id", "worldId", "allowUnsupported" })
        { x = Seed(); args = WriteArgs(); args[flag] = false; Bad(Colony(args), "unsupported_parameter"); NoWrites(x); }
        foreach (string flag in new[] { "allowSandbox", "instantBuild", "allowForce", "allowDestroy" })
        { x = Seed(); args = WriteArgs(); args[flag] = true; Bad(Colony(args), "unsupported_flag"); NoWrites(x); }
        x = Seed(); x.Manager.FixtureSchedules.Add(new Schedule { name = "fixtureplan" }); Bad(Colony(WriteArgs()), "schedule_ambiguous"); NoWrites(x);
        x = Seed(); x.Groups.allGroups.Add(new ScheduleGroup("Other", "Hygene")); Bad(Colony(WriteArgs()), "group_ambiguous"); NoWrites(x);
        x = Seed(); args = WriteArgs(); args["schedule"] = "Missing"; Bad(Colony(args), "schedule_not_found"); args = WriteArgs(); args["group"] = "Missing"; Bad(Colony(args), "group_not_found"); NoWrites(x);
    }
    private static void Writes()
    {
        DryRunProbe(); var f = Seed(); var before = f.Schedule.FixtureBlocks.Select(b => b.GroupId).ToArray();
        var args = WriteArgs(); string original = args.ToString(Formatting.None); var result = Good(Colony(args));
        Check(f.Schedule.FixtureSetCalls == 1 && f.Schedule.FixtureNotifications == 1, "exact_one_native_set_and_notification");
        Check(f.Schedule.FixtureLastSetIndex == 40 && ReferenceEquals(f.Schedule.FixtureLastSetGroup, f.Groups.Hygene), "row1_hour16_native_index40");
        for (int i = 0; i < 72; i++) Check(f.Schedule.FixtureBlocks[i].GroupId == (i == 40 ? "Hygene" : before[i]), "only_exact_native_slot_changes");
        Check((bool)result["nativeInvoked"] && result["applied"].Type == JTokenType.Integer && (int)result["applied"] == 1 && (bool)result["changed"] && (bool)result["requestedStateObserved"], "fresh_observed_success");
        Check((int)result["blockIndex"] == 40 && (string)result["previousGroup"] == "Worktime" && (string)result["observedGroup"] == "Hygene", "actual_response_values");
        Check(args.ToString(Formatting.None) == original, "write_router_keeps_request");
        result = Good(Colony(args)); Check(!(bool)result["nativeInvoked"] && !(bool)result["changed"] && (bool)result["requestedStateObserved"], "already_matching_noop");
        Check(f.Schedule.FixtureSetCalls == 1, "noop_does_not_repeat_native"); Check(result["applied"].Type == JTokenType.Integer && (int)result["applied"] == 0, "noop_known_counter_zero");
        f = Seed(); args = WriteArgs(); args.Remove("timetableIndex"); args["hour"] = 2; result = Good(Colony(args));
        Check((int)result["blockIndex"] == 2 && f.Schedule.FixtureLastSetIndex == 2, "legacy_omitted_row_firstrow_compatible");
    }
    private static void OwnerAndNativeFailures()
    {
        var f = Seed(); f.Schedule.FixtureOnBlocksRead = () => Game.Instance = new Game(); Bad(Colony(ReadArgs()), "owner_changed"); NoWrites(f);
        f = Seed(); f.Schedule.FixtureOnBlocksRead = () => GameClock.Instance = new GameClock(); Bad(Colony(WriteArgs()), "owner_changed"); NoWrites(f);
        f = Seed(); f.Schedule.FixtureAfterSet = () => Game.Instance = new Game(); Partial(f, "owner_changed", true);
        f = Seed(); f.Schedule.FixtureAfterSet = () => f.Schedule.name = "Renamed"; Partial(f, "owner_or_target_changed", true);
        f = Seed(); f.Schedule.FixtureAfterSet = () => f.Schedule.FixtureBlocks = new List<ScheduleBlock>(f.Schedule.FixtureBlocks); Partial(f, "owner_or_target_changed", true);
        f = Seed(); f.Schedule.FixtureAfterSet = () => f.Schedule.FixtureSetAssigned(new List<Ref<Schedulable>> { new Ref<Schedulable> { FixtureTarget = NewAssigned("Fixture minion") } }); Partial(f, "assigned_targets_changed", true);
        f = Seed(); f.Schedule.FixtureAfterSet = () => f.Schedule.FixtureBlocks[40].FixtureNativeSet(f.Groups.Worktime); Partial(f, "native_result_unverified", true);
        f = Seed(); f.Schedule.FixtureThrowSetBeforeMutation = true; Partial(f, "native_call_failed", null); Check(f.Schedule.FixtureNotifications == 0, "throw_before_no_notification");
        f = Seed(); f.Schedule.FixtureThrowSetAfterMutation = true; Partial(f, "native_call_failed", null); Check(f.Schedule.FixtureNotifications == 1, "throw_after_one_notification");
    }
    private static void Partial(Fixture f, string reason, bool? accepted)
    {
        var result = Colony(WriteArgs()); Check(result.IsError, "partial_is_error"); var body = Body(result);
        Check((string)body["status"] == "partial" && (string)body["reasonCode"] == reason, "partial_reason_" + reason);
        Check((bool)body["nativeInvoked"] && f.Schedule.FixtureSetCalls == 1, "partial_one_native_attempt_no_retry");
        Check(body.Property("applied") == null, "partial_counter_unknown_is_absent_not_zero_or_null");
        foreach (string key in new[] { "changed", "requestedStateObserved" }) ExplicitNull(body, key);
        if (!accepted.HasValue) ExplicitNull(body, "accepted"); else Check((bool)body["accepted"] == accepted.Value, "partial_acceptance_separate");
    }
    private static object ActualOutcome(string name, CallToolResult result)
    {
        var method = typeof(WorldEditorTools).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Check(method != null, "actual_private_outcome_method_" + name);
        return method.Invoke(null, new object[] { result });
    }
    private static void Classify(CallToolResult result, int? applied, bool? changed, bool partial, int fallback)
    {
        var outcome = JObject.Parse(JsonConvert.SerializeObject(ActualOutcome("NativeOutcome", result), McpJsonUtil.Settings));
        Check((bool)outcome["observed"] && outcome["reasonCode"].Type == JTokenType.Null, "actual_outcome_valid_native_schema CHILD=" + result.Content[0].Text + " OUTCOME=" + outcome.ToString(Formatting.None));
        var count = (JObject)outcome["counters"];
        if (applied.HasValue) Check(count["applied"]?.Type == JTokenType.Integer && (int)count["applied"] == applied.Value, "actual_outcome_counter");
        else Check(count.Property("applied") == null, "unknown_counter_stays_missing_not_zero");
        if (changed.HasValue) Check((bool?)outcome["changed"] == changed, "actual_outcome_changed_boolean");
        else ExplicitNull(outcome, "changed");
        Check((bool)ActualOutcome("ResultReportsPartial", result) == partial, "actual_partial_classification");
        Check((int)ActualOutcome("ResultAppliedCount", result) == fallback, "actual_applied_count_legacy_fallback");
    }
    private static void OutcomeIntegration()
    {
        var f = Seed(); var preview = ScheduleTools.SetScheduleBlock().Handler(WriteArgs(true));
        Classify(preview, 0, false, false, 0); NoWrites(f);
        f = Seed(); var args = WriteArgs(); args["group"] = "Worktime";
        var noop = Colony(args); Classify(noop, 0, false, false, 0); NoWrites(f);
        f = Seed(); var observed = Colony(WriteArgs()); Classify(observed, 1, true, false, 1);
        Check(f.Schedule.FixtureSetCalls == 1, "classifier_does_not_repeat_native");
        f = Seed(); f.Schedule.FixtureThrowSetAfterMutation = true; var unknown = Colony(WriteArgs());
        Classify(unknown, null, null, true, 0); Check(f.Schedule.FixtureSetCalls == 1, "unknown_classification_no_native_retry");
        f = Seed(); args = WriteArgs(); args["confirm"] = false; var refusal = Colony(args);
        Classify(refusal, 0, false, false, 0); NoWrites(f);
        foreach (JToken malformed in new JToken[] { new JValue(false), JValue.CreateNull(), new JValue(-1), new JValue("1") })
        {
            var invalid = CallToolResult.Text(new JObject { ["applied"] = malformed, ["changed"] = false }.ToString(Formatting.None));
            var outcome = JObject.Parse(JsonConvert.SerializeObject(ActualOutcome("NativeOutcome", invalid), McpJsonUtil.Settings));
            Check((string)outcome["reasonCode"] == "invalid_native_fields", "actual_parser_rejects_invalid_counter_" + malformed.Type);
        }
    }
    private static void DryRunProbe()
    {
        var f = Seed(); f.Schedule.FixtureThrowGetAssigned = false; string original = Blocks(f.Schedule); var args = WriteArgs(true);
        // Direct actual set caller: old whole ScheduleTools ignores dryRun and
        // reaches native SetBlockGroup. Cached Db.Get deliberately returns.
        CallToolResult result = ScheduleTools.SetScheduleBlock().Handler(args);
        Check(f.Schedule.FixtureSetCalls == 0, "dry_run_dispatched_native_set");
        Check(f.Schedule.FixtureNotifications == 0, "dry_run_native_notification");
        Check(Blocks(f.Schedule) == original, "dry_run_changed_supplied_blocks");
        var body = Good(result); Check((string)body["status"] == "preview" && (int)body["blockIndex"] == 40, "actual_preview_row_resolution"); NoWrites(f, original);
    }
    private static Schedulable NewAssigned(string name)
    {
        var owner = new UnityEngine.GameObject { FixtureName = name }; var s = new Schedulable();
        owner.FixtureAdd(s); owner.FixtureAdd(new MinionIdentity()); return s;
    }
    private static Fixture Seed(int blockCount = 72, int progress = 1, int currentIndex = 40)
    {
        Db.FixtureGetCalls = 0; Db.FixtureThrowGet = false; FixtureRoutes.Calls = 0; Components.LiveMinionIdentities.Items.Clear();
        var work = new ScheduleGroup("Worktime", "Fixture work"); var hygiene = new ScheduleGroup("Hygene", "Fixture hygiene");
        var sleep = new ScheduleGroup("Sleep", "Fixture sleep"); var recreation = new ScheduleGroup("Recreation", "Fixture recreation");
        var groups = new Database.ScheduleGroups { allGroups = new List<ScheduleGroup> { work, hygiene, sleep, recreation }, Worktime = work, Hygene = hygiene, Sleep = sleep, Recreation = recreation };
        Db.FixtureSetExisting(new Db { ScheduleGroups = groups }); var game = new Game(); var gameOwner = new UnityEngine.GameObject { FixtureName = "Fixture game" }; gameOwner.FixtureAdd(game); Game.Instance = game; GameClock.Instance = new GameClock();
        var schedulable = NewAssigned("Fixture minion"); var owner = schedulable.gameObject; owner.FixtureAdd(new KPrefabID { InstanceID = 7001 }); Components.LiveMinionIdentities.Items.Add(owner.GetComponent<MinionIdentity>());
        var blocks = new List<ScheduleBlock>(); for (int i = 0; i < blockCount; i++) blocks.Add(new ScheduleBlock("Worktime", "Fixture work"));
        if (blockCount > 16) blocks[16] = new ScheduleBlock("Hygene", "Fixture hygiene");
        var schedule = new Schedule { name = "FixturePlan", FixtureThrowGetAssigned = true };
        schedule.FixtureSeed(blocks, progress, currentIndex, currentIndex >= 0 && currentIndex < blocks.Count ? blocks[currentIndex] : null, new List<Ref<Schedulable>> { new Ref<Schedulable> { FixtureTarget = schedulable } });
        schedulable.FixtureSchedule = schedule; var manager = new ScheduleManager { FixtureSchedules = new List<Schedule> { schedule } }; ScheduleManager.Instance = manager; ScheduleManager.FixtureCurrentHour = currentIndex == 0 ? 0 : 16;
        return new Fixture { Schedule = schedule, Groups = groups, Owner = owner, Schedulable = schedulable, Manager = manager };
    }
    private sealed class Fixture
    { public Schedule Schedule; public Database.ScheduleGroups Groups; public UnityEngine.GameObject Owner; public Schedulable Schedulable; public ScheduleManager Manager; }
}
