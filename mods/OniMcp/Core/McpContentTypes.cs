using System.Collections.Generic;
using Newtonsoft.Json;

namespace OniMcp.Core
{
// ============================================================
    // Prompt 関連の型
    // ============================================================

    public class ListPromptsResult
    {
        [JsonProperty("prompts")]
        public List<McpPromptInfo> Prompts { get; set; }
    }

    public class McpPromptInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("arguments", NullValueHandling = NullValueHandling.Ignore)]
        public List<McpPromptArgument> Arguments { get; set; }
    }

    public class McpPromptArgument
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("required")]
        public bool Required { get; set; }
    }

    public class GetPromptParams
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public Dictionary<string, string> Arguments { get; set; }
    }

    public class GetPromptResult
    {
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("messages")]
        public List<PromptMessage> Messages { get; set; }
    }

    public class PromptMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public ToolContent Content { get; set; }
    }

    // ============================================================
    // Resource 関連の型
    // ============================================================

    public class ListResourcesResult
    {
        [JsonProperty("resources")]
        public List<McpResourceInfo> Resources { get; set; }
    }

    public class ListResourceTemplatesResult
    {
        [JsonProperty("resourceTemplates")]
        public List<McpResourceTemplateInfo> ResourceTemplates { get; set; }
    }

    public class McpResourceInfo
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("mimeType", NullValueHandling = NullValueHandling.Ignore)]
        public string MimeType { get; set; }
    }

    public class McpResourceTemplateInfo
    {
        [JsonProperty("uriTemplate")]
        public string UriTemplate { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string Title { get; set; }

        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        [JsonProperty("mimeType", NullValueHandling = NullValueHandling.Ignore)]
        public string MimeType { get; set; }
    }

    public class ReadResourceParams
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }

    public class ReadResourceResult
    {
        [JsonProperty("contents")]
        public List<TextResourceContent> Contents { get; set; }
    }

    public class TextResourceContent
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("mimeType")]
        public string MimeType { get; set; } = "application/json";

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    /// <summary>
    /// Tool の呼び出し結果
    /// </summary>
    public class CallToolResult
    {
        [JsonProperty("content")]
        public List<ToolContent> Content { get; set; }

        [JsonProperty("isError")]
        public bool IsError { get; set; }

        public static CallToolResult Text(string text)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { ToolContent.CreateText(text) }
            };
        }

        public static CallToolResult Error(string message)
        {
            return new CallToolResult
            {
                Content = new List<ToolContent> { ToolContent.CreateText(message) },
                IsError = true
            };
        }
    }

    public class ToolContent
    {
        public const string ContentKindMetadataKey = "onimcp.contentKind";
        public const string ToolResultKind = "tool_result";
        public const string PrintingPodPolicyKind = "printing_pod_policy";
        public const string NotificationKind = "notification";
        public const string LegacyAliasWarningKind = "legacy_alias_warning";

        [JsonProperty("type")]
        public string Type { get; set; } = "text";

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("_meta", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, object> Meta { get; set; }

        [JsonIgnore]
        internal bool IsOwnedPrintingPodPolicy { get; set; }

        public static ToolContent CreateText(string text, string contentKind = ToolResultKind,
            Dictionary<string, object> metadata = null)
        {
            var meta = metadata == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(metadata);
            meta[ContentKindMetadataKey] = contentKind ?? ToolResultKind;
            return new ToolContent { Text = text, Meta = meta };
        }
    }
}
