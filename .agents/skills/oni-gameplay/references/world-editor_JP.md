# ワールドエディターの操作仕様

`world_editor` でマップ編集、画面外の範囲取得、操作ファイル、管理ファイル、計画、ブループリント、バッチ処理を扱う前に、このリファレンスを読む。

## 目次

- [基本的な仕組み](#基本的な仕組み)
- [仮想パス](#仮想パス)
- [必須の記号照会](#必須の記号照会)
- [共通のパッチ仕様](#共通のパッチ仕様)
- [正確な矩形範囲を指定するマップ編集](#正確な矩形範囲を指定するマップ編集)
- [マップトークンの編集](#マップトークンの編集)
- [型付き操作ファイル](#型付き操作ファイル)
- [管理・計画・ブループリントファイル](#管理計画ブループリントファイル)
- [ワールドエディターのバッチ処理](#ワールドエディターのバッチ処理)
- [使用例](#使用例)
- [トラブルシューティング](#トラブルシューティング)

## 基本的な仕組み

`world_editor` は、リアルタイムのゲーム状態を仮想ディレクトリとして公開する。読み取りでは現在の状態を表示し、編集ではテキストパッチを送信する。送信されたパッチは事前検証によってゲーム操作へ変換される。

```text
world_editor command=pwd|cd|ls|read|zoom|grep|symbols|search|edit|blueprint|batch
  path=<virtual path> content=<patch>? dryRun=? confirm=? allowPartial=?
```

ゲーム速度、カメラ・ビュー・オーバーレイ、スクリーンショットに関する操作の転送にも対応する。仮想ファイルの手順に自然に組み込める操作を除き、専用の集約ツールを優先する。

## 仮想パス

| パス | 用途 | 編集形式 |
|---|---|---|
| `/active/index.md` | 読み取り専用のコロニー索引。必要に応じて初回呼び出し用の状態を含む | 読み取り専用 |
| `/active/map/viewport.md` | 指定した矩形範囲内の地形、オブジェクト、作業指示 | マップトークン |
| `/active/map/index.md` | 編集可能な表示範囲マップの別名 | マップトークン |
| `/active/map/layers/layer_<yMin>_<yMax>.md` | ワールドを高さで区切り、現在のオーバーレイで表示した範囲 | マップトークン。ただし接続記号を除く |
| `/active/symbols/glyphs.md` | 自動生成されるトークン一覧 | 読み取り専用 |
| `/active/ops/tools.md` | 現在公開されている型付き操作ファイルとツール | 読み取り専用 |
| `/active/ops/orders.md` | 採掘、モップ掛け、片付け、消毒、収穫、キャンセル、解体、攻撃、捕獲 | 単一コマンド |
| `/active/ops/build.md` | 座標を直接指定しない意味ベースの建築計画 | 単一コマンド |
| `/active/ops/{game,colony,read,search,dupes,navigation,server,...}.md` | 型付きのツール呼び出し | 単一コマンド |
| `/active/management/{schedule,priorities,dupes,food,skills,research}.md` | 管理パネルのスナップショットと編集コマンド | 単一コマンド |
| `/active/dupes/<name>.md` | 複製人間ごとの詳細 | `Name:` などの対応するフィールドの編集 |
| `/active/buildings/index.md` | 建築済み設備のパラメーター索引 | 読み取り専用 |
| `/active/buildings/instances/<prefab>-<InstanceID>.md` | 設備ごとの安定したパラメーターファイル | 正規形式のフィールド1行 |
| `/active/buildings/plans.oni` | 建築計画のテキスト | 計画のパッチ |
| `/active/infrastructure/*.oni` | 配線・配管の接続計画 | 計画のパッチ |
| `/active/infrastructure/*.md` | インフラのマップ表示 | 正確なマップ範囲の事前検証。接続記号の編集は拒否される |

変更できるのは `/active/` のみ。他のセーブスロットは過去の状態、またはロードされていないデータの表示となる。

ワールドエディターの通常の建築は常に `instantBuild=false` を使用し、通常の素材ルールに従う建築予定を作成する。これはグローバルのデバッグ用即時建築が有効な場合も同じ。操作単位で即時建築を行うには `instantBuild=true allowSandbox=true confirm=true` が必要。サンドボックスへの書き込みでは、さらに `world_editor command=sandbox allowSandbox=true confirm=true` と、操作に対応する個別の許可（`allowTerrainMutation`、`allowEntitySpawn`、`allowDestroy`、`allowForce`）を指定する必要がある。バッチ内の子ステップで親の許可範囲を広げることはできない。

## 必須の記号照会

マップ記号を解釈する前、名前を記号へ変換する前、接続やオーバーレイの記号を読み取る前に、プレイ用スキルに従って正式な照会を行うことを commentary で伝える。その時点で不明な記号や名前をまとめて照会する。

```text
search_control domain=glyphs queries=["砖","?","┼","氧气"] direction=auto matchMode=auto
```

中国語の文字そのもの、古い凡例、記憶から意味を推測しない。`count=0` は不明として扱う。表示内容によって意味が変わる記号には `view=temperature|oxygen|light|decor|disease|radiation|crop|...` を指定する。照会結果を再利用できるのは、同じ実行環境かつ同じターンの中だけ。

記号から意味、意味から記号、および一括照会の例:

```text
search_control domain=glyphs queries=["砖","零"] direction=code_to_meaning view=temperature
search_control domain=glyphs queries=["砖","┼"] direction=code_to_meaning view=logic
search_control domain=glyphs queries=["砖块","研究站","氧气"] direction=meaning_to_code
search_control domain=glyphs queries=["■","液","不","易","可","难"] direction=auto view=oxygen perQueryLimit=20
```

`world_editor command=symbols` は互換用の読み取り専用の別名として引き続き利用でき、同じ照会フィールドに対応している。ただし、プレイ時の照会には `search_control domain=glyphs` を必ず使う。

この文書の例に含まれる中国語の記号・コマンド・座標ヘッダーは、実際の入出力構文なので原文どおりに記載している。日本語の文字へ置き換えない。

## 共通のパッチ仕様

`content` パラメーターには、1つ以上のマーカーブロックを記述する。以下では、リポジトリの差分チェックが未解決のマージ競合と誤認しないよう、マーカー行を字下げしている。パーサーは行内のマーカー文字列を認識する。

```text
  <<<<<<< SEARCH
<exact current text>
  =======
<replacement text>
  >>>>>>> REPLACE
```

実行条件:

- `dryRun=true`、`confirm` の省略、または `confirm=false`: プレビューのみ。
- `dryRun=false confirm=true`: 変換された操作を実行する。

規則:

- 原則として1つのブロックを使う。変更を伴うブロックが複数ある場合は、外側に `allowPartial=true` が必要。ゲーム状態の変更は、トランザクションとしてまとめてロールバックできない。
- すべてのブロックを変更前に事前検証する。明示的に部分成功を許可した方式が適用される場合を除き、どれかが失敗したら `phase=preflight` で中止する。
- パッチはテキストのみ。`editCells` や `editLines` のような座標データを送信しない。
- 実行後は再度読み取る。古くなったパッチを再利用しない。

## 正確な矩形範囲を指定するマップ編集

マップ行では、3行の明示的な X 座標ヘッダーと `Y=<n>:` 行を使用する。

```text
百位X: ...
十位X: ...
个位X: ...
Y=166: ...
Y=165: ...
```

### 推奨される明示指定

SEARCH には `百位X`、`十位X`、`个位X` の**3つすべて**のヘッダーと、その後に対象の Y 行を含める。現在の事前検証では、ここから正確な矩形範囲を導き、その範囲を元の状態から再取得する。カメラを移動・フォーカスせず、現在のカメラ位置にも依存しない。照合前には、内部で `format=edit compact=false` が強制される。

この正確な元範囲に対する検証は、`/active/map/viewport.md`、別名の `/active/map/index.md`、`/active/map/layers/layer_<yMin>_<yMax>.md`、インフラの Markdown マップに適用される。読み取り専用のコロニー索引 `/active/index.md` が編集可能になるわけではない。

SEARCH に X ヘッダーが1つでもあるのに、ヘッダーの組が不足している、形式が不正、内容が矛盾しているなどの問題がある場合、事前検証は安全側に倒して失敗する。表示範囲を基準とした相対座標へ暗黙に切り替わることはない。

旧方式の、表示範囲を基準とした一意な行の照合を使用できるのは、**X ヘッダーが一切ない**パッチだけ。この方式は、現在の表示範囲内ですでに見えている、ごく小さく曖昧さのない行にだけ使用する。

### カメラを動かさずに画面外の範囲を読み取る

画面外の矩形範囲は、カメラを動かさずに読み取れる。

```text
world_editor command=zoom path=/active/map/viewport.md
  x1=81 y1=42 x2=85 y2=42
  syncView=false focusCamera=false format=edit compact=false
```

`zoom` の状態を保持する必要はない。返された3行の X ヘッダーと必要な Y 行を、後で使うパッチへそのままコピーする。正確なヘッダーによる事前検証では、カメラとは独立して該当するワールド座標を再度読み取る。

パッチの位置を指定できるようにするためだけに、`navigation_control focus_cell` を使わない。

### パッチ作成には展開済みの行だけを使う

パッチを作成する際は `format=edit compact=false` を要求する。読み取り専用マップでは RLE（`粉x3`）を使用できるが、圧縮された行は、トークン数が重要となる編集で誤りが起きやすい。REPLACE は、各行の正確なセル数を維持する必要がある。

## マップトークンの編集

手順:

1. `format=edit compact=false` で読み取り、または画面外の zoom を行う。
2. 3行すべての X ヘッダーと、必要最小限の完全な Y 行の組を SEARCH へコピーする。
3. 同じ内容を REPLACE にも記載し、意図したセルだけを変更する。
4. `dryRun=true` でプレビューする。
5. 変換された操作、素材、支え、競合、変更セル数を確認する。
6. 状態が変わっていたら再度読み取る。変わっていなければ、新しい呼び出しで `dryRun=false confirm=true` を指定して実行する。
7. 同じ正確な矩形範囲を再度読み取る。

トークンの形式:

- 建築指示: `建筑名:优先级`。必要に応じて `#材料字` を付ける。例: `梯子:7#粉`。ここで指定するのは、建築名、優先度、素材の記号。
- 建築名だけのトークンは既存のオブジェクトを表し、建築指示ではない。
- 作業指示: `挖`、`拆`、`擦`、`扫`、`毒`、`杀`、`收`、`消`、`捕`。必要に応じて `:优先级` で優先度を付ける。
- マップレイヤーやインフラの Markdown では、接続記号の編集は拒否される。`auto_connect` が検証済みスナップショットの外側のセルを変更する可能性があるため。配線・配管を変更する場合は、明示的な `/active/infrastructure/*.oni` の計画か、`/active/ops/build.md` の `auto_connect` コマンドを使う。
- SEARCH の照合では、表示に付加される `@(x,y)` 座標サフィックスは無視される。`建筑:7#壹` は `建筑:7#壹@(114,138)` に一致する。
- 複数セルを占有する設備には、1つの REPLACE ブロックに幅×高さの占有範囲全体を記載する方法と、左下の基点セルを1つだけ指定する方法がある。一部だけの不完全な占有範囲は拒否される。
- SEARCH のワイルドカード `?` または `*` は1トークンに一致する。`/regex/` または `~regex` は正規表現で1トークンに一致する。REPLACE の `?`、`*`、`.*` は元のトークンを維持する。

占有範囲全体を記載する方法では、複数セルの設備の全体を1つの置換ブロックに含める。照合結果が一意になるよう、支えとなるセルと十分な周辺情報も含める。

既定の書き込み上限は変更セル数で512。`maxWriteCells`/`maxCells` で設定できるが、絶対上限は2500。`partial=true` の場合は再度読み取り、`remainingCells` に対する新しいパッチを作成する。

## 型付き操作ファイル

まず `/active/ops/tools.md` を読み、その後で選んだ操作ファイルを読む。各ファイルには、既定のツール、スキーマ、コメント付きの使用例が記載されている。

規則:

- 1回の編集で送信する実行可能コマンドは、必ず1つだけにする。
- `call tool=<name> key=value ...` を使う。型付きファイルでは `tool=` を省略できる。
- 空の SEARCH ブロックは新しいコマンドの追加を表す。
- 外側の `task` が引き継がれるため、操作内容を説明する値にする。
- 操作ファイル内から `world_editor` を再帰的に呼び出さない。
- 非公開の `coordinate_control` や `/active/ops/coordinate.md` を使わない。
- 生の座標を使えるのは、その型付きファイルで明示的に説明されている構文だけ。

`/active/ops/orders.md` でよく使う省略記法:

```text
挖 土@(83,146):7
擦 x1=90 y1=140 x2=94 y2=142 priority=6
扫 areaId=base_floor priority=6
拆 建筑@(90,141):7
捕 小动物@(101,130):7 dryRun=true
```

液体には `擦`（mop、モップ掛け）、落ちている資源や破片には `扫`（sweep、片付け）を使う。

## 管理・計画・ブループリントファイル

### 管理用 Markdown

表は読み取り専用のスナップショット。状態を変更するには、`## Edit Commands` の下にある1行のコマンドを、コメントを外した状態で使う。

| ファイル | 主なコマンド |
|---|---|
| `schedule.md` | `set_block`、`assign_dupe`、`create_schedule` |
| `priorities.md` | `priority`、`priority_settings` |
| `dupes.md` | `rename` |
| `food.md` | `food`、`food_policy` |
| `skills.md` | `learn_skill` |
| `research.md` | `research`、`clear_research` |

機械可読な状態が必要な場合にのみ `?format=json` を付ける。

### 計画ファイル

- `/active/buildings/plans.oni`: プレビューは `parse_plan`、確認済みの実行は `building_control planning build_area` に振り分けられる。
- `/active/infrastructure/*.oni`: 明示的な配線・配管計画は `auto_connect` に振り分けられる。
- `/active/ops/build.md`: 配線・配管の変更を型付き操作で表すほうが適している場合は、明示的な `auto_connect` コマンドを1つ送信する。

### ブループリント

`world_editor command=blueprint name=<name>` は、一覧表示、読み取り、作成、削除、使用に対応する。ブループリントの Markdown も同じパッチ仕様を使う。

## ワールドエディターのバッチ処理

`world_editor command=batch` は、ワールドエディター、ゲーム制御、ナビゲーション制御の引数オブジェクトを含む `steps`/`items` を、最大20件受け付ける。

- 変更を伴う可能性のあるステップは最大1つで、必ず最後に配置する。
- それより前のステップは読み取り専用でなければならない。
- `stopOnError` の既定値は true。
- ワールドエディターのバッチを入れ子にすることはできない。
- 外側の dry-run と確認に関する方針が引き継がれる。

最後の変更操作が、バッチの開始前から分かっている固定の引数だけに依存する場合に使用する。後のステップが、前のステップで動的に得られた結果を利用できるとは考えない。

## 使用例

### 画面外の正確な位置に梯子を建築するパッチ

まず前述の読み取り専用 zoom を実行し、実際に返されたヘッダーと行をコピーする。マーカーの字下げは意図的なもので、パーサーはこの形式を受け付ける。

```text
world_editor command=edit path=/active/map/viewport.md task="梯子を建築する" dryRun=true
content="""
  <<<<<<< SEARCH
百位X: 0 0 0 0 0
十位X: 8 8 8 8 8
个位X: 1 2 3 4 5
Y=42: 粉 粉 空 空 水
  =======
百位X: 0 0 0 0 0
十位X: 8 8 8 8 8
个位X: 1 2 3 4 5
Y=42: 粉 粉 梯子:7 空 水
  >>>>>>> REPLACE
"""
```

内容を確認した後、新しく生成した同じパッチを `dryRun=false confirm=true` で送信する。

### 新しい採掘コマンドの追加

```text
world_editor command=edit path=/active/ops/orders.md task="採掘して素材を確保する" dryRun=true
content="""
  <<<<<<< SEARCH
  =======
挖 土@(83,146):7
  >>>>>>> REPLACE
"""
```

### 複製人間の改名

```text
world_editor command=edit path=/active/management/dupes.md task="複製人間を改名する" dryRun=false confirm=true
content="""
  <<<<<<< SEARCH
# rename name="Dig" newName="採掘担当"
  =======
rename name="Dig" newName="採掘担当"
  >>>>>>> REPLACE
"""
```

この例の `採掘担当` は改名先のサンプル名。SEARCH の照合対象となる文字列は、読み取ったファイルの内容と一致させる。

## トラブルシューティング

| 症状 | 対応 |
|---|---|
| `SEARCH did not match` | 正確な元の状態を再度読み取り、パッチを生成し直す。古いテキストを再送しない |
| 明示的なヘッダーのエラー | 1回の読み取りで得た有効な X ヘッダー3行をすべて使う。表示範囲に対する相対指定で本当に一意に一致する場合に限り、ヘッダーをすべて取り除く |
| 行の特定が曖昧 | X ヘッダーと、より多くの Y 行の周辺情報を追加する |
| 座標のずれが誤っている | オフセットの計算をやめ、明示的なヘッダーと正確な元範囲の事前検証を使う |
| 圧縮トークンの不一致 | `format=edit compact=false` で再度読み取る |
| 複数セルのプレビューに失敗する | 1つのブロックに占有範囲全体と支えの周辺情報を含める |
| `partial=true` | 再度読み取り、残りのセルだけにパッチを適用する |
| カメラが意図せず移動する | `syncView=false focusCamera=false` で画面外の zoom を行う。編集のためだけにフォーカスしない |
| サンドボックスで研究が未解除と報告される | ロード中の DLL が古い。テストのために研究を予約しない |
