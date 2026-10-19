# 既設一辺の本体UI接続

## 対象と操作契約

`building_control domain=planning action=connect_existing` は、完成した隣接二セルの一辺を本体の通常建設UI経路で接続する。液体管・気体管・コンベア・電線・自動化線を対象とし、同じ定義・素材・network manager、置換予約なしに限定する。既存の `auto_connect` / `repair_line` を置き換えたり、通常建設全体を移行した機能ではない。

本体の `Activate → OnLeftClickDown → OnDragTool → OnLeftClickUp` を呼び、指定二セルのvalidな本体経路を確定前に照合する。MODによる接続マスク変更・ネットワーク再構築・失敗後の補修や自動再試行を行わない。混在する設備定義や素材、空セル、建設中、置換予約、ブリッジ、移動チューブは対象外。各派生設備の実機受入は別途必要。

座標は `/active/ops/coordinate.md` をfreshに読み、正確なSEARCH/REPLACEで指定する。以下は架空の書式例であり、実際の対象座標ではない。

```text
call targetTool=building_control x=20 y=30 payload={"domain":"planning","action":"connect_existing","type":"liquid","direction":"right","confirm":true} dryRun=false detail=true
```

typeは `liquid/gas/solid/wire/logic`、directionは `up/down/left/right`。矩形・points・steps・id・素材・prefab指定を受け付けず、単一の辺だけを扱う。task/confirm・アクティブワールド・両端の可視性・停止中の規約を維持する。直接building_controlへ座標を渡す制限は変更しない。

プレイヤーのドラッグ中、建設ツール使用中、必要UIの不在やAPI変更では拒否する。UIを切り替えた後は成功・失敗とも本体SelectToolへ戻り、ドラッグとプレビューを残さない。選択ツールや画面表示への影響は `uiRetargeted` で示す。

## 受付と観測

確定前に実体・ID・素材・定義・network manager・マスク・可視性・停止・ワールドを再確認する。確定後は同じ実体を保持し、両端に要求方向だけが加わった接続マスクを独立観測する。既に双方向接続ならクリックなしの成功とする。

`actionAttempted` は本体確定処理の試行、`connectionVerified` はマスクの観測であり、実搬送を保証しない。`flowVerified=false` を維持し、実流量は別に観測する。途中失敗は `partial`、読取不能はnull、未知の建設予約有無もnullとして保持し、成功へ要約しない。子ハンドラーのdryRunは読取検査だけを行い、UIの選択・本体ドラッグ受付を試行せず `connectionVerified=null` を返す。

## 公開側の検証範囲

公開側の実ハンドラー・アダプターを使うホスト試験は接続253・既存切断69条件に成功。5系統×4方向、予行・繰返し・拒否条件・確定前の対象変化・本体失敗や途中変更・読取不能・他辺保持・UI片付け失敗を含む。既存Tools回帰も成功。追加14キーのPO4本と実行用JSON3言語について構文・重複・翻訳・placeholderを照合した。

公開IDのゲーム同梱DLL参照Releaseビルドは警告0・エラー0。これはUnity実行の保証ではない。公開IDでの配備・実機受入・復元は別に確認して記録する。個別のセーブ・ログ・ID・設定は公開文書へ含めない。
