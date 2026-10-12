using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static class RocketSystemControlTools
    {
        public static McpTool ControlRocketSystem()
        {
            return new McpTool
            {
                Name = "rocket_system_control",
                Group = "rockets",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "rocket_control", "rocket_domain_control" },
                Tags = new List<string> { "rocket", "space", "modules", "crew", "cargo", "restriction", "side-screen" },
                Description = "ロケットシステムの統合エントリーポイント。domain=ops/module/flight_utility/restriction/usage/crew_request/assignment_group/cargo_status/self_destruct。action を対応する旧 control へそのまま渡す。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "ロケットのサブシステム: ops、module、flight_utility、restriction、usage、crew_request、assignment_group、cargo_status、self_destruct", Required = true, EnumValues = new List<string> { "ops", "module", "flight_utility", "restriction", "usage", "crew_request", "assignment_group", "cargo_status", "self_destruct" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "サブシステムの操作。対応する旧 control の action 値を使う", Required = true },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象のロケット、モジュール、設備、コントローラーの InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "対象のロケット名。domain=ops で使用できる", Required = false },
                    ["rocketId"] = new McpToolParameter { Type = "integer", Description = "対象のロケットの InstanceID", Required = false },
                    ["rocketName"] = new McpToolParameter { Type = "string", Description = "対象のロケット名", Required = false },
                    ["moduleId"] = new McpToolParameter { Type = "integer", Description = "対象のロケットモジュールの InstanceID", Required = false },
                    ["moduleName"] = new McpToolParameter { Type = "string", Description = "対象のロケットモジュール名", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "読み取り操作のキーワードによる絞り込み", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "読み取り操作で返す件数の上限", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "内部の書き込み・実行・危険な操作では true が必要", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (domain)
                    {
                        case "ops":
                        case "operation":
                        case "operations":
                            return RocketTools.ControlRocketOps().Handler(args);
                        case "module":
                        case "modules":
                            return RocketModuleTools.ControlModule().Handler(args);
                        case "flight_utility":
                        case "flight":
                        case "utility":
                            return RocketFlightUtilityTools.ControlFlightUtility().Handler(args);
                        case "restriction":
                        case "restrictions":
                            return RocketFlightUtilityTools.ControlRocketRestriction().Handler(args);
                        case "usage":
                        case "usage_control":
                            return SpecialUserMenuActionTools.ControlRocketUsage().Handler(args);
                        case "crew_request":
                        case "crew":
                            return RocketCrewCargoTools.ControlCrewRequest().Handler(args);
                        case "assignment_group":
                        case "assignment":
                            return RocketCrewCargoTools.ControlAssignmentGroup().Handler(args);
                        case "cargo_status":
                        case "cargo":
                            return RocketCrewCargoTools.ControlCargoStatus().Handler(args);
                        case "self_destruct":
                            return MiscSideScreenTools.ControlSelfDestruct().Handler(args);
                        default:
                            return CallToolResult.Error("domain must be ops, module, flight_utility, restriction, usage, crew_request, assignment_group, cargo_status, or self_destruct");
                    }
                }
            };
        }
    }
}
