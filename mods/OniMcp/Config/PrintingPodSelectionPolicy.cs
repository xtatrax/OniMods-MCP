using System.Collections.Generic;
using OniMcp.Support;

namespace OniMcp.Config
{
    /// <summary>Agent guidance only: never pauses, materializes choices, or claims rewards.</summary>
    internal static class PrintingPodSelectionPolicy
    {
        internal static Dictionary<string, object> Describe(bool required, bool? available)
        {
            if (!required)
                return null; // Disabled means no agent guidance, including no reminders.
            bool pending = available == true;
            return new Dictionary<string, object>
            {
                ["mode"] = "always_resolve",
                ["available"] = available,
                ["selectionRequired"] = pending,
                ["allowedDecisions"] = new[] { "claim", "reject_all" },
                ["decisionRequired"] = pending,
                ["enforcement"] = "agent_instruction",
                ["instruction"] = !pending ? null : McpLocalization.Get(
                    "policy.printingPod.required",
                    "Before routine work, accept one care package or reject all Printing Pod offers with action=reject_all confirm=true. Do not defer or automatically select duplicants. If the operation fails, report it without claiming resolution."),
                ["statusAction"] = new Dictionary<string, object>
                {
                    ["tool"] = "building_control",
                    ["arguments"] = new Dictionary<string, object>
                    {
                        ["domain"] = "side_surface", ["surface"] = "facility",
                        ["kind"] = "printing_pod", ["action"] = "list_rewards",
                        ["task"] = "Inspect Printing Pod supply choices"
                    }
                }
            };
        }
    }
}
