#!/usr/bin/env python3
"""Hermetic tests of the actual verifier module; never call ONI or compile C#.

Run --checker OLD_FULL_SCRIPT CheckerRegression.test_01_catalog_reference_positive
for a semantic negative control through the pre-existing constructor API.
"""
from __future__ import annotations
import argparse
from contextlib import redirect_stderr, redirect_stdout
import importlib.util
from io import StringIO
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import warnings

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
args_parser = argparse.ArgumentParser(add_help=False)
args_parser.add_argument("--checker", type=Path, default=Path(__file__).resolve().parents[1] / "scripts/verify_mcp_localization_catalog.py")
args, test_args = args_parser.parse_known_args()
spec = importlib.util.spec_from_file_location("localization_verifier_under_test", args.checker)
C = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = C
spec.loader.exec_module(C)


def parameter(key, expression, before=""):
    return "[" + json.dumps(key) + "] = new McpToolParameter { Type = \"string\", " + before + "Description = " + expression + ", Required = false }"


def constructor(entries):
    return "Parameters = new Dictionary<string, McpToolParameter> { " + entries + " }, Handler = args => null"


class CheckerRegression(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="oni-localization-verifier-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.mod = self.root / "mods/OniMcp"
        self.catalogs = self.mod / "Localization/Catalogs"
        self.catalogs.mkdir(parents=True)
        self.group("fixture", {"fixture.reference": "Resolved English", "storage_components.scope_description": "Primary or all components."})

    def write(self, relative, text):
        path = self.mod / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def group(self, stem, entries):
        for locale in C.LOCALES:
            values = entries if locale == "en" else {key: locale + " " + value for key, value in entries.items()}
            (self.catalogs / (stem + "." + locale + ".json")).write_text(json.dumps(values), encoding="utf-8")

    def test_01_catalog_reference_positive(self):
        # This uses an existing API so OLD_FULL_SCRIPT fails semantically, not
        # because a new helper or argument is absent.
        source = constructor(parameter("action", 'McpLocalization.Get("fixture.reference")'))
        try:
            result = C.constructor_parameters(self.mod, source, "fixture_tool")
        except ValueError as error:
            self.fail("catalog_reference_positive_rejected: " + str(error))
        self.assertEqual(result, {"action": "Resolved English"})

    def test_02_regular_literal_compatibility(self):
        value = 'Literal , { } // not a comment /* neither */ "quoted"'
        self.assertEqual(C.constructor_parameters(self.mod, constructor(parameter("action", json.dumps(value))), "fixture_tool"), {"action": value})

    def test_03_forced_english_reference_and_provenance(self):
        audit = {}
        result = C.constructor_parameters(self.mod, constructor(parameter("action", 'McpLocalization.GetForLanguage("en", "fixture.reference")')), "fixture_tool", audit=audit)
        self.assertEqual(result["action"], "Resolved English")
        self.assertEqual(audit["references"], [{"context": "fixture_tool.action", "method": "GetForLanguage", "key": "fixture.reference", "baselineLanguage": "en", "catalog": "fixture.en.json"}])

    def test_04_nested_members_comments_and_fake_description(self):
        nested = 'Items = new SchemaProperty { Description = "nested ignored", Properties = new Dictionary<string, SchemaProperty> { ["nested"] = new SchemaProperty { Type = "object" } } }, '
        source = '/* Parameters = Unknown(); */ var note = "Parameters = Unknown();"; ' + constructor('/* ["ghost"] = new McpToolParameter { Description = "ignored" }, */' + parameter("calls", 'McpLocalization.Get("fixture.reference")', nested))
        self.assertEqual(C.constructor_parameters(self.mod, source, "fixture_tool"), {"calls": "Resolved English"})

    def test_05_unknown_description_expressions_are_refused(self):
        for expression in ['Unknown("fixture.reference")', 'McpLocalization.Get(key)', 'McpLocalization.Get("fixture.reference", "inline fallback")', 'McpLocalization.GetForLanguage("ja", "fixture.reference")', '"first" + "second"', '@"verbatim"', '$"interpolated"']:
            with self.subTest(expression=expression), self.assertRaises(ValueError):
                C.constructor_parameters(self.mod, constructor(parameter("action", expression)), "fixture_tool")

    def test_06_missing_catalog_reference_is_not_a_literal(self):
        with self.assertRaisesRegex(ValueError, "Missing catalog reference"):
            C.constructor_parameters(self.mod, constructor(parameter("action", 'McpLocalization.Get("not.present")')), "fixture_tool")

    def test_07_indexed_last_write_has_explicit_shadow_audit(self):
        audit = {}
        entries = parameter("enabled", '"first"') + "," + parameter("enabled", '"second"')
        self.assertEqual(C.constructor_parameters(self.mod, constructor(entries), "fixture_tool", audit=audit), {"enabled": "second"})
        self.assertEqual(audit["indexedParameterShadows"], [{"context": "fixture_tool", "parameter": "enabled", "previousIndex": 0, "replacementIndex": 1, "semantics": "indexed_initializer_last_assignment_wins"}])
        with warnings.catch_warnings(record=True) as emitted:
            warnings.simplefilter("always")
            C.constructor_parameters(self.mod, constructor(entries), "fixture_tool")
        self.assertEqual(len(emitted), 1)
        self.assertIn("Indexed parameter shadow audit", str(emitted[0].message))

    def test_08_invalid_parameter_shapes_are_not_skipped(self):
        for entries in ['{ "value", new McpToolParameter { Description = "old Add syntax" } }', '[dynamicKey] = new McpToolParameter { Description = "dynamic" }', parameter("action", '"value"') + ',,' + parameter("other", '"second"'), '["value"] = UnknownFactory()', '["value"] = new McpToolParameter { Items = new SchemaProperty { Description = "nested only" } }', '["value"] = new McpToolParameter { Description = "first", Description = "second" }']:
            with self.subTest(entries=entries), self.assertRaises(ValueError):
                C.constructor_parameters(self.mod, constructor(entries), "fixture_tool")

    def wrapper(self, tail=""):
        entries = ",".join(parameter(key, json.dumps("World id." if key == "worldId" else "Area id." if key == "areaId" else "Coordinate.")) for key in ["areaId", "x1", "y1", "x2", "y2", "worldId"])
        self.write("Tools/Impl/Orders/OrdersPriorityTools.cs", "private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra) { var parameters = new Dictionary<string, McpToolParameter> { " + entries + " }; foreach (var item in extra) parameters[item.Key] = item.Value; return parameters; " + tail + " }")

    def test_09_known_wrapper_merge_and_unknown_wrapper_tail(self):
        self.wrapper()
        source = 'Parameters = RectParams(new Dictionary<string, McpToolParameter> { ' + parameter("worldId", '"Caller override"') + ' }), Handler = args => null'
        self.assertEqual(C.constructor_parameters(self.mod, source, "orders_control")["worldId"], "Caller override")
        with self.assertRaisesRegex(ValueError, "Unsupported parameter wrapper"):
            C.constructor_parameters(self.mod, source.replace("RectParams", "UnknownParams"), "orders_control")
        self.wrapper('Mutate(parameters);')
        with self.assertRaisesRegex(ValueError, "Unsupported parameter wrapper merge"):
            C.constructor_parameters(self.mod, source, "orders_control")

    def test_10_exact_known_parameter_factory_and_unknown_factory(self):
        body = 'internal static McpToolParameter StorageScopeParameter() { return new McpToolParameter { Type = "string", EnumValues = new List<string> { "primary", "all_components" }, Description = McpLocalization.Get("storage_components.scope_description") }; }'
        self.write("Tools/Impl/Build/StorageComponentDetails.cs", body)
        source = constructor('["storageScope"] = StorageTools.StorageScopeParameter()')
        audit = {}
        self.assertEqual(C.constructor_parameters(self.mod, source, "building_control", audit=audit), {"storageScope": "Primary or all components."})
        self.assertEqual(audit["parameterFactories"][0]["source"], "Tools/Impl/Build/StorageComponentDetails.cs")
        with self.assertRaisesRegex(ValueError, "Unsupported parameter factory"):
            C.constructor_parameters(self.mod, source, "fixture_tool")
        self.write("Tools/Impl/Build/StorageComponentDetails.cs", body.replace('return new McpToolParameter', 'Mutate(); return new McpToolParameter'))
        with self.assertRaisesRegex(ValueError, "Unsupported parameter factory body"):
            C.constructor_parameters(self.mod, source, "building_control")

    def test_11_params_helper_and_unknown_helper_tail(self):
        source = 'Parameters = Params(), Handler = args => null; private static Dictionary<string, McpToolParameter> Params() { return new Dictionary<string, McpToolParameter> { ' + parameter("value", 'McpLocalization.Get("fixture.reference")') + ' }; }'
        self.assertEqual(C.constructor_parameters(self.mod, source, "fixture_tool"), {"value": "Resolved English"})
        with self.assertRaisesRegex(ValueError, "Unsupported Params helper tail"):
            C.constructor_parameters(self.mod, source.replace(' }; }', ' }; Mutate(); }'), "fixture_tool")

    def full_fixture(self):
        for path in self.catalogs.glob("*.json"):
            path.unlink()
        descriptions = {"tools.task.description": "Required shared task."}
        values = {}
        for name, relative in C.TOOL_SOURCES.items():
            descriptions["tools." + name + ".description"] = "Tool " + name
            values["tools." + name + ".parameters.value"] = "Value " + name
            description = 'McpLocalization.Get("tools.' + name + '.description")' if name in ("game_control", "navigation_control") else json.dumps("Tool " + name)
            params = constructor(parameter("value", json.dumps("Value " + name)))
            if name == "orders_control":
                self.wrapper()
                params = 'Parameters = RectParams(new Dictionary<string, McpToolParameter> { ' + parameter("value", json.dumps("Value " + name)) + ' }), Handler = args => null'
                values["tools.orders_control.parameters.areaId"] = "Area id."
                values["tools.orders_control.parameters.worldId"] = "World id."
            self.write(relative, 'return new McpTool { Name = ' + json.dumps(name) + ', Description = ' + description + ', ' + params + ' };')
        self.group("tools", descriptions)
        self.group("feature", values)
        self.group("internal", {"tools.game_save_control.description": "Internal compatibility tool."})
        names = ",".join(json.dumps(name) for name in C.TOOL_SOURCES)
        self.write("Tools/Core/OniToolRegistry.cs", 'DefaultPublicToolNames = new HashSet<string> { ' + names + ' }; internal static bool IsCoordinateParameter(string name) { switch (name) { case "x": case "y": case "x1": case "y1": case "x2": case "y2": return true; default: return false; } } properties[ToolCallMiddleware.TaskDescriptionParameter] = new SchemaProperty { Description = "Required shared task." };')
        english = []
        for name in C.OVERRIDE_METHODS:
            expression = 'McpLocalization.GetForLanguage("en", "tools.' + name + '.description")' if name in ("game_control", "navigation_control") else json.dumps("Tool " + name)
            english.append('case ' + json.dumps(name) + ': tool.Description = ' + expression + '; break;')
        english.append('private static Dictionary<string, string> CommonDescriptions() { return new Dictionary<string, string> { ["value"] = "Common value" }; }')
        for tool, method in C.OVERRIDE_METHODS.items():
            expression = 'McpLocalization.GetForLanguage("en", "tools.' + tool + '.parameters.value")' if tool in ("game_control", "navigation_control") else json.dumps("Value " + tool)
            english.append('private static Dictionary<string, string> ' + method + 'Descriptions() { var d = CommonDescriptions(); d["value"] = ' + expression + '; return d; }')
        self.write("Tools/Entry/CoreToolEnglishDescriptions.cs", "\n".join(english))
        self.write("OniMcp.csproj", '<Project><ItemGroup><EmbeddedResource Include="Localization/Catalogs/*.json" LogicalName="OniMcp.Localization.Catalogs.%(Filename)%(Extension)" WithCulture="false" /></ItemGroup></Project>')
        self.po_fixture()

    def po_fixture(self, aliases=None):
        aliases = aliases or {}
        catalogs = {}
        for language in C.LOCALES:
            catalogs[language] = {}
            for path in self.catalogs.glob("*." + language + ".json"):
                catalogs[language].update(json.loads(path.read_text()))
        for name, language in (("onimcp.pot", None), ("en-us.po", "en"), ("ja-jp.po", "ja"), ("zh-hant.po", "zh-Hant")):
            header = "Language: " + ("zh_Hant" if language == "zh-Hant" else language or "") + "\n"
            blocks = ['msgid ""\nmsgstr ' + json.dumps(header)]
            for key, english in sorted(catalogs["en"].items()):
                if key in aliases:
                    continue
                comments = ["#. Runtime JSON key: " + key]
                comments += ["#. Runtime JSON alias key: " + alias for alias, source in sorted(aliases.items()) if source == key]
                comments += ["#. Runtime zh-Hans msgstr: " + json.dumps(catalogs["zh-Hans"][key])]
                translated = "" if language is None else "繁體 " + english if language == "zh-Hant" else catalogs[language][key]
                fields = ['msgctxt ' + json.dumps("OniMcp.MCP." + key.upper()), 'msgid ' + json.dumps(english), 'msgstr ' + json.dumps(translated, ensure_ascii=False)]
                blocks.append("\n".join(comments + fields))
            self.write("Localization/po/" + name, "\n\n".join(blocks) + "\n")

    def aliased_fixture(self):
        self.full_fixture()
        self.group("po_alias", {"fixture.shared": "Value world_editor"})
        source = self.mod / C.TOOL_SOURCES["world_editor"]
        source.write_text(source.read_text().replace('Description = "Value world_editor"', 'Description = McpLocalization.Get("fixture.shared")'))
        self.po_fixture({"tools.world_editor.parameters.value": "fixture.shared"})

    def snapshot(self, metadata):
        result = {"tools": []}
        for name in C.TOOL_SOURCES:
            prefix = "tools." + name + ".parameters."
            properties = {key[len(prefix):]: {"type": "string"} for key in metadata if key.startswith(prefix)}
            properties["task"] = {"type": "string"}
            result["tools"].append({"name": name, "inputSchema": {"properties": properties, "required": ["task"]}})
        return {"jsonrpc": "2.0", "id": 1, "result": result}

    def test_12_all_group_metadata_namespace_and_core_references(self):
        self.full_fixture()
        audit = {}
        metadata = C.public_metadata(self.mod, audit=audit)
        self.assertEqual(metadata["tools.game_control.description"], "Tool game_control")
        self.assertEqual(metadata["tools.navigation_control.parameters.value"], "Value navigation_control")
        self.assertTrue(any(row["key"] == "tools.navigation_control.parameters.value" and row["catalog"] == "feature.en.json" for row in audit["references"]))
        self.assertIn("source contracts passed", C.verify(self.root))

    def test_13_cross_file_duplicate_even_with_equal_values_is_refused(self):
        self.full_fixture()
        self.group("duplicate", {"tools.game_control.description": "Tool game_control"})
        with self.assertRaisesRegex(ValueError, "Duplicate catalog key across resources"):
            C.verify(self.root)

    def test_14_canonical_coverage_gap_remains_failure(self):
        self.full_fixture()
        path = self.mod / C.TOOL_SOURCES["world_editor"]
        path.write_text(path.read_text().replace('Parameters = new Dictionary<string, McpToolParameter> { ', 'Parameters = new Dictionary<string, McpToolParameter> { ' + parameter("unmigrated", '"Existing fallback prose"') + ', '))
        with self.assertRaisesRegex(ValueError, "Public metadata coverage mismatch.*unmigrated"):
            C.verify(self.root)

    def test_15_unknown_core_override_expression_is_refused(self):
        self.full_fixture()
        path = self.mod / "Tools/Entry/CoreToolEnglishDescriptions.cs"
        path.write_text(path.read_text().replace('d["value"] = "Value building_control"', 'd["value"] = Unknown()'))
        with self.assertRaisesRegex(ValueError, "Unsupported description expression"):
            C.public_metadata(self.mod)

    def test_16_literal_english_change_is_still_refused(self):
        self.full_fixture()
        path = self.catalogs / "feature.en.json"
        entries = json.loads(path.read_text()); entries["tools.benchmark.parameters.value"] = "Changed fallback English."
        path.write_text(json.dumps(entries))
        with self.assertRaisesRegex(ValueError, "Existing effective English changed"):
            C.verify(self.root)

    def test_17_saved_tools_list_task_and_inventory_strictness(self):
        self.full_fixture()
        metadata = C.public_metadata(self.mod)
        good = self.snapshot(metadata)
        C.verify_tools_list_response(good, metadata)
        for index in range(len(good["result"]["tools"])):
            bad = json.loads(json.dumps(good)); bad["result"]["tools"][index]["inputSchema"]["required"] = []
            with self.subTest(index=index), self.assertRaisesRegex(ValueError, "Task missing"):
                C.verify_tools_list_response(bad, metadata)
        bad = json.loads(json.dumps(good)); bad["result"]["tools"][0]["inputSchema"]["properties"]["unadvertised"] = {"type": "string"}
        with self.assertRaisesRegex(ValueError, "Public metadata coverage mismatch"):
            C.verify_tools_list_response(bad, metadata)

    def test_18_existing_cli_root_snapshot_and_self_test_flags(self):
        self.full_fixture()
        path = self.root / "tools-list.json"; path.write_text(json.dumps(self.snapshot(C.public_metadata(self.mod))))
        stdout, stderr = StringIO(), StringIO()
        with patch.object(sys, "argv", [str(args.checker), "--root", str(self.root), "--tools-list", str(path), "--self-test"]), redirect_stdout(stdout), redirect_stderr(stderr):
            result = C.main()
        self.assertEqual(result, 0, stderr.getvalue())
        self.assertIn("Localization verifier regression checks passed.", stdout.getvalue())
        self.assertIn("Localization source contracts passed:", stdout.getvalue())


    def test_19_known_nonpublic_keys_and_unknown_namespace_orphans(self):
        self.full_fixture()
        known = {
            "tools.game_save_control.description": "Internal lifecycle.",
            "tools.game_save_control.parameters.action": "Internal action.",
            "tools.game_save_control.parameters.limit": "Internal limit.",
            "tools.game_save_control.parameters.query": "Internal query.",
            "tools.game_save_control.unsupported_action": "Internal unsupported action.",
        }
        self.group("internal", known)
        self.group("ordinary", {"save_status.non_metadata_prose": "Ordinary catalog prose."})
        audit = {}
        result = C.public_catalog(C.english_catalogs(self.mod), audit)
        self.assertTrue(set(known).isdisjoint(result))
        self.assertEqual({row["key"] for row in audit["excludedNonpublicTools"]}, set(known))
        self.assertTrue(all(row["source"] == "Tools/Impl/Core/GameSaveTools.cs" for row in audit["excludedNonpublicTools"]))
        self.assertIn("source contracts passed", C.verify(self.root))
        cases = {
            "tools.typo_control.description": "Unknown tools catalog namespace",
            "tools.typo_control.parameters.value": "Unknown tools catalog namespace",
            "tools.game_control.parameters.": "Malformed tools catalog key",
            "tools..description": "Malformed tools catalog key",
            "tools.task.parameters.value": "Unknown tools catalog namespace",
            "tools.game_save_control.parameters.orphan": "Unregistered nonpublic tools catalog key",
            "tools.world_editor.parameters.orphan": "Public metadata coverage mismatch",
        }
        for key, message in cases.items():
            with self.subTest(key=key):
                self.full_fixture()
                self.group("orphan", {key: "Unexpected metadata prose."})
                with self.assertRaisesRegex(ValueError, message):
                    C.verify(self.root)


    def test_20_alias_runtime_drift_is_rejected_by_existing_verify_api(self):
        # OLD_FULL_SCRIPT accepts this one-asset mutation. The new guard must
        # reject it semantically, without requiring a new test-only API.
        self.aliased_fixture()
        self.assertIn("source contracts passed", C.verify(self.root))
        path = self.catalogs / "feature.ja.json"
        entries = json.loads(path.read_text())
        entries["tools.world_editor.parameters.value"] = "ja Changed world_editor"
        path.write_text(json.dumps(entries))
        with self.assertRaisesRegex(ValueError, "PO runtime alias mismatch.*ja"):
            C.verify(self.root)

    def test_21_po_mapping_body_and_separate_hans_source_are_checked(self):
        for target, old, new, message in (
            ("ja-jp.po", 'msgstr "ja Value game_control"', 'msgstr "ja Changed game_control"', "PO Japanese runtime mapping mismatch"),
            ("onimcp.pot", '#. Runtime zh-Hans msgstr: "zh-Hans Value game_control"', '#. Runtime zh-Hans msgstr: "Changed Hans game_control"', "PO separate zh-Hans source mismatch"),
            ("zh-hant.po", "繁體 Value game_control", "繁體 Value game_control {0}", "PO placeholder mismatch"),
        ):
            with self.subTest(target=target):
                self.full_fixture()
                path = self.mod / "Localization/po" / target
                text = path.read_text()
                self.assertIn(old, text)
                path.write_text(text.replace(old, new))
                with self.assertRaisesRegex(ValueError, message):
                    C.verify(self.root)

    def test_22_alias_comments_and_hant_header_are_not_silently_reinterpreted(self):
        cases = (
            ("ja-jp.po", "#. Runtime JSON alias key: tools.world_editor.parameters.value\n", "", "PO runtime mapping comments differ"),
            ("onimcp.pot", "#. Runtime JSON alias key:", "#. Runtime JSON alias unknown:", "Unknown PO runtime alias syntax"),
            ("onimcp.pot", "#. Runtime JSON alias key: tools.world_editor.parameters.value", "#. Runtime JSON alias key: tools.world_editor.parameters.typo", "PO alias is not public metadata"),
            ("zh-hant.po", "zh_Hant", "zh_Hans", "Provisional Hant PO header"),
        )
        for target, old, new, message in cases:
            with self.subTest(target=target, mutation=old):
                self.aliased_fixture()
                path = self.mod / "Localization/po" / target
                text = path.read_text()
                self.assertIn(old, text)
                path.write_text(text.replace(old, new))
                with self.assertRaisesRegex(ValueError, message):
                    C.verify(self.root)

    def test_23_po_parser_refuses_unsupported_and_duplicate_records(self):
        for extra, message in (
            ('msgctxt "unrelated"\nmsgid "one"\nmsgid_plural "many"\nmsgstr[0] "one"\n', "Unsupported PO syntax"),
            ('msgctxt "OniMcp.MCP.TOOLS.GAME_CONTROL.DESCRIPTION"\nmsgid "Duplicate"\nmsgstr "Duplicate"\n', "Duplicate PO context"),
        ):
            with self.subTest(message=message):
                self.full_fixture()
                path = self.mod / "Localization/po/ja-jp.po"
                path.write_text(path.read_text() + "\n" + extra)
                with self.assertRaisesRegex(ValueError, message):
                    C.verify(self.root)

    def test_24_migrated_source_mapping_cannot_disappear_from_all_po_files(self):
        self.full_fixture()
        context = 'msgctxt "OniMcp.MCP.TOOLS.GAME_CONTROL.PARAMETERS.VALUE"'
        for name in ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po"):
            path = self.mod / "Localization/po" / name
            blocks = [block for block in path.read_text().split("\n\n") if context not in block]
            path.write_text("\n\n".join(blocks))
        with self.assertRaisesRegex(ValueError, "PO source reference has no mapping"):
            C.verify(self.root)


    def test_25_registered_factory_alias_cannot_disappear_from_all_po_files(self):
        self.full_fixture()
        canonical = "tools.building_control.parameters.storageScope"
        primary = "storage_components.scope_description"
        self.group("storage", {primary: "Scope: primary or all_components.", canonical: "Scope: primary or all_components."})
        self.write("Tools/Impl/Build/StorageComponentDetails.cs", 'internal static McpToolParameter StorageScopeParameter() { return new McpToolParameter { Type = "string", Description = McpLocalization.Get("storage_components.scope_description") }; }')
        source = self.mod / C.TOOL_SOURCES["building_control"]
        source.write_text(source.read_text().replace('Parameters = new Dictionary<string, McpToolParameter> { ', 'Parameters = new Dictionary<string, McpToolParameter> { ["storageScope"] = StorageTools.StorageScopeParameter(), '))
        self.po_fixture({canonical: primary})
        audit = {}
        C.public_metadata(self.mod, audit=audit)
        self.assertTrue(any(row["context"] == "building_control.storageScope:StorageScopeParameter" for row in audit["references"]))
        self.assertIn("source contracts passed", C.verify(self.root))
        for name in ("onimcp.pot", "en-us.po", "ja-jp.po", "zh-hant.po"):
            path = self.mod / "Localization/po" / name
            path.write_text(path.read_text().replace("#. Runtime JSON alias key: " + canonical + "\n", ""))
        with self.assertRaisesRegex(ValueError, "PO effective source metadata has no mapping.*storageScope"):
            C.verify(self.root)
        # Also keep the runtime-drift check effective for the actual registered
        # factory path, rather than only for a direct dictionary description.
        self.po_fixture({canonical: primary})
        path = self.catalogs / "storage.ja.json"
        entries = json.loads(path.read_text())
        entries[canonical] += " changed"
        path.write_text(json.dumps(entries))
        with self.assertRaisesRegex(ValueError, "PO runtime alias mismatch.*storageScope/ja"):
            C.verify(self.root)


if __name__ == "__main__":
    unittest.main(argv=[sys.argv[0], *test_args])
