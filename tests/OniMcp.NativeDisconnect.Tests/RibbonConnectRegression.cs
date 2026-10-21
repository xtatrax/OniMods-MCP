using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using OniMcp.Support;
using UnityEngine;

internal static class RibbonConnectRegression
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static JObject Request(string direction = "right") => new JObject
        { ["x"] = 2, ["y"] = 2, ["type"] = "logic", ["direction"] = direction, ["confirm"] = true };
    private static JObject Data(OniMcp.Core.CallToolResult result) => JObject.Parse(result.TextValue);
    private static ConnectFixture Fixture(string direction = "right", string id = LogicRibbonConfig.ID)
        => new ConnectFixture("logic", direction, id);
    private static void Reject(ConnectFixture fixture, string reason)
    {
        var result = BuildPlanningTools.ConnectForTest(Request());
        Check(result.IsError && result.TextValue.Contains(McpLocalization.Get("connect_existing." + reason)), reason);
        Check(fixture.Tool.Events.Count == 0 && fixture.Manager.Bits[fixture.A.Cell] == fixture.BeforeA
            && fixture.Manager.Bits[fixture.B.Cell] == fixture.BeforeB, "rejected ribbon request is read-only before native UI");
    }
    internal static void Run()
    {
        Check(!BuildPlanningTools.ExistingLinearClassifierForTest(LogicRibbonConfig.ID), "shared classifier must remain unchanged for ribbon");
        foreach (string direction in new[] { "right", "left", "up", "down" })
        {
            var f = Fixture(direction); var a = f.A.gameObject; var b = f.B.gameObject;
            f.Tool.OnRelease = () => { f.Manager.Bits[f.A.Cell] |= f.AddA; f.Manager.Bits[f.B.Cell] |= f.AddB; };
            var result = BuildPlanningTools.ConnectForTest(Request(direction)); var report = result.IsError ? null : Data(result);
            Check(!result.IsError && (bool)report["connectionVerified"], "exact native LogicRibbon must reach the existing connection gesture: " + direction);
            Check((string)report["prefabId"] == LogicRibbonConfig.ID && (bool)report["actionAttempted"]
                && !(bool)report["constructionQueued"] && !(bool)report["flowVerified"], "ribbon result separates connection from new construction and signal proof");
            Check(f.Tool.Events.SequenceEqual(new[] { "activate", "down", "drag", "up", "deactivate" })
                && f.Tool.Material == f.Material && f.Tool.First == f.A.Cell && f.Tool.Second == f.B.Cell,
                "single native gesture uses the original material and exact endpoints");
            Check(Grid.Objects[f.A.Cell, (int)f.Def.ObjectLayer] == a && Grid.Objects[f.B.Cell, (int)f.Def.ObjectLayer] == b
                && a.GetComponent<KPrefabID>().InstanceID == f.A.Cell && b.GetComponent<KPrefabID>().InstanceID == f.B.Cell
                && a.GetComponent<PrimaryElement>().Element.tag == f.Material && b.GetComponent<PrimaryElement>().Element.tag == f.Material,
                "ribbon identity and material remain unchanged");
            Check(Grid.Objects[f.A.Cell, (int)f.Def.ReplacementLayer] == null && Grid.Objects[f.B.Cell, (int)f.Def.ReplacementLayer] == null
                && f.OtherManager.Bits[f.A.Cell] == UtilityConnections.Down, "no replacement reservation or colocated other-layer change");
            int count = f.Tool.Events.Count; report = Data(BuildPlanningTools.ConnectForTest(Request(direction)));
            Check((bool)report["alreadyConnected"] && !(bool)report["actionAttempted"] && f.Tool.Events.Count == count,
                "repeated ribbon request performs no additional gesture");
        }
        var fixture = Fixture(); var preview = Request(); preview["dryRun"] = true; preview.Remove("confirm");
        var data = Data(BuildPlanningTools.ConnectForTest(preview));
        Check((bool)data["success"] && data["connectionVerified"].Type == JTokenType.Null && fixture.Tool.Events.Count == 0,
            "ribbon preview remains read-only and does not validate native acceptance");
        foreach (string id in new[] { "LogicRibbonBridge", "TravelTube", "LogicRibbonOther", "logicribbon", "LogicRibbon " })
            Reject(Fixture(id: id), "target_required");
        fixture = Fixture(); fixture.B.GetComponent<PrimaryElement>().Element.tag = new Tag(2); Reject(fixture, "same_required");
        fixture = Fixture(); fixture.B.GetComponent<BuildingComplete>().Def = new BuildingDef
        {
            PrefabID = "LogicWire", ObjectLayer = fixture.Def.ObjectLayer, TileLayer = fixture.Def.TileLayer,
            ReplacementLayer = fixture.Def.ReplacementLayer, BuildingComplete = fixture.Def.BuildingComplete
        };
        Reject(fixture, "same_required");
        foreach (bool first in new[] { true, false })
        {
            fixture = Fixture(); Grid.Objects[first ? fixture.A.Cell : fixture.B.Cell, (int)fixture.Def.ReplacementLayer] = new GameObject();
            Reject(fixture, "target_required");
        }
        fixture = Fixture(); var request = Request(); request["type"] = "wire";
        var wrongLayer = BuildPlanningTools.ConnectForTest(request);
        Check(wrongLayer.IsError && fixture.Tool.Events.Count == 0, "ribbon cannot be selected through another utility layer");
        fixture = Fixture(); data = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)data["success"] && !(bool)data["connectionVerified"] && (bool)data["partial"],
            "native ribbon no-op cannot be called connected");
        fixture = Fixture(); var captured = fixture;
        fixture.Tool.OnRelease = () => { captured.Manager.Bits[captured.A.Cell] |= captured.AddA; throw new Exception("fixture partial ribbon action"); };
        data = Data(BuildPlanningTools.ConnectForTest(Request()));
        Check(!(bool)data["success"] && (bool)data["partial"] && !(bool)data["connectionVerified"]
            && fixture.Manager.Bits[fixture.B.Cell] == fixture.BeforeB, "partial ribbon native action has no reciprocal repair fallback");
        Console.WriteLine($"Native ribbon production-boundary tests passed: {checks} checks");
    }
}
public static class LogicRibbonConfig { public const string ID = "LogicRibbon"; }
