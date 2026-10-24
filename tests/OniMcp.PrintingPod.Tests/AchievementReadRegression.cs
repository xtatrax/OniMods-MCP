using System;
using System.Collections.Generic;
using System.Linq;
using Database;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

// These checks call the linked whole production readers. Native stubs only supply data/poisoned APIs.
internal static class AchievementReadRegression
{
    private const string CacheSource = "SaveGame.ColonyAchievementTracker.achievements";
    private const string CacheMode = "cached_no_reevaluation";
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception("achievement_cached_read:" + message);
    }
    private static void Null(JToken value, string message) => Check(value != null && value.Type == JTokenType.Null, message);
    private static void Keys(JObject value, params string[] expected) =>
        Check(value.Properties().Select(p => p.Name).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), "unexpected row fields");
    private static ColonyAchievement Definition(string id, bool victory = true)
    {
        return new ColonyAchievement {
            Id = id, Name = id, description = "<i>description</i>", isVictoryCondition = victory,
            requirementChecklist = new List<ColonyAchievementRequirement> { new PoisonAchievementRequirement() }
        };
    }
    private static void Fresh()
    {
        AchievementNativePoison.Evaluations = AchievementNativePoison.UiActions = AchievementNativePoison.StatusConstructions = 0;
        Db.ThrowOnGet = true;
        Db.GetCalls = 0;
        Db.Instance = new Db();
        SaveGame.Instance = new SaveGame { ColonyAchievementTracker = new ColonyAchievementTracker() };
        Game.Instance = new Game();
        GameClock.Instance = new GameClock { Cycle = 40 };
        Immigration.Instance = null;
        GameFlowManager.Instance = null;
        ManagementMenu.Instance = null;
        Components.MinionResumes.Items.Clear();
        Components.Telepads.Items.Clear();
        Components.Telepads.Items.Add(FacilitySideScreenTools.Target);
    }
    private static void Cache(string id, bool success, bool failed)
    {
        SaveGame.Instance.ColonyAchievementTracker.achievements.Add(id,
            new ColonyAchievementStatus(id) { success = success, failed = failed });
    }
    private static JObject ReadMain(JObject args = null)
    {
        var result = ColonyAchievementTools.Read(args ?? new JObject());
        Check(!result.IsError, "main reader unexpectedly unavailable");
        return JObject.Parse(result.Content[0].Text);
    }
    private static JArray Victory() => JArray.FromObject(FacilitySideScreenTools.VictoryRowsForAchievementTest());
    private static JObject Row(JArray rows, string id) => (JObject)rows.Single(r => (string)r["id"] == id);
    private static JArray Items(JObject result) => (JArray)result["items"];
    private static void Status(JObject row, string status, bool? success, bool? failed, string successKey)
    {
        Check((string)row["status"] == status, "wrong cached status");
        if (success.HasValue) Check((bool?)row[successKey] == success, "wrong observed success flag");
        else Null(row[successKey], "missing cached success must stay explicit null");
        if (failed.HasValue) Check((bool?)row["failed"] == failed, "wrong observed failed flag");
        else Null(row["failed"], "missing cached failure must stay explicit null");
    }
    private static void Counts(JObject result, int completed, int incomplete, int failed, int unknown)
    {
        var counts = result["counts"];
        Check((int)counts["completed"] == completed && (int)counts["incomplete"] == incomplete
            && (int)counts["failed"] == failed && (int)counts["unknown"] == unknown, "counts changed with filtering or cache precedence");
    }
    private sealed class Snapshot
    {
        private readonly SaveGame save = SaveGame.Instance;
        private readonly ColonyAchievementTracker tracker = SaveGame.Instance?.ColonyAchievementTracker;
        private readonly Dictionary<string, ColonyAchievementStatus> cache;
        private readonly KeyValuePair<string, ColonyAchievementStatus>[] entries;
        private readonly bool?[] successes, failures;
        private readonly List<string> queue;
        private readonly string[] queued;
        private readonly List<ColonyAchievement> definitions = Db.Instance?.ColonyAchievements?.resources;
        private readonly ColonyAchievement[] definitionEntries;
        private readonly List<ColonyAchievementRequirement>[] requirements;
        private readonly ColonyAchievementRequirement[][] requirementEntries;
        private readonly int constructions = AchievementNativePoison.StatusConstructions;
        private readonly int? cycle = GameClock.Instance?.Cycle;
        internal Snapshot()
        {
            cache = tracker?.achievements;
            entries = cache?.ToArray();
            successes = entries?.Select(e => (bool?)e.Value?.success).ToArray();
            failures = entries?.Select(e => (bool?)e.Value?.failed).ToArray();
            queue = tracker?.achievementsToDisplay;
            queued = queue?.ToArray();
            definitionEntries = definitions?.ToArray();
            requirements = definitionEntries?.Select(d => d.requirementChecklist).ToArray();
            requirementEntries = requirements?.Select(r => r?.ToArray()).ToArray();
        }
        internal void AssertUnchanged()
        {
            Check(ReferenceEquals(save, SaveGame.Instance) && ReferenceEquals(tracker, save?.ColonyAchievementTracker), "reader replaced native owner/tracker");
            Check(ReferenceEquals(cache, tracker?.achievements), "reader replaced cache");
            if (cache != null) {
                Check(cache.SequenceEqual(entries), "reader created/replaced/removed cached status");
                Check(entries.Select(e => (bool?)e.Value?.success).SequenceEqual(successes)
                    && entries.Select(e => (bool?)e.Value?.failed).SequenceEqual(failures), "reader rewrote cached flags");
            }
            Check(ReferenceEquals(queue, tracker?.achievementsToDisplay) && (queue == null || queue.SequenceEqual(queued)), "reader changed achievement queue");
            Check(ReferenceEquals(definitions, Db.Instance?.ColonyAchievements?.resources)
                && (definitions == null || definitions.SequenceEqual(definitionEntries)), "reader changed definition list");
            if (definitions != null) for (int i = 0; i < definitions.Count; i++)
                Check(ReferenceEquals(requirements[i], definitions[i].requirementChecklist)
                    && (requirements[i] == null || requirements[i].SequenceEqual(requirementEntries[i])), "reader changed requirement list");
            Check(AchievementNativePoison.StatusConstructions == constructions, "reader constructed native cache entries");
            Check(GameClock.Instance?.Cycle == cycle, "reader changed observed cycle");
            Check(AchievementNativePoison.Evaluations == 0 && AchievementNativePoison.UiActions == 0, "reader evaluated or opened native UI");
            Check(Db.GetCalls == 0, "reader invoked lazy Db.Get");
        }
    }
    private static void Guarded(Action read)
    {
        var before = new Snapshot();
        read();
        before.AssertUnchanged();
    }

    internal static void Run()
    {
        Console.WriteLine("AchievementReadRegression: linked full Facility and Colony readers, poisoned native evaluation");
        FailurePrecedenceAcrossCachedReads();
        KnownStatesAndActualTelepadWire();
        MissingCachesAndKnownEmpty();
        RequirementsAndFullShape();
        FiltersLimitsAndErrors();
        Console.WriteLine("PASS: actual achievement cached readers, " + checks + " boundary checks; native game/UI not exercised");
    }
    private static void FailurePrecedenceAcrossCachedReads()
    {
        Fresh();
        const string id = "BOTH_FLAGS";
        Db.Instance.ColonyAchievements.resources.Add(Definition(id));
        Cache(id, true, true);
        Guarded(() => {
            var victory = Victory();
            Check(victory.Count == 1 && (string)Row(victory, id)["status"] == "failed",
                "both_true_failure_priority: cached failure must override success on victory reads");
            Status(Row(victory, id), "failed", true, true, "success");
            foreach (var detail in new[] { "brief", "full" }) {
                var result = ReadMain(new JObject { ["achievementDetail"] = detail });
                Counts(result, 0, 0, 1, 0);
                Check((int)result["matched"] == 1 && (int)result["returned"] == 1 && !(bool)result["truncated"],
                    "both_true_failure_priority: unfiltered row must remain present");
                Status(Row(Items(result), id), "failed", true, true, "completed");
            }
            var failed = ReadMain(new JObject { ["achievementStatus"] = "failed" });
            Counts(failed, 0, 0, 1, 0);
            Check((int)failed["matched"] == 1 && (int)failed["returned"] == 1,
                "both_true_failure_priority: failed filter must include the conflicting cached flags");
            Status(Row(Items(failed), id), "failed", true, true, "completed");
            var completed = ReadMain(new JObject { ["achievementStatus"] = "completed" });
            Counts(completed, 0, 0, 1, 0);
            Check((int)completed["matched"] == 0 && (int)completed["returned"] == 0 && !Items(completed).Any()
                && !(bool)completed["truncated"],
                "both_true_failure_priority: completed filter must exclude the conflicting cached flags");
        });
    }
    private static void KnownStatesAndActualTelepadWire()
    {
        Fresh();
        var definitions = Db.Instance.ColonyAchievements.resources;
        foreach (var id in new[] { "NULL_CACHED", "COMPLETE", "MISSING", "FAIL", "BOTH", "INCOMPLETE" }) definitions.Add(Definition(id));
        definitions.Single(d => d.Id == "COMPLETE").Name = "<b>Winner</b>";
        definitions.Add(Definition("ORDINARY", false));
        definitions.Add(Definition("DISABLED")); definitions.Last().Disabled = true;
        definitions.Add(Definition("INCOMPATIBLE")); definitions.Last().ValidForSave = false;
        Cache("COMPLETE", true, false); Cache("FAIL", false, true); Cache("BOTH", true, true);
        Cache("INCOMPLETE", false, false); Cache("ORDINARY", false, false);
        SaveGame.Instance.ColonyAchievementTracker.achievements.Add("NULL_CACHED", null);
        SaveGame.Instance.ColonyAchievementTracker.achievementsToDisplay.AddRange(new[] { "queued-a", "queued-b" });
        // A warm-cache first probe lets the old whole Facility reach its Success poison.
        // Any successful read must still prove zero Db.Get calls; missing-cache probes poison it.
        Db.ThrowOnGet = false;
        Guarded(() => {
            var victory = Victory();
            Check(Db.GetCalls == 0, "warm cached read invoked Db.Get");
            Db.ThrowOnGet = true;
            Check(victory.Count == 6 && victory.Select(r => (string)r["id"]).SequenceEqual(definitions.Take(6).Select(d => d.Id)), "victory filter/order changed");
            Status(Row(victory, "COMPLETE"), "completed", true, false, "success");
            Status(Row(victory, "FAIL"), "failed", false, true, "success");
            Status(Row(victory, "BOTH"), "failed", true, true, "success");
            Status(Row(victory, "INCOMPLETE"), "incomplete", false, false, "success");
            Status(Row(victory, "MISSING"), "unknown", null, null, "success");
            Status(Row(victory, "NULL_CACHED"), "unknown", null, null, "success");
            Check((string)Row(victory, "COMPLETE")["name"] == "<b>Winner</b>", "victory native name changed");
            foreach (JObject row in victory) {
                Keys(row, "id", "name", "success", "failed", "status", "source", "stateMode", "requirements");
                Check((string)row["source"] == CacheSource && (string)row["stateMode"] == CacheMode, "victory provenance absent");
            }
            var main = ReadMain();
            Counts(main, 1, 2, 2, 2);
            Check((string)main["source"] == CacheSource && (string)main["stateMode"] == CacheMode
                && (string)main["scope"] == "current_colony_valid_achievements" && (int)main["observedCycle"] == 40, "main provenance changed");
            Check((int)main["matched"] == 7 && (int)main["returned"] == 7 && !(bool)main["truncated"], "brief counts changed");
            foreach (JObject row in Items(main)) Keys(row, "id", "name", "status", "completed", "failed", "isVictoryCondition");
            Check(Items(main).Select(r => (string)r["id"]).SequenceEqual(new[] { "BOTH", "COMPLETE", "FAIL", "INCOMPLETE", "MISSING", "NULL_CACHED", "ORDINARY" }), "main sorting/filter changed");
            Status(Row(Items(main), "COMPLETE"), "completed", true, false, "completed");
            Status(Row(Items(main), "FAIL"), "failed", false, true, "completed");
            Status(Row(Items(main), "BOTH"), "failed", true, true, "completed");
            Status(Row(Items(main), "INCOMPLETE"), "incomplete", false, false, "completed");
            Status(Row(Items(main), "MISSING"), "unknown", null, null, "completed");
            Status(Row(Items(main), "NULL_CACHED"), "unknown", null, null, "completed");
            Check((string)Row(Items(main), "COMPLETE")["name"] == "Winner", "main plain name changed");
            Check((bool)Row(Items(main), "ORDINARY")["isVictoryCondition"] == false, "ordinary achievements lost their main-list classification");

            var telepadResult = FacilitySideScreenTools.ListTelepads().Handler(new JObject());
            Check(!telepadResult.IsError, "actual telepad handler failed");
            var telepads = JObject.Parse(telepadResult.Content[0].Text);
            Check((int)telepads["returned"] == 1 && (int)telepads["telepads"][0]["newAchievementsQueued"] == 2, "telepad metadata changed");
            Check(JToken.DeepEquals(telepads["telepads"][0]["victoryConditions"], victory), "telepad did not forward actual cached projection");
        });
        Db.ThrowOnGet = true;
        var priorDb = Db.Instance;
        Db.Instance = null;
        try {
            var noVictory = FacilitySideScreenTools.ListTelepads().Handler(new JObject { ["includeVictory"] = false });
            Check(!noVictory.IsError && JObject.Parse(noVictory.Content[0].Text)["telepads"][0]["victoryConditions"] == null, "includeVictory=false should omit and avoid achievement definitions");
            Check(Db.GetCalls == 0, "includeVictory=false invoked Db.Get");
        } finally { Db.Instance = priorDb; Db.ThrowOnGet = true; }
    }
    private static void MissingCachesAndKnownEmpty()
    {
        Fresh();
        Db.Instance.ColonyAchievements.resources.Add(Definition("UNOBSERVED"));
        SaveGame.Instance = null;
        Guarded(() => {
            Status(Row(Victory(), "UNOBSERVED"), "unknown", null, null, "success");
            Check(ColonyAchievementTools.Read(new JObject()).IsError, "main missing owner must be unavailable");
        });
        SaveGame.Instance = new SaveGame();
        Guarded(() => {
            Status(Row(Victory(), "UNOBSERVED"), "unknown", null, null, "success");
            Check(ColonyAchievementTools.Read(new JObject()).IsError, "main missing tracker must be unavailable");
        });
        SaveGame.Instance.ColonyAchievementTracker = new ColonyAchievementTracker { achievements = null };
        Guarded(() => {
            Status(Row(Victory(), "UNOBSERVED"), "unknown", null, null, "success");
            var unknown = ReadMain(); Counts(unknown, 0, 0, 0, 1);
            Status(Row(Items(unknown), "UNOBSERVED"), "unknown", null, null, "completed");
        });
        SaveGame.Instance.ColonyAchievementTracker.achievements = new Dictionary<string, ColonyAchievementStatus>();
        Guarded(() => Status(Row(Victory(), "UNOBSERVED"), "unknown", null, null, "success"));
        Db.Instance.ColonyAchievements.resources.Clear();
        Guarded(() => {
            var empty = ReadMain(); Counts(empty, 0, 0, 0, 0);
            Check((int)empty["matched"] == 0 && Items(empty).Count == 0 && Victory().Count == 0, "known empty definitions must return zero rows");
            var telepad = FacilitySideScreenTools.ListTelepads().Handler(new JObject());
            Check(!telepad.IsError, "telepad initialized-empty catalog failed");
            var wireRow = (JObject)JObject.Parse(telepad.Content[0].Text)["telepads"][0];
            Check(wireRow.Property("victoryConditions") != null && wireRow["victoryConditions"] is JArray array && array.Count == 0, "telepad initialized-empty catalog must be present empty array");
        });
        Db.Instance.ColonyAchievements.resources = null;
        Guarded(() => {
            Check(ColonyAchievementTools.Read(new JObject()).IsError && FacilitySideScreenTools.VictoryRowsForAchievementTest() == null, "absent definition cache must remain unknown");
            var telepad = FacilitySideScreenTools.ListTelepads().Handler(new JObject());
            Check(!telepad.IsError, "telepad metadata should still be readable without achievement catalog");
            var wireRow = (JObject)JObject.Parse(telepad.Content[0].Text)["telepads"][0];
            Check(wireRow.Property("victoryConditions") != null, "telepad missing catalog key must be present");
            Null(wireRow["victoryConditions"], "telepad missing catalog must be explicit null");
        });
        Db.Instance.ColonyAchievements = null;
        Guarded(() => Check(ColonyAchievementTools.Read(new JObject()).IsError && FacilitySideScreenTools.VictoryRowsForAchievementTest() == null, "absent achievement catalog must remain unknown"));
        Db.Instance = null;
        Guarded(() => Check(ColonyAchievementTools.Read(new JObject()).IsError && FacilitySideScreenTools.VictoryRowsForAchievementTest() == null, "absent database must remain unknown"));
        Fresh(); Game.Instance = null;
        Check(ColonyAchievementTools.Read(new JObject()).IsError, "main missing game must be unavailable");
        Check(FacilitySideScreenTools.ListTelepads().Handler(new JObject()).IsError, "telepad missing game must be unavailable");
    }
    private static void RequirementsAndFullShape()
    {
        Fresh();
        var nullList = Definition("NULL_LIST"); nullList.requirementChecklist = null;
        var emptyList = Definition("EMPTY_LIST"); emptyList.requirementChecklist.Clear();
        var nullReq = Definition("NULL_REQ"); nullReq.requirementChecklist = new List<ColonyAchievementRequirement> { null };
        var poison = Definition("POISON_REQ");
        Db.Instance.ColonyAchievements.resources.AddRange(new[] { nullList, emptyList, nullReq, poison });
        foreach (var d in Db.Instance.ColonyAchievements.resources) Cache(d.Id, false, false);
        Guarded(() => {
            var victory = Victory();
            Null(Row(victory, "NULL_LIST")["requirements"], "null checklist must remain null");
            Check(!Row(victory, "EMPTY_LIST")["requirements"].Any(), "empty checklist must remain an array");
            var missing = (JObject)Row(victory, "NULL_REQ")["requirements"][0];
            Keys(missing, "type", "success", "successState", "progress", "progressState");
            Null(missing["type"], "null requirement must not invent type");
            Null(missing["success"], "null requirement must not invent success");
            Null(missing["progress"], "null requirement must not invent progress");
            Check((string)missing["successState"] == "unavailable" && (string)missing["progressState"] == "unavailable", "null requirement must be unavailable");
            var present = (JObject)Row(victory, "POISON_REQ")["requirements"][0];
            Check((string)present["type"] == nameof(PoisonAchievementRequirement), "requirement type missing");
            Null(present["success"], "requirement success must not be evaluated");
            Null(present["progress"], "requirement progress must not be evaluated");
            Check((string)present["successState"] == "not_evaluated_readonly" && (string)present["progressState"] == "not_evaluated_readonly", "unevaluated requirement states missing");

            var full = ReadMain(new JObject { ["achievementDetail"] = " FULL " });
            foreach (JObject row in Items(full)) Keys(row, "id", "name", "status", "completed", "failed", "isVictoryCondition", "description", "requirements");
            Check((string)Row(Items(full), "POISON_REQ")["description"] == "description", "full plain description changed");
            Null(Row(Items(full), "NULL_LIST")["requirements"], "full null checklist must remain null");
            Check(!Row(Items(full), "EMPTY_LIST")["requirements"].Any(), "full empty checklist must remain empty array");
            var fullMissing = (JObject)Row(Items(full), "NULL_REQ")["requirements"][0];
            Keys(fullMissing, "type", "progress", "progressState");
            Null(fullMissing["type"], "main null requirement must not invent type");
            Null(fullMissing["progress"], "main null requirement must not invent progress");
            Check((string)fullMissing["progressState"] == "unavailable", "main null requirement state");
            var fullPresent = (JObject)Row(Items(full), "POISON_REQ")["requirements"][0];
            Keys(fullPresent, "type", "progress", "progressState");
            Null(fullPresent["progress"], "main progress must stay null");
            Check((string)fullPresent["progressState"] == "not_evaluated_readonly", "main progress state changed");
        });
    }
    private static void FiltersLimitsAndErrors()
    {
        Fresh();
        foreach (var id in new[] { "A", "B", "C", "D", "E" }) Db.Instance.ColonyAchievements.resources.Add(Definition(id));
        Db.Instance.ColonyAchievements.resources[1].Name = "<b>SearchTarget</b>";
        Cache("A", true, true); Cache("B", true, false); Cache("C", false, true); Cache("D", false, false);
        Guarded(() => {
            foreach (var filter in new[] { "all", "completed", "failed", "incomplete", "unknown" }) {
                var result = ReadMain(new JObject { ["achievementStatus"] = filter });
                Counts(result, 1, 1, 2, 1);
                Check(Items(result).All(r => filter == "all" || (string)r["status"] == filter), "status filter escaped");
                Check((int)result["matched"] == (filter == "all" ? 5 : filter == "failed" ? 2 : 1), "filter matched count changed");
            }
            var query = ReadMain(new JObject { ["query"] = "searchTARGET", ["achievementDetail"] = "full" });
            Check(Items(query).Count == 1 && (string)Items(query)[0]["id"] == "B", "query must use case-insensitive plain name");
            Counts(query, 1, 1, 2, 1);
            var limited = ReadMain(new JObject { ["limit"] = 1 });
            Check((int)limited["matched"] == 5 && (int)limited["returned"] == 1 && (bool)limited["truncated"], "limit must not change matched count");
            Check((int)ReadMain(new JObject { ["limit"] = 0 })["returned"] == 1, "minimum limit changed");
            Check((int)ReadMain(new JObject { ["limit"] = 1000 })["returned"] == 5, "large requested limit lost matching rows");
            Check(ColonyAchievementTools.Read(new JObject { ["achievementDetail"] = "invalid" }).IsError, "invalid detail must fail");
            Check(ColonyAchievementTools.Read(new JObject { ["achievementStatus"] = "invalid" }).IsError, "invalid status must fail");
        });
        Fresh();
        for (int i = 0; i < 201; i++) {
            var definition = Definition("DEFAULT_" + i.ToString("D3"));
            definition.requirementChecklist.Clear();
            Db.Instance.ColonyAchievements.resources.Add(definition);
        }
        Guarded(() => {
            var full = ReadMain(new JObject { ["achievementDetail"] = "full" });
            Check((int)full["matched"] == 201 && (int)full["returned"] == 10 && (bool)full["truncated"], "full default limit changed");
            var brief = ReadMain();
            Check((int)brief["matched"] == 201 && (int)brief["returned"] == 100 && (bool)brief["truncated"], "brief default limit changed");
            var capped = ReadMain(new JObject { ["limit"] = 1000 });
            Check((int)capped["matched"] == 201 && (int)capped["returned"] == 200 && (bool)capped["truncated"], "maximum limit changed");
        });
        Fresh(); GameClock.Instance = null;
        Guarded(() => Null(ReadMain()["observedCycle"], "missing clock must stay null"));
    }
}
