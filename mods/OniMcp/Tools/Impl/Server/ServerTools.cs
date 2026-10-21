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
    public static partial class ServerTools
    {
        private const string ToolName = "server_control";

        public static McpTool ControlServer()
        {
            return new McpTool
            {
                Name = ToolName,
                Group = "server",
                Mode = "read/execute",
                Risk = "medium",
                Aliases = new List<string> { "mcp_server_control", "server_diagnostics_control", "mcp_client_request_control", "tools_catalog_control", "tools_call_many", "agent_program_execute" },
                Description = "サーバー/MCP の統合窓口: domain=diagnostics action=status/capabilities/logs_tail。domain=client_request action=create_sampling/create_elicitation。domain=catalog action=manifest/search/guide/coverage/static_audit/surface_audit。domain=batch action=call_many でツールを一括呼び出し。domain=program action=execute で制限付きフロー DSL を実行",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "diagnostics、client_request、catalog、batch、program、middleware のいずれか。既定値は diagnostics", Required = false, EnumValues = new List<string> { "diagnostics", "client_request", "catalog", "batch", "program", "middleware" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = "diagnostics: status/capabilities/logs_tail；client_request: create_sampling/create_elicitation；catalog: manifest/search/guide/coverage/static_audit/surface_audit；batch: call_many；program: execute；middleware: queue/status/clear", Required = true },
                    ["includePatches"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("patch_registration.include_patches_description"), Required = false },
                    ["file"] = new McpToolParameter { Type = "string", Description = "diagnostics logs_tail: current または previous", Required = false },
                    ["lines"] = new McpToolParameter { Type = "integer", Description = "diagnostics logs_tail: 末尾から返す行数。既定値は 120、上限は 1000", Required = false },
                    ["filter"] = new McpToolParameter { Type = "string", Description = "diagnostics logs_tail: 任意のキーワード絞り込み", Required = false },
                    ["surface"] = new McpToolParameter { Type = "string", Description = "catalog surface_audit：side_screen/user_menu/management/tool_menu/ui_menu/global_control/notification", Required = false, EnumValues = new List<string> { "side_screen", "user_menu", "management", "tool_menu", "ui_menu", "global_control", "notification" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "catalog manifest/search/coverage/surface_audit のキーワードまたは目的", Required = false },
                    ["goal"] = new McpToolParameter { Type = "string", Description = "catalog guide のプレイヤーの目標または操作の意図", Required = false },
                    ["group"] = new McpToolParameter { Type = "string", Description = "catalog manifest/search/coverage のツールまたは操作グループの絞り込み", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "catalog manifest/search の read/write/execute/any による絞り込み", Required = false },
                    ["risk"] = new McpToolParameter { Type = "string", Description = "catalog manifest/search/static_audit の none/low/medium/dangerous/any による絞り込み", Required = false },
                    ["status"] = new McpToolParameter { Type = "string", Description = "catalog coverage/surface_audit の状態による絞り込み", Required = false, EnumValues = new List<string> { "all", "covered", "partial", "missing", "review", "no_action" } },
                    ["detail"] = new McpToolParameter { Type = "string", Description = "catalog が返す詳細度: brief/compact/full。対応状況は action による", Required = false },
                    ["includeResources"] = new McpToolParameter { Type = "boolean", Description = "catalog coverage で resourceAnchors を返すか", Required = false },
                    ["includeHotkeys"] = new McpToolParameter { Type = "boolean", Description = "catalog coverage でゲームの Action 列挙型のホットキー対応状況の概要を返すか", Required = false },
                    ["includeNoAction"] = new McpToolParameter { Type = "boolean", Description = "catalog surface_audit surface=side_screen で表示専用/プレイヤー操作のないサイドスクリーンを返すか", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "catalog manifest/search/coverage の最大返却項目数", Required = false },
                    ["calls"] = new McpToolParameter { Type = "array", Items = new SchemaProperty { Type = "object" }, Description = "domain=batch action=call_many: 呼び出すツールの配列。形式は [{\"name\":\"tool_name\",\"arguments\":{...}}]。短縮フィールド {t,a} にも対応。最大 20 件", Required = false },
                    ["items"] = new McpToolParameter { Type = "array", Items = new SchemaProperty { Type = "object" }, Description = "domain=batch action=call_many: calls の別名", Required = false },
                    ["defaults"] = new McpToolParameter { Type = "object", Description = "domain=batch action=call_many: 各子呼び出しの arguments に結合する既定の引数オブジェクト。同名の引数は子呼び出し側を優先", Required = false },
                    ["defaultArguments"] = new McpToolParameter { Type = "object", Description = "domain=batch action=call_many: defaults の別名", Required = false },
                    ["stopOnError"] = new McpToolParameter { Type = "boolean", Description = "domain=batch action=call_many: エラー発生後に後続の呼び出しを停止するか。既定値は false", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "domain=batch action=call_many: 構造、安全性、ツールの存在の事前検証のみを行い、子呼び出しは実行しない。既定値は false", Required = false },
                    ["requireAllValid"] = new McpToolParameter { Type = "boolean", Description = "domain=batch action=call_many: 実行前にすべての子呼び出しが事前検証を通過していることを要求。既定値は true", Required = false },
                    ["responseMode"] = new McpToolParameter { Type = "string", Description = "domain=batch action=call_many: full/summary/errors。既定値は summary", Required = false, EnumValues = new List<string> { "full", "summary", "errors" } },
                    ["includeArguments"] = new McpToolParameter { Type = "boolean", Description = "domain=batch action=call_many: defaults を結合した arguments を結果に含めるか。既定値は false", Required = false },
                    ["maxTextChars"] = new McpToolParameter { Type = "integer", Description = "domain=batch action=call_many: summary/errors モードで各項目の text の最大文字数。既定値は 500、上限は 4000", Required = false },
                    ["program"] = new McpToolParameter { Type = "object", Description = "domain=program action=execute: 制限付きフロー DSL のオブジェクト、配列、または JSON 文字列。形式例: {vars:{...},steps:[...]}", Required = false },
                    ["maxSteps"] = new McpToolParameter { Type = "integer", Description = "domain=program action=execute: 最大実行文数。既定値は 200、上限は 2000", Required = false },
                    ["maxLoopIterations"] = new McpToolParameter { Type = "integer", Description = "domain=program action=execute: 個々の while/repeat の最大反復回数。既定値は 100、上限は 1000", Required = false },
                    ["trace"] = new McpToolParameter { Type = "boolean", Description = "domain=program action=execute: 詳細な実行 trace を返すか。既定値は true", Required = false },
                    ["prompt"] = new McpToolParameter { Type = "string", Description = "client_request create_sampling: ユーザーメッセージ", Required = false },
                    ["systemPrompt"] = new McpToolParameter { Type = "string", Description = "client_request create_sampling: 任意の system prompt", Required = false },
                    ["maxTokens"] = new McpToolParameter { Type = "integer", Description = "client_request create_sampling: 最大 token 数。既定値は 1000", Required = false },
                    ["temperature"] = new McpToolParameter { Type = "number", Description = "client_request create_sampling: 任意の temperature", Required = false },
                    ["includeContext"] = new McpToolParameter { Type = "string", Description = "client_request create_sampling: コンテキストの範囲", Required = false, EnumValues = new List<string> { "none", "thisServer", "allServers" } },
                    ["message"] = new McpToolParameter { Type = "string", Description = "client_request create_elicitation または middleware queue: ユーザーに表示する質問、説明、または次回ツール呼び出しの通知", Required = false },
                    ["level"] = new McpToolParameter { Type = "string", Description = "middleware queue: 通知レベル。既定値は info", Required = false, EnumValues = new List<string> { "info", "warning", "error" } },
                    ["fieldName"] = new McpToolParameter { Type = "string", Description = "client_request create_elicitation: 構造化レスポンスのフィールド名。既定値は response", Required = false },
                    ["fieldDescription"] = new McpToolParameter { Type = "string", Description = "client_request create_elicitation: 構造化レスポンスのフィールドの説明", Required = false },
                    ["fieldType"] = new McpToolParameter { Type = "string", Description = "client_request create_elicitation: フィールドの型。既定値は string", Required = false, EnumValues = new List<string> { "string", "boolean", "integer", "number" } },
                    ["required"] = new McpToolParameter { Type = "boolean", Description = "client_request create_elicitation: フィールドが必須か。既定値は true", Required = false },
                    ["schema"] = new McpToolParameter { Type = "string", Description = "client_request create_elicitation: 任意の完全な JSON schema", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? "diagnostics").Trim().ToLowerInvariant();
                    if (domain == "diagnostics" || domain == "diagnostic" || domain == "server")
                        return DiagnosticsControl().Handler(args);
                    if (domain == "client_request" || domain == "client" || domain == "request")
                        return ControlClientRequest().Handler(args);
                    if (domain == "catalog" || domain == "tools" || domain == "tool_catalog")
                    {
                        var forwarded = new JObject(args);
                        forwarded.Remove("domain");
                        return ToolCatalogTools.ControlToolCatalog().Handler(forwarded);
                    }
                    if (domain == "batch" || domain == "call_many" || domain == "many")
                        return ToolBatchTools.CallMany().Handler(args);
                    if (domain == "middleware" || domain == "tool_middleware" || domain == "notice")
                        return ToolCallMiddlewareControl(args);
                    if (domain == "program" || domain == "agent_program" || domain == "flow" || domain == "script")
                    {
                        string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                        if (action == "execute" || action == "run" || action == "start")
                            return AgentProgramTools.ExecuteProgram().Handler(args);
                        return CallToolResult.Error("domain=program action must be execute");
                    }
                    return CallToolResult.Error("domain must be diagnostics, client_request, catalog, batch, program, or middleware");
                }
            };
        }

        internal static bool IsServerControlDomainCall(string name, JObject arguments, params string[] domains)
        {
            McpTool tool;
            if (!OniToolRegistry.TryGetTool(name, out tool)
                || !string.Equals(tool.Name, ToolName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string domain = (arguments?["domain"]?.ToString() ?? "diagnostics").Trim().ToLowerInvariant();
            foreach (var candidate in domains)
            {
                if (string.Equals(domain, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static CallToolResult ToolCallMiddlewareControl(JObject args)
        {
            string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            Dictionary<string, object> payload;

            if (action == "queue" || action == "notify" || action == "notification")
            {
                string message = args["message"]?.ToString();
                if (string.IsNullOrWhiteSpace(message))
                    return CallToolResult.Error("message is required for domain=middleware action=queue");
                payload = ToolCallMiddleware.QueueNotification(message, args["level"]?.ToString());
                payload["queued"] = true;
                payload["delivery"] = "next tools/call response";
                return CallToolResult.Text(JsonConvert.SerializeObject(payload, McpJsonUtil.Settings));
            }

            if (action == "status")
                return CallToolResult.Text(JsonConvert.SerializeObject(ToolCallMiddleware.Status(), McpJsonUtil.Settings));

            if (action == "clear")
                return CallToolResult.Text(JsonConvert.SerializeObject(ToolCallMiddleware.Clear(), McpJsonUtil.Settings));

            return CallToolResult.Error("domain=middleware action must be queue, status, or clear");
        }

        public static McpTool DiagnosticsControl()
        {
            return new McpTool
            {
                Name = "server_diagnostics_control",
                Hidden = true,
                Group = "server",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "server_diagnostics", "mcp_server_diagnostics" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。server_control domain=diagnostics action=status/capabilities/logs_tail を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter
                    {
                        Type = "string",
                        Description = "診断アクション: status は MCP サーバーの状態を返す。capabilities はクライアント機能を返す。logs_tail は最近のゲームログを読み取る",
                        Required = true,
                        EnumValues = new List<string> { "status", "capabilities", "logs_tail" }
                    },
                    ["includePatches"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("patch_registration.include_patches_description"), Required = false },
                    ["file"] = new McpToolParameter { Type = "string", Description = "action=logs_tail で使用: ログファイルは current または previous。既定値は current", Required = false },
                    ["lines"] = new McpToolParameter { Type = "integer", Description = "action=logs_tail で使用: 末尾から返す行数。既定値は 120、上限は 1000", Required = false },
                    ["filter"] = new McpToolParameter { Type = "string", Description = "action=logs_tail で使用: 任意のキーワード絞り込み。大文字と小文字を区別しない", Required = false }
                },
                Handler = args =>
                {
                    string action = args["action"]?.ToString();
                    if (string.Equals(action, "status", StringComparison.OrdinalIgnoreCase))
                        return GetMcpStatus().Handler(args);
                    if (string.Equals(action, "capabilities", StringComparison.OrdinalIgnoreCase))
                        return GetClientCapabilities().Handler(args);
                    if (string.Equals(action, "logs_tail", StringComparison.OrdinalIgnoreCase))
                        return TailLogs().Handler(args);

                    return CallToolResult.Error("Invalid action. Expected status, capabilities, or logs_tail.");
                }
            };
        }


        public static McpTool TailLogs()
        {
            return new McpTool
            {
                Name = "logs_tail",
                Group = "server",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "tail_logs", "player_log" },
                Description = "旧名との互換用。server_diagnostics_control action=logs_tail を推奨",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["file"] = new McpToolParameter { Type = "string", Description = "ログファイル: current または previous。既定値は current", Required = false },
                    ["lines"] = new McpToolParameter { Type = "integer", Description = "末尾から返す行数。既定値は 120、上限は 1000", Required = false },
                    ["filter"] = new McpToolParameter { Type = "string", Description = "任意のキーワード絞り込み。大文字と小文字を区別しない", Required = false }
                },
                Handler = args =>
                {
                    string requested = args["file"]?.ToString();
                    string fileName = string.Equals(requested, "previous", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(requested, "prev", StringComparison.OrdinalIgnoreCase)
                            ? "Player-prev.log"
                            : "Player.log";

                    int lines = 120;
                    if (args["lines"] != null && int.TryParse(args["lines"].ToString(), out int parsed))
                        lines = Math.Max(1, Math.Min(parsed, 1000));

                    string filter = args["filter"]?.ToString();
                    string path = ResolveLogPath(fileName);
                    if (path == null || !File.Exists(path))
                        return CallToolResult.Error($"Log file not found: {fileName}");

                    var allLines = ReadAllLinesShared(path);
                    IEnumerable<string> selected = allLines;
                    if (!string.IsNullOrWhiteSpace(filter))
                    {
                        selected = selected.Where(line =>
                            line != null && line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    var tail = selected.Skip(Math.Max(0, selected.Count() - lines)).ToList();
                    var result = new Dictionary<string, object>
                    {
                        ["path"] = path,
                        ["file"] = fileName,
                        ["lines"] = tail.Count,
                        ["filter"] = string.IsNullOrWhiteSpace(filter) ? null : filter,
                        ["text"] = string.Join("\n", tail.ToArray())
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool GetClientCapabilities()
        {
            return new McpTool
            {
                Name = "mcp_client_capabilities",
                Group = "server",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "client_capabilities", "server_client_capabilities" },
                Description = "旧名との互換用。server_diagnostics_control action=capabilities を推奨",
                Parameters = new Dictionary<string, McpToolParameter>(),
                Handler = args =>
                {
                    var server = McpHttpServer.Instance;
                    object sessions = server == null ? (object)new List<object>() : server.GetSessionSummaries();
                    var result = new Dictionary<string, object>
                    {
                        ["loaded"] = server != null,
                        ["sessions"] = sessions
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool CreateSamplingRequest()
        {
            return new McpTool
            {
                Name = "mcp_sampling_request_create",
                Group = "server",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "sampling_request_create", "client_sampling_request" },
                Description = "旧名との互換用。mcp_client_request_control action=create_sampling を推奨",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["prompt"] = new McpToolParameter { Type = "string", Description = "クライアントモデルに生成または分析を依頼するユーザーメッセージ", Required = true },
                    ["systemPrompt"] = new McpToolParameter { Type = "string", Description = "任意の system prompt", Required = false },
                    ["maxTokens"] = new McpToolParameter { Type = "integer", Description = "最大 token 数。既定値は 1000", Required = false },
                    ["temperature"] = new McpToolParameter { Type = "number", Description = "任意の temperature", Required = false },
                    ["includeContext"] = new McpToolParameter { Type = "string", Description = "コンテキストの範囲", Required = false, EnumValues = new List<string> { "none", "thisServer", "allServers" } }
                },
                Handler = args =>
                {
                    string prompt = args["prompt"]?.ToString();
                    if (string.IsNullOrWhiteSpace(prompt))
                        return CallToolResult.Error("Missing prompt");

                    int maxTokens = ReadInt(args, "maxTokens", 1000, 1, 32000);
                    var @params = new JObject
                    {
                        ["messages"] = new JArray
                        {
                            new JObject
                            {
                                ["role"] = "user",
                                ["content"] = new JObject
                                {
                                    ["type"] = "text",
                                    ["text"] = prompt
                                }
                            }
                        },
                        ["maxTokens"] = maxTokens
                    };

                    string systemPrompt = args["systemPrompt"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(systemPrompt))
                        @params["systemPrompt"] = systemPrompt;

                    string includeContext = args["includeContext"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(includeContext))
                        @params["includeContext"] = includeContext;

                    double temperature;
                    if (args["temperature"] != null && double.TryParse(args["temperature"].ToString(), out temperature))
                        @params["temperature"] = temperature;

                    return CallToolResult.Text(JsonConvert.SerializeObject(ClientRequest("sampling/createMessage", @params), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlClientRequest()
        {
            return new McpTool
            {
                Name = "mcp_client_request_control",
                Hidden = true,
                Group = "server",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "client_request_control", "mcp_client_request" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。server_control domain=client_request action=create_sampling/create_elicitation を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "アクション: create_sampling または create_elicitation", Required = true, EnumValues = new List<string> { "create_sampling", "create_elicitation" } },
                    ["prompt"] = new McpToolParameter { Type = "string", Description = "create_sampling: クライアントモデルに生成または分析を依頼するユーザーメッセージ", Required = false },
                    ["systemPrompt"] = new McpToolParameter { Type = "string", Description = "create_sampling: 任意の system prompt", Required = false },
                    ["maxTokens"] = new McpToolParameter { Type = "integer", Description = "create_sampling: 最大 token 数。既定値は 1000", Required = false },
                    ["temperature"] = new McpToolParameter { Type = "number", Description = "create_sampling: 任意の temperature", Required = false },
                    ["includeContext"] = new McpToolParameter { Type = "string", Description = "create_sampling: コンテキストの範囲", Required = false, EnumValues = new List<string> { "none", "thisServer", "allServers" } },
                    ["message"] = new McpToolParameter { Type = "string", Description = "create_elicitation: ユーザーに表示する質問または説明", Required = false },
                    ["fieldName"] = new McpToolParameter { Type = "string", Description = "create_elicitation: 構造化レスポンスのフィールド名。既定値は response", Required = false },
                    ["fieldDescription"] = new McpToolParameter { Type = "string", Description = "create_elicitation: 構造化レスポンスのフィールドの説明", Required = false },
                    ["fieldType"] = new McpToolParameter { Type = "string", Description = "create_elicitation: フィールドの型。既定値は string", Required = false, EnumValues = new List<string> { "string", "boolean", "integer", "number" } },
                    ["required"] = new McpToolParameter { Type = "boolean", Description = "create_elicitation: フィールドが必須か。既定値は true", Required = false },
                    ["schema"] = new McpToolParameter { Type = "string", Description = "create_elicitation: 任意の完全な JSON schema。指定時は field* 引数を上書き", Required = false }
                },
                Handler = args =>
                {
                    string action = args["action"]?.ToString();
                    switch (action)
                    {
                        case "create_sampling":
                            return CreateSamplingRequest().Handler(args);
                        case "create_elicitation":
                            return CreateElicitationRequest().Handler(args);
                        default:
                            return CallToolResult.Error("Invalid action. Expected create_sampling or create_elicitation.");
                    }
                }
            };
        }

        public static McpTool CreateElicitationRequest()
        {
            return new McpTool
            {
                Name = "mcp_elicitation_request_create",
                Group = "server",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "elicitation_request_create", "client_elicitation_request" },
                Description = "旧名との互換用。mcp_client_request_control action=create_elicitation を推奨",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["message"] = new McpToolParameter { Type = "string", Description = "ユーザーに表示する質問または説明", Required = true },
                    ["fieldName"] = new McpToolParameter { Type = "string", Description = "構造化レスポンスのフィールド名。既定値は response", Required = false },
                    ["fieldDescription"] = new McpToolParameter { Type = "string", Description = "構造化レスポンスのフィールドの説明", Required = false },
                    ["fieldType"] = new McpToolParameter { Type = "string", Description = "フィールドの型。既定値は string", Required = false, EnumValues = new List<string> { "string", "boolean", "integer", "number" } },
                    ["required"] = new McpToolParameter { Type = "boolean", Description = "フィールドが必須か。既定値は true", Required = false },
                    ["schema"] = new McpToolParameter { Type = "string", Description = "任意の完全な JSON schema。指定時は field* 引数を上書き", Required = false }
                },
                Handler = args =>
                {
                    string message = args["message"]?.ToString();
                    if (string.IsNullOrWhiteSpace(message))
                        return CallToolResult.Error("Missing message");

                    JObject schema;
                    string schemaText = args["schema"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(schemaText))
                    {
                        try
                        {
                            schema = JObject.Parse(schemaText);
                        }
                        catch (Exception ex)
                        {
                            return CallToolResult.Error($"Invalid schema JSON: {ex.Message}");
                        }
                    }
                    else
                    {
                        string fieldName = string.IsNullOrWhiteSpace(args["fieldName"]?.ToString()) ? "response" : args["fieldName"].ToString();
                        string fieldType = string.IsNullOrWhiteSpace(args["fieldType"]?.ToString()) ? "string" : args["fieldType"].ToString();
                        bool required = args["required"] == null || string.Equals(args["required"].ToString(), "true", StringComparison.OrdinalIgnoreCase);
                        schema = new JObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject
                            {
                                [fieldName] = new JObject
                                {
                                    ["type"] = fieldType,
                                    ["description"] = string.IsNullOrWhiteSpace(args["fieldDescription"]?.ToString()) ? message : args["fieldDescription"].ToString()
                                }
                            }
                        };
                        if (required)
                            schema["required"] = new JArray(fieldName);
                    }

                    var @params = new JObject
                    {
                        ["message"] = message,
                        ["requestedSchema"] = schema
                    };
                    return CallToolResult.Text(JsonConvert.SerializeObject(ClientRequest("elicitation/create", @params), McpJsonUtil.Settings));
                }
            };
        }

        private static string ResolveLogPath(string fileName)
        {
            var candidates = new List<string>();
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (!string.IsNullOrEmpty(home))
            {
                candidates.Add(Path.Combine(home, ".config", "unity3d", "Klei", "Oxygen Not Included", fileName));
                candidates.Add(Path.Combine(home, "Library", "Application Support", "unity.Klei.Oxygen Not Included", fileName));
                candidates.Add(Path.Combine(home, "AppData", "LocalLow", "Klei", "Oxygen Not Included", fileName));
            }

            if (!string.IsNullOrEmpty(localAppData))
                candidates.Add(Path.Combine(localAppData, "Klei", "Oxygen Not Included", fileName));

            return candidates.FirstOrDefault(File.Exists);
        }

        private static JObject ClientRequest(string method, JObject @params)
        {
            return new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = Guid.NewGuid().ToString("N"),
                ["method"] = method,
                ["params"] = @params,
                ["note"] = "This is a client-side MCP request object. OniMcp records client capabilities but does not push requests because GET SSE is disabled."
            };
        }

        private static int ReadInt(JObject args, string key, int fallback, int min, int max)
        {
            int value;
            if (args[key] != null && int.TryParse(args[key].ToString(), out value))
                return Math.Max(min, Math.Min(value, max));
            return fallback;
        }

        private static string[] ReadAllLinesShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd()
                    .Replace("\r\n", "\n")
                    .Split(new[] { '\n' }, StringSplitOptions.None);
            }
        }
    }
}
