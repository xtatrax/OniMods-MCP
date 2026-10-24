using System.Net;
using System.Text.RegularExpressions;
using OniMcp.Support;
using OniMcp.Tools;

WorldEditorTools.RunRawButtonRegressions(args.Contains("injection", StringComparer.Ordinal));

// Only the untouched color helper's compile-time Element dependency is modeled.
// GetMapHtml and the localization loader/catalogs are the complete actual sources.
internal sealed class Element
{
    public string id = "host_element";
    public bool IsSolid = false;
    public bool IsLiquid = false;
}

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static void RunRawButtonRegressions(bool injection)
        {
            int assertions = 0;
            void Check(bool condition, string message)
            {
                assertions++;
                if (!condition) throw new InvalidOperationException(message);
            }
            const string cells = "<div class=\"cell\" data-state=\"unknown\" title=\"native &quot;name&quot; &amp; coordinate\">酸</div>";
            const string legend = "<span class=\"legend-item\">native legend</span>";
            var originalLanguage = McpLocalization.LanguageProvider;
            var originalGameLanguage = McpLocalization.GameLanguageProvider;
            string originalCulture = System.Globalization.CultureInfo.CurrentCulture.Name;
            string originalUICulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            try
            {
                McpLocalization.GameLanguageProvider = () => "ja";
                string pageWithoutButton = null;
                var cases = injection
                    ? new[] { (Language: "en", Raw: (string)null, Map: (string)null) }
                    : new[] {
                        (Language: "ja-JP", Raw: "HTMLソースを表示", Map: "地図を表示"),
                        (Language: "en", Raw: "Show Raw HTML", Map: "Show Visual Map"),
                        (Language: "zh-cn", Raw: "显示原始HTML", Map: "显示地图")
                    };
                foreach (var item in cases)
                {
                    McpLocalization.LanguageProvider = () => item.Language;
                    string raw = McpLocalization.GetForLanguage(item.Language, "map_html.show_raw");
                    string map = McpLocalization.GetForLanguage(item.Language, "map_html.show_map");
                    if (!injection)
                        Check(raw == item.Raw && map == item.Map, "actual catalog resolves both expected labels: " + item.Language);
                    else
                        Check(raw.Contains("</button><script>") && raw.Contains('"') && raw.Contains('\'')
                            && raw.Contains('&') && map.Contains("</script>") && map.Contains('\\')
                            && map.Contains('\u2028') && map.Contains('\u2029'), "host fixture contains every intended HTML/JS boundary");

                    string page = GetMapHtml("native &lt;view&gt;", "/active/map/", "native back", "native description", 7, cells, legend);
                    Match button = Regex.Match(page, @"<button\b(?<attrs>[^>]*)>(?<body>.*?)</button>", RegexOptions.Singleline);
                    Check(button.Success, "actual shell emits the toggle button");
                    string body = button.Groups["body"].Value;
                    // First localized positive with the old complete source must fail here.
                    Check(WebUtility.HtmlDecode(body) == raw, "initial button label follows current MCP language: " + item.Language);
                    Check(!body.Contains('<') && !body.Contains('>'), "localized initial label remains text, not markup");
                    string Attribute(string name)
                    {
                        Match match = Regex.Match(button.Groups["attrs"].Value, "(?:^|\\s)" + Regex.Escape(name) + "=\"([^\"]*)\"");
                        Check(match.Success, "localized button attribute exists: " + name);
                        string encoded = match.Groups[1].Value;
                        Check(!encoded.Contains('<') && !encoded.Contains('>') && !encoded.Contains('"'), "localized attribute cannot terminate its quoted HTML context");
                        return WebUtility.HtmlDecode(encoded);
                    }
                    Check(Attribute("data-show-raw") == raw && Attribute("data-show-map") == map,
                        "both labels survive attribute encoding exactly");
                    Check(Attribute("id") == "toggle-btn" && Attribute("class") == "toggle-btn"
                        && Attribute("onclick") == "toggleRaw()", "existing button identity/style/click handler remain fixed");

                    var scripts = Regex.Matches(page, @"<script>(.*?)</script>", RegexOptions.Singleline);
                    Check(scripts.Count == 1 && Regex.Matches(page, "<script>").Count == 1
                        && Regex.Matches(page, "</script>").Count == 1, "translation cannot inject a script boundary");
                    string script = scripts[0].Groups[1].Value;
                    Check(!script.Contains(raw, StringComparison.Ordinal) && !script.Contains(map, StringComparison.Ordinal),
                        "localized values never enter JavaScript source literals");
                    Check(script.Contains("btn.textContent = btn.getAttribute('data-show-map');", StringComparison.Ordinal)
                        && script.Contains("btn.textContent = btn.getAttribute('data-show-raw');", StringComparison.Ordinal),
                        "fixed JavaScript uses decoded HTML attributes through textContent on both toggle branches");
                    Check(!script.Contains("btn.innerHTML", StringComparison.Ordinal), "toggle never interprets the localized label as markup");
                    Check(page.Contains(cells, StringComparison.Ordinal) && page.Contains(legend, StringComparison.Ordinal)
                        && page.Contains("repeat(7, 10px)", StringComparison.Ordinal)
                        && page.Contains("href=\"/active/map/\"", StringComparison.Ordinal),
                        "cell/unknown marker/native legend/grid width/back target retain their exact input");
                    string normalized = page.Remove(button.Index, button.Length).Insert(button.Index, "HOST_BUTTON_BOUNDARY");
                    pageWithoutButton ??= normalized;
                    Check(normalized == pageWithoutButton, "changing only button prose preserves the complete surrounding emitted HTML/CSS/script");
                    Check(McpLocalization.GameLanguageProvider() == "ja"
                        && System.Globalization.CultureInfo.CurrentCulture.Name == originalCulture
                        && System.Globalization.CultureInfo.CurrentUICulture.Name == originalUICulture,
                        "rendering leaves game-language provider and both process cultures unchanged");
                }
                Console.WriteLine("PASS: " + assertions + " actual HTML shell/localization assertions; injection=" + injection);
                Console.WriteLine("Boundary: emitted HTML and fixed JS source only; no browser clicks, ONI runtime or native UI acceptance.");
            }
            finally
            {
                McpLocalization.LanguageProvider = originalLanguage;
                McpLocalization.GameLanguageProvider = originalGameLanguage;
            }
        }
    }
}
