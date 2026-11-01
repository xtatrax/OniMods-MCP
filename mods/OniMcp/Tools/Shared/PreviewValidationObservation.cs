using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    internal sealed class PreviewValidationObservation
    {
        internal const string MetadataKey = "onimcp.validation";
        internal readonly string RequestedScope, ValidationScope, Coverage;
        internal readonly bool GameplayMutationRequested, UiInteractionRequested;
        private readonly string[] factPaths;

        internal PreviewValidationObservation(string requestedScope, string validationScope, string coverage,
            bool gameplayMutationRequested, bool uiInteractionRequested, params string[] reportedFactPaths)
        {
            RequestedScope = requestedScope;
            ValidationScope = validationScope;
            Coverage = coverage;
            GameplayMutationRequested = gameplayMutationRequested;
            UiInteractionRequested = uiInteractionRequested;
            factPaths = reportedFactPaths == null ? new string[0] : reportedFactPaths.ToArray();
        }

        private JObject Envelope(Func<string, JToken> fact)
        {
            var facts = new JObject();
            foreach (string path in factPaths)
            {
                JToken value = fact(path);
                // Scope metadata repeats only explicitly selected existing scalar
                // observations; absence never becomes a manufactured false value.
                if (value is JValue) facts[path] = value.DeepClone();
            }
            return new JObject
            {
                ["requestedScope"] = RequestedScope,
                ["validationScope"] = ValidationScope,
                ["coverage"] = Coverage,
                ["reportedFacts"] = facts,
                ["gameplayMutationRequested"] = GameplayMutationRequested,
                ["uiInteractionRequested"] = UiInteractionRequested
            };
        }

        internal CallToolResult Attach(CallToolResult source)
        {
            if (source == null) return null;
            var copy = new CallToolResult { IsError = source.IsError,
                Content = source.Content == null ? null : source.Content.Select(CloneContent).ToList() };
            if (copy.Content == null) return copy;
            foreach (ToolContent content in copy.Content)
            {
                if (!IsResultContent(content)) continue;
                JObject payload = null;
                try { payload = JObject.Parse(content.Text ?? string.Empty); } catch { }
                bool alreadyObserved = content.Meta != null && content.Meta.ContainsKey(MetadataKey)
                    || payload?["validation"] != null;
                if (alreadyObserved) break;
                var envelope = Envelope(path => JsonFact(payload, path));
                if (content.Meta == null) content.Meta = new Dictionary<string, object>();
                if (!content.Meta.ContainsKey(MetadataKey)) content.Meta[MetadataKey] = envelope.DeepClone();
                if (payload != null && payload["validation"] == null)
                {
                    payload["validation"] = envelope;
                    content.Text = payload.ToString(Formatting.None);
                }
                break;
            }
            return copy;
        }

        internal Dictionary<string, object> AttachPayload(Dictionary<string, object> source)
        {
            if (source == null) return null;
            var copy = new Dictionary<string, object>(source);
            if (!copy.ContainsKey("validation"))
                copy["validation"] = Envelope(path => DictionaryFact(source, path));
            return copy;
        }

        private static ToolContent CloneContent(ToolContent source)
        {
            return source == null ? null : new ToolContent
            {
                Type = source.Type, Text = source.Text,
                Meta = source.Meta == null ? null : new Dictionary<string, object>(source.Meta),
                IsOwnedPrintingPodPolicy = source.IsOwnedPrintingPodPolicy
            };
        }

        private static bool IsResultContent(ToolContent content)
        {
            if (content == null || content.Type != "text" || content.IsOwnedPrintingPodPolicy) return false;
            object kind;
            return content.Meta == null || !content.Meta.TryGetValue(ToolContent.ContentKindMetadataKey, out kind)
                || string.Equals(kind as string, ToolContent.ToolResultKind, StringComparison.Ordinal);
        }

        private static JToken JsonFact(JObject payload, string path)
        {
            JToken value = payload;
            foreach (string part in path.Split('.'))
            {
                var obj = value as JObject;
                if (obj == null || !obj.TryGetValue(part, out value)) return null;
            }
            return value;
        }

        private static JToken DictionaryFact(Dictionary<string, object> payload, string path)
        {
            object value = payload;
            foreach (string part in path.Split('.'))
            {
                var dictionary = value as IDictionary<string, object>;
                if (dictionary != null)
                {
                    if (!dictionary.TryGetValue(part, out value)) return null;
                    continue;
                }
                var json = value as JObject;
                if (json == null) return null;
                JToken token;
                if (!json.TryGetValue(part, out token)) return null;
                value = token;
            }
            if (value == null) return JValue.CreateNull();
            var scalar = value as JValue;
            if (scalar != null) return scalar;
            if (value is string || value is bool || value is byte || value is sbyte
                || value is short || value is ushort || value is int || value is uint
                || value is long || value is ulong || value is float || value is double || value is decimal)
                return new JValue(value);
            return null;
        }

        internal static CallToolResult Rejected(CallToolResult source, string requestedScope,
            bool gameplayMutationRequested = false, bool uiInteractionRequested = false)
        {
            return new PreviewValidationObservation(requestedScope, "request_rejected", "not_evaluated",
                gameplayMutationRequested, uiInteractionRequested).Attach(source);
        }
    }
}
