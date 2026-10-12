using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class NativeBuildingFootprintRegression
{
    private static int checks;
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new InvalidOperationException(message);
    }
    private static HashSet<string> Positions(NativeBuildingFootprint shape) =>
        new HashSet<string>(shape.Cells.Select(c => c.X + "," + c.Y));
    internal static void Run()
    {
        var strip = new TestBuildingDef { PrefabID = "native-strip", PlacementOffsets = new[]
            { new CellOffset(-1,0), new CellOffset(0,0), new CellOffset(1,0) } };
        var originByOrientation = new Dictionary<Orientation,int[]>
        {
            [Orientation.Neutral] = new[] {11,10}, [Orientation.R90] = new[] {10,11},
            [Orientation.R180] = new[] {11,10}, [Orientation.R270] = new[] {10,11},
            [Orientation.FlipH] = new[] {11,10}, [Orientation.FlipV] = new[] {11,10}
        };
        foreach (var item in originByOrientation)
        {
            var shape = NativeBuildingFootprint.FromLowerLeft(strip,10,10,item.Key);
            bool vertical = item.Key == Orientation.R90 || item.Key == Orientation.R270;
            var expected = vertical ? new[] {"10,10","10,11","10,12"} : new[] {"10,10","11,10","12,10"};
            Check(shape.Available && Positions(shape).SetEquals(expected), "rotated occupied set " + item.Key);
            Check(shape.Origin.X == item.Value[0] && shape.Origin.Y == item.Value[1], "lower-left to native origin " + item.Key);
            Check(shape.Bounds.SequenceEqual(vertical ? new[] {10,10,10,12} : new[] {10,10,12,10}), "actual rotated bounds " + item.Key);
        }
        var sparse = new TestBuildingDef { PrefabID = "native-sparse", PlacementOffsets = new[]
            {new CellOffset(-1,0),new CellOffset(0,0),new CellOffset(-1,1)} };
        foreach (var orientation in originByOrientation.Keys)
        {
            var shape = NativeBuildingFootprint.FromLowerLeft(sparse,20,20,orientation);
            Check(shape.Available && shape.FillsBounds == false && shape.Cells.Count == 3, "sparse bounds preserve hole " + orientation);
            // Opposite corners, relative to the requested lower-left, independently checked.
            var masks = new Dictionary<Orientation,string[]>
            {
                [Orientation.Neutral]=new[]{"20,20","21,20","20,21"},
                [Orientation.R90]=new[]{"20,21","20,20","21,21"},
                [Orientation.R180]=new[]{"21,21","20,21","21,20"},
                [Orientation.R270]=new[]{"21,20","21,21","20,20"},
                [Orientation.FlipH]=new[]{"21,20","20,20","21,21"},
                [Orientation.FlipV]=new[]{"20,21","21,21","20,20"}
            };
            Check(Positions(shape).SetEquals(masks[orientation]), "sparse rotated cells " + orientation);
        }
        Check(!NativeBuildingFootprint.AtOrigin(strip,0,5,Orientation.Neutral).Available, "row-wrap is invalid even if linear index exists");
        Check(!NativeBuildingFootprint.FromLowerLeft(strip,Grid.WidthInCells-2,5,Orientation.Neutral).Available, "right edge overflow rejected");
        Check(NativeBuildingFootprint.FromLowerLeft(new BuildingDef(),2,2,Orientation.Neutral).Error == "placement_offsets_missing", "no rectangle substituted for missing metadata");
        Check(NativeBuildingFootprint.Definition(strip,Orientation.NumRotations).Error == "orientation_invalid", "sentinel rotation rejected");
        strip.CorruptNative = true;
        Check(NativeBuildingFootprint.FromLowerLeft(strip,10,10,Orientation.Neutral).Error == "native_footprint_mismatch", "native callback mismatch remains unknown");
        strip.CorruptNative = false;

        Components.BuildingCompletes.Items.Clear(); UnityEngine.Object.Constructables.Clear();
        var bridge = new GameObject {Cell = Grid.XYToCell(10,11)};
        bridge.Building = new Building {Def = strip, Orientation = Orientation.R90};
        Components.BuildingCompletes.Items.Add(new BuildingComplete {gameObject=bridge});
        NativeBuildingCellIndex.Invalidate();
        foreach (int y in new[]{10,11,12}) Check(NativeBuildingCellIndex.At(Grid.XYToCell(10,y)).Contains(bridge), "bridge indexed without Grid.Objects registration");
        Check(!NativeBuildingCellIndex.At(Grid.XYToCell(11,11)).Contains(bridge), "old horizontal rectangle removed");
        var blueprint = new GameObject {Cell=Grid.XYToCell(20,20)};
        blueprint.Building = new Building {Def=sparse,Orientation=Orientation.FlipH};
        UnityEngine.Object.Constructables.Add(new Constructable {gameObject=blueprint});
        NativeBuildingCellIndex.Invalidate();
        Check(NativeBuildingCellIndex.At(Grid.XYToCell(21,21)).Contains(blueprint), "same-frame blueprint invalidation");
        Check(!NativeBuildingCellIndex.At(Grid.XYToCell(20,21)).Contains(blueprint), "blueprint sparse hole remains empty");
        UnityEngine.Object.Constructables.Clear(); Time.frameCount++;
        Check(!NativeBuildingCellIndex.At(Grid.XYToCell(21,21)).Contains(blueprint), "next-frame removal refresh");
        Components.BuildingCompletes.Items.Clear();NativeBuildingCellIndex.Invalidate();

        Assets.BuildingDefs.Add(strip);Assets.BuildingDefs.Add(sparse);
        string error; JArray anchors;
        Check(WorldEditorTools.FootprintMaskTest("native-strip","R90",new[]{new[]{10,10},new[]{10,11},new[]{10,12}},out anchors,out error) && anchors.Count==1, "map accepts vertical native mask");
        Check(!WorldEditorTools.FootprintMaskTest("native-strip","R90",new[]{new[]{10,10},new[]{11,10},new[]{12,10}},out anchors,out error), "map rejects old horizontal rectangle");
        Check(WorldEditorTools.FootprintMaskTest("native-sparse","FlipH",new[]{new[]{20,20},new[]{21,20},new[]{21,21}},out anchors,out error) && anchors.Count==1, "map accepts exact sparse mask");
        Check(!WorldEditorTools.FootprintMaskTest("native-sparse","Neutral",new[]{new[]{20,20},new[]{21,20},new[]{20,21},new[]{21,21}},out anchors,out error), "map rejects filled sparse bounds");
        Check(WorldEditorTools.FootprintMaskTest("native-strip","R270",new[]{new[]{10,10}},out anchors,out error) && anchors[0]["y"].Value<int>()==10, "single marker remains lower-left shorthand");
        var disconnected = new TestBuildingDef { PrefabID="native-disconnected", PlacementOffsets=new[]
            {new CellOffset(0,0),new CellOffset(2,0)} };
        Assets.BuildingDefs.Add(disconnected);
        Check(!WorldEditorTools.FootprintMaskTest("native-disconnected","Neutral",new[]{new[]{20,20},new[]{22,20}},out anchors,out error), "disconnected full mask does not create multiple buildings");
        Check(WorldEditorTools.FootprintMaskTest("native-disconnected","Neutral",new[]{new[]{20,20}},out anchors,out error) && anchors.Count==1, "disconnected shape supports one explicit lower-left marker");
        Assets.BuildingDefs.Remove(disconnected);
        Assets.BuildingDefs.Remove(strip); Assets.BuildingDefs.Remove(sparse);
        Console.WriteLine("Native footprint and map-mask regression passed: " + checks + " checks");
    }
}
namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static bool FootprintMaskTest(string id,string orientation,int[][] positions,out JArray anchors,out string error) =>
            TryBuildAnchorsForPrefabFootprints(new JObject {["orientation"]=orientation},id,
                positions.Select(p=>new MapEditCell {X=p[0],Y=p[1]}),out anchors,out error);
    }
}
// Substitute only the game boundary. Production geometry and mask parsing are linked above.
internal enum Orientation { Neutral,R90,R180,R270,FlipH,FlipV,NumRotations }
internal struct CellOffset
{
    internal int x,y;
    internal CellOffset(int x,int y) {this.x=x;this.y=y;}
}
internal class Rotatable
{
    internal static CellOffset GetRotatedCellOffset(CellOffset c,Orientation o)
    {
        switch(o)
        {
            case Orientation.R90:return new CellOffset(c.y,-c.x);
            case Orientation.R180:return new CellOffset(-c.x,-c.y);
            case Orientation.R270:return new CellOffset(-c.y,c.x);
            case Orientation.FlipH:return new CellOffset(-c.x,c.y);
            case Orientation.FlipV:return new CellOffset(c.x,-c.y);
            default:return c;
        }
    }
}
internal partial class BuildingDef
{
    internal CellOffset[] PlacementOffsets {get;set;}
    internal bool CorruptNative {get;set;}
    internal void RunOnArea(int cell,Orientation orientation,Action<int> callback)
    {
        foreach(var offset in PlacementOffsets)
        {
            var rotated=Rotatable.GetRotatedCellOffset(offset,orientation);
            callback(cell+rotated.x+rotated.y*Grid.WidthInCells+(CorruptNative?1:0));
        }
    }
}
internal class Building {internal BuildingDef Def;internal Orientation Orientation;}
internal class BuildingComplete {internal GameObject gameObject;}
internal class Constructable {internal GameObject gameObject;}
internal static partial class Components
{
    internal static class BuildingCompletes {internal static readonly List<BuildingComplete> Items=new List<BuildingComplete>();}
}
internal static partial class Grid
{
    internal const int WidthInCells=100,HeightInCells=100,InvalidCell=-1;
    internal static int XYToCell(int x,int y)=>x+y*WidthInCells;
    internal static bool IsValidCell(int c)=>c>=0 && c<WidthInCells*HeightInCells;
    internal static int CellColumn(int c)=>c%WidthInCells;
    internal static int CellRow(int c)=>c/WidthInCells;
    internal static int PosToCell(GameObject go)=>go.Cell;
}
namespace UnityEngine
{
    internal enum FindObjectsSortMode {None}
    internal class Object
    {
        internal static readonly List<Constructable> Constructables=new List<Constructable>();
        internal static T[] FindObjectsByType<T>(FindObjectsSortMode mode)=>Constructables.Cast<T>().ToArray();
    }
    internal class GameObject : Object
    {
        internal int Cell;internal Building Building;
        internal T GetComponent<T>() where T:class => Building as T;
    }
    internal static class Time {internal static int frameCount {get;set;}}
}
