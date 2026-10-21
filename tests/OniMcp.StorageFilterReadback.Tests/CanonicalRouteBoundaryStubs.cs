using System;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Support
{
    public static class OniMcpLog { public static void Debug(string message) { } }
}
namespace OniMcp.Tools
{
    // The complete canonical building_control router is linked. These unrelated domains cannot run.
    public static class BuildPlanningTools { public static McpTool ControlBuildPlanning() { throw new NotSupportedException(); } }
    public static class BuildingConfigTools { public static McpTool ControlBuildingConfig() { throw new NotSupportedException(); } }
    public static class ProductionTools { public static McpTool ControlQueue() { throw new NotSupportedException(); } }
    public static class GenericSideSurfaceTools { public static McpTool ControlSideSurface() { throw new NotSupportedException(); } }
    public static class FacilityTools { public static McpTool ControlFacility() { throw new NotSupportedException(); } }
    public static class RocketSystemControlTools { public static McpTool ControlRocketSystem() { throw new NotSupportedException(); } }
    public static class BuildingGeometryTools { public static CallToolResult Read(JObject arguments) { throw new NotSupportedException(); } }
}
