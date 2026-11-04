#!/usr/bin/env python3
"""Check the native construction material prose PO source against its runtime catalogs."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
from verify_operation_file_localization import PLACEHOLDERS, read_po, require, unique_object


def check(root: Path) -> int:
    base = root / "mods/OniMcp/Localization"
    names = ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po")
    sources = {name: read_po(base / "po" / name) for name in names}
    catalogs = {language: json.loads((base / "Catalogs" / ("build_materials." + language + ".json"))
                .read_text(encoding="utf-8"), object_pairs_hook=unique_object)
                for language in ("en", "ja", "zh-Hans")}
    keys = set(catalogs["en"])
    require(keys and all(set(catalog) == keys for catalog in catalogs.values()), "Build-material runtime key sets differ")
    mappings = {}
    for context, row in sources["onimcp.pot"].items():
        if not context.startswith("OniMcp.MCP.BUILD_MATERIALS."): continue
        marker = "#. Runtime JSON key: "
        mapped = [comment[len(marker):] for comment in row["comments"] if comment.startswith(marker)]
        require(len(mapped) == 1 and mapped[0] not in mappings, "Build-material source mapping is missing or repeated")
        mappings[mapped[0]] = context
    require(set(mappings) == keys, "Build-material PO/runtime ownership differs")
    for name, records in sources.items():
        require({c for c in records if c.startswith("OniMcp.MCP.BUILD_MATERIALS.")} == set(mappings.values()),
                "Build-material PO contexts differ: " + name)
        for key, context in mappings.items():
            row = records[context]
            require(row["msgid"] == sources["onimcp.pot"][context]["msgid"], "Build-material source differs: " + context)
            require("#. Runtime JSON key: " + key in row["comments"], "Build-material runtime mapping differs: " + context)
            marker = "#. Runtime zh-Hans msgstr: "
            hans = [comment[len(marker):] for comment in row["comments"] if comment.startswith(marker)]
            require(len(hans) == 1 and json.loads(hans[0]) == catalogs["zh-Hans"][key], "Separate Hans source differs: " + context)
            if name != "onimcp.pot":
                require(sorted(PLACEHOLDERS.findall(row["msgstr"])) == sorted(PLACEHOLDERS.findall(row["msgid"])),
                        "Build-material placeholders differ: " + context)
            require(row["msgstr"] == "" if name == "onimcp.pot" else bool(row["msgstr"]), "Build-material translation state differs")
    for key, context in mappings.items():
        require(catalogs["en"][key] == sources["onimcp.pot"][context]["msgid"] == sources["en-us.po"][context]["msgstr"],
                "Build-material English source differs: " + key)
        require(catalogs["ja"][key] == sources["ja-jp.po"][context]["msgstr"], "Build-material Japanese source differs: " + key)
        require(sorted(PLACEHOLDERS.findall(catalogs["zh-Hans"][key])) == sorted(PLACEHOLDERS.findall(catalogs["en"][key])),
                "Separate Hans runtime placeholders differ: " + key)
    return len(keys)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    print("Build-material localization: " + str(check(args.root)) + " PO4/runtime3 entries validated.")
