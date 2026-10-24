using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static class UiControlTools
    {
        public static McpTool ControlUi()
        {
            return new McpTool
            {
                Name = "ui_control",
                Group = "ui",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "ui_unified_control", "player_feedback_control" },
                Tags = new List<string> { "ui", "action", "feedback", "hint", "marker", "management", "overlay" },
                Description = McpLocalization.Get("ui_dialog.ui_control_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("ui_dialog.ui_domain_description"), Required = true, EnumValues = new List<string> { "action", "feedback", "dialog", "selection" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("ui_dialog.ui_action_description"), Required = true },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "domain=action action=list の場合の絞り込みタイプ: all/management/overlay/build/navigation。その他の domain ではサブツールの意味に従って使用", Required = false },
                    ["dialog"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("tools.game_control.parameters.dialog"), Required = false, EnumValues = new List<string> { "mod_mismatch" } },
                    ["dialogToken"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("tools.game_control.parameters.dialogToken"), Required = false },
                    ["keepPaused"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.keepPaused"), Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.confirm"), Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.dryRun"), Required = false },
                    ["uiAction"] = new McpToolParameter { Type = "string", Description = "domain=action action=trigger の場合の Action 列挙型の名前", Required = false },
                    ["screen"] = new McpToolParameter { Type = "string", Description = "domain=action action=open_management の場合のページ名", Required = false },
                    ["title"] = new McpToolParameter { Type = "string", Description = "domain=feedback action=notification の場合の通知タイトル", Required = false },
                    ["message"] = new McpToolParameter { Type = "string", Description = "通知本文または案内内容。サブアクションの意味に従って使用", Required = false },
                    ["text"] = new McpToolParameter { Type = "string", Description = "domain=feedback action=popup/speech_bubble の場合の表示テキスト。speech_bubble では必須", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "domain=feedback action=speech_bubble の場合の複製人間名。id とどちらか一方を指定", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("tools.game_control.parameters.id"), Required = false },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "domain=feedback action=speech_bubble の場合の表示秒数。既定値は 5、範囲は 0.5-30", Required = false },
                    ["markerAction"] = new McpToolParameter { Type = "string", Description = "domain=feedback action=marker の場合のサブアクション: create/list/clear", Required = false, EnumValues = new List<string> { "create", "list", "clear" } },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X 座標。action に応じて解釈", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y 座標。action に応じて解釈", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "領域の始点 X 座標。action に応じて解釈", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "領域の始点 Y 座標。action に応じて解釈", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "領域の終点 X 座標。action に応じて解釈", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "領域の終点 Y 座標。action に応じて解釈", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の返却数の制限", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "強制実行。action に応じて解釈", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    switch (domain)
                    {
                        case "action":
                        case "ui_action":
                        case "actions":
                            return UiTools.ControlUiAction().Handler(args);
                        case "dialog":
                            return UiDialogTools.Control(args);
                        case "selection":
                            return UiSelectionTools.Control(args);
                        case "feedback":
                        case "hint":
                        case "hints":
                            return ForwardFeedback(args, "hint");
                        default:
                            return CallToolResult.Error(McpLocalization.Get("ui_dialog.invalid_ui_domain"));
                    }
                }
            };
        }

        private static CallToolResult ForwardFeedback(JObject args, string feedbackDomain)
        {
            var forwarded = (JObject)args.DeepClone();
            forwarded["domain"] = feedbackDomain;
            return UiHintTools.ControlUiFeedback().Handler(forwarded);
        }
    }
}
