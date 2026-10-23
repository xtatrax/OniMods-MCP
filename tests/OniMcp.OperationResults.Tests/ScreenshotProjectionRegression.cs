using System;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;

internal static class ScreenshotProjectionRegression
{
    internal static void Run()
    {
        int checks = 0;
        foreach (string shape in new[] { "plain", "switched", "coordinate" })
        {
            var image = new JObject { ["path"] = "/capture/fixture.png", ["url"] = "http://example.invalid/capture.png",
                ["readyAfterFrames"] = 3 };
            var receipt = new JObject { ["requestId"] = "fixture-request", ["status"] = "queued", ["screenshot"] = image };
            JObject native = shape == "switched" ? new JObject { ["queue"] = receipt } :
                shape == "coordinate" ? receipt : new JObject(image) { ["requestId"] = "fixture-request", ["status"] = "queued" };
            var projected = ScreenshotCaptureProjection.Build(shape, shape == "plain", CallToolResult.Text(native.ToString()));
            Require((string)projected["url"] == (string)image["url"] && (string)projected["path"] == (string)image["path"],
                shape + " preserves the actual image link", ref checks);
            Require((string)projected["requestId"] == "fixture-request" && (string)projected["status"] == "queued",
                shape + " preserves the exact pending receipt", ref checks);
            Require(JToken.DeepEquals(projected["result"], native), shape + " retains all native request context", ref checks);
            Require(projected["captured"] == null && projected["imageState"] == null,
                shape + " does not turn acceptance into frame or file proof", ref checks);
        }
        var error = ScreenshotCaptureProjection.Build("current", true, CallToolResult.Error(new string('e', 1200)));
        Require((bool)error["isError"] && ((string)error["text"]).Length == 903,
            "textual failure remains a bounded error", ref checks);
        Require(error["requestId"] == null && error["url"] == null, "error has no invented receipt or image", ref checks);
        var absent = ScreenshotCaptureProjection.Build("current", true, null);
        Require((bool)absent["isError"] && absent["result"] == null, "missing result remains an error", ref checks);
        Console.WriteLine("Screenshot projection checks passed: " + checks);
    }

    private static void Require(bool condition, string message, ref int checks)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
