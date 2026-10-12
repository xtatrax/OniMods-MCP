using System;
using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    internal sealed class RawNeedSample
    {
        internal float Value;
        internal string Unit;
    }

    internal sealed class NeedReading
    {
        internal float? Value;
        internal string Status = "unknown";
        internal string Reason = "not_read";
        internal string Unit;
        internal readonly string AmountId;
        internal const string Source = "Db.Amounts.Lookup(MinionIdentity).value";

        internal NeedReading(string amountId) { AmountId = amountId; }

        internal void Read(Func<RawNeedSample> getter)
        {
            // A retry must not leave an earlier successful value behind.
            Value = null;
            Unit = null;
            Status = "unknown";
            Reason = "amount_missing";
            try
            {
                var sample = getter();
                if (sample == null) return;
                Unit = sample.Unit;
                if (float.IsNaN(sample.Value) || float.IsInfinity(sample.Value))
                {
                    Reason = "non_finite_value";
                    return;
                }
                Value = sample.Value;
                Status = "observed";
                Reason = null;
            }
            catch
            {
                Reason = "read_failed";
            }
        }

        internal object RoundedValue => Value.HasValue ? (object)Math.Round(Value.Value, 2) : null;

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["status"] = Status,
                ["available"] = Value.HasValue,
                ["source"] = Value.HasValue ? Source : null,
                ["attemptedSource"] = Source,
                ["amountId"] = AmountId,
                ["unit"] = Unit,
                ["valueFormat"] = "raw_amount",
                ["reasonCode"] = Reason
            };
        }
    }

    internal sealed class KeyNeeds
    {
        private readonly Dictionary<string, NeedReading> readings = new Dictionary<string, NeedReading>
        {
            ["stamina"] = new NeedReading("Stamina"),
            ["calories"] = new NeedReading("Calories"),
            ["stress"] = new NeedReading("Stress"),
            ["bladder"] = new NeedReading("Bladder"),
            ["breath"] = new NeedReading("Breath"),
            ["bodyTemperature"] = new NeedReading("Temperature")
        };

        internal NeedReading this[string name] => readings[name];
        internal float? Stamina => readings["stamina"].Value;
        internal float? Calories => readings["calories"].Value;
        internal float? Breath => readings["breath"].Value;
        internal bool IsComplete => readings.Values.All(r => r.Value.HasValue);
        internal string[] UnknownFields => readings.Where(r => !r.Value.Value.HasValue).Select(r => r.Key).ToArray();

        internal Dictionary<string, object> ToDictionary()
        {
            return readings.ToDictionary(kv => kv.Key, kv => kv.Value.RoundedValue);
        }

        internal Dictionary<string, object> EvidenceDictionary()
        {
            return readings.ToDictionary(kv => kv.Key, kv => (object)kv.Value.ToDictionary());
        }

        internal void AddRiskReasons(List<string> reasons)
        {
            if (Stamina.HasValue && Stamina.Value >= 0f && Stamina.Value < 15f) reasons.Add("low_stamina");
            if (Calories.HasValue && Calories.Value >= 0f && Calories.Value < 1000f) reasons.Add("low_calories");
            if (Breath.HasValue && Breath.Value < 35f) reasons.Add("low_breath");
        }

        internal string RiskWithCoverage(List<string> reasons, bool bionicCoverageComplete = true)
        {
            string risk = reasons.Contains("invalid_current_cell") || reasons.Contains("no_reachable_nearby_cells") || reasons.Contains("low_breath") || reasons.Contains("bionic_battery_empty") || reasons.Contains("bionic_oxygen_empty")
                ? "critical" : reasons.Count > 0 ? "warning" : IsComplete && bionicCoverageComplete ? "ok" : "unknown";
            if (!IsComplete) reasons.Add("needs_unavailable");
            if (!bionicCoverageComplete) reasons.Add("bionic_needs_unavailable");
            return risk;
        }
    }

    internal static class BionicRiskAssessment
    {
        internal static bool AddReasons(object snapshot, Dictionary<string, object> chore, List<string> reasons)
        {
            if (snapshot == null) return true; // Ordinary duplicant.
            var monitors = snapshot as Dictionary<string, object>;
            bool complete = true;
            foreach (var name in new[] { "battery", "oxygenTank", "oil", "gunk" })
            {
                object value;
                var monitor = monitors != null && monitors.TryGetValue(name, out value)
                    ? value as Dictionary<string, object> : null;
                if (!Flag(monitor, "available")) { complete = false; continue; }
                if (name == "battery")
                {
                    if (Flag(monitor, "noCharge")) reasons.Add("bionic_battery_empty");
                    else if (Flag(monitor, "criticallyLow") || Flag(monitor, "belowNotificationThreshold")) reasons.Add("bionic_battery_low");
                }
                else if (name == "oxygenTank")
                {
                    if (Flag(monitor, "empty") || Flag(monitor, "critical")) reasons.Add("bionic_oxygen_empty");
                    // safe=false is a refill threshold, not proof of suffocation.
                }
                else if (name == "oil")
                {
                    object hasOil;
                    if (monitor.TryGetValue("hasOil", out hasOil) && hasOil is bool && !(bool)hasOil) reasons.Add("bionic_oil_empty");
                    else if (Flag(monitor, "wantsOilChange")) reasons.Add("bionic_oil_refill_needed");
                }
                else if (Flag(monitor, "criticalUrge") || Flag(monitor, "atMaximum")) reasons.Add("bionic_gunk_urgent");
            }
            object runtime;
            if (chore != null && chore.TryGetValue("runtimeType", out runtime) && runtime as string == "BionicGunkSpillChore")
                reasons.Add("bionic_gunk_spill");
            return complete;
        }

        private static bool Flag(Dictionary<string, object> monitor, string key)
        {
            object value;
            return monitor != null && monitor.TryGetValue(key, out value) && value is bool && (bool)value;
        }
    }

}
