using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Klei.AI;

namespace OniMcp.Tools
{
    public static partial class SnapshotTools
    {
        private static readonly FieldInfo SnapshotExistingDb = BindSnapshotExistingDb();

        private static FieldInfo BindSnapshotExistingDb()
        {
            try
            {
                var field = typeof(Db).GetField("_Instance", BindingFlags.Static | BindingFlags.NonPublic);
                return field != null && field.DeclaringType == typeof(Db) && field.FieldType == typeof(Db)
                    && field.IsStatic ? field : null;
            }
            catch { return null; }
        }

        private sealed class SnapshotNeedReading
        {
            internal float? Value;
            internal string Unit;
            internal string Reason;
            internal readonly string AmountId;
            internal string Source => "Db.Amounts." + AmountId + ".Lookup(MinionIdentity).value";

            internal SnapshotNeedReading(string amountId) { AmountId = amountId; }

            internal Dictionary<string, object> Evidence()
            {
                return new Dictionary<string, object>
                {
                    ["available"] = Value.HasValue,
                    ["status"] = Value.HasValue ? "observed" : "unknown",
                    ["source"] = Value.HasValue ? Source : null,
                    ["attemptedSource"] = Source,
                    ["amountId"] = AmountId,
                    ["unit"] = Unit,
                    ["valueFormat"] = "raw_amount",
                    ["reasonCode"] = Reason
                };
            }
        }

        private sealed class SnapshotNeedReadings
        {
            internal SnapshotNeedReading Stress;
            internal SnapshotNeedReading Stamina;
            internal SnapshotNeedReading Calories;

            internal Dictionary<string, object> Evidence()
            {
                return new Dictionary<string, object>
                {
                    ["stress"] = Stress.Evidence(),
                    ["stamina"] = Stamina.Evidence(),
                    ["calories"] = Calories.Evidence()
                };
            }
        }

        private static SnapshotNeedReadings ReadSnapshotNeeds(MinionIdentity dupe)
        {
            Db db = null;
            string failure = null;
            if (SnapshotExistingDb == null) failure = "database_binding_unavailable";
            else
            {
                try
                {
                    db = SnapshotExistingDb.GetValue(null) as Db;
                    if (db == null) failure = "database_cache_missing";
                }
                catch { failure = "database_cache_read_failed"; }
            }
            return new SnapshotNeedReadings
            {
                Stress = ReadSnapshotNeed(dupe, db, failure, "Stress", definitions => definitions.Stress),
                Stamina = ReadSnapshotNeed(dupe, db, failure, "Stamina", definitions => definitions.Stamina),
                Calories = ReadSnapshotNeed(dupe, db, failure, "Calories", definitions => definitions.Calories)
            };
        }

        private static SnapshotNeedReading ReadSnapshotNeed(MinionIdentity dupe, Db db, string cacheFailure,
            string id, Func<Database.Amounts, Amount> select)
        {
            var reading = new SnapshotNeedReading(id) { Reason = cacheFailure };
            if (cacheFailure != null) return reading;
            Amount definition;
            try
            {
                var definitions = db.Amounts;
                if (definitions == null) { reading.Reason = "amount_definitions_missing"; return reading; }
                definition = select(definitions);
                if (definition == null) { reading.Reason = "amount_definition_missing"; return reading; }
                reading.Unit = definition.units.ToString();
            }
            catch { reading.Reason = "amount_definition_read_failed"; return reading; }
            if (dupe == null) { reading.Reason = "duplicant_missing"; return reading; }
            AmountInstance instance;
            try { instance = definition.Lookup(dupe); }
            catch { reading.Reason = "amount_lookup_failed"; return reading; }
            if (instance == null) { reading.Reason = "amount_instance_missing"; return reading; }
            try
            {
                float raw = instance.value;
                if (float.IsNaN(raw) || float.IsInfinity(raw)) reading.Reason = "non_finite_value";
                else { reading.Value = raw; reading.Reason = null; }
            }
            catch { reading.Reason = "amount_value_read_failed"; }
            return reading;
        }

        private static bool KnownSnapshotNeed(float? value)
        {
            return value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value);
        }

        private static object RoundedSnapshotNeed(float? value)
        {
            return KnownSnapshotNeed(value) ? (object)Math.Round(value.Value, 1) : null;
        }

        private static Dictionary<string, object> SnapshotNeedsCoverage(List<DupeSnapshot> dupes)
        {
            return new Dictionary<string, object>
            {
                ["stress"] = SnapshotNeedCoverage(dupes, item => item.Stress),
                ["stamina"] = SnapshotNeedCoverage(dupes, item => item.Stamina),
                ["calories"] = SnapshotNeedCoverage(dupes, item => item.Calories)
            };
        }

        private static Dictionary<string, object> SnapshotNeedCoverage(List<DupeSnapshot> dupes,
            Func<DupeSnapshot, float?> select)
        {
            int observed = dupes.Count(item => KnownSnapshotNeed(select(item)));
            return new Dictionary<string, object>
            {
                ["observed"] = observed,
                ["unknown"] = dupes.Count - observed,
                ["complete"] = observed == dupes.Count
            };
        }

        private static bool SnapshotNeedsComplete(List<DupeSnapshot> dupes)
        {
            return dupes.All(item => KnownSnapshotNeed(item.Stress)
                && KnownSnapshotNeed(item.Stamina) && KnownSnapshotNeed(item.Calories));
        }

        private static object SnapshotMaxStress(List<DupeSnapshot> dupes)
        {
            return dupes.Count > 0 && dupes.All(item => KnownSnapshotNeed(item.Stress))
                ? (object)Math.Round(dupes.Max(item => item.Stress.Value), 1) : null;
        }

        private static object SnapshotStressedCount(List<DupeSnapshot> dupes)
        {
            return dupes.All(item => KnownSnapshotNeed(item.Stress))
                ? (object)dupes.Count(item => item.Stress.Value >= 40f) : null;
        }

        private static object SnapshotLowStaminaCount(List<DupeSnapshot> dupes)
        {
            return dupes.All(item => KnownSnapshotNeed(item.Stamina))
                ? (object)dupes.Count(item => item.Stamina.Value >= 0f && item.Stamina.Value < 30f) : null;
        }
    }
}
