using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class PowerAndRoomTools
    {
        public static McpTool InfrastructureReadControl()
        {
            return new McpTool
            {
                Name = "infrastructure_read_control",
                Group = "infrastructure",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "infrastructure_read" },
                Tags = new List<string> { "power", "electricity", "rooms", "infrastructure", "电力", "房间", "基础设施" },
                Description = "電力・部屋の読み取り専用の集約入口: action=power_summary/power_ports/ports/rooms/room_types。room_types は未成立も含む部屋種別の条件と効果を返します。ports は電力、液体パイプ、気体パイプ、自動化信号、輸送のポートを返します。",
                Parameters = InfrastructureReadParams(),
                Handler = args =>
                {
                    args = args ?? new JObject();
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    var forwarded = new JObject(args);
                    forwarded.Remove("action");

                    switch (action)
                    {
                        case "power_summary":
                            return GetPowerSummary().Handler(forwarded);
                        case "power_ports":
                            return GetBuildingPowerPorts().Handler(forwarded);
                        case "ports":
                        case "utility_ports":
                        case "all_ports":
                            return InfrastructurePortReadTools.ReadPorts(forwarded);
                        case "rooms":
                            return ListRooms().Handler(forwarded);
                        case "room_types":
                            return RoomTypeCatalogTools.ReadRoomTypes(forwarded);
                        default:
                            return CallToolResult.Error("Unsupported action. Use power_summary, power_ports, ports, rooms, or room_types.");
                    }
                }
            };
        }

        public static McpTool GetBuildingPowerPortsCompat()
        {
            var tool = GetBuildingPowerPorts();
            tool.Hidden = true;
            tool.Description = "互換用の入口。read_control domain=infrastructure action=power_ports を使用してください";
            return tool;
        }

        public static McpTool GetPowerSummaryCompat()
        {
            var tool = GetPowerSummary();
            tool.Hidden = true;
            tool.Description = "互換用の入口。read_control domain=infrastructure action=power_summary を使用してください";
            return tool;
        }

        public static McpTool ListRoomsCompat()
        {
            var tool = ListRooms();
            tool.Hidden = true;
            tool.Description = "互換用の入口。read_control domain=infrastructure action=rooms を使用してください";
            return tool;
        }

        public static McpTool GetBuildingPowerPorts()
        {
            return new McpTool
            {
                Name = "building_power_ports",
                Group = "power",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "power_ports_list", "building_power_connection_points", "power_connection_points" },
                Tags = new List<string> { "power", "electricity", "ports", "connector", "wire", "电力", "接口", "接线" },
                Description = "指定区域内の完成済み設備と建築予定の電力接続セルを列挙。アンカー、入出力ポート、相対オフセット、配線可能な状態を返し、テキストマップから推測せずに直接配線できるようにする",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "名前または prefabId のキーワードで絞り込み。例: battery、generator、transformer、wire", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定は 120、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 120, 500);

                    var results = new List<Dictionary<string, object>>();
                    var seen = new HashSet<string>();
                    foreach (var building in Components.BuildingCompletes.Items)
                    {
                        var go = building?.gameObject;
                        if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                            continue;
                        int cell = Grid.PosToCell(go);
                        if (rect != null && !CellInRect(cell, rect, worldId))
                            continue;
                        if (!MatchesQuery(go, query))
                            continue;

                        var def = building.Def;
                        if (def == null)
                            continue;

                        bool hasInput = def.RequiresPowerInput;
                        bool hasOutput = def.RequiresPowerOutput;
                        if (!hasInput && !hasOutput)
                            continue;

                        results.Add(BuildingPowerPortInfo(go, def, hasInput, hasOutput, "built"));
                        seen.Add(BuildingPortKey(go, def));
                        if (results.Count >= limit)
                            break;
                    }

                    if (results.Count < limit)
                    {
                        foreach (var constructable in FindConstructables(worldId))
                        {
                            var go = constructable?.gameObject;
                            if (go == null)
                                continue;
                            int cell = Grid.PosToCell(go);
                            if (rect != null && !CellInRect(cell, rect, worldId))
                                continue;
                            if (!MatchesQuery(go, query))
                                continue;

                            var building = go.GetComponent<Building>();
                            var kpid = go.GetComponent<KPrefabID>();
                            var def = building?.Def ?? Assets.GetBuildingDef(kpid?.PrefabTag.Name ?? go.name);
                            if (def == null)
                                continue;
                            if (seen.Contains(BuildingPortKey(go, def)))
                                continue;

                            bool hasInput = def.RequiresPowerInput;
                            bool hasOutput = def.RequiresPowerOutput;
                            if (!hasInput && !hasOutput)
                                continue;

                            results.Add(BuildingPowerPortInfo(go, def, hasInput, hasOutput, "blueprint"));
                            if (results.Count >= limit)
                                break;
                        }
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = results.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["query"] = string.IsNullOrWhiteSpace(query) ? null : query,
                        ["rect"] = rect,
                        ["buildings"] = results
                    }, McpJsonUtil.Settings));
                }
            };
        }

    }
}
