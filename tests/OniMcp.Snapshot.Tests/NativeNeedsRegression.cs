using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class NativeNeedsRegression
{
    private static int checks;
    private static Database.Amounts definitions;
    private static readonly string[] Fields = { "stress", "stamina", "calories" };
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException("Native snapshot needs: " + message); }
    private static void Null(JToken parent, string field)
    { Check(parent[field] != null && parent[field].Type == JTokenType.Null, field + " remains explicit serialized JSON null"); }
    private static void Reset()
    {
        Components.LiveMinionIdentities.Items.Clear(); Components.LiveMinionIdentities.Count = 0;
        Db.GetCalls = 0;
        definitions = new Database.Amounts
        {
            Stress = new Klei.AI.Amount { Id = "unrelated-id-1", Name = "Localized decoy A", units = Klei.AI.Units.Percentage },
            Stamina = new Klei.AI.Amount { Id = "unrelated-id-2", Name = "Localized decoy B", units = Klei.AI.Units.Percentage },
            Calories = new Klei.AI.Amount { Id = "unrelated-id-3", Name = "Localized decoy C", units = Klei.AI.Units.Calories }
        };
        Db.FixtureSet(new Db { Amounts = definitions });
        McpLocalization.LanguageProvider = () => "en";
    }
    private static MinionIdentity Add(int id, string name, float? stress = 10, float? stamina = 78, float? calories = 2980000.5f, int world = 7)
    {
        var dupe = new MinionIdentity { Name = name, WorldId = world, Cell = 210 + id };
        dupe.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = id };
        dupe.Components[typeof(MinionResume)] = new MinionResume { AvailableSkillpoints = id };
        Put(dupe, definitions.Stress, stress); Put(dupe, definitions.Stamina, stamina); Put(dupe, definitions.Calories, calories);
        Components.LiveMinionIdentities.Items.Add(dupe); Components.LiveMinionIdentities.Count++;
        return dupe;
    }
    private static void Put(MinionIdentity dupe, Klei.AI.Amount definition, float? value)
    {
        if (value.HasValue) dupe.NativeAmounts[definition] = new Klei.AI.AmountInstance { amount = definition, value = value.Value };
    }
    private static JObject Snapshot(int world = 7, int limit = 100, string profile = "full")
    { return SnapshotTools.NativeNeedsFixture(world, limit, profile); }
    private static JObject Row(JObject snapshot, int index = 0) { return (JObject)snapshot["dupes"]["items"][index]; }
    private static void Coverage(JObject snapshot, string field, int known, int unknown)
    {
        foreach (JToken coverage in new[] { snapshot["metrics"]["needsCoverage"], snapshot["dupes"]["needsCoverage"], snapshot["minimal"]["metrics"]["needsCoverage"], snapshot["watchOnly"]["needsCoverage"] })
            Check((int)coverage[field]["observed"] == known && (int)coverage[field]["unknown"] == unknown && (bool)coverage[field]["complete"] == (unknown == 0), field + " coverage propagates to all production projections");
    }
    private static bool Alert(JObject snapshot) { return ((JArray)snapshot["alerts"]["items"]).Any(x => (string)x["category"] == "stress"); }
    private static JObject Evidence(JObject row, string field) { return (JObject)row["needsEvidence"][field]; }
    private static void Reason(JObject snapshot, string field, string reason)
    {
        var row = Row(snapshot); Null(row, field);
        Check((string)Evidence(row, field)["reasonCode"] == reason && (string)Evidence(row, field)["status"] == "unknown" && !(bool)Evidence(row, field)["available"], field + " reports " + reason);
    }

    private static void NativeValuesAndZero()
    {
        Reset(); var dupe = Add(1, "Normal");
        var before = dupe.NativeAmounts.Values.Select(x => x.Writes).ToArray();
        var snapshot = Snapshot(); var row = Row(snapshot);
        Check((double?)row["stamina"] == 78 && (double?)row["calories"] == 2980000.5 && (double?)row["stress"] == 10, "fixed native Lookup preserves ordinary values even when the old component and modifier-list paths are unavailable");
        Check((int)row["id"] == 1 && (int)row["worldId"] == 7 && (int)row["skillPoints"] == 1, "existing dupe identity and metadata survive the reader change");
        foreach (string field in Fields)
        {
            var evidence = Evidence(row, field);
            string amount = char.ToUpperInvariant(field[0]) + field.Substring(1);
            Check((bool)evidence["available"] && (string)evidence["status"] == "observed" && (string)evidence["amountId"] == amount && (string)evidence["source"] == "Db.Amounts." + amount + ".Lookup(MinionIdentity).value" && (string)evidence["attemptedSource"] == (string)evidence["source"] && (string)evidence["valueFormat"] == "raw_amount", "fixed definition source and raw unit format remain explicit for " + field);
            Null(evidence, "reasonCode"); Coverage(snapshot, field, 1, 0);
        }
        Check((string)Evidence(row, "calories")["unit"] == "Calories" && (string)Evidence(row, "stamina")["unit"] == "Percentage", "units come from the native definition without converting calories to kcal or guessing display percent");
        Check(Db.GetCalls == 0 && dupe.InvalidAmountComponentReads == 0 && dupe.NativeAmounts.Values.Select(x => x.Writes).SequenceEqual(before), "read calls neither lazy Db.Get, invalid Amounts component, nor native value setters");
        Check(definitions.Stress.Lookups == 1 && definitions.Stamina.Lookups == 1 && definitions.Calories.Lookups == 1, "each native fixed definition is queried once for the selected dupe");
        Reset(); Add(1, "Zero", 0, 0, 0); snapshot = Snapshot(); row = Row(snapshot);
        Check((double)row["stress"] == 0 && (double)row["stamina"] == 0 && (double)row["calories"] == 0 && (int)snapshot["metrics"]["low_stamina"] == 1 && (int)snapshot["dupes"]["lowStamina"] == 1, "true zero stays observed and zero stamina is included in the native low-stamina count");
        Check(!Alert(snapshot) && !((string)snapshot["summary"]).Contains("not applicable"), "observed stress zero does not become unknown or an empty population");
        foreach (string field in Fields) Coverage(snapshot, field, 1, 0);
        foreach (float stamina in new[] { -1f, 0f, 29.9f, 30f })
        {
            Reset(); Add(1, "Boundary", 10, stamina); snapshot = Snapshot();
            Check((int)snapshot["metrics"]["low_stamina"] == (stamina >= 0 && stamina < 30 ? 1 : 0), "stamina count keeps exact zero-inclusive and thirty-exclusive boundary");
        }
    }
    private static void MissingAndFailures()
    {
        foreach (int mode in new[] { 0, 1, 2, 3, 4, 5, 6 })
        {
            Reset(); var dupe = Add(1, "Missing");
            string reason;
            if (mode == 0) { Db.FixtureSet(null); reason = "database_cache_missing"; }
            else if (mode == 1) { Db.FixtureCurrent.Amounts = null; reason = "amount_definitions_missing"; }
            else if (mode == 2) { definitions.Stamina = null; reason = "amount_definition_missing"; }
            else if (mode == 3) { dupe.NativeAmounts.Remove(definitions.Stamina); reason = "amount_instance_missing"; }
            else if (mode == 4) { definitions.Stamina.ThrowLookup = true; reason = "amount_lookup_failed"; }
            else if (mode == 5) { dupe.NativeAmounts[definitions.Stamina].ThrowValue = true; reason = "amount_value_read_failed"; }
            else { definitions.Stamina.ThrowUnits = true; reason = "amount_definition_read_failed"; }
            var snapshot = Snapshot(); Reason(snapshot, "stamina", reason); Coverage(snapshot, "stamina", 0, 1);
            Null(snapshot["metrics"], "low_stamina"); Null(snapshot["dupes"], "lowStamina"); Null(snapshot["watch"]["values"], "low_stamina");
            Check(!((JArray)snapshot["watch"]["triggered"]).Any(x => (string)x["metric"] == "low_stamina"), "unknown stamina count never matches zero watch threshold");
            if (mode >= 2)
            {
                Check((double)Row(snapshot)["calories"] == 2980000.5 && (double)snapshot["metrics"]["stress"] == 10, "independent calories and stress survive another field failure");
                Coverage(snapshot, "stress", 1, 0); Coverage(snapshot, "calories", 1, 0);
            }
            Check(Db.GetCalls == 0 && dupe.InvalidAmountComponentReads == 0, "missing native state never initializes or uses an invalid component fallback");
        }
        foreach (string field in Fields)
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Reset(); var dupe = Add(1, "Nonfinite");
            var definition = field == "stress" ? definitions.Stress : field == "stamina" ? definitions.Stamina : definitions.Calories;
            dupe.NativeAmounts[definition].value = invalid;
            var snapshot = Snapshot(); Reason(snapshot, field, "non_finite_value"); Coverage(snapshot, field, 0, 1);
            Check(!((string)snapshot["summary"]).Contains("NaN") && !((string)snapshot["summary"]).Contains("Infinity"), "nonfinite native amount never leaks into aggregate summary");
        }
    }
    private static void PartialSortScopeAndProfiles()
    {
        Reset(); var unknown = Add(1, "A_unknown", null, null, null); var known = Add(2, "Z_high", 80, 50); Add(3, "M_zero", 0, 0);
        var snapshot = Snapshot();
        Null(snapshot["metrics"], "stress"); Null(snapshot["metrics"], "stressed_dupes"); Null(snapshot["dupes"], "maxStress"); Null(snapshot["dupes"], "lowStamina");
        Check(Alert(snapshot) && (string)snapshot["metrics"]["alertLevel"] == "yellow", "known high-stress alert survives incomplete whole-population stress coverage");
        Check(!((JArray)snapshot["watch"]["triggered"]).Any(x => (string)x["metric"] == "stress") && ((JArray)snapshot["watch"]["triggered"]).Any(x => (string)x["metric"] == "alerts"), "null aggregate stress cannot trigger a numeric watch while the known stress alert can");
        Check((int)Row(snapshot)["id"] == 2 && (int)Row(snapshot, 1)["id"] == 3 && (int)Row(snapshot, 2)["id"] == 1, "unknown stress sorts after observed high and true zero regardless of its name");
        Check((int)snapshot["metrics"]["dupes"] == 3 && !((string)snapshot["summary"]).Contains("no duplicants") && !((string)snapshot["summary"]).Contains("max stress 0%"), "inhabited partial state is not described as an empty population or normal zero stress");
        foreach (string field in Fields) Coverage(snapshot, field, 2, 1);
        foreach (string profile in new[] { "brief", "standard", "full" })
        {
            JObject limited = Snapshot(limit: 1, profile: profile); Coverage(limited, "stress", 2, 1);
            Check(((JArray)limited["dupes"]["items"]).Count == 1, "profile limit controls detail rows only");
            Check((Row(limited)["needsEvidence"] != null) == (profile == "full"), "only full per-dupe rows contain detailed needs evidence");
            Check(Row(limited)["stamina"] != null == (profile == "full"), "brief and standard omit full raw need values");
        }
        JObject noRows = Snapshot(limit: 0, profile: "brief");
        Check(noRows["dupes"]["items"] == null, "zero detail limit omits row payload"); Coverage(noRows, "stress", 2, 1);
        Check(snapshot["watchOnly"]["dupes"] == null && snapshot["watchOnly"]["metrics"] == null && snapshot["watchOnly"]["needsEvidence"] == null && snapshot["minimal"]["needsEvidence"] == null, "compact projections keep coverage without copying full per-dupe evidence");
        Check(unknown.InvalidAmountComponentReads == 0 && known.InvalidAmountComponentReads == 0, "partial need rows never fall back to component or text matching");
        var otherWorld = Add(4, "OtherWorld", 99, 0, 0, world: 0);
        JObject scoped = Snapshot(); Check((int)scoped["dupes"]["count"] == 3 && otherWorld.NativeAmounts.Values.All(instance => instance.Reads == 0), "exact world filter excludes another world's native needs before querying its amount values");
        JObject all = Snapshot(world: -1); Check((int)all["dupes"]["count"] == 4, "explicit all-world read includes native need rows from both worlds"); Coverage(all, "stress", 3, 1);
        Reset(); Add(1, "Known", 39, 50); Add(2, "Other", 40, 20);
        snapshot = Snapshot(); Check((double)snapshot["metrics"]["stress"] == 40 && (int)snapshot["metrics"]["stressed_dupes"] == 1 && !Alert(snapshot), "complete set retains inclusive count and strict alert boundaries");
        Reset(); snapshot = Snapshot(); Null(snapshot["metrics"], "stress");
        Check((int)snapshot["metrics"]["stressed_dupes"] == 0 && (int)snapshot["metrics"]["low_stamina"] == 0 && (int)snapshot["dupes"]["count"] == 0 && !Alert(snapshot), "empty world has zero counts without phantom values");
        foreach (string field in Fields) Coverage(snapshot, field, 0, 0);
    }
    private static void DeltaAndLocalization()
    {
        Reset(); var dupe = Add(1, "Changing", null, null, null);
        var unknown = Snapshot(); var first = SnapshotTools.NativeNeedsDelta(unknown, "full", true);
        Check((bool)first["baseline"], "unknown state establishes full delta baseline");
        Check((bool)SnapshotTools.NativeNeedsDelta(Snapshot(), "full", false)["unchanged"], "repeated unknown state is unchanged");
        Put(dupe, definitions.Stress, 0); Put(dupe, definitions.Stamina, 0); Put(dupe, definitions.Calories, 0);
        var realZero = SnapshotTools.NativeNeedsDelta(Snapshot(), "full", false);
        Check(!(bool)realZero["unchanged"] && (double)realZero["changed"]["dupes"]["items"][0]["stamina"] == 0 && (double)realZero["changed"]["metrics"]["stress"] == 0, "unknown to observed zero appears as a real serialized delta");
        dupe.NativeAmounts.Clear(); var lost = SnapshotTools.NativeNeedsDelta(Snapshot(), "full", false);
        Null(lost["changed"]["metrics"], "stress"); Null(lost["changed"]["dupes"]["items"][0], "stamina");
        Check((bool)SnapshotTools.NativeNeedsDelta(Snapshot(), "full", false)["unchanged"], "repeated lost state stabilizes delta");
        SnapshotTools.NativeNeedsDelta((JObject)Snapshot()["watchOnly"], "compact", true);
        definitions.Stamina.ThrowLookup = true;
        var changedReason = Snapshot();
        Check((bool)SnapshotTools.NativeNeedsDelta((JObject)changedReason["watchOnly"], "compact", false)["unchanged"], "compact same-coverage reason changes do not add verbose delta churn");
        var detailed = SnapshotTools.NativeNeedsDelta(changedReason, "full", false);
        Check(!(bool)detailed["unchanged"] && (string)detailed["changed"]["dupes"]["items"][0]["needsEvidence"]["stamina"]["reasonCode"] == "amount_lookup_failed", "full evidence preserves a changed unavailable reason in delta");
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            McpLocalization.LanguageProvider = () => language;
            var snapshot = Snapshot(); string summary = (string)snapshot["summary"];
            Check(!summary.Contains("snapshot_needs.") && !summary.Contains("{") && summary.Length > 0, "inhabited unknown summary and incomplete suffix resolve in " + language);
            Check(McpLocalization.Get("snapshot_needs.summary.stress_unavailable") != "snapshot_needs.summary.stress_unavailable", "new localized summary key is present in " + language);
        }
        McpLocalization.LanguageProvider = () => "en";
    }
    internal static int Run()
    {
        NativeValuesAndZero(); MissingAndFailures(); PartialSortScopeAndProfiles(); DeltaAndLocalization();
        return checks;
    }
}
