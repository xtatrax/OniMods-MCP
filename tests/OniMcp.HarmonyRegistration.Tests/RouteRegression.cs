using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Server;
using OniMcp.Tools;

internal static class RouteRegression
{
    private static McpTool Tool(int route)
    {
        return route == 0 ? ServerTools.GetMcpStatus() : route == 1 ? ServerTools.DiagnosticsControl() : ServerTools.ControlServer();
    }
    private static JObject Payload(CallToolResult result)
    {
        Program.Check(!result.IsError && result.Content.Count == 1 && result.Content[0].Type == "text", "actual status route returns one successful native JSON result");
        return JObject.Parse(result.Content[0].Text);
    }
    internal static void Run()
    {
        McpHttpServer.Instance = new McpHttpServer();
        for (int route = 0; route < 3; route++)
        {
            var tool = Tool(route);
            Program.Check(tool.Parameters.ContainsKey("includePatches") && tool.Parameters["includePatches"].Type == "boolean" && !tool.Parameters["includePatches"].Required, "includePatches is optional boolean in each actual schema");
            var args = new JObject { ["domain"] = "diagnostics", ["action"] = "status" };
            Harmony.Reset(); Harmony.AllOverride = () => { throw new InvalidOperationException("default status must not scan native metadata"); };
            var baseline = Payload(tool.Handler(args));
            Program.Check(Harmony.Scans == 0 && baseline["patchRegistration"] == null, "default status omits audit and performs zero native scans");
            args["includePatches"] = false;
            var explicitFalse = Payload(tool.Handler(args));
            Program.Check(Harmony.Scans == 0 && JToken.DeepEquals(baseline, explicitFalse), "explicit false preserves exact compact status and zero scans");
            Program.Check((bool)baseline["loaded"] && (int)baseline["port"] == 1234 && (int)baseline["listedToolCount"] == 7 && (string)baseline["endpoint"] == "fixture-endpoint", "existing server status fields remain intact");
            foreach (JToken invalid in new JToken[] { JValue.CreateNull(), new JValue("true"), new JValue("false"), new JValue(1), new JValue(0), new JArray(), new JObject() })
            {
                args["includePatches"] = invalid;
                var rejected = tool.Handler(args);
                Program.Check(rejected.IsError && Harmony.Scans == 0, "non-boolean includePatches rejects before any metadata scan");
            }
            Harmony.Reset(); AuditRegression.Add(AuditRegression.OriginalMethod, "postfix", AuditRegression.HookMethod, "fixture-owner");
            args["includePatches"] = true;
            var withPatches = Payload(tool.Handler(args));
            Program.Check(Harmony.Scans == 1 && (bool)withPatches["patchRegistration"]["available"] && (int)withPatches["patchRegistration"]["totalEntries"] == 1, "true status traverses the actual route into the real metadata reader once");
            withPatches.Remove("patchRegistration");
            Program.Check(JToken.DeepEquals(baseline, withPatches) && Harmony.PatchCalls == 0 && Harmony.FactoryReads == 0, "opt-in adds only audit data and performs no repair/factory action");
            Harmony.Reset(); Harmony.AllOverride = () => null;
            var unknown = Payload(tool.Handler(args));
            var audit = (JObject)unknown["patchRegistration"];
            Program.Check(!(bool)audit["available"] && audit["totalEntries"].Type == JTokenType.Null && audit["rows"].Type == JTokenType.Null && (bool)unknown["loaded"], "unknown audit remains null inside an otherwise available server status");
        }
        foreach (string domain in new[] { "diagnostics", "diagnostic", "server" })
        {
            Harmony.Reset();
            var data = Payload(ServerTools.ControlServer().Handler(new JObject { ["domain"] = domain, ["action"] = "STATUS", ["includePatches"] = true }));
            Program.Check(Harmony.Scans == 1 && (bool)data["patchRegistration"]["available"], "existing diagnostics domain aliases reach the actual audit status");
        }
        Harmony.Reset();
        var capabilities = Payload(ServerTools.ControlServer().Handler(new JObject { ["domain"] = "diagnostics", ["action"] = "capabilities", ["includePatches"] = true }));
        Program.Check(Harmony.Scans == 0 && capabilities["patchRegistration"] == null && capabilities["sessions"] != null, "non-status diagnostics retain their original behavior without scanning");
        Harmony.Reset(); AuditRegression.Add(AuditRegression.OriginalMethod, "postfix", AuditRegression.HookMethod, "fixture-owner"); AuditRegression.Add(AuditRegression.OriginalMethod, "postfix", AuditRegression.HookMethod, "fixture-owner");
        var duplicate = Payload(ServerTools.ControlServer().Handler(new JObject { ["domain"] = "diagnostics", ["action"] = "status", ["includePatches"] = true, ["limit"] = 1 }));
        Program.Check((int)duplicate["patchRegistration"]["duplicateGroups"] == 1 && (int)duplicate["patchRegistration"]["limit"] == 128 && Harmony.Table[AuditRegression.OriginalMethod].Postfixes.Count == 2 && Harmony.PatchCalls == 0, "read-only diagnostics report existing duplicate metadata with fixed limit rather than repairing it");
    }
}
