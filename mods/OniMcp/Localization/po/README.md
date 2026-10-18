# MCP port hint localization sources

These PO sources cover the four cell port decision hints in this public fix.
The runtime continues to read the matching entries in `../Catalogs/cell-details.*.json`;
these preparation files do not add a PO loader or a new runtime language.
Existing `translations/*.po` and game-owned strings remain unchanged.

- `onimcp.pot` is the template with stable namespaced `msgctxt` and English `msgid`.
- `en-us.po` and `ja-jp.po` contain the source text and current English/Japanese translations.
- `zh-hant.po` is a provisional compatibility filename whose intended original-code
  locale/script remains unconfirmed. Its Traditional Chinese sample/header does not
  identify the original code as Traditional Chinese or add `zh-Hant` runtime support.
  Keep the existing `zh-Hans` runtime wording and aliases unchanged until that mapping
  is established. Each `#. Runtime zh-Hans msgstr:` comment records its separate source.

| PO context suffix | Runtime JSON key |
|---|---|
| `MISSING` | `cell.decision.ports.missing` |
| `CONNECTED` | `cell.decision.ports.connected` |
| `OPTIONAL` | `cell.decision.ports.optional` |
| `UNKNOWN` | `cell.decision.ports.unknown` |

All context names begin with `OniMcp.MCP.CELL.DECISION.PORTS.`. Keep the template,
all three PO files and current runtime catalogs aligned when changing these entries.
Preserve placeholders and protocol values. Validate syntax, uniqueness, source text,
placeholders and the separate Chinese mapping from the repository root:

```sh
rtk proxy python3 scripts/verify_cell_port_hint_localization.py
```

PO source preparation and runtime loading are separate validation boundaries.
