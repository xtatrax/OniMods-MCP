using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
{
        public static McpTool ControlDupeCommands()
        {
            return new McpTool
            {
                Name = "dupes_command_control",
                Group = "dupes",
                Mode = "execute",
                Risk = "medium",
                Aliases = new List<string> { "dupe_command_control", "duplicant_command_control", "dupes_direct_command_control" },
                Tags = new List<string> { "dupes", "commands", "move", "batch", "force", "direct", "rescue", "rename", "auto-rename" },
                Description = "複製人間への直接操作の集約入口: action=move_to は1人の移動、action=force_action はキャンセル/強制移動、action=move_batch_to は一括移動、action=rename/auto_rename は命名。force_action の具体的な操作は commandAction=cancel_all/move_to/cancel_all_and_move で指定する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "直接操作: move_to、force_action、move_batch_to、rename、auto_rename", Required = true, EnumValues = new List<string> { "move_to", "force_action", "move_batch_to", "rename", "auto_rename" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "個別操作の対象となる複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "個別操作の対象となる複製人間の名前", Required = false },
                    ["newName"] = new McpToolParameter { Type = "string", Description = "action=rename の新しい名前", Required = false },
                    ["style"] = new McpToolParameter { Type = "string", Description = "action=auto_rename の命名形式: role_prefix、cn_job、short。既定は role_prefix", Required = false },
                    ["apply"] = new McpToolParameter { Type = "boolean", Description = "action=auto_rename で改名を適用するか。既定は false でプレビューのみ", Required = false },
                    ["commandAction"] = new McpToolParameter { Type = "string", Description = "action=force_action の強制操作: cancel_all、move_to、cancel_all_and_move", Required = false, EnumValues = new List<string> { "cancel_all", "move_to", "cancel_all_and_move" } },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X。一括処理では既定の対象として使用可能", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y。一括処理では既定の対象として使用可能", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。一括処理では既定の対象として使用可能", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Description = "action=move_batch_to の移動指示配列。各項目に id/i または name/n を含める。x/y を省略すると最上位の既定の対象を使う", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=move_batch_to で処理する最大件数。既定は 50、最大 100", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "直接操作の実行確認。書き込み/実行操作では true が必須", Required = true }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "move_to":
                            return MoveDupe().Handler(args);
                        case "move_batch_to":
                            return MoveDupesBatch().Handler(args);
                        case "rename":
                            if (!ToolUtil.GetBool(args, "confirm", false))
                                return CallToolResult.Error("confirm=true is required for action=rename");
                            return RenameDupe().Handler(args);
                        case "auto_rename":
                            if (ToolUtil.GetBool(args, "apply", false) && !ToolUtil.GetBool(args, "confirm", false))
                                return CallToolResult.Error("confirm=true is required for action=auto_rename apply=true");
                            return AutoRenameDupes().Handler(args);
                        case "force_action":
                            var forwarded = (JObject)args.DeepClone();
                            var commandAction = forwarded["commandAction"];
                            if (commandAction == null || string.IsNullOrWhiteSpace(commandAction.ToString()))
                                return CallToolResult.Error("commandAction is required for action=force_action");
                            forwarded["action"] = commandAction;
                            forwarded.Remove("commandAction");
                            return ForceDupeAction().Handler(forwarded);
                        default:
                            return CallToolResult.Error("action must be move_to, force_action, move_batch_to, rename, or auto_rename");
                    }
                }
            };
        }

        public static McpTool MoveDupe()
        {
            return new McpTool
            {
                Name = "dupes_move_to",
                Group = "dupes",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "dupe_move", "move_dupe", "duplicant_move_to" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=command action=move_to を使用すること。ゲーム標準の MoveToLocationMonitor/MoveChore を使い、複製人間に「ここへ移動」を指示する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X。省略時は query/target/search で検索して位置を特定する", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y。省略時は query/target/search で検索して位置を特定する", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "座標省略時、オブジェクト/元素/複製人間の名前で対象セルを検索する", Required = false },
                    ["target"] = new McpToolParameter { Type = "string", Description = "query の別名", Required = false },
                    ["search"] = new McpToolParameter { Type = "string", Description = "query の別名", Required = false },
                    ["nearX"] = new McpToolParameter { Type = "integer", Description = "検索で位置を特定する際、この X 座標に近い順に並べる", Required = false },
                    ["nearY"] = new McpToolParameter { Type = "integer", Description = "検索で位置を特定する際、この Y 座標に近い順に並べる", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "移動指示の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");

                    int x;
                    int y;
                    string resolveError;
                    if (!TryResolveActionCell(args, out x, out y, out resolveError))
                        return CallToolResult.Error(resolveError);
                    int cell = Grid.XYToCell(x, y);
                    if (!Grid.IsValidCell(cell) || !Grid.IsVisible(cell))
                        return CallToolResult.Error("Target cell is invalid or not visible");
                    int worldId = ToolUtil.ResolveWorldId(args, dupe.GetMyWorldId());
                    Dictionary<string, object> moved;
                    string error = TryMoveDupeToCell(dupe, x, y, worldId, out moved);
                    if (error != null)
                        return CallToolResult.Error(error);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["moved"] = true,
                        ["dupe"] = moved["dupe"],
                        ["target"] = moved["target"]
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ForceDupeAction()
        {
            return new McpTool
            {
                Name = "dupes_force_action",
                Group = "dupes",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "dupe_force_action", "duplicant_force_action", "dupe_cancel_all" },
                Tags = new List<string> { "dupes", "force", "cancel", "move", "direct", "rescue" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=command action=force_action commandAction=cancel_all/move_to/cancel_all_and_move を使用すること。複製人間に強制操作を実行する。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "強制操作: cancel_all、move_to、cancel_all_and_move", Required = true, EnumValues = new List<string> { "cancel_all", "move_to", "cancel_all_and_move" } },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "move_to / cancel_all_and_move の対象セル X。省略時は query/target/search で検索して位置を特定する", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "move_to / cancel_all_and_move の対象セル Y。省略時は query/target/search で検索して位置を特定する", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "座標省略時、オブジェクト/元素/複製人間の名前で対象セルを検索する", Required = false },
                    ["target"] = new McpToolParameter { Type = "string", Description = "query の別名", Required = false },
                    ["search"] = new McpToolParameter { Type = "string", Description = "query の別名", Required = false },
                    ["nearX"] = new McpToolParameter { Type = "integer", Description = "検索で位置を特定する際、この X 座標に近い順に並べる", Required = false },
                    ["nearY"] = new McpToolParameter { Type = "integer", Description = "検索で位置を特定する際、この Y 座標に近い順に並べる", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定は複製人間が現在いるワールド", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "強制操作の実行確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");

                    string action = args["action"]?.ToString();
                    if (string.IsNullOrWhiteSpace(action))
                        return CallToolResult.Error("action is required");

                    action = action.Trim().ToLowerInvariant();
                    var response = new Dictionary<string, object>
                    {
                        ["dupe"] = DupeRef(dupe),
                        ["action"] = action
                    };

                    switch (action)
                    {
                        case "cancel_all":
                            response["cancelled"] = ForceCancelAllDupeWork(dupe, "MCP force action");
                            return CallToolResult.Text(JsonConvert.SerializeObject(response, McpJsonUtil.Settings));

                        case "move_to":
                        case "cancel_all_and_move":
                            int x;
                            int y;
                            string resolveError;
                            if (!TryResolveActionCell(args, out x, out y, out resolveError))
                                return CallToolResult.Error(resolveError);

                            if (action == "cancel_all_and_move")
                                response["cancelled"] = ForceCancelAllDupeWork(dupe, "MCP force action before move");

                            int worldId = ToolUtil.ResolveWorldId(args, dupe.GetMyWorldId());
                            Dictionary<string, object> moved;
                            string error = TryMoveDupeToCell(dupe, x, y, worldId, out moved);
                            if (error != null)
                                return CallToolResult.Error(error);

                            response["moved"] = moved;
                            return CallToolResult.Text(JsonConvert.SerializeObject(response, McpJsonUtil.Settings));

                        default:
                            return CallToolResult.Error("Unsupported action; use cancel_all, move_to, or cancel_all_and_move");
                    }
                }
            };
        }

        public static McpTool MoveDupesBatch()
        {
            return new McpTool
            {
                Name = "dupes_move_batch_to",
                Group = "dupes",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "dupes_move_many", "move_dupes_batch", "duplicants_move_batch", "batch_move_dupes" },
                Tags = new List<string> { "dupes", "commands", "move", "batch", "direct" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=command action=move_batch_to を使用すること。複製人間に「ここへ移動」を一括で指示する。items は {id|i,name|n,x,y,worldId|w} に対応し、最上位の x/y/worldId を既定の対象にできる",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["items"] = new McpToolParameter { Type = "array", Description = "移動指示の配列。各項目に id/i または name/n を含める。x/y を省略すると最上位の既定の対象を使う", Required = true },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "既定の対象セル X。items の項目で省略されている場合に使う", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "既定の対象セル Y。items の項目で省略されている場合に使う", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "既定の対象ワールド ID。items の項目で省略されている場合は複製人間の現在のワールドを使う", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "処理する最大件数。既定は 50、最大 100", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "移動指示の一括発行の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to batch move duplicants");
                    var items = args["items"] as JArray;
                    if (items == null || items.Count == 0)
                        return CallToolResult.Error("items array is required");

                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 50, 100));
                    int? defaultX = ToolUtil.GetInt(args, "x");
                    int? defaultY = ToolUtil.GetInt(args, "y");
                    int? defaultWorldId = ToolUtil.GetInt(args, "worldId");
                    var moved = new List<Dictionary<string, object>>();
                    var failed = new List<Dictionary<string, object>>();

                    foreach (var token in items.Take(limit))
                    {
                        var item = token as JObject;
                        if (item == null)
                        {
                            failed.Add(new Dictionary<string, object> { ["reason"] = "item must be an object" });
                            continue;
                        }

                        var lookup = new JObject();
                        JToken idToken = item["id"] ?? item["i"];
                        JToken nameToken = item["name"] ?? item["n"];
                        if (idToken != null)
                            lookup["id"] = idToken;
                        if (nameToken != null)
                            lookup["name"] = nameToken;
                        var dupe = ToolUtil.FindDupe(lookup);
                        if (dupe == null)
                        {
                            failed.Add(new Dictionary<string, object> { ["item"] = item, ["reason"] = "Duplicant not found" });
                            continue;
                        }

                        int? x = GetIntValue(item, "x", null) ?? defaultX;
                        int? y = GetIntValue(item, "y", null) ?? defaultY;
                        if (!x.HasValue || !y.HasValue)
                        {
                            failed.Add(new Dictionary<string, object> { ["dupe"] = DupeRef(dupe), ["reason"] = "x and y are required" });
                            continue;
                        }

                        int worldId = GetIntValue(item, "worldId", "w") ?? defaultWorldId ?? dupe.GetMyWorldId();
                        Dictionary<string, object> movedItem;
                        string error = TryMoveDupeToCell(dupe, x.Value, y.Value, worldId, out movedItem);
                        if (error != null)
                            failed.Add(new Dictionary<string, object> { ["dupe"] = DupeRef(dupe), ["target"] = new { x = x.Value, y = y.Value, worldId }, ["reason"] = error });
                        else
                            moved.Add(movedItem);
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changed"] = moved.Count,
                        ["failed"] = failed.Count,
                        ["processed"] = Math.Min(items.Count, limit),
                        ["limit"] = limit,
                        ["moved"] = moved,
                        ["errors"] = failed
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static bool TryResolveActionCell(JObject args, out int x, out int y, out string error)
        {
            x = 0;
            y = 0;
            error = null;

            int? requestedX = ToolUtil.GetInt(args, "x");
            int? requestedY = ToolUtil.GetInt(args, "y");
            if (requestedX.HasValue && requestedY.HasValue)
            {
                x = requestedX.Value;
                y = requestedY.Value;
                return true;
            }

            return ToolUtil.TryResolveSearchCell(args, out x, out y, out error);
        }

        private static string TryMoveDupeToCell(MinionIdentity dupe, int x, int y, int worldId, out Dictionary<string, object> moved)
        {
            moved = null;
            int cell = Grid.XYToCell(x, y);
            if (!Grid.IsValidCell(cell) || !Grid.IsVisible(cell))
                return "Target cell is invalid or not visible";
            if (!ToolUtil.CellMatchesWorld(cell, worldId))
                return $"Target cell is not in worldId={worldId}";

            var navigator = dupe.GetComponent<Navigator>();
            var moveMonitor = navigator?.GetSMI<MoveToLocationMonitor.Instance>();
            if (navigator == null || moveMonitor == null)
                return "Duplicant cannot receive move-to-location commands";
            if (!navigator.CanReach(cell))
                return "Duplicant cannot reach target cell";

            moveMonitor.MoveToLocation(cell);
            moved = new Dictionary<string, object>
            {
                ["dupe"] = DupeRef(dupe),
                ["target"] = new { x, y, cell, worldId }
            };
            return null;
        }

        private static Dictionary<string, object> ForceCancelAllDupeWork(MinionIdentity dupe, string reason)
        {
            var result = new Dictionary<string, object>
            {
                ["reason"] = reason,
                ["stoppedConsumer"] = false,
                ["stoppedDriver"] = false,
                ["cancelledCurrentChore"] = false,
                ["cancelledBrainChore"] = false,
                ["cancelledBrainFetches"] = false,
                ["cancelledMoveCommand"] = false
            };

            var consumer = dupe.GetComponent<ChoreConsumer>();
            var driver = dupe.GetComponent<ChoreDriver>();
            var currentChore = driver?.GetCurrentChore();
            if (TryCancelChore(currentChore, reason))
                result["cancelledCurrentChore"] = true;

            if (consumer != null && ConsumerStopChoreMethod != null)
            {
                ConsumerStopChoreMethod.Invoke(consumer, null);
                result["stoppedConsumer"] = true;
            }

            if (driver != null)
            {
                driver.StopChore();
                result["stoppedDriver"] = true;
            }

            var brain = dupe.GetComponent<MinionBrain>();
            if (brain != null && BrainCancelFetchesMethod != null)
            {
                BrainCancelFetchesMethod.Invoke(brain, new object[] { reason });
                result["cancelledBrainFetches"] = true;
            }
            if (brain != null && BrainCancelChoreMethod != null)
            {
                BrainCancelChoreMethod.Invoke(brain, new object[] { reason });
                result["cancelledBrainChore"] = true;
            }

            var navigator = dupe.GetComponent<Navigator>();
            var moveMonitor = navigator?.GetSMI<MoveToLocationMonitor.Instance>();
            if (moveMonitor != null && MoveMonitorCancelMethod != null)
            {
                MoveMonitorCancelMethod.Invoke(moveMonitor, null);
                result["cancelledMoveCommand"] = true;
            }

            return result;
        }

        private static bool TryCancelChore(Chore chore, string reason)
        {
            if (chore == null)
                return false;

            if (ChoreCancelWithStringMethod != null)
            {
                ChoreCancelWithStringMethod.Invoke(chore, new object[] { reason });
                return true;
            }

            if (ChoreCancelNoArgsMethod != null)
            {
                ChoreCancelNoArgsMethod.Invoke(chore, null);
                return true;
            }

            return false;
        }

        private static int? GetIntValue(JObject obj, string name, string shortName)
        {
            JToken token = obj[name] ?? (shortName == null ? null : obj[shortName]);
            int value;
            return token != null && int.TryParse(token.ToString(), out value) ? value : (int?)null;
        }
        }
}
