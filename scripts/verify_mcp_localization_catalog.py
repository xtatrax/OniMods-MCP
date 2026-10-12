#!/usr/bin/env python3
"""Check localization catalog source contracts without starting ONI.

This is a supporting source check, not an ONI or compiled-assembly test. It
checks catalog key parity, .NET format placeholders, all public metadata keys,
unchanged existing English descriptions, and embedding declarations. An optional
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
CS_STRING = r'"(?:\\.|[^"\\])*"'
PARAMETER = re.compile(
    r'\["(\w+)"\]\s*=\s*new McpToolParameter\s*\{[^{}]*?'
    r'Description\s*=\s*(' + CS_STRING + r')', re.S
)
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


def braced_block(source: str, opening: int) -> tuple[str, int]:
    """Return a balanced C# brace block while skipping strings/comments."""
    require(opening >= 0 and source[opening] == "{", "Expected C# opening brace")
    depth = 0
    tokens = re.compile(CS_STRING + r'|//[^\n]*|/\*.*?\*/|[{}]', re.S)
    for token in tokens.finditer(source, opening):
        value = token.group(0)
        if value == "{":
            depth += 1
        elif value == "}":
            depth -= 1
            if depth == 0:
                return source[opening + 1:token.start()], token.end()
    raise ValueError("Unbalanced C# brace block")


def dictionary_parameters(block: str, context: str) -> dict[str, str]:
    pairs = PARAMETER.findall(block)
    require(len(pairs) == block.count("new McpToolParameter"), f"Unsupported parameter declaration: {context}")
    params = {key: string_literal(value) for key, value in pairs}
    require(len(params) == len(pairs), f"Duplicate parameter declaration: {context}")
    return params


def wrapper_parameters(mod: Path, tool: str, wrapper: str) -> dict[str, str]:
    path = PARAMETER_WRAPPERS.get((tool, wrapper))
    require(path is not None, f"Unsupported parameter wrapper: {tool}:{wrapper}")
    source = (mod / path).read_text(encoding="utf-8")
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
    return dictionary_parameters(block, f"{tool}:{wrapper}")


def constructor_parameters(mod: Path, source: str, tool: str) -> dict[str, str]:
    assignment = re.search(r'\bParameters\s*=\s*', source)
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
        return dictionary_parameters(block, tool)

    wrapper = re.match(r'(\w+)\s*\(\s*', expression)
    shared = {}
    if wrapper:
        shared = wrapper_parameters(mod, tool, wrapper.group(1))
        expression = expression[wrapper.end():]
    initializer = re.match(r'new Dictionary<string, McpToolParameter>\s*\{', expression)
    require(initializer is not None, f"Unsupported Parameters expression: {tool}")
    block, end = braced_block(expression, initializer.end() - 1)
    tail = r'\s*\)\s*,\s*Handler\s*=' if wrapper else r'\s*,\s*Handler\s*='
    require(re.match(tail, expression[end:]) is not None, f"Unsupported Parameters expression tail: {tool}")
    return shared | dictionary_parameters(block, tool)


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


def public_metadata(mod: Path) -> dict[str, str]:
    registry = (mod / "Tools/Core/OniToolRegistry.cs").read_text(encoding="utf-8")
    names = re.search(r'DefaultPublicToolNames\s*=.*?\{(.*?)\};', registry, re.S)
    require(names is not None, "Cannot locate DefaultPublicToolNames")
    public = set(re.findall(CS_STRING, names.group(1)))
    public = {string_literal(name) for name in public}
    require(public == set(TOOL_SOURCES), f"Update TOOL_SOURCES for public tool set: {sorted(public)}")
    coordinate_method = registry.split("internal static bool IsCoordinateParameter(", 1)[1].split("default:", 1)[0]
    coordinate_fields = set(re.findall(r'case "(\w+)":', coordinate_method))
    require(bool(coordinate_fields), "Could not extract coordinate parameter exclusions")
    english = (mod / "Tools/Entry/CoreToolEnglishDescriptions.cs").read_text(encoding="utf-8")
    overrides = {}
    for name, body in re.findall(
        r'private static Dictionary<string, string> (\w+)Descriptions\(\)\s*\{(.*?)\n        \}', english, re.S
    ):
        overrides[name] = {
            key: string_literal(value)
            for key, value in re.findall(r'(?:d)?\["(\w+)"\]\s*=\s*(' + CS_STRING + r')', body)
        }

    result = {}
    for tool, relative in TOOL_SOURCES.items():
        source = (mod / relative).read_text(encoding="utf-8")
        description = re.search(r'Description\s*=\s*(' + CS_STRING + r')', source)
        require(description is not None, f"Cannot extract description: {tool}")
        description = string_literal(description.group(1))
        params = constructor_parameters(mod, source, tool)
        if tool in OVERRIDE_METHODS:
            match = re.search(r'case "' + tool + r'":\s*tool.Description\s*=\s*(' + CS_STRING + r')', english)
            require(match is not None, f"Missing English override: {tool}")
            description = string_literal(match.group(1))
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

    effective = public_metadata(mod)
    tools = groups["tools"]["en"]
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
                require(token in groups["tools"][locale][key], f"Code token {token!r} missing: tools.{locale}:{key}")

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
