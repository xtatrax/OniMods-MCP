using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static int contextChecks;
        private static void ContextCheck(bool value,string reason) {contextChecks++;if(!value)throw new InvalidOperationException("Preview request context: "+reason);}
        private static Dictionary<string,object> InvokeContextPlan(BuildingDef def,int x,int y,JObject args,PlacementPreviewReadContext context)
        {
            var method=typeof(BuildPlanningTools).GetMethods(BindingFlags.NonPublic|BindingFlags.Static).Single(value=>value.Name=="TryPlanOne");
            object[] all={def.PrefabID,x,y,args,null,null,context};
            return (Dictionary<string,object>)method.Invoke(null,all.Take(method.GetParameters().Length).ToArray());
        }
        internal static void RunPreviewContextRegression()
        {
            BlueprintCompletionRegression.Setup();DebugHandler.InstantBuildMode=false;WorldEditorTools.FixtureModeRequest=null;ResetPlannerFixture();
            var def=new BuildingDef{PrefabID="ContextFixture",Name="Context fixture",BuildLocationRule=BuildLocationRule.Anywhere};FixtureDefs[def.PrefabID]=def;
            def.BuildingComplete=new GameObject();def.BuildingComplete.Add(new LogicPorts());
            var args=new JObject{["dryRun"]=true,["worldId"]=0,["material"]="Cuprite"};
            FixtureFacadeCalls=0;FixtureUncachedDiscoveries=0;UnityEngine.Object.EnumerationCalls=0;
            var context=new PlacementPreviewReadContext(def,args,0);
            for(int cell=0;cell<64;cell++)
            {
                var result=InvokeContextPlan(def,cell%8,cell/8,(JObject)args.DeepClone(),context);
                ContextCheck(GetBool(result,"valid"),"full real parent preview remains valid for anchor "+cell);
            }
            ContextCheck(def.FixtureAvailabilityReads==1,"native availability captured once for full candidate window");
            ContextCheck(FixtureSelectCalls==1&&FixtureFacadeCalls==1,"material/facade collaborators each captured once");
            ContextCheck(FixtureSupportCalls==64,"per-anchor support is not cached as accepted");
            ContextCheck(UnityEngine.Object.EnumerationCalls==1&&FixtureUncachedDiscoveries==0,"constructables discovered once across actual matching/footprint/logic paths");
            ContextCheck(def.DirectBuildCalls==0&&def.TryPlaceCalls==0&&BuildTool.Instance.BuildCalls==0,"read context invokes no native placement/UI action");
            var first=context.Material();first.Elements[0]=new Tag("Changed");first.Selected.AvailableKg=999;
            var second=context.Material();ContextCheck(second.Elements[0].Name=="Cuprite"&&second.Selected.AvailableKg==100,"mutable material snapshots do not alias later anchors");
            var oldObjects=context.CompletedObjects().ToList();var extra=new GameObject();extra.Add(new BuildingComplete());Components.BuildingCompletes.Items.Add(extra.GetComponent<BuildingComplete>());
            ContextCheck(!context.CompletedObjects().Contains(extra)&&oldObjects.Count==0,"completed collection is a request-owned snapshot");Components.BuildingCompletes.Items.Clear();
            var changed=(JObject)args.DeepClone();changed["material"]="GoldAmalgam";
            InvokeContextPlan(def,2,3,changed,context);
            ContextCheck(def.FixtureAvailabilityReads==2&&FixtureSelectCalls==2,"argument mismatch reads fresh rather than reusing context");
            Game.Instance=new Game();InvokeContextPlan(def,2,3,args,context);
            ContextCheck(def.FixtureAvailabilityReads==3&&FixtureSelectCalls==3,"game identity mismatch reads fresh");
            var writing=(JObject)args.DeepClone();writing["dryRun"]=false;FixtureSupportValid=false;
            int previousDiscoveries=FixtureUncachedDiscoveries;
            InvokeContextPlan(def,2,3,writing,context);
            ContextCheck(FixtureUncachedDiscoveries>previousDiscoveries,"actual write retains fresh global object discovery");
            ContextCheck(def.FixtureAvailabilityReads==4&&FixtureSelectCalls==4&&FixtureFacadeCalls==4,"actual write request never reuses preview selections");
            ContextCheck(def.DirectBuildCalls==0&&def.TryPlaceCalls==0&&BuildTool.Instance.BuildCalls==0,"rejected write boundary avoids actual game mutation in host");
            FixtureSupportValid=true;UnityEngine.Object.ThrowEnumeration=true;
            var failing=new PlacementPreviewReadContext(def,args,0);int selects=FixtureSelectCalls;
            var unknown=InvokeContextPlan(def,2,3,args,failing);var details=(Dictionary<string,object>)unknown["details"];
            ContextCheck(!GetBool(unknown,"valid")&&(string)details["assessmentState"]=="unknown"&&(string)details["reasonCode"]=="constructable_snapshot_failed","discovery failure remains unknown rather than known-clear");
            ContextCheck(FixtureSelectCalls==selects,"unused selection is not eagerly evaluated after discovery failure");
            UnityEngine.Object.ThrowEnumeration=false;
            DebugHandler.InstantBuildMode=false;ClusterManager.Instance=null;
            var missingInventory=new PlacementPreviewReadContext(def,args,0);selects=FixtureSelectCalls;
            var materialUnknown=InvokeContextPlan(def,2,3,args,missingInventory);
            var wire=JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(materialUnknown,OniMcp.Support.McpJsonUtil.Settings));
            ContextCheck((string)wire["details"]["assessmentState"]=="unknown"&&(string)wire["details"]["reasonCode"]=="cluster_manager_missing","full parent retains explicit missing inventory assessment");
            ContextCheck(wire["details"]["selectedAvailableKg"]!=null&&wire["details"]["selectedAvailableKg"].Type==JTokenType.Null&&wire["details"]["satisfied"].Type==JTokenType.Null,"wire serialization preserves unknown stock/sufficiency as explicit null");
            ContextCheck(FixtureSelectCalls==selects,"missing preview inventory rejects before selection can invent zero stock");
            Console.WriteLine("Preview request context: "+contextChecks+" host checks PASS (full real parent/context; native selection/support inputs substituted)");
        }
    }
}
