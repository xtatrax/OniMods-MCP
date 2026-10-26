# 野営トイレ診断の受動読取

メンテナンス一覧の `Toilet` 診断では、`GetSMI()` が返す既存cacheだけを一回取得する。通常の `smi` getterは未生成時にSMI・scheduler・controllerを生成し得るため読取には使わない。`Toilet` と `Toilet.StatesInstance` のexact型、masterの参照一致、既存current stateを確認し、不在・型違い・所有違い・未開始・読取例外を区別する。読取からStartSM、選択、Refresh、状態遷移や補完を行わない。

既存の `flushesUsed` と `maxFlushes` はserialized masterから独立して読む。SMI由来の `flushesRemaining`、`isSoiled`、`isCloggedWithGunk`、`hasCleanChore`、`canCleanEarly` は取得不能を明示JSON nullとし、真0・false・負のnative残回数と区別する。個別の取得例外で既知の他値まで捨てない。`stateMachineAvailability`／`stateMachineReason` はcacheの可用性、`unavailableFields` はfield別の取得不能理由を示す。既存stateの `name` だけを `currentState` と取得元へ投影し、SMIオブジェクト全体は返さない。

一覧のactionとstateは同じsnapshotを使う。外側 `canExecute=false` は実行用guardであり、内側 `canCleanEarly=null` を正常なfalseへ置換しない。`canExecute=true` もnative Buttonの現在の存在・操作成功・清掃完了を保証しない。実行側の既存処理を本段階でnative UI委譲へ変更した意味ではない。

清掃actionのtitleは、同じ早期清掃ButtonInfoが使う `global::STRINGS.UI.USERMENUACTIONS.CLEANTOILET.NAME` の解決済み名称を再利用する。ゲーム言語での作業名であり、MCP独立prose言語に従う名称ではない。新しいPO文言は作らず、新しいJSON key・reason codeは機械用識別子として扱う。`clean_toilet`、既存tool aliases、引数とqueryの絞込順序は保つ。queryは従来どおりJSON全文を対象に大文字小文字を無視した部分一致を先に調べ、不一致時は元のmanaged辞書で `clean_toilet` とstring titleの行があるかを先に確かめる。該当行がある場合だけ独立したJSON投影の清掃row.titleを固定互換aliasへ置き換えて、同じ大文字小文字を無視する部分一致を調べる。旧英語の表示文全体やそのtitleのJSON断片を検索に使える一方、元辞書・他action・順序・native snapshotは変更せず、aliasを応答fieldや表示fallbackへ出さない。actionKeyでの検索も引き続き使える。

実helper全文を既存NativeUi試験へ接続したホスト398条件と、本体参照ビルドの0警告・0エラーを確認した。native objectと共通metadata／action整形だけを境界へ置き、cache不在・型／所有／開始状態・個別例外・明示nullのwire・真0／false／負値・一回snapshot・非生成／非書込／非選択を照合した。追加投影の有無は試験専用のmanaged serializer getter回数で区別し、製品へ計測処理を加えていない。旧2メソッドの完全メンバー抽出と修正前helper全文の負対照は、それぞれコンパイル成功後、SMI非生成と不要な追加投影を禁止する意味条件で失敗した。旧TargetInfo全文や実行handler全体の試験、実機内部のfactory呼出計測とは区別する。

開発版と公開版のそれぞれ自身のソース・ホスト398条件・本体参照0W0Eと配備DLLで、停止中の可視で完成済みのOuthouseをworldと1セル矩形で絞り、使用回数・上限・残回数、既存state名、soiled／clogged／cleanChore／早期清掃条件とnative作業名、取得不能理由の明示nullと空配列を限定確認した。反復取得、actionKey・旧英語title・旧titleのJSON断片・native titleのJSON断片と不一致の6検索を照合し、同じ対象と状態を保持した。6時計項目・報告カメラ・固有設定先／所有パッチを照合し、保存せず通常終了後に元の導入物・設定・保護セーブ・既存画像と未起動状態へ復帰した。清掃操作・時間進行・保存・UI選択は要求していない。公開版固有の設定先・所有パッチID・重複なしを独立照合し、切替時に観測したMOD不一致は通常のOK経路で一回だけ確定した。双方で元の導入物・設定・保護セーブ・他MOD／DLC・共有データ・既存画像と未起動状態への復帰を照合した。

listのqueryとlimitは投影後なので、queryだけで全maintenance読取の非変更は主張しない。別の受動検索で同原点の完成設備は対象1基だけと確認したが、任意のKPrefabIDや追加componentの網羅的な不在証明ではない。対象外の既存処理へ一般化しない。cache欠測・型違い・未開始・例外の境界はホストでの確認であり、実機の自然な欠測例を得た証拠とはしない。早期清掃後の使用回数回復・補給・中断・満杯からの通常清掃との比較とnative清掃ボタンの操作は引き続き別の受入範囲であり、早期清掃の成立条件全体は未完了である。

ホストの置換境界はMOD内の同名STRINGS衝突をモデル化していない。名前解決は本体参照ビルドで確認し、本体namespaceを `global::STRINGS` と明示する。

1セル矩形の読取は、公開 `world_editor` の `/active/ops/coordinate.md` から既存の `building_control domain=side_surface surface=maintenance action=list` へ委譲して確認した。四辺すべてとworldを指定し、通常toolへの直接座標入力やqueryだけの絞込で代用しない。
