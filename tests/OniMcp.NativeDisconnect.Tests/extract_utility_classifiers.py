#!/usr/bin/env python3
"""Link exact production classifiers rather than a fixture-only prefab allowlist."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import sys

parser = argparse.ArgumentParser()
parser.add_argument("--repo-root", type=Path, required=True)
parser.add_argument("--source", type=Path, required=True)
parser.add_argument("--errors", type=Path, required=True)
parser.add_argument("--output", type=Path, required=True)
parser.add_argument("--anchors", type=Path)
args = parser.parse_args()
sys.path.insert(0, str(args.repo_root / "scripts"))
from onimcp_verify_parsing import matching_delimiter

slices, records = [], []
for path, names in [(args.source, ["IsUtilityPrefab", "IsLinearUtilityPrefab"]),
                    (args.errors, ["EqualsIgnoreCase"])] + ([(args.anchors,["IsDryRun"])] if args.anchors else []):
    raw = path.read_bytes()
    text = raw.decode("utf-8")
    for name in names:
        hits = list(re.finditer(r"(?m)^        private static bool " + name + r"\s*\(", text))
        if len(hits) != 1:
            raise SystemExit(f"Expected one production declaration for {name}, got {len(hits)}")
        start = hits[0].start()
        opening = text.index("{", hits[0].end())
        end = matching_delimiter(text, opening, "{", "}") + 1
        data = text[start:end].encode("utf-8")
        byte_start, byte_end = len(text[:start].encode("utf-8")), len(text[:end].encode("utf-8"))
        assert raw[byte_start:byte_end] == data
        slices.append(data.decode("utf-8"))
        records.append({"method": name, "sourceFile": path.name,
            "sourceSha256": hashlib.sha256(raw).hexdigest(), "byteStart": byte_start,
            "byteEndExclusive": byte_end, "sliceSha256": hashlib.sha256(data).hexdigest()})
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text("// Exact production method slices; provenance is recorded alongside this file.\n"
    + "using System;\nusing Newtonsoft.Json.Linq;\nnamespace OniMcp.Tools { public static partial class BuildPlanningTools {\n"
    + "\n\n".join(slices) + "\n} }\n")
args.output.with_suffix(".ranges.json").write_text(json.dumps(records, indent=2))
print(f"Linked {len(records)} exact utility classifier methods")
