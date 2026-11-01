using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UnityEngine { public struct Vector3 {public float x,y,z;} }
public enum Orientation {Neutral,R90,R180,R270}

public sealed class Game { public static Game Instance = new Game(); }
public sealed class BuildingDef { public string Name = "Fixture building", PrefabID = "FixtureBuilding"; }
internal static class Fixture
{
    internal static Dictionary<string, int> Rect;
    internal static AreaHandle Area;
    internal static int World = 7, Calls;
    internal static Func<int, int, Dictionary<string, object>> Preview;internal static JObject LastArgs;internal static int ContextCreations;internal static object LastContext;internal static bool RequireContext=true;
}
internal sealed class AreaHandle { internal string Id; internal int X1, Y1; }

namespace OniMcp.Tools
{


    internal static class BuildingOperationGuidance { internal const string Placement = "fixture placement guide";internal static object MapPlacement(params object[] values){return null;} }
    internal static class ToolUtil
    {
        internal static Dictionary<string, int> GetRect(JObject args) { return Fixture.Rect; }
        internal static int ResolveWorldId(JObject args) { return (int?)args["worldId"] ?? Fixture.World; }
        internal static int? GetInt(JObject args, string key) { return (int?)args[key]; }
        internal static bool GetBool(JObject args, string key, bool fallback) { return (bool?)args[key] ?? fallback; }
        internal static string CleanName(string value) { return value; }
    }
    internal static class WorldEditor
    {
        internal static AreaHandle ResolveRelativeArea(JObject args)
        { return Fixture.Area != null && (string)args["areaId"] == Fixture.Area.Id ? Fixture.Area : null; }
        internal static void AddRelativeInfo(Dictionary<string, object> row, JObject args, int x, int y)
        { throw new NotSupportedException("unused generic relative projection collaborator"); }
    }
    public static partial class BuildPlanningTools
    {
        private static BuildingDef ResolveBuildingDef(string id, out string resolved, out string error)
        { resolved = "FixtureBuilding"; error = null; return new BuildingDef(); }
        private sealed class PlacementPreviewReadContext {internal PlacementPreviewReadContext(BuildingDef def,JObject args,int world){Fixture.ContextCreations++;}}
        private static Dictionary<string,object> TryPlanOne(string id,int x,int y,JObject args,PlacementPreviewReadContext previewReads=null)
        {return PlanOneWithoutLayerContext(id,x,y,args,previewReads);}
        private static Dictionary<string, object> PlanOneWithoutLayerContext(string id, int x, int y, JObject args,PlacementPreviewReadContext previewReads=null)
        {
            if (args["dryRun"]?.Type != JTokenType.Boolean || !(bool)args["dryRun"])
                throw new InvalidOperationException("Fixture forbids mutating candidate planning");
            if(previewReads==null&&Fixture.RequireContext)throw new InvalidOperationException("candidate did not forward a request read context");
            Fixture.LastContext=previewReads;Fixture.LastArgs=(JObject)args.DeepClone();Fixture.Calls++; return Fixture.Preview(x, y);
        }
    }
}
