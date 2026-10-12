using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class StorageTools
    {
        public static McpTool GetStorageList()
        {
            return new McpTool
            {
                Name = "resources_storage_list",
                Hidden = true,
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "storage_list" },
                Description = "互換用の入口。building_control domain=storage action=list を優先してください。収納庫・液体貯蔵庫・気体貯蔵庫などの収納設備を、容量、収納済み質量、フィルターの要約とともに列挙します",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["resource"] = new McpToolParameter { Type = "string", Description = "収納フィルターのタグまたは設備名で絞り込み", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID で絞り込み", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す収納設備数の上限。既定は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    string filter = args["resource"]?.ToString()?.ToLowerInvariant();
                    int? worldId = ToolUtil.GetInt(args, "worldId");
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var storages = GetStorageBuildings()
                        .Where(item => !worldId.HasValue || item.WorldId == worldId.Value)
                        .Where(item => string.IsNullOrEmpty(filter) || item.Matches(filter))
                        .Take(limit)
                        .Select(item => item.ToDictionary(includeItems: false))
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["returned"] = storages.Count,
                        ["storages"] = storages
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetStorageDetail()
        {
            return new McpTool
            {
                Name = "resources_storage_detail",
                Hidden = true,
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "storage_detail" },
                Description = "互換用の入口。building_control domain=storage action=detail を優先してください。個別の収納設備のフィルタータグ、在庫アイテム、容量を読み取ります",
                Parameters = StorageLookupParams(),
                Handler = args =>
                {
                    StorageInfo target;
                    string error;
                    if (!TryFindStorage(args, out target, out error))
                        return CallToolResult.Error(error);
                    return CallToolResult.Text(JsonConvert.SerializeObject(target.ToDictionary(includeItems: true), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetStorageFilter()
        {
            return new McpTool
            {
                Name = "resources_storage_set_filter",
                Hidden = true,
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Description = "互換用の入口。building_control domain=storage action=set_filter を優先してください。収納設備の資源フィルタータグを設定します。既定では現在のフィルターを置き換え、add/remove も使用可能です",
                Parameters = StorageLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["tags"] = new McpToolParameter { Type = "array", Description = "資源 Tag の一覧。例: Dirt、Algae、SandStone", Required = true },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "replace、add、remove。既定は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove" } }
                }),
                Handler = args =>
                {
                    StorageInfo target;
                    string error;
                    if (!TryFindStorage(args, out target, out error))
                        return CallToolResult.Error(error);

                    var filterable = target.GameObject.GetComponent<TreeFilterable>();
                    if (filterable == null)
                        return CallToolResult.Error("Selected building does not expose TreeFilterable");

                    var tags = ParseTags(args["tags"]);
                    if (tags.Count == 0)
                        return CallToolResult.Error("tags must contain at least one resource tag");

                    string mode = (args["mode"]?.ToString() ?? "replace").ToLowerInvariant();
                    var next = new HashSet<Tag>(filterable.GetTags());
                    if (mode == "replace")
                        next.Clear();

                    foreach (var tag in tags)
                    {
                        if (mode == "remove")
                            next.Remove(tag);
                        else
                            next.Add(tag);
                    }

                    filterable.UpdateFilters(next);
                    target.AcceptedTags = filterable.GetTags().Select(tag => tag.Name).OrderBy(tag => tag).ToList();
                    return CallToolResult.Text(JsonConvert.SerializeObject(target.ToDictionary(includeItems: false), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlStorage()
        {
            return new McpTool
            {
                Name = "storage_control",
                Hidden = true,
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "resources_storage_control", "storage_filter_control" },
                Tags = new List<string> { "resources", "storage", "filter", "inventory" },
                Description = "収納設備の集約ツール: action=list/detail/set_filter。収納の一覧・詳細の読み取り、収納フィルタータグの変更を行います。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、detail または set_filter", Required = true, EnumValues = new List<string> { "list", "detail", "set_filter" } },
                    ["resource"] = new McpToolParameter { Type = "string", Description = "action=list で収納フィルターのタグまたは設備名による絞り込み", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。detail/set_filter の query のみの検索では現在アクティブなワールドが既定", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定は 100、最大 500", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=detail/set_filter の設備の InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=detail/set_filter の設備があるセルの X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=detail/set_filter の設備があるセルの Y", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=detail/set_filter でローカライズ名または prefabId による検索", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "query の別名", Required = false },
                    ["tags"] = new McpToolParameter { Type = "array", Description = "action=set_filter の資源 Tag 一覧。例: Dirt、Algae、SandStone", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "action=set_filter では replace、add、remove。既定は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove" } }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return GetStorageList().Handler(args);
                    if (action == "detail")
                        return GetStorageDetail().Handler(args);
                    if (action == "set_filter" || action == "set")
                        return SetStorageFilter().Handler(args);
                    return CallToolResult.Error("action must be list, detail, or set_filter");
                }
            };
        }

        public static McpTool ControlStorageSystem()
        {
            return new McpTool
            {
                Name = "storage_system_control",
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "storage_controls", "inventory_storage_control" },
                Tags = new List<string> { "resources", "storage", "filter", "inventory", "side-screen", "receptacle" },
                Description = "収納・フィルター・エンティティ受け入れ枠の集約ツール: domain=storage/tile_selection/filter/receptacle を選び、action 引数で対応するサブ操作を実行します。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "storage、tile_selection、filter または receptacle", Required = true, EnumValues = new List<string> { "storage", "tile_selection", "filter", "receptacle" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "サブツールの操作: storage=list/detail/set_filter、tile_selection=list/set/batch、filter=list/set、receptacle=list/request/cancel_request/remove_occupant/cancel_remove/batch", Required = false },
                    ["resource"] = new McpToolParameter { Type = "string", Description = "domain=storage action=list で収納フィルターのタグまたは設備名による絞り込み", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "domain=tile_selection/filter/receptacle action=list で名前、prefabId、tag による絞り込み", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "一覧取得時に選択可能な項目を返すか", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "一覧取得時に返す件数の上限", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象の InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象または区域の X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象または区域の Y", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の始点 X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の始点 Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の終点 X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の終点 Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID", Required = false },
                    ["tag"] = new McpToolParameter { Type = "string", Description = "domain=filter action=set kind=single の対象 tag・元素", Required = false },
                    ["tags"] = new McpToolParameter { Type = "array", Description = "domain=storage/filter の書き込み時の tag 一覧", Required = false },
                    ["itemTag"] = new McpToolParameter { Type = "string", Description = "domain=tile_selection action=set の対象アイテムの tag", Required = false },
                    ["entityTag"] = new McpToolParameter { Type = "string", Description = "domain=receptacle action=request のエンティティの tag", Required = false },
                    ["additionalTag"] = new McpToolParameter { Type = "string", Description = "domain=receptacle action=request の追加 tag", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "フィルターの書き込みモード: replace、add、remove または clear", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "単一選択または StorageTile の対象をクリア", Required = false },
                    ["replaceExistingRequest"] = new McpToolParameter { Type = "boolean", Description = "domain=receptacle action=request で既存の要求を置き換えるか。既定は true", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Description = "バッチ操作の項目", Required = false },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "バッチ操作の既定の引数", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "defaults の別名", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "書き込み操作の確認。サブツールが要求する場合は true が必須", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(domain))
                    {
                        string legacyKind = (args["kind"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                        if (legacyKind == "storage" || legacyKind == "building" || legacyKind == "buildings" ||
                            legacyKind == "tile_selection" || legacyKind == "tile" || legacyKind == "storage_tile" || legacyKind == "single_item" ||
                            legacyKind == "filter" || legacyKind == "filters" ||
                            legacyKind == "receptacle" || legacyKind == "receptacles" || legacyKind == "entity_slot" || legacyKind == "entity_slots")
                        {
                            domain = legacyKind;
                        }
                    }

                    switch (domain)
                    {
                        case "storage":
                        case "building":
                        case "buildings":
                            return ControlStorage().Handler(args);
                        case "tile_selection":
                        case "tile":
                        case "storage_tile":
                        case "single_item":
                            return ReceptacleTools.ControlStorageTileSelection().Handler(args);
                        case "filter":
                        case "filters":
                            return FilterTools.ControlFilter().Handler(args);
                        case "receptacle":
                        case "receptacles":
                        case "entity_slot":
                        case "entity_slots":
                            return ReceptacleTools.ControlReceptacle().Handler(args);
                        default:
                            return CallToolResult.Error("domain must be storage, tile_selection, filter, or receptacle");
                    }
                }
            };
        }

        private static Dictionary<string, McpToolParameter> StorageLookupParams(Dictionary<string, McpToolParameter> extra = null)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "設備の InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "設備があるセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "設備があるセルの Y", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "ローカライズされた設備名または prefabId で検索。例: 洗手盆 / WashBasin", Required = false },
                ["name"] = new McpToolParameter { Type = "string", Description = "query の別名", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。query のみの場合は現在アクティブなワールドが既定", Required = false }
            };
            if (extra != null)
            {
                foreach (var item in extra)
                    parameters[item.Key] = item.Value;
            }
            return parameters;
        }

        private static List<StorageInfo> GetStorageBuildings()
        {
            var result = new List<StorageInfo>();
            var seen = new HashSet<int>();

            foreach (var building in Components.BuildingCompletes.Items)
            {
                if (building == null || building.gameObject == null) continue;
                var go = building.gameObject;
                var storage = go.GetComponent<Storage>();
                if (storage == null) continue;
                var id = go.GetComponent<KPrefabID>()?.InstanceID ?? go.GetInstanceID();
                if (!seen.Add(id)) continue;

                var pos = go.transform.GetPosition();
                var cell = Grid.PosToCell(pos);
                var filterable = go.GetComponent<TreeFilterable>();
                result.Add(new StorageInfo
                {
                    Id = id,
                    GameObject = go,
                    Storage = storage,
                    Cell = cell,
                    X = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : Mathf.RoundToInt(pos.x),
                    Y = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : Mathf.RoundToInt(pos.y),
                    WorldId = go.GetMyWorldId(),
                    Name = ToolUtil.CleanName(go.GetProperName()),
                    PrefabId = go.GetComponent<KPrefabID>()?.PrefabTag.Name ?? go.name,
                    AcceptedTags = filterable != null ? filterable.GetTags().Select(tag => tag.Name).OrderBy(tag => tag).ToList() : new List<string>()
                });
            }

            return result.OrderBy(item => item.PrefabId).ThenBy(item => item.Id).ToList();
        }

        private static List<Tag> ParseTags(Newtonsoft.Json.Linq.JToken token)
        {
            var tags = new List<Tag>();
            if (token == null)
                return tags;

            if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array)
            {
                foreach (var item in token)
                    AddTag(tags, item?.ToString());
            }
            else
            {
                foreach (var value in token.ToString().Split(','))
                    AddTag(tags, value);
            }

            return tags;
        }

        private static void AddTag(List<Tag> tags, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            tags.Add(new Tag(value.Trim()));
        }

        private class StorageInfo
        {
            public int Id;
            public GameObject GameObject;
            public Storage Storage;
            public int Cell;
            public int X;
            public int Y;
            public int WorldId;
            public string Name;
            public string PrefabId;
            public List<string> AcceptedTags;

            public bool Matches(string filter)
            {
                return Name.ToLowerInvariant().Contains(filter) ||
                       PrefabId.ToLowerInvariant().Contains(filter) ||
                       AcceptedTags.Any(tag => tag.ToLowerInvariant().Contains(filter));
            }

            public bool MatchesIdentity(string query)
            {
                return Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                       PrefabId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            public Dictionary<string, object> ToDictionary(bool includeItems)
            {
                var data = new Dictionary<string, object>
                {
                    ["id"] = Id,
                    ["name"] = Name,
                    ["prefabId"] = PrefabId,
                    ["position"] = new { x = X, y = Y },
                    ["worldId"] = WorldId,
                    ["capacityKg"] = Math.Round(ToolUtil.SafeFloat(Storage.capacityKg), 3),
                    ["massStoredKg"] = Math.Round(ToolUtil.SafeFloat(Storage.MassStored()), 3),
                    ["acceptedTags"] = AcceptedTags
                };

                var locker = GameObject.GetComponent<StorageLocker>();
                if (locker != null)
                    data["userMaxCapacityKg"] = Math.Round(ToolUtil.SafeFloat(locker.UserMaxCapacity), 3);

                if (includeItems)
                {
                    data["items"] = Storage.items
                        .Where(item => item != null)
                        .Select(item =>
                        {
                            var primary = item.GetComponent<PrimaryElement>();
                            var prefab = item.GetComponent<KPrefabID>();
                            return new Dictionary<string, object>
                            {
                                ["name"] = ToolUtil.CleanName(item.GetProperName()),
                                ["prefabId"] = prefab?.PrefabTag.Name ?? item.name,
                                ["massKg"] = Math.Round(primary != null ? ToolUtil.SafeFloat(primary.Mass) : 0f, 3)
                            };
                        })
                        .ToList();
                }

                return data;
            }
        }
    }
}
