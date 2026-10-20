using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    // This store has no native dependencies. Only projected records leave it.
    internal sealed class SpeedTransitionTraceStore
    {
        internal const int Capacity = 64;
        internal const int DefaultLimit = 8;
        internal const int MaximumLimit = 32;
        private readonly object sync = new object();
        private readonly TraceRecord[] records = new TraceRecord[Capacity];
        private readonly string epoch = Guid.NewGuid().ToString("N");
        private long sequence;
        private long observerFaults;

        internal sealed class State
        {
            internal string Utc;
            internal double? MonotonicSeconds;
            internal int? Frame;
            internal bool? GameAvailable;
            internal bool? IsPaused;
            internal int? InternalSpeed;
            internal double? TimeScale;
            internal double? GameTimeSeconds;
            internal int? ContextGeneration;
            internal int? ObservedContextGeneration;
            internal string ObservationStatus;

            internal Dictionary<string, object> Project()
            {
                return new Dictionary<string, object>
                {
                    ["utc"] = Utc, ["monotonicSeconds"] = MonotonicSeconds,
                    ["frame"] = Frame, ["gameAvailable"] = GameAvailable,
                    ["isPaused"] = IsPaused, ["internalSpeed"] = InternalSpeed,
                    ["timeScale"] = TimeScale, ["gameTimeSeconds"] = GameTimeSeconds,
                    ["contextGeneration"] = ContextGeneration,
                    ["observedContextGeneration"] = ObservedContextGeneration,
                    ["observationStatus"] = ObservationStatus ?? "unknown"
                };
            }

            internal State Copy() => (State)MemberwiseClone();
        }

        private sealed class TraceRecord
        {
            internal long Sequence, CallId;
            internal long? ParentCallId;
            internal string Method, SessionId, ExceptionType;
            internal State Before, After;
            internal bool Threw;

            internal Dictionary<string, object> Project(string readerSession)
            {
                string relation = string.IsNullOrEmpty(SessionId) ? "unattributed"
                    : string.IsNullOrEmpty(readerSession) ? "unknown"
                    : string.Equals(SessionId, readerSession, StringComparison.Ordinal) ? "same" : "other";
                return new Dictionary<string, object>
                {
                    ["sequence"] = Sequence, ["callId"] = CallId, ["parentCallId"] = ParentCallId,
                    ["method"] = Method, ["sessionRelation"] = relation,
                    ["before"] = Before?.Project(), ["after"] = After?.Project(),
                    ["threw"] = Threw, ["exceptionType"] = ExceptionType
                };
            }
        }

        internal void Append(string method, long callId, long? parentCallId, string sessionId,
            State before, State after, bool threw, string exceptionType)
        {
            lock (sync)
            {
                long next = checked(sequence + 1);
                records[(int)((next - 1) % Capacity)] = new TraceRecord
                {
                    Sequence = next, CallId = callId, ParentCallId = parentCallId,
                    Method = method, SessionId = sessionId, Before = before?.Copy(), After = after?.Copy(),
                    Threw = threw, ExceptionType = exceptionType
                };
                sequence = next;
            }
        }

        internal long ObserverFaultCount { get { lock (sync) return observerFaults; } }

        internal void ObserverFault()
        {
            lock (sync) { if (observerFaults < long.MaxValue) observerFaults++; }
        }

        internal bool TryRead(int limit, long? afterSequence, string traceEpoch, string readerSession,
            out Dictionary<string, object> result, out string reason)
        {
            result = null;
            reason = null;
            if (limit < 1 || limit > MaximumLimit) { reason = "invalid_limit"; return false; }
            if (afterSequence < 0) { reason = "invalid_after_sequence"; return false; }
            if (afterSequence.HasValue && string.IsNullOrEmpty(traceEpoch)) { reason = "epoch_required"; return false; }
            if (traceEpoch != null && !string.Equals(traceEpoch, epoch, StringComparison.Ordinal))
            { reason = "epoch_mismatch"; return false; }
            lock (sync)
            {
                if (afterSequence > sequence) { reason = "cursor_future"; return false; }
                long oldest = Math.Max(1, sequence - Capacity + 1);
                long start = afterSequence.HasValue ? Math.Max(oldest, afterSequence.Value + 1)
                    : Math.Max(oldest, sequence - limit + 1);
                long end = Math.Min(sequence, start + limit - 1);
                var items = new List<Dictionary<string, object>>();
                for (long value = start; value <= end; value++)
                    items.Add(records[(int)((value - 1) % Capacity)].Project(readerSession));
                result = new Dictionary<string, object>
                {
                    ["traceEpoch"] = epoch, ["capacity"] = Capacity, ["limit"] = limit,
                    ["oldestSequence"] = sequence == 0 ? (object)null : oldest,
                    ["newestSequence"] = sequence, ["droppedCount"] = Math.Max(0, sequence - Capacity),
                    ["cursorGap"] = afterSequence.HasValue && afterSequence.Value < oldest - 1,
                    ["hasMore"] = end < sequence,
                    ["nextAfterSequence"] = items.Count > 0 ? end : (afterSequence ?? sequence),
                    ["observerFaultCount"] = observerFaults,
                    ["observationStatus"] = observerFaults == 0 ? "observed" : "observer_fault",
                    ["events"] = items
                };
                return true;
            }
        }
    }
}
