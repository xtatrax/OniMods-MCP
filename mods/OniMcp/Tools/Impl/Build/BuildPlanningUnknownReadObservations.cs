using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static Dictionary<string, object> FacadeSelectionErrorDetails(FacadeSelection selection)
        {
            if (selection.AssessmentState != "unknown") return null;
            return new Dictionary<string, object>
            {
                ["assessmentState"] = "unknown", ["reasonCode"] = "facade_read_unavailable",
                ["facadeSelection"] = selection.ToDictionary(),
                ["availableNow"] = JValue.CreateNull(), ["nativePlacementValid"] = JValue.CreateNull()
            };
        }

        private static Dictionary<string, object> GeometryObservationFailureDetails(PlacementDetails placement,
            Dictionary<string, object> details)
        {
            // These are producer-defined observation failures, not request/bounds failures.
            switch (placement.GeometryError)
            {
                case "placement_offsets_missing":
                case "geometry_read_failed":
                case "native_footprint_mismatch":
                    var unknown = new Dictionary<string, object>(details);
                    unknown["assessmentState"] = "unknown";
                    unknown["reasonCode"] = "geometry_observation_unavailable";
                    unknown["geometryReasonCode"] = placement.GeometryError;
                    unknown["availableNow"] = JValue.CreateNull();
                    unknown["nativePlacementValid"] = JValue.CreateNull();
                    return unknown;
                default: return details;
            }
        }
    }
}
