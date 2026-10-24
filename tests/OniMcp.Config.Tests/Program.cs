using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using OniMcp.Config;
using OniMcp.Support;
using PeterHan.PLib;
using PeterHan.PLib.Options;

internal static class Program
{
    private static readonly FieldInfo CurrentField = typeof(OniMcpOptions)
        .GetField("_current", BindingFlags.Static | BindingFlags.NonPublic);

    private static void Main()
    {
        Run("missing config keeps authentication opt-in", MissingConfig);
        Run("loopback hosts stay available without authentication", LoopbackHosts);
        Run("unauthenticated remote hosts fail closed", RemoteHostsRequireAuthentication);
        Run("authenticated remote HTTP is allowed with a plaintext warning", AuthenticatedRemoteHost);
        Run("invalid initial config remains untouched and prevents startup", InvalidInitialConfig);
        Run("unreadable initial config prevents startup", UnreadableInitialConfig);
        Run("failed reload retains active authentication and original file", FailedReload);
        Run("legacy config preserves authentication during migration", LegacyConfig);
        Run("concurrent first access publishes a single configuration", ConcurrentInitialization);
        Run("concurrent saves expose complete JSON to readers", AtomicSaves);
        Run("failed save retains current configuration and cleans temporary files", FailedSave);
        Run("options status stays compact, token stays masked, and support link opens", OptionsSurface);
        Run("language selection resolves game locale and falls back safely", LanguageResolution);
        Run("language configuration persists and old web forms preserve it", LanguageSettings);
        Run("localized settings HTML escapes values and never reveals tokens", LocalizedSettingsPage);
        Run("printing pod policy switches, persists, and preserves older forms", PrintingPodPolicy);
        Run("pending printing choices remain visible across tool calls and toggle changes", PrintingPodMiddleware);
        Run("tool content metadata preserves opaque legacy results", ToolContentWireContract);
        Run("printing policy refresh removes only middleware-owned blocks", PrintingPolicyOwnership);
        Run("content metadata copies caller fields without exposing ownership", ContentMetadataIsolation);
        Run("PLib dialog storage uses isolated canonical settings", PlibOptionsStorageRegression.Run);
        Console.WriteLine("PASS: 21 OniMcp config regression tests");
    }

    private static void Run(string name, Action test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "OniMcp.Config.Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        OniMcpPaths.ConfigPath = Path.Combine(directory, "OniMcpConfig.json");
        OniMcpPaths.ModPath = directory;
        CurrentField.SetValue(null, null);
        McpLocalization.LanguageProvider = () => OniMcpOptions.Current.Language;
        McpLocalization.GameLanguageProvider = () => "en";
        try
        {
            test();
            Console.WriteLine("PASS: " + name);
        }
        finally
        {
            CurrentField.SetValue(null, null);
            McpLocalization.LanguageProvider = null;
            McpLocalization.GameLanguageProvider = null;
            Directory.Delete(directory, true);
        }
    }

    private static void MissingConfig()
    {
        var options = OniMcpOptions.Current;
        Check(!options.AuthEnabled && options.Host == "localhost", "New installs must keep the existing defaults.");
        Check(!string.IsNullOrEmpty(options.AuthToken), "New installs need a persistent token ready for opt-in.");
        Check(JObject.Parse(File.ReadAllText(OniMcpPaths.ConfigPath))["AuthToken"].Value<string>() == options.AuthToken,
            "The default token must be saved exactly once.");
    }

    private static void LoopbackHosts()
    {
        foreach (string host in new[] { "localhost", "127.0.0.1", "::1", "[::1]" })
        {
            var options = new OniMcpOptions { Host = host, AuthEnabled = false };
            OniMcpOptions.Save(options);
            options.ValidateListenSecurity();
            string[] prefixes = options.ListenPrefixes.ToArray();
            Check(prefixes.Length > 0, "Loopback host produced no listener prefixes: " + host);
            Check(prefixes.All(prefix => !prefix.Contains("http://+:")), "Loopback host widened to a wildcard listener: " + host);
        }

        var ipv6 = new OniMcpOptions { Host = "::1", AuthEnabled = false };
        Check(ipv6.ListenPrefixes.All(prefix => prefix.Contains("http://[::1]:")), "IPv6 loopback prefixes must use bracketed URI syntax.");
        Check(ipv6.EndpointUrl.StartsWith("http://[::1]:", StringComparison.Ordinal), "IPv6 loopback endpoint URL is malformed.");
    }

    private static void RemoteHostsRequireAuthentication()
    {
        foreach (string host in new[] { "0.0.0.0", "192.0.2.10", "2001:db8::10", "mcp.example.test" })
        {
            var options = new OniMcpOptions { Host = host, AuthEnabled = false };
            ExpectFailure(options.ValidateListenSecurity);
            ExpectFailure(() => OniMcpOptions.Save(options));
            Check(!File.Exists(OniMcpPaths.ConfigPath), "Unsafe remote settings were persisted for " + host);
        }
    }

    private static void AuthenticatedRemoteHost()
    {
        var options = new OniMcpOptions
        {
            Host = "0.0.0.0",
            AuthEnabled = true,
            AuthToken = "remote-secret"
        };
        OniMcpOptions.Save(options);
        options.ValidateListenSecurity();
        Check(!string.IsNullOrEmpty(options.PlaintextRemoteWarning), "Remote HTTP must expose a plaintext/TLS warning.");
        Check(options.ListenPrefixes.Any(prefix => prefix == "http://+:8788/"), "Authenticated wildcard bind did not produce the expected listener prefix.");

        var local = new OniMcpOptions { Host = "localhost", AuthEnabled = false };
        Check(string.IsNullOrEmpty(local.PlaintextRemoteWarning), "Loopback-only HTTP must not be labeled as remote exposure.");
    }

    private static void InvalidInitialConfig()
    {
        const string malformed = "{\"AuthEnabled\":true,\"AuthToken\":\"secret\"";
        File.WriteAllText(OniMcpPaths.ConfigPath, malformed);
        ExpectFailure(() => { var ignored = OniMcpOptions.Current; });
        Check(File.ReadAllText(OniMcpPaths.ConfigPath) == malformed, "Failed reads must not destroy the original file.");
        Check(CurrentField.GetValue(null) == null, "Invalid initial settings must not publish unauthenticated defaults.");
    }

    private static void UnreadableInitialConfig()
    {
        Directory.CreateDirectory(OniMcpPaths.ConfigPath);
        ExpectFailure(() => { var ignored = OniMcpOptions.Current; });
        Check(CurrentField.GetValue(null) == null, "A path that cannot be read must not publish defaults.");
    }

    private static void FailedReload()
    {
        var options = new OniMcpOptions { AuthEnabled = true, AuthToken = "retain-me", Port = 9234 };
        OniMcpOptions.Save(options);
        File.WriteAllText(OniMcpPaths.ConfigPath, "incomplete write");
        OniMcpOptions.Reload();
        Check(ReferenceEquals(options, OniMcpOptions.Current), "A failed reload must preserve active settings.");
        Check(OniMcpOptions.Current.AuthEnabled && OniMcpOptions.Current.AuthToken == "retain-me", "Authentication changed.");
        Check(File.ReadAllText(OniMcpPaths.ConfigPath) == "incomplete write", "Reload must leave the file available for recovery.");
    }

    private static void LegacyConfig()
    {
        File.WriteAllText(OniMcpPaths.ConfigPath, "{\"AuthEnabled\":true,\"AuthToken\":\" token \",\"Port\":99999}");
        var options = OniMcpOptions.Current;
        Check(options.AuthEnabled && options.AuthToken == "token", "Migration must retain opted-in authentication.");
        Check(options.Port == 8788 && options.SecurityMigrationVersion == 1, "Migration and validation did not run.");
        OniMcpOptions.Reload();
        Check(OniMcpOptions.Current.AuthEnabled && OniMcpOptions.Current.AuthToken == "token", "Migrated settings did not persist.");
    }

    private static void ConcurrentInitialization()
    {
        var observed = new OniMcpOptions[16];
        using (var start = new ManualResetEventSlim())
        {
            var threads = Enumerable.Range(0, observed.Length).Select(index => new Thread(() =>
            {
                start.Wait();
                observed[index] = OniMcpOptions.Current;
            })).ToArray();
            foreach (var thread in threads)
                thread.Start();
            start.Set();
            foreach (var thread in threads)
                thread.Join();
        }
        Check(observed.All(options => ReferenceEquals(options, observed[0])), "Concurrent readers received different settings / tokens.");
        Check(JObject.Parse(File.ReadAllText(OniMcpPaths.ConfigPath))["AuthToken"].Value<string>() == observed[0].AuthToken,
            "The published token differs from the saved token.");
    }

    private static void AtomicSaves()
    {
        OniMcpOptions.Save(new OniMcpOptions());
        using (var stop = new CancellationTokenSource())
        using (var started = new ManualResetEventSlim())
        {
            int reads = 0;
            var reader = Task.Run(() =>
            {
                do
                {
                    using (var stream = new FileStream(OniMcpPaths.ConfigPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete))
                    using (var text = new StreamReader(stream))
                        JObject.Parse(text.ReadToEnd());
                    Interlocked.Increment(ref reads);
                    started.Set();
                } while (!stop.IsCancellationRequested);
            });
            try
            {
                Check(started.Wait(TimeSpan.FromSeconds(10)), "The config reader did not start.");
                Parallel.For(0, 100, index => OniMcpOptions.Save(new OniMcpOptions
                {
                    AuthEnabled = true,
                    AuthToken = new string((char)('a' + index % 26), 32768),
                    Port = 9000 + index
                }));
            }
            finally
            {
                stop.Cancel();
                reader.GetAwaiter().GetResult();
            }
            Check(reads > 1, "The test did not observe concurrent config reads.");
            var saved = JObject.Parse(File.ReadAllText(OniMcpPaths.ConfigPath));
            Check(saved["Port"].Value<int>() == OniMcpOptions.Current.Port, "Disk and active settings disagree.");
        }
        Check(!Directory.GetFiles(OniMcpPaths.ModPath, "*.tmp").Any(), "Successful saves left temporary files.");
    }

    private static void FailedSave()
    {
        var options = new OniMcpOptions { AuthEnabled = true, AuthToken = "keep-me" };
        OniMcpOptions.Save(options);
        string originalPath = OniMcpPaths.ConfigPath;
        string original = File.ReadAllText(originalPath);
        OniMcpPaths.ConfigPath = Path.Combine(OniMcpPaths.ModPath, "blocked");
        Directory.CreateDirectory(OniMcpPaths.ConfigPath);
        ExpectFailure(() => OniMcpOptions.Save(new OniMcpOptions { AuthEnabled = false }));
        Check(ReferenceEquals(options, OniMcpOptions.Current), "A failed save replaced the active settings.");
        Check(File.ReadAllText(originalPath) == original, "A failed save changed the previous file.");
        Check(!Directory.GetFiles(OniMcpPaths.ModPath, "*.tmp").Any(), "A failed replacement left a temporary file.");
    }

    private static void OptionsSurface()
    {
        OniMcpPaths.ConfigPath = Path.Combine(OniMcpPaths.ModPath, new string('x', 240), "OniMcpConfig.json");
        var options = new OniMcpOptions { Host = new string('h', 100), AuthToken = "never-show-this-token" };
        var entries = options.CreateOptions().ToArray();
        var status = entries.OfType<TextBlockOptionsEntry>().Single();
        Check(status.Option.Title.Split('\n').All(line => line.Length <= 60), "Long paths or hosts widened the options dialog.");
        Check(!status.Option.Title.Contains(OniMcpPaths.ConfigPath), "The full config path appeared in the dialog.");
        Check(status.Option.Tooltip.Contains(OniMcpPaths.ConfigPath), "The full config path is not available in the status tooltip.");
        Check(entries.OfType<TextBlockOptionsEntry>().All(entry => !entry.Option.Title.Contains(options.AuthToken)),
            "The token appeared in visible status text.");
        Check(typeof(OniMcpOptions).GetProperty(nameof(OniMcpOptions.AuthToken))
            .GetCustomAttributes(typeof(DynamicOptionAttribute), false).Length == 1,
            "The token no longer uses the masked input handler.");

        var support = entries.OfType<ButtonOptionsEntry>().Single(entry => entry.Name == "OpenProjectSupport");
        Check(support.Option.Category == "Support" && support.Option.Title == "AI トークンを購入 / プロジェクトを支援",
            "The support button does not clearly name the optional AI token purchase.");
        Check(support.Option.Tooltip.Contains("https://api.lmm.best")
            && support.Option.Tooltip.Contains("完全に任意")
            && support.Option.Tooltip.Contains("optional")
            && support.Option.Tooltip.Contains("MOD のアクセストークンとは無関係"),
            "The support tooltip must distinguish optional AI tokens from OniMcp access tokens in both languages.");
        ((Action<object>)support.Value)(null);
        Check(UnityEngine.Application.LastOpenedUrl == "https://api.lmm.best", "The support button opened the wrong URL.");
    }

    private static void LanguageResolution()
    {
        foreach (var pair in new[]
        {
            new[] { "ja_JP", "ja" }, new[] { " JP ", "ja" }, new[] { "zh_cn", "zh-Hans" },
            new[] { "zh_hans", "zh-Hans" }, new[] { "zh_klei", "zh-Hans" },
            new[] { "en-US", "en" }, new[] { "zh-TW", "en" }, new[] { "de", "en" },
            new[] { "", "auto" }
        })
            Check(McpLocalization.NormalizeLanguage(pair[0]) == pair[1], "Unexpected language normalization: " + pair[0]);
        Check(McpLocalization.ResolveLanguage("auto", "ja_JP") == "ja", "Auto must follow a supported game language.");
        Check(McpLocalization.ResolveLanguage("zh-Hans", "ja") == "zh-Hans", "An explicit choice must override the game language.");
        Check(McpLocalization.ResolveLanguage("auto", null) == "en", "A missing game locale must fall back to English.");
        Check(McpLocalization.GetForLanguage("ja", "settings.title") == "OniMcp 設定", "Japanese catalog was not embedded or loaded.");
        Check(McpLocalization.GetForLanguage("zh-Hans", "settings.title") == "OniMcp 设置", "Chinese catalog was not embedded or loaded.");
        Check(McpLocalization.GetForLanguage("ja", "test.englishOnly") == "English fallback {0}", "Missing translated keys must use English.");
        Check(McpLocalization.GetForLanguage("ja", "test.missing", "caller fallback") == "caller fallback", "Unknown keys must preserve the caller fallback.");
        foreach (var language in new[] { "en", "ja", "zh-Hans" })
        {
            var group = McpLocalization.GetGroup("humanMap.", language);
            Check(group.Count == 61 && group["title"] == McpLocalization.GetForLanguage(language, "humanMap.title"), "Browser group follows chosen language.");
            group["title"] = "mutated";
            Check(McpLocalization.GetGroup("humanMap.", language)["title"] != "mutated", "Browser groups are detached snapshots.");
            var rendered = McpLocalization.RenderTextTemplate("<html lang=\"{{language}}\">{{humanMap.title}} / Oxygen / /active/map/</html>", language);
            Check(rendered.Contains("lang=\"" + language + "\"") && !rendered.Contains("{{") && rendered.Contains("Oxygen / /active/map/"), "Template changes prose and locale only.");
        }
        Check(McpLocalization.RenderTextTemplate("{{test.missing}}", "ja") == "test.missing", "Missing template key fallback.");
        Check(McpLocalization.RenderTextTemplate("{{test.html}}", "en") == "&lt;script&gt;&amp;&quot;&apos;", "HTML template values must be escaped.");
        McpLocalization.LanguageProvider = () => "auto";
        McpLocalization.GameLanguageProvider = () => "zh_hans";
        Check(McpLocalization.CurrentLanguage == "zh-Hans", "Provider language was not resolved.");
        McpLocalization.GameLanguageProvider = () => { throw new InvalidOperationException("not ready"); };
        Check(McpLocalization.CurrentLanguage == "en", "An unavailable game provider must not break MCP requests.");
    }

    private static void LanguageSettings()
    {
        File.WriteAllText(OniMcpPaths.ConfigPath, "{\"AuthToken\":\"preserve-token\",\"Port\":9123}");
        Check(OniMcpOptions.Current.Language == "auto", "Existing config files must default to auto.");
        var options = OniMcpOptions.Current;
        options.Language = "JA_jp";
        OniMcpOptions.Save(options);
        OniMcpOptions.Reload();
        Check(OniMcpOptions.Current.Language == "ja", "Normalized language did not persist.");
        Check(OniMcpOptions.Current.AuthToken == "preserve-token" && OniMcpOptions.Current.Port == 9123,
            "Language persistence changed unrelated settings.");
        Check(OniMcpOptions.SubmittedLanguage(new Dictionary<string, string>(), "ja") == "ja", "Old forms must preserve the selected language.");
        var oldForm = new Dictionary<string, string>
        {
            ["host"] = "localhost", ["port"] = "9123", ["screenshotRetentionMinutes"] = "120", ["screenshotMaxFiles"] = "40"
        };
        var parsed = OniMcp.Server.McpHttpServer.ParseSettingsForm(oldForm, OniMcpOptions.Current);
        Check(parsed.Language == "ja" && parsed.AuthToken == "preserve-token", "The real settings parser must retain language and token for older forms.");
        oldForm["language"] = "not-supported";
        ExpectFailure(() => OniMcp.Server.McpHttpServer.ParseSettingsForm(oldForm, OniMcpOptions.Current));
        foreach (string language in new[] { "auto", "en", "ja", "zh-Hans" })
            Check(OniMcpOptions.SubmittedLanguage(new Dictionary<string, string> { ["language"] = language }, "en") == language,
                "The web form rejected a supported language.");
        foreach (string language in new[] { "", "de", "JA", "<script>", "auto,en" })
            ExpectFailure(() => OniMcpOptions.SubmittedLanguage(new Dictionary<string, string> { ["language"] = language }, "ja"));
        Check(OniMcpOptions.Current.Language == "ja", "Rejected form input must not alter active settings.");
    }

    private static void LocalizedSettingsPage()
    {
        var server = new OniMcp.Server.McpHttpServer();
        foreach (var pair in new[] { new[] { "en", "OniMcp Settings" }, new[] { "ja", "OniMcp 設定" }, new[] { "zh-Hans", "OniMcp 设置" } })
        {
            var options = new OniMcpOptions { Language = pair[0], AuthToken = "never-render-this-token" };
            OniMcpOptions.Save(options);
            options.Host = "\"><script>alert(1)</script>";
            string html = server.SettingsHtmlForTest("<script>alert(2)</script>");
            Check(WebUtility.HtmlDecode(html).Contains("<title>" + pair[1] + "</title>"), "Settings title did not use " + pair[0]);
            Check(html.Contains("<option value=\"" + pair[0] + "\" selected>"), "The language select lost its current value.");
            Check(html.Contains("<html lang=\"" + pair[0] + "\">"), "HTML language does not match the selected language.");
            Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;"), "Settings values or status text were not HTML escaped.");
            Check(!html.Contains(options.AuthToken), "The bearer token leaked into rendered settings HTML.");
            Check(!html.Contains("{{settings.") && !html.Contains("{{language}}"), "An untranslated template placeholder leaked into HTML.");
        }
        OniMcpOptions.Save(new OniMcpOptions { Language = "auto" });
        McpLocalization.GameLanguageProvider = () => "ja_JP";
        string automatic = server.SettingsHtmlForTest();
        Check(automatic.Contains("<html lang=\"ja\">") && automatic.Contains("<option value=\"auto\" selected>"),
            "Auto must show the game language while preserving the auto selection.");
        const string translatedText = "Literal {value}, {0}, and &lt;tag&gt;";
        string direct = OniMcp.Server.McpHttpServer.LocalizedTemplateForTest("<p>{{settings.fixture.literalBraces}}</p>", false);
        Check(direct == "<p>" + translatedText + "</p>", "Plain append sections must retain literal translation braces without doubling them.");
        string format = OniMcp.Server.McpHttpServer.LocalizedTemplateForTest("<p>{{settings.fixture.literalBraces}}</p><input value=\"{0}\">", true);
        var rendered = new System.Text.StringBuilder().AppendFormat(format, "actual value").ToString();
        Check(rendered == "<p>" + translatedText + "</p><input value=\"actual value\">",
            "Translation braces must remain literal while template format arguments still substitute.");
    }

    private static void PrintingPodPolicy()
    {
        Check(!new OniMcpOptions().RequirePrintingPodSelection, "Fresh options must default to OFF.");
        Check(!Newtonsoft.Json.JsonConvert.DeserializeObject<OniMcpOptions>("{}").RequirePrintingPodSelection, "Older configs without the setting must default to OFF.");
        Check(!OniMcpOptions.Current.RequirePrintingPodSelection, "Existing installs must retain disabled behavior.");
        var form = new Dictionary<string, string>
        {
            ["host"] = "localhost", ["port"] = "8788",
            ["screenshotRetentionMinutes"] = "120", ["screenshotMaxFiles"] = "40",
            ["printingPodPolicyPresent"] = "true", ["requirePrintingPodSelection"] = "true"
        };
        var parsed = OniMcp.Server.McpHttpServer.ParseSettingsForm(form, OniMcpOptions.Current);
        OniMcpOptions.Save(parsed);
        OniMcpOptions.Reload();
        Check(OniMcpOptions.Current.RequirePrintingPodSelection, "Policy must survive config reload.");
        var server = new OniMcp.Server.McpHttpServer();
        Check(server.SettingsHtmlForTest().Contains(@"value=""true"" checked"), "Enabled checkbox must render checked.");
        form.Remove("printingPodPolicyPresent"); form.Remove("requirePrintingPodSelection");
        Check(OniMcp.Server.McpHttpServer.ParseSettingsForm(form, OniMcpOptions.Current).RequirePrintingPodSelection,
            "Old settings forms must not silently disable the policy.");
        form["printingPodPolicyPresent"] = "true";
        Check(!OniMcp.Server.McpHttpServer.ParseSettingsForm(form, OniMcpOptions.Current).RequirePrintingPodSelection,
            "Unchecked current form must disable guidance.");
        foreach (bool required in new[] { false, true })
        foreach (bool? available in new bool?[] { null, false, true })
        {
            var policy = PrintingPodSelectionPolicy.Describe(required, available);
            if (!required)
            {
                Check(policy == null, "OFF must not produce guidance even when supplies are ready.");
                continue;
            }
            Check(((string[])policy["allowedDecisions"]).SequenceEqual(new[] { "claim", "reject_all" }), "Both acceptance and full rejection must resolve the offer.");
            Check((bool)policy["selectionRequired"] == (required && available == true), "Selection requirement must reflect readiness and mode.");
            Check((bool)policy["decisionRequired"] == (available == true), "Unavailable or unknown rewards must not demand action.");
            Check(policy["available"] == null ? !available.HasValue : (bool)policy["available"] == available,
                "Unknown readiness must remain distinct from false.");
            Check((policy["instruction"] != null) == (available == true), "Only available rewards need instructions.");
        }
        foreach (string lang in new[] { "en", "ja", "zh-Hans" })
        {
            OniMcpOptions.Current.Language = lang;
            Check(!server.SettingsHtmlForTest().Contains("{{settings."), "Policy label must be localized.");
            Check((string)PrintingPodSelectionPolicy.Describe(true, true)["instruction"] == McpLocalization.Get("policy.printingPod.required", "missing"),
                "Policy instruction must use the selected language.");
        }
    }

    private static void PrintingPodMiddleware()
    {
        Game.Instance = new Game();
        Immigration.Instance = new Immigration { ImmigrantsAvailable = true };
        try
        {
            foreach (bool required in new[] { true, true, false })
            {
                OniMcpOptions.Current.RequirePrintingPodSelection = required;
                var result = OniMcp.Tools.ToolCallMiddleware.Inject(OniMcp.Core.CallToolResult.Error("original error"), null);
                Check(result.IsError && result.Content[0].Text == "original error", "Policy must not replace the tool result or error.");
                OniMcp.Tools.ToolCallMiddleware.Inject(result, null);
                if (!required)
                {
                    Check(result.Content.Count == 1, "OFF must leave original tool results untouched.");
                    continue;
                }
                Check(result.Content.Count == 2, "Nested tool dispatch must not duplicate policy blocks.");
                var policy = JObject.Parse(result.Content.Last().Text)["printingPodSelection"];
                Check(policy["selectionRequired"].Value<bool>() == required, "Readiness must remain visible and mode changes apply on next call.");
                var args = policy["statusAction"]["arguments"];
                Check(args["domain"].Value<string>() == "side_surface" && args["kind"].Value<string>() == "printing_pod" && args["action"].Value<string>() == "list_rewards",
                    "Guidance must use the public building_control path.");
            }
            OniMcpOptions.Current.RequirePrintingPodSelection = true;
            var stale = OniMcp.Tools.ToolCallMiddleware.Inject(OniMcp.Core.CallToolResult.Text("original"), null);
            OniMcpOptions.Current.RequirePrintingPodSelection = false;
            OniMcp.Tools.ToolCallMiddleware.Inject(stale, null);
            Check(stale.Content.Count == 1 && stale.Content[0].Text == "original", "Switching OFF must remove nested old reminders.");
            OniMcpOptions.Current.RequirePrintingPodSelection = true;
            Immigration.Instance.ImmigrantsAvailable = false;
            var resolved = OniMcp.Tools.ToolCallMiddleware.Inject(OniMcp.Core.CallToolResult.Text("claimed"), null);
            Check(!JObject.Parse(resolved.Content.Last().Text)["printingPodSelection"]["decisionRequired"].Value<bool>(),
                "Consumed rewards must clear the pending decision.");
            Game.Instance.Loading = true;
            Check(OniMcp.Tools.ToolCallMiddleware.Inject(OniMcp.Core.CallToolResult.Text("loading"), null).Content.Count == 1,
                "Do not publish stale reward state during load.");
            Game.Instance = null;
            Check(OniMcp.Tools.ToolCallMiddleware.Inject(OniMcp.Core.CallToolResult.Text("menu"), null).Content.Count == 1,
                "Main menu must not inherit a previous colony's pending decision.");
        }
        finally { Game.Instance = null; Immigration.Instance = null; }
    }

    private static JObject Wire(object value)
    {
        return JObject.Parse(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
    }

    private static string ContentKind(ToolContent content)
    {
        return (string)Wire(content)["_meta"]?["onimcp.contentKind"];
    }

    private static void ToolContentWireContract()
    {
        OniMcpOptions.Current.RequirePrintingPodSelection = false;
        ToolCallMiddleware.Clear();
        Game.Instance = new Game();
        Immigration.Instance = new Immigration { ImmigrantsAvailable = true };
        try
        {
            // These are opaque legacy bodies, not native candidate-generation fixtures.
            string[] bodies = { "{\"candidateCount\":null}", "{\"candidateCount\":0}",
                "{\"candidateCount\":2,\"rewards\":[1,2]}", "[]", "null", "plain\ntext", "", null };
            foreach (bool error in new[] { false, true })
            {
                var result = new CallToolResult { Content = new List<ToolContent>(), IsError = error };
                foreach (string body in bodies)
                    result.Content.Add((error ? CallToolResult.Error(body) : CallToolResult.Text(body)).Content[0]);
                ToolContent[] originals = result.Content.ToArray();
                Check(originals.All(content => ContentKind(content) == "tool_result"),
                    "Text and Error factories must mark every body as tool_result.");
                var returned = ToolCallMiddleware.Inject(result, null);
                Check(ReferenceEquals(returned, result) && result.IsError == error && result.Content.Count == bodies.Length,
                    "OFF must retain the original result, error flag and content count.");
                for (int index = 0; index < bodies.Length; index++)
                    Check(ReferenceEquals(result.Content[index], originals[index]) && result.Content[index].Text == bodies[index],
                        "Middleware parsed, reordered or reformatted an opaque business body.");
                var wire = Wire(result);
                Check(wire["isError"].Value<bool>() == error && wire["content"].Count() == bodies.Length,
                    "Wire serialization changed the legacy result envelope.");
                for (int index = 0; index < bodies.Length; index++)
                {
                    Check((string)wire["content"][index]["text"] == bodies[index], "Wire text differs from legacy body.");
                    Check((string)wire["content"][index]["_meta"]["onimcp.contentKind"] == "tool_result",
                        "Wire content lost its machine-readable result kind.");
                }
            }
            var legacy = new ToolContent { Text = "legacy manual block" };
            var unmarked = new CallToolResult { Content = new List<ToolContent> { legacy } };
            Check(ReferenceEquals(ToolCallMiddleware.Inject(unmarked, null).Content[0], legacy)
                && Wire(legacy).Property("_meta") == null, "Unmarked legacy blocks must remain unmodified.");
            Check(ToolCallMiddleware.Inject(null, null) == null, "OFF without notifications must not invent a result.");
        }
        finally { Game.Instance = null; Immigration.Instance = null; ToolCallMiddleware.Clear(); }
    }

    private static void PrintingPolicyOwnership()
    {
        Game.Instance = new Game();
        Immigration.Instance = new Immigration { ImmigrantsAvailable = true };
        ToolCallMiddleware.Clear();
        try
        {
            var lookalike = CallToolResult.Text("{\"printingPodSelection\":{\"business\":true}}").Content[0];
            var spacedLookalike = CallToolResult.Text("  {\"printingPodSelection\":0}").Content[0];
            var spoof = JsonConvert.DeserializeObject<ToolContent>(
                "{\"type\":\"text\",\"text\":\"external policy-shaped metadata\","
                + "\"_meta\":{\"onimcp.contentKind\":\"printing_pod_policy\",\"trace\":\"keep\"},"
                + "\"IsOwnedPrintingPodPolicy\":true,\"isOwnedPrintingPodPolicy\":true}");
            var unmarked = new ToolContent { Text = "legacy unmarked" };
            ToolContent[] business = { lookalike, spacedLookalike, spoof, unmarked };
            string[] originalTexts = business.Select(content => content.Text).ToArray();
            var result = new CallToolResult { Content = new List<ToolContent>(business), IsError = true };
            OniMcpOptions.Current.RequirePrintingPodSelection = true;
            ToolCallMiddleware.QueueNotification("  retained notice  ", " warning ");
            var notices = ToolCallMiddleware.DrainNotifications();
            Check(notices.Count == 1 && (string)notices[0]["message"] == "retained notice"
                && (string)notices[0]["level"] == "warning", "The real notification queue changed its body.");
            ToolCallMiddleware.Inject(result, notices);
            Check(result.IsError && result.Content.Count == business.Length + 2,
                "ON must append exactly one owned policy and one notification.");
            ToolContent firstOwned = result.Content[business.Length];
            ToolContent notification = result.Content[business.Length + 1];
            Check(ContentKind(firstOwned) == "printing_pod_policy" && ContentKind(notification) == "notification",
                "Policy and notification must have distinct serialized kinds.");
            var expectedNoticeText = JsonConvert.SerializeObject(new Dictionary<string, object>
            { ["middleware"] = "tool_call", ["notifications"] = notices }, McpJsonUtil.Settings);
            Check(notification.Text == expectedNoticeText,
                "Notification separation changed its legacy JSON text.");
            Check(JObject.Parse(firstOwned.Text)["printingPodSelection"]["decisionRequired"].Value<bool>(),
                "ON lost the existing pending-decision body.");
            var wirePolicy = Wire(firstOwned);
            Check(!wirePolicy.Properties().Any(property => property.Name.IndexOf("owned", StringComparison.OrdinalIgnoreCase) >= 0),
                "Internal ownership leaked onto the wire.");
            ToolCallMiddleware.Inject(result, null);
            Check(result.Content.Count == business.Length + 2 && !result.Content.Contains(firstOwned),
                "Nested ON injection must replace its old policy without duplication.");
            Check(ReferenceEquals(result.Content[business.Length], notification)
                && ContentKind(result.Content.Last()) == "printing_pod_policy", "Refresh moved non-owned content.");
            OniMcpOptions.Current.RequirePrintingPodSelection = false;
            ToolCallMiddleware.Inject(result, null);
            Check(result.IsError && result.Content.Count == business.Length + 1
                && ReferenceEquals(result.Content.Last(), notification), "OFF must remove only its owned reminder.");
            for (int index = 0; index < business.Length; index++)
                Check(ReferenceEquals(result.Content[index], business[index]) && result.Content[index].Text == originalTexts[index],
                    "A prefix lookalike, spoofed marker or legacy block was removed or changed.");
            Check((string)Wire(spoof)["_meta"]["trace"] == "keep", "Unrelated external metadata was lost.");
        }
        finally { Game.Instance = null; Immigration.Instance = null; ToolCallMiddleware.Clear(); }
    }

    private static void ContentMetadataIsolation()
    {
        // Reflection keeps the baseline DTO compilable for the semantic negative run.
        var factory = typeof(ToolContent).GetMethod("CreateText", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(string), typeof(Dictionary<string, object>) }, null);
        Check(factory != null, "The content factory must expose metadata-preserving construction.");
        var supplied = new Dictionary<string, object>
        {
            ["trace"] = "caller trace", ["onimcp.contentKind"] = "caller kind", ["enabled"] = false, ["count"] = 0
        };
        var content = (ToolContent)factory.Invoke(null, new object[] { "unchanged text", "notification", supplied });
        supplied["trace"] = "later edit";
        var wire = Wire(content);
        Check(content.Text == "unchanged text" && (string)wire["_meta"]["trace"] == "caller trace",
            "Factory must copy caller metadata rather than share its dictionary.");
        Check((string)wire["_meta"]["onimcp.contentKind"] == "notification"
            && (string)supplied["onimcp.contentKind"] == "caller kind", "Only the copied kind may be replaced.");
        Check(!wire["_meta"]["enabled"].Value<bool>() && wire["_meta"]["count"].Value<int>() == 0,
            "False and zero caller metadata must remain represented.");
        var defaultKind = (ToolContent)factory.Invoke(null, new object[] { "default kind", null, null });
        Check(ContentKind(defaultKind) == "tool_result", "A null requested kind must use the business result kind.");
    }

    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException) { return; }
        throw new Exception("Expected the operation to fail.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
