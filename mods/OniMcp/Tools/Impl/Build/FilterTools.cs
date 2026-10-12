using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static class FilterTools
    {
        public static McpTool ListFilters()
        {
            return new McpTool
            {
                Name = "filters_list",
                Hidden = true,
                Group = "filters",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "building_filters_list", "filterable_controls_list" },
                Tags = new List<string> { "filters", "filterable", "tree-filter", "element-filter", "side-screen", "conduit" },
                Description = "互換用の入口。building_control domain=filter action=list を優先してください。プレイヤーが設定可能なフィルター（気体・液体・固体の管路フィルター、元素センサー、収納・ツリー形式のフィルター、フラットな tag フィルター）を列挙します",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "フィルター種別: any、single、tree、flat。既定は any", Required = false, EnumValues = new List<string> { "any", "single", "tree", "flat" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、選択済み tag、選択可能な tag で絞り込み", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "選択可能な tag の候補を返すか。既定は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string kind = (args["kind"]?.ToString() ?? "any").Trim().ToLowerInvariant();
                    string query = args["query"]?.ToString();
                    bool includeOptions = ToolUtil.GetBool(args, "includeOptions", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var results = new List<Dictionary<string, object>>();
                    foreach (var building in Components.BuildingCompletes.Items)
                    {
                        var go = building?.gameObject;
                        if (!MatchesTarget(go, rect, worldId))
                            continue;
                        var info = FilterInfo(go, includeOptions);
                        var kinds = (List<string>)info["filterKinds"];
                        if (kinds.Count == 0)
                            continue;
                        if (kind != "any" && !kinds.Contains(kind))
                            continue;
                        if (!MatchesQuery(info, query))
                            continue;

                        results.Add(info);
                        if (results.Count >= limit)
                            break;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = results.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["kind"] = kind,
                        ["filters"] = results
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetSingleFilter()
        {
            return new McpTool
            {
                Name = "filters_single_set",
                Group = "filters",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "element_filter_set", "filterable_set" },
                Tags = new List<string> { "filters", "filterable", "element-filter", "sensor", "conduit" },
                Description = "互換用の入口。building_control domain=filter action=set kind=single を優先してください",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["tag"] = new McpToolParameter { Type = "string", Description = "対象の tag・元素。例: Oxygen、Water、Dirt。clear=true の場合は省略可能", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "true の場合、GameTags.Void・未選択に設定", Required = false }
                }),
                Handler = SetSingleFilter
            };
        }

        public static McpTool SetFilter()
        {
            return new McpTool
            {
                Name = "filter_set",
                Hidden = true,
                Group = "filters",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "filters_set", "building_filter_set" },
                Tags = new List<string> { "filters", "filterable", "tree-filter", "flat-tag-filter", "element-filter", "side-screen" },
                Description = "互換用の入口。building_control domain=filter action=set を優先してください。kind=single は単一選択の Filterable、kind=tree/flat は TreeFilterable/FlatTagFilterable の複数選択タグを設定します。",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "フィルター種別: single、tree または flat", Required = true, EnumValues = new List<string> { "single", "tree", "flat" } },
                    ["tag"] = new McpToolParameter { Type = "string", Description = "kind=single の対象 tag・元素。clear=true の場合は省略可能", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "kind=single では未選択に設定。kind=tree/flat では mode=clear と同じ", Required = false },
                    ["tags"] = new McpToolParameter { Type = "array", Description = "kind=tree/flat の tag 一覧。例: Dirt、Algae、BasicSingleHarvestPlantSeed", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "kind=tree/flat では replace、add、remove または clear。既定は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove", "clear" } }
                }),
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (kind)
                    {
                        case "single":
                            return SetSingleFilter(args);
                        case "tree":
                        case "flat":
                            if (ToolUtil.GetBool(args, "clear", false) && string.IsNullOrWhiteSpace(args["mode"]?.ToString()))
                                args["mode"] = "clear";
                            return SetTreeFilter(args);
                        default:
                            return CallToolResult.Error("kind must be single, tree, or flat");
                    }
                }
            };
        }

        public static McpTool ControlFilter()
        {
            return new McpTool
            {
                Name = "filter_control",
                Hidden = true,
                Group = "filters",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "filters_control", "building_filter_control" },
                Tags = new List<string> { "filters", "filterable", "tree-filter", "flat-tag-filter", "element-filter", "side-screen", "conduit" },
                Description = "フィルターの集約ツール: action=list/set。設定可能なフィルターの読み取り、single/tree/flat のフィルタータグの設定を行います。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list または set", Required = true, EnumValues = new List<string> { "list", "set" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "action=set の対象オブジェクトの InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=set の対象セルの X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=set の対象セルの Y", Required = false },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "action=list では any/single/tree/flat、action=set では single/tree/flat", Required = false, EnumValues = new List<string> { "any", "single", "tree", "flat" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、選択済み tag、選択可能な tag による絞り込み", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "action=list で選択可能な tag の候補を返すか。既定は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定は 100、最大 500", Required = false },
                    ["tag"] = new McpToolParameter { Type = "string", Description = "action=set kind=single の対象 tag・元素。clear=true の場合は省略可能", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "action=set でフィルターをクリア", Required = false },
                    ["tags"] = new McpToolParameter { Type = "array", Description = "action=set kind=tree/flat の tag 一覧", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "action=set kind=tree/flat では replace、add、remove または clear。既定は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove", "clear" } }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListFilters().Handler(args);
                    if (action == "set")
                        return SetFilter().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool SetTreeFilter()
        {
            return new McpTool
            {
                Name = "filters_tree_set",
                Group = "filters",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "tree_filter_set", "flat_tag_filter_set", "multi_filter_set" },
                Tags = new List<string> { "filters", "tree-filter", "flat-tag-filter", "storage", "side-screen" },
                Description = "互換用の入口。building_control domain=filter action=set kind=tree または kind=flat を優先してください",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["tags"] = new McpToolParameter { Type = "array", Description = "tag 一覧。例: Dirt、Algae、BasicSingleHarvestPlantSeed。clear の場合は省略可能", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "replace、add、remove または clear。既定は replace", Required = false, EnumValues = new List<string> { "replace", "add", "remove", "clear" } }
                }),
                Handler = SetTreeFilter
            };
        }

        private static CallToolResult SetSingleFilter(JObject args)
        {
            var go = FindTarget(args);
            if (go == null)
                return CallToolResult.Error("Target not found");
            var filterable = go.GetComponent<Filterable>();
            if (filterable == null)
                return CallToolResult.Error("Target does not expose Filterable");

            Tag before = filterable.SelectedTag;
            if (ToolUtil.GetBool(args, "clear", false))
            {
                filterable.SelectedTag = GameTags.Void;
            }
            else
            {
                string tagName = args["tag"]?.ToString();
                if (string.IsNullOrWhiteSpace(tagName))
                    return CallToolResult.Error("tag is required unless clear=true");
                var tag = new Tag(tagName.Trim());
                if (!SingleFilterOptions(filterable).Contains(tag))
                    return CallToolResult.Error("tag is not currently valid for this Filterable; inspect building_control domain=filter action=list includeOptions=true");
                filterable.SelectedTag = tag;
            }

            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["target"] = TargetInfo(go),
                ["kind"] = "single",
                ["before"] = TagInfo(before),
                ["selected"] = TagInfo(filterable.SelectedTag),
                ["changed"] = before != filterable.SelectedTag
            }, McpJsonUtil.Settings));
        }

        private static CallToolResult SetTreeFilter(JObject args)
        {
            var go = FindTarget(args);
            if (go == null)
                return CallToolResult.Error("Target not found");
            var tree = go.GetComponent<TreeFilterable>();
            if (tree == null)
                return CallToolResult.Error("Target does not expose TreeFilterable");

            string mode = (args["mode"]?.ToString() ?? "replace").Trim().ToLowerInvariant();
            var before = tree.GetTags().Select(TagInfo).ToList();
            var next = new HashSet<Tag>(tree.GetTags());
            var requested = ParseTags(args["tags"]);

            if (mode == "clear" || mode == "replace")
                next.Clear();
            if (mode != "clear" && requested.Count == 0)
                return CallToolResult.Error("tags must contain at least one tag unless mode=clear");

            foreach (var tag in requested)
            {
                if (mode == "remove")
                    next.Remove(tag);
                else
                    next.Add(tag);
            }

            var flat = go.GetComponent<FlatTagFilterable>();
            if (flat != null)
                ApplyFlatTags(flat, next);
            else
                tree.UpdateFilters(next);

            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["target"] = TargetInfo(go),
                ["kind"] = flat != null ? "flat" : "tree",
                ["mode"] = mode,
                ["before"] = before,
                ["selected"] = tree.GetTags().Select(TagInfo).OrderBy(item => item["tag"].ToString()).ToList()
            }, McpJsonUtil.Settings));
        }

        private static Dictionary<string, object> FilterInfo(GameObject go, bool includeOptions)
        {
            var result = TargetInfo(go);
            var kinds = new List<string>();

            var filterable = go.GetComponent<Filterable>();
            if (filterable != null)
            {
                kinds.Add("single");
                var single = new Dictionary<string, object>
                {
                    ["elementState"] = filterable.filterElementState.ToString(),
                    ["selected"] = TagInfo(filterable.SelectedTag)
                };
                if (includeOptions)
                    single["options"] = SingleFilterOptions(filterable).Select(TagInfo).OrderBy(item => item["tag"].ToString()).ToList();
                result["single"] = single;
            }

            var tree = go.GetComponent<TreeFilterable>();
            if (tree != null)
            {
                kinds.Add("tree");
                var treeInfo = new Dictionary<string, object>
                {
                    ["selected"] = tree.GetTags().Select(TagInfo).OrderBy(item => item["tag"].ToString()).ToList(),
                    ["dropIncorrectOnFilterChange"] = tree.dropIncorrectOnFilterChange,
                    ["storageCategories"] = tree.GetFilterStorage()?.storageFilters.Select(TagInfo).OrderBy(item => item["tag"].ToString()).ToList()
                };
                result["tree"] = treeInfo;
            }

            var flat = go.GetComponent<FlatTagFilterable>();
            if (flat != null)
            {
                kinds.Add("flat");
                var flatInfo = new Dictionary<string, object>
                {
                    ["header"] = flat.GetHeaderText(),
                    ["selected"] = flat.selectedTags.Select(TagInfo).OrderBy(item => item["tag"].ToString()).ToList(),
                    ["currentlyUserAssignable"] = flat.currentlyUserAssignable
                };
                if (includeOptions)
                    flatInfo["options"] = flat.tagOptions.Select(TagInfo).OrderBy(item => item["tag"].ToString()).ToList();
                result["flat"] = flatInfo;
            }

            result["filterKinds"] = kinds.Distinct().OrderBy(kind => kind).ToList();
            return result;
        }

        private static List<Tag> SingleFilterOptions(Filterable filterable)
        {
            return filterable.GetTagOptions()
                .SelectMany(group => group.Value)
                .Distinct()
                .ToList();
        }

        private static void ApplyFlatTags(FlatTagFilterable flat, HashSet<Tag> next)
        {
            foreach (var tag in flat.selectedTags.ToList())
                flat.SelectTag(tag, false);
            foreach (var tag in next)
            {
                if (flat.tagOptions.Contains(tag))
                    flat.SelectTag(tag, true);
            }
            flat.GetComponent<TreeFilterable>().UpdateFilters(new HashSet<Tag>(flat.selectedTags));
        }

        private static bool MatchesTarget(GameObject go, Dictionary<string, int> rect, int worldId)
        {
            if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                return false;
            int cell = Grid.PosToCell(go);
            return rect == null || CellInRect(cell, rect, worldId);
        }

        private static GameObject FindTarget(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);

            foreach (var building in Components.BuildingCompletes.Items)
            {
                var go = building?.gameObject;
                if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return go;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return go;
            }

            return null;
        }

        private static bool MatchesQuery(Dictionary<string, object> info, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            string q = query.Trim();
            string serialized = JsonConvert.SerializeObject(info);
            return serialized.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<Tag> ParseTags(JToken token)
        {
            var tags = new List<Tag>();
            if (token == null)
                return tags;
            if (token.Type == JTokenType.Array)
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
            if (!string.IsNullOrWhiteSpace(value))
                tags.Add(new Tag(value.Trim()));
        }

        private static Dictionary<string, object> TagInfo(Tag tag)
        {
            return new Dictionary<string, object>
            {
                ["tag"] = tag.Name,
                ["name"] = SafeProperName(tag),
                ["valid"] = tag.IsValid
            };
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
            var building = go.GetComponent<Building>();
            var kpid = go.GetComponent<KPrefabID>();
            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["prefabId"] = building?.Def?.PrefabID ?? kpid?.PrefabTag.Name ?? go.name,
                ["name"] = ToolUtil.CleanName(go.GetProperName()),
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["worldId"] = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : -1
            };
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象オブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y", Required = false },
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
    }
}
