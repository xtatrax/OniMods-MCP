using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class SandboxTools
    {
        private static CallToolResult SetBuildingEnabledDebug(JObject args)
        {
            if (ToolUtil.GetBool(args, "dryRun", false))
                return CallToolResult.Error("dryRun is not supported for this debug mutation");
            if (!ValidateSandbox(args, out string error))
                return CallToolResult.Error(error);
            if (args["enabled"] == null)
                return CallToolResult.Error("enabled is required");
            var building = FindBuilding(args);
            var button = building?.GetComponent<BuildingEnabledButton>();
            if (button == null)
                return CallToolResult.Error("Target does not support BuildingEnabledButton");
            var state = NativeBuildingToggle.Read(button);
            if ((bool)state["toggleQueued"])
                return CallToolResult.Error("Cancel the pending native toggle before directly changing debug state");
            bool before = button.IsEnabled;
            button.IsEnabled = ToolUtil.GetBool(args, "enabled", true);
            return CallToolResult.Text(JsonConvert.SerializeObject(new
            {
                target = BuildingInfo(building), beforeEnabled = before,
                enabled = button.IsEnabled, executionPath = "debug_direct_state",
                bypassesDuplicantWork = true
            }, McpJsonUtil.Settings));
        }
    }
}
