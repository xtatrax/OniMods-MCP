using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OniMcp.Config;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class CameraTools
    {
        public static Dictionary<string, object> CleanupTemporaryScreenshots()
        {
            return CleanupTemporaryScreenshots(0);
        }

        private static Dictionary<string, McpToolParameter> CameraControlParams()
        {
            return new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter
                {
                    Type = "string",
                    Description = McpLocalization.Get("screenshot.camera_actions"),
                    Required = true,
                    EnumValues = new List<string> { "get_view", "set_active_world", "set_view", "move", "switch_view", "focus_cell", "focus_dupe", "screenshot", "coordinate_screenshot", "screenshot_status" }
                },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。set_active_world では必須。その他の action では現在アクティブなワールドが既定", Required = false },
                ["requireDiscovered"] = new McpToolParameter { Type = "boolean", Description = "set_active_world: 対象ワールドが発見済みであることを要求するか。既定は true", Required = false },
                ["lookAtSurface"] = new McpToolParameter { Type = "boolean", Description = "set_active_world: 未訪問のワールドで LookAtSurface を呼び出すか。既定は true", Required = false },
                ["x"] = new McpToolParameter { Type = "number", Description = "set_view/move jump の対象 X、focus_cell のセル X。座標による位置指定にも使用可能", Required = false },
                ["y"] = new McpToolParameter { Type = "number", Description = "set_view/move jump の対象 Y、focus_cell のセル Y。座標による位置指定にも使用可能", Required = false },
                ["zoom"] = new McpToolParameter { Type = "number", Description = "カメラの平行投影倍率。省略時は各 action の既定値を使用", Required = false },
                ["snap"] = new McpToolParameter { Type = "boolean", Description = "set_view/move: 即座に移動するか", Required = false },
                ["mode"] = new McpToolParameter { Type = "string", Description = "move: pan=dx/dy で相対移動、jump=x/y のワールド座標へ移動", Required = false, EnumValues = new List<string> { "pan", "jump" } },
                ["dx"] = new McpToolParameter { Type = "number", Description = "move pan: X 方向の移動量。既定は 0", Required = false },
                ["dy"] = new McpToolParameter { Type = "number", Description = "move pan: Y 方向の移動量。既定は 0", Required = false },
                ["duration"] = new McpToolParameter { Type = "number", Description = "move: 滑らかな移動にかける秒数。既定は 0.5", Required = false },
                ["view"] = new McpToolParameter { Type = "string", Description = "switch_view/coordinate_screenshot のレイヤービュー", Required = false, EnumValues = OverlayViewNames },
                ["screenshot"] = new McpToolParameter { Type = "boolean", Description = "switch_view: 切り替え後のスクリーンショットを保存するか。既定は true", Required = false },
                ["filename"] = new McpToolParameter { Type = "string", Description = "screenshot/switch_view/coordinate_screenshot: 任意のスクリーンショットのファイル名", Required = false },
                ["waitFrames"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("screenshot.wait_frames"), Required = false },
                ["requestId"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("screenshot.request_id"), Required = false },
                ["allowSound"] = new McpToolParameter { Type = "boolean", Description = "switch_view: ビュー切り替え効果音を再生するか。既定は false", Required = false },
                ["id"] = new McpToolParameter { Type = "integer", Description = "focus_dupe: 複製人間の InstanceID", Required = false },
                ["name"] = new McpToolParameter { Type = "string", Description = "focus_dupe: 複製人間の名前", Required = false },
                ["areaId"] = new McpToolParameter { Type = "string", Description = "coordinate_screenshot: 区域ハンドル。指定すると x1/y1/x2/y2/worldId を省略可能", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "coordinate_screenshot: 区域の左下の X", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "coordinate_screenshot: 区域の左下の Y", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "coordinate_screenshot: 区域の右上の X", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "coordinate_screenshot: 区域の右上の Y", Required = false },
                ["focusCamera"] = new McpToolParameter { Type = "boolean", Description = "coordinate_screenshot: 区域全体が映るようにカメラを自動で移動するか。既定は true", Required = false },
                ["paddingCells"] = new McpToolParameter { Type = "number", Description = "coordinate_screenshot: 自動で対象に合わせる際の周囲の余白セル数。既定は 1.5", Required = false },
                ["showGrid"] = new McpToolParameter { Type = "boolean", Description = "coordinate_screenshot: グリッド線を表示するか。既定は true", Required = false },
                ["showCoordinates"] = new McpToolParameter { Type = "boolean", Description = "coordinate_screenshot: 端に x/y 座標を表示するか。既定は true", Required = false },
                ["includeCellLabels"] = new McpToolParameter { Type = "boolean", Description = "coordinate_screenshot: セルの中央に間隔をあけて x,y を表示するか", Required = false },
                ["step"] = new McpToolParameter { Type = "integer", Description = "coordinate_screenshot: 座標ラベルの表示間隔", Required = false }
            };
        }

        private static bool TryResolveOverlayView(string requestedView, out string viewName, out HashedString mode)
        {
            viewName = NormalizeViewName(requestedView);
            switch (viewName)
            {
                case "none":
                case "normal":
                case "base":
                    viewName = "none";
                    mode = OverlayModes.None.ID;
                    return true;
                case "oxygen":
                case "gas":
                    viewName = "oxygen";
                    mode = OverlayModes.Oxygen.ID;
                    return true;
                case "power":
                case "electric":
                case "electrical":
                    viewName = "power";
                    mode = OverlayModes.Power.ID;
                    return true;
                case "gas_conduits":
                case "gas_pipe":
                case "gas_pipes":
                    viewName = "gas_conduits";
                    mode = OverlayModes.GasConduits.ID;
                    return true;
                case "liquid_conduits":
                case "liquid_pipe":
                case "liquid_pipes":
                case "plumbing":
                    viewName = "liquid_conduits";
                    mode = OverlayModes.LiquidConduits.ID;
                    return true;
                case "solid_conveyor":
                case "shipping":
                case "conveyor":
                    viewName = "solid_conveyor";
                    mode = OverlayModes.SolidConveyor.ID;
                    return true;
                case "logic":
                case "automation":
                    viewName = "logic";
                    mode = OverlayModes.Logic.ID;
                    return true;
                case "temperature":
                case "temp":
                    viewName = "temperature";
                    mode = OverlayModes.Temperature.ID;
                    return true;
                case "heat_flow":
                case "heatflow":
                    viewName = "heat_flow";
                    mode = OverlayModes.HeatFlow.ID;
                    return true;
                case "thermal_conductivity":
                case "conductivity":
                    viewName = "thermal_conductivity";
                    mode = OverlayModes.ThermalConductivity.ID;
                    return true;
                case "materials":
                case "material":
                case "tile":
                case "tiles":
                    viewName = "materials";
                    mode = OverlayModes.TileMode.ID;
                    return true;
                case "light":
                    viewName = "light";
                    mode = OverlayModes.Light.ID;
                    return true;
                case "decor":
                    viewName = "decor";
                    mode = OverlayModes.Decor.ID;
                    return true;
                case "rooms":
                case "room":
                    viewName = "rooms";
                    mode = OverlayModes.Rooms.ID;
                    return true;
                case "priorities":
                case "priority":
                    viewName = "priorities";
                    mode = OverlayModes.Priorities.ID;
                    return true;
                case "disease":
                case "germs":
                    viewName = "disease";
                    mode = OverlayModes.Disease.ID;
                    return true;
                case "radiation":
                case "rad":
                    viewName = "radiation";
                    mode = OverlayModes.Radiation.ID;
                    return true;
                case "sound":
                case "noise":
                    viewName = "sound";
                    mode = OverlayModes.Sound.ID;
                    return true;
                case "suit":
                case "exosuit":
                case "atmo_suit":
                    viewName = "suit";
                    mode = OverlayModes.Suit.ID;
                    return true;
                case "crop":
                case "farming":
                    viewName = "crop";
                    mode = OverlayModes.Crop.ID;
                    return true;
                case "harvest":
                    viewName = "harvest";
                    mode = OverlayModes.Harvest.ID;
                    return true;
                default:
                    mode = default(HashedString);
                    return false;
            }
        }

        private static string NormalizeViewName(string view)
        {
            return view.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        }

        private sealed class OverlaySwitchRequest
        {
            public string Id;
            public string ViewName;
            public HashedString Mode;
            public bool AllowSound;
            public bool Screenshot;
            public string ScreenshotPath;
            public Dictionary<string, object> Cleanup;
            public int WaitFrames;
            public CoordinateGridOverlay.OverlayRequest CoordinateOverlay;
            public Game Game;
            public int SceneHandle;
            public double Deadline;
            public string Status = "queued";
            public Dictionary<string, object> Requested;
            public Dictionary<string, object> Captured;
            public Dictionary<string, object> Image;
            public Dictionary<string, object> Error;
        }

        private sealed class OverlaySwitchQueue : MonoBehaviour
        {
            internal static OverlaySwitchQueue instance;
            private readonly Queue<OverlaySwitchRequest> requests = new Queue<OverlaySwitchRequest>();
            private bool running;
            private int generation;
            private Coroutine runner;
            private OverlaySwitchRequest active;

            public static Dictionary<string, object> Enqueue(string viewName, HashedString mode, bool allowSound, bool screenshot, string fileName, int waitFrames, CoordinateGridOverlay.OverlayRequest coordinateOverlay = null)
            {
                ValidateScreenshotRequest(fileName, screenshot);
                var request = new OverlaySwitchRequest
                {
                    Id = Guid.NewGuid().ToString("N"), ViewName = viewName, Mode = mode,
                    AllowSound = allowSound, Screenshot = screenshot, WaitFrames = Math.Max(1, Math.Min(waitFrames, 30)),
                    CoordinateOverlay = coordinateOverlay, Game = Game.Instance,
                    SceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle,
                    Deadline = Time.realtimeSinceStartupAsDouble + ScreenshotDeadlineSeconds,
                    Requested = ObserveScreenshotContext("enqueue")
                };
                request.Requested["view"] = viewName;
                request.Requested["requestedOverlay"] = viewName == "current"
                    ? ((Dictionary<string, object>)request.Requested["overlay"])["id"] : mode.ToString();
                request.Requested["waitFrames"] = request.WaitFrames;
                if (coordinateOverlay != null)
                    request.Requested["coordinateOverlay"] = new
                    {
                        x1 = coordinateOverlay.X1, y1 = coordinateOverlay.Y1, x2 = coordinateOverlay.X2, y2 = coordinateOverlay.Y2,
                        step = coordinateOverlay.Step, showGrid = coordinateOverlay.ShowGrid,
                        showCoordinates = coordinateOverlay.ShowCoordinates, includeCellLabels = coordinateOverlay.IncludeCellLabels,
                        scope = "requested_configuration"
                    };
                if (screenshot) request.ScreenshotPath = PrepareScreenshotPath(fileName, viewName);
                EnsureInstance();
                ReserveScreenshot(request);
                try
                {
                    if (screenshot)
                    {
                        request.Cleanup = CleanupTemporaryScreenshots(1);
                        Directory.CreateDirectory(ScreenshotDirectory);
                    }
                    int queuePosition = instance.requests.Count + 1;
                    var queued = ScreenshotReceipt(request, "queued");
                    var result = new Dictionary<string, object>
                    {
                        ["id"] = request.Id, ["requestId"] = request.Id, ["status"] = "queued", ["queued"] = true,
                        ["position"] = queuePosition, ["requested"] = request.Requested,
                        ["screenshot"] = screenshot ? (object)queued : null
                    };
                    instance.requests.Enqueue(request);
                    instance.StartProcessing();
                    result["running"] = instance.running;
                    return ScreenshotClone(result);
                }
                catch (Exception error)
                {
                    FinishScreenshot(request, failure: error);
                    throw;
                }
            }

            private static void EnsureInstance()
            {
                if (instance != null) return;
                var obj = new GameObject("OniMcp_OverlaySwitchQueue");
                UnityEngine.Object.DontDestroyOnLoad(obj);
                instance = obj.AddComponent<OverlaySwitchQueue>();
            }

            private void StartProcessing()
            {
                if (running || requests.Count == 0 || !isActiveAndEnabled) return;
                running = true;
                int owner = ++generation;
                runner = StartCoroutine(ProcessQueue(owner));
            }

            private bool Owns(int owner, OverlaySwitchRequest request) => owner == generation && ReferenceEquals(active, request) && !ScreenshotTerminal(request);

            private IEnumerator ProcessQueue(int owner)
            {
                try
                {
                    while (owner == generation && requests.Count > 0)
                    {
                        var request = requests.Dequeue();
                        if (ScreenshotTerminal(request)) continue;
                        active = request;
                        request.Status = "running";
                        Exception setupFailure = null;
                        try
                        {
                            RequireScreenshotOwner(request);
                            if (request.ViewName != "coordinate_grid" && request.ViewName != "current")
                            {
                                if (OverlayScreen.Instance == null) throw new ScreenshotCaptureException("overlay_unavailable");
                                ApplyOverlayMode(request.Mode, request.AllowSound);
                            }
                            if (request.CoordinateOverlay != null) CoordinateGridOverlay.Show(request.CoordinateOverlay);
                        }
                        catch (Exception error) { setupFailure = error; }
                        if (setupFailure != null) { FinishScreenshot(request, failure: setupFailure); active = null; continue; }
                        if (request.Screenshot)
                        {
                            for (int i = 0; i < request.WaitFrames; i++) yield return null;
                            yield return new WaitForEndOfFrame();
                            if (!Owns(owner, request)) yield break;
                            try { CaptureScreenshotFrame(request); }
                            catch (Exception error) { FinishScreenshot(request, failure: error); }
                        }
                        else
                        {
                            yield return null;
                            if (!Owns(owner, request)) yield break;
                            try { RequireScreenshotOwner(request); FinishScreenshot(request, ObserveScreenshotContext("overlay_processed")); }
                            catch (Exception error) { FinishScreenshot(request, failure: error); }
                        }
                        active = null;
                    }
                }
                finally
                {
                    if (owner == generation) { running = false; runner = null; active = null; }
                }
            }

            private void Update()
            {
                double now = Time.realtimeSinceStartupAsDouble;
                foreach (var request in ScreenshotRequests.Values.Where(item => !ScreenshotTerminal(item) && now >= item.Deadline).ToArray())
                    FinishScreenshot(request, failure: new ScreenshotCaptureException("timeout"));
                if (active != null && ScreenshotTerminal(active)) StopProcessing();
                StartProcessing();
            }

            private void StopProcessing()
            {
                ++generation;
                if (runner != null) StopCoroutine(runner);
                runner = null; active = null; running = false;
            }

            private void CancelPending()
            {
                StopProcessing();
                foreach (var request in ScreenshotRequests.Values.Where(item => !ScreenshotTerminal(item)).ToArray())
                    FinishScreenshot(request, failure: new ScreenshotCaptureException("host_unavailable"));
                requests.Clear();
            }

            private void OnDisable() { if (ReferenceEquals(instance, this)) CancelPending(); }
            private void OnDestroy()
            {
                if (!ReferenceEquals(instance, this)) return;
                CancelPending(); instance = null;
            }
        }

        private static void ApplyOverlayMode(HashedString mode, bool allowSound)
        {
            var overlay = OverlayScreen.Instance;
            if (overlay == null)
                return;

            overlay.ToggleOverlay(mode, allowSound);
            if (Game.Instance != null)
                Game.Instance.ForceOverlayUpdate(true);
        }

        private static string PrepareScreenshotPath(string fileName, string viewName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                var clock = ScreenshotClock();
                string prefix = clock["cycle"] == null ? "capture" : "cycle_" + clock["cycle"];
                fileName = $"{prefix}_{NormalizeScreenshotName(viewName)}_{System.DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid().ToString("N").Substring(0, 8)}.png";
            }
            return ScreenshotTargetPath(fileName);
        }

        private static string NormalizeScreenshotName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "overlay";
            var chars = value.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '_')
                .ToArray();
            return new string(chars);
        }

        private static string ScreenshotDirectory
        {
            get
            {
                return Path.Combine(Path.GetTempPath(), "oni-mcp", "screenshots");
            }
        }

        private static Dictionary<string, object> CleanupTemporaryScreenshots(int reservedSlots)
        {
            var options = OniMcpOptions.Current;
            string dir = ScreenshotDirectory;
            var result = new Dictionary<string, object>
            {
                ["path"] = dir,
                ["enabled"] = options.ScreenshotCleanupEnabled,
                ["retentionMinutes"] = options.ScreenshotRetentionMinutes,
                ["maxFiles"] = options.ScreenshotMaxFiles,
                ["deletedFiles"] = 0,
                ["remainingFiles"] = 0,
                ["errors"] = new List<string>()
            };

            if (!options.ScreenshotCleanupEnabled)
                return result;

            if (!Directory.Exists(dir))
                return result;

            var errors = (List<string>)result["errors"];
            int deleted = 0;
            System.DateTime cutoff = System.DateTime.UtcNow.AddMinutes(-options.ScreenshotRetentionMinutes);
            var files = ListScreenshotFiles(dir, errors).Where(file => !ScreenshotPathReserved(file.FullName)).ToList();

            foreach (var file in files.Where(f => f.LastWriteTimeUtc < cutoff).ToList())
            {
                if (TryDeleteScreenshot(file, errors))
                    deleted++;
            }

            files = ListScreenshotFiles(dir, errors)
                .Where(file => !ScreenshotPathReserved(file.FullName))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            int keep = Math.Max(0, options.ScreenshotMaxFiles - Math.Max(0, reservedSlots));
            foreach (var file in files.Skip(keep).ToList())
            {
                if (TryDeleteScreenshot(file, errors))
                    deleted++;
            }

            result["deletedFiles"] = deleted;
            result["remainingFiles"] = ListScreenshotFiles(dir, errors).Count;
            return result;
        }

        private static List<FileInfo> ListScreenshotFiles(string dir, List<string> errors)
        {
            try
            {
                return Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly)
                    .Select(path => new FileInfo(path))
                    .Where(file => file.Exists)
                    .ToList();
            }
            catch (Exception ex)
            {
                AddCleanupError(errors, $"list screenshots failed: {ex.Message}");
                return new List<FileInfo>();
            }
        }

        private static bool TryDeleteScreenshot(FileInfo file, List<string> errors)
        {
            try
            {
                file.Delete();
                return true;
            }
            catch (Exception ex)
            {
                AddCleanupError(errors, $"{file.Name}: {ex.Message}");
                return false;
            }
        }

        private static void AddCleanupError(List<string> errors, string message)
        {
            if (errors.Count < 5)
                errors.Add(message);
        }
    }
}
