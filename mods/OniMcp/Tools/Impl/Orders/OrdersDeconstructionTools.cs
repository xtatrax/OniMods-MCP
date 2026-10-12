using System.Collections.Generic;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class OrdersTools
    {
        public static McpTool DeconstructBuilding()
        {
            return new McpTool
            {
                Name = "buildings_deconstruct",
                Hidden = true,
                Group = "buildings",
                Mode = "execute",
                Risk = "dangerous",
                Description = "互換用の入口。orders_control domain=designation action=deconstruct を優先して使う。指定した設備を解体対象にする。confirm=true が必要。dryRun=true ではプレビューだけを行う。",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。実行時は true が必須", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "対象をプレビューするだけで、解体指示をキューに入れない", Required = false }
                }),
                Handler = args =>
                {
                    GameObject go;
                    var resolveResult = ResolvePreciseDeconstructTarget(args, out go);
                    if (resolveResult != null)
                        return resolveResult;

                    if (ToolUtil.GetBool(args, "dryRun", false))
                    {
                        return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                        {
                            ["dryRun"] = true,
                            ["wouldQueue"] = true,
                            ["target"] = DeconstructionTargetInfo(go),
                            ["next"] = "Re-run with confirm=true and dryRun=false to queue deconstruction."
                        }, McpJsonUtil.Settings));
                    }

                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required for deconstruction");

                    string error;
                    if (!TryQueueObjectDeconstruction(go, args, out error))
                        return CallToolResult.Error(error);
                    return CallToolResult.Text($"Queued deconstruction for {go.GetProperName()}");
                }
            };
        }

        private static Dictionary<string, object> DeconstructionTargetInfo(UnityEngine.GameObject go)
        {
            var kpid = go.GetComponent<KPrefabID>();
            int cell = Grid.PosToCell(go);
            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? 0,
                ["name"] = go.GetProperName(),
                ["prefabId"] = kpid?.PrefabTag.Name ?? go.name,
                ["x"] = Grid.CellColumn(cell),
                ["y"] = Grid.CellRow(cell),
                ["worldId"] = go.GetMyWorldId()
            };
        }
    }
}
