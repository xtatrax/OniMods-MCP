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

## 製造ポッド候補の受動観測

| PO context | 現行JSONキー | 状態 |
|---|---|---|
| `OniMcp.MCP.PRINTING_POD.REWARDS.DUPE_CLAIM_SUPPORT` | `printingPod.rewards.dupe_claim_support` | 本体候補キャッシュの観測説明。PO正本と en/ja、注記の zh-Hans JSON を照合。zh-Hant は暫定訳 |
| `OniMcp.MCP.PRINTING_POD.REWARDS.NOTES.NO_READY_OFFERS` | `printingPod.rewards.notes.no_ready_offers` | 本体候補キャッシュの観測説明。PO正本と en/ja、注記の zh-Hans JSON を照合。zh-Hant は暫定訳 |
| `OniMcp.MCP.PRINTING_POD.REWARDS.NOTES.UNKNOWN` | `printingPod.rewards.notes.unknown` | 本体候補キャッシュの観測説明。PO正本と en/ja、注記の zh-Hans JSON を照合。zh-Hant は暫定訳 |
| `OniMcp.MCP.PRINTING_POD.REWARDS.NOTES.NOT_MATERIALIZED` | `printingPod.rewards.notes.not_materialized` | 本体候補キャッシュの観測説明。PO正本と en/ja、注記の zh-Hans JSON を照合。zh-Hant は暫定訳 |
| `OniMcp.MCP.PRINTING_POD.REWARDS.NOTES.GENERATION_PENDING` | `printingPod.rewards.notes.generation_pending` | 本体候補キャッシュの観測説明。PO正本と en/ja、注記の zh-Hans JSON を照合。zh-Hant は暫定訳 |
| `OniMcp.MCP.PRINTING_POD.REWARDS.NOTES.MATERIALIZED` | `printingPod.rewards.notes.materialized` | 本体候補キャッシュの観測説明。PO正本と en/ja、注記の zh-Hans JSON を照合。zh-Hant は暫定訳 |

ランタイムは `Catalogs/printing-pod.en.json`、`.ja.json`、`.zh-Hans.json` を参照する。状態ID、JSONキー、操作IDは翻訳しない。この説明は明示的な候補読取の結果であり、既定OFFの供給判断ポリシーを有効にするものではない。

互換性: `printingRewards.available` は Immigration 未取得時に `null`、`rewardCount` は候補未生成・生成途中・対象不一致・欠測時に `null` を返す。`rewards=[]` と未知の件数を、生成済み補給品0件と同一視しない。`candidateObservation` の小さな `status/reason/source/totalCount/carePackageCount` で根拠を示し、候補配列は二重化しない。`no_ready_offers` は以前の画面キャッシュを無視して現在0件、`materialized` は対象一致の非空nativeキャッシュと全CarePackage.Infoを確認できた状態である。画面を閉じただけでは既存キャッシュを不明扱いにしない。

status/read は候補の生成・画面表示・refresh・requirement delegate を呼ばない。要件を再評価していない候補は `requirementMet=null`、`claimable=null`、`claimabilityStatus=not_evaluated` とする。従来の真偽値を前提とするクライアントはnullを未知として扱う。実際の claim/reject 操作は既存のnative画面経路と確認条件を維持し、候補読取だけで受取可否を断定しない。

候補一覧の `index` と claim の `rewardIndex` は、対象Telepadに一致する同じ `ImmigrantScreen` の既存containers順・同じCarePackageInfoを使用する。古いstatic global候補が残っていても選択元に混ぜない。unknown／未生成／生成途中は候補を生成せず既存の受取拒否を返し、実行時のconfirm・dryRun・native選択／OnProceed・cleanup条件は維持する。
