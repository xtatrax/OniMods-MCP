using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;

// Shapes confirmed from installed Assembly-CSharp metadata without initializing it.
public sealed class BuildingDef
{
    public string PrefabID;
    public string NativeName;
    public bool ThrowName;
    public int NameReads;
    public string Name
    {
        get { NameReads++; if (ThrowName) throw new InvalidOperationException(); return NativeName; }
    }
}
public static class Assets { public static List<BuildingDef> BuildingDefs = new List<BuildingDef>(); }
public sealed class RoomType { public string Id; public string Name; }
public sealed class RoomTypes { public List<RoomType> resources = new List<RoomType>(); }
public sealed class Db
{
    public static int Reads;
    public RoomTypes RoomTypes = new RoomTypes();
    public static Db Get() { Reads++; throw new InvalidOperationException("must not initialize or read room database in these fixtures"); }
}
namespace OniMcp.Core
{
    public sealed class CallToolResult
    {
        public string Value;
        public bool IsError;
        public static CallToolResult Text(string value) { return new CallToolResult { Value = value }; }
        public static CallToolResult Error(string value) { return new CallToolResult { Value = value, IsError = true }; }
    }
}
namespace OniMcp.Tools
{
    public static class ToolUtil
    {
        public static int? GetInt(JObject args, string key) { return (int?)args[key]; }
    }
    public static partial class WorldEditorTools
    {
        internal static int CachedGlyphCount() { return ResolvedGlyphById.Count; }
        internal static void SeedCachedGlyph(string id, char glyph) { ResolvedGlyphById[id] = glyph; }
        // Unchanged text/connection adapter boundaries used by the existing symbol engine.
        private static string SaveRelativePath(string path) { return path; }
        private static bool IsConnectionGlyph(char value) { return "*─│┌┐└┘┬┴├┤┼←→↑↓●".IndexOf(value) >= 0; }
        private static string StripLinkTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var result = new StringBuilder(); bool inTag = false;
            foreach (char value in text)
            {
                if (value == '<') { inTag = true; continue; }
                if (value == '>') { inTag = false; continue; }
                if (!inTag) result.Append(value);
            }
            return result.ToString().Trim();
        }
        private static string MapTokenPart(string text)
        {
            text = StripLinkTags(text);
            if (string.IsNullOrWhiteSpace(text)) return "?";
            var result = new StringBuilder();
            foreach (char value in text)
            {
                if (char.IsWhiteSpace(value)) continue;
                result.Append(value == '@' || value == ':' || value == '#' ? '_' : value);
            }
            return result.Length == 0 ? "?" : result.ToString();
        }
    }
}
