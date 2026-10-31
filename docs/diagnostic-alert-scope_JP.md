# MCP診断とHUD通知の取得範囲

MCPの `alertLevel=green` や警告の空配列は、実行したMCP独自チェックで指摘がなかったことを表します。HUDの全警告がないことや、コロニー全体の安全を保証する値ではありません。可視酸素の在庫が十分でも、日次の酸素収支が赤字である場合があります。

## 取得経路と互換性

| 経路 | 返すもの |
| --- | --- |
| `oni://colony/diagnostics` | 既存のMCP独自チェックと、新しい `alertCoverage` |
| `oni://colony/alerts` | 従来どおりMCPの指摘の配列。`[]` をオブジェクトで包み直さない |
| 状態snapshot | 要求範囲のMCPチェック、従来の本体非常モード読取、トップレベルの `alertCoverage` |
| `oni://colony/notifications` | 本体通知を別に読む経路 |
| `oni://colony/report` | 本体日報を別に読む経路 |

既存の警告項目の形、配列形式、しきい値はこの変更では置き換えません。snapshotの `alerts` オブジェクトには `coverage` を追加します。時刻とワールド範囲を含めて読む際は、トップレベルの `alertCoverage` を使います。snapshotの `cost.alertReplacementScope` は `mcp_rule_checks_only`、`cost.notReplaced` は通知と日報のURIを明示します。必要なときだけ、それらを追加取得します。

## `alertCoverage` の意味

| フィールド | 値・意味 |
| --- | --- |
| `source` | `mcp_rule_checks`。本体HUD通知の一覧ではない |
| `model` | `diagnostics` または `snapshot`。既存の判定モデルを区別する |
| `evaluated` | 今回MCPルールチェックを実行したか。入力の全件取得やHUD全体の評価完了は表さない |
| `overallComplete` | 常に `false`。この取得だけで全警告の評価は完了しない |
| `hudWarningCount` | `null`。HUD警告数は未取得であり、0件へ補完しない |
| `dailyOxygenBalanceKg` | `null`。日次酸素収支は未取得であり、0kgへ補完しない |
| `oxygenCellCheck` | `visible_stock_only` または `not_evaluated`。可視大気の在庫確認と未評価を区別する |
| `populationScope` / `buildingScope` | 下記の人口・完成設備の対象範囲 |
| `worldId` | snapshotの要求範囲。診断では可視大気を読むアクティブワールド |
| `sampledCycle` / `sampledTimeOfDayPercent` | MCP取得時のサイクルと時刻。時刻を渡せない診断経路の後者は `null` |

これらの取得時刻は、本体の個別警告条件が最後に更新された時刻ではありません。`includeAlerts=false` でもminimalやwatchなどの要求でルールチェックを実行する場合があるため、実行有無は `evaluated` を見ます。要約文にもMCPチェックの範囲とHUD通知・日次酸素収支の未取得を明示します。

## ワールド範囲

診断の `populationScope=all_live_worlds` は全ワールドの生存複製人間、`buildingScope=all_completed_worlds` は全ワールドの完成設備です。診断の可視大気はアクティブワールドだけを読みます。人口・設備までアクティブワールド限定と解釈しないでください。

snapshotの人口・設備は、ワールド指定時に `requested_world`、負の `worldId` による全体集計時に `all_worlds` と表示します。完成設備の重複除外キーにはワールドIDを含め、別ワールドの同じ座標・同じ設備を1台にまとめません。本体非常モードは既存の `AlertManager` 読取を使い、全体集計時はアクティブワールドを参照する従来の動作です。

## 追加処理の境界と残る確認

追加したcoverageは、既存結果から作るメタデータ投影です。本体通知・日報・過熱の診断条件を新たに読み出したり、条件評価や `Refresh` を呼び出したりしません。`includeAtmosphere=true` の既存セル走査は可視酸素の在庫を追加するもので、生産量と消費量の収支を計算する機能ではありません。

検証結果は実施した範囲だけ追記します。

- ホスト回帰: snapshot 202条件・本体欲求境界309条件・診断513条件と、10ラベルのPO／runtime対応を確認しました。実ゲームの初期化やHUD描画の証明とは区別します。
- 修正前のsnapshot／診断の全文を使う負対照は、コンパイル後にcoverage欠落の意味的な検査で失敗します。
- 冷ロードの停止実機では、最小表示・watch・詳細診断・チェック省略の全惑星指定でcoverageと解決済みラベルを確認し、従来配列と非食料診断を照合します。版ごとの配備物・所有ID・設定分離・元環境への復帰は、別々に確認します。
- 食料の集計済み状態と冷ロード直後を数値同一とする比較は成立しませんでした。これは既存の初期化待ちの観測として分け、確認のための自動進行やキャッシュの更新要求は追加しません。

この限定修正だけで親TODOを閉じません。同じ停止点でHUD通知・診断・個別設備・日次酸素報告を対応させ、酸素収支、過熱・損傷、ストレスを比較する作業は残ります。
