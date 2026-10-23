using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OniMcp.Tools
{
    public static partial class CameraTools
    {
        private static Dictionary<string, object> ObserveScreenshotContext(string phase)
        {
            RequireScreenshotEnvironment();
            var camera = Camera.main;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            var scene = SceneManager.GetActiveScene();
            var clock = ScreenshotClock();
            var overlay = OverlayScreen.Instance;
            HashedString? observedMode = overlay == null ? (HashedString?)null : overlay.GetMode();
            string overlayView = null;
            if (observedMode.HasValue)
            {
                foreach (string candidate in OverlayViewNames)
                {
                    string canonical;
                    HashedString mode;
                    if (TryResolveOverlayView(candidate, out canonical, out mode) && observedMode.Value == mode)
                    {
                        overlayView = canonical;
                        break;
                    }
                }
            }
            var viewport = ScreenshotViewport(camera);
            int? world = ClusterManager.Instance == null ? (int?)null : ClusterManager.Instance.activeWorldId;
            if (world.HasValue && world.Value < 0) world = null;
            var projection = camera.projectionMatrix;
            var rows = new List<double?[]>();
            for (int y = 0; y < 4; y++)
                rows.Add(new[] { ScreenshotNumber(projection[y, 0]), ScreenshotNumber(projection[y, 1]), ScreenshotNumber(projection[y, 2]), ScreenshotNumber(projection[y, 3]) });
            Rect pixel = camera.pixelRect;
            bool cameraAvailable = ScreenshotVector(position).Values.All(value => value != null)
                && new[] { rotation.x, rotation.y, rotation.z, rotation.w, camera.orthographicSize, camera.aspect,
                    camera.fieldOfView, pixel.x, pixel.y, pixel.width, pixel.height }.All(value => ScreenshotNumber(value).HasValue)
                && rows.All(row => row.All(value => value.HasValue));
            Vector3? controller = CameraController.Instance == null ? (Vector3?)null : CameraController.Instance.transform.position;
            return new Dictionary<string, object>
            {
                ["frame"] = Time.frameCount, ["utc"] = System.DateTime.UtcNow.ToString("O"),
                ["realtimeSeconds"] = Time.realtimeSinceStartupAsDouble,
                ["capturePhase"] = phase, ["imageScope"] = "composed_screen",
                ["activeWorldId"] = world, ["worldAvailable"] = world.HasValue,
                ["scene"] = new { handle = (int)scene.handle, name = scene.name, loaded = scene.isLoaded },
                ["overlay"] = new Dictionary<string, object>
                {
                    ["available"] = observedMode.HasValue, ["id"] = observedMode.HasValue ? observedMode.Value.ToString() : null,
                    ["view"] = overlayView, ["viewAvailable"] = overlayView != null
                },
                ["camera"] = new Dictionary<string, object>
                {
                    ["available"] = cameraAvailable, ["source"] = "Camera.main", ["position"] = ScreenshotVector(position),
                    ["rotation"] = new { x = ScreenshotNumber(rotation.x), y = ScreenshotNumber(rotation.y), z = ScreenshotNumber(rotation.z), w = ScreenshotNumber(rotation.w) },
                    ["orthographic"] = camera.orthographic, ["orthographicSize"] = ScreenshotNumber(camera.orthographicSize),
                    ["aspect"] = ScreenshotNumber(camera.aspect), ["fieldOfView"] = ScreenshotNumber(camera.fieldOfView), ["projection"] = rows,
                    ["pixelRect"] = new { x = ScreenshotNumber(pixel.x), y = ScreenshotNumber(pixel.y), width = ScreenshotNumber(pixel.width), height = ScreenshotNumber(pixel.height) },
                    ["controllerPosition"] = controller.HasValue ? ScreenshotVector(controller.Value) : null
                },
                ["screen"] = new Dictionary<string, object> { ["width"] = Screen.width, ["height"] = Screen.height },
                ["clock"] = clock, ["viewport"] = viewport,
                ["contextComplete"] = cameraAvailable && world.HasValue && overlay != null && (bool)clock["available"] && (bool)viewport["available"]
            };
        }

        private static Dictionary<string, object> ScreenshotClock()
        {
            var result = new Dictionary<string, object> { ["available"] = false, ["cycle"] = null, ["timeOfDayPercent"] = null, ["gameTimeSeconds"] = null };
            if (GameClock.Instance == null) { result["reason"] = "clock_unavailable"; return result; }
            try
            {
                result["cycle"] = GameUtil.GetCurrentCycle();
                result["timeOfDayPercent"] = ScreenshotNumber(GameClock.Instance.GetCurrentCycleAsPercentage() * 100);
                result["gameTimeSeconds"] = ScreenshotNumber(GameClock.Instance.GetTime());
                result["available"] = result["timeOfDayPercent"] != null && result["gameTimeSeconds"] != null;
                if (!(bool)result["available"]) result["reason"] = "nonfinite_clock";
            }
            catch { result["reason"] = "clock_read_failed"; }
            return result;
        }

        private static Dictionary<string, object> ScreenshotViewport(Camera camera)
        {
            var result = new Dictionary<string, object>
            {
                ["available"] = false, ["approximateCellBounds"] = true,
                ["scope"] = "camera_world_plane_aabb", ["visibility"] = "not_fog_or_ui_occlusion"
            };
            try
            {
                var plane = new Plane(Vector3.forward, Vector3.zero);
                var corners = new List<Vector3>();
                foreach (var point in new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0) })
                {
                    Ray ray = camera.ViewportPointToRay(point);
                    float distance;
                    if (!plane.Raycast(ray, out distance)) { result["reason"] = "world_plane_unavailable"; return result; }
                    Vector3 world = ray.GetPoint(distance);
                    if (!ScreenshotNumber(world.x).HasValue || !ScreenshotNumber(world.y).HasValue) { result["reason"] = "projection_nonfinite"; return result; }
                    corners.Add(world);
                }
                result["worldCorners"] = corners.Select(ScreenshotVector).ToArray();
                result["cellBounds"] = new
                {
                    x1 = Mathf.Clamp(Mathf.RoundToInt(corners.Min(point => point.x)), 0, Grid.WidthInCells - 1),
                    y1 = Mathf.Clamp(Mathf.RoundToInt(corners.Min(point => point.y)), 0, Grid.HeightInCells - 1),
                    x2 = Mathf.Clamp(Mathf.RoundToInt(corners.Max(point => point.x)), 0, Grid.WidthInCells - 1),
                    y2 = Mathf.Clamp(Mathf.RoundToInt(corners.Max(point => point.y)), 0, Grid.HeightInCells - 1)
                };
                result["available"] = true;
            }
            catch { result["reason"] = "viewport_read_failed"; }
            return result;
        }

        private static Dictionary<string, object> ScreenshotVector(Vector3 value) => new Dictionary<string, object>
        { ["x"] = ScreenshotNumber(value.x), ["y"] = ScreenshotNumber(value.y), ["z"] = ScreenshotNumber(value.z) };
        private static double? ScreenshotNumber(float value) => float.IsNaN(value) || float.IsInfinity(value) ? (double?)null : value;
    }
}
