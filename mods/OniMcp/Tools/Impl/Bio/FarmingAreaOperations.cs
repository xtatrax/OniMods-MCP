using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class FarmingTools
    {
        public static McpTool BatchSetPlanting()
        {
            return new McpTool
            {
                Name = "farming_planting_batch_set",
                Group = "farming",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "planters_batch_set_seed", "plant_seed_area_set" },
                Tags = new List<string> { "farming", "plants", "planters", "seeds", "batch" },
                Description = "互換用の入口。colony_control domain=bio bioDomain=farming action=batch を優先して使う。領域内の栽培要求を一括で設定または取り消す。複数の農耕タイル・プランターに同じ種子をまとめて指定する用途に適している",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["seedTag"] = new McpToolParameter { Type = "string", Description = "種子の prefab/tag。例: BasicPlantSeed。action=set では必須", Required = false },
                    ["mutationTag"] = new McpToolParameter { Type = "string", Description = "植物の変異・亜種の tag。変異を使わない場合は空欄にする", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "set または cancel。既定値は set", Required = false, EnumValues = new List<string> { "set", "cancel" } },
                    ["emptyOnly"] = new McpToolParameter { Type = "boolean", Description = "現在植物のない栽培スロットだけを処理するか。既定値は true", Required = false },
                    ["removeOccupant"] = new McpToolParameter { Type = "boolean", Description = "植物がすでにある場合、先に OrderRemoveOccupant を呼び出して掘り起こすか。既定値は false", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "栽培スロットの名前、prefabId、現在の植物、要求中の種子で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "処理する件数の上限。既定値は 100、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "true が必須。領域が 100 セルを超える場合や removeOccupant=true の場合も true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to batch change planting requests");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (cells > 100 && !ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required when changing planting requests in more than 100 cells");

                    string action = (args["action"]?.ToString() ?? "set").Trim().ToLowerInvariant();
                    bool isSet = action == "set";
                    if (!isSet && action != "cancel")
                        return CallToolResult.Error("action must be set or cancel");

                    Tag seedTag = Tag.Invalid;
                    GameObject seedPrefab = null;
                    if (isSet)
                    {
                        string seedName = args["seedTag"]?.ToString();
                        if (string.IsNullOrWhiteSpace(seedName))
                            return CallToolResult.Error("seedTag is required for action=set");
                        seedTag = TagManager.Create(seedName.Trim());
                        seedPrefab = Assets.GetPrefab(seedTag);
                        if (seedPrefab == null || seedPrefab.GetComponent<PlantableSeed>() == null)
                            return CallToolResult.Error("seedTag is not a PlantableSeed prefab");
                    }

                    var mutationTag = string.IsNullOrWhiteSpace(args["mutationTag"]?.ToString())
                        ? Tag.Invalid
                        : TagManager.Create(args["mutationTag"].ToString().Trim());
                    int worldId = ToolUtil.ResolveWorldId(args);
                    string query = args["query"]?.ToString();
                    bool emptyOnly = ToolUtil.GetBool(args, "emptyOnly", true);
                    bool removeOccupant = ToolUtil.GetBool(args, "removeOccupant", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));
                    var seedInfo = isSet ? SeedInfo(seedPrefab.GetComponent<PlantableSeed>()) : null;
                    int changedCount = 0;
                    int failedCount = 0;
                    int unchangedCount = 0;
                    var changed = new List<Dictionary<string, object>>();
                    var failures = new List<Dictionary<string, object>>();
                    var unchanged = new List<Dictionary<string, object>>();
                    foreach (var plot in Components.BuildingCompletes.Items
                                 .Select(building => building?.GetComponent<PlantablePlot>())
                                 .Where(plot => plot != null && ToolUtil.GameObjectMatchesWorld(plot.gameObject, worldId))
                                 .Where(plot => CellInRect(Grid.PosToCell(plot.gameObject), rect, worldId))
                                 .Where(plot => PlantingMatches(plot, query))
                                 .Take(limit))
                    {
                        if (!isSet)
                        {
                            bool hadRequestedTag = plot.requestedEntityTag.IsValid;
                            bool hadActiveRequest = plot.GetActiveRequest != null;
                            if (!hadRequestedTag && !hadActiveRequest)
                            {
                                unchangedCount++;
                                AddBatchDetail(unchanged, BatchPlotOutcome("no_active_request", plot));
                                continue;
                            }

                            var before = PlotInfo(plot);
                            plot.CancelActiveRequest();
                            bool cleared = !plot.requestedEntityTag.IsValid && plot.GetActiveRequest == null;
                            if (cleared)
                            {
                                changedCount++;
                                AddBatchDetail(changed, PlotInfo(plot));
                            }
                            else
                            {
                                failedCount++;
                                var failure = BatchPlotOutcome("cancel_did_not_clear_request", plot);
                                failure["before"] = before;
                                AddBatchDetail(failures, failure);
                            }
                            continue;
                        }

                        bool hasDeposit = SeedMatchesPlotDepositTags(plot, seedPrefab, seedTag);
                        bool validEntity = plot.IsValidEntity(seedPrefab);
                        if (!hasDeposit || !validEntity)
                        {
                            failedCount++;
                            var failure = BatchPlotOutcome("seed_not_valid_for_plot", plot);
                            failure["hasDepositTag"] = hasDeposit;
                            failure["isValidEntity"] = validEntity;
                            failure["seed"] = seedInfo;
                            AddBatchDetail(failures, failure);
                            continue;
                        }
                        if (RequestedPlantingMatches(plot, seedTag, mutationTag))
                        {
                            unchangedCount++;
                            AddBatchDetail(unchanged, BatchPlotOutcome("request_already_active", plot));
                            continue;
                        }
                        if (plot.Occupant != null)
                        {
                            if (emptyOnly)
                            {
                                unchangedCount++;
                                AddBatchDetail(unchanged, BatchPlotOutcome("occupant_present_empty_only", plot));
                                continue;
                            }
                            if (!removeOccupant)
                            {
                                unchangedCount++;
                                AddBatchDetail(unchanged, BatchPlotOutcome("occupant_present_remove_disabled", plot));
                                continue;
                            }
                            plot.OrderRemoveOccupant();
                        }
                        plot.CancelActiveRequest();
                        plot.CreateOrder(seedTag, mutationTag);
                        bool requestedPlantingMatches = RequestedPlantingMatches(plot, seedTag, mutationTag);
                        bool requestActive = plot.GetActiveRequest != null;
                        if (requestedPlantingMatches)
                        {
                            changedCount++;
                            AddBatchDetail(changed, PlotInfo(plot));
                        }
                        else
                        {
                            failedCount++;
                            var failure = BatchPlotOutcome("create_order_postcondition_failed", plot);
                            failure["requestedPlantingMatches"] = requestedPlantingMatches;
                            failure["hasActiveRequest"] = requestActive;
                            failure["hasDepositTag"] = hasDeposit;
                            failure["isValidEntity"] = validEntity;
                            failure["worldInventoryAmount"] = Math.Round(AvailableSeedAmount(plot.gameObject, seedTag), 3);
                            failure["inventoryNote"] = "World inventory amount is informational only; it does not prove fetchability or explain CreateOrder outcome.";
                            failure["seed"] = seedInfo;
                            AddBatchDetail(failures, failure);
                        }
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["action"] = action,
                        ["seedTag"] = seedTag.IsValid ? seedTag.Name : null,
                        ["changed"] = changedCount,
                        ["failed"] = failedCount,
                        ["unchanged"] = unchangedCount,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["detailLimit"] = 20,
                        ["plots"] = changed,
                        ["failures"] = failures,
                        ["unchangedPlots"] = unchanged
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool UprootArea()
        {
            return new McpTool
            {
                Name = "plants_uproot_area",
                Group = "farming",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "farming_uproot_area", "plants_cancel_uproot" },
                Tags = new List<string> { "farming", "plants", "uproot", "harvest" },
                Description = "互換用の入口。colony_control domain=bio bioDomain=farming action=uproot を優先して使う。領域内の植物の掘り起こしを指定または取り消す",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "mark または cancel。既定値は mark", Required = false, EnumValues = new List<string> { "mark", "cancel" } },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "掘り起こし作業の優先度 1-9。既定値は 5", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "赤色の最優先にするか。既定値は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "領域が 100 セルを超える場合は true が必須。mark 操作では true の指定を推奨する", Required = false }
                }),
                Handler = args =>
                {
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (cells > 100 && !ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required when changing uproot orders in more than 100 cells");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    bool mark = (args["action"]?.ToString() ?? "mark").Trim().ToLowerInvariant() != "cancel";
                    int changed = 0;
                    var results = new List<Dictionary<string, object>>();
                    foreach (var uprootable in Components.Uprootables.Items)
                    {
                        var go = uprootable?.gameObject;
                        if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                            continue;
                        int cell = Grid.PosToCell(go);
                        if (!CellInRect(cell, rect, worldId))
                            continue;

                        if (mark)
                        {
                            if (!uprootable.CanUproot())
                                continue;
                            uprootable.MarkForUproot();
                            ApplyPriority(go, args);
                            results.Add(TargetInfo(go, "marked"));
                        }
                        else
                        {
                            if (!uprootable.IsMarkedForUproot)
                                continue;
                            uprootable.ForceCancelUproot();
                            go.Trigger(CancelEvent);
                            results.Add(TargetInfo(go, "cancelled"));
                        }
                        changed++;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["action"] = mark ? "mark" : "cancel",
                        ["changed"] = changed,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["targets"] = results.Take(200).ToList(),
                        ["truncatedTargets"] = Math.Max(0, results.Count - 200)
                    }, McpJsonUtil.Settings));
                }
            };
        }

    }
}
