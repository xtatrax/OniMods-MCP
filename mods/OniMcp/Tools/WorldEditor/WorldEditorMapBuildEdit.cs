using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private sealed class MapBuildGroupPlan
        {
            internal string Token, PrefabId, Material;
            internal char Symbol;
            internal int Priority, WorldId, Cells, OriginalCells;
            internal JObject Args;
            internal JArray Anchors;
            internal List<MapEditAnchorRequest> Requests, OriginalRequests;
            internal CallToolResult Preflight;
        }

        private static CallToolResult ApplyBuildMapEdit(JObject parentArgs, IEnumerable<MapEditCell> cells, IEnumerable<MapEditCell> originalCells)
        {
            // Resolve every selected group before invoking any child. Outer map
            // dry-run still exits in its existing syntax/shape-only dispatcher.
            var originals = originalCells.ToList();
            var plans = new List<MapBuildGroupPlan>();
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
                var originalGroup = originals.Where(cell => cell.ToToken == group.Key).ToList();
                var originalRequests = requests;
                if (originalGroup.Count != group.Count() && !TryBuildAnchorsForPrefabFootprints(parentArgs, prefabId,
                    originalGroup, out _, out originalRequests, out _)) originalRequests = null;
                var buildArgs = CopyPayload(parentArgs);
                int buildWorldId = ToolUtil.ResolveWorldId(buildArgs);
                foreach (string selector in new[] {
                    "x", "y", "x1", "y1", "x2", "y2", "cell", "cells", "id",
                    "points", "fromX", "fromY", "toX", "toY", "fromId", "toId",
                    "fromQuery", "toQuery", "sourceQuery", "targetQuery",
                    "query", "target", "search", "name", "nearX", "nearY",
                    "areaId", "relative", "offsetX", "offsetY", "dx", "dy",
                    "plan", "blueprint", "sequence", "text", "steps", "items",
                    "dense", "stepX", "stepY"
                }) buildArgs.Remove(selector);
                buildArgs["worldId"] = buildWorldId;
                buildArgs["domain"] = "planning";
                buildArgs["action"] = "build_area";
                buildArgs["prefabId"] = prefabId;
                buildArgs["anchors"] = anchors;
                buildArgs["priority"] = priority.Value;
                buildArgs["confirm"] = ToolUtil.GetBool(parentArgs, "confirm", false);
                if (!string.IsNullOrWhiteSpace(material)) buildArgs["material"] = material;
                plans.Add(new MapBuildGroupPlan
                {
                    Token = group.Key, Symbol = buildSymbol, PrefabId = prefabId,
                    Priority = priority.Value, Material = material, WorldId = buildWorldId,
                    Cells = group.Count(), OriginalCells = originalGroup.Count,
                    Args = buildArgs, Anchors = anchors, Requests = requests, OriginalRequests = originalRequests
                });
            }

            // Stable support-first ordering; only selected groups participate.
            plans = plans.OrderByDescending(plan => BuildPlanningTools.IsRequestSupportPrefab(plan.PrefabId)).ToList();
            var support = new BuildPlanningRequestSupportContext(plans.FirstOrDefault()?.WorldId ?? -1);
            bool preflightFailed = false;
            foreach (var plan in plans)
            {
                var preview = (JObject)plan.Args.DeepClone();
                preview["dryRun"] = true;
                preview["confirm"] = true;
                plan.Preflight = BuildingControlTools.ControlBuildingFromVirtualFile(preview, support);
                preflightFailed |= WorldEditorResultFailed(plan.Preflight, parentArgs);
            }
            if (preflightFailed && !ToolUtil.GetBool(parentArgs, "allowPartial", false))
            {
                var rejected = new MapCellProgress { Requested = originals.Count, Changed = false };
                return CallToolResult.Error(JsonResultText(new JObject
                {
                    ["ok"] = false, ["committed"] = false, ["applied"] = 0,
                    ["appliedUnit"] = "requested_map_cells", ["cellProgress"] = rejected.ToJson(),
                    ["changed"] = false, ["completionKnown"] = true, ["partial"] = false,
                    ["failed"] = 1, ["reasonCode"] = "map_request_preflight_failed",
                    ["executionStatus"] = "not_started", ["remainingCells"] = originals.Count,
                    ["error"] = McpLocalization.Get("map_request_support.preflight_failed"),
                    ["results"] = new JArray(plans.Select(plan => new JObject
                    {
                        ["token"] = plan.Token, ["prefabId"] = plan.PrefabId,
                        ["cells"] = plan.Cells, ["executed"] = false,
                        ["preflightOk"] = !WorldEditorResultFailed(plan.Preflight, parentArgs),
                        ["preflightResult"] = plan.Preflight?.Content?.FirstOrDefault()?.Text ?? string.Empty
                    }))
                }));
            }

            var results = new JArray();
            var progressItems = new List<MapCellProgress>();
            bool anyError = false, partial = false;
            foreach (var plan in plans)
            {
                // No future dependency set crosses into actual execution. This
                // child rereads existing native supports/blueprints after earlier
                // support writes; failed/deferred/auto-dig-only floors confer none.
                var result = BuildingControlTools.ControlBuildingFromVirtualFile(plan.Args);
                bool failed = WorldEditorResultFailed(result, parentArgs);
                anyError |= failed;
                var progress = CorrelateOriginalBuildProgress(result, parentArgs, plan.PrefabId,
                    plan.Requests, plan.OriginalRequests, plan.OriginalCells);
                progressItems.Add(progress);
                partial |= NativeRefusalReportsPartial(result, progress.Changed == false
                    && progress.KnownSatisfied == 0 && progress.Unknown == 0);
                results.Add(new JObject
                {
                    ["token"] = plan.Token, ["symbol"] = plan.Symbol.ToString(),
                    ["prefabId"] = plan.PrefabId, ["priority"] = plan.Priority,
                    ["material"] = plan.Material, ["anchors"] = plan.Anchors,
                    ["cells"] = plan.Cells, ["requestedAnchors"] = plan.Requests.Count,
                    ["executed"] = true, ["preflightOk"] = !WorldEditorResultFailed(plan.Preflight, parentArgs),
                    ["preflightResult"] = plan.Preflight?.Content?.FirstOrDefault()?.Text ?? string.Empty,
                    ["cellProgress"] = progress.ToJson(), ["changed"] = progress.Changed,
                    ["nativeOutcome"] = NativeOutcome(result), ["ok"] = !failed,
                    ["error"] = failed ? result?.Content?.FirstOrDefault()?.Text ?? string.Empty : string.Empty,
                    ["result"] = result?.Content?.FirstOrDefault()?.Text ?? string.Empty
                });
            }
            MapCellProgress total = SumCellProgress(originals.Count, progressItems);
            var summary = new JObject
            {
                ["ok"] = !anyError, ["applied"] = total.Unknown == 0 ? new JValue(total.KnownSatisfied) : JValue.CreateNull(),
                ["appliedUnit"] = "requested_map_cells", ["cellProgress"] = total.ToJson(),
                ["changed"] = total.Changed, ["completionKnown"] = total.Unknown == 0,
                ["executionStatus"] = total.Unknown > 0 ? "unknown"
                    : total.KnownSatisfied == total.Requested ? "complete"
                    : total.KnownSatisfied > 0 ? "partial" : "not_completed",
                ["remainingCells"] = total.Requested - total.KnownSatisfied,
                ["partial"] = partial || total.KnownSatisfied > 0 && total.KnownSatisfied < total.Requested
                    || anyError && (total.KnownSatisfied > 0 || total.Changed == true),
                ["failed"] = anyError ? 1 : 0, ["results"] = results
            };
            return anyError ? CallToolResult.Error(JsonResultText(summary)) : JsonResult(summary);
        }
    }
}
