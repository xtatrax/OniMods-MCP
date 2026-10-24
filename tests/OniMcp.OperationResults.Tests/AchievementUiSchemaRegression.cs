using System;
using OniMcp.Support;
using OniMcp.Tools;

internal static class AchievementUiSchemaRegression
{
    internal static void Run()
    {
        var previous = McpLocalization.LanguageProvider;
        try {
            foreach (string language in new[] { "en", "ja", "zh-Hans" }) {
            McpLocalization.LanguageProvider = () => language;
            var parameters = WorldEditorTools.ControlWorldEditor().Parameters;
            if (!parameters.TryGetValue("includeAchievementUi", out var parameter)
                || parameter.Type != "boolean" || parameter.Required)
                throw new Exception("achievement_ui_schema: actual formal parameter must be optional boolean");
            string expected = McpLocalization.Get("achievement_read.include_ui");
            if (string.IsNullOrWhiteSpace(expected) || expected == "achievement_read.include_ui"
                || parameter.Description != expected)
                throw new Exception("achievement_ui_schema: actual formal description must use resolved achievement_read.include_ui");
            foreach (string suffix in new[] { "detail", "filter", "not_initialized", "invalid_detail", "invalid_filter", "coverage" }) {
                string key = "achievement_read." + suffix;
                if (string.IsNullOrWhiteSpace(McpLocalization.Get(key)) || McpLocalization.Get(key) == key)
                    throw new Exception("achievement_ui_schema: unresolved runtime key " + key + "/" + language);
            }
            }
            Console.WriteLine("PASS: actual world_editor optional achievement UI schema and localized description");
        }
        finally { McpLocalization.LanguageProvider = previous; }
    }
}
