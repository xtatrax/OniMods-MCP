using System;
using System.Collections.Generic;
using System.Linq;
using Database;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class SpecialBuildingTools
    {
        private static CallToolResult ListBuildingComponent(JObject args, Func<GameObject, bool> predicate, Func<GameObject, Dictionary<string, object>> selector, string payloadKey)
        {
            if (Game.Instance == null)
                return CallToolResult.Error("Game not initialized");
            bool hasRect = HasRectInput(args);
            var rect = hasRect ? ToolUtil.GetRect(args) : null;
            int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
            string query = args["query"]?.ToString();
            int limit = ToolUtil.ClampLimit(args, 100, 500);
            var items = Components.BuildingCompletes.Items
                .Select(building => building?.gameObject)
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

        private static Dictionary<string, object> ArtableInfo(Artable artable, bool includeOptions)
        {
            var result = TargetInfo(artable.gameObject);
            result["currentStage"] = artable.CurrentStage;
            result["sideScreenValid"] = artable.CurrentStage != "Default";
            result["options"] = includeOptions ? GetSelectableArtableStages(artable).Select(ArtableStageInfo).ToList() : new List<Dictionary<string, object>>();
            return result;
        }

        private static IEnumerable<ArtableStage> GetSelectableArtableStages(Artable artable)
        {
            var prefabStages = Db.GetArtableStages().GetPrefabStages(artable.GetComponent<KPrefabID>().PrefabID());
            var current = prefabStages.Find(stage => stage.id == artable.CurrentStage);
            return prefabStages
                .Where(stage => stage.id != "Default")
                .Where(stage => current == null || stage.statusItem.StatusType == current.statusItem.StatusType)
                .Where(stage => stage.IsUnlocked());
        }

        private static Dictionary<string, object> ArtableStageInfo(ArtableStage stage)
        {
            return new Dictionary<string, object>
            {
                ["id"] = stage.id,
                ["name"] = stage.Name,
                ["status"] = stage.statusItem.Id,
                ["quality"] = stage.statusItem.StatusType.ToString(),
                ["decor"] = stage.decor,
                ["unlocked"] = stage.IsUnlocked()
            };
        }

        private static Dictionary<string, object> CreatureLureInfo(CreatureLure lure)
        {
            var result = TargetInfo(lure.gameObject);
            result["activeBait"] = lure.activeBaitSetting == Tag.Invalid ? null : lure.activeBaitSetting.Name;
            result["baitTypes"] = lure.baitTypes.Select(BaitInfo).ToList();
            result["storageKg"] = lure.baitStorage == null ? 0 : Math.Round(ToolUtil.SafeFloat(lure.baitStorage.MassStored()), 3);
            return result;
        }

        private static Dictionary<string, object> BaitInfo(Tag tag)
        {
            var element = ElementLoader.GetElement(tag);
            return new Dictionary<string, object>
            {
                ["tag"] = tag.Name,
                ["name"] = element?.name ?? tag.ProperName(),
                ["element"] = element?.id.ToString()
            };
        }

        private static Dictionary<string, object> GeneShufflerInfo(GeneShuffler shuffler)
        {
            var result = TargetInfo(shuffler.gameObject);
            result["isConsumed"] = shuffler.IsConsumed;
            result["rechargeRequested"] = shuffler.RechargeRequested;
            result["workComplete"] = shuffler.WorkComplete;
            result["isWorking"] = shuffler.IsWorking;
            result["assigned"] = shuffler.assignable?.assignee == null ? null : ToolUtil.CleanName(shuffler.assignable.assignee.GetProperName());
            result["storageKg"] = shuffler.storage == null ? 0 : Math.Round(ToolUtil.SafeFloat(shuffler.storage.MassStored()), 3);
            return result;
        }

        private static Dictionary<string, object> MissileInfo(MissileLauncher.Instance launcher)
        {
            var result = TargetInfo(launcher.gameObject);
            result["ammunition"] = GetMissileAmmunitionTags(launcher).Select(tag => new Dictionary<string, object>
            {
                ["tag"] = tag.Name,
                ["name"] = tag.ProperNameStripLink(),
                ["allowed"] = launcher.AmmunitionIsAllowed(tag)
            }).ToList();
            result["anyCosmicBlastShotAllowed"] = launcher.IsAnyCosmicBlastShotAllowed();
            return result;
        }

        private static List<Tag> GetMissileAmmunitionTags(MissileLauncher.Instance launcher)
        {
            var tags = launcher.GetValidAmmunitionTags();
            if (DlcManager.IsExpansion1Active())
            {
                foreach (var tag in MissileLauncherConfig.CosmicBlastShotTypes)
                {
                    if (!tags.Contains(tag))
                        tags.Add(tag);
                }
            }
            return tags;
        }

        private static Dictionary<string, object> MonumentInfo(MonumentPart part, bool includeOptions)
        {
            var result = TargetInfo(part.gameObject);
            result["part"] = part.part.ToString();
            result["chosenState"] = GetMonumentChosenState(part);
            result["options"] = includeOptions ? Db.GetMonumentParts().GetParts(part.part).Select(MonumentOptionInfo).ToList() : new List<Dictionary<string, object>>();
            return result;
        }

        private static Dictionary<string, object> MonumentOptionInfo(MonumentPartResource resource)
        {
            return new Dictionary<string, object>
            {
                ["id"] = resource.Id,
                ["name"] = resource.Name,
                ["part"] = resource.part.ToString(),
                ["state"] = resource.State,
                ["unlocked"] = resource.IsUnlocked()
            };
        }

        private static string GetMonumentChosenState(MonumentPart part)
        {
            var field = OniReflection.GetFieldSafe(typeof(MonumentPart), "chosenState", false);
            return field?.GetValue(part)?.ToString();
        }

        private static GameObject FindBuildingTarget(JObject args, Func<GameObject, bool> predicate)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            foreach (var building in Components.BuildingCompletes.Items)
            {
                var go = building?.gameObject;
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

        private static GeneShuffler FindGeneShuffler(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            foreach (var shuffler in UnityEngine.Object.FindObjectsByType<GeneShuffler>(FindObjectsSortMode.None))
            {
                if (shuffler == null || !ToolUtil.GameObjectMatchesWorld(shuffler.gameObject, worldId))
                    continue;
                var kpid = shuffler.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return shuffler;
                if (cell.HasValue && Grid.PosToCell(shuffler) == cell.Value)
                    return shuffler;
            }
            return null;
        }

        private static MissileLauncher.Instance FindMissileLauncher(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            foreach (var launcher in Components.MissileLaunchers.Items)
            {
                if (launcher == null || !ToolUtil.GameObjectMatchesWorld(launcher.gameObject, worldId))
                    continue;
                var kpid = launcher.gameObject.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return launcher;
                if (cell.HasValue && Grid.PosToCell(launcher.gameObject) == cell.Value)
                    return launcher;
            }
            return null;
        }

        private static MonumentPart FindMonumentPart(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            foreach (var part in Components.MonumentParts.Items)
            {
                if (part == null || !ToolUtil.GameObjectMatchesWorld(part.gameObject, worldId))
                    continue;
                var kpid = part.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return part;
                if (cell.HasValue && Grid.PosToCell(part) == cell.Value)
                    return part;
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

        private static CallToolResult JsonResult(Dictionary<string, object> payload)
        {
            return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
        }

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

        private static Dictionary<string, McpToolParameter> AreaLookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = RectParams(new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y", Required = false }
            });
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> ArtableControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set_stage", Required = true, EnumValues = new List<string> { "list", "set_stage" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、段階の id、段階の名前により絞り込む", Required = false },
                ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "action=list で選択可能な段階を返すか。既定値は true", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["stageId"] = new McpToolParameter { Type = "string", Description = "action=set_stage の対象の ArtableStage の id。clear=true の場合は無視する", Required = false },
                ["clear"] = new McpToolParameter { Type = "boolean", Description = "action=set_stage で true を指定すると Default に戻し、再び制作を待つ状態にする", Required = false },
                ["force"] = new McpToolParameter { Type = "boolean", Description = "action=set_stage で解放状態と現在の品質による絞り込みのチェックを省略するか。既定値は false", Required = false }
            });
        }

        private static Dictionary<string, McpToolParameter> CreatureLureControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set_bait", Required = true, EnumValues = new List<string> { "list", "set_bait" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、餌の tag により絞り込む", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["baitTag"] = new McpToolParameter { Type = "string", Description = "action=set_bait の餌の tag。例: SlimeMold、Phosphorite。clear=true の場合は無視する", Required = false },
                ["clear"] = new McpToolParameter { Type = "boolean", Description = "action=set_bait で true を指定すると餌の選択を解除する", Required = false }
            });
        }

        private static Dictionary<string, McpToolParameter> MissileLauncherControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set_ammunition", Required = true, EnumValues = new List<string> { "list", "set_ammunition" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、弾薬の tag により絞り込む", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["ammoTag"] = new McpToolParameter { Type = "string", Description = "action=set_ammunition の弾薬の tag。例: MissileBasic、MissileLongRange、DLC の cosmic blast の種類", Required = false },
                ["allowed"] = new McpToolParameter { Type = "boolean", Description = "action=set_ammunition でこの弾薬を許可するか", Required = false }
            });
        }

        private static Dictionary<string, McpToolParameter> GeneShufflerControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list、complete、request_recharge、cancel_recharge、toggle_recharge のいずれか", Required = false, EnumValues = new List<string> { "list", "complete", "request_recharge", "cancel_recharge", "toggle_recharge" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で名前、prefabId、割り当て対象により絞り込む", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false }
            });
        }

        private static Dictionary<string, McpToolParameter> MonumentPartControlParams()
        {
            return AreaLookupParams(new Dictionary<string, McpToolParameter>
            {
                ["action"] = new McpToolParameter { Type = "string", Description = "list または set", Required = true, EnumValues = new List<string> { "list", "set" } },
                ["query"] = new McpToolParameter { Type = "string", Description = "action=list で設備名、prefabId、part、外観の id により絞り込む", Required = false },
                ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "action=list で選択可能な外観を返すか。既定値は true", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                ["partId"] = new McpToolParameter { Type = "string", Description = "action=set の MonumentPartResource の id。rotate=true では省略できる", Required = false },
                ["rotate"] = new McpToolParameter { Type = "boolean", Description = "action=set で true を指定すると反転ボタンを実行する", Required = false }
            });
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
    }
}
