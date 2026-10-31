using System.Collections.Generic;

namespace OniMcp.Tools
{
    // Describes the existing MCP checks. This projection never reads or evaluates
    // native notifications, report data, or diagnostic criteria.
    internal static class DiagnosticAlertCoverage
    {
        internal static Dictionary<string, object> Create(string model, bool evaluated, bool atmosphere)
        {
            return new Dictionary<string, object>
            {
                ["source"] = "mcp_rule_checks",
                ["model"] = model,
                ["evaluated"] = evaluated,
                ["overallComplete"] = false,
                ["hudWarningCount"] = null,
                ["dailyOxygenBalanceKg"] = null,
                ["oxygenCellCheck"] = atmosphere ? "visible_stock_only" : "not_evaluated",
                ["populationScope"] = model == "diagnostics" ? "all_live_worlds" : "requested_scope",
                ["buildingScope"] = model == "diagnostics" ? "all_completed_worlds" : "requested_scope"
            };
        }

        internal static Dictionary<string, object> Sample(object coverage, Dictionary<string, object> time, int worldId)
        {
            var original = coverage as Dictionary<string, object>;
            var result = original != null ? new Dictionary<string, object>(original) : Create("snapshot", false, false);
            result["worldId"] = worldId;
            if ((string)result["model"] == "snapshot")
            {
                result["populationScope"] = worldId < 0 ? "all_worlds" : "requested_world";
                result["buildingScope"] = worldId < 0 ? "all_worlds" : "requested_world";
            }
            object cycle;
            object fraction;
            result["sampledCycle"] = time.TryGetValue("cycle", out cycle) ? cycle : null;
            result["sampledTimeOfDayPercent"] = time.TryGetValue("timeOfDayPercent", out fraction) ? fraction : null;
            return result;
        }
    }
}
