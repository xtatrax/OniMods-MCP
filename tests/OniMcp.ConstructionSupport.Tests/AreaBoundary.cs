using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Core
{
    public sealed class McpToolParameter { public string Type,Description; public bool Required; }
    public sealed class McpTool
    {
        public string Name,Group,Mode,Risk,Description;public bool Hidden;public List<string> Aliases,Tags;
        public Dictionary<string,McpToolParameter> Parameters;
        public Func<JObject,CallToolResult> Handler;
    }

}
namespace OniMcp.Support
{
    internal static class McpLocalization { internal static string Get(string key)=>key; }
}
public static class Assets
{
    public static readonly Dictionary<string,BuildingDef> Definitions=new Dictionary<string,BuildingDef>();
    public static BuildingDef GetBuildingDef(string id)=>Definitions.TryGetValue(id,out var d)?d:null;
}
namespace OniMcp.Tools
{
    public static partial class ToolUtil
    {
        public static bool GetBool(JObject a,string k,bool f=false)=>a?[k]?.Type==JTokenType.Boolean?(bool)a[k]:f;
        public static int? GetInt(JObject a,string k)=>a?[k]?.Type==JTokenType.Integer?(int?)a[k]:null;
        public static int ResolveWorldId(JObject a)=>GetInt(a,"worldId")??0;
    }
    internal static class AreaFixture
    {
        internal static readonly HashSet<int> Blocked=new HashSet<int>(),DigOnly=new HashSet<int>(),ActualFailed=new HashSet<int>();
        internal static int Mutations;
        internal static void Reset()
        {
            Blocked.Clear();DigOnly.Clear();ActualFailed.Clear();Mutations=0;
            BuildPlanningTools.Blueprints.Clear();Assets.Definitions.Clear();BuildingDef.NativeResult=false;
            Array.Clear(Grid.Solid);Array.Clear(Grid.Foundation);Array.Clear(Grid.Objects);Array.Clear(Grid.WorldIdx);Array.Fill(Grid.Visible,true);
            Assets.Definitions["Tile"]=new BuildingDef{PrefabID="Tile",BuildLocationRule=BuildLocationRule.Tile,IsFoundation=true,TileLayer=ObjectLayer.FoundationTile};
            Assets.Definitions["Locker"]=new BuildingDef{PrefabID="Locker",BuildLocationRule=BuildLocationRule.OnFloor};
        }
    }
    public static partial class BuildPlanningTools
    {
        private sealed class CellCoord { internal int x,y;internal CellCoord(int x,int y){this.x=x;this.y=y;} }
        private sealed class Resolution
        {
            internal string PrefabId,Material,AnchorQuery;
            internal Dictionary<string,object> ToDictionary()=>new Dictionary<string,object>();
        }
        private sealed class AutoDigContext { internal bool LimitReached=false;internal static AutoDigContext FromArgs(JObject a)=>new AutoDigContext(); }
        private static Dictionary<string,McpToolParameter> BuildPlacementParameters(bool includeConfirm,bool includeArea)=>new Dictionary<string,McpToolParameter>();
        private static string ValidateLayerContextOptions(JObject a)=>null;
        private static Resolution ResolveBuildPlan(JObject a)=>new Resolution{PrefabId=(string)a["prefabId"],Material=(string)a["material"],AnchorQuery=null};
        private static BuildingDef ResolveBuildingDef(string p,out string resolved,out string error){resolved=p;error=null;return Assets.GetBuildingDef(p);}
        private static bool IsLinearUtilityPrefab(string p)=>false;
        private static bool TryBuildAutoConnectArgs(JObject a,string p,out JObject result){result=null;return false;}
        private static McpTool AutoConnectUtility()=>throw new NotSupportedException();
        private static List<CellCoord> ResolveAnchors(JObject a,BuildingDef d,out string error)
        { error=null;return ((JArray)a["anchors"]).Cast<JObject>().Select(v=>new CellCoord((int)v["x"],(int)v["y"])).ToList(); }
        private static object BuildAnchorResolution(JObject a,List<CellCoord> anchors)=>new Dictionary<string,object>();
        private static bool IsDryRun(JObject a)=>ToolUtil.GetBool(a,"dryRun");
        private static Orientation ParseOrientation(string s)=>string.IsNullOrEmpty(s)?Orientation.Neutral:(Orientation)Enum.Parse(typeof(Orientation),s);
        private static Dictionary<string,object> PlannedFootprintOverlap(BuildingDef d,int x,int y,int w,HashSet<int> footprints,Orientation o)
            =>footprints.Contains(Grid.XYToCell(x,y))?new Dictionary<string,object>{{"reasonCode","planned_footprint_overlap"}}:null;
        private static void ReservePlannedFootprint(BuildingDef d,int x,int y,int w,HashSet<int> footprints,Orientation o)=>footprints.Add(Grid.XYToCell(x,y));
        private static Dictionary<string,object> ErrorResult(string p,int x,int y,string error,Dictionary<string,object> details=null)
        {
            var r=new Dictionary<string,object>{{"prefabId",p},{"x",x},{"y",y},{"valid",false},{"planned",false},{"blueprintPlaced",false},{"error",error}};
            if(details!=null){r["details"]=details;if(details.TryGetValue("reasonCode",out object reason))r["reasonCode"]=reason;}return r;
        }
        private static bool IsAutoDiggableFailure(Dictionary<string,object> r)=>GetBool(r,"autoDiggable");
        private static Dictionary<string,object> CaptureLayerContext(BuildingDef d,JObject a,IEnumerable<CellCoord> anchors,IEnumerable<Dictionary<string,object>> previews)=>null;
        private static void AttachLayerContext(Dictionary<string,object> r,Dictionary<string,object> context){}
        private static bool EqualsIgnoreCase(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
        private static bool GetBool(Dictionary<string,object> d,string k)=>d.TryGetValue(k,out object v)&&v is bool b&&b;
        private static int GetInt(Dictionary<string,object> d,string k)=>d.TryGetValue(k,out object v)&&v is int i?i:0;
        private static int GetAutoDigInt(Dictionary<string,object> d,string k)=>0;
        private static bool IsAutoDigResult(Dictionary<string,object> d)=>GetBool(d,"autoDiggable");
        private static bool SameAnchor(Dictionary<string,object> d,CellCoord a)=>GetInt(d,"x")==a.x&&GetInt(d,"y")==a.y;
        private static Dictionary<string,object> AnchorDictionary(int x,int y,int w)=>new Dictionary<string,object>{{"x",x},{"y",y},{"worldId",w}};
        private static object BuildRemainingBuildAreaAction(JObject args,string prefab,List<CellCoord> anchors)=>new Dictionary<string,object>();
        private static bool IsSupportPrefab(string p)=>p=="Tile"||p=="AirflowTile";
        private static Dictionary<string,object> TryPlanOne(string p,int x,int y,JObject args,HashSet<int> supports=null,AutoDigContext dig=null)
            =>PlanOneWithoutLayerContext(p,x,y,args,supports,dig);
        private static Dictionary<string,object> PlanOneWithoutLayerContext(string p,int x,int y,JObject args,HashSet<int> supports=null,AutoDigContext dig=null)
        {
            int cell=Grid.XYToCell(x,y);
            if(AreaFixture.Blocked.Contains(cell))return ErrorResult(p,x,y,"fixture_material_or_collision");
            if(AreaFixture.DigOnly.Contains(cell)){var d=ErrorResult(p,x,y,"fixture_autodig");d["autoDiggable"]=true;return d;}
            var support=ValidateSupport(Assets.GetBuildingDef(p),x,y,false,supports,ParseOrientation((string)args["orientation"]));
            if(!support.Valid)return ErrorResult(p,x,y,support.Error,support.ToDictionary());
            bool preview=IsDryRun(args);
            if(!preview&&AreaFixture.ActualFailed.Contains(cell))return ErrorResult(p,x,y,"fixture_native_refusal");
            if(IsSupportPrefab(p))
            {
                if(preview)supports?.Add(cell);
                else Blueprints.Add(cell);
            }
            if(!preview)AreaFixture.Mutations++;
            return new Dictionary<string,object>{{"prefabId",p},{"x",x},{"y",y},{"valid",true},{"planned",!preview},
                {"blueprintPlaced",!preview},{"support",support.ToDictionary()},{"id",preview?0:100+cell}};
        }
    }
}
