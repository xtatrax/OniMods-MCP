using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    // Read cached colony state only. IsAchievementUnlocked updates status and must not be called here.
    internal static class ColonyAchievementTools
    {
        internal static CallToolResult Read(JObject args)
        {
            var tracker = SaveGame.Instance?.ColonyAchievementTracker;
            var definitions = Db.Get()?.ColonyAchievements?.resources;
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
                ColonyAchievementStatus cached = null;
                if (tracker.achievements != null) tracker.achievements.TryGetValue(definition.Id, out cached);
                string state = cached == null ? "unknown" : cached.success ? "completed" : cached.failed ? "failed" : "incomplete";
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
                    row["requirements"] = definition.requirementChecklist?.Select(req => new Dictionary<string, object> {
                        ["type"] = req.GetType().Name, ["progress"] = null,
                        ["progressState"] = "not_evaluated_readonly"
                    }).ToList();
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
    }
}
