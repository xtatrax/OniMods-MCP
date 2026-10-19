using System;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static bool TryBuildAutoConnectArgs(JObject args, string prefabId, out JObject connectArgs)
        {
            connectArgs = args == null ? new JObject() : (JObject)args.DeepClone();
            // Explicit anchors describe a placement set, including invalid or empty
            // input for the caller to reject. They never imply a connecting route.
            if (args?.Property("anchors") != null)
                return false;

            connectArgs["prefabId"] = prefabId;
            connectArgs["action"] = "auto_connect";

            if (connectArgs["points"] != null)
                return true;

            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? x1 = ToolUtil.GetInt(args, "x1");
            int? y1 = ToolUtil.GetInt(args, "y1");
            int? x2 = ToolUtil.GetInt(args, "x2");
            int? y2 = ToolUtil.GetInt(args, "y2");

            if (x.HasValue && y.HasValue && x2.HasValue && y2.HasValue)
            {
                connectArgs["fromX"] = x.Value;
                connectArgs["fromY"] = y.Value;
                connectArgs["toX"] = x2.Value;
                connectArgs["toY"] = y2.Value;
                return true;
            }

            if (x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue)
            {
                connectArgs["fromX"] = x1.Value;
                connectArgs["fromY"] = y1.Value;
                connectArgs["toX"] = x2.Value;
                connectArgs["toY"] = y2.Value;
                return true;
            }

            if (HasAutoConnectQuery(args))
                return true;

            return false;
        }

        private static bool HasAutoConnectQuery(JObject args)
        {
            foreach (string key in new[] { "toQuery", "targetQuery", "fromQuery", "sourceQuery", "query", "target", "search", "name" })
                if (!string.IsNullOrWhiteSpace(args?[key]?.ToString()))
                    return true;
            return false;
        }

    }
}
