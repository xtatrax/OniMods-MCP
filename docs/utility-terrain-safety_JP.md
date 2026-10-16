# 配線・配管敷設に伴う自然地形の採掘

自動電力接続は既定ONを維持する。自然固体を通る経路は既定で敷設前に拒否し、意図して掘り進める場合だけ `allowUtilityTerrainDig=true` を指定する。`confirm=true` や従来の `autoDigObstructions=true` だけでは許可しない。

## 指定と応答

| 指定 | 動作 |
| --- | --- |
| 省略／`allowUtilityTerrainDig=false` | 自然地形セルを通るutility敷設を拒否。既設の床タイル内や空洞の敷設は維持。 |
| `allowUtilityTerrainDig=true` | 採掘を伴う敷設を明示許可。自動配線の追加経路にも適用する。 |
| `autoDigObstructions=false` | 明示許可があっても自然地形を採掘しない。 |
| `maxAutoDigCells` | 採掘予算。経路の自然固体数が予算を超える場合は経路全体を拒否する。 |
| `autoConnectPower=false` | 設備本体だけを建設し、自動配線しない。 |

通常の `world_editor` 地図編集に指定を添える。例えば意図した配管経路のSEARCH/REPLACEと併せ、トップレベルで `allowUtilityTerrainDig=true`、`payload` に `autoDigObstructions=true` と `maxAutoDigCells` を渡せる。採掘許可は当該要求だけに適用され、永続設定ではない。sandboxの地形生成・削除を許可する引数とは別であり、通常の複製人間による採掘・建設を予約する。

拒否理由は `utility_natural_terrain_blocked`。該当座標・埋蔵マーカーの `present/absent/unavailable`・既存採掘予約を返す。自動配線の結果は `powerAutoConnect.status=blocked_natural_terrain` に入り、設備本体の予約成否とは分かれる。本体が成功しても接続成功と解釈しない。

許可時は `terrainImpact` に自然固体の予定セルを返し、native dragを避けてセルごとの採掘・建設経路へ通す。`digNewlyQueued` と `digAlreadyQueued` を分離し、自動配線の `autoDigQueued` は新規分だけを返す。新規採掘が失敗・上限で省略されたセルには、そのまま建築配置を続行しない。utilityの採掘に伴って別レイヤーの植物を自動掘り起こししない。

## 予行と限界

- 建設計画の予行は地形ポリシーと予定セルを確認する。仮想地図SEARCH/REPLACEの外側dryRunは従来どおり構文・形状確認に留まり、地形・配置の検証済みとはしない。
- `supportAnalysis=not_evaluated`。設備支持・落下する砂・上方の植生・採掘後の液体流入の安全性を自動保証しない。意図した採掘経路と支持・帰路を別途確認する。
- 自然固体を通る既存の採掘予約があっても、既定モードでは地形が空くまで拒否する。明示許可で同じ予約を重複追加したことにはしない。
- 全グループのロールバックや、すべての建築操作の非変更予行を追加する変更ではない。設備本体が自然地形に重なる場合の従来の建築autoDigは別の契約。
- 拒否は代替経路探索ではない。迂回路を指定するか、明示許可を使うか、先に別の採掘を行う。

## 検証

共通修正の開発側検証では、本番の地形判定をリンクしたホスト試験56件と、4施工経路で変更前にガードするソース契約を確認。既存Tools回帰97件（形状・食料等の関連回帰を含む）とUtilityConnections56件も成功。本体参照Releaseビルドは警告0・エラー0。

実機の修正後受入は未実施。既定拒否／明示許可／autoDig禁止／上限超過、予行の非変更、実採掘から完成、既設タイル・植物・埋蔵マーカー・支持床を試験分岐で比較する。稼働DLLへは未配備。

公開側への選択反映後も、地形判定56件と4経路のソース契約、本体DLL参照のResolveReferences/Compile成功を確認した。公開側では梱包・DLL配備・実機受入を行っていない。
