using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    internal static class ScreenshotCaptureProjection
    {
        internal static JObject Build(string view, bool current, CallToolResult result)
        {
            var item = new JObject
            {
                ["view"] = view,
                ["source"] = current ? "current_overlay" : "switched_overlay",
                ["isError"] = result == null || result.IsError
            };

            string text = result?.Content?.FirstOrDefault()?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return item;

            try
            {
                var parsed = JObject.Parse(text);
                item["result"] = parsed;
                var screenshot = parsed["screenshot"] ?? parsed["queue"]?["screenshot"] ?? parsed;
                item["screenshot"] = screenshot;
                item["requestId"] = parsed["requestId"] ?? parsed["queue"]?["requestId"] ?? screenshot["requestId"];
                item["status"] = parsed["status"] ?? parsed["queue"]?["status"] ?? screenshot["status"];
                item["url"] = screenshot["url"];
                item["path"] = screenshot["path"];
                item["readyAfterFrames"] = screenshot["readyAfterFrames"] ?? parsed["readyAfterFrames"];
            }
            catch
            {
                item["text"] = text.Length <= 900 ? text : text.Substring(0, 900) + "...";
            }
            return item;
        }

    }
}
