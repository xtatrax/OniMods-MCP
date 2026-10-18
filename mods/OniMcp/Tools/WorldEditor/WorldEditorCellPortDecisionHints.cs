using System.Collections.Generic;
using System.Linq;
using System.Text;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static bool AppendPortDecisionHint(StringBuilder sb, int x, int y, int cell)
        {
            GameObject go = CellBuildingObject(cell);
            Building building = go != null ? go.GetComponent<Building>() : null;
            if (go == null || building == null || building.Def == null)
                return false;

            var missing = new List<string>();
            var connected = new List<string>();
            var optional = new List<string>();
            var unknown = new List<string>();
            AppendBuildingPortHints(go, building, missing, connected, optional, unknown);
            if (missing.Count == 0 && connected.Count == 0 && optional.Count == 0 && unknown.Count == 0)
                return false;

            if (missing.Count > 0)
                sb.AppendLine(McpLocalization.Format("cell.decision.ports.missing", null,
                    string.Join(", ", missing.Take(6).ToArray()), LocalZoomCall(x, y, "power,liquid,gas,logic,conveyor")));
            if (connected.Count > 0)
                sb.AppendLine(McpLocalization.Format("cell.decision.ports.connected", null,
                    string.Join(", ", connected.Take(6).ToArray())));
            if (optional.Count > 0)
                sb.AppendLine(McpLocalization.Format("cell.decision.ports.optional", null,
                    string.Join(", ", optional.Take(6).ToArray())));
            if (unknown.Count > 0)
                sb.AppendLine(McpLocalization.Format("cell.decision.ports.unknown", null,
                    string.Join(", ", unknown.Take(6).ToArray()), LocalZoomCall(x, y, "power,liquid,gas,logic,conveyor")));
            return true;
        }

        private static void AppendBuildingPortHints(GameObject go, Building building, List<string> missing, List<string> connected,
            List<string> optional, List<string> unknown)
        {
            if (building.Def.RequiresPowerInput)
                AddPortHint("powerIn", building.GetPowerInputCell(), PowerLayers, missing, connected);
            if (building.Def.RequiresPowerOutput)
                AddPortHint("powerOut", building.GetPowerOutputCell(), PowerLayers, missing, connected);

            foreach (var consumer in go.GetComponents<ConduitConsumer>())
                AddPortHint(consumer.ConduitType == ConduitType.Gas ? "gasIn" : "liquidIn",
                    building.GetUtilityInputCell(), consumer.ConduitType == ConduitType.Gas ? GasLayers : LiquidLayers, missing, connected);
            foreach (var dispenser in go.GetComponents<ConduitDispenser>())
                AddPortHint(dispenser.ConduitType == ConduitType.Gas ? "gasOut" : "liquidOut",
                    building.GetUtilityOutputCell(), dispenser.ConduitType == ConduitType.Gas ? GasLayers : LiquidLayers, missing, connected);

            LogicPorts logic = go.GetComponent<LogicPorts>();
            if (logic != null)
            {
                if (LogicPortReadSemantics.TryBridgeRoute(go, out int from, out int to))
                {
                    AddPortHint("logicIn", from, LogicLayers, missing, connected, optional, unknown,
                        LogicPortReadSemantics.ConnectedAtCell(from), BridgePortRequirement(logic, from));
                    AddPortHint("logicOut", to, LogicLayers, missing, connected, optional, unknown,
                        LogicPortReadSemantics.ConnectedAtCell(to), BridgePortRequirement(logic, to));
                }
                else
                {
                    AppendLogicPortHints(logic, logic.inputPortInfo, "logicIn", missing, connected, optional, unknown);
                    AppendLogicPortHints(logic, logic.outputPortInfo, "logicOut", missing, connected, optional, unknown);
                }
            }

            foreach (var consumer in go.GetComponents<SolidConduitConsumer>())
                AddPortHint("railIn", building.GetUtilityInputCell(), ConveyorLayers, missing, connected, consumer.IsConnected);
            foreach (var dispenser in go.GetComponents<SolidConduitDispenser>())
                AddPortHint("railOut", building.GetUtilityOutputCell(), ConveyorLayers, missing, connected, dispenser.IsConnected);
        }

        private static void AppendLogicPortHints(LogicPorts ports, LogicPorts.Port[] infos, string label, List<string> missing, List<string> connected,
            List<string> optional, List<string> unknown)
        {
            if (infos == null)
                return;
            foreach (var port in infos)
            {
                int cell = LogicPortReadSemantics.ActualCell(ports, port);
                AddPortHint(label, cell, LogicLayers, missing, connected, optional, unknown,
                    LogicPortReadSemantics.ConnectedAtCell(cell), port.requiresConnection);
            }
        }

        private static bool? BridgePortRequirement(LogicPorts ports, int cell)
        {
            // Native bridge endpoints can share an ID and both be input ports.
            // Match their rotated cells, not the synthetic from/to labels.
            bool? required = null;
            foreach (var infos in new[] { ports.inputPortInfo, ports.outputPortInfo })
            {
                if (infos == null)
                    continue;
                foreach (var port in infos)
                    if (LogicPortReadSemantics.ActualCell(ports, port) == cell)
                    {
                        if (port.requiresConnection)
                            return true;
                        required = false;
                    }
            }
            return required;
        }

        private static void AddPortHint(string label, int cell, ObjectLayer[] layers, List<string> missing, List<string> connected,
            bool? connectedOverride = null)
        {
            AddPortHint(label, cell, layers, missing, connected, null, null, connectedOverride, true);
        }

        private static void AddPortHint(string label, int cell, ObjectLayer[] layers, List<string> missing, List<string> connected,
            List<string> optional, List<string> unknown, bool? connectedOverride, bool? required)
        {
            if (!Grid.IsValidCell(cell))
                return;
            bool hasLine = connectedOverride ?? HasLayer(cell, layers);
            string text = label + DecisionCellRef(cell);
            if (hasLine)
                connected.Add(text);
            else if (required == true)
                missing.Add(text);
            else if (required == false)
                optional.Add(text);
            else
                unknown.Add(text);
        }

    }
}
