using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static class ResearchTools
    {
        public static McpTool GetResearchStatus()
        {
            return new McpTool
            {
                Name = "research_status",
                Group = "research",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "互換用の入口。colony_control domain=management kind=research action=status を優先すること。現在の研究対象、キュー、進捗を確認する",
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args =>
                {
                    if (Research.Instance == null || Db.Get()?.Techs == null)
                        return CallToolResult.Error("Research not initialized");

                    var active = Research.Instance.GetActiveResearch();
                    var target = Research.Instance.GetTargetResearch();
                    var queue = Research.Instance.GetResearchQueue();
                    var result = new Dictionary<string, object>
                    {
                        ["active"] = active != null ? TechToDictionary(active.tech, includeDetails: true) : null,
                        ["target"] = target != null ? TechToDictionary(target.tech, includeDetails: false) : null,
                        ["queue"] = queue.Select(item => TechToDictionary(item.tech, includeDetails: false)).ToList()
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListResearch()
        {
            return new McpTool
            {
                Name = "research_list",
                Group = "research",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "list_research" },
                Description = "互換用の入口。colony_control domain=management kind=research action=list を優先すること。研究可能な技術を一覧表示または検索する。query は技術の ID、名前、解禁される設備の ID/名前、検索語に一致する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "任意の検索語", Required = false },
                    ["includeComplete"] = new McpToolParameter { Type = "boolean", Description = "研究済みの技術を含めるか。既定は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す最大件数。既定は 30、最大 100", Required = false }
                },
                Handler = args =>
                {
                    if (Research.Instance == null || Db.Get()?.Techs == null)
                        return CallToolResult.Error("Research not initialized");

                    string query = args["query"]?.ToString();
                    bool includeComplete = ToolUtil.GetBool(args, "includeComplete", true);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 30, 100));

                    var matches = AllTechs()
                        .Where(tech => includeComplete || !IsComplete(tech))
                        .Where(tech => string.IsNullOrWhiteSpace(query) || Matches(tech, query))
                        .OrderBy(tech => IsComplete(tech))
                        .ThenBy(tech => tech.tier)
                        .ThenBy(tech => tech.Id)
                        .Take(limit)
                        .Select(tech => TechToDictionary(tech, includeDetails: true))
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["query"] = string.IsNullOrWhiteSpace(query) ? null : query,
                        ["returned"] = matches.Count,
                        ["research"] = matches
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetResearch()
        {
            return new McpTool
            {
                Name = "research_set",
                Group = "research",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "set_research" },
                Description = "互換用の入口。colony_control domain=management kind=research action=set を優先すること。現在の研究対象を選ぶ。id による正確な指定を優先し、query で技術や解禁される設備を検索することもできる",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "string", Description = "技術 ID。例: FarmingTech、SanitationSciences、ImprovedOxygen", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "検索語。技術の名前、ID、解禁される項目に一致する", Required = false },
                    ["clearQueue"] = new McpToolParameter { Type = "boolean", Description = "以前のキューを空にするか。既定は true", Required = false }
                },
                Handler = args =>
                {
                    if (Research.Instance == null || Db.Get()?.Techs == null)
                        return CallToolResult.Error("Research not initialized");

                    string id = args["id"]?.ToString();
                    string query = args["query"]?.ToString();
                    bool clearQueue = ToolUtil.GetBool(args, "clearQueue", true);
                    Tech tech = null;

                    if (!string.IsNullOrWhiteSpace(id))
                        tech = Db.Get().Techs.TryGet(id.Trim());

                    if (tech == null && !string.IsNullOrWhiteSpace(query))
                    {
                        var matches = FindMatches(query).Where(candidate => !IsComplete(candidate)).ToList();
                        var exact = matches.FirstOrDefault(candidate => IsExactMatch(candidate, query));
                        if (exact != null)
                            tech = exact;
                        else if (matches.Count == 1)
                            tech = matches[0];
                        else if (matches.Count > 1)
                        {
                            var result = new Dictionary<string, object>
                            {
                                ["error"] = "Multiple research matches; pass id to select one",
                                ["matches"] = matches.Take(10).Select(candidate => TechToDictionary(candidate, includeDetails: true)).ToList()
                            };
                            return CallToolResult.Error(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                        }
                    }

                    if (tech == null)
                        return CallToolResult.Error("Research tech not found");

                    if (IsComplete(tech))
                        return CallToolResult.Error($"Research already complete: {tech.Id}");

                    Research.Instance.SetActiveResearch(tech, clearQueue);

                    var active = Research.Instance.GetActiveResearch();
                    var queue = Research.Instance.GetResearchQueue();
                    var response = new Dictionary<string, object>
                    {
                        ["selected"] = TechToDictionary(tech, includeDetails: true),
                        ["active"] = active != null ? TechToDictionary(active.tech, includeDetails: false) : null,
                        ["queue"] = queue.Select(item => TechToDictionary(item.tech, includeDetails: false)).ToList()
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(response, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ClearResearch()
        {
            return new McpTool
            {
                Name = "research_clear",
                Group = "research",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "clear_research", "research_cancel", "research_queue_clear" },
                Tags = new List<string> { "research", "queue", "cancel", "clear", "management", "researchscreen" },
                Description = "互換用の入口。colony_control domain=management kind=research action=clear を優先すること。現在の研究キューをキャンセルする。ResearchScreen の研究キャンセルボタンと同等。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "true が必須。現在の研究キューを空にすることを確認する", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to clear the research queue");

                    if (Research.Instance == null || Db.Get()?.Techs == null)
                        return CallToolResult.Error("Research not initialized");

                    var activeBefore = Research.Instance.GetActiveResearch();
                    var targetBefore = Research.Instance.GetTargetResearch();
                    var queue = Research.Instance.GetResearchQueue();
                    var queueBefore = queue.Select(item => TechToDictionary(item.tech, includeDetails: false)).ToList();

                    queue.Clear();

                    var activeAfter = Research.Instance.GetActiveResearch();
                    var targetAfter = Research.Instance.GetTargetResearch();
                    var queueAfter = Research.Instance.GetResearchQueue()
                        .Select(item => TechToDictionary(item.tech, includeDetails: false))
                        .ToList();

                    var response = new Dictionary<string, object>
                    {
                        ["cleared"] = queueBefore.Count,
                        ["before"] = new Dictionary<string, object>
                        {
                            ["active"] = activeBefore != null ? TechToDictionary(activeBefore.tech, includeDetails: false) : null,
                            ["target"] = targetBefore != null ? TechToDictionary(targetBefore.tech, includeDetails: false) : null,
                            ["queue"] = queueBefore
                        },
                        ["after"] = new Dictionary<string, object>
                        {
                            ["active"] = activeAfter != null ? TechToDictionary(activeAfter.tech, includeDetails: false) : null,
                            ["target"] = targetAfter != null ? TechToDictionary(targetAfter.tech, includeDetails: false) : null,
                            ["queue"] = queueAfter
                        }
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(response, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlResearch()
        {
            return new McpTool
            {
                Name = "research_control",
                Group = "research",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "research_queue_control", "research_management" },
                Tags = new List<string> { "research", "queue", "tech", "management", "researchscreen" },
                Description = "研究キューの確認、検索、設定、消去を統合。action=status/list/set/clear。status は現在のキューの確認、list は技術検索、set は現在の研究の選択、clear は confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: status、list、set、clear", Required = true },
                    ["id"] = new McpToolParameter { Type = "string", Description = "action=set の技術 ID。例: FarmingTech、SanitationSciences、ImprovedOxygen", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list/set の検索語。技術の名前、ID、解禁される項目に一致する", Required = false },
                    ["includeComplete"] = new McpToolParameter { Type = "boolean", Description = "action=list で研究済みの技術を含めるか。既定は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す最大件数。既定は 30、最大 100", Required = false },
                    ["clearQueue"] = new McpToolParameter { Type = "boolean", Description = "action=set で以前のキューを空にするか。既定は true", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=clear では true が必須。現在の研究キューを空にすることを確認する", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "status")
                        return GetResearchStatus().Handler(args);
                    if (action == "list")
                        return ListResearch().Handler(args);
                    if (action == "set")
                        return SetResearch().Handler(args);
                    if (action == "clear")
                        return ClearResearch().Handler(args);
                    return CallToolResult.Error("action must be one of status, list, set, clear");
                }
            };
        }

        private static IEnumerable<Tech> AllTechs()
        {
            var techs = Db.Get().Techs;
            for (int i = 0; i < techs.Count; i++)
            {
                var tech = techs.GetResource(i) as Tech;
                if (tech != null)
                    yield return tech;
            }
        }

        private static List<Tech> FindMatches(string query)
        {
            return AllTechs()
                .Where(tech => Matches(tech, query))
                .OrderByDescending(tech => IsExactMatch(tech, query))
                .ThenBy(tech => tech.tier)
                .ThenBy(tech => tech.Id)
                .ToList();
        }

        private static bool Matches(Tech tech, string query)
        {
            if (tech == null || string.IsNullOrWhiteSpace(query))
                return false;

            string q = query.Trim();
            return Contains(tech.Id, q)
                || Contains(tech.Name, q)
                || Contains(tech.category, q)
                || tech.searchTerms.Any(term => Contains(term, q))
                || tech.unlockedItemIDs.Any(item => Contains(item, q))
                || tech.unlockedItems.Any(item => item != null && (Contains(item.Id, q) || Contains(item.Name, q)));
        }

        private static bool IsExactMatch(Tech tech, string query)
        {
            if (tech == null || string.IsNullOrWhiteSpace(query))
                return false;

            string q = query.Trim();
            return EqualsIgnoreCase(tech.Id, q)
                || EqualsIgnoreCase(tech.Name, q)
                || tech.unlockedItemIDs.Any(item => EqualsIgnoreCase(item, q))
                || tech.unlockedItems.Any(item => item != null && (EqualsIgnoreCase(item.Id, q) || EqualsIgnoreCase(item.Name, q)));
        }

        private static Dictionary<string, object> TechToDictionary(Tech tech, bool includeDetails)
        {
            var instance = Research.Instance?.Get(tech);
            var result = new Dictionary<string, object>
            {
                ["id"] = tech.Id,
                ["name"] = tech.Name,
                ["tier"] = tech.tier,
                ["category"] = tech.category,
                ["complete"] = instance?.IsComplete() ?? false,
                ["active"] = Research.Instance?.IsBeingResearched(tech) ?? false,
                ["costs"] = tech.costsByResearchTypeID.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 2))
            };

            if (instance != null)
                result["progress"] = Math.Round(instance.GetTotalPercentageComplete() * 100.0, 1);

            if (includeDetails)
            {
                result["description"] = tech.desc;
                result["requires"] = tech.requiredTech.Select(required => new Dictionary<string, object>
                {
                    ["id"] = required.Id,
                    ["name"] = required.Name,
                    ["complete"] = IsComplete(required)
                }).ToList();
                result["unlocks"] = tech.unlockedItems.Select(item => new Dictionary<string, object>
                {
                    ["id"] = item.Id,
                    ["name"] = item.Name
                }).ToList();
            }

            return result;
        }

        private static bool IsComplete(Tech tech)
        {
            return Research.Instance?.Get(tech)?.IsComplete() ?? false;
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EqualsIgnoreCase(string value, string query)
        {
            return string.Equals(value, query, StringComparison.OrdinalIgnoreCase);
        }
    }
}
