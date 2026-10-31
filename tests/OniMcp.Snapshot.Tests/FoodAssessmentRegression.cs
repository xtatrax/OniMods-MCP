using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class FoodAssessmentRegression
{
    private static int checks;

    internal static void Run()
    {
        var pendingEmpty = SnapshotTools.EmptyFoodReadinessFixture(false);
        AssertPending(pendingEmpty, "initializing");
        var missingEmpty = SnapshotTools.EmptyFoodReadinessFixture(null);
        AssertPending(missingEmpty, "unavailable");
        var readyEmpty = SnapshotTools.EmptyFoodReadinessFixture(true);
        Require((bool)readyEmpty["metrics"]["food_assessment_complete"]
            && (string)readyEmpty["metrics"]["food_assessment_state"] == "complete"
            && HasAlert(readyEmpty, "food") && (string)readyEmpty["minimal"]["alertLevel"] == "red",
            "a native ready empty inventory remains confirmed zero and triggers the existing shortage rule");

        var unknownAccess = new FoodAccessBreakdown();
        unknownAccess.ObserveInventory(new FoodInventoryObservation { WorldId = 7, Ready = false,
            Reason = "world_inventory_initializing" });
        unknownAccess.Add(new FoodAccessObservation
        {
            Inventory = new FoodInventoryObservation { WorldId = 7, Ready = false, Reason = "world_inventory_initializing" },
            CaloriesKcal = 258792.7, Reachable = null, PermittedReachableConsumer = null,
            Fetchable = null, StoredPrivate = false, BeingConsumed = false, AutomationOnlyStorage = false,
            FetchTotalAmount = 1, UnreservedFetchAmount = 1
        });
        var pendingMeasured = SnapshotTools.Fixture(new[] { 0f, 10f }, available: 258792.7, beds: 2, foodAccess: unknownAccess);
        AssertPending(pendingMeasured, "initializing");
        Require((double)pendingMeasured["minimal"]["metrics"]["foodUnknownKcal"] == 258792.7
            && (double)pendingMeasured["minimal"]["metrics"]["foodAvailableKcal"] == 0,
            "measured unknown and confirmed lower-bound zero remain separate numeric observations");

        var red = SnapshotTools.Fixture(new[] { 90f }, available: 258792.7, beds: 1,
            foodAccess: unknownAccess, nativeRedAlert: true);
        Require(HasAlert(red, "food_assessment_incomplete") && HasAlert(red, "red_alert")
            && HasAlert(red, "stress") && !HasAlert(red, "food")
            && (string)red["minimal"]["alertLevel"] == "red",
            "known native red alert and observed stress remain visible beside pending food");

        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            McpLocalization.LanguageProvider = () => language;
            var translated = SnapshotTools.Fixture(new[] { 10f }, available: 258792.7, beds: 1, foodAccess: unknownAccess);
            string summary = (string)translated["summary"];
            Require(summary.Contains(McpLocalization.Get("food_assessment.state.initializing"))
                && summary.Contains("/active/resources/food.oni") && summary.Contains("worldId")
                && !summary.Contains("food_assessment.") && !summary.Contains("{"),
                language + " uses runtime localization and a same-world paused-read hint");
            Require((string)translated["minimal"]["summary"] == summary
                && (string)translated["watchOnly"]["summary"] == summary
                && (string)translated["watchOnly"]["foodAssessmentState"] == "initializing",
                language + " propagates readiness through minimal and watch-only paths");
            Require(summary.EndsWith(McpLocalization.Get("diagnostic_scope.summary.suffix"), StringComparison.Ordinal),
                language + " incomplete-food summary retains the accepted No22 coverage suffix");
            var coverage = translated["minimal"]["alertCoverage"];
            Require((string)coverage["source"] == "mcp_rule_checks" && !(bool)coverage["overallComplete"]
                && coverage["hudWarningCount"].Type == JTokenType.Null
                && coverage["dailyOxygenBalanceKg"].Type == JTokenType.Null,
                language + " incomplete food does not discard or overstate accepted No22 coverage");
        }
        McpLocalization.LanguageProvider = () => "en";

        var first = SnapshotTools.NativeNeedsDelta(pendingEmpty, "food-readiness", true);
        var repeated = SnapshotTools.NativeNeedsDelta(pendingEmpty, "food-readiness", false);
        var knownZero = SnapshotTools.NativeNeedsDelta(readyEmpty, "food-readiness", false);
        var unknownAgain = SnapshotTools.NativeNeedsDelta(pendingEmpty, "food-readiness", false);
        Require((bool)first["baseline"] && (bool)repeated["unchanged"], "repeated initializing data is an unchanged delta");
        Require(!(bool)knownZero["unchanged"] && !(bool)unknownAgain["unchanged"]
            && (string)knownZero["changed"]["metrics"]["food_assessment_state"] == "complete"
            && (string)unknownAgain["changed"]["metrics"]["food_assessment_state"] == "initializing",
            "readiness transitions remain visible even when confirmed numeric food is zero");
        Console.WriteLine("Food assessment: " + checks + " host checks passed (native inventory boundary substituted)");
    }

    private static void AssertPending(JObject result, string state)
    {
        Require(!(bool)result["metrics"]["food_assessment_complete"]
            && (string)result["metrics"]["food_assessment_state"] == state,
            "food_initialization_contract: missing or pending inventory is not a confirmed food zero");
        Require(!HasAlert(result, "food") && HasAlert(result, "food_assessment_incomplete")
            && (string)result["minimal"]["alertLevel"] == "yellow",
            "unknown food remains a warning without a fabricated critical shortage or green result");
        Require((string)result["minimal"]["metrics"]["foodAssessmentState"] == state
            && !((string)result["summary"]).Contains("(available 0 kcal)"),
            "summary and compact state do not present a pending lower bound as a complete available zero");
    }

    private static bool HasAlert(JObject result, string category)
    { return ((JArray)result["alerts"]["items"]).Any(row => (string)row["category"] == category); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; }
}
