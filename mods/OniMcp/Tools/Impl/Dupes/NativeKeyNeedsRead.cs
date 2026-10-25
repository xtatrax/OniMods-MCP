using System;
using System.Collections.Generic;
using Klei.AI;

namespace OniMcp.Tools
{
    internal static class NativeKeyNeedsRead
    {
        private static readonly string[] Keys = { "stamina", "calories", "stress", "bladder", "breath", "bodyTemperature" };

        internal static KeyNeeds Read(MinionIdentity dupe)
        {
            var result = new KeyNeeds();
            Database.Amounts definitions;
            string reason;
            if (!DupeAmountUtil.TryGetExistingAmountDefinitions(out definitions, out reason))
                return Unknown(result, reason);
            try
            {
                if (dupe == null) return Unknown(result, "dupe_missing");
                var modifiers = dupe.GetComponent<Modifiers>();
                if (modifiers == null || modifiers.gameObject != dupe.gameObject)
                    return Unknown(result, "amount_owner_unavailable");
                var amounts = modifiers.amounts;
                if (amounts == null || !ReferenceEquals(amounts, dupe.GetAmounts()) || amounts.ModifierList == null)
                    return Unknown(result, "amounts_unavailable");
                if (amounts.gameObject != dupe.gameObject)
                    return Unknown(result, "amount_owner_mismatch");

                var instances = new List<AmountInstance>(amounts.ModifierList);
                var configured = modifiers.initialAmounts == null ? null : new List<string>(modifiers.initialAmounts);
                var fixedDefinitions = new[] { definitions.Stamina, definitions.Calories, definitions.Stress,
                    definitions.Bladder, definitions.Breath, definitions.Temperature };
                for (int i = 0; i < Keys.Length; i++)
                    ReadValue(result[Keys[i]], fixedDefinitions[i], instances);

                int instantiated;
                string profileReason;
                bool profile;
                try
                {
                    profile = StockBionicProfile(dupe, modifiers, definitions, configured, instances,
                        out instantiated, out profileReason);
                }
                catch
                {
                    profile = false;
                    instantiated = 0;
                    profileReason = "native_profile_read_failed";
                }
                foreach (string key in new[] { "stamina", "calories", "bladder" })
                {
                    var reading = result[key];
                    int configuredCount = CountConfigured(configured, reading.AmountId);
                    int instanceCount;
                    Find(instances, reading.AmountId, out instanceCount);
                    var evidence = new Dictionary<string, object>
                    {
                        ["source"] = NeedReading.RegistrationSource,
                        ["modelId"] = ModelId(dupe),
                        ["configured"] = configuredCount < 0 ? null : (object)(configuredCount > 0),
                        ["configuredOccurrences"] = configuredCount < 0 ? null : (object)configuredCount,
                        ["instanceCount"] = instanceCount,
                        ["stockBionicProfileVerified"] = profile,
                        ["profileRegisteredCount"] = configured == null ? null : (object)configured.Count,
                        ["profileInstantiatedCount"] = profile ? (object)instantiated : null,
                        ["reasonCode"] = profileReason
                    };
                    if (configuredCount > 0 && reading.Status == "unknown" && reading.Reason == "amount_missing")
                        reading.Reason = "registered_amount_missing";
                    bool notApplicable = profile && configuredCount == 0 && instanceCount == 0
                        && reading.Status == "unknown" && reading.Reason == "amount_missing";
                    reading.SetRegistrationEvidence(evidence, notApplicable);
                }
                if (dupe.GetComponent<Modifiers>() != modifiers || modifiers.gameObject != dupe.gameObject
                    || !ReferenceEquals(modifiers.amounts, amounts) || amounts.gameObject != dupe.gameObject
                    || !ReferenceEquals(dupe.GetAmounts(), amounts))
                    return Unknown(result, "amount_owner_changed");
                return result;
            }
            catch
            {
                return Unknown(result, "read_failed");
            }
        }

        private static KeyNeeds Unknown(KeyNeeds result, string reason)
        {
            foreach (string key in Keys)
            {
                result[key].Read(() => null);
                result[key].Reason = reason;
            }
            return result;
        }

        private static void ReadValue(NeedReading reading, Amount definition, List<AmountInstance> instances)
        {
            if (definition == null || definition.Id != reading.AmountId)
            {
                reading.Reason = "amount_definition_unavailable";
                return;
            }
            int count;
            var instance = Find(instances, reading.AmountId, out count);
            if (count != 1)
            {
                reading.Reason = count == 0 ? "amount_missing" : "amount_ambiguous";
                return;
            }
            if (!ReferenceEquals(instance.amount, definition))
            {
                reading.Reason = "amount_definition_mismatch";
                return;
            }
            reading.Read(() => new RawNeedSample { Value = instance.value, Unit = definition.units.ToString() });
        }

        private static AmountInstance Find(List<AmountInstance> instances, string id, out int count)
        {
            count = 0;
            AmountInstance found = null;
            foreach (var instance in instances)
            {
                if (instance == null || instance.amount == null || instance.amount.Id != id) continue;
                count++;
                found = instance;
            }
            return found;
        }

        private static string ModelId(MinionIdentity dupe)
        {
            try { return dupe.model.ToString(); }
            catch { return null; }
        }

        private static int CountConfigured(List<string> configured, string id)
        {
            if (configured == null) return -1;
            int count = 0;
            foreach (string item in configured) if (item == id) count++;
            return count;
        }

        private static bool StockBionicProfile(MinionIdentity dupe, Modifiers valueOwner,
            Database.Amounts definitions, List<string> configured, List<AmountInstance> instances,
            out int instantiated, out string reason)
        {
            instantiated = 0;
            reason = "unknown_model";
            if (dupe.model != GameTags.Minions.Models.Bionic)
            {
                if (dupe.model == GameTags.Minions.Models.Standard) reason = "native_standard_model";
                return false;
            }
            reason = "native_profile_owner_mismatch";
            var modifiers = dupe.GetComponent<MinionModifiers>();
            if (modifiers == null || !ReferenceEquals(valueOwner, modifiers) || dupe.modifiers != modifiers
                || modifiers.gameObject != dupe.gameObject) return false;
            reason = "native_prefab_model_mismatch";
            var prefabId = dupe.GetComponent<KPrefabID>();
            if (prefabId == null || prefabId.PrefabTag != GameTags.Minions.Models.Bionic
                || prefabId.PrefabTag.ToString() != "BionicMinion") return false;
            reason = "amount_lifecycle_pending";
            if (!dupe.IsInitialized() || !dupe.isSpawned || !modifiers.IsInitialized() || !modifiers.isSpawned)
                return false;
            reason = "native_profile_mismatch";
            var expected = new[] { definitions.HitPoints, definitions.ImmuneLevel, definitions.Breath,
                definitions.Stress, definitions.Toxicity, definitions.Temperature, definitions.Decor,
                definitions.RadiationBalance, definitions.BionicOil, definitions.BionicGunk,
                definitions.BionicInternalBattery, definitions.BionicOxygenTank };
            var ids = new[] { "HitPoints", "ImmuneLevel", "Breath", "Stress", "Toxicity", "Temperature",
                "Decor", "RadiationBalance", "BionicOil", "BionicGunk", "BionicInternalBattery", "BionicOxygenTank" };
            if (configured == null || configured.Count != expected.Length) return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < expected.Length; i++)
            {
                var definition = expected[i];
                if (definition == null || definition.Id != ids[i] || !unique.Add(definition.Id)
                    || CountConfigured(configured, definition.Id) != 1) return false;
            }
            // Additional runtime disease amounts are native; malformed or duplicate entries are not.
            var runtimeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var instance in instances)
            {
                if (instance == null || instance.amount == null || string.IsNullOrEmpty(instance.amount.Id)
                    || !runtimeIds.Add(instance.amount.Id)) return false;
            }
            reason = "registered_profile_incomplete";
            foreach (var definition in expected)
            {
                int count;
                var instance = Find(instances, definition.Id, out count);
                if (count != 1 || !ReferenceEquals(instance.amount, definition)) return false;
                instantiated++;
            }
            reason = "native_initial_amount_profile_observed";
            return true;
        }
    }
}
