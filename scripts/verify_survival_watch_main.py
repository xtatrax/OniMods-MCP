#!/usr/bin/env python3
"""Exercise the actual watcher main against synthetic native/HTTP boundaries."""
from contextlib import redirect_stdout
import copy
import io
import json
from pathlib import Path
import sys
import types
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
HELPERS = ROOT / ".agents/skills/oni-mcp-autonomous-iteration/scripts"
sys.path.insert(0, str(HELPERS))
import survival_watch as production

TARGET = "/fixture/watch.sav"
OTHER = "/fixture/other.sav"


def require(value, reason):
    if not value:
        raise AssertionError(reason)


def envelope(value, error=False):
    text = value if isinstance(value, str) else json.dumps(value)
    return {"result": {"isError": error, "content": [
        {"type": "text", "text": text}, {"type": "text", "text": "fixture_auxiliary_notice"}]}}


class Boundary:
    def __init__(self, **options):
        self.options = options
        self.loaded = options.get("loaded", True)
        self.path = options.get("path", TARGET)
        self.paused = options.get("paused", True)
        self.cycle = 42
        self.calls = []
        self.sleeps = []
        self.speed_calls = 0
        self.status_calls = 0
        self.stop_times = list(options.get("stop_times", []))

    def status(self):
        return {"gameInitialized": self.loaded, "loaded": self.loaded, "activeSaveUsable": True,
                "activeSaveFile": self.path, "isPaused": self.paused, "canStart": True,
                "saves": [{"index": 0, "path": TARGET}]}

    def clock(self):
        return {"cycle": self.cycle, "timeOfDayPercent": 12.345, "isPaused": self.paused,
                "timeScale": 0 if self.paused else 1, "speed": 1, "internalSpeed": 0}

    def post(self, payload, session_id=None, parse=True, timeout=20):
        name = payload["params"]["name"]
        args = payload["params"]["arguments"]
        self.calls.append((name, copy.deepcopy(args)))
        require(isinstance(args.get("task"), str) and bool(args["task"].strip()), "every request needs a task")
        require(session_id == "fixture-session", "session must be preserved")
        if name == "game_control":
            domain, action = args.get("domain"), args.get("action")
            if domain == "launch" and action == "status":
                self.status_calls += 1
                if self.options.get("status_error"):
                    return None, envelope("fixture_status_failure", True)
                result = self.status()
                if self.options.get("unknown_status"):
                    result["gameInitialized"] = None
                return None, envelope(result)
            if domain == "launch" and action == "start":
                require(args.get("resume") is False and args.get("path") == TARGET, "one exact paused load is required")
                require("forceLoad" not in args and "index" not in args, "never force or re-resolve a selected save")
                if not self.options.get("load_stays_pending"):
                    self.loaded, self.path, self.paused = True, TARGET, True
                if self.options.get("load_transport"):
                    raise TimeoutError("fixture_load_timeout")
                return None, envelope({"started": True, "target": TARGET})
            if domain == "ui":
                require(args.get("uiDomain") == "dialog" and action == "status", "dialog must remain read-only")
                if self.options.get("switch_at_dialog"):
                    self.path = OTHER
                return None, envelope({"available": not self.options.get("dialog_unknown", False),
                                       "present": self.options.get("dialog", False),
                                       "count": 1 if self.options.get("dialog") else 0,
                                       "dialogs": [{}] if self.options.get("dialog") else []})
            if domain == "speed" and action == "set_speed":
                self.speed_calls += 1
                if self.options.get("speed_error"):
                    return None, envelope("fixture_speed_failure", True)
                self.paused, self.cycle = False, 43
                if self.options.get("switch_after_speed"):
                    self.path = OTHER
                return None, envelope({"requestedSpeed": args["speed"], "speed": args["speed"], "internalSpeed": args["speed"] - 1, "isPaused": False})
            if domain == "speed" and action == "pause":
                self.paused = True
                return None, envelope("Game paused")
            if domain == "speed" and action == "time":
                if self.options.get("stop_transport"):
                    raise TimeoutError("fixture_stop_timeout")
                return None, envelope(self.stop_times.pop(0) if self.stop_times else self.clock())
            raise AssertionError("unexpected game mutation")
        if name == "server_control":
            return None, envelope({"valid": False, "dryRun": False, "requested": 1,
                                   "executed": 0, "failed": 1, "results": [{"isError": True,
                                   "name": "colony_control", "text": "Tool not found: colony_control"}]}, True)
        require(name == "world_editor", "only existing virtual read routes are supported")
        if args.get("command") == "read":
            require(args.get("path") == "/active/colony/status.oni" and args.get("profile") == "minimal", "exact minimal snapshot virtual file")
            value = {"cycle": self.cycle, "paused": self.paused, "speed": 1,
                     "alertLevel": self.options.get("alert_level", "green"),
                     "metrics": {"dupes": 3, "maxStress": 0, "redAlert": False}, "summary": "fixture"}
            if self.options.get("snapshot_error") or (self.speed_calls and self.options.get("sample_error")):
                return None, envelope("fixture_snapshot_failure", True)
            if self.options.get("invalid_snapshot"):
                value = {"text": "fixture_missing_snapshot"}
            return None, envelope(value)
        require(args.get("command") == "edit" and args.get("path") == "/active/ops/colony.md", "exact typed diagnostic ops file")
        require(args.get("confirm") is True and args.get("dryRun") is False, "diagnostic ops must actually execute the fixed read command")
        require(args.get("content") == "<<<<<<< SEARCH\n\n=======\ncall domain=diagnostic action=alerts limit=20 detail=true\n>>>>>>> REPLACE", "one fixed read line; no arbitrary DSL or mutation")
        value = [] if not self.options.get("critical") else [{"severity": "critical", "category": "fixture", "message": "fixture"}]
        if "invalid_diagnostics" in self.options:
            value = self.options["invalid_diagnostics"]
        return None, envelope(diagnostic_operation(value))


def diagnostic_operation(value):
    child = {"tool": "colony_control", "ok": True, "isError": False,
             "arguments": {"domain": "diagnostic", "action": "alerts", "limit": 20, "detail": True, "confirm": True, "dryRun": False},
             "result": json.dumps(value)}
    ops = {"ok": True, "partial": False, "file": "ops/colony.md", "applied": 0,
           "executedCommands": 1, "failed": 0, "changed": None, "results": [child]}
    return {"ok": True, "partial": False, "applied": 1, "appliedUnit": "edit_blocks", "failed": 0,
            "changed": None, "results": [{"block": 0, "ok": True, "result": json.dumps(ops)}]}


def run_main(module=production, options=None, arguments=None, monotonic=None):
    boundary = Boundary(**(options or {}))
    argv = ["survival_watch.py", "--no-load", "--expected-save", TARGET,
            "--target-cycles", "0", "--ignore-critical-diagnostics"] if arguments is None else ["survival_watch.py", *arguments]
    output, caught = io.StringIO(), None
    with mock.patch.object(module, "open_session", return_value="fixture-session"), \
         mock.patch.object(module, "post", boundary.post), mock.patch.object(module.time, "sleep", side_effect=boundary.sleeps.append), \
         mock.patch.object(sys, "argv", argv), redirect_stdout(output):
        if monotonic is not None:
            clock = mock.patch.object(module.time, "monotonic", side_effect=monotonic)
        else:
            clock = mock.patch.object(module.time, "monotonic", wraps=module.time.monotonic)
        with clock:
            try:
                module.main()
            except (SystemExit, RuntimeError, OSError, ValueError) as error:
                caught = error
    text = output.getvalue()
    return (json.loads(text) if text else None), caught, boundary


def verify(module=production):
    count = 0
    def check(value, reason):
        nonlocal count
        count += 1
        require(value, reason)
    def mutations(boundary, action):
        return [args for name, args in boundary.calls if name == "game_control" and args.get("action") == action]
    def fails(result, caught):
        return result is not None and result.get("ok") is False and isinstance(caught, SystemExit) and caught.code == 1

    for raw in [None, {}, {"error": {}}, {"result": {"isError": True}},
                {"result": {"isError": "false", "content": [{"type": "text", "text": "{}"}]}},
                {"result": {"content": []}}, {"result": {"content": [{"type": "image", "text": "{}"}]}}]:
        with mock.patch.object(module, "post", return_value=(None, raw)):
            try:
                module.call_tool("fixture-session", "game_control", {"domain": "speed", "action": "time"})
            except RuntimeError:
                check(True, "strict response rejected")
            else:
                check(False, "an invalid RPC/MCP response must not become normal data")
    with mock.patch.object(module, "post", return_value=(None, envelope({"value": 17}))):
        check(module.call_tool("fixture-session", "game_control", {"domain": "speed", "action": "time"}) == {"value": 17}, "auxiliary text must not corrupt primary JSON")

    rpc = {"jsonrpc": "2.0", "id": 7, "result": {"fixture": True}}
    for body in (json.dumps(rpc), ("\ufeff" + json.dumps(rpc)).encode("utf-8"),
                 "event: message\ndata: " + json.dumps(rpc) + "\n\n",
                 "event: message\ndata: {\"method\":\"fixture\"}\n\nevent: message\ndata: " + json.dumps(rpc) + "\n\n"):
        check(module.decode_rpc_body(body) == rpc, "legacy JSON and single-result SSE preserve the actual response")
    for body in ("", "data: {}\n\n", "data: " + json.dumps(rpc) + "\n\ndata: " + json.dumps(rpc) + "\n\n"):
        try:
            module.decode_rpc_body(body)
        except (RuntimeError, ValueError):
            check(True, "ambiguous or missing RPC is rejected")
        else:
            check(False, "a missing or duplicate SSE result cannot be guessed")
    for init in (None, {"error": {}}, {"result": {"protocolVersion": "unknown"}}, {"result": {}}):
        response = types.SimpleNamespace(headers={"Mcp-Session-Id": "fixture-session"})
        with mock.patch.object(module, "post", return_value=(response, init)) as post:
            try:
                module.open_session()
            except RuntimeError:
                check(post.call_count == 1, "invalid initialization cannot notify or invoke gameplay")
            else:
                check(False, "protocol readiness cannot be assumed")
    for value in ({}, {"requestedSpeed": True, "speed": True, "internalSpeed": False, "isPaused": False},
                  {"requestedSpeed": 1, "speed": 1, "internalSpeed": 0, "isPaused": True}):
        with mock.patch.object(module, "post", return_value=(None, envelope(value))):
            try:
                module.set_speed("fixture-session", 1)
            except RuntimeError:
                check(True, "unknown or malformed speed acknowledgement is rejected")
            else:
                check(False, "speed acknowledgement must confirm the actual typed native state")

    unsupported = Boundary()
    with mock.patch.object(module, "post", unsupported.post):
        try:
            module.call_tool("fixture-session", "server_control", {"domain": "batch", "action": "call_many",
                             "calls": [{"name": "colony_control", "arguments": {"domain": "snapshot", "action": "get"}}]})
        except RuntimeError:
            check(True, "real batch preflight cannot find an internal-only colony operation")
        else:
            check(False, "a synthetic batch success must not stand in for the actual public surface")
    valid_op = diagnostic_operation([])
    check(module.operation_payload(valid_op) == [], "legacy applied=0 is not a failed read when one command returned a real empty list")
    for phase in ("outer_failed", "outer_partial", "outer_preview", "missing_detail", "wrong_tool", "child_failed", "ops_preview", "two_commands", "child_non_json", "child_confirm_false", "child_dry_run_true", "child_preview_string", "block_boolean"):
        value = copy.deepcopy(valid_op)
        ops = json.loads(value["results"][0]["result"])
        child = ops["results"][0]
        if phase == "outer_failed": value["ok"] = False
        if phase == "outer_partial": value["partial"] = True
        if phase == "outer_preview": value["preview"] = True
        if phase == "missing_detail": child["arguments"]["detail"] = False
        if phase == "wrong_tool": child["tool"] = "fixture_other"
        if phase == "child_failed": child["isError"] = True
        if phase == "ops_preview": ops["preview"] = True
        if phase == "two_commands": ops["executedCommands"] = 2
        if phase == "child_non_json": child["result"] = "fixture_no_details"
        if phase == "child_confirm_false": child["arguments"]["confirm"] = False
        if phase == "child_dry_run_true": child["arguments"]["dryRun"] = True
        if phase == "child_preview_string": child["preview"] = "true"
        if phase == "block_boolean": value["results"][0]["block"] = False
        value["results"][0]["result"] = json.dumps(ops)
        try:
            module.operation_payload(value)
        except RuntimeError:
            check(True, "incomplete or foreign ops results are not diagnostics")
        else:
            check(False, "every fixed diagnostic route boundary must be verified")

    result, error, boundary = run_main(module)
    check(result["ok"] and error is None and result["pausedAtEnd"] is True, "zero-target read must finish paused")
    check(not mutations(boundary, "set_speed") and not mutations(boundary, "start"), "zero target must never resume or load")
    check(result["sampleCount"] == 1, "zero target still reads one real snapshot")
    result, error, boundary = run_main(module, {"dialog": True})
    check(result["ok"] and not mutations(boundary, "set_speed") and not mutations(boundary, "acknowledge"), "zero target does not intervene in an existing dialog")
    result, error, boundary = run_main(module, arguments=["--no-load", "--target-cycles", "0"])
    check(isinstance(error, RuntimeError) and not boundary.calls, "no-load requires an explicit expected identity before requests")
    for options in ({"path": OTHER}, {"loaded": False}, {"unknown_status": True}, {"status_error": True}):
        result, error, boundary = run_main(module, options)
        check(fails(result, error), "unready or wrong save must fail")
        check(not mutations(boundary, "set_speed") and not mutations(boundary, "start") and not mutations(boundary, "pause"), "identity failure must not mutate any save")
    for options in ({"snapshot_error": True}, {"invalid_snapshot": True}):
        result, error, boundary = run_main(module, options)
        check(fails(result, error), "unknown snapshot must fail")
        check(not mutations(boundary, "start") and not mutations(boundary, "set_speed"), "snapshot failure is not a load or resume signal")
        check(len(mutations(boundary, "pause")) == 1, "known target failure still executes protected final stop")

    play = ["--no-load", "--expected-save", TARGET, "--target-cycles", "1", "--ignore-critical-diagnostics", "--poll-seconds", "1"]
    result, error, boundary = run_main(module, arguments=play)
    check(result["ok"] and error is None and result["finalCycle"] == 43, "one requested advance succeeds")
    check(len(mutations(boundary, "set_speed")) == 1 and len(mutations(boundary, "pause")) == 1, "one start and one verified final stop")
    partial_args = play + ["--max-seconds", "2", "--allow-partial"]
    result, error, boundary = run_main(module, arguments=partial_args, monotonic=[0, 0, 3])
    check(result["ok"] and not mutations(boundary, "set_speed"), "expired time after guard reads must not start advancement")
    result, error, boundary = run_main(module, arguments=partial_args + ["--poll-seconds", "20"], monotonic=[0, 0, 0, 0.5])
    check(result["ok"] and boundary.sleeps[0] == 1.5, "poll waits are limited to the remaining requested duration")
    for options in ({"dialog": True}, {"dialog_unknown": True}, {"switch_at_dialog": True}):
        result, error, boundary = run_main(module, options, play)
        check(fails(result, error) and not mutations(boundary, "set_speed"), "blocked or unidentified target must not advance")
        check(not mutations(boundary, "acknowledge"), "watcher must never accept a dialog")
    for options in ({"sample_error": True}, {"speed_error": True}):
        result, error, boundary = run_main(module, options, play)
        check(fails(result, error), "an advance or sample failure stays failed")
        check(len(mutations(boundary, "set_speed")) == 1 and len(mutations(boundary, "pause")) == 1, "failure must not replay the advance")
    result, error, boundary = run_main(module, {"switch_after_speed": True}, play)
    check(fails(result, error) and len(mutations(boundary, "set_speed")) == 1 and not mutations(boundary, "pause"), "save changes end the watcher without touching the other save")
    for color in ("green", "info", "yellow"):
        result, error, boundary = run_main(module, {"alert_level": color}, play)
        check(result["ok"] and boundary.speed_calls == 1, "known non-critical native colors preserve normal monitoring")
    for color in ("red", "error", "danger", "critical"):
        result, error, boundary = run_main(module, {"alert_level": color}, play)
        check(fails(result, error) and not mutations(boundary, "set_speed"), "native red and known critical aliases block the initial advance")
    for color in (None, "", "unknown", [], True):
        result, error, boundary = run_main(module, {"alert_level": color}, play)
        check(fails(result, error) and result["failure"] == "watch_snapshot_alerts_unknown" and not mutations(boundary, "set_speed"), "unknown native alert color is not safe")
    diagnostics_play = [value for value in play if value != "--ignore-critical-diagnostics"]
    for invalid in ({}, [{}], [{"severity": None}], [{"severity": "unknown", "category": "fixture", "message": "fixture"}], [{"severity": "info"}]):
        result, error, boundary = run_main(module, {"invalid_diagnostics": invalid}, diagnostics_play)
        check(fails(result, error) and result["failure"] == "watch_diagnostics_unknown", "unread diagnostics are not an empty safe list")
        check(not mutations(boundary, "set_speed"), "diagnostic unknown must not authorize advance")
    result, error, boundary = run_main(module, {"critical": True}, diagnostics_play)
    check(fails(result, error) and not mutations(boundary, "set_speed"), "a known critical diagnostic blocks the initial advance")
    loaded_args = ["--expected-save", TARGET, "--target-cycles", "0", "--ignore-critical-diagnostics"]
    result, error, boundary = run_main(module, {"loaded": False}, loaded_args)
    check(result["ok"] and len(mutations(boundary, "start")) == 1 and not mutations(boundary, "set_speed"), "menu loads exactly one paused target")
    result, error, boundary = run_main(module, {"loaded": False, "load_transport": True}, loaded_args)
    check(fails(result, error) and len(mutations(boundary, "start")) == 1, "uncertain load is not retried")
    check(len(mutations(boundary, "pause")) == 1 and not mutations(boundary, "set_speed"), "loaded exact target gets only a protective stop after a load timeout")
    result, error, boundary = run_main(module, {"loaded": False, "load_stays_pending": True}, loaded_args, [0, 0, 61])
    check(fails(result, error) and len(mutations(boundary, "start")) == 1 and not mutations(boundary, "pause"), "bounded load wait does not write into an unready world")
    for token in ("nan", "inf", "-inf"):
        for option in ("--target-cycles", "--max-seconds", "--poll-seconds"):
            result, error, boundary = run_main(module, arguments=["--no-load", "--expected-save", TARGET, option + "=" + token])
            check(isinstance(error, RuntimeError) and not boundary.calls, "non-finite CLI clocks must fail before any request")
    for states in ([{"isPaused": False}], [{"isPaused": None}], []):
        time_values = [{"cycle": 42, "timeOfDayPercent": 1, "speed": 1, "internalSpeed": 0,
                        "timeScale": 0, **entry} for entry in states]
        options = {"paused": False, "stop_times": time_values * 2, "stop_transport": not states}
        result, error, boundary = run_main(module, options)
        check(fails(result, error) and result["pauseIncident"], "initial stop incidents must remain failures")
        check(result["failure"] == "watch_initial_pause_unconfirmed", "initial failure is not overwritten")
        check(len(mutations(boundary, "pause")) == (2 if states else 1), "finally must not repeat a failed initial stop sequence")
        check(not mutations(boundary, "set_speed"), "initial pause failure never advances")
    result, error, boundary = run_main(module, arguments=["--no-load", "--expected-save", TARGET, "--target-cycles", "0", "--ignore-critical-diagnostics", "--no-pause-at-end"])
    check(result["ok"] and result["pauseStatus"] == "not_requested" and not mutations(boundary, "pause"), "explicit final-stop opt-out is retained")
    return count


def candidate(watcher_source, helper_source):
    helper = types.ModuleType("survival_watch_runtime")
    exec(compile(helper_source, "<watch-runtime-candidate>", "exec"), helper.__dict__)
    module = types.ModuleType("watch_candidate")
    with mock.patch.dict(sys.modules, {"survival_watch_runtime": helper}):
        exec(compile(watcher_source, "<watch-candidate>", "exec"), module.__dict__)
    return module


def main():
    checks = verify()
    watcher = (HELPERS / "survival_watch.py").read_text()
    helper = (HELPERS / "survival_watch_runtime.py").read_text()
    variants = [
        ("ignore_mcp_error", watcher, helper.replace('result.get("isError", False) is not False', 'False')),
        ("concatenate_auxiliary_content", watcher, helper.replace('return first["text"]', 'return "".join(part.get("text", "") for part in content if part.get("type") == "text")')),
        ("resume_during_load", watcher.replace('"path": target, "resume": False', '"path": target, "resume": True'), helper),
        ("skip_save_identity", watcher, helper.replace('status.get("activeSaveUsable") is not True or save_path(status.get("activeSaveFile")) != save_path(expected)', 'False')),
        ("old_unavailable_batch_route", watcher.replace('call_tool(session_id, "world_editor", {\n        "command": "read", "path": "/active/colony/status.oni", "profile": "minimal"})', 'call_tool(session_id, "server_control", {"domain": "batch", "action": "call_many", "calls": [{"name": "colony_control", "arguments": {"domain": "snapshot", "action": "get"}}]})'), helper),
        ("repeat_initial_stop_failure", watcher.replace('end_stop_allowed = False', 'end_stop_allowed = True'), helper),
    ]
    for name, changed_watcher, changed_helper in variants:
        require(changed_watcher != watcher or changed_helper != helper, "mutation must change a product statement")
        try:
            verify(candidate(changed_watcher, changed_helper))
        except (AssertionError, RuntimeError, KeyError, TypeError, ValueError):
            continue
        raise AssertionError("known-bad mutation survived: " + name)
    print("PASS survival watcher main: " + str(checks) + " checks; 6 known-bad mutations detected")


if __name__ == "__main__":
    main()
