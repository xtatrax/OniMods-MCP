using System;
using System.Collections.Generic;
using System.Linq;
using Klei.AI;

namespace OniMcp.Tools
{
    internal static class BasicDuplicantVitals
    {
        private const string AmountSource = "MinionIdentity.GetAmounts().ModifierList";
        private const string HealthSource = "Health.hitPoints/maxHitPoints";

        internal static Dictionary<string, object> Read(MinionIdentity minion)
        {
            var localized = new Dictionary<string, object>();
            var byId = new Dictionary<string, object>();
            var reasons = new List<string>();
            bool acquired = false;
            bool completed = false;
            try
            {
                var amounts = minion == null ? null : minion.GetAmounts();
                if (amounts == null)
                    AddReason(reasons, "amounts_missing");
                else if (amounts.ModifierList == null)
                    AddReason(reasons, "collection_missing");
                else
                {
                    acquired = true;
                    foreach (var instance in amounts.ModifierList)
                    {
                        if (instance == null || instance.amount == null)
                        {
                            AddReason(reasons, "invalid_entry");
                            continue;
                        }
                        var amount = instance.amount;
                        float raw = instance.value;
                        object value = Finite(raw) ? (object)raw : null;
                        if (value == null) AddReason(reasons, "non_finite_value");
                        // Localized-name collisions retain the legacy last-value behavior.
                        // The native-ID view keeps distinct amounts with the same name.
                        if (string.IsNullOrEmpty(amount.Name)) AddReason(reasons, "name_missing");
                        else localized[amount.Name] = value;
                        if (string.IsNullOrEmpty(amount.Id)) AddReason(reasons, "id_missing");
                        else byId[amount.Id] = value;
                    }
                    completed = true;
                }
            }
            catch
            {
                // A failed enumeration must not look like a complete partial list.
                localized.Clear();
                byId.Clear();
                AddReason(reasons, "read_failed");
            }

            return new Dictionary<string, object>
            {
                ["amounts"] = localized,
                ["amountsById"] = byId,
                ["amountsEvidence"] = new Dictionary<string, object>
                {
                    ["available"] = completed && reasons.Count == 0,
                    ["readState"] = completed && reasons.Count == 0 ? "available"
                        : completed && (localized.Values.Any(value => value != null) || byId.Values.Any(value => value != null)) ? "partial" : "unavailable",
                    ["source"] = acquired ? AmountSource : null,
                    ["reasonCodes"] = reasons
                },
                ["health"] = ReadHealth(minion)
            };
        }

        private static Dictionary<string, object> ReadHealth(MinionIdentity minion)
        {
            object hitPoints = null;
            object maximum = null;
            bool acquired = false;
            var reasons = new List<string>();
            try
            {
                var health = minion == null ? null : minion.GetComponent<Health>();
                if (health == null) AddReason(reasons, "component_missing");
                else
                {
                    acquired = true;
                    float current = health.hitPoints;
                    float max = health.maxHitPoints;
                    if (Finite(current)) hitPoints = current;
                    else AddReason(reasons, "non_finite_hit_points");
                    if (Finite(max) && max > 0f) maximum = max;
                    else AddReason(reasons, Finite(max) ? "invalid_maximum" : "non_finite_maximum");
                }
            }
            catch { AddReason(reasons, "read_failed"); }

            bool available = hitPoints != null && maximum != null && reasons.Count == 0;
            return new Dictionary<string, object>
            {
                ["hitPoints"] = hitPoints,
                ["maxHitPoints"] = maximum,
                ["available"] = available,
                ["readState"] = available ? "available" : hitPoints != null || maximum != null ? "partial" : "unavailable",
                ["source"] = acquired ? HealthSource : null,
                ["reasonCodes"] = reasons
            };
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void AddReason(List<string> reasons, string reason)
        {
            if (!reasons.Contains(reason)) reasons.Add(reason);
        }
    }
}
