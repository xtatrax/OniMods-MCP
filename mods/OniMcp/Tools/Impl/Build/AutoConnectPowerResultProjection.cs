using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    // Project the child result only; never infer construction or physical flow.
    internal static class AutoConnectPowerResultProjection
    {
        private static readonly string[] CountFields =
            { "planned", "reusedExisting", "failed", "digNewlyQueued", "digAlreadyQueued" };
        private static readonly string[] FlagFields = { "success", "complete", "committed", "dryRun" };

        internal static Dictionary<string, object> Project(CallToolResult child, bool dryRun)
        {
            string primary = child?.Content != null && child.Content.Count > 0 ? child.Content[0]?.Text : null;
            JObject payload = null;
            if (!string.IsNullOrWhiteSpace(primary) && child.Content[0].Type == "text")
            {
                try { payload = JObject.Parse(primary); }
                catch (JsonException) { }
            }
            var result = new Dictionary<string, object>
            {
                ["enabled"] = true, ["dryRun"] = dryRun,
                ["childIsError"] = child == null ? (object)JValue.CreateNull() : child.IsError,
                ["countsScope"] = dryRun ? "preview" : "observed",
                ["connectResult"] = payload == null ? (object)JValue.CreateNull() : payload
            };
            var unknownCounts = new List<string>();
            var unknownFlags = new List<string>();
            var counts = new Dictionary<string, int?>();
            foreach (string field in CountFields)
            {
                int? value = Count(payload?[field]);
                counts[field] = value;
                if (!value.HasValue) unknownCounts.Add(field);
            }
            var flags = new Dictionary<string, bool?>();
            foreach (string field in FlagFields)
            {
                JToken token = payload?[field];
                bool? value = token?.Type == JTokenType.Boolean ? (bool?)token : null;
                flags[field] = value;
                if (!value.HasValue) unknownFlags.Add(field);
            }
            bool? success = flags["success"], complete = flags["complete"], committed = flags["committed"];
            bool? childDryRun = flags["dryRun"];
            int? planned = counts["planned"], reused = counts["reusedExisting"], failed = counts["failed"];
            int? autoDigQueued = counts["digNewlyQueued"];
            bool contradiction = childDryRun.HasValue && childDryRun.Value != dryRun;
            if (dryRun && (planned > 0 || committed == true || autoDigQueued > 0))
            {
                // A preview cannot establish an executed reservation, even if its child claims one.
                contradiction = true;
                planned = null;
                if (!unknownCounts.Contains("planned")) unknownCounts.Add("planned");
                if (autoDigQueued > 0)
                {
                    autoDigQueued = null;
                    unknownCounts.Add("digNewlyQueued");
                }
            }
            bool refused = child?.IsError == true || success == false || complete == false || failed > 0
                || (!dryRun && committed == false);
            string status;
            if (payload == null)
                status = "connect_result_unparseable";
            else if (contradiction)
                status = "unknown";
            else if (refused)
                status = !dryRun && childDryRun == false && committed == true && planned > 0
                    ? "partially_planned" : dryRun ? "preview_rejected" : "rejected";
            else if (child != null && !child.IsError && success == true && complete == true
                && childDryRun == dryRun && unknownCounts.Count == 0 && failed == 0)
            {
                status = dryRun && committed == false && planned == 0 ? "preview_valid"
                    : !dryRun && committed == true && (planned > 0 || reused > 0) ? "planned_or_reused" : "unknown";
            }
            else
                status = "unknown";
            result["status"] = status;
            result["planned"] = Value(planned);
            result["reused"] = Value(reused);
            result["failed"] = Value(failed);
            result["autoDigQueued"] = Value(autoDigQueued);
            result["autoDigAlreadyQueued"] = Value(counts["digAlreadyQueued"]);
            result["childSuccess"] = Value(success);
            result["childComplete"] = Value(complete);
            result["childCommitted"] = Value(committed);
            result["childDryRun"] = Value(childDryRun);
            result["unknownCountFields"] = unknownCounts;
            result["unknownFlagFields"] = unknownFlags;
            if (payload == null) result["error"] = primary == null ? (object)JValue.CreateNull() : primary;
            return result;
        }

        private static int? Count(JToken token)
        {
            if (token?.Type != JTokenType.Integer) return null;
            int value;
            return int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value >= 0 ? value : (int?)null;
        }

        private static object Value<T>(T? value) where T : struct => value.HasValue ? (object)value.Value : JValue.CreateNull();
    }
}
