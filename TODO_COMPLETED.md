# Completed work

See the feature documentation, changelogs, and executable regression tests. Personal runtime journals are excluded from this public snapshot.


<a id="exact-map-build-scope"></a>
## 地図差分の明示的な配置範囲

- [x] **地図の一セル・疎な建設マスクを、親viewportや暗黙の連続経路で拡大しない。** 絶対anchorsとworldを保持し、相対・検索・経路用の元指定を除去。空／不正anchorsは別指定へ切り替えず拒否する。確認・素材・回転・地形許可は保持し、明示したpoints等の経路は維持する。
- 実製品連結のホスト434／1100条件、既知の不正変異、参照ビルドと、開発ID・公開ID双方の通常設計図4ケースで範囲と非対象保持を確認した。全予行、レイヤー別操作計画、工事完了・接続・実流量の親課題は未完了として残す。[契約と受入範囲](docs/map-build-scope_JP.md)。


<a id="ops-array-arguments"></a>
## 仮想opsの配列引数の型保持

- [x] **JSON配列を括弧付き文字列一要素にせず、子ツールへ型を保持して渡す。** 単一・複数・空・空白・入れ子・escapeを共通parserで扱い、不正JSONはdispatch前に拒否する。scalar・object payload・一コマンド・確認/予行を保持し、nativeの候補規則と書込先を拡張しない。
- 開発ID・公開IDの各26ケースと独立したacceptedTags再読取・元タグ/容量の復元が成功。公開ソースhost追加532・既存1100条件、翻訳2キーのPO4/JSON3と参照ビルドも成功した。未知タグの保存を通常UI候補とせず、通常UIの手動フィルター表示・実配送・全management書込は未検証。[契約と受入範囲](docs/ops-array-arguments_JP.md)。
