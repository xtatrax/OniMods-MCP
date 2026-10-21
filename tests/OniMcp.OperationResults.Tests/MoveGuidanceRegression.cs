using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class MoveGuidanceRegression
{
    private static int checks;
    private static void Check(bool value,string message) { checks++;if(!value)throw new InvalidOperationException("Move guidance: "+message); }
    private static JObject Body(CallToolResult result) { Check(!result.IsError,"actual source call succeeds");return JObject.Parse(result.Content[0].Text); }
    private static MinionIdentity Dupe(int id,string name,bool navigator=false,bool monitor=false)
    {
        var d=new MinionIdentity{Name=name,Reference=new Dictionary<string,object>{{"id",id},{"name",name},{"worldId",3}}};
        if(navigator)d.gameObject.Components[typeof(Navigator)]=new Navigator{Monitor=monitor?new MoveToLocationMonitor.Instance():null};
        Components.LiveMinionIdentities.Items.Add(d);return d;
    }
    private static JObject Read(JObject args=null)=>Body(DuplicantTools.ControlDupes().Handler(args??new JObject{{"domain","side_screen"},{"action","direct_commands"}}));
    private static JObject Guide(JObject result)
    {
        Check(result["moveGuidance"] is JObject,"current public-route descriptor exists once at top level");
        var guide=(JObject)result["moveGuidance"];Check((string)guide["tool"]==WorldEditorTools.ControlWorldEditor().Name&&(string)guide["readCommand"]=="read"&&(string)guide["editCommand"]=="edit","guide resolves to actual public world_editor command metadata");
        Check((string)guide["previewScope"]=="syntax_and_capability_only"&&(bool)guide["taskRequired"]&&(bool)guide["preview"]["dryRun"]&&!(bool)guide["preview"]["confirm"]&&!(bool)guide["execute"]["dryRun"]&&(bool)guide["execute"]["confirm"],"preview and execution are explicit distinct outer policies");
        Check(!result.ToString().Contains("dupes_move_to")&&!result.ToString().Contains("dupes_move_batch_to"),"legacy unregistered movement names are not recommended");
        return guide;
    }
    private static void PeopleAndSelectors()
    {
        var z=Dupe(2,"Zulu",true,false);var a=Dupe(1,"Alpha",true,true);Dupe(3,"No Navigator");
        a.gameObject.Components[typeof(MinionResume)]=new MinionResume{AvailableSkillpoints=4};
        var eq=new Equipment();var suit=new Equippable{slotID="Suit",Name="Cached Suit",Assigned=true,isEquipped=true,Payload=new Dictionary<string,object>{{"marker","equipped input"}},assignee=new Assignee{Name="Alpha"}};
        eq.Slots.Add(new EquipmentSlot{slot=new SlotDescriptor{Id="Suit",Name="Cached Slot"},assignable=suit,Assigned=true});eq.Slots.Add(null);a.assignableProxy.Object=new NativeDupeObject();a.assignableProxy.Object.Components[typeof(Equipment)]=eq;
        Components.AssignableItems.Items.Add(suit);Components.AssignableItems.Items.Add(new Assignable{slotID="Bed",Name="Cached Bed",Assigned=true,Payload=new Dictionary<string,object>{{"marker","assigned input"}},assignee=new Assignee{Name="ALPHA"}});Components.AssignableItems.Items.Add(new Assignable{slotID="Bed",Name="Other",Assigned=true,Payload=new Dictionary<string,object>{{"marker","other input"}},assignee=new Assignee{Name="Other"}});
        var result=Read();Guide(result);var rows=(JArray)result["dupes"];
        Check((int)result["returned"]==3&&rows.Select(x=>(string)x["dupe"]["name"]).SequenceEqual(new[]{"Alpha","No Navigator","Zulu"}),"existing people sort and returned count preserved");
        var alpha=(JObject)rows[0];Check((bool)alpha["canMoveTo"]&&!(bool)rows[1]["canMoveTo"]&&!(bool)rows[2]["canMoveTo"]&&(int)alpha["availableSkillPoints"]==4,"canMoveTo still requires both cached navigator and monitor; skill data preserved");
        Check((string)alpha["equipmentSlots"][0]["slotId"]=="Suit"&&(bool)alpha["equipmentSlots"][0]["assigned"]&&(bool)alpha["equipmentSlots"][0]["isEquipped"]&&(string)alpha["equipmentSlots"][0]["assignable"]["marker"]=="equipped input","actual equipment projection retains native cached slot payload");
        Check(((JArray)alpha["assignedObjects"]).Count==2&&(string)alpha["assignedObjects"][0]["marker"]=="assigned input","actual assigned-object selector keeps case-insensitive owner and slot order");
        Check(rows.All(x=>(string)x["recommendedLookup"]["move"]=="#/moveGuidance"&&x["moveGuidance"]==null),"per-person movement lookup references one shared descriptor");
        Check(((JArray)result["tools"]).Skip(2).Select(x=>(string)x).SequenceEqual(new[]{"dupes_skill_control","assignable_control","dupes_control domain=side_screen action=equipment","colony_control domain=management kind=diet","colony_control domain=management kind=schedule"}),"unrelated existing tools guidance retained");
        foreach(string selector in new[]{"id","name"})
        {
            MoveGuidanceBoundary.Resolver=arg=>z;var args=new JObject{{"domain","side_screen"},{"action","direct_commands"},{"limit",1}};args[selector]=selector=="id"?(JToken)2:(JToken)"Zulu";result=Read(args);
            Check((int)result["returned"]==1&&(string)result["dupes"][0]["dupe"]["name"]=="Zulu"&&JToken.DeepEquals(MoveGuidanceBoundary.SelectorArguments,args),"source forwards complete "+selector+" selector to native lookup and uses its exact result");
        }
        MoveGuidanceBoundary.Resolver=null;
        result=Read(new JObject{{"domain","side_screen"},{"action","direct_commands"},{"limit",0}});Check((int)result["returned"]==1,"lower limit clamp retained");
        Components.LiveMinionIdentities.Items.Clear();result=Read();Guide(result);Check((int)result["returned"]==0&&((JArray)result["dupes"]).Count==0,"zero people still returns readable guidance");
        for(int i=0;i<501;i++)Dupe(i,"Person "+i.ToString("D3"));result=Read();Check((int)result["returned"]==100,"default people limit retained");result=Read(new JObject{{"domain","side_screen"},{"action","direct_commands"},{"limit",999}});Check((int)result["returned"]==500,"upper people limit retained");Components.LiveMinionIdentities.Items.Clear();Components.AssignableItems.Items.Clear();
    }
    private static JObject EditPreview(JObject guide,string kind,string line,bool dryRun=true,bool confirm=false)
    {
        var args=new JObject{{"command",guide["editCommand"]},{"path",guide[kind]["path"]},{"content",((string)guide["contentTemplate"]).Replace("<commandLine>",line)},{"task","Host guidance syntax preview"},{"dryRun",dryRun},{"confirm",confirm}};
        string before=args.ToString(Formatting.None);var response=WorldEditorTools.ControlWorldEditor().Handler(args);var body=Body(response);
        Check((bool)body["preview"]&&(string)body["route"]=="search_replace"&&(int)body["applied"]==0&&!(bool)body["changed"]&&args.ToString(Formatting.None)==before,"actual public edit performs only SEARCH/REPLACE preflight and preserves caller args");
        var compiled=JObject.Parse((string)body["details"][0]["result"]);var command=(JObject)compiled["commands"][0];Check((string)command["tool"]==(string)guide[kind]["operation"],"actual operation file resolves guide route "+kind);return (JObject)command["arguments"];
    }
    private static void TemplatesReachRealParser()
    {
        var guide=Guide(Read());
        foreach(string kind in new[]{"single","batch"})
        {
            string relative=((string)guide[kind]["path"]).Substring("/active/".Length);var file=WorldEditorTools.ReadOperationForTest(relative);Check(!file.IsError&&file.Content[0].Text.Contains((string)guide[kind]["operation"]),"guide virtual read path has actual operation metadata "+kind);
        }
        foreach(var query in new[]{new{Token="簡易ベッド",Value="簡易ベッド"},new{Token="\"確認 ベッド\"",Value="確認 ベッド"}})
        {
            string single=((string)guide["single"]["lineTemplate"]).Replace("<dupeId>","7").Replace("<worldId>","3").Replace("<targetQueryToken>",query.Token);
            foreach(var gate in new[]{new[]{true,false},new[]{true,true},new[]{false,false}})
            {
                var args=EditPreview(guide,"single",single,gate[0],gate[1]);Check((string)args["domain"]=="command"&&(string)args["action"]=="move_to"&&(int)args["id"]==7&&(string)args["query"]==query.Value&&(int)args["worldId"]==3&&(bool)args["dryRun"]==gate[0]&&(bool)args["confirm"]==gate[1],"ordinary Japanese or simply quoted space token and outer policy survive actual parser");
            }
        }
        string items=((string)guide["batch"]["itemsTemplate"]).Replace("<dupeId>","7").Replace("<x>","10").Replace("<y>","20").Replace("<worldId>","3");string batch=((string)guide["batch"]["lineTemplate"]).Replace("<itemsJson>",items);var wrapper=EditPreview(guide,"batch",batch);
        Check((string)wrapper["targetTool"]=="dupes_control"&&wrapper["payload"].Type==JTokenType.String,"batch parser preserves existing object-payload string envelope");
        var read=typeof(CoordinateControlTools).GetMethod("ReadPayload",BindingFlags.Static|BindingFlags.NonPublic);var merge=typeof(CoordinateControlTools).GetMethod("MergeCoordinateFields",BindingFlags.Static|BindingFlags.NonPublic);Check(read!=null&&merge!=null,"actual coordinate read/merge helpers available");
        var typed=(JObject)read.Invoke(null,new object[]{wrapper});Check(typed["items"].Type==JTokenType.Array&&(int)typed["items"][0]["id"]==7&&(int)typed["items"][0]["x"]==10&&(int)typed["items"][0]["y"]==20&&(int)typed["items"][0]["worldId"]==3&&(string)typed["domain"]=="command"&&(string)typed["action"]=="move_batch_to","actual coordinate helper restores typed items and target action without dispatch");
        string error;Check(CoordinateForwardingSafety.TryValidate(wrapper,typed,out error),"outer preview with child confirmation is compatible");merge.Invoke(null,new object[]{wrapper,typed});Check((bool)typed["dryRun"]&&!(bool)typed["confirm"]&&typed["items"].Type==JTokenType.Array,"actual coordinate field merge preserves parent preview and typed items");
        Check(!CoordinateForwardingSafety.TryValidate(new JObject{{"confirm",true}},new JObject{{"confirm",false}},out error)&&!CoordinateForwardingSafety.TryValidate(new JObject{{"dryRun",false}},new JObject{{"dryRun",true}},out error),"actual safety helper rejects child refusal or preview being overridden");
        var rejected=WorldEditorTools.ApplyOperationForTest(new JObject{{"task","Host rejected coordinate"},{"dryRun",true},{"confirm",false}},"ops/dupes.md","call domain=command action=move_to id=7 x=10 y=20 confirm=true");Check(rejected.IsError,"raw coordinates are not advertised or accepted through ordinary dupes operation");
        Check(MoveGuidanceBoundary.OperationDispatches==0&&MoveGuidanceBoundary.MoveDispatches==0&&NativeBoundary.Calls==0,"guidance, public previews and coordinate read helpers dispatch no native movement");
    }
    private static void LocaleRead()
    {
        var provider=McpLocalization.LanguageProvider;var game=McpLocalization.GameLanguageProvider;var descriptions=new List<string>();var instructions=new List<string>();
        try
        {
            foreach(string language in new[]{"en","ja","zh-Hans"})
            {
                string selected=language;McpLocalization.LanguageProvider=()=>selected;McpLocalization.GameLanguageProvider=()=>"en";var tool=DuplicantTools.ListDirectCommands();var guide=Guide(Read());
                Check(tool.Description==McpLocalization.Get("dupe_move_guidance.description")&&tool.Parameters["id"].Description==McpLocalization.Get("dupe_move_guidance.id")&&tool.Parameters["name"].Description==McpLocalization.Get("dupe_move_guidance.name")&&tool.Parameters["limit"].Description==McpLocalization.Get("dupe_move_guidance.limit")&&(string)guide["instructions"]==McpLocalization.Get("dupe_move_guidance.instructions"),"actual factory and guide use all five selected runtime entries "+language);
                Check(!tool.Description.StartsWith("dupe_move_guidance.",StringComparison.Ordinal)&&!string.IsNullOrWhiteSpace((string)guide["instructions"]),"localized metadata does not expose lookup keys "+language);descriptions.Add(tool.Description);instructions.Add((string)guide["instructions"]);
            }
            Check(descriptions.Distinct().Count()==3&&instructions.Distinct().Count()==3,"all three runtime catalogs are embedded and independently selected");
        }
        finally{McpLocalization.LanguageProvider=provider;McpLocalization.GameLanguageProvider=game;}
    }
    internal static void Run()
    {
        var old=Components.LiveMinionIdentities.Items.ToArray();var assigned=Components.AssignableItems.Items.ToArray();bool enabled=MoveGuidanceBoundary.Enabled;var resolver=MoveGuidanceBoundary.Resolver;MoveGuidanceBoundary.Enabled=true;MoveGuidanceBoundary.OperationDispatches=0;MoveGuidanceBoundary.MoveDispatches=0;NativeBoundary.Reset("unused",false);Components.LiveMinionIdentities.Items.Clear();Components.AssignableItems.Items.Clear();
        try{PeopleAndSelectors();TemplatesReachRealParser();LocaleRead();Check(MoveGuidanceBoundary.OperationDispatches==0&&MoveGuidanceBoundary.MoveDispatches==0,"all read guidance paths preserve zero movement dispatch");Console.WriteLine("Move guidance: "+checks+" checks passed (actual factories, dispatchers, public edit and parser; native inputs substituted)");}
        finally{Components.LiveMinionIdentities.Items.Clear();Components.LiveMinionIdentities.Items.AddRange(old);Components.AssignableItems.Items.Clear();Components.AssignableItems.Items.AddRange(assigned);MoveGuidanceBoundary.Enabled=enabled;MoveGuidanceBoundary.Resolver=resolver;}
    }
}
