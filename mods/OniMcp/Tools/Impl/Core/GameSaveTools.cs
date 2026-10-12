using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Server;
using OniMcp.Support;

namespace OniMcp.Tools
{
    /// <summary>
    /// ゲーム操作に関する MCP ツール
    /// </summary>
    public static partial class GameControlTools
    {
        public static McpTool ControlGameSave()
        {
            return new McpTool
            {
                Name = "game_save_control",
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "game_saves_control", "game_lifecycle_control" },
                Tags = new List<string> { "game", "save", "load", "quit", "pause-menu", "lifecycle" },
                Description = "セーブ/ライフサイクルの集約ツール。action=list/save/load/quit。save/load/quit には confirm=true が必須。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、save、load、quit のいずれか", Required = true, EnumValues = new List<string> { "list", "save", "load", "quit" } },
                    ["type"] = new McpToolParameter { Type = "string", Description = "list/load index の検索範囲: local、cloud、both。既定は both", Required = false, EnumValues = new List<string> { "local", "cloud", "both" } },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す最大件数。既定は 40、最大 200", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "save/quit saveFirst=true で別名保存するファイル名。ディレクトリ区切り文字は使用不可", Required = false },
                    ["overwrite"] = new McpToolParameter { Type = "boolean", Description = "対象ファイルが既に存在する場合に上書きを許可するか。既定は false", Required = false },
                    ["updateActiveSave"] = new McpToolParameter { Type = "boolean", Description = "action=save の保存成功後、対象を現在の active save に設定するか。既定は true", Required = false },
                    ["index"] = new McpToolParameter { Type = "integer", Description = "action=load に使用する、game_control domain=save action=list が返した index。path が空の場合に使う", Required = false },
                    ["path"] = new McpToolParameter { Type = "string", Description = "action=load の完全なセーブパス。ONI のローカルまたはクラウドのセーブディレクトリ内であることが必須", Required = false },
                    ["target"] = new McpToolParameter { Type = "string", Description = "action=quit: menu はメインメニューへ戻り、desktop はプログラムを終了する。既定は menu", Required = false, EnumValues = new List<string> { "menu", "desktop" } },
                    ["saveFirst"] = new McpToolParameter { Type = "boolean", Description = "action=quit で終了前に保存するか。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=save/load/quit では true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                            return ListSaves().Handler(args);
                        case "save":
                            return SaveGame().Handler(args);
                        case "load":
                            return LoadSave().Handler(args);
                        case "quit":
                            return QuitGame().Handler(args);
                        default:
                            return CallToolResult.Error("action must be list, save, load, or quit");
                    }
                }
            };
        }

        public static McpTool ListSaves()
        {
            return new McpTool
            {
                Name = "game_saves_list",
                Hidden = true,
                Group = "game",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "saves_list", "save_files_list" },
                Tags = new List<string> { "game", "save", "load", "pause-menu", "lifecycle" },
                Description = "互換用の入口。game_control domain=save action=list を優先すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["type"] = new McpToolParameter { Type = "string", Description = "local、cloud、both のいずれか。既定は both", Required = false, EnumValues = new List<string> { "local", "cloud", "both" } },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す最大件数。既定は 40、最大 200", Required = false }
                },
                Handler = args =>
                {
                    if (SaveLoader.Instance == null)
                        return CallToolResult.Error("SaveLoader not initialized");

                    SaveLoader.SaveType type = ParseSaveType(args["type"]?.ToString());
                    int limit = ToolUtil.ClampLimit(args, 40, 200);
                    string active = SaveLoader.GetActiveSaveFilePath();
                    var files = SaveLoader.GetAllFiles(sort: true, type: type)
                        .Take(limit)
                        .Select((entry, index) => SaveFileInfo(entry.path, entry.timeStamp, index, active))
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["activeSaveFile"] = active,
                        ["activeSaveFolder"] = SafeCall(SaveLoader.GetActiveSaveFolder),
                        ["activeColonyFolder"] = SafeCall(SaveLoader.GetActiveSaveColonyFolder),
                        ["localSaveRoot"] = SafeCall(SaveLoader.GetSavePrefixAndCreateFolder),
                        ["cloudSaveRoot"] = SafeCall(SaveLoader.GetCloudSavePrefix),
                        ["returned"] = files.Count,
                        ["saves"] = files
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SaveGame()
        {
            return new McpTool
            {
                Name = "game_save",
                Hidden = true,
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "save_game", "save_as" },
                Tags = new List<string> { "game", "save", "save-as", "pause-menu", "filesystem" },
                Description = "互換用の入口。game_control domain=save action=save を優先すること。confirm=true が必須。既存ファイルの上書きには overwrite=true も必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["name"] = new McpToolParameter { Type = "string", Description = "任意の別名保存用ファイル名。ディレクトリ区切り文字は使用不可。.sav を自動補完する", Required = false },
                    ["overwrite"] = new McpToolParameter { Type = "boolean", Description = "対象ファイルが既に存在する場合に上書きを許可するか。既定は false", Required = false },
                    ["updateActiveSave"] = new McpToolParameter { Type = "boolean", Description = "保存成功後、対象を現在の active save に設定するか。既定は true", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "対象パスと上書きの要否をプレビューするだけで、セーブは書き込まない", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険を伴うファイル書き込みの確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (Game.Instance == null || SaveLoader.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    string error;
                    string target = ResolveSaveTarget(args["name"]?.ToString(), out error);
                    if (target == null)
                        return CallToolResult.Error(error);

                    bool overwrite = ToolUtil.GetBool(args, "overwrite", false);
                    bool existedBefore = File.Exists(target);
                    bool updateActiveSave = ToolUtil.GetBool(args, "updateActiveSave", true);
                    string before = SaveLoader.GetActiveSaveFilePath();
                    if (ToolUtil.GetBool(args, "dryRun", false))
                    {
                        return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                        {
                            ["dryRun"] = true,
                            ["wouldSave"] = target,
                            ["target"] = target,
                            ["exists"] = existedBefore,
                            ["wouldOverwrite"] = existedBefore && overwrite,
                            ["requiresOverwrite"] = existedBefore && !overwrite,
                            ["activeSaveBefore"] = before,
                            ["updateActiveSave"] = updateActiveSave,
                            ["next"] = existedBefore && !overwrite
                                ? "Pass overwrite=true and dryRun=false to overwrite this save."
                                : "Pass dryRun=false with confirm=true to save."
                        }, McpJsonUtil.Settings));
                    }
                    if (existedBefore && !overwrite)
                        return CallToolResult.Error("Target save already exists; pass overwrite=true after verifying the path with game_control domain=save action=list");

                    string saved = SaveLoader.Instance.Save(target, isAutoSave: false, updateSavePointer: updateActiveSave);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["saved"] = saved,
                        ["target"] = target,
                        ["overwroteExisting"] = existedBefore,
                        ["activeSaveBefore"] = before,
                        ["activeSaveAfter"] = SaveLoader.GetActiveSaveFilePath(),
                        ["updateActiveSave"] = updateActiveSave
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool LoadSave()
        {
            return new McpTool
            {
                Name = "game_load_save",
                Hidden = true,
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "load_save", "game_load" },
                Tags = new List<string> { "game", "load", "save", "pause-menu", "lifecycle" },
                Description = "互換用の入口。game_control domain=save action=load を優先すること。必ず事前に game_control domain=save action=list で index/path を確認し、confirm=true を渡すこと",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["index"] = new McpToolParameter { Type = "integer", Description = "game_control domain=save action=list が返した index。path が空の場合に使う", Required = false },
                    ["path"] = new McpToolParameter { Type = "string", Description = "完全なセーブパス。ONI のローカルまたはクラウドのセーブディレクトリ内であることが必須", Required = false },
                    ["type"] = new McpToolParameter { Type = "string", Description = "index の検索範囲: local、cloud、both。既定は both", Required = false, EnumValues = new List<string> { "local", "cloud", "both" } },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険を伴う読み込みの確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (SaveLoader.Instance == null)
                        return CallToolResult.Error("SaveLoader not initialized");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    string error;
                    string target = ResolveLoadTarget(args, out error);
                    if (target == null)
                        return CallToolResult.Error(error);

                    int contextGeneration = GameContextLifecycle.BeginSaveLoad();
                    try
                    {
                        LoadScreen.DoLoad(target);
                    }
                    catch
                    {
                        GameContextLifecycle.SaveLoadFailed(contextGeneration);
                        throw;
                    }
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["loading"] = target,
                        ["activeSaveFile"] = SaveLoader.GetActiveSaveFilePath(),
                        ["note"] = "LoadScreen.DoLoad was invoked; current scene will transition through ONI loading."
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool QuitGame()
        {
            return new McpTool
            {
                Name = "game_quit",
                Hidden = true,
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "quit_game", "game_quit_to_menu", "game_quit_to_desktop" },
                Tags = new List<string> { "game", "quit", "pause-menu", "lifecycle", "desktop" },
                Description = "互換用の入口。game_control domain=save action=quit を優先すること。先に保存することもできる。操作は現在のゲームシーンを中断し、desktop はゲームプロセスを終了する。confirm=true が必須",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["target"] = new McpToolParameter { Type = "string", Description = "menu はメインメニューへ戻り、desktop はプログラムを終了する。既定は menu", Required = false, EnumValues = new List<string> { "menu", "desktop" } },
                    ["saveFirst"] = new McpToolParameter { Type = "boolean", Description = "終了前に保存するか。既定は false", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "saveFirst=true で別名保存が必要な場合のファイル名。ディレクトリ区切り文字は使用不可", Required = false },
                    ["overwrite"] = new McpToolParameter { Type = "boolean", Description = "saveFirst=true で対象ファイルが既に存在する場合に上書きを許可するか。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険を伴うライフサイクル操作の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    string target = (args["target"]?.ToString() ?? "menu").Trim().ToLowerInvariant();
                    if (target != "menu" && target != "desktop")
                        return CallToolResult.Error("target must be menu or desktop");

                    string saved = null;
                    bool saveFirst = ToolUtil.GetBool(args, "saveFirst", false);
                    if (saveFirst)
                    {
                        if (SaveLoader.Instance == null)
                            return CallToolResult.Error("SaveLoader not initialized");

                        string error;
                        string saveTarget = ResolveSaveTarget(args["name"]?.ToString(), out error);
                        if (saveTarget == null)
                            return CallToolResult.Error(error);

                        bool overwrite = ToolUtil.GetBool(args, "overwrite", false);
                        if (File.Exists(saveTarget) && !overwrite)
                            return CallToolResult.Error("Target save already exists; pass overwrite=true after verifying the path with game_control domain=save action=list");

                        saved = SaveLoader.Instance.Save(saveTarget, isAutoSave: false, updateSavePointer: true);
                    }

                    MainThreadBridge.Enqueue(new System.Action(() =>
                    {
                        if (target == "desktop")
                            App.Quit();
                        else
                            PauseScreen.TriggerQuitGame();
                    }));

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["scheduled"] = target == "desktop" ? "quit_desktop" : "quit_to_menu",
                        ["saveFirst"] = saveFirst,
                        ["saved"] = saved,
                        ["note"] = target == "desktop"
                            ? "App.Quit was scheduled for the next Unity frame; MCP server process will terminate with the game."
                            : "PauseScreen.TriggerQuitGame was scheduled for the next Unity frame."
                    }, McpJsonUtil.Settings));
                }
            };
        }


        private static void UnpauseAll(SpeedControlScreen speedControl)
        {
            for (int i = 0; i < 16 && speedControl.IsPaused; i++)
                speedControl.Unpause(playSound: i == 0);
        }

        private static SaveLoader.SaveType ParseSaveType(string value)
        {
            switch ((value ?? "both").Trim().ToLowerInvariant())
            {
                case "local": return SaveLoader.SaveType.local;
                case "cloud": return SaveLoader.SaveType.cloud;
                default: return SaveLoader.SaveType.both;
            }
        }

        private static Dictionary<string, object> SaveFileInfo(string path, System.DateTime timeStamp, int index, string active)
        {
            var file = new FileInfo(path);
            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["fileName"] = Path.GetFileName(path),
                ["name"] = Path.GetFileNameWithoutExtension(path),
                ["path"] = path,
                ["folder"] = Path.GetDirectoryName(path),
                ["timestampUtc"] = timeStamp.ToString("o"),
                ["sizeBytes"] = file.Exists ? file.Length : 0,
                ["isActive"] = string.Equals(path, active, StringComparison.OrdinalIgnoreCase),
                ["isAutoSave"] = SaveLoader.IsSaveAuto(path),
                ["location"] = SaveLoader.IsSaveCloud(path) ? "cloud" : SaveLoader.IsSaveLocal(path) ? "local" : "unknown"
            };
        }

        private static string ResolveSaveTarget(string name, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                string active = SaveLoader.GetActiveSaveFilePath();
                if (string.IsNullOrWhiteSpace(active))
                {
                    error = "No active save file; provide name to save into the active colony folder";
                    return null;
                }
                return active;
            }

            string trimmed = name.Trim();
            if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || trimmed.Contains("/")
                || trimmed.Contains("\\"))
            {
                error = "name must be a plain file name without path separators";
                return null;
            }

            string folder = SaveLoader.GetActiveSaveColonyFolder();
            if (string.IsNullOrWhiteSpace(folder))
            {
                error = "Active colony save folder is not available";
                return null;
            }
            return Path.Combine(folder, SaveScreen.GetValidSaveFilename(trimmed));
        }

        private static string ResolveLoadTarget(JObject args, out string error)
        {
            error = null;
            string path = args["path"]?.ToString();
            if (!string.IsNullOrWhiteSpace(path))
            {
                string full = Path.GetFullPath(path);
                if (!File.Exists(full))
                {
                    error = "Save file does not exist";
                    return null;
                }
                if (!IsUnderSaveRoot(full))
                {
                    error = "path must be inside ONI local or cloud save roots";
                    return null;
                }
                return full;
            }

            int index = ToolUtil.GetInt(args, "index") ?? -1;
            if (index < 0)
            {
                error = "index or path is required";
                return null;
            }

            var files = SaveLoader.GetAllFiles(sort: true, type: ParseSaveType(args["type"]?.ToString()));
            if (index >= files.Count)
            {
                error = "index is outside game_control domain=save action=list range";
                return null;
            }
            return files[index].path;
        }

        private static bool IsUnderSaveRoot(string path)
        {
            string full = Path.GetFullPath(path);
            return IsUnderRoot(full, SafeCall(SaveLoader.GetSavePrefixAndCreateFolder))
                   || IsUnderRoot(full, SafeCall(SaveLoader.GetCloudSavePrefix));
        }

        private static bool IsUnderRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                return false;

            string normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return normalizedPath.StartsWith(normalizedRoot, comparison);
        }

        private static string SafeCall(Func<string> getter)
        {
            try
            {
                return getter?.Invoke();
            }
            catch
            {
                return null;
            }
        }
    }
}
