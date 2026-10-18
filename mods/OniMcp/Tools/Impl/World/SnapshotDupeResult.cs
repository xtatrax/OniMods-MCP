using System;
using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    public static partial class SnapshotTools
    {
        private static Dictionary<string, object> BuildDupeResult(List<DupeSnapshot> dupes, int limit, string profile)
        {
            var result = new Dictionary<string, object>
            {
                ["count"] = dupes.Count,
                ["maxStress"] = dupes.Count == 0 ? null : (object)Math.Round(dupes.Max(item => item.Stress), 1),
                ["lowStamina"] = dupes.Count(item => item.Stamina > 0f && item.Stamina < 30f),
                ["skillPointsTotal"] = dupes.Sum(item => item.SkillPoints)
            };
            if (limit > 0)
            {
                result["items"] = dupes
                    .OrderByDescending(item => item.Stress)
                    .ThenBy(item => item.Name)
                    .Take(limit)
                    .Select(item => item.ToDictionary(profile == "full"))
                    .ToList();
            }
            return result;
        }
    }
}
