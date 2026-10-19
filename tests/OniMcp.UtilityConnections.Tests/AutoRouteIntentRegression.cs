using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class AutoRouteIntentRegression
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(name);
    }

    private static JObject Rect() => new JObject { ["x1"] = 1, ["y1"] = 1, ["x2"] = 7, ["y2"] = 5 };

    internal static void Run()
    {
        var masks = new Dictionary<string, JToken>
        {
            ["single object"] = new JArray(new JObject { ["x"] = 3, ["y"] = 2 }),
            ["single pair"] = new JArray(new JArray(3, 2)),
            ["sparse"] = new JArray(new JArray(1, 2), new JArray(6, 2)),
            ["diagonal"] = new JArray(new JArray(2, 1), new JArray(5, 4)),
            ["reversed"] = new JArray(new JArray(6, 2), new JArray(1, 2)),
            ["unordered"] = new JArray(new JArray(5, 4), new JArray(1, 2), new JArray(3, 2)),
            ["adjacent"] = new JArray(new JArray(1, 2), new JArray(2, 2)),
            ["duplicate"] = new JArray(new JArray(1, 2), new JArray(1, 2)),
            ["empty"] = new JArray(),
            ["null"] = JValue.CreateNull(),
            ["string"] = new JValue("1,2"),
            ["object"] = new JObject { ["x"] = 1, ["y"] = 2 },
            ["integer"] = new JValue(1),
            ["boolean"] = new JValue(false)
        };
        var selectors = new Dictionary<string, JObject>
        {
            ["none"] = new JObject(),
            ["parent viewport"] = Rect(),
            ["points"] = new JObject { ["points"] = new JArray(new JArray(0, 0), new JArray(7, 5)) },
            ["anchor endpoints"] = new JObject { ["x"] = 0, ["y"] = 1, ["x2"] = 7, ["y2"] = 5 },
            ["queries"] = new JObject { ["query"] = "target", ["fromQuery"] = "source" },
            ["combined"] = new JObject { ["points"] = new JArray(new JArray(0, 0), new JArray(7, 5)),
                ["x"] = 1, ["y"] = 1, ["x1"] = 0, ["y1"] = 0, ["x2"] = 7, ["y2"] = 5,
                ["fromX"] = 1, ["fromY"] = 1, ["toX"] = 7, ["toY"] = 5,
                ["query"] = "target", ["areaId"] = "fixture", ["relative"] = true }
        };
        foreach (var mask in masks)
            foreach (var selector in selectors)
            {
                var args = (JObject)selector.Value.DeepClone();
                args["anchors"] = mask.Value.DeepClone();
                args["worldId"] = 2;
                args["confirm"] = true;
                args["material"] = "CopperOre";
                string before = args.ToString();
                bool route = BuildPlanningTools.AutoRouteForTest(args, "Wire", out JObject result);
                string scenario = mask.Key + "/" + selector.Key;
                Check(!route, "Explicit anchors must remain a placement set: " + scenario);
                Check(args.ToString() == before, "Input must remain unchanged: " + scenario);
                Check(!ReferenceEquals(result, args) && JToken.DeepEquals(result["anchors"], args["anchors"]), "Caller can validate the original anchor shape: " + scenario);
                Check(result["worldId"].Value<int>() == 2 && result["confirm"].Value<bool>() && result["material"].Value<string>() == "CopperOre",
                    "Unrelated request options survive extraction: " + scenario);
            }

        foreach (var points in new JToken[] { new JArray(new JArray(1, 2), new JArray(5, 2)), new JArray(), new JValue("invalid"), JValue.CreateNull() })
        {
            var args = Rect(); args["points"] = points.DeepClone();
            string before = args.ToString();
            Check(BuildPlanningTools.AutoRouteForTest(args, "Wire", out JObject result), "Without anchors, explicit points retain existing dispatch semantics");
            Check(JToken.DeepEquals(result["points"], points) && result["fromX"] == null, "Points precede rectangle conversion without validation or interpolation here");
            Check(args.ToString() == before, "Explicit path dispatch does not modify input");
        }
        var rectangle = Rect();
        Check(BuildPlanningTools.AutoRouteForTest(rectangle, "GasConduit", out JObject converted), "Explicit rectangle route stays available");
        Check(converted["fromX"].Value<int>() == 1 && converted["fromY"].Value<int>() == 1
            && converted["toX"].Value<int>() == 7 && converted["toY"].Value<int>() == 5, "Rectangle endpoints retain their old conversion");
        Check(converted["prefabId"].Value<string>() == "GasConduit" && converted["action"].Value<string>() == "auto_connect", "Route metadata stays canonical");
        rectangle["x"] = 3; rectangle["y"] = 2;
        Check(BuildPlanningTools.AutoRouteForTest(rectangle, "Wire", out converted), "Explicit anchor/end endpoints stay available");
        Check(converted["fromX"].Value<int>() == 3 && converted["fromY"].Value<int>() == 2
            && converted["toX"].Value<int>() == 7 && converted["toY"].Value<int>() == 5, "x/y still precedes x1/y1 when converting an explicit route");
        foreach (string key in new[] { "toQuery", "targetQuery", "fromQuery", "sourceQuery", "query", "target", "search", "name" })
        {
            var args = new JObject { [key] = "  fixture  " };
            Check(BuildPlanningTools.AutoRouteForTest(args, "Wire", out converted), "Query alias route remains available: " + key);
            Check(converted[key].Value<string>() == "  fixture  " && converted["points"] == null, "Query text is forwarded without early endpoint resolution: " + key);
        }
        foreach (JObject args in new[] { new JObject(), new JObject { ["query"] = "  ", ["name"] = "" },
            new JObject { ["x"] = 1, ["y"] = 2 }, new JObject { ["x1"] = 1, ["y1"] = 2, ["x2"] = 3 },
            new JObject { ["fromX"] = 1, ["fromY"] = 2, ["toX"] = 3, ["toY"] = 4 } })
            Check(!BuildPlanningTools.AutoRouteForTest(args, "Wire", out converted), "Unchanged incomplete/non-converting selector remains with placement resolver");
        Check(!BuildPlanningTools.AutoRouteForTest(null, "Wire", out converted), "Missing input is not a route");
        Check(converted["prefabId"].Value<string>() == "Wire", "Missing input still produces the canonical detached argument object");

        var original = new JObject { ["points"] = new JArray(new JArray(1, 2), new JArray(3, 4)) };
        Check(BuildPlanningTools.AutoRouteForTest(original, "Wire", out converted), "Explicit path clone test is routed");
        converted["points"][0][0] = 99;
        Check(original["points"][0][0].Value<int>() == 1, "Returned route arguments do not alias the caller's mutable path");
        Console.WriteLine("Auto-route production intent regression passed: " + checks + " checks");
    }
}

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        internal static bool AutoRouteForTest(JObject args, string prefabId, out JObject converted)
            => TryBuildAutoConnectArgs(args, prefabId, out converted);
    }

    internal static class ToolUtil
    {
        internal static int? GetInt(JObject args, string key)
            => int.TryParse(args?[key]?.ToString(), out int value) ? value : (int?)null;
    }
}
