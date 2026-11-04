# OniMcp core regression checks

Run with the .NET 10 SDK; an ONI installation is unnecessary:

```sh
dotnet run --project tests/OniMcp.Core.Tests/OniMcp.Core.Tests.csproj -c Release
```

The executable links the production MCP types, registries, resource routes and
middleware. It checks JSON-RPC null fields, initialization retries, invalid tool
arguments, visibility and metadata cache isolation, every registered static
resource, and resource-template query overrides. Any failed group exits with code 1.

The navigation factory and English metadata overrides are actual linked product
source. Native camera execution throws; existing resource-routing groups briefly
substitute recording handlers and restore them afterward. Other game factories
are recording fixtures, and the Unity speech overlay is replaced by a counter. These checks verify dispatch and protocol behavior; game
assembly compatibility, actual game reads and Unity rendering still require a
full mod build and an in-game smoke test.

The additional metadata group preserves the existing navigation actions and field
set, constructs metadata in Japanese, and requests English, Japanese, Simplified
Chinese and auto-language snapshots through the actual registry/localizer. Its
eight shared canonical parameter schemas are plain metadata fixtures, not the
owner factories; validate actual owner metadata separately with the localization
source verifier and each build's runtime tools/list. English query/limit overrides
execute the actual override source on a plain game-control metadata fixture.

Keep NuGetAudit enabled and isolate build outputs. NavigationControlSource and
CoreEnglishDescriptionsSource permit exact whole-source negative controls. The
old English override must compile, then fail the action-specific query/limit
meaning check. These hosts do not execute game assemblies or prove game UI behavior.
