using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class ToolCatalogTools
{
        public static McpTool ControlToolCatalog()
        {
            return new McpTool
            {
                Name = "tools_catalog_control",
                Group = "tools",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "tools_control", "tool_catalog_control" },
                Tags = new List<string> { "catalog", "search", "discovery", "intent", "manifest", "guide", "coverage", "audit", "surfaces", "low-token" },
                Description = "ツールカタログの統合窓口: action=manifest/search/guide/coverage/static_audit/surface_audit。ツール一覧、ツール検索、目的別ガイド、対応状況の監査、surface の監査を返す。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "カタログのアクション: manifest=ツール一覧、search=ツール検索、guide=目的別のツールガイド生成、coverage=プレイヤー操作への対応状況、static_audit=静的な自己検査、surface_audit=surface の対応状況の監査", Required = true, EnumValues = new List<string> { "manifest", "search", "guide", "coverage", "static_audit", "surface_audit" } },
                    ["surface"] = new McpToolParameter { Type = "string", Description = "action=surface_audit の場合の監査タイプ: side_screen/user_menu/management/tool_menu/ui_menu/global_control/notification", Required = false, EnumValues = new List<string> { "side_screen", "user_menu", "management", "tool_menu", "ui_menu", "global_control", "notification" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=manifest/search/coverage/surface_audit の場合のキーワードまたは目的", Required = false },
                    ["goal"] = new McpToolParameter { Type = "string", Description = "action=guide の場合のプレイヤーの目標または操作の意図", Required = false },
                    ["group"] = new McpToolParameter { Type = "string", Description = "action=manifest/search/coverage の場合、ツールまたは操作グループで絞り込み", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "action=manifest/search の場合、read/write/execute/any で絞り込み", Required = false },
                    ["risk"] = new McpToolParameter { Type = "string", Description = "action=manifest/search/static_audit の場合、none/low/medium/dangerous/any で絞り込み", Required = false },
                    ["status"] = new McpToolParameter { Type = "string", Description = "action=coverage の場合は all/covered/partial/missing、action=surface_audit の場合は all/covered/review/no_action で絞り込み", Required = false, EnumValues = new List<string> { "all", "covered", "partial", "missing", "review", "no_action" } },
                    ["detail"] = new McpToolParameter { Type = "string", Description = "返却する詳細度。manifest/search/coverage は brief/compact/full、guide は brief/compact に対応", Required = false },
                    ["includeResources"] = new McpToolParameter { Type = "boolean", Description = "action=coverage の場合、resourceAnchors を返すか", Required = false },
                    ["includeHotkeys"] = new McpToolParameter { Type = "boolean", Description = "action=coverage の場合、ゲームの Action 列挙型のホットキー対応状況の概要を返すか", Required = false },
                    ["includeNoAction"] = new McpToolParameter { Type = "boolean", Description = "action=surface_audit surface=side_screen の場合、表示専用/プレイヤー操作のないサイドスクリーンを返すか", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=manifest/search/coverage の場合の最大返却項目数", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "manifest":
                            return GetToolsManifest().Handler(args);
                        case "search":
                            return SearchTools().Handler(args);
                        case "guide":
                            return GetToolsGuide().Handler(args);
                        case "coverage":
                        case "player_action_coverage":
                            return ToolCoverageTools.GetPlayerActionCoverage().Handler(args);
                        case "static_audit":
                        case "audit":
                            return ToolCoverageTools.GetStaticAudit().Handler(args);
                        case "surface_audit":
                        case "surface":
                            return HandleSurfaceAudit(args);
                        default:
                            return CallToolResult.Error("action must be one of: manifest, search, guide, coverage, static_audit, surface_audit");
                    }
                }
            };
        }

        private static CallToolResult HandleSurfaceAudit(JObject args)
        {
            string surface = (args["surface"]?.ToString() ?? args["kind"]?.ToString() ?? args["audit"]?.ToString() ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(surface))
                return CallToolResult.Error("surface is required for action=surface_audit; use one of side_screen, user_menu, management, tool_menu, ui_menu, global_control, notification");

            var delegated = (JObject)args.DeepClone();
            delegated["action"] = surface;
            delegated.Remove("surface");
            delegated.Remove("kind");
            delegated.Remove("audit");
            return SurfaceAuditControlTools.ControlSurfaceAudit().Handler(delegated);
        }
}
}
