using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class ProductionTools
    {
        private static Dictionary<string, object> FabricatorInfo(ComplexFabricator fabricator, bool includeRecipes)
        {
            var go = fabricator.gameObject;
            var result = TargetInfo(go);
            var operational = go.GetComponent<Operational>();
            result["operational"] = operational == null ? null : (object)new Dictionary<string, object>
            {
                ["isOperational"] = operational.IsOperational,
                ["isActive"] = operational.IsActive
            };
            result["hasAnyOrder"] = fabricator.HasAnyOrder;
            result["waitingForWorker"] = fabricator.WaitingForWorker;
            result["hasWorker"] = fabricator.HasWorker;
            result["duplicantOperated"] = fabricator.duplicantOperated;
            result["forbidMutantSeeds"] = fabricator.ForbidMutantSeeds;
            result["orderProgress"] = ToolUtil.SafeFloat(fabricator.OrderProgress);
            result["recipeCount"] = fabricator.GetRecipes().Length;
            result["currentRecipe"] = RecipeSummary(fabricator.CurrentWorkingOrder);
            result["nextRecipe"] = RecipeSummary(fabricator.NextOrder);
            result["queue"] = QueueSummary(fabricator);
            if (includeRecipes)
                result["recipes"] = fabricator.GetRecipes().Select(recipe => RecipeInfo(fabricator, recipe, includeFabricator: false)).ToList();
            return result;
        }

        private static Dictionary<string, object> RecipeInfo(ComplexFabricator fabricator, ComplexRecipe recipe, bool includeFabricator)
        {
            var result = RecipeSummary(recipe);
            result["queueCount"] = FormatQueueCount(fabricator.GetRecipeQueueCount(recipe));
            result["isQueued"] = fabricator.GetRecipeQueueCount(recipe) != 0;
            result["isCurrent"] = fabricator.CurrentWorkingOrder != null && fabricator.CurrentWorkingOrder.id == recipe.id;
            result["isNext"] = fabricator.NextOrder != null && fabricator.NextOrder.id == recipe.id;
            result["timeSeconds"] = ToolUtil.SafeFloat(recipe.time);
            result["requiredTech"] = recipe.requiredTech;
            result["requiresTechUnlock"] = recipe.RequiresTechUnlock();
            result["techUnlocked"] = recipe.IsRequiredTechUnlocked();
            result["requiresAllIngredientsDiscovered"] = recipe.RequiresAllIngredientsDiscovered;
            result["consumedHEP"] = recipe.consumedHEP;
            result["producedHEP"] = recipe.producedHEP;
            result["ingredients"] = recipe.ingredients.Select(RecipeElementInfo).ToList();
            result["results"] = recipe.results.Select(RecipeElementInfo).ToList();
            if (includeFabricator)
                result["fabricator"] = TargetInfo(fabricator.gameObject);
            return result;
        }

        private static Dictionary<string, object> RecipeSummary(ComplexRecipe recipe)
        {
            if (recipe == null)
                return null;

            return new Dictionary<string, object>
            {
                ["recipeId"] = recipe.id,
                ["categoryId"] = recipe.recipeCategoryID,
                ["name"] = SafeRecipeName(recipe, includeAmounts: false),
                ["nameWithAmounts"] = SafeRecipeName(recipe, includeAmounts: true),
                ["description"] = recipe.description,
                ["firstResult"] = recipe.FirstResult.Name
            };
        }

        private static Dictionary<string, object> RecipeElementInfo(ComplexRecipe.RecipeElement element)
        {
            return new Dictionary<string, object>
            {
                ["tag"] = element.material.Name,
                ["name"] = SafeProperName(element.material),
                ["amount"] = ToolUtil.SafeFloat(element.amount),
                ["temperatureOperation"] = element.temperatureOperation.ToString(),
                ["storeElement"] = element.storeElement,
                ["inheritElement"] = element.inheritElement,
                ["facadeId"] = element.facadeID,
                ["doNotConsume"] = element.doNotConsume
            };
        }

        private static Dictionary<string, object> QueueSummary(ComplexFabricator fabricator)
        {
            var queued = fabricator.GetRecipes()
                .Where(recipe => fabricator.GetRecipeQueueCount(recipe) != 0)
                .Select(recipe => new Dictionary<string, object>
                {
                    ["recipeId"] = recipe.id,
                    ["categoryId"] = recipe.recipeCategoryID,
                    ["name"] = SafeRecipeName(recipe, includeAmounts: false),
                    ["count"] = FormatQueueCount(fabricator.GetRecipeQueueCount(recipe))
                })
                .ToList();

            return new Dictionary<string, object>
            {
                ["queuedRecipeCount"] = queued.Count,
                ["totalQueued"] = FormatQueueCount(QueuedRecipeCount(fabricator)),
                ["recipes"] = queued
            };
        }

        private static int QueuedRecipeCount(ComplexFabricator fabricator)
        {
            int total = 0;
            foreach (var recipe in fabricator.GetRecipes())
            {
                int count = fabricator.GetRecipeQueueCount(recipe);
                if (count == ComplexFabricator.QUEUE_INFINITE)
                    return ComplexFabricator.QUEUE_INFINITE;
                total += count;
            }
            return total;
        }

        private static object FormatQueueCount(int count)
        {
            if (count == ComplexFabricator.QUEUE_INFINITE)
                return "infinite";
            return count;
        }

        private static bool TryComputeQueueCount(int before, string mode, int requested, out int next)
        {
            switch (mode)
            {
                case "clear":
                    next = 0;
                    return true;
                case "infinite":
                    next = ComplexFabricator.QUEUE_INFINITE;
                    return true;
                case "add":
                    next = before == ComplexFabricator.QUEUE_INFINITE
                        ? ComplexFabricator.QUEUE_INFINITE
                        : Mathf.Clamp(before + Math.Max(1, requested), 0, ComplexFabricator.MAX_QUEUE_SIZE);
                    return true;
                case "remove":
                    next = before == ComplexFabricator.QUEUE_INFINITE
                        ? Mathf.Max(0, ComplexFabricator.MAX_QUEUE_SIZE - Math.Max(1, requested))
                        : Mathf.Clamp(before - Math.Max(1, requested), 0, ComplexFabricator.MAX_QUEUE_SIZE);
                    return true;
                case "set":
                    next = Mathf.Clamp(requested, 0, ComplexFabricator.MAX_QUEUE_SIZE);
                    return true;
                default:
                    next = before;
                    return false;
            }
        }

        private static bool RecipeMatches(ComplexRecipe recipe, string query)
        {
            if (recipe == null)
                return false;
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string q = query.Trim();
            return Contains(recipe.id, q)
                || Contains(recipe.recipeCategoryID, q)
                || Contains(SafeRecipeName(recipe, includeAmounts: false), q)
                || recipe.ingredients.Any(element => ElementMatches(element, q))
                || recipe.results.Any(element => ElementMatches(element, q));
        }

        private static bool ElementMatches(ComplexRecipe.RecipeElement element, string query)
        {
            return Contains(element.material.Name, query) || Contains(SafeProperName(element.material), query);
        }

        private static string SafeRecipeName(ComplexRecipe recipe, bool includeAmounts)
        {
            try
            {
                return ToolUtil.CleanName(recipe.GetUIName(includeAmounts));
            }
            catch
            {
                return recipe.id;
            }
        }

        private static string SafeProperName(Tag tag)
        {
            try
            {
                return ToolUtil.CleanName(tag.ProperName());
            }
            catch
            {
                return tag.Name;
            }
        }

        private static Dictionary<string, object> TargetInfo(GameObject go)
        {
            int cell = Grid.PosToCell(go);
            return new Dictionary<string, object>
            {
                ["id"] = go.GetComponent<KPrefabID>()?.InstanceID ?? go.GetInstanceID(),
                ["prefabId"] = PrefabId(go),
                ["name"] = TargetName(go),
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["worldId"] = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : -1
            };
        }

        private static string PrefabId(GameObject go)
        {
            var building = go.GetComponent<Building>();
            return building?.Def?.PrefabID ?? go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name;
        }

        private static string TargetName(GameObject go)
        {
            return ToolUtil.CleanName(go.GetProperName());
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象の製造設備の InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象の製造設備のセル X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象の製造設備のセル Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。座標による検索では現在アクティブなワールドが既定", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。指定すると x1/y1/x2/y2 を省略可能", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の左下・始点 X。areaId を使用する場合は省略可能", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の左下・始点 Y。areaId を使用する場合は省略可能", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の右上・終点 X。areaId を使用する場合は省略可能", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の右上・終点 Y。areaId を使用する場合は省略可能", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。既定は areaId に紐付くワールドまたは現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> ProductionQueueControlParams()
        {
            return LookupParams(RectParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list_fabricators、list_recipes、set、batch、set_mutant_seeds、mutant_seed_list または mutant_seed_set", Required = false, EnumValues = new List<string> { "list_fabricators", "list_recipes", "set", "batch", "set_mutant_seeds", "mutant_seed_list", "mutant_seed_set" } },
                ["recipeId"] = new McpToolParameter { Type = "string", Description = "action=list_recipes では完全一致で絞り込み、action=set では対象の ComplexRecipe.id", Required = false },
                ["categoryId"] = new McpToolParameter { Type = "string", Description = "action=list_recipes でレシピ分類 id の完全一致による絞り込み", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "読み取り時に設備名、prefabId、レシピ、素材、生成物、変異種子の受け入れ切り替えコンポーネントで絞り込み", Required = false },
                ["fabricatorQuery"] = new McpToolParameter { Type = "string", Description = "action=list_recipes で製造設備による絞り込み", Required = false },
                ["queuedOnly"] = new McpToolParameter { Type = "boolean", Description = "読み取り時に既存のキュー・注文項目だけを返すか。既定は false", Required = false },
                ["includeRecipes"] = new McpToolParameter { Type = "boolean", Description = "action=list_fabricators で各製造設備のレシピの要約を含めるか。既定は false", Required = false },
                ["includeLocked"] = new McpToolParameter { Type = "boolean", Description = "action=list_recipes で研究が未解除のレシピを含めるか。既定は true", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "読み取り時に返す件数の上限", Required = false },
                ["mode"] = new McpToolParameter { Type = "string", Description = "action=set/batch のキューモード: set、add、remove、infinite または clear。既定は set", Required = false, EnumValues = new List<string> { "set", "add", "remove", "infinite", "clear" } },
                ["count"] = new McpToolParameter { Type = "integer", Description = "action=set/batch の set/add/remove で扱う数量", Required = false },
                ["items"] = new McpToolParameter { Type = "array", Description = "action=batch のキュー操作配列。各項目: recipeId/r、mode/m、count/c", Required = false },
                ["defaults"] = new McpToolParameter { Type = "object", Description = "action=batch で各項目に結合するキューの既定の引数", Required = false },
                ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                ["clearAll"] = new McpToolParameter { Type = "boolean", Description = "action=batch で true=先にその製造設備の全レシピキューをクリア", Required = false },
                ["forbid"] = new McpToolParameter { Type = "boolean", Description = "action=set_mutant_seeds で true=変異種子を拒否、false=変異種子を受け入れ", Required = false },
                ["accept"] = new McpToolParameter { Type = "boolean", Description = "action=mutant_seed_set で true=変異種子を受け入れ、false=変異種子を拒否", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "キューや変異種子の受け入れ設定の書き込みには、生産状態の誤変更を防ぐため true が必須", Required = false }
            }));
        }

        private static bool HasLookupInput(JObject args)
        {
            return args["id"] != null || (args["x"] != null && args["y"] != null);
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

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
