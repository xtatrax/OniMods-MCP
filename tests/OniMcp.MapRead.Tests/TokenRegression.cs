using System;
using System.Linq;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static void RunMapContractRegressions()
        {
            var definitions = Assets.BuildingDefs.ToArray();
            var symbols = UniqueCharMap.ToArray();
            int assertions = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition)
                    throw new InvalidOperationException("Map contract: " + message);
                assertions++;
            };

            try
            {
                Assets.BuildingDefs.Clear();
                Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "MeshTile", Name = "Mesh" });
                Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "Tile", Name = "Floor" });
                Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "OtherTile", Name = "Floor" });
                check(TryResolveBuildPrefabFromToken("Tile:5", 'T', out string prefab) && prefab == "Tile",
                    "an exact prefab must win over an earlier substring candidate");
                check(TryResolveBuildPrefabFromToken("tile:5", 't', out prefab) && prefab == "Tile",
                    "exact prefab IDs remain case-insensitive");
                check(!TryResolveBuildPrefabFromToken("Floor:5", 'F', out prefab),
                    "duplicate display names must not select the first building");
                check(!TryResolveBuildPrefabFromToken("TileTypo:5", 'T', out prefab),
                    "an unknown full name must not fall back to its first glyph");
                Assets.BuildingDefs.Reverse();
                check(TryResolveBuildPrefabFromToken("Tile:5", 'T', out prefab) && prefab == "Tile",
                    "resolution must not depend on asset enumeration order");

                Assets.BuildingDefs.Clear();
                Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "AliasForLadder", Name = "Ladder" });
                Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "Ladder", Name = "梯子" });
                Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "AdvancedResearchCenter", Name = "高性能研究端末" });
                Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "ResearchCenter", Name = "研究端末" });
                check(TryResolveBuildPrefabFromToken("Ladder:5", 'L', out prefab) && prefab == "Ladder",
                    "a native ID wins even when an earlier different prefab has that exact display name");
                check(TryResolveBuildPrefabFromToken("梯子:5", '梯', out prefab) && prefab == "Ladder",
                    "an exact Japanese display name resolves to its native prefab");
                check(TryResolveBuildPrefabFromToken("研究端末:5", '研', out prefab) && prefab == "ResearchCenter",
                    "an exact Japanese name wins over an earlier longer display name");
                check(!TryResolveBuildPrefabFromToken("研究:5", '研', out prefab) && prefab == null,
                    "an ambiguous Japanese partial name does not choose the first candidate");
                check(!TryResolveBuildPrefabFromToken("AdvancedResearchC:5", 'A', out prefab) && prefab == null,
                    "even a unique multi-character ID substring is not an exact build token");

                UniqueCharMap.Clear();
                UniqueCharMap.Add("Ladder", '梯');
                UniqueCharMap.Add("AliasForLadder", '偽');
                check(TryResolveBuildPrefabFromToken("梯:5", '梯', out prefab) && prefab == "Ladder",
                    "a unique one-character building glyph resolves to its native prefab");
                check(!TryResolveBuildPrefabFromToken("梯子Typo:5", '梯', out prefab) && prefab == null,
                    "an unknown full token cannot fall back to a valid first-character glyph");
                UniqueCharMap["AliasForLadder"] = '梯';
                check(!TryResolveBuildPrefabFromToken("梯:5", '梯', out prefab) && prefab == null,
                    "a glyph shared by different prefabs is rejected without a stale candidate");
                check(TryResolveBuildPrefabFromToken("梯子:5", '梯', out prefab) && prefab == "Ladder",
                    "an exact display name still resolves when its first glyph is ambiguous");
                check(!TryResolveBuildPrefabFromToken("Ω:5", 'Ω', out prefab) && prefab == null,
                    "an unregistered building glyph remains unresolved");
                Assets.BuildingDefs.Reverse();
                check(!TryResolveBuildPrefabFromToken("梯:5", '梯', out prefab) && prefab == null,
                    "ambiguous glyph rejection does not depend on definition order");
                Assets.BuildingDefs.Add(new TestBuildingDef { PrefabID = "DuplicateLadder", Name = "梯子" });
                check(!TryResolveBuildPrefabFromToken("梯子:5", '梯', out prefab) && prefab == null,
                    "duplicate Japanese display names are rejected without a stale candidate");
                check(TryResolveBuildPrefabFromToken("Ladder:5", 'L', out prefab) && prefab == "Ladder",
                    "a stable native ID still resolves when display names are duplicated");

                UniqueCharMap.Clear();
                UniqueCharMap.Add("TestElement", '材');
                check(ParseBuildToken("Tile:7#TestElement@(14,9)", out char symbol, out int? priority, out string material)
                    && symbol == 'T' && priority == 7 && material == "TestElement",
                    "a full material ID must survive an annotated token round trip");
                check(ParseBuildToken("Tile:7#材", out symbol, out priority, out material)
                    && material == "TestElement", "known material glyphs remain supported");
                check(ParseBuildToken("Tile:7@(14,-9)", out symbol, out priority, out material)
                    && priority == 7 && material == null, "valid signed coordinates are annotations only");

                foreach (string invalid in new[]
                {
                    "Tile:0", "Tile:10", "Tile:-1", "Tile:2147483648", "Tile:bad", "Tile:",
                    "Tile:5#", "Tile:5#TestElement,OtherElement", "dig:10", "dig:bad", "Tile:5#TestElement#Other", "Tile:5#TestElement:7",
                    "Tile:5@(1,2)junk", "Tile:5@(bad,2)", "Tile:5@(1,2)@(3,4)"
                })
                {
                    check(!ParseBuildToken(invalid, out symbol, out priority, out material),
                        "malformed token must be rejected: " + invalid);
                }
                check(MapMaterialSelectionError("Tile:5#TestElement,OtherElement").Contains("Multi-slot"), "multi-slot evidence has explicit unsupported error");
                check(!ParsePriority("dig:10").HasValue, "invalid priorities must not be silently clamped");
                check(NormalizeMapCompareToken("Tile@(1,2)junk") == "Tile@(1,2)junk",
                    "normalization must not discard an arbitrary suffix");
                check(SearchTokenMatches("Tile@(1,2)", "Tile"), "omitted coordinate annotations remain supported");
                check(!SearchTokenMatches("Tile", "*"), "the disconnected-line glyph is not a wildcard");
                check(!SearchTokenMatches("Tile", "?"), "unknown data is not a wildcard");
                check(SearchTokenMatches("*", "*") && SearchTokenMatches("?", "?"),
                    "map glyphs match literally");
                check(SearchTokenMatches("Tile", ".*"), "explicit wildcard syntax remains supported");
                Console.WriteLine("PASS: " + assertions + " map token contract assertions");
            }
            finally
            {
                Assets.BuildingDefs.Clear();
                Assets.BuildingDefs.AddRange(definitions);
                UniqueCharMap.Clear();
                foreach (var pair in symbols)
                    UniqueCharMap.Add(pair.Key, pair.Value);
            }
        }
    }
}
