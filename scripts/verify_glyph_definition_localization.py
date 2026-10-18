#!/usr/bin/env python3
"""Validate the glyph-lookup PO sources and their current runtime catalog mapping."""
from __future__ import annotations

import argparse
import ast
import json
from pathlib import Path
import re


KEY_PREFIX = "glyph_lookup."
CONTEXT_PREFIX = "OniMcp.MCP.GLYPH_LOOKUP."
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
    catalogs = {language: json.loads((base / "Catalogs" / ("glyph_lookup." + language + ".json"))
                                    .read_text(encoding="utf-8"), object_pairs_hook=unique_object)
                for language in ("en", "ja", "zh-Hans")}
    source = "\n".join((root / "mods/OniMcp/Tools/WorldEditor" / name).read_text(encoding="utf-8")
                        for name in ("WorldEditorGlyphLookup.cs", "WorldEditorNativeGlyphDefinitions.cs"))
    used = set(re.findall(r'McpLocalization\.(?:Get|Format)\("(glyph_lookup\.[^" ]+)"', source))
    keys = {key for key in catalogs["en"] if key.startswith(KEY_PREFIX)}
    require(used == keys and len(keys) == 3, "Runtime glyph lookup keys differ from the adapters")
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
    return len(keys)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    count = check(args.root)
    print("Glyph lookup localization: " + str(count) + " keys validated across 4 PO sources and 3 runtime catalogs.")
