using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
namespace OniMcp.Tools {
 internal static class BlueprintModeBoundary { internal static int Uses;internal static bool FailUse;internal static Action Observer; }
 public static partial class WorldEditorTools {
  private struct BlueprintRect { internal int Width,Height; }
  private static string FirstZoomText(JObject args,params string[] keys){foreach(var key in keys){string value=args[key]?.ToString();if(!string.IsNullOrEmpty(value))return value;}return string.Empty;}
  private static IEnumerable<string> BlueprintFiles(){yield break;}
  private static string ResolveBlueprintPath(string path)=>null;
  private static string ReadBlueprintIndexMarkdown()=>"fixture blueprint list";
  private static string ReadBlueprintText(string path)=>"{}";
  private static string ReadBlueprintMarkdown(string path)=>"fixture blueprint markdown";
  private static bool AtomicWriteBlueprint(string path,string text,out string error){error=null;throw new NotSupportedException("file writes are outside this host");}
  private static JObject ReadBlueprintJson(string path)=>new JObject{{"buildings",new JArray(new JObject())}};
  private static bool ValidateBlueprintMarkdownRoundTrip(JObject original,string current,out string error){error=null;return false;}
  private static JObject MarkdownToBlueprint(JObject original,string text,out string error){error=null;throw new NotSupportedException();}
  private static string FindBlueprintPath(string name)=>name=="fixture"?"fixture.blueprint":null;
  private static bool TryGetBlueprintPath(string name,bool exists,out string path,out string error){path=null;error=null;return false;}
  private static JObject EmptyBlueprint(string name)=>new JObject();
  private static bool ValidateBlueprintIoPath(string path,out string error){error=null;return path=="fixture.blueprint";}
  private static BlueprintRect BlueprintBounds(JObject root)=>new BlueprintRect{Width=1,Height=1};
  private static bool InvokeBlueprintsExpandedUse(string path,int x,int y,out string error){BlueprintModeBoundary.Uses++;BlueprintModeBoundary.Observer?.Invoke();error=BlueprintModeBoundary.FailUse?"fixture rejected native use":null;return error==null;}
  internal static CallToolResult HostBlueprintMode(JObject args)=>UseBlueprint(args);
 }
}
