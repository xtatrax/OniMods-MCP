using System;
using System.Collections.Generic;
using Klei.AI;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
    {
        private static Dictionary<string, object> NativeAmountDetails(MinionIdentity dupe)
        {
            var result = new Dictionary<string, object>();
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
