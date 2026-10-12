# Native building toggle behavior

- Normal `building_control domain=config action=set_enabled` uses `BuildingEnabledButton.OnMenuToggle`, preserving native duplicant jobs and cancellation. It reports current and requested state separately and refuses InstantBuild or missing APIs rather than assigning state directly.
- `set_toggle` follows `IPlayerControlledToggle`: requests wait for simulation while paused; these switches do not require duplicant work.
- Direct state assignment exists only in `game_control domain=sandbox kind=entity action=set_building_enabled_debug`. Confirmation and sandbox/force checks apply. Pending normal toggles and unsupported dry runs are rejected.

Host regressions cover queued requests, idempotence, cancellation, missing APIs, InstantBuild refusal and switch state combinations. Development runtime checks covered coal generator, water cooler and printing pod duplicant completion, both switch types without duplicant work, and remote facility request/cancellation. Remote work completion, downstream network operation and all modded facilities were not covered. Private captures and save identities are not included in this snapshot.
