using System;
using System.Reflection;
using Klei.AI;

namespace OniMcp.Tools
{
    internal static class DupeAmountUtil
    {
        private static readonly FieldInfo ExistingDb = BindExistingDb();

        private static FieldInfo BindExistingDb()
        {
            try
            {
                var field = typeof(Db).GetField("_Instance", BindingFlags.Static | BindingFlags.NonPublic);
                return field != null && field.DeclaringType == typeof(Db) && field.FieldType == typeof(Db)
                    && field.IsStatic ? field : null;
            }
            catch { return null; }
        }

        public static bool TryGetStressValue(MinionIdentity dupe, out float value)
        {
            value = 0f;
            if (dupe == null || ExistingDb == null) return false;
            try
            {
                var db = ExistingDb.GetValue(null) as Db;
                var stress = db?.Amounts?.Stress?.Lookup(dupe);
                if (stress == null) return false;
                float raw = stress.value;
                if (float.IsNaN(raw) || float.IsInfinity(raw)) return false;
                value = raw;
                return true;
            }
            catch { return false; }
        }

        // Retained for historical callers; active diagnostics use the nullable stress summary.
        public static float StressValue(MinionIdentity dupe, float fallback = 0f)
        {
            return TryGetStressValue(dupe, out var value)
                ? value
                : AmountValueByName(dupe, "Stress", fallback);
        }

        public static float AmountValueByName(MinionIdentity dupe, string query, float fallback = 0f)
        {
            var amounts = dupe?.GetComponent<Amounts>();
            if (amounts == null)
                return fallback;

            foreach (var amount in amounts.ModifierList)
            {
                if (amount == null || amount.amount == null)
                    continue;
                string id = amount.amount.Id ?? "";
                string name = amount.amount.Name ?? "";
                if (Contains(id, query) || Contains(name, query))
                    return ToolUtil.SafeFloat(amount.value);
            }

            return fallback;
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value)
                   && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
