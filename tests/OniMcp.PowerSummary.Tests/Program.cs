using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static readonly List<string> failures = new List<string>();
    private static readonly List<Operational> observed = new List<Operational>();
    private static void Main()
    {
        MixedCircuit();
        EmptyAndUnavailable();
        if (failures.Count != 0)
        {
            foreach (string failure in failures) Console.Error.WriteLine("FAIL: " + failure);
            throw new InvalidOperationException("Power operational count semantic failures: " + failures.Count);
        }
        Console.WriteLine("Power operational counts: " + checks + " checks passed (verbatim production factory/helpers; native inputs substituted)");
    }
    private static void Check(bool condition, string message) { checks++; if (!condition) failures.Add(message); }
    private static void Reset()
    {
        Game.Instance = new Game(); ClusterManager.Instance = new ClusterManager { activeWorldId = 0 };
        Components.Batteries.Items.Clear(); Components.Generators.Items.Clear(); Components.EnergyConsumers.Items.Clear();
        Grid.WorldIdx.Clear(); observed.Clear();
    }
    private static GameObject Object(int id, int world, bool? operational)
    {
        var go = new GameObject { Identity = id, Cell = id, World = world, Name = "fixture " + id };
        go.ExistingComponents[typeof(KPrefabID)] = new KPrefabID { InstanceID = id, PrefabTag = new Tag { Name = "Fixture" + id } };
        if (operational.HasValue)
        {
            var op = new Operational { Value = operational.Value };
            go.ExistingComponents[typeof(Operational)] = op; observed.Add(op);
        }
        Grid.WorldIdx[id] = world;
        return go;
    }
    private static Generator Generator(int id, int world, ushort circuit, bool? op, float rating, float stored, bool empty)
    {
        var value = new Generator { gameObject = Object(id, world, op), CircuitID = circuit, Capacity = 200,
            WattageRating = rating, JoulesAvailable = stored, IsEmpty = empty };
        Components.Generators.Items.Add(value); return value;
    }
    private static EnergyConsumer Consumer(int id, int world, ushort circuit, bool? op, float rating, float needed, float used, bool powered)
    {
        var value = new EnergyConsumer { gameObject = Object(id, world, op), CircuitID = circuit,
            BaseWattageRating = rating, WattsNeededWhenActive = needed, WattsUsed = used, IsPowered = powered };
        Components.EnergyConsumers.Items.Add(value); return value;
    }
    private static JObject Read(JObject args)
    {
        CallToolResult result = PowerAndRoomTools.GetPowerSummary().Handler(args);
        Check(!result.IsError, "actual GetPowerSummary handler returns success");
        return JObject.Parse(result.Content[0].Text);
    }
    private static JObject Circuit(JObject body, string id) { return body["circuits"].OfType<JObject>().Single(row => (string)row["circuitId"] == id); }
    private static JObject WithoutDetails(JObject body)
    { var copy = (JObject)body.DeepClone(); copy.Remove("generators"); copy.Remove("consumers"); copy.Remove("batteries"); return copy; }
    private static void Number(JToken value, double expected, string message)
    { Check(value != null && Math.Abs(value.Value<double>() - expected) < 0.0001, message); }
    private static void MixedCircuit()
    {
        Reset();
        Generator(11, 0, 2, true, 600, 120, false);
        Generator(12, 0, 2, false, 400, 0, true);
        Generator(13, 0, 7, false, 50, 4, false);
        Generator(14, 0, ushort.MaxValue, null, 20, 2, false);
        var outsideG = Generator(15, 1, 2, true, 999, 999, true);
        Consumer(21, 0, 2, true, 100, 110, 20, false);
        Consumer(22, 0, 2, false, 200, 220, 0, true);
        Consumer(23, 0, 7, false, 25, 30, 3, false);
        Consumer(24, 0, ushort.MaxValue, null, 5, 6, 0, false);
        var outsideC = Consumer(25, 1, 2, true, 999, 999, 999, true);
        Components.Batteries.Items.Add(new Battery { gameObject = Object(31, 0, true), CircuitID = 2, Capacity = 1000, JoulesAvailable = 250, WattsUsed = 3 });
        Components.Generators.Items.Add(null); Components.EnergyConsumers.Items.Add(null); Components.Batteries.Items.Add(null);
        var generatorRefs = Components.Generators.Items.ToArray(); var consumerRefs = Components.EnergyConsumers.Items.ToArray();
        JObject detailed = Read(JObject.Parse("{includeDetails:true,limit:80}"));
        JObject summary = (JObject)detailed["summary"], main = Circuit(detailed, "2"), stopped = Circuit(detailed, "7"), unconnected = Circuit(detailed, "-1");
        Number(summary["generatorCount"], 4, "world and null filters preserve generator count");
        Number(summary["consumerCount"], 4, "world and null filters preserve consumer count");
        Number(summary["generatorOperational"], 1, "summary uses native IsOperational input");
        Number(summary["consumerOperational"], 1, "summary consumer predicate remains unchanged");
        Number(main["generatorCount"], 2, "same circuit contains active and stopped generators");
        Number(main["consumerCount"], 2, "same circuit contains active and stopped consumers");
        Number(main["generatorOperational"], 1, "generator circuit count uses observed operational predicate");
        Number(main["consumerOperational"], 1, "consumer circuit count uses observed operational predicate");
        Number(stopped["generatorOperational"], 0, "all-stopped circuit keeps known generator zero");
        Number(stopped["consumerOperational"], 0, "all-stopped circuit keeps known consumer zero");
        Number(unconnected["generatorOperational"], 0, "missing Operational generator retains old false meaning");
        Number(unconnected["consumerOperational"], 0, "missing Operational consumer retains old false meaning");
        Check((bool)unconnected["isConnected"] == false && (string)unconnected["circuitKey"] == "-1", "invalid native circuit retains public identifier");
        Check(detailed["circuits"].Select(row => (string)row["circuitId"]).SequenceEqual(new[] { "2", "7", "-1" }), "existing circuit metric sort retained");
        Number(detailed["circuits"].Sum(row => row["generatorOperational"].Value<int>()), 1, "circuit generator counts agree with summary");
        Number(detailed["circuits"].Sum(row => row["consumerOperational"].Value<int>()), 1, "circuit consumer counts agree with summary");
        Check(detailed["generators"].Count() == 4 && detailed["consumers"].Count() == 4 && detailed["batteries"].Count() == 1, "details retain world-scoped native devices");
        Number(detailed["generators"].Count(row => (bool)row["operational"]), 1, "generator details agree with aggregate");
        Number(detailed["consumers"].Count(row => (bool)row["operational"]), 1, "consumer details agree with aggregate");
        Check((bool)detailed["consumers"][0]["operational"] && !(bool)detailed["consumers"][0]["powered"]
            && !(bool)detailed["consumers"][1]["operational"] && (bool)detailed["consumers"][1]["powered"], "IsPowered is independent of count predicate");
        Number(summary["generatorEmpty"], 1, "legacy native IsEmpty count preserved without fuel inference");
        Number(summary["generatorWatts"], 1070, "legacy ratings sum preserved");
        Number(summary["generatorStoredJ"], 126, "legacy generator J preserved");
        Number(summary["consumerBaseWatts"], 330, "legacy consumer base W preserved");
        Number(summary["consumerNeededWatts"], 366, "legacy active demand W preserved");
        Number(summary["consumerUsedWatts"], 23, "native consumer used W inputs preserved");
        Number(summary["netRatedWatts"], 740, "legacy rated difference unchanged");
        Number(summary["netActiveWatts"], 704, "legacy active demand difference unchanged");
        Number(summary["batteryCount"], 1, "battery count unchanged");
        Number(summary["batteryCapacityJ"], 1000, "battery capacity unchanged");
        Number(summary["batteryStoredJ"], 250, "battery energy unchanged");
        Number(summary["batteryStoredPercent"], 25, "battery percentage unchanged");
        Number(summary["batteryWattsUsed"], 3, "battery used W unchanged");
        Number(main["netActiveWatts"], 670, "per-circuit existing W formula unchanged");
        Number(main["batteryStoredJ"], 250, "per-circuit battery J unchanged");
        Number(detailed["diagnostics"]["issueCount"], 1, "unconnected consumer diagnostic retained");
        Check((string)detailed["diagnostics"]["items"][0]["reasonCode"] == "consumer_unconnected", "existing diagnostic reason retained");
        Check(detailed["diagnostics"]["items"][0]["ports"]?.Type == JTokenType.Null, "actual diagnostic null dictionary value remains JSON null");
        Check(observed.Where(op => op != outsideG.gameObject.GetComponent<Operational>() && op != outsideC.gameObject.GetComponent<Operational>()).All(op => op.Reads == 1), "one existing Operational read per selected device; no added getter");
        Check(outsideG.gameObject.GetComponent<Operational>().Reads == 0 && outsideC.gameObject.GetComponent<Operational>().Reads == 0, "world selection precedes operational read");
        JObject compact = Read(JObject.Parse("{worldId:0}"));
        Check(compact["generators"] == null && compact["consumers"] == null && compact["batteries"] == null, "details remain opt-in");
        Check(JToken.DeepEquals(WithoutDetails(detailed), compact), "default detail omission preserves complete aggregate and diagnostics");
        JObject limited = Read(JObject.Parse("{worldId:0,includeDetails:true,limit:1}"));
        Check(limited["generators"].Count() == 1 && limited["consumers"].Count() == 1 && limited["batteries"].Count() == 1, "each detail collection honors existing limit");
        Check(JToken.DeepEquals(WithoutDetails(detailed), WithoutDetails(limited)), "detail limit does not truncate aggregate or circuit counts");
        JObject repeat = Read(JObject.Parse("{worldId:0,includeDetails:true,limit:80}"));
        Check(JToken.DeepEquals(detailed, repeat), "repeat returns same values without product simulation");
        Check(generatorRefs.SequenceEqual(Components.Generators.Items) && consumerRefs.SequenceEqual(Components.EnergyConsumers.Items), "reader does not change registry membership or order");
        JObject all = Read(JObject.Parse("{worldId:-1}"));
        Number(all["summary"]["generatorCount"], 5, "explicit all-world input includes separate world generator");
        Number(all["summary"]["consumerCount"], 5, "explicit all-world input includes separate world consumer");
        Number(all["summary"]["generatorOperational"], 2, "all-world operational total remains correct");
        Number(all["summary"]["consumerOperational"], 2, "all-world consumer operational total remains correct");
        Number(Circuit(all, "2")["generatorOperational"], 2, "all-world same-circuit generator count");
        Number(Circuit(all, "2")["consumerOperational"], 2, "all-world same-circuit consumer count");
    }
    private static void EmptyAndUnavailable()
    {
        Reset(); JObject empty = Read(new JObject());
        Check(!empty["circuits"].Any(), "empty native registries preserve empty circuit list");
        foreach (string key in new[] { "generatorCount", "generatorOperational", "consumerCount", "consumerOperational", "batteryCount" })
            Number(empty["summary"][key], 0, "observed empty count remains zero: " + key);
        Game.Instance = null;
        CallToolResult result = PowerAndRoomTools.GetPowerSummary().Handler(new JObject());
        Check(result.IsError && result.Content[0].Text == "Game not initialized", "existing game-unavailable error unchanged");
    }
}
