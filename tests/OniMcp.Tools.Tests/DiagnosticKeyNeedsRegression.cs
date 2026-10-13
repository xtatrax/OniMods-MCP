using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Tools;

internal static class DiagnosticKeyNeedsRegression
{
    internal static void Run()
    {
        var missing = new KeyNeeds();
        missing["breath"].Read(() => null);
        Assert(missing.ToDictionary()["breath"] == null, "Missing breath must not become 100 or 0");
        var reasons = new List<string>();
        missing.AddRiskReasons(reasons);
        Assert(!reasons.Contains("low_breath"), "Missing breath must not mean confirmed suffocation");
        Assert(missing.RiskWithCoverage(reasons) == "unknown" && reasons.Contains("needs_unavailable"), "Missing needs cannot report ok");
        Assert(!missing.IsComplete && missing.UnknownFields.Length == 6, "Expose incomplete assessment");

        foreach (float value in new[] { 100f, 34f, 0f })
        {
            var observed = Complete();
            observed["breath"].Read(() => new RawNeedSample { Value = value, Unit = "Flat" });
            reasons = new List<string>();
            observed.AddRiskReasons(reasons);
            Assert(observed.RiskWithCoverage(reasons) == (value < 35f ? "critical" : "ok"), "Known normal/low/zero breath risk");
            Assert((double)observed.ToDictionary()["breath"] == value, "Keep native breath value");
            var evidence = (Dictionary<string, object>)observed.EvidenceDictionary()["breath"];
            Assert((bool)evidence["available"] && (string)evidence["status"] == "observed"
                && (string)evidence["unit"] == "Flat" && (string)evidence["amountId"] == "Breath"
                && evidence["source"] != null, "Known 100 requires observed provenance");
        }
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var observed = Complete();
            observed["breath"].Read(() => new RawNeedSample { Value = value, Unit = "Flat" });
            Assert(observed.ToDictionary()["breath"] == null && observed["breath"].Reason == "non_finite_value", "Invalid numbers stay unknown");
            JsonConvert.SerializeObject(observed.ToDictionary());
        }
        var retry = Complete();
        retry["breath"].Read(() => { throw new InvalidOperationException(); });
        Assert(retry.Breath == null && retry["breath"].Reason == "read_failed", "Failed retry must clear earlier normal value");
        reasons = new List<string> { "standing_in_liquid" };
        Assert(retry.RiskWithCoverage(reasons) == "unknown", "Liquid observation must not replace missing breath with a warning or safety claim");
        var liquidNormal = Complete();
        reasons = new List<string> { "standing_in_liquid" };
        Assert(liquidNormal.RiskWithCoverage(reasons) == "ok" && !reasons.Contains("standing_in_liquid"), "Liquid alone does not create an intervention reason");
        reasons = new List<string> { "standing_in_liquid", "low_breath" };
        Assert(liquidNormal.RiskWithCoverage(reasons) == "critical", "Liquid must not hide confirmed low breath");
        reasons = new List<string> { "standing_in_liquid", "dangerous_temperature" };
        Assert(liquidNormal.RiskWithCoverage(reasons) == "warning", "Liquid must not suppress independent thermal warning");
        reasons = new List<string> { "no_reachable_nearby_cells" };
        Assert(retry.RiskWithCoverage(reasons) == "critical", "Missing observations must preserve known critical risk");
        var zero = Complete();
        zero["calories"].Read(() => new RawNeedSample { Value = 0, Unit = "Flat" });
        reasons = new List<string>();zero.AddRiskReasons(reasons);
        Assert(reasons.Contains("low_calories"), "Observed zero calories are not missing");
        zero["bodyTemperature"].Read(() => new RawNeedSample { Value = -1, Unit = "Kelvin" });
        Assert((double)zero.ToDictionary()["bodyTemperature"] == -1, "No sentinel overwrites an observed finite value");
        BionicRegression();
        Console.WriteLine("PASS: need diagnostics distinguish observed normal/low/zero, missing, exception, non-finite and partial risk");
    }

    private static void BionicRegression()
    {
        foreach (var test in new[] {
            new[] { "battery", "noCharge", "bionic_battery_empty", "critical" },
            new[] { "battery", "criticallyLow", "bionic_battery_low", "warning" },
            new[] { "oxygenTank", "empty", "bionic_oxygen_empty", "critical" },
            new[] { "oil", "wantsOilChange", "bionic_oil_refill_needed", "warning" },
            new[] { "gunk", "criticalUrge", "bionic_gunk_urgent", "warning" }
        })
        {
            var snapshot = BionicSnapshot();
            ((Dictionary<string, object>)snapshot[test[0]])[test[1]] = true;
            ((Dictionary<string, object>)snapshot["gunk"])["available"] = false;
            if (test[0] == "gunk") { ((Dictionary<string, object>)snapshot["gunk"])["available"] = true; ((Dictionary<string, object>)snapshot["oil"])["available"] = false; }
            var reasons = new List<string>();
            bool complete = BionicRiskAssessment.AddReasons(snapshot, null, reasons);
            Assert(!complete && reasons.Contains(test[2]), "Known bionic risk survives missing monitor");
            Assert(new KeyNeeds().RiskWithCoverage(reasons, complete) == test[3], "Bionic risk survives missing common needs");
        }
        var normal = BionicSnapshot();
        ((Dictionary<string, object>)normal["oxygenTank"])["safe"] = false;
        var r = new List<string>();
        Assert(BionicRiskAssessment.AddReasons(normal, null, r) && r.Count == 0, "Oxygen refill threshold alone is not danger");
        ((Dictionary<string, object>)normal["oil"])["hasOil"] = false;
        BionicRiskAssessment.AddReasons(normal, null, r);
        Assert(r.Contains("bionic_oil_empty") && Complete().RiskWithCoverage(r) == "warning", "Oil empty does not imply offline");
        r.Clear();
        var chore = new Dictionary<string, object> { ["runtimeType"] = "BionicGunkSpillChore" };
        BionicRiskAssessment.AddReasons(BionicSnapshot(), chore, r);
        Assert(r.Contains("bionic_gunk_spill"), "Actual spill chore distinguishes accident");
        r.Clear();
        BionicRiskAssessment.AddReasons(BionicSnapshot(), new Dictionary<string, object> { ["runtimeType"] = "WorkChore" }, r);
        Assert(r.Count == 0, "Ordinary toilet work is not a spill");
        Assert(Complete().RiskWithCoverage(r, false) == "unknown" && r.Contains("bionic_needs_unavailable"), "Missing bionic monitor cannot report ok");
        Console.WriteLine("PASS: bionic depletion, refill, spill, recovery and partial coverage diagnostics");
    }

    private static Dictionary<string, object> BionicSnapshot()
    {
        var result = new Dictionary<string, object>();
        foreach (var name in new[] { "battery", "oxygenTank", "oil", "gunk" })
            result[name] = new Dictionary<string, object> { ["available"] = true };
        return result;
    }

    private static KeyNeeds Complete()
    {
        var result = new KeyNeeds();
        foreach (string field in new[] { "stamina", "calories", "stress", "bladder", "breath", "bodyTemperature" })
            result[field].Read(() => new RawNeedSample { Value = field == "calories" ? 2000000 : field == "bodyTemperature" ? 310 : 100, Unit = field == "bodyTemperature" ? "Kelvin" : "Flat" });
        return result;
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Need diagnostic regression: " + message);
    }
}
