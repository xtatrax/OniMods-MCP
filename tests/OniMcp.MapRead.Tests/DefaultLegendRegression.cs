using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        // Contract fixtures are independent of the runtime lookup and the linked Reader.
        private static readonly string[] DefaultLegendKeys = { "map_html.legend.vacuum_space", "map_html.legend.building", "map_html.legend.dirt", "map_html.legend.sandstone", "map_html.legend.water", "map_html.legend.polluted_water", "map_html.legend.algae", "map_html.legend.granite_rock", "map_html.legend.oxygen_gas", "map_html.legend.carbon_dioxide" };
        private static readonly string[] DefaultLegendColors = { "#1a202c", "#ecc94b", "#8c5b30", "#a88060", "#3182ce", "#4a3728", "#38a169", "#718096", "#e6fffa", "#feb2b2" };
        private static readonly Dictionary<string, string[]> DefaultLegendLabels = new()
        {
            ["en"] = new[] { "Vacuum / Space", "Building", "Dirt", "Sandstone", "Water", "Polluted Water", "Algae", "Granite / Rock", "Oxygen Gas", "Carbon Dioxide" },
            ["ja"] = new[] { "真空 / 宇宙", "設備", "土", "砂岩", "水", "汚染水", "緑藻", "花崗岩 / 岩石", "酸素ガス", "二酸化炭素" },
            ["zh-Hans"] = new[] { "真空 / 太空", "建筑", "泥土", "砂岩", "水", "污染水", "藻类", "花岗岩 / 岩石", "氧气", "二氧化碳" },
        };
        // Exact pre-change output; whitespace, markup, colors and prose remain in scope.
        private static readonly Dictionary<string, (string Viewport, string Layer)> OtherLegendHashes = new()
        {
            ["temperature"] = ("6344392aaeaf19ac9732a2afa7ac9ce988f3bdf8b8c3c56bf0fa54fc57a3f393", "bd68cf8b9b8c849fb55b42ac7624f79849c4f296731280926f1cbc734094127b"),
            ["power"] = ("dd5b2374b6f4073564fe31731d5187b404e80c186f9f21d2634b66b32e4e8407", "c30144d8184d6123b636e5f3aa42d289b22d13bd9e86ed1caae155bc7f3025cd"),
            ["oxygen"] = ("9acd4bd8619eec25e9ce2f3f182781476cfaa82e190dba509f738347167085fd", "e9fe6e60d18de9c422524788cbd7fba496266c36ccac64fde04423e6a99a044f"),
            ["liquid_conduits"] = ("245d247f801d82696dbbf00ee75428d14fac1027a9a152d5f9d985a4d4821ccb", "6f3e5e175e64b1e91c8dca4e70f685821ce1ceb54c4e9bcc06663fd46bbb5ca8"),
            ["gas_conduits"] = ("190206dce41086822ecc9e012b201ee66f937120ce90e6f852b77dc518f0c4d0", "88603da7152df8dd5b9fabb7e18c08554c60f8362e8119667ec5fe725c0406a5"),
            ["light"] = ("a134b5aeea2b2dccba483d55049beb35322ba7a24daa88c9abb5859d48ad1fe0", "237dc3ef36db4bd3411115db82a1eb278a251cb873a189fdf3a9fa911ef00d28"),
        };

        private static string RemoveDefaultLegendLabelText(string html)
        {
            const string pattern = @"<div class=""legend-item""><div class=""legend-color"" style=""background:(#[0-9a-f]{6});""></div>([^<>]*)</div>";
            var matches = Regex.Matches(html, pattern);
            if (matches.Count != 10) throw new InvalidOperationException("default legend must contain ten plain encoded label spans");
            var result = new StringBuilder(html);
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                if (matches[i].Groups[1].Value != DefaultLegendColors[i])
                    throw new InvalidOperationException("default legend swatch order changed");
                var label = matches[i].Groups[2]; result.Remove(label.Index, label.Length);
            }
            return result.ToString(); // Every character outside those ten text spans is retained.
        }

        internal static void RunDefaultLegendRegressions()
        {
            int assertions = 0; var failures = new List<string>();
            void Check(bool value, string label) { assertions++; if (!value) failures.Add(label); }
            const int x = 12, y = 22;
            int cell = Grid.XYToCell(x, y);
            string[] paths = { "/active/map/viewport.html", "/active/map/index.html", "/active/map/layers/layer_0_0.html" };
            string[] languages = { "en", "ja-JP", "zh-cn" };
            var cluster = ClusterManager.Instance; var camera = Camera.main; var overlay = OverlayScreen.Instance;
            var elements = Grid.Element; var temperatures = Grid.Temperature; var objects = Grid.Objects;
            var visible = Grid.Visible; var worldIds = Grid.WorldIdx; var worldValid = Grid.WorldValid;
            int invalid = Grid.LegacyInvalidCell; var languageProvider = McpLocalization.LanguageProvider;
            var gameLanguageProvider = McpLocalization.GameLanguageProvider;
            var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
            var originalCells = new Dictionary<string, string>();
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                ClusterManager.Instance = new ClusterManager { activeWorldId = 0,
                    world = new World { WorldOffset = new(x, y), WorldSize = new(1, 1) } };
                Camera.main = new Camera { orthographicSize = 0, aspect = 1 }; Camera.main.transform.position = new(x, y, 0);
                OverlayScreen.Instance = new OverlayScreen { mode = OverlayModes.None.ID };
                Grid.Element = new Element[16384]; Grid.Temperature = new float[16384];
                Grid.Objects = new GameObject[16384, Enum.GetValues<ObjectLayer>().Length];
                Grid.Visible = Enumerable.Repeat(true, 16384).ToArray(); Grid.WorldIdx = new int[16384];
                Grid.WorldValid = Enumerable.Repeat(true, 16384).ToArray(); Grid.LegacyInvalidCell = -1;
                var nativeElement = new Element { id = SimHashes.Oxygen, name = "native_element_name", IsGas = true };
                Grid.Element[cell] = nativeElement; Grid.Temperature[cell] = 295.525f;
                McpLocalization.GameLanguageProvider = () => "ja";
                int moves = CameraController.Instance.Moves, changes = OverlayScreen.Instance.Changes;
                var position = Camera.main.transform.position; float zoom = Camera.main.orthographicSize;
                foreach (string language in languages)
                {
                    McpLocalization.LanguageProvider = () => language;
                    string canonical = language == "ja-JP" ? "ja" : language == "zh-cn" ? "zh-Hans" : "en";
                    string[] expectedLabels = DefaultLegendLabels[canonical];
                    for (int i = 0; i < DefaultLegendKeys.Length; i++)
                        Check(McpLocalization.Get(DefaultLegendKeys[i]) == expectedLabels[i], "actual catalog resolves complete default legend " + language + "/" + DefaultLegendKeys[i]);
                    foreach (string path in paths)
                    {
                        OverlayScreen.Instance.mode = OverlayModes.None.ID; Grid.Visible[cell] = true;
                        LegacyHtmlBoundary.Poison = false; LegacyHtmlBoundary.Clear();
                        string html = ReadFileDirectly(path); var page = LegacyHtmlBoundary.Pages.Single();
                        string indent = new(' ', path.Contains("/layers/") ? 36 : 28);
                        string expectedLegend = "\n" + string.Join("\n", Enumerable.Range(0, 10).Select(i => indent
                            + "<div class=\"legend-item\"><div class=\"legend-color\" style=\"background:" + DefaultLegendColors[i]
                            + ";\"></div>" + WebUtility.HtmlEncode(expectedLabels[i]) + "</div>"));
                        Check(page.Legend == expectedLegend, "default legend exact HTML/color/order/text " + language + " " + path);
                        if (!originalCells.ContainsKey(path)) originalCells[path] = html;
                        Check(NormalizeOwnedLegacyTooltipLabels(html, language) == originalCells[path],
                            "MCP legend locale preserves native cells outside four owned tooltip labels " + path);
                        Check(LegacyHtmlBoundary.Colors.Count == 1 && LegacyHtmlBoundary.Colors[0]
                            == (cell, nativeElement, (GameObject)null, OverlayModes.None.ID, 295.525f, ""), "legend locale preserves native color arguments");
                        Check(!page.Legend.Contains("map_html.") && page.Width == 1, "default legend has no unresolved key or grid change");
                        Grid.Visible[cell] = false; LegacyHtmlBoundary.Clear(); LegacyHtmlBoundary.Poison = true;
                        string hidden = ReadFileDirectly(path);
                        Check(hidden.Contains("data-state=\"unknown\"") && LegacyHtmlBoundary.NativeValueReads == 0 && LegacyHtmlBoundary.ColorReads == 0,
                            "localized default legend retains hidden-cell native guard");
                        Check(LegacyHtmlBoundary.Pages.Single().Legend == page.Legend, "hidden and visible default legends agree");
                    }
                }
                foreach (var entry in OtherLegendHashes) foreach (string language in languages) foreach (string path in paths)
                {
                    McpLocalization.LanguageProvider = () => language; OverlayScreen.Instance.mode = new HashedString(entry.Key);
                    Grid.Visible[cell] = true; LegacyHtmlBoundary.Poison = false; LegacyHtmlBoundary.Clear();
                    string html = ReadFileDirectly(path); var page = LegacyHtmlBoundary.Pages.Single();
                    string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(page.Legend))).ToLowerInvariant();
                    Check(digest == (path.Contains("/layers/") ? entry.Value.Layer : entry.Value.Viewport) && NormalizeOwnedLegacyTooltipLabels(html, language) == originalCells[path],
                        "other overlay retains exact pre-change legend and cells " + entry.Key + " " + language + " " + path);
                }
                Check(CameraController.Instance.Moves == moves && OverlayScreen.Instance.Changes == changes
                    && Camera.main.transform.position == position && Camera.main.orthographicSize == zoom
                    && McpLocalization.GameLanguageProvider() == "ja", "legend reads do not move camera, toggle overlay or change game locale");
                if (failures.Count > 0) throw new InvalidOperationException("default_legend_regression_failures="
                    + failures.Count + "\n" + string.Join("\n", failures));
                Console.WriteLine("PASS: " + assertions + " actual default legend localization assertions");
            }
            finally
            {
                LegacyHtmlBoundary.Poison = false; ClusterManager.Instance = cluster; Camera.main = camera; OverlayScreen.Instance = overlay;
                Grid.Element = elements; Grid.Temperature = temperatures; Grid.Objects = objects;
                Grid.Visible = visible; Grid.WorldIdx = worldIds; Grid.WorldValid = worldValid; Grid.LegacyInvalidCell = invalid;
                McpLocalization.LanguageProvider = languageProvider; McpLocalization.GameLanguageProvider = gameLanguageProvider;
                CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; LegacyHtmlBoundary.Clear();
            }
        }
    }
}
