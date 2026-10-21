# Public fork snapshot

[Changes from main (Japanese)](docs/main-differences_JP.md) documents the pinned baseline, feature changes, compatibility and validation limits.

This branch is an independent, parentless source snapshot. It preserves the latest committed feature work while excluding development-machine and gameplay records. It does not include uncommitted development changes.

## Included and excluded

- Source, regression fixtures, licenses, upstream attribution and general technical documentation are retained.
- Private save identities, raw runtime captures/screenshots, personal paths, local schedules/client configuration and session scripts are excluded.
- The mod/Harmony identity is `PublicFork.OniMcp`. This is distinct from the development and upstream IDs; settings use `PublicFork.OniMcpConfig.json` and restart intents use `PublicFork.OniMcpRestartIntent.json`; legacy files are not read or automatically migrated. Do not enable these variants together.
- Steam publishing remains disabled. Account and default Workshop target configuration are absent. Public upstream item references and synthetic offline test fixtures are not publishing targets.
- Livestream tools require an explicit room ID. MCP configuration is an opt-in example in `examples/mcp-config.json`.
- Third-party project names, public reference URLs, game API constants and required copyright notices are retained as attribution and reproducibility information.
- Upstream purchase/support notices, the open letter, RTK instructions and Bilibili integration are retained out of respect for the original project. Their retention does not guarantee operation or compatibility of the referenced services, tools or integrations with this fork.

## Local setup

Copy `Directory.Build.props.example` to the ignored `Directory.Build.props` and configure your game installation. Configure MCP clients separately using the example; no local job or client approval policy is installed by this branch.

## Maintaining this boundary

Review future imports as source changes; merging development history defeats the parentless boundary. Recheck committed files and commit metadata before publishing. Push only this branch, never `--all`, `--mirror`, or private tags. Creating this branch does not remove any history already published elsewhere.

Runtime reports from development are historical evidence, not a runtime test of this newly identified public build. Validate the public build in an authorized test world before distributing binaries.

## Snapshot validation

- Committed-file scans found no development account handles, personal Steam IDs, private home/workspace paths, private LAN addresses, known secret-token formats or runtime/save artifacts. The dotted Visual Studio version in the solution file is a version, not a private IP address. This is a scoped scan, not a guarantee against every possible secret format.
- Publisher regression tests passed, including unset owner/target rejection and synthetic owner fixtures. Both publication wrapper scripts refuse execution before network or build work.
- Configuration regressions: 17 passed. Rust tests: 17 passed. Map localization tests: English, Japanese and Simplified Chinese passed. Livestream invocation without a room ID is rejected.
- Source contracts: 45 of 49 passed after rerunning network/loopback checks with the required access. Four failures reproduce in the development source: bridge placement-offset expectation, duplicate game_control localization declaration, disabled-knowledge tool guidance, and the 515-line InventoryTools source limit. They remain unresolved; the full suite is not green.
- No game runtime test or game-reference build of the new public identity was performed. Retained PNG assets are the existing three Git LFS pointers; Git LFS and their binary contents are unavailable in this checkout. They were not rendered or newly audited.


## Later runtime acceptance

The management-skill Markdown query fix was tested under this fork's own identity with its own installed DLL and settings. Twenty read cases passed, and the original development installation and save were restored afterward. Detailed scope and limits are in the [change notes](docs/main-differences_JP.md). This supplements the historical snapshot checks above; it does not establish runtime acceptance for every retained feature or authorize distribution.

The virtual action-progress fix was also accepted under the public identity. Mixed outcomes, total refusal, requested-cell/anchor counts, deferred work and explicit detailed batch results were checked against independent cell reads. The original development environment and save were restored afterward. See [the result contract and validation scope](docs/operation-result-progress_JP.md); this acceptance does not cover every retained feature or authorize distribution.


The existing-edge connection operation was accepted under the public identity using its own build and settings. Five basic utility families, endpoint/material/other-layer preservation, repeated no-op requests, actual liquid/gas/solid delivery, lamp consumption recovery and normal save/restart persistence passed 656 runtime checks. Restoration of the original development environment and colony passed 59 checks and a normal-screen inspection. Derived and mixed utilities, exhaustive directions and migration of legacy auto-connect paths remain outside this acceptance. See [the operation contract and runtime scope](docs/native-existing-connection_JP.md); private runtime records and settings are excluded.


The exact map-placement scope fix was accepted under the public identity using its own build. Single-cell, sparse, multi-row and rotated blueprint cases preserved the requested native footprint and unrelated objects, residents, terrain and dig orders. The original installation, settings and paused save were restored and independently checked. See [the scope contract and acceptance limits](docs/map-build-scope_JP.md); construction completion, physical connections and real flow are separate gates. Private runtime records are excluded.


The virtual-operation array parser fix was accepted using the public identity and its own build. Typed arrays, scalar and JSON-payload compatibility, independent storage/critter filter readback, malformed inputs and restoration passed 26 runtime cases. Post-dialog readback and restoration of the original paused environment were checked independently, while preserving an explicit user change to another mod. Native tag eligibility rules are unchanged; retaining an unknown tag does not establish that it is a valid UI choice. See [the argument contract and validation limits](docs/ops-array-arguments_JP.md). Private runtime records and settings are excluded.


The native MOD mismatch acknowledgement path was accepted using the public identity and its own build. Read-only status, displayed text, invalid-request rejection, native preview, detailed virtual operations, generation tokens and pause preservation were checked on naturally generated notifications. The original installation, settings and saves were restored with the game stopped. See [the operation contract and validation limits](docs/mod-mismatch-dialog_JP.md). Arbitrary dialogs and every third-party mod combination are outside this acceptance; private runtime records are excluded.


The survival watcher main was repaired and accepted under the public identity using the public checkout’s Python source. Expected-save binding, one paused normal load, safe refusal, zero-target behavior, diagnostic reads, short bounded progression and final pause were checked separately. The original installation, settings, saves and initially stopped game state were restored and independently verified. This does not identify historical resume actors or provide a native timeout watchdog. See [the contract and acceptance limits](docs/watcher-main-safety_JP.md); private runtime records are excluded.


Bounded native speed-call tracing was accepted under the public identity. Read-only history, cursor validation across processes, synchronous call nesting, retained modal pause layers and two-session attribution were checked against native state and clocks. The original installation, settings, saves and initially stopped game state were restored and independently verified. Records describe observed call boundaries; they do not identify an unknown actor or guarantee execution of every original instruction. See [the observation contract and validation limits](docs/native-speed-trace_JP.md). Private runtime records are excluded.


The existing-blueprint self-conflict fix was accepted under the public identity using its own build. Exact self-exclusion, guarded native Tile requests, material/prefab refusal, unsupported definitions and ordinary blueprint reuse preserved the inspected objects and paused state. The native Tile action returned without completing the blueprint; this is not a runtime proof of successful instant completion. The original installation, settings, protected saves and initially stopped game state were restored and independently checked. See [the contract and remaining limits](docs/existing-blueprint-native-completion_JP.md). Private runtime records and development history are excluded.


The UI Action binding guard was accepted using the public identity and its own build. Unbound actions were refused before input, a bound overlay action was dispatched and restored, and the inspected paused map state was preserved. The source-linked host passed 94 conditions. Listed installation/settings files, protected saves, save selection and recorded shared-cache snapshots were restored with the game stopped. The pre-run retired-summary history was not fully backed up, so exact restoration of the entire environment is not claimed. See [the binding contract and validation limits](docs/ui-action-binding_JP.md).


Limited normal replacement of a completed, neutral single-cell Tile with an airflow tile was accepted using the public identity and its own build. Synchronously prepared native UI caches, read-only preview, exact reservation reuse, material/priority conflicts, ordinary worker construction, material return, colocated wire preservation, observed supporting-equipment function and normal save/reload passed. The source-linked native host passed 253 conditions; relevant static and material-guard checks also passed. The isolated wire does not prove powered-edge current, polling does not prove uninterrupted subframe support, and unknown original UI priority remains unknown. Recorded installation and shared-save snapshots were restored with the game stopped, but the retired-summary state before the first branch save was not fully backed up. Other tile families, rotations, multi-cell definitions and every cold Prebuild/UI case remain outside this acceptance. See [the replacement contract and limits](docs/native-tile-replacement_JP.md). Private runtime evidence is excluded.
