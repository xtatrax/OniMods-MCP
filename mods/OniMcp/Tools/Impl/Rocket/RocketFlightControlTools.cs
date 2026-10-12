using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class RocketTools
    {
        public static McpTool ControlRocketFlight()
        {
            return new McpTool
            {
                Name = "rocket_flight_control",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "rocket_destination_side_control", "rocket_landing_control" },
                Tags = new List<string> { "rocket", "destination", "landing", "round-trip", "side-screen" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=set_round_trip/set_landing_pad を使用すること",
                Hidden = true,
                Parameters = RocketLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["operation"] = new McpToolParameter { Type = "string", Description = "set_round_trip または set_landing_pad", Required = true, EnumValues = new List<string> { "set_round_trip", "set_landing_pad" } },
                    ["repeat"] = new McpToolParameter { Type = "boolean", Description = "operation=set_round_trip の場合: true=往復、false=片道", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "operation=set_landing_pad の場合: land または cancel", Required = false, EnumValues = new List<string> { "land", "cancel" } },
                    ["padId"] = new McpToolParameter { Type = "integer", Description = "operation=set_landing_pad action=land の場合の、対象の LaunchPad の InstanceID", Required = false },
                    ["padX"] = new McpToolParameter { Type = "integer", Description = "operation=set_landing_pad action=land の場合の、対象の LaunchPad のセルの X", Required = false },
                    ["padY"] = new McpToolParameter { Type = "integer", Description = "operation=set_landing_pad action=land の場合の、対象の LaunchPad のセルの Y", Required = false },
                    ["padWorldId"] = new McpToolParameter { Type = "integer", Description = "operation=set_landing_pad で発射台を座標から探す際のワールド ID", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    string operation = (args["operation"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (operation)
                    {
                        case "set_round_trip":
                        case "round_trip":
                            return SetRocketRoundTrip().Handler(args);
                        case "set_landing_pad":
                        case "landing_pad":
                            return SetRocketLandingPad().Handler(args);
                        default:
                            return CallToolResult.Error("operation must be set_round_trip or set_landing_pad");
                    }
                }
            };
        }

        public static McpTool SetRocketLandingPad()
        {
            return new McpTool
            {
                Name = "rocket_landing_pad_set",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "rocket_land_at_pad", "rocket_landing_cancel" },
                Tags = new List<string> { "rocket", "launch-pad", "landing", "side-screen" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=set_landing_pad landingAction=land/cancel を使用すること",
                Hidden = true,
                Parameters = RocketLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "land または cancel", Required = true, EnumValues = new List<string> { "land", "cancel" } },
                    ["padId"] = new McpToolParameter { Type = "integer", Description = "対象の LaunchPad の InstanceID。action=land で使用できる", Required = false },
                    ["padX"] = new McpToolParameter { Type = "integer", Description = "対象の LaunchPad のセルの X。action=land で使用できる", Required = false },
                    ["padY"] = new McpToolParameter { Type = "integer", Description = "対象の LaunchPad のセルの Y。action=land で使用できる", Required = false },
                    ["padWorldId"] = new McpToolParameter { Type = "integer", Description = "対象の LaunchPad のワールド ID。座標から検索する場合に使用できる", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    var craft = FindClustercraft(args);
                    if (craft == null)
                        return CallToolResult.Error("Rocket not found");
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    var selector = craft.ModuleInterface?.GetClusterDestinationSelector();
                    if (selector == null)
                        return CallToolResult.Error("Rocket does not expose a cluster destination selector");

                    var before = ClustercraftToDictionary(craft, includeDetail: true);
                    if (action == "cancel")
                    {
                        selector.SetDestination(craft.Location);
                    }
                    else if (action == "land")
                    {
                        var pad = FindLaunchPad(args);
                        if (pad == null)
                            return CallToolResult.Error("Target LaunchPad not found");
                        string failReason;
                        if (craft.CanLandAtPad(pad, out failReason) == Clustercraft.PadLandingStatus.CanNeverLand)
                            return CallToolResult.Error(failReason);
                        craft.LandAtPad(pad);
                    }
                    else
                    {
                        return CallToolResult.Error("action must be land or cancel");
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["before"] = before,
                        ["rocket"] = ClustercraftToDictionary(craft, includeDetail: true)
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetRocketDestination()
        {
            return new McpTool
            {
                Name = "rockets_set_destination",
                Group = "rockets",
                Mode = "execute",
                Risk = "medium",
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=set_destination を使用すること",
                Hidden = true,
                Parameters = RocketLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["q"] = new McpToolParameter { Type = "integer", Description = "目的地の星図の軸座標 q。worldId を使う場合は省略できる", Required = false },
                    ["r"] = new McpToolParameter { Type = "integer", Description = "目的地の星図の軸座標 r。worldId を使う場合は省略できる", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "目的地の小惑星のワールド ID。指定すると星図上の位置を自動で解決する", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "CanTravelToCell の事前チェックを省略する。既定値は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "目的地の変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var craft = FindClustercraft(args);
                    if (craft == null)
                        return CallToolResult.Error("Rocket not found");

                    AxialI destination;
                    string destinationSource;
                    if (!TryResolveDestination(args, out destination, out destinationSource))
                        return CallToolResult.Error("q/r or worldId is required");

                    var moduleInterface = craft.ModuleInterface;
                    if (moduleInterface == null || !moduleInterface.HasClusterDestinationSelector())
                        return CallToolResult.Error("Rocket has no cluster destination selector");

                    bool force = ToolUtil.GetBool(args, "force", false);
                    if (!force && !CanTravelToCell(craft, destination))
                        return CallToolResult.Error($"Rocket cannot travel to q={destination.Q}, r={destination.R}; pass force=true only if the game UI can handle this destination.");

                    var selector = moduleInterface.GetClusterDestinationSelector();
                    if (selector == null)
                        return CallToolResult.Error("Rocket destination selector is not available");

                    selector.SetDestination(destination);
                    var traveler = craft.GetComponent<ClusterTraveler>();
                    MarkPathDirty(traveler);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["rocket"] = ClustercraftToDictionary(craft),
                        ["destination"] = AxialToDictionary(destination),
                        ["destinationSource"] = destinationSource,
                        ["forced"] = force
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlRocketOps()
        {
            return new McpTool
            {
                Name = "rocket_ops_control",
                Group = "rockets",
                Mode = "execute",
                Risk = "medium",
                Aliases = new List<string> { "rocket_control", "rocket_operation_control", "rocket_launch_control" },
                Tags = new List<string> { "rocket", "destination", "launch", "landing", "round-trip", "side-screen" },
                Description = "ロケット操作の統合ツール。action=list/status/detail/list_destinations/list_launch_pads/set_destination/request_launch/cancel_launch/set_round_trip/set_landing_pad。",
                Parameters = RocketLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、status、detail、list_destinations、list_launch_pads、set_destination、request_launch、cancel_launch、set_round_trip、set_landing_pad", Required = true, EnumValues = new List<string> { "list", "status", "detail", "list_destinations", "list_launch_pads", "set_destination", "request_launch", "cancel_launch", "set_round_trip", "set_landing_pad" } },
                    ["includeDetail"] = new McpToolParameter { Type = "boolean", Description = "action=status で各ロケットの詳細なモジュール情報を含めるか。既定値は false", Required = false },
                    ["includeBaseGame"] = new McpToolParameter { Type = "boolean", Description = "action=list/status で基本版の宇宙船を含めるか。既定値は true", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "action=list_destinations で星図に表示されているエンティティだけを返すか。既定値は true", Required = false },
                    ["layer"] = new McpToolParameter { Type = "string", Description = "action=list_destinations で星図のレイヤーにより絞り込む。例: Asteroid、Craft、POI", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list_launch_pads で発射台名、prefabId、ワールド名、着陸待ちのロケット名により絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list_destinations/list_launch_pads で返す件数の上限", Required = false },
                    ["q"] = new McpToolParameter { Type = "integer", Description = "action=set_destination の目的地の星図の軸座標 q。worldId を使う場合は省略できる", Required = false },
                    ["r"] = new McpToolParameter { Type = "integer", Description = "action=set_destination の目的地の星図の軸座標 r。worldId を使う場合は省略できる", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "action=set_destination の目的地の小惑星のワールド ID。指定すると星図上の位置を自動で解決する", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "action=set_destination で CanTravelToCell の事前チェックを省略するか。既定値は false", Required = false },
                    ["automated"] = new McpToolParameter { Type = "boolean", Description = "action=request_launch で自動発射の要求として扱うか。既定値は false", Required = false },
                    ["repeat"] = new McpToolParameter { Type = "boolean", Description = "action=set_round_trip では true=往復、false=片道", Required = false },
                    ["landingAction"] = new McpToolParameter { Type = "string", Description = "action=set_landing_pad の場合: land または cancel", Required = false, EnumValues = new List<string> { "land", "cancel" } },
                    ["padId"] = new McpToolParameter { Type = "integer", Description = "action=set_landing_pad landingAction=land の場合の、対象の LaunchPad の InstanceID", Required = false },
                    ["padX"] = new McpToolParameter { Type = "integer", Description = "action=set_landing_pad landingAction=land の場合の、対象の LaunchPad のセルの X", Required = false },
                    ["padY"] = new McpToolParameter { Type = "integer", Description = "action=set_landing_pad landingAction=land の場合の、対象の LaunchPad のセルの Y", Required = false },
                    ["padWorldId"] = new McpToolParameter { Type = "integer", Description = "action=set_landing_pad で発射台を座標から探す際のワールド ID", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "変更の確認。書き込み操作では true が必須", Required = true }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? args["operation"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                            return ListRockets().Handler(args);
                        case "status":
                            return GetRocketStatus().Handler(args);
                        case "detail":
                            return GetRocketDetail().Handler(args);
                        case "list_destinations":
                        case "destinations":
                            return ListSpaceDestinations().Handler(args);
                        case "list_launch_pads":
                        case "launch_pads":
                        case "pads":
                            return ListLaunchPads().Handler(args);
                        case "set_destination":
                        case "destination":
                            return SetRocketDestination().Handler(args);
                        case "request_launch":
                        case "launch":
                            return RequestRocketLaunch().Handler(args);
                        case "cancel_launch":
                        case "cancel":
                            return CancelRocketLaunch().Handler(args);
                        case "set_round_trip":
                        case "round_trip":
                            return SetRocketRoundTrip().Handler(args);
                        case "set_landing_pad":
                        case "landing_pad":
                            var forwarded = (JObject)args.DeepClone();
                            forwarded["action"] = args["landingAction"] ?? args["landing_action"] ?? args["padAction"] ?? args["pad_action"];
                            return SetRocketLandingPad().Handler(forwarded);
                        default:
                            return CallToolResult.Error("action must be list, status, detail, list_destinations, list_launch_pads, set_destination, request_launch, cancel_launch, set_round_trip, or set_landing_pad");
                    }
                }
            };
        }

        public static McpTool RequestRocketLaunch()
        {
            return new McpTool
            {
                Name = "rockets_request_launch",
                Group = "rockets",
                Mode = "execute",
                Risk = "medium",
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=request_launch を使用すること",
                Hidden = true,
                Parameters = RocketLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["automated"] = new McpToolParameter { Type = "boolean", Description = "自動発射の要求として扱うか。既定値は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "発射要求を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var craft = FindClustercraft(args);
                    if (craft == null)
                        return CallToolResult.Error("Rocket not found");

                    if (!craft.CheckReadyToLaunch())
                        return CallToolResult.Error("Rocket is not ready to launch");

                    bool automated = ToolUtil.GetBool(args, "automated", false);
                    craft.RequestLaunch(automated);
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["requested"] = true,
                        ["automated"] = automated,
                        ["rocket"] = ClustercraftToDictionary(craft)
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool CancelRocketLaunch()
        {
            return new McpTool
            {
                Name = "rockets_cancel_launch",
                Group = "rockets",
                Mode = "execute",
                Risk = "medium",
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=cancel_launch を使用すること",
                Hidden = true,
                Parameters = RocketLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "発射の取り消しを確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var craft = FindClustercraft(args);
                    if (craft == null)
                        return CallToolResult.Error("Rocket not found");

                    craft.CancelLaunch();
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["cancelled"] = true,
                        ["rocket"] = ClustercraftToDictionary(craft)
                    }, McpJsonUtil.Settings));
                }
            };
        }

    }
}
