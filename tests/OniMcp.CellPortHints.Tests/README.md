# Cell port decision adapter host checks

The project compiles the production `WorldEditorCellPortDecisionHints.cs` directly.
Only Unity/game components, transformed native port cells, connection reads, and
the localization formatting boundary are substituted. It does not reimplement
the production classification policy. These checks do not establish native game
port registration, UI rendering, or catalog-loading behavior; use the respective
native and localization tests for those boundaries.

```sh
rtk proxy dotnet run --project tests/OniMcp.CellPortHints.Tests/OniMcp.CellPortHints.Tests.csproj
rtk proxy python3 tests/OniMcp.CellPortHints.Tests/run-mutations.py
```

Coverage includes required/optional input and output ports; connected precedence;
power, pipe, and rail compatibility; bridge requirement matching by transformed
cell across both native arrays, reused IDs, and duplicate definitions; unknown
bridge metadata; invalid/absent targets; and the existing six-entry output cap.
The mutation runner checks that treating every logic port as required and taking
the first co-located bridge definition both fail behavioral assertions.
