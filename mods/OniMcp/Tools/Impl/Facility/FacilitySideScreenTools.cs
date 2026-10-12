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
    public static partial class FacilitySideScreenTools
    {
        public static McpTool ControlFacilitySideScreen()
        {
            return new McpTool
            {
                Name = "facility_sidescreen_control",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "facility_control", "story_facility_sidescreen_control" },
                Tags = new List<string> { "building", "story", "side-screen", "facility" },
                Description = "設備のサイドパネルの統合ツール。kind=dispenser/suit_locker/lore_bearer/telepad/artifact。action=list または対応する操作を指定する",
                Parameters = FacilitySideScreenControlParams(),
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    string action = (args["action"]?.ToString() ?? "list").Trim().ToLowerInvariant();
                    bool list = string.IsNullOrWhiteSpace(action) || action == "list" || action == "status";

                    switch (kind)
                    {
                        case "dispenser":
                        case "dispensers":
                            return list ? ListDispensers().Handler(args) : ControlDispenser().Handler(args);
                        case "suit_locker":
                        case "suit_lockers":
                        case "locker":
                            return list ? ListSuitLockers().Handler(args) : ControlSuitLocker().Handler(args);
                        case "lore":
                        case "lore_bearer":
                        case "lore_bearers":
                            return list ? ListLoreBearers().Handler(args) : PressLoreBearer().Handler(args);
                        case "telepad":
                        case "telepads":
                        case "printing_pod":
                            return list ? ListTelepads().Handler(args) : ControlTelepad().Handler(args);
                        case "artifact":
                        case "artifacts":
                            return list ? ListArtifacts().Handler(args) : OpenArtifactReveal().Handler(args);
                        default:
                            return CallToolResult.Error("kind must be dispenser, suit_locker, lore_bearer, telepad, or artifact");
                    }
                }
            };
        }




        private static CallToolResult ListBuildingTargets(JObject args, Func<GameObject, bool> predicate, Func<GameObject, Dictionary<string, object>> selector, string payloadKey)
        {
            var items = BuildingTargets(args, predicate, selector).ToList();
            int worldId = HasRectInput(args) || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
            return JsonResult(new Dictionary<string, object>
            {
                ["returned"] = items.Count,
                ["worldId"] = worldId >= 0 ? (object)worldId : null,
                [payloadKey] = items
            });
        }

        private static IEnumerable<Dictionary<string, object>> BuildingTargets(JObject args, Func<GameObject, bool> predicate, Func<GameObject, Dictionary<string, object>> selector)
        {
            if (Game.Instance == null)
                return Enumerable.Empty<Dictionary<string, object>>();
            bool hasRect = HasRectInput(args);
            var rect = hasRect ? ToolUtil.GetRect(args) : null;
            int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
            string query = args["query"]?.ToString();
            int limit = ToolUtil.ClampLimit(args, 100, 500);
            return Components.BuildingCompletes.Items
                .Select(building => building?.gameObject)
                .Where(go => MatchesTarget(go, rect, worldId))
                .Where(predicate)
                .Select(selector)
                .Where(info => MatchesQuery(info, query))
                .OrderBy(info => info["name"].ToString())
                .Take(limit);
        }

        private static CallToolResult ListObjectTargets(JObject args, Func<GameObject, bool> predicate, Func<GameObject, Dictionary<string, object>> selector, string payloadKey)
        {
            if (Game.Instance == null)
                return CallToolResult.Error("Game not initialized");
            bool hasRect = HasRectInput(args);
            var rect = hasRect ? ToolUtil.GetRect(args) : null;
            int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
            string query = args["query"]?.ToString();
            int limit = ToolUtil.ClampLimit(args, 100, 500);
            var items = AllCandidateObjects()
                .Where(go => MatchesTarget(go, rect, worldId))
                .Where(predicate)
                .Select(selector)
                .Where(info => MatchesQuery(info, query))
                .OrderBy(info => info["name"].ToString())
                .Take(limit)
                .ToList();
            return JsonResult(new Dictionary<string, object>
            {
                ["returned"] = items.Count,
                ["worldId"] = worldId >= 0 ? (object)worldId : null,
                [payloadKey] = items
            });
        }

        private static IEnumerable<GameObject> AllCandidateObjects()
        {
            var seen = new HashSet<int>();
            foreach (var kpid in UnityEngine.Object.FindObjectsByType<KPrefabID>(FindObjectsSortMode.None))
            {
                if (kpid == null || kpid.gameObject == null)
                    continue;
                int id = kpid.gameObject.GetInstanceID();
                if (seen.Add(id))
                    yield return kpid.gameObject;
            }
        }

        private static GameObject FindBuildingTarget(JObject args, Func<GameObject, bool> predicate)
        {
            return FindTarget(args, Components.BuildingCompletes.Items.Select(building => building?.gameObject), predicate);
        }

        private static GameObject FindObjectTarget(JObject args, Func<GameObject, bool> predicate)
        {
            return FindTarget(args, AllCandidateObjects(), predicate);
        }

        private static GameObject FindTarget(JObject args, IEnumerable<GameObject> candidates, Func<GameObject, bool> predicate)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            foreach (var go in candidates)
            {
                if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId) || !predicate(go))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return go;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return go;
            }
            return null;
        }

        private static bool MatchesTarget(GameObject go, Dictionary<string, int> rect, int worldId)
        {
            if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                return false;
            int cell = Grid.PosToCell(go);
            return rect == null || CellInRect(cell, rect, worldId);
        }

        private static bool MatchesQuery(Dictionary<string, object> info, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            return JsonConvert.SerializeObject(info).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
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
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトのセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトのセルの Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標で検索する場合の既定値は現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> FacilitySideScreenControlParams()
        {
            return LookupParams(RectParams(new Dictionary<string, McpToolParameter>
            {
                ["kind"] = new McpToolParameter { Type = "string", Description = "dispenser、suit_locker、lore_bearer、telepad、artifact のいずれか", Required = true },
                ["action"] = new McpToolParameter { Type = "string", Description = "list/status または対応する操作: select_item/order/cancel、request_suit/no_suit/drop_suit、press、list_rewards/claim/open_immigrants/open_colony_summary/open_skills/open_research、open", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で名前、prefabId、状態、アイテムにより絞り込む。artifact では artifact id でも絞り込める", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限", Required = false },
                ["itemId"] = new McpToolParameter { Type = "string", Description = "kind=dispenser action=select_item または kind=telepad action=claim の対象の Tag/prefab id", Required = false },
                ["rewardIndex"] = new McpToolParameter { Type = "integer", Description = "kind=telepad action=claim で受け取る rewards[index]。既定値は 0", Required = false },
                ["itemIndex"] = new McpToolParameter { Type = "integer", Description = "kind=dispenser action=select_item の供給可能なアイテムの番号", Required = false },
                ["artifactId"] = new McpToolParameter { Type = "string", Description = "kind=artifact action=open の分析済み artifact の prefab id", Required = false },
                ["interactableOnly"] = new McpToolParameter { Type = "boolean", Description = "kind=lore_bearer action=list で操作可能な対象だけを返す", Required = false },
                ["includeVictory"] = new McpToolParameter { Type = "boolean", Description = "kind=telepad action=list で勝利条件の checklist を含めるか。既定値は true", Required = false },
                ["includeStations"] = new McpToolParameter { Type = "boolean", Description = "kind=artifact action=list で分析端末の状態を含めるか。既定値は true", Required = false },
                ["includeWorldArtifacts"] = new McpToolParameter { Type = "boolean", Description = "kind=artifact action=list でワールド内の artifact を含めるか。既定値は true", Required = false },
                ["force"] = new McpToolParameter { Type = "boolean", Description = "kind=lore_bearer action=press で interactable のチェックを省略する", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "書き込み・UI を開く・ポップアップの表示を実行する場合は、旧ツールの要件に従って true を渡す", Required = false }
            }));
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

        private static bool HasRectInput(JObject args)
        {
            return !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                   || ToolUtil.GetInt(args, "x1").HasValue
                   || ToolUtil.GetInt(args, "y1").HasValue
                   || ToolUtil.GetInt(args, "x2").HasValue
                   || ToolUtil.GetInt(args, "y2").HasValue;
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

        private static CallToolResult JsonResult(Dictionary<string, object> payload)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
        }
    }
}
