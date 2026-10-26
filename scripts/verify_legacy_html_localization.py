#!/usr/bin/env python3
"""Validate legacy HTML map PO sources and their runtime catalog mappings."""
from __future__ import annotations

import argparse
import importlib.util
import json
from pathlib import Path
import re


KEYS = ("unreadable", "viewport_title", "viewport_back", "viewport_description",
        "layer_title", "layer_back", "layer_description", "show_raw", "show_map",
        "legend.vacuum_space", "legend.building", "legend.dirt", "legend.sandstone", "legend.water", "legend.polluted_water", "legend.algae", "legend.granite_rock", "legend.oxygen_gas", "legend.carbon_dioxide",
        "tooltip.cell", "tooltip.element", "tooltip.temperature", "tooltip.building")


def check(root: Path) -> dict:
    parser_path = root / "scripts/verify_dupe_environment_localization.py"
    spec = importlib.util.spec_from_file_location("existing_po_parser", parser_path)
    parser = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(parser)
    require = parser.require
    base = root / "mods/OniMcp/Localization"
    names = ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po")
    records = {name: parser.read_po(base / "po" / name) for name in names}
    template = records["onimcp.pot"]
    for name, entries in records.items():
        require(entries.keys() == template.keys(), "PO context parity differs: " + name)
        for context, entry in entries.items():
            require(entry["msgid"] == template[context]["msgid"], "PO msgid differs: " + context)
            if context and entry["msgstr"]:
                require(sorted(parser.PLACEHOLDERS.findall(entry["msgstr"])) ==
                        sorted(parser.PLACEHOLDERS.findall(entry["msgid"])), "PO placeholder mismatch: " + context)
    catalogs = {language: json.loads((base / "Catalogs" / ("map_html." + language + ".json"))
                .read_text(encoding="utf-8"), object_pairs_hook=parser.unique_object)
                for language in ("en", "ja", "zh-Hans")}
    expected = {"map_html." + key for key in KEYS}
    for language, entries in catalogs.items():
        require(entries.keys() == expected, "Unexpected legacy HTML keys: " + language)
    source = (root / "mods/OniMcp/Tools/WorldEditor/WorldEditorVirtualFileReader.cs").read_text(encoding="utf-8")
    shell_source = (root / "mods/OniMcp/Tools/WorldEditor/WorldEditorMapHtml.cs").read_text(encoding="utf-8")
    for key in KEYS:
        runtime = "map_html." + key
        context = "OniMcp.MCP.MAP_HTML." + key.upper()
        entry = template[context]
        require(entry["msgstr"] == "", "POT source must remain untranslated")
        require(entry["msgid"] == catalogs["en"][runtime] == records["en-us.po"][context]["msgstr"],
                "English source/runtime mismatch: " + runtime)
        require(records["ja-jp.po"][context]["msgstr"] == catalogs["ja"][runtime],
                "Japanese source/runtime mismatch: " + runtime)
        require(bool(records["zh-hant.po"][context]["msgstr"]), "Missing provisional Hant translation")
        for name, entries in records.items():
            comments = entries[context]["comments"]
            require(comments.count("#. Runtime JSON key: " + runtime) == 1, "Runtime key mapping differs: " + name)
            marker = "#. Runtime zh-Hans msgstr: "
            translations = [x[len(marker):] for x in comments if x.startswith(marker)]
            require(len(translations) == 1 and json.loads(translations[0]) == catalogs["zh-Hans"][runtime],
                    "Legacy Hans mapping differs: " + name + runtime)
        for language, entries in catalogs.items():
            require(sorted(parser.PLACEHOLDERS.findall(entries[runtime])) ==
                    sorted(parser.PLACEHOLDERS.findall(entry["msgid"])), "Runtime placeholder mismatch: " + language + runtime)
        if key.endswith("_title"):
            require(source.count('WebUtility.HtmlEncode(McpLocalization.Format("' + runtime + '", null, ') == 1,
                    "Title must format without inline prose fallback and HTML encode: " + runtime)
        elif key.startswith("legend."):
            require(source.count('WebUtility.HtmlEncode(McpLocalization.Get("' + runtime + '"))') == 2,
                    "Default legend must resolve twice without inline fallback and HTML encode: " + runtime)
        elif key.startswith("tooltip."):
            require(source.count('McpLocalization.Get("' + runtime + '")') == 2,
                    "Tooltip labels must resolve in both Reader branches without inline fallback: " + runtime)
            require(source.count("WebUtility.HtmlEncode(tooltip)") == 2,
                    "The complete tooltip must retain HTML encoding in both Reader branches")
        elif key in ("show_raw", "show_map"):
            require(shell_source.count('System.Security.SecurityElement.Escape(OniMcp.Support.McpLocalization.Get("' + runtime + '"))') == 1,
                    "Button label must resolve without inline fallback and HTML encode: " + runtime)
        else:
            expected_count = 2 if key == "unreadable" else 1
            require(source.count('WebUtility.HtmlEncode(McpLocalization.Get("' + runtime + '"))') == expected_count,
                    "Page prose must resolve without inline fallback and HTML encode: " + runtime)
    return {"status": "PASS_LEGACY_HTML_LOCALIZATION", "keys": len(KEYS), "poFiles": 4,
            "runtimeCatalogs": 3, "allPoContexts": len(template) - 1, "gameActions": 0,
            "nativeAcceptance": False}


def main() -> None:
    args_parser = argparse.ArgumentParser(description=__doc__)
    args_parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = args_parser.parse_args()
    print(json.dumps(check(args.root.resolve()), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
