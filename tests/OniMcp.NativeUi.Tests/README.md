# Native UI adapter host regressions

The existing toggle cases remain active. The Blueprint cases compile the complete production `NativeBlueprintInstantCompletion.cs`, `BuildPlanningExistingBlueprintCompletion.cs` and existing-material reader. `extract_blueprint_safety.py` also copies 26 required production methods verbatim from obstruction, safety, geometry, runtime-placement and instant-completion files into the build directory. Each generated `.ranges.json` records the original UTF-8 byte range, line range, source hash and slice hash. No extracted method is translated or reproduced in a test implementation.

The Blueprint fixture covers exact self-exclusion while retaining other same-prefab objects and occupied endpoints; virtual/confirm/preview/world guards; the Tile-only scope; existing material, rotation and facade identity; busy/unavailable native UI; immutable selected-material verification; single native invocation; repeated scene/tool/preview ownership checks; and nullable completion/change observations. It distinguishes a native no-op, verified completion, unregistered or wrong-geometry completed candidates, missing before-observation and collateral occupant loss. Missing facade components differ from a known native default/original facade.

Only the native/game boundaries are substituted: BuildTool callbacks and their state, native object enumeration, geometry acquisition, the fixture placement model/position builder, terrain/plant queries and unrelated native UI collaborators. Grid uses an indexer struct with separate layer dictionaries and a `NumLayers` sentinel, so a 2-D-array-only API such as `GetLength` cannot accidentally compile. The normal C# source is still validated separately against the installed game assemblies.

A controlled native callback constructs the exact registered object for the positive completion case. This is not evidence that the real BuildTool will complete an occupied Tile blueprint: native invocation may return without completing it. Host acceptance does not establish real construction, transport, UI rendering or whole-game non-change. Active-tool cleanup is scoped to the captured scene and owned preview; selection and overlay restoration remain unknown.

Run from the repository root (requires the .NET SDK and Python 3):

```sh
rtk proxy dotnet run --project tests/OniMcp.NativeUi.Tests/OniMcp.NativeUi.Tests.csproj -c Release
```

Source override properties permit negative tests with temporary copies outside the checkout. The previous obstruction implementation must fail exact self-exclusion. The previous existing-completion main/failure methods can be compiled as exact slices with a substituted direct-build engine boundary; `--completion-no-direct-write` inspects preservation/deletion/build counters independently of the legacy result schema. Meaning-changing mutations additionally check broad prefab exclusion, skipped native material validation, treating a void action as success, skipped protected-occupant checks, lost same-preview cleanup ownership and hiding an unregistered completed candidate. Do not modify production files to run these negative cases. Static safety scripts remain supplemental contracts, not behavioral or game-runtime proof.
