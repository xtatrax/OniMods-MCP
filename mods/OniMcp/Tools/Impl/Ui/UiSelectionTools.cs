using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    internal static class UiSelectionTools
    {
        internal static CallToolResult Control(JObject arguments)
        {
            var args = arguments ?? new JObject();
            string action = args["action"]?.Type == JTokenType.String
                ? args["action"].Value<string>().Trim().ToLowerInvariant() : string.Empty;
            if (action != "status" && action != "select" && action != "close"
                && action != "scroll_up" && action != "scroll_down")
                return Result(Failure("unsupported_action"));
            if (args["id"]?.Type != JTokenType.Integer
                || !int.TryParse(args["id"].ToString(Formatting.None), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int id) || id == 0)
                return Result(Failure("invalid_id"));
            foreach (string flag in new[] { "confirm", "dryRun" })
                if (args[flag] != null && args[flag].Type != JTokenType.Boolean)
                    return Result(Failure("invalid_boolean", flag));
            // This endpoint has one exact target and never falls back to another selector.
            foreach (string selector in new[] { "name", "query", "target", "search", "x", "y", "x1", "y1", "x2", "y2", "worldId", "allWorlds", "areaId", "force" })
                if (args[selector] != null)
                    return Result(Failure("unsupported_selector", selector));
            if (action == "status") return Result(NativeDuplicantSelectionUi.Read(id));
            if (args["task"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(args["task"].Value<string>()))
                return Result(Failure("task_required"));
            bool dryRun = args["dryRun"]?.Value<bool>() ?? false;
            if (!dryRun && !(args["confirm"]?.Value<bool>() ?? false))
                return Result(Failure("confirm_required"));
            if (action == "select") return Result(NativeDuplicantSelectionUi.Select(id, dryRun));
            if (action == "close") return Result(NativeDuplicantSelectionUi.Close(id, dryRun));
            return Result(NativeDuplicantSelectionUi.Scroll(id, action == "scroll_down", dryRun));
        }

        private static JObject Failure(string reason, string parameter = null)
        {
            var value = new JObject
            {
                ["ok"] = false, ["status"] = "unavailable", ["available"] = false,
                ["reasonCode"] = reason, ["requested"] = false, ["nativeInvoked"] = false
            };
            if (parameter != null) value["parameter"] = parameter;
            return value;
        }

        private static CallToolResult Result(JObject value)
        {
            string reason = value["reasonCode"]?.Value<string>();
            if (!string.IsNullOrEmpty(reason))
                value["reasonMessage"] = McpLocalization.Get(value["status"]?.Value<string>() == "pending"
                    ? "ui_selection.pending" : value["nativeInvoked"]?.Value<bool>() == true
                    ? "ui_selection.native_failed" : value["ok"]?.Value<bool>() == false
                    ? "ui_selection.invalid_request" : "ui_selection.unavailable");
            var result = CallToolResult.Text(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
            result.IsError = value["ok"]?.Type == JTokenType.Boolean && !(bool)value["ok"];
            return result;
        }
    }
}
