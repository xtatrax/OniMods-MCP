using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Net;
using OniMcp.Support;
using UnityEngine;

internal static class LegacyHtmlBoundary
{
    internal static bool Poison;
    internal static int NativeValueReads, ColorReads;
    internal static readonly List<string> NativeReadKinds = new();
    internal static readonly List<(int Cell, Element Element, GameObject Object, HashedString Mode, float Temperature, string Building)> Colors = new();
    internal static readonly List<(string Title, string BackUrl, string BackText, string Description, int Width, string Cells, string Legend)> Pages = new();
    internal static void ReadNativeValue(string kind)
    {
        NativeValueReads++; NativeReadKinds.Add(kind);
        if (Poison) throw new InvalidOperationException("forbidden_native_html_value_read_" + kind);
    }
    internal static void Clear()
    {
        NativeValueReads = ColorReads = 0; NativeReadKinds.Clear(); Colors.Clear(); Pages.Clear();
    }
}

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        // Only rendering dependencies outside the linked legacy reader are modeled.
        private static string ReadScreenshotsIndexMarkdown() => "screenshot_index_boundary";
        private static bool TryParseCellSnapshotPath(string path, out int x, out int y) { x = y = 0; return false; }
        private static string ReadCellSnapshotMarkdown(JObject args, int x, int y) => "cell_snapshot_boundary";
        private static string GetMapHtml(string title, string backUrl, string backText, string description,
            int width, string cellsHtml, string legendHtml)
        {
            LegacyHtmlBoundary.Pages.Add((title, backUrl, backText, description, width, cellsHtml, legendHtml));
            return cellsHtml;
        }
        private static string GetHtmlCellColor(int cell, Element element, GameObject go, HashedString mode, float temp, string building)
        {
            LegacyHtmlBoundary.ColorReads++;
            if (LegacyHtmlBoundary.Poison) throw new InvalidOperationException("forbidden_native_html_color_read");
            LegacyHtmlBoundary.Colors.Add((cell, element, go, mode, temp, building));
            return "#abcdef";
        }

        internal static void RunLegacyHtmlRegressions()
        {
            int assertions = 0;
            void Check(bool value, string label) { assertions++; if (!value) throw new InvalidOperationException(label); }
            const int x = 12, y = 22;
            int cell = Grid.XYToCell(x, y);
            string[] paths = { "/active/map/viewport.html", "/active/map/index.html", "/active/map/layers/layer_0_0.html" };
            HashedString[] modes = { OverlayModes.None.ID, OverlayModes.Temperature.ID, OverlayModes.Power.ID,
                OverlayModes.Oxygen.ID, OverlayModes.LiquidConduits.ID, OverlayModes.GasConduits.ID, OverlayModes.Light.ID };
            var savedCluster = ClusterManager.Instance; var savedCamera = Camera.main; var savedOverlay = OverlayScreen.Instance;
            var savedElement = Grid.Element; var savedTemperature = Grid.Temperature; var savedObjects = Grid.Objects;
            var savedVisible = Grid.Visible; var savedWorldIdx = Grid.WorldIdx; var savedWorldValid = Grid.WorldValid;
            int savedInvalid = Grid.LegacyInvalidCell; var savedLanguage = McpLocalization.LanguageProvider;
            var savedGameLanguage = McpLocalization.GameLanguageProvider; var savedCulture = CultureInfo.CurrentCulture;
            var savedUICulture = CultureInfo.CurrentUICulture;
            void Reset(HashedString mode)
            {
                LegacyHtmlBoundary.Poison = false;
                ClusterManager.Instance = new ClusterManager { activeWorldId = 0, world = new World { WorldOffset = new(x, y), WorldSize = new(1, 1) } };
                Camera.main = new Camera { orthographicSize = 0, aspect = 1 };
                Camera.main.transform.position = new(x, y, 0);
                OverlayScreen.Instance = new OverlayScreen { mode = mode };
                Grid.Element = new Element[16384]; Grid.Temperature = new float[16384];
                Grid.Objects = new GameObject[16384, Enum.GetValues<ObjectLayer>().Length];
                Grid.Visible = Enumerable.Repeat(true, 16384).ToArray(); Grid.WorldIdx = new int[16384];
                Grid.WorldValid = Enumerable.Repeat(true, 16384).ToArray(); Grid.LegacyInvalidCell = -1;
                Grid.Element[cell] = new Element { id = SimHashes.Oxygen, name = "native_element_name", IsGas = true };
                Grid.Temperature[cell] = 295.525f;
                McpLocalization.LanguageProvider = () => "en"; McpLocalization.GameLanguageProvider = () => "ja";
                LegacyHtmlBoundary.Clear();
            }
            string UnknownCell() => "<div class=\"cell\" data-state=\"unknown\" style=\"background:#171d29;\" title=\""
                + WebUtility.HtmlEncode(McpLocalization.Get("map_html.unreadable")) + "\"></div>";
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                foreach (string path in paths) foreach (HashedString mode in modes)
                foreach (string kind in new[] { "hidden", "foreign", "invalid", "invalid_world", "no_active_world" })
                {
                    Reset(mode);
                    switch (kind)
                    {
                        case "hidden": Grid.Visible[cell] = false; break;
                        case "foreign": Grid.WorldIdx[cell] = 1; break;
                        case "invalid": Grid.LegacyInvalidCell = cell; break;
                        case "invalid_world": Grid.WorldValid[cell] = false; break;
                        case "no_active_world": ClusterManager.Instance.activeWorldId = -1; break;
                    }
                    LegacyHtmlBoundary.Poison = true;
                    string html = ReadFileDirectly(path);
                    Check(html == UnknownCell(), "unreadable HTML is a single fixed unknown cell: " + path + "/" + mode + "/" + kind);
                    Check(LegacyHtmlBoundary.NativeValueReads == 0 && LegacyHtmlBoundary.ColorReads == 0,
                        "unreadable cell never reads native element/temperature/objects/color: " + kind);
                    Check(LegacyHtmlBoundary.Pages.Count == 1 && LegacyHtmlBoundary.Pages[0].Width == 1,
                        "unreadable output retains the requested grid layout");
                }
                foreach (string path in paths.Take(2))
                {
                    Reset(OverlayModes.None.ID); ClusterManager.Instance = null; LegacyHtmlBoundary.Poison = true;
                    Check(ReadFileDirectly(path) == UnknownCell() && LegacyHtmlBoundary.NativeValueReads == 0 && LegacyHtmlBoundary.ColorReads == 0,
                        "missing cluster/current-world context cannot read native values");
                }
                Reset(OverlayModes.None.ID); ClusterManager.Instance.world = null; LegacyHtmlBoundary.Poison = true;
                Check(ReadFileDirectly(paths[2]) == "<h1>File not found</h1>" && LegacyHtmlBoundary.NativeValueReads == 0
                    && LegacyHtmlBoundary.ColorReads == 0 && LegacyHtmlBoundary.Pages.Count == 0,
                    "missing layer world retains its existing route response without native reads");
                foreach (string language in new[] { "en", "ja-JP", "zh-cn" }) foreach (string path in paths)
                {
                    Reset(OverlayModes.None.ID); Grid.Visible[cell] = false; McpLocalization.LanguageProvider = () => language;
                    LegacyHtmlBoundary.Poison = true;
                    string html = ReadFileDirectly(path);
                    Check(html == UnknownCell() && !html.Contains("map_html.unreadable"), "runtime unknown tooltip resolves and encodes " + language);
                    Check(McpLocalization.Get("map_html.unreadable") == McpLocalization.GetForLanguage(language, "map_html.unreadable"),
                        "independent MCP prose language is retained " + language);
                    Check(LegacyHtmlBoundary.NativeValueReads == 0, "translation cannot trigger native cell data");
                }
                foreach (string path in paths) foreach (HashedString mode in modes)
                {
                    Reset(mode);
                    var nativeElement = Grid.Element[cell];
                    var go = new GameObject { name = "ignored_object_name" };
                    const string building = "native <script>alert(1)</script> \" & building";
                    go.Components[typeof(BuildingComplete)] = new BuildingComplete { name = building };
                    Grid.Objects[cell, (int)ObjectLayer.Building] = go; LegacyHtmlBoundary.Clear();
                    string expectedTooltip = $"Cell: {cell} ({x}, {y})\nElement: Oxygen\nTemp: {295.525f - 273.15f:F1}°C\nBuilding: {building}";
                    string expected = "<div class=\"cell\" style=\"background:#abcdef;\" title=\"" + WebUtility.HtmlEncode(expectedTooltip) + "\"></div>";
                    string html = ReadFileDirectly(path);
                    Check(html == expected && !html.Contains("<script>") && !html.Contains("data-state=\"unknown\""),
                        "visible native cell retains metadata, floating temperature and HTML encoding");
                    Check(LegacyHtmlBoundary.Colors.Count == 1 && LegacyHtmlBoundary.Colors[0] == (cell, nativeElement, go, mode, 295.525f, building),
                        "visible overlay color receives original native arguments without substitution");
                    var page = LegacyHtmlBoundary.Pages.Single();
                    Check(page.Width == 1 && page.Legend.Length > 0 && page.Title.Contains(mode.ToString()), "visible page metadata/legend/overlay retained");
                    Check(page.BackUrl == (path.Contains("/layers/") ? "/active/map/layers/" : "/active/map/"), "legacy navigation metadata retained");
                }
                foreach (string path in paths)
                {
                    Reset(OverlayModes.None.ID); Grid.Element[cell] = null; LegacyHtmlBoundary.Clear();
                    string html = ReadFileDirectly(path);
                    Check(html.Contains("Element: Vacuum") && LegacyHtmlBoundary.ColorReads == 1 && !html.Contains("data-state=\"unknown\""),
                        "visible null element keeps its existing legacy behavior");
                    Reset(OverlayModes.None.ID); Grid.Visible[cell] = false;
                    int moves = CameraController.Instance.Moves, changes = OverlayScreen.Instance.Changes;
                    string first = ReadFileDirectly(path), repeat = ReadFileDirectly(path);
                    Check(first == repeat && !Grid.Visible[cell] && CameraController.Instance.Moves == moves && OverlayScreen.Instance.Changes == changes,
                        "repeat does not reveal or change camera/overlay");
                }
                int invalidLayerStartAssertions = assertions;
                var invalidLayerLocales = new[]
                {
                    (Language: "en", Title: "Invalid map layer", Description: "Requested map layer must match a listed 32-cell layer within the active world bounds."),
                    (Language: "ja-JP", Title: "無効な地図レイヤー", Description: "指定した地図レイヤーは、現在のワールド範囲内で一覧にある32セル単位のレイヤーと一致する必要があります。"),
                    (Language: "zh-cn", Title: "地图图层无效", Description: "请求的地图图层必须与当前世界范围内按32格分段列出的图层一致。"),
                };
                foreach (var locale in invalidLayerLocales) foreach (bool visible in new[] { true, false })
                foreach (string invalidPath in new[] { "/active/map/layers/layer_1_31.html", "/active/map/layers/layer_0_30.html" })
                {
                    Reset(OverlayModes.None.ID);
                    ClusterManager.Instance.world.WorldSize = new(1, 64);
                    Array.Fill(Grid.Visible, visible);
                    McpLocalization.LanguageProvider = () => locale.Language;
                    int moves = CameraController.Instance.Moves, changes = OverlayScreen.Instance.Changes;
                    var position = Camera.main.transform.position; float zoom = Camera.main.orthographicSize;
                    LegacyHtmlBoundary.Clear(); LegacyHtmlBoundary.Poison = true;
                    string html = ReadFileDirectly(invalidPath);
                    int nativeReads = LegacyHtmlBoundary.NativeValueReads, colorReads = LegacyHtmlBoundary.ColorReads;
                    int pageCount = LegacyHtmlBoundary.Pages.Count;
                    string expected = "<h1>" + WebUtility.HtmlEncode(locale.Title) + "</h1><p>"
                        + WebUtility.HtmlEncode(locale.Description) + "</p>";
                    Check(html == expected && !html.Contains("map_html.invalid_layer."),
                        "invalid_layer_localization_contract: exact encoded error prose for both guards " + locale.Language + visible + invalidPath);
                    Check(nativeReads == 0 && colorReads == 0 && pageCount == 0,
                        "invalid_layer_localization_contract: invalid layer must not read native cells, color or construct a page");
                    Check(CameraController.Instance.Moves == moves && OverlayScreen.Instance.Changes == changes
                        && Camera.main.transform.position == position && Camera.main.orthographicSize == zoom
                        && OverlayScreen.Instance.mode == OverlayModes.None.ID && Grid.Visible[cell] == visible
                        && McpLocalization.GameLanguageProvider() == "ja",
                        "invalid_layer_localization_contract: rejection preserves camera, overlay, visibility and game locale");
                }
                foreach (var locale in invalidLayerLocales) foreach (bool visible in new[] { true, false })
                foreach (var valid in new[]
                {
                    (Path: "/active/map/layers/layer_0_31.html", Height: 64, Cells: 32),
                    (Path: "/active/map/layers/layer_32_32.html", Height: 33, Cells: 1),
                })
                {
                    Reset(OverlayModes.None.ID);
                    ClusterManager.Instance.world.WorldSize = new(1, valid.Height);
                    Array.Fill(Grid.Visible, visible);
                    var fixtureElements = Grid.Element; var fixtureTemperatures = Grid.Temperature;
                    var fixtureElement = fixtureElements[cell];
                    for (int row = 0; row < valid.Height; row++)
                    {
                        int fixtureCell = Grid.XYToCell(x, y + row);
                        fixtureElements[fixtureCell] = fixtureElement; fixtureTemperatures[fixtureCell] = 295.525f;
                    }
                    McpLocalization.LanguageProvider = () => locale.Language;
                    int moves = CameraController.Instance.Moves, changes = OverlayScreen.Instance.Changes;
                    var position = Camera.main.transform.position; float zoom = Camera.main.orthographicSize;
                    LegacyHtmlBoundary.Clear(); LegacyHtmlBoundary.Poison = !visible;
                    string html = ReadFileDirectly(valid.Path);
                    int nativeReads = LegacyHtmlBoundary.NativeValueReads, colorReads = LegacyHtmlBoundary.ColorReads;
                    int pageCount = LegacyHtmlBoundary.Pages.Count;
                    int cells = html.Split(new[] { "<div class=\"cell\" " }, StringSplitOptions.None).Length - 1;
                    Check(cells == valid.Cells && pageCount == 1 && LegacyHtmlBoundary.Pages[0].Width == 1
                        && LegacyHtmlBoundary.Pages[0].Cells == html,
                        "invalid_layer_localization_contract: valid complete and partial-final layers keep exact cell count and page route");
                    Check(visible ? nativeReads > 0 && colorReads == valid.Cells
                        : nativeReads == 0 && colorReads == 0,
                        "invalid_layer_localization_contract: valid layers retain known versus unknown native read boundary");
                    Check(html.Contains("data-state=\"unknown\"") == !visible && !html.Contains("<h1>")
                        && !html.Contains("map_html.invalid_layer."),
                        "invalid_layer_localization_contract: a permitted short final layer is not an invalid-layer error");
                    Check(CameraController.Instance.Moves == moves && OverlayScreen.Instance.Changes == changes
                        && Camera.main.transform.position == position && Camera.main.orthographicSize == zoom
                        && OverlayScreen.Instance.mode == OverlayModes.None.ID && Grid.Visible[cell] == visible
                        && McpLocalization.GameLanguageProvider() == "ja",
                        "invalid_layer_localization_contract: valid control preserves camera, overlay, visibility and game locale");
                }
                Console.WriteLine("PASS: actual invalid layer HTML localization assertions=" + (assertions - invalidLayerStartAssertions)
                    + " (included in legacy boundary total)");
                Console.WriteLine("PASS: " + assertions + " actual legacy HTML boundary assertions");
            }
            finally
            {
                LegacyHtmlBoundary.Poison = false;
                Grid.Element = savedElement; Grid.Temperature = savedTemperature; Grid.Objects = savedObjects;
                Grid.Visible = savedVisible; Grid.WorldIdx = savedWorldIdx; Grid.WorldValid = savedWorldValid;
                Grid.LegacyInvalidCell = savedInvalid; ClusterManager.Instance = savedCluster; Camera.main = savedCamera;
                OverlayScreen.Instance = savedOverlay; McpLocalization.LanguageProvider = savedLanguage;
                McpLocalization.GameLanguageProvider = savedGameLanguage; CultureInfo.CurrentCulture = savedCulture;
                CultureInfo.CurrentUICulture = savedUICulture; LegacyHtmlBoundary.Clear();
            }
        }

        internal static void RunLegacyHtmlMetadataRegressions()
        {
            int assertions = 0;
            void Check(bool value, string label) { assertions++; if (!value) throw new InvalidOperationException(label); }
            const int x = 12, y = 22;
            int cell = Grid.XYToCell(x, y);
            var cluster = ClusterManager.Instance; var camera = Camera.main; var overlay = OverlayScreen.Instance;
            var elements = Grid.Element; var temperatures = Grid.Temperature; var objects = Grid.Objects;
            var visible = Grid.Visible; var worldIds = Grid.WorldIdx; var worldValid = Grid.WorldValid;
            int invalid = Grid.LegacyInvalidCell; var languageProvider = McpLocalization.LanguageProvider;
            var gameLanguageProvider = McpLocalization.GameLanguageProvider;
            var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
            string[] paths = { "/active/map/viewport.html", "/active/map/index.html", "/active/map/layers/layer_0_0.html" };
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
                ClusterManager.Instance = new ClusterManager { activeWorldId = 0,
                    world = new World { WorldOffset = new(x, y), WorldSize = new(1, 1) } };
                Camera.main = new Camera { orthographicSize = 0, aspect = 1 };
                Camera.main.transform.position = new(x, y, 0);
                const string nativeView = "native <script> & \"view\"";
                OverlayScreen.Instance = new OverlayScreen { mode = new HashedString(nativeView) };
                Grid.Element = new Element[16384]; Grid.Temperature = new float[16384];
                Grid.Objects = new GameObject[16384, Enum.GetValues<ObjectLayer>().Length];
                Grid.Visible = Enumerable.Repeat(true, 16384).ToArray(); Grid.WorldIdx = new int[16384];
                Grid.WorldValid = Enumerable.Repeat(true, 16384).ToArray(); Grid.LegacyInvalidCell = -1;
                Grid.Element[cell] = new Element { id = SimHashes.Oxygen, name = "native_element_name", IsGas = true };
                Grid.Temperature[cell] = 295.525f;
                McpLocalization.GameLanguageProvider = () => "ja";
                int moves = CameraController.Instance.Moves, overlayChanges = OverlayScreen.Instance.Changes;
                foreach (string path in paths)
                {
                    string initialCells = null, initialLegend = null;
                    bool layer = path.Contains("/layers/");
                    string prefix = layer ? "map_html.layer_" : "map_html.viewport_";
                    foreach (string language in new[] { "en", "ja-JP", "zh-cn" })
                    {
                        McpLocalization.LanguageProvider = () => language;
                        Grid.Visible[cell] = true; LegacyHtmlBoundary.Poison = false; LegacyHtmlBoundary.Clear();
                        string html = ReadFileDirectly(path);
                        var page = LegacyHtmlBoundary.Pages.Single();
                        object[] args = layer ? new object[] { nativeView, 0, 0 } : new object[] { nativeView, x, x, y, y };
                        string title = string.Format(CultureInfo.InvariantCulture,
                            McpLocalization.GetForLanguage(language, prefix + "title"), args);
                        Check(page.Title == WebUtility.HtmlEncode(title), "page title resolves requested prose locale and all native bounds " + path + language);
                        Check(!page.Title.Contains("<script>") && page.Title.Contains("&lt;script&gt;"), "native view name is HTML encoded");
                        Check(page.BackText == WebUtility.HtmlEncode(McpLocalization.GetForLanguage(language, prefix + "back")), "back label resolves requested locale");
                        Check(page.Description == WebUtility.HtmlEncode(McpLocalization.GetForLanguage(language, prefix + "description")), "description resolves requested locale");
                        Check(!page.Title.Contains("map_html.") && !page.Title.Contains("{0}") && !page.Description.Contains("map_html."), "no unresolved key or placeholder");
                        Check(page.BackUrl == (layer ? "/active/map/layers/" : "/active/map/") && page.Width == 1, "route and grid width stay fixed");
                        initialCells ??= html; initialLegend ??= page.Legend;
                        Check(NormalizeOwnedLegacyTooltipLabels(html, language) == initialCells
                            && RemoveDefaultLegendLabelText(page.Legend) == RemoveDefaultLegendLabelText(initialLegend),
                            "changing page prose preserves native cells outside four owned tooltip labels and exact legend markup outside its ten label spans");
                        Grid.Visible[cell] = false; LegacyHtmlBoundary.Clear(); LegacyHtmlBoundary.Poison = true;
                        string hidden = ReadFileDirectly(path);
                        Check(hidden.Contains("data-state=\"unknown\"") && !hidden.Contains("Element:")
                            && LegacyHtmlBoundary.NativeValueReads == 0 && LegacyHtmlBoundary.ColorReads == 0,
                            "translated page does not bypass hidden-cell guard");
                        Check(LegacyHtmlBoundary.Pages.Single().Title == page.Title, "hidden and visible cells retain same page title");
                    }
                }
                Check(CameraController.Instance.Moves == moves && OverlayScreen.Instance.Changes == overlayChanges
                    && McpLocalization.GameLanguageProvider() == "ja", "prose reads do not move camera, change overlay or game locale");
                Console.WriteLine("PASS: " + assertions + " actual legacy HTML metadata localization assertions");
            }
            finally
            {
                LegacyHtmlBoundary.Poison = false;
                ClusterManager.Instance = cluster; Camera.main = camera; OverlayScreen.Instance = overlay;
                Grid.Element = elements; Grid.Temperature = temperatures; Grid.Objects = objects;
                Grid.Visible = visible; Grid.WorldIdx = worldIds; Grid.WorldValid = worldValid; Grid.LegacyInvalidCell = invalid;
                McpLocalization.LanguageProvider = languageProvider; McpLocalization.GameLanguageProvider = gameLanguageProvider;
                CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; LegacyHtmlBoundary.Clear();
            }
        }
    }
}
