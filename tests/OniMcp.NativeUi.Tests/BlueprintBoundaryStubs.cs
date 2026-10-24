using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
#pragma warning disable CS0649 // Native fixture members may intentionally retain default values.

// UI/native writes, native geometry and unrelated placement collaborators are
// substituted. Exact production callers/adapter/safety slices are compiled.
namespace UnityEngine
{
    internal class Object
    {
        public static readonly List<BuildingComplete> DetachedCompletes=new List<BuildingComplete>();
        public static bool ThrowEnumeration;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T:class
        {if(ThrowEnumeration)throw new InvalidOperationException("fixture enumeration unavailable");return All().Where(value=>value.gameObject.Active).OfType<T>().ToArray();}
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive,FindObjectsSortMode mode) where T:class
        {if(ThrowEnumeration)throw new InvalidOperationException("fixture enumeration unavailable");return All().OfType<T>().ToArray();}
        private static IEnumerable<Component> All()
        {return global::Components.BuildingCompletes.Items.Cast<Component>().Concat(DetachedCompletes).Concat(OniMcp.Tools.BuildPlanningTools.FixtureBlueprints).Distinct().Where(value=>!value.gameObject.Destroyed);}
    }
    internal struct Vector3
    {
        public float x,y,z;
        public Vector3(float a,float b,float c) { x=a;y=b;z=c; }
    }
}
internal struct Tag
{
    public string Name;
    public Tag(string value) { Name=value; }
    public bool IsValid { get { return !string.IsNullOrEmpty(Name); } }
    public static Tag Invalid {get{return new Tag();}}
    public string ProperNameStripLink(){return Name;}
    public static bool operator ==(Tag a,Tag b){return a.Name==b.Name;}
    public static bool operator !=(Tag a,Tag b){return a.Name!=b.Name;}
    public override bool Equals(object value){return value is Tag && this==(Tag)value;}
    public override int GetHashCode(){return Name==null?0:Name.GetHashCode();}
    public override string ToString() { return Name; }
}
internal enum FindObjectsSortMode { None }
internal enum FindObjectsInactive { Include }
internal struct CellOffset {public int x,y;}
internal enum ConduitType {None,Gas,Liquid}
internal interface ILogicUIElement {int GetLogicUICell();}
internal sealed class LogicCircuitManager {public List<ILogicUIElement> GetVisElements(){return new List<ILogicUIElement>();}}
internal sealed class LogicGate : Component {public int InputCellOne,InputCellTwo,InputCellThree,InputCellFour,OutputCellOne,OutputCellTwo,OutputCellThree,OutputCellFour,ControlCellOne,ControlCellTwo;public bool RequiresTwoInputs,RequiresFourInputs,RequiresFourOutputs,RequiresControlInputs;}
internal sealed class ConduitBridgeBase : Component {public bool isSpawned;}
internal class UtilityNetworkLink : Component {public bool isSpawned,visualizeOnly; public void GetCells(int cell,Orientation o,out int a,out int b){a=cell;b=cell+1;}}
internal sealed class LogicUtilityNetworkLink : UtilityNetworkLink {}
internal enum Orientation { Neutral, R90, R180, R270 }
internal enum ObjectLayer { Building, DigPlacer, Wire, WireConnectors, GasConduit, LiquidConduit, LogicWire, Backwall, Other, FoundationTile, ReplacementTile, ReplacementLadder, NumLayers=16 }
internal enum BuildLocationRule { Other, Conduit, WireBridge, LogicBridge, Tile, Anywhere, OnFloor }
internal sealed partial class BuildingDef
{
    public string PrefabID,Name;
    public int SceneLayer;
    public ObjectLayer ObjectLayer=ObjectLayer.Building;
    public ConduitType InputConduitType,OutputConduitType;
    public CellOffset UtilityInputOffset,UtilityOutputOffset;
    public void RunOnArea(int cell,Orientation orientation,System.Action<int> action){action(cell);}
    public BuildLocationRule BuildLocationRule;
    public GameObject BuildingComplete;
    public int DirectBuildCalls;
    public GameObject Build(int cell,Orientation orientation,object unused,IList<Tag> elements,float temperature,string facade,bool playsound,float time)
    { DirectBuildCalls++;throw new InvalidOperationException("fixture direct build forbidden"); }
}
internal sealed class Building : Component { public BuildingDef Def; public Orientation Orientation {get{return GetComponent<Rotatable>()?.Orientation??Orientation.Neutral;}} }
internal sealed class BuildingComplete : Component { public BuildingDef Def; }
internal sealed partial class Constructable : Component { public List<Tag> SelectedElementsTags = new List<Tag>(); }
internal sealed class BuildingFacade : Component { public string CurrentFacade; public bool IsOriginal; }
internal sealed class PrimaryElement : Component {public string ElementID;public float Mass=100f;}
internal sealed class Rotatable : Component
{
    public Orientation Orientation;
    public Orientation GetOrientation() { return Orientation; }
    public static CellOffset GetRotatedCellOffset(CellOffset value,Orientation orientation){return value;}
}
internal sealed class LogicPorts : Component
{
    internal sealed class Port { public int Cell; public string id;public CellOffset cellOffset; }
    public Port[] inputPortInfo,outputPortInfo;
}
internal static class LogicPortReadSemantics
{ internal static int ActualCell(LogicPorts ports,LogicPorts.Port port) { return port.Cell; }
internal static bool RegisteredEndpointsMatch(LogicPorts ports,LogicCircuitManager manager){return true;}
internal static bool TryBridgeRoute(GameObject go,out int first,out int second){first=0;second=1;return true;} }
internal sealed partial class Game { public static Game Instance; public LogicCircuitManager logicCircuitManager;public bool Loading; public bool IsLoading(){return Loading;} }
internal sealed partial class PlanScreen { public static PlanScreen Instance; }
internal sealed class World { public static World Instance; public object blockTileRenderer; }
internal sealed class GridCompositor { public static GridCompositor Instance; }
internal sealed class ResourceRemainingDisplayScreen { public static ResourceRemainingDisplayScreen instance; }
internal sealed class SpeedControlScreen { public static SpeedControlScreen Instance; public bool IsPaused = true; }
internal sealed class KScreen
{
    public bool Active,Modal;
    public bool IsScreenActive(){return Active;}public bool IsModal(){return Modal;}
}
internal sealed class KScreenManager
{
    public static KScreenManager Instance;
    public List<KScreen> screenStack = new List<KScreen>();
    public static bool inputDisabled;
}
internal sealed class BuildToolHoverTextCard { }
internal sealed class PlayerController
{
    public static PlayerController Instance;
    public object ActiveTool;
    public bool Dragging,RejectRestore;
    public int RestoreCalls;
    public System.Action OnRestore;
    public bool IsDragging(){return Dragging;}
    public void ActivateTool(SelectTool tool){RestoreCalls++;if(!RejectRestore){ActiveTool=tool;OnRestore?.Invoke();}}
}
internal sealed partial class BuildTool : Component
{
    public static BuildTool Instance;
    public bool Dragging;
    public GameObject visualizer;
    public int GetLastCell;
    public Orientation GetBuildingOrientation;
    public BuildingDef ActivatedDef;
    public IList<Tag> ActivatedElements;
    private IList<Tag> selectedElements;
    private BuildingDef def;
    public void ReplaceNativeMaterials(IList<Tag> value){selectedElements=value;}
    public void EditNativeMaterial(Tag value){selectedElements[0]=value;}
    public string ActivatedFacade;
    public int Activations,MouseMoves,BuildCalls;
    public System.Action OnActivate,OnMouseMoveFixture;
    public System.Action<int> OnBuild;
    public void Activate(BuildingDef def,IList<Tag> elements,string facade)
    {
        Activations++;ActivatedDef=def;this.def=def;ActivatedElements=elements;selectedElements=elements;ActivatedFacade=facade;
        PlayerController.Instance.ActiveTool=this;
        visualizer=new GameObject();visualizer.Add(new Building { Def=def });
        visualizer.Add(new BuildingFacade{CurrentFacade=facade,IsOriginal=facade==null||facade=="DEFAULT_FACADE"});
        OnActivate?.Invoke();
    }
    public void SetToolOrientation(Orientation orientation){GetBuildingOrientation=orientation;}
    public void OnMouseMove(Vector3 value)
    {
        MouseMoves++;GetLastCell=Grid.XYToCell((int)value.x,(int)value.y);visualizer.Cell=GetLastCell;
        OnMouseMoveFixture?.Invoke();
    }
    private void TryBuild(int cell){BuildCalls++;OnBuild?.Invoke(cell);}
}
internal static class DebugHandler { public static bool InstantBuildMode; }
internal static class Components
{
    internal sealed class Collection<T>{public readonly List<T> Items=new List<T>();}
    public static readonly Collection<BuildingComplete> BuildingCompletes=new Collection<BuildingComplete>();
}
namespace OniMcp.Tools
{
    internal static class BuildingControlTools { internal static bool IsVirtualFileEditContext; }
    internal static partial class ToolUtil
    {
        internal static bool GetBool(JObject args,string key,bool fallback=false){return args?[key]?.Value<bool?>()??fallback;}
        internal static int? GetInt(JObject args,string key){return args?[key]?.Value<int?>();}
        internal static string CleanName(string value){return value;}
        internal static bool CellMatchesWorld(int cell,int world){return Grid.IsValidCell(cell)&&Grid.WorldIdx[cell]==world;}
    }
    internal sealed class BuildingGeometryCell
    { public int X,Y;public int? Cell; }
    internal sealed class FixtureGeometry
    {
        public bool Available=true;
        public List<BuildingGeometryCell> Cells=new List<BuildingGeometryCell>();
        public BuildingGeometryCell Origin;
        public int[] Bounds=new int[4];
        public Orientation Orientation;
        public Dictionary<string,object> Summary(){return new Dictionary<string,object>{["orientation"]=Orientation.ToString()};}
    }
    internal static class NativeBuildingCellIndex
    {
        internal static readonly Dictionary<GameObject,FixtureGeometry> Shapes=new Dictionary<GameObject,FixtureGeometry>();
        internal static int Invalidations,Reads;
        internal static System.Action<GameObject,int> OnRead;
        internal static void Invalidate(){Invalidations++;}
        internal static FixtureGeometry Geometry(GameObject go)
        {Reads++;OnRead?.Invoke(go,Reads);FixtureGeometry result;return Shapes.TryGetValue(go,out result)?result:new FixtureGeometry {Available=false}; }
    }
    internal static class NativeBuildingFootprint { internal const string NativeSource="fixture_native_geometry";internal static FixtureGeometry Instance(GameObject go,BuildingDef def){return NativeBuildingCellIndex.Geometry(go);} }
    public static partial class BuildPlanningTools
    {
        private sealed class PlacementDetails
        {
            public string PrefabId,GeometryError;
            public int AnchorX,AnchorY,OriginX,OriginY,OriginCell,WorldId;
            public Orientation Orientation;
            public bool GeometryAvailable=true;
            public List<FootprintCell> Footprint=new List<FootprintCell>();
            public Dictionary<string,object> ToDictionary(){return new Dictionary<string,object>{["anchorX"]=AnchorX,["anchorY"]=AnchorY,["worldId"]=WorldId,["orientation"]=Orientation.ToString()};}
        }
        private sealed class FootprintCell
        {
            public int Cell,X,Y,WorldId;
            public bool Valid=true,Visible=true,InWorld=true;
            public Dictionary<string,object> ToDictionary(){return new Dictionary<string,object>{["cell"]=Cell,["x"]=X,["y"]=Y};}
        }
        private sealed class FootprintValidation
        {
            public bool Valid;
            public string Error;
            public List<Dictionary<string,object>> InvalidCells,Obstructions;
            public Dictionary<string,object> ToDictionary(PlacementDetails placement){return new Dictionary<string,object>{["valid"]=Valid,["obstructions"]=Obstructions,["invalidCells"]=InvalidCells,["placement"]=placement.ToDictionary()};}
            public static FootprintValidation Success(){return new FootprintValidation{Valid=true,InvalidCells=new List<Dictionary<string,object>>(),Obstructions=new List<Dictionary<string,object>>()};}
            public static FootprintValidation Invalid(string error,List<Dictionary<string,object>> invalid,List<Dictionary<string,object>> obstructions)
            {return new FootprintValidation{Error=error,InvalidCells=invalid,Obstructions=obstructions};}
        }
        private sealed class BridgeEndpointTarget{public int Cell;public ObjectLayer Layer;public string Role;}
        internal static readonly List<Constructable> FixtureBlueprints=new List<Constructable>();
        internal static readonly Dictionary<string,BuildingDef> FixtureDefs=new Dictionary<string,BuildingDef>();
        internal static readonly List<Tuple<int,ObjectLayer>> FixtureEndpoints=new List<Tuple<int,ObjectLayer>>();
        private static bool EqualsIgnoreCase(string a,string b){return string.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
        private static bool IsUtilityPrefab(string id){return id=="FixtureBridge"||id=="Wire";}
        private static bool IsLinearUtilityPrefab(string id){return id=="Wire";}
        private static List<ObjectLayer> UtilityLayersForPrefab(string id){return new List<ObjectLayer>{ObjectLayer.Wire};}
        private static BuildingDef ResolveBuildingDef(string id,out string resolved,out string error)
        {resolved=id;error=null;BuildingDef def;return FixtureDefs.TryGetValue(id,out def)?def:null;}
        private static bool IsNaturalDiggableSolidCell(int cell,int world){return Grid.Solid[cell]&&!Grid.Foundation[cell];}
        private static IEnumerable<Dictionary<string,object>> UprootableObstructionsAtCell(int cell,int world){return Enumerable.Empty<Dictionary<string,object>>();}
        private static IEnumerable<Constructable> FindConstructables(int world)
        {return FixtureBlueprints.Where(value=>!value.gameObject.Destroyed&&ToolUtil.GameObjectMatchesWorld(value.gameObject,world));}
        private static IEnumerable<BridgeEndpointTarget> NativeBridgeEndpointTargets(BuildingDef def,PlacementDetails placement)
        {return FixtureEndpoints.Select(value=>new BridgeEndpointTarget{Cell=value.Item1,Layer=value.Item2,Role="fixture_endpoint"});}
        private static void AddBackwallFoundationFailure(PlacementDetails placement,List<Dictionary<string,object>> obstructions){}
        private static bool IsDryRun(JObject args){return ToolUtil.GetBool(args,"dryRun")||ToolUtil.GetBool(args,"validateOnly");}
        private static bool GetBool(Dictionary<string,object> value,string key)
        {object raw;return value!=null&&value.TryGetValue(key,out raw)&&raw is bool&&(bool)raw;}
        private static PlacementDetails BuildPlacementDetails(BuildingDef def,int x,int y,int world,Orientation orientation=Orientation.Neutral)
        {return new PlacementDetails{PrefabId=def.PrefabID,AnchorX=x,AnchorY=y,OriginX=x,OriginY=y,OriginCell=Grid.XYToCell(x,y),WorldId=world,Orientation=orientation,
            Footprint=new List<FootprintCell>{new FootprintCell{Cell=Grid.XYToCell(x,y),X=x,Y=y,WorldId=world}}};}
        private static Dictionary<string,object> ErrorResult(string id,int x,int y,string error,Dictionary<string,object> details=null)
        {return new Dictionary<string,object>{["valid"]=false,["error"]=error,["details"]=details};}
        private static Dictionary<string,object> AnchorDictionary(int x,int y,int world)
        {return new Dictionary<string,object>{["x"]=x,["y"]=y,["worldId"]=world};}
        private static object ActualAnchorArray(Dictionary<string,object> actual)
        {return new[]{Convert.ToInt32(actual["derivedAnchorX"]),Convert.ToInt32(actual["derivedAnchorY"])};}
        internal static Dictionary<string,object> CompletionForTest(BuildingDef def,int cell,int world,Orientation orientation,JObject args)
        {return TryCompleteExistingVirtualFileBlueprint(def,BuildPlacementDetails(def,Grid.CellColumn(cell),Grid.CellRow(cell),world,orientation),args,new Dictionary<string,object>{["kind"]="blueprint"});}
        internal static List<Dictionary<string,object>> ObstructionsForTest(BuildingDef def,int cell,GameObject ignored)
        {return FindFootprintObstructions(BuildPlacementDetails(def,Grid.CellColumn(cell),Grid.CellRow(cell),0),ignored);}

    }
}

namespace OniMcp.Server { internal static class GameContextLifecycle {internal static int Generation=1;internal static int CaptureGeneration(){return Generation;}} }
