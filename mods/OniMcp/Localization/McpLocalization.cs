using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace OniMcp.Support
{
    /// <summary>Localizes MCP-owned prose without changing game names or protocol values.</summary>
    public static class McpLocalization
    {
        private const string ResourcePrefix = "OniMcp.Localization.Catalogs.";
        private static readonly string[] Languages = { "en", "ja", "zh-Hans" };
        private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Catalogs =
            new Lazy<Dictionary<string, Dictionary<string, string>>>(LoadCatalogs);

        public static Func<string> LanguageProvider { get; set; }
        public static Func<string> GameLanguageProvider { get; set; }

        public static string CurrentLanguage => ResolveLanguage(
            ReadProvider(LanguageProvider), ReadProvider(GameLanguageProvider));

        public static string NormalizeLanguage(string language)
        {
            string value = (language ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();
            if (value.Length == 0 || value == "auto")
                return "auto";
            if (value == "ja" || value == "jp" || value.StartsWith("ja-", StringComparison.Ordinal))
                return "ja";
            if (value == "zh" || value == "zh-cn" || value == "zh-klei"
                || value == "zh-hans" || value.StartsWith("zh-hans-", StringComparison.Ordinal))
                return "zh-Hans";
            return "en";
        }

        public static string ResolveLanguage(string preferred, string gameLanguage)
        {
            string language = NormalizeLanguage(preferred);
            if (language == "auto")
                language = NormalizeLanguage(gameLanguage);
            return language == "auto" ? "en" : language;
        }

        public static string Get(string key, string fallback = null)
        {
            return GetForLanguage(CurrentLanguage, key, fallback);
        }

        public static string GetForLanguage(string language, string key, string fallback = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            language = ResolveLanguage(language, ReadProvider(GameLanguageProvider));
            string value;
            if (Catalogs.Value[language].TryGetValue(key, out value)
                || Catalogs.Value["en"].TryGetValue(key, out value))
                return value;
            return fallback ?? key;
        }

        public static string Format(string key, string fallback, params object[] arguments)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key, fallback), arguments);
        }

        public static Dictionary<string, string> GetGroup(string prefix, string language = null)
        {
            language = ResolveLanguage(language ?? CurrentLanguage, ReadProvider(GameLanguageProvider));
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entries in new[] { Catalogs.Value["en"], Catalogs.Value[language] })
                foreach (var entry in entries)
                    if (entry.Key.StartsWith(prefix, StringComparison.Ordinal))
                        result[entry.Key.Substring(prefix.Length)] = entry.Value;
            return result;
        }

        public static string RenderTextTemplate(string template, string language = null)
        {
            language = ResolveLanguage(language ?? CurrentLanguage, ReadProvider(GameLanguageProvider));
            return System.Text.RegularExpressions.Regex.Replace(template, @"\{\{([A-Za-z0-9_.]+)\}\}", match =>
                System.Security.SecurityElement.Escape(match.Groups[1].Value == "language" ? language
                    : GetForLanguage(language, match.Groups[1].Value)));
        }

        private static string ReadProvider(Func<string> provider)
        {
            try { return provider == null ? null : provider(); }
            catch { return null; }
        }

        private static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
        {
            var result = Languages.ToDictionary(language => language,
                _ => new Dictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);
            var assembly = typeof(McpLocalization).Assembly;
            foreach (string name in assembly.GetManifestResourceNames().OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                    continue;
                string language = Languages.FirstOrDefault(value => name.EndsWith("." + value + ".json", StringComparison.Ordinal));
                if (language == null)
                    continue;
                using (var stream = assembly.GetManifestResourceStream(name))
                using (var reader = new StreamReader(stream))
                {
                    var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
                    if (entries == null)
                        throw new InvalidOperationException("Empty localization catalog: " + name);
                    foreach (var entry in entries)
                    {
                        if (entry.Value == null || result[language].ContainsKey(entry.Key))
                            throw new InvalidOperationException("Invalid or duplicate localization key: " + name + "/" + entry.Key);
                        result[language].Add(entry.Key, entry.Value);
                    }
                }
            }
            return result;
        }
    }
}
