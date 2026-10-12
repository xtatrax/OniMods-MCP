using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static class GeoTunerTools
    {
        private const int MaxTunersPerGeyser = 5;

        public static McpTool ControlGeoTuner()
        {
            return new McpTool
            {
                Name = "geo_tuner_control",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "geotuner_control", "geo_tuner_assignment_control" },
                Tags = new List<string> { "buildings", "geotuner", "geyser", "side-screen", "spaced-out" },
                Description = "GeoTuner の集約ツール。action=list で地殻チューナーを照会し、action=list_geysers で選択可能な間欠泉を照会し、action=assign で対象の間欠泉を設定または解除する",
                Parameters = GeoTunerControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListGeoTuners().Handler(args);
                    if (action == "list_geysers" || action == "geysers")
                        return ListGeoTunerGeysers().Handler(args);
                    if (action == "assign" || action == "set")
                        return AssignGeoTuner().Handler(args);
                    return CallToolResult.Error("action must be list, list_geysers, or assign");
                }
            };
        }

        public static McpTool ListGeoTuners()
        {
            return new McpTool
            {
                Name = "geo_tuners_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "geotuners_list", "geo_tuner_assignments_list" },
                Tags = new List<string> { "buildings", "geotuner", "geyser", "side-screen", "spaced-out" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=geo_tuner action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、間欠泉名、間欠泉の種類で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var tuners = AllGeoTuners(worldId)
                        .Where(tuner => MatchesTarget(tuner?.gameObject, rect, worldId))
                        .Select(GeoTunerInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = tuners.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["maxTunersPerGeyser"] = MaxTunersPerGeyser,
                        ["geoTuners"] = tuners
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListGeoTunerGeysers()
        {
            return new McpTool
            {
                Name = "geo_tuner_geysers_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "geotuner_geysers_list", "geotuner_targets_list" },
                Tags = new List<string> { "buildings", "geotuner", "geyser", "side-screen", "spaced-out" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=geo_tuner action=list_geysers を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "間欠泉名、prefabId、間欠泉の種類で絞り込む", Required = false },
                    ["includeUnstudied"] = new McpToolParameter { Type = "boolean", Description = "表示されているが未分析の間欠泉を含めるか。既定値は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    var tuner = HasLookupInput(args) ? FindGeoTuner(args) : null;
                    int worldId = tuner != null ? tuner.GetMyWorldId() : ToolUtil.ResolveWorldId(args);
                    string query = args["query"]?.ToString();
                    bool includeUnstudied = ToolUtil.GetBool(args, "includeUnstudied", true);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var geysers = Components.Geysers.GetItems(worldId)
                        .Where(geyser => geyser != null)
                        .Select(geyser => GeyserInfo(geyser, tuner))
                        .Where(info => (bool)info["studied"] || (includeUnstudied && (bool)info["visible"] && (bool)info["uncovered"]))
                        .Where(info => MatchesQuery(info, query))
                        .OrderByDescending(info => (bool)info["studied"])
                        .ThenBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["worldId"] = worldId,
                        ["targetGeoTuner"] = tuner != null ? TargetInfo(tuner.gameObject) : null,
                        ["maxTunersPerGeyser"] = MaxTunersPerGeyser,
                        ["returned"] = geysers.Count,
                        ["geysers"] = geysers
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool AssignGeoTuner()
        {
            return new McpTool
            {
                Name = "geo_tuner_assign",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "geotuner_assign", "geo_tuner_set_geyser" },
                Tags = new List<string> { "buildings", "geotuner", "geyser", "side-screen", "spaced-out" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=side_surface surface=geo_tuner action=assign を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["geyserId"] = new McpToolParameter { Type = "integer", Description = "対象の間欠泉の InstanceID。clear=true の場合は無視する", Required = false },
                    ["geyserX"] = new McpToolParameter { Type = "integer", Description = "対象の間欠泉のセルの X。geyserId が空の場合は座標で指定できる", Required = false },
                    ["geyserY"] = new McpToolParameter { Type = "integer", Description = "対象の間欠泉のセルの Y。geyserId が空の場合は座標で指定できる", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "true は Nothing を選択し、今後の対象を解除する", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "未分析と上限 5 台のチェックを省略する。既定値は false", Required = false }
                }),
                Handler = args =>
                {
                    var tuner = FindGeoTuner(args);
                    if (tuner == null)
                        return CallToolResult.Error("Target GeoTuner not found");

                    bool clear = ToolUtil.GetBool(args, "clear", false);
                    bool force = ToolUtil.GetBool(args, "force", false);
                    Geyser geyser = null;
                    if (!clear)
                    {
                        geyser = FindGeyser(args, tuner.GetMyWorldId());
                        if (geyser == null)
                            return CallToolResult.Error("Target geyser not found; provide geyserId or geyserX/geyserY, or set clear=true");
                        if (geyser.GetMyWorldId() != tuner.GetMyWorldId())
                            return CallToolResult.Error("GeoTuner and geyser must be in the same world");
                        if (!force && !IsStudied(geyser))
                            return CallToolResult.Error("GeoTuner side screen only allows studied geysers; use force=true to override");
                        int count = CountFutureOrAssigned(tuner.GetMyWorldId(), geyser);
                        if (!force && geyser != tuner.GetFutureGeyser() && count >= MaxTunersPerGeyser)
                            return CallToolResult.Error("Target geyser already has the maximum number of assigned/future GeoTuners");
                    }

                    var before = GeoTunerInfo(tuner);
                    tuner.AssignFutureGeyser(geyser);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(tuner.gameObject),
                        ["clear"] = clear,
                        ["before"] = before,
                        ["geoTuner"] = GeoTunerInfo(tuner),
                        ["geyser"] = geyser != null ? GeyserInfo(geyser, tuner) : null
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static Dictionary<string, object> GeoTunerInfo(GeoTuner.Instance tuner)
        {
            var result = TargetInfo(tuner.gameObject);
            var future = tuner.GetFutureGeyser();
            var assigned = tuner.GetAssignedGeyser();
            result["futureGeyser"] = future != null ? GeyserInfo(future, tuner) : null;
            result["assignedGeyser"] = assigned != null ? GeyserInfo(assigned, tuner) : null;
            result["enhancementDuration"] = Math.Round(ToolUtil.SafeFloat(tuner.enhancementDuration), 2);
            result["hasSwitchChore"] = future != assigned;
            return result;
        }

        private static Dictionary<string, object> GeyserInfo(Geyser geyser, GeoTuner.Instance contextTuner)
        {
            int cell = Grid.PosToCell(geyser);
            int worldId = geyser.GetMyWorldId();
            int futureCount = Components.GeoTuners.GetItems(worldId).Count(tuner => tuner.GetFutureGeyser() == geyser);
            int assignedCount = Components.GeoTuners.GetItems(worldId).Count(tuner => tuner.GetAssignedGeyser() == geyser);
            int futureOrAssignedCount = Components.GeoTuners.GetItems(worldId).Count(tuner => tuner.GetFutureGeyser() == geyser || tuner.GetAssignedGeyser() == geyser);
            bool isCurrentFuture = contextTuner != null && contextTuner.GetFutureGeyser() == geyser;
            bool studied = IsStudied(geyser);
            bool visible = Grid.IsValidCell(cell) && Grid.Visible[cell] > 0;
            bool uncovered = geyser.GetComponent<Uncoverable>()?.IsUncovered ?? false;
            var kpid = geyser.GetComponent<KPrefabID>();
            var info = new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? geyser.gameObject.GetInstanceID(),
                ["prefabId"] = kpid?.PrefabTag.Name ?? geyser.gameObject.name,
                ["name"] = ToolUtil.CleanName(geyser.GetProperName()),
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["worldId"] = worldId,
                ["typeId"] = geyser.configuration.typeId.ToString(),
                ["element"] = geyser.configuration.geyserType.element.ToString(),
                ["temperatureK"] = Math.Round(ToolUtil.SafeFloat(geyser.configuration.geyserType.temperature), 2),
                ["studied"] = studied,
                ["visible"] = visible,
                ["uncovered"] = uncovered,
                ["futureGeoTuners"] = futureCount,
                ["assignedGeoTuners"] = assignedCount,
                ["futureOrAssignedGeoTuners"] = futureOrAssignedCount,
                ["selectedByContextTuner"] = isCurrentFuture,
                ["assignableBySideScreen"] = studied && (isCurrentFuture || futureOrAssignedCount < MaxTunersPerGeyser),
                ["blockedReason"] = studied ? (isCurrentFuture || futureOrAssignedCount < MaxTunersPerGeyser ? null : "max_geotuners") : "not_studied"
            };

            if (contextTuner != null && studied)
            {
                var settings = contextTuner.def.GetSettingsForGeyser(geyser);
                info["geotuneMaterial"] = settings.material.ProperName();
            }
            return info;
        }

        private static bool IsStudied(Geyser geyser)
        {
            return geyser.GetComponent<Studyable>()?.Studied ?? false;
        }

        private static int CountFutureOrAssigned(int worldId, Geyser geyser)
        {
            return Components.GeoTuners.GetItems(worldId)
                .Count(tuner => tuner.GetFutureGeyser() == geyser || tuner.GetAssignedGeyser() == geyser);
        }

        private static GeoTuner.Instance FindGeoTuner(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);

            foreach (var tuner in AllGeoTuners(worldId))
            {
                if (tuner == null)
                    continue;
                var go = tuner.gameObject;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return tuner;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return tuner;
            }
            return null;
        }

        private static Geyser FindGeyser(JObject args, int worldId)
        {
            int? geyserId = ToolUtil.GetInt(args, "geyserId");
            int? x = ToolUtil.GetInt(args, "geyserX");
            int? y = ToolUtil.GetInt(args, "geyserY");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;

            foreach (var geyser in Components.Geysers.GetItems(worldId))
            {
                if (geyser == null)
                    continue;
                var kpid = geyser.GetComponent<KPrefabID>();
                if (geyserId.HasValue && kpid != null && kpid.InstanceID == geyserId.Value)
                    return geyser;
                if (cell.HasValue && Grid.PosToCell(geyser) == cell.Value)
                    return geyser;
            }
            return null;
        }

        private static IEnumerable<GeoTuner.Instance> AllGeoTuners(int worldId)
        {
            if (worldId >= 0)
                return Components.GeoTuners.GetItems(worldId);
            return Components.GeoTuners.GetWorldsIds()
                .SelectMany(id => Components.GeoTuners.GetItems(id));
        }

        private static bool MatchesTarget(GameObject go, Dictionary<string, int> rect, int worldId)
        {
            if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                return false;
            int cell = Grid.PosToCell(go);
            return rect == null || CellInRect(cell, rect, worldId);
        }

        private static bool MatchesQuery(Dictionary<string, object> info, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            return JsonConvert.SerializeObject(info).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, object> TargetInfo(GameObject go)
        {
            int cell = Grid.PosToCell(go);
            var building = go.GetComponent<Building>();
            var kpid = go.GetComponent<KPrefabID>();
            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["prefabId"] = building?.Def?.PrefabID ?? kpid?.PrefabTag.Name ?? go.name,
                ["name"] = ToolUtil.CleanName(go.GetProperName()),
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["worldId"] = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : -1
            };
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象の GeoTuner の InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象の GeoTuner のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象の GeoTuner のセルの Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標で検索する場合の既定値は現在アクティブなワールド", Required = false }
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

        private static Dictionary<string, McpToolParameter> AreaLookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = RectParams(new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象の GeoTuner の InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象の GeoTuner のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象の GeoTuner のセルの Y", Required = false }
            });
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> GeoTunerControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list、list_geysers、assign のいずれか", Required = true, EnumValues = new List<string> { "list", "list_geysers", "assign" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list/list_geysers で、設備、間欠泉、種類により絞り込む", Required = false },
                ["includeUnstudied"] = new McpToolParameter { Type = "boolean", Description = "action=list_geysers で、表示されているが未分析の間欠泉を含めるか。既定値は true", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list/list_geysers で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["geyserId"] = new McpToolParameter { Type = "integer", Description = "action=assign の対象の間欠泉の InstanceID。clear=true の場合は無視する", Required = false },
                ["geyserX"] = new McpToolParameter { Type = "integer", Description = "action=assign の対象の間欠泉のセルの X。geyserId が空の場合は座標で指定できる", Required = false },
                ["geyserY"] = new McpToolParameter { Type = "integer", Description = "action=assign の対象の間欠泉のセルの Y。geyserId が空の場合は座標で指定できる", Required = false },
                ["clear"] = new McpToolParameter { Type = "boolean", Description = "action=assign で true を指定すると Nothing を選択し、今後の対象を解除する", Required = false },
                ["force"] = new McpToolParameter { Type = "boolean", Description = "action=assign で未分析と上限 5 台のチェックを省略する。既定値は false", Required = false }
            });
        }

        private static bool HasLookupInput(JObject args)
        {
            return ToolUtil.GetInt(args, "id").HasValue
                   || ToolUtil.GetInt(args, "x").HasValue
                   || ToolUtil.GetInt(args, "y").HasValue;
        }

        private static bool HasRectInput(JObject args)
        {
            return !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                   || ToolUtil.GetInt(args, "x1").HasValue
                   || ToolUtil.GetInt(args, "y1").HasValue
                   || ToolUtil.GetInt(args, "x2").HasValue
                   || ToolUtil.GetInt(args, "y2").HasValue;
        }

        private static bool CellInRect(int cell, Dictionary<string, int> rect, int worldId)
        {
            return Grid.IsValidCell(cell)
                   && ToolUtil.CellMatchesWorld(cell, worldId)
                   && Grid.CellColumn(cell) >= rect["x1"]
                   && Grid.CellColumn(cell) <= rect["x2"]
                   && Grid.CellRow(cell) >= rect["y1"]
                   && Grid.CellRow(cell) <= rect["y2"];
        }
    }
}
