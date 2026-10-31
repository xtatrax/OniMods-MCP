using System;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    public static partial class SnapshotTools
    {
        private static FoodAccessBreakdown ReadyFoodAccess(double available, int worldId)
        {
            var result = new FoodAccessBreakdown { Available = available };
            result.ObserveInventory(new FoodInventoryObservation { WorldId = worldId, Ready = true });
            return result;
        }

        internal static JObject EmptyFoodReadinessFixture(bool? ready, int worldId = 7)
        {
            if (Components.Edibles.Items.Count != 0)
                throw new InvalidOperationException("food_initialization_contract: empty-food case must have no edible fixture");
            bool wasEnabled = FoodAvailabilityReader.FoodReadinessFixtureEnabled;
            bool? oldReady = FoodAvailabilityReader.FixtureInventoryReady;
            int oldWorld = FoodAvailabilityReader.FixtureWorld;
            try
            {
                FoodAvailabilityReader.FoodReadinessFixtureEnabled = true;
                FoodAvailabilityReader.FixtureInventoryReady = ready;
                FoodAvailabilityReader.FixtureWorld = worldId;
                FoodAvailabilityReader.ReadinessReads = FoodAvailabilityReader.NativeRationReads = 0;
                var food = BuildFood(worldId, 0, true);
                if (FoodAvailabilityReader.ReadinessReads != 1 || FoodAvailabilityReader.NativeRationReads != 1)
                    throw new InvalidOperationException("food_initialization_contract: production empty food path must observe native readiness exactly once");
                return Fixture(new[] { 0f, 10f }, worldId, food.TotalKcal, beds: 2, foodAccess: food.Access);
            }
            finally
            {
                FoodAvailabilityReader.FoodReadinessFixtureEnabled = wasEnabled;
                FoodAvailabilityReader.FixtureInventoryReady = oldReady;
                FoodAvailabilityReader.FixtureWorld = oldWorld;
            }
        }
    }
}
