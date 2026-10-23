using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class CameraTools
    {
        private static void CaptureScreenshotFrame(OverlaySwitchRequest request)
        {
            Texture2D texture = null;
            try
            {
                RequireScreenshotOwner(request);
                int frame = Time.frameCount;
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                var captured = ObserveScreenshotContext("end_of_frame");
                if (texture == null || texture.width <= 0 || texture.height <= 0 || frame != Time.frameCount)
                    throw new ScreenshotCaptureException("capture_unavailable");
                var screen = (Dictionary<string, object>)captured["screen"];
                if (texture.width != (int)screen["width"] || texture.height != (int)screen["height"])
                    throw new ScreenshotCaptureException("capture_unavailable");
                byte[] png = ImageConversion.EncodeToPNG(texture);
                if (png == null || png.Length == 0) throw new ScreenshotCaptureException("capture_unavailable");
                RequireScreenshotOwner(request);
                WriteScreenshotAtomically(request, png);
                RequireScreenshotOwner(request);
                captured["requestedWorldMatches"] = (bool)request.Requested["worldAvailable"] && (bool)captured["worldAvailable"]
                    ? (object)Equals(request.Requested["activeWorldId"], captured["activeWorldId"]) : null;
                var actualOverlay = (Dictionary<string, object>)captured["overlay"];
                captured["requestedOverlayMatches"] = request.Requested["requestedOverlay"] != null && (bool)actualOverlay["available"] && actualOverlay["id"] != null
                    ? (object)Equals(request.Requested["requestedOverlay"], actualOverlay["id"]) : null;
                var image = new Dictionary<string, object>
                {
                    ["sha256"] = ScreenshotDigest(png), ["bytes"] = png.LongLength,
                    ["width"] = texture.width, ["height"] = texture.height,
                    ["writtenUtc"] = System.DateTime.UtcNow.ToString("O")
                };
                FinishScreenshot(request, captured, image);
            }
            finally
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }

        private static void WriteScreenshotAtomically(OverlaySwitchRequest request, byte[] png)
        {
            string temporary = request.ScreenshotPath + "." + request.Id + ".capturetmp";
            bool owned = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    owned = true;
                    stream.Write(png, 0, png.Length);
                    stream.Flush(true);
                }
                RequireScreenshotOwner(request);
                if (File.Exists(request.ScreenshotPath)) File.Replace(temporary, request.ScreenshotPath, null);
                else File.Move(temporary, request.ScreenshotPath);
            }
            finally
            {
                if (owned && File.Exists(temporary))
                {
                    try { File.Delete(temporary); } catch { }
                }
            }
        }

        private static string ScreenshotDigest(byte[] bytes)
        {
            using (var algorithm = SHA256.Create()) return ScreenshotHex(algorithm.ComputeHash(bytes));
        }

        private static string ScreenshotHex(byte[] digest) => BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();

        private static Dictionary<string, object> ScreenshotImageState(OverlaySwitchRequest request)
        {
            var result = new Dictionary<string, object> { ["imageAvailable"] = false, ["imageState"] = request.Screenshot ? "pending" : "not_requested" };
            if (!request.Screenshot) return result;
            if (request.Status == "failed") { result["imageState"] = "capture_failed"; return result; }
            if (request.Status != "complete" || request.Image == null) return result;
            try
            {
                if (!File.Exists(request.ScreenshotPath)) { result["imageState"] = "missing"; return result; }
                using (var stream = new FileStream(request.ScreenshotPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var algorithm = SHA256.Create())
                {
                    bool same = stream.Length == (long)request.Image["bytes"]
                        && ScreenshotHex(algorithm.ComputeHash(stream)) == (string)request.Image["sha256"];
                    result["imageAvailable"] = same;
                    result["imageState"] = same ? "verified" : "changed";
                }
            }
            catch { result["imageState"] = "read_failed"; }
            return result;
        }
    }
}
