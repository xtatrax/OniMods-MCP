using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static MapCellProgress CorrelateOriginalBuildProgress(CallToolResult result, JObject args,
            string prefabId, List<MapEditAnchorRequest> executed, List<MapEditAnchorRequest> original, int requested)
        {
            MapCellProgress execution = NativeBuildCellProgress(result, args, prefabId, executed);
            var unknown = new MapCellProgress { Requested = requested, Unknown = requested, Changed = execution.Changed };
            if (original == null) return unknown;
            var mapped = new List<MapEditAnchorRequest>();
            foreach (MapEditAnchorRequest request in executed)
            {
                var matches = original.Where(item => item.X == request.X && item.Y == request.Y).ToList();
                if (matches.Count != 1 || !request.RequestedCells.All(cell =>
                    matches[0].RequestedCells.Any(candidate => candidate.X == cell.X && candidate.Y == cell.Y))) return unknown;
                mapped.Add(matches[0]);
            }
            MapCellProgress covered = NativeBuildCellProgress(result, args, prefabId, mapped);
            covered.Requested = requested;
            return covered;
        }

        private static MapCellProgress NativeBuildCellProgress(CallToolResult result, JObject args,
            string prefabId, List<MapEditAnchorRequest> requests)
        {
            int requested = requests.Sum(item => item.RequestedCells.Count);
            var unknown = new MapCellProgress { Requested = requested, Unknown = requested, Changed = ResultChanged(result) };
            JObject native = ParseWorldEditorResult(result);
            if (native == null || !BuildResultScopeMatches(native, args, prefabId)) return unknown;
            var expected = new HashSet<string>(requests.Select(item => MapAnchorKey(item.X, item.Y)));
            if (expected.Count != requests.Count) return unknown;
            var rows = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
            var blocked = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
            var remaining = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
            if (!IndexBuildRows(native["results"], rows, expected, args, prefabId)
                || !IndexBuildRows(native["errors"], blocked, expected, args, prefabId)
                || !IndexBuildRows(native["preflightErrors"], blocked, expected, args, prefabId)
                || !IndexBuildRows(native["remainingAnchors"], remaining, expected, args, prefabId)) return unknown;

            var progress = new MapCellProgress { Requested = requested };
            var changes = new List<bool?>();
            bool notCommitted = OutcomeBool(native["committed"]) == false;
            foreach (MapEditAnchorRequest request in requests)
            {
                string key = MapAnchorKey(request.X, request.Y);
                rows.TryGetValue(key, out List<JObject> executed);
                blocked.TryGetValue(key, out List<JObject> errors);
                remaining.TryGetValue(key, out List<JObject> pending);
                bool ambiguous = (executed?.Count ?? 0) > 1 || (errors?.Count ?? 0) > 1
                    || (pending?.Count ?? 0) > 1 || executed != null && errors != null;
                bool? satisfied = null, changed = null;
                if (!ambiguous && executed != null && BuildRowFieldsValid(executed[0]))
                {
                    JObject row = executed[0];
                    bool accepted = OutcomeBool(row["planned"]) == true || OutcomeBool(row["alreadyPresent"]) == true
                        || OutcomeBool(row["alreadyConnected"]) == true;
                    if (accepted && OutcomeBool(row["valid"]) != false && pending == null)
                        satisfied = true;
                    else if (!accepted && (OutcomeBool(row["valid"]) == false || pending != null))
                        satisfied = false;
                    changed = NativeBuildRowChanged(row, notCommitted);
                }
                else if (!ambiguous && executed == null && (pending != null || errors != null))
                {
                    if (errors == null || OutcomeBool(errors[0]["valid"]) == false)
                        satisfied = false;
                    changed = notCommitted ? false : (bool?)null;
                }
                if (satisfied == true) progress.KnownSatisfied += request.RequestedCells.Count;
                else if (!satisfied.HasValue) progress.Unknown += request.RequestedCells.Count;
                changes.Add(changed);
            }
            bool? explicitChanged = OutcomeBool(native["changed"]);
            progress.Changed = explicitChanged == true ? true : MergeOutcomeChanged(changes);
            if (!progress.Changed.HasValue && explicitChanged.HasValue) progress.Changed = explicitChanged;
            return progress;
        }

        private static bool IndexBuildRows(JToken token, Dictionary<string, List<JObject>> rows,
            HashSet<string> expected, JObject args, string prefabId)
        {
            if (token == null) return true;
            if (!(token is JArray array)) return false;
            foreach (JToken item in array)
            {
                if (!(item is JObject row) || !TryOutcomeInt(row["x"], out int x)
                    || !TryOutcomeInt(row["y"], out int y) || !BuildResultScopeMatches(row, args, prefabId)) return false;
                string key = MapAnchorKey(x, y);
                if (!expected.Contains(key)) return false;
                if (!rows.TryGetValue(key, out List<JObject> list)) rows[key] = list = new List<JObject>();
                list.Add(row);
            }
            return true;
        }

        private static bool BuildResultScopeMatches(JObject value, JObject args, string prefabId)
        {
            JToken prefab = value["prefabId"];
            if (prefab != null && (prefab.Type != JTokenType.String
                || !string.Equals((string)prefab, prefabId, StringComparison.OrdinalIgnoreCase))) return false;
            if (value["worldId"] != null)
            {
                if (!TryOutcomeInt(value["worldId"], out int world)) return false;
                if (args["worldId"] != null && (!TryOutcomeInt(args["worldId"], out int requestedWorld) || requestedWorld != world)) return false;
            }
            return true;
        }

        private static bool BuildRowFieldsValid(JObject row)
        {
            foreach (string key in new[] { "planned", "alreadyPresent", "alreadyConnected", "valid", "changed", "blueprintPlaced", "buildingCompleted" })
                if (row[key] != null && row[key].Type != JTokenType.Boolean && row[key].Type != JTokenType.Null) return false;
            return row["id"] == null || TryOutcomeInt(row["id"], out _);
        }

        private static bool? NativeBuildRowChanged(JObject row, bool notCommitted)
        {
            if (OutcomeBool(row["blueprintPlaced"]) == true || OutcomeBool(row["buildingCompleted"]) == true
                || OutcomeBool(row["planned"]) == true && TryOutcomeInt(row["id"], out int id) && id > 0
                    && OutcomeBool(row["alreadyPresent"]) != true && OutcomeBool(row["alreadyConnected"]) != true)
                return true;
            var dig = row["autoDig"] as JObject;
            if (TryOutcomeInt(dig?["marked"], out int marked) && marked > 0) return true;
            bool? changed = OutcomeBool(row["changed"]);
            if (changed.HasValue) return changed;
            if (notCommitted || (OutcomeBool(row["alreadyPresent"]) == true || OutcomeBool(row["alreadyConnected"]) == true)
                && dig == null && OutcomeBool(row["planned"]) != true) return false;
            return null;
        }

        private static string MapAnchorKey(int x, int y) => x + "," + y;
    }
}
