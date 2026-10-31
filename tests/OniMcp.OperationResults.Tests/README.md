# Operation result response regression

This host suite compiles the production operation-file execution code, its command tokenizer/parser, semantic parser, confirmation/dry-run execution policy and JSON response types. Only the game/registry dispatch boundary, sandbox entry scope and unrelated file/screen access are substituted. Tests use regular `call key=value` commands and actual virtual-map edit/outer-batch adapters with supplied native results. Native placement and game rules are outside this suite.

An explicit child `detail=true` must retain the complete raw result string, including long JSON objects, arrays, Unicode and non-JSON text. Ordinary replies remain compact. Successful canonical `search_control` calls preserve the complete child result by default, without argument echo. Native `detail=brief|compact|full` values keep their search meaning and pass through unchanged; legacy boolean `detail=false` opts out of result output. Search failures retain compact previews unless boolean `detail=true` explicitly requests the full error. Existing error summaries, confirmation/dry-run gates, task requirements and the one-command restriction remain in effect. Tests also check that the native operation is called once and that response handling does not modify parent arguments or native result content.

Run from the repository root:

```sh
rtk proxy dotnet run --project tests/OniMcp.OperationResults.Tests/OniMcp.OperationResults.Tests.csproj --configuration Release
```

When the local test environment cannot reach NuGet vulnerability metadata, restore cached dependencies with `-p:NuGetAudit=false --ignore-failed-sources`, then run with `--no-restore`. This is an execution-environment workaround, not a project setting.

`OperationFilesSource` can override the linked production file for mutation checks. Reintroducing the former 12,000-character detail truncation must fail the first complete-result check.

These are host adapter tests. They do not establish successful compilation against the game's assemblies or runtime acceptance under either mod identity.

The search regression uses the same production operation adapter with substituted registry/native results. It preserves heterogeneous child counts and truncation fields without inferring totals or changing the `applied` aggregate. It also covers generic canonical routes, rejection of currently unregistered internal aliases, typed-file tool overrides, long results, unchanged child arguments and the existing execution guards. Direct native search equivalence and live non-mutation remain runtime acceptance checks.

The management-skill regression also compiles the complete production management-table reader and management-file dispatcher. Its native skill-list boundary supplies fixed responses and records arguments; it does not implement query filtering, target resolution, skill eligibility or learning. Unrelated game/screen entry points throw if called. Tests exercise scalar queries, the existing explicit/nested/path format precedence (including empty/null behavior), target/global Markdown fields, detached argument forwarding, escaping and exact raw JSON output. `ManagementTablesSource` can replace that linked production file for a known-bad or semantic mutation check. Live native list/Markdown equivalence, model-specific eligibility and actual game state preservation remain separate acceptance work.

The count/progress regressions additionally link the complete production map edit,
SEARCH/REPLACE preflight and parser, footprint-to-anchor compiler, outer edit,
world editor dispatcher and batch safety sources. Native building/order results
are queued raw fixtures; the wrapper results and aggregate counts are never
implemented by the fixture. Tests distinguish requested map cells, native anchor
outcomes, actual operation calls and successfully executed edit blocks, including
partial content in a successful block. Native `changed=false`, integer zero and
unknown evidence remain distinct. Explicit Boolean `detail=true` on a batch step
with progress metadata preserves the entire child result; default/false/string
values retain the existing bounded text.

The map fixture supplies a fixed visible snapshot and native definition shapes.
It does not validate game visibility, rendering, native placement, geometry APIs,
registry initialization or save restoration. Unrelated detail/screen branches
throw; narrow file/token helpers support only the exact plain prefab IDs and
active map path used by these tests. The existing Tools.Tests geometry suite
continues to link NativeBuildingFootprint itself and now verifies the compiled
original-request mapping for masks, markers, sparse holes and rotations. Native
reference compilation and development/public runtime acceptance remain separate.


The colony-read regression links the complete production selector helper and the
existing operation adapter. Only successful canonical colony calls for seed
catalog, planting-list and research-list reads gain default first-result
retention. The fixture records the exact native argument envelope but does not
implement native filtering, research, planting or game rules. Tests distinguish
read selectors and their existing aliases/precedence from non-target reads and
writes, preserve Boolean compact/full options and bounded failure previews, and
retain long/scalar/non-JSON content without merging secondary policy content.
Most cases use the actual DSL/parser/execution path. A few JValue-null selector
precedence cases call the production predicate by reflection because the DSL
retains scalar null as a string; these are predicate-boundary tests rather than
evidence that native routing ran. Both parent/child arguments and native content
remain unchanged. The existing search and execution-guard regression still runs.

`ColonyReadResultsSource` overrides the linked helper for temporary negative
fixtures. The former operation source without colony retention must compile and
then fail the default-result assertion. Known-bad variants must also compile
before their behavioral failures count. Direct native-read equivalence, actual
query/limit behavior and live preservation under each mod identity require
separate deployed-game acceptance.


The directory-listing regression links the complete production listing and browser
files alongside the existing production command dispatcher and normalizer. The
only behavior change under test is recognition of `/active/objects/` and
`/active/buildings/instances/`, which were already advertised as directories. It
checks parent-to-child listings, absolute/relative navigation, missing trailing
slashes, empty/nonempty building inputs, browser parent/child links, every current
map/ops listing entry, all advertised active directories and rejection of missing,
similar-prefix and file paths with cwd preservation. The existing browser-only
map link remains outside the MCP directory whitelist. Native object enumeration,
native naming and file reads are input boundaries; no native rules or directory
classification are implemented in the fixture. The fixture verifies that directory
browser/list/navigation paths do not dispatch the substituted file reader.

`WorldEditorListingSource` accepts a full production source path for semantic
negative runs. The known-bad full source must compile and then fail the advertised
directory navigation assertion. Host results do not establish live enumeration,
child detail content, unknown-file read rejection, Unity/Mono behavior or game-state
preservation. Each deployed mod identity requires separate paused runtime
acceptance and restoration.

The same directory regression verifies both existing object descriptions through
the real runtime localization lookup against each unchanged embedded
food_freshness catalog (en, ja, zh-Hans), plus existing auto-language normalization
and English fallback. Language providers are restored after the check.
