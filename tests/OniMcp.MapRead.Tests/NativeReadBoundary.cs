using UnityEngine;

namespace UnityEngine { public class Component { } }
public class FlowUtilityNetwork { } public class ElectricalUtilityNetwork { } public class LogicCircuitNetwork { } public class Vent { }
public class UtilityNetworkManager<TNetwork,TLink> : TestManager { }
public class Conduit : TestProvider { } public class Wire : TestProvider { } public class LogicWire : TestProvider { } public class SolidConduit : TestProvider { }
internal static class NativeReadBoundary
{
    internal static TestManager Manager(ObjectLayer layer)=>layer switch
    {
        ObjectLayer.Wire or ObjectLayer.WireTile or ObjectLayer.ReplacementWire=>new UtilityNetworkManager<ElectricalUtilityNetwork,Wire>(),
        ObjectLayer.LogicWire or ObjectLayer.LogicWireTile or ObjectLayer.ReplacementLogicWire=>new UtilityNetworkManager<LogicCircuitNetwork,LogicWire>(),
        ObjectLayer.SolidConduit or ObjectLayer.SolidConduitTile or ObjectLayer.ReplacementSolidConduit=>new UtilityNetworkManager<FlowUtilityNetwork,SolidConduit>(),
        _=>new UtilityNetworkManager<FlowUtilityNetwork,Vent>()
    };
    internal static TestProvider Provider(ObjectLayer layer,TestManager manager)=>layer switch
    {
        ObjectLayer.Wire or ObjectLayer.WireTile or ObjectLayer.ReplacementWire=>new Wire{Manager=manager},
        ObjectLayer.LogicWire or ObjectLayer.LogicWireTile or ObjectLayer.ReplacementLogicWire=>new LogicWire{Manager=manager},
        ObjectLayer.SolidConduit or ObjectLayer.SolidConduitTile or ObjectLayer.ReplacementSolidConduit=>new SolidConduit{Manager=manager},
        _=>new Conduit{Manager=manager}
    };
}
