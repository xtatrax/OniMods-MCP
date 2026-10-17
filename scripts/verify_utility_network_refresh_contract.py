#!/usr/bin/env python3
"""Static regression contract for completed utility network registration and refresh."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "mods/OniMcp/Tools/Impl/Build"


def body(source: str, marker: str) -> str:
    start = source.find(marker)
    assert start >= 0, marker
    opening = source.find("{", start)
    depth = 0
    for index in range(opening, len(source)):
        if source[index] == "{": depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0: return source[opening + 1:index]
    raise AssertionError(marker)


def main() -> None:
    refresh = (BUILD / "BuildPlanningUtilityNetworkRefresh.cs").read_text(encoding="utf-8")
    completion = (BUILD / "BuildPlanningInstantCompletion.cs").read_text(encoding="utf-8")
    native = (BUILD / "BuildPlanningNativeUtilityPath.cs").read_text(encoding="utf-8")
    placement = (BUILD / "BuildPlanningActionPlacement.cs").read_text(encoding="utf-8")

    # Guard against the two runtime failures: premature native registration and
    # silently skipping the connection plan while normal construction is pending.
    for forbidden in ("manager.AddToNetworks(", "GetField(\"items\"", "disconnectable.Connect()"):
        assert forbidden not in refresh
    assert "ConnectionManager" in refresh and "CanAddConnection" in refresh
    for caller in (native, placement):
        assert "RefreshAndValidateUtilityPathNetwork(def, path" in caller
        assert "!IsCompletedUtilityPath(def, path) ||" not in caller
        assert '["flowVerified"] = false' in caller
    assert '["constructionComplete"]' in placement
    assert '["presentCells"]' in placement
    exact = body(refresh, "private static bool IsExactConnectionUtilityPrefab")
    for component in ("LogicWire", "Wire", "Conduit", "SolidConduit"):
        assert "GetComponent<" + component + ">" in exact
    disconnect = (ROOT / "mods/OniMcp/Tools/Impl/Orders/OrdersUtilityDisconnect.cs").read_text()
    assert "QueueDeconstruct" not in disconnect and "DeleteObject" not in disconnect
    assert "beforeA & ~from" in disconnect and "beforeB & ~to" in disconnect
    assert "if (!dryRun && requested.Count > 0)" in disconnect
    assert "NativeUtilityDisconnect.Prepare(DisconnectTool.Instance)" in disconnect
    assert "native.Execute(cell, target, utility, remove)" in disconnect
    for forbidden in (".UpdateConnections(", ".ForceRebuildNetworks(", ".Refresh()", "connectionManager ="):
        assert forbidden not in disconnect, forbidden
    native_disconnect = (ROOT / "mods/OniMcp/Tools/Impl/Orders/NativeUtilityDisconnect.cs").read_text()
    assert '"DisconnectCellsAction"' in native_disconnect
    assert "typeof(int), typeof(GameObject), typeof(IHaveUtilityNetworkMgr), typeof(UtilityConnections)" in native_disconnect
    assert "action.ReturnType != typeof(void)" in native_disconnect
    assert "TargetInvocationException" in native_disconnect

    LEFT, RIGHT, UP, DOWN = 1, 2, 4, 8

    def explicit_path(existing, path):
        result = dict(existing)
        for point in path:
            result.setdefault(point, 0)
        for first, second in zip(path, path[1:]):
            dx, dy = second[0] - first[0], second[1] - first[1]
            bits = {
                (1, 0): (RIGHT, LEFT), (-1, 0): (LEFT, RIGHT),
                (0, 1): (UP, DOWN), (0, -1): (DOWN, UP),
            }
            assert (dx, dy) in bits
            first_bit, second_bit = bits[(dx, dy)]
            result[first] |= first_bit
            result[second] |= second_bit
        return result

    # Adjacent parallel runs remain isolated; spatial adjacency is not an edge request.
    parallel = explicit_path({(0, 0): UP, (0, 1): DOWN}, [(1, 0), (1, 1)])
    assert parallel[(0, 0)] == UP and parallel[(0, 1)] == DOWN
    assert parallel[(1, 0)] == UP and parallel[(1, 1)] == DOWN
    assert not parallel[(0, 0)] & RIGHT and not parallel[(1, 0)] & LEFT

    # Adjacent gate input stubs do not merge merely because their cells touch.
    gate_inputs = explicit_path({}, [(4, 4), (4, 5)])
    gate_inputs = explicit_path(gate_inputs, [(5, 4), (5, 5)])
    assert all(not gate_inputs[cell] & (LEFT | RIGHT) for cell in gate_inputs)

    # Requested edges are bidirectional, preserve unrelated bits, and are idempotent.
    connected = explicit_path({(8, 8): LEFT}, [(8, 8), (9, 8), (9, 9)])
    assert connected[(8, 8)] == (LEFT | RIGHT)
    assert connected[(9, 8)] == (LEFT | UP)
    assert connected[(9, 9)] == DOWN
    assert explicit_path(connected, [(8, 8), (9, 8), (9, 9)]) == connected

    # All supported utility prefabs share this exact path-edge implementation.
    for prefab in ("LogicWire", "Wire", "LiquidConduit", "GasConduit", "SolidConduit"):
        assert explicit_path({}, [(0, 0), (1, 0)]) == {(0, 0): RIGHT, (1, 0): LEFT}
    print("utility network refresh contract passed")


if __name__ == "__main__":
    main()
