#!/usr/bin/env python3
"""Check screenshot PO/runtime mappings; this does not test Unity rendering."""
import argparse
import json
from pathlib import Path
from verify_dupe_environment_localization import PLACEHOLDERS, read_po, require, unique_object


def check(root):
    base = root / "mods/OniMcp/Localization"
    records = {name: read_po(base / "po" / name)
               for name in ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po")}
    template = records["onimcp.pot"]
    for name, entries in records.items():
        require(entries.keys() == template.keys(), "PO contexts differ: " + name)
        for context, entry in entries.items():
            require(entry["msgid"] == template[context]["msgid"], "PO source differs: " + context)
            if context and entry["msgstr"]:
                require(sorted(PLACEHOLDERS.findall(entry["msgstr"])) ==
                        sorted(PLACEHOLDERS.findall(entry["msgid"])), "PO placeholders differ: " + context)
    catalogs = {}
    for language in ("en", "ja", "zh-Hans"):
        combined = {}
        for group in ("screenshot", "tools"):
            values = json.loads((base / "Catalogs" / (group + "." + language + ".json"))
                                .read_text(encoding="utf-8"), object_pairs_hook=unique_object)
            selected = {key: value for key, value in values.items()
                        if key.startswith("screenshot.") or key in (
                            "tools.navigation_control.description", "tools.navigation_control.parameters.action",
                            "tools.navigation_control.parameters.requestId", "tools.navigation_control.parameters.waitFrames")}
            require(not combined.keys() & selected.keys(), "Duplicate runtime mapping")
            combined.update(selected)
        catalogs[language] = combined
    keys = catalogs["en"].keys()
    require(len(keys) == 32, "Expected 28 screenshot and 4 metadata keys")
    for language, entries in catalogs.items():
        require(entries.keys() == keys, "Runtime key parity differs: " + language)
    for key in keys:
        context = "OniMcp.MCP." + key.upper()
        require(context in template and template[context]["msgstr"] == "", "Missing POT entry: " + key)
        english = catalogs["en"][key]
        require(english == template[context]["msgid"] == records["en-us.po"][context]["msgstr"],
                "English mapping differs: " + key)
        require(catalogs["ja"][key] == records["ja-jp.po"][context]["msgstr"], "Japanese mapping differs: " + key)
        for language, entries in catalogs.items():
            require(bool(entries[key]) and sorted(PLACEHOLDERS.findall(entries[key])) ==
                    sorted(PLACEHOLDERS.findall(english)), "Runtime placeholders differ: " + key + "/" + language)
        marker = "#. Runtime zh-Hans msgstr: "
        for entries in records.values():
            variants = [comment[len(marker):] for comment in entries[context]["comments"] if comment.startswith(marker)]
            require(len(variants) == 1 and json.loads(variants[0]) == catalogs["zh-Hans"][key],
                    "Separate zh-Hans source differs: " + key)
    return len(keys)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    print("Screenshot localization: " + str(check(args.root)) + " mappings validated (source only).")
