using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Server;
using STRINGS;
using OniMcp.Support;

namespace OniMcp.Tools
{
    /// <summary>
    /// ゲーム操作に関する MCP ツール
    /// </summary>
    public static partial class GameControlTools
    {
        public static McpTool ControlDlcActivation()
        {
            return new McpTool
            {
                Name = "game_dlc_activation_control",
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "game_dlc_control", "dlc_activation_control" },
                Tags = new List<string> { "game", "dlc", "pause-menu", "save", "reload", "lifecycle" },
                Description = "DLC のセーブ内有効化を扱う集約ツール。action=list/activate。activate には confirm=true が必須。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list または activate", Required = true, EnumValues = new List<string> { "list", "activate" } },
                    ["includeCosmetic"] = new McpToolParameter { Type = "boolean", Description = "action=list で cosmetic/content-only DLC を含めるか。既定は false", Required = false },
                    ["dlcId"] = new McpToolParameter { Type = "string", Description = "action=activate の DLC id。例: DLC2_ID、DLC3_ID、DLC4_ID、DLC5_ID", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=activate では true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                            return ListDlcActivation().Handler(args);
                        case "activate":
                            return ActivateDlcForSave().Handler(args);
                        default:
                            return CallToolResult.Error("action must be list or activate");
                    }
                }
            };
        }

        public static McpTool ListDlcActivation()
        {
            return new McpTool
            {
                Name = "game_dlc_activation_list",
                Hidden = true,
                Group = "game",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "dlc_activation_list", "game_dlc_list" },
                Tags = new List<string> { "game", "dlc", "pause-menu", "save", "lifecycle" },
                Description = "互換用の入口。game_control domain=dlc action=list を優先すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["includeCosmetic"] = new McpToolParameter { Type = "boolean", Description = "cosmetic/content-only DLC を含めるか。既定は false", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null || SaveLoader.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool includeCosmetic = ToolUtil.GetBool(args, "includeCosmetic", false);
                    var entries = DlcActivationInfos(includeCosmetic).ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["activeSaveFile"] = SaveLoader.GetActiveSaveFilePath(),
                        ["activeDlcIds"] = SaveLoader.Instance.GameInfo.dlcIds != null ? SaveLoader.Instance.GameInfo.dlcIds.ToList() : new List<string>(),
                        ["returned"] = entries.Count,
                        ["dlc"] = entries
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ActivateDlcForSave()
        {
            return new McpTool
            {
                Name = "game_dlc_activate",
                Hidden = true,
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "activate_dlc_for_save", "dlc_activate" },
                Tags = new List<string> { "game", "dlc", "pause-menu", "save", "reload", "lifecycle" },
                Description = "互換用の入口。game_control domain=dlc action=activate を優先すること。先にバックアップ用のセーブを書き込み、現在のセーブを変更して再読み込みする。必ず事前に game_control domain=dlc action=list で有効化できることを確認し、confirm=true を渡すこと",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["dlcId"] = new McpToolParameter { Type = "string", Description = "DLC id。例: DLC2_ID、DLC3_ID、DLC4_ID、DLC5_ID", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険を伴うセーブ変更と再読み込みの確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (Game.Instance == null || SaveLoader.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    string dlcId = (args["dlcId"]?.ToString() ?? "").Trim();
                    string error;
                    var info = DlcActivationInfo(dlcId, includeCosmetic: true, out error);
                    if (info == null)
                        return CallToolResult.Error(error);

                    bool canActivate = (bool)info["canActivate"];
                    if (!canActivate)
                        return CallToolResult.Error(info["blockedReason"]?.ToString() ?? "DLC cannot be activated for this save");

                    string activeSave = SaveLoader.GetActiveSaveFilePath();
                    string backupSave = null;
                    try
                    {
                        string activeSaveFolder = SaveLoader.GetActiveSaveFolder();
                        string baseName = global::SaveGame.Instance?.BaseName;
                        if (!string.IsNullOrWhiteSpace(activeSaveFolder) && !string.IsNullOrWhiteSpace(baseName))
                            backupSave = Path.Combine(activeSaveFolder, baseName + UI.FRONTEND.OPTIONS_SCREEN.TOGGLE_SANDBOX_SCREEN.BACKUP_SAVE_GAME_APPEND + ".sav");
                    }
                    catch
                    {
                        backupSave = null;
                    }

                    MainThreadBridge.Enqueue(new System.Action(() =>
                    {
                        SaveLoader.Instance.UpgradeActiveSaveDLCInfo(dlcId, trigger_load: true);
                    }));

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["scheduled"] = true,
                        ["dlcId"] = dlcId,
                        ["title"] = info["title"],
                        ["activeSaveFile"] = activeSave,
                        ["backupSaveFile"] = backupSave,
                        ["note"] = "SaveLoader.UpgradeActiveSaveDLCInfo was scheduled for the next Unity frame; ONI will save a backup, update the active save, and reload it."
                    }, McpJsonUtil.Settings));
                }
            };
        }


        private static IEnumerable<Dictionary<string, object>> DlcActivationInfos(bool includeCosmetic)
        {
            string error;
            var expansion1 = DlcActivationInfo(DlcManager.EXPANSION1_ID, includeCosmetic: true, out error);
            if (expansion1 != null)
                yield return expansion1;

            foreach (var pair in DlcManager.DLC_PACKS.OrderBy(pair => pair.Key))
            {
                if (!includeCosmetic && pair.Value.isCosmetic)
                    continue;

                var info = DlcActivationInfo(pair.Key, includeCosmetic, out error);
                if (info != null)
                    yield return info;
            }
        }

        private static Dictionary<string, object> DlcActivationInfo(string dlcId, bool includeCosmetic, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(dlcId))
            {
                error = "dlcId is required";
                return null;
            }

            if (!DlcManager.IsDlcId(dlcId))
            {
                error = "Unknown DLC id";
                return null;
            }

            bool isExpansion1 = dlcId == DlcManager.EXPANSION1_ID;
            bool isCosmetic = false;
            string versionLetter = DlcManager.GetContentLetter(dlcId);
            if (DlcManager.DLC_PACKS.TryGetValue(dlcId, out var pack))
            {
                isCosmetic = pack.isCosmetic;
                versionLetter = pack.versionLetter;
            }

            if (isCosmetic && !includeCosmetic)
            {
                error = "Cosmetic/content-only DLC is not part of pause-menu save activation";
                return null;
            }

            bool subscribed = DlcManager.IsContentSubscribed(dlcId);
            bool activeForSave = Game.IsDlcActiveForCurrentSave(dlcId);
            bool userEditable = !isExpansion1 && !isCosmetic;
            string blockedReason = "";
            if (isExpansion1)
                blockedReason = "EXPANSION1_ID is shown in the pause menu but is not user editable for an existing save";
            else if (isCosmetic)
                blockedReason = "Cosmetic/content-only DLC is not a save activation operation";
            else if (!subscribed)
                blockedReason = "DLC content is not subscribed/enabled on this installation";
            else if (activeForSave)
                blockedReason = "DLC is already active for the current save";

            return new Dictionary<string, object>
            {
                ["dlcId"] = dlcId,
                ["title"] = DlcManager.GetDlcTitleNoFormatting(dlcId),
                ["versionLetter"] = versionLetter,
                ["isSubscribed"] = subscribed,
                ["isActiveForSave"] = activeForSave,
                ["isCosmetic"] = isCosmetic,
                ["userEditableInPauseMenu"] = userEditable,
                ["canActivate"] = userEditable && subscribed && !activeForSave,
                ["blockedReason"] = blockedReason
            };
        }

    }
}
