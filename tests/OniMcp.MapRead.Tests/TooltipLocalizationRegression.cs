using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        // Independent contract fixtures; neither expected prose nor values come from the Reader.
        private static readonly string[] TooltipKeys = { "map_html.tooltip.cell", "map_html.tooltip.element", "map_html.tooltip.temperature", "map_html.tooltip.building" };
        private static readonly Dictionary<string, string[]> TooltipLabels = new()
        {
            ["en"] = new[] { "Cell", "Element", "Temp", "Building" },
            ["ja"] = new[] { "セル", "元素", "温度", "設備" },
            ["zh-Hans"] = new[] { "格子", "元素", "温度", "建筑" },
        };

        // Normalize only the four owned label prefixes in a known cell's tooltip attribute.
        // Native values (including words that resemble labels), units and all other markup remain.
        private static string NormalizeOwnedLegacyTooltipLabels(string html, string language)
        {
            string[] labels = TooltipLabels[McpLocalization.NormalizeLanguage(language)];
            return Regex.Replace(html, "title=\"([^\"]*)\"", match =>
            {
                string[] lines = WebUtility.HtmlDecode(match.Groups[1].Value).Split('\n');
                if (lines.Length < 3 || Enumerable.Range(0, 3).Any(i => !lines[i].StartsWith(labels[i] + ": ", StringComparison.Ordinal)))
                    return match.Value;
                for (int i = 0; i < 3; i++) lines[i] = TooltipLabels["en"][i] + lines[i].Substring(labels[i].Length);
                if (lines.Length >= 4 && lines[3].StartsWith(labels[3] + ": ", StringComparison.Ordinal))
                    lines[3] = TooltipLabels["en"][3] + lines[3].Substring(labels[3].Length);
                return "title=\"" + WebUtility.HtmlEncode(string.Join("\n", lines)) + "\"";
            });
        }

        internal static void RunLegacyHtmlTooltipRegressions()
        {
            int assertions = 0;
            void Check(bool value, string message)
            {
                assertions++;
                if (!value) throw new InvalidOperationException("tooltip_localization_contract: " + message);
            }
            const int x = 12, y = 22;
            int cell = Grid.XYToCell(x, y);
            string[] paths = { "/active/map/viewport.html", "/active/map/index.html", "/active/map/layers/layer_0_0.html" };
            string[] languages = { "en", "ja-JP", "zh-cn" };
            var savedCluster = ClusterManager.Instance; var savedCamera = Camera.main; var savedOverlay = OverlayScreen.Instance;
            var savedElement = Grid.Element; var savedTemperature = Grid.Temperature; var savedObjects = Grid.Objects;
            var savedVisible = Grid.Visible; var savedWorldIdx = Grid.WorldIdx; var savedWorldValid = Grid.WorldValid;
            int savedInvalid = Grid.LegacyInvalidCell; var savedLanguage = McpLocalization.LanguageProvider;
            var savedGameLanguage = McpLocalization.GameLanguageProvider; var savedCulture = CultureInfo.CurrentCulture;
            var savedUICulture = CultureInfo.CurrentUICulture;
            try
            {
                ClusterManager.Instance = new ClusterManager { activeWorldId = 0,
                    world = new World { WorldOffset = new(x, y), WorldSize = new(1, 1) } };
                Camera.main = new Camera { orthographicSize = 0, aspect = 1 };
                Camera.main.transform.position = new(x, y, 0);
                OverlayScreen.Instance = new OverlayScreen { mode = OverlayModes.None.ID };
                Grid.Element = new Element[16384]; Grid.Temperature = new float[16384];
                Grid.Objects = new GameObject[16384, Enum.GetValues<ObjectLayer>().Length];
                Grid.Visible = Enumerable.Repeat(true, 16384).ToArray(); Grid.WorldIdx = new int[16384];
                Grid.WorldValid = Enumerable.Repeat(true, 16384).ToArray(); Grid.LegacyInvalidCell = -1;
                var element = new Element { id = SimHashes.Oxygen, name = "unchanged native element name", IsGas = true };
                Grid.Element[cell] = element; Grid.Temperature[cell] = 295.525f;
                McpLocalization.GameLanguageProvider = () => "ja";
                int moves = CameraController.Instance.Moves, changes = OverlayScreen.Instance.Changes;
                var position = Camera.main.transform.position; float zoom = Camera.main.orthographicSize;
                var englishCells = new Dictionary<string, string>();
                var nativeReads = new Dictionary<string, string[]>();
                foreach (string culture in new[] { "", "fr-FR" })
                {
                    CultureInfo.CurrentCulture = culture == "" ? CultureInfo.InvariantCulture : new CultureInfo(culture);
                    CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                    foreach (string language in languages)
                    {
                        McpLocalization.LanguageProvider = () => language;
                        string canonical = McpLocalization.NormalizeLanguage(language);
                        string[] labels = TooltipLabels[canonical];
                        for (int i = 0; i < TooltipKeys.Length; i++)
                            Check(McpLocalization.Get(TooltipKeys[i]) == labels[i], "actual catalog resolves tooltip label " + language + "/" + TooltipKeys[i]);
                        foreach (string path in paths) foreach (string kind in new[] { "complete", "fallback_name", "no_building", "null_element" })
                        {
                            LegacyHtmlBoundary.Poison = false;
                            Grid.Visible[cell] = true; Grid.Element[cell] = kind == "null_element" ? null : element;
                            GameObject go = null;
                            string building = "";
                            if (kind == "complete" || kind == "fallback_name")
                            {
                                // Label-like prose inside the native name must survive normalization and encoding.
                                building = "native <script>alert(1)</script> \" & セル:keep 元素:keep 温度:keep 設備:keep 格子:keep 建筑:keep\nTemp: keep";
                                go = new GameObject { name = kind == "fallback_name" ? building : "ignored object name" };
                                if (kind == "complete") go.Components[typeof(BuildingComplete)] = new BuildingComplete { name = building };
                            }
                            Grid.Objects[cell, (int)ObjectLayer.Building] = go;
                            LegacyHtmlBoundary.Poison = false; LegacyHtmlBoundary.Clear();
                            string html = ReadFileDirectly(path);
                            var page = LegacyHtmlBoundary.Pages.Single();
                            string expectedTooltip = $"{labels[0]}: {cell} ({x}, {y})\n{labels[1]}: {(kind == "null_element" ? "Vacuum" : "Oxygen")}\n{labels[2]}: {295.525f - 273.15f:F1}°C";
                            if (building.Length > 0) expectedTooltip += "\n" + labels[3] + ": " + building;
                            string expected = "<div class=\"cell\" style=\"background:#abcdef;\" title=\"" + WebUtility.HtmlEncode(expectedTooltip) + "\"></div>";
                            string key = culture + "|" + path + "|" + kind;
                            Check(html == expected && page.Cells == expected, "exact localized/encoded tooltip and shell input " + language + "/" + key);
                            Check(!html.Contains("<script>") && !html.Contains("map_html.tooltip.") && !html.Contains("{0}"),
                                "no raw native markup, unresolved tooltip key or placeholder");
                            Check(WebUtility.HtmlDecode(html).Contains(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? "22,4°C" : "22.4°C"),
                                "existing F1 format follows the current culture independently of prose locale");
                            Check(LegacyHtmlBoundary.Colors.Count == 1 && LegacyHtmlBoundary.Colors[0]
                                == (cell, kind == "null_element" ? null : element, go, OverlayModes.None.ID, 295.525f, building),
                                "native color inputs remain exact, including a known cell with a null element");
                            Check(page.Width == 1 && page.BackUrl == (path.Contains("/layers/") ? "/active/map/layers/" : "/active/map/"),
                                "grid and navigation route are unchanged");
                            if (canonical == "en") englishCells[key] = html;
                            Check(NormalizeOwnedLegacyTooltipLabels(html, language) == englishCells[key],
                                "only the owned label prefixes differ; all native values and markup survive");
                            if (!nativeReads.ContainsKey(key)) nativeReads[key] = LegacyHtmlBoundary.NativeReadKinds.ToArray();
                            Check(LegacyHtmlBoundary.NativeReadKinds.SequenceEqual(nativeReads[key]),
                                "prose language adds no native value acquisition");
                            string[] kinds = LegacyHtmlBoundary.NativeReadKinds.ToArray();
                            LegacyHtmlBoundary.Clear(); string repeat = ReadFileDirectly(path);
                            Check(repeat == html && LegacyHtmlBoundary.NativeReadKinds.SequenceEqual(kinds),
                                "repeat retains tooltip output and native acquisition sequence");
                            Grid.Visible[cell] = false; LegacyHtmlBoundary.Clear(); LegacyHtmlBoundary.Poison = true;
                            string hidden = ReadFileDirectly(path);
                            string expectedUnknown = "<div class=\"cell\" data-state=\"unknown\" style=\"background:#171d29;\" title=\""
                                + WebUtility.HtmlEncode(McpLocalization.Get("map_html.unreadable")) + "\"></div>";
                            Check(hidden == expectedUnknown && !hidden.Contains("map_html.tooltip.")
                                && LegacyHtmlBoundary.NativeValueReads == 0 && LegacyHtmlBoundary.ColorReads == 0,
                                "unknown tooltip remains the existing fixed message without native data access");
                        }
                    }
                }
                Check(CameraController.Instance.Moves == moves && OverlayScreen.Instance.Changes == changes
                    && Camera.main.transform.position == position && Camera.main.orthographicSize == zoom
                    && McpLocalization.GameLanguageProvider() == "ja", "tooltip localization does not move camera, toggle overlay or change game locale");
                Console.WriteLine("PASS: " + assertions + " actual legacy HTML tooltip localization assertions");
            }
            finally
            {
                LegacyHtmlBoundary.Poison = false;
                ClusterManager.Instance = savedCluster; Camera.main = savedCamera; OverlayScreen.Instance = savedOverlay;
                Grid.Element = savedElement; Grid.Temperature = savedTemperature; Grid.Objects = savedObjects;
                Grid.Visible = savedVisible; Grid.WorldIdx = savedWorldIdx; Grid.WorldValid = savedWorldValid; Grid.LegacyInvalidCell = savedInvalid;
                McpLocalization.LanguageProvider = savedLanguage; McpLocalization.GameLanguageProvider = savedGameLanguage;
                CultureInfo.CurrentCulture = savedCulture; CultureInfo.CurrentUICulture = savedUICulture; LegacyHtmlBoundary.Clear();
            }
        }
    }
}
