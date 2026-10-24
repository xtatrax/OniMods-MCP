#!/usr/bin/env python3
"""Check cell pickup Markdown PO/runtime contracts without starting ONI."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import re
import verify_cell_port_hint_localization as po

SUFFIXES = ("title", "items", "count", "details", "groups", "element", "mass_unavailable", "mass_partial")
EXPECTED_NEW = {
    "en": ("mass unavailable", "{0} kg known; mass unavailable for {1} item(s)"),
    "ja": ("質量未取得", "既知分 {0} kg; {1} 件の質量未取得"),
    "zh-Hans": ("质量未获取", "已知 {0} kg；{1} 件物品的质量未获取"),
}

def check(root: Path) -> dict:
    base = root / "mods/OniMcp/Localization"
    records = {name: po.read_po(base / "po" / name) for name in ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po")}
    template = records["onimcp.pot"]
    for name, entries in records.items():
        po.require(entries.keys() == template.keys(), "PO context parity differs: " + name)
        for context, entry in entries.items():
            po.require(entry["msgid"] == template[context]["msgid"], "PO source differs: " + context)
            if context and entry["msgstr"]:
                po.require(sorted(po.PLACEHOLDERS.findall(entry["msgstr"])) == sorted(po.PLACEHOLDERS.findall(entry["msgid"])), "PO placeholders differ: " + context)
    catalogs = {lang: json.loads((base / "Catalogs" / ("cell-details." + lang + ".json")).read_text(encoding="utf-8"), object_pairs_hook=po.unique_object) for lang in EXPECTED_NEW}
    prefix = "cell.pickups."
    keys = {prefix + suffix for suffix in SUFFIXES}
    for language, catalog in catalogs.items():
        po.require({key for key in catalog if key.startswith(prefix)} == keys, "Pickup runtime key parity differs: " + language)
        for suffix, expected in zip(("mass_unavailable", "mass_partial"), EXPECTED_NEW[language]):
            po.require(catalog[prefix + suffix] == expected, "Fixed mass contract differs: " + language + suffix)
    for suffix in SUFFIXES:
        key = prefix + suffix; context = "OniMcp.MCP.CELL.PICKUPS." + suffix.upper()
        po.require(context in template and template[context]["msgstr"] == "", "Missing or translated POT source: " + key)
        po.require(template[context]["msgid"] == records["en-us.po"][context]["msgstr"] == catalogs["en"][key], "English source/runtime differs: " + key)
        po.require(records["ja-jp.po"][context]["msgstr"] == catalogs["ja"][key], "Japanese source/runtime differs: " + key)
        po.require(bool(records["zh-hant.po"][context]["msgstr"]), "Missing provisional Hant sample: " + key)
        for name, entries in records.items():
            po.require(entries[context]["comments"].count("#. Runtime JSON key: " + key) == 1, "PO key mapping differs: " + name + key)
        marker = "#. Runtime zh-Hans msgstr: "
        variants = [line[len(marker):] for line in records["zh-hant.po"][context]["comments"] if line.startswith(marker)]
        po.require(len(variants) == 1 and json.loads(variants[0]) == catalogs["zh-Hans"][key], "Separate Hans source/runtime differs: " + key)
        for language, catalog in catalogs.items():
            po.require(sorted(po.PLACEHOLDERS.findall(catalog[key])) == sorted(po.PLACEHOLDERS.findall(catalogs["en"][key])), "Runtime placeholders differ: " + language + key)
    source = (root / "mods/OniMcp/Tools/WorldEditor/WorldEditorCellPickupDetails.cs").read_text(encoding="utf-8")
    used = set(re.findall(r'McpLocalization\.(?:Get|Format)\("(cell\.pickups\.[^" ]+)"', source))
    po.require(used == keys, "Actual Markdown adapter and pickup catalogs differ")
    po.require('McpLocalization.Format("cell.pickups.mass_partial", null,' in source, "Partial mass must have no inline prose fallback")
    return {"status": "PASS_PICKUP_DETAIL_LOCALIZATION", "pickupKeys": len(keys), "poFiles": 4, "runtimeCatalogs": 3, "allPoContexts": len(template)-1, "nativeAcceptance": False}

def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__); ap.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1]); args=ap.parse_args()
    print(json.dumps(check(args.root.resolve()), ensure_ascii=False, indent=2))

if __name__ == "__main__": main()
