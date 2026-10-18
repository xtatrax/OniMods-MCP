using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Tools;
using OniMcp.Support;

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
    private static void ExpectCoverage(string description, int x1, int x2, string availability, int? markerCount,
        int requested, int readable, int present, int absent, int notSolid, int unobserved, int unavailable)
    {
        var result = WorldEditorTools.MarkerCoverageTest(x1, x2, 0, 0);
        Check((string)result["availability"] == availability, description + ": availability");
        Check(markerCount.HasValue ? result["markerCount"] is int && (int)result["markerCount"] == markerCount.Value
            : result["markerCount"] == null, description + ": nullable area total");
        string[] keys = { "requested", "readable", "present", "absent", "not_solid", "unobserved", "unavailable" };
        int[] expected = { requested, readable, present, absent, notSolid, unobserved, unavailable };
        for (int i = 0; i < keys.Length; i++) Check((int)result[keys[i]] == expected[i], description + ": " + keys[i]);
        Check((int)result["present"] + (int)result["absent"] + (int)result["not_solid"]
            + (int)result["unobserved"] + (int)result["unavailable"] == (int)result["requested"], description + ": states partition requested cells");
    }
    private static void CoverageRegressions()
    {
        var visualizer = Reset();
        Grid.Visible[0] = 255; Grid.Solid[0] = true;
        ExpectCoverage("confirmed native zero", 0, 0, "observed", 0, 1, 1, 0, 1, 0, 0, 0);
        Check(visualizer.Queries.SequenceEqual(new[] { 0 }), "confirmed zero uses one native marker read");
        string text = WorldEditorTools.MarkerSummaryTest(0, 0, 0, 0);
        Check(text.Contains("availability=observed; markerCount=0; present=0;") && text.Contains("cells=none;"), "known absence explicitly differs from unknown total");
        visualizer = Reset(); Grid.Visible[0] = 255;
        ExpectCoverage("all open terrain", 0, 0, "not_applicable", null, 1, 1, 0, 0, 1, 0, 0);
        Check(visualizer.Queries.Count == 0 && Game.Instance.ComponentReads == 0, "open terrain requires no native marker source");
        text = WorldEditorTools.MarkerSummaryTest(0, 0, 0, 0);
        Check(text.Contains("availability=not_applicable; markerCount=null;") && text.Contains("cells=not_applicable;"), "open-only summary does not invent a native absence reading");
        visualizer = Reset(); Grid.Solid[0] = true; visualizer.Cells.Add(0);
        ExpectCoverage("all hidden", 0, 0, "unobserved", null, 1, 0, 0, 0, 0, 1, 0);
        Check(visualizer.Queries.Count == 0 && Game.Instance.ComponentReads == 0, "hidden marker never queries native source");
        text = WorldEditorTools.MarkerSummaryTest(0, 0, 0, 0);
        Check(text.Contains("markerCount=null;") && !text.Contains("(0,0)"), "hidden area does not reveal marker coordinate or zero total");
        Grid.Visible[0] = 255; Grid.WorldIdx[0] = 7;
        ExpectCoverage("foreign visible cell", 0, 0, "unobserved", null, 1, 0, 0, 0, 0, 1, 0);
        Check(visualizer.Queries.Count == 0 && Game.Instance.ComponentReads == 0, "foreign marker never queries native source");
        ClusterManager.Instance = null;
        ExpectCoverage("missing active world", 0, 0, "unobserved", null, 1, 0, 0, 0, 0, 1, 0);
        visualizer = Reset(); Grid.Visible[0] = 255; Grid.Solid[0] = true; Grid.WorldValid[0] = false;
        ExpectCoverage("invalid world cell", 0, 0, "unavailable", null, 1, 0, 0, 0, 0, 0, 1);
        ExpectCoverage("invalid grid bound", -1, -1, "unavailable", null, 1, 0, 0, 0, 0, 0, 1);
        Check(visualizer.Queries.Count == 0 && Game.Instance.ComponentReads == 0, "invalid bounds/world do not acquire source");
        visualizer = Reset(); Grid.Visible[0] = 255; Grid.Solid[0] = true;
        Game.Instance.Visualizer = null;
        ExpectCoverage("missing native source", 0, 0, "unavailable", null, 1, 1, 0, 0, 0, 0, 1);
        Game.Instance = null;
        ExpectCoverage("missing game", 0, 0, "unavailable", null, 1, 1, 0, 0, 0, 0, 1);
        visualizer = Reset(); Grid.Visible[0] = 255; Grid.Solid[0] = true; Game.Instance.ThrowComponent = true;
        ExpectCoverage("source acquisition exception", 0, 0, "unavailable", null, 1, 1, 0, 0, 0, 0, 1);
        Check(Game.Instance.ComponentReads == 1 && visualizer.Queries.Count == 0, "failed acquisition is not retried");
        visualizer = Reset(); Grid.Visible[0] = 255; Grid.Solid[0] = true; visualizer.ThrowCells.Add(0);
        ExpectCoverage("native read exception", 0, 0, "unavailable", null, 1, 1, 0, 0, 0, 0, 1);
        Check(Game.Instance.ComponentReads == 1 && visualizer.Queries.SequenceEqual(new[] { 0 }), "failed native read is not retried or counted absent");
        visualizer = Reset();
        for (int cell = 0; cell <= 6; cell++) { Grid.Visible[cell] = 255; Grid.Solid[cell] = true; }
        visualizer.Cells.UnionWith(new[] { 0, 3, 4, 5, 6 });
        Grid.Solid[2] = false; Grid.Visible[3] = 0; Grid.WorldIdx[4] = 7; Grid.WorldValid[5] = false; visualizer.ThrowCells.Add(6);
        ExpectCoverage("mixed coverage", 0, 6, "partial", null, 7, 4, 1, 1, 1, 2, 2);
        Check(visualizer.Queries.SequenceEqual(new[] { 0, 1, 6 }) && Game.Instance.ComponentReads == 3, "aggregate queries each eligible solid once, including failed read");
        var mixed = WorldEditorTools.MarkerCoverageTest(0, 6, 0, 0);
        Check(((string[])mixed["samples"]).SequenceEqual(new[] { "(0,0)" }) && (int)mixed["omitted"] == 0, "mixed samples include only observed present marker");
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            string selected = language; McpLocalization.LanguageProvider = () => selected;
            text = WorldEditorTools.MarkerSummaryTest(0, 6, 0, 0);
            Check(text.Contains("availability=partial; markerCount=null; present=1;")
                && text.Contains("requested=7; readable=4; absent=1; not_solid=1; unobserved=2; unavailable=2;"), "localized summary preserves compact machine coverage fields");
            Check(text.Contains("cells=(0,0); omitted=0") && !text.Contains("(3,0)") && !text.Contains("(4,0)")
                && !text.Contains("(5,0)") && !text.Contains("(6,0)"), "localized summary never leaks unknown/foreign/unavailable coordinates");
            Check(!text.Contains("buried_markers.summary"), "runtime summary locale key is actually loaded");
        }
        McpLocalization.LanguageProvider = () => "en";
        ExpectCoverage("empty requested rectangle", 1, 0, "not_applicable", null, 0, 0, 0, 0, 0, 0, 0);
    }
    private static void Main()
    {
        McpLocalization.LanguageProvider = () => "en";
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
        Check(text.Contains("markerCount=40; present=40;"), "summary retains native marker total");
        Check(text.Contains("omitted=8."), "summary retains bounded sample remainder");
        Check(text.Contains("(0,0)") && text.Contains("(31,0)"), "summary includes bounded native sample");
        Check(!text.Contains("(32,0)"), "summary does not emit beyond sample limit");
        Check(visualizer.Queries.Count == 40, "summary inspects each readable solid once");

        visualizer.Queries.Clear(); int components = Game.Instance.ComponentReads;
        ClusterManager.Instance.activeWorldId = 7;
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("availability=unobserved; markerCount=null; present=0;") && text.Contains("cells=unknown; omitted=0"), "foreign-world summary excludes totals and coordinates");
        Check(!text.Contains("(0,0)") && !text.Contains("(31,0)"), "foreign-world samples never leak");
        Check(visualizer.Queries.Count == 0 && Game.Instance.ComponentReads == components, "foreign summary never acquires or calls native visualizer");

        Grid.WorldIdx[35] = 7;
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("availability=partial; markerCount=null; present=1;") && text.Contains("cells=(35,0); omitted=0"), "mixed-world summary retains only requested active world");
        Check(visualizer.Queries.SequenceEqual(new[] {35}), "mixed-world summary native queries stay inside world");
        Grid.Visible[35] = 0; visualizer.Queries.Clear();
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("cells=unknown;") && visualizer.Queries.Count == 0, "hidden marker does not become a sample or native query");
        Grid.Visible[35] = 255; Grid.WorldValid[35] = false;
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("cells=unknown;") && visualizer.Queries.Count == 0, "invalid-world marker does not become a sample or native query");

        Grid.WorldValid[35] = true; Grid.WorldIdx[35] = 0; ClusterManager.Instance.activeWorldId = 0;
        text = WorldEditorTools.MarkerSummaryTest(0, 39, 0, 0);
        Check(text.Contains("markerCount=40; present=40;") && visualizer.Queries.Count == 40, "returning to original world restores visible markers without stale cache");
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
        CoverageRegressions();
        McpLocalization.LanguageProvider = null;
        Console.WriteLine("Buried marker adapter: " + assertions + " assertions passed");
    }
}
