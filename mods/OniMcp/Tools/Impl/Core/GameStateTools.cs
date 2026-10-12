using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
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
        public static McpTool GetRedAlertStatus()
        {
            return new McpTool
            {
                Name = "game_red_alert_status",
                Hidden = true,
                Group = "game",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "red_alert_status", "emergency_status" },
                Tags = new List<string> { "game", "red-alert", "emergency", "priority", "紧急", "红色警戒" },
                Description = "互換用の入口。game_control domain=state action=red_alert_status を優先すること",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["allWorlds"] = new McpToolParameter { Type = "boolean", Description = "読み込み済みの全ワールドを返すか。既定は false", Required = false }
                },
                Handler = args =>
                {
                    if (ClusterManager.Instance == null)
                        return CallToolResult.Error("ClusterManager not initialized");

                    bool allWorlds = ToolUtil.GetBool(args, "allWorlds", false);
                    var worlds = ResolveAlertWorlds(args, allWorlds);
                    if (worlds.Count == 0)
                        return CallToolResult.Error("No matching world found");

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["activeWorldId"] = ClusterManager.Instance.activeWorldId,
                        ["returned"] = worlds.Count,
                        ["worlds"] = worlds.Select(RedAlertWorldInfo).ToList()
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetRedAlert()
        {
            return new McpTool
            {
                Name = "game_red_alert_set",
                Hidden = true,
                Group = "game",
                Mode = "execute",
                Risk = "medium",
                Aliases = new List<string> { "red_alert_set", "emergency_set", "game_emergency_set" },
                Tags = new List<string> { "game", "red-alert", "emergency", "priority", "紧急", "红色警戒" },
                Description = "互換用の入口。game_control domain=state action=set_red_alert を優先すること。既定では現在のワールドだけを変更する。allWorlds=true で読み込み済みの全ワールドを同期できる。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "true で非常警報/緊急モードを有効にし、false で無効にする", Required = true },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド。allWorlds=true の場合は無視する", Required = false },
                    ["allWorlds"] = new McpToolParameter { Type = "boolean", Description = "読み込み済みの全ワールドに適用するか。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "緊急モード切り替えの確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (ClusterManager.Instance == null)
                        return CallToolResult.Error("ClusterManager not initialized");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    if (args["enabled"] == null)
                        return CallToolResult.Error("enabled is required");

                    bool enabled = ToolUtil.GetBool(args, "enabled", false);
                    bool allWorlds = ToolUtil.GetBool(args, "allWorlds", false);
                    var worlds = ResolveAlertWorlds(args, allWorlds);
                    if (worlds.Count == 0)
                        return CallToolResult.Error("No matching world found");

                    var before = worlds.Select(RedAlertWorldInfo).ToList();
                    foreach (var world in worlds)
                    {
                        var alert = world?.AlertManager;
                        if (alert == null)
                            continue;
                        if (alert.IsRedAlertToggledOn() != enabled)
                            alert.ToggleRedAlert(enabled);
                    }
                    var after = worlds.Select(RedAlertWorldInfo).ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["requestedEnabled"] = enabled,
                        ["allWorlds"] = allWorlds,
                        ["changed"] = JsonConvert.SerializeObject(before) != JsonConvert.SerializeObject(after),
                        ["before"] = before,
                        ["after"] = after
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetSandboxMode()
        {
            return new McpTool
            {
                Name = "game_sandbox_mode_set",
                Hidden = true,
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "sandbox_mode_set", "sandbox_toggle" },
                Tags = new List<string> { "game", "sandbox", "debug", "toggle", "top-left" },
                Description = "互換用の入口。game_control domain=state action=set_sandbox_mode を優先すること。左上の Sandbox Toggle と同等の、サンドボックスモードの切り替え。セーブで sandbox が有効になっていることと confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "true でサンドボックスモードを有効にし、false で無効にする", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (Game.Instance == null || global::SaveGame.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    if (!global::SaveGame.Instance.sandboxEnabled)
                        return CallToolResult.Error("Sandbox mode is locked for this save");

                    bool enabled = ToolUtil.GetBool(args, "enabled", false);
                    bool before = Game.Instance.SandboxModeActive;
                    Game.Instance.SandboxModeActive = enabled;
                    TopLeftControlScreen.Instance?.UpdateSandboxToggleState();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["before"] = before,
                        ["after"] = Game.Instance.SandboxModeActive,
                        ["sandboxEnabledForSave"] = global::SaveGame.Instance.sandboxEnabled
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlGameState()
        {
            return new McpTool
            {
                Name = "game_state_control",
                Group = "game",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "game_emergency_control", "game_sandbox_control" },
                Tags = new List<string> { "game", "red-alert", "sandbox", "emergency", "state" },
                Description = "ゲーム状態の集約ツール。action=red_alert_status/set_red_alert/set_sandbox_mode。高リスクの操作には confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "red_alert_status、set_red_alert、set_sandbox_mode のいずれか", Required = true, EnumValues = new List<string> { "red_alert_status", "set_red_alert", "set_sandbox_mode" } },
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "set_red_alert/set_sandbox_mode では true=有効、false=無効", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "red alert のワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["allWorlds"] = new McpToolParameter { Type = "boolean", Description = "red alert で読み込み済みの全ワールドに適用するか、全ワールドを読み取るか。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "set_red_alert/set_sandbox_mode では true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "red_alert_status" || action == "status")
                        return GetRedAlertStatus().Handler(args);
                    if (action == "set_red_alert" || action == "red_alert")
                        return SetRedAlert().Handler(args);
                    if (action == "set_sandbox_mode" || action == "sandbox")
                        return SetSandboxMode().Handler(args);
                    return CallToolResult.Error("action must be red_alert_status, set_red_alert, or set_sandbox_mode");
                }
            };
        }

        private static List<WorldContainer> ResolveAlertWorlds(JObject args, bool allWorlds)
        {
            if (ClusterManager.Instance == null)
                return new List<WorldContainer>();

            if (allWorlds)
                return ClusterManager.Instance.WorldContainers
                    .Where(item => item != null)
                    .OrderBy(item => item.id)
                    .ToList();

            int worldId = ToolUtil.GetInt(args, "worldId") ?? ClusterManager.Instance.activeWorldId;
            var world = ClusterManager.Instance.GetWorld(worldId);
            return world == null ? new List<WorldContainer>() : new List<WorldContainer> { world };
        }

        private static Dictionary<string, object> RedAlertWorldInfo(WorldContainer world)
        {
            var alert = world?.AlertManager;
            return new Dictionary<string, object>
            {
                ["worldId"] = world?.id ?? -1,
                ["name"] = world == null ? null : ToolUtil.CleanName(world.GetProperName()),
                ["isActive"] = ClusterManager.Instance != null && world != null && world.id == ClusterManager.Instance.activeWorldId,
                ["available"] = alert != null,
                ["redAlertToggledOn"] = alert != null && alert.IsRedAlertToggledOn(),
                ["isRedAlert"] = alert != null && alert.IsRedAlert(),
                ["isYellowAlert"] = alert != null && alert.IsYellowAlert(),
                ["isOn"] = alert != null && alert.IsOn()
            };
        }

    }
}
