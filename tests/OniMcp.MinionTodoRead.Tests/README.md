# Minion TODO snapshot and precondition-cache read regression

This host project compiles the full production `FacilityDupeSideScreenTools.cs` and `NativeChorePreconditionObservation.cs`, then invokes the actual `MinionTodoInfo` method through a test-only wrapper. It does not extract or reproduce the product methods. The actual runtime localizer and embedded catalogs are also linked.

The original 16 option combinations and shared-snapshot assertions remain: blocked results on/off, dirty display-order flag on/off, potential filtering, limits, repeated reads and empty/missing targets. They verify that `snapshot.failedContexts` retains its reference, entries and order and that `snapshot.doFailedContextsNeedSorting` is not written. Native total counts, current-chore summaries and schedule projection remain distinct from returned rows.

The additional fixtures model the installed native cache field owners/types and deliberately dangerous getter boundaries. A dirty getter sorts its list and clears its flag; the singleton getter creates an instance if absent. These are boundary fault models, not copies of ONI eligibility rules. The native potential decision remains an explicit fixture boolean. Tests cover:

- Dirty caches on blocked and succeeded rows, unknown retention with potential-only filtering, limits and repeated reads, with no getter/potential/singleton calls or cache writes.
- Missing singleton, missing cache, nonstandard chore type, overridden precondition getter and a native decision exception. A separate direct-adapter test rejects an overridden driver getter without invoking it; whole-reader name/report/driver callbacks are outside that assertion.
- Ready native true/false decisions delegated exactly once, passive failure-reason projection, no-failure sentinel and explicit invalid failure indices.
- Unknown versus known-false filtering and the actual localized unknown note in English, Japanese and the existing Chinese runtime language.

Run from the repository root:

```sh
rtk proxy dotnet run --project tests/OniMcp.MinionTodoRead.Tests/OniMcp.MinionTodoRead.Tests.csproj -c Release
```

`TodoReaderSource` can point to a temporary copy of the previous complete production file for a negative run. Its compilation must succeed, then the first dirty-cache assertion must fail because the getter consumes that cache. `TodoObservationSource` can select a temporary helper for focused negative runs. Keep all old or mutated sources outside this test project.

The reflection guard and reader behavior are tested with substituted native types. This does not execute ONI assemblies, independently observe native dirty flags in the live game, prove arbitrary-thread/other-MOD race freedom, or establish purity of every schedule/name/driver getter. Runtime acceptance and installed API signature evidence must remain separate.
