# Building read availability host contract

This standalone host links the complete production availability reader, text models,
text parser, sequence parser, catalog, aliases, execution planner, serializer and
localization loader. Thin partial accessors call production private methods without
copying candidate construction, ranking, filtering or serialization logic.

Run from the repository root:

```sh
dotnet run --project tests/OniMcp.BuildAvailability.Tests/OniMcp.BuildAvailability.Tests.csproj
```

Normal package restore and vulnerability audit remain enabled. The project does not
load ONI or prove runtime UI behavior. `NativeBoundaryStubs.cs` supplies explicit
cache/definition/research inputs; native eligibility is a configurable predicate
whose invocation, exact prefab ID, failures and result are observed. Separate Techs
and TechItems boundaries preserve the native UI distinction: an individually POI
unlocked item may be available while its parent research is incomplete. Unstarted
research without an instance remains false and is never created by a read. These
fixtures do not claim to verify the game's own predicate implementation.

Material inventory, placement geometry and unrelated utility-write collaborators
are outside this contract. Empty material inputs are ordinary fixtures. Native UI,
write, database initialization and Research.GetOrAdd boundaries throw canaries.
Native TechItem parent lookups may call Db.Get against an already-existing cache;
the host tracks those calls separately from forbidden initialization attempts.
Cosmetic nonempty facades remain an existing eager catalog path: ordinary fixtures use null facades,
and a separate boundary test acknowledges that limitation. Cache-only assertions
cover the new availability reader, candidates and read filtering, not every catalog
operation. A later material/cosmetic callback must not overwrite the availability
captured when catalog reading begins.

The host covers nullable true/false/unknown observations, cached-controller absence,
real unstarted research, native-predicate authority, bypass provenance, independent
unlocked/definition values, exact/alias/sequence candidates, missing definitions,
ranking, filtering, limits and explicit serialized nulls. Runtime deployment and
public entry routing are separate acceptance work. `SearchBuildables` in the full
`BuildPlanningActionPlacement.cs` and `PlanOne` are not linked by this host:
filtering evidence here is specifically the production candidate filter. Search
query/category routing and write-guard equivalence require separate source/runtime
evidence. Unmatched candidate queries are checked to perform no native availability
read for either `includeUnavailable` setting.

PassiveReaderCases adds native-field-only category discovery, missing-parent readiness, and existing facade/ownership cache cases. The actual reader/catalog/facade source and production serializer remain linked. Missing cache and known false stay distinct; arbitrary runtime property getters and lazy database initialization are forbidden fixture boundaries. Existing original availability assertions remain active.
