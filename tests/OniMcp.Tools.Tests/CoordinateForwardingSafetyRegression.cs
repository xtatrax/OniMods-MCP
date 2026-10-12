using System;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class CoordinateForwardingSafetyRegression
{
    internal static void Run()
    {
        Check("{dryRun:false,confirm:true}", "{dryRun:true}", false);
        Check("{dryRun:false,confirm:true}", "{confirm:false}", false);
        Check("{dryRun:false,confirm:true}", "{dryRun:false,confirm:true}", true);
        Check("{dryRun:true}", "{dryRun:false}", true);
        Check("{confirm:false}", "{confirm:true}", true);
        Check("{}", "{dryRun:true}", true);
        Check("{dryRun:false}", "{}", true);
        Check("{dryRun:'false'}", "{dryRun:'true'}", false);
        Console.WriteLine("PASS: coordinate forwarding cannot override preview or withheld confirmation");
    }
    private static void Check(string wrapperJson, string payloadJson, bool expected)
    {
        var wrapper=JObject.Parse(wrapperJson); var payload=JObject.Parse(payloadJson);
        string before=payload.ToString(); string error;
        bool actual=CoordinateForwardingSafety.TryValidate(wrapper,payload,out error);
        if(actual!=expected || (actual && error!=null) || (!actual && string.IsNullOrEmpty(error)) || payload.ToString()!=before)
            throw new InvalidOperationException("Coordinate safety mismatch: "+wrapperJson+" / "+payloadJson);
    }
}
