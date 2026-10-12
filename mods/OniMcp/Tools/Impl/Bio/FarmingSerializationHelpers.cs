using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class FarmingTools
    {
        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標で検索する場合の既定値は現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の領域ハンドル。指定すると x1/y1/x2/y2 を省略できる", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の左下・始点の X。areaId を使う場合は省略できる", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の左下・始点の Y。areaId を使う場合は省略できる", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の右上・終点の X。areaId を使う場合は省略できる", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の右上・終点の Y。areaId を使う場合は省略できる", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定値は areaId に関連付けられたワールド、または現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static PlantablePlot FindPlot(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);

            foreach (var building in Components.BuildingCompletes.Items)
            {
                var go = building?.gameObject;
                var plot = go == null ? null : go.GetComponent<PlantablePlot>();
                if (plot == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return plot;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return plot;
            }
            return null;
        }

        private static HarvestDesignatable FindHarvestable(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);

            foreach (var harvestable in Components.HarvestDesignatables.Items)
            {
                var go = harvestable?.gameObject;
                if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return harvestable;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return harvestable;
            }
            return null;
        }

        private static Dictionary<string, object> PlotInfo(PlantablePlot plot)
        {
            var result = TargetInfo(plot.gameObject, null);
            result["requestedSeed"] = plot.requestedEntityTag.IsValid ? plot.requestedEntityTag.Name : null;
            result["requestedMutation"] = plot.requestedEntityAdditionalFilterTag.IsValid ? plot.requestedEntityAdditionalFilterTag.Name : null;
            result["hasActiveRequest"] = plot.GetActiveRequest != null;
            result["validPreview"] = plot.ValidPlant;
            result["acceptsFertilizer"] = plot.AcceptsFertilizer;
            result["acceptsIrrigation"] = plot.AcceptsIrrigation;
            result["occupant"] = plot.Occupant == null ? null : TargetInfo(plot.Occupant, null);
            result["acceptedSeedTags"] = plot.possibleDepositObjectTags.Select(tag => tag.Name).OrderBy(name => name).ToList();
            return result;
        }

        private static Dictionary<string, object> HarvestableInfo(HarvestDesignatable harvestable)
        {
            var result = TargetInfo(harvestable.gameObject, null);
            result["canBeHarvested"] = harvestable.CanBeHarvested();
            result["markedForHarvest"] = harvestable.MarkedForHarvest;
            result["harvestWhenReady"] = harvestable.HarvestWhenReady;
            result["inPlanterBox"] = harvestable.InPlanterBox;
            var harvestableComponent = harvestable.GetComponent<Harvestable>();
            if (harvestableComponent != null)
                result["harvestableComponent"] = harvestableComponent.GetType().Name;
            return result;
        }

        private static Dictionary<string, object> SeedInfo(PlantableSeed seed)
        {
            var kpid = seed.GetComponent<KPrefabID>();
            var seedGo = seed.gameObject;
            var seedTag = kpid?.PrefabTag ?? Tag.Invalid;
            // Tags agents need when matching list_planting.acceptedSeedTags (e.g. CropSeed).
            var depositTags = new List<string>();
            if (kpid != null)
            {
                foreach (var tag in kpid.Tags)
                {
                    if (tag.IsValid && !string.IsNullOrEmpty(tag.Name))
                        depositTags.Add(tag.Name);
                }
                depositTags = depositTags.Distinct().OrderBy(name => name).ToList();
            }

            // Live play: agents saw acceptedSeedTags=["CropSeed"] but seed_catalog only
            // returned seedTag/plantId, so they tried BasicSingleHarvestPlantSeed and got
            // "Seed is not valid for this planter direction/type" with no catalog guidance.
            return new Dictionary<string, object>
            {
                ["seedTag"] = seedTag.IsValid ? seedTag.Name : seedGo.name,
                ["name"] = TargetName(seedGo),
                ["plantId"] = seed.PlantID.Name,
                ["previewId"] = seed.PreviewID.Name,
                ["direction"] = seed.Direction.ToString(),
                ["replantGroundTag"] = seed.replantGroundTag.IsValid ? seed.replantGroundTag.Name : null,
                ["seedTags"] = depositTags,
                ["hasCropSeedTag"] = depositTags.Any(tag => tag == "CropSeed"),
                ["isCropSeed"] = depositTags.Any(tag => tag == "CropSeed"),
                ["planterBoxCandidate"] = depositTags.Any(tag => tag == "CropSeed")
            };
        }

        private static bool PlantingMatches(PlantablePlot plot, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            string q = query.Trim();
            return Contains(TargetName(plot.gameObject), q)
                || Contains(plot.gameObject.GetComponent<KPrefabID>()?.PrefabTag.Name ?? plot.gameObject.name, q)
                || Contains(plot.requestedEntityTag.Name, q)
                || Contains(plot.Occupant?.GetProperName(), q)
                || Contains(plot.Occupant?.GetComponent<KPrefabID>()?.PrefabTag.Name, q);
        }

        private static bool HarvestableMatches(HarvestDesignatable harvestable, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            string q = query.Trim();
            var go = harvestable.gameObject;
            return Contains(TargetName(go), q)
                || Contains(go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name, q);
        }

        private static bool SeedMatches(PlantableSeed seed, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            string q = query.Trim();
            var kpid = seed.GetComponent<KPrefabID>();
            return Contains(TargetName(seed.gameObject), q)
                || Contains(kpid?.PrefabTag.Name ?? seed.gameObject.name, q)
                || Contains(seed.PlantID.Name, q)
                || Contains(seed.PreviewID.Name, q)
                || (kpid != null && kpid.Tags.Any(tag => Contains(tag.Name, q)));
        }

        private static bool HasCropSeedTag(PlantableSeed seed)
        {
            var kpid = seed?.GetComponent<KPrefabID>();
            return kpid != null && kpid.Tags.Any(tag => tag.Name == "CropSeed");
        }

        private static bool SeedMatchesPlotDepositTags(PlantablePlot plot, GameObject seedPrefab, Tag seedTag)
        {
            if (plot == null || seedPrefab == null)
                return false;
            if (plot.HasDepositTag(seedTag))
                return true;
            var kpid = seedPrefab.GetComponent<KPrefabID>();
            if (kpid == null)
                return false;
            // Category deposit tags (CropSeed) must match tags on the seed prefab, not only seed prefab id.
            foreach (var tag in kpid.Tags)
            {
                if (tag.IsValid && plot.HasDepositTag(tag))
                    return true;
            }
            return false;
        }

        private static bool RequestedPlantingMatches(PlantablePlot plot, Tag seedTag, Tag mutationTag)
        {
            if (plot == null || plot.GetActiveRequest == null)
                return false;
            if (!plot.requestedEntityTag.IsValid || !seedTag.IsValid
                || !string.Equals(plot.requestedEntityTag.Name, seedTag.Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var requestedMutationTag = plot.requestedEntityAdditionalFilterTag;
            if (requestedMutationTag.IsValid != mutationTag.IsValid)
                return false;
            return !mutationTag.IsValid
                || string.Equals(requestedMutationTag.Name, mutationTag.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, object> BatchPlotOutcome(string reason, PlantablePlot plot)
        {
            return new Dictionary<string, object>
            {
                ["reason"] = reason,
                ["plot"] = PlotInfo(plot)
            };
        }

        private static void AddBatchDetail(List<Dictionary<string, object>> details, Dictionary<string, object> item)
        {
            if (details.Count < 20)
                details.Add(item);
        }

        private static float AvailableSeedAmount(GameObject target, Tag seedTag)
        {
            if (target == null || !seedTag.IsValid)
                return 0f;
            var world = target.GetMyWorld();
            if (world == null)
                return 0f;
            return ToolUtil.SafeFloat(world.worldInventory.GetTotalAmount(seedTag, includeRelatedWorlds: true));
        }

        private static void ApplyPriority(GameObject go, JObject args)
        {
            var prioritizable = go.GetComponent<Prioritizable>();
            if (prioritizable == null)
                return;
            bool top = ToolUtil.GetBool(args, "topPriority", false);
            int priority = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "priority") ?? 5, 9));
            prioritizable.SetMasterPriority(new PrioritySetting(top ? PriorityScreen.PriorityClass.topPriority : PriorityScreen.PriorityClass.basic, top ? 1 : priority));
        }

        private static Dictionary<string, object> TargetInfo(GameObject go, string status)
        {
            int cell = Grid.PosToCell(go);
            var kpid = go.GetComponent<KPrefabID>();
            var result = new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["name"] = TargetName(go),
                ["prefabId"] = kpid?.PrefabTag.Name ?? go.name,
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["worldId"] = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : -1
            };
            if (!string.IsNullOrEmpty(status))
                result["status"] = status;
            return result;
        }

        private static string TargetName(GameObject go)
        {
            return ToolUtil.CleanName(go.GetProperName());
        }

        private static bool HasRectInput(JObject args)
        {
            return !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                || (args["x1"] != null && args["y1"] != null && args["x2"] != null && args["y2"] != null);
        }

        private static bool CellInRect(int cell, Dictionary<string, int> rect, int worldId)
        {
            if (!Grid.IsValidCell(cell)) return false;
            if (!ToolUtil.CellMatchesWorld(cell, worldId)) return false;
            int x = Grid.CellColumn(cell);
            int y = Grid.CellRow(cell);
            return x >= rect["x1"] && x <= rect["x2"] && y >= rect["y1"] && y <= rect["y2"];
        }

        private static int RectCellCount(Dictionary<string, int> rect)
        {
            return (rect["x2"] - rect["x1"] + 1) * (rect["y2"] - rect["y1"] + 1);
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
