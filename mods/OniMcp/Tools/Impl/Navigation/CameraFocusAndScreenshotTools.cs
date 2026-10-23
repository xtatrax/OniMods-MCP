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
        public static McpTool SwitchView()
        {
            return new McpTool
            {
                Name = "camera_switch_view",
                Hidden = true,
                Group = "camera",
                Mode = "execute",
                Risk = "low",
                Description = McpLocalization.Get("screenshot.switch_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["view"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "対象ビュー: none、oxygen、power、gas_conduits、liquid_conduits、solid_conveyor、logic、temperature、heat_flow、thermal_conductivity、materials、light、decor、rooms、priorities、disease、radiation、sound、suit、crop、harvest",
                        Required = true,
                        EnumValues = OverlayViewNames
                    },
                    ["screenshot"] = new McpToolParameter { Type = "boolean", Description = "切り替え後のスクリーンショットを保存するか。既定は true", Required = false },
                    ["filename"] = new McpToolParameter { Type = "string", Description = "スクリーンショットのファイル名。screenshot=true の場合のみ使用", Required = false },
                    ["waitFrames"] = new McpToolParameter { Type = "integer", Description = "撮影前に待つ Unity フレーム数。既定は 2。レイヤーの描画完了を待つために使用", Required = false },
                    ["allowSound"] = new McpToolParameter { Type = "boolean", Description = "ゲームのビュー切り替え効果音を再生するか。既定は false", Required = false }
                },
                Handler = args => TryScreenshot(() =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error(McpLocalization.Get("screenshot.error_game_unavailable"));

                    var overlay = OverlayScreen.Instance;
                    if (overlay == null)
                        return CallToolResult.Error(McpLocalization.Get("screenshot.error_overlay_unavailable"));

                    string requestedView = args["view"]?.ToString();
                    if (string.IsNullOrEmpty(requestedView))
                        return CallToolResult.Error(McpLocalization.Get("screenshot.view_required"));

                    if (!TryResolveOverlayView(requestedView, out var viewName, out var mode))
                    {
                        return CallToolResult.Error(McpLocalization.Format("screenshot.unknown_view", null, requestedView, string.Join(", ", OverlayViewNames)));
                    }

                    bool allowSound = ToolUtil.GetBool(args, "allowSound", false);
                    bool screenshot = ToolUtil.GetBool(args, "screenshot", true);
                    int waitFrames = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "waitFrames") ?? 2, 30));
                    var queued = OverlaySwitchQueue.Enqueue(
                        viewName,
                        mode,
                        allowSound,
                        screenshot,
                        args["filename"]?.ToString(),
                        waitFrames);

                    var camera = CameraController.Instance;
                    var pos = camera != null ? camera.transform.GetPosition() : Vector3.zero;
                    var result = new Dictionary<string, object>
                    {
                        ["view"] = viewName,
                        ["requestedOverlay"] = mode.ToString(),
                        ["activeOverlayAtEnqueue"] = overlay.GetMode().ToString(),
                        ["contextTiming"] = "request",
                        ["queued"] = true,
                        ["queue"] = queued,
                        ["requestId"] = queued["requestId"],
                        ["status"] = queued["status"],
                        ["next"] = McpLocalization.Get("screenshot.next"),
                        ["activeWorldId"] = ClusterManager.Instance?.activeWorldId ?? -1,
                        ["camera"] = new
                        {
                            position = new { x = Math.Round(pos.x, 2), y = Math.Round(pos.y, 2), z = Math.Round(pos.z, 2) },
                            orthographicSize = Camera.main != null ? Math.Round(Camera.main.orthographicSize, 2) : 0
                        },
                        ["screen"] = new { width = Screen.width, height = Screen.height }
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                })
            };
        }

        public static McpTool FocusCell()
        {
            return new McpTool
            {
                Name = "camera_focus_cell",
                Hidden = true,
                Group = "camera",
                Mode = "execute",
                Risk = "low",
                Description = "互換用の入口。優先して使用してください: navigation_control action=focus_cell",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["x"] = new McpToolParameter { Type = "integer", Description = "セルの X 座標", Required = true },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "セルの Y 座標", Required = true },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。既定は現在アクティブなワールド", Required = false },
                    ["zoom"] = new McpToolParameter { Type = "number", Description = "カメラの平行投影倍率。既定は 8", Required = false }
                },
                Handler = args =>
                {
                    int? x = ToolUtil.GetInt(args, "x");
                    int? y = ToolUtil.GetInt(args, "y");
                    if (!x.HasValue || !y.HasValue)
                        return CallToolResult.Error("x and y are required");

                    int cell = Grid.XYToCell(x.Value, y.Value);
                    int worldId = ToolUtil.ResolveWorldId(args);
                    if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                        return CallToolResult.Error($"Cell ({x.Value},{y.Value}) is not in worldId={worldId}");

                    float zoom = ToolUtil.GetFloat(args, "zoom") ?? 8f;
                    CameraController.Instance?.SnapTo(new Vector3(x.Value + 0.5f, y.Value + 0.5f, -100f), zoom);
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["focused"] = new { x = x.Value, y = y.Value },
                        ["worldId"] = worldId,
                        ["zoom"] = Math.Round(zoom, 2)
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool FocusDupe()
        {
            return new McpTool
            {
                Name = "camera_focus_dupe",
                Hidden = true,
                Group = "camera",
                Mode = "execute",
                Risk = "low",
                Description = "互換用の入口。優先して使用してください: navigation_control action=focus_dupe",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false }
                },
                Handler = args =>
                {
                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");
                    CameraController.Instance?.SetFollowTarget(dupe.transform);
                    return CallToolResult.Text($"Camera following {dupe.GetProperName()}");
                }
            };
        }

        public static McpTool TakeScreenshot()
        {
            return new McpTool
            {
                Name = "game_screenshot",
                Hidden = true,
                Group = "game",
                Mode = "execute",
                Risk = "low",
                Description = McpLocalization.Get("screenshot.capture_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["filename"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("screenshot.filename"), Required = false },
                    ["waitFrames"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("screenshot.wait_frames"), Required = false }
                },
                Handler = args => TryScreenshot(() =>
                {
                    var result = WorldEditor.SaveScreenshot(args["filename"]?.ToString(), ToolUtil.GetInt(args, "waitFrames") ?? 2);
                    result["next"] = McpLocalization.Get("screenshot.next");
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                })
            };
        }

        public static McpTool TakeCoordinateScreenshot()
        {
            return new McpTool
            {
                Name = "camera_coordinate_screenshot",
                Hidden = true,
                Group = "camera",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "world_coordinate_screenshot", "map_coordinate_screenshot", "coordinate_screenshot" },
                Tags = new List<string> { "camera", "screenshot", "coordinates", "grid", "vision", "map", "截图", "坐标" },
                Description = McpLocalization.Get("screenshot.coordinate_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。指定すると x1/y1/x2/y2/worldId を省略可能", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の X。省略時は現在のカメラ付近", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の Y。省略時は現在のカメラ付近", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の X。省略時は現在のカメラ付近", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の Y。省略時は現在のカメラ付近", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。既定は現在アクティブなワールド、または areaId に紐付くワールド", Required = false },
                    ["view"] = new McpToolParameter { Type = "string", Description = "撮影前に切り替えるレイヤー: none、oxygen、power、gas_conduits、liquid_conduits、solid_conveyor、logic、temperature など。既定では切り替えない", Required = false, EnumValues = OverlayViewNames },
                    ["focusCamera"] = new McpToolParameter { Type = "boolean", Description = "区域全体が映るようにカメラを自動で移動するか。既定は true", Required = false },
                    ["paddingCells"] = new McpToolParameter { Type = "number", Description = "自動で対象に合わせる際の周囲の余白セル数。既定は 1.5", Required = false },
                    ["showGrid"] = new McpToolParameter { Type = "boolean", Description = "グリッド線を表示するか。既定は true", Required = false },
                    ["showCoordinates"] = new McpToolParameter { Type = "boolean", Description = "端に x/y 座標を表示するか。既定は true", Required = false },
                    ["includeCellLabels"] = new McpToolParameter { Type = "boolean", Description = "セルの中央に間隔をあけて x,y を表示するか。既定では区域の大きさに応じて自動で有効・無効を切り替える", Required = false },
                    ["step"] = new McpToolParameter { Type = "integer", Description = "座標ラベルの表示間隔。既定では区域の大きさに応じて自動選択", Required = false },
                    ["waitFrames"] = new McpToolParameter { Type = "integer", Description = "撮影前に待つフレーム数。既定は 3、最大 30", Required = false },
                    ["filename"] = new McpToolParameter { Type = "string", Description = "任意のスクリーンショットのファイル名", Required = false }
                },
                Handler = args => TryScreenshot(() =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error(McpLocalization.Get("screenshot.error_game_unavailable"));
                    if (Camera.main == null || CameraController.Instance == null)
                        return CallToolResult.Error(McpLocalization.Get("screenshot.error_camera_unavailable"));

                    ValidateScreenshotRequest(args["filename"]?.ToString());
                    int maxCells = 3600;
                    var rect = WorldEditor.ResolveRectOrCamera(args, maxCells);
                    int width = rect["x2"] - rect["x1"] + 1;
                    int height = rect["y2"] - rect["y1"] + 1;
                    int cells = width * height;
                    if (cells > maxCells)
                        return CallToolResult.Error(McpLocalization.Format("screenshot.area_too_large", null, width, height, cells, maxCells));

                    int worldId = WorldEditor.ResolveWorldId(args);
                    int centerCell = Grid.XYToCell((rect["x1"] + rect["x2"]) / 2, (rect["y1"] + rect["y2"]) / 2);
                    if (Grid.IsValidCell(centerCell) && !ToolUtil.CellMatchesWorld(centerCell, worldId))
                        return CallToolResult.Error(McpLocalization.Format("screenshot.rect_world_mismatch", null, worldId));

                    string requestedView = args["view"]?.ToString();
                    string viewName = "current";
                    if (!string.IsNullOrWhiteSpace(requestedView))
                    {
                        if (!TryResolveOverlayView(requestedView, out viewName, out var mode))
                            return CallToolResult.Error(McpLocalization.Format("screenshot.unknown_view", null, requestedView, string.Join(", ", OverlayViewNames)));
                        ApplyOverlayMode(mode, false);
                    }

                    bool focusCamera = ToolUtil.GetBool(args, "focusCamera", true);
                    float padding = Math.Max(0f, ToolUtil.GetFloat(args, "paddingCells") ?? 1.5f);
                    if (focusCamera)
                    {
                        float centerX = (rect["x1"] + rect["x2"]) * 0.5f;
                        float centerY = (rect["y1"] + rect["y2"]) * 0.5f;
                        float aspect = Math.Max(0.5f, Screen.width / Math.Max(1f, (float)Screen.height));
                        float vertical = (height * 0.5f) + padding;
                        float horizontalAsVertical = ((width * 0.5f) + padding) / aspect;
                        float zoom = Math.Max(4f, Math.Max(vertical, horizontalAsVertical));
                        CameraController.Instance.SnapTo(new Vector3(centerX, centerY, -100f), zoom);
                    }

                    int autoStep = cells <= 180 ? 1 : cells <= 900 ? 2 : 5;
                    int step = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "step") ?? autoStep, 25));
                    bool includeCellLabels = ToolUtil.GetBool(args, "includeCellLabels", cells <= 180);
                    int waitFrames = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "waitFrames") ?? 3, 30));

                    var coordinateOverlay = new CoordinateGridOverlay.OverlayRequest
                    {
                        X1 = rect["x1"],
                        Y1 = rect["y1"],
                        X2 = rect["x2"],
                        Y2 = rect["y2"],
                        Step = step,
                        MajorEvery = Math.Max(step, 10),
                        ShowGrid = ToolUtil.GetBool(args, "showGrid", true),
                        ShowCoordinates = ToolUtil.GetBool(args, "showCoordinates", true),
                        IncludeCellLabels = includeCellLabels,
                        VisibleSeconds = Math.Max(1f, waitFrames / 30f + 1.5f)
                    };

                    string fileName = args["filename"]?.ToString();
                    if (string.IsNullOrWhiteSpace(fileName))
                        fileName = $"cycle_{GameUtil.GetCurrentCycle()}_coord_{rect["x1"]}_{rect["y1"]}_{rect["x2"]}_{rect["y2"]}_{System.DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png";
                    var screenshot = OverlaySwitchQueue.Enqueue("coordinate_grid", OverlayScreen.Instance?.GetMode() ?? OverlayModes.None.ID, false, true, fileName, waitFrames, coordinateOverlay);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["queued"] = true,
                        ["tool"] = "camera_coordinate_screenshot",
                        ["requestId"] = screenshot["requestId"],
                        ["status"] = screenshot["status"],
                        ["requested"] = screenshot["requested"],
                        ["view"] = viewName,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["size"] = new { width, height, cells },
                        ["coordinateOverlay"] = new
                        {
                            step,
                            showGrid = ToolUtil.GetBool(args, "showGrid", true),
                            showCoordinates = ToolUtil.GetBool(args, "showCoordinates", true),
                            includeCellLabels,
                            coordinateSystem = McpLocalization.Get("screenshot.coordinate_system")
                        },
                        ["screenshot"] = screenshot["screenshot"],
                        ["next"] = McpLocalization.Get("screenshot.coordinate_next")
                    }, McpJsonUtil.Settings));
                })
            };
        }

        private static CallToolResult TryScreenshot(Func<CallToolResult> capture)
        {
            try { return capture(); }
            catch (InvalidOperationException error) { return CallToolResult.Error(error.Message); }
        }
    }
}
