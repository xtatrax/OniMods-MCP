using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class DirectoryListingRegression
{
    private static int checks;

    internal static void Run()
    {
        string originalCwd = (string)Body(Command("pwd"))["cwd"];
        string originalSnapshot = MapNativeBoundary.Snapshot;
        int originalReads = DirectoryListingBoundary.FileReads;
        try
        {
            DirectoryListingBoundary.Buildings.Clear();
            DirectoryListingBoundary.BuildingEnumerations = 0;
            DirectoryListingBoundary.FileReads = 0;
            AdvertisedDirectoriesRemainNavigable();
            ObjectsRoundTrip();
            BuildingInstancesRoundTrip();
            ExistingMapAndOperationListingsRemainExact();
            UnknownPathsAndFilesRemainRejected();
            LocalizationLookupsRemainCurrent();
            Check(DirectoryListingBoundary.FileReads == 0,
                "directory listing, navigation and directory browser responses never dispatch file reads");
            Console.WriteLine("Virtual directory listing: " + checks + " checks passed (complete production listing, browser, normalizer and command dispatcher; native data supplied)");
        }
        finally
        {
            Command("cd", originalCwd);
            MapNativeBoundary.Snapshot = originalSnapshot;
            DirectoryListingBoundary.Buildings.Clear();
            DirectoryListingBoundary.BuildingEnumerations = 0;
            DirectoryListingBoundary.FileReads = originalReads;
        }
    }

    private static void AdvertisedDirectoriesRemainNavigable()
    {
        var active = Entries(Command("ls", "/active/"));
        foreach (JObject entry in active.Where(item => (string)item["type"] == "dir"))
        {
            string path = (string)entry["path"];
            Check(!Command("cd", path).IsError, "every advertised active directory accepts cd: " + path);
            Check((string)Body(Command("pwd"))["cwd"] == path, "cd preserves the advertised canonical path: " + path);
        }
        Check(DirectoryListingBoundary.BuildingEnumerations == 0,
            "directory recognition never enumerates live buildings");
    }

    private static void ObjectsRoundTrip()
    {
        var active = Entries(Command("ls", "/active/"));
        JObject objects = Row(active, "objects/");
        Check((string)objects["type"] == "dir" && (string)objects["path"] == "/active/objects/",
            "objects retains its advertised directory type and path");
        AssertListing((string)objects["path"], new[] { "details.oni" });
        Check(!Command("cd", "/active").IsError, "the existing missing-trailing-slash normalization remains available");
        Check(!Command("cd", "objects").IsError, "relative cd accepts the advertised objects directory");
        Check((string)Body(Command("pwd"))["cwd"] == "/active/objects/", "relative objects cd updates cwd");
        var relative = Body(Command("ls"));
        Check((string)relative["path"] == "/active/objects/", "ls without a path uses the objects cwd");
        Check((string)Row((JArray)relative["entries"], "details.oni")["path"] == "/active/objects/details.oni",
            "objects listing returns the existing direct-read file path");
        AssertBrowserDirectory("/active/objects", "/active/", "details.oni", "/active/objects/details.oni");
    }

    private static void BuildingInstancesRoundTrip()
    {
        JObject instances = Row(Entries(Command("ls", "/active/buildings/")), "instances/");
        Check((string)instances["type"] == "dir" && (string)instances["path"] == "/active/buildings/instances/",
            "building instances retains its advertised directory type and path");
        AssertListing((string)instances["path"], new string[0]);
        AssertBrowserDirectory("/active/buildings/instances/", "/active/buildings/", null, null);
        Check(!Command("cd", "/active/buildings/").IsError, "the existing building parent accepts cd");
        Check(!Command("cd", "instances").IsError, "relative cd accepts the advertised building instances directory");
        Check((string)Body(Command("pwd"))["cwd"] == "/active/buildings/instances/", "relative instances cd updates cwd");
        DirectoryListingBoundary.Buildings.Add(new DirectoryListingBuilding("Fixture Pump", "building_41.md"));
        DirectoryListingBoundary.Buildings.Add(new DirectoryListingBuilding("Fixture Door", "building_42.md"));
        int before = DirectoryListingBoundary.BuildingEnumerations;
        AssertListing("/active/buildings/instances", new[] { "building_41.md", "building_42.md" });
        Check(DirectoryListingBoundary.BuildingEnumerations == before + 1,
            "one ls enumerates the supplied live-building boundary once");
        AssertBrowserDirectory("/active/buildings/instances/", "/active/buildings/", "building_41.md", "/active/buildings/instances/building_41.md");
        var relative = Entries(Command("ls"));
        Check(relative.Count == 2 && (string)Row(relative, "building_42.md")["path"] == "/active/buildings/instances/building_42.md",
            "ls from the instance cwd preserves per-building filenames and child paths");
        DirectoryListingBoundary.Buildings.Clear();
    }

    private static void ExistingMapAndOperationListingsRemainExact()
    {
        AssertListing("/active/map/", new[]
        {
            "index.html", "index.md", "viewport.md", "layers/"
        });
        AssertListing("/active/map/layers/", new string[0]);
        AssertListing("/active/ops/", new[]
        {
            "any.md", "game.md", "colony.md", "read.md", "search.md", "build.md", "orders.md", "dupes.md",
            "navigation.md", "coordinate.md", "server.md", "tools.md", "facilities.md", "storage.md", "power.md",
            "automation.md", "farming.md", "ranching.md", "rockets.md", "resources.md", "ui.md", "medical.md", "rooms.md", "sandbox.md"
        });
        JObject mapBrowser = JObject.FromObject(WorldEditorTools.BuildBrowserListing("/active/map/", "/mcp/", "fixture"));
        JObject browserOnly = Row((JArray)mapBrowser["entries"], "current-world-map/");
        Check(browserOnly["browserOnly"]?.Value<bool>() == true,
            "the existing browser-only map entry retains its marker");
        Check(Command("ls", (string)browserOnly["path"]).IsError && Command("cd", (string)browserOnly["path"]).IsError,
            "browser-only map links remain outside the MCP directory whitelist");
    }

    private static void UnknownPathsAndFilesRemainRejected()
    {
        foreach (string path in new[]
        {
            "/active/missing/", "/active/object/", "/active/objects/missing/", "/active/objects/details.oni/",
            "/active/objects/details.oni", "/active/objects-extra/", "/active/buildings/instance/",
            "/active/buildings/instances-extra/", "/active/buildings/instances/missing/",
            "/active/buildings/instances/building_41.md", "/active/buildings/instances/building_41.md/",
            "/active/map/missing/", "/active/ops/missing/", "/active/ops/game.md"
        })
        {
            string cwd = (string)Body(Command("pwd"))["cwd"];
            Check(Command("ls", path).IsError, "ls rejects absent paths and file paths: " + path);
            Check(Command("cd", path).IsError, "cd rejects absent paths and file paths: " + path);
            Check((string)Body(Command("pwd"))["cwd"] == cwd, "failed navigation preserves cwd: " + path);
        }
    }

    private static void LocalizationLookupsRemainCurrent()
    {
        var originalLanguage = McpLocalization.LanguageProvider;
        var originalGameLanguage = McpLocalization.GameLanguageProvider;
        try
        {
            foreach (string language in new[] { "en", "ja", "zh-Hans" })
            {
                string selected = language;
                McpLocalization.LanguageProvider = () => selected;
                McpLocalization.GameLanguageProvider = () => "en";
                var assembly = typeof(McpLocalization).Assembly;
                using (var stream = assembly.GetManifestResourceStream("OniMcp.Localization.Catalogs.food_freshness." + language + ".json"))
                {
                    Check(stream != null, "the existing food freshness runtime catalog is embedded: " + language);
                    using (var reader = new StreamReader(stream))
                    {
                        var expected = JObject.Parse(reader.ReadToEnd());
                        foreach (var request in new[]
                        {
                            new[] { "/active/", "objects/", "food_freshness.listing.objects" },
                            new[] { "/active/objects/", "details.oni", "food_freshness.listing.details" }
                        })
                        {
                            string description = (string)Row(Entries(Command("ls", request[0])), request[1])["description"];
                            Check(!string.IsNullOrWhiteSpace(description) && description != request[2],
                                "listing descriptions resolve through the current runtime lookup: " + language + "/" + request[2]);
                            Check(description == (string)expected[request[2]],
                                "listing descriptions exactly match the unchanged language catalog: " + language + "/" + request[2]);
                        }
                    }
                }
            }
            McpLocalization.LanguageProvider = () => "auto";
            McpLocalization.GameLanguageProvider = () => "ja-JP";
            Check((string)Row(Entries(Command("ls", "/active/objects/")), "details.oni")["description"]
                == McpLocalization.GetForLanguage("ja", "food_freshness.listing.details"),
                "automatic MCP language preserves the existing game-language normalization");
            McpLocalization.LanguageProvider = () => "fr";
            Check((string)Row(Entries(Command("ls", "/active/objects/")), "details.oni")["description"]
                == McpLocalization.GetForLanguage("en", "food_freshness.listing.details"),
                "an unsupported MCP language preserves the existing English fallback");
        }
        finally
        {
            McpLocalization.LanguageProvider = originalLanguage;
            McpLocalization.GameLanguageProvider = originalGameLanguage;
        }
    }

    private static void AssertListing(string path, string[] expectedNames)
    {
        var response = Command("ls", path);
        Check(!response.IsError, "listed directory is accepted by ls: " + path);
        JObject body = Body(response);
        string canonical = path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
        Check((string)body["path"] == canonical, "ls returns the canonical directory path: " + path);
        var rows = (JArray)body["entries"];
        Check(rows.Select(row => (string)row["name"]).SequenceEqual(expectedNames), "all existing listing entries and their order remain intact: " + path);
        foreach (JObject row in rows)
        {
            Check((string)row["path"] == canonical + (string)row["name"], "listing child paths preserve the advertised parent: " + row["name"]);
            bool directory = (string)row["type"] == "dir";
            string cwd = (string)Body(Command("pwd"))["cwd"];
            Check(Command("cd", (string)row["path"]).IsError != directory, "entry type matches cd behavior: " + row["path"]);
            Check(!Command("cd", cwd).IsError, "the prior valid cwd remains navigable");
        }
    }

    private static void AssertBrowserDirectory(string path, string parent, string fileName, string filePath)
    {
        var browser = JObject.FromObject(WorldEditorTools.BuildBrowserListing(path, "/mcp/", "fixture"));
        Check(browser["isFile"] == null && browser["content"] == null && browser["error"] == null,
            "the browser takes its production directory branch: " + path);
        var entries = (JArray)browser["entries"];
        Check(entries != null && (string)Row(entries, "..")["path"] == parent, "the browser retains the parent link: " + path);
        if (fileName == null)
            Check(entries.Count == 1, "an empty building directory remains browsable with only its parent link");
        else
            Check((string)Row(entries, fileName)["path"] == filePath, "the browser exposes the same child path as ls: " + path);
    }

    private static JArray Entries(CallToolResult result)
    {
        Check(!result.IsError, "the listing request succeeds before its entries are inspected");
        return (JArray)Body(result)["entries"];
    }

    private static JObject Row(JArray rows, string name)
    {
        JObject row = rows.OfType<JObject>().SingleOrDefault(item => (string)item["name"] == name);
        Check(row != null, "expected listing entry is present exactly once: " + name);
        return row;
    }

    private static CallToolResult Command(string command, string path = null)
    {
        var args = new JObject { ["command"] = command };
        if (path != null) args["path"] = path;
        return WorldEditorTools.ControlWorldEditor().Handler(args);
    }

    private static JObject Body(CallToolResult result) => JObject.Parse(result.Content[0].Text);

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
