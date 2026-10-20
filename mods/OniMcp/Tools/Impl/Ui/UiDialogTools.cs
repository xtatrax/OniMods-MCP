using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    // Presentation/routing only. The native adapter owns identification and execution guards.
    internal static class UiDialogTools
    {
        internal static CallToolResult Control(JObject arguments)
        {
            var args = arguments ?? new JObject();
            string action = (args["action"]?.ToString() ?? "status").Trim().ToLowerInvariant();
            if (action != "status" && action != "acknowledge")
                return Result(Failure("unsupported_action"));
            string kind = (args["dialog"]?.ToString() ?? "mod_mismatch").Trim().ToLowerInvariant();
            if (kind != "mod_mismatch")
                return Result(Failure("unsupported_dialog"));
            if (action == "status")
                return Result(NativeModMismatchDialog.Read());

            foreach (string flag in new[] { "confirm", "dryRun", "keepPaused" })
            {
                if (args[flag] != null && args[flag].Type != JTokenType.Boolean)
                    return Result(Failure("invalid_boolean", flag));
            }
            bool dryRun = args["dryRun"]?.Value<bool>() ?? false;
            bool keepPaused = args["keepPaused"]?.Value<bool>() ?? true;
            if (!dryRun && !(args["confirm"]?.Value<bool>() ?? false))
                return Result(Failure("confirm_required"));
            string token = args["dialogToken"]?.Type == JTokenType.String
                ? args["dialogToken"].Value<string>() : null;
            if (string.IsNullOrWhiteSpace(token))
                return Result(Failure("token_required"));
            return Result(NativeModMismatchDialog.Acknowledge(token, keepPaused, dryRun));
        }

        private static JObject Failure(string reason, string parameter = null)
        {
            var result = new JObject
            {
                ["ok"] = false,
                ["reasonCode"] = reason,
                ["requested"] = false,
                ["closed"] = false
            };
            if (parameter != null) result["parameter"] = parameter;
            return result;
        }

        private static CallToolResult Result(JObject value)
        {
            AddMessage(value);
            if (value["dialogs"] is JArray dialogs)
                foreach (var item in dialogs)
                    if (item is JObject dialog) AddMessage(dialog);
            var result = CallToolResult.Text(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
            result.IsError = value["ok"]?.Type == JTokenType.Boolean && !(bool)value["ok"];
            return result;
        }

        private static void AddMessage(JObject value)
        {
            string reason = value["reasonCode"]?.Value<string>();
            if (string.IsNullOrEmpty(reason)) return;
            string key = "ui_dialog." + reason;
            if (reason == "invalid_boolean")
                value["reasonMessage"] = McpLocalization.Format(key, null, value["parameter"]?.Value<string>());
            else if (reason == "click_failed")
                value["reasonMessage"] = McpLocalization.Format(key, null, value["exceptionMessage"]?.Value<string>());
            else
                value["reasonMessage"] = McpLocalization.Get(key);
            // Native display text remains in its own field; no game locale is changed.
        }
    }
}
