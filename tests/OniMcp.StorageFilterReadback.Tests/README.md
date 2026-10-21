# Storage readback host regression

This console suite links the complete production `StorageTools.cs`, `StorageTargetResolver.cs`, `StorageItemDetails.cs`, and `McpJsonUtil.cs`. It invokes the actual detail/filter handlers and both existing storage routing layers, then checks the serialized JSON produced with the production serializer settings.

```sh
rtk proxy dotnet run --project tests/OniMcp.StorageFilterReadback.Tests/OniMcp.StorageFilterReadback.Tests.csproj
```

The filter cases retain native post-update filter/mass readback and selector guards. The item cases cover native item identity, mass, raw Kelvin and germ values, rounded Celsius, cached disease definition lookup, observed zero, native none index, unavailable/exception/nonfinite properties, independent getter failures, explicit JSON nulls with per-field reasons, duplicate item references, empty detail, and compact list/filter-write responses. Canaries reject lazy `Db.Get()` initialization and changes to primary properties, item inventory, filters, or the building registry during detail reads. Raw germ observations do not assert purification, disinfection, drinkability, or safety.

`StorageSourceRoot` can select a directory containing all three linked storage source files. The accepted regression was also run against the previous complete `StorageTools.cs` projection with the current helper linked but unused: it compiled and then failed the item-identity behavior assertion. Temporary source-copy mutations were detected for missing mass converted to zero, raw Kelvin converted to Celsius, none-index count erasure, lazy database initialization, Unity instance ID fallback, loss of an independently read disease ID, nonfinite values converted to zero, and expansion of the compact list route. Product source files were not changed by these negative runs.

The fixture replaces Unity/component access, `PrimaryElement`, the existing database cache and resource definitions, and unrelated routing/tool metadata. It does not run Unity lifecycle, the installed game's actual getters, locale resolution, multi-Storage component behavior, pipe flow, germ simulation, or a save/load cycle. These host results establish adapter/serialization behavior; matching the same stored item through a live alternative reader and establishing game behavior require separate runtime acceptance.
