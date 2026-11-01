#if OLD_AREA
using OniMcp.Core;
namespace OniMcp.Tools
{
    // Compile-only adapter for the exact old production file. It deliberately
    // does not implement sharing, so the new behavioral assertion must fail.
    public static partial class BuildPlanningTools
    {
        internal static McpTool BuildArea(BuildPlanningRequestSupportContext ignored) => BuildArea();
    }
}
#endif
