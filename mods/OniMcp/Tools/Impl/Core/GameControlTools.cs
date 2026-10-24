using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    /// <summary>
    /// ゲーム操作に関する MCP ツール
    /// </summary>
    public static partial class GameControlTools
    {
        public static McpTool ControlGame()
        {
            return new McpTool
            {
                Name = "game_control",
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "game_system_control" },
                Tags = new List<string> { "game", "speed", "pause", "save", "launch", "dlc", "red-alert", "sandbox", "debug", "map", "ui", "feedback" },
                Description = McpLocalization.Get("tools.game_control.description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "ゲームのサブシステム: launch、speed、state、save、dlc、sandbox、ui", Required = true, EnumValues = new List<string> { "launch", "speed", "state", "save", "dlc", "sandbox", "ui" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("tools.game_control.parameters.action"), Required = true },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "domain=sandbox のサブドメイン: read、area、entity、map_designate。既定は read。domain=ui uiDomain=action action=list では種類の絞り込みに使用", Required = false, EnumValues = new List<string> { "read", "area", "entity", "map_designate", "all", "management", "overlay", "build", "navigation" } },
                    ["uiDomain"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("tools.game_control.parameters.uiDomain"), Required = false, EnumValues = new List<string> { "action", "feedback", "dialog", "selection" } },
                    ["afterSequence"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("tools.game_control.parameters.afterSequence"), Required = false },
                    ["traceEpoch"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("tools.game_control.parameters.traceEpoch"), Required = false },
                    ["speed"] = new McpToolParameter { Type = "integer", Description = "domain=speed action=set_speed の速度段階: 0=一時停止, 1=通常, 2=高速, 3=超高速", Required = false },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "domain=state の書き込み操作: true=有効、false=無効", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "domain=state のワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["allWorlds"] = new McpToolParameter { Type = "boolean", Description = "domain=state で読み込み済みの全ワールドに適用するか、全ワールドを読み取るか。既定は false", Required = false },
                    ["type"] = new McpToolParameter { Type = "string", Description = "domain=save の list/load index の検索範囲: local、cloud、both。既定は both", Required = false, EnumValues = new List<string> { "local", "cloud", "both" } },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("tools.game_control.parameters.limit"), Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "domain=save で別名保存するファイル名。ディレクトリ区切り文字は使用不可。domain=ui uiDomain=feedback action=speech_bubble では複製人間の名前", Required = false },
                    ["overwrite"] = new McpToolParameter { Type = "boolean", Description = "domain=save action=save で対象ファイルが既に存在する場合に上書きするか", Required = false },
                    ["updateActiveSave"] = new McpToolParameter { Type = "boolean", Description = "domain=save action=save の成功後、現在の active save に設定するか。既定は true", Required = false },
                    ["index"] = new McpToolParameter { Type = "integer", Description = "domain=save action=load に使用する、game_control domain=save action=list が返した index", Required = false },
                    ["path"] = new McpToolParameter { Type = "string", Description = "domain=save action=load または domain=launch action=start の完全なセーブパス。restart_load は現在の active save を保存して再読み込みする", Required = false },
                    ["forceLoad"] = new McpToolParameter { Type = "boolean", Description = "domain=launch action=start: 既にゲーム内にいる場合も対象セーブを強制的に読み込むか。既定は false", Required = false },
                    ["resume"] = new McpToolParameter { Type = "boolean", Description = "domain=launch: start の既定は true、restart_load の既定は false", Required = false },
                    ["jobId"] = new McpToolParameter { Type = "string", Description = "domain=launch action=restart_status の任意のタスク ID", Required = false },
                    ["target"] = new McpToolParameter { Type = "string", Description = "domain=save action=quit: menu はメインメニューへ戻り、desktop はプログラムを終了する。既定は menu", Required = false, EnumValues = new List<string> { "menu", "desktop" } },
                    ["saveFirst"] = new McpToolParameter { Type = "boolean", Description = "domain=save action=quit で終了前に保存するか。既定は false", Required = false },
                    ["includeCosmetic"] = new McpToolParameter { Type = "boolean", Description = "domain=dlc action=list で cosmetic/content-only DLC を含めるか。既定は false", Required = false },
                    ["dlcId"] = new McpToolParameter { Type = "string", Description = "domain=dlc action=activate の DLC id。例: DLC2_ID、DLC3_ID、DLC4_ID、DLC5_ID", Required = false },
                    ["dialog"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("tools.game_control.parameters.dialog"), Required = false, EnumValues = new List<string> { "mod_mismatch" } },
                    ["dialogToken"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("tools.game_control.parameters.dialogToken"), Required = false },
                    ["keepPaused"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.keepPaused"), Required = false },
                    ["uiAction"] = new McpToolParameter { Type = "string", Description = "domain=ui uiDomain=action action=trigger の Action 列挙名", Required = false },
                    ["screen"] = new McpToolParameter { Type = "string", Description = "domain=ui uiDomain=action action=open_management の画面名", Required = false },
                    ["title"] = new McpToolParameter { Type = "string", Description = "domain=ui uiDomain=feedback action=notification の通知タイトル", Required = false },
                    ["message"] = new McpToolParameter { Type = "string", Description = "domain=ui の通知本文または案内内容。action に応じて解釈する", Required = false },
                    ["text"] = new McpToolParameter { Type = "string", Description = "domain=ui uiDomain=feedback action=popup/speech_bubble の表示文。speech_bubble では必須", Required = false },
                    ["duration"] = new McpToolParameter { Type = "number", Description = "domain=ui uiDomain=feedback action=speech_bubble の表示秒数。既定は 5、範囲は 0.5-30", Required = false },
                    ["markerAction"] = new McpToolParameter { Type = "string", Description = "domain=ui uiDomain=feedback action=marker のサブ操作: create/list/clear", Required = false, EnumValues = new List<string> { "create", "list", "clear" } },
                    ["search"] = new McpToolParameter { Type = "string", Description = "domain=sandbox kind=map_designate で検索するテキストマップの断片", Required = false },
                    ["designate"] = new McpToolParameter { Type = "string", Description = "domain=sandbox kind=map_designate で指定する断片。_、same、keep は元のセルを保持する", Required = false },
                    ["replace"] = new McpToolParameter { Type = "string", Description = "旧パラメーターとの互換用。designate を使用すること", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox の開始/対象セルの X。action に応じて解釈する", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox の開始/対象セルの Y。action に応じて解釈する", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox の矩形/検索範囲の左下 X", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox の矩形/検索範囲の左下 Y", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox の矩形/検索範囲の右上 X", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox の矩形/検索範囲の右上 Y", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "domain=sandbox の任意の区域ハンドル", Required = false },
                    ["element"] = new McpToolParameter { Type = "string", Description = "domain=sandbox の paint/flood_fill または map_designate の既定の元素", Required = false },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "domain=sandbox kind=entity action=set_building_enabled_debug の直接変更値。通常操作には building_control config set_enabled を使用", Required = false },
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "domain=sandbox kind=entity action=spawn_entity の Prefab ID", Required = false },
                    ["storyId"] = new McpToolParameter { Type = "string", Description = "domain=sandbox kind=entity action=story_trait_stamp のストーリー ID", Required = false },
                    ["id"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("tools.game_control.parameters.id"), Required = false },
                    ["includeSaveContext"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.includeSaveContext"), Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("tools.game_control.parameters.query"), Required = false },
                    ["massKg"] = new McpToolParameter { Type = "number", Description = "domain=sandbox の元素の質量 kg。action に応じて解釈する", Required = false },
                    ["temperatureK"] = new McpToolParameter { Type = "number", Description = "domain=sandbox の温度 K。action に応じて解釈する", Required = false },
                    ["disease"] = new McpToolParameter { Type = "string", Description = "domain=sandbox の病原菌 ID。既定はなし", Required = false },
                    ["diseaseCount"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox のセルごとの病原菌数。既定は 0", Required = false },
                    ["matchMode"] = new McpToolParameter { Type = "string", Description = "domain=sandbox kind=map_designate の一致処理: unique/first/all。既定は unique", Required = false, EnumValues = new List<string> { "unique", "first", "all" } },
                    ["matchIndex"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox kind=map_designate で複数一致した場合に選ぶ位置。0 始まり", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "domain=sandbox の区域/検索の安全上限", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.dryRun"), Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "domain=sandbox kind=map_designate の検索で未探索セルを unk として扱うか。既定は false", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "domain=sandbox で対応する内部ツールのサンドボックスモードまたは InstantBuild の条件を回避することを許可するか。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("tools.game_control.parameters.confirm"), Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (domain)
                {
                    case "launch":
                    case "start":
                    case "startup":
                        return GameLaunchTools.ControlGameLaunch().Handler(args);
                    case "speed":
                        case "time":
                            return ControlGameSpeed().Handler(args);
                        case "state":
                        case "red_alert":
                            return ControlGameState().Handler(args);
                        case "sandbox":
                        case "sandbox_tools":
                        case "debug":
                            return ForwardSandbox(args);
                        case "ui":
                        case "interface":
                            return ForwardUi(args);
                        case "save":
                        case "saves":
                        case "lifecycle":
                            return ControlGameSave().Handler(args);
                        case "dlc":
                        case "dlc_activation":
                            return ControlDlcActivation().Handler(args);
                        default:
                            return CallToolResult.Error("domain must be speed, state, save, dlc, sandbox, or ui");
                    }
                }
            };
        }

        private static CallToolResult ForwardUi(JObject args)
        {
            var forwarded = args == null ? new JObject() : (JObject)args.DeepClone();
            string uiDomain = (forwarded["uiDomain"]?.ToString() ?? string.Empty).Trim();
            bool uiDomainFromKind = false;
            if (string.IsNullOrWhiteSpace(uiDomain))
            {
                uiDomain = (forwarded["kind"]?.ToString() ?? string.Empty).Trim();
                uiDomainFromKind = true;
            }

            forwarded["domain"] = uiDomain;
            forwarded.Remove("uiDomain");
            if (uiDomainFromKind && !string.IsNullOrWhiteSpace(uiDomain))
                forwarded.Remove("kind");

            return UiControlTools.ControlUi().Handler(forwarded);
        }

        private static CallToolResult ForwardSandbox(JObject args)
        {
            string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            string kind = (args["kind"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            if (SandboxDryRunRoutingPolicy.TryRejectUnsupported(kind, action,
                    ToolUtil.GetBool(args, "dryRun", false), out string dryRunError))
            {
                return CallToolResult.Error(dryRunError);
            }

            if (string.IsNullOrWhiteSpace(kind) &&
                (action == "set_sandbox_mode" || action == "sandbox_mode" || action == "sandbox_toggle" || action == "sandbox"))
                return ControlGameState().Handler(args);

            var forwarded = args == null ? new JObject() : (JObject)args.DeepClone();
            forwarded.Remove("domain");
            return SandboxTools.ControlSandbox().Handler(forwarded);
        }


    }
}
