# Minion TODO snapshot read regression

This host project compiles the full production `FacilityDupeSideScreenTools.cs` and invokes its actual `MinionTodoInfo` method through a test-only wrapper. It does not extract or reproduce that method.

The fixture verifies that `snapshot.failedContexts` retains the same list, entries and order, and that `snapshot.doFailedContextsNeedSorting` retains its value without a write. It covers blocked results on/off, dirty state on/off, potential filtering, limits, repeated reads and empty/missing targets. A nonnegative failed-precondition index also retains its reason ID/description on the same returned chore row. Returned blocked ordering follows the existing comparator when dirty, while current/succeeded results and total counts retain their existing meaning.

Native context comparison, `IsPotentialSuccess`, chore/precondition getters, schedules and unrelated bionic collaborators are substituted boundaries. This proves preservation of the two shared snapshot members above, not purity of the entire TODO reader. In particular, real `IsPotentialSuccess` and `GetPreconditions` may affect other native caches. No native cache refresh, whole-game non-change or runtime behavior is established by this host test.

Run from the repository root:

```sh
dotnet run --project tests/OniMcp.MinionTodoRead.Tests/OniMcp.MinionTodoRead.Tests.csproj -c Release
```

`TodoReaderSource` can point to a temporary copy of the previous production file for a negative run. The old shared-list sort and dirty-flag clear must fail the shared-snapshot preservation assertion. Keep mutated sources outside this project; do not modify the production file to run a negative case.
