using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    internal sealed class DupeStressSummary
    {
        internal int Observed { get; private set; }
        internal int Unknown { get; private set; }
        internal bool Complete => Unknown == 0;
        internal float? ObservedMaximum { get; private set; }
        internal object RoundedMaximum => Complete && ObservedMaximum.HasValue
            ? (object)Math.Round(ObservedMaximum.Value, 1) : JValue.CreateNull();

        internal static DupeStressSummary Read(IEnumerable<MinionIdentity> dupes)
        {
            var summary = new DupeStressSummary();
            if (dupes == null) throw new ArgumentNullException(nameof(dupes));
            foreach (var dupe in dupes)
            {
                if (!DupeAmountUtil.TryGetStressValue(dupe, out float value))
                {
                    summary.Unknown++;
                    continue;
                }
                summary.Observed++;
                if (!summary.ObservedMaximum.HasValue || value > summary.ObservedMaximum.Value)
                    summary.ObservedMaximum = value;
            }
            return summary;
        }

        internal Dictionary<string, object> Coverage()
        {
            return new Dictionary<string, object>
            {
                ["observed"] = Observed,
                ["unknown"] = Unknown,
                ["complete"] = Complete,
                ["status"] = Observed + Unknown == 0 ? "empty"
                    : Complete ? "complete" : Observed == 0 ? "unknown" : "partial"
            };
        }

        internal string WarningText()
        {
            return ObservedMaximum.HasValue
                ? McpLocalization.Format(Complete ? "diagnostic_stress.warning.complete"
                    : "diagnostic_stress.warning.partial", null, Math.Round(ObservedMaximum.Value, 1))
                : null;
        }
    }
}
