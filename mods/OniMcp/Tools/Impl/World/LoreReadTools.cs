using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    // Reads the same lock-to-entry cache used by ONI's "Go to Database" button.
    // Never initializes the cache, unlocks entries, or invokes lore callbacks.
    internal static class LoreReadTools
    {
        internal static HashSet<string> SnapshotUnlocks()
        {
            return Game.Instance?.unlocks == null ? null
                : new HashSet<string>(Game.Instance.unlocks.GetAllUnlockedIds(), StringComparer.Ordinal);
        }

        internal static void AddLegacyContent(Dictionary<string, object> result, string searched, string legacy)
        {
            // BeenSearched is a generic UI message, even on uninspected objects.
            string text = ValidText(legacy) ? legacy : null;
            result["alreadyInspectedMessage"] = ValidText(searched) ? searched : null;
            result["content"] = text;
            result["contentSource"] = text == null ? null : "legacy_string";
            result["contentStatus"] = text == null ? "legacy_text_unavailable_use_database" : "available";
        }

        private static bool ValidText(string text) => !string.IsNullOrWhiteSpace(text)
            && !text.StartsWith("MISSING.", StringComparison.Ordinal);

        internal static Dictionary<string, object> ReadNewUnlocks(HashSet<string> before)
        {
            var after = SnapshotUnlocks();
            var rows = new List<Dictionary<string, object>>();
            bool available = before != null && after != null;
            var ids = available ? after.Except(before).OrderBy(id => id, StringComparer.Ordinal).ToList() : new List<string>();
            foreach (string id in ids)
            {
                var row = new Dictionary<string, object> { ["unlockId"] = id };
                rows.Add(row);
                if (CodexCache.entries == null || CodexCache.entries.Count == 0)
                {
                    row["status"] = "codex_cache_not_ready";
                    continue;
                }
                string entryId = CodexCache.GetEntryForLock(id);
                row["databaseEntryId"] = entryId;
                if (string.IsNullOrEmpty(entryId))
                {
                    row["status"] = "no_codex_mapping";
                    continue;
                }
                // Reuse the existing reader's lock/DLC checks and bounded rich text.
                var read = CodexReadTools.Read(new JObject
                {
                    ["id"] = entryId, ["includeContent"] = true, ["maxChars"] = 6000
                });
                row["status"] = read.IsError ? "read_error" : "read";
                if (read.IsError) row["database"] = read.Content[0].Text;
                else
                {
                    var database = JObject.Parse(read.Content[0].Text);
                    // Omit unrelated catalog metadata from the investigation response.
                    database.Remove("categories");
                    row["database"] = database;
                }
            }
            return new Dictionary<string, object>
            {
                ["available"] = available,
                ["reason"] = available ? null : "unlock_state_unavailable",
                ["unlockIds"] = ids, ["records"] = rows,
                ["scope"] = "unlock_delta_during_this_press",
                ["note"] = "An empty delta does not imply the investigation failed; its records may already be unlocked. Use database paging for truncated entries."
            };
        }
    }
}
