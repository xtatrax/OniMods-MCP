using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class SpaceStoryTools
    {
        public static McpTool ControlTelescope()
        {
            return new McpTool
            {
                Name = "telescope_control",
                Group = "space",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "telescope_open_starmap", "starmap_toggle_from_telescope" },
                Tags = new List<string> { "space", "telescope", "starmap", "ui", "side-screen" },
                Description = "互換用の入口。building_control domain=space_story kind=telescope action=open_starmap を使用すること",
                Hidden = true,
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "open_starmap", Required = true, EnumValues = new List<string> { "open_starmap" } }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action != "open_starmap")
                        return CallToolResult.Error("action must be open_starmap");
                    if (ManagementMenu.Instance == null)
                        return CallToolResult.Error("ManagementMenu not initialized");
                    ManagementMenu.Instance.ToggleStarmap();
                    return JsonResult(new Dictionary<string, object>
                    {
                        ["action"] = action,
                        ["analysis"] = AnalysisSummary(includeDestinations: false)
                    });
                }
            };
        }

        public static McpTool ListTemporalTears()
        {
            return new McpTool
            {
                Name = "temporal_tears_list",
                Group = "story",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "temporal_tear_status", "wormhole_status" },
                Tags = new List<string> { "story", "temporal-tear", "wormhole", "rocket", "cluster", "side-screen" },
                Description = "互換用の入口。building_control domain=space_story kind=temporal_tear action=list を使用すること",
                Hidden = true,
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "ロケット名、状態、位置でロケット候補を絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返すロケット候補の上限。既定値は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    var tear = GetTemporalTear();
                    if (tear == null)
                        return CallToolResult.Error("TemporalTear not found");
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 100, 500);
                    var candidates = TemporalTearCandidates(tear)
                        .Select(ClustercraftInfo)
                        .Where(info => MatchesQuery(info, query))
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["tear"] = TemporalTearInfo(tear),
                        ["returned"] = candidates.Count,
                        ["candidateCraft"] = candidates
                    });
                }
            };
        }

        public static McpTool ConsumeTemporalTearCraft()
        {
            return new McpTool
            {
                Name = "temporal_tear_consume_craft",
                Group = "story",
                Mode = "execute",
                Risk = "critical",
                Aliases = new List<string> { "temporal_tear_enter", "wormhole_consume_rocket" },
                Tags = new List<string> { "story", "temporal-tear", "wormhole", "rocket", "destructive", "side-screen" },
                Description = "互換用の入口。building_control domain=space_story kind=temporal_tear action=consume_craft を使用すること",
                Hidden = true,
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["craftId"] = new McpToolParameter { Type = "integer", Description = "対象の Clustercraft の InstanceID", Required = false },
                    ["craftName"] = new McpToolParameter { Type = "string", Description = "対象のロケット名。craftId とのどちらか一方を指定する", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "true が必須", Required = false },
                    ["confirmDestructive"] = new McpToolParameter { Type = "string", Description = "consume craft と正確に入力する必要がある", Required = false }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false) || args["confirmDestructive"]?.ToString() != "consume craft")
                        return CallToolResult.Error("confirm=true and confirmDestructive=\"consume craft\" are required; this destroys the craft, modules, and onboard dupes");
                    var tear = GetTemporalTear();
                    if (tear == null)
                        return CallToolResult.Error("TemporalTear not found");
                    if (!tear.IsOpen())
                        return CallToolResult.Error("TemporalTear is closed");
                    var craft = FindClustercraft(args);
                    if (craft == null)
                        return CallToolResult.Error("craftId or craftName must match a Clustercraft at the TemporalTear location");
                    if (craft.Location != tear.Location)
                        return CallToolResult.Error("Clustercraft is not at the TemporalTear location");
                    if (craft.IsFlightInProgress())
                        return CallToolResult.Error("Clustercraft is still in flight and cannot be consumed");

                    var before = TemporalTearInfo(tear);
                    var craftBefore = ClustercraftInfo(craft);
                    tear.ConsumeCraft(craft);
                    return JsonResult(new Dictionary<string, object>
                    {
                        ["action"] = "consume_craft",
                        ["before"] = before,
                        ["craft"] = craftBefore,
                        ["tear"] = TemporalTearInfo(tear)
                    });
                }
            };
        }

        public static McpTool ListProcessConditions()
        {
            return new McpTool
            {
                Name = "process_conditions_list",
                Group = "diagnostics",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "condition_list_side_screen", "rocket_process_conditions" },
                Tags = new List<string> { "conditions", "side-screen", "rocket", "diagnostics", "process" },
                Description = "互換用の入口。building_control domain=space_story kind=process_conditions action=list を使用すること",
                Hidden = true,
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象の設備・ロケットの InstanceID。省略すると、条件を持つすべてのオブジェクトを一覧表示する", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象の設備のセルの X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象の設備のセルの Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標で検索する場合の既定値は現在アクティブなワールド", Required = false },
                    ["conditionType"] = new McpToolParameter { Type = "string", Description = "All、RocketFlight、RocketPrep、RocketStorage、RocketBoard のいずれか。既定値は All", Required = false, EnumValues = new List<string> { "All", "RocketFlight", "RocketPrep", "RocketStorage", "RocketBoard" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "オブジェクト名、prefabId、条件テキスト、状態で絞り込む", Required = false },
                    ["showHidden"] = new McpToolParameter { Type = "boolean", Description = "ShowInUI=false の条件を含めるか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返すオブジェクト数の上限。既定値は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    var type = ParseConditionType(args["conditionType"]?.ToString());
                    bool showHidden = ToolUtil.GetBool(args, "showHidden", false);
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 100, 500);
                    var target = FindConditionTarget(args);
                    IEnumerable<GameObject> targets = target == null ? ConditionTargets() : new[] { target };
                    var items = targets
                        .Select(go => ProcessConditionSetInfo(go, type, showHidden))
                        .Where(info => info != null)
                        .Where(info => MatchesQuery(info, query))
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = items.Count,
                        ["conditionType"] = type.ToString(),
                        ["targets"] = items
                    });
                }
            };
        }

    }
}
