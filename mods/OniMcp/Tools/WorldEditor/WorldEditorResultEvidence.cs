using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static readonly string[] OutcomeCounterKeys =
        {
            "planned", "succeeded", "marked", "executedCells", "applied", "failed", "hardFailed",
            "deferred", "alreadyPresent", "alreadyConnected", "autoDigQueued", "autoDigAlreadyMarked",
            "attempted", "anchorCount", "pendingBuildAfterDig"
        };

        private static JObject NativeOutcome(CallToolResult result)
        {
            JObject value = ParseWorldEditorResult(result);
            var counters = new JObject();
            bool invalid = false;
            if (value != null)
                foreach (string key in OutcomeCounterKeys)
                {
                    JToken token = value[key];
                    if (token == null) continue;
                    if (TryOutcomeInt(token, out int count) && count >= 0) counters[key] = count;
                    else invalid = true;
                }
            JToken changed = value?["changed"];
            if (changed != null && changed.Type != JTokenType.Boolean && changed.Type != JTokenType.Null)
                invalid = true;
            return new JObject
            {
                ["observed"] = value != null,
                ["changed"] = OutcomeBool(changed),
                ["counters"] = counters,
                ["countUnit"] = null,
                ["reasonCode"] = value == null ? "native_result_unavailable" : invalid ? "invalid_native_fields" : null
            };
        }

        private static bool TryOutcomeInt(JToken token, out int value)
        {
            value = 0;
            return token?.Type == JTokenType.Integer && int.TryParse(token.ToString(), out value);
        }

        private static bool? OutcomeBool(JToken token)
        {
            return token?.Type == JTokenType.Boolean ? token.Value<bool>() : (bool?)null;
        }

        private static bool? MergeOutcomeChanged(IEnumerable<bool?> values)
        {
            bool any = false, unknown = false;
            foreach (bool? value in values)
            {
                any = true;
                if (value == true) return true;
                if (!value.HasValue) unknown = true;
            }
            return any && !unknown ? false : (bool?)null;
        }

        private static bool? ResultChanged(CallToolResult result)
        {
            return OutcomeBool(ParseWorldEditorResult(result)?["changed"]);
        }

        private static bool ResultHasProgressEvidence(CallToolResult result)
        {
            JObject value = ParseWorldEditorResult(result);
            string unit = value?["appliedUnit"]?.Type == JTokenType.String ? (string)value["appliedUnit"] : null;
            return value != null && (unit == "edit_blocks" || unit == "legacy_child_counter"
                || unit == "requested_map_cells" || value["cellProgress"] is JObject);
        }

        private static bool ResultProgressPartial(CallToolResult result)
        {
            JObject value = ParseWorldEditorResult(result);
            bool? partial = OutcomeBool(value?["partial"]);
            return ResultHasProgressEvidence(result) && partial.HasValue ? partial.Value : ResultReportsPartial(result);
        }

        private static bool NativeRefusalReportsPartial(CallToolResult result, bool knownUnchangedRefusal)
        {
            if (!knownUnchangedRefusal) return ResultReportsPartial(result);
            JObject value = ParseWorldEditorResult(result);
            string status = value?["status"]?.Type == JTokenType.String ? (string)value["status"] : string.Empty;
            return OutcomeBool(value?["partial"]) == true || OutcomeBool(value?["throttled"]) == true
                || TryOutcomeInt(value?["deferred"], out int deferred) && deferred > 0
                || status.IndexOf("partial", StringComparison.OrdinalIgnoreCase) >= 0
                || status.IndexOf("throttled", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class MapCellProgress
        {
            internal int Requested;
            internal int KnownSatisfied;
            internal int Unknown;
            internal bool? Changed;
            internal JObject ToJson() => new JObject
            {
                ["unit"] = "requested_map_cells", ["requested"] = Requested,
                ["satisfied"] = Unknown == 0 ? new JValue(KnownSatisfied) : JValue.CreateNull(),
                ["knownSatisfied"] = KnownSatisfied, ["unknown"] = Unknown
            };
        }

        private static MapCellProgress ReadCellProgress(CallToolResult result, int requested)
        {
            JObject value = ParseWorldEditorResult(result);
            var progress = value?["cellProgress"] as JObject;
            if (progress != null && (string)progress["unit"] == "requested_map_cells"
                && TryOutcomeInt(progress["requested"], out int childRequested) && childRequested == requested
                && TryOutcomeInt(progress["knownSatisfied"], out int known) && known >= 0 && known <= requested
                && TryOutcomeInt(progress["unknown"], out int unknown) && unknown >= 0 && unknown <= requested - known)
                return new MapCellProgress { Requested = requested, KnownSatisfied = known, Unknown = unknown, Changed = OutcomeBool(value["changed"]) };
            return new MapCellProgress { Requested = requested, Unknown = requested, Changed = ResultChanged(result) };
        }

        private static MapCellProgress SumCellProgress(int requested, IEnumerable<MapCellProgress> progress)
        {
            var items = progress.ToList();
            return new MapCellProgress
            {
                Requested = requested, KnownSatisfied = items.Sum(item => item.KnownSatisfied),
                Unknown = items.Sum(item => item.Unknown),
                Changed = items.Count == 0 ? false : MergeOutcomeChanged(items.Select(item => item.Changed))
            };
        }
    }
}
