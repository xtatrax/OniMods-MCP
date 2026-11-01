using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
namespace OniMcp.Tools {
 public class McpToolParameter{public string Type,Description;public bool Required;}
 public class McpTool{public string Name,Group,Mode,Risk,Description;public bool Hidden;public Dictionary<string,McpToolParameter> Parameters;public Func<JObject,CallToolResult> Handler;}
 public static class ToolUtil{
  public static int? GetInt(JObject args,string key)=>(int?)args[key];
  public static bool GetBool(JObject args,string key,bool fallback)=>(bool?)args[key]??fallback;
  public static int ResolveWorldId(JObject args)=>(int?)args["worldId"]??0;
  public static bool TryResolveSearchCell(JObject args,out int x,out int y,out string error){x=y=0;error=null;return false;}
 }
 public static class WorldEditor{public static Dictionary<string,int> ResolveRect(JObject args)=>new Dictionary<string,int>{{"x1",0},{"y1",0},{"x2",7},{"y2",3}};}
 public static class OniToolRegistry{
  public static readonly Queue<Func<CallToolResult>> Results=new Queue<Func<CallToolResult>>();
  public static readonly List<JObject> Arguments=new List<JObject>();
  public static CallToolResult CallTool(string tool,JObject args){Arguments.Add((JObject)args.DeepClone());return Results.Count>0?Results.Dequeue()():CallToolResult.Text("{}");}
  public static void Reset(){Results.Clear();Arguments.Clear();}
 }
 public static partial class BuildPlanningTools{
  internal static CallToolResult HostValidationFallback(Func<JObject,CallToolResult> handler,JObject args)=>WithValidationRequest(handler,"fixture_requested",true)(args);
  private static RoomTemplateAnchor TryAutoRoomTemplateAnchor(JObject args,string kind,int world,out string error){error=null;return null;}
  private static JObject BuildRoomTemplateExecutionPlan(string kind,RoomTemplateAnchor anchor,int priority)=>new JObject();
  private static JObject BuildRoomTemplateVerificationPlan(string kind,RoomTemplateAnchor anchor,int priority)=>new JObject();
  private static JObject BuildRoomTemplateNextActions(string kind,RoomTemplateAnchor anchor,int priority)=>new JObject();
  private static JObject DiagnoseRoomTemplateResult(string text,bool error)=>new JObject{{"error",error}};
 }
}
