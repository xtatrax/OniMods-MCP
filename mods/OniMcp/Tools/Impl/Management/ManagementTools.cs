using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static class ManagementTools
    {
        public static McpTool ControlManagement()
        {
            return new McpTool
            {
                Name = "management_control",
                Group = "management",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "colony_management_control", "management_screen_control" },
                Tags = new List<string> { "management", "schedule", "diet", "research", "medical" },
                Description = "コロニー管理の集約入口: domain=schedule/diet/research/medical。スケジュール、食事の許可、研究キュー、医療管理に振り分け、各ツールの元の action と確認ルールを維持する。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "schedule、diet、research、medical のいずれか", Required = true, EnumValues = new List<string> { "schedule", "diet", "research", "medical" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "対応する domain の元の action。例: schedule=list/create/set_block/assign_dupe/optimize、diet=status/set/policy、research=status/list/set/clear、medical=patients/clinics/doctor_stations/set_threshold/batch_set_threshold/assign_bed", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象の InstanceID。サブ操作の意味に従って使用する", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "対象の名前。サブ操作の意味に従って使用する", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "検索または絞り込みの語句。サブ操作の意味に従って使用する", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "一覧または一括処理の上限。サブ操作の意味に従って使用する", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "書き込みや危険な操作で確認が必要な場合は true を渡す", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    switch (domain)
                    {
                        case "schedule":
                        case "schedules":
                            return ScheduleTools.ControlSchedule().Handler(args);
                        case "diet":
                        case "consumables":
                            return DietTools.ControlDiet().Handler(args);
                        case "research":
                        case "tech":
                            return ResearchTools.ControlResearch().Handler(args);
                        case "medical":
                        case "medicine":
                        case "clinic":
                            return MedicalTools.ControlMedical().Handler(args);
                        default:
                            return CallToolResult.Error("domain must be schedule, diet, research, or medical");
                    }
                }
            };
        }
    }
}
