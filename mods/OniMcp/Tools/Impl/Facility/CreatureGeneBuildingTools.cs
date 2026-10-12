using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class SpecialBuildingTools
    {
        public static McpTool ControlCreatureLure()
        {
            return new McpTool
            {
                Name = "creature_lure_control",
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "lure_control", "creature_lure_bait_control" },
                Tags = new List<string> { "buildings", "ranching", "lure", "bait", "side-screen" },
                Description = "動物のおびき寄せ設備の集約ツール。action=list で選択可能な餌を照会し、action=set_bait で餌を選択または解除する",
                Parameters = CreatureLureControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListCreatureLures().Handler(args);
                    if (action == "set_bait" || action == "set")
                        return SetCreatureLureBait().Handler(args);
                    return CallToolResult.Error("action must be list or set_bait");
                }
            };
        }

        public static McpTool ListCreatureLures()
        {
            return new McpTool
            {
                Name = "creature_lures_list",
                Hidden = true,
                Group = "ranching",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "lures_list", "creature_lure_baits_list" },
                Tags = new List<string> { "buildings", "ranching", "lure", "bait", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=creature_lure action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、餌の tag で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args => ListBuildingComponent(args, go => go.GetComponent<CreatureLure>() != null, go => CreatureLureInfo(go.GetComponent<CreatureLure>()), "lures")
            };
        }

        public static McpTool SetCreatureLureBait()
        {
            return new McpTool
            {
                Name = "creature_lure_bait_set",
                Hidden = true,
                Group = "ranching",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "lure_bait_set" },
                Tags = new List<string> { "buildings", "ranching", "lure", "bait", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=creature_lure action=set_bait を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["baitTag"] = new McpToolParameter { Type = "string", Description = "餌の tag。例: SlimeMold、Phosphorite。clear=true の場合は無視する", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "true は餌の選択を解除する", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindBuildingTarget(args, component => component.GetComponent<CreatureLure>() != null);
                    if (go == null)
                        return CallToolResult.Error("Target CreatureLure not found");
                    var lure = go.GetComponent<CreatureLure>();
                    var before = CreatureLureInfo(lure);
                    bool clear = ToolUtil.GetBool(args, "clear", false);
                    Tag bait = Tag.Invalid;
                    if (!clear)
                    {
                        bait = new Tag(args["baitTag"]?.ToString() ?? "");
                        if (bait == Tag.Invalid || !lure.baitTypes.Contains(bait))
                            return CallToolResult.Error("baitTag must be one of the target lure baitTypes");
                    }
                    lure.ChangeBaitSetting(bait);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["lure"] = CreatureLureInfo(lure)
                    });
                }
            };
        }

        public static McpTool ListGeneShufflers()
        {
            return new McpTool
            {
                Name = "gene_shufflers_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "neural_vacillators_list" },
                Tags = new List<string> { "buildings", "gene-shuffler", "trait", "recharge", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=special kind=gene_shuffler action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "名前、prefabId、割り当て対象で絞り込む", Required = false },
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
                    var items = UnityEngine.Object.FindObjectsByType<GeneShuffler>(FindObjectsSortMode.None)
                        .Where(item => MatchesTarget(item?.gameObject, rect, worldId))
                        .Select(GeneShufflerInfo)
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = items.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["geneShufflers"] = items
                    });
                }
            };
        }

        public static McpTool ControlGeneShuffler()
        {
            return new McpTool
            {
                Name = "gene_shuffler_control",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "neural_vacillator_control" },
                Tags = new List<string> { "buildings", "gene-shuffler", "trait", "recharge", "side-screen" },
                Description = "Gene Shuffler の集約ツール。action=list で状態を照会し、complete/request_recharge/cancel_recharge/toggle_recharge でボタンを実行する",
                Parameters = GeneShufflerControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(action) || action == "list")
                        return ListGeneShufflers().Handler(args);

                    var shuffler = FindGeneShuffler(args);
                    if (shuffler == null)
                        return CallToolResult.Error("Target GeneShuffler not found");
                    var before = GeneShufflerInfo(shuffler);

                    if (action == "complete")
                    {
                        if (!shuffler.WorkComplete)
                            return CallToolResult.Error("GeneShuffler work is not complete");
                        shuffler.SetWorkTime(0f);
                    }
                    else if (action == "request_recharge" || action == "cancel_recharge" || action == "toggle_recharge")
                    {
                        if (!shuffler.IsConsumed)
                            return CallToolResult.Error("GeneShuffler is not consumed");
                        bool next = action == "toggle_recharge" ? !shuffler.RechargeRequested : action == "request_recharge";
                        shuffler.RequestRecharge(next);
                        shuffler.RefreshSideScreen();
                    }
                    else
                    {
                        return CallToolResult.Error("action must be complete, request_recharge, cancel_recharge, or toggle_recharge");
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(shuffler.gameObject),
                        ["before"] = before,
                        ["geneShuffler"] = GeneShufflerInfo(shuffler)
                    });
                }
            };
        }

    }
}
