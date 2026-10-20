#!/usr/bin/env python3
"""Verify that free-build utility paths retain blueprint placement semantics."""

from __future__ import annotations

from contextlib import redirect_stderr
from io import StringIO
from pathlib import Path

from onimcp_verify_parsing import fail, matching_delimiter


def extract_block(text: str, marker: str) -> str:
    marker_index = text.find(marker)
    if marker_index < 0:
        fail(f"missing marker: {marker}")
    open_index = text.find("{", marker_index)
    if open_index < 0:
        fail(f"missing block after marker: {marker}")
    close_index = matching_delimiter(text, open_index, "{", "}")
    return text[open_index : close_index + 1]


def require_order(text: str, markers: tuple[str, ...], label: str) -> None:
    cursor = 0
    for marker in markers:
        position = text.find(marker, cursor)
        if position < 0:
            fail(f"{label}: expected ordered markers {markers}")
        cursor = position + len(marker)


def verify_existing_material_reuse(plan_one: str) -> None:
    early_guard = (
        'if (earlyExistingBuild != null && ExistingMaterialRequestSatisfied('
        'def, earlyExistingBuild, args["material"]?.ToString()))'
    )
    require_order(
        plan_one,
        (
            "var earlyExistingBuild = ExistingMatchingBuildAtPlacement(def, earlyPlacement);",
            early_guard,
            'var materialResult = SelectElements(def, args["material"]?.ToString(), worldId);',
        ),
        "early reuse must test the explicit material in the branch condition",
    )

    # Check each actual reuse block. A global count can be satisfied by an
    # unrelated call and cannot prove that both paths reject before completion.
    for target in ("existingBuild", "executionExistingBuild", "existingUtility", "executionExistingUtility"):
        branch = extract_block(plan_one, f"if ({target} != null)")
        reuse_marker = (
            "RegisterSupportBlueprint(" if target == "existingUtility" else
            "return ExistingPlacementResult(" if target == "executionExistingUtility" else
            "var instantRetry = TryCompleteExistingVirtualFileBlueprint("
        )
        require_order(
            branch,
            (
                "var materialMismatch = ExistingMaterialMismatchResult(",
                f'prefabId, x, y, {target}, materialResult, args["material"]?.ToString());',
                "if (materialMismatch != null)",
                "return materialMismatch;",
                reuse_marker,
            ),
            f"{target} must reject its own material mismatch before reuse or completion",
        )


def verify_existing_material_reuse_regressions(plan_one: str) -> None:
    """Mutation checks for the source guard, not ONI runtime acceptance."""
    verify_existing_material_reuse(plan_one)
    early_test = (
        ' && ExistingMaterialRequestSatisfied('
        'def, earlyExistingBuild, args["material"]?.ToString())'
    )
    mutations = [("missing early guard", plan_one.replace(early_test, "", 1))]
    for target in ("existingBuild", "executionExistingBuild", "existingUtility", "executionExistingUtility"):
        branch = extract_block(plan_one, f"if ({target} != null)")
        reuse_marker = (
            "RegisterSupportBlueprint(" if target == "existingUtility" else
            "return ExistingPlacementResult(" if target == "executionExistingUtility" else
            "var instantRetry = TryCompleteExistingVirtualFileBlueprint("
        )
        guard_start = branch.index("                var materialMismatch")
        completion_start = branch.index("                " + reuse_marker)
        guard = branch[guard_start:completion_start]
        completion_end = branch.index("\n", completion_start) + 1
        late_guard = (
            branch[:guard_start]
            + branch[completion_start:completion_end]
            + guard
            + branch[completion_end:]
        )
        for label, broken in (
            ("missing guard", branch.replace(guard, "", 1)),
            ("wrong target", branch.replace(f"{target}, materialResult,", "otherBuild, materialResult,", 1)),
            ("late guard", late_guard),
        ):
            mutations.append((f"{target}: {label}", plan_one.replace(branch, broken, 1)))

    for label, mutated in mutations:
        if mutated == plan_one:
            fail(f"material guard regression did not change its fixture: {label}")
        with redirect_stderr(StringIO()):
            try:
                verify_existing_material_reuse(mutated)
            except SystemExit:
                continue
        fail(f"material guard verifier accepted broken source: {label}")
    print(f"OK: {len(mutations)} material reuse guard mutations were rejected")



def verify_existing_blueprint_native_retry(source: str, adapter: str) -> None:
    """Supplemental source contract; executable host tests prove behavior."""
    retry = extract_block(source, "private static Dictionary<string, object> TryCompleteExistingVirtualFileBlueprint")
    require_order(retry, ("IsAuthorizedVirtualFileInstantBuild(args)", "FindConstructableAtPlacement",
        "GetOrientation()", "BuildPlacementDetails", "RevalidateExistingBlueprintTarget",
        "ExistingBlueprintCompletionSafetyFailure", "NativeBlueprintInstantCompletion.Run",
        "FindCompletedBuildAtPlacement", "VerifyNativeExistingBlueprintCompletion"),
        "existing blueprint must bind and revalidate its actual native target before UI invocation and observe completion afterward")
    for forbidden in ("DeleteObject", "TryBuildVirtualFileInstantBuild", "def.Build(", "FinishConstruction"):
        if forbidden in source or forbidden in adapter:
            fail(f"existing blueprint native path must not contain direct fallback: {forbidden}")
    for required in ('string.Equals(def.PrefabID, "Tile", StringComparison.Ordinal)', "target.Elements.Count != 1",
        "before != null && completed != null", "protectedPreserved == true", "outcome.UiRestored == true",
        '["blueprintPlaced"] = false', '["changed"]', '["applied"]', '["partial"]'):
        if required not in source:
            fail(f"existing blueprint native observation guard missing: {required}")
    for required in ("tool.Activate(def, new List<Tag>(expectedElements), facadeId)", "tool.SetToolOrientation(orientation)",
        "tool.OnMouseMove", "TryBuild.Invoke(tool", "revalidate();", "RequireReady", "RequireIdle", "RequireActive",
        "FacadeEquivalent", "SelectedElementsMatch(tool, expectedElements)", "typeof(IList<Tag>)", "IsPaused", "IsModal()", "finally", "GameContextLifecycle.CaptureGeneration()",
        "ReferenceEquals(controller.ActiveTool, tool)", "ReferenceEquals(tool.visualizer, ownedPreview)",
        "ReferenceEquals(ownedPreviewDef, def)", "controller.ActivateTool(select)"):
        if required not in adapter:
            fail(f"native UI exact binding/readiness/cleanup contract missing: {required}")


def verify_building_blueprint_safety(
    root: Path, sources: dict[Path, str] | None = None
) -> None:
    build_root = root / "mods" / "OniMcp" / "Tools" / "Impl" / "Build"
    paths = {
        "native": build_root / "BuildPlanningNativeUtilityPath.cs",
        "placement": build_root / "BuildPlanningActionPlacement.cs",
        "plan_one": build_root / "BuildPlanningPlanOne.cs",
        "materials": build_root / "BuildPlanningMaterials.cs",
        "runtime": build_root / "BuildPlanningRuntimePlacement.cs",
        "existing_completion": build_root / "BuildPlanningExistingBlueprintCompletion.cs",
        "completion_ui": build_root / "NativeBlueprintInstantCompletion.cs",
    }
    for path in paths.values():
        if not path.is_file():
            fail(f"required source file not found: {path.relative_to(root)}")

    if sources is None:
        selected = {path: path.read_text(encoding="utf-8") for path in paths.values()}
    else:
        selected = sources

    native_method = extract_block(
        selected[paths["native"]],
        "private static Dictionary<string, object> TryPlaceUtilityPathNative",
    )
    free_build_marker = "if (IsFreeBuildContext())"
    require_order(
        native_method,
        (free_build_marker, "SelectElements", "SelectUtilityBuildTool"),
        "free-build fallback must precede material selection and native utility tools",
    )
    free_build_branch = extract_block(native_method, free_build_marker)
    for token in (
        'result["attempted"] = false;',
        'result["placementMode"] = "blueprint_cell_fallback";',
        'result["shouldFallback"] = true;',
    ):
        if token not in free_build_branch:
            fail(f"free-build utility fallback missing: {token}")
    if 'result["reason"]' not in free_build_branch:
        fail("free-build utility fallback must explain why native placement was skipped")

    auto_connect = extract_block(
        selected[paths["placement"]],
        "public static McpTool AutoConnectUtility()",
    )
    require_order(
        auto_connect,
        (
            "TryPlaceUtilityPathNative",
            'GetBool(nativePath, "success")',
            'GetBool(nativePath, "shouldFallback")',
            "foreach (var point in path)",
            "TryPlanOne(def.PrefabID, point.x, point.y",
        ),
        "utility auto-connect must continue from native fallback into per-cell planning",
    )
    path_loop = extract_block(auto_connect, "foreach (var point in path)")
    if "TryPlanOne(def.PrefabID, point.x, point.y" not in path_loop:
        fail("utility fallback path loop must call TryPlanOne for each cell")

    plan_one = extract_block(
        selected[paths["plan_one"]],
        "private static Dictionary<string, object> TryPlanOne",
    )
    require_order(
        plan_one,
        (
            "bool completedImmediately = IsAuthorizedVirtualFileInstantBuild(args);",
            "if (completedImmediately)",
            "TryBuildVirtualFileInstantBuild",
            "return InstantCompletionFailureResult",
            "else",
            "def.TryPlace(",
            'return ErrorResult(prefabId, x, y, "Placement failed"',
            "return new Dictionary<string, object>",
            '["blueprintPlaced"] = !completedImmediately,',
            '["buildingCompleted"] = completedImmediately,',
        ),
        "TryPlanOne must distinguish successful blueprints from successful instant completion",
    )

    materials = selected[paths["materials"]]
    select_elements = extract_block(
        materials,
        "private static MaterialSelection SelectElements",
    )
    auto_selection = extract_block(select_elements, "if (auto)")
    require_order(
        auto_selection,
        (
            "var defaults = DefaultBuildElements(def);",
            "if (IsFreeBuildContext())",
            "return ValidatedMaterialSelection(defaults",
            "available.FirstOrDefault()",
        ),
        "free-build auto material selection must prefer ordered building defaults",
    )
    defaults_index = auto_selection.find("var defaults = DefaultBuildElements(def);")
    free_build_index = auto_selection.find("if (IsFreeBuildContext())", defaults_index)
    if "defaults.Count" in auto_selection[defaults_index:free_build_index]:
        fail("free-build defaults must reach validation even when the ordered list is empty")
    if "MaterialSelection.Success(" in select_elements:
        fail("SelectElements success exits must use the unified validation helper")
    if select_elements.count("return ValidatedMaterialSelection(") != 4:
        fail("all four SelectElements success exits must use the unified validation helper")

    validated_success = extract_block(
        materials,
        "private static MaterialSelection ValidatedMaterialSelection",
    )
    require_order(
        validated_success,
        (
            "if (elements == null || elements.Count == 0)",
            "ElementLoader.GetElement(elements[0]) == null",
            "return MaterialSelection.Success(",
        ),
        "material success validation must reject missing or non-element primary materials",
    )
    if validated_success.count("return MaterialSelection.Invalid(") < 2:
        fail("material success validation must reject empty and invalid primary elements")

    verify_existing_material_reuse(plan_one)
    verify_existing_blueprint_native_retry(selected[paths["existing_completion"]], selected[paths["completion_ui"]])

    runtime_existing = extract_block(
        selected[paths["runtime"]],
        "private static Dictionary<string, object> ExistingMatchingBuildAtPlacement",
    )
    if runtime_existing.count('["material"]') < 2:
        fail("existing completed buildings and blueprints must both report their construction material identity")
    runtime_utility = extract_block(selected[paths["runtime"]], "private static Dictionary<string, object> ExistingMatchingUtilityAtPlacement")
    runtime_cell = extract_block(selected[paths["runtime"]], "private static Dictionary<string, object> ExistingUtilityAtCell")
    if '["material"]' not in runtime_utility or 'ExistingConstructionMaterialTag(go)' not in runtime_cell:
        fail("legacy utility reuse must expose its own material for explicit request validation")


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    verify_building_blueprint_safety(root)
    plan_path = root / "mods/OniMcp/Tools/Impl/Build/BuildPlanningPlanOne.cs"
    plan_one = extract_block(
        plan_path.read_text(encoding="utf-8"),
        "private static Dictionary<string, object> TryPlanOne",
    )
    verify_existing_material_reuse_regressions(plan_one)
    print("OK: free-build utility paths fall back to per-cell blueprint placement")


if __name__ == "__main__":
    main()
