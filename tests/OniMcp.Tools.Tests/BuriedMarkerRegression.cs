using System;
using System.Text;
using OniMcp.Tools;
internal static class BuriedMarkerRegression
{
    internal static void Run()
    {
        Grid.Visible = new byte[10000]; Grid.Solid = new bool[10000];
        var visualizer=new EntombedItemVisualizer(); Game.Instance=new Game { Visualizer=visualizer };
        visualizer.Cells.Add(1); Grid.Solid[1] = true;
        Check(WorldEditorTools.MarkerTest(1) == "unobserved", "hidden contents must not leak");
        Grid.Visible[1] = 255;
        Check(WorldEditorTools.MarkerTest(1) == "present", "visible solid marker is observed");
        Grid.Solid[1] = false;
        Check(WorldEditorTools.MarkerTest(1) == "not_solid", "open cell is not buried terrain");
        Grid.Solid[1] = true; visualizer.Cells.Remove(1);
        Check(WorldEditorTools.MarkerTest(1) == "absent", "zero is an absent marker");
        Game.Instance.Visualizer = null;
        Check(WorldEditorTools.MarkerTest(1) == "unavailable", "missing observation is not absence");
        Game.Instance = null;
        Check(WorldEditorTools.MarkerTest(1) == "unavailable", "missing game is unavailable");
        Check(WorldEditorTools.MarkerTest(-1) == "unavailable", "invalid cells are unavailable");
        Game.Instance=new Game { Visualizer=visualizer };
        for(int x=0;x<40;x++){Grid.Visible[x]=255;Grid.Solid[x]=true;visualizer.Cells.Add(x);}
        string summary=WorldEditorTools.MarkerSummaryTest(0,39,0,0);
        Check(summary.Contains("cells=40")&&summary.Contains("omitted=8")&&!summary.Contains("(32,0)"), "bounded samples retain total count");
        Grid.Visible[0]=0;
        Check(!WorldEditorTools.MarkerSummaryTest(0,0,0,0).Contains("(0,0)"), "hidden coordinates omitted");
        Console.WriteLine("Buried markers: visibility, terrain, unavailable data and bounded summaries passed");
    }
    private static void Check(bool value,string name){if(!value)throw new Exception(name);}
}
internal static partial class Grid
{
    internal static byte[] Visible;
    internal static bool[] Solid;

}
internal sealed partial class Game
{
    internal EntombedItemVisualizer Visualizer;
    internal T GetComponent<T>() where T:class=>Visualizer as T;
}
internal sealed class EntombedItemVisualizer
{
    internal readonly System.Collections.Generic.HashSet<int> Cells=new System.Collections.Generic.HashSet<int>();
    internal bool IsEntombedItem(int cell)=>Cells.Contains(cell);
}
namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static string MarkerTest(int cell)=>ReadBuriedMarker(cell);
        internal static string MarkerSummaryTest(int x1,int x2,int y1,int y2){var sb=new StringBuilder();AppendBuriedMarkerSummary(sb,x1,x2,y1,y2);return sb.ToString();}
    }
}
