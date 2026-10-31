using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    // The five primary buckets partition measured calories. Reachability and
    // reservation observations are separate, overlapping dimensions.
    internal sealed class FoodInventoryObservation
    {
        internal int WorldId;
        internal bool? Ready;
        internal string Reason;

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["worldId"] = WorldId >= 0 ? (object)WorldId : null,
                ["ready"] = Ready,
                ["reason"] = Reason,
                ["source"] = "WorldInventory.HasValidCount"
            };
        }
    }

    internal sealed class FoodAccessObservation
    {
        internal FoodInventoryObservation Inventory;
        internal double? CaloriesKcal;
        internal bool? Reachable;
        internal bool? PermittedReachableConsumer;
        internal bool? Fetchable;
        internal bool? StoredPrivate;
        internal bool? BeingConsumed;
        internal bool? AutomationOnlyStorage;
        internal double? FetchTotalAmount;
        internal double? UnreservedFetchAmount;
    }

    internal sealed class FoodConsumerAccess
    {
        private bool reachable, permittedReachable, reachUnknown, permissionUnknown;
        internal bool? Reachable { get { return reachable ? (bool?)true : reachUnknown ? null : (bool?)false; } }
        internal bool? PermittedReachable { get { return permittedReachable ? (bool?)true : permissionUnknown ? null : (bool?)false; } }
        internal void Add(bool? reach, bool? permitted)
        {
            if (reach == true) reachable = true;
            else if (!reach.HasValue) reachUnknown = true;
            if (reach == true && permitted == true) permittedReachable = true;
            else if (reach != false && permitted != false && (!reach.HasValue || !permitted.HasValue))
                permissionUnknown = true;
        }
    }

    internal sealed class FoodAccessBreakdown
    {
        internal double Available;
        internal double Unreachable;
        internal double Excluded;
        internal double Reserved;
        internal double Unknown;
        internal double Reachable;
        internal double ReachabilityUnknown;
        internal int UnknownCalorieItems;
        internal int UnknownAvailabilityItems;
        internal int Items;
        private bool inventoryScopeObserved;
        private readonly Dictionary<int, FoodInventoryObservation> inventories =
            new Dictionary<int, FoodInventoryObservation>();

        internal void MarkInventoryScopeObserved() { inventoryScopeObserved = true; }

        internal void ObserveInventory(FoodInventoryObservation observation)
        {
            inventoryScopeObserved = true;
            if (observation == null) return;
            FoodInventoryObservation previous;
            if (!inventories.TryGetValue(observation.WorldId, out previous))
                inventories.Add(observation.WorldId, new FoodInventoryObservation
                { WorldId = observation.WorldId, Ready = observation.Ready, Reason = observation.Reason });
            else if (previous.Ready != observation.Ready
                || !string.Equals(previous.Reason, observation.Reason, StringComparison.Ordinal))
                inventories[observation.WorldId] = new FoodInventoryObservation
                {
                    WorldId = observation.WorldId, Ready = null,
                    Reason = "inventory_readiness_changed_within_observation"
                };
        }

        internal void CopyInventoryTo(FoodAccessBreakdown target)
        {
            if (!inventoryScopeObserved) return;
            target.MarkInventoryScopeObserved();
            foreach (var observation in inventories.Values) target.ObserveInventory(observation);
        }

        internal int InventoryInitializingWorlds
        {
            get
            {
                int count = 0;
                foreach (var observation in inventories.Values) if (observation.Ready == false) count++;
                return count;
            }
        }

        internal int InventoryUnavailableWorlds
        {
            get
            {
                int count = 0;
                foreach (var observation in inventories.Values) if (observation.WorldId >= 0 && !observation.Ready.HasValue) count++;
                return count;
            }
        }

        internal bool? InventoryReady
        {
            get
            {
                if (!inventoryScopeObserved || inventories.Count == 0) return null;
                bool unavailable = false;
                foreach (var observation in inventories.Values)
                {
                    if (observation.Ready == false) return false;
                    if (!observation.Ready.HasValue) unavailable = true;
                }
                return unavailable ? (bool?)null : true;
            }
        }

        internal bool AssessmentComplete
        {
            get { return InventoryReady == true && UnknownAvailabilityItems == 0 && UnknownCalorieItems == 0; }
        }

        internal string AssessmentState
        {
            get { return AssessmentComplete ? "complete" : InventoryInitializingWorlds > 0 ? "initializing" : "unavailable"; }
        }

        private List<Dictionary<string, object>> InventoryEvidence()
        {
            var keys = new List<int>(inventories.Keys);
            keys.Sort();
            var result = new List<Dictionary<string, object>>();
            foreach (int key in keys) result.Add(inventories[key].ToDictionary());
            return result;
        }

        internal void Add(FoodAccessObservation item)
        {
            Items++;
            if (item.Inventory != null) ObserveInventory(item.Inventory);
            if (!FiniteNonnegative(item.CaloriesKcal))
            {
                UnknownCalorieItems++;
                return;
            }
            double kcal = item.CaloriesKcal.Value;
            if (item.Reachable == true) Reachable += kcal;
            else if (!item.Reachable.HasValue) ReachabilityUnknown += kcal;

            // A confirmed blocker takes precedence over missing, unrelated data.
            if (item.Reachable == false) { Unreachable += kcal; return; }
            if (item.StoredPrivate == true || item.BeingConsumed == true || item.AutomationOnlyStorage == true
                || item.Fetchable == false || item.PermittedReachableConsumer == false)
            { Excluded += kcal; return; }
            if (item.Reachable != true || item.StoredPrivate != false
                || item.BeingConsumed != false || item.AutomationOnlyStorage != false || item.Fetchable != true
                || item.PermittedReachableConsumer != true
                || !FiniteNonnegative(item.FetchTotalAmount) || item.FetchTotalAmount.Value <= 0
                || !FiniteNonnegative(item.UnreservedFetchAmount)
                || item.UnreservedFetchAmount.Value > item.FetchTotalAmount.Value)
            { Unknown += kcal; UnknownAvailabilityItems++; return; }
            double free = kcal * item.UnreservedFetchAmount.Value / item.FetchTotalAmount.Value;
            Available += free;
            Reserved += kcal - free;
        }

        internal static bool FiniteNonnegative(double? value)
        {
            return value.HasValue && !double.IsNaN(value.Value)
                && !double.IsInfinity(value.Value) && value.Value >= 0;
        }

        internal Dictionary<string, object> ToDictionary(bool includePolicy = false)
        {
            var result = new Dictionary<string, object>
            {
                ["availableCaloriesKcal"] = Math.Round(Available, 1),
                ["unreachableCaloriesKcal"] = Math.Round(Unreachable, 1),
                ["excludedCaloriesKcal"] = Math.Round(Excluded, 1),
                ["reservedCaloriesKcal"] = Math.Round(Reserved, 1),
                ["unknownCaloriesKcal"] = Math.Round(Unknown, 1),
                ["reachableCaloriesKcal"] = Math.Round(Reachable, 1),
                ["reachabilityUnknownCaloriesKcal"] = Math.Round(ReachabilityUnknown, 1),
                ["unknownCalorieItems"] = UnknownCalorieItems,
                ["unknownAvailabilityItems"] = UnknownAvailabilityItems,
                ["items"] = Items,
                ["assessmentComplete"] = AssessmentComplete,
                ["assessmentState"] = AssessmentState,
                ["inventoryReady"] = InventoryReady,
                ["inventoryInitializingWorlds"] = InventoryInitializingWorlds,
                ["inventoryUnavailableWorlds"] = InventoryUnavailableWorlds,
            };
            if (includePolicy) result["inventoryReadiness"] = InventoryEvidence();
            if (includePolicy) result["policy"] = "available is the unreserved fraction of fetchable food reachable by at least one same-world permitted consumer; excludes private storage and food being consumed. Five primary calorie buckets partition measured food; reachable is an overlapping observation. Not a per-duplicant survival guarantee or cooking forecast.";
            return result;
        }
    }
}
