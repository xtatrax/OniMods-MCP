# Power operational-count regression host

This small host compiles the actual `GetPowerSummary` factory/handler and its
complete aggregation/helpers by **verbatim member extraction**, plus the actual
MCP result types and JSON serializer. `extract_sources.py` records full source
hashes, exact byte intervals, member hashes and the generated source hash beside
the compiler input. It does not carry a second implementation of the loops.

Run normally with `dotnet run --project tests/OniMcp.PowerSummary.Tests
-p:NuGetAudit=true`. Python 3 is required only for deterministic source extraction.
`PowerSummarySource` may point to a complete immutable prior caller for a semantic
negative; the same extractor and unchanged assertions then run against that code.

Coverage is limited to per-circuit operational counts: mixed true/false devices,
false-only/missing-component circuits, summary/detail/circuit agreement, world
selection, detail limits, repeated reads and unchanged legacy numeric fields.
`Operational.IsOperational` read counters detect added getter calls. Independent
`IsPowered` inputs and throwing `IsActive`/production predicates guard the selected
source of the count.

Native device registries, component lookups, circuit IDs, ratings/energy/power
values, world and coordinate helpers, name cleaning and ToolUtil argument parsing
are explicit input boundaries. Their native algorithms and live registration are
not tested. The unrelated diagnostic TargetInfo is a fixture; port geometry is a
throwing canary and the one unconnected consumer has no Building definition, so
the actual diagnostic method takes its existing null-ports branch. No controller,
room, port, native power simulation or UI rule is recreated here.

These tests do not establish native getter purity, actual generation/consumption,
fuel exhaustion, circuit freshness, transport, UI semantics or the meaning of
legacy `netActiveWatts`/`generatorEmpty`; those remain separate acceptance scopes.
The source extractor omits unrelated room and geometry members rather than adding
large stubs or modifying existing host projects.
