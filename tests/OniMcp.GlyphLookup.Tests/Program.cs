using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++; if (!condition) throw new Exception(message);
    }
    private static BuildingDef Definition(string id, string name)
    {
        return new BuildingDef { PrefabID = id, NativeName = name };
    }
    private static JObject Search(string query, string direction = "auto", string mode = "auto", string view = null, int limit = 20)
    {
        var args = new JObject { ["query"] = query, ["direction"] = direction, ["matchMode"] = mode, ["perQueryLimit"] = limit };
        if (view != null) args["view"] = view;
        var result = WorldEditorTools.SearchGlyphs(args);
        Check(!result.IsError, "actual glyph search succeeds");
        return JObject.Parse(result.Value);
    }
    private static JObject Query(JObject result) { return (JObject)result["results"][0]; }
    private static JArray Matches(JObject result) { return (JArray)Query(result)["matches"]; }
    private static IEnumerable<JToken> Native(JObject result)
    {
        return Matches(result).Where(row => (string)row["source"] == "native_building_definition");
    }
    private static string State(JObject result) { return (string)Query(result)["nativeDefinitionLookup"]?["readState"]; }
    private static bool Reason(JObject result, string reason)
    {
        return Query(result)["nativeDefinitionLookup"]["reasonCodes"].Values<string>().Contains(reason);
    }
    private static void CheckNative(JToken row, string id, string name)
    {
        Check((string)row["id"] == id && (string)row["name"] == name, "native definition preserves actual ID and current-game name");
        Check(row["symbol"] != null && row["symbol"].Type == JTokenType.Null, "unmapped native definition has explicit null symbol");
        Check(!(bool)row["glyphAvailable"], "missing mapping is not advertised as a glyph");
        Check((string)row["reasonCode"] == "not_in_generated_catalog", "missing mapping has stable reason code");
        Check((string)row["source"] == "native_building_definition" && (string)row["kind"] == "Building", "native row provenance and kind");
        Check(row["availableNow"] == null && row["buildable"] == null && row["placementValidated"] == null, "identity metadata does not invent building availability");
        Check(((string)row["guidance"]).Contains(id + ":5"), "guidance gives the complete native-ID build token");
    }

    private static void Main()
    {
        McpLocalization.LanguageProvider = () => "en";
        Assets.BuildingDefs = null;
        var baselineLadder = Search("Ladder", "meaning_to_code", "exact");
        Check((int)baselineLadder["count"] == 1 && (string)Matches(baselineLadder)[0]["id"] == "Ladder", "existing generated native-ID search remains available without native definitions");
        var baselineEmpty = Search("");
        var baselineCode = Search("?", "code_to_meaning", "exact");
        var baselineKind = Search("Building");
        var missing = Search("LogicPressureSensorGas", "meaning_to_code", "exact");
        Check((int)missing["count"] == 0 && State(missing) == "unavailable" && Reason(missing, "definitions_missing"), "zero matches with missing native data is explicit unavailable");
        Check(Query(baselineEmpty)["nativeDefinitionLookup"] == null && Query(baselineCode)["nativeDefinitionLookup"] == null, "empty and code-only paths do not attempt native fallback");

        var sensor = Definition("LogicPressureSensorGas", "<link=\"sensor\">気圧センサー</link>");
        var localLadder = Definition("Ladder", "Current-game name cannot replace authoritative generated row");
        var partial = Definition("NativeLadderExtension", "Native ladder extension");
        var alias = Definition("LadderComplete", "Complete suffix already has a generated glyph");
        var nativeBuilding = Definition("NativeBuildingOnly", "Native component");
        Assets.BuildingDefs = new List<BuildingDef> { sensor, localLadder, partial, alias, nativeBuilding };
        int cachedCount = WorldEditorTools.CachedGlyphCount();
        var result = Search("LogicPressureSensorGas", "meaning_to_code", "exact");
        Check((int)result["count"] == 1 && (bool)Query(result)["exact"] && State(result) == "observed", "exact native ID fills a missing generated entry");
        CheckNative(Matches(result)[0], "LogicPressureSensorGas", "気圧センサー");
        Check((int)Query(result)["count"] == (int)result["count"] && JToken.DeepEquals(result["symbols"], Matches(result)), "legacy single-query projection retains full native row/count");
        Check(WorldEditorTools.CachedGlyphCount() == cachedCount && Db.Reads == 0, "native lookup never registers a glyph or initializes the database");
        Check(localLadder.NameReads == 0 && alias.NameReads == 0, "already mapped definitions and Complete aliases bypass native name reads");
        Check(!Native(Search("LadderComplete", "meaning_to_code", "exact")).Any(), "generated Complete alias is never falsely labeled glyphAvailable=false");
        WorldEditorTools.SeedCachedGlyph("NativeCachedRoom", '室');
        Assets.BuildingDefs.Add(Definition("NativeCachedRoomComplete", "Cached room alias"));
        Check(!Native(Search("NativeCachedRoomComplete", "meaning_to_code", "exact")).Any(), "already cached runtime Complete alias is not falsely called unmapped");

        var japanese = Search("気圧センサー", "meaning_to_code", "exact", "default");
        Check((int)japanese["count"] == 1 && (string)Matches(japanese)[0]["id"] == "LogicPressureSensorGas", "native current-game exact name is discoverable");
        var caseInsensitive = Search("logicpressuresensorgas", "auto", "exact");
        Check((int)caseInsensitive["count"] == 1 && (string)Matches(caseInsensitive)[0]["id"] == "LogicPressureSensorGas", "native IDs preserve existing case-insensitive lookup semantics");
        var contains = Search("PressureSensor", "meaning_to_code", "contains");
        Check(Native(contains).Count() == 1 && !(bool)Query(contains)["exact"], "native partial discovery is explicit contains search");
        Check((int)Search("PressureSensor", "meaning_to_code", "exact")["count"] == 0, "exact search never promotes a native partial match");
        var exactGenerated = Search("Ladder");
        Check(JToken.DeepEquals(Matches(exactGenerated), Matches(baselineLadder)), "global auto exact preference keeps generated exact ahead of native partial");
        Check((int)Search("Ladder", "meaning_to_code", "contains")["count"] > (int)exactGenerated["count"], "contains mode retains additional matching native metadata");
        Check(JToken.DeepEquals(Matches(Search("Building")), Matches(baselineKind)), "native kind metadata cannot create a new exact search match");
        Check(!Native(Search("native_building_definition", "meaning_to_code", "contains")).Any()
            && !Native(Search("not_in_generated_catalog", "meaning_to_code", "contains")).Any(), "native provenance and reason codes are not searchable names");

        foreach (string view in new[] { "oxygen", "temperature", "rooms", "power", "gas_conduits", "materials", "sound" })
        {
            int reads = sensor.NameReads;
            var scoped = Search("LogicPressureSensorGas", "meaning_to_code", "exact", view);
            Check((int)scoped["count"] == 0 && Query(scoped)["nativeDefinitionLookup"] == null, "nondefault view does not gain native building metadata");
            Check(sensor.NameReads == reads, "nondefault view does not read native definition names");
        }
        int beforeCode = sensor.NameReads;
        var code = Search("LogicPressureSensorGas", "code_to_meaning", "exact");
        Check((int)code["count"] == 0 && Query(code)["nativeDefinitionLookup"] == null && sensor.NameReads == beforeCode, "code-to-meaning lookup never treats a native ID as a glyph");
        var empty = Search("");
        Check(JToken.DeepEquals(empty, baselineEmpty), "empty/default glyph catalog output is unchanged by native definitions");
        Check(JToken.DeepEquals(Search("?", "code_to_meaning", "exact"), baselineCode), "existing unknown-symbol lookup never absorbs native metadata");

        Assets.BuildingDefs = new List<BuildingDef>
        {
            Definition("NativeSensorC", "Sensor C"), Definition("NativeSensorA", "Sensor A"), Definition("NativeSensorB", "Sensor B"),
            Definition("nativesensora", "Case duplicate")
        };
        var limited = Search("NativeSensor", "meaning_to_code", "contains", null, 1);
        Check((int)limited["count"] == 3 && Matches(limited).Count == 1 && (string)Matches(limited)[0]["id"] == "NativeSensorA", "native count precedes truncation and IDs are deduplicated/sorted");
        Assets.BuildingDefs.Reverse();
        Check(JToken.DeepEquals(Search("NativeSensor", "meaning_to_code", "contains", null, 1), limited), "native ordering/dedup does not depend on asset enumeration order");
        var batchResult = WorldEditorTools.SearchGlyphs(new JObject
        {
            ["queries"] = new JArray("NativeSensor", "Ladder", "?"), ["direction"] = "auto", ["perQueryLimit"] = 1
        });
        Check(!batchResult.IsError, "actual batch query handler accepts typed queries");
        var batch = JObject.Parse(batchResult.Value);
        Check(batch["results"].Count() == 3 && (int)batch["results"][0]["count"] == 3 && batch["results"][0]["matches"].Count() == 1, "batch retains independent native total and per-query limit");
        Check((string)batch["results"][1]["matches"][0]["id"] == "Ladder" && batch["results"][2]["nativeDefinitionLookup"] == null, "mixed batch retains generated and code-only semantics");
        Check(batch["symbols"] == null && batch["query"] == null && batch["count"] == null, "batch does not grow legacy single-query fields");

        Assets.BuildingDefs = new List<BuildingDef>();
        var observedEmpty = Search("NativeSensorA", "meaning_to_code", "exact");
        Check((int)observedEmpty["count"] == 0 && State(observedEmpty) == "observed", "successfully acquired empty native collection differs from missing collection");
        var broken = Definition("NativeBroken", "Broken name"); broken.ThrowName = true;
        Assets.BuildingDefs = new List<BuildingDef> { broken, null, Definition("", "Invalid ID") };
        var partialNative = Search("NativeBroken", "meaning_to_code", "exact");
        Check((int)partialNative["count"] == 1 && State(partialNative) == "partial", "failed name keeps observed native ID and marks incomplete evidence");
        Check(Matches(partialNative)[0]["name"].Type == JTokenType.Null && Reason(partialNative, "name_read_failed") && Reason(partialNative, "invalid_definition"), "missing native name is not fabricated and errors remain explicit");
        var generatedDespiteError = Search("Ladder", "meaning_to_code", "exact");
        Check((int)generatedDespiteError["count"] == 1 && (string)Matches(generatedDespiteError)[0]["id"] == "Ladder" && State(generatedDespiteError) == "partial", "native definition failures never hide an existing generated match");

        Assets.BuildingDefs = new List<BuildingDef> { Definition("LogicPressureSensorGas", "気圧センサー") };
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            string selected = language; McpLocalization.LanguageProvider = () => selected;
            var localized = Search("LogicPressureSensorGas", "meaning_to_code", "exact");
            Check((string)localized["source"] == McpLocalization.GetForLanguage(language, "glyph_lookup.source"), "actual result source uses selected runtime locale");
            Check((string)localized["resultContract"]["rule"] == McpLocalization.GetForLanguage(language, "glyph_lookup.result_rule"), "actual result rule distinguishes identity metadata in selected locale");
            Check((string)Matches(localized)[0]["guidance"] == McpLocalization.GetForLanguage(language, "glyph_lookup.native_definition_guidance").Replace("{0}", "LogicPressureSensorGas"), "actual native row guidance uses selected locale and full ID");
            Check((string)Matches(localized)[0]["name"] == "気圧センサー", "MCP prose language does not translate native game names");
        }
        Check(Db.Reads == 0, "all native definition lookups preserve existing database readiness");
        McpLocalization.LanguageProvider = null;
        Console.WriteLine("Glyph lookup native metadata: " + assertions + " assertions passed");
    }
}
