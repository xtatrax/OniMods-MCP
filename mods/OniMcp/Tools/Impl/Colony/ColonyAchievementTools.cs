using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    // Read cached colony state only. IsAchievementUnlocked updates status and must not be called here.
    internal static class ColonyAchievementTools
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

        private static Db ReadExistingDatabase()
        {
            if (ExistingDb == null) return null;
            try { return ExistingDb.GetValue(null) as Db; }
            catch { return null; }
        }

        internal static CallToolResult Read(JObject args)
        {
            var tracker = SaveGame.Instance?.ColonyAchievementTracker;
            var definitions = ReadExistingDatabase()?.ColonyAchievements?.resources;
            if (Game.Instance == null || tracker == null || definitions == null)
                return CallToolResult.Error("Colony achievement data is not initialized");
            string detail = (args["achievementDetail"]?.ToString() ?? "brief").Trim().ToLowerInvariant();
            if (detail != "brief" && detail != "full")
                return CallToolResult.Error("achievementDetail must be brief or full");
            string filter = (args["achievementStatus"]?.ToString() ?? "all").Trim().ToLowerInvariant();
            if (!new[] { "all", "completed", "incomplete", "failed", "unknown" }.Contains(filter))
                return CallToolResult.Error("achievementStatus must be all, completed, incomplete, failed or unknown");
            string query = args["query"]?.ToString();
            int limit = Math.Max(1, Math.Min(200, ToolUtil.GetInt(args, "limit") ?? (detail == "full" ? 10 : 100)));
            var items = new List<Dictionary<string, object>>();
            var counts = new Dictionary<string, int> { ["completed"] = 0, ["incomplete"] = 0, ["failed"] = 0, ["unknown"] = 0 };
            foreach (var definition in definitions.OrderBy(d => d.Id))
            {
                if (definition.Disabled || !definition.IsValidForSave()) continue;
                var cached = ReadCachedStatus(tracker, definition.Id);
                string state = CachedState(cached);
                counts[state]++;
                string name = EntitySectionText.Plain(definition.Name);
                if (filter != "all" && state != filter) continue;
                if (!string.IsNullOrWhiteSpace(query)
                    && definition.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var row = new Dictionary<string, object> {
                    ["id"] = definition.Id, ["name"] = name, ["status"] = state,
                    ["completed"] = cached == null ? (object)null : cached.success,
                    ["failed"] = cached == null ? (object)null : cached.failed,
                    ["isVictoryCondition"] = definition.isVictoryCondition
                };
                if (detail == "full") {
                    row["description"] = EntitySectionText.Plain(definition.description);
                    row["requirements"] = ReadRequirements(definition, includeSuccess: false);
                }
                items.Add(row);
            }
            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object> {
                ["scope"] = "current_colony_valid_achievements", ["source"] = "SaveGame.ColonyAchievementTracker.achievements",
                ["stateMode"] = "cached_no_reevaluation", ["achievementDetail"] = detail,
                ["achievementStatus"] = filter, ["counts"] = counts,
                ["observedCycle"] = GameClock.Instance == null ? (object)null : GameClock.Instance.GetCycle(),
                ["matched"] = items.Count, ["returned"] = Math.Min(limit, items.Count),
                ["truncated"] = items.Count > limit, ["items"] = items.Take(limit).ToList(),
                ["coverage"] = "Colony state, not Steam account unlocks. Disabled/save-incompatible definitions excluded. Missing cached status is unknown; requirement progress is not reevaluated."
            }, McpJsonUtil.Settings));
        }

        // Read existing cached flags and definition metadata only. Requirement evaluation can mutate reports.
        internal static List<Dictionary<string, object>> ReadVictoryConditions()
        {
            var results = new List<Dictionary<string, object>>();
            var definitions = ReadExistingDatabase()?.ColonyAchievements?.resources;
            if (definitions == null) return null;
            var tracker = SaveGame.Instance?.ColonyAchievementTracker;
            foreach (var definition in definitions)
            {
                if (!definition.isVictoryCondition || definition.Disabled || !definition.IsValidForSave()) continue;
                var cached = ReadCachedStatus(tracker, definition.Id);
                results.Add(new Dictionary<string, object> {
                    ["id"] = definition.Id, ["name"] = definition.Name,
                    ["success"] = cached == null ? (object)null : cached.success,
                    ["failed"] = cached == null ? (object)null : cached.failed,
                    ["status"] = CachedState(cached),
                    ["source"] = "SaveGame.ColonyAchievementTracker.achievements",
                    ["stateMode"] = "cached_no_reevaluation",
                    ["requirements"] = ReadRequirements(definition, includeSuccess: true)
                });
            }
            return results;
        }

        private static ColonyAchievementStatus ReadCachedStatus(ColonyAchievementTracker tracker, string id)
        {
            ColonyAchievementStatus cached = null;
            if (tracker?.achievements != null && id != null) tracker.achievements.TryGetValue(id, out cached);
            return cached;
        }

        private static string CachedState(ColonyAchievementStatus cached)
        {
            return cached == null ? "unknown" : cached.success ? "completed" : cached.failed ? "failed" : "incomplete";
        }

        private static List<Dictionary<string, object>> ReadRequirements(Database.ColonyAchievement definition, bool includeSuccess)
        {
            return definition.requirementChecklist?.Select(req => {
                var row = new Dictionary<string, object> {
                    ["type"] = req?.GetType().Name,
                    ["progress"] = null,
                    ["progressState"] = req == null ? "unavailable" : "not_evaluated_readonly"
                };
                if (includeSuccess) {
                    row["success"] = null;
                    row["successState"] = req == null ? "unavailable" : "not_evaluated_readonly";
                }
                return row;
            }).ToList();
        }
    }
}
