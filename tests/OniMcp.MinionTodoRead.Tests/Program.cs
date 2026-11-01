using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using OniMcp.Support;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string reason)
    { checks++; if (!condition) throw new InvalidOperationException("Minion TODO cache: " + reason); }
    private static Chore MakeChore(string name)
    {
        return new StandardChoreBase { Name = name, choreType = new ChoreType { Id = "FixtureChoreType", groups = new ChoreGroup[0] } };
    }
    private static Chore.Precondition.Context Context(string name, int nativeOrder, int score, bool potential)
    {
        return new Chore.Precondition.Context { chore = name == null ? null : MakeChore(name),
            FixtureOrder = nativeOrder, priority = score, FixturePotential = potential, failedPreconditionId = -1 };
    }
    private static MinionIdentity Fixture(bool dirty, out ChoreConsumer consumer)
    {
        var minion = new MinionIdentity();
        consumer = new ChoreConsumer { Owner = minion, choreDriver = new ChoreDriver() };
        minion.gameObject.Components[typeof(ChoreConsumer)] = consumer;
        minion.gameObject.Components[typeof(Schedulable)] = new Schedulable
        { Schedule = new Schedule { Current = new Schedule.Block { name = "Fixture schedule block" } } };
        consumer.choreDriver.Current = MakeChore("Current chore");
        consumer.choreDriver.Current.driver = consumer.choreDriver;
        consumer.Shared.failedContexts.AddRange(new[] { Context("Blocked C",3,0,true), Context(null,0,0,true),
            Context("Blocked A",1,0,false), Context("Blocked B",2,0,true) });
        consumer.Shared.succeededContexts.AddRange(new[] { Context("Task low",0,10,true), Context(null,0,100,true),
            Context("Task high",0,50,true), Context("Task medium",0,30,true) });
        consumer.Shared.FixtureDirty(dirty);
        return minion;
    }
    private static string[] Names(JObject result, string key)
    { return ((JArray)result[key]).Select(value => (string)value["name"]).ToArray(); }
    private static void Main()
    {
        TestDirtyPreconditionsReadOnly();
        TestSucceededPendingCache();
        TestUnavailableBoundaries();
        TestReadyNativeDecisionAndInvalidIndex();
        TestUnknownPotentialFilteringAndLocalization();
        ChorePreconditions.FixtureReset(true);
        int scenarios = 0;
        foreach (bool dirty in new[] { false, true })
        foreach (bool includeBlocked in new[] { false, true })
        foreach (bool potentialOnly in new[] { false, true })
        foreach (int limit in new[] { 1, 9 })
        {
            scenarios++;
            ChoreConsumer consumer;
            var minion = Fixture(dirty, out consumer);
            var snapshot = consumer.Shared;
            var failedList = snapshot.failedContexts; var succeededList = snapshot.succeededContexts;
            var failedBefore = failedList.ToArray(); var succeededBefore = succeededList.ToArray();
            string label = "dirty=" + dirty + ",includeBlocked=" + includeBlocked + ",potentialOnly=" + potentialOnly + ",limit=" + limit;
            JObject first = null;
            for (int read = 0; read < 2; read++)
            {
                Chore.CompareReads = 0;
                var result = JObject.FromObject(FacilitySideScreenTools.TodoForTest(minion, includeBlocked, potentialOnly, limit));
                Check(ReferenceEquals(consumer.Shared, snapshot) && ReferenceEquals(snapshot.failedContexts, failedList)
                    && ReferenceEquals(snapshot.succeededContexts, succeededList)
                    && failedList.SequenceEqual(failedBefore) && succeededList.SequenceEqual(succeededBefore)
                    && snapshot.doFailedContextsNeedSorting == dirty && snapshot.FlagWrites == 0,
                    "read must preserve shared snapshot, list references/order/content and dirty flag: " + label + ",read=" + read);
                string[] expectedBlocked = !includeBlocked ? new string[0]
                    : dirty ? potentialOnly ? new[] { "Blocked B", "Blocked C" } : new[] { "Blocked A", "Blocked B", "Blocked C" }
                    : potentialOnly ? new[] { "Blocked C", "Blocked B" } : new[] { "Blocked C", "Blocked A", "Blocked B" };
                Check(Names(result,"blocked").SequenceEqual(expectedBlocked.Take(limit))
                    && Names(result,"tasks").SequenceEqual(new[] { "Task high", "Task medium", "Task low" }.Take(limit)),
                    "returned ordering/filter/limit retains previous semantics without sorting the shared list: " + label);
                Check((int)result["counts"]["succeeded"] == 4 && (int)result["counts"]["blocked"] == 4
                    && (int)result["counts"]["returnedTasks"] == Math.Min(3,limit)
                    && (int)result["counts"]["returnedBlocked"] == Math.Min(expectedBlocked.Length,limit)
                    && (string)result["current"]["name"] == "Current chore" && (bool)result["current"]["isCurrent"]
                    && (string)result["current"]["fixtureCurrentSummary"] == "preserved"
                    && (string)result["currentScheduleBlock"] == "Fixture schedule block",
                    "native total counts, current chore summary and schedule remain intact: " + label);
                Check((Chore.CompareReads > 0) == (dirty && includeBlocked),
                    "native comparator is needed only for included dirty blocked results: " + label);
                Check(result["tasks"].All(item => (bool)item["success"]) && result["blocked"].All(item => !(bool)item["success"]),
                    "succeeded/blocked classification remains separate: " + label);
                if (first == null) first = result;
                else Check(JToken.DeepEquals(first,result),"repeated reads return the same view without clearing native dirty state: " + label);
            }
            Check(consumer.SnapshotReads == 2, "each read observes existing snapshot once without a refresh loop: " + label);
        }
        TestFailedReasonProjection();
        TestMissingAndEmpty();
        Console.WriteLine("Minion TODO cache: " + scenarios + " option scenarios read twice, empty/missing cases; " + checks
            + " checks passed (full product reader and passive cache adapter; native comparator/eligibility and cache boundaries substituted)");
    }
    private static void TestFailedReasonProjection()
    {
        ChoreConsumer consumer; var minion = Fixture(true,out consumer);
        var blocked = consumer.Shared.failedContexts[0];
        blocked.failedPreconditionId = 1;
        blocked.chore.Preconditions.Add(new Chore.PreconditionInstance { condition = new Chore.Condition
            { id = "Fixture earlier rule", description = "Fixture earlier description" } });
        blocked.chore.Preconditions.Add(new Chore.PreconditionInstance { condition = new Chore.Condition
            { id = "Fixture selected rule", description = "Fixture selected description" } });
        consumer.Shared.failedContexts[0] = blocked;
        var before = consumer.Shared.failedContexts.ToArray();
        var result = JObject.FromObject(FacilitySideScreenTools.TodoForTest(minion,true,false,9));
        var row = ((JArray)result["blocked"]).Single(item => (string)item["name"] == "Blocked C");
        Check(Names(result,"blocked").SequenceEqual(new[] { "Blocked A", "Blocked B", "Blocked C" })
            && (int)row["failedPrecondition"]["index"] == 1
            && (string)row["failedPrecondition"]["id"] == "Fixture selected rule"
            && (string)row["failedPrecondition"]["description"] == "Fixture selected description",
            "existing failed-precondition index/id/description follow the same returned blocked row after local sorting");
        Check(blocked.chore.PreconditionReads == 1 && consumer.Shared.failedContexts.SequenceEqual(before)
            && consumer.Shared.doFailedContextsNeedSorting && consumer.Shared.FlagWrites == 0,
            "reason getter is a substituted native boundary; the shared TODO snapshot still retains its order and flag");
    }
    private static void TestMissingAndEmpty()
    {
        var missing = JObject.FromObject(FacilitySideScreenTools.TodoForTest(new MinionIdentity(),true,true,1));
        Check(missing["current"].Type == JTokenType.Null && !missing["tasks"].Any() && !missing["blocked"].Any(),
            "missing ChoreConsumer retains the existing empty-result contract");
        ChoreConsumer consumer; var minion = Fixture(true,out consumer);
        consumer.Shared.failedContexts.Clear();consumer.Shared.succeededContexts.Clear();consumer.choreDriver.Current = null;
        var empty = JObject.FromObject(FacilitySideScreenTools.TodoForTest(minion,true,true,1));
        Check(consumer.Shared.doFailedContextsNeedSorting && consumer.Shared.FlagWrites == 0,
            "empty dirty native cache is still not consumed or marked sorted by a reader");
        Check((int)empty["counts"]["succeeded"]==0 && (int)empty["counts"]["blocked"]==0
            && empty["current"].Type==JTokenType.Null && !empty["tasks"].Any() && !empty["blocked"].Any(),
            "empty native snapshot reports known empty results without inventing a current chore");
    }
    private static Chore.PreconditionInstance Rule(string id, int order)
    {
        return new Chore.PreconditionInstance { condition = new Chore.Condition
            { id = id, description = "Fixture description " + id, FixtureSortOrder = order } };
    }
    private static Chore.Precondition.Context CachedContext(string name, bool dirty, bool potential, int index = 0)
    {
        var context = Context(name,0,0,potential);
        context.failedPreconditionId = index;
        ((StandardChoreBase)context.chore).FixtureCache(dirty,Rule("Fixture later rule",20),Rule("Fixture earlier rule",10));
        return context;
    }
    private static MinionIdentity Single(Chore.Precondition.Context context, out ChoreConsumer consumer)
    {
        var minion = Fixture(true,out consumer);
        consumer.Shared.failedContexts.Clear();
        consumer.Shared.succeededContexts.Clear();
        consumer.Shared.failedContexts.Add(context);
        Chore.PotentialReads = 0;
        return minion;
    }
    private static JObject ReadSingle(Chore.Precondition.Context context, out ChoreConsumer consumer)
    {
        var minion = Single(context,out consumer);
        return JObject.FromObject(FacilitySideScreenTools.TodoForTest(minion,true,true,9));
    }
    private static void TestDirtyPreconditionsReadOnly()
    {
        foreach (bool includeBlocked in new[] { true, false })
        foreach (bool potentialOnly in new[] { false, true })
        foreach (int limit in new[] { 1, 9 })
        {
            ChorePreconditions.FixtureReset(true);
            var context = CachedContext("Pending blocked",true,true);
            var chore = (StandardChoreBase)context.chore;
            var cached = chore.Preconditions; var before = cached.ToArray();
            ChoreConsumer consumer; var minion = Single(context,out consumer);
            var snapshot = consumer.Shared; var failed = snapshot.failedContexts; var failedBefore = failed.ToArray();
            JObject first = null;
            for (int read = 0; read < 2; read++)
            {
                var result = JObject.FromObject(FacilitySideScreenTools.TodoForTest(minion,includeBlocked,potentialOnly,limit));
                // First assertion is deliberately about mutation, not newly added JSON fields.
                // The fully compiled old reader must fail here by consuming the dirty getter.
                Check(ReferenceEquals(chore.Preconditions,cached) && cached.SequenceEqual(before) && chore.FixtureDirty
                    && chore.CacheSorts == 0 && chore.CacheFlagWrites == 0,
                    "dirty native precondition cache must retain its list/order/dirty flag before any schema assertion");
                Check(chore.PreconditionReads == 0 && Chore.PotentialReads == 0
                    && ChorePreconditions.GetterReads == 0 && ChorePreconditions.ConstructionWrites == 0,
                    "pending read must not call the mutating getter, native potential boundary or lazy singleton");
                Check(ReferenceEquals(consumer.Shared,snapshot) && ReferenceEquals(snapshot.failedContexts,failed)
                    && failed.SequenceEqual(failedBefore) && snapshot.doFailedContextsNeedSorting && snapshot.FlagWrites == 0,
                    "pending precondition read also preserves the existing shared consumer snapshot");
                Check((int)result["counts"]["returnedUnknownPotential"] == (includeBlocked ? 1 : 0)
                    && (int)result["counts"]["blocked"] == 1 && result["blocked"].Count() == (includeBlocked ? 1 : 0),
                    "unknown potential is retained when requested, counted separately and never invented as empty native data");
                if (includeBlocked)
                {
                    var row = result["blocked"][0];
                    Check(row["potentialSuccess"].Type == JTokenType.Null && (string)row["preconditionState"] == "pending"
                        && (int)row["failedPrecondition"]["index"] == 0
                        && row["failedPrecondition"]["id"].Type == JTokenType.Null
                        && row["failedPrecondition"]["description"].Type == JTokenType.Null
                        && (string)row["failedPrecondition"]["state"] == "pending",
                        "pending reason keeps its snapshot index while identity, description and eligibility remain unknown");
                    Check((string)result["note"] == McpLocalization.Get("minion_todo_read.unknown_note"),
                        "unknown rows carry the actual runtime-localized explanation");
                }
                else Check(result["note"] == null,"a hidden blocked list does not add an unknown-note payload");
                if (first == null) first = result;
                else Check(JToken.DeepEquals(first,result),"pending reads stay stable without consuming native dirty state");
            }
        }
    }
    private static void TestSucceededPendingCache()
    {
        ChorePreconditions.FixtureReset(true);
        var context = CachedContext("Succeeded snapshot with pending cache",true,true);
        ChoreConsumer consumer; var minion = Single(context,out consumer);
        consumer.Shared.failedContexts.Clear(); consumer.Shared.succeededContexts.Add(context);
        var result = JObject.FromObject(FacilitySideScreenTools.TodoForTest(minion,false,true,9));
        var row = result["tasks"][0]; var chore = (StandardChoreBase)context.chore;
        Check((bool)row["success"] && row["potentialSuccess"].Type == JTokenType.Null
            && (string)row["preconditionState"] == "pending" && !result["blocked"].Any()
            && (int)result["counts"]["returnedUnknownPotential"] == 1,
            "the existing succeeded snapshot classification and unknown fresh eligibility remain separate");
        Check(chore.FixtureDirty && chore.CacheSorts == 0 && chore.CacheFlagWrites == 0
            && chore.PreconditionReads == 0 && Chore.PotentialReads == 0,
            "the succeeded projection also avoids lazy getter/potential side effects when blocked output is disabled");
    }
    private static void CheckUnknown(JObject result, string state, int index, string label)
    {
        var row = result["blocked"][0];
        Check(row["potentialSuccess"].Type == JTokenType.Null && (string)row["preconditionState"] == state
            && (int)row["failedPrecondition"]["index"] == index
            && row["failedPrecondition"]["description"].Type == JTokenType.Null
            && (string)row["failedPrecondition"]["state"] == state
            && (int)result["counts"]["returnedUnknownPotential"] == 1,
            label + ": unknown stays distinct from native false and keeps its reason index");
    }
    private static void TestUnavailableBoundaries()
    {
        ChoreConsumer consumer;
        ChorePreconditions.FixtureReset(false);
        var missingSingleton = CachedContext("Rules not ready",false,true);
        var result = ReadSingle(missingSingleton,out consumer);
        CheckUnknown(result,"unavailable",0,"absent native singleton");
        Check(!ChorePreconditions.FixtureExists && ChorePreconditions.GetterReads == 0 && ChorePreconditions.ConstructionWrites == 0
            && missingSingleton.chore.PreconditionReads == 0 && Chore.PotentialReads == 0,
            "observing unavailable rules must not create the lazy native singleton or invoke potential/getter");

        ChorePreconditions.FixtureReset(true);
        var custom = CachedContext("Custom override",false,true);
        var customChore = new CustomOverrideChore { Name = "Custom override" };
        customChore.FixtureCache(true,Rule("Fixture custom high",20),Rule("Fixture custom low",10));
        custom.chore = customChore;
        result = ReadSingle(custom,out consumer);
        CheckUnknown(result,"unavailable",0,"unsupported virtual override");
        Check(customChore.OverrideReads == 0 && customChore.PreconditionReads == 0 && customChore.FixtureDirty
            && customChore.CacheSorts == 0 && customChore.CacheFlagWrites == 0 && Chore.PotentialReads == 0,
            "an unknown virtual override must never be invoked as a fallback");

        var driverOverride = CachedContext("Custom driver",false,true);
        var driverChore = new CustomDriverChore { Name = "Custom driver" };
        driverChore.FixtureCache(false,Rule("Fixture driver rule",10));
        driverOverride.chore = driverChore; Chore.PotentialReads = 0;
        var driverObservation = NativeChorePreconditionObservation.Read(driverOverride);
        Check(driverObservation.PotentialSuccess == null && driverObservation.State == "unavailable"
            && driverChore.DriverReads == 0 && driverChore.PreconditionReads == 0 && Chore.PotentialReads == 0,
            "the cache adapter rejects an unknown driver getter without invoking it (other reader callbacks are outside this assertion)");

        var other = CachedContext("Other chore",false,true);
        other.chore = new OtherChore { Name = "Other chore" };
        result = ReadSingle(other,out consumer);
        CheckUnknown(result,"unavailable",0,"unsupported native chore cache owner");
        Check(other.chore.PreconditionReads == 0 && Chore.PotentialReads == 0,
            "a nonstandard cache owner is not probed by calling its getter");

        var missingList = CachedContext("Missing cache",false,true);
        ((StandardChoreBase)missingList.chore).FixtureCache(false,null);
        result = ReadSingle(missingList,out consumer);
        CheckUnknown(result,"unavailable",0,"missing native list");
        Check(missingList.chore.PreconditionReads == 0 && Chore.PotentialReads == 0,
            "missing cache data remains unavailable without a getter refresh");

        var throwing = CachedContext("Getter changed",false,true);
        ((StandardChoreBase)throwing.chore).ThrowOnGetter = true;
        result = ReadSingle(throwing,out consumer);
        CheckUnknown(result,"unavailable",0,"native decision exception");
        Check(throwing.chore.PreconditionReads == 1 && Chore.PotentialReads == 1,
            "a native decision failure is reported unknown without retrying the getter");
    }
    private static void TestReadyNativeDecisionAndInvalidIndex()
    {
        ChoreConsumer consumer;
        foreach (bool potential in new[] { false,true })
        {
            ChorePreconditions.FixtureReset(true);
            var context = CachedContext("Ready blocked",false,potential,1);
            var chore = (StandardChoreBase)context.chore;
            var cached = chore.Preconditions; var before = cached.ToArray();
            var minion = Single(context,out consumer);
            var result = JObject.FromObject(FacilitySideScreenTools.TodoForTest(minion,true,false,9));
            var row = result["blocked"][0];
            Check((bool)row["potentialSuccess"] == potential && (string)row["preconditionState"] == "ready"
                && (string)row["failedPrecondition"]["id"] == "Fixture earlier rule"
                && (string)row["failedPrecondition"]["description"] == "Fixture description Fixture earlier rule"
                && (string)row["failedPrecondition"]["state"] == "ready",
                "ready output retains the delegated native decision and exact cached reason");
            Check(chore.PreconditionReads == 1 && Chore.PotentialReads == 1 && ChorePreconditions.GetterReads == 1
                && ChorePreconditions.ConstructionWrites == 0 && !chore.FixtureDirty && chore.CacheSorts == 0
                && chore.CacheFlagWrites == 0 && ReferenceEquals(cached,chore.Preconditions) && cached.SequenceEqual(before),
                "each ready row is observed once: native potential only, passive reason projection, no cache sorting");
            Check((int)result["counts"]["returnedUnknownPotential"] == 0 && result["note"] == null,
                "fully ready rows do not add an unknown-note payload");
        }
        foreach (int index in new[] { -2,2,100 })
        {
            var context = CachedContext("Invalid index",false,true,index);
            var result = ReadSingle(context,out consumer);
            CheckUnknown(result,"out_of_range",index,"invalid cached failure index");
            Check((string)result["blocked"][0]["failedPrecondition"]["id"] == "out_of_range"
                && context.chore.PreconditionReads == 0 && Chore.PotentialReads == 0,
                "invalid index is explicit without invoking a native getter/error-producing eligibility path");
        }
        var negative = CachedContext("No failed index",false,true,-1);
        var noFailure = ReadSingle(negative,out consumer)["blocked"][0];
        Check((bool)noFailure["potentialSuccess"] && (string)noFailure["preconditionState"] == "ready"
            && noFailure["failedPrecondition"].Type == JTokenType.Null,
            "the native no-failure sentinel retains its null reason");
    }
    private static void TestUnknownPotentialFilteringAndLocalization()
    {
        ChorePreconditions.FixtureReset(true);
        foreach (string language in new[] { "en","ja","zh-Hans" })
        foreach (int limit in new[] { 1,9 })
        {
            McpLocalization.LanguageProvider = () => language;
            ChoreConsumer consumer; var minion = Fixture(true,out consumer);
            consumer.Shared.succeededContexts.Clear(); consumer.Shared.failedContexts.Clear();
            var unknown = CachedContext("Unknown",true,false); unknown.FixtureOrder = 0;
            var denied = CachedContext("Native false",false,false); denied.FixtureOrder = 1;
            var allowed = CachedContext("Native true",false,true); allowed.FixtureOrder = 2;
            consumer.Shared.failedContexts.AddRange(new[] { allowed,denied,unknown });
            var before = consumer.Shared.failedContexts.ToArray();
            var result = JObject.FromObject(FacilitySideScreenTools.TodoForTest(minion,true,true,limit));
            Check(Names(result,"blocked").SequenceEqual(new[] { "Unknown","Native true" }.Take(limit))
                && result["blocked"][0]["potentialSuccess"].Type == JTokenType.Null
                && (int)result["counts"]["returnedUnknownPotential"] == 1
                && (int)result["counts"]["blocked"] == 3 && (int)result["counts"]["returnedBlocked"] == Math.Min(limit,2),
                "potential-only keeps unknown distinct from false, respects limit and native total count: " + language);
            string note = (string)result["note"];
            Check(note == McpLocalization.GetForLanguage(language,"minion_todo_read.unknown_note")
                && !string.IsNullOrEmpty(note) && note != "minion_todo_read.unknown_note",
                "all runtime languages resolve the unknown-note key: " + language);
            Check(consumer.Shared.failedContexts.SequenceEqual(before) && consumer.Shared.doFailedContextsNeedSorting
                && consumer.Shared.FlagWrites == 0 && ((StandardChoreBase)unknown.chore).FixtureDirty
                && unknown.chore.PreconditionReads == 0,
                "unknown filtering and local display ordering preserve both native caches: " + language);
        }
        McpLocalization.LanguageProvider = null;
    }

}
