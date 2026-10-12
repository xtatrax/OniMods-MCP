# 製造ポッドの補給品の決着方針

`/settings/` の「製造ポッドの受取・全拒否を必須にする」で切り替えます。
ゲーム内MODオプションでも設定可能です。既存の設定キー `RequirePrintingPodSelection` は維持し、既定値は `false`（無効）です。

- ON / `always_resolve`: 受取可能なら通常作業の前に補給品を1つ受け取る、または全て拒否して決着するようAGENTへ指示します。保留・放置は完了扱いにしません。
- OFF: この機能は介入しません。確認・判断・保留理由・再確認の要求も出しません。放置や失念を含め、AGENTのコンテキスト任せです。

ON時だけツール応答へ `printingPodSelection`、補給品一覧へ `selectionPolicy` を添えます。
OFF時にはこれらの項目自体を出力しません。通常の候補照会・受取・全拒否操作は設定に関係なく利用できます。
ONの案内には `mode`・`available`・`selectionRequired`・`decisionRequired`・`allowedDecisions=[claim,reject_all]` を返します。
`selectionRequired` は互換のための名前であり、受取か全拒否のいずれかを求める意味です。
ロード中・メインメニューでは案内しません。設定変更は次の応答に反映します。

これはAGENTへの行動指示で、ゲーム自動停止・MODによる自動受取・ツール実行の強制遮断ではありません。
AGENT未接続時やツールを呼ばない間にMODが代行することはありません。複製人間の自動選択も含みません。

操作は `building_control domain=side_surface surface=facility kind=printing_pod` の以下のactionを使います。

1. `list_rewards` で状態・候補を確認。読取だけでは候補画面を生成しません。
2. 必要なら `open_immigrants confirm=true` で対象ポッドの画面を開きます。
3. `claim itemId=取得したID confirm=true` または `reject_all confirm=true` で決着します。

全拒否は複製人間を含む現在の全候補を拒否するゲーム本体の `ImmigrantScreen.OnRejectionConfirmed` 経路です。
対象の画面が開いていること、受取可能であることを確認し、候補の破棄・画面閉鎖・本体の待ち時間処理をまとめて行います。
`dryRun=true` は変更せず予行を返します。未確認・対象違い・画面なし・受取不可は拒否します。
操作不能ならAGENTは理由を報告し、決着済みとは扱いません。

2026-10-07の以前の実機レポートは旧仕様（OFFでも状況判断を要求）の履歴です。
今回の仕様変更はホスト回帰試験・ゲームDLL参照ビルドに加え、更新DLLを適用した実機でOFF無介入・ON全拒否・ON受取を確認しました。
詳細は 新仕様の実機試験 (private runtime report omitted) を参照。自然クールダウンの連続試験とは分けて記録しています。
