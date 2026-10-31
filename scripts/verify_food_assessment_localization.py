#!/usr/bin/env python3
"""Validate the food assessment PO sources and their current runtime catalog mapping."""
from __future__ import annotations

import argparse
import ast
import json
from pathlib import Path
import re


KEY_PREFIX = "food_assessment."
CONTEXT_PREFIX = "OniMcp.MCP.FOOD_ASSESSMENT."
EXPECTED_KEYS = {
    'food_assessment.alert.initializing',
    'food_assessment.alert.shortage',
    'food_assessment.alert.unavailable',
    'food_assessment.state.initializing',
    'food_assessment.state.unavailable',
    'food_assessment.summary',
    'food_assessment.summary.no_dupes',
    'food_assessment.summary.stress_unavailable',
}
PLACEHOLDERS = re.compile(r"(?<!\{)\{(\d+(?:,-?\d+)?(?::[^{}]+)?)\}(?!\})")


def require(value: bool, message: str) -> None:
    if not value:
        raise ValueError(message)


def unique_object(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        require(key not in result, "Duplicate JSON key: " + key)
        result[key] = value
    return result


def read_po(path: Path) -> dict:
    # These migrated entries use singular PO records. Fail on syntax the reader
    # cannot validate instead of silently accepting plural or obsolete records.
    entries, record, field, comments = {}, {}, None, []

    def flush() -> None:
        nonlocal record, field, comments
        if not record:
            comments = []
            return
        require("msgid" in record and "msgstr" in record, "Incomplete PO record in " + str(path))
        context = record.get("msgctxt", "")
        require(context not in entries, "Duplicate PO context: " + context)
        record["comments"] = comments
        entries[context] = record
        record, field, comments = {}, None, []

    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = line.strip()
        if not line:
            flush()
        elif line.startswith("#"):
            comments.append(line)
        else:
            match = re.fullmatch(r'(msgctxt|msgid|msgstr) (".*")', line)
            if match:
                field = match[1]
                require(field not in record, "Repeated PO field in " + str(path))
                token = match[2]
            else:
                require(line.startswith('"') and line.endswith('"') and field is not None,
                        "Unsupported PO syntax at " + str(path) + ":" + str(number))
                token = line
            value = ast.literal_eval(token)
            require(isinstance(value, str), "Invalid PO string in " + str(path))
            record[field] = record.get(field, "") + value
    flush()
    require("" in entries and entries[""]["msgid"] == "", "Missing PO header in " + str(path))
    return entries


def check(root: Path) -> int:
    base = root / "mods/OniMcp/Localization"
    files = {name: read_po(base / "po" / name)
             for name in ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po")}
    template = files["onimcp.pot"]
    for name, records in files.items():
        require(records.keys() == template.keys(), "PO contexts differ: " + name)
        for context, record in records.items():
            require(record["msgid"] == template[context]["msgid"], "PO source differs: " + context)
            if record["msgstr"] and context:
                require(sorted(PLACEHOLDERS.findall(record["msgstr"])) ==
                        sorted(PLACEHOLDERS.findall(record["msgid"])), "PO placeholders differ: " + context)
    expected_contexts = {CONTEXT_PREFIX + key.removeprefix(KEY_PREFIX).upper() for key in EXPECTED_KEYS}
    for name, records in files.items():
        scoped = {context: record for context, record in records.items() if context.startswith(CONTEXT_PREFIX)}
        require(set(scoped) == expected_contexts, "Food assessment PO context set differs: " + name)
        if name != "onimcp.pot":
            require(all(bool(record["msgstr"]) for record in scoped.values()), "Food assessment PO translation missing: " + name)
    catalogs = {language: json.loads((base / "Catalogs" / ("food_assessment." + language + ".json"))
                                    .read_text(encoding="utf-8"), object_pairs_hook=unique_object)
                for language in ("en", "ja", "zh-Hans")}
    source = "\n".join((root / "mods/OniMcp/Tools/Impl" / name).read_text(encoding="utf-8")
                        for name in ("World/SnapshotMetrics.cs", "World/SnapshotAlertReadTools.cs", "Colony/DiagnosticsTools.cs"))
    used = set(re.findall(r'McpLocalization\.(?:Format|Get)\("(food_assessment\.[^" ]+)"', source))
    keys = EXPECTED_KEYS
    require(used == keys, "Food assessment adapter key set differs")
    for language, catalog in catalogs.items():
        require(set(catalog) == keys, "Food assessment catalog key set differs: " + language)
    for key in sorted(keys):
        context = CONTEXT_PREFIX + key.removeprefix(KEY_PREFIX).upper()
        require(context in template, "Missing PO source for " + key)
        require(template[context]["msgstr"] == "", "POT translation must be empty: " + key)
        require(catalogs["en"][key] == template[context]["msgid"] == files["en-us.po"][context]["msgstr"],
                "English source/runtime mismatch: " + key)
        require(catalogs["ja"][key] == files["ja-jp.po"][context]["msgstr"], "Japanese source/runtime mismatch: " + key)
        for language, catalog in catalogs.items():
            require(key in catalog and bool(catalog[key]), "Missing runtime translation: " + key + "/" + language)
            require(sorted(PLACEHOLDERS.findall(catalog[key])) == sorted(PLACEHOLDERS.findall(catalogs["en"][key])),
                    "Runtime placeholders differ: " + key + "/" + language)
        marker = "#. Runtime zh-Hans msgstr: "
        for name, records in files.items():
            variants = [comment[len(marker):] for comment in records[context]["comments"] if comment.startswith(marker)]
            require(len(variants) == 1 and json.loads(variants[0]) == catalogs["zh-Hans"][key],
                    "Separate zh-Hans source/runtime mismatch: " + key + "/" + name)
    for key in sorted(keys):
        context = CONTEXT_PREFIX + key.removeprefix(KEY_PREFIX).upper()
        texts = [catalog[key] for catalog in catalogs.values()] + [files["zh-hant.po"][context]["msgstr"]]
        if key.startswith("food_assessment.summary"):
            # Snapshot negative IDs mean all worlds; food reads use omitted worldId.
            # Keep this source-contract guard separate from translated PO equality.
            require("omit worldId for all worlds" in catalogs["en"][key],
                    "All-world food guidance must specify omitted worldId: " + key)
            require("same worldId" not in catalogs["en"][key],
                    "Snapshot and food negative-ID aliases must not be conflated: " + key)
        for token in ("/active/resources/food.oni", "worldId", "kcal"):
            expected = catalogs["en"][key].count(token)
            require(all(text.count(token) == expected for text in texts), "Reserved food token differs: " + key + "/" + token)
    return len(keys)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    count = check(args.root)
    print("Food assessment localization: " + str(count) + " keys validated across 4 PO sources and 3 runtime catalogs.")
