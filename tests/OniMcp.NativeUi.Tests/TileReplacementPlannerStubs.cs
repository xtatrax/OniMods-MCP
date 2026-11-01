using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
#pragma warning disable CS0649

namespace OniMcp.Tools
{
    // Unrelated selection/catalog/planning services are explicit boundary substitutes.
    // The entire real TryPlanOne, replacement caller and native adapter remain linked.
    public static partial class BuildPlanningTools
    {
        private sealed class AutoDigContext {}
        private sealed class SupportValidation
        {
            public bool Valid=true;
            public string Error;
            public List<Dictionary<string,object>> MissingSupportCells=new List<Dictionary<string,object>>();
            public Dictionary<string,object> ToDictionary(){return new Dictionary<string,object>{["valid"]=Valid};}
        }

        private struct FacadeSelection {public bool Valid;public string Error,ResponseId,TryPlaceId;
            public string AssessmentState,ReasonCode;
            public Dictionary<string,object> ToDictionary(){return new Dictionary<string,object>{["valid"]=Valid,["status"]=AssessmentState=="unknown"?"unknown":Valid?"selected":"rejected",["assessmentState"]=AssessmentState??"known",["reasonCode"]=ReasonCode==null?(object)JValue.CreateNull():ReasonCode};}}
        internal static string FixtureAvailabilityError,FixtureFacadeUnknownReason;
        internal static bool FixtureMaterialValid=true,FixtureFacadeValid=true,FixtureSupportValid=true;
        internal static int FixtureSelectCalls,FixtureAutoDigCalls,FixtureFallbackCalls;
        internal static int FixtureSupportCalls,FixtureFacadeCalls;
        internal static float FixtureMaterialAvailableKg=100f;
        internal static void ResetPlannerFixture()
        {FixtureAvailabilityError=null;FixtureMaterialValid=true;FixtureFacadeValid=true;FixtureSupportValid=true;FixtureSelectCalls=0;FixtureAutoDigCalls=0;FixtureFallbackCalls=0;FixtureSupportCalls=0;}
        private static string BuildAvailabilityError(BuildingDef def,JObject args)
        {BuildingAvailabilityReader.Observation observed;return BuildAvailabilityError(def,args,out observed);}
        private static string BuildAvailabilityError(BuildingDef def,JObject args,out BuildingAvailabilityReader.Observation observed)
        {observed=BuildingAvailabilityReader.Read(def);return FixtureAvailabilityError;}
        private static string BuildAvailabilityErrorFromObservation(BuildingDef def,JObject args,BuildingAvailabilityReader.Observation observed){return FixtureAvailabilityError;}
        private static bool IsFreeBuildContext(){return DebugHandler.InstantBuildMode||(Game.Instance!=null&&Game.Instance.SandboxModeActive);}
        private static bool IsTechUnlocked(BuildingDef def){return FixtureAvailabilityError==null;}
        private static bool IsUnlockedAndAvailable(BuildingDef def){return FixtureAvailabilityError==null;}
        private static Orientation ParseOrientation(string value)
        {Orientation result;return value!=null&&Enum.TryParse(value,out result)?result:Orientation.Neutral;}
        private static Dictionary<string,object> UtilityTerrainConflict(BuildingDef def,IEnumerable<int> cells,int world,bool allow,int max,bool enabled){return null;}
        private static MaterialSelection SelectElements(BuildingDef def,string material,int world)
        {FixtureSelectCalls++;return new MaterialSelection{Valid=FixtureMaterialValid,Error=FixtureMaterialValid?null:"fixture material rejected",Elements=new List<Tag>{new Tag(material??"Cuprite")},Selected=new BuildMaterialInfo{Tag=new Tag(material??"Cuprite"),AvailableKg=FixtureMaterialAvailableKg,ValidForBuilding=FixtureMaterialValid}};}
        private static float RequiredMaterialKg(BuildingDef def){return def.Mass[0];}
        private static FacadeSelection ResolveFacade(BuildingDef def,string value)
        {FixtureFacadeCalls++;return new FacadeSelection{Valid=FixtureFacadeValid&&FixtureFacadeUnknownReason==null,AssessmentState=FixtureFacadeUnknownReason==null?"known":"unknown",ReasonCode=FixtureFacadeUnknownReason,Error=FixtureFacadeValid&&FixtureFacadeUnknownReason==null?null:"fixture facade rejected",ResponseId=value??"DEFAULT_FACADE",TryPlaceId=value};}
        private static SupportValidation ValidateSupport(BuildingDef def,int x,int y,bool allow,HashSet<int> planned,Orientation orientation)
        {FixtureSupportCalls++;return new SupportValidation{Valid=FixtureSupportValid,Error=FixtureSupportValid?null:"fixture support rejected"};}
        private static void RegisterSupportBlueprint(string id,int x,int y,HashSet<int> cells){}
        private static Dictionary<string,object> ExistingPlacementResult(BuildingDef def,PlacementDetails placement,Dictionary<string,object> existing,bool utility)
        {return new Dictionary<string,object>{["valid"]=true,["alreadyPresent"]=true,["existing"]=existing};}
        private static Dictionary<string,object> TryAutoDigObstructions(PlacementDetails placement,FootprintValidation validation,JObject args,AutoDigContext context)
        {FixtureAutoDigCalls++;return null;}
        private static Dictionary<string,object> TryAutoConnectPower(BuildingDef def,int x,int y,Orientation orientation,JObject args,HashSet<int> support,AutoDigContext dig){return null;}
        private static int GetInt(Dictionary<string,object> value,string key)
        {object raw;return value!=null&&value.TryGetValue(key,out raw)?Convert.ToInt32(raw):0;}
        private static Vector3 BuildPlacementPosition(int cell,BuildingDef def){return Grid.CellToPosCBC(cell,def.SceneLayer);}
        private static bool TryBuildVirtualFileInstantBuild(BuildingDef def,PlacementDetails placement,JObject args,int cell,Orientation orientation,IList<Tag> elements,string facade,out GameObject go,out Dictionary<string,object> details)
        {go=null;details=null;throw new InvalidOperationException("fixture instant placement forbidden for ordinary tile replacement");}
        private static Dictionary<string,object> InstantCompletionFailureResult(BuildingDef def,PlacementDetails placement,GameObject go,Dictionary<string,object> details,bool placedByThisRequest)
        {throw new InvalidOperationException("fixture instant failure fallback forbidden");}
        private static GameObject TryPlaceWithBuildTool(BuildingDef def,int cell,Orientation orientation,IList<Tag> elements,string facade,PlacementDetails placement,JObject args,out Dictionary<string,object> details)
        {FixtureFallbackCalls++;details=null;throw new InvalidOperationException("fixture ordinary fallback forbidden");}
        private static Dictionary<string,object> BuildPlacementFailureDetails(PlacementDetails placement,MaterialSelection material){return new Dictionary<string,object>();}
        private static void SetPriority(GameObject go,int value){throw new InvalidOperationException("fixture low-level object priority write forbidden");}
        private static IEnumerable<Tag> MaterialCategoryTags(BuildingDef def){return new[]{new Tag(def.PrefabID=="Tile"?"RawMinerals":"AllMetals")};}
        private static bool MaterialMatchesCategory(Tag tag,Tag category){return category.Name=="AllMetals"&&(tag.Name=="Cuprite"||tag.Name=="GoldAmalgam");}
        internal static JObject PlanForTest(string id,int cell,JObject args,bool wireSerialization=false)
        {
            var result = TryPlanOne(id,Grid.CellColumn(cell),Grid.CellRow(cell),args);
            return wireSerialization
                ? JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(result, OniMcp.Support.McpJsonUtil.Settings))
                : JObject.FromObject(result);
        }
    }
}

namespace OniMcp.Core {internal static class FixtureNamespace {}}
