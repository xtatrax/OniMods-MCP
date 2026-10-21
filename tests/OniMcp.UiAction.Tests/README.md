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
