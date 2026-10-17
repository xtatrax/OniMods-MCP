using System;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class BuildingOperationGuidanceRegression
{
    private static void Main() => Run();

    internal static void Run()
    {
        FullPrefabIdWins();
        foreach (string prefab in new[] { "Tile", "Bed", "LiquidConduitBridge" })
        {
            var guide = JObject.FromObject(BuildingOperationGuidance.MapPlacement(prefab));
            Check(!(bool)guide["directBuildAreaAllowed"], "Construction must not promise direct build_area");
            Check((string)guide["read"]["arguments"]["path"] == "/active/map/viewport.md", "Construction needs editable map");
            Check((string)guide["preview"]["tool"] == "world_editor", "Preview must use public map route");
            Check((string)guide["preview"]["arguments"]["command"] == "edit", "Preview validates map patch");
            Check((bool)guide["preview"]["arguments"]["dryRun"] && !(bool)guide["preview"]["arguments"]["confirm"], "Preview must not commit");
            Check((string)guide["buildToken"] == prefab + ":5", "Token preserves exact prefab identity");
        }
        var direction = JObject.FromObject(BuildingOperationGuidance.ConfigurationRead("direction", false, "WashBasin", 0, 123));
        Check(!(bool)direction["stateIncluded"], "Capability alone is not an observed direction");
        Check((string)direction["read"]["arguments"]["surface"] == "option", "Direction reader must reach option surface");
        Check((int)direction["expectedTargetId"] == 123, "List reader must identify the intended instance");
        var activation = JObject.FromObject(BuildingOperationGuidance.ConfigurationRead("activation_range", false, "BatterySmart", 0, 456));
        Check((string)activation["read"]["arguments"]["surface"] == "activation", "Battery thresholds use activation surface");
        var threshold = JObject.FromObject(BuildingOperationGuidance.ConfigurationRead("threshold", true, "LogicTemperatureSensor", 0, 789));
        Check((bool)threshold["stateIncluded"] && (string)threshold["read"]["arguments"]["domain"] == "config", "Native threshold state remains in config");
        foreach (string capability in new[] { "filterable", "tree_filter", "flat_filter" })
        {
            var filter = JObject.FromObject(BuildingOperationGuidance.ConfigurationRead(capability, false, "GasFilter", 2, 321));
            Check(filter["read"].Type == JTokenType.Object, "Filter capability must link to its existing state reader");
            var args = filter["read"]["arguments"];
            Check((string)args["domain"] == "filter" && (string)args["action"] == "list", "Filter state reader uses filter list");
            string kind = capability == "filterable" ? "single" : capability == "tree_filter" ? "tree" : "flat";
            Check((string)args["kind"] == kind && (string)args["query"] == "GasFilter", "Filter reader preserves kind and prefab query");
            Check((int)args["worldId"] == 2 && (int)filter["expectedTargetId"] == 321, "Reader preserves world and target matching guidance");
            Check(args["id"] == null && !(bool)filter["stateIncluded"], "List must not promise an unsupported id filter or included state");
        }
        var unsupported = JObject.FromObject(BuildingOperationGuidance.ConfigurationRead("capacity", false, "StorageLocker", 0, 1));
        Check(unsupported["read"].Type == JTokenType.Null, "Do not promise a reader that only repeats capability");
        Console.WriteLine("Building operation guidance regression passed");
    }

    private static void FullPrefabIdWins()
    {
        var previous = Assets.BuildingDefs.ToArray();
        var method = typeof(WorldEditorTools).GetMethod("TryResolveBuildPrefabFromToken",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Check(method != null, "Map token resolver is available");
        try
        {
            Assets.BuildingDefs.Clear();
            Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "LadderBed", Name = "Ladder Bed" });
            Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "Ladder", Name = "Ladder" });
            Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "LogicWireBridge", Name = "Logic Bridge" });
            Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "WireBridge", Name = "Wire Bridge" });
            foreach (string id in new[] { "Ladder", "WireBridge" })
            {
                object[] args = { id + ":5", 'x', null };
                Check((bool)method.Invoke(null, args) && (string)args[2] == id,
                    "Exact prefab must beat earlier partial match: " + id);
            }
        }
        finally
        {
            Assets.BuildingDefs.Clear();
            Assets.BuildingDefs.AddRange(previous);
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
