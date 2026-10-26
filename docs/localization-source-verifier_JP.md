# ローカライズのソース検査と実行時の一覧照合

`scripts/verify_mcp_localization_catalog.py` は、ONIを起動せず、現在のソースとJSONカタログの契約を調べる補助検査です。C#のコンパイル、本体動作、全翻訳の完成を証明するものではありません。今回の修正は検査器とPython試験だけで、製品C#や翻訳本文を変更していません。

## 読み取る範囲

- 通常の引用符付き文字列と、定数キーの `McpLocalization.Get("key")`、`McpLocalization.GetForLanguage("en", "key")` を英語基準で解決します。入れ子のinitializer、通常文字列、コメントを区別し、トップレベルの説明を読みます。C#を実行する処理ではありません。
- 既存の `Params()`、登録済みの `RectParams` と、正確な呼出元・引数名・定義先を登録した `StorageTools.StorageScopeParameter()` だけを扱います。Coreの英語説明の上書きも追跡します。未知の式・factory・wrapperや未対応の構文は、黙って飛ばさず拒否します。汎用C#パーサーではありません。
- 同名の索引代入はC#と同じ後勝ちとして読み、上書き前後の位置をshadow監査へ残します。catalog参照のキー・解決元ファイル・文脈も別監査として保持し、重複や参照元を隠しません。

各言語の複数カタログを平坦化し、ファイル内・ファイル間の重複キーは値が同じでも拒否します。`en`／`ja`／`zh-Hans` のキーと書式引数の対応を保ち、1個の `tools.en.json` だけを全名前空間とみなしません。既存の `game_save_control` 用5キーだけは出典付きで公開一覧の対象外へ登録し、除外を監査します。未知の `tools.*` 名前空間、不正な形、孤立した公開キーは拒否します。

暫定 `zh-hant.po` と既存 `zh-Hans` runtimeの区別は変えません。PO読込や言語選択を追加せず、既存翻訳を別catalogへ複製して検査を通す変更もしません。[言語切替・翻訳追加の手順](mcp-localization_JP.md)を引き続き適用します。

## 実行と判定の順序

```sh
rtk proxy python3 tests/test_verify_mcp_localization_catalog.py -v
rtk proxy python3 scripts/verify_mcp_localization_catalog.py --self-test
rtk proxy python3 scripts/verify_mcp_localization_catalog.py --self-test --root . --tools-list tools-list-response.json
```

`--root`、`--self-test`、`--tools-list` の既存CLIは保持します。`--tools-list` は保存済みJSON-RPC応答またはそのresultオブジェクトを読み、ゲームへ接続しません。

主CLIは、catalogの言語間対応・重複、ソース抽出と監査、canonical metadata coverageを調べ、その後に任意の `tools/list`、英語説明・コード語句、埋込宣言を照合します。coverageで拒否された場合、その呼出では後段の `--tools-list` も埋込宣言等も未到達です。`--self-test` の成功やファイル引数を渡したことだけで、主CLI全体の成功と記録しません。

## coverageと実行時inventoryは別の検査

canonical coverageは、公開説明の `tools.<tool>.description`、`tools.<tool>.parameters.<parameter>` と共有taskの正規キーが揃うかを厳密に調べます。一方、実行時inventoryの補助検査は、抽出したソースの一覧を期待集合として保存済み応答へ渡し、公開ツール・引数集合と各ツールの必須string taskを照合します。後者は `verify_tools_list_response` を独立に呼ぶ検査で、主CLIのcoverage拒否を成功へ変える操作ではありません。

別catalogへの有効な直接参照や既存fallbackがあっても、正規キーの整備とは区別します。coverage gapの件数から、実行時の未翻訳・説明欠落を断定しません。inventory一致も、全説明の翻訳品質、全言語での追従、fallbackの時機や画面表示の受入ではありません。各版自身のソースに対応する配備物から得た応答を用い、別版の結果を代用しません。

## 今回の限定確認

19件のPython回帰試験と、旧検査器全文を同じ正例へ読み込む意味的な負対照を確認しました。旧版はimportや構文の失敗ではなく、既存のconstructor APIが定数catalog参照の正例を拒否することで失敗します。

開発側ではソース抽出と自身の実応答が306正規化項目で一致しました。一方、主CLIは既存18件のcanonical coverage gapsで拒否したままで、全体PASSではありません。この18件を実行時の翻訳欠陥とは判定していません。変更していない製品ソースに対応する既存配備物を使用しており、新しいDLLビルドやC#の実機変更を行った証拠とは扱いません。

公開側も自身のソースと配備物から新規取得した英語応答を照合し、295正規化項目・全7toolの必須taskが一致しました。canonical不足は8件・余剰0で、主CLIはcoverageで拒否したままです。両版の取得した英語説明では、未解決キーの露出と日本語かなの混入は確認されませんでした。これらの限定確認を、全言語の翻訳品質や主CLI全体の成功とは扱いません。

各版でMCP言語だけを通常設定から変更・復帰し、停止時計と視野を保持しました。変更のないMODソース・資源と既存の各版自身のDLLを照合して用いたため、新DLLビルドは0件です。公開版切替時の単一MOD不一致は、観測したトークンと通常確認ボタン経路で一度だけ閉じました。保存・進行は行わず、元の導入物・設定・保存先・保護セーブ・既存画像と未起動状態へ復帰しました。

残る正規キー整備、未移行の文言、言語・画面の受入は別に残し、親のローカライズTODO全体を完了にしません。
