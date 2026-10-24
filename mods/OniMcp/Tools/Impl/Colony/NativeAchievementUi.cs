using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace OniMcp.Tools
{
    // Observe an existing native cache only; opening the summary is a separate explicit operation.
    internal static class NativeAchievementUi
    {
        private const int MaxStrings = 8, MaxStringLength = 1024, MaxTotalLength = 8192;

        private static FieldInfo Bind(Type owner, string name, Type valueType)
        {
            var field = owner.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null && field.DeclaringType == owner && field.FieldType == valueType
                && !field.IsStatic ? field : null;
        }

        internal static Dictionary<string, object> Read(IEnumerable<string> ids)
        {
            var result = new Dictionary<string, object> {
                ["status"] = "unavailable", ["reasonCode"] = null,
                ["source"] = "existing_retired_colony_info_screen_cache",
                ["currentColonyVerified"] = null, ["screenOpen"] = null, ["items"] = null
            };
            try
            {
                var screen = RetiredColonyInfoScreen.Instance;
                if (screen == null || screen.GetType() != typeof(RetiredColonyInfoScreen))
                    return Unavailable(result, "screen_missing");
                bool open = screen.isActiveAndEnabled && screen.gameObject.activeInHierarchy;
                result["screenOpen"] = open;
                if (!open) return Unavailable(result, "screen_closed");
                var manager = KScreenManager.Instance;
                var stackField = Bind(typeof(KScreenManager), "screenStack", typeof(List<KScreen>));
                var entriesField = Bind(typeof(RetiredColonyInfoScreen), "achievementEntries", typeof(Dictionary<string, GameObject>));
                var containerField = Bind(typeof(RetiredColonyInfoScreen), "achievementsContainer", typeof(GameObject));
                if (stackField == null || entriesField == null || containerField == null)
                    return Unavailable(result, "binding_unavailable");
                var stack = manager == null ? null : stackField.GetValue(manager) as List<KScreen>;
                if (stack == null || stack.Count > 64) return Unavailable(result, "screen_stack_unavailable");
                int occurrences = 0, activeSummaries = 0;
                foreach (var entry in stack) {
                    if (ReferenceEquals(entry, screen)) occurrences++;
                    if (entry != null && entry.GetType() == typeof(RetiredColonyInfoScreen)
                        && entry.isActiveAndEnabled && entry.gameObject.activeInHierarchy) activeSummaries++;
                }
                if (occurrences != 1 || activeSummaries != 1) return Unavailable(result, "screen_stack_mismatch");
                var container = containerField.GetValue(screen) as GameObject;
                if (container == null || !container.activeInHierarchy
                    || !container.transform.IsChildOf(screen.transform))
                    return Unavailable(result, "container_unavailable");
                var entries = entriesField.GetValue(screen) as Dictionary<string, GameObject>;
                if (entries == null) return Unavailable(result, "entries_unavailable");
                var rows = new List<Dictionary<string, object>>();
                int remaining = MaxTotalLength;
                foreach (string id in ids) rows.Add(ReadRow(entries, container, id, ref remaining));
                result["status"] = "observed";
                result["items"] = rows;
                return result;
            }
            catch { return Unavailable(result, "observation_unavailable"); }
        }

        private static Dictionary<string, object> Unavailable(Dictionary<string, object> result, string reason)
        {
            result["reasonCode"] = reason;
            return result;
        }

        private static T ExactComponent<T>(GameObject go) where T : Component
        {
            var components = go.GetComponents<T>();
            return components.Length == 1 && components[0] != null && components[0].GetType() == typeof(T)
                && components[0].gameObject == go ? components[0] : null;
        }

        private static Dictionary<string, object> ReadRow(Dictionary<string, GameObject> entries,
            GameObject container, string id, ref int remaining)
        {
            var row = new Dictionary<string, object> {
                ["id"] = id, ["status"] = "unavailable", ["reasonCode"] = null,
                ["toggleState"] = null, ["tooltipStrings"] = null,
                ["tooltipStringCount"] = null, ["tooltipTruncated"] = null
            };
            try
            {
                GameObject go;
                if (id == null || !entries.TryGetValue(id, out go)) {
                    row["status"] = "not_present";
                    return row;
                }
                if (go == null) return Unavailable(row, "widget_missing");
                if (!go.activeInHierarchy) return Unavailable(row, "widget_hidden");
                if (go == container || !go.transform.IsChildOf(container.transform))
                    return Unavailable(row, "widget_parent_mismatch");
                if (ExactComponent<AchievementWidget>(go) == null)
                    return Unavailable(row, "widget_component_mismatch");
                var toggle = ExactComponent<MultiToggle>(go);
                var tooltip = ExactComponent<ToolTip>(go);
                if (toggle != null) row["toggleState"] = toggle.CurrentState;
                if (tooltip != null) {
                    int count = tooltip.multiStringCount;
                    if (count < 0) return Unavailable(row, "tooltip_unavailable");
                    var strings = new List<string>();
                    bool truncated = false;
                    for (int i = 0; i < Math.Min(count, MaxStrings) && remaining > 0; i++) {
                        string text = tooltip.GetMultiString(i);
                        if (text != null) {
                            int length = Math.Min(text.Length, Math.Min(MaxStringLength, remaining));
                            if (length > 0 && length < text.Length && char.IsHighSurrogate(text[length - 1])
                                && char.IsLowSurrogate(text[length])) length--;
                            if (length < text.Length) truncated = true;
                            text = text.Substring(0, length);
                            remaining -= length;
                        }
                        strings.Add(text);
                    }
                    row["tooltipStringCount"] = count;
                    row["tooltipStrings"] = strings;
                    row["tooltipTruncated"] = truncated || strings.Count < count;
                }
                row["status"] = toggle != null && tooltip != null ? "observed" : "partial";
                if (toggle == null || tooltip == null) row["reasonCode"] = "widget_fields_unavailable";
                return row;
            }
            catch { return Unavailable(row, "widget_observation_unavailable"); }
        }
    }
}
