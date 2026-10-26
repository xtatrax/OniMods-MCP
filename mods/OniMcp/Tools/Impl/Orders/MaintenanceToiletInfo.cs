using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    public static partial class MaintenanceActionTools
    {
        private sealed class ToiletReadSnapshot
        {
            internal readonly Dictionary<string, object> Values = new Dictionary<string, object>();
            internal readonly List<Dictionary<string, object>> Unavailable = new List<Dictionary<string, object>>();
            internal bool? CanCleanEarly;
        }

        // Fixed compatibility query alias only; never display or return as a fallback title.
        private const string LegacyToiletCleanTitleQueryAlias = "Request early toilet cleaning";

        private static bool MatchesMaintenanceQueryWithToiletAlias(Dictionary<string, object> info, string query)
        {
            if (string.IsNullOrWhiteSpace(query)
                || JsonConvert.SerializeObject(info).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!HasLegacyToiletTitleQueryRow(info))
                return false;
            var projection = JObject.FromObject(info);
            var actions = projection["actions"] as JArray;
            if (actions == null)
                return false;
            bool aliased = false;
            foreach (var token in actions)
            {
                var row = token as JObject;
                if (row == null || row["actionKey"]?.Type != JTokenType.String
                    || !string.Equals((string)row["actionKey"], "clean_toilet", StringComparison.Ordinal)
                    || row["title"]?.Type != JTokenType.String)
                    continue;
                row["title"] = LegacyToiletCleanTitleQueryAlias;
                aliased = true;
            }
            return aliased
                && JsonConvert.SerializeObject(projection).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasLegacyToiletTitleQueryRow(Dictionary<string, object> info)
        {
            object value;
            if (info == null || !info.TryGetValue("actions", out value))
                return false;
            var rows = value as IEnumerable<Dictionary<string, object>>;
            if (rows == null)
                return false;
            foreach (var row in rows)
            {
                object key, title;
                if (row != null && row.TryGetValue("actionKey", out key) && key is string
                    && string.Equals((string)key, "clean_toilet", StringComparison.Ordinal)
                    && row.TryGetValue("title", out title) && title is string)
                    return true;
            }
            return false;
        }

        private static readonly string[] ToiletSmiFields =
        {
            "flushesRemaining", "isSoiled", "isCloggedWithGunk", "hasCleanChore", "canCleanEarly"
        };

        private static Dictionary<string, object> ToiletActionInfo(Toilet toilet)
        {
            var snapshot = ReadToiletSnapshot(toilet);
            return ActionInfo("clean_toilet", global::STRINGS.UI.USERMENUACTIONS.CLEANTOILET.NAME,
                "maintenance", "Toilet", CanCleanToilet(snapshot), ToiletInfo(toilet, snapshot));
        }

        private static Dictionary<string, object> ToiletInfo(Toilet toilet)
        {
            return ToiletInfo(toilet, ReadToiletSnapshot(toilet));
        }

        private static Dictionary<string, object> ToiletInfo(Toilet toilet, ToiletReadSnapshot snapshot)
        {
            var info = toilet != null && toilet.gameObject != null
                ? TargetInfo(toilet.gameObject) : new Dictionary<string, object>();
            foreach (var pair in snapshot.Values)
                info[pair.Key] = pair.Value;
            return info;
        }

        private static bool CanCleanToilet(ToiletReadSnapshot snapshot)
        {
            return snapshot.CanCleanEarly == true;
        }

        private static ToiletReadSnapshot ReadToiletSnapshot(Toilet toilet)
        {
            var snapshot = new ToiletReadSnapshot();
            var data = snapshot.Values;
            data["flushesUsed"] = JValue.CreateNull();
            data["maxFlushes"] = JValue.CreateNull();
            foreach (string field in ToiletSmiFields)
                data[field] = JValue.CreateNull();
            data["currentState"] = JValue.CreateNull();
            data["currentStateSource"] = "Toilet.GetSMI().GetCurrentState().name";
            data["stateMachineAvailability"] = "unavailable";
            data["stateMachineReason"] = JValue.CreateNull();
            data["unavailableFields"] = snapshot.Unavailable;

            if (toilet == null)
            {
                ToiletReadMissing(snapshot, "flushesUsed", "toilet_missing");
                ToiletReadMissing(snapshot, "maxFlushes", "toilet_missing");
                ToiletSmiUnavailable(snapshot, "toilet_missing");
                return snapshot;
            }

            ReadToiletValue(snapshot, "flushesUsed", () => toilet.FlushesUsed, "master_read_failed");
            ReadToiletValue(snapshot, "maxFlushes", () => toilet.maxFlushes, "master_read_failed");
            if (toilet.GetType() != typeof(Toilet))
            {
                ToiletSmiUnavailable(snapshot, "toilet_type_unsupported");
                return snapshot;
            }

            StateMachine.Instance raw;
            try { raw = toilet.GetSMI(); }
            catch
            {
                ToiletSmiUnavailable(snapshot, "cached_smi_read_failed");
                return snapshot;
            }
            if (raw == null)
            {
                ToiletSmiUnavailable(snapshot, "cached_smi_missing");
                return snapshot;
            }
            if (raw.GetType() != typeof(Toilet.StatesInstance))
            {
                ToiletSmiUnavailable(snapshot, "cached_smi_type_mismatch");
                return snapshot;
            }
            var smi = (Toilet.StatesInstance)raw;
            try
            {
                if (!ReferenceEquals(smi.master, toilet))
                {
                    ToiletSmiUnavailable(snapshot, "cached_smi_owner_mismatch");
                    return snapshot;
                }
            }
            catch
            {
                ToiletSmiUnavailable(snapshot, "cached_smi_owner_read_failed");
                return snapshot;
            }

            StateMachine.BaseState currentState;
            try { currentState = smi.GetCurrentState(); }
            catch
            {
                ToiletSmiUnavailable(snapshot, "cached_smi_state_read_failed");
                return snapshot;
            }
            if (currentState == null)
            {
                ToiletSmiUnavailable(snapshot, "cached_smi_unstarted");
                return snapshot;
            }
            data["stateMachineAvailability"] = "observed";
            try
            {
                if (currentState.name == null)
                    ToiletReadMissing(snapshot, "currentState", "native_state_name_missing");
                else data["currentState"] = currentState.name;
            }
            catch { ToiletReadMissing(snapshot, "currentState", "native_read_failed"); }

            ReadToiletValue(snapshot, "flushesRemaining", () => smi.GetFlushesRemaining(), "native_read_failed");
            bool? isSoiled = ReadToiletFlag(snapshot, "isSoiled", () => smi.IsSoiled);
            ReadToiletFlag(snapshot, "isCloggedWithGunk", () => smi.IsCloggedWithGunk);
            bool? hasCleanChore = ReadToiletFlag(snapshot, "hasCleanChore", () => smi.cleanChore != null);
            bool? isFull = null;
            try
            {
                var states = smi.sm;
                if (states == null || states.full == null)
                    ToiletReadMissing(snapshot, "canCleanEarly", "native_state_definition_missing");
                else isFull = ReferenceEquals(currentState, states.full);
            }
            catch { ToiletReadMissing(snapshot, "canCleanEarly", "native_read_failed"); }
            if (isFull.HasValue)
            {
                if (isSoiled.HasValue && hasCleanChore.HasValue)
                {
                    snapshot.CanCleanEarly = !isFull.Value && isSoiled.Value && !hasCleanChore.Value;
                    data["canCleanEarly"] = snapshot.CanCleanEarly.Value;
                }
                else ToiletReadMissing(snapshot, "canCleanEarly", "native_condition_unavailable");
            }
            return snapshot;
        }

        private static void ReadToiletValue(ToiletReadSnapshot snapshot, string field, Func<object> read, string reason)
        {
            try { snapshot.Values[field] = read(); }
            catch { ToiletReadMissing(snapshot, field, reason); }
        }

        private static bool? ReadToiletFlag(ToiletReadSnapshot snapshot, string field, Func<bool> read)
        {
            try
            {
                bool value = read();
                snapshot.Values[field] = value;
                return value;
            }
            catch
            {
                ToiletReadMissing(snapshot, field, "native_read_failed");
                return null;
            }
        }

        private static void ToiletSmiUnavailable(ToiletReadSnapshot snapshot, string reason)
        {
            snapshot.Values["stateMachineReason"] = reason;
            ToiletReadMissing(snapshot, "currentState", reason);
            foreach (string field in ToiletSmiFields)
                ToiletReadMissing(snapshot, field, reason);
        }

        private static void ToiletReadMissing(ToiletReadSnapshot snapshot, string field, string reason)
        {
            snapshot.Values[field] = JValue.CreateNull();
            snapshot.Unavailable.Add(new Dictionary<string, object>
            {
                ["field"] = field, ["reasonCode"] = reason
            });
        }
    }
}
