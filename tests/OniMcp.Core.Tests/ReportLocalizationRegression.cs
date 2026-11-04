using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class ReportLocalizationRegression
{
    private static int checks;
    private const string NativeName = "<link=OXYGEN>酸素の産出量</link>\n本体の名前";
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
    private static CallToolResult Call(JObject args = null) { return ColonyReportTools.GetColonyReport().Handler(args ?? new JObject()); }
    private static JObject Read(JObject args = null)
    {
        CallToolResult result = Call(args);
        Check(!result.IsError && result.Content.Count == 1, "Report must return one successful text result");
        return JObject.Parse(result.Content[0].Text);
    }
    private static ReportManager Fixture()
    {
        var manager = new ReportManager();
        manager.ReportGroups[ReportManager.ReportType.Header] = new ReportManager.ReportGroup
            { stringKey = "<b>基地の詳細</b>", isHeader = true, reportIfZero = true, group = 7 };
        manager.ReportGroups[ReportManager.ReportType.OxygenCreated] = new ReportManager.ReportGroup
        {
            stringKey = NativeName, group = 7,
            formatfn = value => "native-unit|" + value.ToString("R", CultureInfo.InvariantCulture),
            groupFormatfn = (value, count) => { throw new InvalidOperationException("Existing MCP formatting must not be changed to UI averages"); }
        };
        manager.ReportGroups[ReportManager.ReportType.EmptyName] = new ReportManager.ReportGroup
            { stringKey = "", formatfn = value => { throw new InvalidOperationException("Formatter unavailable fixture"); } };
        manager.ReportGroups[ReportManager.ReportType.NullName] = new ReportManager.ReportGroup { stringKey = null };
        var main = new ReportManager.ReportEntry
        {
            reportType = ReportManager.ReportType.OxygenCreated, Net = 1.23456f, Positive = 3.5f,
            Negative = -2.26544f, accumulate = 12.34567f
        };
        main.notes.Add(new ReportManager.ReportEntry.Note { value = 1.23456f, note = "<b>本体注釈</b>\nそのまま" });
        main.contextEntries.Add(new ReportManager.ReportEntry
            { reportType = ReportManager.ReportType.OxygenCreated, context = "複製人間 A", Net = 2f, Positive = 2f });
        var today = new ReportManager.DailyReport { day = 42 };
        today.reportEntries.AddRange(new[]
        {
            new ReportManager.ReportEntry { reportType = ReportManager.ReportType.Header }, main,
            new ReportManager.ReportEntry { reportType = ReportManager.ReportType.MissingGroup, Net = 4f },
            new ReportManager.ReportEntry { reportType = ReportManager.ReportType.EmptyName, Net = 1f },
            new ReportManager.ReportEntry { reportType = ReportManager.ReportType.NullName, Net = 1f },
            new ReportManager.ReportEntry { reportType = ReportManager.ReportType.Zero }
        });
        var yesterday = new ReportManager.DailyReport { day = 41, reportEntries = today.reportEntries };
        manager.TodaysReport = today; manager.YesterdaysReport = yesterday;
        manager.reports.Add(today); manager.reports.Add(yesterday); ReportManager.Instance = manager;
        Strings.Lookups = 0;
        return manager;
    }
    private static void NativeNamesAndContracts()
    {
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            McpLocalization.LanguageProvider = () => language;
            McpLocalization.GameLanguageProvider = () => "ja";
            ReportManager manager = Fixture();
            var original = manager.TodaysReport.reportEntries;
            JObject result = Read(new JObject { ["includeNotes"] = true });
            JArray entries = (JArray)result["entries"];
            JObject row = (JObject)entries[1];
            Check((string)row["name"] == NativeName, "Native report name must be preserved without Strings lookup; MCP language=" + language);
            Check((string)entries[0]["name"] == "<b>基地の詳細</b>", "Native header name and markup must be preserved");
            Check((string)row["stringKey"] == NativeName && (string)row["type"] == "OxygenCreated", "Raw stringKey and machine type must be stable");
            Check((string)row["contexts"][0]["name"] == NativeName && (string)row["contexts"][0]["context"] == "複製人間 A", "Nested native name and context must be preserved");
            Check((string)entries[2]["name"] == "MissingGroup", "Missing group keeps enum-name fallback");
            Check((string)entries[3]["name"] == "", "Empty native name remains empty");
            Check(entries[4]["name"] == null || entries[4]["name"].Type == JTokenType.Null, "Null native name remains null");
            Check(Strings.Lookups == 0, "Resolved native labels must never be looked up again");
            Check((double)row["net"] == Math.Round(1.23456f, 3) && (double)row["positive"] == 3.5 &&
                (double)row["negative"] == Math.Round(-2.26544f, 3) && (double)row["accumulate"] == Math.Round(12.34567f, 3), "Raw values and existing three-digit rounding must be stable");
            Check((string)row["formattedNet"] == "native-unit|1.23456" &&
                (string)row["formattedPositive"] == "native-unit|3.5" &&
                (string)row["formattedNegative"] == "native-unit|-2.26544", "Native formatter units and existing signed-negative behavior must be stable");
            Check(entries[3]["formattedNet"] == null || entries[3]["formattedNet"].Type == JTokenType.Null, "Formatter failure remains null");
            Check((string)row["notes"][0]["note"] == "<b>本体注釈</b>\nそのまま" &&
                (double)row["notes"][0]["value"] == Math.Round(1.23456f, 3), "Native notes and values must be preserved");
            Check((bool)entries[0]["isHeader"] && (int)entries[0]["group"] == 7, "Header/group metadata must be stable");
            Check((int)result["day"] == 41 && (string)result["which"] == "latest" && (int)result["entryCount"] == 5, "Latest selection and zero filtering must be stable");
            Check(((JArray)result["availableDays"]).Select(day => (int)day).SequenceEqual(new[] { 41, 42 }), "Available days remain sorted");
            Check(ReferenceEquals(original, manager.TodaysReport.reportEntries) && original.Count == 6 && original[1].Negative == -2.26544f, "Read must retain cached reports and values");
        }
    }
    private static void SelectionAndOptions()
    {
        ReportManager manager = Fixture();
        Check((int)Read(new JObject { ["which"] = "today" })["day"] == 42, "Today selection");
        Check((int)Read(new JObject { ["day"] = 42, ["which"] = "yesterday" })["day"] == 42, "Explicit day takes precedence");
        JObject full = Read(new JObject { ["includeZero"] = true, ["includeContexts"] = false });
        Check((int)full["entryCount"] == 6 && full["entries"][1]["contexts"] == null && full["entries"][1]["notes"] == null, "Zero/context/note options remain explicit");
        Check((int)Read(new JObject { ["limit"] = 0 })["entryCount"] == 1, "Nonpositive limit keeps existing minimum");
        manager.YesterdaysReport = null;
        Check((int)Read()["day"] == 42, "Latest fallback to today");
        manager.TodaysReport = null;
        Check((int)Read()["day"] == 41, "Latest fallback to last cached report");
        manager = Fixture();
        manager.TodaysReport.reportEntries.Clear();
        for (int i = 0; i < 350; i++) manager.TodaysReport.reportEntries.Add(new ReportManager.ReportEntry { reportType = ReportManager.ReportType.OxygenCreated, Net = i + 1 });
        Check((int)Read()["entryCount"] == 80, "Report adapter passes default 80 through the fixture parser");
        Check((int)Read(new JObject { ["limit"] = 999 })["entryCount"] == 300, "Report adapter passes max 300 through the fixture parser");
    }
    private static void LocalizedErrors()
    {
        string[] languages = { "en", "ja", "zh-Hans" };
        string[] managerErrors = { "ReportManager not initialized", "日報マネージャーが初期化されていません。", "日报管理器尚未初始化。" };
        string[] missingErrors = { "Colony report not found", "コロニーの日報が見つかりません。", "未找到殖民地日报。" };
        for (int i = 0; i < languages.Length; i++)
        {
            string language = languages[i]; McpLocalization.LanguageProvider = () => language;
            ReportManager.Instance = null;
            CallToolResult result = Call();
            Check(result.IsError && result.Content[0].Text == managerErrors[i], "Manager error uses MCP locale " + language);
            Fixture(); result = Call(new JObject { ["day"] = 987654 });
            Check(result.IsError && result.Content[0].Text == missingErrors[i], "Missing report error uses MCP locale " + language);
        }
    }
    internal static void Run()
    {
        var preferred = McpLocalization.LanguageProvider;
        var game = McpLocalization.GameLanguageProvider;
        var manager = ReportManager.Instance;
        int stringLookups = Strings.Lookups;
        try
        {
            checks = 0;
            NativeNamesAndContracts();
            SelectionAndOptions();
            LocalizedErrors();
            Console.WriteLine("PASS: " + checks + " report assertions; adapter names/values/options and independent error locale");
        }
        finally
        {
            McpLocalization.LanguageProvider = preferred;
            McpLocalization.GameLanguageProvider = game;
            ReportManager.Instance = manager;
            Strings.Lookups = stringLookups;
        }
    }
}
