using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class ColonyTools
    {
        public static McpTool GetDuplicants()
        {
            return new McpTool
            {
                Name = "dupes_list",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "get_duplicants" },
                Hidden = true,
                Description = McpLocalization.Format("warnings.legacy_alias", null, "dupes_list", "0.3.0", "colony_control domain=read action=dupes"),
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error(McpLocalization.Get("errors.game_not_initialized"));

                    var minions = new List<Dictionary<string, object>>();

                    foreach (var minion in Components.LiveMinionIdentities.Items)
                    {
                        if (minion == null) continue;

                        var name = minion.GetProperName();
                        var resume = minion.GetComponent<MinionResume>();
                        var skills = resume != null
                            ? resume.MasteryBySkillID.Where(kv => kv.Value).Select(kv => kv.Key).ToList()
                            : new List<string>();

                        var vitals = BasicDuplicantVitals.Read(minion);

                        minions.Add(new Dictionary<string, object>
                        {
                            ["id"] = minion.GetComponent<KPrefabID>()?.InstanceID ?? -1,
                            ["name"] = name,
                            ["skillsMastered"] = skills,
                            ["availableSkillPoints"] = resume?.AvailableSkillpoints ?? 0,
                            ["amounts"] = vitals["amounts"],
                            ["amountsById"] = vitals["amountsById"],
                            ["amountsEvidence"] = vitals["amountsEvidence"],
                            ["health"] = vitals["health"],
                            ["worldId"] = minion.GetMyWorldId()
                        });
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(minions, McpJsonUtil.Settings));
                }
            };
        }

    }
}
