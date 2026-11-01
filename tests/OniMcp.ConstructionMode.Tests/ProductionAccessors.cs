using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
namespace OniMcp.Tools
{
 internal static partial class ToolUtil {internal static int ResolveWorldId(JObject args){return (int?)args["worldId"]??0;}internal static bool GameObjectMatchesWorld(UnityEngine.GameObject go,int world){return true;}internal static string CleanName(string text){return text;}internal static float SafeFloat(float value){return float.IsNaN(value)||float.IsInfinity(value)?0:value;}internal static bool GetBool(JObject args,string name,bool fallback){return args?[name]?.Value<bool?>()??fallback;}}
 internal static class BuildingControlTools {internal static bool IsVirtualFileEditContext {get{return false;}}}

 public static partial class BuildPlanningTools
 {
  internal static bool HostFreeContext(){return IsFreeBuildContext();}
  private static bool IsDryRun(JObject args){return ToolUtil.GetBool(args,"dryRun",false);}
  private static Dictionary<string,object> ErrorResult(string id,int x,int y,string message,Dictionary<string,object> details){return details;}
#if OLD_FACADE_COMPARISON
  internal static void HostCachedMaterial(BuildingDef def){throw new InvalidOperationException("not used by negative facade case");}
#else
  internal static void HostCachedMaterial(BuildingDef def)
  {
   var args=new JObject{["dryRun"]=true,["worldId"]=0,["material"]="auto"};
   var context=new PlacementPreviewReadContext(def,args,0);
   for(int i=0;i<64;i++) {var value=context.Material();if(!value.Valid)throw new InvalidOperationException("real cached selection rejected");value.Elements[0]=new Tag("modified clone");value.Selected.AvailableKg=999;}
   if(context.Material().Elements[0].Name!="Cuprite"||context.Material().Selected.AvailableKg!=100)throw new InvalidOperationException("real cached material aliased response");
  }
#endif
#if OLD_FACADE_COMPARISON
  internal static JObject HostMaterialReadiness(BuildingDef def){throw new InvalidOperationException("not used by negative facade case");}
#else
  internal static JObject HostMaterialReadiness(BuildingDef def)
  {
   var context=new PlacementPreviewReadContext(def,new JObject{["dryRun"]=true,["worldId"]=0,["material"]="auto"},0);
   var value=context.Material();var output=JObject.FromObject(value.ToDictionary());
   output["readinessReason"]=context.MaterialReadinessReason==null?JValue.CreateNull():new JValue(context.MaterialReadinessReason);
   return output;
  }
#endif
  private static bool EqualsIgnoreCase(string a,string b){return string.Equals(a,b,StringComparison.OrdinalIgnoreCase);}
  private static bool Contains(string a,string b){return a!=null&&a.IndexOf(b,StringComparison.OrdinalIgnoreCase)>=0;}
  private static object BuildDefPlacementToDictionary(BuildingDef def){return null;}
  private static bool IsExactConnectionUtilityPrefab(string id){return false;}
  internal static object HostMaterial(BuildingDef def,string requested,int world){var result=SelectElements(def,requested,world);result.RequiredKg=RequiredMaterialKg(def);return result;}
  internal static JObject HostMaterialJson(object value){return JObject.FromObject(value.GetType().GetMethod("ToDictionary").Invoke(value,null));}
  internal static object HostClone(object value){return value.GetType().GetMethod("CloneForPreview").Invoke(value,null);}
  internal static bool HostFacadeValid(BuildingDef def,string value){return ResolveFacade(def,value).Valid;}
 }
}
