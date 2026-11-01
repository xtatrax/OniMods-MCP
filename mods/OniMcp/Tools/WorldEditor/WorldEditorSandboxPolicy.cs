using System;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static ConstructionModeRequest activeConstructionRequest;
        internal static ConstructionModeRequest ActiveConstructionRequest => activeConstructionRequest;
        internal static bool EnforceNormalMaterialRules => activeConstructionRequest == null || !activeConstructionRequest.InstantBuild;
        private static readonly string[] WorldEditorSandboxFlags =
        {
            "allowSandbox", "instantBuild", "allowForce", "allowTerrainMutation",
            "allowEntitySpawn", "allowDestroy"
        };

        private static CallToolResult HandleWorldEditorScoped(JObject rawArgs)
        {
            JObject args = rawArgs ?? new JObject();
            if (!ValidateInstantBuildConsent(args, out string error)) return CallToolResult.Error(error);
            // A command may only read or compile a preview. Its eventual write
            // adapter owns any temporary construction-mode scope.
            return HandleWorldEditorCommand(args);
        }

        private static bool ValidateInstantBuildConsent(JObject args, out string error)
        {
            error = null;
            if (ToolUtil.GetBool(args, "instantBuild", false) && (!ToolUtil.GetBool(args, "allowSandbox", false)
                || !ToolUtil.GetBool(args, "confirm", false)))
                error = McpLocalization.Get("construction_mode.instant_consent_required");
            return error == null;
        }

        private static CallToolResult RunWithWorldEditorInstantBuildScope(JObject rawArgs, Func<CallToolResult> action,
            string toolName = null)
        {
            JObject args = rawArgs ?? new JObject();
            var sandboxPreview = WorldEditorBuildModePolicy.SandboxPreviewArguments(toolName, args);
            if (sandboxPreview != null && !WorldEditorBuildModePolicy.IsKnownSandboxPreview(sandboxPreview))
            {
                string existingError;
                SandboxDryRunRoutingPolicy.TryRejectUnsupported(sandboxPreview["kind"]?.ToString(),
                    sandboxPreview["action"]?.ToString(), true, out existingError);
                return new PreviewValidationObservation("sandbox_preview", "request_rejected", "not_evaluated", false, false)
                    .Attach(CallToolResult.Error(existingError ?? McpLocalization.Get("construction_mode.unsupported_sandbox_preview")));
            }
            if (WorldEditorBuildModePolicy.IsUnsupportedConstructionPreview(toolName, args))
            {
                var body = new JObject
                {
                    ["ok"] = false,
                    ["reasonCode"] = "unsupported_preview_action",
                    ["nativePlacementValid"] = JValue.CreateNull(),
                    ["availableNow"] = JValue.CreateNull(),
                    ["error"] = McpLocalization.Get("construction_mode.unsupported_preview_action")
                };
                return new PreviewValidationObservation("construction_preview", "request_rejected", "not_evaluated", false, false,
                    "reasonCode", "nativePlacementValid", "availableNow")
                    .Attach(CallToolResult.Error(body.ToString(Newtonsoft.Json.Formatting.None)));
            }
            if (!WorldEditorBuildModePolicy.RequiresScope(toolName, args))
            {
                // Requested future mode is not simulated by changing native flags.
                // Internal preflight inside an authorized active write scope may
                // observe the mode that the actual write has already established.
                var preflightArgs = WorldEditorBuildModePolicy.ConstructionPreflightArguments(toolName, args);
                if (preflightArgs != null)
                {
                    var mode = ConstructionModeObservation.Read(preflightArgs);
                    if (!mode.MatchesRequestedPolicy) return UnknownConstructionModePreflight(mode);
                }
                return action();
            }
            if (!ValidateInstantBuildConsent(args, out string error)) return CallToolResult.Error(error);
            bool previous = DebugHandler.InstantBuildMode;
            var previousRequest = activeConstructionRequest;
            var request = ConstructionModeRequest.Resolve(args, previousRequest);
            bool instantBuild = request.InstantBuild;
            try
            {
                if (DebugHandler.InstantBuildMode != instantBuild) DebugHandler.InstantBuildMode = instantBuild;
                activeConstructionRequest = request;
                return action();
            }
            finally
            {
                if (DebugHandler.InstantBuildMode != previous) DebugHandler.InstantBuildMode = previous;
                activeConstructionRequest = previousRequest;
            }
        }

        private static CallToolResult UnknownConstructionModePreflight(ConstructionModeObservation mode)
        {
            var body = JObject.FromObject(mode.Details());
            body["ok"] = false;
            body["valid"] = JValue.CreateNull();
            body["error"] = mode.ErrorText;
            return CallToolResult.Error(body.ToString(Newtonsoft.Json.Formatting.None));
        }

        private static JObject InheritWorldEditorSandboxPolicy(JObject parent, JObject child)
        {
            JObject result = child ?? new JObject();
            foreach (string key in WorldEditorSandboxFlags)
            {
                bool parentAllowed = ToolUtil.GetBool(parent, key, false);
                bool childAllowed = result[key] == null ? parentAllowed : ToolUtil.GetBool(result, key, false);
                result[key] = parentAllowed && childAllowed;
            }
            int parentMaxCells = SandboxCellLimit(parent);
            int childMaxCells = result["sandboxMaxCells"] == null
                ? parentMaxCells
                : SandboxCellLimit(result);
            result["sandboxMaxCells"] = Math.Min(parentMaxCells, childMaxCells);
            return result;
        }

        private static int SandboxCellLimit(JObject args)
        {
            return Math.Max(1, Math.Min(1000, ToolUtil.GetInt(args, "sandboxMaxCells") ?? 100));
        }

        private static CallToolResult ForwardSandbox(JObject args)
        {
            var forwarded = CopyPayload(args);
            forwarded["domain"] = "sandbox";
            foreach (string key in WorldEditorSandboxFlags)
                forwarded[key] = ToolUtil.GetBool(args, key, false);
            forwarded["confirm"] = ToolUtil.GetBool(args, "confirm", false);
            forwarded["sandboxMaxCells"] = SandboxCellLimit(args);
            if (!ValidateWorldEditorSandboxPolicy(forwarded, out string error))
                return CallToolResult.Error(error);
            return RunWithWorldEditorInstantBuildScope(forwarded,
                () => GameControlTools.ControlGame().Handler(forwarded), "game_control");
        }

        private static bool ValidateWorldEditorSandboxPolicy(JObject args, out string error)
        {
            error = null;
            string kind = (args?["kind"]?.ToString() ?? "read").Trim().ToLowerInvariant();
            string action = (args?["action"]?.ToString() ?? "list_actions").Trim().ToLowerInvariant();
            if (action == "list" || action == "status")
                action = "list_actions";
            args["kind"] = kind;
            args["action"] = action;
            int policyMax = SandboxCellLimit(args);
            int requestedMax = Math.Max(1, ToolUtil.GetInt(args, "maxCells") ?? policyMax);
            args["maxCells"] = Math.Min(policyMax, requestedMax);

            bool read = (kind == "read" || kind == "info") && (action == "list_actions"
                || action == "sample_cell" || action == "list_story_traits");
            if (ToolUtil.GetBool(args, "force", false) && !ToolUtil.GetBool(args, "allowForce", false))
            {
                error = "Sandbox force=true requires allowForce=true";
                return false;
            }
            if (read)
                return true;
            if (!ToolUtil.GetBool(args, "allowSandbox", false)
                || !ToolUtil.GetBool(args, "confirm", false))
            {
                error = "Sandbox writes require allowSandbox=true and confirm=true";
                return false;
            }
            if ((kind == "area" || kind == "map_designate")
                && !ToolUtil.GetBool(args, "allowTerrainMutation", false))
            {
                error = "Sandbox area/map_designate writes require allowTerrainMutation=true";
                return false;
            }
            if (kind == "entity" && !ToolUtil.GetBool(args, "allowEntitySpawn", false))
            {
                error = "Sandbox entity writes require allowEntitySpawn=true";
                return false;
            }
            if (action == "destroy" && !ToolUtil.GetBool(args, "allowDestroy", false))
            {
                error = "Sandbox destroy requires allowDestroy=true";
                return false;
            }
            return true;
        }
    }
}
