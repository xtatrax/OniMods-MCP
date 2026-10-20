#!/usr/bin/env python3
import argparse
import json
import math
import sys
import time
import urllib.error
import urllib.request

from loopback_http import open_url
from survival_watch_runtime import (assert_target, operation_payload, decode_rpc_body, finite_number,
                                    launch_state, select_target, tool_payload, validate_snapshot)

URL = "http://localhost:8788/mcp/"
PROTOCOL = "2025-11-25"


def post(payload, session_id=None, parse=True, timeout=20):
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
        "Mcp-Protocol-Version": PROTOCOL,
    }
    if session_id:
        headers["Mcp-Session-Id"] = session_id
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(), headers=headers)
    with open_url(req, timeout=timeout) as response:
        body = response.read()
        return response, decode_rpc_body(body) if parse and body else None


def call_tool(session_id, name, arguments, timeout=30):
    arguments = dict(arguments)
    task = "survival_watch." + name + "." + str(arguments.get("domain", "world" if name == "world_editor" else "")) + "." + str(arguments.get("action", arguments.get("command", "")))
    arguments.setdefault("task", task)
    _, data = post(
        {"jsonrpc": "2.0", "id": int(time.time() * 1000) % 1_000_000,
         "method": "tools/call", "params": {"name": name, "arguments": arguments}},
        session_id=session_id, timeout=timeout,
    )
    return tool_payload(data)


def open_session():
    response, init = post(
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": PROTOCOL,
                "capabilities": {},
                "clientInfo": {"name": "oni-survival-watch", "version": "1"},
            },
        }
    )
    if (not isinstance(init, dict) or "error" in init or not isinstance(init.get("result"), dict)
            or init["result"].get("protocolVersion") != PROTOCOL):
        raise RuntimeError("watch_protocol_mismatch")
    session_id = response.headers.get("Mcp-Session-Id")
    if not isinstance(session_id, str) or not session_id.strip():
        raise RuntimeError("watch_session_unavailable")
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, session_id, parse=False)
    return session_id


def launch_status(session_id, limit=1):
    return launch_state(call_tool(session_id, "game_control", {"domain": "launch", "action": "status", "limit": limit}))


def prepare_target(session_id, save_index, expected_save, no_load, binding):
    status = launch_status(session_id, limit=50)
    if no_load and expected_save is None:
        raise RuntimeError("watch_expected_save_required")
    target = select_target(status, save_index, expected_save)
    binding["target"] = target
    if status["gameInitialized"]:
        return assert_target(status, target)
    if no_load:
        raise RuntimeError("watch_game_not_loaded")
    if status.get("canStart") is not True:
        raise RuntimeError("watch_load_unavailable")
    binding["loadRequested"] = True
    accepted = call_tool(session_id, "game_control", {
        "domain": "launch", "action": "start", "confirm": True, "path": target, "resume": False}, timeout=30)
    if not isinstance(accepted, dict) or accepted.get("started") is not True or accepted.get("target") != target:
        raise RuntimeError("watch_load_not_accepted")
    deadline = time.monotonic() + 60
    while time.monotonic() < deadline:
        status = launch_status(session_id)
        if status["gameInitialized"]:
            return assert_target(status, target)
        time.sleep(1)
    raise RuntimeError("watch_load_timeout")


def ensure_loaded(session_id, save_index, resume=False, speed=1, expected_save=None):
    binding = {}
    prepare_target(session_id, save_index, expected_save, False, binding)
    return snapshot(session_id)


def snapshot(session_id):
    value = call_tool(session_id, "world_editor", {
        "command": "read", "path": "/active/colony/status.oni", "profile": "minimal"})
    return validate_snapshot(value)


def diagnostic_alerts(session_id):
    line = "call domain=diagnostic action=alerts limit=20 detail=true"
    outer = call_tool(session_id, "world_editor", {
        "command": "edit", "path": "/active/ops/colony.md", "confirm": True, "dryRun": False,
        "content": "<<<<<<< SEARCH\n\n=======\n" + line + "\n>>>>>>> REPLACE"})
    alerts = operation_payload(outer)
    if not isinstance(alerts, list) or any(
            not isinstance(row, dict) or row.get("severity") not in ("info", "warning", "critical")
            or not isinstance(row.get("category"), str) or not row["category"].strip()
            or not isinstance(row.get("message"), str) for row in alerts):
        raise RuntimeError("watch_diagnostics_unknown")
    return alerts


def set_speed(session_id, speed):
    value = call_tool(session_id, "game_control", {"domain": "speed", "action": "set_speed", "speed": speed})
    if (not isinstance(value, dict)
            or any(type(value.get(key)) is not int for key in ("requestedSpeed", "speed", "internalSpeed"))
            or value.get("requestedSpeed") != speed or value.get("speed") != speed
            or value.get("internalSpeed") != speed - 1 or value.get("isPaused") is not False):
        raise RuntimeError("watch_speed_unconfirmed")
    return value


def pause(session_id):
    return call_tool(session_id, "game_control", {"domain": "speed", "action": "pause"})


def _stop_tool_call(session_id, action):
    try:
        _, data = post(
            {"jsonrpc": "2.0", "id": int(time.time() * 1000) % 1_000_000,
             "method": "tools/call", "params": {"name": "game_control", "arguments": {
                 "domain": "speed", "action": action, "task": "survival_watch.stop." + action}}},
            session_id=session_id, timeout=10,
        )
    except (OSError, RuntimeError, ValueError) as exc:
        raise RuntimeError("pause_transport_unavailable") from exc
    if not isinstance(data, dict) or "error" in data:
        raise RuntimeError("pause_rpc_error")
    result = data.get("result")
    if not isinstance(result, dict) or result.get("isError", False) is not False:
        raise RuntimeError("pause_mcp_error")
    content = result.get("content")
    first = content[0] if isinstance(content, list) and content else None
    if not isinstance(first, dict) or first.get("type") != "text" or not isinstance(first.get("text"), str):
        raise RuntimeError("pause_primary_text_unavailable")
    return first["text"]


def _finite_clock_number(value):
    return type(value) is int or (type(value) is float and math.isfinite(value))


def _stop_observation(state):
    if not isinstance(state, dict):
        return None, "unknown", "pause_time_invalid"
    paused = state.get("isPaused")
    if type(paused) is not bool:
        return None, "unknown", "pause_flag_unknown"
    speed, internal = state.get("speed"), state.get("internalSpeed")
    if type(speed) is not int or type(internal) is not int or internal not in (0, 1, 2) or speed != internal + 1:
        return None, "unknown", "pause_ui_unavailable"
    if not all(_finite_clock_number(state.get(key)) for key in ("cycle", "timeOfDayPercent")):
        return paused, "unknown", "pause_time_metadata_unknown"
    scale = state.get("timeScale")
    try:
        finite_scale = type(scale) in (int, float) and math.isfinite(scale) and scale >= 0
    except OverflowError:
        finite_scale = False
    if not finite_scale:
        return paused, "unknown", "pause_timescale_unknown"
    if paused is True and scale == 0:
        return True, "confirmed", None
    return paused, "unconfirmed", "pause_not_confirmed"


def pause_and_verify(session_id):
    outcome = {"pausedAtEnd": None, "pauseStatus": "unknown", "pauseAttempts": 0,
               "pauseIncident": False, "pauseReason": None, "postPause": None}
    for attempt in range(2):
        outcome["pauseAttempts"] += 1
        try:
            acknowledgement = _stop_tool_call(session_id, "pause")
            if not acknowledgement.strip():
                raise RuntimeError("pause_acknowledgement_empty")
            time.sleep(0.5)
            text = _stop_tool_call(session_id, "time")
        except RuntimeError as exc:
            outcome.update(pausedAtEnd=None, pauseStatus="unknown", pauseIncident=True, pauseReason=str(exc), postPause=None)
            return outcome
        try:
            state = json.loads(text, parse_constant=lambda _value: None)
        except (ValueError, RecursionError):
            state = None
        paused, status, reason = _stop_observation(state)
        outcome.update(pausedAtEnd=paused, pauseStatus=status)
        outcome["postPause"] = ({key: state.get(key) for key in
            ("cycle", "timeOfDayPercent", "timeScale", "isPaused", "speed", "internalSpeed")}
            if isinstance(state, dict) else None)
        if outcome["postPause"] is not None:
            for key in ("cycle", "timeOfDayPercent", "timeScale", "speed", "internalSpeed"):
                if not _finite_clock_number(outcome["postPause"][key]):
                    outcome["postPause"][key] = None
            if type(outcome["postPause"]["isPaused"]) is not bool:
                outcome["postPause"]["isPaused"] = None
            outcome["postPause"]["paused"] = paused
        if status == "confirmed":
            return outcome
        outcome.update(pauseIncident=True, pauseReason=reason)
    return outcome


def is_bad_snapshot(snap):
    metrics = snap.get("metrics") or {}
    if metrics.get("dupes", 1) <= 0:
        return "no duplicants alive or visible"
    if metrics.get("maxStress", 0) >= 100:
        return "max stress reached 100%"
    if metrics.get("redAlert"):
        return "red alert enabled"
    if str(snap.get("alertLevel", "")).lower() in {"red", "error", "danger", "critical"}:
        return f"alertLevel={snap.get('alertLevel')}"
    return None


def critical_diagnostic_failure(alerts):
    for alert in alerts or []:
        if str(alert.get("severity", "")).lower() == "critical":
            return f"critical diagnostic: {alert.get('category')}: {alert.get('message')}"
    return None


def compact_alerts(alerts):
    return [
        {
            "severity": alert.get("severity"),
            "category": alert.get("category"),
            "message": alert.get("message"),
        }
        for alert in (alerts or [])[:5]
    ]


def _require_no_mod_dialog(session_id):
    value = call_tool(session_id, "game_control", {"domain": "ui", "uiDomain": "dialog", "action": "status"})
    if not isinstance(value, dict) or value.get("available") is not True or type(value.get("present")) is not bool:
        raise RuntimeError("watch_dialog_unavailable")
    if value["present"] or type(value.get("count")) is not int or value.get("count") != 0 or value.get("dialogs") != []:
        raise RuntimeError("watch_dialog_blocked")


def _end_stop(session_id, binding):
    try:
        if not binding.get("target"):
            raise RuntimeError("watch_stop_identity_unknown")
        assert_target(launch_status(session_id), binding["target"])
    except (OSError, RuntimeError, ValueError) as error:
        return {"pausedAtEnd": None, "pauseStatus": "unknown", "pauseAttempts": 0,
                "pauseIncident": True, "pauseReason": "watch_stop_identity_unconfirmed", "postPause": None}
    return pause_and_verify(session_id)


def main():
    parser = argparse.ArgumentParser(description="Low-token ONI MCP survival watcher.")
    parser.add_argument("--target-cycles", type=float, default=100.0)
    parser.add_argument("--max-seconds", type=float, default=0.0, help="0 means no wall-time cap.")
    parser.add_argument("--poll-seconds", type=float, default=20.0)
    parser.add_argument("--speed", type=int, default=3, choices=[1, 2, 3])
    parser.add_argument("--save-index", type=int, default=0)
    parser.add_argument("--expected-save")
    parser.add_argument("--allow-partial", action="store_true")
    parser.add_argument("--no-load", action="store_true")
    parser.add_argument("--no-pause-at-end", action="store_true")
    parser.add_argument("--ignore-critical-diagnostics", action="store_true")
    args = parser.parse_args()
    if (not all(finite_number(value) and value >= 0 for value in (args.target_cycles, args.max_seconds))
            or not finite_number(args.poll_seconds) or args.poll_seconds <= 0):
        raise RuntimeError("watch_arguments_invalid")
    if args.no_load and args.expected_save is None:
        raise RuntimeError("watch_expected_save_required")

    session_id, binding = None, {}
    start_cycle, target_cycle = None, None
    samples, ok, failure = [], False, None
    end_stop_allowed = True
    stop = {"pausedAtEnd": None, "pauseStatus": "not_requested", "pauseAttempts": 0,
            "pauseIncident": False, "pauseReason": None, "postPause": None}
    try:
        session_id = open_session()
        status = prepare_target(session_id, args.save_index, args.expected_save, args.no_load, binding)
        if status.get("isPaused") is not True:
            initial_stop = pause_and_verify(session_id)
            if initial_stop["pauseStatus"] != "confirmed" or initial_stop["pauseIncident"]:
                stop = initial_stop
                end_stop_allowed = False
                raise RuntimeError("watch_initial_pause_unconfirmed")
        start = snapshot(session_id)
        if start["paused"] is not True:
            raise RuntimeError("watch_initial_snapshot_unpaused")
        start_cycle = float(start["cycle"])
        target_cycle = start_cycle + args.target_cycles
        if not finite_number(target_cycle):
            raise RuntimeError("watch_target_cycle_invalid")
        deadline = time.monotonic() + args.max_seconds if args.max_seconds > 0 else None
        current, advancing = start, False
        while True:
            assert_target(launch_status(session_id), binding["target"])
            alerts = [] if args.ignore_critical_diagnostics else diagnostic_alerts(session_id)
            cycle = float(current["cycle"])
            samples.append({
                "cycle": current["cycle"],
                "paused": current["paused"],
                "speed": current["speed"],
                "alertLevel": current.get("alertLevel"),
                "summary": current.get("summary"),
                "diagnosticsIgnored": args.ignore_critical_diagnostics,
                "alerts": compact_alerts(alerts),
            })
            failure = is_bad_snapshot(current)
            if not failure and not args.ignore_critical_diagnostics:
                failure = critical_diagnostic_failure(alerts)
            if failure:
                break
            if cycle >= target_cycle:
                ok = True
                break
            if deadline and time.monotonic() >= deadline:
                ok = bool(args.allow_partial)
                break
            if not advancing:
                _require_no_mod_dialog(session_id)
                assert_target(launch_status(session_id), binding["target"])
                if deadline and time.monotonic() >= deadline:
                    ok = bool(args.allow_partial)
                    break
                set_speed(session_id, args.speed)
                advancing = True
            delay = max(1.0, args.poll_seconds)
            if deadline:
                delay = min(delay, max(0.0, deadline - time.monotonic()))
            time.sleep(delay)
            current = snapshot(session_id)
    except (RuntimeError, OSError, ValueError, OverflowError) as error:
        failure = str(error) if isinstance(error, RuntimeError) else "watch_runtime_unavailable"
    finally:
        if not args.no_pause_at_end and session_id is not None and end_stop_allowed:
            stop = _end_stop(session_id, binding)

    final = samples[-1] if samples else {}
    pause_ok = args.no_pause_at_end or (stop["pauseStatus"] == "confirmed" and not stop["pauseIncident"])
    result = {
        "ok": ok and not failure and pause_ok,
        "targetCycles": args.target_cycles,
        "startCycle": start_cycle,
        "targetCycle": target_cycle,
        "finalCycle": final.get("cycle"),
        "sampleCount": len(samples),
        "failure": failure,
        "partialAllowed": args.allow_partial,
        "diagnosticsIgnored": args.ignore_critical_diagnostics,
        "last": final,
    }
    result.update(stop)
    print(json.dumps(result, ensure_ascii=False, allow_nan=False))
    if not result["ok"]:
        sys.exit(1)


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, urllib.error.URLError, TimeoutError, json.JSONDecodeError) as exc:
        print(json.dumps({"ok": False, "error": str(exc)}, ensure_ascii=False), file=sys.stderr)
        sys.exit(1)
