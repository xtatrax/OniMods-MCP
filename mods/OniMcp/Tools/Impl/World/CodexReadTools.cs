using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    // Only inspect the cache populated by ONI. Rebuilding it is a game/UI operation.
    internal static class CodexReadTools
    {
        private static readonly Regex DisplayTags = new Regex("<[^>]*>", RegexOptions.Compiled);
        private static string DisplayText(string value) => value == null ? null : DisplayTags.Replace(value, "");

        internal static CallToolResult Read(JObject args)
        {
            if (Game.Instance?.unlocks == null)
                return CallToolResult.Error("Load a colony before reading the Codex; no initialization was attempted.");
            if (CodexCache.entries == null || CodexCache.entries.Count == 0)
                return CallToolResult.Error("Codex cache is not ready. Open the game's Database once, then retry; this read does not initialize it.");
            args = args ?? new JObject();
            string action = (args["action"]?.ToString() ?? "query").Trim().ToLowerInvariant();
            if (action != "query" && action != "list" && action != "search" && action != "categories" && action != "page")
                return CallToolResult.Error("database action must be query, list, search, categories, or page");
            string id = args["id"]?.ToString()?.Trim();
            string query = (args["query"] ?? args["target"] ?? args["search"])?.ToString()?.Trim();
            string category = args["category"]?.ToString()?.Trim();
            int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 10, 50));
            int maxChars = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "maxChars") ?? 6000, 20000));
            bool includeContent = ToolUtil.GetBool(args, "includeContent", action == "query");
            var rows = new List<Item>();
            foreach (var pair in CodexCache.entries)
            {
                var entry = pair.Value;
                if (entry == null || entry.disabled || !Game.IsCorrectDlcActiveForCurrentSave(entry)) continue;
                rows.Add(new Item(pair.Key, entry));
            }
            if (CodexCache.subEntries != null)
                foreach (var pair in CodexCache.subEntries)
                {
                    var sub = pair.Value;
                    if (sub == null || sub.disabled || !Game.IsCorrectDlcActiveForCurrentSave(sub)) continue;
                    CodexEntry parent;
                    CodexCache.entries.TryGetValue(sub.parentEntryID ?? "", out parent);
                    if (parent != null && (parent.disabled || !Game.IsCorrectDlcActiveForCurrentSave(parent))) continue;
                    rows.Add(new Item(pair.Key, sub, parent?.category));
                }
            var categories = rows.GroupBy(row => row.Category ?? "").OrderBy(group => group.Key)
                .Select(group => new Dictionary<string, object> { ["id"] = group.Key, ["count"] = group.Count() }).ToList();
            var matches = rows.Where(row => string.IsNullOrWhiteSpace(category) || Equal(row.Category, category))
                .Where(row => string.IsNullOrWhiteSpace(id) || Equal(row.Id, id) || Equal(row.Key, id))
                .Where(row => string.IsNullOrWhiteSpace(query) || row.Matches(query))
                .OrderByDescending(row => Equal(row.Id, query) || Equal(row.Key, query))
                .ThenByDescending(row => Equal(DisplayText(row.Name), query) || Equal(DisplayText(row.Title), query))
                .ThenBy(row => row.Category, StringComparer.Ordinal).ThenBy(row => row.Id, StringComparer.Ordinal)
                .ToList();
            if (action == "page")
            {
                if (string.IsNullOrWhiteSpace(id) || matches.Count != 1)
                    return CallToolResult.Error("database page requires an unambiguous exact id/cacheKey");
                // Small fragments survive nested operation-file envelopes. Join fragments then parse JSON.
                var selected = matches[0].Serialize(true, 2000000);
                string document = JsonConvert.SerializeObject(selected, Formatting.None, McpJsonUtil.Settings);
                int offset = Math.Max(0, ToolUtil.GetInt(args, "offset") ?? 0);
                int pageSize = Math.Max(1, Math.Min(2000, ToolUtil.GetInt(args, "pageSize") ?? 1000));
                if (offset > document.Length) return CallToolResult.Error("database page offset exceeds document length");
                string revision;
                using (var hash = SHA256.Create()) revision = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(document))).Replace("-", "");
                string expectedRevision = args["revision"]?.ToString();
                if (expectedRevision != null && expectedRevision != revision)
                    return CallToolResult.Error("Codex content changed; restart paging at offset 0");
                int end = Math.Min(document.Length, offset + pageSize);
                // Never split a surrogate pair across transport messages.
                if (end < document.Length && end > offset && char.IsHighSurrogate(document[end - 1]) && char.IsLowSurrogate(document[end])) end++;
                return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                {
                    ["source"] = "CodexCache.entries/subEntries", ["action"] = "page", ["encoding"] = "json_fragments_utf16_offsets",
                    ["id"] = matches[0].Id, ["revision"] = revision, ["offset"] = offset,
                    ["totalChars"] = document.Length, ["nextOffset"] = end < document.Length ? (object)end : null,
                    ["text"] = document.Substring(offset, end - offset)
                }, Formatting.None, McpJsonUtil.Settings));
            }
            int listOffset = Math.Max(0, ToolUtil.GetInt(args, "offset") ?? 0);
            var results = action == "categories" ? new List<Dictionary<string, object>>()
                : matches.Skip(listOffset).Take(limit).Select(row => row.Serialize(includeContent, maxChars)).ToList();
            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["source"] = "CodexCache.entries/subEntries",
                ["scope"] = "loaded_codex_cache",
                ["action"] = action,
                ["totalEntries"] = CodexCache.entries.Count,
                ["totalSubEntries"] = CodexCache.subEntries?.Count ?? 0,
                ["returned"] = results.Count,
                ["matched"] = matches.Count,
                ["truncated"] = action == "categories" ? 0 : Math.Max(0, matches.Count - listOffset - results.Count),
                ["offset"] = listOffset,
                ["categories"] = categories,
                ["results"] = results,
                ["note"] = "Metadata search only. Native game language/rich text is preserved. Text and allowlisted panel fields are read without configuring UI; unsupported widgets and unexecuted formatters are reported. Use action=page to retrieve long content without transport truncation. This is not a live equipment or resource reading."
            }, McpJsonUtil.Settings));
        }

        private static bool Equal(string a, string b) => !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private static bool Contains(string a, string b) => a != null && a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string TitleText(string title)
        {
            return title != null && title.StartsWith("STRINGS.", StringComparison.Ordinal)
                ? (string)Strings.Get(title) : title;
        }

        private sealed class Item
        {
            internal readonly string Key;
            private readonly CodexEntry entry;
            private readonly SubEntry sub;
            internal string Id => entry != null ? entry.id : sub.id;
            internal string Category => entry != null ? entry.category : parentCategory;
            private readonly string parentCategory;
            internal string Name => entry != null ? entry.name : sub.name;
            internal string Title => TitleText(entry != null ? entry.title : sub.title);
            private string Subtitle => entry != null ? entry.subtitle : sub.subtitle;
            internal Item(string key, CodexEntry value) { Key = key; entry = value; }
            internal Item(string key, SubEntry value, string category) { Key = key; sub = value; parentCategory = category; }
            internal bool Matches(string query) => Contains(Id, query) || Contains(Key, query) || Contains(Name, query)
                || Contains(Title, query) || Contains(Subtitle, query) || Contains(Category, query);
            internal Dictionary<string, object> Serialize(bool content, int maxChars)
            {
                string lockId = sub?.lockID;
                bool locked = !string.IsNullOrEmpty(lockId) && !Game.Instance.unlocks.IsUnlocked(lockId);
                var row = new Dictionary<string, object>
                {
                    ["kind"] = entry != null ? "entry" : "subEntry",
                    ["id"] = Id, ["cacheKey"] = Key,
                    ["parentId"] = entry != null ? entry.parentId : sub.parentEntryID,
                    ["category"] = Category, ["name"] = Name, ["displayName"] = DisplayText(Name), ["title"] = Title, ["subtitle"] = Subtitle,
                    ["locked"] = locked, ["lockId"] = lockId
                };
                if (content) row["content"] = locked
                    ? Unavailable("locked") : ReadContent(entry != null ? entry.contentContainers : sub.contentContainers, maxChars);
                return row;
            }
        }

        private static Dictionary<string, object> Unavailable(string reason) => new Dictionary<string, object>
        { ["available"] = false, ["complete"] = false, ["reason"] = reason, ["blocks"] = null };

        private static Dictionary<string, object> ReadContent(List<ContentContainer> containers, int maxChars)
        {
            if (containers == null) return Unavailable("content_missing");
            var reader = new ContentReader(maxChars);
            foreach (var container in containers) reader.Container(container, 0);
            return reader.Result(maxChars);
        }

        private sealed class ContentReader
        {
            private readonly List<Dictionary<string, object>> blocks = new List<Dictionary<string, object>>();
            private readonly Dictionary<string, int> unsupported = new Dictionary<string, int>();
            private readonly Dictionary<string, int> layouts = new Dictionary<string, int>();
            private readonly HashSet<ContentContainer> active = new HashSet<ContentContainer>();
            private readonly HashSet<ContentContainer> visited = new HashSet<ContentContainer>();
            private int remaining, locked, filtered, missing, examined, panelIssues;
            private bool truncated;
            internal ContentReader(int budget) { remaining = budget; }
            private static void Count(Dictionary<string, int> counts, string key)
            {
                int count; counts.TryGetValue(key, out count); counts[key] = count + 1;
            }
            internal void Container(ContentContainer container, int depth)
            {
                if (truncated || container == null) return;
                if (depth > 32 || active.Contains(container)) { missing++; return; }
                // ONI also places collapsible contents in the flat container list.
                if (!visited.Add(container)) return;
                if (!Game.IsCorrectDlcActiveForCurrentSave(container)) { filtered++; return; }
                if (!string.IsNullOrEmpty(container.lockID) && !Game.Instance.unlocks.IsUnlocked(container.lockID)) { locked++; return; }
                if (container.content == null) { missing++; return; }
                active.Add(container);
                foreach (var widget in container.content)
                {
                    if (widget == null) continue;
                    if (++examined > 10000) { truncated = true; break; }
                    string type = widget.GetType().FullName;
                    if (CodexPanelReader.IsKnown(widget, "CodexSpacer") || CodexPanelReader.IsKnown(widget, "CodexLargeSpacer")
                        || CodexPanelReader.IsKnown(widget, "CodexDividerLine")) { Count(layouts, type); continue; }
                    string text = null, tooltip = null;
                    ContentContainer nested = null;
                    bool header = CodexPanelReader.IsKnown(widget, "CodexCollapsibleHeader");
                    if (widget is CodexText plain) text = plain.text;
                    else if (widget is CodexTextWithTooltip tip) { text = tip.text; tooltip = tip.tooltip; }
                    else if (widget is CodexLabelWithIcon icon) text = icon.label?.text;
                    else if (widget is CodexIndentedLabelWithIcon indent) text = indent.label?.text;
                    else if (header)
                    {
                        object label, child;
                        if (!CodexPanelReader.TryField(widget, "label", out label)
                            || !CodexPanelReader.TryField(widget, "contents", out child)) { Count(unsupported, type); continue; }
                        text = label as string;
                        nested = child as ContentContainer;
                    }
                    else
                    {
                        var panel = CodexPanelReader.Read(widget);
                        if (panel == null) { Count(unsupported, type); continue; }
                        if (((List<string>)panel["issues"]).Count > 0) panelIssues++;
                        string json = JsonConvert.SerializeObject(panel, Formatting.None, McpJsonUtil.Settings);
                        if (json.Length > remaining)
                        {
                            blocks.Add(new Dictionary<string, object> { ["type"] = type, ["dataJsonFragment"] = Bounded(json, ref remaining, ref truncated) });
                            break;
                        }
                        remaining -= json.Length;
                        blocks.Add(panel);
                        continue;
                    }
                    var block = new Dictionary<string, object> { ["type"] = widget.GetType().Name };
                    block["text"] = Bounded(text, ref remaining, ref truncated);
                    if (tooltip != null) block["tooltip"] = Bounded(tooltip, ref remaining, ref truncated);
                    blocks.Add(block);
                    if (remaining == 0) { truncated = true; break; }
                    if (nested != null) Container(nested, depth + 1);
                    if (truncated) break;
                }
                active.Remove(container);
            }
            internal Dictionary<string, object> Result(int maxChars)
            {
                bool readable = !truncated && missing == 0 && unsupported.Count == 0 && panelIssues == 0;
                return new Dictionary<string, object>
                {
                    ["available"] = true, ["complete"] = readable && locked == 0,
                    ["unlockedContentComplete"] = readable,
                    ["format"] = "game_rich_text_and_raw_panel_fields", ["blocks"] = blocks,
                    ["unsupportedWidgets"] = unsupported, ["layoutWidgetsSkipped"] = layouts,
                    ["panelsWithIssues"] = panelIssues, ["lockedContainers"] = locked,
                    ["lockPolicy"] = "game_locks_respected_not_read_errors",
                    ["missingContainers"] = missing, ["dlcFilteredContainers"] = filtered,
                    ["truncated"] = truncated, ["maxChars"] = maxChars
                };
            }
        }
        private static string Bounded(string value, ref int remaining, ref bool truncated)
        {
            if (value == null) return null;
            if (value.Length <= remaining) { remaining -= value.Length; return value; }
            string result = value.Substring(0, remaining); remaining = 0; truncated = true; return result;
        }
    }
}
