using System;using Newtonsoft.Json.Linq;using OniMcp.Core;using OniMcp.Tools;
internal static class SandboxCallerScopeRegression {
 static int checks,calls;static void Check(bool value,string message){checks++;if(!value)throw new Exception("sandbox caller mode: "+message);}
 static JObject Args(string kind,string action)=>new JObject{{"command","sandbox"},{"kind",kind},{"action",action},{"allowSandbox",true},{"allowTerrainMutation",true},{"confirm",true},{"task","host mode contract"}};
 static void Setup(bool native){DebugHandler.Seed(native);calls=0;MapNativeBoundary.GameHandler=args=>{calls++;return CallToolResult.Text("{\"acknowledged\":true}");};}
 internal static void Run(){try{
  Setup(true);MapNativeBoundary.GameHandler=args=>{calls++;Check(DebugHandler.InstantBuildMode&&WorldEditorTools.ActiveConstructionRequest==null,"actual ForwardSandbox omitted-default map preview reaches child without mode scope");return CallToolResult.Text("{\"dryRun\":true}");};
  var result=WorldEditorTools.ControlWorldEditor().Handler(Args("map_designate","replace"));Check(!result.IsError&&calls==1&&DebugHandler.Writes.Count==0&&DebugHandler.InstantBuildMode,"actual sandbox command preserves map preview native mode");
  Setup(true);var info=Args("info","list_actions");info["allowSandbox"]=false;info["confirm"]=false;
  result=WorldEditorTools.ControlWorldEditor().Handler(info);Check(!result.IsError&&calls==1&&DebugHandler.Writes.Count==0&&DebugHandler.InstantBuildMode,"actual info read alias requires no sandbox write consent or native mode changes");
  Setup(true);var bad=Args("area","paint");bad["dryRun"]=true;result=WorldEditorTools.ControlWorldEditor().Handler(bad);
  Check(result.IsError&&calls==0&&DebugHandler.Writes.Count==0&&DebugHandler.InstantBuildMode,"actual unsupported sandbox preview stops before game child and flags");
  Check((string)((JObject)result.Content[0].Meta[PreviewValidationObservation.MetadataKey])["coverage"]=="not_evaluated","actual sandbox preview refusal is not native validation");
  foreach(bool instant in new[]{false,true}){
   Setup(!instant);MapNativeBoundary.GameHandler=args=>{calls++;Check(DebugHandler.InstantBuildMode==instant&&WorldEditorTools.EnforceNormalMaterialRules==!instant,"actual ForwardSandbox write establishes requested authorized mode");return CallToolResult.Text("{\"ok\":true}");};
   var write=Args("area","paint");write["dryRun"]=false;write["instantBuild"]=instant;result=WorldEditorTools.ControlWorldEditor().Handler(write);
   Check(!result.IsError&&calls==1&&DebugHandler.Writes.Count==2&&DebugHandler.InstantBuildMode==!instant&&WorldEditorTools.ActiveConstructionRequest==null,"actual ForwardSandbox writer restores exact native/material context");
  }
  Console.WriteLine("PASS "+checks+" actual sandbox command caller mode checks (native terminal action supplied)");
 }finally{MapNativeBoundary.GameHandler=null;DebugHandler.Seed(false);}}
}
