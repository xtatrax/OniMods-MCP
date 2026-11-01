using OniMcp.Core;
namespace OniMcp.Tools
{
    // Opaque compile boundary only: actual typed sharing/ownership is tested by
    // the real map/area/dispatcher hosts. The candidate route never calls BuildArea.
    internal sealed class BuildPlanningRequestSupportContext {}
    public static partial class BuildPlanningTools
    {
        internal static McpTool BuildArea(BuildPlanningRequestSupportContext support)
            =>GatewayFixture.Unrelated();
    }
}
