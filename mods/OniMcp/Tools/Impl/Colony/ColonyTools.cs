using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    /// <summary>
    /// コロニー情報関連の MCP ツール
    /// </summary>
    public static class ColonyTools
    {
        public static McpTool ControlColony()
        {
            return new McpTool
            {
                Name = "colony_control",
                Group = "colony",
                Mode = "execute",
                Risk = "medium",
                Aliases = new List<string> { "colony_status_control", "colony_ops_control" },
                Tags = new List<string> { "colony", "snapshot", "diagnostics", "alerts", "survival", "long-run", "report", "notifications", "dupes", "worlds", "management", "schedule", "diet", "research", "medical", "bio", "farming", "ranching" },
                Description = "コロニーの統合窓口: domain=snapshot/read/report/diagnostic/survival/notification/management/bio。効率的な状態スナップショット、コロニーの基本状態、レポート、診断/警告/診断設定、長期運転に向けた生存上の問題の優先度判定、HUD 通知、コロニー管理、生物生産を読み取る。書き込み/クリック操作では、下位ツールの confirm/force の意味を維持する。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "snapshot、read、report、diagnostic、survival、notification、management、bio のいずれか。既定値は read", Required = false, EnumValues = new List<string> { "snapshot", "read", "report", "diagnostic", "survival", "notification", "management", "bio" } },
                    ["kind"] = new McpToolParameter { Type = "string", Description = "domain=management の場合は schedule、diet、research、medical。domain=bio かつ bioDomain=ranching の場合は critters/dropoff/incubator", Required = false },
                    ["bioDomain"] = new McpToolParameter { Type = "string", Description = "domain=bio の場合は farming または ranching。互換性のため kind=farming/ranching も bioDomain として扱う", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "domain=snapshot: get。read: status/dupes/worlds/resources。report: report/summary。diagnostic: diagnostics/alerts/list_settings/set_settings/set_auto_disinfect。survival: plan/status。notification: list/click/dismiss。management: schedule/diet/research/medical のサブアクション。bio: farming/ranching のサブアクション", Required = true },
                    ["profile"] = new McpToolParameter { Type = "string", Description = "domain=snapshot：minimal/brief/standard/full", Required = false, EnumValues = new List<string> { "minimal", "brief", "standard", "full" } },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。action に応じて解釈", Required = false },
                    ["includeDupes"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: 複製人間の概要を含めるか。既定値は true", Required = false },
                    ["includeFood"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: 食料の概要を含めるか。既定値は true", Required = false },
                    ["includeResearch"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: 研究状況を含めるか。既定値は true", Required = false },
                    ["includeBuildings"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: 主要な設備の数を含めるか。既定値は true", Required = false },
                    ["includeAlerts"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: スナップショットの警告を含めるか。既定値は true", Required = false },
                    ["includeAtmosphere"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: 可視範囲の気体セルを走査するか。既定値は false", Required = false },
                    ["dupeLimit"] = new McpToolParameter { Type = "integer", Description = "domain=snapshot: 複製人間の詳細の最大返却数", Required = false },
                    ["foodLimit"] = new McpToolParameter { Type = "integer", Description = "domain=snapshot: 食料の種類の最大返却数", Required = false },
                    ["delta"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: 同じ deltaKey の前回呼び出しからの変更のみを返す", Required = false },
                    ["deltaKey"] = new McpToolParameter { Type = "string", Description = "domain=snapshot: delta キャッシュのスロット", Required = false },
                    ["resetDelta"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: delta の基準状態をリセット", Required = false },
                    ["watch"] = new McpToolParameter { Type = "array", Description = "domain=snapshot: 監視する指標の配列またはカンマ区切り文字列。例: stress,food_kcal,red_alert,alerts,oxygen", Required = false },
                    ["watchOnly"] = new McpToolParameter { Type = "boolean", Description = "domain=snapshot: watch 指標、警告レベル、概要のみを返す", Required = false },
                    ["thresholds"] = new McpToolParameter { Type = "object", Description = "domain=snapshot: 監視のしきい値。例: {\"stress\":\">60\"}", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "検索/絞り込みテキスト。action に応じて解釈", Required = false },
                    ["cropSeedOnly"] = new McpToolParameter { Type = "boolean", Description = "domain=bio、bioDomain=farming、action=seed_catalog の場合、CropSeed タグを持つ種子候補のみを返す", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "最大返却数", Required = false },
                    ["includeMessageBody"] = new McpToolParameter { Type = "boolean", Description = "domain=notification action=list: 本文・ツールチップを開封せず取得。既定false。ゲーム言語とリッチテキストを保持", Required = false },
                    ["includePending"] = new McpToolParameter { Type = "boolean", Description = "domain=notification action=list の場合、pending 通知を含めるか", Required = false },
                    ["index"] = new McpToolParameter { Type = "integer", Description = "domain=notification action=click/dismiss の場合の通知インデックス", Required = false },
                    ["title"] = new McpToolParameter { Type = "string", Description = "domain=notification action=click/dismiss の場合、タイトルで照合", Required = false },
                    ["type"] = new McpToolParameter { Type = "string", Description = "domain=notification action=click/dismiss の場合、通知タイプで照合", Required = false },
                    ["allWithSameTitle"] = new McpToolParameter { Type = "boolean", Description = "domain=notification action=dismiss の場合、同じタイトルの通知グループを消去。既定値は true", Required = false },
                    ["diagnosticId"] = new McpToolParameter { Type = "string", Description = "domain=diagnostic action=set_settings の場合の診断 ID", Required = false },
                    ["displaySetting"] = new McpToolParameter { Type = "string", Description = "domain=diagnostic action=set_settings の場合の表示モード: always、alert_only、never", Required = false, EnumValues = new List<string> { "always", "alert_only", "never" } },
                    ["criteriaId"] = new McpToolParameter { Type = "string", Description = "domain=diagnostic action=set_settings の場合の子条件 ID", Required = false },
                    ["criteriaEnabled"] = new McpToolParameter { Type = "boolean", Description = "domain=diagnostic action=set_settings の場合、指定した子条件を有効にするか", Required = false },
                    ["debugNotificationsDisabled"] = new McpToolParameter { Type = "boolean", Description = "domain=diagnostic action=set_settings の場合、Debug 通知を無効にするか", Required = false },
                    ["disabled"] = new McpToolParameter { Type = "boolean", Description = "domain=diagnostic action=set_auto_disinfect の場合、true=自動消毒を全体で無効化", Required = false },
                    ["applyNow"] = new McpToolParameter { Type = "boolean", Description = "domain=diagnostic action=set_auto_disinfect の場合、既存のオブジェクトに即時反映するか。既定値は true", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "対応する下位ツールの強制操作を許可。action に応じて解釈", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "書き込み操作の確認。action に応じて解釈", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? "read").Trim().ToLowerInvariant();
                    var forwarded = new JObject(args);
                    forwarded.Remove("domain");
                    switch (domain)
                    {
                        case "snapshot":
                        case "state":
                        case "overview":
                            return SnapshotTools.GetColonyStateSnapshot().Handler(forwarded);
                        case "read":
                        case "basic":
                            return ReadColonyControl().Handler(forwarded);
                        case "report":
                        case "reports":
                            return ColonyReportTools.ControlColonyReport().Handler(forwarded);
                    case "diagnostic":
                    case "diagnostics":
                        return DiagnosticsTools.ControlDiagnostics().Handler(forwarded);
                    case "survival":
                    case "survive":
                    case "long_run":
                        return SurvivalPlanTools.ControlSurvivalPlan().Handler(forwarded);
                    case "notification":
                        case "notifications":
                            return NotificationTools.ControlNotification().Handler(forwarded);
                        case "management":
                        case "manage":
                        case "screen":
                        case "screens":
                            forwarded["domain"] = (args["kind"]?.ToString() ?? args["managementDomain"]?.ToString() ?? string.Empty).Trim();
                            forwarded.Remove("kind");
                            forwarded.Remove("managementDomain");
                            return ManagementTools.ControlManagement().Handler(forwarded);
                        case "bio":
                        case "life":
                        case "biology":
                            string bioDomain = (args["bioDomain"]?.ToString() ?? string.Empty).Trim();
                            string bioKind = (args["kind"]?.ToString() ?? string.Empty).Trim();
                            if (string.IsNullOrEmpty(bioDomain) && (bioKind == "farming" || bioKind == "ranching"))
                            {
                                bioDomain = bioKind;
                                forwarded.Remove("kind");
                            }
                            forwarded["domain"] = bioDomain;
                            forwarded.Remove("bioDomain");
                            return BioControlTools.ControlBio().Handler(forwarded);
                        default:
                            return CallToolResult.Error("domain must be snapshot, read, report, diagnostic, notification, management, or bio");
                    }
                }
            };
        }

        public static McpTool ReadColonyControl()
        {
            return new McpTool
            {
                Name = "colony_read_control",
                Group = "colony",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "colony_info_control", "colony_basic_read" },
                Tags = new List<string> { "colony", "dupes", "worlds", "resources", "status" },
                Description = "コロニーの基本情報を読み取る統合ツール: action=status/dupes/worlds/resources",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "読み取り対象: status、dupes、worlds、resources のいずれか",
                        Required = true,
                        EnumValues = new List<string> { "status", "dupes", "worlds", "resources" }
                    }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "status":
                            return GetColonyInfo().Handler(args);
                        case "dupes":
                        case "duplicants":
                            return GetDuplicants().Handler(args);
                        case "worlds":
                        case "world_list":
                            return GetWorlds().Handler(args);
                        case "resources":
                        case "discovered_resources":
                            return GetResources().Handler(args);
                        default:
                            return CallToolResult.Error("action must be status, dupes, worlds, or resources");
                    }
                }
            };
        }

        public static McpTool GetColonyInfo()
        {
            return new McpTool
            {
                Name = "colony_status",
                Group = "colony",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "get_colony_info" },
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=read action=status を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    var cycle = GameUtil.GetCurrentCycle();
                    var duplicantCount = Components.LiveMinionIdentities.Count;
                    var worldCount = ClusterManager.Instance?.worldCount ?? 0;
                    var activeWorldId = ClusterManager.Instance?.activeWorldId ?? -1;

                    var info = new Dictionary<string, object>
                    {
                        ["cycle"] = cycle,
                        ["duplicantCount"] = duplicantCount,
                        ["worldCount"] = worldCount,
                        ["activeWorldId"] = activeWorldId,
                        ["gameSpeed"] = Time.timeScale,
                        ["isPaused"] = Time.timeScale == 0f
                    };

                    // セーブ名の取得を試みる
                    try
                    {
                        if (SaveLoader.Instance?.GameInfo != null)
                        {
                            info["saveName"] = SaveLoader.Instance.GameInfo.baseName;
                        }
                    }
                    catch { }

                    return CallToolResult.Text(JsonConvert.SerializeObject(info, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetDuplicants()
        {
            return new McpTool
            {
                Name = "dupes_list",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "get_duplicants" },
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=read action=dupes を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    var minions = new List<Dictionary<string, object>>();

                    foreach (var minion in Components.LiveMinionIdentities.Items)
                    {
                        if (minion == null) continue;

                        var name = minion.GetProperName();
                        var resume = minion.GetComponent<MinionResume>();
                        var skills = resume != null
                            ? resume.MasteryBySkillID.Where(kv => kv.Value).Select(kv => kv.Key).ToList()
                            : new List<string>();

                        var amounts = new Dictionary<string, float>();
                        var amountInstance = minion.GetComponent<Klei.AI.Amounts>();
                        if (amountInstance != null)
                        {
                            foreach (var amount in amountInstance.ModifierList)
                            {
                                if (amount != null)
                                    amounts[amount.amount.Name] = amount.value;
                            }
                        }

                        minions.Add(new Dictionary<string, object>
                        {
                            ["id"] = minion.GetComponent<KPrefabID>()?.InstanceID ?? -1,
                            ["name"] = name,
                            ["skillsMastered"] = skills,
                            ["availableSkillPoints"] = resume?.AvailableSkillpoints ?? 0,
                            ["amounts"] = amounts,
                            ["worldId"] = minion.GetMyWorldId()
                        });
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(minions, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetWorlds()
        {
            return new McpTool
            {
                Name = "world_list",
                Group = "world",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "get_worlds" },
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=read action=worlds を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args =>
                {
                    if (ClusterManager.Instance == null)
                        return CallToolResult.Error("ClusterManager not initialized");

                    var worlds = new List<Dictionary<string, object>>();

                    foreach (var world in ClusterManager.Instance.WorldContainers)
                    {
                        if (world == null) continue;

                        worlds.Add(new Dictionary<string, object>
                        {
                            ["id"] = world.id,
                            ["name"] = world.GetProperName(),
                            ["isActive"] = world.id == ClusterManager.Instance.activeWorldId,
                            ["width"] = world.Width,
                            ["height"] = world.Height,
                            ["asteroidType"] = world.worldName
                        });
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(worlds, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetResources()
        {
            return new McpTool
            {
                Name = "resources_discovered",
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "get_resources" },
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。colony_control domain=read action=resources を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args =>
                {
                    if (DiscoveredResources.Instance == null)
                        return CallToolResult.Error("DiscoveredResources not initialized");

                    var discovered = DiscoveredResources.Instance.GetDiscovered();
                    var resources = discovered.Select(tag => tag.Name).OrderBy(n => n).ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["count"] = resources.Count,
                        ["resources"] = resources
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }
    }
}
