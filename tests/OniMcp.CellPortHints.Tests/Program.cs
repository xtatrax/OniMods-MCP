using System;
using System.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static Building building;
    private static GameObject target;
    private const string Missing = "cell.decision.ports.missing";
    private const string Connected = "cell.decision.ports.connected";
    private const string Optional = "cell.decision.ports.optional";
    private const string Unknown = "cell.decision.ports.unknown";

    private static void Main()
    {
        TestLogicRequirements();
        TestMixedUtilities();
        TestBridgeActualCells();
        TestEmptyAndInvalidTargets();
        TestDisplayLimit();
        Console.WriteLine("Cell port decision adapter: " + checks + " host checks passed (Unity/game and localization formatting boundaries substituted)");
    }

    private static void Setup()
    {
        target = new GameObject();
        building = new Building();
        target.Add(building);
        WorldEditorTools.FixtureBuilding = target;
        WorldEditorTools.FixtureLayers.Clear();
        LogicPortReadSemantics.ConnectedCells.Clear();
    }

    private static LogicPorts.Port Port(int cell, bool required, int id = 7)
    { return new LogicPorts.Port { ActualCell = cell, requiresConnection = required, id = id }; }

    private static LogicPorts Logic(LogicPorts.Port[] input = null, LogicPorts.Port[] output = null)
    {
        var logic = new LogicPorts { inputPortInfo = input, outputPortInfo = output };
        target.Add(logic);
        return logic;
    }

    private static string Render()
    {
        bool wrote;
        var text = WorldEditorTools.RenderFixture(out wrote);
        Check(wrote && text.Length > 0, "a reported port produces a hint");
        return text;
    }

    private static void OnlyCategory(string text, string expected, string port)
    {
        Check(text.Contains(expected + ":") && text.Contains(port), expected + " contains " + port);
        foreach (string other in new[] { Missing, Connected, Optional, Unknown }.Where(value => value != expected))
            Check(!text.Contains(other + ":"), "no spurious " + other + " classification");
    }

    private static void TestLogicRequirements()
    {
        Setup(); Logic(new[] { Port(110, false) });
        OnlyCategory(Render(), Optional, "logicIn@(10,1)");

        Setup(); Logic(new[] { Port(110, true) });
        OnlyCategory(Render(), Missing, "logicIn@(10,1)");

        Setup(); Logic(output: new[] { Port(211, false) });
        OnlyCategory(Render(), Optional, "logicOut@(11,2)");

        Setup(); Logic(output: new[] { Port(211, true) });
        OnlyCategory(Render(), Missing, "logicOut@(11,2)");

        foreach (bool required in new[] { false, true })
        {
            Setup(); Logic(new[] { Port(110, required) }, new[] { Port(211, required) });
            LogicPortReadSemantics.ConnectedCells.Add(110);
            LogicPortReadSemantics.ConnectedCells.Add(211);
            string text = Render();
            OnlyCategory(text, Connected, "logicIn@(10,1)");
            Check(text.Contains("logicOut@(11,2)"), "connected output stays connected irrespective of requirement flag");
        }
    }

    private static void TestMixedUtilities()
    {
        Setup(); building.Def.RequiresPowerInput = true; building.PowerInputCell = 305;
        Logic(new[] { Port(306, false) });
        string text = Render();
        Check(Line(text, Missing).Contains("powerIn@(5,3)"), "required power input remains missing");
        Check(!Line(text, Missing).Contains("logicIn"), "optional logic input excluded from missing lines");
        Check(Line(text, Optional).Contains("logicIn@(6,3)"), "mixed optional logic remains visible");

        Setup(); building.Def.RequiresPowerOutput = true; building.PowerOutputCell = 308;
        WorldEditorTools.FixtureLayers.Add("308:Power");
        target.Add(new ConduitConsumer { ConduitType = ConduitType.Gas });
        building.UtilityInputCell = 309;
        target.Add(new ConduitDispenser { ConduitType = ConduitType.Liquid });
        building.UtilityOutputCell = 310;
        text = Render();
        Check(Line(text, Connected).Contains("powerOut@(8,3)"), "power layer read remains authoritative");
        Check(Line(text, Missing).Contains("gasIn@(9,3)") && Line(text, Missing).Contains("liquidOut@(10,3)"), "ordinary pipe ports remain required");

        Setup(); building.UtilityInputCell = 311; building.UtilityOutputCell = 312;
        target.Add(new SolidConduitConsumer { IsConnected = true });
        target.Add(new SolidConduitDispenser { IsConnected = false });
        text = Render();
        Check(Line(text, Connected).Contains("railIn@(11,3)"), "rail consumer uses native connection override");
        Check(Line(text, Missing).Contains("railOut@(12,3)"), "rail dispenser retains native missing status");
    }

    private static void TestBridgeActualCells()
    {
        // Native bridge endpoints can be sourced from either array and reuse an ID.
        // The endpoint actual cell, already transformed by the native reader, binds
        // the requirement; array role and ID cannot substitute for that match.
        Setup(); target.Add(new BridgeFixture { From = 520, To = 620 });
        Logic(new[] { Port(620, false, 7), Port(520, true, 7) });
        string text = Render();
        Check(Line(text, Missing).Contains("logicIn@(20,5)"), "bridge input requirement matches actual cell in reordered input array");
        Check(Line(text, Optional).Contains("logicOut@(20,6)"), "bridge output can match optional native entry in input array");

        Setup(); target.Add(new BridgeFixture { From = 621, To = 521 });
        Logic(output: new[] { Port(521, true, 7), Port(621, false, 7) });
        text = Render();
        Check(Line(text, Optional).Contains("logicIn@(21,6)"), "rotated bridge input looks across output definitions by transformed cell");
        Check(Line(text, Missing).Contains("logicOut@(21,5)"), "rotated bridge output retains its own native flag");

        Setup(); target.Add(new BridgeFixture { From = 522, To = 622 });
        Logic(new[] { Port(522, false) });
        text = Render();
        Check(Line(text, Optional).Contains("logicIn@(22,5)"), "known bridge endpoint stays optional");
        Check(Line(text, Unknown).Contains("logicOut@(22,6)"), "bridge endpoint without matching native definition is unknown");
        Check(!text.Contains(Missing + ":"), "unknown bridge requirement never becomes required by default");

        Setup(); target.Add(new BridgeFixture { From = 523, To = 623 });
        Logic(); LogicPortReadSemantics.ConnectedCells.Add(523);
        text = Render();
        Check(Line(text, Connected).Contains("logicIn@(23,5)"), "connected bridge endpoint is connected even if requirement is unavailable");
        Check(!Line(text, Unknown).Contains("logicIn"), "connected endpoint is not duplicated as unknown");

        Setup(); target.Add(new BridgeFixture { From = 524, To = 624 });
        Logic(new[] { Port(524, false), Port(624, false) }, new[] { Port(524, true) });
        text = Render();
        Check(Line(text, Missing).Contains("logicIn@(24,5)"), "a required co-located output definition overrides earlier optional input definition");
        Check(!Line(text, Optional).Contains("logicIn"), "duplicate definitions cannot downgrade required bridge endpoint");
        Check(Line(text, Optional).Contains("logicOut@(24,6)"), "all-optional bridge endpoint remains optional");
    }

    private static void TestEmptyAndInvalidTargets()
    {
        Setup(); ExpectNoHint("building without ports");
        Setup(); Logic(new LogicPorts.Port[0], null); ExpectNoHint("empty native port arrays");
        Setup(); Logic(new[] { Port(Grid.InvalidCell, false), Port(10000, true) });
        ExpectNoHint("invalid native actual cells");
        Setup(); WorldEditorTools.FixtureBuilding = null; ExpectNoHint("missing building object");
        Setup(); WorldEditorTools.FixtureBuilding = new GameObject(); ExpectNoHint("missing native Building component");
        Setup(); building.Def = null; ExpectNoHint("missing Building definition");
    }

    private static void TestDisplayLimit()
    {
        Setup(); Logic(Enumerable.Range(0, 8).Select(i => Port(700 + i, false)).ToArray());
        string text = Render();
        for (int i = 0; i < 6; i++) Check(text.Contains("logicIn@(" + i + ",7)"), "first six optional port hints retained: " + i);
        Check(!text.Contains("logicIn@(6,7)") && !text.Contains("logicIn@(7,7)"), "optional hints preserve six-entry output cap");
    }

    private static void ExpectNoHint(string label)
    {
        bool wrote;
        string text = WorldEditorTools.RenderFixture(out wrote);
        Check(!wrote && text.Length == 0, label + " produces no hint");
    }

    private static string Line(string text, string key)
    { return text.Split('\n').FirstOrDefault(line => line.StartsWith(key + ":", StringComparison.Ordinal)) ?? ""; }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
}
