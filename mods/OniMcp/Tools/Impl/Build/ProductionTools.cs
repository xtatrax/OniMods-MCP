using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class ProductionTools
    {
        public static McpTool ListFabricators()
        {
            return new McpTool
            {
                Name = "production_fabricators_list",
                Hidden = true,
                Group = "production",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "fabricators_list", "crafting_stations_list" },
                Tags = new List<string> { "production", "fabricator", "recipe", "queue", "craft", "refinery", "kitchen" },
                Description = "互換用の入口。building_control domain=production action=list_fabricators を優先してください。製造設備・精錬設備・調理設備などの ComplexFabricator のレシピキュー、現在の注文、次の注文、稼働状態を列挙します",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、現在・次のレシピ、キューにあるレシピで絞り込み", Required = false },
                    ["queuedOnly"] = new McpToolParameter { Type = "boolean", Description = "待機中または処理中の注文がある製造設備だけを返すか。既定は false", Required = false },
                    ["includeRecipes"] = new McpToolParameter { Type = "boolean", Description = "各製造設備のレシピの要約を含めるか。既定は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    bool queuedOnly = ToolUtil.GetBool(args, "queuedOnly", false);
                    bool includeRecipes = ToolUtil.GetBool(args, "includeRecipes", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var results = Components.ComplexFabricators.Items
                        .Where(fabricator => MatchesFabricator(fabricator, rect, worldId, query))
                        .Where(fabricator => !queuedOnly || fabricator.HasAnyOrder || QueuedRecipeCount(fabricator) != 0)
                        .OrderBy(fabricator => TargetName(fabricator.gameObject))
                        .Take(limit)
                        .Select(fabricator => FabricatorInfo(fabricator, includeRecipes))
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = results.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["queuedOnly"] = queuedOnly,
                        ["fabricators"] = results
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListRecipes()
        {
            return new McpTool
            {
                Name = "production_recipes_list",
                Hidden = true,
                Group = "production",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "fabricator_recipes_list", "crafting_recipes_list" },
                Tags = new List<string> { "production", "fabricator", "recipe", "queue", "ingredients", "results" },
                Description = "互換用の入口。building_control domain=production action=list_recipes を優先してください。製造設備で使用する ComplexRecipe を recipeId、分類、素材、生成物、研究によるロック、現在のキュー数量とともに列挙します。個別の製造設備、区域、キーワードで絞り込み可能です",
                Parameters = LookupParams(RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["recipeId"] = new McpToolParameter { Type = "string", Description = "レシピ id の完全一致で絞り込み", Required = false },
                    ["categoryId"] = new McpToolParameter { Type = "string", Description = "レシピ分類 id の完全一致で絞り込み", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "レシピ名、recipeId、categoryId、素材、生成物で絞り込み", Required = false },
                    ["queuedOnly"] = new McpToolParameter { Type = "boolean", Description = "キューにあるレシピだけを返すか。既定は false", Required = false },
                    ["includeLocked"] = new McpToolParameter { Type = "boolean", Description = "研究が未解除のレシピを含めるか。既定は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返すレシピ数の上限。既定は 200、最大 1000", Required = false }
                })),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    string recipeId = args["recipeId"]?.ToString();
                    string categoryId = args["categoryId"]?.ToString();
                    string query = args["query"]?.ToString();
                    bool queuedOnly = ToolUtil.GetBool(args, "queuedOnly", false);
                    bool includeLocked = ToolUtil.GetBool(args, "includeLocked", true);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 200, 1000));

                    var recipes = new List<Dictionary<string, object>>();
                    foreach (var fabricator in FindFabricators(args))
                    {
                        foreach (var recipe in fabricator.GetRecipes())
                        {
                            if (!string.IsNullOrWhiteSpace(recipeId) && !string.Equals(recipe.id, recipeId.Trim(), StringComparison.Ordinal))
                                continue;
                            if (!string.IsNullOrWhiteSpace(categoryId) && !string.Equals(recipe.recipeCategoryID, categoryId.Trim(), StringComparison.Ordinal))
                                continue;
                            if (queuedOnly && fabricator.GetRecipeQueueCount(recipe) == 0)
                                continue;
                            if (!includeLocked && !recipe.IsRequiredTechUnlocked())
                                continue;
                            if (!RecipeMatches(recipe, query))
                                continue;

                            recipes.Add(RecipeInfo(fabricator, recipe, includeFabricator: true));
                            if (recipes.Count >= limit)
                                break;
                        }
                        if (recipes.Count >= limit)
                            break;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = recipes.Count,
                        ["queuedOnly"] = queuedOnly,
                        ["includeLocked"] = includeLocked,
                        ["recipes"] = recipes
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetQueue()
        {
            return new McpTool
            {
                Name = "production_queue_set",
                Group = "production",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "fabricator_queue_set", "crafting_queue_set" },
                Tags = new List<string> { "production", "fabricator", "recipe", "queue", "craft" },
                Description = "互換用の入口。building_control domain=production action=set を優先してください。製造設備のレシピキューの数量を設定します: set/add/remove/infinite/clear。製造設備のサイドパネルのキュー増減・無限製造に対応します",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["recipeId"] = new McpToolParameter { Type = "string", Description = "対象の ComplexRecipe.id。先に production_recipes_list で確認可能", Required = true },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "set、add、remove、infinite または clear。既定は set", Required = false, EnumValues = new List<string> { "set", "add", "remove", "infinite", "clear" } },
                    ["count"] = new McpToolParameter { Type = "integer", Description = "set/add/remove の数量。set の既定は 1、add/remove の既定は 1", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "生産キューの誤変更を防ぐため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to change a production queue");

                    var fabricator = FindFabricator(args);
                    if (fabricator == null)
                        return CallToolResult.Error("ComplexFabricator target not found");

                    string recipeId = args["recipeId"]?.ToString();
                    if (string.IsNullOrWhiteSpace(recipeId))
                        return CallToolResult.Error("recipeId is required");

                    var recipe = fabricator.GetRecipe(recipeId.Trim());
                    if (recipe == null)
                        return CallToolResult.Error("recipeId is not available on this fabricator");

                    int before = fabricator.GetRecipeQueueCount(recipe);
                    string mode = (args["mode"]?.ToString() ?? "set").Trim().ToLowerInvariant();
                    int requested = Math.Max(0, ToolUtil.GetInt(args, "count") ?? 1);
                    int next;
                    if (!TryComputeQueueCount(before, mode, requested, out next))
                        return CallToolResult.Error("mode must be set, add, remove, infinite or clear");

                    fabricator.SetRecipeQueueCount(recipe, next);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["fabricator"] = TargetInfo(fabricator.gameObject),
                        ["recipe"] = RecipeInfo(fabricator, recipe, includeFabricator: false),
                        ["mode"] = mode,
                        ["before"] = FormatQueueCount(before),
                        ["after"] = FormatQueueCount(fabricator.GetRecipeQueueCount(recipe)),
                        ["changed"] = before != fabricator.GetRecipeQueueCount(recipe)
                    }, McpJsonUtil.Settings));
                }
            };
        }

    }
}
