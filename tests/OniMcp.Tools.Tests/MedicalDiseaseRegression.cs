using System;
using System.Collections.Generic;
using System.Linq;
using Klei.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;

internal static class MedicalDiseaseRegression
{
    private static int checks, dbBefore;
    private sealed class Fixture { internal GameObject Go; internal MinionIdentity Dupe; internal Health Health; }
    private static void Check(bool ok,string message) { checks++; if (!ok) throw new InvalidOperationException("Medical disease: " + message); }
    private static void Null(JToken token,string message) { Check(token != null && token.Type == JTokenType.Null,message+" explicit null"); }
    private static JObject Json(object value) { return JObject.Parse(JsonConvert.SerializeObject(value,McpJsonUtil.Settings)); }
    private static void Reset()
    {
        Components.LiveMinionIdentities.Items.Clear(); Components.AssignableItems.Items.Clear(); Components.Clinics.Items.Clear();
        Strings.Entries.Clear(); Strings.EntryReader = null; Strings.TryGetReads = Strings.GetReads = 0; MedicalBoundary.Reset();
        Db.Current = new Db();
    }
    private static Fixture Patient(string name="Unknown",int id=1,int world=0,Sicknesses sicknesses=null)
    {
        var go = new GameObject { NativeName=name,WorldId=world,ExistingSicknesses=sicknesses };
        var f=new Fixture { Go=go,Dupe=new MinionIdentity { gameObject=go },Health=new Health() };
        go.ExistingComponents[typeof(KPrefabID)]=new KPrefabID { gameObject=go,InstanceID=id,PrefabTag=new Tag("Minion") };
        go.ExistingComponents[typeof(Health)]=f.Health; go.ExistingComponents[typeof(MinionIdentity)]=f.Dupe;
        Components.LiveMinionIdentities.Items.Add(f.Dupe); return f;
    }
    private static SicknessInstance Illness(int id=123,string name="Cached illness",string symptoms="Cached symptoms")
    {
        string key="illness."+id; var def=new MedicalFixtureSickness(new HashedString(id),new StringKey(key+".name"),new StringKey(key+".symptoms"));
        Strings.Entries[key+".name"]=new StringEntry { String=name }; Strings.Entries[key+".symptoms"]=new StringEntry { String=symptoms };
        return new SicknessInstance { ExistingSickness=def,ExistingExposure=new SicknessExposureInfo { sicknessID="native-id",sourceInfo="Cached exposure" } };
    }
    private static Sicknesses List(params SicknessInstance[] items) { return new Sicknesses { ExistingList=items.ToList() }; }
    private static MedicalSicknessReader.Observation Read(GameObject go)
    {
        MedicalBoundary.InRead=true; try { return MedicalSicknessReader.Read(go); } finally { MedicalBoundary.InRead=false; }
    }
    private static JObject Route(JObject args=null,bool management=true)
    {
        var request=args == null ? new JObject() : new JObject(args);
        if (management) request["domain"]="medical";
        request["action"]="patients";
        MedicalBoundary.InRead=true;
        CallToolResult result;
        try { result=management ? ManagementTools.ControlManagement().Handler(request) : MedicalTools.ControlMedical().Handler(request); }
        finally { MedicalBoundary.InRead=false; }
        Check(!result.IsError,"actual management/medical patients route succeeds");
        return JObject.Parse(result.Content[0].Text);
    }
    private static void NoMutation(string label)
    {
        Check(MedicalBoundary.ForbiddenCalls==0,label+" no factory/cure/formatter/unrelated route");
        Check(Db.Reads==dbBefore,label+" empty bed registry and absent legacy Amounts avoid Db.Get");
        Check(MedicalBoundary.NameReads==0 && Strings.GetReads==0,label+" no native text formatter fallback");
    }
    private static void DefaultUnknown()
    {
        Reset(); Patient(); var response=Route();
        Check((int)response["returned"]==1,"default retains disease-unknown patient");
        var row=response["patients"][0]; Null(row["sicknesses"],"missing list"); Null(row["hasSickness"],"missing disease predicate"); Null(row["needsMedicalAttention"],"missing clinical decision");
        Check((string)row["sicknessRead"]["status"]=="unknown" && (string)row["sicknessRead"]["reasonCode"]=="sicknesses_missing","missing provenance distinct from healthy");
        foreach (string key in new[] { "totalEntries","observedEntries","unknownEntries" }) Null(row["sicknessRead"][key],"missing "+key);
        Check((bool)response["includeHealthy"]==false,"default healthy filter retained"); Null(response["worldId"],"existing allworld response"); NoMutation("default");
    }
    private static void EmptyAndPositive()
    {
        Reset(); var f=Patient(sicknesses:List());var empty=Read(f.Go);var meta=Json(empty.Metadata());
        Check(empty.HasSickness==false && empty.Items.Count==0 && empty.TotalEntries==0 && empty.ObservedEntries==0 && empty.UnknownEntries==0,"observed zero is healthy empty");
        Check((string)meta["status"]=="observed" && meta["reasonCode"].Type==JTokenType.Null,"empty observation not unavailable");
        Check((int)Route()["returned"]==0,"default excludes observed healthy");
        var all=Route(new JObject { ["includeHealthy"]=true });var row=all["patients"][0];
        Check(row["sicknesses"].Type==JTokenType.Array && !row["sicknesses"].Any() && (bool)row["hasSickness"]==false && (bool)row["needsMedicalAttention"]==false,"true empty and false survive actual serializer");
        Reset(); var illness=Illness();var list=List(illness);f=Patient(sicknesses:list);var observed=Read(f.Go);
        Check(observed.HasSickness==true && observed.TotalEntries==1 && observed.ObservedEntries==1 && observed.UnknownEntries==0,"one native disease positive");
        var body=Json(observed.Metadata());Check((string)body["status"]=="observed" && body["reasonCode"].Type==JTokenType.Null,"known observation complete");
        var item=observed.Items[0];Check(item["id"] is HashedString && ((HashedString)item["id"]).HashValue==123,"raw native ID type retained");
        Check((string)item["name"]=="Cached illness" && (string)item["descriptiveSymptoms"]=="Cached symptoms" && (string)item["exposure"]=="Cached exposure","cached localized text and exposure preserved");
        var idsBefore=list.ExistingList.ToArray();var stringsBefore=Strings.Entries.ToDictionary(x=>x.Key,x=>x.Value);var a=Json(Route()["patients"][0]);var b=Json(Route()["patients"][0]);
        Check(JToken.DeepEquals(a,b) && ReferenceEquals(list,f.Go.ExistingSicknesses) && list.ExistingList.SequenceEqual(idsBefore) && stringsBefore.Count==Strings.Entries.Count && stringsBefore.All(x=>ReferenceEquals(x.Value,Strings.Entries[x.Key])),"repeat keeps source list/cache/rows unchanged");
        foreach (string source in new string[] { null,"" })
        {
            illness.ExistingExposure=new SicknessExposureInfo { sourceInfo=source };var current=Read(f.Go);Check(current.HasSickness==true && current.Status=="observed" && current.UnavailableFields.Count==0,"null/empty exposure is normal observed value");
            if (source==null) Null(JToken.FromObject(current.Items)[0]["exposure"],"native null exposure");else Check((string)current.Items[0]["exposure"]=="","native empty exposure retained");
        }
        NoMutation("empty and positive");
    }
    private static void UnknownAndPartial()
    {
        Reset();var missing=Read(null);Check(missing.Items==null && missing.HasSickness==null && missing.ReasonCode=="target_missing","missing target not fabricated entry");
        var f=Patient();
        foreach (string reason in new[] { "sicknesses_missing","sicknesses_read_failed","sickness_list_missing","sickness_list_read_failed" })
        {
            f.Go.SicknessReader=null;f.Go.ExistingSicknesses=null;
            if (reason=="sicknesses_read_failed") f.Go.SicknessReader=()=>{throw new Exception();};
            if (reason.StartsWith("sickness_list")) f.Go.ExistingSicknesses=new Sicknesses { ExistingList=null };
            if (reason=="sickness_list_read_failed") f.Go.ExistingSicknesses.ListReader=()=>{throw new Exception();};
            var x=Read(f.Go);Check(x.Items==null && x.HasSickness==null && x.Status=="unknown" && x.ReasonCode==reason && x.TotalEntries==null && x.ObservedEntries==null && x.UnknownEntries==null,reason+" remains unavailable not zero");
        }
        f.Go.SicknessReader=null;
        foreach (string kind in new[] { "null_instance","missing_definition","definition_fault" })
        {
            SicknessInstance bad=null;
            if(kind!="null_instance") bad=new SicknessInstance();
            if(kind=="definition_fault") bad.SicknessReader=()=>{throw new Exception();};
            f.Go.ExistingSicknesses=List(bad);var x=Read(f.Go);
            Check(x.Items.Count==1 && x.Items[0]==null && x.HasSickness==null && x.TotalEntries==1 && x.ObservedEntries==0 && x.UnknownEntries==1 && x.Status=="unknown",kind+" position and unknown preserved");
            f.Go.ExistingSicknesses=List(Illness(),bad);x=Read(f.Go);
            Check(x.Items.Count==2 && x.Items[1]==null && x.HasSickness==true && x.ObservedEntries==1 && x.UnknownEntries==1 && x.Status=="partial",kind+" partial known positive retained");
            var row=Route()["patients"][0];Check((bool)row["hasSickness"] && (bool)row["needsMedicalAttention"] && row["sicknesses"][1].Type==JTokenType.Null,"partial null slot survives caller/serializer");
        }
        NoMutation("unknown and partial");
    }
    private static void CachedTextFaults()
    {
        foreach (string kind in new[] { "missing_name","missing_symptoms","null_entry","null_text","cache_fault","symptom_key_fault","exposure_fault" })
        {
            Reset();var illness=Illness();var f=Patient(sicknesses:List(illness));
            if(kind=="missing_name") Strings.Entries.Remove("illness.123.name");
            else if(kind=="missing_symptoms") Strings.Entries.Remove("illness.123.symptoms");
            else if(kind=="null_entry") Strings.Entries["illness.123.name"]=null;
            else if(kind=="null_text") Strings.Entries["illness.123.name"].String=null;
            else if(kind=="cache_fault") Strings.EntryReader=_=>{throw new Exception();};
            else if(kind=="symptom_key_fault") illness.ExistingSickness.SymptomKeyReader=()=>{throw new Exception();};
            else illness.ExposureReader=()=>{throw new Exception();};
            var x=Read(f.Go);Check(x.HasSickness==true && x.Items.Count==1 && x.ObservedEntries==1 && x.UnknownEntries==0 && x.Status=="partial" && x.ReasonCode=="fields_unavailable" && x.UnavailableFields.Count>0,kind+" missing prose does not erase native disease positive");
            var row=Route()["patients"][0];Check((bool)row["needsMedicalAttention"] && (bool)row["hasSickness"],kind+" caller keeps clinical positive");
            Check(x.Items[0]["id"] is HashedString,kind+" raw native identity retained");NoMutation(kind);
        }
    }
    private static void CallerSelection()
    {
        Reset();var healthy=Patient("Zhealthy",1,sicknesses:List());var unknown=Patient("Aunknown",2);var ill=Illness(456,"Known disease");var sick=Patient("Bsick",3,sicknesses:List(ill,null));var injured=Patient("Cinjured",4);injured.Health.hitPoints=20;var foreign=Patient("Eforeign",5,1,List(Illness(789)));
        Components.LiveMinionIdentities.Items.Add(null);
        var response=Route(new JObject { ["worldId"]=0 });var rows=(JArray)response["patients"];
        Check(rows.Select(x=>(string)x["name"]).SequenceEqual(new[] { "Bsick","Cinjured","Aunknown" }),"default known positives then unknown sorted by name");
        Check(foreign.Go.MedicalSicknessReads==0,"world filter precedes native sickness reader");
        Check((bool)rows[1]["needsMedicalAttention"] && rows[1]["hasSickness"].Type==JTokenType.Null,"known injured positive survives unknown disease");
        response=Route(new JObject { ["worldId"]=0,["includeHealthy"]=true });Check(((JArray)response["patients"]).Select(x=>(string)x["name"]).SequenceEqual(new[] { "Bsick","Cinjured","Aunknown","Zhealthy" }),"includeHealthy true orders true unknown false");
        response=Route(new JObject { ["worldId"]=0,["query"]="Aunknown" });Check((int)response["returned"]==1 && (int)response["patients"][0]["id"]==2,"name query keeps unknown");
        Check(ill.ExistingSickness.id.ToString()=="0x1C8" && new HashedString(-1).HashValue==-1 && new HashedString(-1).ToString()=="0xFFFFFFFF" && new HashedString(-1).IsValid,"stub matches pinned signed raw hash and unpadded uppercase query form");
        foreach (string query in new[] { "known disease","0x1C8" })
        { response=Route(new JObject { ["worldId"]=0,["query"]=query });Check((int)response["returned"]==1 && (int)response["patients"][0]["id"]==3,"known disease query positive match and null-row safe"); }
        response=Route(new JObject { ["worldId"]=99 });Check((int)response["returned"]==0,"positive nonmatching world has no patients");
        response=Route();Check((int)response["returned"]==4,"default negative allworld sentinel keeps crossworld positives");
        response=Route(new JObject { ["worldId"]=0,["includeHealthy"]=true,["limit"]=1 });Check((int)response["returned"]==1 && (int)response["patients"][0]["id"]==3,"limit applies after actual attention sort");
        response=Route(new JObject { ["worldId"]=0,["limit"]=0 });Check((int)response["returned"]==1,"existing lower limit clamp retained");
        var direct=Route(new JObject { ["worldId"]=0 },false);Check(JToken.DeepEquals(direct,Route(new JObject { ["worldId"]=0 })),"actual management and medical dispatcher agree");
        NoMutation("caller selection");
    }
    internal static void Run()
    {
        var priorDb=Db.Current;bool priorGuard=FreshnessBoundary.GuardNativeFactory;dbBefore=Db.Reads;
        FreshnessBoundary.GuardNativeFactory=true;
        try { DefaultUnknown();EmptyAndPositive();UnknownAndPartial();CachedTextFaults();CallerSelection(); }
        finally { Reset();Db.Current=priorDb;FreshnessBoundary.GuardNativeFactory=priorGuard;MedicalBoundary.InRead=false; }
        Console.WriteLine("Medical disease regression: "+checks+" checks passed (full reader/management/medical callers; native inputs substituted)");
    }
}
