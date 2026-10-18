using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Tools;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string description)
    {
        assertions++;
        if (!condition) throw new Exception(description);
    }
    private static EntombedItemVisualizer Reset()
    {
        Array.Clear(Grid.Visible, 0, Grid.Visible.Length);
        Array.Clear(Grid.Solid, 0, Grid.Solid.Length);
        Array.Clear(Grid.WorldIdx, 0, Grid.WorldIdx.Length);
        for (int cell = 0; cell < Grid.WorldValid.Length; cell++) Grid.WorldValid[cell] = true;
        ClusterManager.Instance = new ClusterManager { activeWorldId = 0 };
        var visualizer = new EntombedItemVisualizer();
        Game.Instance = new Game { Visualizer = visualizer };
        return visualizer;
    }
    private static void ExpectMarker(int cell, string state, int nativeReads, string description)
    {
        var visualizer = Game.Instance == null ? null : Game.Instance.Visualizer;
        int beforeQueries = visualizer == null ? 0 : visualizer.Queries.Count;
        int beforeComponents = Game.Instance == null ? 0 : Game.Instance.ComponentReads;
        Check(WorldEditorTools.MarkerTest(cell) == state, description + ": observed state");
        if (visualizer != null)
            Check(visualizer.Queries.Count - beforeQueries == nativeReads, description + ": native marker read count");
        if (nativeReads == 0 && state != "unavailable" && Game.Instance != null)
            Check(Game.Instance.ComponentReads == beforeComponents, description + ": no visualizer acquisition");
    }
    private static void Main()
    {
        var visualizer = Reset();
        Grid.Visible[1] = 255; Grid.Solid[1] = true; visualizer.Cells.Add(1);
        ExpectMarker(1, "present", 1, "visible marker in active world");
        visualizer.Cells.Remove(1);
        ExpectMarker(1, "absent", 1, "visible native absence");
        Grid.Solid[1] = false;
        ExpectMarker(1, "not_solid", 0, "visible open terrain");
        Grid.Solid[1] = true; visualizer.Cells.Add(1);
        Grid.Visible[1] = 0;
        ExpectMarker(1, "unobserved", 0, "hidden active-world cell");
        Grid.Visible[1] = 255; ClusterManager.Instance.activeWorldId = 7;
        ExpectMarker(1, "unobserved", 0, "visible world-zero marker while world seven is active");
        ClusterManager.Instance.activeWorldId = 0; Grid.WorldIdx[1] = 7;
        ExpectMarker(1, "unobserved", 0, "visible world-seven marker while world zero is active");
        Grid.WorldIdx[1] = 0; Grid.WorldValid[1] = false;
        ExpectMarker(1, "unavailable", 0, "allocated but invalid world cell");
        Check(Game.Instance.ComponentReads == 2, "invalid world did not acquire the visualizer");
        ExpectMarker(-1, "unavailable", 0, "negative invalid cell");
        ExpectMarker(Grid.Visible.Length, "unavailable", 0, "upper bound invalid cell");
        Check(Game.Instance.ComponentReads == 2, "invalid bounds did not acquire the visualizer");
        Grid.WorldValid[1] = true; ClusterManager.Instance = null;
        ExpectMarker(1, "unobserved", 0, "missing active-world owner");
        ClusterManager.Instance = new ClusterManager { activeWorldId = -1 };
        ExpectMarker(1, "unobserved", 0, "negative active-world id");
        ClusterManager.Instance.activeWorldId = 0;
        Grid.Visible[1] = 1;
        ExpectMarker(1, "present", 1, "native nonzero visibility remains readable");
        Game.Instance.Visualizer = null;
        ExpectMarker(1, "unavailable", 0, "unavailable marker component is not absence");
        Game.Instance = null;
        ExpectMarker(1, "unavailable", 0, "missing game is not absence");

        visualizer = Reset();
        for (int x = 0; x < 40; x++)
        {
            Grid.Visible[x] = 255; Grid.Solid[x] = true; visualizer.Cells.Add(x);
        }
        string text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("visible solid cells=40;"), "summary retains native marker total");
        Check(text.Contains("omitted=8."), "summary retains bounded sample remainder");
        Check(text.Contains("(0,0)") && text.Contains("(31,0)"), "summary includes bounded native sample");
        Check(!text.Contains("(32,0)"), "summary does not emit beyond sample limit");
        Check(visualizer.Queries.Count == 40, "summary inspects each readable solid once");

        visualizer.Queries.Clear(); int components = Game.Instance.ComponentReads;
        ClusterManager.Instance.activeWorldId = 7;
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("visible solid cells=0; cells=none; omitted=0."), "foreign-world summary excludes totals and coordinates");
        Check(!text.Contains("(0,0)") && !text.Contains("(31,0)"), "foreign-world samples never leak");
        Check(visualizer.Queries.Count == 0 && Game.Instance.ComponentReads == components, "foreign summary never acquires or calls native visualizer");

        Grid.WorldIdx[35] = 7;
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("visible solid cells=1; cells=(35,0); omitted=0."), "mixed-world summary retains only requested active world");
        Check(visualizer.Queries.SequenceEqual(new[] {35}), "mixed-world summary native queries stay inside world");
        Grid.Visible[35] = 0; visualizer.Queries.Clear();
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("cells=none;") && visualizer.Queries.Count == 0, "hidden marker does not become a sample or native query");
        Grid.Visible[35] = 255; Grid.WorldValid[35] = false;
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("cells=none;") && visualizer.Queries.Count == 0, "invalid-world marker does not become a sample or native query");

        Grid.WorldValid[35] = true; Grid.WorldIdx[35] = 0; ClusterManager.Instance.activeWorldId = 0;
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("visible solid cells=40;") && visualizer.Queries.Count == 40, "returning to original world restores visible markers without stale cache");
        Check(text.Contains("(0,0)") && text.Contains("omitted=8."), "returned world retains original bounded samples");

        byte[] visible = (byte[])Grid.Visible.Clone();
        bool[] solid = (bool[])Grid.Solid.Clone();
        int[] worlds = (int[])Grid.WorldIdx.Clone();
        bool[] valid = (bool[])Grid.WorldValid.Clone();
        var markers = new HashSet<int>(visualizer.Cells);
        WorldEditorTools.MarkerTest(1); WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(visible.SequenceEqual(Grid.Visible), "reads preserve visibility");
        Check(solid.SequenceEqual(Grid.Solid), "reads preserve terrain");
        Check(worlds.SequenceEqual(Grid.WorldIdx) && valid.SequenceEqual(Grid.WorldValid), "reads preserve world ownership");
        Check(markers.SetEquals(visualizer.Cells), "reads preserve native marker set");
        Check(ClusterManager.Instance.activeWorldId == 0, "reads preserve active world");
        Console.WriteLine("Buried marker adapter: " + assertions + " assertions passed");
    }
}
