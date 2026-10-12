---
name: oni-mcp-control
description: Oni MCP を通じ、観察・計画・実行・検証の厳格なループで Oxygen Not Included を操作する。稼働中のコロニーの状態確認、ゲーム内操作の計画・実行、仮想ワールドファイルの編集、複製人間やコロニー設定の管理、マップや配管・配線などの確認、MCP 操作エラーからの復旧に使用する。ゲームの助言だけを求められている場合には使用しない。
---

# ONI MCP 操作

## 参照資料の使い分け

操作タスクでは常にこのファイルを読み込んでおく。参照資料は、現在の操作に必要なものだけを読み込む。

- `world_editor` によるマップ編集、画面外の範囲指定、操作ファイルへのコマンド記述、管理項目の編集、計画、設計図、バッチ処理を行う**前に必ず** [references/world-editor_JP.md](references/world-editor_JP.md) を読む。仮想ファイルの正確なプロトコルと使用例を収録している。英語原文: [references/world-editor.md](references/world-editor.md)、中国語版: [references/world-editor.zh.md](references/world-editor.zh.md)
- 集約ツール、リソース、プロンプト、パラメーター、ツールの分類、標準的な複数ツールの操作手順を選ぶときは、[references/tool-reference_JP.md](references/tool-reference_JP.md) を読む。詳細な表とクイックリファレンスを収録している。英語原文: [references/tool-reference.md](references/tool-reference.md)、中国語版: [references/tool-reference.zh.md](references/tool-reference.zh.md)

タスクに必要でない限り、これらの資料を作業コンテキストに重複して読み込まない。

## 操作の基本原則

次の操作ループを最後まで実施する。

1. **観察**: 関連する現在の状態を、必要最小限の範囲で読み取る。
2. **状況整理**: 危険、到達可能性、依存関係、ユーザーが指定した範囲を把握する。
3. **判断**: 対象を限定した到達目標と、その検証方法を決める。
4. **実行**: 意味ベースの集約ツール、または内容を確認済みの仮想ファイル編集を使う。
5. **検証**: 対象を絞った独立の読み取りで結果を確認する。

古い前提に基づいて操作しない。書き込みの応答だけを根拠に成功を報告しない。

自律プレイでは、状態の読み取り、計画、コマンド発行の間はゲームを一時停止する。再開するのは時間を限定した短い区間だけとし、再び一時停止して検証する。ユーザーの直接の指示を視聴者の提案より優先し、ゲームプレイの安全性を速度より優先する。

## 操作範囲と許可

- 助言と操作を区別する。診断のための読み取り専用の調査は可能だが、ユーザーがゲーム内操作を許可していない限り、セーブデータを変更しない。
- マークされた境界、確保済みの区域、「視点を動かさない」「コードを書かない」という指示を、そのまま厳守する。
- 通常のプレイでは、サンドボックス・デバッグ機能による生成や、チートによる資源の追加を行わない。
- 製造ポッドの報酬は原則として物資を選ぶ。ユーザーが明示的に求めない限り、新しい複製人間を選ばない。
- 液体に隣接する採掘、閉じ込められた複製人間への対応、広い矩形範囲の操作、取り消せない編集では、実行前に一時停止して対象セルを正確に確認する。
- ライブ配信中に配信や OBS に障害が発生した場合は、ゲームを一時停止し、配信を復旧してから続行する。

## 公開ツールの利用方針

コンパクトな集約インターフェースを使う。

- `colony_control`: コロニーのスナップショット、診断、管理、研究、スケジュール。
- `dupes_control`: 複製人間の詳細、優先度、スキル、装備、指示。
- `read_control`: 資源、設備、インフラ、ワールドマップ、再利用可能な区域。
- `building_control`: 計画、素材、設定、収納、自動化、特殊設備。
- `orders_control`: 採掘、モップ掛け、片付け、消毒、キャンセル、収穫、解体、捕獲、配管・配線などの切断。
- `game_control`: 一時停止、速度、セーブ、DLC、状態、対応しているサンドボックス操作。
- `navigation_control`: カメラ、レイヤー表示、スクリーンショットのみ。
- `search_control`: 意味ベースのワールド・オブジェクト検索と、探索結果に基づく実行。
- `server_control`: 機能の探索、診断、バッチ処理。
- `world_editor`: 仮想ファイルの読み取りと編集。

`coordinate_control` は公開されていない。通常の集約ツールは座標の直接指定を受け付けない。意味ベースの `areaId`・クエリ・計画を入力するか、対応している `world_editor` のマップファイルや型付き操作ファイル内で正確な座標を指定する。

配管・配線などを切断するときは、次のように対象レイヤーを正確に指定する。

`orders_control domain=designation action=cut_conduits type=wire|liquid|gas|solid|logic|travel_tube ... confirm=true`

通常の設備の解体には、原則として `orders_control domain=designation action=deconstruct id=<instanceId> confirm=true` を使う。1つのセルに複数の候補がある場合は、先に一覧取得またはドライランを行う。

## モードの選択

| ユーザーの意図 | 最初に行うこと | 返す結果 |
|---|---|---|
| 問題を診断する | 最小限のスナップショットと、対象を絞った診断読み取り1件 | 優先順位を付けた問題と根拠 |
| 次に何をすべきか尋ねる | カタログのガイドと、範囲を限定した現状の読み取り | 検証方法を含む短い計画 |
| 許可済みの操作を実行する | ドライランまたは事前検証を行い、その後に実行を確定する | 適用結果と検証結果 |
| 監視する | 監視項目を絞った最小限の差分スナップショット | 状態の変化のみ |

実行に失敗した場合は診断に戻る。効果のなかった同じ呼び出しを繰り返さない。

## 状態確認の原則

最初はコンパクトな読み取りを使う。

```text
colony_control domain=snapshot action=get profile=minimal delta=true watch=stress,food_kcal,red_alert,alerts
```

問題が示された分野だけを詳しく調べる。

- 食料: `read_control domain=resources action=food`
- 電力: `read_control domain=infrastructure action=power_summary`
- 部屋: `read_control domain=infrastructure action=rooms`
- 熱: `read_control domain=world action=thermal_overheat_risk`
- 複製人間の健康・経路探索: `dupes_control domain=info action=status_check`
- 正確な地形や配管・配線など: 仮想マップ、または `read_control domain=world action=area_snapshot|text_map`

同一の引数で同じ読み取りを2回連続して呼び出さない。不足している情報を明らかにし、より対象を絞った読み取りや機能探索の呼び出しへ切り替える。

## 必須のマップ記号照会

マップ記号を解釈する前、設備・元素・エンティティ名から記号を求める前、接続やレイヤーの記号を読み取る前に、このスキルでは正式な記号照会が必要であることを `commentary` で伝える。その後、次を呼び出す。

```text
search_control domain=glyphs queries=["<all unknown glyphs or names>"] direction=auto
```

- 現在のマップに含まれる不明な記号・名前を1回の呼び出しにまとめる。セルごとに照会しない。
- 漢字の見た目、古い凡例、記憶から推測しない。結果が `count=0` なら、不明のままとして扱う。
- 文脈によって意味が変わるレイヤー記号は、`view=<overlay>` で区別する。
- 同じランタイムかつ同じターン内で照会済みの結果は再利用する。

例（照会対象の記号・名前は原文のまま）:

```text
search_control domain=glyphs queries=["砖","?","┼"] direction=code_to_meaning matchMode=auto view=logic
search_control domain=glyphs queries=["砖块","氧气","研究站"] direction=meaning_to_code matchMode=auto
search_control domain=glyphs queries=["零","■","晒","美","微","低","枯"] direction=auto perQueryLimit=20
```

## 空間情報とカメラの扱い

- 正確な判断には、意味ベースの検索、仮想ファイルのマップ、`area_snapshot`、`text_map` を優先する。
- `navigation_control` は、ユーザーが特定の場所を見るよう求めた場合、またはレイヤー表示・スクリーンショットが判断に実質的に役立つ場合にのみ使う。
- 画面外の矩形範囲を編集するためだけに、カメラを移動したり対象に合わせたりしない。`references/world-editor_JP.md` にある、カメラを動かさない範囲指定の手順を使う。
- 意図的にカメラを移動した後、正確な確認が重要な場合は `navigation_control action=get_view` を呼び出す。
- レイヤー表示の撮影には `navigation_control action=switch_view screenshot=true` を使う。通常表示のスクリーンショットには `view=none` を使う。
- スクリーンショットは非同期で生成される。HTTP URL から取得する前に `readyAfterFrames` を守る。後の判断でセルを参照する場合は `coordinate_screenshot` を優先する。
- 採掘、建築、配管、配線、解体の座標は、スクリーンショットではなく構造化マップを正とする。

## 建築と作業指示の原則

建築では次の手順を使う。

1. `building_control domain=planning action=search_defs` でプレハブを特定する。
2. `building_control domain=planning action=materials` で使用可能な素材を確認する。
3. `placement_candidates` またはマップ編集のドライランで、支持、障害物、配置条件を確認する。
4. 意味ベースの `areaId`・計画による配置、または `world_editor` の正確なマップトークンを使う。
5. 対象区域を再読み取りする。

採掘、片付け、モップ掛け、消毒、キャンセル、収穫、捕獲、解体、配管・配線などの切断には、`orders_control` または `/active/ops/orders.md` を使う。UI クリックの模擬操作は行わない。

片付けは落ちている資材などの拾える物だけに使う。液体セルやこぼれた液体にはモップ掛けを使う。

## サンドボックスと即時建築の挙動

`world_editor` の各呼び出しは、グローバルのデバッグ即時建築が有効でも、既定では `instantBuild=false` で動作する。この既定動作では通常の建築予定を作成し、通常の素材を使用する。即時建築を暗黙に要求しない。

- 完成済み設備のパラメーターは `/active/buildings/index.md` からたどり、安定したパス `/active/buildings/instances/<prefab>-<InstanceID>.md` を開いて読み取る。編集するのは、そのファイルの正規形式の行だけにする。
- `instantBuild=true` には `allowSandbox=true confirm=true` が必要で、その呼び出しにだけ適用される。
- サンドボックスでの書き込みには `world_editor command=sandbox allowSandbox=true confirm=true` と、必要な権限だけを指定する。権限は `allowTerrainMutation`、`allowEntitySpawn`、`allowDestroy`、`allowForce`。
- `sandboxMaxCells` は小さく保つ。既定値は 100、上限は 1000。
- バッチ内の子呼び出しで、これらの権限を拡張しない。

## 実行前の条件

- 危険を伴う編集や精密な編集は、先に `dryRun=true` とし、実行確認なしでプレビューする。
- 操作に変換された計画を確認した後にのみ、`dryRun=false confirm=true` で実行する。
- `server_control domain=batch action=call_many` は最大20件に対応する。相互に関連する書き込みには `requireAllValid=true` と `stopOnError=true` を使う。
- 後続の引数に先行結果が必要な、依存関係のある読み取りを同じバッチにまとめない。
- 応答をコンパクトに保つ。通常は `responseMode=summary`、再試行ループでは `errors`、子呼び出しの詳細が必要な場合だけ `full` を使う。
- リクエストが成功しても、一部しか適用されていない場合がある。`changedCells`、`applied`、`failed`、`partial`、`remainingCells`、各項目の結果を確認する。

## 検証パターン

実行前に検証方法を決める。

| 操作 | 検証方法 |
|---|---|
| マップ上の建築・採掘・作業指示 | 対象の矩形範囲、または対応するレイヤーを正確に再読み取りする |
| 電力の変更 | `power_summary` を確認し、切断があれば該当箇所の `power_ports` も確認する |
| スケジュールや方針 | 同じ管理対象の一覧を再取得する |
| 複製人間の更新 | 対象の複製人間、または優先度・スキル一覧を再読み取りする |
| 設備の設定 | その設備 ID の設定一覧を再取得する |
| カメラ・レイヤー表示 | `get_view`、または `readyAfterFrames` 経過後のスクリーンショットで確認する |

作業指示や建築予定が存在するだけでなく、実際の効果を検証する。ただし、ユーザーが指示の登録自体をテスト対象として明示した場合は除く。

## エラーからの復旧

### スキーマやツールの不一致

1. エラーの内容を正確に読む。
2. 現在のカタログを検索する: `server_control domain=catalog action=search query=<intent> detail=brief`。
3. 返されたスキーマに従う。似たツール名を推測して作らない。

### 効果のない書き込みや、古い状態に基づく書き込み

1. 同じ操作を繰り返さない。
2. 対象の状態を最小限の範囲で再読み取りする。
3. 変更された ID、座標、到達可能性、素材、支持条件を確認し直す。
4. 修正した内容でドライランを行う。

### マップの事前検証の不一致

再読み取りしてパッチを作り直す。矩形範囲を明示する場合は、`references/world-editor_JP.md` の3つのヘッダーを使うプロトコルに従う。表示範囲からのオフセットを推測して代用しない。

### タイムアウト

タスク ID が返された場合は、そのタスクの状態を確認する。それ以外の場合は、区域、件数上限、応答の詳細度を絞ってから再試行する。

### 確認不足による拒否

ユーザーが変更を許可し、ドライランの結果が妥当である場合にのみ `confirm=true` を追加する。

## リスク確認リスト

中程度以上のリスクがある操作の前に、次を確認する。

- [ ] 現在の対象状態を読み取った。
- [ ] ユーザーの許可が今回の変更を含んでいる。
- [ ] 正確な ID、ワールド、矩形範囲、支持、配管・配線などのレイヤーが正しい。
- [ ] 広範囲または危険を伴う作業のドライランを行った。
- [ ] `confirm=true` は実行の呼び出しにだけ付いている。
- [ ] 対象を絞った検証用の読み取りを用意した。
- [ ] 複雑な空間操作の間はゲームが一時停止している。

## 効率化の原則

- 読み込み中のランタイムのカタログ結果をキャッシュする。ただし、再読み込み後は以前の前提を無効にする。
- 書き込み・実行のたびに、関連する状態のキャッシュを無効にする。
- 同じ範囲の読み取りや操作には、範囲を限定した `areaId` ハンドルを再利用する。一時的なカスタム区域は使用後に登録を解除する。
- 広範囲の読み取りを多数組み合わせる代わりに、最小限のスナップショットを1回取得する。
- 独立した読み取りはバッチ化し、依存関係のある呼び出しは順番に実行する。
- 詳細をコンパクトにし、矩形範囲を絞り、RLE は読み取り専用の走査にだけ使ってトークン消費を抑える。パッチには展開済みの編集形式を使う。
