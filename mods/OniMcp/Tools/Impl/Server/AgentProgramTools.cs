using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class AgentProgramTools
    {
        private const string ToolName = "agent_program_execute";
        private const int DefaultMaxSteps = 100;
        private const int HardMaxSteps = 200;
        private const int DefaultMaxLoopIterations = 10;
        private const int HardMaxLoopIterations = 25;
        private const int MaxChildToolCalls = 50;
        private const int MaxStoredResultChars = 4000;

        public static McpTool ExecuteProgram()
        {
            return new McpTool
            {
                Name = ToolName,
                Group = "tools",
                Mode = "execute",
                Risk = "medium",
                Aliases = new List<string> { "agent_script_run", "agent_flow_execute", "agent_program_run" },
                Tags = new List<string> { "agent", "program", "script", "flow", "if", "while", "loop", "control", "脚本", "流程" },
                Description = "制限付き agent フロー DSL を実行する。変数、if/while/repeat、break/continue/return に対応し、条件に応じて登録済み MCP ツールを呼び出せる",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["program"] = new McpToolParameter
                    {
                        Type = "object",
                        Description = "プログラムオブジェクトまたは JSON 文字列。steps には call、if、while、repeat、break、continue、return を使用可能。call は tool と args を使って登録済み MCP ツールを呼び出す。",
                        Required = true
                    },
                    ["dryRun"] = new McpToolParameter
                    {
                        Type = "boolean",
                        Description = "構造とツール名の検証のみを行い、子呼び出しは実行しない。既定値は false",
                        Required = false
                    },
                    ["maxSteps"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "無限ループを防ぐための最大実行文数。既定値は 100、上限は 200",
                        Required = false
                    },
                    ["maxLoopIterations"] = new McpToolParameter
                    {
                        Type = "integer",
                        Description = "個々の while/repeat の最大反復回数。既定値は 10、上限は 25",
                        Required = false
                    },
                    ["trace"] = new McpToolParameter
                    {
                        Type = "boolean",
                        Description = "詳細な実行 trace を返すか。既定値は false。trace 内の子呼び出し結果は切り詰められる",
                        Required = false
                    }
                },
                Handler = args =>
                {
                    JToken program;
                    string error;
                    if (!TryReadProgram(args["program"], out program, out error))
                        return CallToolResult.Error(error);

                    bool dryRun = ToolUtil.GetBool(args, "dryRun", false);
                    int maxSteps = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "maxSteps") ?? DefaultMaxSteps, HardMaxSteps));
                    int maxLoopIterations = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "maxLoopIterations") ?? DefaultMaxLoopIterations, HardMaxLoopIterations));
                    bool includeTrace = ToolUtil.GetBool(args, "trace", false);

                    var runner = new AgentProgramRunner(dryRun, maxSteps, maxLoopIterations, includeTrace);
                    var payload = runner.Run(program);
                    string text = JsonConvert.SerializeObject(payload, McpJsonUtil.Settings);
                    bool ok = payload.ContainsKey("ok") && (bool)payload["ok"];
                    return ok ? CallToolResult.Text(text) : CallToolResult.Error(text);
                }
            };
        }

        private static bool TryReadProgram(JToken token, out JToken program, out string error)
        {
            program = null;
            error = null;
            if (token == null || token.Type == JTokenType.Null)
            {
                error = "program is required";
                return false;
            }

            if (token.Type == JTokenType.String)
            {
                try
                {
                    program = JToken.Parse(token.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    error = "program string must be valid JSON: " + ex.Message;
                    return false;
                }
            }

            if (token.Type != JTokenType.Object && token.Type != JTokenType.Array)
            {
                error = "program must be an object, array, or JSON string";
                return false;
            }

            program = token.DeepClone();
            return true;
        }
        private enum FlowSignal
        {
            None,
            Break,
            Continue,
            Return
        }

        private sealed class AgentProgramException : Exception
        {
            public AgentProgramException(string message) : base(message)
            {
            }
        }
    }
}
