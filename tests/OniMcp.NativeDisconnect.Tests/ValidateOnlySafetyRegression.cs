using Newtonsoft.Json.Linq;
using OniMcp.Tools;
internal static class ValidateOnlySafetyRegression
{
    private static int checks;
    private static void Check(bool ok,string label){checks++;if(!ok)throw new Exception("validate_only_passive: "+label);}
    internal static void RunCase(bool confirm)
    {
        var f=new ConnectFixture();
        f.Tool.OnRelease=()=>{f.Manager.Bits[f.A.Cell]=f.BeforeA|f.AddA;f.Manager.Bits[f.B.Cell]=f.BeforeB|f.AddB;};
        var args=new JObject{["x"]=2,["y"]=2,["direction"]="right",["type"]="liquid",["validateOnly"]=true,["confirm"]=confirm};
        Check((f.BeforeA&f.AddA)==0&&(f.BeforeB&f.AddB)==0,"fixture starts disconnected");
        var result=BuildPlanningTools.ConnectForTest(args);
        Check(f.Tool.Events.Count==0,"alias confirm="+confirm+" nativeActions must be zero; observed "+f.Tool.Events.Count);
        Check(f.Manager.Bits[f.A.Cell]==f.BeforeA&&f.Manager.Bits[f.B.Cell]==f.BeforeB,"validateOnly never changes either mask");
        Check(!result.IsError,"advertised alias accepts read-only preflight without write confirmation: "+result.Content[0].Text);
        var data=JObject.Parse(result.Content[0].Text);
        Check((bool)data["dryRun"]&&!(bool)data["actionAttempted"]&&data["connectionVerified"].Type==JTokenType.Null,"alias returns truthful preview, not verified connection");
    }
    internal static void Run()
    {
        RunCase(false);RunCase(true);
        Console.WriteLine("Connect-existing validateOnly alias: "+checks+" checks PASS");
    }
}
