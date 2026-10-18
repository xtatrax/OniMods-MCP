# 仮想検索操作の結果保持

`ops/search.md`から成功した検索の`results[].result`は、子`search_control`の先頭テキスト結果を完全な文字列で返す。従来は`detail`省略時とnativeの文字列`detail=full`でも`summary=ok`しか返さず、検索内容が失われていた。同じcanonical操作を`ops/any.md`から呼ぶ場合も保持する。

| 子コマンドのdetail | 検索結果 | 引数の重複出力 |
| --- | --- | --- |
| 省略 | 子の既定粒度の完全結果 | なし |
| brief / compact / full | 指定粒度の完全結果 | なし |
| boolean false | 従来の小さい要約 | なし |
| boolean true | 従来の全文 | あり |

`query`、`limit`、world・可視性等の範囲を変えず、必要な量を子の引数で絞る。検索失敗の既定は上限付きエラー概要を維持し、boolean trueを明示したときだけ従来通り全文と引数を添える。他ツールの既定要約は変更しない。`ops/search.md`でtoolを別操作へ上書きした場合も実際のcanonical操作名に従う。内部検索の未登録aliasを新たに有効化する変更ではない。

領域ごとに子結果の構造・件数の意味が違うため、ラッパーで統一・推測しない。たとえば資源の`truncated`は省略件数、世界検索では`matched`がセル数で`returned`がクラスタ数になる場合がある。元に存在しない打切りフィールドを追加せず、glyphの独自形式とnull記号も保持する。検索はゲーム変更件数の`applied=0`のまま、外側SEARCH/REPLACEのパッチ件数と区別する。

案内文は`OniMcp.MCP.OPERATION_FILES.RESULT_CONTRACT`を4 POへ追加し、実行用3 JSONの`operation_files.result_contract`へ対応させる。POの暫定zh-hantと既存zh-Hansの区別を維持する。単一コマンド・task・confirm・dryRun・座標／再帰呼出制約は変更しない。

## 検証と限界

実際のOperationFiles・解析・実行ポリシーを連結するホスト試験は既存197・新規検索342・metadata12条件が成功。ゲーム／registryのdispatch境界、sandbox policy／scope、無関係なファイル／画面処理を代替し、canonical内部名のみ登録する実際のregistry仕様へ合わせた。内部aliasの追加は行っていない。文言1キーのPO4／runtime JSON3を照合した。

開発IDで7領域14ケースの直接相当経路とのJSON一致を確認し、長い明示full、件数／打切り、空結果、確認／予行／拒否、停止時刻と原本・設定の不変を含む210条件が成功した。これは公開IDの変更後実機受入を意味しない。世界検索のkind=cellsはnative側で適用されずkinds=allだったため、セル限定検索の正確性はこの試験の証明範囲外。

公開作業ツリーのホスト試験・文言照合とゲーム同梱DLL参照Compileが成功（警告0／エラー0）。オンラインNuGet監査は今回の実行のみ省略し、脆弱性監査の成功とは扱わない。公開IDでの配備・実機受入は未実施。

任意MOD・全検索領域／全粒度・複数Contentの統合・子自身の不正JSON修復・他opsの既定結果保持は対象外。個別セーブ・座標・生のMCP応答・実機ログ・利用者設定・開発履歴は含めない。
