using System;
using System.Collections.Generic;
using Klei.AI;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
    {
        private static Dictionary<string, object> NativeAmountDetails(MinionIdentity dupe, bool includeBreathDetails = false)
        {
            var result = new Dictionary<string, object>();
            if (includeBreathDetails) result["Breath"] = NativeBreathAmountDetails(dupe);
            var amounts = dupe.GetAmounts();
            if (amounts == null) return result;
            foreach (var instance in amounts.ModifierList)
            {
                if (instance?.amount == null) continue;
                var amount = instance.amount;
                if (amount.Id != "BionicOil" && amount.Id != "BionicGunk"
                    && amount.Id != "BionicOxygenTank" && amount.Id != "BionicInternalBattery") continue;
                var item = NativeAmountSnapshot.Read(instance.value, instance.GetMin, instance.GetMax, instance.GetDelta);
                item["id"] = amount.Id;
                item["name"] = amount.Name;
                item["nativeUnits"] = amount.units.ToString();
                item["rawUnit"] = amount.Id == "BionicInternalBattery" ? "J"
                    : amount.Id == "BionicOil" || amount.Id == "BionicGunk" || amount.Id == "BionicOxygenTank" ? "kg" : null;
                item["rawUnitSource"] = item["rawUnit"] == null ? null : "Game amount displayer / native monitor mass or energy contract";
                try { item["displayValue"] = instance.GetValueString(); item["displayValueAvailable"] = true; }
                catch (Exception ex) { item["displayValue"] = null; item["displayValueAvailable"] = false; item["displayValueUnavailableReason"] = ex.GetType().Name; }
                result[amount.Id] = item;
            }
            return result;
        }

        private static Dictionary<string, object> NativeBreathAmountDetails(MinionIdentity dupe)
        {
            var sources = new Dictionary<string, object>
            {
                ["value"] = null, ["maximum"] = null, ["nativeUnits"] = null, ["displayValue"] = null
            };
            var reasons = new Dictionary<string, object>();
            var result = new Dictionary<string, object>
            {
                ["id"] = "Breath", ["value"] = null, ["maximum"] = null,
                ["nativeUnits"] = null, ["rawUnit"] = null,
                ["rawUnitUnavailableReason"] = "native_physical_unit_not_exposed",
                ["displayValue"] = null, ["available"] = false, ["status"] = "unknown",
                ["attemptedSource"] = "Db._Instance.Amounts.Breath.Lookup(dupe)",
                ["fieldSources"] = sources, ["unavailableReasons"] = reasons
            };
            if (!DupeAmountUtil.TryGetBreathInstance(dupe, out var instance, out var reason))
            {
                foreach (string field in new[] { "value", "maximum", "nativeUnits", "displayValue" })
                    reasons[field] = reason;
                return result;
            }
            result["value"] = ReadBreathNumber(() => instance.value, "value", "AmountInstance.value", sources, reasons);
            result["maximum"] = ReadBreathNumber(instance.GetMax, "maximum", "AmountInstance.GetMax()", sources, reasons);
            try
            {
                if (instance.amount == null) reasons["nativeUnits"] = "instance_amount_missing";
                else
                {
                    result["nativeUnits"] = instance.amount.units.ToString();
                    sources["nativeUnits"] = "AmountInstance.amount.units";
                }
            }
            catch (Exception ex) { reasons["nativeUnits"] = "read_failed:" + ex.GetType().Name; }
            if (result["value"] != null && result["maximum"] != null)
            {
                try
                {
                    string display = instance.GetValueString();
                    if (string.IsNullOrEmpty(display)) reasons["displayValue"] = "empty_native_display";
                    else
                    {
                        result["displayValue"] = display;
                        sources["displayValue"] = "AmountInstance.GetValueString()";
                    }
                }
                catch (Exception ex) { reasons["displayValue"] = "read_failed:" + ex.GetType().Name; }
            }
            else reasons["displayValue"] = "value_or_maximum_unavailable";
            bool available = reasons.Count == 0;
            result["available"] = available;
            result["status"] = available ? "observed"
                : result["value"] != null || result["maximum"] != null || result["nativeUnits"] != null || result["displayValue"] != null
                    ? "partial" : "unknown";
            return result;
        }

        private static object ReadBreathNumber(Func<float> read, string field, string source,
            Dictionary<string, object> sources, Dictionary<string, object> reasons)
        {
            try
            {
                float value = read();
                if (float.IsNaN(value) || float.IsInfinity(value)) { reasons[field] = "non_finite_value"; return null; }
                sources[field] = source;
                return (double)value;
            }
            catch (Exception ex) { reasons[field] = "read_failed:" + ex.GetType().Name; return null; }
        }

        private static object NativeBionicNeeds(MinionIdentity dupe, bool includeAmounts = true)
        {
            if (dupe.GetComponent<KPrefabID>()?.PrefabTag.ToString() != "BionicMinion") return null;
            var result = new Dictionary<string, object>();
            if (includeAmounts) result["amounts"] = NativeAmountDetails(dupe);
            result["oil"] = ReadBionicMonitor("BionicOilMonitor", () =>
            {
                var m = dupe.GetSMI<BionicOilMonitor.Instance>();
                if (m == null) throw new InvalidOperationException("monitor_missing");
                return new Dictionary<string, object>
                {
                    ["value"] = m.CurrentOilMass, ["maximum"] = m.oilAmount.GetMax(), ["unit"] = "kg",
                    ["fraction"] = m.CurrentOilPercentage,
                    ["refillThresholdFraction"] = BionicOilMonitor.OIL_REFILL_TRESHOLD,
                    ["refillThresholdSource"] = "BionicOilMonitor.OIL_REFILL_TRESHOLD (fraction <= threshold)",
                    ["wantsOilChange"] = BionicOilMonitor.WantsOilChange(m), ["hasOil"] = m.HasOil,
                    ["online"] = m.IsOnline
                };
            });
            result["oxygenTank"] = ReadBionicMonitor("BionicOxygenTankMonitor", () =>
            {
                var m = dupe.GetSMI<BionicOxygenTankMonitor.Instance>();
                if (m?.storage == null) throw new InvalidOperationException("monitor_or_storage_missing");
                return new Dictionary<string, object>
                {
                    ["value"] = m.AvailableOxygen, ["maximum"] = m.storage.capacityKg, ["unit"] = "kg",
                    ["fraction"] = m.OxygenPercentage,
                    ["safeThresholdFraction"] = BionicOxygenTankMonitor.SAFE_TRESHOLD,
                    ["criticalThresholdFraction"] = BionicOxygenTankMonitor.CRITICAL_TRESHOLD,
                    ["safe"] = BionicOxygenTankMonitor.AreOxygenLevelsSafe(m),
                    ["critical"] = BionicOxygenTankMonitor.AreOxygenLevelsCritical(m), ["empty"] = m.IsEmpty,
                    ["thresholdSource"] = "BionicOxygenTankMonitor.SAFE_TRESHOLD (>=) / CRITICAL_TRESHOLD (<=)",
                    ["recoveringFromSuffocation"] = m.isRecoveringFromSuffocation
                };
            });
            result["gunk"] = ReadBionicMonitor("GunkMonitor", () =>
            {
                var m = dupe.GetSMI<GunkMonitor.Instance>();
                if (m == null) throw new InvalidOperationException("monitor_missing");
                return new Dictionary<string, object>
                {
                    ["value"] = m.CurrentGunkMass, ["unit"] = "kg", ["fraction"] = m.CurrentGunkPercentage,
                    ["mildUrgeThresholdFraction"] = m.def.SeekForGunkToiletTreshold_InSchedule,
                    ["criticalUrgeThresholdFraction"] = m.def.DesperetlySeekForGunkToiletTreshold,
                    ["thresholdSource"] = "GunkMonitor.Instance.def (fraction >= threshold)",
                    ["mildUrge"] = GunkMonitor.IsGunkLevelsOverMildUrgeThreshold(m),
                    ["criticalUrge"] = GunkMonitor.IsGunkLevelsOverCriticalUrgeThreshold(m),
                    ["atMaximum"] = m.IsGunkBuildupAtMax, ["scheduleAllowsExpelling"] = m.DoesCurrentScheduleAllowsGunkToilet
                };
            });
            result["battery"] = ReadBionicMonitor("BionicBatteryMonitor", () =>
            {
                var m = dupe.GetSMI<BionicBatteryMonitor.Instance>();
                if (m == null) throw new InvalidOperationException("monitor_missing");
                return new Dictionary<string, object>
                {
                    ["value"] = m.CurrentCharge, ["unit"] = "J", ["wattage"] = m.Wattage,
                    ["chargedBankCount"] = m.ChargedElectrobankCount, ["depletedBankCount"] = m.DepletedElectrobankCount,
                    ["bankCountCapacity"] = m.ElectrobankCountCapacity, ["online"] = m.IsOnline,
                    ["criticallyLow"] = BionicBatteryMonitor.IsCriticallyLow(m),
                    ["belowNotificationThreshold"] = BionicBatteryMonitor.ChargeIsBelowNotificationThreshold(m),
                    ["noCharge"] = BionicBatteryMonitor.DoesNotHaveCharge(m),
                    ["notificationThresholdValue"] = null, ["criticalCountThresholdValue"] = null,
                    ["thresholdValueUnavailableReason"] = "public_predicate_only",
                    ["predicateSources"] = new[] { "BionicBatteryMonitor.IsCriticallyLow", "BionicBatteryMonitor.ChargeIsBelowNotificationThreshold", "BionicBatteryMonitor.DoesNotHaveCharge" }
                };
            });
            return result;
        }

        private static object ReadBionicMonitor(string source, Func<Dictionary<string, object>> read)
        {
            try
            {
                var result = read(); result["available"] = true; result["source"] = source + " public instance properties / predicates";
                foreach (var key in new List<string>(result.Keys))
                    if (result[key] is float v && (float.IsNaN(v) || float.IsInfinity(v)))
                        return new Dictionary<string, object> { ["available"] = false, ["source"] = source, ["reason"] = key + ":non_finite" };
                return result;
            }
            catch (Exception ex) { return new Dictionary<string, object> { ["available"] = false, ["source"] = source, ["reason"] = ex.GetType().Name + ":" + ex.Message }; }
        }
    }
}
