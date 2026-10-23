using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class CameraTools
    {
        private static readonly List<string> OverlayViewNames = new List<string>
        {
            "none",
            "oxygen",
            "power",
            "gas_conduits",
            "liquid_conduits",
            "solid_conveyor",
            "logic",
            "temperature",
            "heat_flow",
            "thermal_conductivity",
            "materials",
            "light",
            "decor",
            "rooms",
            "priorities",
            "disease",
            "radiation",
            "sound",
            "suit",
            "crop",
            "harvest"
        };

        public static McpTool ControlCamera()
        {
            return new McpTool
            {
                Name = "camera_control",
                Group = "camera",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "camera", "camera_tools", "screenshot_control" },
                Tags = new List<string> { "camera", "screenshot", "overlay", "world", "navigation" },
                Description = McpLocalization.Get("screenshot.camera_description"),
                Parameters = CameraControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "get_view":
                            return GetCameraView().Handler(args);
                        case "set_active_world":
                        case "set_world":
                        case "switch_world":
                            return SetActiveWorld().Handler(args);
                        case "set_view":
                            return SetCameraView().Handler(args);
                        case "move":
                            return MoveCamera().Handler(args);
                        case "switch_view":
                        case "overlay":
                            return SwitchView().Handler(args);
                        case "focus_cell":
                            return FocusCell().Handler(args);
                        case "focus_dupe":
                            return FocusDupe().Handler(args);
                        case "screenshot":
                        case "game_screenshot":
                            return TakeScreenshot().Handler(args);
                        case "screenshot_status":
                            return ScreenshotStatus(args["requestId"]?.ToString());
                        case "coordinate_screenshot":
                        case "coord_screenshot":
                            return TakeCoordinateScreenshot().Handler(args);
                        default:
                            return CallToolResult.Error(McpLocalization.Get("screenshot.invalid_action"));
                    }
                }
            };
        }

        public static McpTool GetCameraView()
        {
            return new McpTool
            {
                Name = "camera_get_view",
                Hidden = true,
                Group = "camera",
                Mode = "read",
                Risk = "none",
                Description = "互換用の入口。優先して使用してください: navigation_control action=get_view",
                Handler = args =>
                {
                    var camera = CameraController.Instance;
                    if (camera == null || Camera.main == null)
                        return CallToolResult.Error("Camera not available");

                    var pos = camera.transform.GetPosition();
                    var result = new Dictionary<string, object>
                    {
                        ["position"] = new { x = Math.Round(pos.x, 2), y = Math.Round(pos.y, 2), z = Math.Round(pos.z, 2) },
                        ["orthographicSize"] = Math.Round(Camera.main.orthographicSize, 2),
                        ["activeWorldId"] = ClusterManager.Instance?.activeWorldId ?? -1,
                        ["screen"] = new { width = Screen.width, height = Screen.height }
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetActiveWorld()
        {
            return new McpTool
            {
                Name = "camera_set_active_world",
                Hidden = true,
                Group = "camera",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "world_set_active", "camera_switch_world", "view_world" },
                Tags = new List<string> { "camera", "world", "cluster", "rocket", "navigation" },
                Description = "互換用の入口。優先して使用してください: navigation_control action=set_active_world",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。先に oni://world/list で確認可能", Required = true },
                    ["requireDiscovered"] = new McpToolParameter { Type = "boolean", Description = "対象ワールドが発見済みであることを要求するか。既定は true", Required = false },
                    ["lookAtSurface"] = new McpToolParameter { Type = "boolean", Description = "複製人間が未訪問のワールドでは、LookAtSurface を呼び出してから切り替えるか。既定は true", Required = false },
                    ["zoom"] = new McpToolParameter { Type = "number", Description = "切り替え後のカメラ倍率。既定では現在の倍率を維持、または 10", Required = false }
                },
                Handler = args =>
                {
                    if (ClusterManager.Instance == null)
                        return CallToolResult.Error("ClusterManager not initialized");

                    int? worldId = ToolUtil.GetInt(args, "worldId");
                    if (!worldId.HasValue)
                        return CallToolResult.Error("worldId is required");

                    var world = ClusterManager.Instance.GetWorld(worldId.Value);
                    if (world == null)
                        return CallToolResult.Error($"World not found: {worldId.Value}");

                    bool requireDiscovered = ToolUtil.GetBool(args, "requireDiscovered", true);
                    if (requireDiscovered && !world.IsDiscovered)
                        return CallToolResult.Error($"World {worldId.Value} is not discovered");

                    bool lookAtSurface = ToolUtil.GetBool(args, "lookAtSurface", true);
                    if (lookAtSurface && !world.IsDupeVisited)
                        world.LookAtSurface();

                    int previousWorldId = ClusterManager.Instance.activeWorldId;
                    ClusterManager.Instance.SetActiveWorld(world.id);

                    var camera = CameraController.Instance;
                    if (camera != null)
                    {
                        float zoom = ToolUtil.GetFloat(args, "zoom") ?? (Camera.main != null ? Camera.main.orthographicSize : 10f);
                        var center = (world.minimumBounds + world.maximumBounds) * 0.5f;
                        camera.SnapTo(new Vector3(center.x, center.y, -100f), zoom);
                    }

                    ManagementMenu.Instance?.CloseAll();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["previousWorldId"] = previousWorldId,
                        ["activeWorldId"] = ClusterManager.Instance.activeWorldId,
                        ["worldName"] = world.GetProperName(),
                        ["isDiscovered"] = world.IsDiscovered,
                        ["isDupeVisited"] = world.IsDupeVisited,
                        ["isModuleInterior"] = world.IsModuleInterior
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetCameraView()
        {
            return new McpTool
            {
                Name = "camera_set_view",
                Hidden = true,
                Group = "camera",
                Mode = "execute",
                Risk = "low",
                Description = "互換用の入口。優先して使用してください: navigation_control action=set_view",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["x"] = new McpToolParameter { Type = "number", Description = "対象の X 座標", Required = true },
                    ["y"] = new McpToolParameter { Type = "number", Description = "対象の Y 座標", Required = true },
                    ["zoom"] = new McpToolParameter { Type = "number", Description = "カメラの平行投影倍率。既定は 10", Required = false },
                    ["snap"] = new McpToolParameter { Type = "boolean", Description = "即座に移動するか。既定は true", Required = false }
                },
                Handler = args =>
                {
                    var camera = CameraController.Instance;
                    if (camera == null)
                        return CallToolResult.Error("Camera not available");

                    float? x = ToolUtil.GetFloat(args, "x");
                    float? y = ToolUtil.GetFloat(args, "y");
                    if (!x.HasValue || !y.HasValue)
                        return CallToolResult.Error("x and y are required");

                    float zoom = ToolUtil.GetFloat(args, "zoom") ?? 10f;
                    bool snap = ToolUtil.GetBool(args, "snap", true);
                    var pos = new Vector3(x.Value, y.Value, -100f);
                    if (snap)
                        camera.SnapTo(pos, zoom);
                    else
                        camera.CameraGoTo(pos, 2f, true);

                    return CallToolResult.Text($"Camera moved to ({x.Value}, {y.Value}) zoom={zoom}");
                }
            };
        }

        public static McpTool MoveCamera()
        {
            return new McpTool
            {
                Name = "camera_move",
                Hidden = true,
                Group = "camera",
                Mode = "execute",
                Risk = "low",
                Description = "互換用の入口。優先して使用してください: navigation_control action=move",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["mode"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "移動モード: pan=dx/dy で相対移動、jump=x/y のワールド座標へ移動",
                        Required = true,
                        EnumValues = new List<string> { "pan", "jump" }
                    },
                    ["x"] = new McpToolParameter { Type = "number", Description = "jump モードでの対象 X 座標", Required = false },
                    ["y"] = new McpToolParameter { Type = "number", Description = "jump モードでの対象 Y 座標", Required = false },
                    ["dx"] = new McpToolParameter { Type = "number", Description = "pan モードでの X 方向の移動量。既定は 0", Required = false },
                    ["dy"] = new McpToolParameter { Type = "number", Description = "pan モードでの Y 方向の移動量。既定は 0", Required = false },
                    ["zoom"] = new McpToolParameter { Type = "number", Description = "カメラの平行投影倍率。省略時は現在の倍率を維持", Required = false },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "滑らかな移動にかける秒数。既定は 0.5", Required = false },
                    ["snap"] = new McpToolParameter { Type = "boolean", Description = "即座に移動するか。既定は jump=true、pan=false", Required = false }
                },
                Handler = args =>
                {
                    var camera = CameraController.Instance;
                    if (camera == null || Camera.main == null)
                        return CallToolResult.Error("Camera not available");

                    string mode = (args["mode"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (mode != "pan" && mode != "jump")
                        return CallToolResult.Error("mode must be pan or jump");

                    var current = camera.transform.GetPosition();
                    Vector3 target;
                    if (mode == "jump")
                    {
                        float? x = ToolUtil.GetFloat(args, "x");
                        float? y = ToolUtil.GetFloat(args, "y");
                        if (!x.HasValue || !y.HasValue)
                            return CallToolResult.Error("x and y are required for jump mode");

                        target = new Vector3(x.Value, y.Value, -100f);
                    }
                    else
                    {
                        float dx = ToolUtil.GetFloat(args, "dx") ?? 0f;
                        float dy = ToolUtil.GetFloat(args, "dy") ?? 0f;
                        if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                            return CallToolResult.Error("dx or dy is required for pan mode");

                        target = new Vector3(current.x + dx, current.y + dy, -100f);
                    }

                    float zoom = ToolUtil.GetFloat(args, "zoom") ?? Camera.main.orthographicSize;
                    bool snap = ToolUtil.GetBool(args, "snap", mode == "jump");
                    if (snap)
                    {
                        camera.SnapTo(target, zoom);
                    }
                    else
                    {
                        float duration = Math.Max(0.05f, ToolUtil.GetFloat(args, "duration") ?? 0.5f);
                        camera.CameraGoTo(target, duration, true);
                        Camera.main.orthographicSize = zoom;
                    }

                    var result = new Dictionary<string, object>
                    {
                        ["mode"] = mode,
                        ["from"] = new { x = Math.Round(current.x, 2), y = Math.Round(current.y, 2), z = Math.Round(current.z, 2) },
                        ["target"] = new { x = Math.Round(target.x, 2), y = Math.Round(target.y, 2), z = Math.Round(target.z, 2) },
                        ["zoom"] = Math.Round(zoom, 2),
                        ["snap"] = snap
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

    }
}
