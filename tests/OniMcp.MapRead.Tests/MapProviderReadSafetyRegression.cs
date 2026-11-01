using Newtonsoft.Json.Linq;
using UnityEngine;
namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static void RunProviderGuardRegressions()
        {
            int checks=0;
            void Check(bool v,string why){checks++;if(!v)throw new Exception("map_provider_read_safety: "+why);}
            var objects=Grid.Objects;var game=Game.Instance;
            const int cell=10;
            (GameObject Go,KAnimGraphTileVisualizer Graph,TestProvider Provider,TestManager Manager) Put(ObjectLayer layer,bool physical=true)
            {
                Grid.Objects=new GameObject[16384,Enum.GetValues<ObjectLayer>().Length];Game.Instance=new Game();
                var manager=NativeReadBoundary.Manager(layer);manager.Mask=UtilityConnections.Right;
                var provider=NativeReadBoundary.Provider(layer,manager);var graph=new KAnimGraphTileVisualizer{ConnectionManager=manager,connectionManager=manager,isPhysicalBuilding=physical};
                var go=new GameObject();go.Components[typeof(KAnimGraphTileVisualizer)]=graph;go.Components[typeof(IHaveUtilityNetworkMgr)]=provider;
                if(physical)go.Components[typeof(BuildingComplete)]=new BuildingComplete();else go.Components[typeof(Constructable)]=new Constructable();
                Grid.Objects[cell,(int)layer]=go;return(go,graph,provider,manager);
            }
            try
            {
                foreach(var layer in new[]{ObjectLayer.Wire,ObjectLayer.GasConduit,ObjectLayer.LiquidConduit,ObjectLayer.LogicWire,ObjectLayer.SolidConduit})
                {
                    var f=Put(layer);
                    Check(TryGetUtilityConnections(cell,new[]{layer},out var mask)&&mask==UtilityConnections.Right,"known physical map mask "+layer);
                    Check(f.Manager.Reads.Single()==(cell,true),"physical cache only "+layer);
                    var observed=WorldUtilityPhysicalConnectionRead.Read(cell,new[]{layer});
                    Check(observed.State=="physical"&&observed.Mask==UtilityConnections.Right&&observed.HasPhysical==true&&observed.HasLayer==true,"physical summary facts "+layer);
                }
                var c=Put(ObjectLayer.Wire);var custom=new TestProvider{Manager=c.Manager};c.Go.Components[typeof(IHaveUtilityNetworkMgr)]=custom;
                Check(!TryGetUtilityConnections(cell,new[]{ObjectLayer.Wire},out _)&&custom.Reads==0,"unknown provider is refused before map callback");
                var unknown=WorldUtilityPhysicalConnectionRead.Read(cell,new[]{ObjectLayer.Wire});
                Check(unknown.Mask==null&&unknown.State=="unknown"&&unknown.HasPhysical==true&&unknown.HasLayer==true&&custom.Reads==0,"unknown physical provider preserves presence without callback");
                Check(c.Manager.Reads.Count==0,"unsupported provider never reaches mask read");
                c=Put(ObjectLayer.Wire);var derived=new DerivedReadWire{Manager=c.Manager};c.Go.Components[typeof(IHaveUtilityNetworkMgr)]=derived;
                Check(!TryGetUtilityConnections(cell,new[]{ObjectLayer.Wire},out _)&&derived.Reads==0,"custom native subclass not accepted");
                unknown=WorldUtilityPhysicalConnectionRead.Read(cell,new[]{ObjectLayer.Wire});Check(unknown.Mask==null&&derived.Reads==0,"physical summary rejects custom native subclass");
                c=Put(ObjectLayer.Wire);var badManager=new DerivedReadManager();c.Graph.connectionManager=badManager;c.Graph.ConnectionManager=badManager;
                Check(!TryGetUtilityConnections(cell,new[]{ObjectLayer.Wire},out _)&&badManager.Callbacks==0,"unknown manager subtype rejected before callback");
                unknown=WorldUtilityPhysicalConnectionRead.Read(cell,new[]{ObjectLayer.Wire});Check(unknown.Mask==null&&badManager.Callbacks==0,"unknown physical manager stays null");
                c=Put(ObjectLayer.Wire);c.Graph.connectionManager=null;
                Check(!TryGetUtilityConnections(cell,new[]{ObjectLayer.Wire},out _)&&c.Graph.CurrentReads==0&&c.Provider.Reads==0,"cold graph cache does not evaluate getter or provider");
                unknown=WorldUtilityPhysicalConnectionRead.Read(cell,new[]{ObjectLayer.Wire});Check(unknown.HasPhysical==true&&unknown.Mask==null,"cold graph preserves known physical presence");
                c=Put(ObjectLayer.Wire);c.Graph.ConnectionManager=NativeReadBoundary.Manager(ObjectLayer.Wire);
                Check(!TryGetUtilityConnections(cell,new[]{ObjectLayer.Wire},out _)&&c.Provider.Reads==0,"stale cache mismatch before provider callback");
                c=Put(ObjectLayer.Wire,false);c.Go.Components.Remove(typeof(IHaveUtilityNetworkMgr));
                string plan=UtilityConnectionReadState(cell,new[]{ObjectLayer.Wire});
                Check(plan.Contains("plannedDirs=R")&&plan.Contains("physicalConnection=not_built")&&c.Manager.Reads.Single()==(cell,false),"known planned cache does not require completed provider");
                c=Put(ObjectLayer.Wire,false);badManager=new DerivedReadManager();c.Graph.connectionManager=badManager;c.Graph.ConnectionManager=badManager;
                plan=UtilityConnectionReadState(cell,new[]{ObjectLayer.Wire});
                Check(plan.Contains("plannedMask=?")&&plan.Contains("physicalConnection=not_built")&&badManager.Callbacks==0,"planned unknown manager has no callback or physical success");
                unknown=WorldUtilityPhysicalConnectionRead.Read(cell,new[]{ObjectLayer.Wire});
                Check(unknown.State=="planned"&&unknown.HasLayer==true&&unknown.HasPhysical==false&&unknown.HasPlanned==true&&unknown.Mask==null&&badManager.Callbacks==0,"planned-only physical summary remains separately classified");
                c=Put(ObjectLayer.Wire,false);c.Graph.connectionManager=null;plan=UtilityConnectionReadState(cell,new[]{ObjectLayer.Wire});
                Check(plan.Contains("plannedMask=?")&&c.Graph.CurrentReads==0,"cold planned cache does not evaluate manager accessor");
                Console.WriteLine("Map/physical/planned provider boundary: "+checks+" checks PASS");
            }
            finally { Grid.Objects=objects;Game.Instance=game; }
        }
    }
}
public sealed class DerivedReadWire : Wire { }
public sealed class DerivedReadManager : UtilityNetworkManager<ElectricalUtilityNetwork,Wire>,IUtilityNetworkMgr
{
    public int Callbacks;
    UtilityConnections IUtilityNetworkMgr.GetConnections(int cell,bool is_physical_building){Callbacks++;return UtilityConnections.Right;}
}
