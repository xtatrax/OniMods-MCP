using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
internal static class Program
{
 static JObject Read(string args="{}") { var r=CodexReadTools.Read(JObject.Parse(args));Check(!r.IsError,"read failed: "+r.Content[0].Text);return JObject.Parse(r.Content[0].Text); }
 static void Check(bool condition,string name) { if(!condition)throw new Exception(name); }
 static ContentContainer Box(params ICodexWidget[] widgets) => new ContentContainer { content=new List<ICodexWidget>(widgets) };
 static UnityEngine.GameObject Prefab(string id) => new UnityEngine.GameObject {name="PRIVATE_PREFAB_NAME_"+id,Identity=new KPrefabID {PrefabTag=new Tag(id)}};
 static void CategoryMembersRegression()
 {
  var oldGame=Game.Instance;var oldEntries=CodexCache.entries;var oldSubs=CodexCache.subEntries;
  var oldMembers=Assets.Members;var oldTags=Assets.ObservedTags;bool oldThrow=Assets.Throw;int oldNameCalls=UnityEngine.GameObject.ProperNameCalls;
  try
  {
   Game.Instance=new Game {unlocks=new Unlocks()};
   CodexCache.entries=new Dictionary<string,CodexEntry>();CodexCache.subEntries=new Dictionary<string,SubEntry>();
   Assets.Members=new Dictionary<string,List<UnityEngine.GameObject>>();Assets.ObservedTags=new List<string>();Assets.Throw=false;
   UnityEngine.GameObject.ProperNameCalls=0;
   var coal=new CodexEntry {id="COAL",name="<link=COAL>石炭</link>",StoredContent=new List<ContentContainer>{Box(new DangerousWidget())}};
   var ore=new CodexEntry {id="REGOLITH",name="レゴリス",StoredContent=new List<ContentContainer>{Box(new DangerousWidget())}};
   CodexCache.entries["COAL"]=coal;CodexCache.entries["REGOLITH"]=ore;
   var widget=new CodexElementCategoryList(new Tag("MetalOre"));
   var entry=new CodexEntry {id="CATEGORY",StoredContent=new List<ContentContainer>{Box(widget)}};
   CodexCache.entries["CATEGORY"]=entry;
   CodexCache.entries["OTHER"]=new CodexEntry {id="OTHER",StoredContent=new List<ContentContainer>{Box(new CodexElementCategoryList(new Tag("NeverRead")))}};
   Assets.Members["MetalOre"]=new List<UnityEngine.GameObject>{Prefab("Coal"),Prefab("Regolith")};
   Read("{id:'CATEGORY',includeContent:false}");Read("{action:'search',query:'CATEGORY'}");
   Check(Assets.ObservedTags.Count==0,"category metadata paths do not enumerate assets");
   var value=Read("{id:'CATEGORY',maxChars:20000}");var panel=value["results"][0]["content"]["blocks"][0];var members=panel["categoryMembers"];
   Check(members!=null && (bool)members["available"] && (int)members["returned"]==2,"native category cache members are returned by actual reader");
   Check((string)panel["data"]["categoryTag"]=="MetalOre" && !(bool)panel["renderedUiComplete"],"raw category and rendered-UI boundary retained");
   Check((bool)members["complete"] && (int)members["matched"]==2 && (int)members["filteredMembers"]==0 && (int)members["unresolvedMembers"]==0,"observed category is complete without invented omissions");
   Check(((JObject)members["members"][0]).Count==2 && (string)members["members"][0]["entryId"]=="COAL" && (string)members["members"][0]["name"]=="<link=COAL>石炭</link>","category members expose cached Codex ID and localized name only");
   Check(coal.ContentReads==0 && ore.ContentReads==0 && !members.ToString().Contains("PRIVATE_PREFAB_NAME") && UnityEngine.GameObject.ProperNameCalls==0,"member content and prefab naming callbacks are not read");
   Check(Assets.ObservedTags.Count==1 && Assets.ObservedTags[0]=="MetalOre" && Assets.Members["MetalOre"].Count==2,"only selected category is read and cached list remains unchanged");
   CodexCache.subEntries["NOTEMEMBER"]=new SubEntry {id="NOTEMEMBER",parentEntryID="COAL",name="分類済みサブ項目",lockID="open-member",contentContainers=new List<ContentContainer>{Box(new DangerousWidget())}};
   Game.Instance.unlocks.unlocked.Add("open-member");widget.categoryTag=new Tag("OpenSub");Assets.Members["OpenSub"]=new List<UnityEngine.GameObject>{Prefab("NoteMember")};
   members=Read("{id:'CATEGORY'}")["results"][0]["content"]["blocks"][0]["categoryMembers"];
   Check((bool)members["complete"] && ((JObject)members["members"][0]).Count==2 && (string)members["members"][0]["entryId"]=="NOTEMEMBER" && (string)members["members"][0]["name"]=="分類済みサブ項目","unlocked canonical subentry exposes cached localized metadata only");
   widget.categoryTag=new Tag("Aliases");var inaccessibleAlias=Prefab("Co_al");inaccessibleAlias.name="HIDDEN_ALIAS_LABEL";
   Assets.Members["Aliases"]=new List<UnityEngine.GameObject>{Prefab("Coal"),inaccessibleAlias,Prefab("COAL"),Prefab("coal")};
   members=Read("{id:'CATEGORY',maxChars:20000}")["results"][0]["content"]["blocks"][0]["categoryMembers"];
   Check((int)members["returned"]==1 && ((JArray)members["members"]).Count==1 && (string)members["members"][0]["entryId"]=="COAL","case-only repeated links are deduplicated by cached Codex ID");
   Check((int)members["unresolvedMembers"]==1 && members["matched"].Type==JTokenType.Null && !(bool)members["complete"],"destructive normalized alias is unresolved and cannot borrow readable entry identity");
   Check(!members.ToString().Contains("Co_al") && !members.ToString().Contains("HIDDEN_ALIAS_LABEL") && ((JObject)members["members"][0]).Count==2,"inaccessible normalized prefab alias and raw identity fields are never published");
   Check((string)members["scope"]=="normalized_cached_codex_links_with_readable_metadata" && (string)members["identitySemantics"]=="codex_link_projection_not_raw_prefab_identity" && (bool)members["postCopyMetadataWalkBounded"] && !(bool)members["nativeCacheCopyBounded"] && (bool)members["pageRegeneratesNativeCacheCopy"],"link and post-copy budget limits are explicit without native allocation or UI identity promises");
   widget.categoryTag=new Tag("Empty");Assets.ObservedTags.Clear();
   members=Read("{id:'CATEGORY'}")["results"][0]["content"]["blocks"][0]["categoryMembers"];
   Check((bool)members["available"] && (bool)members["complete"] && (int)members["matched"]==0 && (int)members["returned"]==0 && ((JArray)members["members"]).Count==0,"observed empty category remains known empty");
   widget.categoryTag=default(Tag);Assets.ObservedTags.Clear();
   members=Read("{id:'CATEGORY'}")["results"][0]["content"]["blocks"][0]["categoryMembers"];
   Check(!(bool)members["available"] && members["members"].Type==JTokenType.Null && members["returned"].Type==JTokenType.Null && Assets.ObservedTags.Count==0,"invalid category is unknown and does not enumerate assets");
   widget.categoryTag=new Tag("Missing");Assets.Members["Missing"]=null;
   members=Read("{id:'CATEGORY'}")["results"][0]["content"]["blocks"][0]["categoryMembers"];
   Check(!(bool)members["available"] && members["members"].Type==JTokenType.Null && members["matched"].Type==JTokenType.Null,"unavailable cache result does not become empty");
   Assets.Throw=true;members=Read("{id:'CATEGORY'}")["results"][0]["content"]["blocks"][0]["categoryMembers"];Assets.Throw=false;
   Check(!(bool)members["available"] && members["members"].Type==JTokenType.Null,"native cache failure remains unavailable without fallback");
   CodexCache.entries["PRIVATEDLC"]=new CodexEntry {id="PRIVATEDLC",name="PRIVATE_DLC_NAME",ValidDlc=false};
   CodexCache.entries["PRIVATEDISABLED"]=new CodexEntry {id="PRIVATEDISABLED",name="PRIVATE_DISABLED_NAME",disabled=true};
   CodexCache.entries["PRIVATEPARENT"]=new CodexEntry {id="PRIVATEPARENT",name="PRIVATE_PARENT_NAME",ValidDlc=false};
   CodexCache.subEntries["PRIVATELOCKED"]=new SubEntry {id="PRIVATELOCKED",parentEntryID="COAL",name="PRIVATE_LOCKED_NAME",lockID="not-unlocked"};
   CodexCache.subEntries["PRIVATESUBDLC"]=new SubEntry {id="PRIVATESUBDLC",parentEntryID="COAL",name="PRIVATE_SUB_DLC_NAME",ValidDlc=false};
   CodexCache.subEntries["PRIVATECHILD"]=new SubEntry {id="PRIVATECHILD",parentEntryID="PRIVATEPARENT",name="PRIVATE_CHILD_NAME"};
   CodexCache.subEntries["PRIVATEORPHAN"]=new SubEntry {id="PRIVATEORPHAN",parentEntryID="NO_PARENT",name="PRIVATE_ORPHAN_NAME"};
   widget.categoryTag=new Tag("Protected");
   Assets.Members["Protected"]=new List<UnityEngine.GameObject>{Prefab("Coal"),Prefab("PrivateDlc"),Prefab("PrivateDisabled"),Prefab("PrivateLocked"),Prefab("PrivateSubDlc"),Prefab("PrivateChild"),Prefab("PrivateHidden"),Prefab("PrivateOrphan")};
   value=Read("{id:'CATEGORY',maxChars:20000}");members=value["results"][0]["content"]["blocks"][0]["categoryMembers"];
   Check((bool)members["available"] && !(bool)members["complete"] && (int)members["returned"]==1 && (int)members["filteredMembers"]==5 && (int)members["unresolvedMembers"]==2 && members["matched"].Type==JTokenType.Null,"DLC disabled lock parent and unmapped cases are separated from known membership");
   Check(!members.ToString().Contains("PRIVATE") && !members.ToString().Contains("Private"),"filtered or unresolved raw identities and names never enter output");
   CodexCache.entries["AMBIGUOUS"]=new CodexEntry {id="AMBIGUOUS",name="PRIVATE_AMBIGUOUS_NAME"};CodexCache.subEntries["AMBIGUOUS"]=new SubEntry {id="AMBIGUOUS",parentEntryID="COAL",name="PRIVATE_SUB_AMBIGUOUS_NAME"};
   CodexCache.entries["WRONGIDENTITY"]=new CodexEntry {id="PRIVATE_DIFFERENT_ID",name="PRIVATE_DIFFERENT_NAME"};
   widget.categoryTag=new Tag("Unknown");Assets.Members["Unknown"]=new List<UnityEngine.GameObject>{Prefab("Ambiguous"),Prefab("WrongIdentity"),null,new UnityEngine.GameObject()};
   members=Read("{id:'CATEGORY'}")["results"][0]["content"]["blocks"][0]["categoryMembers"];
   Check((int)members["returned"]==0 && (int)members["unresolvedMembers"]==4 && members["matched"].Type==JTokenType.Null && !(bool)members["complete"],"ambiguous mismatched null and missing native identity remain unresolved");
   Check(!members.ToString().Contains("PRIVATE"),"unresolved cache metadata is not exposed");
   widget.categoryTag=new Tag("Many");var many=new List<UnityEngine.GameObject>();for(int i=0;i<5000;i++)many.Add(Prefab("Coal"));Assets.Members["Many"]=many;
   // Use the real page envelope; the native cache copy precedes the bounded metadata walk.
   string joined="",revision=null;int offset=0;
   while(true)
   {
    var args=new JObject{{"action","page"},{"id","CATEGORY"},{"pageSize",997},{"offset",offset}};if(revision!=null)args["revision"]=revision;
    var page=Read(args.ToString());revision=(string)page["revision"];joined+=(string)page["text"];
    if(page["nextOffset"].Type==JTokenType.Null)break;offset=(int)page["nextOffset"];
   }
   members=JObject.Parse(joined)["content"]["blocks"][0]["categoryMembers"];
   Check((bool)members["truncated"] && !(bool)members["complete"] && members["matched"].Type==JTokenType.Null && (int)members["returned"]>0 && (int)members["returned"]<5000 && many.Count==5000,"native category loop shares finite panel budget and leaves source untouched");
   Check(JObject.Parse(joined)["content"].ToString().Contains("data_limit"),"category node limit remains visible through long-content paging");
   widget.categoryTag=new Tag("NeverRead");entry.StoredContent[0].lockID="locked-category";Assets.ObservedTags.Clear();
   Read("{id:'CATEGORY'}");Check(Assets.ObservedTags.Count==0,"locked category container never enumerates members");
   entry.StoredContent[0].lockID=null;entry.ValidDlc=false;Read("{id:'CATEGORY'}");Check(Assets.ObservedTags.Count==0,"DLC-filtered category entry never enumerates members");
   entry.ValidDlc=true;entry.StoredContent[0].ValidDlc=false;Read("{id:'CATEGORY'}");Check(Assets.ObservedTags.Count==0,"DLC-filtered category container never enumerates members");
  }
  finally
  {
   Game.Instance=oldGame;CodexCache.entries=oldEntries;CodexCache.subEntries=oldSubs;
   Assets.Members=oldMembers;Assets.ObservedTags=oldTags;Assets.Throw=oldThrow;UnityEngine.GameObject.ProperNameCalls=oldNameCalls;
  }
 }

 static void SublimationFallbackRegression()
 {
  var oldGame=Game.Instance;var oldEntries=CodexCache.entries;var oldSubs=CodexCache.subEntries;
  var oldPrefabs=Assets.Prefabs;var oldPrefabRequests=Assets.PrefabRequests;bool oldPrefabThrow=Assets.ThrowPrefabLookup;int oldWarnings=Assets.WarningLookupCalls;
  var oldElements=ElementLoader.Elements;var oldRequests=ElementLoader.Requests;var oldFaults=ElementLoader.ThrowHashes;
  int oldLoad=ElementLoader.LoadCalls,oldTags=TagManager.CreateCalls,oldSim=Sublimates.SimCalls,oldConfigure=CodexTemperatureTransitionPanel.ConfigureCalls;
  int oldComponents=UnityEngine.GameObject.SublimationReads,oldNames=UnityEngine.GameObject.ProperNameCalls;
  try
  {
   Game.Instance=new Game {unlocks=new Unlocks()};CodexCache.entries=new Dictionary<string,CodexEntry>();CodexCache.subEntries=new Dictionary<string,SubEntry>();
   Assets.Prefabs=new Dictionary<string,UnityEngine.GameObject>();Assets.PrefabRequests=new List<string>();Assets.ThrowPrefabLookup=false;Assets.WarningLookupCalls=0;
   ElementLoader.Elements=new Dictionary<SimHashes,Element>();ElementLoader.Requests=new List<SimHashes>();ElementLoader.ThrowHashes=new HashSet<SimHashes>();
   ElementLoader.LoadCalls=TagManager.CreateCalls=Sublimates.SimCalls=CodexTemperatureTransitionPanel.ConfigureCalls=0;UnityEngine.GameObject.SublimationReads=UnityEngine.GameObject.ProperNameCalls=0;
   var source=new Element {id=SimHashes.TestSource,sublimateId=SimHashes.Missing,sublimateRate=99f,sublimateEfficiency=.8f};
   var widget=new CodexTemperatureTransitionPanel {sourceElement=source,transitionType="SUBLIMATE"};
   var holder=new CodexEntry {id="SUBLIMATECASE",StoredContent=new List<ContentContainer>{Box(widget)}};CodexCache.entries[holder.id]=holder;
   var targetMetadata=new CodexEntry {id="FALLBACKTARGET",name="<link=FALLBACKTARGET>既存の遷移先</link>",StoredContent=new List<ContentContainer>{Box(new DangerousWidget())}};CodexCache.entries[targetMetadata.id]=targetMetadata;
   ElementLoader.Elements[SimHashes.FallbackTarget]=new Element {id=SimHashes.FallbackTarget};ElementLoader.Elements[SimHashes.DirectTarget]=new Element {id=SimHashes.DirectTarget};
   var component=new Sublimates {info=new Sublimates.Info(.0002f,.0025f,1.8f,.5f,SimHashes.FallbackTarget)};
   var prefab=new UnityEngine.GameObject {name="PRIVATE_PREFAB_NAME",Sublimation=component};Assets.Prefabs["TestSource"]=prefab;
   Func<JToken> readPanel=()=>Read("{id:'SUBLIMATECASE',maxChars:20000}")["results"][0]["content"]["blocks"][0];
   Action reset=()=>{ElementLoader.Requests.Clear();Assets.PrefabRequests.Clear();UnityEngine.GameObject.SublimationReads=0;};
   reset();Read("{id:'SUBLIMATECASE',includeContent:false}");Check(ElementLoader.Requests.Count==0 && Assets.PrefabRequests.Count==0,"sublimation metadata does not perform native definition lookups");
   var panel=readPanel();var fallback=panel["prefabSublimatesFallback"];
   Check(fallback!=null && (bool)fallback["required"] && (bool)fallback["available"] && (string)fallback["status"]=="resolved","cached Sublimates fallback definition is returned by actual reader");
   Check((float)fallback["values"]["sublimationRate"]==.0002f && (float)fallback["values"]["massPower"]==.5f && (string)fallback["semantics"]["massPower"]=="mass_exponent" && (string)fallback["semantics"]["sublimationRate"]=="native_mass_dependent_rate_coefficient","raw coefficient and exponent remain distinct from efficiency mass and predicted flow");
   Check((bool)fallback["targetResolved"] && ((JObject)fallback["target"]).Count==2 && (string)fallback["target"]["entryId"]=="FALLBACKTARGET" && (string)fallback["target"]["name"]==targetMetadata.name && targetMetadata.ContentReads==0,"target uses readable cached Codex metadata without reading its body");
   Check((float)panel["data"]["sourceElement"]["sublimateRate"]==99f && (float)panel["data"]["sourceElement"]["sublimateEfficiency"]==.8f && !(bool)panel["renderedUiComplete"] && !panel["issues"].ToString().Contains("fallback_not_evaluated"),"legacy raw source fields remain separate and rendered UI remains incomplete");
   Check(ElementLoader.Requests.Count==2 && ElementLoader.Requests[0]==SimHashes.Missing && ElementLoader.Requests[1]==SimHashes.FallbackTarget && Assets.PrefabRequests.Count==1 && Assets.PrefabRequests[0]=="TestSource" && UnityEngine.GameObject.SublimationReads==1 && !fallback.ToString().Contains("PRIVATE_PREFAB"),"only native direct absence triggers exact cached prefab lookup without raw prefab identity");
   source.sublimateId=SimHashes.DirectTarget;reset();fallback=readPanel()["prefabSublimatesFallback"];
   Check(((JObject)fallback).Count==3 && !(bool)fallback["required"] && (string)fallback["status"]=="not_required" && (string)fallback["source"]=="Element.sublimateId" && ElementLoader.Requests.Count==1 && Assets.PrefabRequests.Count==0 && UnityEngine.GameObject.SublimationReads==0,"resolved direct target is compact and does not inspect prefab or its component");
   source.sublimateId=SimHashes.Missing;
   foreach(string mode in new[]{"OFFGASS","HEAT","COOL"})
   {widget.transitionType=mode;reset();Check(readPanel()["prefabSublimatesFallback"]==null && ElementLoader.Requests.Count==0 && Assets.PrefabRequests.Count==0,"non SUBLIMATE panel does not gain fallback observation: "+mode);}
   widget.transitionType="SUBLIMATE";
   var savedElements=ElementLoader.Elements;ElementLoader.Elements=null;reset();fallback=readPanel()["prefabSublimatesFallback"];ElementLoader.Elements=savedElements;
   Check(fallback["required"].Type==JTokenType.Null && !(bool)fallback["available"] && (string)fallback["status"]=="direct_lookup_unavailable" && fallback["values"].Type==JTokenType.Null && Assets.PrefabRequests.Count==0,"unprepared direct lookup remains unknown instead of becoming native absence");
   ElementLoader.ThrowHashes.Add(SimHashes.Missing);reset();fallback=readPanel()["prefabSublimatesFallback"];ElementLoader.ThrowHashes.Clear();
   Check(fallback["required"].Type==JTokenType.Null && Assets.PrefabRequests.Count==0,"direct lookup exception does not trigger fallback");
   widget.sourceElement=null;reset();fallback=readPanel()["prefabSublimatesFallback"];widget.sourceElement=source;
   Check(fallback["required"].Type==JTokenType.Null && (string)fallback["status"]=="source_unavailable" && ElementLoader.Requests.Count==0,"missing native source remains unknown with no lookup");
   Assets.Prefabs.Clear();fallback=readPanel()["prefabSublimatesFallback"];Assets.Prefabs["TestSource"]=prefab;
   Check((bool)fallback["required"] && !(bool)fallback["available"] && (string)fallback["status"]=="prefab_unavailable" && fallback["values"].Type==JTokenType.Null,"missing prefab does not invent a zero Info pair");
   prefab.Sublimation=null;fallback=readPanel()["prefabSublimatesFallback"];prefab.Sublimation=component;
   Check(!(bool)fallback["available"] && (string)fallback["status"]=="component_unavailable" && fallback["values"].Type==JTokenType.Null,"missing component differs from an observed default value struct");
   Assets.ThrowPrefabLookup=true;fallback=readPanel()["prefabSublimatesFallback"];Assets.ThrowPrefabLookup=false;
   Check((bool)fallback["required"] && (string)fallback["status"]=="prefab_lookup_unavailable" && fallback["values"].Type==JTokenType.Null,"prefab cache exception remains unavailable without a lower level fallback");
   prefab.ThrowSublimationRead=true;fallback=readPanel()["prefabSublimatesFallback"];prefab.ThrowSublimationRead=false;
   Check((string)fallback["status"]=="prefab_lookup_unavailable" && fallback["values"].Type==JTokenType.Null,"component lookup fault remains unavailable");
   ElementLoader.Elements.Remove(SimHashes.FallbackTarget);fallback=readPanel()["prefabSublimatesFallback"];ElementLoader.Elements[SimHashes.FallbackTarget]=new Element {id=SimHashes.FallbackTarget};
   Check((bool)fallback["available"] && !(bool)fallback["targetResolved"] && fallback["target"].Type==JTokenType.Null && (float)fallback["values"]["massPower"]==.5f && (string)fallback["status"]=="target_unresolved","normal missing target keeps observed Info values and a known false resolution");
   ElementLoader.ThrowHashes.Add(SimHashes.FallbackTarget);fallback=readPanel()["prefabSublimatesFallback"];ElementLoader.ThrowHashes.Clear();
   Check((bool)fallback["available"] && fallback["targetResolved"].Type==JTokenType.Null && (float)fallback["values"]["sublimationRate"]==.0002f && (string)fallback["status"]=="target_lookup_unavailable","target lookup exception keeps observed Info values with unknown resolution");
   CodexCache.entries.Remove("FALLBACKTARGET");fallback=readPanel()["prefabSublimatesFallback"];CodexCache.entries["FALLBACKTARGET"]=targetMetadata;
   Check((bool)fallback["available"] && (bool)fallback["targetResolved"] && fallback["target"].Type==JTokenType.Null && (string)fallback["status"]=="target_metadata_unavailable","native target resolution differs from missing readable Codex projection");
   ElementLoader.Elements[SimHashes.PrivateTarget]=new Element {id=SimHashes.PrivateTarget};component.info=new Sublimates.Info(.0002f,.0025f,1.8f,.5f,SimHashes.PrivateTarget);
   var hidden=new CodexEntry {id="PRIVATETARGET",name="HIDDEN_TARGET_NAME",disabled=true};CodexCache.entries[hidden.id]=hidden;
   fallback=readPanel()["prefabSublimatesFallback"];
   Check((bool)fallback["targetResolved"] && (bool)fallback["available"] && fallback["target"].Type==JTokenType.Null && (string)fallback["status"]=="target_filtered" && !fallback.ToString().Contains("PrivateTarget") && !fallback.ToString().Contains("HIDDEN_TARGET_NAME"),"disabled target identity is withheld while finite source definition remains observed");
   hidden.disabled=false;hidden.ValidDlc=false;fallback=readPanel()["prefabSublimatesFallback"];
   Check((string)fallback["status"]=="target_filtered" && fallback["target"].Type==JTokenType.Null,"DLC target identity is not leaked");
   CodexCache.entries.Remove(hidden.id);CodexCache.subEntries[hidden.id]=new SubEntry {id=hidden.id,parentEntryID=holder.id,name="HIDDEN_LOCK_NAME",lockID="unreleased-target"};fallback=readPanel()["prefabSublimatesFallback"];
   Check((string)fallback["status"]=="target_filtered" && fallback["target"].Type==JTokenType.Null && !fallback.ToString().Contains("HIDDEN_LOCK_NAME"),"locked target identity is not leaked");
   component.info=new Sublimates.Info(0f,0f,0f,0f,SimHashes.FallbackTarget);fallback=readPanel()["prefabSublimatesFallback"];
   Check((bool)fallback["available"] && (float)fallback["values"]["sublimationRate"]==0f && (float)fallback["values"]["massPower"]==0f && (bool)fallback["targetResolved"],"observed zero Info fields remain known zero");
   component.info=new Sublimates.Info(float.NaN,0f,0f,.5f,SimHashes.FallbackTarget);fallback=readPanel()["prefabSublimatesFallback"];
   Check(!(bool)fallback["available"] && fallback["values"]["sublimationRate"].Type==JTokenType.Null && (float)fallback["values"]["massPower"]==.5f && (string)fallback["status"]=="invalid_numeric" && fallback["targetResolved"].Type==JTokenType.Null && !fallback.ToString().Contains("NaN"),"invalid rate is not zero and finite exponent is retained");
   component.info=new Sublimates.Info(0f,0f,0f,float.PositiveInfinity,SimHashes.FallbackTarget);fallback=readPanel()["prefabSublimatesFallback"];
   Check(!(bool)fallback["available"] && (float)fallback["values"]["sublimationRate"]==0f && fallback["values"]["massPower"].Type==JTokenType.Null && !fallback.ToString().Contains("Infinity"),"invalid exponent is null while observed rate zero is retained");
   component.info=new Sublimates.Info(-1f,0f,0f,-.5f,SimHashes.FallbackTarget);fallback=readPanel()["prefabSublimatesFallback"];
   Check((bool)fallback["available"] && (float)fallback["values"]["sublimationRate"]==-1f && (float)fallback["values"]["massPower"]==-.5f,"finite definition values are not replaced with MOD predictions or clamps");
   component.info=new Sublimates.Info(.0002f,.0025f,1.8f,.5f,SimHashes.FallbackTarget);
   var issues=new List<string>();int budget=23;reset();var limited=CodexSublimationReader.Read(widget,issues,ref budget);
   Check((string)limited["status"]=="data_limit" && issues.Contains("data_limit") && limited["required"]==null && ElementLoader.Requests.Count==0 && Assets.PrefabRequests.Count==0 && budget==23,"fallback shares the caller budget before native metadata access");
   holder.StoredContent[0].lockID="unreleased-source";reset();Read("{id:'SUBLIMATECASE'}");Check(ElementLoader.Requests.Count==0 && Assets.PrefabRequests.Count==0,"locked source panel never enters fallback reader");holder.StoredContent[0].lockID=null;
   holder.ValidDlc=false;reset();Read("{id:'SUBLIMATECASE'}");Check(ElementLoader.Requests.Count==0 && Assets.PrefabRequests.Count==0,"DLC filtered source never enters fallback reader");holder.ValidDlc=true;
   string joined="",revision=null;int offset=0;bool ended=false;
   for(int i=0;i<20;i++)
   {
    var args=new JObject{{"action","page"},{"id","SUBLIMATECASE"},{"offset",offset},{"pageSize",997}};if(revision!=null)args["revision"]=revision;
    var page=Read(args.ToString());if(revision==null)revision=(string)page["revision"];Check((string)page["revision"]==revision,"fallback paging revision remains stable");joined+=(string)page["text"];
    if(page["nextOffset"].Type==JTokenType.Null){ended=true;break;}offset=(int)page["nextOffset"];
   }
   Check(ended && (string)JObject.Parse(joined)["content"]["blocks"][0]["prefabSublimatesFallback"]["status"]=="resolved","real Codex page fragments preserve the new fallback projection");
   Check(ElementLoader.LoadCalls==0 && TagManager.CreateCalls==0 && Assets.WarningLookupCalls==0 && CodexTemperatureTransitionPanel.ConfigureCalls==0 && Sublimates.SimCalls==0 && UnityEngine.GameObject.ProperNameCalls==0,"fallback read performs no initialization tag registration warnings UI naming or simulation");
  }
  finally
  {
   Game.Instance=oldGame;CodexCache.entries=oldEntries;CodexCache.subEntries=oldSubs;
   Assets.Prefabs=oldPrefabs;Assets.PrefabRequests=oldPrefabRequests;Assets.ThrowPrefabLookup=oldPrefabThrow;Assets.WarningLookupCalls=oldWarnings;
   ElementLoader.Elements=oldElements;ElementLoader.Requests=oldRequests;ElementLoader.ThrowHashes=oldFaults;ElementLoader.LoadCalls=oldLoad;TagManager.CreateCalls=oldTags;
   Sublimates.SimCalls=oldSim;CodexTemperatureTransitionPanel.ConfigureCalls=oldConfigure;UnityEngine.GameObject.SublimationReads=oldComponents;UnityEngine.GameObject.ProperNameCalls=oldNames;
  }
 }

 static void PreyAmountSemanticsRegression()
 {
  var oldGame=Game.Instance;var oldEntries=CodexCache.entries;var oldSubs=CodexCache.subEntries;
  int oldPrey=GameUtil.PreyCalls,oldDirectPlant=GameUtil.DirectPlantCalls,oldStoragePlant=GameUtil.StoragePlantCalls,oldForeign=UnrelatedPreyFormatters.Calls;
  try
  {
   Game.Instance=new Game {unlocks=new Unlocks()};CodexCache.entries=new Dictionary<string,CodexEntry>();CodexCache.subEntries=new Dictionary<string,SubEntry>();
   GameUtil.PreyCalls=GameUtil.DirectPlantCalls=GameUtil.StoragePlantCalls=UnrelatedPreyFormatters.Calls=0;
   var usage=new ElementUsage {tag=new Tag("PRIVATE_PREY_TAG"),amount=.0025f,continuous=true,customFormating=GameUtil.GetFormattedPreyConsumptionValuePerCycle};
   var output=new ElementUsage {tag=new Tag("PRIVATE_OUTPUT_TAG"),amount=42f,continuous=true};
   var panel=new CodexConversionPanel {title="Cached prey definition",ins=new[]{usage},outs=new[]{output}};
   var entry=new CodexEntry {id="PREYSEMANTICS",StoredContent=new List<ContentContainer>{Box(panel)}};CodexCache.entries[entry.id]=entry;
   Func<JToken> readPanel=()=>Read("{id:'PREYSEMANTICS',maxChars:20000}")["results"][0]["content"]["blocks"][0];
   Read("{id:'PREYSEMANTICS',includeContent:false}");Check(GameUtil.PreyCalls==0,"prey metadata reads execute no formatter");
   var value=readPanel();var row=value["data"]["ins"][0];var semantics=row["amountSemantics"];
   Check(semantics!=null && (string)semantics["rawUnit"]=="entity_count_per_second" && (string)semantics["nativeDisplayUnit"]=="entity_count_per_cycle","known native prey formatter receives amount semantics through actual reader");
   Check(((JObject)semantics).Count==4 && (string)semantics["source"]=="GameUtil.GetFormattedPreyConsumptionValuePerCycle" && !(bool)semantics["formatterExecuted"] && !semantics.ToString().Contains("PRIVATE"),"semantics contain source and non execution metadata without prey identity");
   Check((float)row["amount"]==.0025f && (string)row["tag"]=="PRIVATE_PREY_TAG" && (bool)row["continuous"] && (bool)row["customFormating"]["callbackPresent"],"raw amount tag continuous and callback presence remain unchanged");
   Check(value["issues"].ToString().Contains("formatter_not_executed") && !(bool)value["renderedUiComplete"],"recognized formatter remains unexecuted and rendered UI incomplete");
   Check(value["data"]["outs"][0]["amountSemantics"]==null && (float)value["data"]["outs"][0]["amount"]==42f,"output units are not inferred from prey input formatter");
   usage.amount=0f;row=readPanel()["data"]["ins"][0];Check((float)row["amount"]==0f && row["amountSemantics"]!=null,"prey raw zero is retained without a computed amount");
   usage.amount=-.25f;row=readPanel()["data"]["ins"][0];Check((float)row["amount"]==-.25f && row["amountSemantics"]!=null,"prey raw negative is retained without a correction or validity claim");usage.amount=.0025f;
   usage.customFormating=null;row=readPanel()["data"]["ins"][0];Check(row["amountSemantics"]==null && row["customFormating"].Type==JTokenType.Null,"null formatter remains unclassified");
   var bound=new BoundPreyFormatter();
   var unknown=new Func<Tag,float,bool,string>[]
   {
    GameUtil.GetFormattedDirectPlantConsumptionValuePerCycle,
    GameUtil.GetFormattedPlantStorageConsumptionValuePerCycle,
    UnrelatedPreyFormatters.GetFormattedPreyConsumptionValuePerCycle,
    bound.GetFormattedPreyConsumptionValuePerCycle,
    (tag,amount,perCycle)=>GameUtil.GetFormattedPreyConsumptionValuePerCycle(tag,amount,perCycle)
   };
   foreach(var formatter in unknown)
   {
    usage.customFormating=formatter;value=readPanel();row=value["data"]["ins"][0];
    Check(row["amountSemantics"]==null && (bool)row["customFormating"]["callbackPresent"] && value["issues"].ToString().Contains("formatter_not_executed"),"plant foreign instance and wrapper delegates retain unclassified callback presence");
   }
   Func<Tag,float,bool,string> native=GameUtil.GetFormattedPreyConsumptionValuePerCycle;
   foreach(var formatter in new[]{native+native,native+(Func<Tag,float,bool,string>)UnrelatedPreyFormatters.GetFormattedPreyConsumptionValuePerCycle,(Func<Tag,float,bool,string>)UnrelatedPreyFormatters.GetFormattedPreyConsumptionValuePerCycle+native})
   {usage.customFormating=formatter;row=readPanel()["data"]["ins"][0];Check(row["amountSemantics"]==null && (bool)row["customFormating"]["callbackPresent"],"multicast delegates remain unclassified even when native is first last or repeated");}
   usage.customFormating=native;var expected=readPanel()["data"]["ins"][0];Check(JToken.DeepEquals(expected,readPanel()["data"]["ins"][0]),"repeat read retains identical raw and semantics metadata");
   entry.StoredContent[0].lockID="unreleased-prey";var locked=Read("{id:'PREYSEMANTICS'}");Check(((JArray)locked["results"][0]["content"]["blocks"]).Count==0,"locked container does not expose prey semantics");entry.StoredContent[0].lockID=null;
   var budgetUsages=new List<ElementUsage>();for(int i=0;i<1021;i++)budgetUsages.Add(new ElementUsage {tag=new Tag("PREY"),amount=1f,continuous=true});budgetUsages.Add(usage);
   var limited=CodexPanelReader.Read(new CodexConversionPanel {title="budget",ins=budgetUsages.ToArray()});
   var wire=JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(limited,OniMcp.Support.McpJsonUtil.Settings));var last=wire["data"]["ins"][1021];
   Check((float)last["amount"]==.0025f && (bool)last["customFormating"]["callbackPresent"] && last["amountSemantics"]==null && wire["issues"].ToString().Contains("data_limit"),"shared node budget preserves raw callback data but refuses an unbudgeted semantics object");
   Check(GameUtil.PreyCalls==0 && GameUtil.DirectPlantCalls==0 && GameUtil.StoragePlantCalls==0 && UnrelatedPreyFormatters.Calls==0 && bound.Calls==0,"classification never invokes known plant foreign instance multicast or wrapper formatters");
  }
  finally
  {
   Game.Instance=oldGame;CodexCache.entries=oldEntries;CodexCache.subEntries=oldSubs;
   GameUtil.PreyCalls=oldPrey;GameUtil.DirectPlantCalls=oldDirectPlant;GameUtil.StoragePlantCalls=oldStoragePlant;UnrelatedPreyFormatters.Calls=oldForeign;
  }
 }

 static void ConverterNominalLightRegression()
 {
  var oldGame=Game.Instance;var oldEntries=CodexCache.entries;var oldSubs=CodexCache.subEntries;
  var oldPrefabs=Assets.Prefabs;var oldRequests=Assets.PrefabRequests;bool oldThrow=Assets.ThrowPrefabLookup;
  int oldIdentity=UnityEngine.GameObject.IdentityReads,oldLight=UnityEngine.GameObject.LightReads;
  int oldConstructors=Light2D.ConstructorCalls,oldLux=Light2D.LuxReads,oldRange=Light2D.RangeReads;
  int oldSet=Light2D.SetterCalls,oldDescriptor=Light2D.DescriptorCalls,oldRefresh=Light2D.RefreshCalls,oldSim=Light2D.SimulationCalls;
  try
  {
   Game.Instance=new Game {unlocks=new Unlocks()};CodexCache.entries=new Dictionary<string,CodexEntry>();CodexCache.subEntries=new Dictionary<string,SubEntry>();
   Assets.Prefabs=new Dictionary<string,UnityEngine.GameObject>();Assets.PrefabRequests=new List<string>();Assets.ThrowPrefabLookup=false;
   UnityEngine.GameObject.IdentityReads=0;UnityEngine.GameObject.LightReads=0;
   Light2D.LuxReads=0;Light2D.RangeReads=0;Light2D.SetterCalls=0;Light2D.DescriptorCalls=0;Light2D.RefreshCalls=0;Light2D.SimulationCalls=0;
   var converter=Prefab("PRIVATE_LIGHT_TAG");converter.name="selected converter";
   var light=new Light2D {NominalLux=1800,NominalRange=5.5f};converter.Light=light;
   Assets.Prefabs["PRIVATE_LIGHT_TAG"]=converter;
   var widget=new CodexConversionPanel {title="conversion title",Converter=converter,ins=new[]{new ElementUsage {tag=new Tag("Phosphorite"),amount=0.25f,continuous=true}}};
   var box=Box(widget);var entry=new CodexEntry {id="NOMINALLIGHT",StoredContent=new List<ContentContainer>{box}};CodexCache.entries[entry.id]=entry;
   int constructors=Light2D.ConstructorCalls,warningCalls=Assets.WarningLookupCalls,nameCalls=UnityEngine.GameObject.ProperNameCalls,tagCalls=TagManager.CreateCalls;
   Func<JToken> panel=()=>Read("{id:'NOMINALLIGHT',maxChars:20000}")["results"][0]["content"]["blocks"][0];
   var block=panel();var value=block["converterNominalLight"];
   Check(value!=null && (bool)value["available"] && (string)value["status"]=="observed","exact cached native converter exposes nominal light through actual Read wire");
   Check((int)value["lux"]["value"]==1800 && (bool)value["lux"]["available"] && (string)value["lux"]["unit"]=="lux" && (float)value["range"]["value"]==5.5f && (string)value["range"]["unit"]=="cells" && (string)value["range"]["semantics"]=="radius","native lux and finite radius are independent raw quantities with explicit units");
   Check((string)value["source"]=="CodexConversionPanel.Converter/Assets.TryGetPrefab/Light2D.Lux/Range" && (string)value["semantics"]=="nominal_cached_prefab_light" && ((JObject)value).Count==6 && !value.ToString().Contains("PRIVATE_LIGHT_TAG") && !value.ToString().Contains("selected converter"),"nominal source is explicit without publishing new prefab identities");
   Check((string)block["data"]["title"]=="conversion title" && (float)block["data"]["ins"][0]["amount"]==.25f && (string)block["data"]["Converter"]["assetName"]=="selected converter" && !(bool)block["renderedUiComplete"],"legacy raw conversion values and incomplete native rendering remain intact");
   light.NominalLux=0;light.NominalRange=0f;value=panel()["converterNominalLight"];
   Check((bool)value["available"] && (int)value["lux"]["value"]==0 && (float)value["range"]["value"]==0f,"observed zero is known zero rather than missing defaults");
   light.NominalLux=-7;light.NominalRange=-2.5f;value=panel()["converterNominalLight"];
   Check((bool)value["available"] && (int)value["lux"]["value"]==-7 && (float)value["range"]["value"]==-2.5f,"finite negative raw values are preserved without validity or coverage prediction");
   light.NominalLux=18;
   foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
   {
    light.NominalRange=invalid;value=panel()["converterNominalLight"];
    Check(!(bool)value["available"] && (string)value["status"]=="partial" && (bool)value["lux"]["available"] && (int)value["lux"]["value"]==18 && !(bool)value["range"]["available"] && value["range"]["value"].Type==JTokenType.Null && (string)value["range"]["status"]=="invalid_numeric","nonfinite radius remains missing while observed lux is retained");
   }
   light.NominalRange=3f;light.ThrowLuxRead=true;value=panel()["converterNominalLight"];
   Check((string)value["status"]=="partial" && value["lux"]["value"].Type==JTokenType.Null && (string)value["lux"]["status"]=="read_unavailable" && (float)value["range"]["value"]==3f,"lux getter fault preserves independently observed range");
   light.ThrowLuxRead=false;light.ThrowRangeRead=true;value=panel()["converterNominalLight"];
   Check((string)value["status"]=="partial" && (int)value["lux"]["value"]==18 && value["range"]["value"].Type==JTokenType.Null && (string)value["range"]["status"]=="read_unavailable","range getter fault preserves independently observed lux");
   light.ThrowLuxRead=true;value=panel()["converterNominalLight"];
   Check(!(bool)value["available"] && (string)value["status"]=="unavailable" && value["lux"]["value"].Type==JTokenType.Null && value["range"]["value"].Type==JTokenType.Null,"both scalar getter faults are unknown rather than constructor defaults");light.ThrowLuxRead=false;light.ThrowRangeRead=false;
   converter.Light=null;int beforeLux=Light2D.LuxReads,beforeRange=Light2D.RangeReads;block=panel();value=block["converterNominalLight"];
   Check(((JObject)value).Count==2 && !(bool)value["available"] && (string)value["status"]=="not_present" && value["lux"]==null && value["range"]==null && ((JArray)block["issues"]).Count==0,"ordinary verified nonlight prefab is compact absence without invented zero or an error issue");
   var foreignLight=new ForeignLight2D {ThrowLuxRead=true,ThrowRangeRead=true};converter.Light=foreignLight;value=panel()["converterNominalLight"];
   Check((string)value["status"]=="component_type_unavailable" && Light2D.LuxReads==beforeLux && Light2D.RangeReads==beforeRange,"derived light type is rejected before scalar getters");converter.Light=light;
   converter.ThrowLightRead=true;value=panel()["converterNominalLight"];Check((string)value["status"]=="component_unavailable" && Light2D.LuxReads==beforeLux,"component lookup fault is unavailable without scalar lookup");converter.ThrowLightRead=false;
   var identity=converter.Identity;converter.Identity=new ForeignKPrefabID {PrefabTag=identity.PrefabTag};int beforeRequests=Assets.PrefabRequests.Count,beforeComponent=UnityEngine.GameObject.LightReads;value=panel()["converterNominalLight"];
   Check((string)value["status"]=="identity_unavailable" && Assets.PrefabRequests.Count==beforeRequests && UnityEngine.GameObject.LightReads==beforeComponent,"derived identity is refused before cache and component access");converter.Identity=identity;
   converter.ThrowIdentityRead=true;value=panel()["converterNominalLight"];Check((string)value["status"]=="identity_unavailable" && Assets.PrefabRequests.Count==beforeRequests,"identity getter fault does not fall back to names");converter.ThrowIdentityRead=false;
   converter.Identity=null;value=panel()["converterNominalLight"];Check((string)value["status"]=="identity_unavailable" && Assets.PrefabRequests.Count==beforeRequests,"missing identity remains unknown");converter.Identity=identity;
   identity.PrefabTag=default(Tag);value=panel()["converterNominalLight"];Check((string)value["status"]=="identity_unavailable" && Assets.PrefabRequests.Count==beforeRequests,"invalid existing tag does not register or look up replacement names");identity.PrefabTag=new Tag("PRIVATE_LIGHT_TAG");
   var other=Prefab("PRIVATE_LIGHT_TAG");other.name=converter.name;other.Light=light;Assets.Prefabs["PRIVATE_LIGHT_TAG"]=other;value=panel()["converterNominalLight"];
   Check((string)value["status"]=="cache_identity_mismatch" && UnityEngine.GameObject.LightReads==beforeComponent && Light2D.LuxReads==beforeLux,"matching prefab tag and name cannot substitute a different cached GameObject reference");
   Assets.Prefabs.Clear();value=panel()["converterNominalLight"];Check((string)value["status"]=="cached_prefab_unavailable" && UnityEngine.GameObject.LightReads==beforeComponent,"cache miss remains unavailable without factories or constructor defaults");
   Assets.ThrowPrefabLookup=true;value=panel()["converterNominalLight"];Check((string)value["status"]=="prefab_lookup_unavailable" && UnityEngine.GameObject.LightReads==beforeComponent,"cache exception is distinct from observed absence");Assets.ThrowPrefabLookup=false;Assets.Prefabs["PRIVATE_LIGHT_TAG"]=converter;
   widget.Converter=new UnityEngine.Object {name=converter.name};beforeRequests=Assets.PrefabRequests.Count;value=panel()["converterNominalLight"];
   Check((string)value["status"]=="converter_unavailable" && Assets.PrefabRequests.Count==beforeRequests,"asset name alone does not identify a native GameObject");widget.Converter=null;value=panel()["converterNominalLight"];Check((string)value["status"]=="converter_unavailable","null converter is not a zero light");widget.Converter=converter;
   beforeRequests=Assets.PrefabRequests.Count;Read("{id:'NOMINALLIGHT',includeContent:false}");Read("{action:'search',query:'NOMINALLIGHT'}");
   box.lockID="not-unlocked";Read("{id:'NOMINALLIGHT'}");box.lockID=null;box.ValidDlc=false;Read("{id:'NOMINALLIGHT'}");box.ValidDlc=true;entry.disabled=true;Read("{id:'NOMINALLIGHT'}");entry.disabled=false;
   Check(Assets.PrefabRequests.Count==beforeRequests,"metadata-only search locked DLC and disabled gates do not observe converter components");
   int budget=19,beforeIdentity=UnityEngine.GameObject.IdentityReads;var issues=new List<string>();
   var limited=CodexConverterLightReader.Read(widget,issues,ref budget);
   Check(limited==null && budget==19 && issues.Count==1 && issues[0]=="data_limit" && UnityEngine.GameObject.IdentityReads==beforeIdentity && Assets.PrefabRequests.Count==beforeRequests,"shared budget refuses the whole projection before native lookups");
   budget=20;issues.Clear();var exactBudget=CodexConverterLightReader.Read(widget,issues,ref budget);
   Check(exactBudget!=null && (bool)exactBudget["available"] && budget==0 && issues.Count==0,"fixed reserve permits the bounded projection at the exact boundary");
   budget=20;issues.Clear();Check(CodexConverterLightReader.Read(new CodexRecipePanel(),issues,ref budget)==null && budget==20 && issues.Count==0,"nonconversion panels do not receive or reserve a light facet");
   Game.Instance=null;value=JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(CodexPanelReader.Read(widget),OniMcp.Support.McpJsonUtil.Settings))["converterNominalLight"];
   Check((string)value["status"]=="cache_unavailable","standalone panel helper does not manufacture readiness when game cache is unavailable");Game.Instance=new Game {unlocks=new Unlocks()};
   Check(Light2D.ConstructorCalls==constructors+1 && Light2D.SetterCalls==0 && Light2D.DescriptorCalls==0 && Light2D.RefreshCalls==0 && Light2D.SimulationCalls==0,"reader never constructs native light or invokes setters descriptors refresh or simulation");
   Check(Assets.WarningLookupCalls==warningCalls && UnityEngine.GameObject.ProperNameCalls==nameCalls && TagManager.CreateCalls==tagCalls,"reader does not invoke warning lookups naming callbacks or tag registration");
  }
  finally
  {
   Game.Instance=oldGame;CodexCache.entries=oldEntries;CodexCache.subEntries=oldSubs;Assets.Prefabs=oldPrefabs;Assets.PrefabRequests=oldRequests;Assets.ThrowPrefabLookup=oldThrow;
   UnityEngine.GameObject.IdentityReads=oldIdentity;UnityEngine.GameObject.LightReads=oldLight;
   Light2D.ConstructorCalls=oldConstructors;Light2D.LuxReads=oldLux;Light2D.RangeReads=oldRange;Light2D.SetterCalls=oldSet;Light2D.DescriptorCalls=oldDescriptor;Light2D.RefreshCalls=oldRefresh;Light2D.SimulationCalls=oldSim;
  }
 }

 static void Main()
 {
  Check(CodexReadTools.Read(null).IsError,"menu blocked without initialization");
  Game.Instance=new Game { unlocks=new Unlocks() };
  Check(CodexReadTools.Read(null).IsError,"uninitialized cache blocked without initialization");
  var a=new CodexEntry { id="OXYGEN",name="酸素",title="STRINGS.TITLE",category="ELEMENTS",StoredContent=new List<ContentContainer>{Box(new CodexText {text="<b>説明</b>\n本文"})} };
  var b=new CodexEntry { id="POWER",name="電力",category="BUILDINGS",StoredContent=new List<ContentContainer>{Box(new DangerousWidget())} };
  CodexCache.entries=new Dictionary<string,CodexEntry>{{"OXYGEN",a},{"POWER",b},{"NULL",null}};
  CodexCache.subEntries=new Dictionary<string,SubEntry>();
  var x=Read("{action:'search',query:'酸素'}");
  Check((int)x["returned"]==1&&a.ContentReads==0&&b.ContentReads==0,"metadata search does not inspect any bodies");
  x=Read("{id:'oxygen',limit:1}");
  Check((int)x["returned"]==1&&(string)x["results"][0]["title"]=="日本語見出し","exact id and native title lookup");
  Check((string)x["results"][0]["content"]["blocks"][0]["text"]=="<b>説明</b>\n本文"&&b.ContentReads==0,"selected content only and rich text preserved");
  a.name="<link=OXYGEN>酸素</link>";
  CodexCache.entries["OXYGENMASK"]=new CodexEntry{id="OXYGENMASK",name="<link=OXYGENMASK>酸素マスク</link>",category="BUILDINGS"};
  x=Read("{action:'search',query:'酸素',limit:1}");Check((string)x["results"][0]["id"]=="OXYGEN"&&(string)x["results"][0]["displayName"]=="酸素","exact localized display name precedes partial matches");
  x=Read("{id:'POWER'}");
  Check(!(bool)x["results"][0]["content"]["complete"]&&(int)x["results"][0]["content"]["unsupportedWidgets"]["DangerousWidget"]==1,"unknown widgets reported without executing getters");
  a.StoredContent.Add(new ContentContainer {lockID="secret",content=new List<ICodexWidget>{new CodexText {text="秘密"}}});
  x=Read("{id:'OXYGEN'}");
  Check((int)x["results"][0]["content"]["lockedContainers"]==1&&!x.ToString().Contains("秘密"),"locked content excluded");
  a.StoredContent.Add(new ContentContainer {ValidDlc=false,content=new List<ICodexWidget>{new CodexText {text="別DLC"}}});
  x=Read("{id:'OXYGEN',maxChars:3}");
  Check((bool)x["results"][0]["content"]["truncated"]&&(string)x["results"][0]["content"]["blocks"][0]["text"]=="<b>","text budget enforced");
  x=Read("{id:'OXYGEN'}");Check(!x.ToString().Contains("別DLC"),"DLC content excluded");
  CodexCache.subEntries["NOTE"]=new SubEntry{id="NOTE",parentEntryID="OXYGEN",lockID="secret",contentContainers=new List<ContentContainer>{Box(new CodexText{text="秘密"})}};
  x=Read("{id:'NOTE'}");Check((string)x["results"][0]["category"]=="ELEMENTS"&&(string)x["results"][0]["content"]["reason"]=="locked","subentry metadata and lock");
  Game.Instance.unlocks.unlocked.Add("secret");x=Read("{id:'NOTE'}");Check((string)x["results"][0]["content"]["blocks"][0]["text"]=="秘密","unlocked text readable");
  a.StoredContent.Add(new ContentContainer());
  x=Read("{id:'OXYGEN'}");Check((int)x["results"][0]["content"]["missingContainers"]==1&&!(bool)x["results"][0]["content"]["complete"],"missing content is incomplete");
  b.disabled=true;Check((int)Read("{id:'POWER'}")["returned"]==0,"disabled entries excluded");
  b.disabled=false;b.ValidDlc=false;Check((int)Read("{id:'POWER'}")["returned"]==0,"wrong DLC entries excluded");
  Check((int)Read("{id:'missing'}")["returned"]==0,"missing ID empty");
  a.ContentReads=0;Read("{action:'categories'}");Check(a.ContentReads==0,"categories do not inspect bodies");
  for(int i=0;i<60;i++)CodexCache.entries["E"+i]=new CodexEntry{id="E"+i,category="OTHER"};
  x=Read("{action:'list',limit:999}");Check((int)x["returned"]==50&&(int)x["truncated"]>0,"list cap and matched count");
  x=Read("{action:'list',category:'other',limit:0}");Check((int)x["returned"]==1&&(int)x["matched"]==60,"category and lower limit");
  Check(CodexReadTools.Read(JObject.Parse("{action:'initialize'}")).IsError,"invalid action blocked");
  var panels = new CodexEntry {id="PANELS", StoredContent=new List<ContentContainer> {Box(
   new CodexSpacer(), new CodexDividerLine(),
   new CodexConversionPanel { title="変換", ins=new[]{new ElementUsage{tag=new Tag("Water"),amount=1f,continuous=true}}, outs=new[]{new ElementUsage{tag=new Tag("Oxygen"),amount=.888f,continuous=true}}, Converter=new UnityEngine.Object{name="Electrolyzer"}},
   new CodexRecipePanel {complexRecipe=new ComplexRecipe{id="R",time=40,ingredients=new[]{new ComplexRecipe.RecipeElement{material=new Tag("Dirt"),amount=50}},results=new[]{new ComplexRecipe.RecipeElement{material=new Tag("Food"),amount=1}}}},
   new CodexTemperatureTransitionPanel {transitionType="COOL",sourceElement=new Element{id=SimHashes.Oxygen,lowTemp=90.15f,lowTempTransitionTarget="LiquidOxygen"}},
   new CodexCollapsibleHeader("内側", Box(new CodexText{text="折り畳み本文"})),
   new CodexCollapsibleHeader("未解禁",new ContentContainer{lockID="never",content=new List<ICodexWidget>{new CodexText{text="漏らしてはいけない"}}}))}};
  CodexCache.entries["PANELS"]=panels;
  x=Read("{id:'PANELS',maxChars:20000}"); var body=x["results"][0]["content"];
  Check((float)body["blocks"][0]["data"]["ins"][0]["amount"]==1f,"conversion input preserved");
  Check((float)body["blocks"][1]["data"]["complexRecipe"]["ingredients"][0]["amount"]==50f,"private recipe amount backing field");
  Check((string)body["blocks"][2]["data"]["sourceElement"]["lowTempTransitionTarget"]=="LiquidOxygen","phase target preserved");
  Check(body.ToString().Contains("折り畳み本文")&&!body.ToString().Contains("漏らしてはいけない"),"nested text read and lock respected");
  Check((bool)body["unlockedContentComplete"] && !(bool)body["complete"],"locks separated from readable coverage");
  Check((int)body["layoutWidgetsSkipped"]["CodexSpacer"]==1,"layout is not missing knowledge");
  var conversion=(CodexConversionPanel)panels.StoredContent[0].content[2];
  conversion.ins[0].customFormating=(tag,amount,perCycle)=>throw new Exception("must not run formatter");
  x=Read("{id:'PANELS',maxChars:20000}");Check(x.ToString().Contains("formatter_not_executed"),"callback reported without execution");
  conversion.ins[0].customFormating=null;
  panels.StoredContent.Add(Box(new CodexText{text=new string('文',30000)+"😀終わり"}));
  string joined="",revision=null;int offset=0,pages=0;
  while(true)
  {
   var args=new JObject{{"action","page"},{"id","PANELS"},{"pageSize",997},{"offset",offset}};
   if(revision!=null)args["revision"]=revision;
   var page=Read(args.ToString());revision=(string)page["revision"];joined+=(string)page["text"];pages++;
   if(page["nextOffset"].Type==JTokenType.Null)break;
   Check((int)page["nextOffset"]>offset,"pagination advances");offset=(int)page["nextOffset"];
  }
  var joinedBody=JObject.Parse(joined)["content"];
  Check(pages>30&&!((bool)joinedBody["truncated"])&&joined.Contains("😀終わり"),"paged long content reconstructed without loss");
  var shared=Box(new CodexText{text="shared"});
  CodexCache.entries["SHARED"]=new CodexEntry{id="SHARED",StoredContent=new List<ContentContainer>{Box(new CodexCollapsibleHeader("shared title",shared)),shared}};
  Check(((JArray)Read("{id:'SHARED'}")["results"][0]["content"]["blocks"]).Count==2,"flat and collapsible shared content counted once");
  var cycle=Box();cycle.content.Add(new CodexCollapsibleHeader("cycle",cycle));
  panels.StoredContent.Add(cycle);x=Read("{id:'PANELS',maxChars:20000}");
  // Test the cycle in a short independent entry so a text budget cannot mask it.
  CodexCache.entries["CYCLE"]=new CodexEntry{id="CYCLE",StoredContent=new List<ContentContainer>{cycle}};
  Check((int)Read("{id:'CYCLE'}")["results"][0]["content"]["missingContainers"]==1,"container cycles bounded");
  panels.name="changed";
  Check(CodexReadTools.Read(new JObject{{"action","page"},{"id","PANELS"},{"revision",revision}}).IsError,"changed content rejected during paging");
  Check(CodexReadTools.Read(JObject.Parse("{action:'page'}")).IsError,"page requires exact single id");
  x=Read("{action:'list',category:'OTHER',offset:50,limit:50}");Check((int)x["returned"]==10,"metadata pagination covers final page");
  var legacy = new Dictionary<string,object>();
  LoreReadTools.AddLegacyContent(legacy, "調査済みの案内", "MISSING.STRINGS.LORE.BUILDINGS.GravitasDesk.ENTRY");
  Check(legacy["content"] == null && (string)legacy["contentStatus"] == "legacy_text_unavailable_use_database", "missing legacy keys are not presented as lore");
  LoreReadTools.AddLegacyContent(legacy, "回収済み", "旧本文");
  Check((string)legacy["content"] == "旧本文" && (string)legacy["alreadyInspectedMessage"] == "回収済み", "generic already-inspected UI message is separate from lore");
  LoreReadTools.AddLegacyContent(legacy, "", "旧本文");
  Check((string)legacy["content"] == "旧本文", "valid legacy lore retained");
  var snapshot = LoreReadTools.SnapshotUnlocks();
  Check(((List<string>)LoreReadTools.ReadNewUnlocks(snapshot)["unlockIds"]).Count == 0, "already unlocked records not misattributed");
  CodexCache.entries["LORE"] = new CodexEntry {id="LORE", StoredContent=new List<ContentContainer> {
   new ContentContainer {lockID="new-note",content=new List<ICodexWidget>{new CodexText{text="回収した記録"}}},
   new ContentContainer {lockID="still-locked",content=new List<ICodexWidget>{new CodexText{text="未発見の秘密"}}}}};
  CodexCache.LockEntries["new-note"]="LORE";
  Game.Instance.unlocks.unlocked.Add("new-note");
  Game.Instance.unlocks.unlocked.Add("non-codex-unlock");
  var recovered=JObject.FromObject(LoreReadTools.ReadNewUnlocks(snapshot));
  Check(((JArray)recovered["unlockIds"]).Count==2 && !snapshot.Contains("new-note"), "mutable game collection copied before press");
  Check(recovered.ToString().Contains("回収した記録") && !recovered.ToString().Contains("未発見の秘密"), "new unlock reads native database text while preserving remaining locks");
  Check(recovered.ToString().Contains("no_codex_mapping"), "unmapped unlocks explicitly reported");
  var cache=CodexCache.entries; CodexCache.entries=null;
  Check(JObject.FromObject(LoreReadTools.ReadNewUnlocks(snapshot)).ToString().Contains("codex_cache_not_ready"), "no cache initialization during recovery");
  CodexCache.entries=cache;
  Check(!(bool)LoreReadTools.ReadNewUnlocks(null)["available"], "unknown snapshot not treated as empty unlock set");
  CategoryMembersRegression();
  SublimationFallbackRegression();
  PreyAmountSemanticsRegression();
  ConverterNominalLightRegression();
  Console.WriteLine("PASS: Codex cache-only reads, metadata limits, locks/DLC, native text and unsupported widgets");
 }
}
