using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

// Navigation/English overrides/registry/localizer execute actual product source.
// The eight shared-key schemas below are plain metadata fixtures, not their
// owner factories. Actual owner metadata coverage is checked by the source verifier.
internal static class PublicMetadataLocalizationRegression
{
    private static readonly string[] Languages = { "en", "ja", "zh-Hans" };
    private static readonly string[] Actions =
    {
        "get_view", "set_active_world", "set_view", "move", "switch_view", "focus_cell",
        "focus_dupe", "screenshot", "coordinate_screenshot", "screenshot_status"
    };
    private static readonly string[] NavigationParameters =
    {
        "domain", "action", "worldId", "requireDiscovered", "lookAtSurface", "x", "y", "x1", "y1", "x2", "y2",
        "areaId", "dx", "dy", "zoom", "snap", "mode", "duration", "view", "screenshot", "requestId", "filename",
        "waitFrames", "allowSound", "id", "name", "focusCamera", "paddingCells", "showGrid", "showCoordinates",
        "includeCellLabels", "step"
    };
    private static int checks;

    internal static void Run()
    {
        checks = 0;
        int nativeCalls = CameraTools.NativeBoundaryCalls;
        int handlers = GameStubs.HandlerCalls;
        var preferred = McpLocalization.LanguageProvider;
        var game = McpLocalization.GameLanguageProvider;
        try
        {
            McpLocalization.GameLanguageProvider = () => "ja";
            foreach (string language in Languages)
            {
                McpLocalization.LanguageProvider = () => language;
                NavigationFactory(language);
            }
            RegistryLanguages();
            GameEnglishOverrides();
            SharedMetadataLanguages();
            Check(CameraTools.NativeBoundaryCalls == nativeCalls && GameStubs.HandlerCalls == handlers,
                "Metadata checks invoked a native camera or game fixture handler.");
        }
        finally
        {
            McpLocalization.LanguageProvider = preferred;
            McpLocalization.GameLanguageProvider = game;
        }
        Console.WriteLine("Public metadata localization: " + checks + " checks passed (real navigation; shared eight schemas are fixtures).");
    }

    private static void NavigationFactory(string language)
    {
        var tool = NavigationControlTools.ControlNavigation();
        Check(tool.Name == "navigation_control" && tool.Group == "camera" && tool.Mode == "execute"
            && tool.Risk == "medium" && !tool.Hidden, "Navigation execution metadata changed.");
        Check(new HashSet<string>(tool.Parameters.Keys).SetEquals(NavigationParameters),
            "Public navigation gained or lost parameters.");
        Check(!tool.Parameters.ContainsKey("cameraMode") && !tool.Parameters.ContainsKey("confirm")
            && !tool.Parameters.ContainsKey("dryRun"), "Public navigation gained unsupported camera-mode fields.");
        Check(tool.Aliases.SequenceEqual(new[] { "spatial_control", "view_control" })
            && tool.Parameters["action"].Required && tool.Parameters["action"].Type == "string"
            && tool.Parameters["action"].EnumValues.SequenceEqual(Actions),
            "Public navigation changed its existing ten actions, aliases or requirement.");
        string expected = Catalog(language, "navigation_control", "action");
        Check(tool.Parameters["action"].Description == expected, "Actual navigation action is not canonical.");
        foreach (string unsupported in new[] { "camera_status", "set_camera_mode", "agent_follow", "auto_director" })
            Check(!expected.Contains(unsupported), "Public action description advertises unsupported camera features.");
        var handler = tool.Handler;
        JToken contract = Contract(tool);
        Check(ReferenceEquals(CoreToolEnglishDescriptions.Apply(tool), tool)
            && ReferenceEquals(handler, tool.Handler) && JToken.DeepEquals(contract, Contract(tool)),
            "English overrides changed the real navigation operation contract.");
        Check(tool.Parameters["action"].Description == Catalog("en", "navigation_control", "action"),
            "Navigation English metadata depends on its construction language.");
    }

    private static void RegistryLanguages()
    {
        var registered = OniToolRegistry.GetTools().Single(tool => tool.Name == "navigation_control");
        JToken before = JToken.FromObject(registered.Parameters);
        JToken contract = null;
        var texts = new HashSet<string>(StringComparer.Ordinal);
        foreach (string selected in new[] { "en", "ja", "zh-Hans", "auto", "en", "ja" })
        {
            McpLocalization.LanguageProvider = () => selected;
            McpLocalization.GameLanguageProvider = () => "zh_cn";
            string language = selected == "auto" ? "zh-Hans" : selected;
            foreach (bool all in new[] { false, true })
            {
                var metadata = OniToolRegistry.GetToolInfos(all).Single(tool => tool.Name == "navigation_control");
                string action = metadata.InputSchema.Properties["action"].Description;
                Check(action == Catalog(language, "navigation_control", "action"), "Cached navigation ignored the requested language.");
                texts.Add(action);
                foreach (string value in Actions) Check(action.Contains(value), "Navigation prose omitted existing action " + value + ".");
                Check(metadata.InputSchema.Properties["action"].Enum.Cast<string>().SequenceEqual(Actions)
                    && metadata.InputSchema.Required.SequenceEqual(new[] { "action", "task" }), "Published navigation actions/requirements changed.");
                Check(!metadata.InputSchema.Properties.ContainsKey("cameraMode")
                    && !metadata.InputSchema.Properties.ContainsKey("confirm") && !metadata.InputSchema.Properties.ContainsKey("dryRun"),
                    "Published navigation gained unsupported fields.");
                JToken current = StripDescriptions(JToken.FromObject(metadata));
                if (contract == null) contract = current;
                Check(JToken.DeepEquals(contract, current), "Language selection changed the published navigation contract.");
                metadata.InputSchema.Properties["action"].Description = "caller-only";
                metadata.InputSchema.Properties["action"].Enum.Clear();
                var reread = OniToolRegistry.GetToolInfos(all).Single(tool => tool.Name == "navigation_control");
                Check(reread.InputSchema.Properties["action"].Description == Catalog(language, "navigation_control", "action")
                    && reread.InputSchema.Properties["action"].Enum.Count == 10, "Caller mutated cached navigation metadata.");
            }
        }
        Check(texts.Count == 3, "Navigation metadata stayed on one language or an English fallback.");
        Check(JToken.DeepEquals(before, JToken.FromObject(registered.Parameters)), "Language requests changed the registered factory.");
    }

    private static void GameEnglishOverrides()
    {
        // The actual English override is executed on plain metadata. This does
        // not claim that the game-control owner factory was executed.
        var tool = new McpTool
        {
            Name = "game_control",
            Parameters = new Dictionary<string, McpToolParameter>
            {
                ["query"] = new McpToolParameter { Type = "string", Description = "fixture query" },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "fixture limit" }
            }
        };
        JToken before = Contract(tool);
        CoreToolEnglishDescriptions.Apply(tool);
        string query = Catalog("en", "game_control", "query"), limit = Catalog("en", "game_control", "limit");
        Check(tool.Parameters["query"].Description == query && tool.Parameters["limit"].Description == limit,
            "English game query/limit overrides lost their action-specific canonical descriptions.");
        Check(JToken.DeepEquals(before, Contract(tool)), "Game English overrides changed the metadata fixture contract.");
    }

    private static void SharedMetadataLanguages()
    {
        var fixtures = new List<McpToolInfo>
        {
            Fixture("building_control", new Dictionary<string, SchemaProperty>
            { ["storageScope"] = Property("string", "primary", "all_components") }),
            Fixture("orders_control", new Dictionary<string, SchemaProperty>
            { ["direction"] = Property("string") }),
            Fixture("server_control", new Dictionary<string, SchemaProperty>
            { ["includePatches"] = Property("boolean") }),
            Fixture("world_editor", new Dictionary<string, SchemaProperty>
            {
                ["includeAchievementUi"] = Property("boolean"),
                ["achievementDetail"] = Property("string", "brief", "full"),
                ["achievementStatus"] = Property("string", "all", "completed", "incomplete", "failed", "unknown"),
                ["specDetail"] = Property("string", "brief", "full"),
                ["foodDetail"] = Property("string", "summary", "types", "full")
            })
        };
        JToken before = JToken.FromObject(fixtures);
        var texts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (string language in Languages)
        {
            var localized = McpToolInfoLocalization.Localize(fixtures, language);
            Check(JToken.DeepEquals(StripDescriptions(before.DeepClone()), StripDescriptions(JToken.FromObject(localized))),
                "Shared-key localization changed fixture types, enums, requirements or protocol fields.");
            foreach (var tool in localized)
            foreach (var parameter in tool.InputSchema.Properties.Where(item => item.Key != "task"))
            {
                string key = "tools." + tool.Name + ".parameters." + parameter.Key;
                string expected = Catalog(language, tool.Name, parameter.Key);
                Check(parameter.Value.Description == expected, "Shared canonical description did not follow " + language + ": " + key);
                HashSet<string> variants;
                if (!texts.TryGetValue(key, out variants)) texts.Add(key, variants = new HashSet<string>(StringComparer.Ordinal));
                variants.Add(parameter.Value.Description);
            }
            localized[0].InputSchema.Properties["storageScope"].Enum.Clear();
            localized[0].InputSchema.Properties["storageScope"].Description = "caller-only";
            var reread = McpToolInfoLocalization.Localize(fixtures, language);
            Check(reread[0].InputSchema.Properties["storageScope"].Enum.Count == 2
                && reread[0].InputSchema.Properties["storageScope"].Description == Catalog(language, "building_control", "storageScope"),
                "A shared metadata fixture snapshot poisoned later requests.");
        }
        Check(texts.Count == 8 && texts.All(item => item.Value.Count == 3), "Shared canonical descriptions fell back to a fixed language.");
        Check(JToken.DeepEquals(before, JToken.FromObject(fixtures)), "Localization mutated shared input metadata fixtures.");
    }

    private static McpToolInfo Fixture(string name, Dictionary<string, SchemaProperty> properties)
    {
        properties.Add("task", Property("string"));
        return new McpToolInfo { Name = name, Description = "metadata fixture", InputSchema = new InputSchema
            { Type = "object", Required = new List<string> { "task" }, Properties = properties } };
    }

    private static SchemaProperty Property(string type, params string[] values)
    {
        return new SchemaProperty { Type = type, Description = "metadata fixture",
            Enum = values.Length == 0 ? null : values.Cast<object>().ToList() };
    }

    private static string Catalog(string language, string tool, string parameter)
    {
        string key = "tools." + tool + ".parameters." + parameter;
        string declared = null;
        // Check actual language resources, rather than accepting an English fallback.
        var assembly = typeof(McpLocalization).Assembly;
        foreach (string name in assembly.GetManifestResourceNames().Where(name => name.StartsWith("OniMcp.Localization.Catalogs.", StringComparison.Ordinal)
            && name.EndsWith("." + language + ".json", StringComparison.Ordinal)))
        using (var stream = assembly.GetManifestResourceStream(name))
        using (var reader = new StreamReader(stream))
        {
            var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
            string value;
            if (entries.TryGetValue(key, out value)) { Check(declared == null, "Duplicate declared catalog key: " + key); declared = value; }
        }
        Check(!string.IsNullOrWhiteSpace(declared), "Missing declared " + language + " canonical description: " + key);
        Check(McpLocalization.GetForLanguage(language, key) == declared, "Runtime lookup diverged from the declared language catalog: " + key);
        return declared;
    }

    private static JToken Contract(McpTool tool)
    {
        return StripDescriptions(JToken.FromObject(new { tool.Name, tool.Description, tool.Group, tool.Mode,
            tool.Risk, tool.Hidden, tool.Aliases, tool.Tags, tool.Parameters }));
    }

    private static JToken StripDescriptions(JToken value)
    {
        var obj = value as JObject;
        if (obj != null)
        {
            obj.Remove("description"); obj.Remove("Description");
            foreach (JProperty item in obj.Properties()) StripDescriptions(item.Value);
        }
        else if (value is JArray array) foreach (JToken item in array) StripDescriptions(item);
        return value;
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Public metadata localization: " + message);
    }
}
