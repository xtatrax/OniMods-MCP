using System;
using System.Collections.Generic;
using UnityEngine;

namespace OniMcp.Tools
{
    // Local physical-grid observation only; adjacency, graph membership and flow
    // are independent facts. Never read the combined visual/physical grid here.
    internal static class WorldUtilityPhysicalConnectionRead
    {
        internal sealed class Observation
        {
            internal bool? HasLayer, HasPhysical, HasPlanned;
            internal string State = "unknown", Source, ReasonCode;
            internal UtilityConnections? Mask;
        }

        internal static Observation Read(int cell, ObjectLayer[] layers)
        {
            var result = new Observation();
            if (!Grid.IsValidCell(cell)) return Unknown(result, "cell_invalid");
            if (layers == null || layers.Length == 0) return Unknown(result, "layers_unavailable");
            var objects = new HashSet<GameObject>();
            var objectLayers = new Dictionary<GameObject, ObjectLayer>();
            try
            {
                foreach (var layer in layers)
                {
                    var go = Grid.Objects[cell, (int)layer];
                    if (go != null && objects.Add(go)) objectLayers.Add(go, layer);
                }
            }
            catch { return Unknown(result, "layer_read_failed"); }
            result.HasLayer = objects.Count > 0;
            result.HasPhysical = false;
            result.HasPlanned = false;
            if (objects.Count == 0) { result.State = "absent"; return result; }

            var physical = new List<GameObject>();
            bool unclassified = false;
            try
            {
                foreach (var go in objects)
                {
                    var graph = go.GetComponent<KAnimGraphTileVisualizer>();
                    bool built = go.GetComponent<BuildingComplete>() != null
                        || graph != null && graph.isPhysicalBuilding;
                    bool planned = go.GetComponent<Constructable>() != null
                        || !built && graph != null;
                    if (built) physical.Add(go);
                    if (planned) result.HasPlanned = true;
                    if (!built && !planned) unclassified = true;
                }
                result.HasPhysical = physical.Count > 0 ? true : unclassified ? (bool?)null : false;
                if (result.HasPlanned != true && unclassified) result.HasPlanned = null;
            }
            catch
            {
                result.HasPhysical = null;
                result.HasPlanned = null;
                return Unknown(result, "object_state_read_failed");
            }
            if (unclassified) return Unknown(result, "object_state_unavailable");
            if (physical.Count == 0) { result.State = "planned"; return result; }
            if (Game.Instance == null) return Unknown(result, "game_unavailable");

            IUtilityNetworkMgr selected = null;
            try
            {
                foreach (var go in physical)
                {
                    var graph = go.GetComponent<KAnimGraphTileVisualizer>();
                    if (!NativeReadProviderPolicy.TryGetUtilityManager(graph, go, objectLayers[go],
                        out IUtilityNetworkMgr manager, out string reason)) return Unknown(result, reason);
                    if (selected != null && !ReferenceEquals(selected, manager))
                        return Unknown(result, "manager_ambiguous");
                    selected = manager;
                }
                result.Mask = selected.GetConnections(cell, is_physical_building: true);
                result.State = "physical";
                result.Source = "native_physical";
                return result;
            }
            catch { return Unknown(result, "physical_mask_read_failed"); }
        }

        private static Observation Unknown(Observation result, string reason)
        {
            result.State = "unknown";
            result.Source = null;
            result.Mask = null;
            result.ReasonCode = reason;
            return result;
        }
    }
}
