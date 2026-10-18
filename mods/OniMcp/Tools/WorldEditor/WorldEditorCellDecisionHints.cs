using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static void AppendCellDecisionHints(StringBuilder sb, int x, int y, int cell)
        {
            sb.AppendLine();
            sb.AppendLine("## Decision Hints");

            bool wrote = false;
            wrote |= AppendThermalDecisionHint(sb, x, y, cell);
            wrote |= AppendOrderDecisionHints(sb, x, y, cell);
            wrote |= AppendPortDecisionHint(sb, x, y, cell);
            wrote |= AppendNetworkDecisionHint(sb, x, y, cell);

            if (!wrote)
                sb.AppendLine("- no immediate order/network/thermal hint; use Next Reads for local context.");
        }

        private static bool AppendThermalDecisionHint(StringBuilder sb, int x, int y, int cell)
        {
            float tempC = Grid.Temperature[cell] - 273.15f;
            string state = ToolUtil.GetElementState(Grid.Element[cell]);
            string band = AmbientTemperaturePolicy.MapBand(state, tempC);
            if (!AmbientTemperaturePolicy.SuggestThermalRead(state, tempC))
                return false;

            string level = band == "hot" || band == "danger_hot" ? "hot" : "cold";
            sb.AppendLine("- thermal: " + level + " " + tempC.ToString("F1")
                + "C; read `" + LocalZoomCall(x, y, "temperature") + "` before assigning long work here.");
            return true;
        }

        private static bool AppendOrderDecisionHints(StringBuilder sb, int x, int y, int cell)
        {
            bool wrote = false;
            string at = "@(" + x + "," + y + ")";
            Element element = Grid.Element[cell];

            if (element != null && element.IsSolid)
            {
                sb.AppendLine("- order: solid tile; preview `挖 " + at + ":7 dryRun=true`.");
                wrote = true;
            }

            if (element != null && element.IsLiquid)
            {
                sb.AppendLine("- order: liquid here; preview `擦 " + at + ":6 dryRun=true`.");
                wrote = true;
            }

            var pickups = Components.Pickupables.Items
                .Where(item => IsCellSweepHintCandidate(item, cell))
                .ToList();
            if (pickups.Count > 0)
            {
                float mass = pickups.Sum(item => item.PrimaryElement != null ? item.PrimaryElement.Mass : 0f);
                sb.AppendLine("- order: " + pickups.Count + " pickup(s), " + mass.ToString("F2")
                    + " kg; preview `扫 " + at + ":6 dryRun=true`.");
                wrote = true;
            }

            return wrote;
        }

        private static bool IsCellSweepHintCandidate(Pickupable item, int cell)
        {
            if (item == null || item.gameObject == null || Grid.PosToCell(item.gameObject) != cell)
                return false;
            // Pickupable also includes duplicants, stored contents and critters.
            // Match the ordinary sweep eligibility before recommending an order.
            if (SweepTargetPolicy.IsLiveDuplicant(item.gameObject) || item.storage != null)
                return false;
            var prefab = item.KPrefabID ?? item.GetComponent<KPrefabID>();
            if (prefab != null && (prefab.HasTag(GameTags.Stored) || prefab.HasTag(GameTags.Equipped)))
                return false;
            var clearable = item.GetComponent<Clearable>();
            return SweepEligibilityPolicy.RejectionReason(
                clearable != null, clearable != null && clearable.isClearable) == null;
        }

        private static bool AppendNetworkDecisionHint(StringBuilder sb, int x, int y, int cell)
        {
            bool hasNetwork = HasLayer(cell, PowerLayers) || HasLayer(cell, LiquidLayers)
                || HasLayer(cell, GasLayers) || HasLayer(cell, LogicLayers) || HasLayer(cell, ConveyorLayers);
            if (!hasNetwork)
                return false;

            sb.AppendLine("- network: cell has infrastructure; inspect same-cell dirs/links before editing: `"
                + LocalZoomCall(x, y, "power,liquid,gas,logic,conveyor") + "`.");
            return true;
        }

        private static string DecisionCellRef(int cell)
        {
            return "@(" + Grid.CellColumn(cell) + "," + Grid.CellRow(cell) + ")";
        }
    }
}
