using System;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        // Output retention only. Match the existing mixed colony router without
        // changing dispatch, native arguments, eligibility or result contents.
        private static bool ShouldRetainColonyReadResult(string toolName, JObject arguments)
        {
            if (toolName != "colony_control" || arguments == null)
                return false;
            string domain = (arguments["domain"]?.ToString() ?? "read").Trim().ToLowerInvariant();
            string action = (arguments["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            if (domain == "bio" || domain == "life" || domain == "biology")
            {
                string bioDomain = (arguments["bioDomain"]?.ToString() ?? string.Empty).Trim();
                string kind = (arguments["kind"]?.ToString() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(bioDomain) && (kind == "farming" || kind == "ranching"))
                    bioDomain = kind;
                bioDomain = bioDomain.ToLowerInvariant();
                bool farming = bioDomain == "farming" || bioDomain == "farm"
                    || bioDomain == "planting" || bioDomain == "plants";
                return farming && (action == "seed_catalog" || action == "list_seeds" || action == "seeds"
                    || action == "list_planting" || action == "list");
            }
            if (domain == "management" || domain == "manage" || domain == "screen" || domain == "screens")
            {
                // An explicit empty kind shadows managementDomain in the native router.
                string kind = (arguments["kind"]?.ToString()
                    ?? arguments["managementDomain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                return (kind == "research" || kind == "tech") && action == "list";
            }
            return false;
        }
    }
}
