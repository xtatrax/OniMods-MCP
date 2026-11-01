using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
namespace OniMcp.Tools {
 public class McpToolParameter {public string Type,Description;public bool Required;public List<string> EnumValues;}
 public class McpTool {public string Name,Group,Mode,Risk,Description;public List<string> Aliases,Tags;public Dictionary<string,McpToolParameter> Parameters;public Func<JObject,CallToolResult> Handler;}
 internal static partial class ToolUtil {internal static int? GetInt(JObject args,string key){return (int?)args?[key];}}
 internal static class OniToolRegistry {
  internal static readonly Dictionary<string,McpTool> Tools=new Dictionary<string,McpTool>();
  internal static bool IsCoordinateTool(string name){return name=="coordinate_control"||name=="coordinate_gateway"||name=="coordinate_tool";}
  internal static bool TryGetOperation(string name,out McpTool tool){return Tools.TryGetValue(name,out tool);}
 }
 public static class GameControlTools {public static McpTool ControlGame(){return new McpTool{Handler=args=>CallToolResult.Text("{}")};}}
 public static partial class WorldEditorTools {
  internal static Func<JObject,CallToolResult> CommandBoundary;
  private static CallToolResult HandleWorldEditorCommand(JObject args){return CommandBoundary==null?CallToolResult.Text("{}"):CommandBoundary(args);}
  private static JObject CopyPayload(JObject args){return args==null?new JObject():(JObject)args.DeepClone();}
  #if OLD_SCOPE_COMPARISON
  internal static ConstructionModeRequest ActiveConstructionRequest {get{return null;}}
  private static CallToolResult RunWithWorldEditorInstantBuildScope(JObject args,Func<CallToolResult> action,string tool){return RunWithWorldEditorInstantBuildScope(args,action);}
#endif
  internal static CallToolResult HostOuter(JObject args){return HandleWorldEditorScoped(args);}
  internal static CallToolResult HostScope(JObject args,Func<CallToolResult> action,string tool=null){return RunWithWorldEditorInstantBuildScope(args,action,tool);}
 }
}
namespace UnityEngine {
 public struct Vector3 {public Vector3(float x,float y,float z){}}
 public class Camera {public static Camera main;public float orthographicSize;}
}
public class CameraController {
 public static CameraController Instance;
 public void CameraGoTo(UnityEngine.Vector3 value,float seconds,bool option){Boundary.UiCalls++;}
 public void SnapTo(UnityEngine.Vector3 value,float zoom){Boundary.UiCalls++;}
}
namespace OniMcp.Server {public static class MainThreadBridge {public static void Enqueue(System.Action action){Boundary.UiCalls++;action();}}}
