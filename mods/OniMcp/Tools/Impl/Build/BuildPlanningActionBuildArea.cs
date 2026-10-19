using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        public static McpTool BuildArea()
        {
            var parameters = BuildPlacementParameters(includeConfirm: true, includeArea: true);
            parameters["anchors"] = new McpToolParameter { Type = "array", Description = "任意の anchor 一覧。各項目は {x,y} または [x,y]。x/y と x1/y1/x2/y2 より優先", Required = false };
            parameters["x1"] = new McpToolParameter { Type = "integer", Description = "区域の左下・始点 X。anchors がない場合は anchor の格子状配置を生成", Required = false };
            parameters["y1"] = new McpToolParameter { Type = "integer", Description = "区域の左下・始点 Y。anchors がない場合は anchor の格子状配置を生成", Required = false };
            parameters["x2"] = new McpToolParameter { Type = "integer", Description = "区域の右上・終点 X。anchors がない場合は anchor の格子状配置を生成", Required = false };
            parameters["y2"] = new McpToolParameter { Type = "integer", Description = "区域の右上・終点 Y。anchors がない場合は anchor の格子状配置を生成", Required = false };
            parameters["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。relative=true の場合は x/y または x1/y1/x2/y2 を rx/ry として解釈", Required = false };
            parameters["relative"] = new McpToolParameter { Type = "boolean", Description = "areaId と組み合わせて相対座標を使用", Required = false };
            parameters["dense"] = new McpToolParameter { Type = "boolean", Description = "anchors がない場合、各セルに anchor を配置するか。既定では 1x1 設備は true、複数セルの設備は占有範囲の大きさごとに配置", Required = false };
            parameters["stepX"] = new McpToolParameter { Type = "integer", Description = "anchors がない場合の anchor の X 間隔。既定は 1 または設備の幅", Required = false };
            parameters["stepY"] = new McpToolParameter { Type = "integer", Description = "anchors がない場合の anchor の Y 間隔。既定は 1 または設備の高さ", Required = false };
            parameters["maxAnchors"] = new McpToolParameter { Type = "integer", Description = "処理する anchors の数の上限。既定は 100、最大 500", Required = false };
            parameters["maxCommitAnchors"] = new McpToolParameter { Type = "integer", Description = "confirm=true で1回に実際に書き込む anchor 数の上限。既定は 32、最大 128。超過分は次回続行できるよう remainingAction を返し、大量の建築予定・採掘指示による描画負荷を避ける。", Required = false };
            parameters["allowPartial"] = new McpToolParameter { Type = "boolean", Description = "既定は false。建築実行前に1つでも anchor の事前検証が失敗するとバッチ全体を拒否。true では失敗した anchor をスキップ", Required = false };
            parameters["autoConnectPower"] = new McpToolParameter { Type = "boolean", Description = "電力を消費する設備の既定は true。自然地形を通る経路はallowUtilityTerrainDig=trueを明示した場合だけ許可し、本体の建設結果と分けて返す。false で自動配線を無効化可能", Required = false };
            parameters["maxAutoConnectRadius"] = new McpToolParameter { Type = "integer", Description = "autoConnectPower が最寄りの既存電線・電源出力を探す半径。既定は 80、最大 200", Required = false };

            return new McpTool
            {
                Name = "build_area",
                Group = "buildings",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "build_place_area", "build_blueprints" },
                Tags = new List<string> { "buildings", "batch", "area", "footprint", "建造", "批量" },
                Description = "anchor 一覧または区域に従って建築予定を一括配置。実行前に占有範囲、支え、素材、明らかな衝突を事前検証する。採掘可能な自然の固体には既定で先に採掘を指示し、dryRun=true ではプレビューのみを返す。",
                Parameters = parameters,
                Handler = args =>
                {
                    if (args.Property("anchors") != null
                        && (!(args["anchors"] is JArray explicitAnchors) || explicitAnchors.Count == 0))
                        return CallToolResult.Error(McpLocalization.Get("build_scope.anchors_required"));
                    if (!ToolUtil.GetBool(args, "confirm", false) && !ToolUtil.GetBool(args, "dryRun", false))
                        return CallToolResult.Error("confirm=true is required unless dryRun=true");

                    var planResolution = ResolveBuildPlan(args);
                    if (args["prefabId"] == null && !string.IsNullOrWhiteSpace(planResolution.PrefabId))
                        args["prefabId"] = planResolution.PrefabId;
                    if (args["material"] == null && !string.IsNullOrWhiteSpace(planResolution.Material))
                        args["material"] = planResolution.Material;
                    if (args["query"] == null && args["target"] == null && args["search"] == null && !string.IsNullOrWhiteSpace(planResolution.AnchorQuery))
                        args["query"] = planResolution.AnchorQuery;

                    string prefabId = args["prefabId"]?.ToString();
                    if (string.IsNullOrWhiteSpace(prefabId))
                        return CallToolResult.Error("prefabId is required, or provide plan/blueprint/sequence with a recognizable building name");
                string resolvedPrefabId;
                string resolveError;
                var def = ResolveBuildingDef(prefabId, out resolvedPrefabId, out resolveError);
                if (def == null)
                    return CallToolResult.Error(resolveError);
                prefabId = resolvedPrefabId;
                args["prefabId"] = prefabId;

                JObject connectArgs;
                if (IsLinearUtilityPrefab(def.PrefabID) && TryBuildAutoConnectArgs(args, def.PrefabID, out connectArgs))
                    return AutoConnectUtility().Handler(connectArgs);

                string error;
                var anchors = ResolveAnchors(args, def, out error);
                    if (error != null)
                        return CallToolResult.Error(error);
                    var anchorResolution = BuildAnchorResolution(args, anchors);

                    int maxAnchors = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "maxAnchors") ?? 100, 500));
                    if (anchors.Count > maxAnchors)
                        return CallToolResult.Error($"Refusing to process {anchors.Count} anchors; maxAnchors={maxAnchors}");

                    bool dryRun = IsDryRun(args);
            bool allowPartial = ToolUtil.GetBool(args, "allowPartial", false);
            int maxCommitAnchors = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "maxCommitAnchors") ?? 32, 128));
                    var preflightArgs = (JObject)args.DeepClone();
                    preflightArgs["dryRun"] = true;
                    preflightArgs["confirm"] = true;

                    var preflightSupport = new HashSet<int>();
                    var preflightFootprints = new HashSet<int>();
                    var previews = new List<Dictionary<string, object>>();
                    var validAnchors = new List<CellCoord>();
                    var actionableAnchors = new List<CellCoord>();
                    var autoDigAnchors = new List<CellCoord>();
                    foreach (var anchor in anchors)
                    {
                        int worldId = ToolUtil.ResolveWorldId(args);
                        var batchOverlap = PlannedFootprintOverlap(def, anchor.x, anchor.y, worldId, preflightFootprints, ParseOrientation(args["orientation"]?.ToString()));
                        if (batchOverlap != null)
                        {
                            previews.Add(ErrorResult(prefabId, anchor.x, anchor.y,
                                "Requested anchors overlap within this batch", batchOverlap));
                            continue;
                        }
                        var preview = TryPlanOne(prefabId, anchor.x, anchor.y, preflightArgs, preflightSupport);
                        bool valid = preview.ContainsKey("valid") && (bool)preview["valid"];
                        if (valid)
                        {
                            validAnchors.Add(anchor);
                            actionableAnchors.Add(anchor);
                        }
                        else if (IsAutoDiggableFailure(preview))
                        {
                            autoDigAnchors.Add(anchor);
                            actionableAnchors.Add(anchor);
                        }
                        if (valid || IsAutoDiggableFailure(preview))
                            ReservePlannedFootprint(def, anchor.x, anchor.y, worldId, preflightFootprints, ParseOrientation(args["orientation"]?.ToString()));
                        previews.Add(preview);
                    }

                    var failedPreviews = previews.Where(item => !(item.ContainsKey("valid") && (bool)item["valid"])).ToList();
                    var hardFailedPreviews = previews.Where(item => !(item.ContainsKey("valid") && (bool)item["valid"]) && !IsAutoDiggableFailure(item)).ToList();
                    bool plannedFootprintOverlap = failedPreviews.Any(item =>
                        EqualsIgnoreCase(item.TryGetValue("reasonCode", out object reasonCode) ? reasonCode?.ToString() : null, "planned_footprint_overlap"));
                    if (dryRun || plannedFootprintOverlap || (hardFailedPreviews.Count > 0 && !allowPartial))
                    {
                        var preflightResponse = new Dictionary<string, object>
                        {
                            ["prefabId"] = prefabId,
                            ["dryRun"] = dryRun,
                            ["committed"] = false,
                        ["valid"] = failedPreviews.Count == 0,
                        ["actionable"] = hardFailedPreviews.Count == 0,
                        ["anchorResolution"] = anchorResolution,
                        ["planResolution"] = planResolution.ToDictionary(),
                        ["anchorCount"] = anchors.Count,
                            ["validAnchors"] = validAnchors.Count,
                        ["autoDiggableAnchors"] = autoDigAnchors.Count,
                        ["failed"] = failedPreviews.Count,
                        ["hardFailed"] = hardFailedPreviews.Count,
                        ["next"] = failedPreviews.Count == 0
                            ? "Dry run passed. Re-run with confirm=true to place blueprints."
                            : hardFailedPreviews.Count == 0
                                ? "Only auto-diggable obstructions remain. Re-run with confirm=true to queue required dig/uproot and place blueprints."
                                : "Fix hard failures first; inspect errors[0].reasonCode/details before retrying.",
                        ["tokenHint"] = "Use valid/actionable/anchorResolution/planResolution/errors[0].reasonCode first; read previews only when choosing an alternate anchor.",
                        ["errors"] = failedPreviews.Take(50).ToList(),
                        ["previews"] = previews
                    };
                        if (plannedFootprintOverlap)
                        {
                            preflightResponse["reasonCode"] = "planned_footprint_overlap";
                            preflightResponse["success"] = false;
                            return CallToolResult.Error(JsonConvert.SerializeObject(preflightResponse, McpJsonUtil.Settings));
                        }
                        return CallToolResult.Text(JsonConvert.SerializeObject(preflightResponse, McpJsonUtil.Settings));
                    }

                    var actualSupport = new HashSet<int>();
                    var autoDigContext = AutoDigContext.FromArgs(args);
                    var results = new List<Dictionary<string, object>>();
            var remainingAnchors = new List<CellCoord>();
            var requestedExecutionAnchors = allowPartial ? actionableAnchors : anchors;
            var executionAnchors = requestedExecutionAnchors.Take(maxCommitAnchors).ToList();
            remainingAnchors.AddRange(requestedExecutionAnchors.Skip(maxCommitAnchors));
                    int planned = 0;
                    int alreadyPresent = 0;
                    int alreadyConnected = 0;
                    int succeeded = 0;
                    int failed = 0;
                    int autoDigQueued = 0;
                    int autoDigAlreadyMarked = 0;
            foreach (var anchor in executionAnchors)
                    {
                        var result = TryPlanOne(prefabId, anchor.x, anchor.y, args, actualSupport, autoDigContext);
                        bool wasPlanned = GetBool(result, "planned");
                        bool wasAlreadyPresent = GetBool(result, "alreadyPresent");
                        bool wasAlreadyConnected = GetBool(result, "alreadyConnected");
                        bool isAutoDig = IsAutoDigResult(result);
                        bool ok = wasPlanned || wasAlreadyPresent || wasAlreadyConnected || isAutoDig;
                        autoDigQueued += GetAutoDigInt(result, "marked");
                        autoDigAlreadyMarked += GetAutoDigInt(result, "alreadyMarked");
                        if (wasPlanned && !wasAlreadyPresent && !wasAlreadyConnected)
                            planned++;
                        if (wasAlreadyPresent)
                            alreadyPresent++;
                        if (wasAlreadyConnected)
                            alreadyConnected++;
                        if (ok)
                            succeeded++;
                        else if (!isAutoDig)
                        {
                            failed++;
                            remainingAnchors.Add(anchor);
                        }
                        if (!wasPlanned && !wasAlreadyPresent && !wasAlreadyConnected && isAutoDig)
                            remainingAnchors.Add(anchor);
                        results.Add(result);
                    }

            if (allowPartial && hardFailedPreviews.Count > 0)
                remainingAnchors.AddRange(anchors.Where(anchor => hardFailedPreviews.Any(item => SameAnchor(item, anchor))));

            bool throttled = requestedExecutionAnchors.Count > executionAnchors.Count;
            string executionConflictReason = results
                .Select(item => item.TryGetValue("reasonCode", out object reasonCode) ? reasonCode?.ToString() : null)
                .FirstOrDefault(reasonCode => EqualsIgnoreCase(reasonCode, "placement_conflict")
                    || EqualsIgnoreCase(reasonCode, "utility_path_conflict"));

                    var executionResponse = new Dictionary<string, object>
                    {
                        ["prefabId"] = prefabId,
                        ["dryRun"] = false,
                        ["committed"] = planned > 0 || autoDigQueued > 0,
                        ["allowPartial"] = allowPartial,
                        ["anchorResolution"] = anchorResolution,
                        ["planResolution"] = planResolution.ToDictionary(),
                        ["anchorCount"] = anchors.Count,
                ["attempted"] = executionAnchors.Count,
                ["maxCommitAnchors"] = maxCommitAnchors,
                ["throttled"] = throttled,
                ["deferred"] = requestedExecutionAnchors.Count - executionAnchors.Count,
                        ["succeeded"] = succeeded,
                        ["planned"] = planned,
                        ["alreadyPresent"] = alreadyPresent,
                        ["alreadyConnected"] = alreadyConnected,
                        ["autoDigQueued"] = autoDigQueued,
                        ["autoDigAlreadyMarked"] = autoDigAlreadyMarked,
                        ["autoDigLimitReached"] = autoDigContext.LimitReached,
                        ["pendingBuildAfterDig"] = autoDigQueued + autoDigAlreadyMarked,
                        ["failed"] = failed + (allowPartial ? hardFailedPreviews.Count : 0),
                ["allRequestedSatisfied"] = !throttled && planned + alreadyPresent + alreadyConnected >= anchors.Count,
                        ["remainingAnchors"] = remainingAnchors.Select(anchor => AnchorDictionary(anchor.x, anchor.y, ToolUtil.ResolveWorldId(args))).ToList(),
                ["remainingAction"] = BuildRemainingBuildAreaAction(args, prefabId, remainingAnchors),
                ["next"] = throttled
                    ? "Batch was throttled for game stability. Execute remainingAction after the game processes this batch."
                    : remainingAnchors.Count > 0
                        ? "Some anchors remain blocked or pending auto-dig. Inspect remainingAction/preflightErrors."
                        : "Batch complete.",
                        ["preflightErrors"] = hardFailedPreviews.Take(50).ToList(),
                        ["results"] = results
                    };
                    if (!string.IsNullOrEmpty(executionConflictReason))
                    {
                        executionResponse["reasonCode"] = executionConflictReason;
                        executionResponse["success"] = false;
                        return CallToolResult.Error(JsonConvert.SerializeObject(executionResponse, McpJsonUtil.Settings));
                    }
                    return CallToolResult.Text(JsonConvert.SerializeObject(executionResponse, McpJsonUtil.Settings));
                }
            };
        }

    }
}
