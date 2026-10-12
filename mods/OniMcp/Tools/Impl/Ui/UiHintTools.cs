using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class UiHintTools
    {
        private static readonly Dictionary<string, MapMarkerHandle> Markers = new Dictionary<string, MapMarkerHandle>();
        private static int markerCounter;

        public static McpTool ControlUiFeedback()
        {
            return new McpTool
            {
                Name = "ui_feedback_control",
                Group = "ui",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "ui_signal_control", "player_feedback_control" },
                Tags = new List<string> { "ui", "feedback", "hint", "marker", "notification" },
                Description = "UI フィードバックの統合窓口: domain=hint。hint は通知、フローティングテキスト、複製人間のチャット吹き出し、マップマーカーを作成する。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "hint。既定値は hint", Required = false, EnumValues = new List<string> { "hint" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "domain=hint: notification/popup/speech_bubble/marker", Required = true },
                    ["markerAction"] = new McpToolParameter { Type = "string", Description = "domain=hint action=marker の場合のサブアクション: create/list/clear", Required = false, EnumValues = new List<string> { "create", "list", "clear" } },
                    ["title"] = new McpToolParameter { Type = "string", Description = "domain=hint action=notification の場合の通知タイトル", Required = false },
                    ["message"] = new McpToolParameter { Type = "string", Description = "domain=hint action=notification の場合の通知本文", Required = false },
                    ["text"] = new McpToolParameter { Type = "string", Description = "domain=hint action=popup/speech_bubble の場合の表示テキスト。speech_bubble では必須", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "domain=hint action=speech_bubble の場合の複製人間名。id とどちらか一方を指定", Required = false },
                    ["style"] = new McpToolParameter { Type = "string", Description = "domain=hint action=popup の場合のアイコンスタイル", Required = false },
                    ["label"] = new McpToolParameter { Type = "string", Description = "domain=hint action=marker markerAction=create の場合のラベルテキスト", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X 座標。action に応じて解釈", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y 座標。action に応じて解釈", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "domain=hint action=speech_bubble の場合は既定値 5 秒、範囲は 0.5-30。popup/marker は各アクションに応じて解釈", Required = false },
                    ["focus"] = new McpToolParameter { Type = "boolean", Description = "カメラを対象に移動するか。action に応じて解釈", Required = false },
                    ["id"] = new McpToolParameter { Type = "string", Description = "action=speech_bubble の場合は複製人間の InstanceID。action=clear の場合は対象 ID", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の返却数の制限", Required = false },
                    ["all"] = new McpToolParameter { Type = "boolean", Description = "action=clear の場合、すべて消去するか", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "強制実行。action に応じて解釈", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? "hint").Trim().ToLowerInvariant();
                    var forwarded = new JObject(args);
                    forwarded.Remove("domain");
                    switch (domain)
                    {
                        case "hint":
                        case "hints":
                        case "ui_hint":
                            return ControlUiHint().Handler(forwarded);
                        default:
                            return CallToolResult.Error("domain must be hint");
                    }
                }
            };
        }

        public static McpTool ControlUiHint()
        {
            return new McpTool
            {
                Name = "ui_hint_control",
                Group = "ui",
                Mode = "execute",
                Risk = "low",
                Aliases = new List<string> { "ui_hints_control", "map_hint_control" },
                Tags = new List<string> { "ui", "hint", "notification", "popup", "marker", "map" },
                Description = "UI の案内作成とマップマーカー管理を統合。action=notification/popup/speech_bubble/marker。marker は markerAction=create/list/clear を使用。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "アクション: notification、popup、speech_bubble、marker", Required = true, EnumValues = new List<string> { "notification", "popup", "speech_bubble", "marker" } },
                    ["markerAction"] = new McpToolParameter { Type = "string", Description = "action=marker の場合のサブアクション: create、list、clear", Required = false, EnumValues = new List<string> { "create", "list", "clear" } },
                    ["title"] = new McpToolParameter { Type = "string", Description = "action=notification の場合の通知タイトル", Required = false },
                    ["message"] = new McpToolParameter { Type = "string", Description = "action=notification の場合の通知ツールチップ/本文", Required = false },
                    ["type"] = new McpToolParameter { Type = "string", Description = "action=notification の場合の通知タイプ: neutral、good、bad_minor、bad、tutorial、message、important、event", Required = false, EnumValues = new List<string> { "neutral", "good", "bad_minor", "bad", "tutorial", "message", "important", "event" } },
                    ["playSound"] = new McpToolParameter { Type = "boolean", Description = "action=notification の場合、通知音を再生するか。既定値は true", Required = false },
                    ["clearOnClick"] = new McpToolParameter { Type = "boolean", Description = "action=notification の場合、通知クリック後に消去するか。既定値は false", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "action=notification の場合、任意のフォーカス先セルの X 座標。action=popup/marker create の場合、対象セルの X 座標", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "action=notification の場合、任意のフォーカス先セルの Y 座標。action=popup/marker create の場合、対象セルの Y 座標", Required = false },
                    ["text"] = new McpToolParameter { Type = "string", Description = "action=popup/speech_bubble の場合の表示テキスト。speech_bubble では必須", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "action=speech_bubble の場合の複製人間名。id とどちらか一方を指定", Required = false },
                    ["style"] = new McpToolParameter { Type = "string", Description = "action=popup の場合のアイコンスタイル: info、good、bad、resource、building、research", Required = false, EnumValues = new List<string> { "info", "good", "bad", "resource", "building", "research" } },
                    ["label"] = new McpToolParameter { Type = "string", Description = "action=marker markerAction=create の場合の任意のフローティングラベルのテキスト", Required = false },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "action=speech_bubble の場合は既定値 5 秒、範囲は 0.5-30。popup/marker create は各アクションに応じて解釈", Required = false },
                    ["focus"] = new McpToolParameter { Type = "boolean", Description = "action=popup/marker create の場合、カメラを対象に移動するか", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "action=popup の場合、対象が現在の視野外でも強制的に生成するか。既定値は true", Required = false },
                    ["id"] = new McpToolParameter { Type = "string", Description = "action=speech_bubble の場合は複製人間の InstanceID。action=marker markerAction=clear の場合はマーカー ID", Required = false },
                    ["all"] = new McpToolParameter { Type = "boolean", Description = "action=marker markerAction=clear の場合、すべてのマーカーを消去するか。既定値は false", Required = false },
                    ["expires"] = new McpToolParameter { Type = "boolean", Description = "action=notification の場合、ゲーム通知の既定の表示時間で自動的に消えるか。既定値は true", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "notification":
                            return CreateNotification().Handler(args);
                        case "popup":
                            return CreatePopupText().Handler(args);
                        case "speech_bubble":
                            return CreateDuplicantSpeechBubble().Handler(args);
                        case "marker":
                            var forwarded = (JObject)args.DeepClone();
                            string markerAction = (forwarded["markerAction"]?.ToString() ?? "").Trim().ToLowerInvariant();
                            if (string.IsNullOrEmpty(markerAction))
                                return CallToolResult.Error("markerAction must be create, list, or clear when action=marker");
                            forwarded["action"] = markerAction;
                            forwarded.Remove("markerAction");
                            return ControlMapMarker().Handler(forwarded);
                        default:
                            return CallToolResult.Error("action must be notification, popup, speech_bubble, or marker");
                    }
                }
            };
        }

        public static McpTool CreateNotification()
        {
            return new McpTool
            {
                Name = "game_notification_create",
                Group = "ui",
                Mode = "execute",
                Risk = "low",
                Hidden = true,
                Description = "互換用の窓口: game_control domain=ui uiDomain=feedback action=notification を優先して使用してください。ゲーム標準の通知を作成する。クリック後にマップのセルにフォーカスする設定も指定可能",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["title"] = new McpToolParameter { Type = "string", Description = "通知タイトル", Required = true },
                    ["message"] = new McpToolParameter { Type = "string", Description = "通知ツールチップ/本文", Required = false },
                    ["type"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "通知タイプ: neutral、good、bad_minor、bad、tutorial、message、important、event",
                        Required = false,
                        EnumValues = new List<string> { "neutral", "good", "bad_minor", "bad", "tutorial", "message", "important", "event" }
                    },
                    ["expires"] = new McpToolParameter { Type = "boolean", Description = "ゲーム通知の既定の表示時間で自動的に消えるか。既定値は true", Required = false },
                    ["playSound"] = new McpToolParameter { Type = "boolean", Description = "通知音を再生するか。既定値は true", Required = false },
                    ["clearOnClick"] = new McpToolParameter { Type = "boolean", Description = "通知クリック後に消去するか。既定値は false", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "任意の対象セルの X 座標。指定すると、通知クリック時にそのセルにフォーカスする", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "任意の対象セルの Y 座標。指定すると、通知クリック時にそのセルにフォーカスする", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    if (NotificationManager.Instance == null)
                        return CallToolResult.Error("NotificationManager not available");

                    string title = NormalizeText(args["title"]?.ToString(), 120);
                    if (string.IsNullOrEmpty(title))
                        return CallToolResult.Error("title is required");

                    string message = NormalizeText(args["message"]?.ToString(), 2000);
                    NotificationType type = ParseNotificationType(args["type"]?.ToString());
                    bool expires = ToolUtil.GetBool(args, "expires", true);
                    bool playSound = ToolUtil.GetBool(args, "playSound", true);
                    bool clearOnClick = ToolUtil.GetBool(args, "clearOnClick", false);
                    var target = ResolveTarget(args);

                    Func<List<Notification>, object, string> tooltip = null;
                    if (!string.IsNullOrEmpty(message))
                        tooltip = TooltipText;

                    Notification.ClickCallback callback = null;
                    if (target != null)
                        callback = FocusNotificationTarget;

                    var notification = new Notification(
                        title,
                        type,
                        tooltip,
                        message,
                        expires,
                        0f,
                        callback,
                        target,
                        null,
                        true,
                        clearOnClick,
                        true);

                    notification.playSound = playSound;
                    notification.GameTime = Time.time;
                    notification.Time = KTime.Instance.UnscaledGameTime;
                    NotificationManager.Instance.AddNotification(notification);

                    var result = new Dictionary<string, object>
                    {
                        ["created"] = true,
                        ["title"] = title,
                        ["type"] = type.ToString(),
                        ["expires"] = expires,
                        ["playSound"] = playSound,
                        ["clearOnClick"] = clearOnClick,
                        ["target"] = target?.ToDictionary()
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool CreatePopupText()
        {
            return new McpTool
            {
                Name = "map_popup_text",
                Group = "map",
                Mode = "execute",
                Risk = "low",
                Hidden = true,
                Description = "互換用の窓口: game_control domain=ui uiDomain=feedback action=popup を優先して使用してください。マップのセル上にゲーム標準のフローティングテキストを作成する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X", Required = true },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y", Required = true },
                    ["text"] = new McpToolParameter { Type = "string", Description = "フローティングテキストの内容", Required = true },
                    ["style"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "アイコンスタイル: info、good、bad、resource、building、research",
                        Required = false,
                        EnumValues = new List<string> { "info", "good", "bad", "resource", "building", "research" }
                    },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "表示秒数。既定値は 3、範囲は 0.5-30", Required = false },
                    ["focus"] = new McpToolParameter { Type = "boolean", Description = "カメラも対象に移動するか。既定値は false", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "対象が現在の視野外でも強制的に生成するか。既定値は true", Required = false }
                },
                Handler = args =>
                {
                    var target = ResolveRequiredCell(args);
                    if (target.Error != null)
                        return CallToolResult.Error(target.Error);
                    if (PopFXManager.Instance == null || !PopFXManager.Instance.Ready())
                        return CallToolResult.Error("PopFXManager not available");

                    string text = NormalizeText(args["text"]?.ToString(), 120);
                    if (string.IsNullOrEmpty(text))
                        return CallToolResult.Error("text is required");

                    float duration = Mathf.Clamp(ToolUtil.GetFloat(args, "duration") ?? 3f, 0.5f, 30f);
                    bool focus = ToolUtil.GetBool(args, "focus", false);
                    bool force = ToolUtil.GetBool(args, "force", true);

                    var targetObject = CreateTargetObject("OniMcp_PopupTarget", target.Position);
                    var fx = PopFXManager.Instance.SpawnFX(
                        ResolvePopFxIcon(args["style"]?.ToString()),
                        text,
                        targetObject.transform,
                        Vector3.zero,
                        duration,
                        false,
                        force);
                    UnityEngine.Object.Destroy(targetObject, duration + 1f);

                    if (focus)
                        FocusCell(target);

                    var result = new Dictionary<string, object>
                    {
                        ["created"] = fx != null,
                        ["text"] = text,
                        ["style"] = NormalizeStyle(args["style"]?.ToString()),
                        ["duration"] = duration,
                        ["focused"] = focus,
                        ["target"] = target.ToDictionary()
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        private static McpTool CreateDuplicantSpeechBubble()
        {
            return new McpTool
            {
                Name = "duplicant_speech_bubble_create",
                Group = "ui",
                Mode = "execute",
                Risk = "low",
                Hidden = true,
                Description = "内部用の窓口: 指定した複製人間の頭の横に、複製人間とカメラの移動に追従するチャット吹き出しを表示する",
                Handler = args =>
                {
                    string text = NormalizeText(args["text"]?.ToString(), 220);
                    if (string.IsNullOrEmpty(text))
                        return CallToolResult.Error("text is required");
                    if (args["name"] == null && args["id"] == null)
                        return CallToolResult.Error("name or id is required to select a duplicant");

                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found for the supplied name or id");

                    float duration = Mathf.Clamp(ToolUtil.GetFloat(args, "duration") ?? 5f, 0.5f, 30f);
                    bool created = ToolCallSpeechOverlay.ShowNearDuplicant(text, dupe, duration);
                    var kpid = dupe.GetComponent<KPrefabID>();
                    var result = new Dictionary<string, object>
                    {
                        ["created"] = created,
                        ["text"] = text,
                        ["duration"] = duration,
                        ["dupe"] = new Dictionary<string, object>
                        {
                            ["id"] = kpid?.InstanceID ?? -1,
                            ["name"] = dupe.GetProperName(),
                            ["worldId"] = dupe.GetMyWorldId()
                        }
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

    }
}
