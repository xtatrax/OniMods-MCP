using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static class NavigationControlTools
    {
        public static McpTool ControlNavigation()
        {
            return new McpTool
            {
                Name = "navigation_control",
                Group = "camera",
                Mode = "execute",
                Risk = "medium",
                Aliases = new List<string> { "spatial_control", "view_control" },
                Tags = new List<string> { "camera", "view", "screenshot", "overlay" },
                Description = "カメラとビューの集約エントリーポイント。domain=camera でカメラ移動、ワールド切り替え、オーバーレイ、スクリーンショットを扱う。domain を省略すると、既知のカメラ action から自動で判断する。",
                Parameters = Params(),
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(action))
                        return CallToolResult.Error("action is required");

                    if (string.IsNullOrWhiteSpace(domain))
                        domain = InferDomain(action);

                    var forwarded = new JObject(args);
                    forwarded.Remove("domain");
                    switch (domain)
                    {
                        case "camera":
                        case "view":
                        case "screenshot":
                            return CameraTools.ControlCamera().Handler(forwarded);
                        case "pointer":
                        case "agent_pointer":
                        case "mouse":
                            return CallToolResult.Error("agent pointer control has been removed. Use regular semantic tools with the required task text; the task text is shown near the player's mouse automatically.");
                        default:
                            return CallToolResult.Error("domain must be camera and action must be get_view, set_active_world, set_view, move, switch_view, focus_cell, focus_dupe, screenshot, or coordinate_screenshot");
                    }
                }
            };
        }

        private static string InferDomain(string action)
        {
            switch (action)
            {
                case "get_view":
                case "set_active_world":
                case "set_world":
                case "switch_world":
                case "set_view":
                case "move":
                case "switch_view":
                case "overlay":
                case "focus_cell":
                case "focus_dupe":
                case "screenshot":
                case "game_screenshot":
                case "coordinate_screenshot":
                case "coord_screenshot":
                    return "camera";
                default:
                    return string.Empty;
            }
        }

        private static Dictionary<string, McpToolParameter> Params()
        {
            return new Dictionary<string, McpToolParameter>
            {
                ["domain"] = new McpToolParameter { Type = "string", Description = "カメラのドメイン。省略すると、既知のカメラ action から自動で判断する", Required = false, EnumValues = new List<string> { "camera" } },
                ["action"] = new McpToolParameter { Type = "string", Description = "カメラ操作: get_view、set_active_world、set_view、move、switch_view、focus_cell、focus_dupe、screenshot、coordinate_screenshot", Required = true, EnumValues = new List<string> { "get_view", "set_active_world", "set_view", "move", "switch_view", "focus_cell", "focus_dupe", "screenshot", "coordinate_screenshot" } },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。set_active_world では必須。その他の action は現在アクティブなワールドが既定値", Required = false },
                ["requireDiscovered"] = new McpToolParameter { Type = "boolean", Description = "set_active_world: 対象ワールドが発見済みであることを必須とするか。既定値は true", Required = false },
                ["lookAtSurface"] = new McpToolParameter { Type = "boolean", Description = "set_active_world: 未訪問のワールドで LookAtSurface を行うか。既定値は true", Required = false },
                ["x"] = new McpToolParameter { Type = "number", Description = "set_view/move jump の移動先の X、または focus_cell のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "number", Description = "set_view/move jump の移動先の Y、または focus_cell のセルの Y", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "domain=camera action=coordinate_screenshot: 領域の左下の X", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "domain=camera action=coordinate_screenshot: 領域の左下の Y", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "domain=camera action=coordinate_screenshot: 領域の右上の X", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "domain=camera action=coordinate_screenshot: 領域の右上の Y", Required = false },
                ["areaId"] = new McpToolParameter { Type = "string", Description = "domain=camera action=coordinate_screenshot: 領域のハンドル", Required = false },
                ["dx"] = new McpToolParameter { Type = "number", Description = "move pan: X 方向の移動量。既定値は 0", Required = false },
                ["dy"] = new McpToolParameter { Type = "number", Description = "move pan: Y 方向の移動量。既定値は 0", Required = false },
                ["zoom"] = new McpToolParameter { Type = "number", Description = "カメラのズーム。action に応じて解釈する", Required = false },
                ["snap"] = new McpToolParameter { Type = "boolean", Description = "domain=camera action=set_view/move: 即座に移動するか", Required = false },
                ["mode"] = new McpToolParameter { Type = "string", Description = "domain=camera action=move: pan または jump", Required = false, EnumValues = new List<string> { "pan", "jump" } },
                ["duration"] = new McpToolParameter { Type = "number", Description = "domain=camera action=move: 滑らかに移動する時間（秒）", Required = false },
                ["view"] = new McpToolParameter { Type = "string", Description = "domain=camera action=switch_view/coordinate_screenshot: オーバーレイのビュー", Required = false },
                ["screenshot"] = new McpToolParameter { Type = "boolean", Description = "domain=camera action=switch_view: スクリーンショットを取得するか", Required = false },
                ["filename"] = new McpToolParameter { Type = "string", Description = "domain=camera: スクリーンショットのファイル名", Required = false },
                ["waitFrames"] = new McpToolParameter { Type = "integer", Description = "domain=camera: スクリーンショットの取得前に待機するフレーム数", Required = false },
                ["allowSound"] = new McpToolParameter { Type = "boolean", Description = "domain=camera action=switch_view: 切り替え時の効果音を再生するか", Required = false },
                ["id"] = new McpToolParameter { Type = "integer", Description = "domain=camera action=focus_dupe: 複製人間の InstanceID", Required = false },
                ["name"] = new McpToolParameter { Type = "string", Description = "domain=camera action=focus_dupe: 複製人間の名前", Required = false },
                ["focusCamera"] = new McpToolParameter { Type = "boolean", Description = "domain=camera action=coordinate_screenshot: 自動でカメラを移動するか", Required = false },
                ["paddingCells"] = new McpToolParameter { Type = "number", Description = "domain=camera action=coordinate_screenshot: フォーカス時に余白として確保するセル数", Required = false },
                ["showGrid"] = new McpToolParameter { Type = "boolean", Description = "domain=camera action=coordinate_screenshot: グリッド線を表示するか", Required = false },
                ["showCoordinates"] = new McpToolParameter { Type = "boolean", Description = "domain=camera action=coordinate_screenshot: 座標を表示するか", Required = false },
                ["includeCellLabels"] = new McpToolParameter { Type = "boolean", Description = "domain=camera action=coordinate_screenshot: セル中央の座標を表示するか", Required = false },
                ["step"] = new McpToolParameter { Type = "integer", Description = "domain=camera action=coordinate_screenshot: 座標ラベルの間隔", Required = false }
            };
        }
    }
}
