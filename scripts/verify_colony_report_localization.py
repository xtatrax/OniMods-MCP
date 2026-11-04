#!/usr/bin/env python3
"""Validate the two MCP-owned daily report errors and their PO/runtime mapping."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import re
from verify_operation_file_localization import PLACEHOLDERS, read_po, require, unique_object

KEYS = {
    "colony_report.error.manager_not_initialized": "OniMcp.MCP.COLONY_REPORT.ERROR.MANAGER_NOT_INITIALIZED",
    "colony_report.error.not_found": "OniMcp.MCP.COLONY_REPORT.ERROR.NOT_FOUND",
}


def check(root: Path) -> int:
    base = root / "mods/OniMcp/Localization"
    sources = {name: read_po(base / "po" / name)
               for name in ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po")}
    catalogs = {language: json.loads((base / "Catalogs" / ("colony-report." + language + ".json"))
                                    .read_text(encoding="utf-8"), object_pairs_hook=unique_object)
                for language in ("en", "ja", "zh-Hans")}
    require(all(set(catalog) == set(KEYS) for catalog in catalogs.values()), "Daily report runtime key set differs")
    source = (root / "mods/OniMcp/Tools/Impl/Colony/ColonyReportTools.cs").read_text(encoding="utf-8")
    used = set(re.findall(r'McpLocalization\.Get\("(colony_report\.[^" ]+)"', source))
    require(used == set(KEYS), "Daily report error lookup keys differ")
    for name, records in sources.items():
        require({context for context in records if context.startswith("OniMcp.MCP.COLONY_REPORT.")} == set(KEYS.values()),
                "Daily report PO contexts differ: " + name)
        for key, context in KEYS.items():
            row = records[context]
            require(row["msgid"] == sources["onimcp.pot"][context]["msgid"], "Daily report source differs: " + context)
            require("#. Runtime JSON key: " + key in row["comments"], "Daily report runtime mapping missing: " + context)
            marker = "#. Runtime zh-Hans msgstr: "
            hans = [comment[len(marker):] for comment in row["comments"] if comment.startswith(marker)]
            require(len(hans) == 1 and json.loads(hans[0]) == catalogs["zh-Hans"][key], "Separate Hans source differs: " + context)
            require(sorted(PLACEHOLDERS.findall(row["msgstr"])) == sorted(PLACEHOLDERS.findall(row["msgid"])),
                    "Daily report placeholders differ: " + context)
            if name != "onimcp.pot": require(bool(row["msgstr"]), "Daily report translation missing: " + context)
    for key, context in KEYS.items():
        require(sources["onimcp.pot"][context]["msgstr"] == "", "Daily report POT must be untranslated")
        require(catalogs["en"][key] == sources["onimcp.pot"][context]["msgid"] == sources["en-us.po"][context]["msgstr"],
                "Daily report English mapping differs: " + key)
        require(catalogs["ja"][key] == sources["ja-jp.po"][context]["msgstr"], "Daily report Japanese mapping differs: " + key)
    return len(KEYS)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    print("Daily report localization: " + str(check(args.root)) + " errors, PO4/runtime3 validated; native names stay game-localized.")
