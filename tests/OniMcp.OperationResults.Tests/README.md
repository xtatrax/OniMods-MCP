# Operation result response regression

This host suite compiles the production operation-file execution code, its command tokenizer/parser, semantic parser, confirmation/dry-run execution policy and JSON response types. Only the game/registry dispatch boundary, sandbox entry scope and unrelated file/screen access are substituted. Tests use regular `call key=value` commands; semantic map execution and game rules are outside this suite.

An explicit child `detail=true` must retain the complete raw result string, including long JSON objects, arrays, Unicode and non-JSON text. Ordinary replies remain compact. Successful canonical `search_control` calls preserve the complete child result by default, without argument echo. Native `detail=brief|compact|full` values keep their search meaning and pass through unchanged; legacy boolean `detail=false` opts out of result output. Search failures retain compact previews unless boolean `detail=true` explicitly requests the full error. Existing error summaries, confirmation/dry-run gates, task requirements and the one-command restriction remain in effect. Tests also check that the native operation is called once and that response handling does not modify parent arguments or native result content.

Run from the repository root:

```sh
rtk proxy dotnet run --project tests/OniMcp.OperationResults.Tests/OniMcp.OperationResults.Tests.csproj --configuration Release
```

When the local test environment cannot reach NuGet vulnerability metadata, restore cached dependencies with `-p:NuGetAudit=false --ignore-failed-sources`, then run with `--no-restore`. This is an execution-environment workaround, not a project setting.

`OperationFilesSource` can override the linked production file for mutation checks. Reintroducing the former 12,000-character detail truncation must fail the first complete-result check.

These are host adapter tests. They do not establish successful compilation against the game's assemblies or runtime acceptance under either mod identity.

The search regression uses the same production operation adapter with substituted registry/native results. It preserves heterogeneous child counts and truncation fields without inferring totals or changing the `applied` aggregate. It also covers generic canonical routes, rejection of currently unregistered internal aliases, typed-file tool overrides, long results, unchanged child arguments and the existing execution guards. Direct native search equivalence and live non-mutation remain runtime acceptance checks.
