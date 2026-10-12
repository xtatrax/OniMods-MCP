using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class OrdersTools
{
        public static McpTool ListPriorities()
        {
            return new McpTool
            {
                Name = "priorities_list",
                Hidden = true,
                Group = "orders",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "orders_priorities_list" },
                Description = "互換用エントリー。orders_control domain=priority action=list を優先して使用すること。優先度を設定できるオブジェクトを一覧表示する。範囲、ワールド、名前で絞り込み可能",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "名前または prefabId のキーワードで絞り込む", Required = false },
                    ["includeInactive"] = new McpToolParameter { Type = "boolean", Description = "現在優先度を設定できないオブジェクトを含めるか。既定値 false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = GetWorldFilter(args, hasRect);
                    string query = args["query"]?.ToString();
                    bool includeInactive = ToolUtil.GetBool(args, "includeInactive", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var items = new List<Dictionary<string, object>>();
                    foreach (var prioritizable in Components.Prioritizables.Items)
                    {
                        if (!MatchesPriorityTarget(prioritizable, rect, worldId, query, includeInactive))
                            continue;

                        items.Add(PriorityTargetToDictionary(prioritizable));
                        if (items.Count >= limit)
                            break;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = items.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["rect"] = rect,
                        ["priorities"] = items
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetBuildingPriority()
        {
            return new McpTool
            {
                Name = "buildings_set_priority",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Description = "互換用エントリー。orders_control domain=priority action=set_building を優先して使用すること。設備または優先度を設定できるオブジェクトの作業優先度を設定する",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "優先度 1-9", Required = true },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "赤色の最優先にするか。既定値は false", Required = false }
                }),
                Handler = args =>
                {
                    GameObject go;
                    string error;
                    if (!TryFindPriorityTarget(args, out go, out error))
                        return CallToolResult.Error(error);
                    var prioritizable = go.GetComponent<Prioritizable>();
                    if (prioritizable == null)
                        return CallToolResult.Error("Target is not prioritizable");

                    int priority = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "priority") ?? 5, 9));
                    bool top = ToolUtil.GetBool(args, "topPriority", false);
                    var setting = new PrioritySetting(top ? PriorityScreen.PriorityClass.topPriority : PriorityScreen.PriorityClass.basic, top ? 1 : priority);
                    prioritizable.SetMasterPriority(setting);
                    return CallToolResult.Text($"Set priority for {go.GetProperName()} to {(top ? "topPriority" : priority.ToString())}");
                }
            };
        }

        public static McpTool SetPriorityArea()
        {
            return new McpTool
            {
                Name = "priorities_set_area",
                Hidden = true,
                Group = "orders",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "orders_set_priority_area", "set_priority_area" },
                Description = "互換用エントリー。orders_control domain=priority action=set_area を優先して使用すること。矩形範囲内の優先度を設定できるオブジェクトの作業優先度を一括設定する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "優先度 1-9", Required = true },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "赤色の最優先にするか。既定値は false", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "任意。名前または prefabId のキーワードで絞り込む", Required = false },
                    ["includeInactive"] = new McpToolParameter { Type = "boolean", Description = "現在優先度を設定できないオブジェクトを含めるか。既定値 false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "変更件数の上限。既定値 200、最大 1000", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "領域が 100 セルを超える場合は true が必須", Required = false }
                }),
                Handler = args =>
                {
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (cells > 100 && !ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required when changing priorities in more than 100 cells");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    string query = args["query"]?.ToString();
                    bool includeInactive = ToolUtil.GetBool(args, "includeInactive", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 200, 1000));
                    var setting = ParsePrioritySetting(args);
                    var changed = new List<Dictionary<string, object>>();
                    int matched = 0;

                    foreach (var prioritizable in Components.Prioritizables.Items)
                    {
                        if (!MatchesPriorityTarget(prioritizable, rect, worldId, query, includeInactive))
                            continue;

                        matched++;
                        if (changed.Count >= limit)
                            continue;

                        prioritizable.SetMasterPriority(setting);
                        changed.Add(PriorityTargetToDictionary(prioritizable));
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["matched"] = matched,
                        ["changed"] = changed.Count,
                        ["skippedByLimit"] = Math.Max(0, matched - changed.Count),
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["priorityClass"] = setting.priority_class.ToString(),
                        ["priority"] = setting.priority_value,
                        ["targets"] = changed
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlPriority()
        {
            return new McpTool
            {
                Name = "priority_control",
                Group = "orders",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "orders_priority_control", "prioritize_control" },
                Tags = new List<string> { "orders", "priority", "prioritize", "buildings", "area" },
                Description = "優先度の統合ツール。action=list/set_building/set_area。優先度を設定できるオブジェクトを参照、または単体や範囲の優先度を設定する。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、set_building または set_area", Required = true, EnumValues = new List<string> { "list", "set_building", "set_area" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=set_building の場合の対象オブジェクトの InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=set_building の場合の対象セル X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=set_building の場合の対象セル Y", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "action=set_building/set_area の場合の優先度 1-9", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "action=set_building/set_area の場合、赤色の最優先に設定するか。既定値 false", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list/set_building/set_area の場合、名前または prefabId のキーワードで絞り込む", Required = false },
                    ["includeInactive"] = new McpToolParameter { Type = "boolean", Description = "action=list/set_area の場合、現在優先度を設定できないオブジェクトを含めるか。既定値 false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list/set_area の場合の返却件数または変更件数の上限", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set_area かつ範囲が 100 セルを超える場合は true が必要", Required = false }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListPriorities().Handler(args);
                    if (action == "set_building" || action == "set")
                        return SetBuildingPriority().Handler(args);
                    if (action == "set_area" || action == "area")
                        return SetPriorityArea().Handler(args);
                    return CallToolResult.Error("action must be list, set_building, or set_area");
                }
            };
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
            ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID", Required = false },
            ["x"] = new McpToolParameter { Type = "integer", Description = "対象セル X。省略時は query/target/search による検索で位置を特定できる", Required = false },
            ["y"] = new McpToolParameter { Type = "integer", Description = "対象セル Y。省略時は query/target/search による検索で位置を特定できる", Required = false },
            ["query"] = new McpToolParameter { Type = "string", Description = "オブジェクト名または prefabId で検索する。複数の一致がある場合はエラーとなり、id/x/y の使用を求める", Required = false },
            ["target"] = new McpToolParameter { Type = "string", Description = "query の別名", Required = false },
            ["search"] = new McpToolParameter { Type = "string", Description = "query の別名", Required = false },
            ["nearX"] = new McpToolParameter { Type = "integer", Description = "互換用パラメーター。複数対象のあいまいさの自動解消には使用しない。代わりに id/x/y を使用すること", Required = false },
            ["nearY"] = new McpToolParameter { Type = "integer", Description = "互換用パラメーター。複数対象のあいまいさの自動解消には使用しない。代わりに id/x/y を使用すること", Required = false },
            ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールド ID。既定値は現在のアクティブワールド", Required = false }
        };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の領域ハンドル。指定すると x1/y1/x2/y2 を省略できる", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の左下・始点の X。areaId を使う場合は省略できる", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の左下・始点の Y。areaId を使う場合は省略できる", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の右上・終点の X。areaId を使う場合は省略できる", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の右上・終点の Y。areaId を使う場合は省略できる", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定値は areaId に関連付けられたワールド、または現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static PrioritySetting ParsePrioritySetting(Newtonsoft.Json.Linq.JObject args)
        {
            bool top = ToolUtil.GetBool(args, "topPriority", false);
            int priority = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "priority") ?? 5, 9));
            return new PrioritySetting(top ? PriorityScreen.PriorityClass.topPriority : PriorityScreen.PriorityClass.basic, top ? 1 : priority);
        }

        private static int GetWorldFilter(Newtonsoft.Json.Linq.JObject args, bool hasRect)
        {
            if (ToolUtil.GetInt(args, "worldId").HasValue)
                return ToolUtil.GetInt(args, "worldId").Value;
            if (hasRect)
                return ToolUtil.ResolveWorldId(args);
            return -1;
        }

        private static bool MatchesPriorityTarget(Prioritizable prioritizable, Dictionary<string, int> rect, int worldId, string query, bool includeInactive)
        {
            var go = prioritizable?.gameObject;
            if (go == null) return false;
            if (!includeInactive && !prioritizable.IsPrioritizable()) return false;
            if (!ToolUtil.GameObjectMatchesWorld(go, worldId)) return false;

            int cell = Grid.PosToCell(go);
            if (rect != null && !CellInRect(cell, rect, worldId)) return false;
            if (!MatchesQuery(go, query)) return false;
            return true;
        }

        private static bool MatchesQuery(GameObject go, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string q = query.Trim();
            var kpid = go.GetComponent<KPrefabID>();
            string prefabId = kpid?.PrefabTag.Name ?? go.name;
            string name = ToolUtil.CleanName(go.GetProperName());
            return Contains(name, q) || Contains(prefabId, q) || Contains(go.name, q);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, object> PriorityTargetToDictionary(Prioritizable prioritizable)
        {
            var go = prioritizable.gameObject;
            var kpid = go.GetComponent<KPrefabID>();
            var setting = prioritizable.GetMasterPriority();
            int cell = Grid.PosToCell(go);
            int x = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : Mathf.RoundToInt(go.transform.GetPosition().x);
            int y = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : Mathf.RoundToInt(go.transform.GetPosition().y);

            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["name"] = ToolUtil.CleanName(go.GetProperName()),
                ["prefabId"] = kpid?.PrefabTag.Name ?? go.name,
                ["position"] = new { x, y },
                ["worldId"] = GetTargetWorldId(go, cell),
                ["isPrioritizable"] = prioritizable.IsPrioritizable(),
                ["priorityClass"] = setting.priority_class.ToString(),
                ["priority"] = setting.priority_value,
                ["topPriority"] = setting.priority_class == PriorityScreen.PriorityClass.topPriority
            };
        }
}
}
