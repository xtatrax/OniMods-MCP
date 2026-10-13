using System;
using System.Collections.Generic;
using System.Linq;
using Klei.AI;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
{
        internal static Dictionary<string, object> ReadSectionCurrentChore(MinionIdentity dupe) => CurrentChoreSummary(dupe);

        public static McpTool ControlDupeInfo()
        {
            return new McpTool
            {
                Name = "dupes_info_control",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "duplicants_info_control", "dupe_info_control" },
                Tags = new List<string> { "dupes", "duplicants", "detail", "attributes", "needs", "status", "stuck", "trapped", "navigation", "复制人", "属性", "需求", "被困", "状态" },
                Description = "複製人間の基本情報を読み取り専用で取得する集約ツール。action=detail は個別詳細、action=attributes は能力値/特性、action=needs は欲求/ストレス/士気、action=status_check は状態/閉じ込めの確認。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "読み取りの種類: detail、attributes、needs、status_check", Required = true, EnumValues = new List<string> { "detail", "attributes", "needs", "status_check" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。detail では id または name が必須。他の action では空欄なら全員を返す", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。detail では id または name が必須。他の action では空欄なら全員を返す", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "status_check: ワールド ID。既定は全ワールド", Required = false },
                    ["radius"] = new McpToolParameter { Type = "integer", Description = "status_check: 周辺の到達可能性を走査する半径。既定は 8、最大 20", Required = false },
                    ["targetX"] = new McpToolParameter { Type = "integer", Description = "status_check: 任意の対象セル X", Required = false },
                    ["targetY"] = new McpToolParameter { Type = "integer", Description = "status_check: 任意の対象セル Y", Required = false },
                    ["targetWorldId"] = new McpToolParameter { Type = "integer", Description = "status_check: 対象セルのワールド ID", Required = false },
                    ["includeReachableSamples"] = new McpToolParameter { Type = "boolean", Description = "status_check: 到達可能なセルのサンプルを少数返すか。既定は true", Required = false },
                    ["includeDetails"] = new McpToolParameter { Type = "boolean", Description = "status_check: 能力値、スキル、スケジュール、完全な needs の概要を追加するか。既定は false", Required = false },
                    ["detailMode"] = new McpToolParameter { Type = "string", Description = "status_check: compact または full。既定は compact", Required = false, EnumValues = new List<string> { "compact", "full" } },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "status_check: 返す複製人間の最大人数。既定は 50、最大 100", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "detail":
                            return GetDupeDetails().Handler(args);
                        case "attributes":
                            return GetDupeAttributes().Handler(args);
                        case "needs":
                            return GetDupeNeeds().Handler(args);
                        case "status":
                        case "status_check":
                            return GetDupeStatusCheck().Handler(args);
                        default:
                            return CallToolResult.Error("action must be detail, attributes, needs, or status_check");
                    }
                }
            };
        }

        public static McpTool GetDupeDetails()
        {
            return new McpTool
            {
                Name = "dupes_detail",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=info action=detail を使用すること。複製人間の位置、スケジュール、スキル、能力値、欲求、現在の状態の詳細を取得する",
                Parameters = DupeLookupParams(),
                Handler = args =>
                {
                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");
                    return CallToolResult.Text(JsonConvert.SerializeObject(GetDupeDetail(dupe), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetDupeAttributes()
        {
            return new McpTool
            {
                Name = "dupes_attributes",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=info action=attributes を使用すること。1人または全員の複製人間の能力値、興味、習得済みスキルを取得する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。空欄なら全員を返す", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。空欄なら全員を返す", Required = false }
                },
                Handler = args =>
                {
                    var dupe = ToolUtil.FindDupe(args);
                    var dupes = dupe != null ? new List<MinionIdentity> { dupe } : Components.LiveMinionIdentities.Items.Where(d => d != null).ToList();
                    return CallToolResult.Text(JsonConvert.SerializeObject(dupes.Select(GetAttributeSummary).ToList(), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetDupeNeeds()
        {
            return new McpTool
            {
                Name = "dupes_needs",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=info action=needs を使用すること。カロリー、ストレス、膀胱、体温など、複製人間の主要な欲求の数値を取得する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。空欄なら全員を返す", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。空欄なら全員を返す", Required = false }
                },
                Handler = args =>
                {
                    var dupe = ToolUtil.FindDupe(args);
                    var dupes = dupe != null ? new List<MinionIdentity> { dupe } : Components.LiveMinionIdentities.Items.Where(d => d != null).ToList();
                    return CallToolResult.Text(JsonConvert.SerializeObject(dupes.Select(GetNeedsSummary).ToList(), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetDupeStatusCheck()
        {
            return new McpTool
            {
                Name = "dupes_status_check",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "duplicants_status_check", "dupes_stuck_check", "dupe_rescue_check" },
                Tags = new List<string> { "dupes", "duplicants", "status", "stuck", "trapped", "navigation", "rescue", "复制人", "被困", "状态" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。dupes_control domain=info action=status_check を使用すること。【複製人間の状態/閉じ込め確認に優先】位置、現在の作業、主要な欲求、周辺の到達可能なセル、閉じ込めの疑いを一度に返す。読み取り専用で、複製人間を移動させない。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。空欄なら全員を確認する", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。空欄なら全員を確認する", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は全ワールド。指定時はそのワールドの複製人間だけを確認する", Required = false },
                    ["radius"] = new McpToolParameter { Type = "integer", Description = "周辺の到達可能性を走査する半径。既定は 8、最大 20", Required = false },
                    ["targetX"] = new McpToolParameter { Type = "integer", Description = "任意の対象セル X。targetX/targetY を指定すると各複製人間が到達できるか確認する", Required = false },
                    ["targetY"] = new McpToolParameter { Type = "integer", Description = "任意の対象セル Y。targetX/targetY を指定すると各複製人間が到達できるか確認する", Required = false },
                    ["targetWorldId"] = new McpToolParameter { Type = "integer", Description = "対象セルのワールド ID。既定は worldId、または現在アクティブなワールド", Required = false },
                    ["includeReachableSamples"] = new McpToolParameter { Type = "boolean", Description = "到達可能なセルのサンプルを少数返すか。既定は true", Required = false },
                    ["includeDetails"] = new McpToolParameter { Type = "boolean", Description = "能力値、スキル、スケジュール、完全な needs の概要を追加するか。既定は false。待機/優先度/スキルの問題を調べる場合に有効にする", Required = false },
                    ["detailMode"] = new McpToolParameter { Type = "string", Description = "詳細モード: compact はゼロ値/不足しているローカライズ文字列を除外し、full は従来の完全な明細を返す。既定は compact", Required = false, EnumValues = new List<string> { "compact", "full" } },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す複製人間の最大人数。既定は 50、最大 100", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    int radius = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "radius") ?? 8, 20));
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 50, 100));
                    int worldId = ToolUtil.GetInt(args, "worldId") ?? -1;
                    bool includeReachableSamples = ToolUtil.GetBool(args, "includeReachableSamples", true);
                    bool includeDetails = ToolUtil.GetBool(args, "includeDetails", false);
                    string detailMode = NormalizeDupeDetailMode(args["detailMode"]?.ToString());
                    int? targetX = ToolUtil.GetInt(args, "targetX");
                    int? targetY = ToolUtil.GetInt(args, "targetY");
                    int targetWorldId = ToolUtil.GetInt(args, "targetWorldId") ?? (worldId >= 0 ? worldId : ClusterManager.Instance?.activeWorldId ?? 0);

                    var selected = ToolUtil.FindDupe(args);
                    var dupes = selected != null
                        ? new List<MinionIdentity> { selected }
                        : Components.LiveMinionIdentities.Items
                            .Where(dupe => dupe != null)
                            .Where(dupe => worldId < 0 || dupe.GetMyWorldId() == worldId)
                            .OrderBy(dupe => dupe.GetProperName())
                            .Take(limit)
                            .ToList();

                    int? targetCell = null;
                    Dictionary<string, object> target = null;
                    if (targetX.HasValue && targetY.HasValue)
                    {
                        int cell = Grid.XYToCell(targetX.Value, targetY.Value);
                        bool valid = Grid.IsValidCell(cell) && ToolUtil.CellMatchesWorld(cell, targetWorldId);
                        targetCell = valid ? cell : (int?)null;
                        target = new Dictionary<string, object>
                        {
                            ["x"] = targetX.Value,
                            ["y"] = targetY.Value,
                            ["worldId"] = targetWorldId,
                            ["valid"] = valid,
                            ["visible"] = valid && Grid.IsVisible(cell)
                        };
                    }

                    var checks = dupes
                        .Select(dupe => DupeStatusCheck(dupe, radius, targetCell, includeReachableSamples, includeDetails, detailMode))
                        .ToList();
                    var flagged = checks.Where(item => item["risk"].ToString() != "ok").ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["v"] = 1,
                        ["readOnly"] = true,
                        ["radius"] = radius,
                        ["detailMode"] = includeDetails ? detailMode : null,
                        ["target"] = target,
                        ["count"] = checks.Count,
                        ["flagged"] = flagged.Count,
                        ["items"] = checks,
                        ["summary"] = new Dictionary<string, object>
                        {
                            ["critical"] = checks.Count(item => item["risk"].ToString() == "critical"),
                            ["warning"] = checks.Count(item => item["risk"].ToString() == "warning"),
                            ["unknown"] = checks.Count(item => item["risk"].ToString() == "unknown"),
                            ["ok"] = checks.Count(item => item["risk"].ToString() == "ok")
                        },
                        ["recommendedFollowUp"] = flagged.Count == 0
                            ? "No suspected trapped duplicants. Use world_area_snapshot only if visual terrain confirmation is needed."
                            : "Inspect reasons and needsEvidence for flagged dupes. risk=unknown means missing observations, not a confirmed emergency. Use the returned rect for terrain checks before rescue actions."
                    }, McpJsonUtil.Settings));
                }
            };
        }

        internal static Dictionary<string, object> GetDupeDetail(MinionIdentity dupe)
        {
            var pos = dupe.transform.GetPosition();
            var schedulable = dupe.GetComponent<Schedulable>();
            var schedule = schedulable?.GetSchedule();
            var result = GetAttributeSummary(dupe);
            result["position"] = new { x = Math.Round(pos.x, 2), y = Math.Round(pos.y, 2) };
            result["worldId"] = dupe.GetMyWorldId();
            result["schedule"] = schedule?.name;
            result["currentScheduleBlock"] = schedule?.GetCurrentScheduleBlock()?.GroupId;
            result["needs"] = GetNeedsSummary(dupe)["amounts"];
            return result;
        }

        private static Dictionary<string, object> DupeRef(MinionIdentity dupe)
        {
            return new Dictionary<string, object>
            {
                ["id"] = dupe.GetComponent<KPrefabID>()?.InstanceID ?? -1,
                ["name"] = dupe.GetProperName(),
                ["worldId"] = dupe.GetMyWorldId()
            };
        }

        private static Dictionary<string, object> DupeStatusCheck(MinionIdentity dupe, int radius, int? targetCell, bool includeReachableSamples, bool includeDetails, string detailMode)
        {
            var pos = dupe.transform.GetPosition();
            int cell = Grid.PosToCell(dupe);
            int x = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1;
            int y = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1;
            int worldId = dupe.GetMyWorldId();
            var navigator = dupe.GetComponent<Navigator>();
            var moveMonitor = navigator?.GetSMI<MoveToLocationMonitor.Instance>();
            var reachable = ScanReachableNearby(navigator, x, y, worldId, radius, includeReachableSamples);
            var needs = KeyNeedValues(dupe);
            var environment = CellEnvironment(cell);
            var current = CurrentChoreSummary(dupe);
            var reasons = new List<string>();

            bool canReceiveMove = navigator != null && moveMonitor != null;
            bool currentCellValid = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell);
            bool targetReachable = targetCell.HasValue && navigator != null && SafeCanReach(navigator, targetCell.Value);

            if (!currentCellValid)
                reasons.Add("invalid_current_cell");
            if (!canReceiveMove)
                reasons.Add("cannot_receive_move_command");
            if (reachable.ReachableCells == 0)
                reasons.Add("no_reachable_nearby_cells");
            else if (reachable.ReachableCells <= 2)
                reasons.Add("very_few_reachable_nearby_cells");
            if (targetCell.HasValue && !targetReachable)
                reasons.Add("target_unreachable");
            needs.AddRiskReasons(reasons);
            if (environment.TryGetValue("temperatureC", out var tempObj) && tempObj is double tempC && (tempC < -20d || tempC > 60d))
                reasons.Add("dangerous_temperature");
            bool standingInLiquid = environment.TryGetValue("state", out var stateObj) && stateObj?.ToString() == "liquid";
            // Foot-cell liquid is an observation, not proof of suffocation or entrapment.
            environment["standingInLiquid"] = standingInLiquid;
            var idle = current == null ? IdleDiagnostics(dupe, reachable, needs, canReceiveMove) : null;
            if (idle != null && idle.ContainsKey("reasonCode"))
                reasons.Add("idle_" + idle["reasonCode"]);

            var bionicNeeds = NativeBionicNeeds(dupe);
            bool bionicCoverageComplete = BionicRiskAssessment.AddReasons(bionicNeeds, current, reasons);
            string risk = needs.RiskWithCoverage(reasons, bionicCoverageComplete);

            var result = new Dictionary<string, object>
            {
                ["dupe"] = DupeRef(dupe),
                ["position"] = new Dictionary<string, object>
                {
                    ["x"] = x,
                    ["y"] = y,
                    ["worldId"] = worldId,
                    ["cell"] = cell,
                    ["worldPosition"] = new[] { Math.Round(pos.x, 2), Math.Round(pos.y, 2) }
                },
                ["risk"] = risk,
                ["reasons"] = reasons,
                ["currentChore"] = current,
                ["idle"] = idle,
                ["navigation"] = new Dictionary<string, object>
                {
                    ["hasNavigator"] = navigator != null,
                    ["canReceiveMoveCommand"] = canReceiveMove,
                    ["radius"] = radius,
                    ["reachableCells"] = reachable.ReachableCells,
                    ["visibleCells"] = reachable.VisibleCells,
                    ["solidCells"] = reachable.SolidCells,
                    ["targetReachable"] = targetCell.HasValue ? (object)targetReachable : null,
                    ["samples"] = reachable.Samples
                },
                ["needs"] = needs.ToDictionary(),
                ["needsEvidence"] = needs.EvidenceDictionary(),
                ["bionicNeeds"] = bionicNeeds,
                ["bionicNeedsAssessmentComplete"] = bionicNeeds == null ? (object)null : bionicCoverageComplete,
                ["needsAssessmentComplete"] = needs.IsComplete,
                ["unknownNeeds"] = needs.UnknownFields,
                ["environment"] = environment,
                ["riskScope"] = "observed_needs_and_local_navigation; not a guarantee of safe return",
                ["returnSafety"] = new Dictionary<string, object>
                {
                    ["assessmentComplete"] = false,
                    ["status"] = "not_evaluated",
                    ["reason"] = "breathable_destination_and_route_cost_not_evaluated",
                    ["safe"] = null
                },
                ["scanRect"] = new[] { x - radius, y - radius, x + radius, y + radius },
                ["nextRead"] = risk == "ok" ? null : $"world_area_snapshot x1={x - radius} y1={y - radius} x2={x + radius} y2={y + radius} worldId={worldId} preset=construction encoding=rle"
            };
            if (includeDetails)
            {
                if (detailMode == "full")
                {
                    result["attributes"] = GetAttributeSummary(dupe);
                    result["needsDetail"] = GetNeedsSummary(dupe);
                }
                else
                {
                    result["details"] = GetCompactDupeDiagnosticDetails(dupe);
                }
                result["detailNote"] = detailMode == "full"
                    ? "detailMode=full returns verbose legacy fields and can be token-heavy."
                    : "compact details filter zero-value stats and missing localization names; use detailMode=full only for exhaustive stat dumps.";
            }
            return result;
        }

        private static string NormalizeDupeDetailMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "compact";
            string mode = value.Trim().ToLowerInvariant();
            return mode == "full" || mode == "verbose" || mode == "legacy" ? "full" : "compact";
        }

        private static Dictionary<string, object> GetCompactDupeDiagnosticDetails(MinionIdentity dupe)
        {
            var resume = dupe.GetComponent<MinionResume>();
            var attrs = dupe.GetAttributes();
            var mastered = resume != null
                ? resume.MasteryBySkillID.Where(kv => kv.Value).Select(kv => kv.Key).OrderBy(x => x).ToList()
                : new List<string>();

            var result = new Dictionary<string, object>
            {
                ["profession"] = CleanStatName(attrs?.GetProfession()?.Name, null),
                ["suggestedRole"] = GuessRole(dupe),
                ["availableSkillPoints"] = resume?.AvailableSkillpoints ?? 0,
                ["skillsMasteredCount"] = mastered.Count,
                ["skillsMastered"] = mastered.Take(20).ToList(),
                ["truncatedSkillsMastered"] = Math.Max(0, mastered.Count - 20),
                ["aptitudes"] = CompactAptitudes(resume),
                ["attributes"] = CompactAttributes(dupe),
                ["nonZeroAmounts"] = CompactAmounts(dupe)
            };
            return result;
        }

        private static List<Dictionary<string, object>> CompactAttributes(MinionIdentity dupe)
        {
            var result = new List<Dictionary<string, object>>();
            var attrs = dupe.GetAttributes();
            if (attrs == null)
                return result;

            foreach (AttributeInstance attr in attrs)
            {
                if (attr == null || attr.hide)
                    continue;
                double value = Math.Round(attr.GetTotalValue(), 2);
                double baseValue = Math.Round(attr.GetBaseValue(), 2);
                if (Math.Abs(value) < 0.005d && Math.Abs(baseValue) < 0.005d)
                    continue;

                string id = attr.Id;
                string name = CleanStatName(attr.Name, id);
                result.Add(new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["name"] = name,
                    ["value"] = value,
                    ["baseValue"] = baseValue
                });
            }

            return result
                .OrderByDescending(item => Math.Abs(Convert.ToDouble(item["value"])))
                .ThenBy(item => item["id"]?.ToString())
                .Take(24)
                .ToList();
        }

        private static Dictionary<string, double> CompactAptitudes(MinionResume resume)
        {
            if (resume == null)
                return new Dictionary<string, double>();

            return resume.AptitudeBySkillGroup
                .Select(kv => new { Key = kv.Key.ToString(), Value = Math.Round(kv.Value, 2) })
                .Where(kv => Math.Abs(kv.Value) >= 0.005d)
                .OrderByDescending(kv => Math.Abs(kv.Value))
                .ThenBy(kv => kv.Key)
                .Take(12)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private static Dictionary<string, object> CompactAmounts(MinionIdentity dupe)
        {
            var result = new Dictionary<string, object>();
            var amounts = dupe.GetAmounts();
            if (amounts == null)
                return result;

            foreach (var amount in amounts.ModifierList)
            {
                if (amount == null || amount.amount == null)
                    continue;
                float rawValue = amount.value;
                if (float.IsNaN(rawValue) || float.IsInfinity(rawValue)) continue;
                double value = Math.Round(rawValue, 2);
                if (Math.Abs(value) < 0.005d)
                    continue;
                string key = CleanStatName(amount.amount.Name, amount.amount.Id);
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                result[key] = value;
            }

            return result
                .OrderByDescending(kv => Math.Abs(Convert.ToDouble(kv.Value)))
                .ThenBy(kv => kv.Key)
                .Take(20)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private static string CleanStatName(string name, string fallback)
        {
            string cleaned = ToolUtil.CleanName(name);
            if (IsMissingLocalizationName(cleaned))
                cleaned = fallback;
            if (string.IsNullOrWhiteSpace(cleaned))
                cleaned = fallback;
            return cleaned;
        }

        private static bool IsMissingLocalizationName(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf("MISSING.STRINGS", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        }
}
