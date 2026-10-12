using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class ToolCatalogTools
{
        public static McpTool GetToolsManifest()
        {
            return new McpTool
            {
                Name = "tools_manifest",
                Group = "tools",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "ONI MCP ツールのグループ、読み書きモード、リスクレベル、引数の概要を取得する。既定の brief は少ないトークンで出力し、必要に応じて detail=full を指定すると完全な引数を確認できる",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["group"] = new McpToolParameter { Type = "string", Description = "ツールグループで絞り込み。例: game、dupes、resources、orders", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "キーワードまたは目的。ツール名、説明、タグ、別名、引数に照合", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "read、write、execute、any のいずれか", Required = false },
                    ["risk"] = new McpToolParameter { Type = "string", Description = "none、low、medium、dangerous、any のいずれか", Required = false },
                    ["detail"] = new McpToolParameter { Type = "string", Description = "brief は最小限、compact は概要、full は完全な引数 schema を返す。既定値は brief", Required = false, EnumValues = new List<string> { "brief", "compact", "full" } },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "公開された統合ツールの最大返却数。既定値は 80、上限は 100", Required = false }
                },
                Handler = args =>
                {
                    string query = (args["query"]?.ToString() ?? "").ToLowerInvariant();
                    string expandedQuery = ExpandQuery(query);
                    string groupFilter = (args["group"]?.ToString() ?? "").ToLowerInvariant();
                    string mode = (args["mode"]?.ToString() ?? "any").ToLowerInvariant();
                    string risk = (args["risk"]?.ToString() ?? "any").ToLowerInvariant();
            string detail = string.IsNullOrWhiteSpace(args["detail"]?.ToString()) ? "brief" : NormalizeDetail(args["detail"]?.ToString().ToLowerInvariant());
            int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 80, 100));
            string exactLookup = ExactVisibleToolLookup(query);

            var matchedTools = OniToolRegistry.GetVisibleTools()
                .Where(tool => string.IsNullOrEmpty(groupFilter) || tool.Group.ToLowerInvariant() == groupFilter)
                .Where(tool => mode == "any" || string.IsNullOrEmpty(mode) || tool.Mode.ToLowerInvariant() == mode)
                .Where(tool => risk == "any" || string.IsNullOrEmpty(risk) || tool.Risk.ToLowerInvariant() == risk)
                .Where(tool => string.IsNullOrEmpty(exactLookup) || string.Equals(tool.Name, exactLookup, StringComparison.Ordinal))
                .Select(tool => new { Tool = tool, Score = Score(tool, expandedQuery, query) })
                        .Where(item => string.IsNullOrEmpty(query) || item.Score > 0)
                        .OrderByDescending(item => item.Score)
                        .ThenBy(item => item.Tool.Group)
                        .ThenBy(item => item.Tool.Name)
                        .Take(limit)
                        .ToList();

                    var groups = matchedTools
                        .GroupBy(item => item.Tool.Group)
                        .OrderBy(group => group.Key)
                        .Select(group => new Dictionary<string, object>
                        {
                            ["group"] = group.Key,
                            ["description"] = GroupDescription(group.Key),
                            ["tools"] = group.Select(item => ManifestByDetail(item.Tool, item.Score, detail)).ToList()
                        })
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["toolCount"] = OniToolRegistry.GetVisibleTools().Count,
                        ["returned"] = matchedTools.Count,
                        ["groupSummary"] = matchedTools
                            .GroupBy(item => item.Tool.Group)
                            .OrderBy(group => group.Key)
                            .Select(group => new Dictionary<string, object>
                            {
                                ["group"] = group.Key,
                                ["count"] = group.Count(),
                                ["description"] = GroupDescription(group.Key),
                                ["sampleTools"] = group.Select(item => item.Tool.Name).OrderBy(name => name).Take(8).ToList()
                            })
                            .ToList(),
                        ["detail"] = detail,
                        ["query"] = query,
                        ["expandedQuery"] = expandedQuery,
                        ["limit"] = limit,
                        ["groups"] = groups,
                        ["modes"] = new[] { "read", "write", "execute" },
                        ["risks"] = new[] { "none", "low", "medium", "dangerous" }
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        private static Dictionary<string, object> ToolToBriefManifest(McpTool tool, int score)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = tool.Name,
                ["g"] = tool.Group,
                ["m"] = tool.Mode,
                ["r"] = tool.Risk,
                ["score"] = score
            };

            var required = tool.Parameters
                .Where(kv => kv.Value.Required)
                .Select(kv => kv.Key)
                .OrderBy(name => name)
                .ToList();
            if (required.Count > 0)
                result["req"] = required;

            AddLegacyAliasDeprecation(result, tool);
            return result;
        }

        private static Dictionary<string, object> ManifestByDetail(McpTool tool, int score, string detail)
        {
            if (detail == "brief")
                return ToolToBriefManifest(tool, score);
            if (detail == "compact")
                return ToolToCompactManifest(tool, score);
            return ToolToManifest(tool);
        }

        private static Dictionary<string, object> ToolToCompactManifest(McpTool tool, int score)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = tool.Name,
                ["group"] = tool.Group,
                ["mode"] = tool.Mode,
                ["risk"] = tool.Risk,
                ["score"] = score,
                ["summary"] = McpToolInfoLocalization.Description(tool.Name, tool.Description),
                ["required"] = tool.Parameters
                    .Where(kv => kv.Value.Required)
                    .Select(kv => kv.Key)
                    .OrderBy(name => name)
                    .ToList(),
                ["aliases"] = tool.Aliases != null && tool.Aliases.Count > 0 ? (object)tool.Aliases : null
            };
            AddLegacyAliasDeprecation(result, tool);
            return result;
        }

        private static Dictionary<string, object> ToolToManifest(McpTool tool)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = tool.Name,
                ["group"] = tool.Group,
                ["mode"] = tool.Mode,
                ["risk"] = tool.Risk,
                ["description"] = McpToolInfoLocalization.Description(tool.Name, tool.Description),
                ["aliases"] = tool.Aliases,
                ["tags"] = tool.Tags,
                ["parameters"] = tool.Parameters
                    .OrderBy(kv => kv.Key)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => new Dictionary<string, object>
                        {
                            ["type"] = kv.Value.Type,
                            ["description"] = McpToolInfoLocalization.ParameterDescription(tool.Name, kv.Key, kv.Value.Description),
                            ["required"] = kv.Value.Required,
                            ["enum"] = kv.Value.SchemaEnumValues
                        }),
                ["exampleArguments"] = ExampleArguments(tool)
            };
            AddLegacyAliasDeprecation(result, tool);
            return result;
        }

        private static void AddLegacyAliasDeprecation(Dictionary<string, object> result, McpTool tool)
        {
            if (!ToolMetadata.HasLegacyAliases(tool))
                return;

            result["deprecatedAliases"] = tool.Aliases.OrderBy(alias => alias).ToList();
            result["deprecationWarning"] = ToolMetadata.LegacyAliasesDeprecationWarning(tool);
        }

        private static Dictionary<string, object> ExampleArguments(McpTool tool)
        {
            var example = new Dictionary<string, object>();
            foreach (var param in tool.Parameters)
            {
                if (!param.Value.Required)
                    continue;
                switch (param.Value.Type)
                {
                    case "integer":
                        example[param.Key] = param.Key == "hour" ? 8 : 1;
                        break;
                    case "number":
                        example[param.Key] = 1.0;
                        break;
                    case "boolean":
                        example[param.Key] = true;
                        break;
                    case "array":
                        example[param.Key] = new[] { "Dirt" };
                        break;
                    default:
                        example[param.Key] = param.Value.EnumValues != null && param.Value.EnumValues.Count > 0 ? param.Value.EnumValues[0] : "value";
                        break;
                }
            }
            return example;
        }

        private static string GroupDescription(string group)
        {
            switch (group)
            {
                case "tools": return "ツールカタログ、検索、機能の探索。";
                case "database": return "ゲーム内蔵の Database/百科事典項目の照会と、プレイヤー向けの仕組み/計算式の構造化された早見表。";
                case "server": return "MCP サービスの状態と接続情報。";
                case "game": return "ゲーム時間、一時停止、速度、警報/緊急モード、スクリーンショット。";
                case "camera": return "カメラの視点、フォーカス、観察の制御。";
                case "map": return "マップ上の案内、マーカー、視覚的なフィードバック。";
                case "ui": return "管理パネル、オーバーレイ、建築カテゴリ、百科事典、安全な UI Action の入口。";
                case "sandbox": return "サンドボックス/デバッグモードでの生成、元素の塗布、消去操作。";
                case "colony": return "コロニー全体の状態、警告、診断。";
                case "diagnostics": return "診断条件、処理状況、異常/リスクの確認。";
                case "dupes": return "複製人間の一覧、能力値、欲求、改名、自動命名。";
                case "schedules": return "スケジュールの読み取り、時間枠の編集、複製人間の割り当て。";
                case "resources": return "資源、食料、在庫、収納フィルター。";
                case "diet": return "複製人間の食事許可、食料方針、配給の管理。";
                case "filters": return "単一選択の元素フィルターと、ツリー形式/フラット形式の複数選択フィルター。";
                case "controls": return "方向、少数の選択肢、放送チャンネルなどの汎用サイドスクリーンコントロール。";
                case "automation": return "自動化、論理、センサーのしきい値、自動化サイドスクリーンの設定。";
                case "buildings": return "設備の照会、ブループリント、稼働状態、サイドスクリーンの設定、優先度、解体。";
                case "production": return "製造設備、精製設備、調理設備などのレシピキューと生産設定。";
                case "orders": return "マップ領域に対する片付け、採掘などの指示。";
                case "ranching": return "動物の捕獲、解放、牧畜関連の指示。";
                case "farming": return "植物の収穫、掘り起こし、植え付けの選択、植え付けスロットの状態。";
                case "medical": return "医療用ベッド、治療のしきい値、看護関連の割り当て。";
                case "power": return "電力網、バッテリー、発電/消費電力の統計、電力ポートのセル。";
                case "rooms": return "部屋の判定、部屋の種類、士気に関する概要。";
                case "rockets": return "ロケット、発射台、モジュール構成、乗員と貨物、飛行の制御。";
                case "space": return "星図、望遠鏡、宇宙の対象、裂け目に関する操作。";
                case "story": return "ストーリー設備、遺跡施設、転送装置、特殊なストーリーオブジェクト。";
                case "research": return "研究状況、技術一覧、研究キューの設定とキャンセル。";
                case "world": return "マップのセル、元素、温度、気体/液体/固体の統計。";
                default: return "未分類のツール。";
            }
        }
}
}
