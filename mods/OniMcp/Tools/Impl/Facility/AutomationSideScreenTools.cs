using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class AutomationSideScreenTools
    {
        public static McpTool ListAutomatableControls()
        {
            return new McpTool
            {
                Name = "automatable_controls_list",
                Group = "automation",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "automatable_manual_controls_list", "automation_manual_allowed_list" },
                Tags = new List<string> { "automation", "side-screen", "automatable", "manual", "storage" },
                Description = "互換用の入口。building_control domain=side_surface surface=automation kind=automatable action=list を優先して使う。AutomatableSideScreen の操作項目を一覧表示し、設備が自動化による運搬のみを許可しているか、複製人間の手動運搬を許可しているかを示す",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名や prefabId で絞り込む", Required = false },
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
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var controls = Components.BuildingCompletes.Items
                        .Select(item => item?.gameObject)
                        .Where(go => MatchesTarget(go, rect, worldId))
                        .Where(go => go.GetComponent<Automatable>() != null)
                        .Select(AutomatableInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = controls.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["controls"] = controls
                    });
                }
            };
        }

        public static McpTool SetAutomatableControl()
        {
            return new McpTool
            {
                Name = "automatable_control_set",
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "automatable_manual_control_set", "automation_manual_allowed_set" },
                Tags = new List<string> { "automation", "side-screen", "automatable", "manual", "storage" },
                Description = "互換用の入口。building_control domain=side_surface surface=automation kind=automatable action=set を優先して使う。AutomatableSideScreen の手動運搬の許可を設定する。複製人間が手動運搬できるかに影響するため、confirm=true が必要",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["allowManual"] = new McpToolParameter { Type = "boolean", Description = "true で複製人間の手動運搬を許可し、false で自動化による運搬のみを許可する", Required = false },
                    ["automationOnly"] = new McpToolParameter { Type = "boolean", Description = "Automatable.GetAutomationOnly を直接設定する。allowManual とのどちらかを指定する", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "運搬の制約変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for Automatable changes");

                    var go = FindTarget(args);
                    if (go == null)
                        return CallToolResult.Error("Target Automatable not found");

                    var automatable = go.GetComponent<Automatable>();
                    var before = AutomatableInfo(go);
                    ApplyAutomatable(automatable, args);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["automatable"] = AutomatableInfo(go)
                    });
                }
            };
        }

        public static McpTool BatchSetAutomatableControls()
        {
            return new McpTool
            {
                Name = "automatable_controls_batch_set",
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "automatable_manual_controls_batch_set" },
                Tags = new List<string> { "automation", "side-screen", "automatable", "batch" },
                Description = "互換用の入口。building_control domain=side_surface surface=automation kind=automatable action=batch を優先して使う。AutomatableSideScreen の手動運搬の許可を一括設定する。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "配列。各項目は id または x/y/worldId を指定でき、allowManual または automationOnly を渡す", Required = true },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "各項目へ統合する既定の引数。個別項目の引数を優先する。allowManual/automationOnly/worldId の共有に適している", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "運搬の制約の一括変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for Automatable batch changes");

                    var items = args["items"] as JArray;
                    if (items == null || items.Count == 0)
                        return CallToolResult.Error("items array is required");

                    var defaults = args["defaults"] as JObject ?? args["defaultArguments"] as JObject;
                    var results = new List<Dictionary<string, object>>();
                    foreach (var token in items)
                    {
                        var rawItem = token as JObject;
                        if (rawItem == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "item must be an object" });
                            continue;
                        }
                        var item = MergeDefaults(rawItem, defaults);

                        var go = FindTarget(item);
                        if (go == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "Target Automatable not found", ["input"] = item });
                            continue;
                        }

                        var before = AutomatableInfo(go);
                        ApplyAutomatable(go.GetComponent<Automatable>(), item);
                        results.Add(new Dictionary<string, object>
                        {
                            ["ok"] = true,
                            ["target"] = TargetInfo(go),
                            ["before"] = before,
                            ["automatable"] = AutomatableInfo(go)
                        });
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["requested"] = items.Count,
                        ["succeeded"] = results.Count(item => (bool)item["ok"]),
                        ["failed"] = results.Count(item => !(bool)item["ok"]),
                        ["results"] = results
                    });
                }
            };
        }

        public static McpTool ListCritterSensors()
        {
            return new McpTool
            {
                Name = "critter_sensors_list",
                Group = "automation",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "critter_count_sensors_list", "logic_critter_sensors_list" },
                Tags = new List<string> { "automation", "sensor", "critter", "egg", "side-screen" },
                Description = "互換用の入口。building_control domain=side_surface surface=automation kind=critter_sensor action=list を優先して使う。CritterSensorSideScreen / LogicCritterCountSensor の動物・卵の計数のオン・オフ、しきい値、現在の数を一覧表示する",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名や prefabId で絞り込む", Required = false },
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
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var sensors = Components.BuildingCompletes.Items
                        .Select(item => item?.gameObject)
                        .Where(go => MatchesTarget(go, rect, worldId))
                        .Where(go => go.GetComponent<LogicCritterCountSensor>() != null)
                        .Select(CritterSensorInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = sensors.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["sensors"] = sensors
                    });
                }
            };
        }

        public static McpTool SetCritterSensorCounting()
        {
            return new McpTool
            {
                Name = "critter_sensor_counting_set",
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "critter_count_sensor_set", "logic_critter_sensor_set" },
                Tags = new List<string> { "automation", "sensor", "critter", "egg", "side-screen" },
                Description = "互換用の入口。building_control domain=side_surface surface=automation kind=critter_sensor action=set を優先して使う。CritterSensorSideScreen の Count Critters / Count Eggs のオン・オフを設定する。しきい値の設定には引き続き building_control domain=config action=set_threshold を使う。confirm=true が必要",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["countCritters"] = new McpToolParameter { Type = "boolean", Description = "センサーの現在値に動物を含めるか。未指定なら変更しない", Required = false },
                    ["countEggs"] = new McpToolParameter { Type = "boolean", Description = "センサーの現在値に卵を含めるか。未指定なら変更しない", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "センサーの計数対象の変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for critter sensor changes");

                    var go = FindTarget(args);
                    if (go == null)
                        return CallToolResult.Error("Target LogicCritterCountSensor not found");

                    var sensor = go.GetComponent<LogicCritterCountSensor>();
                    var before = CritterSensorInfo(go);
                    ApplyCritterSensor(sensor, args);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["sensor"] = CritterSensorInfo(go)
                    });
                }
            };
        }

        public static McpTool BatchSetCritterSensors()
        {
            return new McpTool
            {
                Name = "critter_sensors_batch_set",
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "critter_count_sensors_batch_set" },
                Tags = new List<string> { "automation", "sensor", "critter", "egg", "batch" },
                Description = "互換用の入口。building_control domain=side_surface surface=automation kind=critter_sensor action=batch を優先して使う。CritterSensorSideScreen の Count Critters / Count Eggs のオン・オフを一括設定する。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "配列。各項目は id または x/y/worldId を指定でき、countCritters/countEggs も指定可能", Required = true },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "各項目へ統合する既定の引数。個別項目の引数を優先する。countCritters/countEggs/worldId の共有に適している", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "センサーの計数対象の一括変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for critter sensor batch changes");

                    var items = args["items"] as JArray;
                    if (items == null || items.Count == 0)
                        return CallToolResult.Error("items array is required");

                    var defaults = args["defaults"] as JObject ?? args["defaultArguments"] as JObject;
                    var results = new List<Dictionary<string, object>>();
                    foreach (var token in items)
                    {
                        var rawItem = token as JObject;
                        if (rawItem == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "item must be an object" });
                            continue;
                        }
                        var item = MergeDefaults(rawItem, defaults);

                        var go = FindTarget(item);
                        if (go == null)
                        {
                            results.Add(new Dictionary<string, object> { ["ok"] = false, ["error"] = "Target LogicCritterCountSensor not found", ["input"] = item });
                            continue;
                        }

                        var before = CritterSensorInfo(go);
                        ApplyCritterSensor(go.GetComponent<LogicCritterCountSensor>(), item);
                        results.Add(new Dictionary<string, object>
                        {
                            ["ok"] = true,
                            ["target"] = TargetInfo(go),
                            ["before"] = before,
                            ["sensor"] = CritterSensorInfo(go)
                        });
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["requested"] = items.Count,
                        ["succeeded"] = results.Count(item => (bool)item["ok"]),
                        ["failed"] = results.Count(item => !(bool)item["ok"]),
                        ["results"] = results
                    });
                }
            };
        }

    }
}
