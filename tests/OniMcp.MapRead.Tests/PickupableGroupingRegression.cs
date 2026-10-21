using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;

internal static class PickupableGroupingRegression
{
    private static int checks;
    private static int Cell=>Grid.XYToCell(20,20);
    private static void Check(bool value,string message) { checks++;if(!value)throw new InvalidOperationException("Pickupable grouping: "+message); }
    private static void Reset()=>Components.Pickupables.Items.Clear();
    private static Pickupable Item(string name="fixture",string prefab="Fixture",SimHashes element=SimHashes.TestElement,float mass=1f,int? cachedCell=null,int? objectCell=null,int world=0,bool storage=false,bool storedTag=false,int id=7)
    {
        var go=new GameObject{name=name,NativeCell=objectCell??Cell,WorldId=world};
        var identity=new KPrefabID{InstanceID=id,PrefabTag=new Tag(prefab)};if(storedTag)identity.Tags.Add(GameTags.Stored);
        go.Components[typeof(KPrefabID)]=identity;go.Components[typeof(PrimaryElement)]=new PrimaryElement{ElementID=element,Mass=mass};
        var item=new Pickupable{gameObject=go,cachedCell=cachedCell??Cell,storage=storage?new object():null};Components.Pickupables.Items.Add(item);return item;
    }
    private static JObject Summary(int world=0)=>JObject.Parse(JsonConvert.SerializeObject(WorldAnalysisTools.PickupableSummaryForTests(Cell,world),McpJsonUtil.Settings));
    private static JArray Groups(JObject row)=>(JArray)row["groups"];
    private static JObject Group(JObject row,string prefab)=>(JObject)Groups(row).Single(g=>(string)g["prefabId"]==prefab);
    private static void KnownTypes()
    {
        Reset();Item("meat","Meat",mass:2);Item("meat","RotPile",mass:2);var row=Summary();
        Check(Groups(row).Count==2,"same display name with different prefab stays separate");
        foreach(string prefab in new[]{"Meat","RotPile"}){var group=Group(row,prefab);Check((int)group["count"]==1&&(float)group["massKg"]==2,"correct mass and count for "+prefab);}
        Check((int)row["count"]==2&&(float)row["totalMassKg"]==4&&(int)row["stored"]==0&&(int)row["loose"]==2,"all totals unchanged for the mixed-name pair");
        Reset();Item("same","Fixture",SimHashes.TestElement,2);Item("same","Fixture",SimHashes.OtherElement,3);row=Summary();Check(Groups(row).Count==2&&Groups(row).Select(g=>(string)g["element"]).Order().SequenceEqual(new[]{"OtherElement","TestElement"}),"same name and prefab with different elements stays separate");
        Reset();Item("same","Fixture",mass:1.25f,id:1);Item("same","Fixture",mass:2.5f,id:2);row=Summary();Check(Groups(row).Count==1&&(int)Groups(row)[0]["count"]==2&&(float)Groups(row)[0]["massKg"]==3.75f,"different objects with known identical identity merge");
        Item("different","Fixture",mass:1);row=Summary();Check(Groups(row).Count==2,"different display names preserve separate groups");
        Reset();Item("a|b","c");Item("a","b|c");row=Summary();Check(Groups(row).Count==2,"structured identity cannot collide through a delimiter");
        Reset();Item("numeric","Fixture",(SimHashes)7654321,0);Item("numeric","Fixture",(SimHashes)7654321,0);row=Summary();Check(Groups(row).Count==1&&(int)Groups(row)[0]["count"]==2&&(string)Groups(row)[0]["element"]=="7654321"&&(float)row["totalMassKg"]==0,"undefined native enum numeric value and real mass zero remain known");
        Reset();Item("same","Z",SimHashes.TestElement);Item("same","A",SimHashes.OtherElement);Item("same","A",SimHashes.TestElement);row=Summary();Check(Groups(row).Select(g=>(string)g["prefabId"]+"/"+(string)g["element"]).SequenceEqual(new[]{"A/OtherElement","A/TestElement","Z/TestElement"}),"equal mass and name use deterministic prefab then element order");
    }
    private static void MissingIdentity()
    {
        Reset();Item("Unknown","Fixture");Item("Unknown","Fixture");Item(null,"Fixture");Item(" ","Fixture");var row=Summary();
        Check(Groups(row).Count==3&&Groups(row).Select(g=>(int)g["count"]).Order().SequenceEqual(new[]{1,1,2}),"missing raw names stay individual while literal Unknown remains a known name");
        Check(Groups(row).All(g=>(string)g["name"]=="Unknown")&&(int)row["count"]==4&&(float)row["totalMassKg"]==4,"fallback display label and body totals preserved");
        Reset();var a=Item("missing prefab");var b=Item("missing prefab");a.gameObject.Components.Remove(typeof(KPrefabID));b.gameObject.Components.Remove(typeof(KPrefabID));row=Summary();Check(Groups(row).Count==2&&Groups(row).All(g=>(string)g["prefabId"]==""&&(int)g["count"]==1),"missing prefab identities never merge distinct objects");
        Reset();a=Item("invalid prefab");b=Item("invalid prefab");a.KPrefabID.PrefabTag=new Tag("Fixture",false);b.KPrefabID.PrefabTag=new Tag("Fixture",false);row=Summary();Check(Groups(row).Count==2,"invalid prefab tags stay individual");
        Reset();a=Item("empty prefab","");b=Item("empty prefab","");row=Summary();Check(Groups(row).Count==2,"empty prefab text stays individual even when tag validity is present");
        Reset();a=Item("missing primary");b=Item("missing primary");a.gameObject.Components.Remove(typeof(PrimaryElement));b.gameObject.Components.Remove(typeof(PrimaryElement));
        Check(!ReferenceEquals(a.gameObject,b.gameObject)&&a.KPrefabID.Equals(b.KPrefabID),"native prefab equality is distinct from entity reference identity");
        row=Summary();Check(Groups(row).Count==2&&Groups(row).All(g=>(string)g["element"]==""&&(int)g["count"]==1&&(float)g["massKg"]==0),"missing primary stays individual without changing old mass fallback");
        Components.Pickupables.Items.Add(new Pickupable{gameObject=a.gameObject,cachedCell=Cell});row=Summary();Check(Groups(row).Count==2&&Groups(row).Select(g=>(int)g["count"]).Order().SequenceEqual(new[]{1,2})&&(int)row["count"]==3,"same unknown native object reuses identity without deduplicating registry occurrences");
    }
    private static void SelectionAndLimits()
    {
        Reset();Item(mass:1);Item(mass:2,objectCell:Cell+1,storage:true);Item(mass:3,cachedCell:Cell+1,storedTag:true);var wrongWorld=Item(mass:4,world:1,storage:true);var elsewhere=Item(mass:5,cachedCell:Cell+1,objectCell:Cell+1);Components.Pickupables.Items.Add(null);Components.Pickupables.Items.Add(new Pickupable());var row=Summary();
        Check((int)row["count"]==3&&(int)row["stored"]==2&&(int)row["loose"]==1&&(float)row["totalMassKg"]==6&&Groups(row).Count==1,"cached or object cell match, storage and Stored tag, world and null filtering preserve totals");
        Check((int)Groups(row)[0]["stored"]==2&&(int)Groups(row)[0]["loose"]==1&&wrongWorld.gameObject.NameReads==0&&elsewhere.gameObject.NameReads==0,"excluded entries are not name-read or aggregated");
        row=Summary(-1);Check((int)row["count"]==4&&(float)row["totalMassKg"]==10,"negative world selector retains existing all-world meaning");
        Reset();row=Summary();Check((int)row["count"]==0&&(int)row["stored"]==0&&(int)row["loose"]==0&&(float)row["totalMassKg"]==0&&Groups(row).Count==0&&(int)row["truncated"]==0,"empty observation remains real zero");
        for(int i=1;i<=18;i++)Item("fixture "+i,"Prefab"+i,mass:i,storage:i%2==0);row=Summary();Check(Groups(row).Count==16&&(int)row["truncated"]==2&&(int)row["count"]==18&&(int)row["stored"]==9&&(int)row["loose"]==9&&(float)row["totalMassKg"]==171,"limit applies to groups only, totals still include every selected item");
        Check(Groups(row).Select(g=>(float)g["massKg"]).SequenceEqual(Enumerable.Range(3,16).Reverse().Select(x=>(float)x)),"mass descending order and unshown groups preserved");
        Check(row.Properties().Select(p=>p.Name).SequenceEqual(new[]{"count","stored","loose","totalMassKg","groups","truncated"}),"top-level JSON body keys preserved");
        Check(((JObject)Groups(row)[0]).Properties().Select(p=>p.Name).SequenceEqual(new[]{"name","prefabId","element","count","stored","loose","massKg"}),"group JSON body keys preserved without internal key leakage");
    }
    private static void RepeatRead()
    {
        Reset();var a=Item("repeat","Fixture",mass:2,storage:true);var b=Item(null,"Fixture",mass:3);var items=Components.Pickupables.Items.ToArray();var aComponents=a.gameObject.Components.ToArray();var bComponents=b.gameObject.Components.ToArray();var storage=a.storage;var first=Summary();var next=Summary();
        Check(JToken.DeepEquals(first,next)&&Components.Pickupables.Items.SequenceEqual(items)&&a.gameObject.Components.SequenceEqual(aComponents)&&b.gameObject.Components.SequenceEqual(bComponents)&&ReferenceEquals(storage,a.storage)&&a.PrimaryElement.Mass==2&&b.PrimaryElement.Mass==3,"repeated actual summary reads preserve registry components mass and storage");
    }
    internal static void Run()
    {
        var before=Components.Pickupables.Items.ToArray();
        try{KnownTypes();MissingIdentity();SelectionAndLimits();RepeatRead();Console.WriteLine("Pickupable grouping: "+checks+" checks passed (full production summary and serializer; native inputs substituted)");}
        finally{Components.Pickupables.Items.Clear();Components.Pickupables.Items.AddRange(before);}
    }
}
