using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static class LightTools
    {
        public static McpTool ControlLight()
        {
            return new McpTool
            {
                Name = "light_control",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "lights_control", "light_color_control" },
                Tags = new List<string> { "lights", "color", "user-menu", "buildings" },
                Description = "照明色の統合ツール: action=list で照明と色プリセットを照会。action=set_color で LightColorMenu のプリセット色を選択",
                Parameters = LightControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListLights().Handler(args);
                    if (action == "set_color" || action == "set")
                        return SetLightColor().Handler(args);
                    return CallToolResult.Error("action must be list or set_color");
                }
            };
        }

        public static McpTool ListLights()
        {
            return new McpTool
            {
                Name = "lights_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "light_color_controls_list" },
                Tags = new List<string> { "lights", "color", "user-menu", "buildings" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=light action=list を使用してください",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、色名で絞り込み", Required = false },
                    ["configurableOnly"] = new McpToolParameter { Type = "boolean", Description = "LightColorMenu を持つ照明のみを返すか。既定値は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す最大件数。既定は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    bool configurableOnly = ToolUtil.GetBool(args, "configurableOnly", true);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var lights = Components.BuildingCompletes.Items
                        .Select(building => building?.gameObject)
                        .Where(go => MatchesTarget(go, rect, worldId))
                        .Where(go => go.GetComponent<LightColorMenu>() != null || (!configurableOnly && go.GetComponentsInChildren<Light2D>(true).Length > 0))
                        .Select(LightInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = lights.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["configurableOnly"] = configurableOnly,
                        ["lights"] = lights
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetLightColor()
        {
            return new McpTool
            {
                Name = "lights_color_set",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "light_color_set" },
                Tags = new List<string> { "lights", "color", "user-menu", "buildings" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=light action=set_color を使用してください",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["colorIndex"] = new McpToolParameter { Type = "integer", Description = "色プリセットのインデックス。building_control domain=special kind=light action=list で事前に照会可能", Required = false },
                    ["colorName"] = new McpToolParameter { Type = "string", Description = "色プリセット名。colorIndex が空の場合に使用", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindTarget(args);
                    if (go == null)
                        return CallToolResult.Error("Target not found");
                    var menu = go.GetComponent<LightColorMenu>();
                    if (menu == null || menu.lightColors == null || menu.lightColors.Length == 0)
                        return CallToolResult.Error("Target does not expose LightColorMenu presets");

                    int? requestedIndex = ToolUtil.GetInt(args, "colorIndex");
                    int index = requestedIndex ?? FindColorIndex(menu, args["colorName"]?.ToString());
                    if (index < 0 || index >= menu.lightColors.Length)
                        return CallToolResult.Error("colorIndex/colorName does not match any preset");

                    int before = GetCurrentColor(menu);
                    var method = OniReflection.GetMethodSafe(typeof(LightColorMenu), "SetColor", false, new[] { typeof(int) });
                    if (method == null)
                        return CallToolResult.Error("LightColorMenu.SetColor not found");
                    method.Invoke(menu, new object[] { index });

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["selected"] = ColorInfo(menu.lightColors[index], index),
                        ["currentIndex"] = GetCurrentColor(menu),
                        ["changed"] = before != GetCurrentColor(menu)
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static Dictionary<string, object> LightInfo(GameObject go)
        {
            var result = TargetInfo(go);
            var menu = go.GetComponent<LightColorMenu>();
            result["hasColorMenu"] = menu != null;
            result["currentColorIndex"] = menu == null ? -1 : GetCurrentColor(menu);
            result["colorOptions"] = menu == null || menu.lightColors == null
                ? new List<Dictionary<string, object>>()
                : menu.lightColors.Select(ColorInfo).ToList();
            result["lights"] = go.GetComponentsInChildren<Light2D>(true)
                .Select(light => new Dictionary<string, object>
                {
                    ["color"] = ColorToDictionary(light.Color),
                    ["lux"] = light.Lux,
                    ["range"] = ToolUtil.SafeFloat(light.Range),
                    ["enabled"] = light.enabled
                })
                .ToList();
            return result;
        }

        private static Dictionary<string, object> ColorInfo(LightColorMenu.LightColor color, int index)
        {
            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["name"] = color.name,
                ["color"] = ColorToDictionary(color.color)
            };
        }

        private static Dictionary<string, object> ColorToDictionary(Color color)
        {
            return new Dictionary<string, object>
            {
                ["r"] = Math.Round(color.r, 4),
                ["g"] = Math.Round(color.g, 4),
                ["b"] = Math.Round(color.b, 4),
                ["a"] = Math.Round(color.a, 4)
            };
        }

        private static int GetCurrentColor(LightColorMenu menu)
        {
            var field = OniReflection.GetFieldSafe(typeof(LightColorMenu), "currentColor", false);
            if (field == null)
                return -1;
            return (int)field.GetValue(menu);
        }

        private static int FindColorIndex(LightColorMenu menu, string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName))
                return -1;
            string name = colorName.Trim();
            for (int i = 0; i < menu.lightColors.Length; i++)
            {
                if (string.Equals(menu.lightColors[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static bool MatchesTarget(GameObject go, Dictionary<string, int> rect, int worldId)
        {
            if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                return false;
            int cell = Grid.PosToCell(go);
            return rect == null || CellInRect(cell, rect, worldId);
        }

        private static GameObject FindTarget(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);

            foreach (var building in Components.BuildingCompletes.Items)
            {
                var go = building?.gameObject;
                if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return go;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return go;
            }
            return null;
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
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象の照明設備の InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標で検索する場合の既定は現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。指定時は x1/y1/x2/y2 を省略可能", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の左下/始点 X。areaId を使う場合は省略可能", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の左下/始点 Y。areaId を使う場合は省略可能", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の右上/終点 X。areaId を使う場合は省略可能", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の右上/終点 Y。areaId を使う場合は省略可能", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定は areaId に紐付くワールド、または現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> LightControlParams()
        {
            var parameters = RectParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set_color", Required = true, EnumValues = new List<string> { "list", "set_color" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list の場合、設備名、prefabId、色名で絞り込み", Required = false },
                ["configurableOnly"] = new McpToolParameter { Type = "boolean", Description = "action=list の場合、LightColorMenu を持つ照明のみを返すか。既定値は true", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の最大返却数。既定値は 100、上限は 500", Required = false },
                ["id"] = new McpToolParameter { Type = "integer", Description = "action=set_color の場合の対象の照明設備の InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "action=set_color の場合の対象セルの X 座標", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "action=set_color の場合の対象セルの Y 座標", Required = false },
                ["colorIndex"] = new McpToolParameter { Type = "integer", Description = "action=set_color の場合の色プリセットのインデックス。action=list で事前に照会可能", Required = false },
                ["colorName"] = new McpToolParameter { Type = "string", Description = "action=set_color の場合の色プリセット名。colorIndex が空の場合に使用", Required = false }
            });
            return parameters;
        }

        private static bool HasRectInput(JObject args)
        {
            return !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                || (args["x1"] != null && args["y1"] != null && args["x2"] != null && args["y2"] != null);
        }

        private static bool CellInRect(int cell, Dictionary<string, int> rect, int worldId)
        {
            if (!Grid.IsValidCell(cell)) return false;
            if (!ToolUtil.CellMatchesWorld(cell, worldId)) return false;
            int x = Grid.CellColumn(cell);
            int y = Grid.CellRow(cell);
            return x >= rect["x1"] && x <= rect["x2"] && y >= rect["y1"] && y <= rect["y2"];
        }
    }
}
