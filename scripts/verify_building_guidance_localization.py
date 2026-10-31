#!/usr/bin/env python3
"""Check the four migrated placement-guidance PO/runtime entries and reserved identifiers."""
import argparse
import json
from pathlib import Path
import re
from verify_operation_file_localization import read_po, unique_object, require, PLACEHOLDERS

KEY_PREFIX = "building_guidance."
CONTEXT_PREFIX = "OniMcp.MCP.BUILDING_GUIDANCE."
SUFFIXES = ("anchor_source", "orientation", "coordinate_contract", "orientation_unavailable")

def check(root):
    base = root / "mods/OniMcp/Localization"
    files = {name: read_po(base / "po" / name) for name in ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po")}
    catalogs = {language: json.loads((base / "Catalogs" / ("building_guidance." + language + ".json")).read_text(),
                object_pairs_hook=unique_object) for language in ("en", "ja", "zh-Hans")}
    keys = {KEY_PREFIX + suffix for suffix in SUFFIXES}
    contexts = {CONTEXT_PREFIX + suffix.upper() for suffix in SUFFIXES}
    for name, records in files.items():
        require({context for context in records if context.startswith(CONTEXT_PREFIX)} == contexts,
                "Placement guidance PO contexts differ: " + name)
    for language, catalog in catalogs.items():
        require(set(catalog) == keys, "Placement guidance runtime keys differ: " + language)
    source = (root / "mods/OniMcp/Tools/Impl/Build/BuildingOperationGuidance.cs").read_text()
    used = set(re.findall(r'McpLocalization\.Get\("(building_guidance\.[^" ]+)"\)', source))
    require(used == keys, "Placement guidance source keys differ")
    for suffix in SUFFIXES:
        key, context = KEY_PREFIX + suffix, CONTEXT_PREFIX + suffix.upper()
        english = files["onimcp.pot"][context]["msgid"]
        require(files["onimcp.pot"][context]["msgstr"] == "", "POT translation must remain empty")
        require(english == catalogs["en"][key] == files["en-us.po"][context]["msgstr"], "English source/runtime mismatch: " + key)
        require(catalogs["ja"][key] == files["ja-jp.po"][context]["msgstr"], "Japanese source/runtime mismatch: " + key)
        for name, records in files.items():
            record = records[context]
            require(record["msgid"] == english, "PO msgid mismatch: " + key)
            require(name == "onimcp.pot" or bool(record["msgstr"]), "Empty PO translation: " + key)
            marker = "#. Runtime zh-Hans msgstr: "
            values = [json.loads(comment[len(marker):]) for comment in record["comments"] if comment.startswith(marker)]
            require(values == [catalogs["zh-Hans"][key]], "Separate Hans source/runtime mismatch: " + key)
        texts = [catalog[key] for catalog in catalogs.values()] + [files["zh-hant.po"][context]["msgstr"]]
        for text in texts:
            require(text and "\n" not in text and "\r" not in text, "Expected one nonempty guidance line: " + key)
            require(sorted(PLACEHOLDERS.findall(text)) == sorted(PLACEHOLDERS.findall(english)), "Placeholder mismatch: " + key)
            for token in ({"coordinate_contract": ("anchor", "nativeOrigin"), "orientation": ("world_editor", "Neutral")}.get(suffix, ())):
                require(token in text, "Reserved identifier missing: " + key + "/" + token)
    return len(keys)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    count = check(parser.parse_args().root)
    print("Building guidance localization: " + str(count) + " keys validated across 4 PO sources and 3 runtime catalogs.")
