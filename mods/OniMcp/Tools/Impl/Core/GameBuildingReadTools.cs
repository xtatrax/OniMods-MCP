using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    /// <summary>
    /// ゲーム操作に関する MCP ツール
    /// </summary>
    public static partial class GameControlTools
    {
        public static McpTool ControlBuildingsRead()
        {
            return new McpTool
            {
                Name = "buildings_read_control",
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "buildings_control", "building_read_control" },
                Tags = new List<string> { "buildings", "read", "summary" },
                Description = "設備一覧と設備種類ごとの概要の読み取りを統合。action=list/summary。buildings_list と buildings_summary に対応。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "読み取り操作: list=設備の詳細一覧、summary=設備の種類ごとの集計概要",
                        Required = true
                    },
                    ["type"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "設備の種類で絞り込む（例: Generator, Pump, Bed, Tile）。空欄ならすべて返す",
                        Required = false
                    },
                    ["limit"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "返す設備/設備種類の最大件数。既定は 100、最大 500",
                        Required = false
                    }
                },
                Handler = args =>
                {
                    var action = args["action"]?.ToString()?.Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                        case "buildings":
                            return GetBuildings().Handler(args);
                        case "summary":
                        case "aggregate":
                            return GetBuildingSummary().Handler(args);
                        default:
                            return CallToolResult.Error("action must be one of: list, summary");
                    }
                }
            };
        }

        public static McpTool GetBuildings()
        {
            return new McpTool
            {
                Name = "buildings_list",
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "get_buildings" },
                Description = "互換用の入口。read_control domain=buildings action=list を優先すること。コロニーの設備一覧を取得し、種類で絞り込める",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["type"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "設備の種類で絞り込む（例: Generator, Pump, Bed）。空欄ならすべて返す",
                        Required = false
                    },
                    ["query"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "設備名または prefabId で絞り込む。search/target/name も別名として使用する",
                        Required = false
                    },
                    ["limit"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "返す設備の最大件数。既定は 100、最大 500",
                        Required = false
                    }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    string filterType = args["type"]?.ToString()?.ToLower();
                    string query = args["query"]?.ToString();
                    if (string.IsNullOrWhiteSpace(query)) query = args["search"]?.ToString();
                    if (string.IsNullOrWhiteSpace(query)) query = args["target"]?.ToString();
                    if (string.IsNullOrWhiteSpace(query)) query = args["name"]?.ToString();
                    query = query?.ToLower();
                    if (string.IsNullOrWhiteSpace(filterType))
                        filterType = query;
                    int limit = 100;
                    if (args["limit"] != null && int.TryParse(args["limit"].ToString(), out int parsedLimit))
                        limit = Math.Max(1, Math.Min(parsedLimit, 500));

                    var buildings = new List<Dictionary<string, object>>();
                    var seen = new HashSet<string>();

                    foreach (var building in Components.BuildingCompletes.Items)
                    {
                        if (building == null) continue;

                        var prefabName = building.name;
                        var def = building.Def;
                        var name = ToolUtil.CleanName(def?.Name ?? prefabName);
                        var position = building.transform?.position ?? Vector3.zero;
                        var x = Mathf.RoundToInt(position.x);
                        var y = Mathf.RoundToInt(position.y);
                        var worldId = building.GetMyWorldId();
                        var identity = (def?.PrefabID ?? prefabName) + "|" + x + "|" + y + "|" + worldId;
                        if (!seen.Add(identity)) continue;

                        if (!string.IsNullOrEmpty(filterType) &&
                            !name.ToLower().Contains(filterType) &&
                            !prefabName.ToLower().Contains(filterType))
                            continue;

                        var operational = building.GetComponent<Operational>();

                        buildings.Add(new Dictionary<string, object>
                        {
                            ["name"] = name,
                            ["prefabId"] = def?.PrefabID ?? "unknown",
                            ["position"] = new { x, y },
                            ["isOperational"] = operational?.IsOperational ?? false,
                            ["isActive"] = operational?.IsActive ?? false,
                            ["worldId"] = worldId
                        });
                    }

                    var limited = buildings.Take(limit).ToList();
                    var summary = new Dictionary<string, object>
                    {
                        ["total"] = buildings.Count,
                        ["returned"] = limited.Count,
                        ["query"] = query,
                        ["buildings"] = limited
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(summary, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetBuildingSummary()
        {
            return new McpTool
            {
                Name = "buildings_summary",
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "get_building_summary" },
                Description = "互換用の入口。read_control domain=buildings action=summary を優先すること。設備の種類ごとに数量、稼働状態、ワールド別の分布を集計する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["type"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "設備の種類で絞り込む（例: Generator, Pump, Tile）。空欄ならすべて返す",
                        Required = false
                    },
                    ["limit"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "返す設備種類の最大件数。既定は 100、最大 500",
                        Required = false
                    }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    string filterType = args["type"]?.ToString()?.ToLower();
                    int limit = 100;
                    if (args["limit"] != null && int.TryParse(args["limit"].ToString(), out int parsedLimit))
                        limit = Math.Max(1, Math.Min(parsedLimit, 500));

                    var groups = new Dictionary<string, BuildingAggregate>();
                    var seen = new HashSet<string>();

                    foreach (var building in Components.BuildingCompletes.Items)
                    {
                        if (building == null) continue;

                        var def = building.Def;
                        var prefabId = def?.PrefabID ?? building.name;
                        var name = ToolUtil.CleanName(def?.Name ?? prefabId);
                        var position = building.transform?.position ?? Vector3.zero;
                        var x = Mathf.RoundToInt(position.x);
                        var y = Mathf.RoundToInt(position.y);
                        var worldId = building.GetMyWorldId();
                        var identity = prefabId + "|" + x + "|" + y + "|" + worldId;
                        if (!seen.Add(identity)) continue;

                        if (!string.IsNullOrEmpty(filterType) &&
                            !name.ToLower().Contains(filterType) &&
                            !prefabId.ToLower().Contains(filterType))
                            continue;

                        BuildingAggregate aggregate;
                        if (!groups.TryGetValue(prefabId, out aggregate))
                        {
                            aggregate = new BuildingAggregate
                            {
                                Name = name,
                                PrefabId = prefabId,
                                WorldIds = new HashSet<int>()
                            };
                            groups[prefabId] = aggregate;
                        }

                        var operational = building.GetComponent<Operational>();
                        aggregate.Count++;
                        aggregate.OperationalCount += operational != null && operational.IsOperational ? 1 : 0;
                        aggregate.ActiveCount += operational != null && operational.IsActive ? 1 : 0;
                        aggregate.WorldIds.Add(worldId);
                    }

                    var summaries = groups.Values
                        .OrderByDescending(group => group.Count)
                        .Take(limit)
                        .Select(group => group.ToDictionary())
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["totalTypes"] = groups.Count,
                        ["returned"] = summaries.Count,
                        ["buildings"] = summaries
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        private class BuildingAggregate
        {
            public string Name;
            public string PrefabId;
            public int Count;
            public int OperationalCount;
            public int ActiveCount;
            public HashSet<int> WorldIds;

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["name"] = Name,
                    ["prefabId"] = PrefabId,
                    ["count"] = Count,
                    ["operationalCount"] = OperationalCount,
                    ["activeCount"] = ActiveCount,
                    ["worldIds"] = WorldIds.OrderBy(id => id).ToList()
                };
            }
        }

    }
}
