using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
namespace OniMcp.Support {internal static class OniMcpLog{internal static void Debug(string text){}}}
namespace OniMcp.Tools
{
 internal static class NativeBuildingFootprint{internal const string NativeSource="native fixture boundary";}
 internal static class BuildingGeometryTools{internal static CallToolResult Read(JObject args){throw new InvalidOperationException("unrelated geometry route invoked");}}
 internal static class BuildingConfigTools{internal static McpTool ControlBuildingConfig(){return GatewayFixture.Unrelated();}}
 internal static class ProductionTools{internal static McpTool ControlQueue(){return GatewayFixture.Unrelated();}}
 internal static class GenericSideSurfaceTools{internal static McpTool ControlSideSurface(){return GatewayFixture.Unrelated();}}
 internal static class FacilityTools{internal static McpTool ControlFacility(){return GatewayFixture.Unrelated();}}
 internal static class RocketSystemControlTools{internal static McpTool ControlRocketSystem(){return GatewayFixture.Unrelated();}}
 internal static class StorageTools{internal static McpToolParameter StorageScopeParameter(){return new McpToolParameter{Type="string",Description="fixture unrelated parameter"};}internal static bool HasStorageScope(JObject args){return false;}internal static CallToolResult StorageScopeDetailOnly(){throw new InvalidOperationException("unrelated storage scope");}internal static McpTool ControlStorageSystem(){return GatewayFixture.Unrelated();}}
 internal static class GatewayFixture{internal static McpTool Unrelated(){return new McpTool{Parameters=new Dictionary<string,McpToolParameter>(),Handler=args=>{throw new InvalidOperationException("unrelated native action route invoked");}};}}
 public static partial class BuildPlanningTools
 {
  public static McpTool ParseBuildPlan(){return GatewayFixture.Unrelated();}public static McpTool SearchBuildables(){return GatewayFixture.Unrelated();}public static McpTool ListBuildMaterials(){return GatewayFixture.Unrelated();}public static McpTool PreviewBuild(){return GatewayFixture.Unrelated();}public static McpTool AutoConnectUtility(){return GatewayFixture.Unrelated();}public static McpTool BuildArea(){return GatewayFixture.Unrelated();}public static McpTool RoomTemplatePlan(){return GatewayFixture.Unrelated();}
  private static CallToolResult NativeUiBridgePreview(JObject args){throw new InvalidOperationException("unrelated native UI preview");}private static CallToolResult ConnectExistingUtility(JObject args){throw new InvalidOperationException("unrelated connect existing");}private static CallToolResult RepairUtilityLine(JObject args){throw new InvalidOperationException("unrelated repair");}
 }
}
