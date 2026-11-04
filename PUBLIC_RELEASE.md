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


The UI Action binding guard was accepted using the public identity and its own build. Unbound actions were refused before input, a bound overlay action was dispatched twice, and the inspected paused map state was preserved. The source-linked host passed 94 conditions. Listed installation/settings files, protected saves, save selection and recorded shared-cache snapshots were restored with the game stopped. The pre-run retired-summary history was not fully backed up, so exact restoration of the entire environment is not claimed. See [the binding contract and validation limits](docs/ui-action-binding_JP.md).


Limited normal replacement of a completed, neutral single-cell Tile with an airflow tile was accepted using the public identity and its own build. Synchronously prepared native UI caches, read-only preview, exact reservation reuse, material/priority conflicts, ordinary worker construction, material return, colocated wire preservation, observed supporting-equipment function and normal save/reload passed. The source-linked native host passed 253 conditions; relevant static and material-guard checks also passed. The isolated wire does not prove powered-edge current, polling does not prove uninterrupted subframe support, and unknown original UI priority remains unknown. Recorded installation and shared-save snapshots were restored with the game stopped, but the retired-summary state before the first branch save was not fully backed up. Other tile families, rotations, multi-cell definitions and every cold Prebuild/UI case remain outside this acceptance. See [the replacement contract and limits](docs/native-tile-replacement_JP.md). Private runtime evidence is excluded.

These overlay checks observe dispatch, not independent native screen consumption. The earlier development candidate supplied development save/reload evidence; the final public candidate supplied independent public save/reload evidence. Ordinary construction completion and final material mass were checked, while individual material-fetch targets were not tracked continuously.


Existing same-definition, same-material LogicRibbon cells can now use the native single-edge connection route. The public identity passed its source-linked host checks, game-reference build, independent high-bit signal loss/recovery, endpoint/material/outer-edge/colocated-wire preservation, repeated no-op and mixed-target refusal, short progression and normal save/reload. The original installation, selected paused save and recorded shared-cache file sets were restored with the game stopped; the shared baseline preceded this test’s first native save. Fixture setup used authorized sandbox tools. Derived ordinary construction, every direction and all sixteen signal values remain outside this limited acceptance. See [the ribbon contract and limits](docs/native-existing-logic-ribbon_JP.md). Private runtime records and development history are excluded.


Toilet diagnostic reads now use existing native state without invoking the lazy state-machine initializer. Missing or inconsistent cache values remain explicit null with reasons, and the native localized action name retains legacy query compatibility. This public build passed its own host, game-reference and paused runtime checks and restored the original environment with the game stopped. Cleaning execution and the full early-clean lifecycle remain outside this acceptance; see [the read contract and limits](docs/toilet-passive-read_JP.md). Private runtime records and development history are excluded.


The generated storage operation example now uses the supported storage-list route and explicitly retains a maximum of three returned building rows in the operation result. The public source passed its own existing operation regressions, game-reference build and paused guide-to-preview-to-read comparison, with the original environment restored and game stopped. This does not add storage-scope behavior or bound total enumeration work; see [the guidance contract and limits](docs/storage-list-guidance_JP.md). Private runtime records and development history are excluded.


The generated coordinate example now supplies the required target tool and supported cell-read action. The public source passed its own existing operation regressions and game-reference build. Paused native acceptance used observed visible-cell coordinate substitutions with an explicit world payload; scalar cross-checks, repeated detail and wrong-world refusal passed, and the original environment was restored with the game stopped. Synthetic example coordinates were preflight only. See [the contract and limits](docs/coordinate-cell-guidance_JP.md); private records, native source extracts and development history are excluded.


The operation-file mode, edit and syntax hints now use runtime catalogs synchronized with PO sources while preserving legacy English text and executable syntax. This public source passed its own strict seven-key PO/catalog checks, existing host regressions with a semantic old-reader negative, game-reference build and paused three-language document/repeat checks. Full settings and the original environment were restored with the game stopped. Browser/PLib rendering, game-language changes and native child-command execution are outside this acceptance; see [the contract and limits](docs/operation-help-localization_JP.md). Private runtime records and development history are excluded.


Generated capture examples now appear only in the orders and ranching operation guides. The DSL strings, parser, capabilities, safeguards and native action routes are unchanged. This public source passed its own host regressions with a semantic old-reader negative, game-reference build and paused reads of all ordinary guides with representative repeats. The original environment was restored with the game stopped. Capture execution and ranching completion are outside this display-only acceptance; see [the scope and limits](docs/operation-capture-examples_JP.md). Private runtime evidence and development history are excluded.


## Unsupported filter preview requests

Filter writes now reject unsupported dry-run requests before target/native access, including the independent storage filter route. Non-boolean flags are refused; omitted/false execution and read routes retain their existing behavior. This public identity passed its own source-linked host regressions, localization checks, game-reference build, three native preview-refusal paths and normal change/restore readbacks with paused time and observed neighboring storage preserved. Native flat-filter positives, all filter families and transport completion remain outside acceptance. See [the contract and limits](docs/filter-write-preview_JP.md). Private runtime evidence and development history are excluded.


### 時間割画面の実表示確認

時間割画面の通常入力受付と実表示を区別し、未取得null・既知false、受動status／予行、明示確認、表示済みno-op、pending／partialを返す。公開版自身のhost502・翻訳483・本体参照build.81の0警告0エラーと、停止実機の通常表示・後続状態／画像・再要求保持・閉鎖・非対象Action保持を確認。失敗枝の全実機生成は含まず、公開ID／設定分離を保持する。詳細は[時間割UIの観測契約](docs/schedule-ui-observation_JP.md)。

## Canonical public tool metadata descriptions

Eight existing parameter descriptions now have canonical three-language catalog keys, with PO mappings to their existing domain descriptions. The game query/limit English overrides retain their action-specific filtering and bounds guidance. Protocol fields and action routes are preserved. This public source passed its own Core host: 16 groups, including 427 new metadata conditions; Python regressions: 25; and the formal source checker: 46 catalog groups, 298 public metadata items, 138 embedded catalogs, 35 selected PO contexts and 6 runtime aliases. The old whole English-override source compiles, then fails the query/limit meaning check.

The host links the actual navigation factory; its eight shared parameter schemas are plain metadata fixtures and do not execute their owner factories. The public build.82 passed game-reference compilation with zero warnings and errors. Its own paused runtime returned all seven tools with required string task fields and 1,216 catalog-matching descriptions across English, Japanese, Simplified Chinese and auto. Non-prose schemas matched the same build's auto baseline across these selections; preservation against the earlier source is a separate static-review result. Representative game-native Japanese names were retained. Full settings were restored semantically after returning to auto, with protected data, paused clock, speed and observed view preserved. These checks do not establish complete localization or browser/PLib rendering. See [the metadata contract and acceptance limits](docs/canonical-tool-metadata-localization_JP.md). Private runtime records and local configuration are excluded.


## Native daily report names

Daily report names now retain the already-resolved native display text instead of looking it up again, while preserving identifiers, numeric values, units, contexts and notes. Only the two existing MCP-owned read errors use the independent MCP locale through PO4/runtime3. The public source passed its own 17 Core groups, 73 new report assertions, PO/source checks and a zero-warning, zero-error game-reference build. Its own deployed build passed paused three-language plus auto report/name/error comparisons, preserved non-name report data, and restored all settings while retaining the recorded paused state and protected data. See [the report contract and limits](docs/daily-report-localization_JP.md). Private runtime records and configuration are excluded.


## Native construction material quantities

Material quantities now use the same cached API as the native material selector, without adding loose resources a second time. Own-world totals, cached construction needs, related-world availability and unevaluated delivery quantities are reported separately. Unready or unavailable inventory remains unknown/null; non-element item counts are not labeled as kilograms.

This public source passed its own four relevant host groups, 338 new quantity conditions, two semantic old-source negatives, PO checks and a zero-warning game-reference build. Its own public identity and settings passed cold-load missing-value checks and paused comparisons after a short normal advance, including physical-resource totals, known zero, auto/search results and unevaluated transport. Multi-slot requirements, per-object fetch reservations, actual delivery and full continuous-build allocation remain outside acceptance. See [the quantity contract and limits](docs/build-material-quantity_JP.md); private runtime records and development history are excluded.

The original settings, mod selections, protected saves, shared-history file sets and paused save/view were restored. Camera configuration was preserved; restart cleared transient attention, while normal load/session seed updates were retained.
