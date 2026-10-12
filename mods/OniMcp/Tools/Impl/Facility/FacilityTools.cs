using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static class FacilityTools
    {
        public static McpTool ControlFacility()
        {
            return new McpTool
            {
                Name = "facility_control",
                Group = "buildings",
                Mode = "write",
                Risk = "high",
                Aliases = new List<string> { "special_facility_control", "story_space_facility_control" },
                Tags = new List<string> { "facility", "special", "story", "space", "side-screen", "building" },
                Description = "特殊・ストーリー・宇宙関連の設備の集約エントリーポイント。domain=space_building/space_story/special/story_facility。各子ツールの kind/action と確認規則を維持する。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "space_building、space_story、special、story_facility のいずれか", Required = true, EnumValues = new List<string> { "space_building", "space_story", "special", "story_facility" } },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "設備の種類。domain に応じて元の kind を使う。例: comet_detector、railgun、warp_portal、artable、printerceptor", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "子アクション。domain/kind に応じて元の action を使う。例: list、set、set_target、set_stage、consume、assign、cancel", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象の設備の InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象の X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象の Y", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "一覧・選択操作の絞り込み語", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "一覧で返す件数の上限", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "一覧に選択可能な項目を含めるか", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "子アクションに確認が必要な場合は true を渡す", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    switch (domain)
                    {
                        case "space_building":
                        case "space":
                        case "automation_space":
                            return SpaceBuildingTools.ControlSpaceBuilding().Handler(args);
                        case "space_story":
                        case "story_space":
                        case "starmap":
                            return SpaceStoryTools.ControlSpaceStory().Handler(args);
                        case "special":
                        case "special_building":
                        case "building":
                            return SpecialBuildingTools.ControlSpecialBuilding().Handler(args);
                        case "story_facility":
                        case "story":
                        case "facility":
                            return StoryFacilityTools.ControlStoryFacility().Handler(args);
                        default:
                            return CallToolResult.Error("domain must be space_building, space_story, special, or story_facility");
                    }
                }
            };
        }
    }
}
