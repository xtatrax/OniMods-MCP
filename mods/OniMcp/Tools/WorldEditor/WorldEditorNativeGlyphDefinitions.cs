using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static List<JObject> ReadNativeGlyphDefinitions(List<JObject> generatedRows, string input, out JObject evidence)
        {
            var rows = new List<JObject>();
            var reasons = new List<string>();
            var seen = new HashSet<string>(generatedRows.Select(row => GlyphText(row, "id")), StringComparer.OrdinalIgnoreCase);
            bool acquired = false;
            try
            {
                var definitions = Assets.BuildingDefs;
                if (definitions == null) reasons.Add("definitions_missing");
                else
                {
                    acquired = true;
                    foreach (var definition in definitions)
                    {
                        if (definition == null || string.IsNullOrWhiteSpace(definition.PrefabID))
                        {
                            AddNativeGlyphReason(reasons, "invalid_definition");
                            continue;
                        }
                        string id = definition.PrefabID;
                        if (seen.Contains(id) || HasRegisteredNativeGlyph(id)) continue;
                        string name = null;
                        try { name = StripLinkTags(definition.Name); }
                        catch { AddNativeGlyphReason(reasons, "name_read_failed"); }
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = null;
                            AddNativeGlyphReason(reasons, "name_missing");
                        }
                        if (id.IndexOf(input, StringComparison.OrdinalIgnoreCase) < 0
                            && (name == null || name.IndexOf(input, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                        rows.Add(new JObject
                        {
                            ["kind"] = "Building", ["id"] = id, ["name"] = name == null ? JValue.CreateNull() : new JValue(name),
                            ["symbol"] = JValue.CreateNull(), ["glyphAvailable"] = false,
                            ["source"] = "native_building_definition", ["reasonCode"] = "not_in_generated_catalog",
                            ["guidance"] = McpLocalization.Format("glyph_lookup.native_definition_guidance", null, id)
                        });
                    }
                }
            }
            catch { AddNativeGlyphReason(reasons, "read_failed"); }

            // Native metadata is query scoped; it never registers or invents a glyph.
            rows = rows.OrderBy(row => GlyphText(row, "id"), StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => GlyphText(row, "id"), StringComparer.Ordinal)
                .ThenBy(row => GlyphText(row, "name"), StringComparer.Ordinal)
                .Where(row => seen.Add(GlyphText(row, "id"))).ToList();
            evidence = new JObject
            {
                ["source"] = "Assets.BuildingDefs", ["readState"] = !acquired ? "unavailable" : reasons.Count == 0 ? "observed" : "partial",
                ["reasonCodes"] = new JArray(reasons)
            };
            return rows;
        }

        private static void AddNativeGlyphReason(List<string> reasons, string reason)
        {
            if (!reasons.Contains(reason)) reasons.Add(reason);
        }

        private static bool HasRegisteredNativeGlyph(string id)
        {
            if (FindGlyphEntry(id) != null) return true;
            string cleanId = StripLinkTags(id).Trim();
            return ResolvedGlyphById.ContainsKey(cleanId)
                || ResolvedGlyphById.ContainsKey(StripCompleteSuffix(cleanId));
        }
    }
}
