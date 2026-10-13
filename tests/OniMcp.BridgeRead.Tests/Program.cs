using System;
using System.Collections.Generic;
using OniMcp.Tools;
using UnityEngine;

internal static class Program
{
    static int checks;
    static void Check(bool value) { checks++; if (!value) throw new Exception("Bridge check " + checks); }
    static GameObject Bridge(string id, Orientation orientation, bool planned, string kind)
    {
        var go = new GameObject { Cell = 210 };
        var prefab = new GameObject();
        var def = new BuildingDef { PrefabID=id, BuildingComplete=prefab,
            UtilityInputOffset=new CellOffset(-1,0), UtilityOutputOffset=new CellOffset(1,0) };
        go.Put(new Building { Def=def, Orientation=orientation });
        if (kind=="power") { def.ObjectLayer=ObjectLayer.WireConnectors; prefab.Put(new WireUtilityNetworkLink()); if(!planned)go.Put(new WireUtilityNetworkLink()); }
        if (kind=="logic") { def.BuildLocationRule=BuildLocationRule.LogicBridge; prefab.Put(new LogicUtilityNetworkLink()); if(!planned)go.Put(new LogicUtilityNetworkLink()); }
        if (kind=="liquid") def.InputConduitType=def.OutputConduitType=ConduitType.Liquid;
        if (kind=="gas") def.InputConduitType=def.OutputConduitType=ConduitType.Gas;
        if (kind=="solid") def.InputConduitType=def.OutputConduitType=ConduitType.Solid;
        if(planned)go.Put(new Constructable());
        return go;
    }
    static void Main()
    {
        var orientations=new[] { Orientation.Neutral, Orientation.R90, Orientation.R180, Orientation.R270 };
        int[] from={209,230,211,190}, to={211,190,209,230};
        foreach(bool planned in new[] {false,true})
        for(int i=0;i<4;i++)
        foreach(string kind in new[]{"power","logic","liquid","gas","solid"})
        {
            var result=NativeBridgeRead.Read(Bridge("TestBridge",orientations[i],planned,kind));
            Check(result.Kind==kind && result.Planned==planned && result.Available);
            Check(result.From==from[i] && result.To==to[i] && result.Via==210);
        }
        var missing=Bridge("WireBridge",Orientation.Neutral,true,"power");
        missing.GetComponent<Building>().Def.BuildingComplete=null;
        var unknown=NativeBridgeRead.Read(missing);
        Check(!unknown.Available && unknown.From==Grid.InvalidCell && unknown.Source=="unavailable");
        var edge=Bridge("WireBridge",Orientation.Neutral,true,"power");edge.Cell=200;
        Check(!NativeBridgeRead.Read(edge).Available);
        Grid.WorldIdx[211]=1;
        Check(!NativeBridgeRead.Read(Bridge("WireBridge",Orientation.Neutral,true,"power")).Available);
        Grid.WorldIdx[211]=0;
        Check(NativeBridgeRead.Read(new GameObject())==null);
        var ordinary=Bridge("LiquidPump",Orientation.Neutral,false,"liquid");
        Check(NativeBridgeRead.Read(ordinary)==null);
        Console.WriteLine("Native bridge read: " + checks + " checks passed.");
    }
}
namespace UnityEngine
{
    public class GameObject
    {
        public int Cell;
        readonly Dictionary<Type,object> items=new Dictionary<Type,object>();
        public T GetComponent<T>() where T:class { object v; return items.TryGetValue(typeof(T),out v)?(T)v:null; }
        public void Put<T>(T value) { items[typeof(T)]=value; }
    }
}
public struct CellOffset { public int x,y; public CellOffset(int x,int y){this.x=x;this.y=y;} }
public enum Orientation { Neutral,R90,R180,R270 }
public enum ObjectLayer { Building,WireConnectors }
public enum BuildLocationRule { Anywhere,LogicBridge }
public enum ConduitType { None,Liquid,Gas,Solid }
public class Building { public BuildingDef Def; public Orientation Orientation; }
public class BuildingDef
{
    public string PrefabID; public GameObject BuildingComplete;
    public ObjectLayer ObjectLayer; public BuildLocationRule BuildLocationRule;
    public ConduitType InputConduitType,OutputConduitType;
    public CellOffset UtilityInputOffset,UtilityOutputOffset;
}
public class Constructable {}
public class UtilityNetworkLink { public CellOffset link1=new CellOffset(-1,0),link2=new CellOffset(1,0); }
public class WireUtilityNetworkLink:UtilityNetworkLink {}
public class LogicUtilityNetworkLink:UtilityNetworkLink {}
public static class Rotatable
{
    public static CellOffset GetRotatedCellOffset(CellOffset o,Orientation r)
    {
        switch(r) {case Orientation.R90:return new CellOffset(o.y,-o.x);
            case Orientation.R180:return new CellOffset(-o.x,-o.y);
            case Orientation.R270:return new CellOffset(-o.y,o.x);default:return o;}
    }
}
public static class Grid
{
    public const int InvalidCell=-1,WidthInCells=20,HeightInCells=20;
    public static int[] WorldIdx=new int[400];
    public static bool IsValidCell(int c)=>c>=0&&c<400;
    public static int PosToCell(GameObject go)=>go.Cell;
    public static int CellColumn(int c)=>c%20;
    public static int CellRow(int c)=>c/20;
    public static int XYToCell(int x,int y)=>y*20+x;
}
