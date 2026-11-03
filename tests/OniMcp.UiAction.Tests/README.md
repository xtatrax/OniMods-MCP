# UI action binding regression host

This executable links the complete production `UiTools.cs` and `HotkeyStringPatches.cs`, plus the production localization loader and JSON settings. It embeds the three `ui_action` runtime catalogs. It does not extract or reimplement the product binding, action-list, dispatcher or Harmony-prefix logic.

```sh
rtk proxy dotnet run --project tests/OniMcp.UiAction.Tests/OniMcp.UiAction.Tests.csproj
```

The fixtures cover missing binding rows, rows without any input, keyboard/controller bindings, invalid action IDs, unavailable/throwing mapping reads, unavailable/unknown native UI readiness, explicit nullable availability, a fresh binding check after an earlier list, management fallback and integrated dispatch routes, direct native management routes, native event counts, hotkey prefixes and localized refusal messages. Listing must not mutate the fixture binding array or send input. A successful dispatcher result proves only one native key-down/key-up pair; the host does not claim that a screen opened.

`NativeBoundaryStubs.cs` substitutes ONI input/UI and unrelated MCP metadata types. The fixture `FindEntry` throws on a missing action, and the event sink invokes it to detect a forbidden missing-action dispatch. This is a deliberate failure boundary, not a reconstruction of the exact native call stack or Unity timing. Harmony prefixes are called directly; Harmony installation, actual bindings, native UI lifecycle, input consumers and gameplay effects require the installed-game reference build and runtime acceptance.

## Negative source overrides

`UiActionSource` and `HotkeySafetySource` accept alternate full source files. The focused `--probe=bindings` and `--probe=dispatch` entry points use APIs shared with the earlier implementation, so an earlier source pair can compile and fail behaviorally rather than merely failing to compile after an API addition. For example, pass these MSBuild properties with paths to privately retained prior sources:

```text
-p:UiActionSource=<previous-UiTools.cs>
-p:HotkeySafetySource=<previous-HotkeyStringPatches.cs>
-- --probe=bindings
```

The accepted implementation passed 94 assertions. The preceding source pair was rejected for treating an absent in-range action as bound; the preceding dispatcher was rejected when it reached the missing-action assertion. Temporary source overrides also detected four semantic regressions: accepting absent rows, normalizing unknown mappings to unbound, ignoring controller-only bindings and bypassing the dispatcher guard. Compilation failures do not count as behavioral detections. A C# null versus `JValue.CreateNull()` availability override was serialization-equivalent for this dictionary and is not counted as a detected mutation. Product sources are never rewritten for negative testing.


## Schedule acknowledgement extension

The additional suite links the complete production `UiTools.cs`, `UiControlTools.cs`, hotkey safety, localization loader and serializer. `UiControlTools` is the real middle aggregate router; unrelated dialog/selection/feedback branches are poison stubs. The top `GameControlTools` router and public registry task middleware are not linked or proven by this host. All schedule requests carry a valid task; task enforcement remains an independent public-route contract.

The original `Program.Run` cases remain byte-identical. Main adds a separate suite and `--probe=schedule-ack`, with its own condition count. The new native-shaped fixture inputs supply owner references, focus, active/enabled/hierarchy facts and a thin optional event callback. They do not reproduce MCP classification or automatically open a screen. Explicit per-case callbacks independently choose no effect, open state, owner replacement or failure. Native dispatch is still the actual whole dispatcher and hotkey binding guard.

Cases distinguish passive known false from unknown, callback-free status/preview, strict Boolean/confirm validation, already-open no-op, unfocused refusal, one native down/up pair, no-effect pending without opened, post-owner/read failure partial, native exception with unknown dispatch outcome and no retry/fallback, and truthful `dryRun=true` receipts when a valid preview is refused for unavailable state or missing focus. Read getters may count their own reads; no universal zero-write claim is made. Native open-state is not pixel visibility, Unity timing, notification effects or actual installed ABI acceptance.

The compiled old whole `UiTools` control uses the existing `UiActionSource` override and a focused, bound, confirmed, valid-task, closed/no-effect fixture. It must compile, dispatch one pair and fail `compiled_schedule_false_opened`; compiler failure is not behavioral detection. Runtime resources are the actual three `ui_action`, three `ui_dialog` and three `tools` catalogs; PO mapping is checked separately. Record exact source/test pins and keep all output/obj outside the repo with NuGetAudit enabled. The extension also repeats passive reads through the middle-router aliases, preserves request objects, and checks that shared binding refusals and post-owner replacement do not become success acknowledgements.

`--probe=refused-preview` isolates the rejected-preview receipt cases. A full source override with the earlier early-return behavior must compile and then fail the `refused preview retains requested dryRun flag` assertion; a compilation failure is not that regression proof.
