using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class RocketFlightUtilityTools
    {
        public static McpTool ListFlightUtilities()
        {
            return new McpTool
            {
                Name = "rocket_flight_utilities_list",
                Hidden = true,
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "rocket_emptyable_cargo_list", "module_flight_utility_list" },
                Tags = new List<string> { "rocket", "module", "cargo", "deploy", "jettison", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=rocket rocketDomain=flight_utility action=list を使用すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["rocketId"] = new McpToolParameter { Type = "integer", Description = "任意のロケットの InstanceID", Required = false },
                    ["rocketName"] = new McpToolParameter { Type = "string", Description = "任意のロケット名", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "ロケット、モジュール、prefabId、ボタン、対象で絞り込む", Required = false },
                    ["includeDupes"] = new McpToolParameter { Type = "boolean", Description = "選択可能な複製人間を返すか。既定値は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    var rocket = FindRocket(args);
                    string query = args["query"]?.ToString();
                    bool includeDupes = ToolUtil.GetBool(args, "includeDupes", true);
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var modules = EnumerateUtilityModules(rocket)
                        .Select(item => UtilityInfo(item.craft, item.module, includeDupes))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["rocketName"].ToString())
                        .ThenBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = modules.Count,
                        ["modules"] = modules
                    });
                }
            };
        }

        public static McpTool ControlFlightUtility()
        {
            return new McpTool
            {
                Name = "rocket_flight_utility_control",
                Group = "rockets",
                Mode = "write",
                Risk = "high",
                Aliases = new List<string> { "rocket_emptyable_cargo_control", "module_flight_utility_control" },
                Tags = new List<string> { "rocket", "module", "cargo", "deploy", "jettison", "side-screen" },
                Description = "ModuleFlightUtilitySideScreen の集約ツール。action=list で照会し、empty、set_auto_deploy、set_target、clear_target、choose_duplicant、clear_duplicant で操作する。empty と対象の変更は confirm=true が必要",
                Parameters = UtilityLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、empty、set_auto_deploy、set_target、clear_target、choose_duplicant、clear_duplicant", Required = true, EnumValues = new List<string> { "list", "empty", "set_auto_deploy", "set_target", "clear_target", "choose_duplicant", "clear_duplicant" } },
                    ["autoDeploy"] = new McpToolParameter { Type = "boolean", Description = "action=set_auto_deploy で設定する値", Required = false },
                    ["q"] = new McpToolParameter { Type = "integer", Description = "action=set_target の星図の q 座標", Required = false },
                    ["r"] = new McpToolParameter { Type = "integer", Description = "action=set_target の星図の r 座標", Required = false },
                    ["dupeId"] = new McpToolParameter { Type = "integer", Description = "action=choose_duplicant の複製人間の InstanceID", Required = false },
                    ["dupeName"] = new McpToolParameter { Type = "string", Description = "action=choose_duplicant の複製人間の名前", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "empty、set_target、clear_target では true が必須", Required = false }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListFlightUtilities().Handler(args);

                    var found = FindUtilityModule(args);
                    if (found.module == null)
                        return CallToolResult.Error("Target IEmptyableCargo module not found");

                    var before = UtilityInfo(found.craft, found.module, includeDupes: false);
                    bool confirm = ToolUtil.GetBool(args, "confirm", false);

                    if (action == "empty")
                    {
                        if (!confirm)
                            return CallToolResult.Error("confirm=true is required to empty/deploy module cargo");
                        if (!found.module.CanEmptyCargo())
                            return CallToolResult.Error("Module cannot empty cargo right now");
                        found.module.EmptyCargo();
                    }
                    else if (action == "set_auto_deploy")
                    {
                        if (!found.module.CanAutoDeploy)
                            return CallToolResult.Error("Module does not support auto deploy");
                        found.module.AutoDeploy = ToolUtil.GetBool(args, "autoDeploy", found.module.AutoDeploy);
                    }
                    else if (action == "set_target")
                    {
                        if (!confirm)
                            return CallToolResult.Error("confirm=true is required to change module target");
                        var selector = found.module.master.GetComponent<EntityClusterDestinationSelector>();
                        if (!found.module.CanTargetClusterGridEntities || selector == null)
                            return CallToolResult.Error("Module does not support cluster entity targets");
                        int? q = ToolUtil.GetInt(args, "q");
                        int? r = ToolUtil.GetInt(args, "r");
                        if (!q.HasValue || !r.HasValue)
                            return CallToolResult.Error("q and r are required for set_target");
                        selector.SetDestination(new AxialI(q.Value, r.Value));
                    }
                    else if (action == "clear_target")
                    {
                        if (!confirm)
                            return CallToolResult.Error("confirm=true is required to clear module target");
                        var selector = found.module.master.GetComponent<EntityClusterDestinationSelector>();
                        if (!found.module.CanTargetClusterGridEntities || selector == null)
                            return CallToolResult.Error("Module does not support cluster entity targets");
                        selector.SetDestination(AxialI.INVALID);
                    }
                    else if (action == "choose_duplicant")
                    {
                        if (!found.module.ChooseDuplicant)
                            return CallToolResult.Error("Module does not support duplicant selection");
                        var dupe = FindDupeForUtility(args);
                        if (dupe == null)
                            return CallToolResult.Error("dupeId or dupeName must match a live duplicant");
                        if (!CanChooseDuplicant(found.craft, found.module, dupe))
                            return CallToolResult.Error("Duplicant is already chosen by another utility module on this rocket");
                        found.module.ChosenDuplicant = dupe;
                    }
                    else if (action == "clear_duplicant")
                    {
                        if (!found.module.ChooseDuplicant)
                            return CallToolResult.Error("Module does not support duplicant selection");
                        found.module.ChosenDuplicant = null;
                    }
                    else
                    {
                        return CallToolResult.Error("Unsupported action");
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = ModuleTargetInfo(found.module),
                        ["action"] = action,
                        ["before"] = before,
                        ["module"] = UtilityInfo(found.craft, found.module, includeDupes: false)
                    });
                }
            };
        }

        public static McpTool ListRocketRestrictions()
        {
            return new McpTool
            {
                Name = "rocket_restrictions_list",
                Hidden = true,
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "rocket_control_station_restrictions_list" },
                Tags = new List<string> { "rocket", "control-station", "restriction", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=rocket rocketDomain=restriction action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備、prefabId、ロケット名で絞り込む", Required = false },
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

                    var stations = Components.RocketControlStations.Items
                        .Where(station => MatchesTarget(station?.gameObject, rect, worldId))
                        .Select(RestrictionInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = stations.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["stations"] = stations
                    });
                }
            };
        }

        public static McpTool SetRocketRestriction()
        {
            return new McpTool
            {
                Name = "rocket_restriction_set",
                Hidden = true,
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "rocket_control_station_restriction_set" },
                Tags = new List<string> { "rocket", "control-station", "restriction", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=rocket rocketDomain=restriction action=set を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["mode"] = new McpToolParameter { Type = "string", Description = "none または space", Required = true, EnumValues = new List<string> { "none", "space" } },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "自動化信号に接続されていても、保存値を強制的に書き込むか。既定値は false", Required = false }
                }),
                Handler = args =>
                {
                    var station = FindRocketControlStation(args);
                    if (station == null)
                        return CallToolResult.Error("Target RocketControlStation not found");
                    if (station.IsLogicInputConnected() && !ToolUtil.GetBool(args, "force", false))
                        return CallToolResult.Error("Rocket restriction is automation-controlled; pass force=true only to update saved manual preference");

                    string mode = (args["mode"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    var before = RestrictionInfo(station);
                    if (mode == "none")
                        station.RestrictWhenGrounded = false;
                    else if (mode == "space")
                        station.RestrictWhenGrounded = true;
                    else
                        return CallToolResult.Error("mode must be none or space");

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(station.gameObject),
                        ["before"] = before,
                        ["station"] = RestrictionInfo(station)
                    });
                }
            };
        }

        public static McpTool ControlRocketRestriction()
        {
            return new McpTool
            {
                Name = "rocket_restriction_control",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "rocket_control_station_restriction_control" },
                Tags = new List<string> { "rocket", "control-station", "restriction", "side-screen" },
                Description = "ロケット制御装置の制限の集約ツール。action=list で地上・宇宙での使用制限を照会し、action=set で none/space を設定する",
                Parameters = RocketRestrictionControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListRocketRestrictions().Handler(args);
                    if (action == "set")
                        return SetRocketRestriction().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

    }
}
