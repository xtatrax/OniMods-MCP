using System;
using System.Linq;

namespace OniMcp.Tools
{
    internal static class NativeMaterialInventoryReader
    {
        internal static MaterialInventoryObservation Read(int requestedWorldId, Tag tag)
        {
            int worldId = requestedWorldId;
            int? relatedCount = null;
            float? total = null, available = null, needs = null;
            try
            {
                if (!tag.IsValid) return Result("material_tag_invalid");
                if (ElementLoader.GetElement(tag) == null) return Result("material_is_not_mass_element");
                var manager = ClusterManager.Instance;
                if (manager == null) return Result("cluster_manager_missing");
                worldId = requestedWorldId >= 0 ? requestedWorldId : manager.activeWorldId;
                var world = manager.GetWorld(worldId);
                if (world == null) return Result("world_missing");
                string ownReason = ReadOwn(world, tag, out total, out available, out needs);
                if (ownReason != null) return Result(ownReason);
                var worlds = manager.WorldContainers;
                if (worlds == null) return Result("related_worlds_unavailable");
                var related = worlds.Where(value => value != null && value.ParentWorldId == world.ParentWorldId).ToList();
                relatedCount = related.Count;
                if (!related.Any(value => ReferenceEquals(value, world))) return Result("related_worlds_incomplete");
                foreach (var other in related)
                {
                    if (ReferenceEquals(other, world)) continue;
                    string reason = ReadOwn(other, tag, out _, out _, out _);
                    if (reason != null) return Result("related_" + reason);
                }
                // The ordinary MaterialSelector UI uses this exact native API.
                // Do not add floor items, recompute eligibility, or refresh caches.
                float ui = world.worldInventory.GetAmount(tag, includeRelatedWorlds: true);
                if (!MaterialInventoryObservation.Known(ui)) return Result("native_quantity_invalid");
                return new MaterialInventoryObservation(worldId, relatedCount, total, available, needs, ui, null);
            }
            catch { return Result("native_inventory_read_failed"); }

            MaterialInventoryObservation Result(string reason) =>
                new MaterialInventoryObservation(worldId, relatedCount, total, available, needs, null, reason);
        }

        private static string ReadOwn(WorldContainer world, Tag tag,
            out float? total, out float? available, out float? needs)
        {
            total = available = needs = null;
            var inventory = world.worldInventory;
            if (inventory == null) return "world_inventory_missing";
            if (!inventory.HasValidCount) return "world_inventory_initializing";
            var materialNeeds = world.materialNeeds;
            if (materialNeeds == null) return "material_needs_missing";
            float rawTotal = inventory.GetTotalAmount(tag, includeRelatedWorlds: false);
            if (!MaterialInventoryObservation.Known(rawTotal)) return "native_quantity_invalid";
            total = rawTotal;
            float rawNeeds;
            if (!materialNeeds.TryGetValue(tag, out rawNeeds)) rawNeeds = 0f;
            if (!MaterialInventoryObservation.Known(rawNeeds)) return "native_material_needs_invalid";
            needs = rawNeeds;
            float rawAvailable = inventory.GetAmount(tag, includeRelatedWorlds: false);
            if (!MaterialInventoryObservation.Known(rawAvailable)) return "native_quantity_invalid";
            available = rawAvailable;
            return null;
        }
    }
}
