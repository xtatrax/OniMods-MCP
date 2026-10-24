using System.Globalization;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;

internal static class PickupableDetailRegression
{
    private static int checks;
    private static int Cell => Grid.XYToCell(20,20);
    private static void Check(bool value,string message)
    {
        checks++;
        if(!value) throw new InvalidOperationException("Pickup detail: "+message);
    }
    private static void Reset() => Components.Pickupables.Items.Clear();
    private static Pickupable Item(string name="native fixture",string prefab="Fixture",float? mass=1f,
        int? cached=null,int? actual=null,bool storage=false,bool storedTag=false)
    {
        var go=new GameObject { name=name,NativeCell=actual??Cell };
        go.transform.position=new Vector3(20.25f,20.75f,0f);
        var identity=new KPrefabID {InstanceID=7,PrefabTag=new Tag(prefab)};
        if(storedTag) identity.Tags.Add(GameTags.Stored);
        go.Components[typeof(KPrefabID)]=identity;
        if(mass.HasValue) go.Components[typeof(PrimaryElement)]=new PrimaryElement {ElementID=SimHashes.TestElement,Mass=mass.Value};
        var item=new Pickupable {gameObject=go,cachedCell=cached??Cell,storage=storage?new object():null};
        Components.Pickupables.Items.Add(item);
        return item;
    }
    private static string Read(JObject args=null) => WorldEditorTools.PickupableDetailForTests(Cell,args??new JObject());
    private static string L(string key) => McpLocalization.Get("cell.pickups."+key);
    private static string Row(string text,string starts) => text.Split('\n').Single(line=>line.StartsWith(starts,StringComparison.Ordinal)).TrimEnd('\r');
    private static string ItemRow(string text,string name) => Row(text,"  - "+name+" |");
    private static void MissingAndKnown()
    {
        Reset();Item(mass:0f);
        var text=Read();
        Check(text.Contains("0.00 kg")&&!text.Contains(L("mass_unavailable")),"real zero remains known");
        Check(Row(text,"- "+L("items")).Contains("1"+L("count")+"0.00 kg"),"known zero summary retains exact unit and format");

        Reset();Item(name:"missing",mass:null);
        text=Read();
        Check(Row(text,"- "+L("items")).Contains("1"+L("count")+L("mass_unavailable")),"all-missing total is unknown");
        Check(Row(text,"  - missing (").Contains("mass="+L("mass_unavailable")),"all-missing group is unknown");
        Check(ItemRow(text,"missing").Contains(L("element")+"=? | "+L("mass_unavailable")),"missing element and item mass remain unknown");
        Check(!text.Contains("0.00 kg"),"missing primary never prints fabricated zero mass");

        Reset();Item(name:"zero",mass:0f);Item(name:"unknown",mass:null);
        text=Read();
        Check(Row(text,"- "+L("items")).Contains(McpLocalization.Format("cell.pickups.mass_partial",null,"0.00",1)),
            "known zero plus missing mass remains an explicit partial subtotal");

        Reset();Item(name:"known",mass:2.5f);Item(name:"unknown",mass:null);Item(name:"zero",mass:0f);
        text=Read();
        string partial=McpLocalization.Format("cell.pickups.mass_partial",null,"2.50",1);
        Check(Row(text,"- "+L("items")).Contains("3"+L("count")+partial),"mixed summary reports known subtotal and missing item count");
        Check(ItemRow(text,"known").Contains("2.50 kg")&&ItemRow(text,"zero").Contains("0.00 kg")&&ItemRow(text,"unknown").Contains(L("mass_unavailable")),"mixed items distinguish positive zero and unknown");
        Check(text.IndexOf("  - known |",StringComparison.Ordinal)<text.IndexOf("  - unknown |",StringComparison.Ordinal)
            &&text.IndexOf("  - unknown |",StringComparison.Ordinal)<text.IndexOf("  - zero |",StringComparison.Ordinal),"sort keeps positive mass first and stable zero/missing input tie");

        Reset();Item("known A","Same",1.25f);Item("known A","Same",2.5f);Item("unknown A","Missing",null);Item("unknown A","Missing",null);
        text=Read();
        Check(Row(text,"  - known A (").Contains("count=2, mass=3.75 kg"),"known identity grouping preserved");
        Check(Row(text,"  - unknown A (").Contains("count=2, mass="+L("mass_unavailable")),"missing-primary grouping preserved without fabricated mass");
        Check(Row(text,"- "+L("items")).Contains(McpLocalization.Format("cell.pickups.mass_partial",null,"3.75",2)),"mixed summary counts every unknown entry");
    }
    private static void SelectionAndLimits()
    {
        Reset();
        Check(Read()=="","empty cell omits whole detail block");
        var cached=Item("cached","Cached",1,actual:Cell+1,storage:true);
        var actual=Item("actual","Actual",2,cached:Cell+1,storedTag:true);
        var loose=Item("loose","Loose",3);
        var absent=Item("absent","Absent",4,cached:Cell+1,actual:Cell+1);
        Components.Pickupables.Items.Add(null);Components.Pickupables.Items.Add(new Pickupable());
        string text=Read();
        Check(text.Contains("3"+L("count")+"6.00 kg, loose=1, stored=2"),"cached OR object cell, count, storage and Stored tag preserved");
        Check(text.Contains("cachedCell="+cached.cachedCell+" | objectCell="+cached.gameObject.NativeCell),"cached/object disagreement is retained");
        Check(text.Contains("pos=(20.25,20.75)")&&!text.Contains("absent")&&absent.gameObject.NameReads==0,"native position retained and outside/null entries skipped");
        Check(text.Contains("ID=Cached")&&text.Contains(L("element")+"=TestElement"),"native IDs are preserved");
        Check(text.Contains("state=loose")&&text.Contains("state=stored"),"storage states are unchanged");
        var registry=Components.Pickupables.Items.ToArray();
        float mass=loose.PrimaryElement.Mass;
        Check(Read()==text&&registry.SequenceEqual(Components.Pickupables.Items)&&loose.PrimaryElement.Mass==mass,"repeated read preserves observations and native mass");

        Reset();
        for(int i=1;i<=10;i++) Item("item "+i,"P"+i,i);
        text=Read();
        Check(text.Contains("shown=8, truncated=true, itemLimit=8")&&text.Contains("10"+L("count")+"55.00 kg"),"default eight-row limit keeps complete totals");
        Check(text.Split('\n').Count(line=>line.StartsWith("  - item ",StringComparison.Ordinal))==16,"default group and individual limits remain separate");
        text=Read(new JObject {["itemLimit"]=1});
        Check(text.Contains("shown=1, truncated=true, itemLimit=1")&&text.Contains("  - item 10 |"),"explicit limit retains descending highest mass");
        text=Read(new JObject {["includeAllItems"]=true});
        Check(text.Contains("shown=10, truncated=false, itemLimit=1000")&&text.Contains("  - item 1 |"),"includeAllItems retains existing 1000 ceiling");
        Check(Read(new JObject {["fullItems"]=true})==text,"fullItems legacy alias preserved");
        Check(Read(new JObject {["itemLimit"]=0}).Contains("itemLimit=1"),"lower item limit clamps to one");
        Check(Read(new JObject {["itemLimit"]=1001}).Contains("itemLimit=1000"),"upper item limit clamps to 1000");
    }
    private static void Languages()
    {
        var originalLanguage=McpLocalization.LanguageProvider;
        try
        {
            foreach(string language in new[]{"en","ja","zh-Hans"})
            {
                McpLocalization.LanguageProvider=()=>language;
                Reset();Item("日本語native","Fixture",2.5f);Item("欠測native","Missing",null);
                string text=Read();
                foreach(string suffix in new[]{"title","items","count","details","groups","element"})
                    Check(text.Contains(L(suffix)),language+" resolves existing label "+suffix);
                Check(text.Contains("日本語native | ID=Fixture")&&text.Contains("欠測native | ID=Missing")&&text.Contains("TestElement"),language+" leaves native names and IDs unchanged");
                Check(text.Contains(McpLocalization.Format("cell.pickups.mass_partial",null,"2.50",1))&&text.Contains(L("mass_unavailable")),language+" resolves both new mass concepts");
                Check(!text.Contains("cell.pickups."),language+" has no unresolved key");
            }
        }
        finally {McpLocalization.LanguageProvider=originalLanguage;}
    }
    internal static void Run()
    {
        var saved=Components.Pickupables.Items.ToArray();
        var savedCulture=CultureInfo.CurrentCulture;
        var savedLanguage=McpLocalization.LanguageProvider;
        var savedGameLanguage=McpLocalization.GameLanguageProvider;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
            McpLocalization.LanguageProvider=()=>"en";McpLocalization.GameLanguageProvider=()=>"ja";
            MissingAndKnown();SelectionAndLimits();Languages();
            Console.WriteLine("Pickup detail: "+checks+" assertions (full production Markdown source; native inputs substituted)");
        }
        finally
        {
            Components.Pickupables.Items.Clear();Components.Pickupables.Items.AddRange(saved);
            CultureInfo.CurrentCulture=savedCulture;McpLocalization.LanguageProvider=savedLanguage;McpLocalization.GameLanguageProvider=savedGameLanguage;
        }
    }
}
