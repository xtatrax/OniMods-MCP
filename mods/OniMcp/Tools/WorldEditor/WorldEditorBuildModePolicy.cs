using System;
using Newtonsoft.Json.Linq;
namespace OniMcp.Tools
{
    // This selects a construction-mode scope; false does not certify that a
    // call is passive (camera and explicitly confirmed UI operations also skip it).
    internal static class WorldEditorBuildModePolicy
    {
        private static string Text(JObject args, string key) => (args?[key]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
        private static bool OneOf(string value, params string[] choices) => Array.IndexOf(choices, value) >= 0;
        private static bool Preview(JObject args) => ToolUtil.GetBool(args, "dryRun", false) || ToolUtil.GetBool(args, "validateOnly", false);

        internal static JObject SandboxPreviewArguments(string toolName, JObject args)
        {
            string tool = toolName?.Trim().ToLowerInvariant();
            if (tool == "coordinate_control")
            {
                string target; JObject forwarded;
                return CoordinateControlTools.TryDescribeForwarding(args, out target, out forwarded)
                    ? SandboxPreviewArguments(target, forwarded) : null;
            }
            if (tool != "game_control" || !OneOf(Text(args, "domain"), "sandbox", "sandbox_tools", "debug")) return null;
            string kind = Text(args, "kind");
            bool map = OneOf(kind, "map_designate", "designate", "search_designate", "map");
            return ToolUtil.GetBool(args, "dryRun", map) ? args : null;
        }

        internal static bool IsKnownSandboxPreview(JObject args)
        {
            string kind = Text(args, "kind"), action = Text(args, "action");
            return OneOf(kind, "map_designate", "designate", "search_designate", "map")
                || kind == "area" && action == "flood_fill"
                || OneOf(kind, "", "read", "info") && OneOf(action, "", "list", "status", "list_actions", "sample_cell", "list_story_traits");
        }

        internal static bool IsUnsupportedConstructionPreview(string toolName, JObject args)
        {
            string tool = toolName?.Trim().ToLowerInvariant();
            if (tool == "coordinate_control")
            {
                string target; JObject forwarded;
                return CoordinateControlTools.TryDescribeForwarding(args, out target, out forwarded)
                    && IsUnsupportedConstructionPreview(target, forwarded);
            }
            return tool == "building_control" && OneOf(Text(args, "domain"), "", "planning", "plan", "build", "placement")
                && Preview(args) && RequiresScope(tool, args);
        }

        internal static JObject ConstructionPreflightArguments(string toolName, JObject args)
        {
            if (toolName == "coordinate_control")
            {
                string target; JObject forwarded;
                return CoordinateControlTools.TryDescribeForwarding(args, out target, out forwarded)
                    ? ConstructionPreflightArguments(target, forwarded) : null;
            }
            return IsConstructionPreflight(toolName, args) ? args : null;
        }

        internal static bool IsConstructionPreflight(string toolName, JObject args)
        {
            if (toolName == "coordinate_control")
            {
                string target; JObject forwarded;
                return CoordinateControlTools.TryDescribeForwarding(args, out target, out forwarded)
                    && IsConstructionPreflight(target, forwarded);
            }
            if (toolName != "building_control") return false;
            string domain = Text(args, "domain"), action = Text(args, "action");
            if (!OneOf(domain, "", "planning", "plan", "build", "placement")) return false;
            if (OneOf(action, "preview", "validate", "placement_candidates", "candidates", "anchors")) return true;
            if (OneOf(action, "room_template", "room_plan", "quick_room"))
                return ToolUtil.GetBool(args, "execute", false) && Preview(args);
            return Preview(args) && OneOf(action, "build_area", "area", "batch_build", "auto_connect", "utility_auto_connect", "connect",
                "repair_line", "connect_line", "fix_line", "repair_wire", "connect_wire", "接线", "修线", "connect_existing");
        }

        internal static bool RequiresScope(string toolName, JObject args)
        {
            // A null name is used only by already-authorized actual map/build/blueprint writers.
            if (toolName == null) return true;
            string tool = toolName.Trim().ToLowerInvariant();
            if (tool == "coordinate_control")
            {
                string target;
                JObject forwarded;
                if (!CoordinateControlTools.TryDescribeForwarding(args, out target, out forwarded)) return false;
                return RequiresScope(target, forwarded);
            }
            if (OneOf(tool, "read_control", "search_control", "navigation_control", "world_editor")) return false;
            string domain = Text(args, "domain"), action = Text(args, "action");
            if (tool == "colony_control" && OneOf(domain, "snapshot", "state", "overview", "read", "basic", "report", "reports",
                "diagnostic", "diagnostics", "notification", "notifications", "management", "manage", "screen", "screens", "bio", "life", "biology")) return false;
            if (tool == "server_control" && (domain == "catalog" && OneOf(action, "manifest", "search", "guide", "list", "status")
                || domain == "diagnostics" && OneOf(action, "status", "health", "logs", "doctor"))) return false;
            if (tool == "game_control")
            {
                if (!OneOf(domain, "sandbox", "sandbox_tools", "debug")) return false;
                string kind = Text(args, "kind");
                if (kind.Length == 0) kind = "read";
                if (SandboxPreviewArguments(tool, args) != null) return false;
                return !OneOf(kind, "read", "info");
            }
            if (tool == "building_control")
            {
                bool planning = OneOf(domain, "planning", "plan", "build", "placement") || domain.Length == 0;
                if (domain == "geometry" && OneOf(action, "", "read", "inspect", "definition")) return false;
                if (!planning) return false;
                if (OneOf(action, "parse_plan", "parse_sequence", "parse", "plan_text", "search_defs", "search", "defs",
                    "materials", "list_materials", "preview", "validate", "placement_candidates", "candidates", "anchors")) return false;
                if (action == "native_ui_preview") return false;
                if (OneOf(action, "room_template", "room_plan", "quick_room"))
                    return ToolUtil.GetBool(args, "execute", false) && !Preview(args);
                if (OneOf(action, "build_area", "area", "batch_build", "auto_connect", "utility_auto_connect", "connect",
                    "repair_line", "connect_line", "fix_line", "repair_wire", "connect_wire", "接线", "修线", "connect_existing"))
                    return !Preview(args);
                // Unknown planning actions retain conservative enforcement; the
                // original router still validates/denies their capability.
                return true;
            }
            if (tool == "orders_control" && ToolUtil.GetBool(args, "dryRun", false)) return false;
            return true;
        }
    }
}
