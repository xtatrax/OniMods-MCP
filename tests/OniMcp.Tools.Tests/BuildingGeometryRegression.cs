using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class BuildingGeometryRegression
{
    internal static void Run()
    {
        var observed = new BuildingGeometrySnapshot
        {
            Id = 7, PrefabId = "Generator", Scope = "completed_instance",
            CoordinateSpace = "world_cells", Orientation = "Neutral",
            Origin = new BuildingGeometryCell { X = 100, Y = 211, Cell = 1000 },
            Source = "native_fixture", Cells = new System.Collections.Generic.List<BuildingGeometryCell>
            {
                new BuildingGeometryCell { X = 99, Y = 211, Cell = 999 },
                new BuildingGeometryCell { X = 100, Y = 211, Cell = 1000 },
                new BuildingGeometryCell { X = 101, Y = 211, Cell = 1001 }
            }
        };
        observed.Ports.Add(new BuildingGeometryPort
        {
            Kind = "power", Direction = "output", Position = observed.Cells[1],
            Offset = new[] { 0, 0 }, Source = "native_fixture"
        });
        JObject small = JObject.Parse(JsonConvert.SerializeObject(observed.ToDictionary(true), McpJsonUtil.Settings));
        JObject full = JObject.Parse(JsonConvert.SerializeObject(observed.ToDictionary(false), McpJsonUtil.Settings));
        Check((int)small["origin"][0] == 100 && (int)small["bottomLeft"][0] == 99,
            "odd-width native origin is distinct from actual bottom-left");
        foreach (string key in new[] { "bottomLeft", "bounds", "size", "occupiedCount", "fillsBounds", "available", "source", "errors", "portsComplete" })
            Check(JToken.DeepEquals(small[key], full[key]), "both profiles agree: " + key);
        Check(small.ToString(Formatting.None).Length < full.ToString(Formatting.None).Length,
            "compact response reduces serialized context size");
        Check(small["occupiedCells"] == null && full["occupiedCells"].Count() == 3,
            "only full emits every occupied cell");
        Check((int)full["ports"][0]["position"]["x"] == (int)small["ports"][0]["position"][0],
            "port coordinates agree between profiles");
        Check(small["ports"][0]["source"] == null && full["ports"][0]["source"] != null,
            "full retains port acquisition evidence");
        observed.Cells.RemoveAt(1);
        small = JObject.Parse(JsonConvert.SerializeObject(observed.ToDictionary(true), McpJsonUtil.Settings));
        Check(!(bool)small["fillsBounds"] && (int)small["occupiedCount"] == 2,
            "sparse footprint is not falsely represented as a filled rectangle");
        observed.Cells = new System.Collections.Generic.List<BuildingGeometryCell>
        {
            new BuildingGeometryCell { X = 100, Y = 211 },
            new BuildingGeometryCell { X = 100, Y = 212 },
            new BuildingGeometryCell { X = 100, Y = 213 }
        };
        observed.Orientation = "R90";
        small = JObject.Parse(JsonConvert.SerializeObject(observed.ToDictionary(true), McpJsonUtil.Settings));
        Check((int)small["size"][0] == 1 && (int)small["size"][1] == 3,
            "size follows observed rotated cells, not definition dimensions");
        observed.Cells = null;
        observed.PortsComplete = false;
        observed.Errors.Add("geometry_read_failed");
        small = JObject.Parse(JsonConvert.SerializeObject(observed.ToDictionary(true), McpJsonUtil.Settings));
        Check(!(bool)small["available"] && small["occupiedCount"].Type == JTokenType.Null
            && small["bounds"].Type == JTokenType.Null && !(bool)small["portsComplete"],
            "missing observations remain unknown rather than empty/zero");
        observed.Cells = new System.Collections.Generic.List<BuildingGeometryCell>
        { new BuildingGeometryCell { X = -1, Y = -1, Valid = false } };
        full = JObject.Parse(JsonConvert.SerializeObject(observed.ToDictionary(false), McpJsonUtil.Settings));
        Check(!(bool)full["available"] && !(bool)full["occupiedCells"][0]["valid"],
            "invalid native cells do not establish a valid footprint");
        Console.WriteLine("OK: building geometry profiles preserve native origin, sparse/rotated cells and missing evidence");
    }

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("Building geometry: " + message);
    }
}
