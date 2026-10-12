using System;
using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    internal sealed class FoodAvailabilityReader
    {
        private readonly Dictionary<int, List<MinionIdentity>> dupes =
            Components.LiveMinionIdentities.Items.Where(d => d != null)
                .GroupBy(d => d.GetMyWorldId()).ToDictionary(g => g.Key, g => g.ToList());

        internal FoodAccessObservation Read(Edible edible, Pickupable pickup, int worldId,
            out Dictionary<string, object> evidence, bool includeEvidence = true)
        {
            var result = new FoodAccessObservation();
            var reasons = new List<string>();
            try { result.CaloriesKcal = edible.Calories / 1000d; }
            catch { reasons.Add("calories_read_failed"); }
            try { result.BeingConsumed = edible.isBeingConsumed; }
            catch { reasons.Add("consumption_read_failed"); }
            try
            {
                var storageForTransfer = pickup != null ? pickup.storage : null;
                result.AutomationOnlyStorage = storageForTransfer != null
                    && storageForTransfer.automatable != null
                    && storageForTransfer.automatable.GetAutomationOnly();
            }
            catch { reasons.Add("storage_automation_read_failed"); }
            string foodId = edible.FoodID;
            int cell = pickup != null ? ToolUtil.PickupableCell(pickup) : Grid.PosToCell(edible);
            if (pickup != null && Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell))
            {
                var consumerAccess = new FoodConsumerAccess();
                bool inventoryReady = false;
                try
                {
                    var inventory = ClusterManager.Instance?.GetWorld(worldId)?.worldInventory;
                    inventoryReady = inventory != null && inventory.HasValidCount;
                }
                catch { reasons.Add("inventory_readiness_failed"); }
                if (!inventoryReady) reasons.Add("world_inventory_initializing");
                List<MinionIdentity> candidates;
                if (!dupes.TryGetValue(worldId, out candidates)) candidates = new List<MinionIdentity>();
                foreach (var dupe in candidates)
                {
                    bool? canReach = null, permitted = null;
                    try
                    {
                        var navigator = dupe.GetComponent<Navigator>();
                        if (inventoryReady && navigator != null && navigator.NavGrid != null && navigator.PathGrid != null)
                            canReach = navigator.CanReach(pickup);
                    }
                    catch { reasons.Add("navigator_read_failed"); }
                    try
                    {
                        var consumer = dupe.GetComponent<ConsumableConsumer>();
                        if (consumer != null && !string.IsNullOrEmpty(foodId))
                            permitted = consumer.IsPermitted(foodId);
                    }
                    catch { reasons.Add("diet_read_failed"); }
                    consumerAccess.Add(canReach, permitted);
                }
                result.Reachable = consumerAccess.Reachable;
                result.PermittedReachableConsumer = consumerAccess.PermittedReachable;
                if (candidates.Count == 0) reasons.Add("no_live_same_world_duplicants");
                try
                {
                    var kpid = pickup.KPrefabID ?? pickup.GetComponent<KPrefabID>();
                    if (kpid != null) result.StoredPrivate = kpid.HasTag(GameTags.StoredPrivate);
                }
                catch { reasons.Add("storage_tag_read_failed"); }
                try
                {
                    var monitor = pickup.GetSMI<FetchableMonitor.Instance>();
                    if (monitor != null && inventoryReady) result.Fetchable = monitor.IsFetchable();
                    else if (monitor == null) reasons.Add("fetchable_monitor_missing");
                }
                catch { reasons.Add("fetchability_read_failed"); }
                try
                {
                    result.FetchTotalAmount = pickup.FetchTotalAmount;
                    result.UnreservedFetchAmount = pickup.UnreservedFetchAmount;
                }
                catch { reasons.Add("reservation_read_failed"); }
            }
            else reasons.Add(pickup == null ? "pickupable_missing" : "invalid_item_cell");

            evidence = null;
            if (!includeEvidence) return result;

            var storage = pickup != null ? pickup.storage : null;
            var owner = storage != null ? storage.gameObject : null;
            var kpidOwner = owner != null ? owner.GetComponent<KPrefabID>() : null;
            bool? fabricatorStorage = owner != null ? (bool?)(owner.GetComponent<ComplexFabricator>() != null) : false;
            bool? carried = owner != null ? (bool?)(owner.GetComponent<MinionIdentity>() != null) : false;
            var buckets = new FoodAccessBreakdown();
            buckets.Add(result);
            evidence = new Dictionary<string, object>
            {
                ["foodId"] = foodId,
                ["cell"] = Grid.IsValidCell(cell) ? (object)cell : null,
                ["x"] = Grid.IsValidCell(cell) ? (object)Grid.CellColumn(cell) : null,
                ["y"] = Grid.IsValidCell(cell) ? (object)Grid.CellRow(cell) : null,
                ["worldId"] = worldId,
                ["reachable"] = result.Reachable,
                ["permittedReachableConsumer"] = result.PermittedReachableConsumer,
                ["fetchable"] = result.Fetchable,
                ["storedPrivate"] = result.StoredPrivate,
                ["beingConsumed"] = result.BeingConsumed,
                ["fetchTotalAmount"] = result.FetchTotalAmount,
                ["unreservedFetchAmount"] = result.UnreservedFetchAmount,
                ["automationOnlyStorage"] = result.AutomationOnlyStorage,
                ["stored"] = storage != null,
                ["storagePrefabId"] = kpidOwner != null ? kpidOwner.PrefabTag.Name : null,
                ["inFabricatorStorage"] = fabricatorStorage,
                ["carried"] = carried,
                ["access"] = buckets.ToDictionary(),
                ["reasons"] = reasons.Distinct().ToList(),
                ["sources"] = new Dictionary<string, object>
                {
                    ["readiness"] = "WorldInventory.HasValidCount gates navigation/fetchability after load",
                    ["reachability"] = "Navigator.CanReach(Pickupable/IApproachable); same-world live duplicants",
                    ["diet"] = "ConsumableConsumer.IsPermitted(Edible.FoodID); tested on the same reachable duplicant",
                    ["fetchability"] = "FetchableMonitor.Instance.IsFetchable",
                    ["reservation"] = "Pickupable.FetchTotalAmount / UnreservedFetchAmount (same native fetch units, mass)",
                    ["automation"] = "Storage.automatable.GetAutomationOnly (minion pickup exclusion)",
                    ["storage"] = "Pickupable.storage / KPrefabID.HasTag(GameTags.StoredPrivate)",
                    ["consumption"] = "Edible.isBeingConsumed"
                }
            };
            return result;
        }

        internal static FoodAccessBreakdown CaptureAccess(bool visibleOnly, int? worldId = null)
        {
            var reader = new FoodAvailabilityReader();
            var access = new FoodAccessBreakdown();
            foreach (var edible in Components.Edibles.Items)
            {
                if (edible == null || edible.gameObject == null) continue;
                var pickup = edible.GetComponent<Pickupable>();
                int cell = pickup != null ? ToolUtil.PickupableCell(pickup) : Grid.PosToCell(edible);
                if (!ToolUtil.VisibleCellAllowed(cell, visibleOnly)) continue;
                int itemWorld = Grid.IsValidCell(cell) ? Grid.WorldIdx[cell] : edible.GetMyWorldId();
                if (worldId.HasValue && itemWorld != worldId.Value) continue;
                Dictionary<string, object> evidence;
                access.Add(reader.Read(edible, pickup, itemWorld, out evidence, false));
            }
            return access;
        }

        internal static Dictionary<string, object> NativeRations(int? worldId)
        {
            var entries = new List<Dictionary<string, object>>();
            bool complete = ClusterManager.Instance != null;
            double total = 0;
            if (ClusterManager.Instance != null)
            {
                foreach (var world in ClusterManager.Instance.WorldContainers)
                {
                    if (world == null || (worldId.HasValue && world.id != worldId.Value)) continue;
                    double? kcal = null;
                    string reason = null;
                    var inventory = world.worldInventory;
                    try
                    {
                        var tracker = RationTracker.Get();
                        if (inventory == null) reason = "world_inventory_missing";
                        else if (!inventory.HasValidCount) reason = "inventory_initializing";
                        else if (tracker == null) reason = "ration_tracker_missing";
                        else
                        {
                            double value = tracker.CountAmount(null, inventory, true) / 1000d;
                            if (FoodAccessBreakdown.FiniteNonnegative(value)) kcal = value;
                            else reason = "non_finite_or_negative_native_value";
                        }
                    }
                    catch { reason = "native_rations_read_failed"; }
                    if (kcal.HasValue) total += kcal.Value;
                    else complete = false;
                    entries.Add(new Dictionary<string, object>
                    {
                        ["worldId"] = world.id,
                        ["caloriesKcal"] = kcal.HasValue ? (object)Math.Round(kcal.Value, 1) : null,
                        ["inventoryReady"] = inventory != null && inventory.HasValidCount,
                        ["reason"] = reason
                    });
                }
            }
            if (worldId.HasValue && entries.Count == 0) complete = false;
            return new Dictionary<string, object>
            {
                ["caloriesKcal"] = complete ? (object)Math.Round(total, 1) : null,
                ["complete"] = complete,
                ["worlds"] = entries,
                ["source"] = "RationTracker.Get().CountAmount(null, world.worldInventory, true) / 1000",
                ["scope"] = "requested world(s), native fetchable inventory; independent of visibleOnly and limit; not cached HUD text or resource-panel quantity",
                ["displayUpdateTimeKnown"] = false
            };
        }
    }
}
