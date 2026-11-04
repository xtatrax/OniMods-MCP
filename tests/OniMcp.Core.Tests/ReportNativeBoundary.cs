using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// Native game providers are inert data fixtures. No installed game assembly is loaded.
public sealed class ReportManager
{
    public static ReportManager Instance;
    public enum ReportType { Header, OxygenCreated, MissingGroup, EmptyName, NullName, Zero }
    public delegate string FormattingFn(float value);
    public delegate string GroupFormattingFn(float value, float count);
    public struct ReportGroup
    {
        public string stringKey;
        public FormattingFn formatfn;
        public GroupFormattingFn groupFormatfn;
        public bool isHeader;
        public bool reportIfZero;
        public int group;
    }
    public sealed class ReportEntry
    {
        public ReportType reportType;
        public string context;
        public float Net;
        public float Positive;
        public float Negative;
        public float accumulate;
        public List<ReportEntry> contextEntries = new List<ReportEntry>();
        public List<Note> notes = new List<Note>();
        public bool HasContextEntries() { return contextEntries.Count != 0; }
        public void IterateNotes(Action<Note> visitor) { foreach (var note in notes) visitor(note); }
        public struct Note { public float value; public string note; }
    }
    public sealed class DailyReport { public int day; public List<ReportEntry> reportEntries = new List<ReportEntry>(); }
    public Dictionary<ReportType, ReportGroup> ReportGroups = new Dictionary<ReportType, ReportGroup>();
    public List<DailyReport> reports = new List<DailyReport>();
    public DailyReport TodaysReport;
    public DailyReport YesterdaysReport;
    public DailyReport FindReport(int day) { return reports.Find(report => report.day == day); }
}
public static class Strings
{
    public static int Lookups;
    public static object Get(string key) { Lookups++; return "MISSING.STRINGS:" + key; }
}
public sealed class Tuple<TFirst, TSecond> { public TFirst first; public TSecond second; }
public static class RetireColonyUtility
{
    public static RetiredColonyData GetCurrentColonyRetiredColonyData() { throw new InvalidOperationException("Report read must not query retirement summary"); }
}
public sealed class RetiredColonyData
{
    public string colonyName;
    public int cycleCount;
    public string date;
    public string startWorld;
    public Dictionary<string, string> worldIdentities;
    public string[] achievements;
    public RetiredDuplicantData[] Duplicants;
    public List<Tuple<string, int>> buildings;
    public RetiredColonyStatistic[] Stats;
    public sealed class RetiredDuplicantData
    {
        public string name; public int age; public int skillPointsGained; public Dictionary<string, string> accessories;
    }
    public sealed class RetiredColonyStatistic
    {
        public string id; public string name; public string nameX; public string nameY; public Tuple<float, float>[] value;
        public Tuple<float, float> GetByMaxValue() { throw new InvalidOperationException("Summary path was not requested"); }
        public Tuple<float, float> GetByMaxKey() { throw new InvalidOperationException("Summary path was not requested"); }
    }
}
public sealed class Db
{
    public sealed class Achievement { public string Name; public string description; }
    public sealed class AchievementCollection { public Achievement TryGet(string id) { throw new InvalidOperationException("Achievement path was not requested"); } }
    public AchievementCollection ColonyAchievements;
    public static Db Get() { throw new InvalidOperationException("Report read must not query Db"); }
}
namespace OniMcp.Tools
{
    // Argument parsing is a host fixture, not the production ToolUtil implementation.
    internal static class ToolUtil
    {
        public static int? GetInt(JObject args, string key)
        {
            int value; return args[key] != null && int.TryParse(args[key].ToString(), out value) ? value : (int?)null;
        }
        public static bool GetBool(JObject args, string key, bool fallback)
        {
            bool value; return args[key] != null && bool.TryParse(args[key].ToString(), out value) ? value : fallback;
        }
        public static int ClampLimit(JObject args, int fallback, int maximum)
        {
            int? value = GetInt(args, "limit"); return value.HasValue ? Math.Max(1, Math.Min(value.Value, maximum)) : fallback;
        }
    }
}
