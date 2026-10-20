using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string reason)
    { checks++; if (!condition) throw new InvalidOperationException("Minion TODO cache: " + reason); }
    private static Chore MakeChore(string name)
    {
        return new Chore { Name = name, choreType = new ChoreType { Id = "FixtureChoreType", groups = new ChoreGroup[0] } };
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
            + " checks passed (full product reader; native context comparator/potential/getter boundaries substituted)");
    }
    private static void TestFailedReasonProjection()
    {
        ChoreConsumer consumer; var minion = Fixture(true,out consumer);
        var blocked = consumer.Shared.failedContexts[0];
        blocked.failedPreconditionId = 1;
        blocked.chore.Preconditions.Add(new Chore.Entry { condition = new Chore.Condition
            { id = "Fixture earlier rule", description = "Fixture earlier description" } });
        blocked.chore.Preconditions.Add(new Chore.Entry { condition = new Chore.Condition
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
}
