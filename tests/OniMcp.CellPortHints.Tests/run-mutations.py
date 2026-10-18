#!/usr/bin/env python3
"""Verify the adapter suite detects the two original requirement regressions."""
from pathlib import Path
import re
import subprocess
import tempfile

project_dir = Path(__file__).resolve().parent
project = project_dir / "OniMcp.CellPortHints.Tests.csproj"
source_path = project_dir.parents[1] / "mods/OniMcp/Tools/WorldEditor/WorldEditorCellPortDecisionHints.cs"
source = source_path.read_text()

old_call = "LogicPortReadSemantics.ConnectedAtCell(cell), port.requiresConnection);"
assert source.count(old_call) == 1, "actual adapter call must be found exactly once"
always_required = source.replace(old_call, "LogicPortReadSemantics.ConnectedAtCell(cell), true);")

bridge_pattern = r"        private static bool\? BridgePortRequirement\(LogicPorts ports, int cell\)\n        \{.*?\n        \}\n(?=\n        private static void AddPortHint)"
first_match_body = """        private static bool? BridgePortRequirement(LogicPorts ports, int cell)
        {
            foreach (var infos in new[] { ports.inputPortInfo, ports.outputPortInfo })
            {
                if (infos == null) continue;
                foreach (var port in infos)
                    if (LogicPortReadSemantics.ActualCell(ports, port) == cell)
                        return port.requiresConnection;
            }
            return null;
        }
"""
first_match, substitutions = re.subn(bridge_pattern, lambda _: first_match_body, source, flags=re.S)
assert substitutions == 1, "actual bridge requirement reader must be found exactly once"

mutations = [
    ("logic-always-required", always_required, "cell.decision.ports.optional contains logicIn@(10,1)"),
    ("bridge-first-match-downgrades-required", first_match,
     "a required co-located output definition overrides earlier optional input definition"),
]

with tempfile.TemporaryDirectory(prefix="oni-port-hints-mutations-") as directory:
    for name, mutated, expected in mutations:
        mutation_path = Path(directory) / (name + ".cs")
        mutation_path.write_text(mutated)
        result = subprocess.run(
            ["dotnet", "run", "--project", str(project), "-p:PortHintsSource=" + str(mutation_path)],
            text=True, capture_output=True,
        )
        output = result.stdout + result.stderr
        assert result.returncode != 0 and expected in output, name + " did not fail for the expected regression:\n" + output
        print(name + ": regression detected at the intended behavioral assertion")

# Restore the normal source compile and confirm the mutants left no persistent change.
subprocess.run(["dotnet", "run", "--project", str(project)], check=True)
