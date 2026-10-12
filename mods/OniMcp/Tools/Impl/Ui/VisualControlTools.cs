using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static class VisualControlTools
    {
        public static McpTool ControlVisual()
        {
            return new McpTool
            {
                Name = "visual_control",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "building_visual_control", "color_control" },
                Tags = new List<string> { "buildings", "lights", "pixel-pack", "colors", "visual" },
                Description = "設備の外観/色の統合窓口。kind=light/pixel_pack。action は対応する旧 control にそのまま渡す。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "外観コントロールのタイプ: light または pixel_pack", Required = true, EnumValues = new List<string> { "light", "pixel_pack" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "light は list/set_color、pixel_pack は list/set_color/copy_colors に対応", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list の場合、設備名、prefabId、色名で絞り込み", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "action=list の場合の領域ハンドル。x1/y1/x2/y2 とどちらか一方を指定", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の絞り込み矩形の始点 X 座標", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の絞り込み矩形の始点 Y 座標", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の絞り込み矩形の終点 X 座標", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の絞り込み矩形の終点 Y 座標", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。list では絞り込みに使用可能。座標で検索する場合は指定を推奨", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の最大返却数", Required = false },
                    ["configurableOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=light action=list の場合、LightColorMenu を持つ照明のみを返すか。既定値は true", Required = false },
                    ["includePresets"] = new McpToolParameter { Type = "boolean", Description = "kind=pixel_pack action=list の場合、色プリセットを返すか。既定値は false", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "書き込み対象の KPrefabID.InstanceID。指定を推奨", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "書き込み対象セルの X 座標。id 未指定の場合に使用", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "書き込み対象セルの Y 座標。id 未指定の場合に使用", Required = false },
                    ["colorIndex"] = new McpToolParameter { Type = "integer", Description = "対象の色プリセットのインデックス", Required = false },
                    ["colorName"] = new McpToolParameter { Type = "string", Description = "対象の色プリセット名。colorIndex が空の場合に使用", Required = false },
                    ["panel"] = new McpToolParameter { Type = "string", Description = "kind=pixel_pack action=set_color/copy_colors の場合の対象パネル", Required = false },
                    ["state"] = new McpToolParameter { Type = "string", Description = "kind=pixel_pack action=set_color/copy_colors の場合は active または standby", Required = false },
                    ["sourcePanel"] = new McpToolParameter { Type = "string", Description = "kind=pixel_pack action=copy_colors の場合のコピー元パネル", Required = false },
                    ["sourceState"] = new McpToolParameter { Type = "string", Description = "kind=pixel_pack action=copy_colors の場合のコピー元の状態", Required = false },
                    ["targetPanel"] = new McpToolParameter { Type = "string", Description = "kind=pixel_pack action=copy_colors の場合のコピー先パネル", Required = false },
                    ["targetState"] = new McpToolParameter { Type = "string", Description = "kind=pixel_pack action=copy_colors の場合のコピー先の状態", Required = false }
                },
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (kind)
                    {
                        case "light":
                        case "lights":
                            return LightTools.ControlLight().Handler(args);
                        case "pixel_pack":
                        case "pixelpack":
                            return PixelPackTools.ControlPixelPack().Handler(args);
                        default:
                            return CallToolResult.Error("kind must be light or pixel_pack");
                    }
                }
            };
        }
    }
}
