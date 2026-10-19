# Completed work

See the feature documentation, changelogs, and executable regression tests. Personal runtime journals are excluded from this public snapshot.


<a id="exact-map-build-scope"></a>
## 地図差分の明示的な配置範囲

- [x] **地図の一セル・疎な建設マスクを、親viewportや暗黙の連続経路で拡大しない。** 絶対anchorsとworldを保持し、相対・検索・経路用の元指定を除去。空／不正anchorsは別指定へ切り替えず拒否する。確認・素材・回転・地形許可は保持し、明示したpoints等の経路は維持する。
- 実製品連結のホスト434／1100条件、既知の不正変異、参照ビルドと、開発ID・公開ID双方の通常設計図4ケースで範囲と非対象保持を確認した。全予行、レイヤー別操作計画、工事完了・接続・実流量の親課題は未完了として残す。[契約と受入範囲](docs/map-build-scope_JP.md)。
