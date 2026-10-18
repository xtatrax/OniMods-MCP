using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class SnapshotTools
    {
        internal static JObject Fixture(float[] stresses, int worldId = 7,
            double available = 0, int beds = 0, string profile = "full")
        {
            var dupes = stresses.Select((stress, i) => new DupeSnapshot
            {
                Id = i + 1, Name = "Dupe" + i, WorldId = worldId,
                X = 10 + i, Y = 20, Stress = stress, Stamina = 50,
                Calories = 1000000, SkillPoints = i
            }).ToList();
            var food = new FoodSnapshot
            {
                TotalKcal = available, FoodTypes = available > 0 ? 1 : 0,
                Access = new FoodAccessBreakdown { Available = available },
                NativeRations = new Dictionary<string, object> { ["caloriesKcal"] = null, ["complete"] = false }
            };
            var buildings = new BuildingSnapshot
            {
                Total = beds + 6, Beds = beds, Toilets = 1, WashStations = 1,
                ResearchStations = 1, Batteries = 1, Generators = 1, OxygenProducers = 1
            };
            var redAlert = new Dictionary<string, object>
            { ["isRedAlert"] = false, ["isYellowAlert"] = false };
            var alerts = BuildAlerts(dupes, food, buildings, null, redAlert);
            var metrics = BuildMetrics(dupes, food, buildings, null, redAlert, alerts);
            var time = new Dictionary<string, object> { ["cycle"] = 42, ["isPaused"] = true, ["speed"] = 1 };
            var colony = new Dictionary<string, object> { ["activeWorldId"] = 0, ["worldCount"] = 8 };
            var result = new Dictionary<string, object>
            {
                ["metrics"] = metrics,
                ["dupes"] = BuildDupeResult(dupes, 100, profile),
                ["alerts"] = alerts,
                ["summary"] = BuildSummary(metrics),
                ["minimal"] = BuildMinimalSnapshot("minimal", worldId, time, colony, metrics),
                ["watchOnly"] = BuildWatchOnlySnapshot("full", worldId, time, metrics,
                    BuildWatch(new List<string> { "stress", "dupes" }, metrics,
                        JObject.Parse("{stress:'<=0',dupes:'=0'}"))),
                ["stressWatch"] = BuildWatch(new List<string> { "stress" }, metrics,
                    JObject.Parse("{stress:'<=0'}"))
            };
            return JObject.FromObject(result, Newtonsoft.Json.JsonSerializer.Create(McpJsonUtil.Settings));
        }

        internal static JObject ThresholdsFixture(object stress)
        {
            var metrics = new Dictionary<string, object> { ["stress"] = stress };
            var result = new JObject();
            foreach (string expression in new[] { "<1", "<=0", "=0", ">-1", ">=0", "0" })
                result[expression] = JObject.FromObject(BuildWatch(new List<string> { "stress" }, metrics,
                    new JObject { ["stress"] = expression }));
            return result;
        }

        internal static JObject EmptyDeltaFixture()
        {
            var cache = new SnapshotDeltaCache();
            var current = Fixture(new float[0]);
            var dictionary = current["minimal"].ToObject<Dictionary<string, object>>();
            var first = cache.Apply("host", "world7", dictionary, false);
            var second = cache.Apply("host", "world7", dictionary, false);
            return JObject.FromObject(new { first, second });
        }

        internal static object[] AggregateFixture()
        {
            var item = new FoodAggregate { Id = "sample", Name = "sample", Quality = 0, Count = 1, Kcal = 0 };
            item.AddLocation(new UnityEngine.GameObject(), 0, 0);
            var observation = new FoodAccessObservation
            {
                CaloriesKcal = 0, Reachable = false, PermittedReachableConsumer = false,
                Fetchable = false, StoredPrivate = false, BeingConsumed = false,
                AutomationOnlyStorage = false, FetchTotalAmount = 0, UnreservedFetchAmount = 0
            };
            item.Access.Add(observation);
            return new object[] { item.ToDictionary(), new FoodSnapshot().ToDictionary(), BuildRedAlert(7) };
        }
    }
}
