using System;
using UnityEngine;

namespace OniMcp.Tools
{
    // Only native implementations whose installed method bodies read existing
    // fields are eligible. Exact types exclude custom interface reimplementations.
    internal static class NativeReadProviderPolicy
    {
        private static bool Exact<T>(object provider) => provider != null && provider.GetType() == typeof(T);

        internal static bool IsKnownUtilityProvider(IHaveUtilityNetworkMgr provider, ObjectLayer layer)
        {
            switch (layer)
            {
                case ObjectLayer.GasConduit:
                case ObjectLayer.GasConduitTile:
                case ObjectLayer.ReplacementGasConduit:
                case ObjectLayer.LiquidConduit:
                case ObjectLayer.LiquidConduitTile:
                case ObjectLayer.ReplacementLiquidConduit: return Exact<Conduit>(provider);
                case ObjectLayer.Wire:
                case ObjectLayer.WireTile:
                case ObjectLayer.ReplacementWire: return Exact<Wire>(provider);
                case ObjectLayer.LogicWire:
                case ObjectLayer.LogicWireTile:
                case ObjectLayer.ReplacementLogicWire: return Exact<LogicWire>(provider);
                case ObjectLayer.SolidConduit:
                case ObjectLayer.SolidConduitTile:
                case ObjectLayer.ReplacementSolidConduit: return Exact<SolidConduit>(provider);
                default: return false;
            }
        }

        private static bool IsKnownUtilityManager(IUtilityNetworkMgr manager, ObjectLayer layer)
        {
            switch (layer)
            {
                case ObjectLayer.GasConduit:
                case ObjectLayer.GasConduitTile:
                case ObjectLayer.ReplacementGasConduit:
                case ObjectLayer.LiquidConduit:
                case ObjectLayer.LiquidConduitTile:
                case ObjectLayer.ReplacementLiquidConduit: return Exact<UtilityNetworkManager<FlowUtilityNetwork, Vent>>(manager);
                case ObjectLayer.Wire:
                case ObjectLayer.WireTile:
                case ObjectLayer.ReplacementWire: return Exact<UtilityNetworkManager<ElectricalUtilityNetwork, Wire>>(manager);
                case ObjectLayer.LogicWire:
                case ObjectLayer.LogicWireTile:
                case ObjectLayer.ReplacementLogicWire: return Exact<UtilityNetworkManager<LogicCircuitNetwork, LogicWire>>(manager);
                case ObjectLayer.SolidConduit:
                case ObjectLayer.SolidConduitTile:
                case ObjectLayer.ReplacementSolidConduit: return Exact<UtilityNetworkManager<FlowUtilityNetwork, SolidConduit>>(manager);
                default: return false;
            }
        }

        // Planned-mask reads and disconnect preflight need the ready native
        // graph cache, not a completed building or a provider callback.
        internal static bool TryGetCachedUtilityManager(KAnimGraphTileVisualizer graph, ObjectLayer layer,
            out IUtilityNetworkMgr manager, out string reason)
        {
            manager = null;
            reason = "spawned_manager_unavailable";
            var spawned = graph?.connectionManager;
            if (spawned == null) return false;
            reason = "utility_manager_unsupported";
            if (!IsKnownUtilityManager(spawned, layer)) return false;
            var current = graph.ConnectionManager;
            reason = "current_manager_unavailable";
            if (current == null) return false;
            reason = "spawned_manager_mismatch";
            if (!ReferenceEquals(spawned, current)) return false;
            manager = current;
            reason = null;
            return true;
        }

        internal static bool TryGetUtilityManager(KAnimGraphTileVisualizer graph, GameObject providerObject,
            ObjectLayer layer, out IUtilityNetworkMgr manager, out string reason)
        {
            manager = null;
            reason = "spawned_manager_unavailable";
            // Read readiness fields before any native accessor or interface call.
            var spawned = graph?.connectionManager;
            if (spawned == null) return false;
            reason = "utility_manager_unsupported";
            if (!IsKnownUtilityManager(spawned, layer)) return false;
            var provider = providerObject?.GetComponent<IHaveUtilityNetworkMgr>();
            reason = "utility_provider_unsupported";
            if (!IsKnownUtilityProvider(provider, layer)) return false;
            var current = graph.ConnectionManager;
            reason = "current_manager_unavailable";
            if (current == null) return false;
            reason = "spawned_manager_mismatch";
            if (!ReferenceEquals(spawned, current)) return false;
            reason = "provider_manager_mismatch";
            if (!ReferenceEquals(provider.GetNetworkManager(), current)) return false;
            manager = current;
            reason = null;
            return true;
        }

        internal static string SecondaryInputReadFailure(ISecondaryInput provider)
        {
            bool ready;
            if (Exact<ConduitSecondaryInput>(provider)) ready = ((ConduitSecondaryInput)provider).portInfo != null;
            else if (Exact<ConduitPreferentialFlow>(provider)) ready = ((ConduitPreferentialFlow)provider).portInfo != null;
            else if (Exact<JetSuitLocker>(provider)) ready = ((JetSuitLocker)provider).portInfo != null;
            else if (Exact<RailGun>(provider))
            {
                var native = (RailGun)provider;
                ready = native.gasPortInfo != null && native.liquidPortInfo != null && native.solidPortInfo != null;
            }
            else if (Exact<WarpConduitSender>(provider))
            {
                var native = (WarpConduitSender)provider;
                ready = native.gasPortInfo != null && native.liquidPortInfo != null && native.solidPortInfo != null;
            }
            else return "secondary_input_provider_unsupported";
            return ready ? null : "secondary_input_provider_unavailable";
        }

        internal static string SecondaryOutputReadFailure(ISecondaryOutput provider)
        {
            bool ready;
            if (Exact<ConduitSecondaryOutput>(provider)) ready = ((ConduitSecondaryOutput)provider).portInfo != null;
            else if (Exact<ConduitOverflow>(provider)) ready = ((ConduitOverflow)provider).portInfo != null;
            else if (Exact<ElementFilter>(provider)) ready = ((ElementFilter)provider).portInfo != null;
            else if (Exact<WarpConduitReceiver>(provider))
            {
                var native = (WarpConduitReceiver)provider;
                ready = native.gasPortInfo != null && native.liquidPortInfo != null && native.solidPortInfo != null;
            }
            else return "secondary_output_provider_unsupported";
            return ready ? null : "secondary_output_provider_unavailable";
        }
    }
}
