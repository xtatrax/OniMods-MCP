using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class SnapshotTools
    {
        private static Dictionary<string, object> BuildAlerts(List<DupeSnapshot> dupes, FoodSnapshot food, BuildingSnapshot buildings, Dictionary<string, object> atmosphere, Dictionary<string, object> redAlert)
        {
            int dupeCount = dupes.Count;
            double foodKcal = food?.Access?.Available ?? 0d;
            var alerts = new List<Dictionary<string, object>>();

            AddAlert(alerts, redAlert != null && redAlert.ContainsKey("isRedAlert") && Convert.ToBoolean(redAlert["isRedAlert"]), "critical", "red_alert", "Red alert is active.");
            AddAlert(alerts, redAlert != null && redAlert.ContainsKey("isYellowAlert") && Convert.ToBoolean(redAlert["isYellowAlert"]), "warning", "yellow_alert", "Yellow alert is active.");
            AddAlert(alerts, food != null && foodKcal < dupeCount * 2000f, "critical", "food", $"Confirmed unreserved available food below threshold: {Math.Round(foodKcal, 1)} kcal for {dupeCount} dupes.");
            AddAlert(alerts, food?.Access != null && (food.Access.UnknownAvailabilityItems > 0 || food.Access.UnknownCalorieItems > 0),
                "warning", "food_assessment_incomplete", "Some food availability is unknown; known food total is not a survival reserve.");
            AddAlert(alerts, buildings != null && buildings.Beds < dupeCount, "warning", "sleep", $"Beds short: {buildings.Beds}/{dupeCount}.");
            AddAlert(alerts, buildings != null && buildings.Toilets == 0, "warning", "hygiene", "No toilet detected.");
            AddAlert(alerts, buildings != null && buildings.ResearchStations == 0, "info", "research", "No research station detected.");
            AddAlert(alerts, buildings != null && buildings.Batteries == 0, "info", "power", "No battery detected.");
            // Known high stress remains an alert even when whole-population coverage is incomplete.
            var observedStress = dupes.Where(item => KnownSnapshotNeed(item.Stress)).ToList();
            if (observedStress.Count > 0)
            {
                float maxStress = observedStress.Max(item => item.Stress.Value);
                string message = observedStress.Count == dupes.Count
                    ? McpLocalization.Format("snapshot.alert.stress", null, Math.Round(maxStress, 1))
                    : McpLocalization.Format("snapshot_needs.alert.observed_stress", null, Math.Round(maxStress, 1));
                AddAlert(alerts, maxStress > 40f, "warning", "stress", message);
            }

            if (atmosphere != null)
            {
                int breathableCells = Convert.ToInt32(atmosphere["breathableCells"]);
                AddAlert(alerts, breathableCells < dupeCount * 20, "warning", "oxygen", $"Visible breathable cells low: {breathableCells}.");
            }

            return new Dictionary<string, object>
            {
                ["count"] = alerts.Count,
                ["items"] = alerts
            };
        }

        private static void AddAlert(List<Dictionary<string, object>> alerts, bool condition, string severity, string category, string message)
        {
            if (!condition)
                return;
            alerts.Add(new Dictionary<string, object>
            {
                ["severity"] = severity,
                ["category"] = category,
                ["message"] = message
            });
        }
    }
}
