using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static class SurfaceAuditControlTools
    {
        public static McpTool ControlSurfaceAudit()
        {
            return new McpTool
            {
                Name = "surface_audit_control",
                Group = "tools",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "ui_surface_audit", "surface_coverage_audit" },
                Tags = new List<string> { "coverage", "audit", "surfaces", "ui", "tools", "resources" },
                Description = "ONI のプレイヤー操作 surface の対応状況をまとめて監査する。action=side_screen/user_menu/management/tool_menu/ui_menu/global_control/notification",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "監査タイプ: side_screen/user_menu/management/tool_menu/ui_menu/global_control/notification", Required = true, EnumValues = new List<string> { "side_screen", "user_menu", "management", "tool_menu", "ui_menu", "global_control", "notification" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "class、surface、ツール名、リソース名、備考で絞り込み", Required = false },
                    ["status"] = new McpToolParameter { Type = "string", Description = "状態による絞り込み: all、covered、review、no_action。既定値は all", Required = false, EnumValues = new List<string> { "all", "covered", "review", "no_action" } },
                    ["detail"] = new McpToolParameter { Type = "string", Description = "brief または full。side_screen はこの引数を無視する", Required = false, EnumValues = new List<string> { "brief", "full" } },
                    ["includeNoAction"] = new McpToolParameter { Type = "boolean", Description = "side_screen の場合、表示専用/プレイヤー操作のないサイドスクリーンを返すか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "最大返却数。各 audit の元の既定値と上限を維持", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    var delegated = (JObject)args.DeepClone();
                    delegated.Remove("action");

                    switch (action)
                    {
                        case "side_screen":
                            return SideScreenSurfaceTools.AuditSideScreenSurfaces().Handler(delegated);
                        case "user_menu":
                            return UserMenuSurfaceAuditTools.AuditUserMenuSurfaces().Handler(delegated);
                        case "management":
                            return ManagementSurfaceAuditTools.AuditManagementSurfaces().Handler(delegated);
                        case "tool_menu":
                            return ToolMenuSurfaceAuditTools.AuditToolMenuSurfaces().Handler(delegated);
                        case "ui_menu":
                            return UiMenuSurfaceAuditTools.AuditUiMenuSurfaces().Handler(delegated);
                        case "global_control":
                            return GlobalControlSurfaceAuditTools.AuditGlobalControlSurfaces().Handler(delegated);
                        case "notification":
                            return NotificationSurfaceAuditTools.AuditNotificationSurfaces().Handler(delegated);
                        default:
                            return CallToolResult.Error("action must be one of side_screen, user_menu, management, tool_menu, ui_menu, global_control, notification");
                    }
                }
            };
        }
    }
}
