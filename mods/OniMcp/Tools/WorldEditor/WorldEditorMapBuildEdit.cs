using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static CallToolResult ApplyBuildMapEdit(JObject parentArgs, IEnumerable<MapEditCell> cells, IEnumerable<MapEditCell> originalCells)
        {
            var results = new JArray();
            bool anyError = false;
            var progressItems = new List<MapCellProgress>();
            bool partial = false;
            foreach (var group in cells.GroupBy(c => c.ToToken))
            {
                char buildSymbol;
                int? priority;
                string material;
                if (!ParseBuildToken(group.Key, out buildSymbol, out priority, out material))
                    return CallToolResult.Error(McpLocalization.Format("result_progress.map_build_invalid_token", null, group.Key));
                if (!priority.HasValue)
                    return CallToolResult.Error(McpLocalization.Format("result_progress.map_build_missing_priority", null, group.Key));

                string prefabId;
                if (!TryResolveBuildPrefabFromToken(group.Key, buildSymbol, out prefabId))
                    return CallToolResult.Error(McpLocalization.Format("result_progress.map_build_unknown_prefab", null, group.Key));

                string footprintError;
                JArray anchors;
                if (!TryBuildAnchorsForPrefabFootprints(parentArgs, prefabId, group, out anchors, out List<MapEditAnchorRequest> requests, out footprintError))
                    return CallToolResult.Error(footprintError);

                var originalGroup = originalCells.Where(cell => cell.ToToken == group.Key).ToList();
                var originalRequests = requests;
                if (originalGroup.Count != group.Count() && !TryBuildAnchorsForPrefabFootprints(parentArgs, prefabId,
                    originalGroup, out _, out originalRequests, out _)) originalRequests = null;

                var buildArgs = CopyPayload(parentArgs);
                buildArgs["domain"] = "planning";
                buildArgs["action"] = "build_area";
                buildArgs["prefabId"] = prefabId;
                buildArgs["anchors"] = anchors;
                buildArgs["priority"] = priority.Value;
                buildArgs["confirm"] = ToolUtil.GetBool(parentArgs, "confirm", false);
                if (!string.IsNullOrWhiteSpace(material))
                    buildArgs["material"] = material;

                var result = BuildingControlTools.ControlBuildingFromVirtualFile(buildArgs);
                bool failed = WorldEditorResultFailed(result, parentArgs);
                anyError = anyError || failed;
                int groupCells = group.Count();
                MapCellProgress progress = CorrelateOriginalBuildProgress(result, parentArgs, prefabId, requests,
                    originalRequests, originalGroup.Count);
                progressItems.Add(progress);
                partial = partial || NativeRefusalReportsPartial(result, progress.Changed == false
                    && progress.KnownSatisfied == 0 && progress.Unknown == 0);
                results.Add(new JObject
                {
                    ["token"] = group.Key,
                    ["symbol"] = buildSymbol.ToString(),
                    ["prefabId"] = prefabId,
                    ["priority"] = priority,
                    ["material"] = material,
                    ["anchors"] = anchors,
                    ["cells"] = groupCells,
                    ["requestedAnchors"] = requests.Count,
                    ["cellProgress"] = progress.ToJson(),
                    ["changed"] = progress.Changed,
                    ["nativeOutcome"] = NativeOutcome(result),
                    ["ok"] = !failed,
                    ["error"] = failed ? result?.Content?.FirstOrDefault()?.Text ?? string.Empty : string.Empty,
                    ["result"] = result?.Content?.FirstOrDefault()?.Text ?? string.Empty
                });
            }

            MapCellProgress total = SumCellProgress(originalCells.Count(), progressItems);
            var summary = new JObject
            {
                ["ok"] = !anyError, ["applied"] = total.Unknown == 0 ? new JValue(total.KnownSatisfied) : JValue.CreateNull(),
                ["appliedUnit"] = "requested_map_cells", ["cellProgress"] = total.ToJson(),
                ["changed"] = total.Changed, ["completionKnown"] = total.Unknown == 0,
                ["partial"] = partial || total.KnownSatisfied > 0 && total.KnownSatisfied < total.Requested
                    || anyError && (total.KnownSatisfied > 0 || total.Changed == true),
                ["failed"] = anyError ? 1 : 0, ["results"] = results
            };
            return anyError ? CallToolResult.Error(JsonResultText(summary)) : JsonResult(summary);
        }

    }
}
