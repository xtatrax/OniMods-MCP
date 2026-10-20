#!/usr/bin/env python3
"""Compile exact production method slices; never translate their implementation."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

parser = argparse.ArgumentParser()
parser.add_argument("--repo-root", required=True, type=Path)
parser.add_argument("--output", required=True, type=Path)
for name in ("obstructions", "safety", "geometry", "runtime", "instant"):
    parser.add_argument("--" + name, required=True, type=Path)
args = parser.parse_args()
sys.path.insert(0, str(args.repo_root / "scripts"))
from onimcp_verify_parsing import matching_delimiter

methods = {
    "obstructions": ["FindFootprintObstructions", "ResolveBuildingDefForPlacement", "ExistingBuildingFootprintObstructions"],
    "safety": ["FindUtilityLayerConflicts", "UsesNativeBridgeEndpointRegistration", "FindBridgeEndpointLayerConflicts",
        "PlacementObjectPrefabId", "PlacementSafetyFootprint", "FindBuildingLayerConflicts", "FindLogicEndpointConflicts", "AddLogicEndpointConflicts",
        "AddLogicPortGroupConflicts", "ExistingBlueprintCompletionSafetyFailure", "HasUnsafeExecutionConflict"],
    "geometry": ["ValidateFootprint", "ActualPlacementDetails", "ComparePlacement"],
    "runtime": ["ExistingObjectFootprint", "FindConstructableAtPlacement"],
    "instant": ["IsAuthorizedVirtualFileInstantBuild", "FindCompletedBuildAtPlacement", "IsCompletedBuildFullyRegistered",
        "IsCompletedBuildGridRegistered", "IsConduitBridgePortRegistered", "HasRegisteredGateEndpoint", "IsTargetCompletedObject"],
}
records = []
slices = []
for group, names in methods.items():
    path = getattr(args, group)
    data = path.read_bytes()
    text = data.decode("utf-8")
    for name in names:
        matches = list(re.finditer(r"(?m)^        private static[^\n]*\b" + re.escape(name) + r"\s*\(", text))
        if len(matches) != 1:
            raise SystemExit(f"Expected exactly one production declaration for {name}, got {len(matches)}")
        start = matches[0].start()
        opening = text.index("{", matches[0].end())
        end = matching_delimiter(text, opening, "{", "}") + 1
        piece = text[start:end]
        slices.append(piece)
        byte_start = len(text[:start].encode("utf-8"))
        byte_end = len(text[:end].encode("utf-8"))
        assert data[byte_start:byte_end] == piece.encode("utf-8")
        records.append({"group": group, "method": name, "sourceFile": path.name,
            "sourceSha256": hashlib.sha256(data).hexdigest(), "byteStart": byte_start,
            "byteEndExclusive": byte_end, "startLine": text.count("\n", 0, start) + 1,
            "endLine": text.count("\n", 0, end) + 1,
            "sliceSha256": hashlib.sha256(piece.encode("utf-8")).hexdigest()})
header = "// Generated exact production slices. See adjacent ranges JSON for byte provenance.\n"
header += "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Reflection;\nusing Newtonsoft.Json.Linq;\nusing UnityEngine;\nnamespace OniMcp.Tools { public static partial class BuildPlanningTools {\n"
output = header + "\n\n".join(slices) + "\n} }\n"
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(output, encoding="utf-8")
args.output.with_suffix(".ranges.json").write_text(json.dumps(records, indent=2), encoding="utf-8")
print(f"Linked {len(records)} exact production method slices")
