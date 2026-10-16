#!/usr/bin/env python3
from pathlib import Path
r=Path(__file__).resolve().parents[1]/"mods/OniMcp/Tools/Impl/Build"
checks={
 "BuildPlanningPlanOne.cs": "var earlyExistingBuild =",
 "BuildPlanningActionPlacement.cs": 'if (!dryRun && ToolUtil.GetBool(args, "nativePathPlacement", true))',
 "BuildPlanningNativeUtilityPath.cs": 'InvokeBest(tool, "Activate"',
 "BuildPlanningPowerConnect.cs": 'var connectResult = AutoConnectUtility().Handler(wireArgs);',
}
for file,mutation in checks.items():
 s=(r/file).read_text()
 assert s.index("UtilityTerrainConflict(") < s.index(mutation), file
s=(r/"BuildPlanningPowerConnect.cs").read_text()
assert 'ToolUtil.GetBool(args, "autoConnectPower", true)' in s
assert '["nativePathPlacement"] = false' in s
assert '["allowUtilityTerrainDig"] = ToolUtil.GetBool(args, "allowUtilityTerrainDig", false)' in s
assert '["autoDigQueued"] = GetInt(connectPayload, "digNewlyQueued")' in s
s_native=(r/"BuildPlanningNativeUtilityPath.cs").read_text()
assert s_native.index('if (terrainConflict != null)') < s_native.index('InvokeBest(tool, "Activate"')
assert '["status"] = "blocked_natural_terrain"' in s
print("Utility terrain guards precede all four placement routes; default auto wiring remains ON")
