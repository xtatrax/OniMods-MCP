using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// Only the child call and unrelated native/path collaborators are substituted.
// The full production TryAutoConnectPower method and result projector are linked.
public enum Orientation { Neutral, R90, R180, R270 }
public struct CellOffset { public int x, y; public CellOffset(int a, int b) { x=a; y=b; } }
public static class Rotatable
{
    public static CellOffset GetRotatedCellOffset(CellOffset value, Orientation orientation)
        => value;
}
namespace OniMcp.Tools
{
    internal static partial class ToolUtil
    {
        internal static bool GetBool(JObject args, string key, bool fallback = false)
            => args?[key]?.Type == JTokenType.Boolean ? args[key].Value<bool>() : fallback;
        internal static int ResolveWorldId(JObject args) => GetInt(args, "worldId") ?? 0;
        internal static bool CellMatchesWorld(int cell, int world) => world == 0;
    }
    public static partial class BuildPlanningTools
    {
        private sealed class AutoDigContext { }
        internal static CallToolResult AutoPowerChildResult;
        internal static JObject AutoPowerChildArgs;
        internal static int AutoPowerChildCalls;
        internal static int AutoPowerWouldMutate;
        internal static bool AutoPowerSourceAvailable = true;
        internal static BuildingDef AutoPowerWire = new BuildingDef { PrefabID = "Wire" };
        internal static Dictionary<string, object> AutoPowerTerrain;
        internal static int[] AutoPowerTerrainCells;
        internal static bool AutoPowerTerrainConsent;
        internal static int AutoPowerTerrainBudget;
        internal static bool AutoPowerTerrainAutoDig;

        internal static Dictionary<string, object> AutoPowerForTest(BuildingDef def, JObject args)
            => TryAutoConnectPower(def, 4, 2, Orientation.Neutral, args, new HashSet<int>(), new AutoDigContext());

        internal sealed class AutoPowerChildTool
        {
            internal CallToolResult Handler(JObject args)
            {
                AutoPowerChildCalls++;
                AutoPowerChildArgs = (JObject)args.DeepClone();
                if (!IsDryRun(args)) AutoPowerWouldMutate++;
                return AutoPowerChildResult;
            }
        }
        private static AutoPowerChildTool AutoConnectUtility() => new AutoPowerChildTool();
        private static bool IsDryRun(JObject args) => ToolUtil.GetBool(args, "dryRun") || ToolUtil.GetBool(args, "validateOnly");
        private static bool TryFindNearestWireOrPowerOutput(int input, int world, int radius, out int source)
        { source=Grid.XYToCell(2,2);return AutoPowerSourceAvailable; }
        private static CellCoord CellCoordFromCell(int cell) => new CellCoord(Grid.CellColumn(cell), Grid.CellRow(cell));
        private static bool AddManhattanSegment(List<CellCoord> path, CellCoord from, CellCoord to, int max, out string error)
        {
            // Fixed fixture route: generation/interpolation rules are covered elsewhere.
            path.AddRange(new[] { new CellCoord(2,2), new CellCoord(3,2), new CellCoord(4,2) });
            error = max < path.Count ? "fixture path limit" : null;
            return error == null;
        }
        private static BuildingDef ResolveBuildingDef(string name, out string id, out string error)
        { id="Wire";error=AutoPowerWire==null?"fixture missing wire":null;return AutoPowerWire; }
        private static Dictionary<string, object> UtilityTerrainConflict(BuildingDef def,IEnumerable<int> cells,
            int world,bool consent=false,int budget=100,bool autoDig=true)
        {
            AutoPowerTerrainCells=cells.ToArray();AutoPowerTerrainConsent=consent;
            AutoPowerTerrainBudget=budget;AutoPowerTerrainAutoDig=autoDig;return AutoPowerTerrain;
        }
        private static bool GetBool(Dictionary<string, object> value,string key)
            => value != null && value.TryGetValue(key,out object raw) && raw is bool flag && flag;
        private static int GetInt(Dictionary<string, object> value,string key)
            => value!=null && value.TryGetValue(key,out object raw) && int.TryParse(raw?.ToString(),out int count)?count:0;
        private static Dictionary<string, object> ParseToolJsonPayload(CallToolResult result)
        {
            try { return JsonConvert.DeserializeObject<Dictionary<string,object>>(result?.Content?.FirstOrDefault()?.Text ?? ""); }
            catch { return null; }
        }
        private static List<Dictionary<string, object>> BuildPathSegments(List<CellCoord> path)
            => new List<Dictionary<string, object>> { new Dictionary<string, object> { ["fixtureSegmentCells"] = path.Count } };
    }
}
