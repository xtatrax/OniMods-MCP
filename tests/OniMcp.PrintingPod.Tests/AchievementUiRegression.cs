using System;
using System.Collections.Generic;
using System.Linq;
using Database;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using UnityEngine;

// Runs the real observer and real colony reader. UI stubs are data and forbidden-action sentinels.
internal static class AchievementUiRegression
{
    private static int checks;
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new Exception("achievement_ui_read:" + message);
    }
    private static void Null(JToken value, string message) => Check(value != null && value.Type == JTokenType.Null, message);
    private static void Keys(JObject row, params string[] expected) =>
        Check(row.Properties().Select(p => p.Name).OrderBy(s => s).SequenceEqual(expected.OrderBy(s => s)), "unexpected UI fields");
    private sealed class Widget
    {
        internal GameObject Go;
        internal AchievementWidget Achievement;
        internal MultiToggle Toggle;
        internal ToolTip Tooltip;
    }
    private sealed class Fixture
    {
        internal readonly RetiredColonyInfoScreen Screen;
        internal readonly KScreenManager Manager;
        internal readonly GameObject Container;
        internal readonly Dictionary<string, GameObject> Entries = new Dictionary<string, GameObject>();
        internal readonly List<ToolTip> Tips = new List<ToolTip>();
        internal readonly List<KScreen> Stack = new List<KScreen>();
        internal Fixture(params string[] ids)
        {
            AchievementNativePoison.Evaluations = AchievementNativePoison.UiActions = AchievementNativePoison.StatusConstructions = 0;
            AchievementUiBoundary.NativeConstructions = 0;
            Db.GetCalls = 0; Db.ThrowOnGet = true; Db.Instance = new Db();
            Game.Instance = new Game(); GameClock.Instance = new GameClock { Cycle = 40 };
            SaveGame.Instance = new SaveGame { ColonyAchievementTracker = new ColonyAchievementTracker() };
            SaveGame.Instance.ColonyAchievementTracker.achievementsToDisplay.Add("keep-native-notification");
            foreach (string id in ids) {
                Db.Instance.ColonyAchievements.resources.Add(new ColonyAchievement {
                    Id = id, Name = id, description = id + " description", isVictoryCondition = true,
                    requirementChecklist = new List<ColonyAchievementRequirement> { new PoisonAchievementRequirement() }
                });
                SaveGame.Instance.ColonyAchievementTracker.achievements.Add(id,
                    new ColonyAchievementStatus(id) { success = false, failed = false });
            }
            Screen = new RetiredColonyInfoScreen { gameObject = new GameObject() };
            Container = new GameObject(); Container.transform.parent = Screen.transform;
            Screen.SetCacheForTest(Entries, Container);
            RetiredColonyInfoScreen.Instance = Screen;
            Manager = new KScreenManager(); KScreenManager.Instance = Manager;
            Stack.Add(Screen); Manager.SetStackForTest(Stack);
        }
        internal Widget Add(string id, int state = 2, params string[] text)
        {
            var go = new GameObject(); go.transform.parent = Container.transform;
            var widget = new Widget {
                Go = go, Achievement = go.AttachForTest(new AchievementWidget()),
                Toggle = go.AttachForTest(new MultiToggle { StateForTest = state }),
                Tooltip = go.AttachForTest(new ToolTip { Strings = text.ToList() })
            };
            Entries.Add(id, go); Tips.Add(widget.Tooltip);
            return widget;
        }
        internal JObject Read(JObject args = null)
        {
            var tracker = SaveGame.Instance.ColonyAchievementTracker;
            var cache = tracker.achievements; var entries = cache.ToArray();
            var flags = entries.Select(e => e.Value == null ? "null" : e.Value.success + "/" + e.Value.failed).ToArray();
            var queue = tracker.achievementsToDisplay; var queued = queue.ToArray();
            var uiEntries = Entries.ToArray(); var stack = Stack.ToArray();
            var textRefs = Tips.Select(t => t.Strings).ToArray(); var texts = Tips.Select(t => t.Strings.ToArray()).ToArray();
            int nativeObjects = AchievementUiBoundary.NativeConstructions;
            int statusObjects = AchievementNativePoison.StatusConstructions;
            var response = ColonyAchievementTools.Read(args ?? new JObject());
            Check(!response.IsError, "cached achievement data must remain readable when UI is unavailable");
            Check(ReferenceEquals(tracker, SaveGame.Instance.ColonyAchievementTracker) && ReferenceEquals(cache, tracker.achievements)
                && cache.SequenceEqual(entries), "UI observation changed native achievement owners/cache");
            Check(entries.Select(e => e.Value == null ? "null" : e.Value.success + "/" + e.Value.failed).SequenceEqual(flags),
                "UI observation changed cached flags");
            Check(ReferenceEquals(queue, tracker.achievementsToDisplay) && queue.SequenceEqual(queued),
                "UI observation consumed achievement notifications");
            Check(Entries.SequenceEqual(uiEntries) && Stack.SequenceEqual(stack), "UI observation changed native UI containers");
            for (int i = 0; i < Tips.Count; i++) Check(ReferenceEquals(textRefs[i], Tips[i].Strings) && Tips[i].Strings.SequenceEqual(texts[i]),
                "UI observation changed cached tooltip strings");
            Check(AchievementUiBoundary.NativeConstructions == nativeObjects && AchievementNativePoison.StatusConstructions == statusObjects,
                "UI observation constructed native objects or achievement status");
            Check(AchievementNativePoison.Evaluations == 0 && AchievementNativePoison.UiActions == 0 && Db.GetCalls == 0,
                "UI observation evaluated/initialized/opened/refreshed/clicked native state");
            return JObject.Parse(response.Content[0].Text);
        }
    }
    private static JObject Args() => new JObject { ["includeAchievementUi"] = true };
    private static JObject Observation(JObject body)
    {
        Check(body["uiObservation"] is JObject, "includeAchievementUi=true must add actual optional observer output");
        var ui = (JObject)body["uiObservation"];
        Keys(ui, "status", "reasonCode", "source", "currentColonyVerified", "screenOpen", "items");
        Check((string)ui["source"] == "existing_retired_colony_info_screen_cache", "UI cache source changed");
        Null(ui["currentColonyVerified"], "existing screen must not claim current-colony identity");
        return ui;
    }
    private static JObject Item(JObject ui, string id) => (JObject)ui["items"].Single(r => (string)r["id"] == id);
    private static void RowUnknown(JObject row, string reason)
    {
        Check((string)row["status"] == "unavailable" && (string)row["reasonCode"] == reason, "wrong unavailable row reason");
        foreach (var name in new[] { "toggleState", "tooltipStrings", "tooltipStringCount", "tooltipTruncated" })
            Null(row[name], "unavailable row must retain unknown " + name);
    }
    private static void TopUnknown(JObject ui, string reason, bool? open)
    {
        Check((string)ui["status"] == "unavailable" && (string)ui["reasonCode"] == reason, "wrong unavailable screen reason");
        Null(ui["items"], "unavailable screen must not enumerate retained widgets");
        if (open.HasValue) Check((bool?)ui["screenOpen"] == open, "wrong known screen-open state");
        else Null(ui["screenOpen"], "unobserved screen-open state must stay null");
    }
    internal static void Run()
    {
        DefaultParityAndRawObservation();
        ScreenAndStackBoundaries();
        EntryAndComponentBoundaries();
        TooltipAndTextBudgets();
        QueryAndLimitBoundObservation();
        Console.WriteLine("PASS: actual optional achievement UI observer, " + checks + " boundary checks; native rendering not exercised");
    }
    private static void DefaultParityAndRawObservation()
    {
        var f = new Fixture("A"); var widget = f.Add("A", 2, "cached status", null);
        SaveGame.Instance.ColonyAchievementTracker.achievements["A"].success = true;
        var absent = f.Read();
        var explicitFalse = f.Read(new JObject { ["includeAchievementUi"] = false });
        Check(absent.ToString(Newtonsoft.Json.Formatting.None) == explicitFalse.ToString(Newtonsoft.Json.Formatting.None)
            && absent.Property("uiObservation") == null, "false/omitted flag must preserve default bytes and omit UI key");
        Check(widget.Toggle.StateReads == 0 && widget.Tooltip.CountReads == 0 && widget.Tooltip.StringReads == 0
            && widget.Go.ComponentReads == 0, "default read touched UI widgets");
        var included = f.Read(Args()); var ui = Observation(included);
        Check((string)ui["status"] == "observed" && (bool)ui["screenOpen"] && ui["items"].Count() == 1, "known open screen observation missing");
        Null(ui["reasonCode"], "successful observation must not invent a reason");
        var row = Item(ui, "A");
        Keys(row, "id", "status", "reasonCode", "toggleState", "tooltipStrings", "tooltipStringCount", "tooltipTruncated");
        Check((string)row["status"] == "observed" && (int)row["toggleState"] == 2, "raw toggle state must be observed without cache classification");
        Check((string)row["tooltipStrings"][0] == "cached status" && row["tooltipStrings"].Count() == 2
            && (int)row["tooltipStringCount"] == 2 && !(bool)row["tooltipTruncated"], "cached tooltip strings/count not retained");
        Null(row["tooltipStrings"][1], "native null tooltip string must remain null");
        Check((string)included["items"][0]["status"] == "completed" && (bool)included["items"][0]["completed"],
            "raw toggle2 must not overwrite achievement-cache classification");
        included.Remove("uiObservation"); Check(JToken.DeepEquals(included, absent), "optional UI must not change main payload");
    }
    private static void ScreenAndStackBoundaries()
    {
        var f = new Fixture("A"); f.Add("A"); RetiredColonyInfoScreen.Instance = null;
        TopUnknown(Observation(f.Read(Args())), "screen_missing", null);
        f = new Fixture("A"); RetiredColonyInfoScreen.Instance = new DerivedRetiredColonyInfoScreen { gameObject = new GameObject() };
        TopUnknown(Observation(f.Read(Args())), "screen_missing", null);
        f = new Fixture("A"); f.Screen.Destroyed = true;
        TopUnknown(Observation(f.Read(Args())), "screen_missing", null);
        f = new Fixture("A"); var widget = f.Add("A"); f.Screen.isActiveAndEnabled = false;
        TopUnknown(Observation(f.Read(Args())), "screen_closed", false);
        Check(widget.Tooltip.CountReads == 0 && widget.Go.ComponentReads == 0, "closed screen read widget cache");
        f = new Fixture("A"); f.Screen.gameObject.activeInHierarchy = false;
        TopUnknown(Observation(f.Read(Args())), "screen_closed", false);
        f = new Fixture("A"); KScreenManager.Instance = null;
        TopUnknown(Observation(f.Read(Args())), "screen_stack_unavailable", true);
        f = new Fixture("A"); f.Manager.SetStackForTest(null);
        TopUnknown(Observation(f.Read(Args())), "screen_stack_unavailable", true);
        f = new Fixture("A"); f.Stack.Clear();
        TopUnknown(Observation(f.Read(Args())), "screen_stack_mismatch", true);
        f = new Fixture("A"); f.Stack.Add(f.Screen);
        TopUnknown(Observation(f.Read(Args())), "screen_stack_mismatch", true);
        f = new Fixture("A"); f.Stack.Add(new RetiredColonyInfoScreen { gameObject = new GameObject() });
        TopUnknown(Observation(f.Read(Args())), "screen_stack_mismatch", true);
        f = new Fixture("A"); f.Add("A"); f.Stack.Add(new RetiredColonyInfoScreen { gameObject = new GameObject(), isActiveAndEnabled = false });
        Check((string)Observation(f.Read(Args()))["status"] == "observed", "inactive second summary is not an ambiguous active owner");
        f = new Fixture("A"); f.Add("A"); f.Stack.Add(new KScreen { gameObject = new GameObject() });
        Check((string)Observation(f.Read(Args()))["status"] == "observed", "read must not invoke another screen's virtual active method");
        f = new Fixture("A"); for (int i = 0; i < 64; i++) f.Stack.Add(null);
        TopUnknown(Observation(f.Read(Args())), "screen_stack_unavailable", true);
        f = new Fixture("A"); f.Container.transform.parent = new GameObject().transform;
        TopUnknown(Observation(f.Read(Args())), "container_unavailable", true);
        f = new Fixture("A"); f.Container.Destroyed = true;
        TopUnknown(Observation(f.Read(Args())), "container_unavailable", true);
        f = new Fixture("A"); f.Screen.SetCacheForTest(null, f.Container);
        TopUnknown(Observation(f.Read(Args())), "entries_unavailable", true);
    }
    private static void EntryAndComponentBoundaries()
    {
        var f = new Fixture("A"); var empty = Item(Observation(f.Read(Args())), "A");
        Check((string)empty["status"] == "not_present", "initialized empty entries must produce known not-present row");
        foreach (var key in new[] { "toggleState", "tooltipStrings", "tooltipStringCount", "tooltipTruncated" }) Null(empty[key], "not-present field must stay null");
        f = new Fixture("A"); f.Entries["A"] = null;
        RowUnknown(Item(Observation(f.Read(Args())), "A"), "widget_missing");
        f = new Fixture("A"); var widget = f.Add("A"); widget.Go.Destroyed = true;
        RowUnknown(Item(Observation(f.Read(Args())), "A"), "widget_missing");
        f = new Fixture("A"); widget = f.Add("A"); widget.Go.activeInHierarchy = false;
        RowUnknown(Item(Observation(f.Read(Args())), "A"), "widget_hidden");
        Check(widget.Go.ComponentReads == 0, "hidden widget components must not be read");
        f = new Fixture("A"); widget = f.Add("A"); widget.Go.transform.parent = new GameObject().transform;
        RowUnknown(Item(Observation(f.Read(Args())), "A"), "widget_parent_mismatch");
        f = new Fixture("A"); f.Entries["A"] = f.Container;
        RowUnknown(Item(Observation(f.Read(Args())), "A"), "widget_parent_mismatch");
        f = new Fixture("A"); widget = f.Add("A"); widget.Go.AttachForTest(new AchievementWidget());
        RowUnknown(Item(Observation(f.Read(Args())), "A"), "widget_component_mismatch");
        f = new Fixture("A"); var go = new GameObject(); go.transform.parent = f.Container.transform;
        go.AttachForTest(new DerivedAchievementWidget()); f.Entries.Add("A", go);
        RowUnknown(Item(Observation(f.Read(Args())), "A"), "widget_component_mismatch");
        f = new Fixture("A"); widget = f.Add("A"); widget.Achievement.gameObject = new GameObject();
        RowUnknown(Item(Observation(f.Read(Args())), "A"), "widget_component_mismatch");
        f = new Fixture("A"); widget = f.Add("A"); widget.Go.AttachForTest(new MultiToggle());
        var partial = Item(Observation(f.Read(Args())), "A");
        Check((string)partial["status"] == "partial" && (string)partial["reasonCode"] == "widget_fields_unavailable", "duplicate toggle must be partial");
        Null(partial["toggleState"], "duplicate toggle must not select an arbitrary state");
        Check(partial["tooltipStrings"] is JArray, "known tooltip should remain independently available");
        f = new Fixture("A"); widget = f.Add("A"); widget.Go.AttachForTest(new ToolTip());
        partial = Item(Observation(f.Read(Args())), "A");
        Check((string)partial["status"] == "partial" && (int)partial["toggleState"] == 2, "duplicate tooltip must preserve independent raw toggle");
        Null(partial["tooltipStrings"], "duplicate tooltip must not select arbitrary strings");
    }
    private static void TooltipAndTextBudgets()
    {
        var f = new Fixture("A"); var widget = f.Add("A"); var row = Item(Observation(f.Read(Args())), "A");
        Check(row["tooltipStrings"] is JArray array && array.Count == 0 && (int)row["tooltipStringCount"] == 0
            && !(bool)row["tooltipTruncated"], "known zero tooltip count must remain zero/empty");
        f = new Fixture("A"); widget = f.Add("A", 2, Enumerable.Range(0, 9).Select(i => "s" + i).ToArray());
        row = Item(Observation(f.Read(Args())), "A");
        Check(row["tooltipStrings"].Count() == 8 && (int)row["tooltipStringCount"] == 9 && (bool)row["tooltipTruncated"]
            && widget.Tooltip.StringReads == 8, "tooltip string count cap must be bounded with original count");
        f = new Fixture("A"); widget = f.Add("A", 2, new string('x', 1025));
        row = Item(Observation(f.Read(Args())), "A");
        Check(((string)row["tooltipStrings"][0]).Length == 1024 && (bool)row["tooltipTruncated"], "per-string cap must report truncation");
        f = new Fixture("A"); widget = f.Add("A", 2, new string('x', 1023) + "\uD83D\uDE00z");
        row = Item(Observation(f.Read(Args())), "A");
        string bounded = (string)row["tooltipStrings"][0];
        Check(bounded.Length == 1023 && !char.IsHighSurrogate(bounded[bounded.Length - 1]) && (bool)row["tooltipTruncated"],
            "per-string cut must not split a valid UTF16 surrogate pair");
        f = new Fixture("A", "B");
        f.Add("A", 2, Enumerable.Repeat(new string('x', 1024), 8).ToArray());
        var later = f.Add("B", 2, "never copied after shared budget");
        var ui = Observation(f.Read(Args())); row = Item(ui, "B");
        int length = ui["items"].SelectMany(i => i["tooltipStrings"]).Sum(t => t.Type == JTokenType.Null ? 0 : ((string)t).Length);
        Check(length == 8192 && row["tooltipStrings"].Count() == 0 && (bool)row["tooltipTruncated"]
            && (int)row["tooltipStringCount"] == 1 && later.Tooltip.StringReads == 0, "shared string budget must bound all returned rows");
        f = new Fixture("A"); widget = f.Add("A"); widget.Tooltip.CountOverride = -1;
        row = Item(Observation(f.Read(Args())), "A");
        Check((string)row["status"] == "unavailable" && (string)row["reasonCode"] == "tooltip_unavailable", "negative tooltip count unavailable");
        Null(row["tooltipStrings"], "invalid count must not become empty list");
        f = new Fixture("A"); widget = f.Add("A", 2, "cache"); widget.Tooltip.ThrowCount = true;
        Check((string)Item(Observation(f.Read(Args())), "A")["reasonCode"] == "widget_observation_unavailable", "count fault must be contained");
        f = new Fixture("A"); widget = f.Add("A", 2, "cache"); widget.Tooltip.ThrowStringIndex = 0;
        Check((string)Item(Observation(f.Read(Args())), "A")["reasonCode"] == "widget_observation_unavailable", "string fault must be contained");
    }
    private static void QueryAndLimitBoundObservation()
    {
        var f = new Fixture("A", "B", "C");
        var a = f.Add("A", 2, "A"); var b = f.Add("B", 2, "B"); var c = f.Add("C", 2, "C");
        a.Go.ThrowComponentReads = c.Go.ThrowComponentReads = true;
        var result = f.Read(new JObject { ["includeAchievementUi"] = true, ["query"] = "B", ["limit"] = 1 });
        var ui = Observation(result);
        Check((int)result["matched"] == 1 && (int)result["returned"] == 1 && ui["items"].Count() == 1
            && (string)ui["items"][0]["id"] == "B" && a.Go.ComponentReads == 0 && c.Go.ComponentReads == 0,
            "UI observation must use query-filtered returned IDs only");
        result = f.Read(new JObject { ["includeAchievementUi"] = true, ["limit"] = 1 });
        ui = Observation(result);
        Check((int)result["matched"] == 3 && (int)result["returned"] == 1 && (bool)result["truncated"]
            && ui["items"].Count() == 1 && (string)ui["items"][0]["id"] == "A" && c.Go.ComponentReads == 0,
            "UI observation must use limited returned IDs only");
        result = f.Read(new JObject { ["includeAchievementUi"] = true, ["query"] = "NO_MATCH" });
        ui = Observation(result);
        Check((int)result["returned"] == 0 && ui["items"] is JArray rows && rows.Count == 0,
            "known empty returned selection must not enumerate the screen cache");
    }
}
