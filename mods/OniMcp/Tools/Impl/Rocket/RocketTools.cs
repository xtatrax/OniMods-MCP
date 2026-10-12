using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class RocketTools
    {
        private static readonly FieldInfo AsteroidWorldContainerField =
            OniReflection.GetFieldSafe(typeof(AsteroidGridEntity), "m_worldContainer", false);
        private static readonly MethodInfo CanTravelToCellMethod =
            OniReflection.GetMethodSafe(typeof(Clustercraft), "CanTravelToCell", false, new[] { typeof(AxialI) });
        private static readonly MethodInfo MarkPathDirtyMethod =
            OniReflection.GetMethodSafe(typeof(ClusterTraveler), "MarkPathDirty", false, Type.EmptyTypes);

        public static McpTool ListRockets()
        {
            return new McpTool
            {
                Name = "rockets_list",
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "spacecraft_list", "rockets_discover", "spacecraft_discover" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=list を使用すること",
                Hidden = true,
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["includeBaseGame"] = new McpToolParameter { Type = "boolean", Description = "基本版の SpacecraftManager の宇宙船を含めるか。既定値は true", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool includeBaseGame = ToolUtil.GetBool(args, "includeBaseGame", true);
                    var clustercrafts = Components.Clustercrafts.Items
                        .Where(craft => craft != null)
                        .Select(craft => ClustercraftToDictionary(craft))
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["clustercraftCount"] = clustercrafts.Count,
                        ["clustercrafts"] = clustercrafts
                    };

                    if (includeBaseGame)
                    {
                        var spacecraft = GetBaseGameSpacecraft()
                            .Select(SpacecraftToDictionary)
                            .ToList();
                        result["spacecraftCount"] = spacecraft.Count;
                        result["spacecraft"] = spacecraft;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetRocketStatus()
        {
            return new McpTool
            {
                Name = "rockets_status",
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "spacecraft_status" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=status を使用すること",
                Hidden = true,
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["includeDetail"] = new McpToolParameter { Type = "boolean", Description = "各ロケットの詳細なモジュール情報を含めるか。既定値は false", Required = false },
                    ["includeBaseGame"] = new McpToolParameter { Type = "boolean", Description = "基本版の SpacecraftManager の宇宙船を含めるか。既定値は true", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool includeDetail = ToolUtil.GetBool(args, "includeDetail", false);
                    bool includeBaseGame = ToolUtil.GetBool(args, "includeBaseGame", true);
                    var clustercrafts = Components.Clustercrafts.Items
                        .Where(craft => craft != null)
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["clustercraftCount"] = clustercrafts.Count,
                        ["flightInProgressCount"] = clustercrafts.Count(craft => Safe(() => craft.IsFlightInProgress(), false)),
                        ["launchRequestedCount"] = clustercrafts.Count(craft => craft.LaunchRequested),
                        ["readyToLaunchCount"] = clustercrafts.Count(craft => Safe(() => craft.CheckReadyToLaunch(), false)),
                        ["clustercrafts"] = clustercrafts
                            .Select(craft => ClustercraftToDictionary(craft, includeDetail))
                            .ToList()
                    };

                    if (includeBaseGame)
                    {
                        var spacecraft = GetBaseGameSpacecraft();
                        result["spacecraftCount"] = spacecraft.Count;
                        result["spacecraftInMissionCount"] = spacecraft.Count(craft => craft.state.ToString() != "OnGround");
                        result["spacecraft"] = spacecraft
                            .Select(craft => SpacecraftToDictionary(craft))
                            .ToList();
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetRocketDetail()
        {
            return new McpTool
            {
                Name = "rockets_detail",
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "spacecraft_detail" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=detail を使用すること",
                Hidden = true,
                Parameters = RocketLookupParams(),
                Handler = args =>
                {
                    var craft = FindClustercraft(args);
                    if (craft == null)
                        return CallToolResult.Error("Rocket not found");

                    return CallToolResult.Text(JsonConvert.SerializeObject(ClustercraftToDictionary(craft, true), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListSpaceDestinations()
        {
            return new McpTool
            {
                Name = "space_destinations_list",
                Hidden = true,
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "cluster_entities_list" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=list_destinations を使う。星図のエンティティと基本版の宇宙航行の目的地を一覧表示する。set_destination の q/r や worldId の選択に使用できる",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "星図に表示されているエンティティだけを返すか。既定値は true", Required = false },
                    ["layer"] = new McpToolParameter { Type = "string", Description = "星図のレイヤーで絞り込む。例: Asteroid、Craft、POI", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す星図のエンティティ数の上限。既定値は 200、最大 1000", Required = false }
                },
                Handler = args =>
                {
                    bool visibleOnly = ToolUtil.GetBool(args, "visibleOnly", true);
                    string layer = args["layer"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 200, 1000);

                    var entities = GetClusterEntities(visibleOnly, layer, limit)
                        .Select(EntityToDictionary)
                        .ToList();
                    var destinations = GetSpaceDestinations()
                        .Select(SpaceDestinationToDictionary)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["visibleOnly"] = visibleOnly,
                        ["layer"] = string.IsNullOrWhiteSpace(layer) ? null : layer,
                        ["clusterEntityCount"] = entities.Count,
                        ["clusterEntities"] = entities,
                        ["baseGameDestinationCount"] = destinations.Count,
                        ["baseGameDestinations"] = destinations
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListLaunchPads()
        {
            return new McpTool
            {
                Name = "launch_pads_list",
                Hidden = true,
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "rocket_launch_pads_list", "landing_pads_list" },
                Tags = new List<string> { "rocket", "launch-pad", "landing", "side-screen" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=list_launch_pads を使う。LaunchPadSideScreen の発射台の状態、停泊中のロケット、着陸可能・着陸待ちのロケットを一覧表示する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "発射台名、prefabId、ワールド名、着陸待ちのロケット名で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 100, 500);
                    var pads = Components.LaunchPads.Items
                        .Where(pad => pad != null)
                        .Select(LaunchPadToDictionary)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = pads.Count,
                        ["launchPads"] = pads
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetRocketRoundTrip()
        {
            return new McpTool
            {
                Name = "rocket_round_trip_set",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "rocket_repeat_destination_set", "rocket_roundtrip_set" },
                Tags = new List<string> { "rocket", "destination", "round-trip", "side-screen" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=ops action=set_round_trip を使用すること",
                Hidden = true,
                Parameters = RocketLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["repeat"] = new McpToolParameter { Type = "boolean", Description = "true=往復、false=片道", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    var craft = FindClustercraft(args);
                    if (craft == null)
                        return CallToolResult.Error("Rocket not found");
                    var selector = craft.ModuleInterface?.GetClusterDestinationSelector() as RocketClusterDestinationSelector;
                    if (selector == null)
                        return CallToolResult.Error("Rocket does not expose RocketClusterDestinationSelector");

                    bool repeat = ToolUtil.GetBool(args, "repeat", selector.Repeat);
                    bool before = selector.Repeat;
                    selector.Repeat = repeat;

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["rocket"] = ClustercraftToDictionary(craft),
                        ["before"] = before,
                        ["repeat"] = selector.Repeat
                    }, McpJsonUtil.Settings));
                }
            };
        }

    }
}
