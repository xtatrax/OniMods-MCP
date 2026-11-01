using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private const string EditorChildCallsKey = "onimcp.worldEditorChildCalls";

        private static bool EditorGameplayRequested(JObject args)
        {
            return !ToolUtil.GetBool(args, "dryRun", false);
        }

        private static IEnumerable<ToolContent> EditorResultContents(CallToolResult result)
        {
            return (result?.Content ?? new List<ToolContent>()).Where(content =>
            {
                if (content == null || content.Type != "text" || content.IsOwnedPrintingPodPolicy) return false;
                object kind;
                return content.Meta == null || !content.Meta.TryGetValue(ToolContent.ContentKindMetadataKey, out kind)
                    || string.Equals(kind as string, ToolContent.ToolResultKind, StringComparison.Ordinal);
            });
        }

        private static JObject EditorValidation(CallToolResult result)
        {
            foreach (var content in EditorResultContents(result))
            {
                object value;
                if (content.Meta != null && content.Meta.TryGetValue(PreviewValidationObservation.MetadataKey, out value)
                    && value is JObject) return (JObject)((JObject)value).DeepClone();
                try
                {
                    var envelope = JObject.Parse(content.Text ?? string.Empty)["validation"] as JObject;
                    if (envelope != null) return (JObject)envelope.DeepClone();
                }
                catch { }
                break;
            }
            return null;
        }

        private static CallToolResult EditorStampChildCalls(CallToolResult ownedResult, int? calls)
        {
            var content = EditorResultContents(ownedResult).FirstOrDefault();
            if (content != null)
            {
                if (content.Meta == null) content.Meta = new Dictionary<string, object>();
                content.Meta[EditorChildCallsKey] = calls.HasValue ? (object)calls.Value : JValue.CreateNull();
            }
            return ownedResult;
        }

        private static int? EditorChildCalls(CallToolResult result)
        {
            var content = EditorResultContents(result).FirstOrDefault();
            object value;
            if (content?.Meta == null || !content.Meta.TryGetValue(EditorChildCallsKey, out value)) return null;
            if (value is int) return (int)value;
            var token = value as JValue;
            return token != null && token.Type == JTokenType.Integer ? (int?)token.Value<int>() : null;
        }

        private static int? EditorChildCalls(IEnumerable<CallToolResult> results)
        {
            int count = 0;
            foreach (var result in results)
            {
                int? observed = EditorChildCalls(result);
                if (!observed.HasValue) return null;
                count += observed.Value;
            }
            return count;
        }

        private static CallToolResult EditorObserveChild(CallToolResult result, string requestedScope,
            int childCalls, JObject effectiveArgs, bool passive = false)
        {
            var fallback = new PreviewValidationObservation(requestedScope,
                childCalls == 0 ? "child_not_invoked" : "requested_scope_only", "not_evaluated",
                !passive && EditorGameplayRequested(effectiveArgs), false,
                "valid", "availableNow", "assessmentState", "reasonCode", "details.assessmentState", "details.reasonCode");
            return EditorStampChildCalls(fallback.Attach(result), childCalls);
        }

        private static CallToolResult EditorObservePayload(JObject payload, string requestedScope,
            string observedScope, string coverage, int? childCalls, bool gameplayRequested,
            JObject authoritativeChild = null, bool error = false)
        {
            var copy = (JObject)payload.DeepClone();
            copy["childCalls"] = childCalls.HasValue ? new JValue(childCalls.Value) : JValue.CreateNull();
            bool copyChild = copy["validation"] == null && authoritativeChild != null;
            if (copyChild) copy["validation"] = authoritativeChild.DeepClone();
            var raw = error ? CallToolResult.Error(JsonResultText(copy)) : JsonResult(copy);
            if (copyChild)
            {
                var content = EditorResultContents(raw).FirstOrDefault();
                if (content != null)
                {
                    if (content.Meta == null) content.Meta = new Dictionary<string, object>();
                    content.Meta[PreviewValidationObservation.MetadataKey] = authoritativeChild.DeepClone();
                }
            }
            var observation = new PreviewValidationObservation(requestedScope, observedScope, coverage,
                gameplayRequested, false, "childCalls", "placementValidated", "executedCommands", "changedCells",
                "phase", "preview", "details.placementValidated", "details.changedCells", "layerContext.status");
            return EditorStampChildCalls(observation.Attach(raw), childCalls);
        }

        private static string EditorCompiledMapCoverage(List<MapEditCell> changes, JToken context)
        {
            // Compilation permits an unknown definition as an anchor-only shorthand; it has no observed shape.
            foreach (string token in changes.Where(change => ChangeKind(change) == "build")
                .Select(change => change.ToToken).Distinct(StringComparer.Ordinal))
            {
                if (!ParseBuildToken(token, out char symbol, out _, out _)
                    || !TryResolveBuildPrefabFromToken(token, symbol, out string prefabId)
                    || Assets.GetBuildingDef(prefabId) == null) return "partial";
            }
            return EditorMapCoverage(context);
        }

        private static string EditorMapCoverage(JToken context)
        {
            var value = context as JObject;
            if (value == null || (string)value["status"] != "observed") return "partial";
            var groups = value["groups"] as JArray;
            if (groups == null) return value["additionalRead"]?.Type == JTokenType.Boolean
                && (bool)value["additionalRead"] ? "complete" : "partial";
            if (groups.Count == 0 || (int?)value["omittedGroups"] > 0) return "partial";
            return groups.All(group => EditorMapCoverage(group["layerContext"]) == "complete") ? "complete" : "partial";
        }

        private static CallToolResult EditorSyntaxPreview(CallToolResult preview, string requestedScope,
            string observedScope, string coverage, JObject effectiveArgs, JObject authority = null, int? calls = 0,
            bool placementNotValidated = true)
        {
            var payload = ParseWorldEditorResult(preview);
            if (payload == null)
                return EditorStampChildCalls(new PreviewValidationObservation(requestedScope,
                    "requested_scope_only", "not_evaluated", EditorGameplayRequested(effectiveArgs), false).Attach(preview), calls);
            if (placementNotValidated) payload["placementValidated"] = false;
            return EditorObservePayload(payload, requestedScope, observedScope, coverage, calls,
                EditorGameplayRequested(effectiveArgs), authority);
        }
    }
}
