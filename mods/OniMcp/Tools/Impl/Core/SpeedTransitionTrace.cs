using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Server;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    internal static class SpeedTransitionTrace
    {
        internal static readonly string[] Methods = { "Pause", "Unpause", "SetSpeed", "ToggleRidiculousSpeed", "OnChanged" };
        private static readonly SpeedTransitionTraceStore Store = new SpeedTransitionTraceStore();
        private static readonly Dictionary<string, string> Coverage = new Dictionary<string, string>();
        private static readonly object CoverageSync = new object();
        private static long callSequence;
        private static WeakReference observedGame;
        private static bool observedContext;
        private static int observedContextGeneration;
        [ThreadStatic] private static Call current;

        internal sealed class Call
        {
            internal long Id;
            internal Call Parent;
            internal string Method, Session;
            internal SpeedTransitionTraceStore.State Before;
        }

        internal static void SetCoverage(string method, string status)
        {
            try { lock (CoverageSync) Coverage[method] = status; }
            catch { Fault(); }
        }

        internal static Call Begin(SpeedControlScreen instance, MethodBase method)
        {
            try
            {
                var call = new Call
                {
                    Id = Interlocked.Increment(ref callSequence), Parent = current,
                    Method = method.Name, Session = McpHttpServer.CurrentSessionId,
                    Before = Capture(instance)
                };
                current = call;
                return call;
            }
            catch { Fault(); return null; }
        }

        internal static void End(Call call, SpeedControlScreen instance, Exception error)
        {
            if (call == null) return;
            try
            {
                Store.Append(call.Method, call.Id, call.Parent == null ? (long?)null : call.Parent.Id,
                    call.Session, call.Before, Capture(instance), error != null, error?.GetType().FullName);
            }
            catch { Fault(); }
            finally { current = call.Parent; }
        }

        private static void Fault()
        {
            try { Store.ObserverFault(); }
            catch { }
        }

        private static void ObserveContext(Game game)
        {
            if (!observedContext || !ReferenceEquals(observedGame?.Target, game))
            {
                observedContextGeneration++;
                observedContext = true;
                observedGame = game == null ? null : new WeakReference(game);
            }
        }

        private static double? Finite(double value) => double.IsNaN(value) || double.IsInfinity(value) ? (double?)null : value;

        private static SpeedTransitionTraceStore.State Capture(SpeedControlScreen instance)
        {
            var state = new SpeedTransitionTraceStore.State();
            bool failed = false;
            try
            {
                state.Utc = System.DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                state.MonotonicSeconds = Finite((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
                state.Frame = Time.frameCount;
                state.TimeScale = Finite(Time.timeScale);
            }
            catch { failed = true; }
            try
            {
                Game game = Game.Instance;
                ObserveContext(game);
                state.GameAvailable = game != null;
                state.ContextGeneration = GameContextLifecycle.CaptureGeneration();
                state.ObservedContextGeneration = observedContextGeneration;
                if (game != null && GameClock.Instance != null)
                    state.GameTimeSeconds = Finite(GameClock.Instance.GetTime());
            }
            catch { failed = true; }
            try
            {
                if (instance != null)
                {
                    state.IsPaused = instance.IsPaused;
                    state.InternalSpeed = instance.GetSpeed();
                }
            }
            catch { failed = true; }
            state.ObservationStatus = failed ? "observer_fault"
                : instance == null ? "native_state_unavailable" : "observed";
            if (failed) Fault();
            return state;
        }

        internal static CallToolResult Read(JObject args)
        {
            int limit = SpeedTransitionTraceStore.DefaultLimit;
            long? after = null;
            string epoch = null;
            JToken token = args?["limit"];
            if (token != null && (token.Type != JTokenType.Integer
                || !int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out limit)))
                return Error("invalid_limit");
            token = args?["afterSequence"];
            if (token != null)
            {
                long parsed;
                if (token.Type != JTokenType.Integer || !long.TryParse(token.ToString(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out parsed) || parsed < 0)
                    return Error("invalid_after_sequence");
                after = parsed;
            }
            token = args?["traceEpoch"];
            if (token != null)
            {
                if (token.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)token))
                    return Error("epoch_mismatch");
                epoch = (string)token;
            }
            Dictionary<string, object> result;
            string reason;
            if (!Store.TryRead(limit, after, epoch, McpHttpServer.CurrentSessionId, out result, out reason))
                return Error(reason);
            var coverage = new Dictionary<string, string>();
            int installed = 0;
            lock (CoverageSync)
            {
                foreach (string method in Methods)
                {
                    string status;
                    if (!Coverage.TryGetValue(method, out status)) status = "not_installed";
                    coverage[method] = status;
                    if (status == "installed") installed++;
                }
            }
            result["available"] = installed > 0;
            result["completeCoverage"] = installed == Methods.Length;
            result["coverage"] = coverage;
            result["reasonCode"] = installed == 0 ? "unavailable" : installed == Methods.Length ? null : "partial_coverage";
            // Observe context without refreshing native caches or changing the simulation.
            var state = Capture(SpeedControlScreen.Instance);
            result["currentState"] = state.Project();
            long faults = Store.ObserverFaultCount;
            result["observerFaultCount"] = faults;
            result["observationStatus"] = faults == 0 ? "observed" : "observer_fault";
            return CallToolResult.Text(JsonConvert.SerializeObject(JObject.FromObject(result), McpJsonUtil.Settings));
        }

        private static CallToolResult Error(string reason) => CallToolResult.Error(McpLocalization.Get("speed_trace.error." + reason));
    }
}
