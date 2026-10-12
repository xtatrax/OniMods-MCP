using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class InventoryTools
    {
        public static McpTool ControlResources()
        {
            return new McpTool
            {
                Name = "resources_control",
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "inventory_control", "resource_management_control" },
                Tags = new List<string> { "resources", "inventory", "food", "items", "search", "pin", "notification" },
                Description = "資源の統合窓口: action=inventory/food/search_items/pins/set_pin。set_pin の書き込みには confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "inventory、food、search_items、pins、set_pin のいずれか", Required = true, EnumValues = new List<string> { "inventory", "food", "search_items", "pins", "set_pin" } },
                    ["resource"] = new McpToolParameter { Type = "string", Description = "inventory/search_items/set_pin の場合、資源、アイテム名、prefabId、元素、tag で絞り込み/資源を指定", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "search_items/pins の場合、アイテム名、prefabId、元素、tag、資源名であいまい検索", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID で絞り込み。省略時は全ワールドまたは現在アクティブなワールドを返す", Required = false },
                    ["includeStored"] = new McpToolParameter { Type = "boolean", Description = "inventory/search_items の場合、コンテナ/複製人間が保管しているアイテムを含めるか", Required = false },
                    ["looseOnly"] = new McpToolParameter { Type = "boolean", Description = "search_items の場合、地面に散らばっているアイテムのみを返す。includeStored=false と同じ", Required = false },
                    ["foodDetail"] = new McpToolParameter { Type = "string", Description = "食料の返却粒度: summary（既定、集計のみ）、types（種類別）、full（現物サンプルと根拠）", Required = false, EnumValues = new List<string> { "summary", "types", "full" } },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "探索済みセル内の資源のみを集計するか。既定値は true。デバッグ時は false を指定可能", Required = false },
                    ["includeUnpinned"] = new McpToolParameter { Type = "boolean", Description = "pins の場合、固定も通知もされていない発見済み資源を含めるか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "最大返却数。各 action は元のツールの既定値と上限を使用", Required = false },
                    ["pinned"] = new McpToolParameter { Type = "boolean", Description = "set_pin の場合、資源パネルに固定するか。省略時は変更しない", Required = false },
                    ["notify"] = new McpToolParameter { Type = "boolean", Description = "set_pin の場合、資源通知を有効にするか。省略時は変更しない", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "set_pin は資源パネルの切り替え設定の変更を確認するため、true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "inventory":
                        case "food":
                        case "search_items":
                            return ReadResourcesControl().Handler(args);
                        case "pins":
                        case "pin_list":
                            {
                                var forwarded = args != null ? (JObject)args.DeepClone() : new JObject();
                                forwarded["action"] = "list";
                                return ControlResourcePin().Handler(forwarded);
                            }
                        case "set_pin":
                        case "pin_set":
                            {
                                var forwarded = args != null ? (JObject)args.DeepClone() : new JObject();
                                forwarded["action"] = "set";
                                return ControlResourcePin().Handler(forwarded);
                            }
                        default:
                            return CallToolResult.Error("action must be inventory, food, search_items, pins, or set_pin");
                    }
                }
            };
        }

        public static McpTool ReadResourcesControl()
        {
            return new McpTool
            {
                Name = "resources_read_control",
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Tags = new List<string> { "resources", "inventory", "food", "items", "search", "资源", "库存", "食物", "搜索" },
                Description = "資源読み取りの統合窓口: action=inventory で資源の在庫を読み取り。action=food で食料の在庫を読み取り。action=search_items でマップ上の拾得可能なアイテムを検索",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "inventory、food、search_items のいずれか", Required = true, EnumValues = new List<string> { "inventory", "food", "search_items" } },
                    ["resource"] = new McpToolParameter { Type = "string", Description = "action=inventory/search_items の場合、資源、アイテム名、prefabId、元素で絞り込み", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=search_items の場合、アイテム名、prefabId、元素、tag であいまい検索", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID で絞り込み。省略時は全ワールドを返す", Required = false },
                    ["includeStored"] = new McpToolParameter { Type = "boolean", Description = "action=inventory/search_items の場合、コンテナ/複製人間が保管しているアイテムを含めるか", Required = false },
                    ["looseOnly"] = new McpToolParameter { Type = "boolean", Description = "action=search_items の場合、地面に散らばっているアイテムのみを返す。includeStored=false と同じ", Required = false },
                    ["foodDetail"] = new McpToolParameter { Type = "string", Description = "食料の返却粒度: summary（既定、集計のみ）、types（種類別）、full（現物サンプルと根拠）", Required = false, EnumValues = new List<string> { "summary", "types", "full" } },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "探索済みセル内の資源のみを返すか。既定値は true。デバッグ時は false を指定可能", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "最大返却数。各 action は元のツールの既定値と上限を使用", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "inventory")
                        return GetInventory().Handler(args);
                    if (action == "food")
                        return GetFoodInventory().Handler(args);
                    if (action == "search_items")
                    {
                        var forwarded = args != null ? (JObject)args.DeepClone() : new JObject();
                        forwarded.Remove("action");
                        return SearchItems().Handler(forwarded);
                    }

                    return CallToolResult.Error("action must be inventory, food, or search_items");
                }
            };
        }

        public static McpTool GetInventory()
        {
            return new McpTool
            {
                Name = "resources_inventory",
                Hidden = true,
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "get_inventory" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。read_control domain=resources action=inventory を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["resource"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "資源、アイテム名、prefabId で絞り込み。省略時はすべて返す",
                        Required = false
                    },
                    ["worldId"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "ワールド ID で絞り込み。省略時は全ワールドを返す",
                        Required = false
                    },
                    ["includeStored"] = new McpToolParameter
                    {
                        Type = "boolean",
                        Description = "コンテナ/複製人間が保管しているアイテムを含めるか。既定値は true",
                        Required = false
                    },
                    ["limit"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "資源グループの最大返却数。既定値は 100、上限は 500",
                        Required = false
                    },
                    ["visibleOnly"] = new McpToolParameter
                    {
                        Type = "boolean",
                        Description = "探索済みセル内の資源のみを集計するか。既定値は true。デバッグ時は false を指定可能",
                        Required = false
                    }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    string filter = (args["resource"] ?? args["query"])?.ToString()?.ToLowerInvariant();
                    int? worldId = TryGetInt(args, "worldId");
                    bool includeStored = TryGetBool(args, "includeStored", true);
                    bool visibleOnly = TryGetBool(args, "visibleOnly", true);
                    int limit = ClampLimit(args, 100, 500);

                    var groups = new Dictionary<string, InventoryAggregate>();
                    int scanned = 0;

                    foreach (var pickupable in Components.Pickupables.Items)
                    {
                        if (pickupable == null || pickupable.gameObject == null) continue;
                        scanned++;

                        var primary = pickupable.PrimaryElement;
                        if (primary == null) continue;

                        bool stored = pickupable.storage != null || pickupable.KPrefabID.HasTag(GameTags.Stored);
                        if (!includeStored && stored) continue;

                        int cell = ToolUtil.PickupableCell(pickupable);
                        if (!ToolUtil.VisibleCellAllowed(cell, visibleOnly))
                            continue;
                        int itemWorldId = Grid.IsValidCell(cell) ? Grid.WorldIdx[cell] : pickupable.GetMyWorldId();
                        if (worldId.HasValue && itemWorldId != worldId.Value) continue;

                        string prefabId = pickupable.KPrefabID?.PrefabTag.Name ?? pickupable.name;
                        string name = ToolUtil.CleanName(pickupable.GetProperName());
                        string elementId = primary.ElementID.ToString();

                        if (!string.IsNullOrEmpty(filter) &&
                            !prefabId.ToLowerInvariant().Contains(filter) &&
                            !name.ToLowerInvariant().Contains(filter) &&
                            !elementId.ToLowerInvariant().Contains(filter))
                            continue;

                        string key = prefabId + "|" + elementId;
                        InventoryAggregate aggregate;
                        if (!groups.TryGetValue(key, out aggregate))
                        {
                            aggregate = new InventoryAggregate
                            {
                                Name = name,
                                PrefabId = prefabId,
                                ElementId = elementId,
                                WorldIds = new HashSet<int>()
                            };
                            groups[key] = aggregate;
                        }

                        float mass = SafeFloat(primary.Mass);
                        aggregate.Count++;
                        aggregate.TotalMassKg += mass;
                        aggregate.TotalUnits += SafeFloat(primary.Units);
                        aggregate.StoredCount += stored ? 1 : 0;
                        aggregate.LooseCount += stored ? 0 : 1;
                        aggregate.WorldIds.Add(itemWorldId);

                        var edible = pickupable.GetComponent<Edible>();
                        if (edible != null)
                            aggregate.TotalCaloriesKcal += SafeFloat(edible.Calories) / 1000f;

                        if (Grid.IsValidCell(cell))
                        {
                            aggregate.SampleCell = cell;
                            aggregate.SampleX = Grid.CellColumn(cell);
                            aggregate.SampleY = Grid.CellRow(cell);
                        }
                    }

                    var items = groups.Values
                        .OrderByDescending(item => item.TotalMassKg)
                        .Take(limit)
                        .Select(item => item.ToDictionary())
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["scannedPickupables"] = scanned,
                        ["visibleOnly"] = visibleOnly,
                        ["totalGroups"] = groups.Count,
                        ["returned"] = items.Count,
                        ["items"] = items
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetFoodInventory()
        {
            return new McpTool
            {
                Name = "resources_food",
                Hidden = true,
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "get_food_inventory" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。read_control domain=resources action=food を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["worldId"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "ワールド ID で絞り込み。省略時は全ワールドを返す",
                        Required = false
                    },
                    ["limit"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "食料の種類の最大返却数。既定値は 100、上限は 500",
                        Required = false
                    },
                    ["foodDetail"] = new McpToolParameter { Type = "string", Description = "食料の返却粒度: summary（既定、集計のみ）、types（種類別）、full（現物サンプルと根拠）", Required = false, EnumValues = new List<string> { "summary", "types", "full" } },
                    ["visibleOnly"] = new McpToolParameter
                    {
                        Type = "boolean",
                        Description = "探索済みセル内の食料のみを集計するか。既定値は true。デバッグ時は false を指定可能",
                        Required = false
                    }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    int? worldId = TryGetInt(args, "worldId");
                    bool visibleOnly = TryGetBool(args, "visibleOnly", true);
                    string foodDetail = (args["foodDetail"]?.ToString() ?? "summary").Trim().ToLowerInvariant();
                    if (foodDetail != "summary" && foodDetail != "types" && foodDetail != "full")
                        return CallToolResult.Error("foodDetail must be summary, types, or full");
                    bool full = foodDetail == "full";
                    int limit = ClampLimit(args, 100, 500);
                    var groups = new Dictionary<string, FoodAggregate>();
                    double totalCaloriesKcal = 0d;
                    var access = new FoodAccessBreakdown();
                    var accessReader = new FoodAvailabilityReader();

                    foreach (var edible in Components.Edibles.Items)
                    {
                        if (edible == null || edible.gameObject == null) continue;

                        var pickupable = edible.GetComponent<Pickupable>();
                        var primary = edible.GetComponent<PrimaryElement>();
                        int cell = pickupable != null ? ToolUtil.PickupableCell(pickupable) : Grid.PosToCell(edible);
                        if (!ToolUtil.VisibleCellAllowed(cell, visibleOnly))
                            continue;
                        int itemWorldId = Grid.IsValidCell(cell) ? Grid.WorldIdx[cell] : edible.GetMyWorldId();
                        if (worldId.HasValue && itemWorldId != worldId.Value) continue;

                        string prefabId = edible.GetComponent<KPrefabID>()?.PrefabTag.Name ?? edible.name;
                        string name = ToolUtil.CleanName(edible.GetProperName());
                        string key = prefabId;

                        FoodAggregate aggregate;
                        if (!groups.TryGetValue(key, out aggregate))
                        {
                            aggregate = new FoodAggregate
                            {
                                Name = name,
                                PrefabId = prefabId,
                                Quality = edible.GetQuality(),
                                Morale = edible.GetMorale(),
                                WorldIds = new HashSet<int>()
                            };
                            groups[key] = aggregate;
                        }

                        double caloriesKcal = SafeFloat(edible.Calories) / 1000d;
                        totalCaloriesKcal += caloriesKcal;
                        aggregate.Count++;
                        aggregate.TotalCaloriesKcal += caloriesKcal;
                        aggregate.TotalMassKg += primary != null ? SafeFloat(primary.Mass) : 0f;
                        aggregate.StoredCount += pickupable != null && pickupable.storage != null ? 1 : 0;
                        Dictionary<string, object> evidence;
                        var observation = accessReader.Read(edible, pickupable, itemWorldId, out evidence, full && aggregate.LocationSamples.Count < 4);
                        access.Add(observation);
                        aggregate.Access.Add(observation);
                        if (full && aggregate.LocationSamples.Count < 4)
                        {
                            evidence["id"] = edible.GetComponent<KPrefabID>()?.InstanceID ?? edible.GetInstanceID();
                            aggregate.LocationSamples.Add(evidence);
                        }
                        aggregate.WorldIds.Add(itemWorldId);
                    }

                    var foods = groups.Values
                        .OrderByDescending(food => food.TotalCaloriesKcal)
                        .Take(foodDetail == "summary" ? 0 : limit)
                        .Select(food => food.ToDictionary(full))
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["totalCaloriesKcal"] = Math.Round(totalCaloriesKcal, 1),
                        ["visibleOnly"] = visibleOnly,
                        ["scope"] = "Components.Edibles in requested worlds, filtered by visibleOnly; totals include all food types before limit",
                        ["access"] = access.ToDictionary(full),
                        ["foodDetail"] = foodDetail,
                        ["worldId"] = worldId,
                        ["observedAtUtc"] = System.DateTime.UtcNow.ToString("o"),
                        ["cycle"] = GameUtil.GetCurrentCycle(),
                        ["timeOfDayPercent"] = GameClock.Instance != null ? (object)Math.Round(GameClock.Instance.GetCurrentCycleAsPercentage() * 100f, 2) : null,
                        ["foodTypes"] = groups.Count,
                        ["returned"] = foods.Count,
                        ["foods"] = foods
                    };

                    if (foodDetail == "summary") result.Remove("foods");
                    if (full) result["nativeRations"] = FoodAvailabilityReader.NativeRations(worldId);
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SearchItems()
        {
            return new McpTool
            {
                Name = "items_search",
                Hidden = true,
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "find_items", "resources_find_items", "pickupables_search", "map_items_search" },
                Tags = new List<string> { "items", "pickupables", "resources", "search", "map", "物品", "搜索", "全图" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。read_control domain=resources action=search_items を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "アイテム名、prefabId、元素、tag であいまい検索。省略時はすべて返す", Required = false },
                    ["resource"] = new McpToolParameter { Type = "string", Description = "query の別名。Dirt、Water、CopperOre などの資源 tag の検索に適する", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID で絞り込み。省略時は全ワールドを検索", Required = false },
                    ["includeStored"] = new McpToolParameter { Type = "boolean", Description = "収納庫/複製人間/設備内のアイテムを含めるか。既定値は true", Required = false },
                    ["looseOnly"] = new McpToolParameter { Type = "boolean", Description = "地面に散らばっているアイテムのみを返す。includeStored=false と同じ", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "探索済みセル内のアイテムのみを返すか。既定値は true。デバッグ時は false を指定可能", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "インスタンスの最大返却数。既定値は 120、上限は 1000", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    string query = (args["query"] ?? args["resource"])?.ToString();
                    int? worldId = TryGetInt(args, "worldId");
                    bool looseOnly = TryGetBool(args, "looseOnly", false);
                    bool includeStored = !looseOnly && TryGetBool(args, "includeStored", true);
                    bool visibleOnly = TryGetBool(args, "visibleOnly", true);
                    int limit = ClampLimit(args, 120, 1000);

                    int scanned = 0;
                    int matched = 0;
                    var results = new List<Dictionary<string, object>>();

                    foreach (var pickupable in Components.Pickupables.Items)
                    {
                        if (pickupable == null || pickupable.gameObject == null)
                            continue;
                        scanned++;

                        bool stored = pickupable.storage != null || (pickupable.KPrefabID != null && pickupable.KPrefabID.HasTag(GameTags.Stored));
                        if (!includeStored && stored)
                            continue;
                        int cell = ToolUtil.PickupableCell(pickupable);
                        if (!ToolUtil.VisibleCellAllowed(cell, visibleOnly))
                            continue;

                        var info = ItemSearchInfo(pickupable);
                        int itemWorldId = info.ContainsKey("worldId") && info["worldId"] != null ? Convert.ToInt32(info["worldId"]) : pickupable.GetMyWorldId();
                        if (worldId.HasValue && itemWorldId != worldId.Value)
                            continue;

                        if (!ItemMatches(info, query))
                            continue;

                        matched++;
                        if (results.Count < limit)
                            results.Add(info);
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["query"] = string.IsNullOrWhiteSpace(query) ? null : query,
                        ["worldId"] = worldId.HasValue ? (object)worldId.Value : null,
                        ["includeStored"] = includeStored,
                        ["visibleOnly"] = visibleOnly,
                        ["scannedPickupables"] = scanned,
                        ["matched"] = matched,
                        ["returned"] = results.Count,
                        ["truncated"] = Math.Max(0, matched - results.Count),
                        ["items"] = results
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static int? TryGetInt(JObject args, string key)
        {
            int value;
            return args[key] != null && int.TryParse(args[key].ToString(), out value) ? value : (int?)null;
        }

        private static bool TryGetBool(JObject args, string key, bool defaultValue)
        {
            bool value;
            return args[key] != null && bool.TryParse(args[key].ToString(), out value) ? value : defaultValue;
        }

        private static int ClampLimit(JObject args, int defaultValue, int max)
        {
            int value;
            if (args["limit"] != null && int.TryParse(args["limit"].ToString(), out value))
                return Math.Max(1, Math.Min(value, max));
            return defaultValue;
        }

        private static float SafeFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static WorldInventory GetWorldInventory(JObject args, out int worldId, out string error)
        {
            worldId = -1;
            error = null;
            if (ClusterManager.Instance == null)
            {
                error = "ClusterManager not initialized";
                return null;
            }

            worldId = TryGetInt(args, "worldId") ?? ClusterManager.Instance.activeWorldId;
            var world = ClusterManager.Instance.GetWorld(worldId);
            if (world == null || world.worldInventory == null)
            {
                error = $"World inventory not found: {worldId}";
                return null;
            }

            return world.worldInventory;
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(query) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EqualsIgnoreCase(string value, string query)
        {
            return string.Equals(value, query, StringComparison.OrdinalIgnoreCase);
        }


    }
}
