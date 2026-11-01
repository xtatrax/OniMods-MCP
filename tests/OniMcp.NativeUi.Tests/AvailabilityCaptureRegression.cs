using System;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

// The full production TryPlanOne executes; only the availability policy is a
// supplied gate result. Its observation is from the real passive reader.
internal static class AvailabilityCaptureRegression
{
    private static int checks;
    private static void Check(bool value, string reason)
    {
        checks++;
        if (!value) throw new InvalidOperationException("Availability capture: " + reason);
    }

    internal static void Run()
    {
        foreach (bool firstAvailable in new[] { true, false })
        {
            TileReplacementFixture.Setup();
            Db.Seed(null); Db.GetCalls = 0; Research.Instance = null;
            var def = TileReplacementFixture.Definition;
            int observed = 0;
            def.FixtureAvailabilityPredicate = () => ++observed == 1 ? firstAvailable : !firstAvailable;
            BuildPlanningTools.FixtureAvailabilityError = "fixture observed availability rejection";
            var result = TileReplacementFixture.PlanRead(TileReplacementFixture.Args(true));
            var details = (JObject)result["details"];
            Check(result["valid"].Value<bool>() == false, "availability rejection is retained");
            Check(observed == 1 && def.FixtureAvailabilityReads == 1,
                "parent reuses gate observation instead of evaluating native availability again");
            Check(details["unlocked"].Type == JTokenType.Null,
                "missing research remains an explicit null key");
            Check(firstAvailable ? details["availableNow"].Type == JTokenType.Null
                    : details["availableNow"].Type == JTokenType.Boolean && !details["availableNow"].Value<bool>(),
                "parent returns the first independent definition/research aggregate");
            Check((string)details["availability"]["reasonCode"] ==
                    (firstAvailable ? "database_cache_missing" : "definition_unavailable"),
                "first observed reason is not overwritten by a later getter result");
            Check(Db.GetCalls == 0 && Db.CachedForFixture == null,
                "parent keeps the missing database cache uninitialized");
            Check(def.DirectBuildCalls == 0 && def.TryPlaceCalls == 0
                && TileReplacementFixture.Tool.BuildCalls == 0 && TileReplacementFixture.Plan.CopyCalls == 0,
                "rejected preview invokes no placement or native UI preparation");
            Check(details["placement"] is JObject && details["footprint"] is JArray,
                "read-only geometry remains inspectable on rejection");
        }
        Console.WriteLine("Availability capture: " + checks + " host checks passed (full production parent; native inputs substituted)");
    }
}
