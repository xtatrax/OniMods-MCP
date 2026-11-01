using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace UnityEngine {
 public class Component {public GameObject gameObject;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();}
 public class GameObject {
  public int Cell=18;private readonly List<object> components=new();
  public void Add(object o){components.Add(o);if(o is Component c)c.gameObject=this;}
  public T GetComponent<T>()where T:class=>components.OfType<T>().FirstOrDefault();
  public T[] GetComponents<T>()=>components.OfType<T>().ToArray();
  public int GetInstanceID()=>123;public string GetProperName()=>"fixture";
 }
}
public enum Orientation {Neutral,R90,NumRotations}
public enum ObjectLayer {Building,GasConduit,GasConduitTile,ReplacementGasConduit,LiquidConduit,LiquidConduitTile,ReplacementLiquidConduit,Wire,WireTile,ReplacementWire,LogicWire,LogicWireTile,ReplacementLogicWire,SolidConduit,SolidConduitTile,ReplacementSolidConduit}
public class BuildingDef {
 public UnityEngine.GameObject BuildingComplete;public string PrefabID="Fixture",Name="Fixture";
 public int WidthInCells=1,HeightInCells=1;public Orientation PermittedRotations;
 public ObjectLayer ObjectLayer,TileLayer;public string BuildLocationRule="OnFloor";
 public CellOffset[] PlacementOffsets={new CellOffset(0,0)};
 public CellOffset PowerInputOffset,PowerOutputOffset,UtilityInputOffset,UtilityOutputOffset,HighEnergyParticleInputOffset,HighEnergyParticleOutputOffset;
 public bool RequiresPowerInput,RequiresPowerOutput,UseHighEnergyParticleInputPort,UseHighEnergyParticleOutputPort;
 public ConduitType InputConduitType,OutputConduitType;
 public List<LogicPorts.Port> LogicInputPorts=new(),LogicOutputPorts=new();
}
public class Building:UnityEngine.Component {public BuildingDef Def;public Orientation Orientation;}
public class BuildingComplete:Building{}
public class KPrefabID:UnityEngine.Component {public int InstanceID=7;}
public class UtilityNetworkLink:UnityEngine.Component {public CellOffset link1,link2;}
public class LogicPorts:UnityEngine.Component {
 public class Port {public string id;public CellOffset cellOffset;}
 public List<Port> inputPortInfo=new(),outputPortInfo=new();
}
public static class Rotatable {public static CellOffset GetRotatedCellOffset(CellOffset p,Orientation o)=>o==Orientation.R90?new CellOffset(-p.y,p.x):p;}
public static class Grid {
 public const int InvalidCell=-1;public static byte[] WorldIdx=new byte[64];
 public static bool IsValidCell(int c)=>c>=0&&c<64;public static bool IsWorldValidCell(int c)=>IsValidCell(c);
 public static int PosToCell(UnityEngine.GameObject go)=>go.Cell;public static int CellColumn(int c)=>c%8;public static int CellRow(int c)=>c/8;
 public static int OffsetCell(int c,CellOffset p)=>c+p.y*8+p.x;
}
public static class Assets {public static readonly Dictionary<string,BuildingDef> Defs=new();public static BuildingDef GetBuildingDef(string id)=>Defs.GetValueOrDefault(id);}
public class Game {public static Game Instance=new();}
public class ClusterManager {public static ClusterManager Instance=new();public int activeWorldId;}
public static class Components {public static Registry<BuildingComplete> BuildingCompletes=new();}
public class Registry<T>{public List<T> Items=new();}
public interface IUtilityNetworkMgr { }
public interface IHaveUtilityNetworkMgr {IUtilityNetworkMgr GetNetworkManager();}
public class Wire:IHaveUtilityNetworkMgr{public IUtilityNetworkMgr GetNetworkManager()=>throw new Exception("not used by geometry fixture");}
public class Conduit:Wire{} public class LogicWire:Wire{} public class SolidConduit:Wire{}
public class KAnimGraphTileVisualizer {public IUtilityNetworkMgr connectionManager;public IUtilityNetworkMgr ConnectionManager=>connectionManager;}
namespace OniMcp.Core {
 public class CallToolResult {public bool IsError;public string Value;public static CallToolResult Text(string s)=>new(){Value=s};public static CallToolResult Error(string s)=>new(){IsError=true,Value=s};}
}
namespace OniMcp.Support {public static class McpJsonUtil{public static JsonSerializerSettings Settings=new();}}
namespace OniMcp.Tools {
 public static class ToolUtil {
  public static bool GetBool(JObject a,string k,bool d)=>(bool?)a[k]??d;public static int? GetInt(JObject a,string k)=>(int?)a[k];
  public static string CleanName(string s)=>s;public static bool GameObjectMatchesWorld(UnityEngine.GameObject go,int w)=>true;
 }
 public static class McpLocalization {public static string Get(string k)=>k;}
 internal class Footprint {
  public bool Available=true;public string Error=null;public BuildingGeometryCell Origin=new(){X=0,Y=0};
  public List<BuildingGeometryCell> Cells=new(){new(){X=0,Y=0}};
 }
 internal static class NativeBuildingFootprint {
  internal static Footprint Definition(BuildingDef d,Orientation o)=>new();
  internal static Footprint Instance(UnityEngine.GameObject go,BuildingDef d)=>new(){Origin=new(){X=Grid.CellColumn(go.Cell),Y=Grid.CellRow(go.Cell),Cell=go.Cell}};
 }
}

public class FlowUtilityNetwork{} public class ElectricalUtilityNetwork{} public class LogicCircuitNetwork{} public class Vent{}
public class UtilityNetworkManager<TNetwork,TLink>:IUtilityNetworkMgr{}
