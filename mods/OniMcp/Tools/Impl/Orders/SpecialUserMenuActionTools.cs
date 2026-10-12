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
    public static class SpecialUserMenuActionTools
    {
        public static McpTool ControlMutantSeed()
        {
            return new McpTool
            {
                Name = "mutant_seed_control",
                Group = "production",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "accept_mutant_seeds_control", "mutated_seed_toggle_control" },
                Tags = new List<string> { "production", "farming", "mutant-seeds", "fish-feeder", "spice-grinder", "fabricator", "user-menu" },
                Description = "互換用の入口。building_control domain=production action=mutant_seed_list/mutant_seed_set を優先して使う。変異種子の受け入れ設定。action=list でユーザーメニューの設定を照会し、action=set で受け入れ・拒否を設定する",
                Parameters = MutantSeedControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                            return ListMutantSeedControls().Handler(args);
                        case "set":
                            return SetMutantSeedControl().Handler(args);
                        default:
                            return CallToolResult.Error("action must be list or set");
                    }
                }
            };
        }

        public static McpTool ListMutantSeedControls()
        {
            return new McpTool
            {
                Name = "mutant_seed_controls_list",
                Group = "production",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "accept_mutant_seeds_list", "mutated_seed_toggles_list" },
                Tags = new List<string> { "production", "farming", "mutant-seeds", "fish-feeder", "spice-grinder", "fabricator", "user-menu" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=production action=mutant_seed_list を使用すること",
                Hidden = true,
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、コンポーネントの種類で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    var rows = FindMutantSeedTargets(args)
                        .Select(MutantSeedInfo)
                        .Where(info => MatchesQuery(info, args["query"]?.ToString()))
                        .Take(ToolUtil.ClampLimit(args, 100, 500))
                        .ToList();
                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = rows.Count,
                        ["controls"] = rows,
                        ["setTool"] = "building_control domain=production action=mutant_seed_set"
                    });
                }
            };
        }

        public static McpTool SetMutantSeedControl()
        {
            return new McpTool
            {
                Name = "mutant_seed_control_set",
                Group = "production",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "accept_mutant_seeds_set", "mutated_seed_toggle_set" },
                Tags = new List<string> { "production", "farming", "mutant-seeds", "fish-feeder", "spice-grinder", "fabricator", "user-menu" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=production action=mutant_seed_set を使用すること",
                Hidden = true,
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["accept"] = new McpToolParameter { Type = "boolean", Description = "true=変異種子を受け入れる、false=変異種子を拒否する", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "ユーザーメニューの設定変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    var go = FindMutantSeedTarget(args);
                    if (go == null)
                        return CallToolResult.Error("Target mutant seed control not found");
                    bool accept = ToolUtil.GetBool(args, "accept", true);
                    var before = MutantSeedInfo(go);
                    string error = SetMutantSeedAccept(go, accept);
                    if (error != null)
                        return CallToolResult.Error(error);
                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["after"] = MutantSeedInfo(go)
                    });
                }
            };
        }

        public static McpTool ControlRocketUsage()
        {
            return new McpTool
            {
                Name = "rocket_usage_control",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "rocket_usage_restriction_control", "rocket_controlled_building_control" },
                Tags = new List<string> { "rocket", "usage", "restriction", "control-station", "user-menu" },
                Description = "ロケット内部設備の使用制限の集約ツール。action=list で状態を照会し、action=set で RocketControlStation の制限を受けるかを設定する",
                Parameters = RocketUsageControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                            return ListRocketUsageControls().Handler(args);
                        case "set":
                            return SetRocketUsageControl().Handler(args);
                        default:
                            return CallToolResult.Error("action must be list or set");
                    }
                }
            };
        }

        public static McpTool ListRocketUsageControls()
        {
            return new McpTool
            {
                Name = "rocket_usage_controls_list",
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "rocket_usage_restrictions_list", "rocket_controlled_buildings_list" },
                Tags = new List<string> { "rocket", "usage", "restriction", "control-station", "user-menu" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=rocket rocketDomain=usage action=list を使用すること",
                Hidden = true,
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、状態で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    var rows = FindRocketUsageTargets(args)
                        .Select(go => RocketUsageInfo(go.GetSMI<RocketUsageRestriction.StatesInstance>()))
                        .Where(info => MatchesQuery(info, args["query"]?.ToString()))
                        .Take(ToolUtil.ClampLimit(args, 100, 500))
                        .ToList();
                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = rows.Count,
                        ["controls"] = rows,
                        ["setTool"] = "rocket_usage_control"
                    });
                }
            };
        }

        public static McpTool SetRocketUsageControl()
        {
            return new McpTool
            {
                Name = "rocket_usage_control_set",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "rocket_usage_restriction_set", "rocket_controlled_building_set" },
                Tags = new List<string> { "rocket", "usage", "restriction", "control-station", "user-menu" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=rocket rocketDomain=usage action=set を使用すること",
                Hidden = true,
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["controlled"] = new McpToolParameter { Type = "boolean", Description = "true=制御装置の制限を受ける、false=制限を受けない", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "ロケット内部設備の制限変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    var go = FindRocketUsageTarget(args);
                    var smi = go?.GetSMI<RocketUsageRestriction.StatesInstance>();
                    if (smi == null)
                        return CallToolResult.Error("Target rocket usage control not found");
                    var before = RocketUsageInfo(smi);
                    smi.isControlled = ToolUtil.GetBool(args, "controlled", true);
                    smi.GoToRestrictionState();
                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["after"] = RocketUsageInfo(smi)
                    });
                }
            };
        }

        private static IEnumerable<GameObject> FindMutantSeedTargets(JObject args)
        {
            bool hasLookup = HasLookupInput(args);
            if (hasLookup)
            {
                var target = FindMutantSeedTarget(args);
                if (target != null)
                    yield return target;
                yield break;
            }
            foreach (var go in AllCandidateObjects(args))
            {
                if (HasMutantSeedControl(go))
                    yield return go;
            }
        }

        private static GameObject FindMutantSeedTarget(JObject args)
        {
            return AllCandidateObjects(args).FirstOrDefault(HasMutantSeedControl);
        }

        private static bool HasMutantSeedControl(GameObject go)
        {
            return go != null
                   && (go.GetComponent<ComplexFabricator>() != null
                       || go.GetSMI<FishFeeder.Instance>() != null
                       || go.GetSMI<SpiceGrinder.StatesInstance>() != null);
        }

        private static Dictionary<string, object> MutantSeedInfo(GameObject go)
        {
            var result = TargetInfo(go);
            var fabricator = go.GetComponent<ComplexFabricator>();
            var fish = go.GetSMI<FishFeeder.Instance>();
            var spice = go.GetSMI<SpiceGrinder.StatesInstance>();
            if (fabricator != null)
            {
                result["componentType"] = "ComplexFabricator";
                result["acceptMutantSeeds"] = !fabricator.ForbidMutantSeeds;
                result["forbidMutantSeeds"] = fabricator.ForbidMutantSeeds;
            }
            else if (fish != null)
            {
                result["componentType"] = "FishFeeder";
                result["acceptMutantSeeds"] = !fish.ForbidMutantSeeds;
                result["forbidMutantSeeds"] = fish.ForbidMutantSeeds;
            }
            else if (spice != null)
            {
                result["componentType"] = "SpiceGrinder";
                result["acceptMutantSeeds"] = spice.AllowMutantSeeds;
                result["forbidMutantSeeds"] = !spice.AllowMutantSeeds;
            }
            result["dlcRadiationEnabled"] = DlcManager.FeatureRadiationEnabled();
            return result;
        }

        private static string SetMutantSeedAccept(GameObject go, bool accept)
        {
            var fabricator = go.GetComponent<ComplexFabricator>();
            if (fabricator != null)
            {
                fabricator.ForbidMutantSeeds = !accept;
                return null;
            }
            var fish = go.GetSMI<FishFeeder.Instance>();
            if (fish != null)
            {
                fish.ForbidMutantSeeds = !accept;
                return null;
            }
            var spice = go.GetSMI<SpiceGrinder.StatesInstance>();
            if (spice != null)
            {
                spice.AllowMutantSeeds = accept;
                return null;
            }
            return "Target does not support mutant seed controls";
        }

        private static IEnumerable<GameObject> FindRocketUsageTargets(JObject args)
        {
            bool hasLookup = HasLookupInput(args);
            if (hasLookup)
            {
                var target = FindRocketUsageTarget(args);
                if (target != null)
                    yield return target;
                yield break;
            }
            foreach (var go in AllCandidateObjects(args))
            {
                if (go.GetSMI<RocketUsageRestriction.StatesInstance>() != null)
                    yield return go;
            }
        }

        private static GameObject FindRocketUsageTarget(JObject args)
        {
            return AllCandidateObjects(args).FirstOrDefault(go => go.GetSMI<RocketUsageRestriction.StatesInstance>() != null);
        }

        private static Dictionary<string, object> RocketUsageInfo(RocketUsageRestriction.StatesInstance smi)
        {
            var result = TargetInfo(smi.gameObject);
            result["componentType"] = "RocketUsageRestriction";
            result["controlledByRocketStation"] = smi.isControlled;
            result["restrictionApplied"] = smi.isRestrictionApplied;
            result["buildingRestrictionsActive"] = smi.BuildingRestrictionsActive();
            result["operationalAllowed"] = smi.operational == null ? null : (object)smi.operational.GetFlag(RocketUsageRestriction.rocketUsageAllowed);
            result["worldIsRocketInterior"] = smi.master.gameObject.GetMyWorld()?.IsModuleInterior ?? false;
            return result;
        }

        private static IEnumerable<GameObject> AllCandidateObjects(JObject args)
        {
            bool hasRect = HasRectInput(args);
            var rect = hasRect ? ToolUtil.GetRect(args) : null;
            int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            string query = args["query"]?.ToString();

            var seen = new HashSet<int>();
            foreach (var kpid in UnityEngine.Object.FindObjectsByType<KPrefabID>(FindObjectsSortMode.None))
            {
                var go = kpid?.gameObject;
                if (go == null || !seen.Add(go.GetInstanceID()))
                    continue;
                if (!ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                if (id.HasValue && kpid.InstanceID != id.Value)
                    continue;
                if (cell.HasValue && Grid.PosToCell(go) != cell.Value)
                    continue;
                if (rect != null && !CellInRect(Grid.PosToCell(go), rect, worldId))
                    continue;
                if (!string.IsNullOrWhiteSpace(query) && !MatchesQuery(TargetInfo(go), query))
                    continue;
                yield return go;
            }
        }

        private static bool CellInRect(int cell, Dictionary<string, int> rect, int worldId)
        {
            return Grid.IsValidCell(cell)
                   && ToolUtil.CellMatchesWorld(cell, worldId)
                   && Grid.CellColumn(cell) >= rect["x1"]
                   && Grid.CellColumn(cell) <= rect["x2"]
                   && Grid.CellRow(cell) >= rect["y1"]
                   && Grid.CellRow(cell) <= rect["y2"];
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
                ["worldId"] = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : go.GetComponent<KMonoBehaviour>()?.GetMyWorldId() ?? -1
            };
        }

        private static bool MatchesQuery(Dictionary<string, object> info, string query)
        {
            return string.IsNullOrWhiteSpace(query)
                   || JsonConvert.SerializeObject(info).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
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

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> parameters)
        {
            parameters["areaId"] = new McpToolParameter { Type = "string", Description = "領域ハンドル。x1/y1/x2/y2 とのどちらかを指定する", Required = false };
            parameters["x1"] = new McpToolParameter { Type = "integer", Description = "矩形の左下・左上の X", Required = false };
            parameters["y1"] = new McpToolParameter { Type = "integer", Description = "矩形の左下・左上の Y", Required = false };
            parameters["x2"] = new McpToolParameter { Type = "integer", Description = "矩形の右上・右下の X", Required = false };
            parameters["y2"] = new McpToolParameter { Type = "integer", Description = "矩形の右上・右下の Y", Required = false };
            parameters["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。省略するとワールドを限定しない", Required = false };
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> parameters)
        {
            parameters["id"] = new McpToolParameter { Type = "integer", Description = "対象の KPrefabID.InstanceID。指定を推奨する", Required = false };
            parameters["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X。id が未指定の場合に使う", Required = false };
            parameters["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y。id が未指定の場合に使う", Required = false };
            parameters["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標から探す場合は指定を推奨する", Required = false };
            parameters["query"] = new McpToolParameter { Type = "string", Description = "対象のあいまい検索。書き込み時に id/座標の代わりとして使うことは推奨しない", Required = false };
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> MutantSeedControlParams()
        {
            return LookupParams(RectParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set", Required = true },
                ["accept"] = new McpToolParameter { Type = "boolean", Description = "action=set では必須。true=変異種子を受け入れる、false=変異種子を拒否する", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set では、ユーザーメニューの設定変更を確認するため true が必須", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false }
            }));
        }

        private static Dictionary<string, McpToolParameter> RocketUsageControlParams()
        {
            return LookupParams(RectParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set", Required = true },
                ["controlled"] = new McpToolParameter { Type = "boolean", Description = "action=set では必須。true=制御装置の制限を受ける、false=制限を受けない", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set では、ロケット内部設備の制限変更を確認するため true が必須", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false }
            }));
        }

        private static CallToolResult JsonResult(object payload)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
        }
    }
}
