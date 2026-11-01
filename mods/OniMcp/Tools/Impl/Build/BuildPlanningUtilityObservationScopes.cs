using Newtonsoft.Json.Linq;
namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static PreviewValidationObservation UtilityPathObservation(JObject args, string scope, string coverage = "partial")
        {
            bool executionRequested = !IsDryRun(args);
            return new PreviewValidationObservation("utility_path", scope, coverage, executionRequested, executionRequested,
                "dryRun", "attempted", "safetyChecked", "existingSamePrefabCells", "repeatedPathCells",
                "success", "complete", "valid", "failed", "shouldFallback", "planned", "reusedExisting",
                "beforePresentCells", "afterPresentCells", "newPresentCells", "pathCells", "presentCells",
                "connectedCells", "networkConnected", "constructionComplete", "connectionPlanApplied", "flowVerified",
                "committed", "partial", "reasonCode");
        }

        private static PreviewValidationObservation PowerWiringObservation(JObject args, string scope, string coverage = "partial", bool enabled = true)
        {
            bool executionRequested = !IsDryRun(args);
            return new PreviewValidationObservation("automatic_power_wiring", scope, coverage, executionRequested && enabled,
                executionRequested && enabled, "enabled", "dryRun", "status", "planned", "reused", "failed", "autoDigQueued",
                "autoDigAlreadyQueued", "childIsError", "childSuccess", "childComplete", "childCommitted", "childDryRun",
                "countsScope", "pathCells");
        }
    }
}
