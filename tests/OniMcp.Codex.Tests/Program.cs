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
   new CodexTemperatureTransitionPanel {transitionType="COOL",sourceElement=new Element{id="Oxygen",lowTemp=90.15f,lowTempTransitionTarget="LiquidOxygen"}},
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
  conversion.ins[0].customFormating=()=>throw new Exception("must not run formatter");
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
  Console.WriteLine("PASS: Codex cache-only reads, metadata limits, locks/DLC, native text and unsupported widgets");
 }
}
