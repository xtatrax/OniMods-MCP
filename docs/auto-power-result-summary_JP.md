# 自動配線の子結果を親で保持する

建設予行・予約の `powerAutoConnect` が、子の拒否や予行を `connected_or_planned` にまとめる不具合を修正する限定対応です。子の配線処理、経路・定義の選択、地形許可、素材・支持・通常工事の判断は変更しません。

## 問題と返却契約

子の配線は、経路の異なるutility定義や不可視セル等を早期拒否すると `success=false`・`committed=false` とreasonCodeを返し、件数の `failed` を含まない場合があります。旧親集約は `failed>0` だけを判定し、欠測を0へ補うため、拒否を接続済み相当に表示しました。正常な配置予行にも同じ表示が付いていました。

修正後は先頭ContentのJSONと子のIsErrorを確認し、子の報告を有界な型へ読み分けます。JSONでない主結果、未知の型、欠測を成功にしません。後続Contentを主結果の代わりに採用しません。

| status | 意味 |
| --- | --- |
| preview_valid | 子の配置予行が明示的に成立。予約・接続・工事完成の実績ではない。 |
| preview_rejected | 子が予行を拒否、または検査の失敗/未完了を返した。 |
| rejected | 子の実行が拒否/失敗/未完了を報告し、部分配線予約の根拠も確認できない。 |
| partially_planned | 子が通常予約の一部を作った根拠と、失敗/未完了が併存する。 |
| planned_or_reused | 子の通常予約/既存利用が明示的に成立。物理接続や通電の保証ではない。 |
| unknown | 正常や拒否を確定する値が欠ける、または要求と子報告が矛盾する。 |
| connect_result_unparseable | 主結果のJSONを取得できない。元のtextとIsErrorを保持する。 |

`planned`・`reused`・`failed`・採掘の件数は、非負の整数として確認できた値だけを返し、欠測・負値・文字列・小数・範囲超過はnullにします。`unknownCountFields`・`unknownFlagFields` で不確定な項目を示します。`countsScope=preview` は予行内の観測、`observed` は実行結果の子報告です。

子のbool報告は `childSuccess`・`childComplete`・`childCommitted`・`childDryRun` に保持します。予行で予約件数>0、commit、新規採掘予約を主張する矛盾はunknownとし、親のplannedと矛盾した新規採掘件数をnullにします。既存の採掘指定数と既存セル再利用の観測値は分けて保持します。子の原本は `connectResult` に保持し、矛盾を消して通常の成功へ書き換えません。

拒否やunknownは「何も変更しなかった」の保証ではありません。子のcommittedは採掘指定だけでも成立し得るため、それだけで部分配線予約としません。必要な場合は子の原本と件数、独立したセル/指示の読取で変更を確認します。設備本体の `valid` と配線結果を分け、配線拒否から設備本体の配置検査まで失敗へ変更しません。

## 予行の経路と限界

map editの親dryRunとopsの引数予行は構文・展開の確認であり、子の配置検査を実行しません。この限定修正でもその契約を維持します。

配置予行の実機受入は、正式なworld_editorの座標operation経路から `planning action=preview` を呼びます。親operationを通常実行するとpreviewハンドラー自身が子のdryRunをtrueに固定します。親がコマンドを実行したことと、設備を予約/完成したことを分けます。公開窓口で禁止された座標の直接呼出は行いません。

配置予行は現行MCP配置検査です。native予約の受付、実施工、物理接続・通電の受入へ一般化しません。通常Wire固定と各種Wireを候補にする選択の差、全utility混在や全予行の読取非変更は別の残作業です。

## 検証

| 区分 | 状態 |
| --- | --- |
| 実装・ホスト | 子とnative境界を代替し、実際の集約器と親adapterで562条件成功。既存auto-route378・utility56条件も成功。8種類の意味のある変異を検出。 |
| ゲーム参照ビルド | 開発build.56・公開build.12、各警告・エラー0。配備DLLと実際の応答版を照合。 |
| 開発IDの実機 | 通常Wireのpreview_valid、異種Wire競合のpreview_rejected、左右の自動配線OFFを確認。早期拒否の欠測件数・complete/dryRunはnullを保持。 |
| 公開IDの実機 | 公開build.12でも通常Wire/異種WireのON・左右OFFの同じ4条件と独立状態比較に成功。開発実機の結果で代用しない。 |
| 復元 | 元の導入物・設定25ファイル、保護原本、未保存試験分岐、元保存先の選択、開始時のゲーム未起動を独立照合。新規Player.logのException行0。 |

予行4条件の前後で35セルの概要・設備有無・接続表示・採掘指定・自然地形、既存電線2個の実個体IDと素材、停止時刻、カメラ/zoom/world・Overlay metadata・sandbox UI toggleを独立照合しました。観測日時は比較から除外し、全コロニーの不変へ一般化しません。InstantBuildMode・ActiveTool・全selectionは独立読取窓口がないため非変更を実証していません。試験のためのsetupと予行自身の非変更を分け、原本を保存しません。個別の保存先・座標・実ID・生応答・ログ・本体アセンブリ等は公開差分へ含めません。
