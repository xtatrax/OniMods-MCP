using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class SpaceBuildingTools
    {
        public static McpTool ControlSpaceBuilding()
        {
            return new McpTool
            {
                Name = "space_building_control",
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "space_side_building_control" },
                Tags = new List<string> { "automation", "space", "rocket", "sensor", "railgun", "side-screen" },
                Description = "宇宙関連設備のサイドパネルの集約ツール。kind=comet_detector/cluster_location_sensor/railgun。action は対応する旧 control の操作を使う。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "宇宙関連設備の種類: comet_detector、cluster_location_sensor、railgun", Required = true, EnumValues = new List<string> { "comet_detector", "cluster_location_sensor", "railgun" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、set_target、set、set_launch_mass", Required = true },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象の設備の InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象の設備または領域の X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象の設備または領域の Y", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の左下の X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の左下の Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の右上の X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の右上の Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で名前、prefabId、状態により絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "action=list で選択可能な項目を返すか", Required = false },
                    ["targetType"] = new McpToolParameter { Type = "string", Description = "kind=comet_detector action=set_target の対象の種類", Required = false },
                    ["rocketId"] = new McpToolParameter { Type = "integer", Description = "kind=comet_detector targetType=rocket の場合のロケットの id", Required = false },
                    ["rocketName"] = new McpToolParameter { Type = "string", Description = "kind=comet_detector targetType=rocket の場合のロケット名", Required = false },
                    ["target"] = new McpToolParameter { Type = "string", Description = "kind=cluster_location_sensor action=set では space または location", Required = false },
                    ["q"] = new McpToolParameter { Type = "integer", Description = "kind=cluster_location_sensor target=location の場合の q 座標", Required = false },
                    ["r"] = new McpToolParameter { Type = "integer", Description = "kind=cluster_location_sensor target=location の場合の r 座標", Required = false },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "kind=cluster_location_sensor action=set で対象を有効にするか", Required = false },
                    ["massKg"] = new McpToolParameter { Type = "number", Description = "kind=railgun action=set_launch_mass の発射質量（kg）", Required = false }
                },
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (kind)
                    {
                        case "comet_detector":
                        case "space_scanner":
                            return ControlCometDetector().Handler(args);
                        case "cluster_location_sensor":
                        case "cluster_location":
                            return ControlClusterLocationSensor().Handler(args);
                        case "railgun":
                            return ControlRailGun().Handler(args);
                        default:
                            return CallToolResult.Error("kind must be comet_detector, cluster_location_sensor, or railgun");
                    }
                }
            };
        }

        public static McpTool ControlCometDetector()
        {
            return new McpTool
            {
                Name = "comet_detector_control",
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "space_scanner_control", "comet_detector_target_control" },
                Tags = new List<string> { "automation", "space", "comet", "rocket", "sensor", "side-screen" },
                Description = "宇宙スキャナーの集約ツール。action=list で検知対象を照会し、action=set_target で meteor_shower、ballistic_object、rocket のいずれかを設定する",
                Parameters = CometDetectorControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListCometDetectors().Handler(args);
                    if (action == "set_target" || action == "set")
                        return SetCometDetectorTarget().Handler(args);
                    return CallToolResult.Error("action must be list or set_target");
                }
            };
        }

        public static McpTool ListCometDetectors()
        {
            return new McpTool
            {
                Name = "comet_detectors_list",
                Hidden = true,
                Group = "automation",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "space_scanners_list", "comet_detector_targets_list" },
                Tags = new List<string> { "automation", "space", "comet", "rocket", "sensor", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=space_building kind=comet_detector action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、対象の種類、ロケット名で絞り込む", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "選択可能な対象を返すか。既定値は true", Required = false },
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
                    bool includeOptions = ToolUtil.GetBool(args, "includeOptions", true);
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var detectors = Components.BuildingCompletes.Items
                        .Select(building => building?.gameObject)
                        .Where(go => MatchesTarget(go, rect, worldId))
                        .Where(IsCometDetector)
                        .Select(go => CometDetectorInfo(go, includeOptions))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = detectors.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["detectors"] = detectors
                    });
                }
            };
        }

        public static McpTool SetCometDetectorTarget()
        {
            return new McpTool
            {
                Name = "comet_detector_target_set",
                Hidden = true,
                Group = "automation",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "space_scanner_target_set" },
                Tags = new List<string> { "automation", "space", "comet", "rocket", "sensor", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=space_building kind=comet_detector action=set_target を使用すること。DLC モードは meteor_shower、ballistic_object、rocket に対応し、基本版は meteor_shower または rocket に対応する",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["targetType"] = new McpToolParameter { Type = "string", Description = "meteor_shower、ballistic_object、rocket のいずれか", Required = true, EnumValues = new List<string> { "meteor_shower", "ballistic_object", "rocket" } },
                    ["rocketId"] = new McpToolParameter { Type = "integer", Description = "targetType=rocket の場合の、対象の Clustercraft/Spacecraft の id", Required = false },
                    ["rocketName"] = new McpToolParameter { Type = "string", Description = "targetType=rocket の場合にロケット名で対象を指定する", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindBuildingTarget(args, IsCometDetector);
                    if (go == null)
                        return CallToolResult.Error("Target comet detector not found");
                    string targetType = NormalizeTargetType(args["targetType"]?.ToString());
                    if (targetType == null)
                        return CallToolResult.Error("targetType must be meteor_shower, ballistic_object, or rocket");

                    var before = CometDetectorInfo(go, includeOptions: true);
                    if (DlcManager.IsExpansion1Active())
                    {
                        var detector = go.GetSMI<ClusterCometDetector.Instance>();
                        if (detector == null)
                            return CallToolResult.Error("Target does not expose ClusterCometDetector");
                        if (targetType == "meteor_shower")
                        {
                            detector.SetDetectorState(ClusterCometDetector.Instance.ClusterCometDetectorState.MeteorShower);
                            detector.SetClustercraftTarget(null);
                        }
                        else if (targetType == "ballistic_object")
                        {
                            detector.SetDetectorState(ClusterCometDetector.Instance.ClusterCometDetectorState.BallisticObject);
                            detector.SetClustercraftTarget(null);
                        }
                        else
                        {
                            var craft = FindClustercraft(args);
                            if (craft == null)
                                return CallToolResult.Error("rocketId or rocketName must match a Clustercraft");
                            detector.SetDetectorState(ClusterCometDetector.Instance.ClusterCometDetectorState.Rocket);
                            detector.SetClustercraftTarget(craft);
                        }
                    }
                    else
                    {
                        var detector = go.GetSMI<CometDetector.Instance>();
                        if (detector == null)
                            return CallToolResult.Error("Target does not expose CometDetector");
                        if (targetType == "ballistic_object")
                            return CallToolResult.Error("ballistic_object target is only available in DLC cluster mode");
                        detector.SetTargetCraft(targetType == "rocket" ? FindBaseRocketTarget(args) : null);
                        if (targetType == "rocket" && detector.GetTargetCraft() == null)
                            return CallToolResult.Error("rocketId or rocketName must match a base-game Spacecraft");
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["detector"] = CometDetectorInfo(go, includeOptions: true)
                    });
                }
            };
        }

    }
}
