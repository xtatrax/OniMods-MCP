# 配線・配管・コンベアの敷設と切断

## 敷設・接続

building_control domain=planning action=auto_connect は、pointsに指定した連続経路の各辺を両側へ保存する。通常工事の予約と完成設備の混在に対応し、予約の接続方向は通常工事の完成時へ引き継がれる。既存経路の関係ない接続方向は保持する。隣接しているだけの平行経路を自動で接続しない。

電線・自動化ワイヤー・液体配管・気体配管・コンベアレールのネイティブ構成を参照する。断熱・輻射・大容量などの派生設備も同じ構成なら対象になるが、全設備の実機網羅を意味しない。異なるPrefabへの置換をこの接続操作だけで暗黙に許可しない。

world_editorのインフラ経路編集でもnativePathPlacement=falseを維持する。折れ線指定は既存仕様のマンハッタン展開であり、実行前に返却pathを確認する。セルの存在だけを通電・流量の証拠にしない。

応答の意味:

- presentCells: 予約または完成設備が存在するセル数。
- complete/success: 要求した敷設と接続計画の適用成否。通常工事の完了ではない。
- constructionComplete: 全経路セルが完成設備か。
- connectionPlanApplied: 指定辺の接続方向を適用できたか。
- networkConnected: 全セル完成後の物理的な両方向接続。予約中・予行ではnull。
- connectedCells: 全経路の完成・物理接続を確認できた場合のセル数。途中段階の接続セルの総数ではない。
- flowVerified: この操作だけでは流量・消費を測らないためfalse。

即時施工でもネットワークへの設備登録は本体のライフサイクルに任せる。停止中の施工直後には登録・ネットワーク再計算が未完了の場合がある。少し進行して停止し、経路・設備状態・貯蔵量を読み直す。

## 設備を残した切断

orders_control domain=designation action=cut_conduits の既定はmode=disconnect。指定辺を両側から外し、設備本体や内容物を削除せず、解体予約もしない。

- 単一セル: x/yとdirection=up/down/left/rightで隣接する一辺を指定する。方向なしはエラー。
- 矩形: x1/y1/x2/y2またはareaIdを指定。領域内部の隣接辺だけを切り、境界から外へ出る接続は保持する。
- type=wire/liquid/gas/solid/logic/travel_tubeで対象を限定する。
- type=autoは液体・気体・固体の3系統。allは電線・自動化・移動チューブも含む。
- dryRun=trueはハンドラでは読取予行。実行はconfirm=trueが必要。detail=trueで対象辺・設備IDを返す。
- 外側のworld_editor予行は構文と経路の予行だけの場合がある。返却phase/previewを確認し、設備の予行結果と混同しない。

同じ切断の繰り返しは対象辺0となる。再接続は同じ2セルをauto_connectで指定する。

切断は通常UIと同じDisconnectCellsActionを使用し、両端の可視性と正確な対象を検証する。UI/APIが使えない場合に接続マスクの直接変更へ切り替えない。コールバック途中の失敗はpartial=true、disconnectedEdges=nullとなる場合があり、成功件数として扱わず現物を読み直す。flowVerified=falseは維持する。

## 旧動作の解体

旧cut_conduitsは設備の解体予約だった。旧動作が必要な場合はmode=deconstructを明示する。通常の解体操作で正確な設備IDを指定する方法もある。cutだけで従来の解体が予約されることを期待する既存クライアントは変更が必要。

## 検証範囲

本番接続ソースを使うホスト回帰は tests/OniMcp.UtilityConnections.Tests、登録と切断の契約検査は scripts/verify_utility_network_refresh_contract.py。ネイティブマネージャーの振る舞いと実輸送は別途ゲームで検証する。
