using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    internal static class BuildingGeometryTools
    {
        internal static CallToolResult Read(JObject args)
        {
            if (Game.Instance == null) return CallToolResult.Error("Game not initialized");
            string action = (args["action"]?.ToString() ?? "inspect").Trim().ToLowerInvariant();
            if (action == "read" || action.Length == 0) action = "inspect";
            bool compact = ToolUtil.GetBool(args, "compact", true);
            var items = new List<Dictionary<string, object>>();
            int matched = 0;
            int limit = Math.Max(1, Math.Min(100, ToolUtil.GetInt(args, "limit") ?? 10));
            if (action == "definition")
            {
                string prefab = args["prefabId"]?.ToString();
                if (string.IsNullOrWhiteSpace(prefab)) return CallToolResult.Error("geometry definition requires an exact prefabId");
                var def = Assets.GetBuildingDef(prefab);
                if (def == null) return CallToolResult.Error("Building definition not found: " + prefab);
                string requestedOrientation = args["orientation"]?.ToString() ?? "Neutral";
                if (!Enum.TryParse(requestedOrientation, true, out Orientation orientation)
                    || !Enum.IsDefined(typeof(Orientation), orientation) || orientation == Orientation.NumRotations)
                    return CallToolResult.Error("Invalid geometry orientation: " + requestedOrientation);
                items.Add(Snapshot(null, def, orientation).ToDictionary(compact));
                matched = 1;
            }
            else if (action == "inspect")
            {
                int? id = ToolUtil.GetInt(args, "id");
                int world = ToolUtil.GetInt(args, "worldId") ?? ClusterManager.Instance?.activeWorldId ?? -1;
                string prefab = args["prefabId"]?.ToString();
                string query = args["query"]?.ToString() ?? args["target"]?.ToString();
                var buildings = Components.BuildingCompletes.Items
                    .Where(b => b != null && b.gameObject != null && b.Def != null)
                    .Where(b => world < 0 || ToolUtil.GameObjectMatchesWorld(b.gameObject, world))
                    .Where(b => !id.HasValue || InstanceId(b.gameObject) == id.Value)
                    .Where(b => string.IsNullOrWhiteSpace(prefab) || b.Def.PrefabID == prefab)
                    .Where(b => string.IsNullOrWhiteSpace(query)
                        || b.Def.PrefabID.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                        || ToolUtil.CleanName(b.gameObject.GetProperName()).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(b => InstanceId(b.gameObject)).ToList();
                matched = buildings.Count;
                if (id.HasValue && matched == 0) return CallToolResult.Error("Completed building not found in selected world: " + id.Value);
                foreach (var building in buildings.Take(limit))
                    items.Add(Snapshot(building, building.Def, building.Orientation).ToDictionary(compact));
            }
            else return CallToolResult.Error("geometry action must be inspect or definition");
            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["readOnly"] = true, ["profile"] = compact ? "compact" : "full",
                ["count"] = items.Count, ["matched"] = matched, ["truncated"] = matched > items.Count,
                ["axis"] = McpLocalization.Get("coordinates.orientation"), ["items"] = items
            }, McpJsonUtil.Settings));
        }

        private static int InstanceId(GameObject go) => go.GetComponent<KPrefabID>()?.InstanceID ?? go.GetInstanceID();

        private static BuildingGeometryCell Cell(int cell)
        {
            return new BuildingGeometryCell
            {
                Cell = cell, Valid = Grid.IsValidCell(cell),
                X = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                Y = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1
            };
        }

        internal static BuildingGeometrySnapshot Snapshot(Building building, BuildingDef def, Orientation orientation)
        {
            bool instance = building != null;
            var go = instance ? building.gameObject : def.BuildingComplete;
            var result = new BuildingGeometrySnapshot
            {
                Id = instance ? (int?)InstanceId(go) : null,
                PrefabId = def.PrefabID, Name = ToolUtil.CleanName(def.Name),
                Scope = instance ? "completed_instance" : "definition",
                CoordinateSpace = instance ? "world_cells" : "relative_offsets",
                Orientation = orientation.ToString(), DefinitionSize = new[] { def.WidthInCells, def.HeightInCells },
                BuildLocationRule = def.BuildLocationRule.ToString(), ObjectLayer = def.ObjectLayer.ToString(),
                TileLayer = def.TileLayer.ToString(), PermittedRotations = def.PermittedRotations.ToString(),
                Source = instance ? "BuildingDef.RunOnArea(Grid.PosToCell(Building), Building.Orientation)"
                    : "BuildingDef.PlacementOffsets + Rotatable.GetRotatedCellOffset"
            };
            int origin = instance ? Grid.PosToCell(go) : Grid.InvalidCell;
            if (!instance) result.Origin = new BuildingGeometryCell { X = 0, Y = 0 };
            if (instance)
            {
                result.Origin = Cell(origin);
                result.WorldId = Grid.IsWorldValidCell(origin) ? (int?)Grid.WorldIdx[origin] : null;
            }
            var footprint = instance ? NativeBuildingFootprint.Instance(go, def)
                : NativeBuildingFootprint.Definition(def, orientation);
            result.Origin = footprint.Origin;
            result.Cells = footprint.Available ? footprint.Cells : null;
            if (footprint.Error != null) result.Errors.Add(footprint.Error);
            if (def.PlacementOffsets != null)
                result.DefinitionOffsets = def.PlacementOffsets.Select(o => new[] { o.x, o.y }).ToList();
            ReadPorts(result, building, def, go, origin, orientation);
            return result;
        }

        private static void ReadPorts(BuildingGeometrySnapshot result, Building building, BuildingDef def,
            GameObject go, int origin, Orientation orientation)
        {
            AddPort(result, "power", "input", null, def.PowerInputOffset, def.RequiresPowerInput, origin, orientation, "BuildingDef.PowerInputOffset");
            AddPort(result, "power", "output", null, def.PowerOutputOffset, def.RequiresPowerOutput, origin, orientation, "BuildingDef.PowerOutputOffset");
            AddPort(result, def.InputConduitType.ToString(), "input", null, def.UtilityInputOffset, def.InputConduitType != ConduitType.None, origin, orientation, "BuildingDef.UtilityInputOffset");
            AddPort(result, def.OutputConduitType.ToString(), "output", null, def.UtilityOutputOffset, def.OutputConduitType != ConduitType.None, origin, orientation, "BuildingDef.UtilityOutputOffset");
            AddPort(result, "high_energy_particle", "input", null, def.HighEnergyParticleInputOffset, def.UseHighEnergyParticleInputPort, origin, orientation, "BuildingDef.HighEnergyParticleInputOffset");
            AddPort(result, "high_energy_particle", "output", null, def.HighEnergyParticleOutputOffset, def.UseHighEnergyParticleOutputPort, origin, orientation, "BuildingDef.HighEnergyParticleOutputOffset");
            ReadPortGroup(result, "logic_read_failed", () =>
            {
                var logic = building != null ? go?.GetComponent<LogicPorts>() : null;
                IEnumerable<LogicPorts.Port> inputs = logic != null ? (IEnumerable<LogicPorts.Port>)logic.inputPortInfo : def.LogicInputPorts;
                IEnumerable<LogicPorts.Port> outputs = logic != null ? (IEnumerable<LogicPorts.Port>)logic.outputPortInfo : def.LogicOutputPorts;
                foreach (var p in inputs ?? Enumerable.Empty<LogicPorts.Port>())
                    AddPort(result, "logic", "input", p.id.ToString(), p.cellOffset, true, origin, orientation, logic != null ? "LogicPorts.inputPortInfo" : "BuildingDef.LogicInputPorts");
                foreach (var p in outputs ?? Enumerable.Empty<LogicPorts.Port>())
                    AddPort(result, "logic", "output", p.id.ToString(), p.cellOffset, true, origin, orientation, logic != null ? "LogicPorts.outputPortInfo" : "BuildingDef.LogicOutputPorts");
            });
            if (go == null)
            {
                result.PortsComplete = false;
                result.Errors.Add("secondary_port_prefab_missing");
                return;
            }
            ReadPortGroup(result, "network_link_read_failed", () =>
            {
                foreach (var link in go.GetComponents<UtilityNetworkLink>())
                {
                    AddPort(result, "network_link", "endpoint_1", link.GetType().Name,
                        link.link1, true, origin, orientation, "UtilityNetworkLink.link1");
                    AddPort(result, "network_link", "endpoint_2", link.GetType().Name,
                        link.link2, true, origin, orientation, "UtilityNetworkLink.link2");
                }
            });
            ReadPortGroup(result, "secondary_ports_read_failed", () =>
            {
                foreach (var p in go.GetComponents<ISecondaryInput>())
                {
                    string reason = NativeReadProviderPolicy.SecondaryInputReadFailure(p);
                    if (reason != null) { result.PortsComplete = false; result.Errors.Add(reason); continue; }
                    foreach (ConduitType type in Enum.GetValues(typeof(ConduitType)))
                        if (type != ConduitType.None && p.HasSecondaryConduitType(type))
                            AddPort(result, type.ToString(), "secondary_input", p.GetType().Name, p.GetSecondaryConduitOffset(type), true, origin, orientation, "ISecondaryInput.GetSecondaryConduitOffset");
                }
                foreach (var p in go.GetComponents<ISecondaryOutput>())
                {
                    string reason = NativeReadProviderPolicy.SecondaryOutputReadFailure(p);
                    if (reason != null) { result.PortsComplete = false; result.Errors.Add(reason); continue; }
                    foreach (ConduitType type in Enum.GetValues(typeof(ConduitType)))
                        if (type != ConduitType.None && p.HasSecondaryConduitType(type))
                            AddPort(result, type.ToString(), "secondary_output", p.GetType().Name, p.GetSecondaryConduitOffset(type), true, origin, orientation, "ISecondaryOutput.GetSecondaryConduitOffset");
                }
            });
        }

        private static void ReadPortGroup(BuildingGeometrySnapshot result, string reason, System.Action read)
        {
            try { read(); }
            catch { result.PortsComplete = false; result.Errors.Add(reason); }
        }

        private static void AddPort(BuildingGeometrySnapshot result, string kind, string direction, string id,
            CellOffset offset, bool present, int origin, Orientation orientation, string source)
        {
            if (!present) return;
            ReadPortGroup(result, "port_position_read_failed", () =>
            {
                var rotated = Rotatable.GetRotatedCellOffset(offset, orientation);
                bool instance = result.Scope == "completed_instance";
                var position = instance && Grid.IsValidCell(origin) ? Cell(Grid.OffsetCell(origin, rotated))
                    : instance ? null : new BuildingGeometryCell { X = rotated.x, Y = rotated.y };
                result.Ports.Add(new BuildingGeometryPort
                {
                    Kind = kind, Direction = direction, Id = id, Position = position,
                    Offset = new[] { offset.x, offset.y }, Source = source
                });
                if (instance && (position == null || !position.Valid))
                { result.PortsComplete = false; result.Errors.Add("port_cell_invalid"); }
            });
        }
    }
}
