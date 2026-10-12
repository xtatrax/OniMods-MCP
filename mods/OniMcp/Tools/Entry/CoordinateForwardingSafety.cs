using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    internal static class CoordinateForwardingSafety
    {
        // Wrapper defaults must never turn an explicit child preview into execution.
        internal static bool TryValidate(JObject wrapper, JObject payload, out string error)
        {
            error = null;
            if (payload?["dryRun"]?.Value<bool>() == true && wrapper?["dryRun"]?.Value<bool>() == false)
                error = "Conflicting dryRun: payload requests a preview but the coordinate wrapper requests execution. No action was taken. Use matching flags, or define an areaId and call the target tool with dryRun=true.";
            else if (payload?["confirm"]?.Value<bool>() == false && wrapper?["confirm"]?.Value<bool>() == true)
                error = "Conflicting confirm: payload explicitly withholds confirmation. No action was taken. Use matching confirmation flags.";
            return error == null;
        }
    }
}
