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
