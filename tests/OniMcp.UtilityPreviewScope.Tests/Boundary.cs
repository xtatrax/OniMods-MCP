using Newtonsoft.Json.Linq;using OniMcp.Core;using UnityEngine;
namespace OniMcp.Tools {
 internal static class ToolUtil{internal static bool GetBool(JObject a,string k,bool d=false)=>a?[k]?.Type==JTokenType.Boolean?a[k].Value<bool>():d;internal static int? GetInt(JObject a,string k)=>(int?)a?[k];internal static int ResolveWorldId(JObject a)=>(int?)a?["worldId"]??0;internal static bool CellMatchesWorld(int c,int w)=>w==0&&Grid.IsValidCell(c);}
 public static partial class BuildPlanningTools {
  private struct CellCoord{internal int x,y;internal CellCoord(int a,int b){x=a;y=b;}}
  private sealed class AutoDigContext{internal bool LimitReached=false;internal static AutoDigContext FromArgs(JObject a)=>new AutoDigContext();}
  private sealed class Safety{internal bool Valid=true,HasRepeatedCells=false,RequiresCellFallback=false;internal int ExistingSamePrefabCells=0;}
  private sealed class Material{internal bool Valid=true;internal float RequiredKg;internal object Elements=new object();internal Dictionary<string,object> ToDictionary()=>new Dictionary<string,object>{{"valid",Valid}};}
  private sealed class Facade{internal bool Valid=true;internal string ResponseId="default",Error=null;}
  internal static int TerrainCalls,SafetyCalls,RefreshCalls,PlanCalls,ModeResolveCalls,ModeAvailabilityCalls;internal static bool FailPlan,PathConflict,PlanConflict,TerrainBlocked,FallbackConflict;
  internal static BuildingDef Definition=new BuildingDef{PrefabID="Wire"};
  internal static void Reset(){TerrainCalls=SafetyCalls=RefreshCalls=PlanCalls=ModeResolveCalls=ModeAvailabilityCalls=0;DebugHandler.InstantBuildMode=false;WorldEditorTools.FixtureRequest=null;FailPlan=PathConflict=PlanConflict=TerrainBlocked=FallbackConflict=false;NativeTool.Calls=0;WireBuildTool.Instance=new NativeTool();UtilityBuildTool.Instance=new NativeTool();Grid.Objects=new GameObject[100,2];}
  private static List<CellCoord> Path()=>new List<CellCoord>{new CellCoord(2,2),new CellCoord(3,2),new CellCoord(4,2)};
  internal static Dictionary<string,object> NativeForTest(JObject a)=>TryPlaceUtilityPathNative(Definition,Path(),a);
  internal static void SeedPath(){foreach(var p in Path()){var go=new GameObject();go.Add(new Building{Def=Definition});Grid.Objects[Grid.XYToCell(p.x,p.y),0]=go;}}
  private static bool IsDryRun(JObject a)=>ToolUtil.GetBool(a,"dryRun")||ToolUtil.GetBool(a,"validateOnly");
  private static bool EqualsIgnoreCase(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
  private static string ValidateLayerContextOptions(JObject a)=>null;
  private static string DefaultUtilityPrefab(string a)=>"Wire";
  private static BuildingDef ResolveBuildingDef(string p,out string id,out string error){ModeResolveCalls++;id="Wire";error=null;return Definition;}
  private static string BuildAvailabilityError(BuildingDef d,JObject a){ModeAvailabilityCalls++;return null;}
  private static bool IsLinearUtilityPrefab(string p)=>true;
  private static List<CellCoord> ResolveUtilityPath(JObject a,int max,out string e){e=null;return Path();}
  private static object CaptureLayerContext(BuildingDef d,JObject a,List<CellCoord> path)=>new object();
  private static Dictionary<string,object> AttachLayerContext(Dictionary<string,object> r,object c)=>r;
  private static Dictionary<string,object> UtilityTerrainConflict(BuildingDef d,IEnumerable<int> cells,int w,bool consent,int budget,bool autoDig){TerrainCalls++;return TerrainBlocked?new Dictionary<string,object>{{"allowed",false},{"success",false},{"reasonCode","utility_terrain_dig_requires_consent"}}:null;}
  private static Safety ValidateUtilityPathSafety(BuildingDef d,List<CellCoord> p,int w){SafetyCalls++;return new Safety{Valid=!PathConflict&&!(FallbackConflict&&SafetyCalls>=2)};}
  private static Dictionary<string,object> UtilityPathConflictResult(BuildingDef d,List<CellCoord> p,Safety s,string phase)=>new Dictionary<string,object>{{"reasonCode","utility_path_conflict"},{"success",false},{"phase",phase}};
  private static bool IsFreeBuildContext()=>false;
  private static Material SelectElements(BuildingDef d,string material,int world)=>new Material();
  private static float RequiredMaterialKg(BuildingDef d)=>100f;
  private static Facade ResolveFacade(BuildingDef d,string facade)=>new Facade();
  private static Vector3 BuildPlacementPosition(int cell,BuildingDef d)=>new Vector3{Cell=cell};
  private static bool RefreshAndValidateUtilityPathNetwork(BuildingDef d,List<CellCoord> p,out string error){RefreshCalls++;error=null;return true;}
  private static bool IsCompletedUtilityPath(BuildingDef d,List<CellCoord> p)=>true;
  private static bool IsPhysicalUtilityPathConnected(BuildingDef d,List<CellCoord> p)=>true;
  private static IEnumerable<int> UtilityLayersForPrefab(string p)=>new[]{0};
  private static List<Dictionary<string,object>> BuildPathSegments(List<CellCoord> p)=>new List<Dictionary<string,object>>();
  private static bool GetBool(Dictionary<string,object> d,string k)=>d!=null&&d.TryGetValue(k,out object v)&&v is bool b&&b;
  private static Dictionary<string,object> PlanOneWithoutLayerContext(string p,int x,int y,JObject a,HashSet<int> s,AutoDigContext c){PlanCalls++;return new Dictionary<string,object>{{"valid",!FailPlan},{"planned",false},{"alreadyPresent",false},{"alreadyConnected",false},{"reasonCode",PlanConflict?"placement_conflict":null}};}
  private static Dictionary<string,object> TryPlanOne(string p,int x,int y,JObject a,HashSet<int> s,AutoDigContext c)=>PlanOneWithoutLayerContext(p,x,y,a,s,c);
  private static int GetAutoDigInt(Dictionary<string,object> d,string k)=>0;
  private static bool IsAutoDigResult(Dictionary<string,object> d)=>false;
 }
}
namespace UnityEngine{public struct Vector3{public int Cell;}public sealed class GameObject{private readonly List<object> items=new List<object>();public string name="fixture";public void Add(object o)=>items.Add(o);public T GetComponent<T>()where T:class=>items.OfType<T>().FirstOrDefault();}}
public sealed class BuildingDef{public string PrefabID;public int SceneLayer=0;}
public sealed class Building{public BuildingDef Def;}
public sealed class KPrefabID{public Tag PrefabTag;}
public struct Tag{public string Name;}
public static class Grid{public const int WidthInCells=10,HeightInCells=10;public static GameObject[,] Objects=new GameObject[100,2];public static int XYToCell(int x,int y)=>y*10+x;public static bool IsValidCell(int c)=>c>=0&&c<100;}
public sealed class NativeTool{public static int Calls;public void Activate(BuildingDef d,object e,string f){Calls++;}public void OnLeftClickDown(Vector3 p){Calls++;}public void OnDragTool(int c,int i){Calls++;}public void OnMouseMove(Vector3 p){Calls++;}public void OnLeftClickUp(Vector3 p){Calls++;}}
public static class WireBuildTool{public static object Instance;}
public static class UtilityBuildTool{public static object Instance;}
