using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class SnapshotTools
    {
        private static readonly SnapshotDeltaCache NativeFixtureDelta = new SnapshotDeltaCache();

        internal static JObject NativeNeedsFixture(int worldId = 7, int limit = 100, string profile = "full")
        {
            // Only the unrelated food/building/time inputs are fixtures. Need rows
            // come from the complete linked production BuildDupes and reader.
            var dupes = BuildDupes(worldId);
            var food = new FoodSnapshot { TotalKcal = 100000, Access = ReadyFoodAccess(100000, worldId) };
            var buildings = new BuildingSnapshot { Beds = 100, Toilets = 1, ResearchStations = 1, Batteries = 1 };
            var red = new Dictionary<string,object> { ["isRedAlert"] = false, ["isYellowAlert"] = false };
            var alerts = BuildAlerts(dupes, food, buildings, null, red);
            var metrics = BuildMetrics(dupes, food, buildings, null, red, alerts);
            var time = new Dictionary<string,object> { ["cycle"] = 42, ["isPaused"] = true, ["speed"] = 1 };
            var colony = new Dictionary<string,object> { ["activeWorldId"] = 0, ["worldCount"] = 8 };
            var watch = BuildWatch(new List<string> { "stress", "low_stamina", "alerts" }, metrics,
                JObject.Parse("{stress:'>40',low_stamina:'=0',alerts:'>0'}"));
            var result = new Dictionary<string,object>
            {
                ["dupes"] = BuildDupeResult(dupes, limit, profile),
                ["metrics"] = metrics, ["alerts"] = alerts, ["summary"] = BuildSummary(metrics),
                ["minimal"] = BuildMinimalSnapshot("minimal", worldId, time, colony, metrics),
                ["watchOnly"] = BuildWatchOnlySnapshot(profile, worldId, time, metrics, watch),
                ["watch"] = watch
            };
            return JObject.Parse(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
        }

        internal static JObject NativeNeedsDelta(JObject current, string key, bool reset)
        {
            var result = NativeFixtureDelta.Apply("native-needs-fixture", key,
                current.ToObject<Dictionary<string,object>>(), reset);
            return JObject.Parse(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
        }
    }
}
