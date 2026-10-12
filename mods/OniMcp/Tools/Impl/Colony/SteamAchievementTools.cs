using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using Steamworks;

namespace OniMcp.Tools
{
    internal static class SteamAchievementTools
    {
        internal static CallToolResult Read(JObject args)
        {
            string detail = (args["achievementDetail"]?.ToString() ?? "brief").Trim().ToLowerInvariant();
            if (detail != "brief" && detail != "full") return CallToolResult.Error("achievementDetail must be brief or full");
            string filter = (args["achievementStatus"]?.ToString() ?? "all").Trim().ToLowerInvariant();
            if (!new[] { "all", "completed", "incomplete", "unknown" }.Contains(filter))
                return CallToolResult.Error("Steam achievementStatus must be all, completed, incomplete or unknown; Steam has no failed state");
            string query = args["query"]?.ToString();
            int limit = Math.Max(1, Math.Min(200, ToolUtil.GetInt(args, "limit") ?? (detail == "full" ? 10 : 100)));
            var items = new List<Dictionary<string, object>>();
            var counts = new Dictionary<string, int> { ["completed"] = 0, ["incomplete"] = 0, ["unknown"] = 0 };
            string reason = null;
            uint catalogCount = 0;
            try {
                // Do not touch SteamManager.Instance/Initialized: its singleton getter can create it.
                if (!DistributionPlatform.Initialized || !string.Equals(DistributionPlatform.Inst.Name, "Steam", StringComparison.OrdinalIgnoreCase))
                    reason = "steam_platform_not_initialized";
                else {
                    catalogCount = SteamUserStats.GetNumAchievements();
                    if (catalogCount == 0) reason = "steam_catalog_unavailable_or_empty";
                    if (catalogCount > 10000) throw new InvalidOperationException("Steam achievement catalog exceeds read safety bound");
                    for (uint index = 0; index < catalogCount; index++) {
                        string id = SteamUserStats.GetAchievementName(index);
                        if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Steam achievement API name missing");
                        bool unlocked;
                        uint unlockTime;
                        bool acquired = SteamUserStats.GetAchievementAndUnlockTime(id, out unlocked, out unlockTime);
                        string state = !acquired ? "unknown" : unlocked ? "completed" : "incomplete";
                        counts[state]++;
                        string name = SteamUserStats.GetAchievementDisplayAttribute(id, "name");
                        if (filter != "all" && state != filter) continue;
                        if (!string.IsNullOrWhiteSpace(query) && id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                            && (name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var row = new Dictionary<string, object> {
                            ["id"] = id, ["name"] = string.IsNullOrEmpty(name) ? null : name,
                            ["status"] = state, ["unlocked"] = acquired ? (object)unlocked : null,
                            ["unlockTimeUnix"] = acquired && unlocked && unlockTime != 0 ? (object)unlockTime : null,
                            ["unlockTimeState"] = !acquired ? "unknown" : !unlocked ? "not_unlocked" : unlockTime == 0 ? "not_recorded" : "observed",
                            ["readReason"] = acquired ? null : "steam_read_failed_or_stats_not_ready"
                        };
                        if (detail == "full") {
                            string description = SteamUserStats.GetAchievementDisplayAttribute(id, "desc");
                            row["description"] = string.IsNullOrEmpty(description) ? null : description;
                            string hidden = SteamUserStats.GetAchievementDisplayAttribute(id, "hidden");
                            row["hidden"] = hidden == "1" ? (object)true : hidden == "0" ? (object)false : null;
                            row["colonyAchievementIds"] = Db.Get()?.ColonyAchievements?.resources
                                .Where(d => d.platformAchievementId == id).Select(d => d.Id).ToList();
                        }
                        items.Add(row);
                    }
                }
            } catch (Exception ex) {
                // A partially read catalog must not be reported as a complete account snapshot.
                items.Clear(); counts["completed"] = counts["incomplete"] = counts["unknown"] = 0;
                reason = "steam_read_exception:" + ex.GetType().Name;
            }
            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object> {
                ["scope"] = "steam_current_user_current_app", ["source"] = "SteamUserStats cached client API",
                ["available"] = reason == null, ["reason"] = reason,
                ["achievementDetail"] = detail, ["achievementStatus"] = filter,
                ["catalogCount"] = reason == null ? (object)catalogCount : null,
                ["counts"] = reason == null ? (object)counts : null,
                ["matched"] = reason == null ? (object)items.Count : null,
                ["returned"] = Math.Min(limit, items.Count), ["truncated"] = items.Count > limit,
                ["items"] = items.OrderBy(i => (string)i["id"]).Take(limit).ToList(),
                ["coverage"] = "Current Steam account/app, independent of current colony. No refresh, unlock, reset or store calls; backend sync time unknown."
            }, McpJsonUtil.Settings));
        }
    }
}
