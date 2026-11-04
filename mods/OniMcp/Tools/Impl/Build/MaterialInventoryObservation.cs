using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    // Immutable, request-local evidence. Native construction demand is not a
    // per-Pickupable fetch reservation or proof of delivery to a destination.
    internal sealed class MaterialInventoryObservation
    {
        internal readonly int WorldId;
        internal readonly int? RelatedWorldCount;
        internal readonly float? OwnTotalKg, OwnAvailableKg, OwnMaterialNeedsKg, UiAvailableKg;
        internal readonly string ReasonCode;
        internal string State => ReasonCode == null ? "known" : "unknown";

        internal MaterialInventoryObservation(int worldId, int? relatedWorldCount,
            float? ownTotal, float? ownAvailable, float? ownNeeds, float? uiAvailable, string reason)
        {
            WorldId = worldId;
            RelatedWorldCount = relatedWorldCount;
            OwnTotalKg = ownTotal;
            OwnAvailableKg = ownAvailable;
            OwnMaterialNeedsKg = ownNeeds;
            UiAvailableKg = uiAvailable;
            ReasonCode = reason;
        }

        internal static bool Known(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        internal static object JsonMass(float? value) => value.HasValue && Known(value.Value)
            ? (object)Math.Round(value.Value, 3) : null;

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["state"] = State, ["reasonCode"] = ReasonCode, ["worldId"] = WorldId,
                ["relatedWorldCount"] = RelatedWorldCount,
                ["uiAvailableKg"] = JsonMass(UiAvailableKg),
                ["uiAvailableSource"] = "WorldInventory.GetAmount(tag,true)",
                ["uiScope"] = "same_parent_worlds_after_native_material_needs",
                ["ownWorldTotalKg"] = JsonMass(OwnTotalKg),
                ["ownWorldTotalSource"] = "WorldInventory.GetTotalAmount(tag,false)",
                ["ownWorldAvailableKg"] = JsonMass(OwnAvailableKg),
                ["ownWorldAvailableSource"] = "WorldInventory.GetAmount(tag,false)",
                ["ownWorldMaterialNeedsKg"] = JsonMass(OwnMaterialNeedsKg),
                ["materialNeedsSource"] = "WorldContainer.materialNeeds[tag]",
                ["fetchReservationsEvaluated"] = false,
                ["transportableKg"] = null, ["transportStatus"] = "not_evaluated"
            };
        }
    }
}
