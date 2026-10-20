using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OniMcp.Server;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using OniMcp.Support;
using UnityEngine;

internal static class PauseTraceRegression
{
    private static int checks;
    private const string SessionA = "fixture-session-A-opaque";
    private const string SessionB = "fixture-session-B-opaque";

    internal static void Run()
    {
        TestEmptyAndValidation();
        TestProjectionAndIsolation();
        TestRingAndIndependentReaders();
        TestConcurrentRetentionAndFaults();
        TestNativeRegistrationAndRead();
        TestNativeAttributionAndNesting();
        TestNativeFaultsAndContext();
        TestReaderArgumentsAndLocalization();
        Console.WriteLine("Speed transition trace: " + checks + " host checks passed (linked product source; native boundary substituted)");
    }

    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException("Speed trace: " + message); }

    private static JObject Read(SpeedTransitionTraceStore store, int limit = 8,
        long? after = null, string epoch = null, string session = SessionA)
    {
        Dictionary<string, object> value; string reason;
        Check(store.TryRead(limit, after, epoch, session, out value, out reason), "valid read succeeds: " + reason);
        Check(reason == null && value != null, "valid read has no refusal");
        return JObject.FromObject(value);
    }

    private static void Refuse(SpeedTransitionTraceStore store, int limit,
        long? after, string epoch, string expected)
    {
        Dictionary<string, object> value; string reason;
        Check(!store.TryRead(limit, after, epoch, SessionA, out value, out reason), "invalid cursor/limit is refused");
        Check(value == null && reason == expected, "refusal keeps exact reason and no substitute result");
    }

    private static SpeedTransitionTraceStore.State State(bool? paused = true, int generation = 2)
    {
        return new SpeedTransitionTraceStore.State
        {
            Utc = "2000-01-01T00:00:00.0000000Z", MonotonicSeconds = 12.345678901,
            Frame = 7, GameAvailable = true, IsPaused = paused, InternalSpeed = 1,
            TimeScale = paused == true ? 0 : 2, GameTimeSeconds = 123.456789,
            ContextGeneration = generation, ObservedContextGeneration = generation,
            ObservationStatus = "observed"
        };
    }

    private static void TestEmptyAndValidation()
    {
        var store = new SpeedTransitionTraceStore(); var empty = Read(store);
        Guid epoch;
        Check(Guid.TryParseExact((string)empty["traceEpoch"], "N", out epoch), "epoch is opaque per-store identity");
        Check((int)empty["capacity"] == 64 && (int)empty["limit"] == 8, "retention and response budgets are distinct");
        Check(((JArray)empty["events"]).Count == 0 && empty["oldestSequence"].Type == JTokenType.Null
            && (long)empty["newestSequence"] == 0 && (long)empty["nextAfterSequence"] == 0,
            "known empty recorder has no fabricated event or native pause state");
        Check(!(bool)empty["cursorGap"] && !(bool)empty["hasMore"] && (long)empty["droppedCount"] == 0,
            "empty cursor and retention metadata remain exact");
        Check(JToken.DeepEquals(empty, Read(store)), "read does not append, drain or advance a cursor");
        foreach (int invalid in new[] { -1, 0, 33 }) Refuse(store, invalid, null, null, "invalid_limit");
        string identity = (string)empty["traceEpoch"];
        Refuse(store, 8, -1, identity, "invalid_after_sequence");
        Refuse(store, 8, 0, null, "epoch_required");
        Refuse(store, 8, 0, string.Empty, "epoch_required");
        Refuse(store, 8, 1, identity, "cursor_future");
        Refuse(store, 8, long.MaxValue, identity, "cursor_future");
        Refuse(store, 8, null, "fixture-older-process-epoch", "epoch_mismatch");
        string nextEpoch = (string)Read(new SpeedTransitionTraceStore())["traceEpoch"];
        Check(identity != nextEpoch, "a restarted recorder cannot reuse old cursors");
        Refuse(store, 8, 0, nextEpoch, "epoch_mismatch");
    }

    private static void TestProjectionAndIsolation()
    {
        var store = new SpeedTransitionTraceStore(); var before = State(); var after = State(false);
        store.Append("TogglePause", 11, null, SessionA, before, after, false, null);
        store.Append("Unpause", 12, 11, SessionB, State(), State(false), true, "FixtureNativeException");
        store.Append("OnChanged", 13, 11, null, State(null), null, false, null);
        before.GameTimeSeconds = -1; after.IsPaused = true;
        var first = Read(store); var events = (JArray)first["events"];
        Check(events.Count == 3 && (long)events[0]["callId"] == 11
            && events[0]["parentCallId"].Type == JTokenType.Null && (long)events[1]["parentCallId"] == 11,
            "actual call identities and nesting survive projection");
        Check((string)events[0]["sessionRelation"] == "same" && (string)events[1]["sessionRelation"] == "other"
            && (string)events[2]["sessionRelation"] == "unattributed", "reader-relative attribution is explicit");
        Check((double)events[0]["before"]["gameTimeSeconds"] == 123.456789
            && (bool)events[0]["after"]["isPaused"] == false, "append copies snapshots before caller mutation");
        Check((double)events[0]["before"]["monotonicSeconds"] == 12.345678901,
            "native and monotonic clock values are not rounded into coarse status");
        Check(events[2]["before"]["isPaused"].Type == JTokenType.Null && events[2]["after"].Type == JTokenType.Null,
            "unknown observations remain nullable");
        Check((bool)events[1]["threw"] && (string)events[1]["exceptionType"] == "FixtureNativeException",
            "exception classification is kept without a raw message");
        var other = (JArray)Read(store, session: SessionB)["events"];
        Check((string)other[0]["sessionRelation"] == "other" && (string)other[1]["sessionRelation"] == "same",
            "second reader sees relative attribution without changing retained records");
        var unknownReader = (JArray)Read(store, session: null)["events"];
        Check((string)unknownReader[0]["sessionRelation"] == "unknown"
            && (string)unknownReader[2]["sessionRelation"] == "unattributed", "missing reader identity is not guessed");
        string json = first.ToString();
        Check(!json.Contains(SessionA) && !json.Contains(SessionB) && !json.Contains("sessionId"),
            "raw session identifiers do not leave the store");
        var projected = (Dictionary<string, object>)null; string reason;
        Check(store.TryRead(8, null, null, SessionA, out projected, out reason), "detached response read succeeds");
        var rows = (List<Dictionary<string, object>>)projected["events"];
        ((Dictionary<string, object>)rows[0]["before"])["isPaused"] = false; rows.Clear();
        Check(JToken.DeepEquals(first, Read(store)), "mutating a returned projection cannot mutate retained records");
    }

    private static void TestRingAndIndependentReaders()
    {
        var store = new SpeedTransitionTraceStore();
        for (int n = 1; n <= 73; n++) store.Append("Pause", n, null, SessionA, State(), State(), false, null);
        var recent = Read(store); string epoch = (string)recent["traceEpoch"];
        Check((long)recent["oldestSequence"] == 10 && (long)recent["newestSequence"] == 73
            && (long)recent["droppedCount"] == 9, "bounded ring reports actual eviction count");
        var tail = (JArray)recent["events"];
        Check(tail.Count == 8 && (long)tail[0]["sequence"] == 66 && (long)tail[7]["sequence"] == 73,
            "uncursored reads return only the latest limited events");
        var page = Read(store, after: 0, epoch: epoch); var rows = (JArray)page["events"];
        Check((bool)page["cursorGap"] && (bool)page["hasMore"] && (long)page["nextAfterSequence"] == 17,
            "old cursors report a gap and preserve the remaining page");
        Check(rows.Count == 8 && (long)rows[0]["sequence"] == 10 && (long)rows[7]["sequence"] == 17,
            "cursor resumes at the oldest retained event, without fabricated missing data");
        Check(JToken.DeepEquals(page, Read(store, after: 0, epoch: epoch)), "reader cursors are caller-owned and non-consuming");
        var second = Read(store, after: 17, epoch: epoch);
        Check(!(bool)second["cursorGap"] && (long)second["events"][0]["sequence"] == 18,
            "next page preserves chronological ordering");
        var empty = Read(store, after: 73, epoch: epoch);
        Check(((JArray)empty["events"]).Count == 0 && !(bool)empty["hasMore"] && (long)empty["nextAfterSequence"] == 73,
            "caught-up cursor reports known no new events");
        Check(((JArray)Read(store, 32)["events"]).Count == 32 && ((JArray)Read(store, 1)["events"]).Count == 1,
            "caller can narrow or expand only within the hard output budget");
    }

    private static void TestConcurrentRetentionAndFaults()
    {
        var store = new SpeedTransitionTraceStore();
        Parallel.For(0, 128, n => store.Append("OnChanged", n + 1, null, null, State(), State(), false, null));
        var result = Read(store, 32);
        Check((long)result["newestSequence"] == 128 && (long)result["oldestSequence"] == 65
            && (long)result["droppedCount"] == 64, "concurrent appends retain exactly the bounded newest range");
        var rows = (JArray)result["events"];
        for (int i = 0; i < rows.Count; i++) Check((long)rows[i]["sequence"] == 97 + i, "retained sequence is contiguous");
        store.ObserverFault(); store.ObserverFault();
        var faulted = Read(store);
        Check((long)faulted["observerFaultCount"] == 2 && (string)faulted["observationStatus"] == "observer_fault",
            "observer faults remain visible independently of native exceptions");
        Check((long)faulted["newestSequence"] == 128, "recording an observer fault is not a fake native call");
    }

    private static SpeedControlScreen SetupNative()
    {
        Game.ThrowOnRead = false; Game.Instance = new Game();
        GameClock.Instance = new GameClock();
        Time.frameCount = 7; Time.timeScale = 0;
        GameContextLifecycle.Generation = 10;
        McpHttpServer.ThrowOnSessionRead = false; McpHttpServer.CurrentSessionId = SessionA;
        SpeedControlScreen.Instance = new SpeedControlScreen { PauseCount = 1, InternalSpeed = 1 };
        return SpeedControlScreen.Instance;
    }

    private static JObject Trace(JObject extra = null)
    {
        var args = new JObject { ["domain"] = "speed", ["action"] = "trace", ["task"] = "speed observer fixture" };
        if (extra != null) foreach (var item in extra) args[item.Key] = item.Value.DeepClone();
        string unchanged = args.ToString();
        var result = GameControlTools.ControlGame().Handler(args);
        Check(!result.IsError && result.Content.Count == 1, "real game/speed/trace reader returns one primary body");
        Check(args.ToString() == unchanged, "reader and routing do not rewrite caller arguments");
        return JObject.Parse(result.Content[0].Text);
    }

    private static Harmony.Registration Registration(string method)
    { return Harmony.Registrations.Single(item => item.Original.Name == method); }

    private static Exception InvokeNative(string method, SpeedControlScreen instance, System.Action body)
    {
        var patch = Registration(method);
        object[] prefix = { instance, patch.Original, null };
        Check(patch.Prefix.method.Invoke(null, prefix) == null, "observer prefix returns void and cannot skip native work");
        Exception original = null;
        try { body(); }
        catch (Exception error) { original = error; }
        object final = patch.Finalizer.method.Invoke(null, new object[] { instance, prefix[2], original });
        Check(ReferenceEquals(final, original), "observer finalizer preserves the original exception identity");
        return final as Exception;
    }

    private static void NoNativeMutation(SpeedControlScreen speed, int pause, int unpause, int setSpeed, int changed, string reason)
    {
        Check(speed.PauseCalls == pause && speed.UnpauseCalls == unpause && speed.SetSpeedCalls == setSpeed
            && speed.ChangedCalls == changed, reason + ": no native mutation callback");
    }

    private static void TestNativeRegistrationAndRead()
    {
        var speed = SetupNative(); var empty = Trace();
        Check(!(bool)empty["available"] && !(bool)empty["completeCoverage"], "uninstalled observer is distinct from an installed recorder with no events");
        Check(((JArray)empty["events"]).Count == 0 && (long)empty["newestSequence"] == 0,
            "read before installation does not manufacture an event");
        NoNativeMutation(speed, 0, 0, 0, 0, "uninstalled observer read");
        OniMcp.SpeedTransitionTracePatch.EnsureInstalled(null);
        Check(Harmony.Registrations.Count == 0, "missing Harmony does not install a partial substitute");
        Harmony.ThrowOnMethod = "Unpause";
        OniMcp.SpeedTransitionTracePatch.EnsureInstalled(new Harmony("fixture-speed-observer"));
        var partial = Trace();
        Check((bool)partial["available"] && !(bool)partial["completeCoverage"]
            && (string)partial["coverage"]["Unpause"] == "patch_failed", "partial hook availability is visible without aborting other hooks");
        Check(Harmony.Registrations.Count == 4, "one observer installation failure does not abort remaining native methods");
        Harmony.ThrowOnMethod = null;
        OniMcp.SpeedTransitionTracePatch.EnsureInstalled(new Harmony("fixture-speed-observer"));
        Check(Harmony.Registrations.Count == 5, "only missing observer registration is retried");
        OniMcp.SpeedTransitionTracePatch.EnsureInstalled(new Harmony("fixture-speed-observer"));
        Check(Harmony.Registrations.Count == 5, "repeated setup does not duplicate native callbacks");
        var ready = Trace();
        Check((bool)ready["available"] && (bool)ready["completeCoverage"] && ready["reasonCode"].Type == JTokenType.Null,
            "complete native method coverage is distinct from partial and absent");
        foreach (var item in Harmony.Registrations)
        {
            Check(item.Prefix != null && item.Finalizer != null && item.Postfix == null && item.Transpiler == null,
                "observer registration uses only prefix/finalizer, never a native implementation replacement");
            Check(item.Prefix.method.ReturnType == typeof(void) && item.Finalizer.method.ReturnType == typeof(Exception),
                "Harmony observer cannot suppress native execution or erase the original exception");
        }
        Check((bool)ready["currentState"]["isPaused"] && (int)ready["currentState"]["internalSpeed"] == 1
            && Math.Abs((double)ready["currentState"]["gameTimeSeconds"] - GameClock.Instance.TimeValue) < 0.00000001,
            "current native state and raw game clock are returned from getters");
        long sequence = (long)ready["newestSequence"];
        Trace(); Trace();
        Check((long)Trace()["newestSequence"] == sequence, "reads do not append observer events");
        NoNativeMutation(speed, 0, 0, 0, 0, "installed reader");
    }

    private static void TestNativeAttributionAndNesting()
    {
        var speed = SetupNative(); var cursor = Trace();
        string epoch = (string)cursor["traceEpoch"]; long sequence = (long)cursor["newestSequence"];
        InvokeNative("Unpause", speed, () =>
        {
            speed.Unpause(false);
            InvokeNative("SetSpeed", speed, () => speed.SetSpeed(2));
            InvokeNative("OnChanged", speed, () =>
            {
                ((MethodInfo)Registration("OnChanged").Original).Invoke(speed, null);
                Time.timeScale = 3; Time.frameCount = 8;
            });
        });
        McpHttpServer.CurrentSessionId = SessionB;
        InvokeNative("Pause", speed, () => { speed.Pause(false); Time.timeScale = 0; });
        McpHttpServer.CurrentSessionId = null;
        InvokeNative("ToggleRidiculousSpeed", speed, () => speed.ToggleRidiculousSpeed());
        McpHttpServer.CurrentSessionId = SessionA;
        var page = Trace(new JObject { ["afterSequence"] = sequence, ["traceEpoch"] = epoch, ["limit"] = 8 });
        var events = (JArray)page["events"];
        Check(events.Count == 5, "each observed native method produces one completed-call record");
        JObject outer = events.OfType<JObject>().Single(item => (string)item["method"] == "Unpause");
        Check(outer["parentCallId"].Type == JTokenType.Null && (bool)outer["before"]["isPaused"]
            && !(bool)outer["after"]["isPaused"], "native outer transition retains its independent before/after state");
        foreach (var child in events.OfType<JObject>().Where(item => (string)item["method"] == "SetSpeed" || (string)item["method"] == "OnChanged"))
            Check((long)child["parentCallId"] == (long)outer["callId"], "nested native callbacks retain their enclosing call ID");
        Check((string)outer["sessionRelation"] == "same"
            && (string)events[3]["sessionRelation"] == "other" && (string)events[4]["sessionRelation"] == "unattributed",
            "observed call session is projected as same/other/unattributed");
        McpHttpServer.CurrentSessionId = SessionB;
        var second = Trace(new JObject { ["afterSequence"] = sequence, ["traceEpoch"] = epoch, ["limit"] = 8 });
        Check((string)second["events"][2]["sessionRelation"] == "other" && (string)second["events"][3]["sessionRelation"] == "same",
            "second actual reader projects its own session without consuming the first reader cursor");
        Check((long)second["newestSequence"] == (long)page["newestSequence"] && (string)second["traceEpoch"] == epoch,
            "multiple clients do not reset recorder epoch or event sequence");
        string text = page.ToString();
        Check(!text.Contains(SessionA) && !text.Contains(SessionB), "wire reader never leaks session identifiers");
        McpHttpServer.CurrentSessionId = SessionA;
        var pending = SpeedTransitionTrace.Begin(speed, Registration("Pause").Original);
        McpHttpServer.CurrentSessionId = SessionB;
        speed.Pause(false); SpeedTransitionTrace.End(pending, speed, null);
        McpHttpServer.CurrentSessionId = SessionA;
        Check((string)Trace()["events"].Last["sessionRelation"] == "same", "request attribution is captured at native entry, not response time");
    }

    private static void TestNativeFaultsAndContext()
    {
        var speed = SetupNative(); var baseline = Trace();
        var original = new InvalidOperationException("fixture private native exception message");
        Exception after = InvokeNative("Pause", speed, () => { throw original; });
        Check(ReferenceEquals(after, original), "native failure remains the same exception object");
        var failure = Trace(); var row = (JObject)failure["events"].Last;
        Check((bool)row["threw"] && ((string)row["exceptionType"]).EndsWith("InvalidOperationException")
            && !row.ToString().Contains(original.Message), "native exception records contain a type, not a raw private message");
        long beforeFault = (long)failure["observerFaultCount"];
        speed.ThrowOnSpeedRead = true;
        int calls = speed.PauseCalls;
        InvokeNative("Pause", speed, () => speed.Pause(false));
        Check(ReferenceEquals(InvokeNative("Pause", speed, () => { throw original; }), original),
            "native exception identity survives a simultaneous observer getter fault");
        speed.ThrowOnSpeedRead = false;
        var observerFault = Trace(); row = (JObject)observerFault["events"].Reverse().Skip(1).First();
        Check(speed.PauseCalls == calls + 1 && !(bool)row["threw"], "observer getter failure neither skips native work nor becomes its exception");
        Check((long)observerFault["observerFaultCount"] > beforeFault
            && row["before"]["internalSpeed"].Type == JTokenType.Null
            && (string)row["before"]["observationStatus"] == "observer_fault", "native observation fault remains missing and separately counted");
        long beforeMissingContext = (long)observerFault["newestSequence"];
        McpHttpServer.ThrowOnSessionRead = true;
        calls = speed.PauseCalls;
        Check(InvokeNative("Pause", speed, () => speed.Pause(false)) == null, "observer entry failure does not create an exception for a successful native method");
        McpHttpServer.ThrowOnSessionRead = false;
        Check(speed.PauseCalls == calls + 1 && (long)Trace()["newestSequence"] == beforeMissingContext,
            "failed observer entry does not create a fake native event or retry the native body");
        GameClock.Instance.ThrowOnRead = true;
        var unknownClock = Trace();
        Check(unknownClock["currentState"]["gameTimeSeconds"].Type == JTokenType.Null
            && (string)unknownClock["currentState"]["observationStatus"] == "observer_fault", "clock getter failure is not a fabricated zero");
        GameClock.Instance.ThrowOnRead = false; GameClock.Instance.TimeValue = float.NaN; Time.timeScale = float.PositiveInfinity;
        var invalidClock = Trace();
        Check(invalidClock["currentState"]["gameTimeSeconds"].Type == JTokenType.Null
            && invalidClock["currentState"]["timeScale"].Type == JTokenType.Null, "non-finite native clocks and scale remain valid JSON nulls");
        GameClock.Instance.TimeValue = 100; Time.timeScale = 0;
        int generation = (int)Trace()["currentState"]["observedContextGeneration"];
        Game.Instance = new Game(); GameContextLifecycle.Generation = 11;
        var replaced = Trace();
        Check((int)replaced["currentState"]["observedContextGeneration"] > generation
            && (int)replaced["currentState"]["contextGeneration"] == 11, "native object generation and existing lifecycle generation remain distinct");
        string epoch = (string)replaced["traceEpoch"];
        Game.Instance = null; SpeedControlScreen.Instance = null;
        var menu = Trace();
        Check(!(bool)menu["currentState"]["gameAvailable"] && menu["currentState"]["isPaused"].Type == JTokenType.Null
            && menu["currentState"]["gameTimeSeconds"].Type == JTokenType.Null, "menu/missing native UI is not a false pause or zero game clock");
        Check((string)menu["traceEpoch"] == epoch, "scene changes do not masquerade as process recorder restarts");
        Game.ThrowOnRead = true;
        Check(Trace()["currentState"]["gameAvailable"].Type == JTokenType.Null, "unknown Game singleton is distinct from confirmed menu state");
        Game.ThrowOnRead = false;
    }

    private static void TestReaderArgumentsAndLocalization()
    {
        SetupNative(); McpLocalization.LanguageProvider = () => "en";
        var trace = Trace(); long newest = (long)trace["newestSequence"]; string epoch = (string)trace["traceEpoch"];
        foreach (string input in new[] { "{limit:0}", "{limit:33}", "{limit:true}", "{limit:'8'}", "{limit:1.5}", "{limit:null}",
            "{afterSequence:-1}", "{afterSequence:true}", "{afterSequence:'0'}", "{afterSequence:0.5}", "{afterSequence:null}",
            "{afterSequence:9223372036854775808}", "{afterSequence:0}", "{traceEpoch:1}", "{traceEpoch:''}" })
        {
            var args = JObject.Parse(input); args["domain"] = "speed"; args["action"] = "trace";
            var result = GameControlTools.ControlGame().Handler(args);
            Check(result.IsError && result.Content.Count == 1 && !string.IsNullOrWhiteSpace(result.Content[0].Text),
                "typed reader validation rejects malformed budget/cursor/epoch: " + input);
        }
        var future = GameControlTools.ControlGame().Handler(new JObject { ["domain"] = "speed", ["action"] = "trace",
            ["afterSequence"] = newest + 1, ["traceEpoch"] = epoch });
        Check(future.IsError && future.Content[0].Text == McpLocalization.Get("speed_trace.error.cursor_future"), "future cursors are not silently reset");
        var caughtUp = Trace(new JObject { ["afterSequence"] = newest, ["traceEpoch"] = epoch });
        Check(((JArray)caughtUp["events"]).Count == 0 && !(bool)caughtUp["hasMore"], "reader distinguishes known no new events from unavailable coverage");
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            McpLocalization.LanguageProvider = () => language;
            var error = SpeedTransitionTrace.Read(new JObject { ["limit"] = 0 });
            string expected = McpLocalization.Get("speed_trace.error.invalid_limit");
            Check(error.IsError && error.Content[0].Text == expected && expected != "speed_trace.error.invalid_limit",
                "reader localized refusal exists in runtime catalog: " + language);
        }
        McpLocalization.LanguageProvider = () => "en";
    }
}
