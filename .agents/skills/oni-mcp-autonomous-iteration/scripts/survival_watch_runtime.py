import json
import math
import os


def strict_json(text):
    def invalid(_value):
        raise ValueError("watch_json_constant")
    return json.loads(text, parse_constant=invalid)


def decode_rpc_body(body):
    text = body.decode("utf-8-sig") if isinstance(body, bytes) else body
    if not isinstance(text, str) or not text.strip():
        raise RuntimeError("watch_rpc_empty")
    if text.lstrip().startswith(("event:", "data:", ":")):
        events = []
        for block in text.replace("\r\n", "\n").split("\n\n"):
            payload = "\n".join(line[5:].lstrip() for line in block.splitlines() if line.startswith("data:"))
            if payload:
                events.append(strict_json(payload))
        matches = [value for value in events if isinstance(value, dict) and ("result" in value or "error" in value)]
        if len(matches) != 1:
            raise RuntimeError("watch_rpc_event_shape")
        return matches[0]
    return strict_json(text)


def primary_text(data):
    if not isinstance(data, dict) or "error" in data:
        raise RuntimeError("watch_rpc_error")
    result = data.get("result")
    if not isinstance(result, dict) or result.get("isError", False) is not False:
        raise RuntimeError("watch_mcp_error")
    content = result.get("content")
    first = content[0] if isinstance(content, list) and content else None
    if not isinstance(first, dict) or first.get("type") != "text" or not isinstance(first.get("text"), str) or not first["text"].strip():
        raise RuntimeError("watch_primary_text_unavailable")
    return first["text"]


def tool_payload(data):
    text = primary_text(data)
    try:
        return strict_json(text)
    except (ValueError, RecursionError):
        return {"text": text}


def operation_payload(outer):
    def one_result(value, phase):
        if (not isinstance(value, dict) or value.get("ok") is not True
                or value.get("partial") is not False
                or ("preview" in value and value["preview"] is not False)
                or type(value.get("failed")) is not int or value["failed"] != 0):
            raise RuntimeError("watch_diagnostic_" + phase + "_failed")
        rows = value.get("results")
        if not isinstance(rows, list) or len(rows) != 1 or not isinstance(rows[0], dict):
            raise RuntimeError("watch_diagnostic_" + phase + "_shape")
        return rows[0]

    block = one_result(outer, "edit")
    if (type(outer.get("applied")) is not int or outer["applied"] != 1
            or outer.get("appliedUnit") != "edit_blocks"
            or block.get("ok") is not True or type(block.get("block")) is not int or block["block"] != 0):
        raise RuntimeError("watch_diagnostic_edit_incomplete")
    try:
        ops = strict_json(block["result"])
    except (KeyError, TypeError, ValueError, RecursionError) as error:
        raise RuntimeError("watch_diagnostic_ops_json_invalid") from error
    child = one_result(ops, "ops")
    if (ops.get("file") != "ops/colony.md" or type(ops.get("executedCommands")) is not int
            or ops["executedCommands"] != 1 or child.get("tool") != "colony_control"
            or child.get("ok") is not True or child.get("isError") is not False
            or ("preview" in child and child["preview"] is not False)):
        raise RuntimeError("watch_diagnostic_child_failed")
    arguments = child.get("arguments")
    if (not isinstance(arguments, dict) or arguments.get("domain") != "diagnostic"
            or arguments.get("action") != "alerts" or arguments.get("detail") is not True
            or arguments.get("confirm") is not True or arguments.get("dryRun") is not False
            or type(arguments.get("limit")) is not int or arguments["limit"] != 20):
        raise RuntimeError("watch_diagnostic_child_unknown")
    try:
        return strict_json(child["result"])
    except (KeyError, TypeError, ValueError, RecursionError) as error:
        raise RuntimeError("watch_diagnostic_child_json_invalid") from error


def finite_number(value):
    try:
        return type(value) in (int, float) and math.isfinite(value)
    except OverflowError:
        return False


def validate_snapshot(value):
    if not isinstance(value, dict) or not finite_number(value.get("cycle")) or value["cycle"] < 0:
        raise RuntimeError("watch_snapshot_clock_unknown")
    if type(value.get("paused")) is not bool or type(value.get("speed")) is not int or value["speed"] not in (1, 2, 3):
        raise RuntimeError("watch_snapshot_pause_unknown")
    metrics = value.get("metrics")
    if not isinstance(metrics, dict) or type(metrics.get("dupes")) is not int or metrics["dupes"] < 0 or type(metrics.get("redAlert")) is not bool:
        raise RuntimeError("watch_snapshot_metrics_unknown")
    if metrics["dupes"] > 0 and not finite_number(metrics.get("maxStress")):
        raise RuntimeError("watch_snapshot_metrics_unknown")
    if value.get("alertLevel") not in ("green", "yellow", "info", "red", "error", "danger", "critical"):
        raise RuntimeError("watch_snapshot_alerts_unknown")
    return value


def save_path(value):
    if not isinstance(value, str) or not value.strip() or not os.path.isabs(value):
        raise RuntimeError("watch_save_identity_unknown")
    return os.path.normpath(value)


def launch_state(value):
    if not isinstance(value, dict) or type(value.get("gameInitialized")) is not bool or value.get("loaded") is not value["gameInitialized"]:
        raise RuntimeError("watch_launch_state_unknown")
    return value


def assert_target(value, expected):
    status = launch_state(value)
    if not status["gameInitialized"]:
        raise RuntimeError("watch_game_not_loaded")
    if status.get("activeSaveUsable") is not True or save_path(status.get("activeSaveFile")) != save_path(expected):
        raise RuntimeError("watch_save_identity_mismatch")
    return status


def select_target(status, save_index, expected=None):
    launch_state(status)
    if expected is not None:
        target = save_path(expected)
        if status["gameInitialized"]:
            assert_target(status, target)
            return target
        candidates = status.get("saves")
        known = isinstance(candidates, list) and any(isinstance(row, dict) and row.get("path") == target for row in candidates)
        active = status.get("activeSaveUsable") is True and status.get("activeSaveFile") == target
        if not known and not active:
            raise RuntimeError("watch_save_not_listed")
        return target
    candidates = status.get("saves")
    rows = [row for row in candidates if isinstance(row, dict) and type(row.get("index")) is int and row["index"] == save_index] if isinstance(candidates, list) else []
    if len(rows) != 1:
        raise RuntimeError("watch_save_index_unknown")
    return save_path(rows[0].get("path"))
