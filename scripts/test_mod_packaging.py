#!/usr/bin/env python3
"""Run actual PackageMod targets in disposable, game-independent MSBuild fixtures."""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import tarfile
import tempfile
import xml.etree.ElementTree as ET
import zipfile

MODS = ("OniMcp", "CycleTrim", "OniModTemplate")
CASES = ("default", "cli_trailing", "cli_no_trailing", "cli_independent_paths",
         "imported_paths", "relative_paths", "relative_paths_other_cwd", "sibling_prefix", "dist_parent",
         "skip_default", "skip_cli", "skip_imported", "skip_unsafe",
         "unsafe_dist_equal", "unsafe_dist_child", "unsafe_mod_equal", "unsafe_mod_child",
         "unsafe_mod_parent", "unsafe_zip_equal", "unsafe_zip_child",
         "unsafe_relative_dist", "unsafe_relative_mod", "unsafe_relative_zip")
ERROR_ID = "ONIMODS_PACKAGE_DESTINATION_INSIDE_SOURCE"


def require(value: bool, message: str) -> None:
    if not value:
        raise AssertionError(message)


def snapshot(path: Path) -> dict[str, str]:
    return {str(f.relative_to(path)): hashlib.sha256(f.read_bytes()).hexdigest()
            for f in path.rglob("*") if f.is_file()} if path.exists() else {}


def seed_package(root: Path, mod: str) -> None:
    (root / mod).mkdir(parents=True, exist_ok=True)
    (root / mod / "sentinel.keep").write_text("existing package directory", encoding="utf-8")
    (root / (mod + ".zip")).write_text("existing package archive", encoding="utf-8")
    (root / (mod + "-src.tar.gz")).write_text("existing source archive", encoding="utf-8")


def run_case(root: Path, workspace: Path, mod: str, case: str) -> dict:
    work = workspace / mod / case
    project = work / "mods" / mod
    project.mkdir(parents=True)
    output = work / "build output"
    output.mkdir()
    inputs = (mod + ".dll", "mod.yaml", "mod_info.yaml", "README.md", "README_EN.md",
              "preview.png", "docs/steam-description-en.md", "docs/steam-description-zh.md",
              "assets/edit_mark_tool.png", "assets/restart_oni_steam_relay.sh")
    for name in inputs:
        f = output / name
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_bytes(("fixture:" + name).encode())
    for name in ("source.keep", "README.md", "bin/excluded.bin", "obj/excluded.obj", "dist/excluded.zip",
                 ".build-number", ".build-number.lock", ".build-number.123.tmp", ".build-number.notes"):
        f = project / name
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_bytes(("source:" + name).encode())

    original = ET.parse(root / "mods" / mod / (mod + ".csproj")).getroot()
    target = next(t for t in original if t.tag == "Target" and t.get("Name") == "PackageMod")
    # Execute the real target, not a hand-reimplemented packaging algorithm.
    target_import = ET.Element("Project")
    target_import.append(ET.fromstring(ET.tostring(target, encoding="unicode")))
    (project / "PackageMod.production.targets").write_text(ET.tostring(target_import, encoding="unicode"), encoding="utf-8")
    wrapper = ET.Element("Project")
    ET.SubElement(wrapper, "Import", {"Project": "Fixture.props", "Condition": "Exists('Fixture.props')"})
    properties = ET.SubElement(wrapper, "PropertyGroup")
    ET.SubElement(properties, "AssemblyName").text = mod
    ET.SubElement(properties, "OutputPath").text = str(output) + "/"
    ET.SubElement(wrapper, "Import", {"Project": "PackageMod.production.targets"})
    fixture = project / "Packaging.proj"
    fixture.write_text(ET.tostring(wrapper, encoding="unicode"), encoding="utf-8")

    default = work / "dist"
    custom = work / "custom dist"
    seed_package(default, mod)
    seed_package(custom, mod)
    chosen = default
    mod_path = chosen / mod
    zip_path = chosen / (mod + ".zip")
    cli: dict[str, str] = {}
    imported: dict[str, str] = {}
    if case in ("cli_trailing", "cli_no_trailing", "cli_independent_paths", "skip_cli"):
        chosen = custom
        cli["DistPath"] = str(chosen) + ("/" if case == "cli_trailing" else "")
    if case in ("imported_paths", "skip_imported"):
        chosen = custom
        imported["DistPath"] = str(chosen)
    if case in ("relative_paths", "relative_paths_other_cwd"):
        chosen = work / "relative package"
        cli["DistPath"] = "../../relative package"
    if case == "sibling_prefix":
        chosen = project.parent / (mod + "-package")
        cli["DistPath"] = str(chosen)
    if case == "dist_parent":
        chosen = work
        cli["DistPath"] = str(work)
    mod_path = chosen / mod
    zip_path = chosen / (mod + ".zip")
    if case == "cli_independent_paths":
        mod_path = work / "independent content" / mod
        zip_path = work / "independent archives" / "nested" / (mod + ".zip")
        mod_path.mkdir(parents=True)
        (mod_path / "sentinel.keep").write_text("existing explicit content", encoding="utf-8")
        cli.update(DistModPath=str(mod_path), ZipFile=str(zip_path))
    if case == "imported_paths":
        mod_path = chosen / ("Imported " + mod)
        zip_path = work / "imported archives" / "nested" / (mod + ".zip")
        imported.update(DistModPath=str(mod_path), ZipFile=str(zip_path))
    if case.startswith("skip_"):
        (imported if case == "skip_imported" else cli)["SkipModPackaging"] = "true"
    if case == "skip_unsafe":
        cli["DistModPath"] = str(project.parent)

    unsafe = case.startswith("unsafe_")
    unsafe_values = {
        "unsafe_dist_equal": ("DistPath", str(project)),
        "unsafe_dist_child": ("DistPath", str(project / "package")),
        "unsafe_mod_equal": ("DistModPath", str(project)),
        "unsafe_mod_child": ("DistModPath", str(project / "package")),
        "unsafe_mod_parent": ("DistModPath", str(project.parent)),
        "unsafe_zip_equal": ("ZipFile", str(project)),
        "unsafe_zip_child": ("ZipFile", str(project / "package.zip")),
        "unsafe_relative_dist": ("DistPath", "."),
        "unsafe_relative_mod": ("DistModPath", ".."),
        "unsafe_relative_zip": ("ZipFile", "./package.zip"),
    }
    if unsafe:
        key, value = unsafe_values[case]
        cli[key] = value
    if imported:
        props = ET.Element("Project")
        group = ET.SubElement(props, "PropertyGroup")
        for key, value in imported.items():
            ET.SubElement(group, key).text = value
        (project / "Fixture.props").write_text(ET.tostring(props, encoding="unicode"), encoding="utf-8")

    before_all, before_source, before_output = snapshot(work), snapshot(project), snapshot(output)
    before_default = snapshot(default)
    args = ["dotnet", "msbuild", str(fixture), "-nologo", "-t:PackageMod", "-v:minimal"]
    args.extend("-p:" + key + "=" + value for key, value in cli.items())
    run = subprocess.run(args, cwd=work if case == "relative_paths_other_cwd" else project, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=90)
    log = workspace / (mod + "-" + case + ".log")
    log.write_text(run.stdout, encoding="utf-8")
    if unsafe:
        require(run.returncode != 0 and ERROR_ID in run.stdout, mod + "/" + case + ": explicit source-overlap refusal required")
        require(snapshot(work) == before_all, mod + "/" + case + ": refusal must precede every package write/delete")
        return {"mod": mod, "case": case, "status": "refused", "exitCode": run.returncode}
    require(run.returncode == 0, mod + "/" + case + ": MSBuild failed; inspect " + str(log))
    if case.startswith("skip_"):
        require(snapshot(work) == before_all, mod + "/" + case + ": skip must preserve all existing files")
        return {"mod": mod, "case": case, "status": "skipped", "exitCode": 0}

    require(snapshot(project) == before_source, mod + "/" + case + ": source files changed during packaging")
    require(snapshot(output) == before_output, mod + "/" + case + ": input files changed during packaging")
    if case != "default":
        require(snapshot(default) == before_default, mod + "/" + case + ": unrelated default dist must be preserved")
    require(not (mod_path / "sentinel.keep").exists(), mod + "/" + case + ": chosen old package content was not replaced")
    require(zip_path.is_file(), mod + "/" + case + ": configured ZIP path not honored")
    with zipfile.ZipFile(zip_path) as archive:
        require(archive.testzip() is None, mod + "/" + case + ": ZIP CRC failed")
        for name in (mod + ".dll", "mod.yaml", "mod_info.yaml"):
            require(archive.read(name) == (output / name).read_bytes(), mod + "/" + case + ": ZIP input bytes differ")
        if mod == "OniMcp":
            for name in ("assets/edit_mark_tool.png", "assets/restart_oni_steam_relay.sh"):
                require(archive.read(name) == (output / name).read_bytes(), mod + "/" + case + ": assets missing or misplaced")
    source_archive = chosen / (mod + "-src.tar.gz")
    require(source_archive.is_file(), mod + "/" + case + ": configured source archive root not honored")
    with tarfile.open(source_archive) as archive:
        members = [n[2:] if n.startswith("./") else n for n in archive.getnames()]
        require("source.keep" in members and ".build-number.notes" in members, mod + "/" + case + ": source or unrelated dotfile omitted")
        require(not any(PurePosixPath(n).parts and PurePosixPath(n).parts[0] in ("bin", "obj", "dist") for n in members), mod + "/" + case + ": build/output payload leaked into source archive")
        require(not any(PurePosixPath(n).name in (".build-number", ".build-number.lock", ".build-number.123.tmp") for n in members), mod + "/" + case + ": generated build counter state leaked into source archive")
    return {"mod": mod, "case": case, "status": "packaged", "exitCode": 0}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--work-dir", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--jobs", type=int, default=3)
    parser.add_argument("--mod", choices=MODS, action="append")
    parser.add_argument("--case", choices=CASES, action="append")
    args = parser.parse_args()
    require(shutil.which("dotnet") is not None and shutil.which("tar") is not None, "dotnet and tar executables are required")
    require(1 <= args.jobs <= 8, "jobs must be between 1 and 8")
    temporary = None
    if args.work_dir is None:
        temporary = tempfile.TemporaryDirectory(prefix="onimods-packaging-")
        workspace = Path(temporary.name)
    else:
        workspace = args.work_dir.resolve()
        require(not workspace.exists() or not any(workspace.iterdir()), "work-dir must be absent or empty")
        workspace.mkdir(parents=True, exist_ok=True)
    rows = []
    selected_mods = tuple(dict.fromkeys(args.mod or MODS))
    selected_cases = tuple(dict.fromkeys(args.case or CASES))
    try:
        with ThreadPoolExecutor(max_workers=args.jobs) as pool:
            pending = [pool.submit(run_case, args.root.resolve(), workspace, mod, case) for mod in selected_mods for case in selected_cases]
            for future in as_completed(pending):
                rows.append(future.result())
        rows.sort(key=lambda item: (item["mod"], item["case"]))
        report = {"cases": len(rows), "packaged": sum(r["status"] == "packaged" for r in rows),
                  "skipped": sum(r["status"] == "skipped" for r in rows),
                  "refused": sum(r["status"] == "refused" for r in rows), "results": rows}
        if args.report is not None:
            args.report.parent.mkdir(parents=True, exist_ok=True)
            args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
        print("PackageMod regression: " + str(report["cases"]) + " cases passed across " + str(len(selected_mods)) + " production targets ("
              + str(report["packaged"]) + " packaged, " + str(report["skipped"]) + " skipped, " + str(report["refused"]) + " refused).")
    finally:
        if temporary is not None:
            temporary.cleanup()


if __name__ == "__main__":
    main()
