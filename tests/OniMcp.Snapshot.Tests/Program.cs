using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static void Main()
    {
        McpLocalization.LanguageProvider = () => "en";
        DiagnosticScope();
        EmptyWorld();
        EmptyProfiles();
        StressBoundaries();
        ScopedCounts();
        WatchNullAndZero();
        LocalizedSummaries();
        SnapshotTools.AggregateFixture();
        var delta = SnapshotTools.EmptyDeltaFixture();
        Check((bool)delta["first"]["baseline"], "empty scoped snapshot can establish a delta baseline");
        Check((bool)delta["second"]["unchanged"], "repeated empty scoped snapshot is an unchanged delta");
        Console.WriteLine("Snapshot adapters: " + checks + " host checks passed (native game boundaries substituted)");
        Console.WriteLine("Native snapshot needs: " + NativeNeedsRegression.Run() + " host checks passed (native game boundaries substituted)");
    }

    private static void DiagnosticScope()
    {
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            McpLocalization.LanguageProvider = () => language;
            var healthy = SnapshotTools.Fixture(new[] { 0f }, available: 4000, beds: 1);
            var coverage = healthy["minimal"]["alertCoverage"];
            Check(coverage != null, "diagnostic_scope_contract: minimal output exposes alert coverage");
            Check((string)coverage["source"] == "mcp_rule_checks" && (string)coverage["model"] == "snapshot", "MCP source is explicit");
            Check((bool)coverage["evaluated"] && !(bool)coverage["overallComplete"], "rule completion never claims full HUD coverage");
            Null(coverage, "hudWarningCount", "unread HUD warnings remain null, including when MCP findings are zero");
            Null(coverage, "dailyOxygenBalanceKg", "unread oxygen balance remains null");
            Check((string)coverage["oxygenCellCheck"] == "not_evaluated", "default minimal does not claim an atmosphere scan");
            Check((int)coverage["sampledCycle"] == 42 && (int)coverage["worldId"] == 7, "coverage is tied to the actual fixture sample and scope");
            Null(coverage, "sampledTimeOfDayPercent", "unobserved subcycle time remains unknown");
            Check((string)healthy["minimal"]["alertLevel"] == "green" && (int)healthy["minimal"]["metrics"]["alerts"] == 0, "legacy MCP level and count remain compatible");
            string suffix = McpLocalization.Get("diagnostic_scope.summary.suffix");
            Check(!suffix.Contains("diagnostic_scope.") && ((string)healthy["summary"]).EndsWith(suffix, StringComparison.Ordinal), "summary states the unassessed native sources in the selected language");
            Check(JToken.DeepEquals(healthy["watchOnly"]["alertCoverage"], coverage), "watch-only keeps the same qualified scope");
            Check(healthy["metrics"]["alertCoverage"]["sampledCycle"] == null, "sampling clones coverage rather than mutating metric input");
            var global = SnapshotTools.Fixture(new[] { 0f }, worldId: -1, available: 4000, beds: 1);
            Check((int)global["minimal"]["alertCoverage"]["worldId"] == -1
                && (string)global["minimal"]["alertCoverage"]["populationScope"] == "all_worlds"
                && (string)global["minimal"]["alertCoverage"]["buildingScope"] == "all_worlds", "negative world scope is explicitly all worlds");
            var critical = SnapshotTools.Fixture(new[] { 0f }, available: 0, beds: 1);
            Check((string)critical["minimal"]["alertLevel"] == "red" && HasAlert(critical, "food"), "scope qualification never hides a known critical finding");
            var sampled = SnapshotTools.CoverageFixture(true, true);
            Check((string)sampled["alertCoverage"]["oxygenCellCheck"] == "visible_stock_only", "atmosphere collection is stock, not production balance");
            Check((double)sampled["alertCoverage"]["sampledTimeOfDayPercent"] == 12.3, "known subcycle sample is retained");
            var skipped = SnapshotTools.CoverageFixture(false, false);
            Check(!(bool)skipped["alertCoverage"]["evaluated"], "unrequested checks are explicitly unassessed");
            Check(((string)skipped["summary"]).EndsWith(McpLocalization.Get("diagnostic_scope.summary.not_evaluated_suffix"), StringComparison.Ordinal), "skipped check summary does not claim evaluation");
        }
        McpLocalization.LanguageProvider = () => "en";
    }

    private static void EmptyWorld()
    {
        foreach (int unrelatedPopulation in new[] { 0, 4, 42 })
        {
            Components.LiveMinionIdentities.Count = unrelatedPopulation;
            var result = SnapshotTools.Fixture(new float[0]);
            Check((int)result["metrics"]["dupes"] == 0, "empty world never borrows total colony population " + unrelatedPopulation);
            Null(result["metrics"], "stress", "empty-world metric stress is null");
            Check((int)result["dupes"]["count"] == 0, "empty-world dupe result count is zero");
            Null(result["dupes"], "maxStress", "empty-world dupe maximum is null");
            Check(((JArray)result["dupes"]["items"]).Count == 0, "empty world has no phantom dupe rows");
            Check(!HasAlert(result, "stress"), "empty stress set generates no alert or Max exception");
            Check(!HasAlert(result, "food") && !HasAlert(result, "sleep"), "empty world has no shortage borrowed from other-world residents");
            Check((int)result["alerts"]["count"] == 0, "empty fixture without shortages has no invented alert");
            Check((int)result["minimal"]["metrics"]["dupes"] == 0, "minimal snapshot retains scoped count");
            Null(result["minimal"]["metrics"], "maxStress", "minimal snapshot keeps null maximum");
            Check((int)result["minimal"]["worldId"] == 7, "minimal snapshot retains requested unvisited world");
            Check((int)result["minimal"]["colony"]["activeWorldId"] == 0, "active world metadata stays separate from requested scope");
            Check((bool)result["minimal"]["paused"], "minimal snapshot preserves pause metadata");
            Check((string)result["summary"] == "0 dupes, max stress not applicable (no duplicants), known food 0 kcal (available 0 kcal), alert green" + McpLocalization.Get("diagnostic_scope.summary.suffix"), "empty summary explicitly marks stress not applicable");
            Check(!((string)result["summary"]).Contains("0%"), "empty summary never claims normal zero-percent stress");
            Check((string)result["watchOnly"]["summary"] == (string)result["summary"], "watch-only summary uses same empty semantics");
            Null(result["watchOnly"]["watch"]["values"], "stress", "watch-only values preserve stress null");
        }
    }

    private static void EmptyProfiles()
    {
        foreach (string profile in new[] { "brief", "standard", "full" })
        {
            var result = SnapshotTools.Fixture(new float[0], profile: profile);
            Null(result["dupes"], "maxStress", profile + " does not manufacture max stress zero");
            Check(((JArray)result["dupes"]["items"]).Count == 0, profile + " keeps an empty result set");
            Check((string)result["minimal"]["summary"] == (string)result["summary"], profile + " projections share correct summary");
        }
    }

    private static void StressBoundaries()
    {
        foreach (float stress in new[] { 0f, 39.9f, 40f, 41f, 95.5f })
        {
            var result = SnapshotTools.Fixture(new[] { stress }, worldId: 0, available: 4000, beds: 1);
            Check((double)result["metrics"]["stress"] == Math.Round(stress, 1), "known stress remains numeric at " + stress);
            Check((double)result["dupes"]["maxStress"] == Math.Round(stress, 1), "dupe result retains native maximum at " + stress);
            Check(HasAlert(result, "stress") == (stress > 40), "strict stress boundary remains above 40 at " + stress);
            Check(((string)result["summary"]).Contains("max stress " + Math.Round(stress, 1).ToString(CultureInfo.InvariantCulture) + "%"), "inhabited summary retains numeric percentage");
            if (stress > 40)
                Check((string)((JArray)result["alerts"]["items"]).Single(row => (string)row["category"] == "stress")["message"] == "Max stress " + Math.Round(stress, 1).ToString(CultureInfo.InvariantCulture) + ".", "stress warning retains localized numeric message");
        }
        var mixed = SnapshotTools.Fixture(new[] { 10f, 80f, 40f }, available: 7000, beds: 3);
        Check((double)mixed["metrics"]["stress"] == 80, "maximum selected across all scoped dupes");
        Check((int)mixed["metrics"]["stressed_dupes"] == 2, "stressed-dupe count keeps original inclusive 40 boundary");
        Check((int)mixed["dupes"]["items"][0]["id"] == 2, "dupe rows preserve descending stress order");
        Check((int)mixed["dupes"]["items"][1]["id"] == 3, "second row remains next highest scoped stress");
        Check((int)mixed["dupes"]["skillPointsTotal"] == 3, "dupe aggregate fields remain intact");
        Check((int)mixed["metrics"]["dupes"] == 3, "inhabited scope uses its own population");
    }

    private static void ScopedCounts()
    {
        Components.LiveMinionIdentities.Count = 42;
        var enough = SnapshotTools.Fixture(new[] { 0f, 10f }, available: 4000, beds: 2);
        Check(!HasAlert(enough, "food") && !HasAlert(enough, "sleep"), "two residents use a two-resident reserve and bed threshold");
        var shortFood = SnapshotTools.Fixture(new[] { 0f, 10f }, available: 3999, beds: 2);
        Check(HasAlert(shortFood, "food") && !HasAlert(shortFood, "sleep"), "native scoped food threshold still detects a real shortage");
        var shortBeds = SnapshotTools.Fixture(new[] { 0f, 10f }, available: 4000, beds: 1);
        Check(HasAlert(shortBeds, "sleep") && !HasAlert(shortBeds, "food"), "scoped bed shortage remains independent from food");
    }

    private static void WatchNullAndZero()
    {
        var absent = SnapshotTools.ThresholdsFixture(null);
        foreach (var pair in absent.Properties())
        {
            Null(pair.Value["values"], "stress", "null watch value survives threshold " + pair.Name);
            Check(!(bool)pair.Value["alert"], "null stress never matches numeric threshold " + pair.Name);
            Check(((JArray)pair.Value["triggered"]).Count == 0, "null stress produces no numeric trigger " + pair.Name);
        }
        var zero = SnapshotTools.ThresholdsFixture(0d);
        foreach (var pair in zero.Properties())
        {
            Check((double)pair.Value["values"]["stress"] == 0, "known zero stays numeric for " + pair.Name);
            Check((bool)pair.Value["alert"] && ((JArray)pair.Value["triggered"]).Count == 1, "known zero still matches applicable threshold " + pair.Name);
        }
        var empty = SnapshotTools.Fixture(new float[0]);
        var triggered = (JArray)empty["watchOnly"]["watch"]["triggered"];
        Check(triggered.Count == 1 && (string)triggered[0]["metric"] == "dupes", "known zero count can trigger while absent stress cannot");
    }

    private static void LocalizedSummaries()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            foreach (string language in new[] { "en", "ja", "zh-Hans" })
            {
                McpLocalization.LanguageProvider = () => language;
                var empty = SnapshotTools.Fixture(new float[0]);
                var known = SnapshotTools.Fixture(new[] { 41.5f }, available: 4000, beds: 1);
                foreach (string key in new[] { "snapshot.summary", "snapshot.summary.no_dupes", "snapshot.alert.stress" })
                    Check(McpLocalization.Get(key) != key, language + " resolves runtime key " + key);
                Check(!((string)empty["summary"]).Contains("%"), language + " absent stress summary contains no invented percentage");
                Check(((string)known["summary"]).Contains("41.5%"), language + " known summary preserves numeric percent independent of process culture");
                Check(!((string)empty["summary"]).Contains("{"), language + " absent summary has no unresolved placeholders");
                Check(!((string)known["summary"]).Contains("{"), language + " known summary has no unresolved placeholders");
                Check(HasAlert(known, "stress"), language + " translated high stress still carries a stable protocol category");
            }
        }
        finally { CultureInfo.CurrentCulture = original; McpLocalization.LanguageProvider = () => "en"; }
    }

    private static bool HasAlert(JObject result, string category)
    { return ((JArray)result["alerts"]["items"]).Any(row => (string)row["category"] == category); }
    private static void Null(JToken parent, string key, string message)
    { Check(parent[key] != null && parent[key].Type == JTokenType.Null, message); }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; }
}
