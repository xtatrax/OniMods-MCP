using OniMcp.Tools;
using UnityEngine;
public static class Program
{
 static int checks;
 static void Check(bool ok) { checks++; if (!ok) throw new Exception("check " + checks); }
 public static void Main()
 {
  UtilityPowerScopeRegression.Run();
  foreach (object component in new object[] { new Wire(), new LogicWire(), new Conduit(), new SolidConduit() })
  {
   var mgr = new Manager(); var def = new BuildingDef { PrefabID = component.GetType().Name, BuildingComplete = new GameObject() };
   def.BuildingComplete.Add(component); def.BuildingComplete.Add(new KAnimGraphTileVisualizer { ConnectionManager = mgr }); Assets.Def = def;
   Grid.Objects = new GameObject[100,2];
   KAnimGraphTileVisualizer Put(int x,int y,bool physical) {
    int c=Grid.XYToCell(x,y);var go=new GameObject();var graph=new KAnimGraphTileVisualizer { Cell=c,isPhysicalBuilding=physical,ConnectionManager=mgr,connectionManager=mgr };
    go.Add(graph);go.Add(new Building {Def=def});if(physical)go.Add(new BuildingComplete {Def=def});Grid.Objects[c,0]=go;return graph;
   }
   var a=Put(2,2,false);var b=Put(3,2,false);var c=Put(3,3,true);var parallel=Put(4,3,true);
   a.UpdateConnections(UtilityConnections.Left);parallel.UpdateConnections(UtilityConnections.Up);
   Check(BuildPlanningTools.Connect(def,new[]{(2,2),(3,2),(3,3)}));
   Check(a.Connections==(UtilityConnections.Left|UtilityConnections.Right));
   Check(b.Connections==(UtilityConnections.Left|UtilityConnections.Up));Check(c.Connections==UtilityConnections.Down);
   Check(parallel.Connections==UtilityConnections.Up);
   int writes=mgr.Writes;Check(BuildPlanningTools.Connect(def,new[]{(2,2),(3,2),(3,3)}));Check(a.Connections==(UtilityConnections.Left|UtilityConnections.Right));
   // A missing endpoint and a rejected edge must leave all existing masks intact.
   writes=mgr.Writes;Check(!BuildPlanningTools.Connect(def,new[]{(3,3),(3,4)}));Check(mgr.Writes==writes);
   mgr.Reject=true;Check(!BuildPlanningTools.Connect(def,new[]{(3,3),(4,3)}));Check(mgr.Writes==writes);mgr.Reject=false;
   // Native construction transfers the serialized blueprint mask to its new graph.
   var complete=Put(3,2,true);complete.UpdateConnections(b.Connections);Check(complete.Connections==(UtilityConnections.Left|UtilityConnections.Up));
   // A non-adjacent request is rejected, never interpolated by the edge writer.
   writes=mgr.Writes;Check(!BuildPlanningTools.Connect(def,new[]{(2,2),(4,3)}));Check(mgr.Writes==writes);
  }
  AutoRouteIntentRegression.Run();
  AutoPowerResultRegression.Run();
  Console.WriteLine("Utility production-source regression passed: " + checks + " checks (native manager substituted)");
 }
}
namespace OniMcp.Tools { public static partial class BuildPlanningTools {
 private struct CellCoord {public int x,y; public CellCoord(int a,int b){x=a;y=b;}}
 private static bool EqualsIgnoreCase(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
 public static bool Connect(BuildingDef d,(int x,int y)[] p)=>RefreshAndValidateUtilityPathNetwork(d,p.Select(v=>new CellCoord{x=v.x,y=v.y}).ToList(),out _);
} }
namespace UnityEngine {public class GameObject {readonly List<object> cs=new();public void Add(object o)=>cs.Add(o);public T GetComponent<T>() where T:class=>cs.OfType<T>().FirstOrDefault();}}
[Flags] public enum UtilityConnections { Left=1,Right=2,Up=4,Down=8 }
public class Wire{} public class LogicWire{} public class Conduit{} public class SolidConduit{}
public class BuildingDef {public bool RequiresPowerInput;public CellOffset PowerInputOffset;public string PrefabID;public GameObject BuildingComplete;public int TileLayer=0,ObjectLayer=0;}
public class Building {public BuildingDef Def;} public class BuildingComplete {public BuildingDef Def;}
public static class Assets {public static BuildingDef Def;public static BuildingDef GetBuildingDef(string id)=>Def;}
public static class Grid {public const int InvalidCell=-1;public static GameObject[,] Objects=new GameObject[100,2];public static int XYToCell(int x,int y)=>y*10+x;public static int OffsetCell(int c,CellOffset offset)=>XYToCell(CellColumn(c)+offset.x,CellRow(c)+offset.y);public static int CellColumn(int c)=>c%10;public static int CellRow(int c)=>c/10;public static bool IsValidCell(int c)=>c>=0&&c<100;}
public interface IUtilityNetworkMgr {UtilityConnections GetConnections(int c,bool is_physical_building);bool CanAddConnection(UtilityConnections bit,int c,bool physical,out string error);void ForceRebuildNetworks();}
public class Manager:IUtilityNetworkMgr {public bool Reject;public int Writes;public Dictionary<int,UtilityConnections> Bits=new();public UtilityConnections GetConnections(int c,bool is_physical_building)=>Bits.GetValueOrDefault(c);public bool CanAddConnection(UtilityConnections bit,int c,bool physical,out string error){error=Reject?"rejected":null;return !Reject;}public void ForceRebuildNetworks(){} }
public class KAnimGraphTileVisualizer {public int Cell;public bool isPhysicalBuilding;public IUtilityNetworkMgr ConnectionManager,connectionManager;public UtilityConnections Connections;public void UpdateConnections(UtilityConnections bits){Connections=bits;((Manager)connectionManager).Bits[Cell]=bits;((Manager)connectionManager).Writes++;}public void Refresh(){} }
