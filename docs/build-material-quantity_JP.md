# 建材量の取得と配送未評価

建材候補の `availableKg` は、本体の素材選択UIが使う `WorldInventory.GetAmount(tag,true)` に揃える。以前の「本体総量＋同worldの床置き質量」は、床置きを含む本体キャッシュへ同じ物体を再加算するため廃止した。独自の在庫再集計や本体キャッシュの更新は行わない。

## 数量の意味

| 項目 | 取得元と境界 |
|---|---|
| `availableKg` / `selectedAvailableKg` | 同じ親を持つ関連worldについて、本体の建設需要を差し引いた素材選択UIの量。配送可能量ではない |
| `nativeInventory.ownWorldTotalKg` | 対象worldの `GetTotalAmount(tag,false)`。本体が登録・集計した量で、可視実物一覧や全物体質量と同じ範囲とは限らない |
| `nativeInventory.ownWorldMaterialNeedsKg` | 対象worldの既存 `materialNeeds` キャッシュ。個別PickupableのFetch予約とは区別する |
| `nativeInventory.ownWorldAvailableKg` | 対象worldの `GetAmount(tag,false)` |
| `transportableKg` | 未評価のためnull。`transportStatus=not_evaluated` とし、在庫から到達／運搬可能性を補わない |
| `requiredKg` | 既存の単一素材スロットのnative `BuildingDef.Mass`。複数スロットを一素材へ合算しない |

在庫の取得元、対象world、関連world数、既存建設需要と差引後量を区別する。候補一覧は短い状態・理由だけを追加し、詳細なnativeInventoryは選択素材1件へ限定する。検索結果の候補全件へ詳細を繰り返し展開しない。

`inventoryState=known` は既存キャッシュが準備済みで、値を読み取れたことを表す。可視セル・今この瞬間の全物体・配送完了を保証しない。今回確認した本体版の `GetTotalAmount` はbool引数を使わず、自worldのキャッシュを返す。関連world量は `GetAmount(tag,true)` の通常経路で取得する。

## 欠測と対応範囲

本体の `HasValidCount=false`、関連worldの未準備、取得不能、負値／非有限値はunknownとnullで保持する。ロード直後の未集計を既知0へ補わず、読取のためにゲームを進めたりnative更新処理を呼んだりしない。明示した短時間の通常進行後に再取得することと、自動的な読取再評価は別である。

空の関連worldが未集計のまま残る場合も、合計を既知扱いにしない。自worldの観測値があっても関連範囲全体がunknownになり得る。値を得るための強制Refreshや不足世界を0とみなす回避は行わない。

現在の建材選択が対応するElementLoader有効元素だけを数量候補にする。非元素アイテムのnative個数をkgとして扱わず、一覧・auto候補から除外する。従来から非元素の実選択を拒否していた境界を、数量表示にも揃えた。

複数素材スロット、実物ごとの収納／床置き・Fetch予約、目的地までの配送可能量、連続施工全体の必要量と共有在庫の引当は未対応である。通常建設の最終判断は本体UI経路に委譲する。

## 検証

実selector・reader・DTOを使うhostの新規338条件／32ケースと、関連する既存4群を確認した。ゲーム境界はfixtureであり、旧selector全文は正常コンパイル後に床置き二重加算の条件で失敗、単位guard前の実2ファイルも非元素の個数をkg表示する条件で失敗した。公開ソース自身の本体参照build.84は0警告・0エラー。

公開固有ID・設定・自身のbuild.84を使った実機で、停止試験セーブの冷ロードのunknown/nullと、明示した短時間の通常進行後の本体UI基準量・実物一覧を独立に照合した。利用不能素材の既知0、自動選択、検索候補、配送量未評価も確認した。個々のFetch予約・実配送、全素材／全world／第三者MODを網羅した受入ではない。個別のセーブ・座標・実ID・生応答は含めない。

独自文言16件はPO4の `OniMcp.MCP.BUILD_MATERIALS.*` と `Catalogs/build_materials.{en,ja,zh-Hans}.json` を対応させ、`scripts/verify_build_material_localization.py` で照合する。暫定Hantと実行時Hansは別の訳として保持する。ゲーム由来名とMCP文章の言語を独立に保つ。

元の設定・MOD選択、保護した原本と共有履歴の集合／hash、元セーブの停止時刻・速度・視点・カメラ設定を照合して復帰した。再起動で一時的な注目対象は解除され、通常ロードによる採番seedとセッション値の更新は保持した。
