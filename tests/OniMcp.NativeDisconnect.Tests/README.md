# Native utility connection and disconnection host

Run from the repository root:

```sh
rtk proxy dotnet run --project tests/OniMcp.NativeDisconnect.Tests/OniMcp.NativeDisconnect.Tests.csproj -c Release
```

The executable links the complete production connect/disconnect callers and native adapters. It also links the actual `IsUtilityPrefab`, `IsLinearUtilityPrefab` and `EqualsIgnoreCase` methods as byte-exact generated slices. `extract_utility_classifiers.py` records the source hashes, byte ranges and slice hashes beside `UtilityClassifier.g.cs`. The existing fixtures now use the actual five basic prefab IDs, rather than substituting the shared classifier with a fixture-only list.

The existing 69 disconnection checks and 253 connection checks remain active. The LogicRibbon extension adds 47 checks for all four adjacent directions, the exact canonical ID, preserved identity/material/layers, a repeated request without another gesture, read-only preview, mixed definitions/material refusal, replacement reservation refusal, bridge/tube/near-name refusal, native no-op and partial failure. The shared linear classifier must continue rejecting LogicRibbon; only the dedicated existing-connection predicate accepts its native ID.

Native input, network managers, connection-mask updates and Unity object registration are explicit fixture boundaries. A controlled native callback supplies connection outcomes; no connection is created by default. These checks do not prove four-bit signal transmission, native UI rendering, real construction or persistence after reload. Those need the installed-game reference build and separate runtime acceptance.

`ConnectExistingSource` can point to a previous complete caller or a temporary mutant. The previous caller must compile with the same actual classifiers and fail the new ribbon acceptance assertion at runtime. Temporary overrides also exercise an overly broad ribbon-name match, case-insensitive acceptance, missing material validation and missing same-definition validation. Product sources must not be edited to run these negative cases. Compile failures do not count as behavioral detections.
