#!/usr/bin/env python3
"""Validate the basic duplicant read adapter's PO and runtime localization."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re

from verify_snapshot_localization import PLACEHOLDERS, read_po, require, unique_object


KEYS = {"warnings.legacy_alias", "errors.game_not_initialized"}
CONTEXT_PREFIX = "OniMcp.MCP.COMMON."


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
    catalogs = {language: json.loads((base / "Catalogs" / ("common." + language + ".json"))
                                    .read_text(encoding="utf-8"), object_pairs_hook=unique_object)
                for language in ("en", "ja", "zh-Hans")}
    source = (root / "mods/OniMcp/Tools/Impl/Colony/ColonyDuplicantReadTools.cs").read_text(encoding="utf-8")
    used = set(re.findall(r'McpLocalization\.(?:Get|Format)\("([^" ]+)"', source))
    require(used == KEYS, "Basic duplicant adapter runtime localization keys differ")
    for key in sorted(KEYS):
        context = CONTEXT_PREFIX + key.upper()
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
    return len(KEYS)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    count = check(args.root)
    print("Basic duplicant localization: " + str(count) + " keys validated across 4 PO sources and 3 runtime catalogs.")
