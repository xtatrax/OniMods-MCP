using Newtonsoft.Json.Linq;
using OniMcp.Tools;
internal static class DisconnectReadSafetyRegression
{
    private static int checks;
    private static void Check(bool v,string why){checks++;if(!v)throw new Exception("disconnect_preflight_cached_manager: "+why);}
    private static JObject Request()=>new JObject{["x"]=2,["y"]=2,["direction"]="right",["type"]="wire",["worldId"]=0,["dryRun"]=true};
    internal static void Run()
    {
        foreach(string lang in new[]{"en","ja","zh-Hans"})Check(OniMcp.Support.McpLocalization.GetForLanguage(lang,"utility_read_safety.state_unavailable")!="utility_read_safety.state_unavailable","runtime localized refusal "+lang);
        var f=new Fixture();var custom=new UnknownDisconnectManager();custom.Bits[f.A.Cell]=UtilityConnections.Right;custom.Bits[f.B.Cell]=UtilityConnections.Left;
        f.A.connectionManager=custom;f.A.ConnectionManager=custom;f.B.connectionManager=custom;f.B.ConnectionManager=custom;
        var raw=OrdersTools.Disconnect(Request());var result=JObject.Parse(raw.Content[0].Text);
        Check(raw.IsError&&custom.Callbacks==0,"custom manager interface never invoked during dry-run");
        Check((string)result["reasonCode"]=="utility_preflight_read_unavailable"&&result["wouldDisconnectEdges"].Type==JTokenType.Null,"unknown is explicit refusal, not zero edges");
        Check((int)result["nativeCalls"]==0&&f.Tool.Calls.Count==0&&result["connectionVerified"].Type==JTokenType.Null,"no native write or verification claim");
        f=new Fixture();f.A.connectionManager=null;int getters=f.A.ManagerGetterCalls;
        raw=OrdersTools.Disconnect(Request());result=JObject.Parse(raw.Content[0].Text);
        Check(raw.IsError&&f.A.ManagerGetterCalls==getters&&f.Tool.Calls.Count==0,"cold cache refuses without accessor or UI");
        f=new Fixture();var other=NativeManagerFixture.Create("wire");f.B.connectionManager=other;f.B.ConnectionManager=other;
        raw=OrdersTools.Disconnect(Request());result=JObject.Parse(raw.Content[0].Text);
        Check(raw.IsError&&(string)result["readReasonCode"]=="utility_manager_mismatch"&&f.Tool.Calls.Count==0,"different ready native caches are not empty/no-op");
        f=new Fixture();raw=OrdersTools.Disconnect(Request());result=JObject.Parse(raw.Content[0].Text);
        Check(!raw.IsError&&(int)result["wouldDisconnectEdges"]==1&&f.Tool.Calls.Count==0,"ready planned/physical graph mask preview retained");
        Check(f.A.gameObject.GetComponent<BuildingComplete>()==null&&f.B.gameObject.GetComponent<BuildingComplete>()==null,"positive has no completed-building requirement");
        f=new Fixture();f.Manager.ReadError=true;raw=OrdersTools.Disconnect(Request());result=JObject.Parse(raw.Content[0].Text);
        Check(raw.IsError&&(string)result["readReasonCode"]=="utility_mask_read_failed"&&f.Tool.Calls.Count==0,"known native cache read exception remains unavailable before UI");
        f=new Fixture();f.Manager.Bits[f.A.Cell]=0;f.Manager.Bits[f.B.Cell]=0;raw=OrdersTools.Disconnect(Request());result=JObject.Parse(raw.Content[0].Text);
        Check(!raw.IsError&&(int)result["wouldDisconnectEdges"]==0,"observed zero edges is distinct from unknown");
        Console.WriteLine("Disconnect passive native manager preflight: "+checks+" checks PASS");
    }
}
public sealed class UnknownDisconnectManager : UtilityNetworkManager<ElectricalUtilityNetwork,Wire>,IUtilityNetworkMgr
{
    public int Callbacks;
    UtilityConnections IUtilityNetworkMgr.GetConnections(int cell,bool is_physical_building){Callbacks++;return Bits.TryGetValue(cell,out var bits)?bits:0;}
}
