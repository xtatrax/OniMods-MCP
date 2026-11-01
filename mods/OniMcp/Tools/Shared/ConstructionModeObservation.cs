using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
namespace OniMcp.Tools
{
    internal sealed class ConstructionModeRequest
    {
        internal readonly bool InstantBuild;
        internal readonly string Origin;
        private ConstructionModeRequest(bool instantBuild, string origin) { InstantBuild = instantBuild; Origin = origin; }
        internal static ConstructionModeRequest Resolve(JObject args, ConstructionModeRequest inherited)
        {
            if (args?.Property("instantBuild") != null)
                return new ConstructionModeRequest(ToolUtil.GetBool(args, "instantBuild", false), "explicit");
            return inherited == null ? new ConstructionModeRequest(false, "default_normal")
                : new ConstructionModeRequest(inherited.InstantBuild, "active_write_scope");
        }
    }

    internal sealed class ConstructionModeObservation
    {
        internal readonly ConstructionModeRequest Request;
        internal readonly bool CurrentInstantBuild, NormalMaterialRules;
        internal bool MatchesRequestedPolicy => CurrentInstantBuild == Request.InstantBuild
            && NormalMaterialRules == !Request.InstantBuild;
        private ConstructionModeObservation(ConstructionModeRequest request, bool current, bool normalMaterials)
        { Request = request; CurrentInstantBuild = current; NormalMaterialRules = normalMaterials; }
        internal static ConstructionModeObservation Read(JObject args)
        {
            return new ConstructionModeObservation(ConstructionModeRequest.Resolve(args, WorldEditorTools.ActiveConstructionRequest),
                DebugHandler.InstantBuildMode, WorldEditorTools.EnforceNormalMaterialRules);
        }
        internal string ErrorText => McpLocalization.Get("construction_mode.future_preflight_unavailable");
        internal Dictionary<string, object> Details()
        {
            return new Dictionary<string, object>
            {
                ["assessmentState"] = "unknown", ["reasonCode"] = "construction_mode_unobserved",
                ["validationScope"] = "placement_mode_observation",
                ["requestedInstantBuild"] = Request.InstantBuild, ["requestModeOrigin"] = Request.Origin,
                ["currentInstantBuildMode"] = CurrentInstantBuild, ["normalMaterialRules"] = NormalMaterialRules,
                ["modeMatches"] = CurrentInstantBuild == Request.InstantBuild,
                ["materialPolicyMatches"] = NormalMaterialRules == !Request.InstantBuild,
                ["nativePlacementValid"] = null, ["availableNow"] = null
            };
        }
    }
}
