using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static class DiagnosticsTools
    {
        public static McpTool GetColonyDiagnostics()
        {
            return new McpTool
            {
                Name = "colony_diagnostics",
                Group = "colony",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=diagnostic action=diagnostics を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "探索済みセル内の食料のみを集計するか。既定値は true。デバッグ時は false を指定可能", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    var diagnostics = BuildDiagnostics(args);
                    return CallToolResult.Text(JsonConvert.SerializeObject(diagnostics, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetColonyAlerts()
        {
            return new McpTool
            {
                Name = "colony_alerts",
                Group = "colony",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=diagnostic action=alerts を使用してください",
                Handler = args =>
                {
                    var diagnostics = BuildDiagnostics(args);
                    return CallToolResult.Text(JsonConvert.SerializeObject(diagnostics["alerts"], McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListDiagnosticSettings()
        {
            return new McpTool
            {
                Name = "colony_diagnostic_settings_list",
                Hidden = true,
                Group = "colony",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "diagnostic_settings_list", "all_diagnostics_settings_list" },
                Tags = new List<string> { "diagnostics", "alerts", "all-diagnostics", "settings", "criteria" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=diagnostic action=list_settings を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "診断の id/name または子条件の id/name で絞り込み", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "診断の最大返却数。既定値は 100、上限は 300", Required = false }
                },
                Handler = args =>
                {
                    if (!TryGetDiagnosticContext(args, out var utility, out var worldId, out var error))
                        return CallToolResult.Error(error);

                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 100, 300);
                    var diagnostics = DiagnosticRows(utility, worldId)
                        .Where(row => MatchesDiagnosticRow(row, query))
                        .Take(limit)
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["worldId"] = worldId,
                        ["debugNotificationsDisabled"] = DebugHandler.NotificationsDisabled,
                        ["returned"] = diagnostics.Count,
                        ["diagnostics"] = diagnostics
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetDiagnosticSettings()
        {
            return new McpTool
            {
                Name = "colony_diagnostic_settings_set",
                Hidden = true,
                Group = "colony",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "set_diagnostic_settings", "diagnostic_settings_set", "all_diagnostics_settings_set" },
                Tags = new List<string> { "diagnostics", "alerts", "all-diagnostics", "settings", "criteria" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=diagnostic action=set_settings を使用してください。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["diagnosticId"] = new McpToolParameter { Type = "string", Description = "診断 ID。displaySetting または criteriaId の変更時に必須", Required = false },
                    ["displaySetting"] = new McpToolParameter { Type = "string", Description = "表示モード: always、alert_only、never", Required = false, EnumValues = new List<string> { "always", "alert_only", "never" } },
                    ["criteriaId"] = new McpToolParameter { Type = "string", Description = "子条件 ID。criteriaEnabled の指定時に必須", Required = false },
                    ["criteriaEnabled"] = new McpToolParameter { Type = "boolean", Description = "指定した子条件を有効にするか", Required = false },
                    ["debugNotificationsDisabled"] = new McpToolParameter { Type = "boolean", Description = "Debug 通知を無効にするか。InstantBuild/debug UI でのみ表示されるが、状態はグローバルな DebugHandler の状態", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "診断設定の変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to change diagnostic settings");

                    if (!TryGetDiagnosticContext(args, out var utility, out var worldId, out var error))
                        return CallToolResult.Error(error);

                    string diagnosticId = args["diagnosticId"]?.ToString();
                    string criteriaId = args["criteriaId"]?.ToString();
                    var before = DiagnosticSettingsSnapshot(utility, worldId, diagnosticId);

                    if (args["displaySetting"] != null)
                    {
                        if (string.IsNullOrWhiteSpace(diagnosticId))
                            return CallToolResult.Error("diagnosticId is required when displaySetting is provided");
                        if (!utility.diagnosticDisplaySettings[worldId].ContainsKey(diagnosticId))
                            return CallToolResult.Error($"Diagnostic not found: {diagnosticId}");
                        if (!TryParseDisplaySetting(args["displaySetting"]?.ToString(), out var displaySetting))
                            return CallToolResult.Error("displaySetting must be one of: always, alert_only, never");

                        if (utility.IsDiagnosticTutorialDisabled(diagnosticId))
                            utility.ClearDiagnosticTutorialSetting(diagnosticId);
                        utility.diagnosticDisplaySettings[worldId][diagnosticId] = displaySetting;
                    }

                    if (args["criteriaEnabled"] != null)
                    {
                        if (string.IsNullOrWhiteSpace(diagnosticId) || string.IsNullOrWhiteSpace(criteriaId))
                            return CallToolResult.Error("diagnosticId and criteriaId are required when criteriaEnabled is provided");
                        var diagnostic = utility.GetDiagnostic(diagnosticId, worldId);
                        if (diagnostic == null)
                            return CallToolResult.Error($"Diagnostic not found: {diagnosticId}");
                        if (!diagnostic.GetCriteria().Any(criteria => criteria.id == criteriaId))
                            return CallToolResult.Error($"Diagnostic criteria not found: {criteriaId}");

                        utility.SetCriteriaEnabled(worldId, diagnosticId, criteriaId, ToolUtil.GetBool(args, "criteriaEnabled", true));
                    }

                    if (args["debugNotificationsDisabled"] != null)
                    {
                        bool desired = ToolUtil.GetBool(args, "debugNotificationsDisabled", false);
                        if (DebugHandler.NotificationsDisabled != desired)
                            DebugHandler.ToggleDisableNotifications();
                    }

                    if (ColonyDiagnosticScreen.Instance != null)
                        ColonyDiagnosticScreen.Instance.RefreshAll();

                    var result = new Dictionary<string, object>
                    {
                        ["worldId"] = worldId,
                        ["before"] = before,
                        ["after"] = DiagnosticSettingsSnapshot(utility, worldId, diagnosticId)
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetGlobalAutoDisinfect()
        {
            return new McpTool
            {
                Name = "colony_auto_disinfect_set",
                Hidden = true,
                Group = "colony",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "auto_disinfect_set", "global_auto_disinfect_set", "disable_auto_disinfect_global" },
                Tags = new List<string> { "auto-disinfect", "disinfect", "global", "care", "消毒", "全局", "禁用消毒" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=diagnostic action=set_auto_disinfect を使用してください。disabled=true は既存および新規オブジェクトの AutoDisinfectable を無効化する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["disabled"] = new McpToolParameter { Type = "boolean", Description = "true=自動消毒を全体で無効化。false=ゲーム既定の自動消毒動作を許可", Required = true },
                    ["applyNow"] = new McpToolParameter { Type = "boolean", Description = "既存のオブジェクトに即時反映するか。既定値は true", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "全体の自動消毒方針の変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to change global auto-disinfect policy");

                    bool disabled = ToolUtil.GetBool(args, "disabled", false);
                    bool applyNow = ToolUtil.GetBool(args, "applyNow", true);
                    var before = AutoDisinfectPolicy.Status();
                    AutoDisinfectPolicy.SetDisabled(disabled, persist: true);
                    int changed = applyNow ? AutoDisinfectPolicy.ApplyToExisting() : 0;
                    var after = AutoDisinfectPolicy.Status();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["before"] = before,
                        ["after"] = after,
                        ["changedExistingObjects"] = changed,
                        ["note"] = disabled
                            ? "Global auto-disinfect is disabled. New AutoDisinfectable objects will be forced off by the MCP policy patch."
                            : "Global auto-disinfect policy is off; existing object states are not force-enabled."
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlDiagnostics()
        {
            return new McpTool
            {
                Name = "diagnostic_control",
                Group = "colony",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "colony_diagnostic_control", "diagnostic_settings_control" },
                Tags = new List<string> { "diagnostics", "alerts", "all-diagnostics", "settings", "criteria", "auto-disinfect" },
                Description = "コロニー診断の統合ツール: action=diagnostics で診断概要を読み取り。action=alerts で警告を読み取り。action=list_settings で診断設定を読み取り。action=set_settings で診断/子条件/Debug 通知を変更。action=set_auto_disinfect で全体の自動消毒方針を設定",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "diagnostics、alerts、list_settings、set_settings、set_auto_disinfect のいずれか", Required = true, EnumValues = new List<string> { "diagnostics", "alerts", "list_settings", "set_settings", "set_auto_disinfect" } },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "action=list_settings/set_settings の場合のワールド ID。既定値は現在アクティブなワールド", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list_settings の場合、診断の id/name または子条件の id/name で絞り込み", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list_settings の場合の診断の最大返却数。既定値は 100、上限は 300", Required = false },
                    ["diagnosticId"] = new McpToolParameter { Type = "string", Description = "action=set_settings の場合の診断 ID。displaySetting または criteriaId の変更時に必須", Required = false },
                    ["displaySetting"] = new McpToolParameter { Type = "string", Description = "action=set_settings の場合の表示モード: always、alert_only、never", Required = false, EnumValues = new List<string> { "always", "alert_only", "never" } },
                    ["criteriaId"] = new McpToolParameter { Type = "string", Description = "action=set_settings の場合の子条件 ID。criteriaEnabled の指定時に必須", Required = false },
                    ["criteriaEnabled"] = new McpToolParameter { Type = "boolean", Description = "action=set_settings の場合、指定した子条件を有効にするか", Required = false },
                    ["debugNotificationsDisabled"] = new McpToolParameter { Type = "boolean", Description = "action=set_settings の場合、Debug 通知を無効にするか", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "action=diagnostics/alerts の場合、探索済みセル内の食料のみを集計するか。既定値は true。デバッグ時は false を指定可能", Required = false },
                    ["disabled"] = new McpToolParameter { Type = "boolean", Description = "action=set_auto_disinfect の場合、true=自動消毒を全体で無効化。false=ゲーム既定の自動消毒動作を許可", Required = false },
                    ["applyNow"] = new McpToolParameter { Type = "boolean", Description = "action=set_auto_disinfect の場合、既存のオブジェクトに即時反映するか。既定値は true", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set_settings/set_auto_disinfect の場合、true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "diagnostics" || action == "summary")
                        return GetColonyDiagnostics().Handler(args);
                    if (action == "alerts")
                        return GetColonyAlerts().Handler(args);
                    if (action == "list_settings" || action == "list")
                        return ListDiagnosticSettings().Handler(args);
                    if (action == "set_settings" || action == "set")
                        return SetDiagnosticSettings().Handler(args);
                    if (action == "set_auto_disinfect" || action == "auto_disinfect")
                        return SetGlobalAutoDisinfect().Handler(args);
                    return CallToolResult.Error("action must be diagnostics, alerts, list_settings, set_settings, or set_auto_disinfect");
                }
            };
        }

        private static Dictionary<string, object> BuildDiagnostics(Newtonsoft.Json.Linq.JObject args)
        {
            int dupes = Components.LiveMinionIdentities.Count;
            bool visibleOnly = ToolUtil.GetBool(args, "visibleOnly", true);
            float caloriesKcal = Components.Edibles.Items
                .Where(e => e != null && e.gameObject != null && ToolUtil.VisibleCellAllowed(Grid.PosToCell(e), visibleOnly))
                .Sum(e => ToolUtil.SafeFloat(e.Calories) / 1000f);
            var foodAccess = FoodAvailabilityReader.CaptureAccess(visibleOnly);
            float stressMax = 0f;
            foreach (var dupe in Components.LiveMinionIdentities.Items)
            {
                stressMax = Math.Max(stressMax, DupeAmountUtil.StressValue(dupe));
            }

            int activeWorldId = ClusterManager.Instance?.activeWorldId ?? 0;
            float oxygenKg = 0f;
            float pollutedOxygenKg = 0f;
            int breathableCells = 0;
            for (int cell = 0; cell < Grid.CellCount; cell++)
            {
                if (!Grid.IsWorldValidCell(cell) || Grid.WorldIdx[cell] != activeWorldId || !Grid.IsVisible(cell))
                    continue;
                var element = Grid.Element[cell];
                if (element == null) continue;
                if (element.id == SimHashes.Oxygen)
                {
                    oxygenKg += ToolUtil.SafeFloat(Grid.Mass[cell]);
                    breathableCells++;
                }
                else if (element.id == SimHashes.ContaminatedOxygen)
                {
                    pollutedOxygenKg += ToolUtil.SafeFloat(Grid.Mass[cell]);
                    breathableCells++;
                }
            }

            var buildingCounts = CountBuildings();
        int oxygenProducers = CountByPrefab(buildingCounts, "OxygenDiffuser") + CountByPrefab(buildingCounts, "MineralDeoxidizer") + CountByPrefab(buildingCounts, "Electrolyzer");
            int beds = CountByPrefab(buildingCounts, "Bed") + CountByPrefab(buildingCounts, "LuxuryBed");
            int toilets = CountByPrefab(buildingCounts, "Outhouse") + CountByPrefab(buildingCounts, "FlushToilet");
            int researchStations = CountByPrefab(buildingCounts, "ResearchCenter") + CountByPrefab(buildingCounts, "AdvancedResearchCenter");
            int batteries = CountByPrefab(buildingCounts, "Battery") + CountByPrefab(buildingCounts, "BatterySmart");

            var alerts = new List<Dictionary<string, object>>();
            AddAlert(alerts, foodAccess.Available < dupes * 2000f, "critical", "food", $"確認済みの未予約・利用可能食料が基準未満です: {Math.Round(foodAccess.Available, 1)} kcal、複製人間 {dupes} 人。");
            AddAlert(alerts, foodAccess.UnknownAvailabilityItems > 0 || foodAccess.UnknownCalorieItems > 0,
                "warning", "food_assessment_incomplete", "食料の利用可否に未判定があります。可視合計を生存余力とみなさないでください。");
            AddAlert(alerts, oxygenProducers == 0, "warning", "oxygen", "酸素を生成する設備が見つかりません。");
            AddAlert(alerts, breathableCells < dupes * 20, "warning", "oxygen", $"探索済みの呼吸可能なセルが少なくなっています: {breathableCells}。");
            AddAlert(alerts, beds < dupes, "warning", "sleep", $"ベッドが不足しています: {beds}/{dupes}。");
            AddAlert(alerts, toilets == 0, "warning", "hygiene", "トイレが見つかりません。");
            AddAlert(alerts, researchStations == 0, "info", "research", "研究端末が見つかりません。");
            AddAlert(alerts, batteries == 0, "info", "power", "バッテリーが見つかりません。");
            AddAlert(alerts, stressMax > 40f, "warning", "stress", $"複製人間の最大ストレス: {Math.Round(stressMax, 1)}。");

            return new Dictionary<string, object>
            {
                ["cycle"] = GameUtil.GetCurrentCycle(),
                ["duplicants"] = dupes,
                ["foodKcal"] = Math.Round(caloriesKcal, 1),
                ["foodScope"] = "known food; not usable reserve",
                ["foodAccess"] = foodAccess.ToDictionary(),
                ["visibleOnly"] = visibleOnly,
                ["oxygenKgVisible"] = Math.Round(oxygenKg, 1),
                ["pollutedOxygenKgVisible"] = Math.Round(pollutedOxygenKg, 1),
                ["breathableCellsVisible"] = breathableCells,
                ["buildings"] = new Dictionary<string, object>
                {
                    ["oxygenProducers"] = oxygenProducers,
                    ["beds"] = beds,
                    ["toilets"] = toilets,
                    ["researchStations"] = researchStations,
                    ["batteries"] = batteries
                },
                ["maxStress"] = Math.Round(stressMax, 1),
                ["alertCount"] = alerts.Count,
                ["alerts"] = alerts
            };
        }

        private static Dictionary<string, int> CountBuildings()
        {
            var counts = new Dictionary<string, int>();
            var seen = new HashSet<string>();
            foreach (var building in Components.BuildingCompletes.Items)
            {
                if (building == null) continue;
                var def = building.Def;
                string prefabId = def?.PrefabID ?? building.name;
                var pos = building.transform.GetPosition();
                string key = prefabId + "|" + Math.Round(pos.x) + "|" + Math.Round(pos.y) + "|" + building.GetMyWorldId();
                if (!seen.Add(key)) continue;
                counts[prefabId] = counts.ContainsKey(prefabId) ? counts[prefabId] + 1 : 1;
            }
            return counts;
        }

        private static int CountByPrefab(Dictionary<string, int> counts, string prefab)
        {
            return counts.Where(kv => kv.Key.IndexOf(prefab, StringComparison.OrdinalIgnoreCase) >= 0).Sum(kv => kv.Value);
        }

        private static void AddAlert(List<Dictionary<string, object>> alerts, bool condition, string severity, string category, string message)
        {
            if (!condition) return;
            alerts.Add(new Dictionary<string, object>
            {
                ["severity"] = severity,
                ["category"] = category,
                ["message"] = message
            });
        }

        private static bool TryGetDiagnosticContext(Newtonsoft.Json.Linq.JObject args, out ColonyDiagnosticUtility utility, out int worldId, out string error)
        {
            utility = ColonyDiagnosticUtility.Instance;
            worldId = ClusterManager.Instance?.activeWorldId ?? -1;
            error = null;

            if (utility == null || ClusterManager.Instance == null)
            {
                error = "Colony diagnostics not initialized";
                return false;
            }

            worldId = ToolUtil.GetInt(args, "worldId") ?? ClusterManager.Instance.activeWorldId;
            if (!utility.diagnosticDisplaySettings.ContainsKey(worldId) || !utility.diagnosticCriteriaDisabled.ContainsKey(worldId))
            {
                error = $"Diagnostic settings not found for world: {worldId}";
                return false;
            }

            return true;
        }

        private static List<Dictionary<string, object>> DiagnosticRows(ColonyDiagnosticUtility utility, int worldId)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var setting in utility.diagnosticDisplaySettings[worldId].OrderBy(kv => utility.GetDiagnosticName(kv.Key)))
            {
                var diagnostic = utility.GetDiagnostic(setting.Key, worldId);
                if (diagnostic == null)
                    continue;

                result.Add(DiagnosticInfo(utility, worldId, diagnostic));
            }
            return result;
        }

        private static Dictionary<string, object> DiagnosticInfo(ColonyDiagnosticUtility utility, int worldId, ColonyDiagnostic diagnostic)
        {
            var result = new Dictionary<string, object>
            {
                ["id"] = diagnostic.id,
                ["name"] = diagnostic.name,
                ["displaySetting"] = DisplaySettingString(utility.diagnosticDisplaySettings[worldId][diagnostic.id]),
                ["tutorialDisabled"] = utility.IsDiagnosticTutorialDisabled(diagnostic.id),
                ["latestOpinion"] = diagnostic.LatestResult.opinion.ToString(),
                ["latestMessage"] = diagnostic.LatestResult.Message,
                ["criteria"] = diagnostic.GetCriteria().Select(criteria => new Dictionary<string, object>
                {
                    ["id"] = criteria.id,
                    ["name"] = criteria.name,
                    ["enabled"] = utility.IsCriteriaEnabled(worldId, diagnostic.id, criteria.id)
                }).ToList()
            };
            return result;
        }

        private static Dictionary<string, object> DiagnosticSettingsSnapshot(ColonyDiagnosticUtility utility, int worldId, string diagnosticId)
        {
            var result = new Dictionary<string, object>
            {
                ["debugNotificationsDisabled"] = DebugHandler.NotificationsDisabled
            };

            if (!string.IsNullOrWhiteSpace(diagnosticId))
            {
                var diagnostic = utility.GetDiagnostic(diagnosticId, worldId);
                result["diagnostic"] = diagnostic != null ? DiagnosticInfo(utility, worldId, diagnostic) : null;
            }
            else
            {
                result["diagnostics"] = DiagnosticRows(utility, worldId);
            }

            return result;
        }

        private static bool MatchesDiagnosticRow(Dictionary<string, object> row, string query)
        {
            return string.IsNullOrWhiteSpace(query)
                || JsonConvert.SerializeObject(row).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryParseDisplaySetting(string value, out ColonyDiagnosticUtility.DisplaySetting setting)
        {
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "always":
                    setting = ColonyDiagnosticUtility.DisplaySetting.Always;
                    return true;
                case "alert_only":
                case "alertonly":
                case "alert":
                    setting = ColonyDiagnosticUtility.DisplaySetting.AlertOnly;
                    return true;
                case "never":
                    setting = ColonyDiagnosticUtility.DisplaySetting.Never;
                    return true;
                default:
                    setting = ColonyDiagnosticUtility.DisplaySetting.AlertOnly;
                    return false;
            }
        }

        private static string DisplaySettingString(ColonyDiagnosticUtility.DisplaySetting setting)
        {
            switch (setting)
            {
                case ColonyDiagnosticUtility.DisplaySetting.Always:
                    return "always";
                case ColonyDiagnosticUtility.DisplaySetting.Never:
                    return "never";
                default:
                    return "alert_only";
            }
        }
    }
}
