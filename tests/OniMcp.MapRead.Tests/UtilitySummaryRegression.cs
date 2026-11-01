using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldAnalysisTools
    {
        internal static Dictionary<string,object> UtilitySummaryForTests(int cell) => CellUtilityConnectionSummary(cell);
        internal static void RunUtilitySummaryRegressions() => UtilitySummaryRegression.Run();
    }
}
internal static class UtilitySummaryRegression
{
    private static int checks;
    private const int X=20,Y=20;
    private static int Cell=>Grid.XYToCell(X,Y);
    private static readonly (string Kind,ObjectLayer Main,ObjectLayer Tile,ObjectLayer Replacement)[] Families={
        ("power",ObjectLayer.Wire,ObjectLayer.WireTile,ObjectLayer.ReplacementWire),
        ("liquid",ObjectLayer.LiquidConduit,ObjectLayer.LiquidConduitTile,ObjectLayer.ReplacementLiquidConduit),
        ("gas",ObjectLayer.GasConduit,ObjectLayer.GasConduitTile,ObjectLayer.ReplacementGasConduit),
        ("logic",ObjectLayer.LogicWire,ObjectLayer.LogicWireTile,ObjectLayer.ReplacementLogicWire),
        ("rail",ObjectLayer.SolidConduit,ObjectLayer.SolidConduitTile,ObjectLayer.ReplacementSolidConduit)};
    private static void Check(bool good,string label) { checks++;if(!good)throw new InvalidOperationException("Utility summary: "+label); }
    private static void Null(JToken value,string label)=>Check(value!=null&&value.Type==JTokenType.Null,label+" explicit null");
    private static void Reset()
    { Grid.Objects=new GameObject[16384,Enum.GetValues<ObjectLayer>().Length];Game.Instance=new Game{circuitManager=new TestCircuitManager()};UtilitySummaryBoundary.ForbiddenCalls=0; }
    private static (GameObject Go,TestProvider Provider,TestManager Manager) Complete(ObjectLayer layer,UtilityConnections mask,int? cell=null)
    {
        int c=cell??Cell;var go=new GameObject {name="Utility fixture",NativeCell=c};var manager=NativeReadBoundary.Manager(layer);manager.Mask=mask;var provider=NativeReadBoundary.Provider(layer,manager);
        go.Components[typeof(BuildingComplete)]=new BuildingComplete();go.Components[typeof(IHaveUtilityNetworkMgr)]=provider;go.Components[typeof(KAnimGraphTileVisualizer)]=new KAnimGraphTileVisualizer{ConnectionManager=manager,connectionManager=manager};Grid.Objects[c,(int)layer]=go;return(go,provider,manager);
    }
    private static GameObject Plan(ObjectLayer layer,int? cell=null,TestManager manager=null)
    {
        int c=cell??Cell;var go=new GameObject{name="Utility plan",NativeCell=c};go.Components[typeof(Constructable)]=new Constructable();
        go.Components[typeof(KAnimGraphTileVisualizer)]=new KAnimGraphTileVisualizer{ConnectionManager=manager};
        if(manager!=null)go.Components[typeof(IHaveUtilityNetworkMgr)]=new TestProvider{Manager=manager};Grid.Objects[c,(int)layer]=go;return go;
    }
    private static JObject Summary(int? cell=null)=>JObject.Parse(JsonConvert.SerializeObject(WorldAnalysisTools.UtilitySummaryForTests(cell??Cell),McpJsonUtil.Settings));
    private static JObject Gas(int? cell=null)=>(JObject)Summary(cell)["gas"];
    private static void Unknown(JObject row,string state,string reason)
    {
        Check((string)row["connectionState"]==state,"unknown/planned classification "+reason);
        foreach(string key in new[]{"glyph","dirs","links","open","to"})Check((string)row[key]=="?",state+" does not invent "+key);
        Null(row["nativePhysicalMask"],state+" mask");Null(row["connectionSource"],state+" source");
        if(reason!=null)Check((string)row["reasonCode"]==reason,"unknown reason "+reason);
    }
    private static void GasCounterexample()
    {
        Reset();var center=Complete(ObjectLayer.GasConduit,UtilityConnections.Down);var neighbors=new List<GameObject>();
        foreach(var delta in new[]{(0,1),(0,-1),(1,0)}){var go=new GameObject{name="Adjacent occupancy only"};Grid.Objects[Grid.XYToCell(X+delta.Item1,Y+delta.Item2),(int)ObjectLayer.GasConduit]=go;neighbors.Add(go);}
        var gas=Gas();
        Check((string)gas["dirs"]=="D","native Down excludes merely adjacent U/R");
        Check((string)gas["links"]=="D:(20,19)"&&(string)gas["to"]=="(20,19)"&&(string)gas["open"]=="U:(20,21),R:(21,20)","links and open distinguish local physical bit from adjacency");
        Check((bool)gas["hasLayer"]&&(bool)gas["hasPhysical"]&&!(bool)gas["hasPlanned"]&&(int)gas["nativePhysicalMask"]==8&&(string)gas["connectionSource"]=="native_physical","native raw Down8 and complete source retained");
        Check(center.Manager.Reads.Count==1&&center.Manager.Reads[0]==(Cell,true)&&neighbors.All(g=>g.ComponentReads==0),"only local physical cache read without neighbor evaluation");
        Grid.Objects[Grid.XYToCell(X,Y-1),(int)ObjectLayer.GasConduit]=null;
        gas=Gas();Check((string)gas["dirs"]=="D"&&(string)gas["links"]=="D:(20,19)","local mask does not invent reciprocal target validation");
        Grid.Objects[Cell,(int)ObjectLayer.GasConduit]=null;gas=Gas();
        Check(!(bool)gas["hasLayer"]&&!(bool)gas["hasPhysical"]&&!(bool)gas["hasPlanned"]&&(string)gas["connectionState"]=="absent","absent center not inferred from neighbors");
        foreach(string key in new[]{"glyph","dirs","links","open","to"})Check((string)gas[key]==".","absent "+key+" remains absent");Null(gas["nativePhysicalMask"],"absent mask");
    }
    private static void NativeValuesAndFamilies()
    {
        Check((int)UtilityConnections.Left==1&&(int)UtilityConnections.Right==2&&(int)UtilityConnections.Up==4&&(int)UtilityConnections.Down==8,"native enum bit input contract");
        Reset();var center=Complete(ObjectLayer.GasConduit,0);var gas=Gas();
        Check((int)gas["nativePhysicalMask"]==0&&(string)gas["connectionState"]=="physical"&&(string)gas["glyph"]=="*"&&(string)gas["dirs"]=="."&&(string)gas["links"]=="."&&(string)gas["to"]==".","known mask zero is not unknown");Null(gas["reasonCode"],"known zero reason");
        foreach(var pair in new[]{(UtilityConnections.Up,"U","(20,21)"),(UtilityConnections.Down,"D","(20,19)"),(UtilityConnections.Left,"L","(19,20)"),(UtilityConnections.Right,"R","(21,20)")})
        { center.Manager.Mask=pair.Item1;gas=Gas();Check((string)gas["dirs"]==pair.Item2&&(string)gas["to"]==pair.Item3&&(int)gas["nativePhysicalMask"]==(int)pair.Item1,"native named direction "+pair.Item2); }
        Reset();var masks=new[]{UtilityConnections.Left,UtilityConnections.Right,UtilityConnections.Up,UtilityConnections.Down,(UtilityConnections)15};var dirs=new[]{"L","R","U","D","UDLR"};
        for(int i=0;i<Families.Length;i++)Complete(Families[i].Main,masks[i]);
        var all=Summary();Check(all.Properties().Select(p=>p.Name).SequenceEqual(new[]{"power","liquid","gas","logic","rail"}),"five legacy family keys preserved");
        for(int i=0;i<Families.Length;i++)Check((string)all[Families[i].Kind]["dirs"]==dirs[i]&&(int)all[Families[i].Kind]["nativePhysicalMask"]==(int)masks[i],"independent family native cache "+Families[i].Kind);
    }
    private static void MissingAndPlans()
    {
        foreach(string kind in new[]{"provider","manager","provider_fault","mask_fault","game","component_fault","unclassified"})
        {
            Reset();var c=Complete(ObjectLayer.GasConduit,UtilityConnections.Right);string reason;
            if(kind=="provider"){c.Go.Components.Remove(typeof(IHaveUtilityNetworkMgr));reason="utility_provider_unsupported";}
            else if(kind=="manager"){c.Provider.Manager=null;reason="provider_manager_mismatch";}
            else if(kind=="provider_fault"){c.Provider.ManagerReader=()=>throw new InvalidOperationException();reason="physical_mask_read_failed";}
            else if(kind=="mask_fault"){c.Manager.Reader=(_,_)=>throw new InvalidOperationException();reason="physical_mask_read_failed";}
            else if(kind=="game"){Game.Instance=null;reason="game_unavailable";}
            else if(kind=="component_fault"){c.Go.ComponentReader=_=>throw new InvalidOperationException();reason="object_state_read_failed";}
            else{c.Go.Components.Remove(typeof(BuildingComplete));c.Go.Components.Remove(typeof(KAnimGraphTileVisualizer));reason="object_state_unavailable";}
            var gas=Gas();Unknown(gas,"unknown",reason);Check((bool)gas["hasLayer"],kind+" occupancy retained independently");
            Check(c.Manager.Reads.All(x=>x.Physical),kind+" never requests combined visual/physical mask");
        }
        Reset();var plannedManager=new TestManager{NonPhysicalRead=UtilityConnections.Up|UtilityConnections.Right,PhysicalRead=UtilityConnections.Down};Plan(ObjectLayer.GasConduit,manager:plannedManager);
        var row=Gas();Unknown(row,"planned",null);Check((bool)row["hasLayer"]&&!(bool)row["hasPhysical"]&&(bool)row["hasPlanned"]&&plannedManager.Reads.Count==0,"planned-only never reads either physical or combined cache");
        var complete=Complete(ObjectLayer.GasConduit,UtilityConnections.Down);Plan(ObjectLayer.ReplacementGasConduit,manager:plannedManager);row=Gas();
        Check((string)row["dirs"]=="D"&&(bool)row["hasPhysical"]&&(bool)row["hasPlanned"]&&plannedManager.Reads.Count==0&&complete.Manager.Reads.Single().Physical,"complete plus replacement keeps planned marker out of physical bits");
        complete.Go.Components.Remove(typeof(BuildingComplete));complete.Go.Components[typeof(KAnimGraphTileVisualizer)]=new KAnimGraphTileVisualizer{isPhysicalBuilding=true,ConnectionManager=complete.Manager,connectionManager=complete.Manager};row=Gas();Check((string)row["connectionState"]=="physical"&&(string)row["dirs"]=="D","native physical graph marker accepted");
        var first=complete.Go;Grid.Objects[Cell,(int)ObjectLayer.GasConduitTile]=first;int count=complete.Manager.Reads.Count;row=Gas();Check(complete.Manager.Reads.Count==count+1&&(string)row["dirs"]=="D","same native object in two family slots reads mask once");
        Grid.Objects[Cell,(int)ObjectLayer.ReplacementGasConduit]=null;var other=Complete(ObjectLayer.GasConduitTile,UtilityConnections.Down);count=complete.Manager.Reads.Count;row=Gas();Unknown(row,"unknown","manager_ambiguous");Check(complete.Manager.Reads.Count==count&&other.Manager.Reads.Count==0,"distinct managers do not select a first arbitrary mask");
        Reset();var shared=Complete(ObjectLayer.Wire,UtilityConnections.Right);Grid.Objects[Cell,(int)ObjectLayer.GasConduit]=shared.Go;var all=Summary();Check((string)all["power"]["dirs"]=="R"&&(string)all["gas"]["connectionState"]=="unknown"&&all["gas"]["nativePhysicalMask"].Type==JTokenType.Null&&shared.Manager.Reads.Count==1,"wrong native provider family remains unknown, not cross-family connectivity");
    }
    private static void CircuitBoundsAndEndpoints()
    {
        Reset();var center=Complete(ObjectLayer.Wire,0);var neighbor=Complete(ObjectLayer.Wire,UtilityConnections.Left,Grid.XYToCell(X+1,Y));Game.Instance.circuitManager.Values[Cell]=7;Game.Instance.circuitManager.Values[Grid.XYToCell(X+1,Y)]=7;
        var power=Summary()["power"];Check((int)power["circuitId"]==7&&(string)power["dirs"]=="."&&(string)power["open"]=="R:(21,20)","same power circuit is not a local edge");
        Game.Instance.circuitManager.Reader=_=>throw new InvalidOperationException();center.Manager.Mask=UtilityConnections.Down;power=Summary()["power"];Check((int)power["circuitId"]==ushort.MaxValue&&(string)power["dirs"]=="D","circuit missing sentinel cannot cause adjacency fallback");
        foreach(var edge in new[]{(0,8,UtilityConnections.Left,"L"),(127,8,UtilityConnections.Right,"R"),(8,0,UtilityConnections.Down,"D"),(8,127,UtilityConnections.Up,"U")})
        { Reset();int cell=Grid.XYToCell(edge.Item1,edge.Item2);Complete(ObjectLayer.GasConduit,edge.Item3,cell);var gas=Gas(cell);Check((string)gas["dirs"]==edge.Item4&&(string)gas["links"]==edge.Item4+":invalid"&&(string)gas["to"]=="invalid","out-of-two-dimensional-grid bit never wraps "+edge.Item4); }
        Reset();int boundary=Grid.XYToCell(0,8);Complete(ObjectLayer.GasConduit,0,boundary);Complete(ObjectLayer.GasConduit,0,Grid.XYToCell(127,7));Check((string)Gas(boundary)["open"]==".","neighbor occupancy across row boundary is not open");
        Reset();var facility=new GameObject{name="CachedFixtureBridge",NativeCell=Cell};var building=new Building{Def=new BuildingDef{RequiresPowerInput=true,RequiresPowerOutput=true},PowerInput=Grid.XYToCell(17,20),PowerOutput=Grid.XYToCell(23,20),UtilityInput=Grid.XYToCell(19,20),UtilityOutput=Grid.XYToCell(21,20)};
        facility.Components[typeof(Building)]=building;facility.Components[typeof(ConduitConsumer)]=new ConduitConsumer{ConduitType=ConduitType.Gas};facility.Components[typeof(ConduitDispenser)]=new ConduitDispenser{ConduitType=ConduitType.Gas};facility.Components[typeof(SolidConduitConsumer)]=new SolidConduitConsumer();facility.Components[typeof(SolidConduitDispenser)]=new SolidConduitDispenser();
        facility.Components[typeof(LogicPorts)]=new LogicPorts{inputPortInfo=new[]{new LogicPorts.Port{NativeCell=Grid.XYToCell(18,20)},new LogicPorts.Port{NativeCell=Grid.XYToCell(18,21)}},outputPortInfo=new[]{new LogicPorts.Port{NativeCell=Grid.XYToCell(22,20)}}};Grid.Objects[Cell,(int)ObjectLayer.Building]=facility;
        var all=Summary();Check((string)all["gas"]["endpoints"]["input"]["symbol"]=="⊗"&&(int)all["gas"]["endpoints"]["input"]["cell"]==building.UtilityInput&&(string)all["gas"]["endpoints"]["output"]["symbol"]=="⊙"&&(string)all["gas"]["bridge"]=="from:(19,20) via:(20,20)⌒ to:(21,20)","legacy gas endpoints and bridge output preserved independent from layer absence");
        Check(all["liquid"]["endpoints"]==null&&(int)all["rail"]["endpoints"]["output"]["cell"]==building.UtilityOutput&&(int)all["power"]["endpoints"]["input"]["cell"]==building.PowerInput&&(int)all["power"]["endpoints"]["output"]["cell"]==building.PowerOutput,"family-specific legacy endpoint fields retained");
        Check((int)all["logic"]["endpoints"]["input"]["count"]==2&&(int)all["logic"]["endpoints"]["input"]["cell"]==Grid.XYToCell(18,20)&&(int)all["logic"]["endpoints"]["output"]["cell"]==Grid.XYToCell(22,20),"legacy logic endpoint first cell and count retained");
        facility.Components[typeof(NativeLogicRoute)]=new NativeLogicRoute{From=Grid.XYToCell(16,20),To=Grid.XYToCell(24,20),Registered=true};all=Summary();Check((int)all["logic"]["endpoints"]["input"]["cell"]==Grid.XYToCell(16,20)&&(string)all["logic"]["bridge"]=="from:(16,20) via:(20,20)⌒ to:(24,20)","unchanged logic helper route inputs forwarded");
    }
    private static void RepeatAndReadOnly()
    {
        Reset();var c=Complete(ObjectLayer.GasConduit,UtilityConnections.Up|UtilityConnections.Down);c.Manager.NonPhysicalRead=UtilityConnections.Left|UtilityConnections.Right;var objects=(GameObject[,])Grid.Objects.Clone();var components=c.Go.Components.ToArray();int moves=CameraController.Instance.Moves,changes=OverlayScreen.Instance.Changes;var before=Gas();var after=Gas();
        Check(JToken.DeepEquals(before,after)&&Grid.Objects.Cast<GameObject>().SequenceEqual(objects.Cast<GameObject>())&&c.Go.Components.SequenceEqual(components)&&c.Manager.Mask==(UtilityConnections.Up|UtilityConnections.Down)&&c.Manager.NonPhysicalRead==(UtilityConnections.Left|UtilityConnections.Right),"repeat keeps grid/components/native masks unchanged");
        Check(c.Manager.Reads.Count==2&&c.Manager.Reads.All(x=>x.Cell==Cell&&x.Physical)&&UtilitySummaryBoundary.ForbiddenCalls==0&&CameraController.Instance.Moves==moves&&OverlayScreen.Instance.Changes==changes,"summary never reads combined grid or mutates camera/overlay/network");
    }
    internal static void Run()
    {
        var objects=Grid.Objects;var game=Game.Instance;
        try{GasCounterexample();NativeValuesAndFamilies();MissingAndPlans();CircuitBoundsAndEndpoints();RepeatAndReadOnly();Console.WriteLine("Utility connection summary: "+checks+" checks passed (full native-mask reader and summary; inputs substituted)");}
        finally{Grid.Objects=objects;Game.Instance=game;}
    }
}
