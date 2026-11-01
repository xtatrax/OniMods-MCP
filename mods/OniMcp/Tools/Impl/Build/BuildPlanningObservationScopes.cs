using System;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static Func<JObject, CallToolResult> WithValidationRequest(Func<JObject, CallToolResult> handler,
            string requestedScope, bool passive)
        {
            return args =>
            {
                CallToolResult result = handler(args);
                bool mutationRequested = !passive && args != null && !IsDryRun(args);
                if (result?.IsError == true)
                    return PreviewValidationObservation.Rejected(result, requestedScope, mutationRequested);
                return new PreviewValidationObservation(requestedScope, "requested_scope_only", "not_evaluated",
                    mutationRequested, false).Attach(result);
            };
        }
    }
}
