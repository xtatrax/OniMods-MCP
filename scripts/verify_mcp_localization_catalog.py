#!/usr/bin/env python3
"""Check localization catalog source contracts without starting ONI.

This is a supporting source check, not an ONI or compiled-assembly test. It
checks catalog key parity, .NET format placeholders, canonical public metadata keys,
unchanged existing English descriptions, and embedding declarations. It resolves constant catalog references across resource
groups and explicitly audits indexed-parameter shadows. An optional
saved tools/list response checks the actual emitted parameter inventory. Host
tests must separately check runtime selection, fallback, cloning, and resources.

The small source reader intentionally fails on unfamiliar parameter declaration
shapes. Add an extractor for a new shape rather than silently skipping it.
"""

from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path
import re
import sys
import warnings
import xml.etree.ElementTree as ET


LOCALES = ("en", "ja", "zh-Hans")
# The source map is checked against DefaultPublicToolNames below, so adding a
# public tool requires an explicit choice of its constructor source here.
TOOL_SOURCES = {
    "building_control": "Tools/Entry/BuildingControlTools.cs",
    "game_control": "Tools/Impl/Core/GameControlTools.cs",
    "navigation_control": "Tools/Entry/NavigationControlTools.cs",
    "orders_control": "Tools/Impl/Orders/OrdersTools.cs",
    "server_control": "Tools/Impl/Server/ServerTools.cs",
    "world_editor": "Tools/WorldEditor/WorldEditorTools.cs",
    "benchmark": "Tools/Entry/BenchmarkTools.cs",
}
# Exact observed keys for the non-default-public lifecycle child. These five
# keys are referenced by ControlGameSave in Tools/Impl/Core/GameSaveTools.cs;
# game_save_control is not in OniToolRegistry.DefaultPublicToolNames. This is
# not a wildcard exemption for unknown tools.* namespaces or orphan keys.
NONPUBLIC_TOOL_CATALOGS = {
    "game_save_control": {
        "source": "Tools/Impl/Core/GameSaveTools.cs",
        "keys": frozenset({
            "tools.game_save_control.description",
            "tools.game_save_control.parameters.action",
            "tools.game_save_control.parameters.limit",
            "tools.game_save_control.parameters.query",
            "tools.game_save_control.unsupported_action",
        }),
    },
}
OVERRIDE_METHODS = {
    "building_control": "Building",
    "game_control": "Game",
    "navigation_control": "Navigation",
    "orders_control": "Orders",
    "server_control": "Server",
}
# Parameter factories are part of the emitted schema. A constructor's inline
# dictionary is not complete when a wrapper adds shared parameters. Register
# each wrapper by calling tool as well as name, since partial classes can have
# unrelated private methods with the same name.
PARAMETER_WRAPPERS = {
    ("orders_control", "RectParams"): "Tools/Impl/Orders/OrdersPriorityTools.cs",
}
# One observed pure parameter factory; unknown calls remain unsupported.
PARAMETER_FACTORIES = {
    ("building_control", "storageScope", "StorageTools.StorageScopeParameter"):
        ("Tools/Impl/Build/StorageComponentDetails.cs", "StorageScopeParameter"),
}
CS_STRING = r'"(?:\\.|[^"\\])*"'
PLACEHOLDER = re.compile(r'(?<!\{)\{(\d+(?:,-?\d+)?(?::[^{}]+)?)\}(?!\})')
JAPANESE = re.compile(r'[\u3040-\u30ff]')


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def string_literal(text: str) -> str:
    # Current descriptions use regular quoted strings with JSON-compatible
    # escapes. Unsupported C# literal forms must not be silently normalized.
    return json.loads(text)


def unique_object(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        require(key not in result, f"Duplicate catalog key: {key}")
        result[key] = value
    return result


def read_catalog(path: Path) -> dict[str, str]:
    require(path.is_file(), f"Missing catalog: {path}")
    data = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    require(isinstance(data, dict) and bool(data), f"Catalog must be a nonempty object: {path}")
    for key, value in data.items():
        require(isinstance(key, str) and bool(key.strip()), f"Invalid key in {path}")
        require(isinstance(value, str) and bool(value.strip()), f"Empty/non-string value {path.name}:{key}")
    return data


# This remains a bounded source reader, not a C# evaluator. Only regular quoted
# literals and constant-key McpLocalization.Get/GetForLanguage("en", key) prose
# expressions are supported. Other expressions must be added deliberately.
_LEXICAL = re.compile(CS_STRING + '|//[^\\n]*|/\\*.*?\\*/', re.S)


class CatalogValues(dict):
    """Merged values retain their resource origin for the separate source audit."""
    def __init__(self):
        super().__init__()
        self.origins = {}


def code_mask(source: str, comments_only: bool = False) -> str:
    def replace(match):
        text = match.group(0)
        if comments_only and not text.startswith(("//", "/*")):
            return text
        return "".join("\n" if char == "\n" else " " for char in text)
    return _LEXICAL.sub(replace, source)


def split_top_level(source: str, separator: str) -> list[str]:
    """Split only outside balanced initializers/calls/indices and literals."""
    mask = code_mask(source)
    stack = []
    opening = {"(": ")", "[": "]", "{": "}"}
    parts = []
    start = 0
    for index, char in enumerate(mask):
        if char in opening:
            stack.append(opening[char])
        elif char in ")]}":
            require(bool(stack) and stack.pop() == char, "Unbalanced C# expression")
        elif char == separator and not stack:
            parts.append(source[start:index])
            start = index + 1
    require(not stack, "Unbalanced C# expression")
    parts.append(source[start:])
    return parts


def leading_expression(source: str, context: str) -> str:
    mask = code_mask(source)
    stack = []
    opening = {"(": ")", "[": "]", "{": "}"}
    for index, char in enumerate(mask):
        if char in opening:
            stack.append(opening[char])
        elif char in ")]}":
            require(bool(stack) and stack.pop() == char, "Unbalanced expression: " + context)
        elif char in ",;" and not stack:
            return source[:index].strip()
    require(not stack, "Unbalanced expression: " + context)
    return source.strip()


def merge_catalog(target: CatalogValues, entries: dict[str, str], origin: str) -> None:
    for key, value in entries.items():
        require(key not in target, "Duplicate catalog key across resources: " + key
                + " (" + target.origins.get(key, "") + ", " + origin + ")")
        target[key] = value
        target.origins[key] = origin


def english_catalogs(mod: Path) -> CatalogValues:
    result = CatalogValues()
    files = sorted((mod / "Localization/Catalogs").glob("*.en.json"))
    require(bool(files), "No English localization catalogs found")
    for path in files:
        merge_catalog(result, read_catalog(path), path.name)
    return result


def description_value(expression: str, catalog: dict[str, str], context: str,
                      audit: dict | None = None) -> str:
    expression = code_mask(expression, comments_only=True).strip()
    if re.fullmatch(CS_STRING, expression):
        return string_literal(expression)
    reference = re.fullmatch(
        r'McpLocalization\s*\.\s*(Get|GetForLanguage)\s*\(\s*(' + CS_STRING
        + r')(?:\s*,\s*(' + CS_STRING + r'))?\s*\)', expression)
    require(reference is not None, "Unsupported description expression: " + context)
    method, first, second = reference.groups()
    require((method == "Get" and second is None)
            or (method == "GetForLanguage" and second is not None and string_literal(first) == "en"),
            "Unsupported localization reference shape: " + context)
    key = string_literal(first if method == "Get" else second)
    require(bool(key) and key in catalog, "Missing catalog reference: " + context + ":" + key)
    if audit is not None:
        audit.setdefault("references", []).append({"context": context, "method": method,
            "key": key, "baselineLanguage": "en", "catalog": getattr(catalog, "origins", {}).get(key)})
    return catalog[key]


def assigned_description(source: str, context: str) -> str:
    match = re.search(r'\bDescription\s*=', code_mask(source))
    require(match is not None, "Missing description assignment: " + context)
    return leading_expression(source[match.end():], context)


def object_description(body: str, context: str) -> str:
    values = []
    for item in split_top_level(body, ","):
        item = code_mask(item, comments_only=True).strip()
        if not item:
            continue
        match = re.match(r'(\w+)\s*=\s*', item)
        require(match is not None, "Unsupported parameter member: " + context)
        if match.group(1) == "Description":
            values.append(item[match.end():])
    require(len(values) == 1, "Expected one top-level parameter description: " + context)
    return values[0]


def record_shadow(audit: dict | None, context: str, key: str, previous: int,
                  replacement: int) -> None:
    event = {"context": context, "parameter": key, "previousIndex": previous,
             "replacementIndex": replacement, "semantics": "indexed_initializer_last_assignment_wins"}
    if audit is None:
        warnings.warn("Indexed parameter shadow audit: " + json.dumps(event, sort_keys=True),
                      RuntimeWarning, stacklevel=3)
    else:
        audit.setdefault("indexedParameterShadows", []).append(event)


def public_catalog(catalog: dict[str, str], audit: dict | None = None) -> dict[str, str]:
    result = {}
    excluded = {key: (name, entry["source"]) for name, entry in NONPUBLIC_TOOL_CATALOGS.items()
                for key in entry["keys"]}
    for key, value in catalog.items():
        if not key.startswith("tools."):
            continue
        if key == "tools.task.description":
            result[key] = value
            continue
        if key in excluded:
            namespace, source = excluded[key]
            require(namespace not in TOOL_SOURCES, "Excluded tool namespace is now public; update policy: " + namespace)
            if audit is not None:
                audit.setdefault("excludedNonpublicTools", []).append({"key": key,
                    "namespace": namespace, "source": source,
                    "catalog": getattr(catalog, "origins", {}).get(key)})
            continue
        match = re.fullmatch('tools\\.([A-Za-z_][A-Za-z_0-9]*)\\.(?:description|parameters\\.[A-Za-z_][A-Za-z_0-9]*)', key)
        require(match is not None, "Malformed tools catalog key: " + key)
        namespace = match.group(1)
        require(namespace not in NONPUBLIC_TOOL_CATALOGS, "Unregistered nonpublic tools catalog key: " + key)
        require(namespace in TOOL_SOURCES, "Unknown tools catalog namespace: " + namespace)
        result[key] = value
    return result



def english_overrides(source: str, catalog: dict[str, str], audit: dict | None = None) -> dict:
    methods = {}
    mask = code_mask(source)
    for name in {"Common", *OVERRIDE_METHODS.values()}:
        signature = re.search(r'private static Dictionary<string, string> ' + re.escape(name)
                              + r'Descriptions\(\)\s*\{', mask)
        require(signature is not None, "Cannot locate English description method: " + name)
        body, _ = braced_block(source, signature.end() - 1)
        body = code_mask(body, comments_only=True)
        if name == "Common":
            initializer = re.match(r'\s*return new Dictionary<string, string>\s*\{', body)
            require(initializer is not None, "Unsupported CommonDescriptions initializer")
            block, end = braced_block(body, initializer.end() - 1)
            require(re.fullmatch(r'\s*;\s*', body[end:]) is not None, "Unsupported CommonDescriptions tail")
            values = {}
            for item in split_top_level(block, ","):
                if not item.strip():
                    continue
                entry = re.fullmatch(r'\s*\[\s*(' + CS_STRING + r')\s*\]\s*=\s*(.*?)\s*', item, re.S)
                require(entry is not None, "Unsupported CommonDescriptions entry")
                key = string_literal(entry.group(1))
                require(key not in values, "Duplicate CommonDescriptions entry: " + key)
                values[key] = description_value(entry.group(2), catalog, "CommonDescriptions." + key, audit)
            methods[name] = values
        else:
            statements = [part.strip() for part in split_top_level(body, ";") if part.strip()]
            require(len(statements) >= 2 and re.fullmatch(r'var d\s*=\s*CommonDescriptions\(\)', statements[0]) is not None
                    and statements[-1] == "return d", "Unsupported English description method: " + name)
            values = {}
            for statement in statements[1:-1]:
                entry = re.fullmatch(r'd\[\s*(' + CS_STRING + r')\s*\]\s*=\s*(.*?)', statement, re.S)
                require(entry is not None, "Unsupported English description update: " + name)
                key = string_literal(entry.group(1))
                values[key] = description_value(entry.group(2), catalog, name + "Descriptions." + key, audit)
            methods[name] = values
    return methods


def braced_block(source: str, opening: int) -> tuple[str, int]:
    """Return a balanced C# brace block while skipping strings/comments."""
    require(opening >= 0 and source[opening] == "{", "Expected C# opening brace")
    depth = 0
    for index, char in enumerate(code_mask(source)[opening:], opening):
        if char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return source[opening + 1:index], index + 1
    raise ValueError("Unbalanced C# brace block")


def parameter_factory(mod: Path | None, context: str, key: str, expression: str,
                      catalog: dict[str, str], audit: dict | None) -> str:
    call = re.fullmatch(r"([A-Za-z_][A-Za-z_0-9.]*)\s*\(\s*\)", expression)
    spec = PARAMETER_FACTORIES.get((context, key, call.group(1))) if call else None
    require(mod is not None and spec is not None, "Unsupported parameter factory: " + context + "." + key)
    path, method = spec
    source = code_mask((mod / path).read_text(encoding="utf-8"), comments_only=True)
    signature = re.search(r"internal static McpToolParameter " + re.escape(method) + r"\(\)\s*\{", source)
    require(signature is not None, "Cannot locate parameter factory: " + context + "." + key)
    body, _ = braced_block(source, signature.end() - 1)
    initializer = re.match(r"\s*return new McpToolParameter\s*\{", body)
    require(initializer is not None, "Unsupported parameter factory body: " + context + "." + key)
    block, end = braced_block(body, initializer.end() - 1)
    require(re.fullmatch(r"\s*;\s*", body[end:]) is not None, "Unsupported parameter factory tail: " + context + "." + key)
    if audit is not None:
        audit.setdefault("parameterFactories", []).append({"context": context, "parameter": key,
            "call": call.group(1), "source": path, "method": method})
    return description_value(object_description(block, context + "." + key), catalog,
                             context + "." + key + ":" + method, audit)


def dictionary_parameters(block: str, context: str, catalog: dict[str, str] | None = None,
                          audit: dict | None = None, mod: Path | None = None) -> dict[str, str]:
    catalog = {} if catalog is None else catalog
    params = {}
    indices = {}
    parts = split_top_level(block, ",")
    for index, item in enumerate(parts):
        item = code_mask(item, comments_only=True).strip()
        if not item:
            require(index == len(parts) - 1, "Unsupported empty parameter entry: " + context)
            continue
        assignment = re.match(r'\[\s*(' + CS_STRING + r')\s*\]\s*=\s*', item)
        require(assignment is not None, "Unsupported parameter declaration: " + context)
        key = string_literal(assignment.group(1))
        require(re.fullmatch(r'\w+', key) is not None, "Unsupported parameter name: " + context)
        expression = item[assignment.end():]
        header = re.match(r'new McpToolParameter\s*\{', expression)
        if header:
            body, end = braced_block(expression, header.end() - 1)
            require(not expression[end:].strip(), "Unsupported parameter declaration tail: " + context)
            value = description_value(object_description(body, context + "." + key), catalog, context + "." + key, audit)
        else:
            value = parameter_factory(mod, context, key, expression, catalog, audit)
        if key in params:
            record_shadow(audit, context, key, indices[key], index)
        params[key] = value
        indices[key] = index
    return params



def wrapper_parameters(mod: Path, tool: str, wrapper: str, catalog: dict[str, str] | None = None,
                       audit: dict | None = None) -> dict[str, str]:
    path = PARAMETER_WRAPPERS.get((tool, wrapper))
    require(path is not None, f"Unsupported parameter wrapper: {tool}:{wrapper}")
    source = code_mask((mod / path).read_text(encoding="utf-8"), comments_only=True)
    signature = re.search(
        r'private static Dictionary<string, McpToolParameter> ' + re.escape(wrapper)
        + r'\(Dictionary<string, McpToolParameter> extra\)\s*\{', source
    )
    require(signature is not None, f"Cannot locate parameter wrapper: {tool}:{wrapper}")
    body, _ = braced_block(source, signature.end() - 1)
    initializer = re.match(r'\s*var parameters\s*=\s*new Dictionary<string, McpToolParameter>\s*\{', body)
    require(initializer is not None, f"Unsupported wrapper initializer: {tool}:{wrapper}")
    block, end = braced_block(body, initializer.end() - 1)
    # Model the observed merge order exactly: base dictionary first, then extra
    # overwrites its keys. Reject additional mutations/helper calls rather than
    # reporting completeness from only the visible dictionary literals.
    require(re.fullmatch(
        r'\s*;\s*foreach\s*\(var item in extra\)\s*'
        r'parameters\[item.Key\]\s*=\s*item.Value;\s*return parameters;\s*', body[end:]
    ) is not None, f"Unsupported parameter wrapper merge: {tool}:{wrapper}")
    return dictionary_parameters(block, f"{tool}:{wrapper}", catalog, audit, mod)


def constructor_parameters(mod: Path, source: str, tool: str, catalog: dict[str, str] | None = None,
                           audit: dict | None = None) -> dict[str, str]:
    catalog = english_catalogs(mod) if catalog is None else catalog
    source = code_mask(source, comments_only=True)
    assignment = re.search(r'\bParameters\s*=\s*', code_mask(source))
    require(assignment is not None, f"Missing Parameters assignment: {tool}")
    expression = source[assignment.end():]
    helper = re.match(r'Params\(\)\s*,', expression)
    if helper:
        signature = re.search(r'private static Dictionary<string, McpToolParameter> Params\(\)\s*\{', source)
        require(signature is not None, f"Cannot locate Params helper: {tool}")
        body, _ = braced_block(source, signature.end() - 1)
        initializer = re.match(r'\s*return new Dictionary<string, McpToolParameter>\s*\{', body)
        require(initializer is not None, f"Unsupported Params helper: {tool}")
        block, end = braced_block(body, initializer.end() - 1)
        require(re.fullmatch(r'\s*;\s*', body[end:]) is not None, f"Unsupported Params helper tail: {tool}")
        return dictionary_parameters(block, tool, catalog, audit, mod)

    wrapper = re.match(r'(\w+)\s*\(\s*', expression)
    shared = {}
    if wrapper:
        shared = wrapper_parameters(mod, tool, wrapper.group(1), catalog, audit)
        expression = expression[wrapper.end():]
    initializer = re.match(r'new Dictionary<string, McpToolParameter>\s*\{', expression)
    require(initializer is not None, f"Unsupported Parameters expression: {tool}")
    block, end = braced_block(expression, initializer.end() - 1)
    tail = r'\s*\)\s*,\s*Handler\s*=' if wrapper else r'\s*,\s*Handler\s*='
    require(re.match(tail, expression[end:]) is not None, f"Unsupported Parameters expression tail: {tool}")
    return shared | dictionary_parameters(block, tool, catalog, audit, mod)


def require_metadata_keys(effective: dict[str, str], catalog: dict[str, str]) -> None:
    missing = set(effective) - set(catalog)
    extra = set(catalog) - set(effective)
    require(not missing and not extra, f"Public metadata coverage mismatch; missing={sorted(missing)}, extra={sorted(extra)}")


def verify_tools_list(path: Path, catalog: dict[str, str]) -> None:
    """Inspect a saved JSON-RPC tools/list response; never call the game."""
    response = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    verify_tools_list_response(response, catalog)


def verify_tools_list_response(response: object, catalog: dict[str, str]) -> None:
    """Validate the emitted per-tool task contract as well as catalog coverage."""
    require(isinstance(response, dict), "tools/list snapshot must be a JSON object")
    result = response.get("result", response)
    require(isinstance(result, dict) and isinstance(result.get("tools"), list), "Expected tools/list result.tools array")
    names = set()
    effective = {}
    for tool in result["tools"]:
        require(isinstance(tool, dict) and isinstance(tool.get("name"), str), "Invalid tool in tools/list snapshot")
        name = tool["name"]
        require(name not in names, f"Duplicate tool in tools/list snapshot: {name}")
        names.add(name)
        effective[f"tools.{name}.description"] = ""
        schema = tool.get("inputSchema")
        require(isinstance(schema, dict), f"Missing inputSchema for {name}")
        properties = schema.get("properties")
        require(isinstance(properties, dict), f"Missing inputSchema.properties for {name}")
        # BuildToolInfos injects task into every public tool and marks it
        # required. A shared catalog entry is not evidence that any particular
        # runtime tool still exposes that property or requires callers to send it.
        require("task" in properties, f"Missing required task property for {name}")
        task = properties["task"]
        require(isinstance(task, dict) and task.get("type") == "string", f"Task property must have type string for {name}")
        required = schema.get("required")
        require(isinstance(required, list) and "task" in required, f"Task missing from inputSchema.required for {name}")
        effective["tools.task.description"] = ""
        for parameter in properties:
            if parameter != "task":
                effective[f"tools.{name}.parameters.{parameter}"] = ""
    require(names == set(TOOL_SOURCES), f"Unexpected tools/list tool set: {sorted(names)}")
    require_metadata_keys(effective, catalog)


def self_test(root: Path) -> None:
    """Regression checks for parameter factories, worldId, and required task."""
    mod = root / "mods/OniMcp"
    source = (mod / TOOL_SOURCES["orders_control"]).read_text(encoding="utf-8")
    parameters = constructor_parameters(mod, source, "orders_control")
    require("worldId" in parameters, "Regression: RectParams worldId was not extracted")
    require({"x1", "y1", "x2", "y2"} <= set(parameters), "Regression: RectParams coordinates were not extracted before filtering")
    synthetic = 'Parameters = RectParams(new Dictionary<string, McpToolParameter> {' \
        '["worldId"] = new McpToolParameter { Description = "Caller override" } }), Handler = args => null'
    require(constructor_parameters(mod, synthetic, "orders_control")["worldId"] == "Caller override", "Regression: caller parameters did not override wrapper defaults")
    try:
        constructor_parameters(mod, synthetic.replace("RectParams", "UnknownParams"), "orders_control")
    except ValueError as error:
        require("Unsupported parameter wrapper" in str(error), f"Unexpected wrapper rejection: {error}")
    else:
        raise ValueError("Regression: unknown parameter wrapper was silently ignored")
    metadata = public_metadata(mod)
    key = "tools.orders_control.parameters.worldId"
    require(key in metadata, "Regression: public worldId key was omitted")
    for parameter in ("x1", "y1", "x2", "y2"):
        require(f"tools.orders_control.parameters.{parameter}" not in metadata, "Regression: hidden coordinate parameter became public")
    omitted = dict(metadata)
    omitted.pop(key)
    try:
        require_metadata_keys(metadata, omitted)
    except ValueError as error:
        require(key in str(error), f"Unexpected omission rejection: {error}")
    else:
        raise ValueError("Regression: the missing public worldId key was accepted")

    # Synthetic tools/list has the complete current source-derived inventory.
    # Each mutation below must fail at the affected tool, including when the
    # shared task metadata key still exists in the catalog.
    response = {"tools": []}
    for name in TOOL_SOURCES:
        prefix = f"tools.{name}.parameters."
        properties = {key[len(prefix):]: {"type": "string"} for key in metadata if key.startswith(prefix)}
        properties["task"] = {"type": "string"}
        response["tools"].append({"name": name, "inputSchema": {"properties": properties, "required": ["task"]}})
    verify_tools_list_response(response, metadata)
    verify_tools_list_response({"jsonrpc": "2.0", "id": 1, "result": response}, metadata)

    def rejected(snapshot: dict, expected: str) -> None:
        try:
            verify_tools_list_response(snapshot, metadata)
        except ValueError as error:
            require(expected in str(error), f"Unexpected tools/list rejection: {error}")
        else:
            raise ValueError(f"Regression: invalid tools/list was accepted; expected {expected}")

    for index, tool in enumerate(response["tools"]):
        name = tool["name"]
        missing_task = json.loads(json.dumps(response))
        missing_task["tools"][index]["inputSchema"]["properties"].pop("task")
        rejected(missing_task, f"Missing required task property for {name}")
        optional_task = json.loads(json.dumps(response))
        optional_task["tools"][index]["inputSchema"]["required"].remove("task")
        rejected(optional_task, f"Task missing from inputSchema.required for {name}")
    all_missing = json.loads(json.dumps(response))
    for tool in all_missing["tools"]:
        tool["inputSchema"]["properties"].pop("task")
    rejected(all_missing, "Missing required task property")
    no_required = json.loads(json.dumps(response))
    no_required["tools"][0]["inputSchema"].pop("required")
    rejected(no_required, "Task missing from inputSchema.required")
    wrong_type = json.loads(json.dumps(response))
    wrong_type["tools"][0]["inputSchema"]["properties"]["task"]["type"] = "boolean"
    rejected(wrong_type, "Task property must have type string")


def public_metadata(mod: Path, catalog: dict[str, str] | None = None,
                    audit: dict | None = None) -> dict[str, str]:
    catalog = english_catalogs(mod) if catalog is None else catalog
    registry = (mod / "Tools/Core/OniToolRegistry.cs").read_text(encoding="utf-8")
    names = re.search(r'DefaultPublicToolNames\s*=.*?\{(.*?)\};', registry, re.S)
    require(names is not None, "Cannot locate DefaultPublicToolNames")
    public = set(re.findall(CS_STRING, names.group(1)))
    public = {string_literal(name) for name in public}
    require(public == set(TOOL_SOURCES), f"Update TOOL_SOURCES for public tool set: {sorted(public)}")
    coordinate_method = registry.split("internal static bool IsCoordinateParameter(", 1)[1].split("default:", 1)[0]
    coordinate_fields = set(re.findall(r'case "(\w+)":', coordinate_method))
    require(bool(coordinate_fields), "Could not extract coordinate parameter exclusions")
    english = code_mask((mod / "Tools/Entry/CoreToolEnglishDescriptions.cs").read_text(encoding="utf-8"), comments_only=True)
    overrides = english_overrides(english, catalog, audit)

    result = {}
    for tool, relative in TOOL_SOURCES.items():
        source = (mod / relative).read_text(encoding="utf-8")
        description = description_value(assigned_description(source, tool), catalog, tool + ".description", audit)
        params = constructor_parameters(mod, source, tool, catalog, audit)
        if tool in OVERRIDE_METHODS:
            match = re.search(r'case "' + tool + r'":\s*tool.Description\s*=\s*', code_mask(english, comments_only=True))
            require(match is not None, f"Missing English override: {tool}")
            description = description_value(leading_expression(english[match.end():], tool), catalog,
                                            "CoreToolEnglishDescriptions." + tool, audit)
            effective = overrides["Common"] | overrides[OVERRIDE_METHODS[tool]]
            params = {key: effective.get(key, value) for key, value in params.items()}
        result[f"tools.{tool}.description"] = description
        for name, value in params.items():
            if name == "task" or (tool != "world_editor" and name in coordinate_fields):
                continue
            result[f"tools.{tool}.parameters.{name}"] = value
    task = re.search(
        r'properties\[ToolCallMiddleware.TaskDescriptionParameter\]\s*=.*?Description\s*=\s*(' + CS_STRING + r')',
        registry, re.S
    )
    require(task is not None, "Cannot extract shared task description")
    result["tools.task.description"] = string_literal(task.group(1))
    return result


def verify(root: Path, tools_list: Path | None = None) -> str:
    mod = root / "mods/OniMcp"
    directory = mod / "Localization/Catalogs"
    files = sorted(directory.glob("*.json"))
    require(bool(files), "No localization catalogs found")
    groups = {}
    for path in files:
        stem, locale = path.stem.rsplit(".", 1)
        require(locale in LOCALES, f"Unexpected locale: {path.name}")
        groups.setdefault(stem, {})[locale] = read_catalog(path)
    require("tools" in groups, "Missing tools catalogs")
    for stem, locales in groups.items():
        require(set(locales) == set(LOCALES), f"Missing locale for {stem}")
        baseline = locales["en"]
        for locale in LOCALES:
            require(set(locales[locale]) == set(baseline), f"Catalog key mismatch: {stem}.{locale}")
            for key, value in locales[locale].items():
                require(
                    Counter(PLACEHOLDER.findall(value)) == Counter(PLACEHOLDER.findall(baseline[key])),
                    f"Placeholder mismatch: {stem}.{locale}:{key}"
                )

    merged = {locale: CatalogValues() for locale in LOCALES}
    for stem, locales in groups.items():
        for locale, entries in locales.items():
            merge_catalog(merged[locale], entries, stem + "." + locale + ".json")
    audit = {}
    effective = public_metadata(mod, merged["en"], audit)
    shadows = audit.get("indexedParameterShadows", [])
    if shadows:
        print("Localization indexed-parameter shadow audit: " + json.dumps(shadows, sort_keys=True), file=sys.stderr)
    tools = public_catalog(merged["en"], audit)
    excluded = audit.get("excludedNonpublicTools", [])
    if excluded:
        print("Localization known nonpublic catalog audit: " + json.dumps(excluded, sort_keys=True), file=sys.stderr)
    require_metadata_keys(effective, tools)
    if tools_list is not None:
        verify_tools_list(tools_list, tools)
    for key, source in effective.items():
        if not JAPANESE.search(source):
            require(tools[key] == source, f"Existing effective English changed: {key}")
        else:
            require(not JAPANESE.search(tools[key]), f"English catalog still contains Japanese prose: {key}")
        # Preserve recognizable code tokens without treating ordinary English
        # prose as an API. This complements, rather than replaces, translation
        # review of examples, units, defaults, and safety conditions.
        code_tokens = {
            token for token in re.findall(r'[A-Za-z][A-Za-z_0-9]*', tools[key])
            if '_' in token or re.search(r'[a-z][A-Z]|[A-Za-z][0-9]', token)
            or token in ("true", "false")
        }
        for locale in LOCALES:
            for token in code_tokens:
                require(token in merged[locale][key], f"Code token {token!r} missing: tools.{locale}:{key}")

    project = ET.parse(mod / "OniMcp.csproj").getroot()
    declarations = [item for item in project.iter() if item.tag.rsplit("}", 1)[-1] == "EmbeddedResource"]
    embedded = set()
    for item in declarations:
        pattern = item.attrib.get("Include", "").replace("\\", "/")
        if not pattern.startswith("Localization/Catalogs/"):
            continue
        require(item.attrib.get("WithCulture", "").lower() == "false", "Catalogs must use WithCulture=false to stay in the main assembly")
        expected_name = "OniMcp.Localization.Catalogs.%(Filename)%(Extension)"
        require(item.attrib.get("LogicalName") == expected_name, "Unexpected localization resource LogicalName")
        embedded.update(path.resolve() for path in mod.glob(pattern) if path.is_file())
    require(all(path.resolve() in embedded for path in files), "Some catalogs are not embedded by OniMcp.csproj")
    return f"Localization source contracts passed: {len(groups)} catalog groups, {len(effective)} public metadata keys, {len(files)} embedded catalogs."


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--tools-list", type=Path, help="Saved JSON-RPC tools/list response or its result object; read-only runtime inventory check")
    parser.add_argument("--self-test", action="store_true", help="Run parameter-factory regression checks before catalog verification")
    args = parser.parse_args()
    try:
        if args.self_test:
            self_test(args.root.resolve())
            print("Localization verifier regression checks passed.")
        print(verify(args.root.resolve(), args.tools_list))
    except (ValueError, KeyError, IndexError, ET.ParseError, OSError) as error:
        print(f"Localization source contract failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
