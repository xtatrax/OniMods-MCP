# 自然タイルの埋蔵物マーカーを読む

ユーザー指摘の「ヒビ模様」は画面の EntombedItemVisualizer が管理する埋蔵物表示。セル詳細と通常ビューの Visual Spatial Summary に、マーカーの有無を追加する。

- 出力元: `Game.Instance.GetComponent<EntombedItemVisualizer>().IsEntombedItem(cell)`。
- ゲームアセンブリのILを確認し、当該メソッドは辞書のContainsKeyとrefCount>0の読み取りのみ。AddItem/RemoveItem、Spawn、Revealは呼ばない。
- `present` は表示マーカーあり、`absent` は表示マーカーなし。中身の種類・個数は未確認のまま。
- 非表示セルは `unobserved`、非固体は `not_solid`、表示コンポーネントを取得できなければ `unavailable`。不明を「なし」にしない。
- 地図補足は観測済み固体だけを数え、座標は最大32件。総数と省略数を返す。地図の編集トークンは変更しない。

初期候補のGrid.Spawnable非ゼロ判定は、通常の固体まで48/48セルを拾い画像と一致しなかったため破棄した。これは埋蔵物表示の存在判定ではない。実際の描画管理側を採用する。

ホスト回帰: 未探索の非公開、非固体の区別、コンポーネント/ゲーム不在、表示サンプル上限を確認。ゲーム参照ビルド11は警告0・エラー0。実機画像との対照は後続証跡に記録する。
