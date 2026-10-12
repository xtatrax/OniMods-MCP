using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class SpecialBuildingTools
    {
        public static McpTool ControlSpecialBuilding()
        {
            return new McpTool
            {
                Name = "special_building_control",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "special_side_building_control" },
                Tags = new List<string> { "buildings", "special", "side-screen", "art", "ranching", "rocket", "monument" },
                Description = "特殊設備のサイドパネルの集約ツール。kind=artable/creature_lure/gene_shuffler/missile_launcher/monument_part。action は対応する旧 control の操作を使う。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "特殊設備の種類: artable、creature_lure、gene_shuffler、missile_launcher、monument_part", Required = true, EnumValues = new List<string> { "artable", "creature_lure", "gene_shuffler", "missile_launcher", "monument_part" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、set_stage、set_bait、complete、request_recharge、cancel_recharge、toggle_recharge、set_ammunition、set", Required = true },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象の設備の InstanceID", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象の設備または領域の X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象の設備または領域の Y", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の左下の X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の左下の Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の右上の X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の右上の Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list で名前、prefabId、選択肢により絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "action=list で選択可能な項目を返すか", Required = false },
                    ["stageId"] = new McpToolParameter { Type = "string", Description = "kind=artable action=set_stage の段階の id", Required = false },
                    ["baitTag"] = new McpToolParameter { Type = "string", Description = "kind=creature_lure action=set_bait の餌の tag", Required = false },
                    ["missileId"] = new McpToolParameter { Type = "string", Description = "kind=missile_launcher action=set_ammunition の弾薬の id", Required = false },
                    ["allowed"] = new McpToolParameter { Type = "boolean", Description = "kind=missile_launcher action=set_ammunition で、弾薬を許可するか", Required = false },
                    ["partId"] = new McpToolParameter { Type = "string", Description = "kind=monument_part action=set の外観の id", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "現在の選択を解除する。artable/creature_lure に使用する", Required = false },
                    ["rotate"] = new McpToolParameter { Type = "boolean", Description = "kind=monument_part action=set で反転を実行する", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "一部の事前チェックを省略する", Required = false }
                },
                Handler = args =>
                {
                    string kind = (args["kind"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (kind)
                    {
                        case "artable":
                            return ControlArtable().Handler(args);
                        case "creature_lure":
                        case "lure":
                            return ControlCreatureLure().Handler(args);
                        case "gene_shuffler":
                        case "neural_vacillator":
                            return ControlGeneShuffler().Handler(args);
                        case "missile_launcher":
                            return ControlMissileLauncher().Handler(args);
                        case "monument_part":
                            return ControlMonumentPart().Handler(args);
                        default:
                            return CallToolResult.Error("kind must be artable, creature_lure, gene_shuffler, missile_launcher, or monument_part");
                    }
                }
            };
        }

        public static McpTool ControlArtable()
        {
            return new McpTool
            {
                Name = "artable_control",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "artwork_selection_control", "artable_facade_control" },
                Tags = new List<string> { "buildings", "art", "decor", "facade", "side-screen" },
                Description = "芸術作品の外観の集約ツール。action=list で選択可能な段階を照会し、action=set_stage で段階を設定する。clear=true では作り直す",
                Parameters = ArtableControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListArtables().Handler(args);
                    if (action == "set_stage" || action == "set")
                        return SetArtableStage().Handler(args);
                    return CallToolResult.Error("action must be list or set_stage");
                }
            };
        }

        public static McpTool ListArtables()
        {
            return new McpTool
            {
                Name = "artables_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "artwork_selection_list", "artable_facades_list" },
                Tags = new List<string> { "buildings", "art", "decor", "facade", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=artable action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、段階の id、段階の名前で絞り込む", Required = false },
                    ["includeOptions"] = new McpToolParameter { Type = "boolean", Description = "選択可能な段階を返すか。既定値は true", Required = false },
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

                    var results = Components.BuildingCompletes.Items
                        .Select(building => building?.gameObject)
                        .Where(go => MatchesTarget(go, rect, worldId))
                        .Where(go => go.GetComponent<Artable>() != null)
                        .Select(go => ArtableInfo(go.GetComponent<Artable>(), includeOptions))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = results.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["artables"] = results
                    });
                }
            };
        }

        public static McpTool SetArtableStage()
        {
            return new McpTool
            {
                Name = "artable_stage_set",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "artwork_selection_set", "artable_facade_set" },
                Tags = new List<string> { "buildings", "art", "decor", "facade", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=artable action=set_stage を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["stageId"] = new McpToolParameter { Type = "string", Description = "対象の ArtableStage の id。clear=true の場合は無視する", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "true は Default に戻し、再び制作を待つ状態にする", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "解放状態と現在の品質による絞り込みのチェックを省略する。既定値は false", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindBuildingTarget(args, component => component.GetComponent<Artable>() != null);
                    if (go == null)
                        return CallToolResult.Error("Target Artable not found");
                    var artable = go.GetComponent<Artable>();
                    bool clear = ToolUtil.GetBool(args, "clear", false);
                    bool force = ToolUtil.GetBool(args, "force", false);
                    var before = ArtableInfo(artable, true);

                    if (clear)
                    {
                        artable.SetDefault();
                    }
                    else
                    {
                        string stageId = args["stageId"]?.ToString();
                        if (string.IsNullOrWhiteSpace(stageId))
                            return CallToolResult.Error("stageId or clear=true is required");
                        var stage = Db.GetArtableStages().TryGet(stageId);
                        if (stage == null)
                            return CallToolResult.Error("Unknown ArtableStage id");
                        if (!force && !GetSelectableArtableStages(artable).Any(item => item.id == stage.id))
                            return CallToolResult.Error("Stage is not currently selectable for this artable; use force=true to override");
                        artable.SetUserChosenTargetState(stage.id);
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["artable"] = ArtableInfo(artable, true)
                    });
                }
            };
        }

    }
}
