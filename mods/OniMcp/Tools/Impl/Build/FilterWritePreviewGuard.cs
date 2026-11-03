using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    // These native write paths have no preview implementation. Refuse preview
    // requests before target lookup instead of running a write as a dry run.
    internal static class FilterWritePreviewGuard
    {
        internal static bool TryReject(JObject args, out CallToolResult rejection)
        {
            rejection = null;
            JToken token = args?["dryRun"];
            if (token == null) return false;
            string reason = token.Type != JTokenType.Boolean ? "invalid_boolean"
                : token.Value<bool>() ? "unsupported_preview" : null;
            if (reason == null) return false;

            rejection = CallToolResult.Error(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["reasonCode"] = reason,
                ["parameter"] = "dryRun",
                ["previewSupported"] = false,
                ["nativeInvoked"] = false,
                ["changed"] = false,
                ["message"] = McpLocalization.Get("filter_preview.error." + reason)
            }, McpJsonUtil.Settings));
            return true;
        }

        internal static McpToolParameter Parameter()
        {
            return new McpToolParameter
            {
                Type = "boolean",
                Description = McpLocalization.Get("filter_preview.parameter.dry_run"),
                Required = false
            };
        }
    }
}
