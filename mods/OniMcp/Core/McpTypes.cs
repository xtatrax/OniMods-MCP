using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OniMcp.Core
{
    // ============================================================
    // MCP Protocol Types - Model Context Protocol の仕様に準拠
    // シリアライズには Newtonsoft.Json を使用する（ゲームに同梱されているため、追加の依存関係は不要）
    // ============================================================

    /// <summary>
    /// JSON-RPC 2.0 リクエスト
    /// </summary>
    public class JsonRpcRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public JObject Params { get; set; }

        public bool IsNotification => Id == null;
    }

    /// <summary>
    /// JSON-RPC 2.0 レスポンス
    /// </summary>
    public class JsonRpcResponse
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonProperty("id", NullValueHandling = NullValueHandling.Include)]
        public object Id { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Include)]
        public object Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public JsonRpcError Error { get; set; }

        public bool ShouldSerializeResult() => Error == null;

        public static JsonRpcResponse Success(object id, object result)
        {
            return new JsonRpcResponse { Id = id, Result = result };
        }

        public static JsonRpcResponse MakeError(object id, int code, string message, object data = null)
        {
            return new JsonRpcResponse
            {
                Id = id,
                Error = new JsonRpcError { Code = code, Message = message, Data = data }
            };
        }
    }

    public class JsonRpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }
    }

    /// <summary>
    /// MCP エラーコード
    /// </summary>
    public static class McpErrorCode
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
    }

    // ============================================================
    // MCP プロトコルのメッセージ型
    // ============================================================

    /// <summary>
    /// initialize リクエストのパラメーター
    /// </summary>
    public class InitializeParams
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; }

        [JsonProperty("capabilities")]
        public ClientCapabilities Capabilities { get; set; }

        [JsonProperty("clientInfo")]
        public Implementation ClientInfo { get; set; }
    }

    public class ClientCapabilities
    {
        [JsonProperty("roots")]
        public object Roots { get; set; }

        [JsonProperty("sampling")]
        public object Sampling { get; set; }

        [JsonProperty("elicitation")]
        public object Elicitation { get; set; }

        [JsonProperty("tasks")]
        public object Tasks { get; set; }

        [JsonProperty("experimental")]
        public JObject Experimental { get; set; }
    }

    public class Implementation
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }
    }

    /// <summary>
    /// initialize レスポンスの結果
    /// </summary>
    public class InitializeResult
    {
        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; } = "2025-11-25";

        [JsonProperty("capabilities")]
        public ServerCapabilities Capabilities { get; set; }

        [JsonProperty("serverInfo")]
        public Implementation ServerInfo { get; set; }
    }

    public class ServerCapabilities
    {
        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public ToolsCapability Tools { get; set; }

        [JsonProperty("prompts", NullValueHandling = NullValueHandling.Ignore)]
        public PromptsCapability Prompts { get; set; }

        [JsonProperty("resources", NullValueHandling = NullValueHandling.Ignore)]
        public ResourcesCapability Resources { get; set; }

        [JsonProperty("tasks", NullValueHandling = NullValueHandling.Ignore)]
        public TasksCapability Tasks { get; set; }

        [JsonProperty("experimental", NullValueHandling = NullValueHandling.Ignore)]
        public JObject Experimental { get; set; }
    }

    public class ToolsCapability
    {
        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; }
    }

    public class PromptsCapability
    {
        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; }
    }

    public class ResourcesCapability
    {
        [JsonProperty("subscribe")]
        public bool Subscribe { get; set; }

        [JsonProperty("listChanged")]
        public bool ListChanged { get; set; }
    }

    public class TasksCapability
    {
        [JsonProperty("list")]
        public object List { get; set; } = new JObject();

        [JsonProperty("cancel")]
        public object Cancel { get; set; } = new JObject();

        [JsonProperty("requests")]
        public JObject Requests { get; set; }
    }

    // ============================================================
    // Tool 関連の型
    // ============================================================

    /// <summary>
    /// Tool 一覧のレスポンス
    /// </summary>
    public class ListToolsResult
    {
        [JsonProperty("tools")]
        public List<McpToolInfo> Tools { get; set; }
    }

    public class McpToolInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("inputSchema")]
        public InputSchema InputSchema { get; set; }

        [JsonProperty("execution", NullValueHandling = NullValueHandling.Ignore)]
        public ToolExecution Execution { get; set; }
    }

    public class ToolExecution
    {
        [JsonProperty("taskSupport")]
        public string TaskSupport { get; set; }
    }

    public class InputSchema
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        [JsonProperty("properties")]
        public Dictionary<string, SchemaProperty> Properties { get; set; }

        [JsonProperty("required")]
        public List<string> Required { get; set; }
    }

    public class SchemaProperty
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("enum")]
        public List<object> Enum { get; set; }

        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, SchemaProperty> Properties { get; set; }

        [JsonProperty("x-mcp-header", NullValueHandling = NullValueHandling.Ignore)]
        public string McpHeader { get; set; }
    }

    /// <summary>
    /// Tool 呼び出しリクエスト
    /// </summary>
    public class CallToolParams
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public JObject Arguments { get; set; }

        [JsonProperty("task")]
        public TaskRequest Task { get; set; }
    }

    public class TaskRequest
    {
        [JsonProperty("ttl")]
        public int? Ttl { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("metadata")]
        public JObject Metadata { get; set; }
    }

    public class TaskIdParams
    {
        [JsonProperty("taskId")]
        public string TaskId { get; set; }
    }

    public class McpTaskInfo
    {
        [JsonProperty("taskId")]
        public string TaskId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("statusMessage", NullValueHandling = NullValueHandling.Ignore)]
        public string StatusMessage { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }

        [JsonProperty("lastUpdatedAt")]
        public string LastUpdatedAt { get; set; }

        [JsonProperty("ttl", NullValueHandling = NullValueHandling.Ignore)]
        public int? Ttl { get; set; }

        [JsonProperty("pollInterval", NullValueHandling = NullValueHandling.Ignore)]
        public int? PollInterval { get; set; }
    }

    public class CreateTaskResult
    {
        [JsonProperty("task")]
        public McpTaskInfo Task { get; set; }

        [JsonProperty("_meta", NullValueHandling = NullValueHandling.Ignore)]
        public JObject Meta { get; set; }
    }

    public class ListTasksResult
    {
        [JsonProperty("tasks")]
        public List<McpTaskInfo> Tasks { get; set; }
    }

    public class TaskResult
    {
        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }

        [JsonProperty("_meta")]
        public JObject Meta { get; set; }
    }
}
