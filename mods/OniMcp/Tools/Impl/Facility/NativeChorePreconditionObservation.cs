using System;
using System.Collections.Generic;
using System.Reflection;

namespace OniMcp.Tools
{
    // Observe an existing native cache; never sort it or initialize its singleton.
    internal static class NativeChorePreconditionObservation
    {
        private static readonly FieldInfo Preconditions = BindField(typeof(StandardChoreBase), "preconditions", typeof(List<Chore.PreconditionInstance>), false);
        private static readonly FieldInfo Dirty = BindField(typeof(StandardChoreBase), "arePreconditionsDirty", typeof(bool), false);
        private static readonly FieldInfo ExistingRules = BindField(typeof(ChorePreconditions), "_instance", typeof(ChorePreconditions), true);

        internal sealed class Observation
        {
            internal bool? PotentialSuccess;
            internal string State;
            internal Dictionary<string, object> FailedPrecondition;
        }

        private static FieldInfo BindField(Type owner, string name, Type type, bool isStatic)
        {
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                    | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                var field = owner.GetField(name, flags);
                return field != null && field.DeclaringType == owner && field.FieldType == type
                    && field.IsStatic == isStatic ? field : null;
            }
            catch { return null; }
        }

        internal static Observation Read(Chore.Precondition.Context context)
        {
            var result = new Observation { State = "unavailable" };
            if (context.chore == null || Preconditions == null || Dirty == null || ExistingRules == null)
                return Unknown(result, context.failedPreconditionId);

            try
            {
                // An override may evaluate or refresh state even when the base cache is clean.
                var method = context.chore.GetType().GetMethod("GetPreconditions", BindingFlags.Public | BindingFlags.Instance,
                    null, Type.EmptyTypes, null);
                var driver = context.chore.GetType().GetProperty("driver", BindingFlags.Public | BindingFlags.Instance);
                if (!(context.chore is StandardChoreBase) || method == null
                    || method.DeclaringType != typeof(StandardChoreBase)
                    || method.ReturnType != typeof(List<Chore.PreconditionInstance>)
                    || driver == null || driver.PropertyType != typeof(ChoreDriver)
                    || driver.GetGetMethod()?.DeclaringType != typeof(StandardChoreBase))
                    return Unknown(result, context.failedPreconditionId);

                var cached = Preconditions.GetValue(context.chore) as List<Chore.PreconditionInstance>;
                if (cached == null)
                    return Unknown(result, context.failedPreconditionId);

                // Match the native getter's synchronization without consuming its dirty state.
                lock (cached)
                {
                    if ((bool)Dirty.GetValue(context.chore))
                    {
                        result.State = "pending";
                        return Unknown(result, context.failedPreconditionId);
                    }
                    if (ExistingRules.GetValue(null) == null)
                        return Unknown(result, context.failedPreconditionId);

                    if (context.failedPreconditionId >= cached.Count || context.failedPreconditionId < -1)
                    {
                        result.State = "out_of_range";
                        return Unknown(result, context.failedPreconditionId);
                    }

                    // The native eligibility rule is used only with an already-ready cache.
                    result.PotentialSuccess = context.IsPotentialSuccess();
                    result.State = "ready";
                    if (context.failedPreconditionId >= 0)
                    {
                        var condition = cached[context.failedPreconditionId].condition;
                        result.FailedPrecondition = new Dictionary<string, object>
                        {
                            ["index"] = context.failedPreconditionId,
                            ["id"] = condition.id,
                            ["description"] = condition.description,
                            ["state"] = result.State
                        };
                    }
                    return result;
                }
            }
            catch
            {
                result.PotentialSuccess = null;
                result.State = "unavailable";
                return Unknown(result, context.failedPreconditionId);
            }
        }

        private static Observation Unknown(Observation result, int failedIndex)
        {
            if (failedIndex >= 0 || result.State == "out_of_range")
            {
                result.FailedPrecondition = new Dictionary<string, object>
                {
                    ["index"] = failedIndex,
                    ["id"] = result.State == "out_of_range" ? "out_of_range" : null,
                    ["description"] = null,
                    ["state"] = result.State
                };
            }
            return result;
        }
    }
}
