using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class SpecialBuildingTools
    {
        public static McpTool ControlMissileLauncher()
        {
            return new McpTool
            {
                Name = "missile_launcher_control",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "missile_ammunition_control", "missile_launcher_ammunition_control" },
                Tags = new List<string> { "buildings", "rocket", "missile", "ammunition", "side-screen" },
                Description = "隕石ブラスターの集約ツール。action=list で弾薬の許可状態を照会し、action=set_ammunition で弾薬の許可状態を設定する",
                Parameters = MissileLauncherControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListMissileLaunchers().Handler(args);
                    if (action == "set_ammunition" || action == "set")
                        return SetMissileAmmunition().Handler(args);
                    return CallToolResult.Error("action must be list or set_ammunition");
                }
            };
        }

        public static McpTool ListMissileLaunchers()
        {
            return new McpTool
            {
                Name = "missile_launchers_list",
                Hidden = true,
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "missile_ammunition_list" },
                Tags = new List<string> { "buildings", "rocket", "missile", "ammunition", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=missile_launcher action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、弾薬の tag で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 100, 500);
                    var items = Components.MissileLaunchers.Items
                        .Where(item => MatchesTarget(item?.gameObject, rect, worldId))
                        .Select(MissileInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = items.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["missileLaunchers"] = items
                    });
                }
            };
        }

        public static McpTool SetMissileAmmunition()
        {
            return new McpTool
            {
                Name = "missile_ammunition_set",
                Hidden = true,
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "missile_launcher_ammunition_set" },
                Tags = new List<string> { "buildings", "rocket", "missile", "ammunition", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=missile_launcher action=set_ammunition を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["ammoTag"] = new McpToolParameter { Type = "string", Description = "弾薬の tag。例: MissileBasic、MissileLongRange、DLC の cosmic blast の種類", Required = true },
                    ["allowed"] = new McpToolParameter { Type = "boolean", Description = "この弾薬を許可するか", Required = true }
                }),
                Handler = args =>
                {
                    var launcher = FindMissileLauncher(args);
                    if (launcher == null)
                        return CallToolResult.Error("Target missile launcher not found");
                    var ammo = new Tag(args["ammoTag"]?.ToString() ?? "");
                    var valid = GetMissileAmmunitionTags(launcher);
                    if (ammo == Tag.Invalid || !valid.Contains(ammo))
                        return CallToolResult.Error("ammoTag is not valid for this missile launcher");
                    bool allowed = ToolUtil.GetBool(args, "allowed", launcher.AmmunitionIsAllowed(ammo));
                    var before = MissileInfo(launcher);
                    launcher.ChangeAmmunition(ammo, allowed);
                    launcher.OnRowToggleClick();
                    if (DetailsScreen.Instance != null && launcher.gameObject.GetComponent<KSelectable>()?.IsSelected == true)
                        DetailsScreen.Instance.Refresh(launcher.gameObject);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(launcher.gameObject),
                        ["before"] = before,
                        ["missileLauncher"] = MissileInfo(launcher)
                    });
                }
            };
        }

        public static McpTool ControlMonumentPart()
        {
            return new McpTool
            {
                Name = "monument_part_control",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "monument_facade_control" },
                Tags = new List<string> { "buildings", "monument", "decor", "facade", "side-screen" },
                Description = "記念碑の部品の集約ツール。action=list で選択可能な外観を照会し、action=set で外観を設定する。rotate=true では反転する",
                Parameters = MonumentPartControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListMonumentParts().Handler(args);
                    if (action == "set")
                        return SetMonumentPart().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool ListMonumentParts()
        {
            return new McpTool
            {
                Name = "monument_parts_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "monument_facades_list" },
                Tags = new List<string> { "buildings", "monument", "decor", "facade", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=monument_part action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、part、外観の id で絞り込む", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "選択可能な外観を返すか。既定値は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    bool includeOptions = ToolUtil.GetBool(args, "includeOptions", true);
                    int limit = ToolUtil.ClampLimit(args, 100, 500);
                    var items = Components.MonumentParts.Items
                        .Where(item => MatchesTarget(item?.gameObject, rect, worldId))
                        .Select(item => MonumentInfo(item, includeOptions))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = items.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["monumentParts"] = items
                    });
                }
            };
        }

        public static McpTool SetMonumentPart()
        {
            return new McpTool
            {
                Name = "monument_part_set",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "monument_facade_set" },
                Tags = new List<string> { "buildings", "monument", "decor", "facade", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=monument_part action=set を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["partId"] = new McpToolParameter { Type = "string", Description = "MonumentPartResource の id。rotate=true では省略できる", Required = false },
                    ["rotate"] = new McpToolParameter { Type = "boolean", Description = "true は反転ボタンを実行する", Required = false }
                }),
                Handler = args =>
                {
                    var part = FindMonumentPart(args);
                    if (part == null)
                        return CallToolResult.Error("Target monument part not found");
                    bool rotate = ToolUtil.GetBool(args, "rotate", false);
                    string partId = args["partId"]?.ToString();
                    if (!rotate && string.IsNullOrWhiteSpace(partId))
                        return CallToolResult.Error("partId or rotate=true is required");

                    var before = MonumentInfo(part, true);
                    if (!string.IsNullOrWhiteSpace(partId))
                    {
                        var option = Db.GetMonumentParts().TryGet(partId);
                        if (option == null || option.part != part.part)
                            return CallToolResult.Error("partId is not valid for this monument part type");
                        part.SetState(option.Id);
                    }
                    if (rotate)
                    {
                        var rotatable = part.GetComponent<Rotatable>();
                        if (rotatable == null)
                            return CallToolResult.Error("Target monument part is not rotatable");
                        rotatable.Rotate();
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(part.gameObject),
                        ["before"] = before,
                        ["monumentPart"] = MonumentInfo(part, true)
                    });
                }
            };
        }

    }
}
