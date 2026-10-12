using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static class BioControlTools
    {
        public static McpTool ControlBio()
        {
            return new McpTool
            {
                Name = "bio_control",
                Group = "bio",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "life_control", "farming_ranching_control" },
                Tags = new List<string> { "farming", "ranching", "plants", "critters", "seeds", "incubator", "dropoff" },
                Description = "生物による生産の統合エントリーポイント。domain=farming/ranching。栽培、収穫、掘り起こし、動物、解放地点、孵卵器をすべて domain 引数で振り分ける。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "farming または ranching", Required = true, EnumValues = new List<string> { "farming", "ranching" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "farming: list_planting/seed_catalog/list_harvestables/set_harvestable/set_planting/batch_set_planting/uproot；ranching: critters/list/configure/batch", Required = false },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "domain=ranching では critters/dropoff/incubator", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "検索・絞り込みの語。子アクションの仕様に従って使用する", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "領域ハンドル。子アクションの仕様に従って使用する", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象の X。子アクションの仕様に従って使用する", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象の Y。子アクションの仕様に従って使用する", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の始点の X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の始点の Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の終点の X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の終点の Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。子アクションの仕様に従って使用する", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返却・処理する件数の上限", Required = false },
                    ["seedTag"] = new McpToolParameter { Type = "string", Description = "domain=farming で栽培要求を設定する際の種子の prefab/tag", Required = false },
                    ["cropSeedOnly"] = new McpToolParameter { Type = "boolean", Description = "domain=farming action=seed_catalog で CropSeed タグを持つ種子候補だけを返す", Required = false },
                    ["eggTag"] = new McpToolParameter { Type = "string", Description = "domain=ranching kind=incubator で卵の要求を設定する際の卵の prefab/tag", Required = false },
                    ["critterTags"] = new McpToolParameter { Type = "array", Description = "domain=ranching kind=dropoff の動物の prefab/tag のリスト", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "一括操作や危険な書き込み操作の確認。子ツールの規則に従って使用する", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    switch (domain)
                    {
                        case "farming":
                        case "farm":
                        case "planting":
                        case "plants":
                            return Forward(args, FarmingTools.ControlPlanting());
                        case "ranching":
                        case "ranch":
                        case "critters":
                        case "critter":
                            return Forward(args, RanchingTools.ControlRanching());
                        default:
                            return CallToolResult.Error("domain must be farming or ranching");
                    }
                }
            };
        }

        private static CallToolResult Forward(JObject args, McpTool tool)
        {
            var forwarded = args == null ? new JObject() : (JObject)args.DeepClone();
            forwarded.Remove("domain");
            return tool.Handler(forwarded);
        }
    }
}
