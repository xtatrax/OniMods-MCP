using System;
using OniMcp.Tools;

internal static class FoodAccessRegression
{
    private static int checks;
    internal static void Run()
    {
        var compact = new FoodAccessBreakdown(); compact.Add(Ready(100));
        Check(!compact.ToDictionary().ContainsKey("policy"), "default response omits long policy");
        Check(compact.ToDictionary(true).ContainsKey("policy"), "explicit evidence response includes policy");
        foreach (var field in compact.ToDictionary())
            Check(Equals(field.Value, compact.ToDictionary(true)[field.Key]), "output detail preserves aggregate " + field.Key);
        var split = new FoodConsumerAccess(); split.Add(true, false); split.Add(false, true);
        Check(split.Reachable == true && split.PermittedReachable == false, "different dupe access and permission cannot be combined");
        split.Add(null, true);
        Check(split.PermittedReachable == null, "unknown route may belong to permitted dupe");
        split.Add(true, true);
        Check(split.PermittedReachable == true, "one confirmed eligible dupe is sufficient");
        var absentDiet = new FoodConsumerAccess(); absentDiet.Add(true, null);
        Check(absentDiet.PermittedReachable == null, "missing diet remains unknown");
        var noDupes = new FoodConsumerAccess();
        Check(noDupes.Reachable == false && noDupes.PermittedReachable == false, "empty world has no current consumer");
        var zeroMissing = Ready(0); zeroMissing.Fetchable = null;
        var unknownZero = new FoodAccessBreakdown(); unknownZero.Add(zeroMissing);
        Check(!(bool)unknownZero.ToDictionary()["assessmentComplete"], "zero calories cannot hide missing availability");
        var mixed = new FoodAccessBreakdown();
        var loose = Ready(1000); mixed.Add(loose);
        var closedStorage = Ready(2000); closedStorage.Reachable = false; mixed.Add(closedStorage);
        var cooking = Ready(1600); cooking.StoredPrivate = true; mixed.Add(cooking);
        var denied = Ready(300); denied.PermittedReachableConsumer = false; mixed.Add(denied);
        var partial = Ready(1000); partial.UnreservedFetchAmount = 0.25; mixed.Add(partial);
        var missing = Ready(400); missing.Fetchable = null; mixed.Add(missing);
        Equal(1250, mixed.Available, "loose plus unreserved quarter");
        Equal(2000, mixed.Unreachable, "closed storage remains unreachable");
        Equal(1900, mixed.Excluded, "private cooking plus forbidden diet");
        Equal(750, mixed.Reserved, "reserved fraction");
        Equal(400, mixed.Unknown, "missing monitor is unknown");
        Equal(6300, mixed.Available + mixed.Unreachable + mixed.Excluded + mixed.Reserved + mixed.Unknown, "partition conservation");
        Equal(4300, mixed.Reachable, "reachable is independent of usability");
        Check(!(bool)mixed.ToDictionary()["assessmentComplete"], "unknown prevents complete assessment");

        var carrying = Ready(200); carrying.StoredPrivate = true;
        Expect(carrying, "excludedCaloriesKcal", 200, "carrier private storage");
        var eating = Ready(200); eating.BeingConsumed = true;
        Expect(eating, "excludedCaloriesKcal", 200, "being consumed is not a shared reserve");
        var autoOnly = Ready(200); autoOnly.Fetchable = false;
        Expect(autoOnly, "excludedCaloriesKcal", 200, "unfetchable");
        var splitDupe = Ready(200); splitDupe.PermittedReachableConsumer = false;
        Expect(splitDupe, "excludedCaloriesKcal", 200, "reach and permission must hold for the same dupe");
        var noNavigator = Ready(200); noNavigator.Reachable = null; noNavigator.PermittedReachableConsumer = null;
        Expect(noNavigator, "unknownCaloriesKcal", 200, "missing navigator is unknown");
        var fullReservation = Ready(100); fullReservation.UnreservedFetchAmount = 0;
        Expect(fullReservation, "reservedCaloriesKcal", 100, "fully reserved");
        var onlyAutomation = Ready(200); onlyAutomation.AutomationOnlyStorage = true;
        Expect(onlyAutomation, "excludedCaloriesKcal", 200, "automation-only storage cannot feed a dupe");
        var fetchMass = Ready(100); fetchMass.FetchTotalAmount = 2; fetchMass.UnreservedFetchAmount = 0.5;
        Expect(fetchMass, "availableCaloriesKcal", 25, "reservation ratio uses matching fetch amounts, not item units");
        var noReservation = Ready(100); noReservation.UnreservedFetchAmount = 1;
        Expect(noReservation, "availableCaloriesKcal", 100, "zero reservation");
        foreach (var units in new double?[] { null, 0, -1, double.NaN, double.PositiveInfinity })
        {
            var invalid = Ready(100); invalid.FetchTotalAmount = units;
            Expect(invalid, "unknownCaloriesKcal", 100, "invalid total units");
        }
        foreach (var units in new double?[] { null, -1, 2, double.NaN, double.PositiveInfinity })
        {
            var invalid = Ready(100); invalid.UnreservedFetchAmount = units;
            Expect(invalid, "unknownCaloriesKcal", 100, "invalid free units");
        }
        foreach (var calories in new double?[] { null, -1, double.NaN, double.PositiveInfinity })
        {
            var invalid = Ready(100); invalid.CaloriesKcal = calories;
            var b = new FoodAccessBreakdown(); b.Add(invalid);
            Check(b.UnknownCalorieItems == 1, "unmeasured calorie item retained");
            Check(!(bool)b.ToDictionary()["assessmentComplete"], "unmeasured calorie invalidates completeness");
        }
        var zero = new FoodAccessBreakdown(); zero.Add(Ready(0));
        Check(zero.UnknownCalorieItems == 0 && (bool)zero.ToDictionary()["assessmentComplete"], "real zero is a valid observation");
        var blockedUnknown = Ready(100); blockedUnknown.Reachable = false; blockedUnknown.Fetchable = null;
        Expect(blockedUnknown, "unreachableCaloriesKcal", 100, "confirmed blocker independent of missing monitor");
        ReadinessBoundaries();
        Console.WriteLine("Food access regression: " + checks + " checks passed");
    }
    private static void ReadinessBoundaries()
    {
        var unobserved = new FoodAccessBreakdown();
        Check(!unobserved.AssessmentComplete && unobserved.AssessmentState == "unavailable",
            "food_initialization_contract: an unobserved inventory is not a confirmed zero");
        Check(unobserved.InventoryReady == null, "unobserved inventory stays null");
        var pending = new FoodAccessBreakdown();
        pending.ObserveInventory(new FoodInventoryObservation { WorldId = 7, Ready = false,
            Reason = "world_inventory_initializing" });
        Check(!pending.AssessmentComplete && pending.AssessmentState == "initializing"
            && pending.InventoryInitializingWorlds == 1 && pending.Items == 0,
            "food_initialization_contract: pending empty inventory remains initializing");
        Check(!pending.ToDictionary().ContainsKey("inventoryReadiness")
            && pending.ToDictionary(true).ContainsKey("inventoryReadiness"),
            "world readiness evidence is only present in explicit detail");
        var missingScope = new FoodAccessBreakdown();
        missingScope.ObserveInventory(new FoodInventoryObservation { WorldId = -1, Reason = "cluster_manager_missing" });
        Check(missingScope.InventoryReady == null && !missingScope.AssessmentComplete
            && missingScope.InventoryUnavailableWorlds == 0,
            "unavailable global scope does not manufacture a number of unknown worlds");
        var zero = new FoodAccessBreakdown();
        zero.ObserveInventory(new FoodInventoryObservation { WorldId = 7, Ready = true });
        Check(zero.AssessmentComplete && zero.AssessmentState == "complete" && zero.Available == 0,
            "confirmed ready empty inventory remains a measured zero");
        var absent = new FoodAccessBreakdown();
        absent.ObserveInventory(new FoodInventoryObservation { WorldId = 7, Ready = null,
            Reason = "world_inventory_missing" });
        Check(!absent.AssessmentComplete && absent.AssessmentState == "unavailable"
            && absent.InventoryReady == null && absent.InventoryInitializingWorlds == 0,
            "missing inventory must not be inferred to be initializing");
        var mixed = new FoodAccessBreakdown();
        mixed.ObserveInventory(new FoodInventoryObservation { WorldId = 7, Ready = true });
        mixed.ObserveInventory(new FoodInventoryObservation { WorldId = 8, Ready = false,
            Reason = "world_inventory_initializing" });
        Check(!mixed.AssessmentComplete && mixed.AssessmentState == "initializing",
            "one pending requested world prevents a complete all-world assessment");
        mixed.ObserveInventory(new FoodInventoryObservation { WorldId = 8, Ready = false,
            Reason = "world_inventory_initializing" });
        Check(mixed.InventoryInitializingWorlds == 1, "repeated item evidence does not count a world twice");
        var perType = new FoodAccessBreakdown();
        mixed.CopyInventoryTo(perType);
        Check(perType.AssessmentState == "initializing" && perType.InventoryInitializingWorlds == 1
            && perType.Items == 0 && perType.Available == 0,
            "type readiness inherits requested worlds without copying or duplicating calories");
        var conflict = new FoodAccessBreakdown();
        conflict.ObserveInventory(new FoodInventoryObservation { WorldId = 7, Ready = true });
        conflict.ObserveInventory(new FoodInventoryObservation { WorldId = 7, Ready = false,
            Reason = "world_inventory_initializing" });
        Check(conflict.InventoryReady == null && conflict.AssessmentState == "unavailable",
            "contradictory same-observation evidence is not promoted to ready");
        var missingItem = Ready(0);
        missingItem.Fetchable = null;
        var readyButMissing = new FoodAccessBreakdown(); readyButMissing.Add(missingItem);
        Check(readyButMissing.InventoryReady == true && !readyButMissing.AssessmentComplete
            && readyButMissing.AssessmentState == "unavailable" && readyButMissing.UnknownAvailabilityItems == 1,
            "ready inventory does not conceal unknown zero-calorie item availability");
    }

    private static FoodAccessObservation Ready(double kcal)
    {
        return new FoodAccessObservation { Inventory = new FoodInventoryObservation { WorldId = 0, Ready = true },
            CaloriesKcal = kcal, Reachable = true,
            PermittedReachableConsumer = true, Fetchable = true, StoredPrivate = false,
            BeingConsumed = false, AutomationOnlyStorage = false, FetchTotalAmount = 1, UnreservedFetchAmount = 1 };
    }
    private static void Expect(FoodAccessObservation item, string bucket, double value, string name)
    {
        var b = new FoodAccessBreakdown(); b.Add(item);
        Equal(value, Convert.ToDouble(b.ToDictionary()[bucket]), name);
        if (item.CaloriesKcal.HasValue)
            Equal(item.CaloriesKcal.Value, b.Available + b.Unreachable + b.Excluded + b.Reserved + b.Unknown, name + " conservation");
    }
    private static void Equal(double expected, double actual, string name)
    { Check(Math.Abs(expected - actual) < 0.000001, name); }
    private static void Check(bool ok, string name)
    { checks++; if (!ok) throw new InvalidOperationException("Food access: " + name); }
}
