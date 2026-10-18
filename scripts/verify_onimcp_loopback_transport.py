#!/usr/bin/env python3
"""Verify OniMcp runtime helpers ignore ambient proxies only for loopback URLs."""

from contextlib import contextmanager, redirect_stdout
import io
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import sys
import threading
from unittest import mock


ROOT = Path(__file__).resolve().parents[1]
HELPER_DIR = ROOT / ".agents/skills/oni-mcp-autonomous-iteration/scripts"
sys.path.insert(0, str(HELPER_DIR))

import loopback_http  # noqa: E402
import runtime_smoke  # noqa: E402
import survival_watch  # noqa: E402


class FixtureHandler(BaseHTTPRequestHandler):
    def log_message(self, *_args):
        pass

    def do_GET(self):
        if self.path == "/http-error":
            self.send_response(503)
            self.end_headers()
            return
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b"ready")

    def do_POST(self):
        length = int(self.headers.get("Content-Length", "0"))
        body = self.rfile.read(length)
        request = json.loads(body.decode())
        self.server.requests.append(
            {
                "path": self.path,
                "headers": dict(self.headers.items()),
                "body": request,
            }
        )

        protocol = self.headers.get("Mcp-Protocol-Version")
        if protocol == runtime_smoke.MODERN_PROTOCOL:
            result = self._modern_result(request)
        else:
            result = {"ok": True}

        payload = json.dumps(
            {"jsonrpc": "2.0", "id": request.get("id", 1), "result": result}
        ).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        if protocol == runtime_smoke.MODERN_PROTOCOL:
            self.send_header("Mcp-Protocol-Version", runtime_smoke.MODERN_PROTOCOL)
        else:
            self.send_header("Mcp-Session-Id", "fixture-session")
        self.end_headers()
        self.wfile.write(payload)

    @staticmethod
    def _modern_result(request):
        method = request.get("method")
        if method == "server/discover":
            return {
                "resultType": "complete",
                "supportedVersions": [runtime_smoke.MODERN_PROTOCOL],
                "capabilities": {"resources": {}, "tools": {}},
                "ttlMs": 3600000,
                "cacheScope": "public",
            }
        if method == "tools/list":
            return {
                "resultType": "complete",
                "tools": [
                    {
                        "name": "benchmark",
                        "description": "read-only benchmark fixture",
                        "inputSchema": {
                            "type": "object",
                            "properties": {"task": {"type": "string"}},
                            "required": ["task"],
                        },
                    }
                ],
                "ttlMs": 300000,
                "cacheScope": "public",
            }
        if method == "tools/call":
            return {
                "resultType": "complete",
                "content": [
                    {
                        "type": "text",
                        "text": json.dumps({"ok": True, "status": "passed"}),
                    }
                ],
                "isError": False,
            }
        return {"ok": True}


class ProxyHandler(BaseHTTPRequestHandler):
    def log_message(self, *_args):
        pass

    def do_GET(self):
        self.server.requests.append(self.path)
        self.send_response(418, "proxy reached")
        self.end_headers()


@contextmanager
def running_server(handler):
    server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
    server.requests = []
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        yield server
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=2)


@contextmanager
def hostile_proxy_environment(proxy_url):
    names = ("HTTP_PROXY", "http_proxy", "NO_PROXY", "no_proxy")
    previous = {name: os.environ.get(name) for name in names}
    os.environ["HTTP_PROXY"] = proxy_url
    os.environ["http_proxy"] = proxy_url
    os.environ.pop("NO_PROXY", None)
    os.environ.pop("no_proxy", None)
    try:
        yield
    finally:
        for name, value in previous.items():
            if value is None:
                os.environ.pop(name, None)
            else:
                os.environ[name] = value


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def header_value(headers, name):
    for key, value in headers.items():
        if key.lower() == name.lower():
            return value
    return None


def verify_survival_pause_adapter():
    checks = 0

    def check(condition, message):
        nonlocal checks
        checks += 1
        require(condition, message)

    def state(paused=True, **values):
        return {"cycle": 42, "timeOfDayPercent": 3.2, "isPaused": paused,
                "timeScale": 0, "speed": 1, "internalSpeed": 0, **values}

    def envelope(text):
        return {"result": {"isError": False, "content": [
            {"type": "text", "text": text},
            {"type": "text", "text": '{"middleware":"fixture"}'}]}}

    def exercise(states, fault=None):
        calls, pending = [], list(states)

        def post(payload, session_id=None, timeout=None):
            args = payload["params"]["arguments"]
            calls.append(dict(args))
            check(len(calls) <= 4, "stop adapter exceeded two pause attempts")
            check(payload["params"]["name"] == "game_control" and args["domain"] == "speed",
                  "stop adapter must use the public game speed route")
            check(session_id == "fixture-session" and timeout == 10, "stop adapter keeps bounded session requests")
            check(args["task"] == "survival_watch.stop." + args["action"], "stop calls include their task identifier")
            if fault and len(calls) == fault[0]:
                if isinstance(fault[1], BaseException):
                    raise fault[1]
                return None, fault[1]
            value = "Game paused" if args["action"] == "pause" else pending.pop(0)
            text = value if isinstance(value, str) else json.dumps(value)
            return None, envelope(text)

        before = json.dumps(states)
        with mock.patch.object(survival_watch, "post", post), mock.patch.object(survival_watch.time, "sleep"):
            result = survival_watch.pause_and_verify("fixture-session")
        check(json.dumps(states) == before, "stop adapter must not mutate supplied native observations")
        check(all(call["action"] in ("pause", "time") for call in calls), "stop adapter must never resume or change speed")
        check(sum(call["action"] == "pause" for call in calls) == result["pauseAttempts"], "pause attempt count matches actual requests")
        check(isinstance(json.dumps(result, allow_nan=False), str), "stop result must remain valid JSON even for invalid native numeric observations")
        if result["postPause"] is not None:
            check(result["postPause"]["paused"] is result["pausedAtEnd"], "postPause retains its compatible tri-state paused field")
        return result, calls

    cases = [
        ([state()], True, "confirmed", 1, False),
        ([state(False), state()], True, "confirmed", 2, True),
        ([state(False), state(False)], False, "unconfirmed", 2, True),
        ([state(None), state(None)], None, "unknown", 2, True),
        ([state(None), state()], True, "confirmed", 2, True),
        ([{}, {}], None, "unknown", 2, True),
        ([state("false"), state("false")], None, "unknown", 2, True),
        ([state(1), state(1)], None, "unknown", 2, True),
        ([state(speed=0, internalSpeed=-1)] * 2, None, "unknown", 2, True),
        ([state(timeScale=1)] * 2, True, "unconfirmed", 2, True),
        ([state(timeScale=None)] * 2, True, "unknown", 2, True),
        ([state(timeScale=False)] * 2, True, "unknown", 2, True),
        ([state(timeScale=-1)] * 2, True, "unknown", 2, True),
        ([state(timeScale=float("nan"))] * 2, True, "unknown", 2, True),
        ([state(timeScale=10**400)] * 2, True, "unknown", 2, True),
        ([[], []], None, "unknown", 2, True),
    ]
    for states, flag, status, attempts, incident in cases:
        result, calls = exercise(states)
        check(result["pausedAtEnd"] is flag and result["pauseStatus"] == status, "native false/null/status must retain their distinct meanings")
        check(result["pauseAttempts"] == attempts and result["pauseIncident"] is incident, "recovery must stay bounded and must not erase an incident")
        check(len(calls) == attempts * 2, "only one time read follows each acknowledged pause")
    for index in (1, 2, 3, 4):
        result, calls = exercise([state(False), state()], (index, TimeoutError("fixture timeout")))
        check(len(calls) == index, "transport failure must not trigger another pause or time request")
        check(result["pausedAtEnd"] is None and result["pauseIncident"] and result["pauseStatus"] == "unknown",
              "a missing transport observation must stay unknown")
        check(result["pauseReason"] == "pause_transport_unavailable", "transport reason remains a machine ID")
    for invalid in ({"error": {"code": -1}}, {"result": {"isError": True}},
                    {"result": {"isError": "false"}}, {"result": {"content": []}},
                    {"result": {"content": [{"type": "image", "text": "ignored"}]}},
                    {"result": {"content": [{"type": "text", "text": ""}]}}):
        result, calls = exercise([], (1, invalid))
        check(len(calls) == 1 and result["pausedAtEnd"] is None and result["pauseIncident"],
              "invalid MCP pause responses must not be retried or reported as confirmed")
    for invalid in ({"error": {"code": -1}}, {"result": {"isError": True}},
                    {"result": {"content": [{"type": "text", "text": None}]}}):
        result, calls = exercise([], (2, invalid))
        check(len(calls) == 2 and result["pausedAtEnd"] is None and result["pauseIncident"],
              "an unsuccessful time response must not trigger another pause")
    for text in ("not JSON", "", "[1,2]", "1" * 5000):
        result, calls = exercise([state()], (2, envelope(text)))
        check(len(calls) == 4 and result["pausedAtEnd"] is True and result["pauseIncident"],
              "invalid observations in successful time responses allow only one recovery and remain incidents")

    def run_main(observations, extra_args=(), speed_failure=False, bad_snapshot=False):
        trace, pending = [], list(observations)
        snap = {"cycle": 42, "paused": True, "metrics": {"dupes": 0 if bad_snapshot else 3}}

        def native_speed(*_args):
            trace.append("set_speed")
            if speed_failure:
                raise RuntimeError("fixture_speed_error")

        def native_stop(_session, action):
            trace.append(action)
            value = "Game paused" if action == "pause" else pending.pop(0)
            return value if isinstance(value, str) else json.dumps(value)

        output, caught = io.StringIO(), None
        argv = ["survival_watch.py", "--no-load", "--target-cycles", "0", "--ignore-critical-diagnostics", *extra_args]
        with mock.patch.object(sys, "argv", argv), mock.patch.object(survival_watch, "open_session", return_value="fixture-session"), \
             mock.patch.object(survival_watch, "snapshot", return_value=snap), mock.patch.object(survival_watch, "set_speed", native_speed), \
             mock.patch.object(survival_watch, "_stop_tool_call", native_stop), mock.patch.object(survival_watch.time, "sleep"), redirect_stdout(output):
            try:
                survival_watch.main()
            except (SystemExit, RuntimeError) as exc:
                caught = exc
        return json.loads(output.getvalue()) if output.getvalue() else None, caught, trace

    for observations, expected_ok in (([state()], True), ([state(False), state()], False),
                                       ([state(False)] * 2, False), ([state(None)] * 2, False)):
        result, error, trace = run_main(observations)
        check(result["ok"] is expected_ok, "main success must require an incident-free confirmed stop")
        check((error is None) if expected_ok else isinstance(error, SystemExit) and error.code == 1,
              "failed final stopping must return a nonzero process outcome")
        check(trace.count("set_speed") == 1 and trace[-1] == "time", "a pause incident must never start another play loop")
    result, error, trace = run_main([], ("--no-pause-at-end",))
    check(result["ok"] is True and error is None and trace == ["set_speed"], "explicit no-pause opt-out must remain effective")
    check(result["pausedAtEnd"] is None and result["pauseStatus"] == "not_requested", "opt-out must not claim a stopped game")
    result, error, trace = run_main([state()], speed_failure=True)
    check(isinstance(error, RuntimeError) and str(error) == "fixture_speed_error" and trace == ["set_speed", "pause", "time"],
          "set_speed failure must still execute final stopping and preserve the original exception")
    result, error, trace = run_main([state()], bad_snapshot=True)
    check(result["ok"] is False and result["failure"] == "no duplicants alive or visible", "final stopping must not erase an earlier gameplay failure")
    print("PASS survival watcher stop adapter baseline: " + str(checks) + " host checks")

    raw = '{"cycle":42,"timeOfDayPercent":3.2,"isPaused":true,"timeScale":0,"speed":1,"internalSpeed":0}'
    for key, original in (("cycle", "42"), ("timeOfDayPercent", "3.2"), ("timeScale", "0")):
        for token in ("1e400", "-1e400", "NaN", "Infinity", "-Infinity"):
            invalid = raw.replace('"' + key + '":' + original, '"' + key + '":' + token)
            result, calls = exercise([invalid, invalid])
            check(result["pauseStatus"] == "unknown" and result["pauseIncident"] and result["pauseAttempts"] == 2,
                  "invalid clock metadata must prevent confirmed success and preserve the retry bound")
            check(result["postPause"][key] is None, "non-finite postPause numbers must become null")
            for other, value in (("cycle", 42), ("timeOfDayPercent", 3.2)):
                if other != key:
                    check(result["postPause"][other] == value, "valid companion clock values must remain unchanged")
    for key in ("cycle", "timeOfDayPercent"):
        for value in (None, True, False, "42", [], {"value": float("inf")}):
            invalid = state(**{key: value})
            result, calls = exercise([invalid, invalid])
            check(result["pauseStatus"] == "unknown" and result["pauseReason"] == "pause_time_metadata_unknown",
                  "missing or non-numeric clock metadata must remain unknown")
            check(result["postPause"][key] is None, "invalid clock metadata must not be retained as a valid number")
        invalid = state()
        del invalid[key]
        result, calls = exercise([invalid, invalid])
        check(result["pauseStatus"] == "unknown" and result["postPause"][key] is None, "absent clock metadata is not zero")
    for cycle, percent in ((0, 0.0), (42.125, 3.23456789), (10**400, 1e-200)):
        result, calls = exercise([state(cycle=cycle, timeOfDayPercent=percent)])
        check(result["pauseStatus"] == "confirmed" and not result["pauseIncident"], "finite native clock metadata remains eligible for confirmation")
        check(result["postPause"]["cycle"] == cycle and result["postPause"]["timeOfDayPercent"] == percent,
              "finite native clock values must not be rounded or replaced")
    overflow = raw.replace('"cycle":42', '"cycle":1e400')
    result, error, trace = run_main([overflow, overflow])
    check(result["ok"] is False and isinstance(error, SystemExit) and error.code == 1, "actual main must fail when clock metadata is non-finite")
    check(result["pauseStatus"] == "unknown" and result["pauseReason"] == "pause_time_metadata_unknown", "actual main must expose the clock metadata incident")
    check(isinstance(json.dumps(result, allow_nan=False), str), "actual main clock-failure output must remain strict JSON")
    print("PASS survival watcher stop adapter: " + str(checks) + " host checks")


def main():
    verify_survival_pause_adapter()
    require(loopback_http.is_loopback_url("http://localhost:8788/mcp/"), "localhost not recognized")
    require(loopback_http.is_loopback_url("http://127.0.0.1:8788/mcp/"), "IPv4 loopback not recognized")
    require(loopback_http.is_loopback_url("http://[::1]:8788/mcp/"), "IPv6 loopback not recognized")
    require(
        not loopback_http.is_loopback_url("http://127.0.0.1.example.invalid/mcp/"),
        "lookalike hostname incorrectly bypasses proxies",
    )

    closed_fixture_url = None
    with running_server(ProxyHandler) as proxy, running_server(FixtureHandler) as fixture:
        proxy_url = f"http://127.0.0.1:{proxy.server_port}"
        fixture_url = f"http://127.0.0.1:{fixture.server_port}/mcp/"
        closed_fixture_url = fixture_url
        with hostile_proxy_environment(proxy_url):
            with loopback_http.open_url(fixture_url, timeout=2) as response:
                require(response.read() == b"ready", "direct loopback GET failed")
            require(not proxy.requests, "loopback GET leaked through ambient proxy")

            client = runtime_smoke.McpClient(fixture_url)
            result = client.post({"jsonrpc": "2.0", "id": 1, "method": "fixture"})
            require(result.get("result", {}).get("ok") is True, "runtime_smoke legacy POST failed")
            require(client.session_id == "fixture-session", "runtime_smoke session header regressed")

            modern = runtime_smoke.run_modern_smoke(fixture_url)
            require(
                modern.get("protocol") == runtime_smoke.MODERN_PROTOCOL,
                "runtime_smoke modern protocol summary regressed",
            )
            require(modern.get("tools") == ["benchmark"], "runtime_smoke modern tool surface regressed")

            old_url = survival_watch.URL
            survival_watch.URL = fixture_url
            try:
                _, result = survival_watch.post({"jsonrpc": "2.0", "id": 1, "method": "fixture"})
            finally:
                survival_watch.URL = old_url
            require(result.get("result", {}).get("ok") is True, "survival_watch POST failed")
            require(not proxy.requests, "loopback POST leaked through ambient proxy")

            legacy_requests = []
            modern_requests = []
            for item in fixture.requests:
                protocol = header_value(item["headers"], "Mcp-Protocol-Version")
                if protocol == runtime_smoke.MODERN_PROTOCOL:
                    modern_requests.append(item)
                else:
                    legacy_requests.append(item)

            require(legacy_requests, "fixture did not observe a legacy runtime request")
            require(modern_requests, "fixture did not observe a modern runtime request")
            require(
                all(
                    header_value(item["headers"], "Mcp-Protocol-Version") == runtime_smoke.PROTOCOL
                    for item in legacy_requests
                ),
                "legacy MCP protocol header changed while fixing transport",
            )
            require(
                all(header_value(item["headers"], "Mcp-Session-Id") is None for item in modern_requests),
                "modern runtime smoke sent legacy Mcp-Session-Id",
            )
            require(
                all(
                    header_value(item["headers"], "Mcp-Method") == item["body"].get("method")
                    for item in modern_requests
                ),
                "modern runtime smoke Mcp-Method did not mirror the JSON-RPC method",
            )
            modern_tool_calls = [
                item for item in modern_requests if item["body"].get("method") == "tools/call"
            ]
            require(len(modern_tool_calls) == 1, "modern runtime smoke did not call benchmark exactly once")
            require(
                header_value(modern_tool_calls[0]["headers"], "Mcp-Name") == "benchmark",
                "modern runtime smoke did not mirror benchmark in Mcp-Name",
            )
            require(
                modern_tool_calls[0]["body"].get("params", {}).get("_meta", {}).get(
                    "io.modelcontextprotocol/protocolVersion"
                )
                == runtime_smoke.MODERN_PROTOCOL,
                "modern runtime smoke omitted per-request protocol metadata",
            )

            try:
                loopback_http.open_url("http://example.invalid/proxy-check", timeout=2)
            except RuntimeError as exc:
                require("HTTP 418" in str(exc), f"unexpected remote proxy error: {exc}")
            else:
                raise AssertionError("non-loopback request bypassed ambient proxy")
            require(
                any("example.invalid/proxy-check" in path for path in proxy.requests),
                "non-loopback request did not use ambient proxy",
            )

            try:
                loopback_http.open_url(
                    f"http://127.0.0.1:{fixture.server_port}/http-error", timeout=2
                )
            except RuntimeError as exc:
                require("HTTP 503" in str(exc), f"HTTP response not distinguished: {exc}")
            else:
                raise AssertionError("loopback HTTP 503 unexpectedly succeeded")

    try:
        loopback_http.open_url(closed_fixture_url, timeout=0.5)
    except RuntimeError as exc:
        require("loopback MCP request" in str(exc), f"connection failure not identified: {exc}")
    else:
        raise AssertionError("closed loopback fixture unexpectedly accepted a request")

    print("PASS OniMcp runtime helpers cover legacy/modern MCP over direct loopback without disabling remote proxy policy")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (AssertionError, OSError, RuntimeError) as exc:
        print(f"FAIL {exc}", file=sys.stderr)
        sys.exit(1)
