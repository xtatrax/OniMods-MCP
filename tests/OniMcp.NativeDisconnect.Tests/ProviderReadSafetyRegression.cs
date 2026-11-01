using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using OniMcp.Support;
using UnityEngine;

internal static class ProviderReadSafetyRegression
{
    private static int checks;
    private static void Check(bool ok, string message)
    { checks++; if (!ok) throw new Exception(message); }
    private static JObject Request(string type = "liquid") => new JObject
        { ["x"] = 2, ["y"] = 2, ["type"] = type, ["direction"] = "right", ["dryRun"] = true };
    private static void Reject(ConnectFixture f, IHaveUtilityNetworkMgr provider, string label, string type = "liquid")
    {
        var prefab = new GameObject(); if (provider != null) prefab.Add(provider); f.Def.BuildingComplete = prefab;
        var result = BuildPlanningTools.ConnectForTest(Request(type));
        Check(result.IsError, label + ": rejects request");
        Check(f.Tool.Events.Count == 0, label + ": no native UI callback");
        Check(f.Manager.Bits[f.A.Cell] == f.BeforeA && f.Manager.Bits[f.B.Cell] == f.BeforeB,
            label + ": connection masks unchanged");
    }
    internal static void Run()
    {
        // Start with the original defect: an unsupported prefab's arbitrary
        // interface implementation must not execute merely to reject the read.
        var f = new ConnectFixture(); f.Def.PrefabID = "Tile";
        var unknown = new CustomProvider(); Reject(f, unknown, "unsupported custom provider");
        Check(unknown.Calls == 0, "unsupported prefab must reject BEFORE custom provider invocation");
        Check(f.A.ManagerGetterCalls == 0 && f.B.ManagerGetterCalls == 0, "unsupported prefab must not evaluate native manager getter");

        f = new ConnectFixture(); unknown = new CustomProvider(); Reject(f, unknown, "supported ID with custom provider");
        Check(unknown.Calls == 0, "supported ID must not authorize an unknown provider callback");
        Check(f.A.ManagerGetterCalls == 0, "unknown provider gate precedes graph manager getter");

        f = new ConnectFixture("wire"); var derived = new CustomWire(f.Manager); Reject(f, derived, "derived native provider", "wire");
        Check(derived.Calls == 0 && derived.CustomCalls == 0 && f.A.ManagerGetterCalls == 0,
            "native subclass reimplementation is unknown and receives no callbacks");

        f = new ConnectFixture(); var wrongFamily = new Wire(f.Manager); Reject(f, wrongFamily, "wrong native provider family");
        Check(wrongFamily.Calls == 0 && f.A.ManagerGetterCalls == 0, "wrong-family native provider receives no callback");

        f = new ConnectFixture(); f.Def.PrefabID = "Tile"; var knownUnsupported = new Conduit(f.Manager);
        Reject(f, knownUnsupported, "unsupported ID with known provider");
        Check(knownUnsupported.Calls == 0 && f.A.ManagerGetterCalls == 0, "prefab gate precedes every known-provider invocation");

        f = new ConnectFixture(); f.A.connectionManager = null; var cold = new Conduit(f.Manager);
        Reject(f, cold, "missing spawned manager cache");
        Check(cold.Calls == 0 && f.A.ManagerGetterCalls == 0, "cold spawned cache must refuse before manager/provider getters");
        Check(f.Manager.ReadCalls == 0, "cold cache must not read connection manager");

        f = new ConnectFixture(); f.A.connectionManager = f.OtherManager; var stale = new Conduit(f.Manager);
        Reject(f, stale, "stale spawned manager cache");
        Check(stale.Calls == 0 && f.A.ManagerGetterCalls == 1 && f.Manager.ReadCalls == 0,
            "stale cache refuses before provider callback or connection read");

        f = new ConnectFixture(); var wrongManager = new Conduit(f.OtherManager);
        Reject(f, wrongManager, "wrong native provider manager");
        Check(wrongManager.Calls == 1 && f.A.ManagerGetterCalls == 1 && f.Manager.ReadCalls == 0,
            "ready known provider must match current manager before connection reads");

        f = new ConnectFixture(); f.A.ConnectionManager = null; var noCurrent = new Conduit(f.Manager);
        Reject(f, noCurrent, "missing current native manager");
        Check(noCurrent.Calls == 0 && f.A.ManagerGetterCalls == 1 && f.Manager.ReadCalls == 0,
            "unavailable current native manager refuses before provider read");

        f = new ConnectFixture(); Reject(f, null, "missing provider");
        Check(f.A.ManagerGetterCalls == 0 && f.Manager.ReadCalls == 0, "missing provider refuses without dependent reads");

        f = new ConnectFixture(); var customManager = new CustomConnectManager();
        f.A.connectionManager = customManager; f.A.ConnectionManager = customManager;
        var customManagerProvider = new Conduit(customManager);
        Reject(f, customManagerProvider, "custom utility manager");
        Check(customManager.CustomCalls == 0 && customManagerProvider.Calls == 0 && f.A.ManagerGetterCalls == 0,
            "unknown utility manager must refuse before getters/provider/mask callbacks");

        foreach (var entry in new[] {
            ("liquid", "LiquidConduit"), ("liquid", "InsulatedLiquidConduit"), ("liquid", "LiquidConduitRadiant"),
            ("gas", "GasConduit"), ("gas", "InsulatedGasConduit"), ("gas", "GasConduitRadiant"),
            ("solid", "SolidConduit"), ("wire", "Wire"), ("wire", "WireRefined"),
            ("wire", "WireHighWattage"), ("wire", "WireRefinedHighWattage"),
            ("logic", "LogicWire"), ("logic", LogicRibbonConfig.ID) })
        {
            f = new ConnectFixture(entry.Item1, prefabId: entry.Item2);
            var provider = f.Def.BuildingComplete.GetComponent<NativeUtilityProvider>();
            var result = BuildPlanningTools.ConnectForTest(Request(entry.Item1));
            Check(!result.IsError, entry.Item2 + ": ready known native provider preview remains supported: " + result.Content[0].Text);
            var data = JObject.Parse(result.Content[0].Text);
            Check((bool)data["success"] && (bool)data["dryRun"] && !(bool)data["actionAttempted"]
                && data["connectionVerified"].Type == JTokenType.Null, entry.Item2 + ": honest passive preview result");
            Check(provider.Calls == 2 && f.A.ManagerGetterCalls == 1 && f.B.ManagerGetterCalls == 1
                && f.Manager.ReadCalls == 2, entry.Item2 + ": each ready endpoint is read once");
            Check(f.Tool.Events.Count == 0 && f.Manager.Bits[f.A.Cell] == f.BeforeA && f.Manager.Bits[f.B.Cell] == f.BeforeB,
                entry.Item2 + ": no UI or connection mutation");
        }
        Console.WriteLine($"Existing utility provider read-safety tests passed: {checks} checks");
    }
}

// Substitute native boundaries, not game assemblies. The four exact component
// types are verified against pinned installed metadata in the pinned native metadata report.
public abstract class NativeUtilityProvider : IHaveUtilityNetworkMgr
{
    private readonly IUtilityNetworkMgr manager;
    public int Calls;
    protected NativeUtilityProvider(IUtilityNetworkMgr manager) { this.manager = manager; }
    public IUtilityNetworkMgr GetNetworkManager() { Calls++; return manager; }
}
public class Conduit : NativeUtilityProvider { public Conduit(IUtilityNetworkMgr manager) : base(manager) { } }
public class Wire : NativeUtilityProvider { public Wire(IUtilityNetworkMgr manager) : base(manager) { } }
public class LogicWire : NativeUtilityProvider { public LogicWire(IUtilityNetworkMgr manager) : base(manager) { } }
public class SolidConduit : NativeUtilityProvider { public SolidConduit(IUtilityNetworkMgr manager) : base(manager) { } }
public sealed class CustomProvider : IHaveUtilityNetworkMgr
{
    public int Calls;
    public IUtilityNetworkMgr GetNetworkManager() { Calls++; throw new Exception("unknown provider callback must never run"); }
}
public sealed class CustomWire : Wire, IHaveUtilityNetworkMgr
{
    public int CustomCalls;
    public CustomWire(IUtilityNetworkMgr manager) : base(manager) { }
    IUtilityNetworkMgr IHaveUtilityNetworkMgr.GetNetworkManager() { CustomCalls++; throw new Exception("derived callback must never run"); }
}

public sealed class CustomConnectManager:UtilityNetworkManager<FlowUtilityNetwork,Vent>,IUtilityNetworkMgr {
 public int CustomCalls;
 UtilityConnections IUtilityNetworkMgr.GetConnections(int cell,bool physical){CustomCalls++;throw new Exception("custom manager callback must never run");}
}
