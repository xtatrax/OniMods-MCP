# 排尿関連の現在作業表示

`Pee` は通常の緊急トイレ使用にも実失禁にも使われる作業型です。作業型や翻訳された名前だけでは事故を判断しません。

`dupes_control domain=info action=status_check`（`/active/ops/dupes.md` 経由）の `currentChore` と、設備側の複製人間の現在作業診断で、次の根拠により表示を分けます。

| 根拠 | category / phase | 日本語の name |
| --- | --- | --- |
| `Pee` / `BreakPee`、対象または提供元に `ToiletWorkableUse`、本人の Navigator が移動中 | `toilet` / `moving` | トイレへ移動 |
| 同じトイレ使用対象を、本人の WorkerBase が使用中 | `toilet` / `using` | トイレを使用 |
| トイレ使用対象は確認できるが、移動／使用段階を取得できない | `toilet` / `moving_or_using` | トイレへ移動／使用 |
| 現在の作業が `PeeChore` で、InProgress と `running` 状態を確認 | `incontinence` / `active` | 失禁 |
| 排尿関連だが対象・提供元・実失禁状態の根拠が不足 | `unknown` / `unknown` | 排尿関連・未判別 |

実失禁の作業クラスでも、実行状態を確認できなければ未判別です。床の汚染水、膀胱値、過去の事故通知、設備に他の使用者がいることは、この判定の入力にしていません。トイレ清掃など他の作業表示は変更しません。

## 応答の追加情報

- `name`: 上記の根拠を使った表示。日本語・英語・簡体字中国語に対応。
- `rawName`: ゲームが返した元の翻訳済み作業名。
- `reportName`: 既存のゲーム側報告名を維持。これだけでは事故を判断しない。
- `type`: 生の ChoreType ID を維持。`runtimeType`: 実際の作業クラス名。
- `target` / `provider`: 取得できれば ID、Prefab ID、名前、cell、x/y、worldId。対象なし・不明な座標は null。
- `urination`: 排尿関連の作業のみ。category、phase、displayName、reasonCode、取得した evidence を返す。
- `unavailableEvidence`: 根拠の取得で例外が起きた項目。不明な根拠を false や事故に置き換えない。

実際の提供元は `Chore.provider` を参照します。現在作業の所有 GameObject を提供元と推測して代用しません。使用者は対象トイレの busy フラグではなく、その複製人間自身の WorkerBase.GetWorkable() で確認します。
