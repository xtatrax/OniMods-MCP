using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OniMcp.Tools
{
    public static partial class CameraTools
    {
        private const int ScreenshotQueueLimit = 16;
        private const int ScreenshotReceiptLimit = 64;
        private const double ScreenshotDeadlineSeconds = 60;
        private static readonly Dictionary<string, OverlaySwitchRequest> ScreenshotRequests = new Dictionary<string, OverlaySwitchRequest>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ScreenshotPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<string> CompletedScreenshots = new Queue<string>();

        private sealed class ScreenshotCaptureException : InvalidOperationException
        {
            internal readonly string Code;
            internal ScreenshotCaptureException(string code) : base(McpLocalization.Get("screenshot.error_" + code)) { Code = code; }
        }

        public static void ValidateScreenshotRequest(string fileName, bool screenshot = true)
        {
            RequireScreenshotEnvironment();
            if (OverlaySwitchQueue.instance != null && !OverlaySwitchQueue.instance.isActiveAndEnabled)
                throw new ScreenshotCaptureException("host_unavailable");
            if (ScreenshotRequests.Values.Count(request => !ScreenshotTerminal(request)) >= ScreenshotQueueLimit)
                throw new ScreenshotCaptureException("queue_full");
            if (screenshot && !string.IsNullOrEmpty(fileName) && ScreenshotPaths.ContainsKey(ScreenshotTargetPath(fileName)))
                throw new ScreenshotCaptureException("filename_busy");
        }

        public static Dictionary<string, object> QueueScreenshot(string fileName, int waitFrames = 2)
        {
            var queue = OverlaySwitchQueue.Enqueue("current", OverlayModes.None.ID, false, true, fileName, waitFrames);
            var result = new Dictionary<string, object>(queue);
            foreach (var entry in JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(queue["screenshot"], McpJsonUtil.Settings))) result[entry.Key] = entry.Value;
            return ScreenshotClone(result);
        }

        public static CallToolResult ScreenshotStatus(string requestId)
        {
            Guid parsed;
            if (string.IsNullOrWhiteSpace(requestId) || !Guid.TryParseExact(requestId, "N", out parsed))
                return CallToolResult.Error(McpLocalization.Get("screenshot.error_request_id"));
            OverlaySwitchRequest request;
            Dictionary<string, object> result;
            if (!ScreenshotRequests.TryGetValue(requestId, out request))
                result = new Dictionary<string, object> { ["requestId"] = requestId, ["status"] = "unknown", ["captured"] = null, ["imageAvailable"] = false };
            else
            {
                result = ScreenshotReceipt(request, request.Status);
                result["captured"] = request.Captured;
                result["image"] = request.Image;
                result["error"] = request.Error;
                foreach (var item in ScreenshotImageState(request)) result[item.Key] = item.Value;
            }
            return CallToolResult.Text(JsonConvert.SerializeObject(ScreenshotClone(result), McpJsonUtil.Settings));
        }

        private static void RequireScreenshotEnvironment()
        {
            if (Game.Instance == null || Grid.WidthInCells <= 0 || Grid.HeightInCells <= 0)
                throw new ScreenshotCaptureException("game_unavailable");
            if (Camera.main == null || CameraController.Instance == null)
                throw new ScreenshotCaptureException("camera_unavailable");
            if (!SceneManager.GetActiveScene().isLoaded)
                throw new ScreenshotCaptureException("context_changed");
        }

        private static void RequireScreenshotOwner(OverlaySwitchRequest request)
        {
            RequireScreenshotEnvironment();
            if (!ReferenceEquals(request.Game, Game.Instance) || request.SceneHandle != SceneManager.GetActiveScene().handle)
                throw new ScreenshotCaptureException("context_changed");
            if (ScreenshotTerminal(request) || Time.realtimeSinceStartupAsDouble >= request.Deadline) throw new ScreenshotCaptureException("timeout");
        }

        private static bool ScreenshotTerminal(OverlaySwitchRequest request) => request.Status == "complete" || request.Status == "failed";
        internal static bool ScreenshotPathReserved(string path) => ScreenshotPaths.ContainsKey(Path.GetFullPath(path));

        private static string ScreenshotTargetPath(string fileName)
        {
            string name = Path.GetFileName(fileName);
            if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) name += ".png";
            return Path.GetFullPath(Path.Combine(ScreenshotDirectory, name));
        }

        private static void ReserveScreenshot(OverlaySwitchRequest request)
        {
            if (request.Screenshot)
            {
                if (ScreenshotPaths.ContainsKey(request.ScreenshotPath)) throw new ScreenshotCaptureException("filename_busy");
                ScreenshotPaths.Add(request.ScreenshotPath, request.Id);
            }
            ScreenshotRequests.Add(request.Id, request);
        }

        private static void FinishScreenshot(OverlaySwitchRequest request, Dictionary<string, object> captured = null,
            Dictionary<string, object> image = null, Exception failure = null)
        {
            if (ScreenshotTerminal(request)) return;
            request.Status = failure == null ? "complete" : "failed";
            request.Game = null;
            request.Captured = failure == null ? captured : null;
            request.Image = failure == null ? image : null;
            if (failure != null)
            {
                var known = failure as ScreenshotCaptureException;
                string code = known == null ? "capture_failed" : known.Code;
                request.Error = new Dictionary<string, object> { ["code"] = code, ["message"] = McpLocalization.Get("screenshot.error_" + code), ["exceptionType"] = failure.GetType().Name };
            }
            string owner;
            if (request.ScreenshotPath != null && ScreenshotPaths.TryGetValue(request.ScreenshotPath, out owner) && owner == request.Id)
                ScreenshotPaths.Remove(request.ScreenshotPath);
            CompletedScreenshots.Enqueue(request.Id);
            while (CompletedScreenshots.Count > ScreenshotReceiptLimit)
            {
                string oldest = CompletedScreenshots.Dequeue();
                OverlaySwitchRequest retained;
                if (ScreenshotRequests.TryGetValue(oldest, out retained) && ScreenshotTerminal(retained)) ScreenshotRequests.Remove(oldest);
            }
        }

        private static Dictionary<string, object> ScreenshotReceipt(OverlaySwitchRequest request, string status)
        {
            var result = new Dictionary<string, object>
            {
                ["id"] = request.Id, ["requestId"] = request.Id, ["status"] = status,
                ["queued"] = status == "queued" || status == "running", ["requested"] = request.Requested,
                ["receiptPhase"] = status == "queued" ? "enqueue" : "status", ["screenshotRequested"] = request.Screenshot
            };
            if (request.Screenshot)
            {
                var requestedClock = (Dictionary<string, object>)request.Requested["clock"];
                result["path"] = request.ScreenshotPath;
                result["url"] = WorldEditor.ScreenshotUrl(request.ScreenshotPath);
                result["latestUrl"] = OniMcp.Config.OniMcpOptions.Current.ScreenshotLatestUrl;
                result["cycle"] = requestedClock["cycle"];
                result["screen"] = request.Requested["screen"];
                result["enqueueSemantics"] = "request_time";
                result["readyAfterFrames"] = request.WaitFrames + 1;
                result["readyEstimateOnly"] = true;
                result["cleanup"] = request.Cleanup;
            }
            return result;
        }

        private static Dictionary<string, object> ScreenshotClone(Dictionary<string, object> value)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
        }
    }
}
