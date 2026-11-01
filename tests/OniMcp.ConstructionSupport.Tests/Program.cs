using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Tools;
using UnityEngine;

namespace UnityEngine
{
    public class GameObject
    {
        public int cell, id;
        public string name = "test";
        public List<object> components = new List<object>();
        public T GetComponent<T>() where T : class => components.OfType<T>().FirstOrDefault();
        public int GetInstanceID() => id;
    }
}
public class Component
{
    public GameObject gameObject = new GameObject();
    public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
}
public class Deconstructable { public bool marked; public bool IsMarkedForDeconstruction() => marked; }
public enum Orientation { Neutral, R90, R180, R270, FlipH, FlipV }
public struct CellOffset { public int x,y; public CellOffset(int x,int y) { this.x=x;this.y=y; } }
public class Rotatable
{
    public Orientation Orientation;
    public static CellOffset GetRotatedCellOffset(CellOffset c,Orientation o) => o switch {
        Orientation.R90 => new CellOffset(c.y,-c.x), Orientation.R180 => new CellOffset(-c.x,-c.y),
        Orientation.R270 => new CellOffset(-c.y,c.x), Orientation.FlipH => new CellOffset(-c.x,c.y),
        Orientation.FlipV => new CellOffset(c.x,-c.y), _ => c };
}
public enum BuildLocationRule { OnFloor, OnCeiling, OnFoundationRotatable, OnWall, WallFloor, InCorner, OnBackWall, Anywhere, Tile }
public class BuildingDef
{
    public string PrefabID="Building";
    public bool IsFoundation;
    public ObjectLayer TileLayer=ObjectLayer.Building;
    public CellOffset[] PlacementOffsets=new[]{new CellOffset(0,0)};
    public int WidthInCells=1,HeightInCells=1;
    public BuildLocationRule BuildLocationRule;
    public static int GetXOffset(int width) => -(width-1)/2;
    public static bool NativeResult;
    public static int NativeCalls;
    public static bool CheckFoundation(int c,Orientation o,BuildLocationRule r,int w,int h) { NativeCalls++;return NativeResult; }
}
public enum ObjectLayer { DigPlacer, Building, FoundationTile }
public static class Grid
{
    public const int InvalidCell=-1, Width=40, Height=45;
    public static bool[] Solid = new bool[Width*Height],Foundation = new bool[Width*Height],Visible = Enumerable.Repeat(true,Width*Height).ToArray();
    public static byte[] WorldIdx=new byte[Width*Height];
    public static GameObject[,] Objects=new GameObject[Width*Height,3];
    public static bool IsValidCell(int c)=>c>=0&&c<Width*Height;
    public static bool IsWorldValidCell(int c)=>IsValidCell(c);
    public static bool IsVisible(int c)=>Visible[c];
    public static int CellColumn(int c)=>c%Width;
    public static int CellRow(int c)=>c/Width;
    public static int XYToCell(int x,int y)=>x+y*Width;
    public static int PosToCell(GameObject go)=>go.cell;
}
namespace OniMcp.Tools
{
    public static partial class ToolUtil { public static bool CellMatchesWorld(int c,int w)=>Grid.IsValidCell(c)&&(w<0||Grid.WorldIdx[c]==w); }
    public class Position { public bool Valid; public int? Cell; }
    public class Shape { public bool Available=true; public Position Origin; public List<Position> Cells; }
    public static class NativeBuildingFootprint
    {
        public static Position AbsoluteCell(int x,int y)=>new Position{Valid=x>=0&&x<Grid.Width&&y>=0&&y<Grid.Height,Cell=x>=0&&x<Grid.Width&&y>=0&&y<Grid.Height?y*Grid.Width+x:(int?)null};
        public static Shape FromLowerLeft(BuildingDef d,int x,int y,Orientation o)=>new Shape{Origin=AbsoluteCell(x,y),Cells=new List<Position>{AbsoluteCell(x,y)}};
    }
    public static partial class BuildPlanningTools
    {
        public static HashSet<int> Blueprints=new HashSet<int>();
        private static bool HasSupportBlueprint(int c)=>Blueprints.Contains(c);
        public static Dictionary<string,object> Support(BuildingDef d,int x,int y,bool allow=false,HashSet<int> planned=null,Orientation o=Orientation.Neutral)
            =>ValidateSupport(d,x,y,allow,planned,o).ToDictionary();
    }
}
class Program
{
    static int checks;
    static void Check(bool v,string why){checks++;if(!v)throw new Exception(why);}
    static int C(int x,int y)=>y*Grid.Width+x;
    static void Reset()
    {
        Array.Clear(Grid.Solid);Array.Clear(Grid.Foundation);Array.Clear(Grid.Objects);Array.Clear(Grid.WorldIdx);
        Array.Fill(Grid.Visible,true);BuildPlanningTools.Blueprints.Clear();
        BuildingDef.NativeCalls=0;BuildingDef.NativeResult=false;
    }
    static void Geometry()
    {
        var cases=new[]{
            (Orientation.Neutral,new[]{"0,2","1,2"}), (Orientation.R90,new[]{"2,0","2,-1"}),
            (Orientation.R180,new[]{"0,-2","-1,-2"}), (Orientation.R270,new[]{"-2,0","-2,1"})};
        foreach(var c in cases)
            Check(ConstructionSupportGeometry.Offsets("OnCeiling",2,2,c.Item1).Select(v=>$"{v.x},{v.y}").SequenceEqual(c.Item2),"Ceiling supports use rotated native origin");
        Check(ConstructionSupportGeometry.Offsets("OnFloor",3,2,Orientation.R90).Select(v=>$"{v.x},{v.y}").SequenceEqual(new[]{"-1,1","-1,0","-1,-1"}),"Floor rotation does not use bounding-box bottom");
        Check(ConstructionSupportGeometry.Offsets("OnWall",2,2,Orientation.FlipH).All(v=>v.x==2),"Wall side uses native FlipH");
        Check(ConstructionSupportInspection.Offset(C(39,4),new CellOffset(1,0))==-1,"Offset must not wrap row");
    }
    static void Support()
    {
        Reset();var d=new BuildingDef{BuildLocationRule=BuildLocationRule.OnCeiling,HeightInCells=2};int top=C(10,12);
        var r=BuildPlanningTools.Support(d,10,10);Check(!(bool)r["valid"]&&!(bool)r["nativeFoundationValid"],"Missing ceiling must not pass");
        Grid.Solid[top]=true;BuildingDef.NativeResult=true;r=BuildPlanningTools.Support(d,10,10);Check((bool)r["currentValid"]&&(bool)r["valid"],"Native supported ceiling");
        Grid.Objects[top,0]=new GameObject();r=BuildPlanningTools.Support(d,10,10);Check((bool)r["warningOnly"]&&(string)r["status"]=="support_scheduled_for_removal","Dig on ceiling support is visible");
        Grid.Solid[top]=false;Grid.Objects[top,0]=null;BuildingDef.NativeResult=false;BuildPlanningTools.Blueprints.Add(top);
        r=BuildPlanningTools.Support(d,10,10);Check((bool)r["valid"]&&!(bool)r["currentValid"]&&(bool)r["requiresSupportCompletion"],"Blueprint is future support, not current support");
        BuildPlanningTools.Blueprints.Clear();r=BuildPlanningTools.Support(d,10,10,planned:new HashSet<int>{top});Check((bool)r["requiresSupportCompletion"],"Batch-planned tile is a dependency");
        r=BuildPlanningTools.Support(d,10,10,allow:true);Check((bool)r["valid"]&&(bool)r["warningOnly"]&&!(bool)r["currentValid"],"Explicit unsupported placement preserves native failure");
        Grid.Visible[top]=false;int calls=BuildingDef.NativeCalls;r=BuildPlanningTools.Support(d,10,10,allow:true);Check(!(bool)r["valid"]&&r["currentValid"]==null&&calls==BuildingDef.NativeCalls,"Hidden support unknown without native query");
        Grid.Visible[top]=true;Grid.WorldIdx[top]=1;r=BuildPlanningTools.Support(d,10,10);Check(r["currentValid"]==null,"Foreign world support unknown");
        Reset();d.BuildLocationRule=BuildLocationRule.OnFloor;d.WidthInCells=2;
        r=BuildPlanningTools.Support(d,10,10,o:Orientation.R90);var cells=(List<Dictionary<string,object>>)r["supportCells"];
        Check(cells.Select(v=>(int)v["cell"]).SequenceEqual(new[]{C(9,10),C(9,9)}),"Rotated floor support cells exposed");
    }
    static void Main(){AreaSupportRegression.Run();AreaScopeRegression.Run();Geometry();Support();Console.WriteLine($"Construction support: {checks} checks passed");}
}
