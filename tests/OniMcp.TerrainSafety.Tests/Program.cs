using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Tools;

public class BuildingDef { public string PrefabID; }
public enum ObjectLayer { DigPlacer }
public static class Grid
{
    public static bool[] Solid = new bool[20], Foundation = new bool[20], Visible = Enumerable.Repeat(true,20).ToArray();
    public static object[,] Objects = new object[20,1];
    public static bool IsValidCell(int c) => c >= 0 && c < 20;
    public static bool IsVisible(int c) => Visible[c];
    public static int CellColumn(int c) => c % 5;
    public static int CellRow(int c) => c / 5;
}
public class EntombedItemVisualizer { public bool IsEntombedItem(int c) => c == 7; }
public class Game
{
    public static Game Instance;
    public T GetComponent<T>() where T : class => new EntombedItemVisualizer() as T;
}
namespace OniMcp.Tools
{
    public static class ToolUtil { public static bool CellMatchesWorld(int c, int w) => c / 10 == w; }
    public static partial class BuildPlanningTools
    {
        private static bool IsUtilityPrefab(string id) => new[]{"Wire","LiquidConduit","GasConduit","SolidConduit","LogicWire"}.Contains(id);
        public static Dictionary<string,object> Inspect(string id, int[] cells, int world=0, bool allow=false, int budget=100, bool autoDig=true)
            => UtilityTerrainConflict(new BuildingDef{PrefabID=id},cells,world,allow,budget,autoDig);
    }
}
class Program
{
    static int checks;
    static void Check(bool v,string why) { checks++; if(!v) throw new Exception(why); }
    static void Main()
    {
        foreach(var id in new[]{"Wire","LiquidConduit","GasConduit","SolidConduit","LogicWire"})
        {
            Check(BuildPlanningTools.Inspect(id,new[]{1,2,3})==null,"Open route must remain allowed");
            Grid.Solid[7]=true;
            var r=BuildPlanningTools.Inspect(id,new[]{1,7,2,7});
            Check((string)r["reasonCode"]=="utility_natural_terrain_blocked","Natural solid must block route");
            Check((int)r["blockedCellCount"]==1,"Duplicate cells must not duplicate hazards");
            Check(!(bool)r["committed"] && (int)r["planned"]==0,"Rejection must not claim placement");
            var cells=(List<Dictionary<string,object>>)r["blockedCells"];
            Check((string)cells[0]["buriedMarker"]=="unavailable","Missing visualizer is unknown, not absent");
            Game.Instance=new Game(); Grid.Objects[7,0]=new object();
            r=BuildPlanningTools.Inspect(id,new[]{7}); cells=(List<Dictionary<string,object>>)r["blockedCells"];
            Check((string)cells[0]["buriedMarker"]=="present" && (bool)cells[0]["alreadyMarkedForDig"],"Existing dig must still block until terrain clears");
            Check(Grid.Objects[7,0]!=null && Grid.Solid[7],"Inspection must not alter terrain or dig");
            Grid.Foundation[7]=true;
            Check(BuildPlanningTools.Inspect(id,new[]{7})==null,"Constructed tile must remain allowed");
            Grid.Foundation[7]=false; Grid.Visible[7]=false;
            Check(BuildPlanningTools.Inspect(id,new[]{-1,7,25})==null,"Inspection must not leak hidden cells; placement validation rejects them separately");
            Grid.Visible[7]=true;
            Check(BuildPlanningTools.Inspect(id,new[]{7},1)==null,"Do not expose foreign world terrain");
            Grid.Solid[7]=false; Game.Instance=null; Grid.Objects[7,0]=null;
        }
        Grid.Solid[7]=true;
        Check(BuildPlanningTools.Inspect("Bed",new[]{7})==null,"Ordinary building obstruction behavior remains separate");
        var allowed=BuildPlanningTools.Inspect("Wire",new[]{1,7,2},allow:true);
        Check((bool)allowed["allowed"] && (int)allowed["naturalCellCount"]==1,"Explicit consent exposes planned terrain impact");
        Check(!allowed.ContainsKey("committed"),"Preview must not claim terrain was changed");
        Check(!(bool)BuildPlanningTools.Inspect("Wire",new[]{7},allow:true,autoDig:false)["allowed"],"Explicit no-dig must win over terrain consent");
        Grid.Solid[8]=true;
        Check(!(bool)BuildPlanningTools.Inspect("Wire",new[]{7,8},allow:true,budget:1)["allowed"],"Reject whole route beyond dig budget");
        Check((bool)BuildPlanningTools.Inspect("Wire",new[]{7,8},allow:true,budget:2)["allowed"],"Exact budget is accepted");
        Console.WriteLine($"Utility terrain safety: {checks} checks passed");
    }
}
