using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class OrdersTools
    {
                public static McpTool Attack()
                {
                    return new McpTool
                    {
                        Name = "orders_attack",
                        Hidden = true,
                        Group = "orders",
                        Mode = "execute",
                        Risk = "dangerous",
                        Description = "互換用の入口。orders_control domain=designation action=attack を優先して使う。動物・敵対対象への攻撃専用で、採掘には使えない。領域攻撃では confirm=true に加えて、action=mark と attackAreaConfirm=\"attack area\" が必須",
                        Parameters = RectParams(new Dictionary<string, McpToolParameter>
                        {
                            ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID。指定すると座標・領域より優先する", Required = false },
                            ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X。矩形が未指定の場合は単一セルで検索する", Required = false },
                            ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y。矩形が未指定の場合は単一セルで検索する", Required = false },
                            ["action"] = new McpToolParameter { Type = "string", Description = "mark は攻撃を指定し、cancel は攻撃を取り消す。既定値は mark", Required = false, EnumValues = new List<string> { "mark", "cancel" } },
                            ["priority"] = new McpToolParameter { Type = "integer", Description = "攻撃作業の優先度 1-9。既定値は 5", Required = false },
                            ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "赤色の最優先にするか。既定値は false", Required = false },
                            ["force"] = new McpToolParameter { Type = "boolean", Description = "友好的・協力的な陣営の対象への攻撃指定を許可するか。既定値は false", Required = false },
                            ["attackAreaConfirm"] = new McpToolParameter { Type = "string", Description = "領域攻撃の追加確認。矩形領域への mark 攻撃では、採掘のつもりで攻撃を呼び出す事故を防ぐため、attack area と正確に入力する必要がある", Required = false },
                            ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。攻撃を指定する場合は true が必須", Required = false }
                        }),
                        Handler = args =>
                        {
                            string action = (args["action"]?.ToString() ?? "mark").Trim().ToLowerInvariant();
                            bool mark = action != "cancel";
                            if (mark && !ToolUtil.GetBool(args, "confirm", false))
                                return CallToolResult.Error("confirm=true is required for attack orders");
                            bool areaAttack = mark && args["id"] == null && HasRectInput(args);
                            if (areaAttack && args["attackAreaConfirm"]?.ToString() != "attack area")
                                return CallToolResult.Error("Refusing area attack without attackAreaConfirm=\"attack area\". For terrain excavation use orders_control domain=area action=dig, not orders_attack.");
                            if (FactionManager.Instance == null)
                                return CallToolResult.Error("FactionManager is not initialized");

                            var targets = FindAttackTargets(args);
                            if (targets.Count == 0)
                                return CallToolResult.Error("No attack target found");

                            bool force = ToolUtil.GetBool(args, "force", false);
                            int changed = 0;
                            int skipped = 0;
                            var results = new List<Dictionary<string, object>>();
                            foreach (var target in targets)
                            {
                                var go = target?.gameObject;
                                if (go == null)
                                    continue;

                                if (mark && !force && FactionManager.Instance.GetDisposition(FactionManager.FactionID.Duplicant, target.Alignment) == FactionManager.Disposition.Assist)
                                {
                                    skipped++;
                                    results.Add(TargetResult(go, target, "skipped_assist_faction"));
                                    continue;
                                }

                                target.SetPlayerTargeted(mark);
                                if (mark)
                                    ApplyPriority(go, args);

                                changed++;
                                results.Add(TargetResult(go, target, mark ? "marked" : "cancelled"));
                            }

                            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                            {
                                ["action"] = mark ? "mark" : "cancel",
                                ["changed"] = changed,
                                ["skipped"] = skipped,
                                ["targets"] = results
                            }, McpJsonUtil.Settings));
                        }
                    };
                }
    }
}
