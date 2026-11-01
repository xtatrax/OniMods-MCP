using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
public static class DebugHandler {
 private static bool instant;
 internal static readonly List<bool> Writes=new List<bool>();
 public static bool InstantBuildMode{get{return instant;}set{Writes.Add(value);instant=value;}}
 internal static void Seed(bool value){instant=value;Writes.Clear();}
}
namespace OniMcp.Tools {
 public static partial class GameControlTools {public static McpTool ControlGame(){return GameControlEntryTools.ControlGame();}}
}
