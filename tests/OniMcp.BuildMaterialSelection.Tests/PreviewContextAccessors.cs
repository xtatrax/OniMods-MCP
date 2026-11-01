using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        internal static object HostContextMaterialObject(BuildingDef def)
        {var value=SelectElements(def,"auto",0);value.RequiredKg=RequiredMaterialKg(def);return value;}
        internal static JObject HostContextMaterialJson(object value)
        {return JObject.Parse(JsonConvert.SerializeObject(value.GetType().GetMethod("ToDictionary").Invoke(value,null),OniMcp.Support.McpJsonUtil.Settings));}
        internal static object HostContextClone(object value){return value.GetType().GetMethod("CloneForPreview").Invoke(value,null);}
        internal static JObject HostContextFacadeRecord(BuildingDef def,string facade){return JObject.FromObject(ResolveFacade(def,facade));}
        internal static bool HostContextFacadeValid(BuildingDef def,string facade){return ResolveFacade(def,facade).Valid;}
#if OLD_FACADE_COMPARISON
        internal static void HostContextCachedMaterials(BuildingDef def){throw new NotSupportedException("unused old-facade comparison path");}
        internal static JObject HostContextReadiness(BuildingDef def){throw new NotSupportedException("unused old-facade comparison path");}
#else
        internal static void HostContextCachedMaterials(BuildingDef def)
        {
            var context=new PlacementPreviewReadContext(def,new JObject { ["dryRun"]=true,["worldId"]=0,["material"]="auto" },0);
            for(int i=0;i<64;i++){var value=context.Material();if(!value.Valid)throw new InvalidOperationException("actual cached material rejected");value.Elements[0]=new Tag("Changed");value.Selected.AvailableKg=999;}
            if(context.Material().Elements[0].Name!="Cuprite"||context.Material().Selected.AvailableKg!=100)throw new InvalidOperationException("real context material snapshots aliased");
        }
        internal static JObject HostContextReadiness(BuildingDef def)
        {
            var context=new PlacementPreviewReadContext(def,new JObject { ["dryRun"]=true,["worldId"]=0,["material"]="auto" },0);
            var value=context.Material();var json=JObject.Parse(JsonConvert.SerializeObject(value.ToDictionary(),OniMcp.Support.McpJsonUtil.Settings));
            json["readinessReason"]=context.MaterialReadinessReason==null?JValue.CreateNull():new JValue(context.MaterialReadinessReason);return json;
        }
#endif
        // Unused collaborators fail loudly. No planner/eligibility algorithm is
        // copied merely to compile this material-only real-source context host.
        private static bool IsDryRun(JObject args){throw new NotSupportedException("unused context identity collaborator");}
        private static Dictionary<string,object> ErrorResult(string id,int x,int y,string message,Dictionary<string,object> details)
        {throw new NotSupportedException("unused object-discovery collaborator");}
    }
    internal static partial class ToolUtil
    {
        internal static int ResolveWorldId(JObject args){throw new NotSupportedException("unused context matching collaborator");}
        internal static bool GameObjectMatchesWorld(UnityEngine.GameObject go,int world){throw new NotSupportedException("unused constructable world collaborator");}
        internal static string CleanName(string text){return text;}
    }
    internal static class BuildingAvailabilityReader
    {
        internal sealed class Observation {}
        internal static Observation Read(BuildingDef definition){throw new NotSupportedException("unused availability collaborator");}
    }
}
namespace UnityEngine
{
    public enum FindObjectsSortMode { None }
    public class Object
    {
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T:class
        {throw new NotSupportedException("unused native object enumeration boundary");}
    }
}
public sealed class BuildingComplete { public UnityEngine.GameObject gameObject; }
public sealed class Constructable { public UnityEngine.GameObject gameObject; }
internal static partial class Components
{
    internal static class BuildingCompletes { internal static readonly List<BuildingComplete> Items=new List<BuildingComplete>(); }
}
public class KleiItems
{
    public struct Inventory {public Dictionary<string,int> ItemsByType;}
    private static Inventory InventoryData;
    public static void Seed(bool ready){InventoryData=new Inventory {ItemsByType=ready?new Dictionary<string,int>():null};}
}
namespace Database {public enum PermitRarity {Common,Universal}}
public sealed class Facade {public string Id,Name;}
public sealed class FacadeSet
{
    public List<Facade> resources=new List<Facade>();
    public Facade TryGet(string id){return resources.Find(value=>value.Id==id);}
}
