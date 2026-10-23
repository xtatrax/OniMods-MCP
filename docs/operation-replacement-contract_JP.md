# 操作replacementの単一コマンド制限

`/active/ops/*.md` の実行コマンドは、1つのSEARCH/REPLACEのreplacementにつき最大1件とする。既存の制限を維持し、複数件の拒否を実行・事前検査の両経路で同じ構造化結果へ揃える。一括実行エンジンや自動rollbackは追加しない。

## 複数コマンドを拒否した結果

| フィールド | 意味 |
| --- | --- |
| `ok=false` / `phase=preflight` | このreplacementを実行前に拒否 |
| `code=operation_replacement_command_limit` | 安定した拒否ID |
| `scope=operation_replacement` / `file` | 拒否対象のreplacementと既存の相対操作ファイル名 |
| `maxCommands=1` / `requestedCommands` | 上限と、既存の行抽出規則が拾ったコマンド候補行数 |
| `executedCommands=0` / `changed=false` / `partial=false` | このreplacementの子コマンドを呼ばず、変更していない |
| `error` / `guidance` | 現在のMCP言語による理由と次の操作 |

`world_editor command=edit` は、この拒否を外側のpreflightエラーへ包む。外側の `error` は内側の構造化結果をJSON文字列として保持する。通常結果のContentを識別した後、外側と必要な内側を別々に読む。外側の `applied=0` / `appliedUnit=edit_blocks` は適用した編集ブロック数、内側の `executedCommands=0` はこのreplacementの子コマンド実行数を表し、単位とscopeを混同しない。

空行・コメント・コードフェンス等の扱いは既存の抽出規則を維持する。上限拒否は個々の行の構文・本体側適合性を検査した意味ではない。1件の場合の解析、task・confirm・dryRun、能力検査、実行順、停止条件とpartial集約は変えない。

## 複数の操作を進める場合

1件ずつ予行し、対象と結果を確認して実行し、結果を再取得する。複数SEARCH/REPLACEブロックの要求は既定で拒否し、明示的な `allowPartial=true` の場合も各replacementは最大1コマンドのままとする。全ブロックの事前検査の後に実行ループへ進むが、事前検査の通過は実行完了の保証ではない。実行時に後の操作が失敗すると先に成功した操作の変更が残る場合があり、自動rollbackは行わない。今回、原子的な一括予約APIは追加しない。

拒否結果のゼロ実行・ゼロ変更は、そのreplacementの複数コマンド拒否についての値である。要求全体・他の操作・すべてのnative処理の原子性を表すものではない。

## ローカライズ対応

| PO context | Runtime JSONキー |
| --- | --- |
| `OniMcp.MCP.OPERATION_FILES.SINGLE_COMMAND_ERROR` | `operation_files.single_command_error` |
| `OniMcp.MCP.OPERATION_FILES.SINGLE_COMMAND_GUIDANCE` | `operation_files.single_command_guidance` |
| `OniMcp.MCP.OPERATION_FILES.SINGLE_COMMAND_SAFETY` | `operation_files.single_command_safety` |

PO4ファイルを正本・準備資産とし、現行の `operation_files.en/ja/zh-Hans.json` に対応する。暫定名 `zh-hant.po` の繁体字訳と、注記に保持する既存互換のzh-Hans runtime訳は別に保つ。言語マッピングやPOローダーの対応は変更しない。機械ID・JSONキー・DSLのSEARCH/REPLACE、confirm、dryRun等は翻訳しない。

## 検証の境界

旧ソースとの比較と実ソースのホスト試験、本体参照ビルド、両版の初回・通常再ロード後の実HTTP応答を別々に確認した。複数コマンドの予行・確認実行・未確認での同じ拒否、コメントを除いた単一コマンド、明示的な既存複数ブロックと後段の事前検査拒否、同じ言語の理由・案内、停止時計と保護対象の保持を受入れた。

全blockの事前検査順序と拒否時の子コマンド非呼出は実ソース・ホストの根拠を併用する。実機の冪等な一時停止と時計保持だけで、先のnative呼出が無かったことを独立に証明したとはしない。直接Applyガードの独立した実機呼出、native子処理の実行時partial、GUI全工程は未検証で、新しいbatch機能や原子性・rollbackを保証しない。

ポリシー設定を変えず、試験後は元導入物・設定・保護セーブ・共有保存・既存画像・他MOD/DLCと開始時の未起動状態へ復帰した。保存先は通常の原本ロードで戻し、本体が記録した採番値を採用した。preferencesの直接書戻しは行っていない。
