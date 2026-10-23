# 昇華パネルのPrefab代替定義を読み取る

既存のデータベース本文読取で、選択した `CodexTemperatureTransitionPanel` の種別が `SUBLIMATE` の場合だけ、`prefabSublimatesFallback` を添える。新しいAPIやゲーム操作は追加しない。元素定義の生フィールド、カテゴリの図鑑リンク、通常本文と長文ページングは従来の経路を保つ。

## 代替定義が必要になる条件

本体と同じ順序で、まず既存の `ElementLoader` キャッシュから `sourceElement.sublimateId` の遷移先を調べる。正常に元素が見つかった場合、Prefabは参照せず、次の短い結果だけを返す。

```json
{"required":false,"status":"not_required","source":"Element.sublimateId"}
```

正常な照会が `null` を返した場合だけ、Prefab側の代替定義が必要になる。元素のIDが特定値かどうかや、タグの有無から必要性を推測しない。キャッシュ未準備・例外・元の元素の欠測は `required=null` とし、代替経路へ進めない。`OFFGASS`・`HEAT`・`COOL` などにはこの項目を追加しない。

## 返却項目と欠測

代替が必要、または必要性を確認できない場合の項目は次のとおり。

| 項目 | 意味 |
| --- | --- |
| `required` | 正常な直接照会で遷移先がなければ `true`。元の元素・キャッシュ・直接照会を確認できなければ `null` |
| `available` | `Sublimates.Info` の `sublimationRate` と `massPower` の両方を、有限な値として観測できたか。遷移先や図鑑情報の可読性は含めない |
| `status` | 読み取れた段階、未準備、未解決、対象の制限、数値の不正などを示す状態ID |
| `source` | 代替が必要と確定した場合は `Assets.TryGetPrefab/Sublimates.info`。必要性が不明なら `null` |
| `values` | Info未取得なら `null`。取得後は `sublimationRate` と `massPower` を個別に保持する |
| `semantics` | 数値の意味を示す安定したID。Info取得後に返す |
| `targetResolved` | Infoの `sublimatedElement` を本体の元素キャッシュで解決できたか。正常に元素があれば `true`、正常に対象がなければ `false`、未実行・例外なら `null` |
| `target` | 読取可能な既存図鑑へ対応した場合だけ `{entryId,name}`。対応不明・制限対象・未解決なら `null` |

`available=true` でも、遷移先の照会が失敗したり、図鑑の情報を読めなかったりする場合がある。遷移先の失敗で、先に観測できた有限なInfo値を消さない。`targetResolved=true` も、DLC・無効・ロックなどの条件を満たす図鑑情報が読めることまでは意味しない。図鑑側のIDと名前は既存の可読性・正規化別名の境界を守り、生Prefabや未解決対象のID・名前を補わない。

Info内の既知の0はそのまま0を返す。NaNや無限値は該当する項目だけを `null` にし、他方が有限ならその値を保つ。この場合は `available=false`、`status=invalid_numeric` となる。有限の生定義値を、MOD側の予測値や補正値へ置き換えない。

## 状態の読み方

| `status` | 状態 |
| --- | --- |
| `not_required` | 直接の元素定義で遷移先を解決したため、Prefab未参照 |
| `source_unavailable` / `cache_unavailable` | 元の元素または必要なキャッシュが未取得 |
| `direct_lookup_unavailable` | 直接照会を確認できず、代替の必要性も不明 |
| `prefab_unavailable` / `component_unavailable` | 代替が必要だが、既存Prefabまたはコンポーネントが見つからない |
| `prefab_lookup_unavailable` | Prefab／コンポーネントの読取に失敗 |
| `invalid_numeric` | Infoの数値に非有限値がある。有限側は保持 |
| `target_lookup_unavailable` | Infoは取得済みだが、遷移先照会に失敗 |
| `target_unresolved` | 遷移先を正常に照会したが、元素が見つからない |
| `target_filtered` | 元素は解決したが、図鑑情報は制限対象 |
| `target_metadata_unavailable` | 元素は解決したが、可読な図鑑情報へ対応できない |
| `resolved` | 有限なInfo値と、読取可能な図鑑の遷移先を取得 |
| `data_limit` | パネルの共有ノード予算が不足し、追加照会を行わない |

不足は各ブロックの `issues` にも状態に応じた `prefab_sublimates_*`、または `data_limit` として示す。本文の `panelsWithIssues`・`complete` は従来の保守的な判定を保つ。

## 数値の意味と本体の画面表示

`values.sublimationRate` の意味IDは `native_mass_dependent_rate_coefficient`、`values.massPower` は `mass_exponent`。

調査した本体の実処理では、`massPower` を現在質量のべき乗の指数として使い、`sublimationRate` と組み合わせた値へ時間幅や現在質量、圧力・温度・保管状態などの条件が影響する。指数をkgや変換効率として扱わず、この係数を任意の状態で一定のkg/sを表す実流量として扱わない。ここでは本体の式を実行せず、予測流量も作らない。

本体の画面構築側には、代替定義の `massPower` を質量表示の整形へ渡す箇所がある。この返却はその画面文字列を再現するものではなく、元のフィールド名と意味を分けた定義観測である。`renderedUiComplete=false` を保ち、画面の整形値と実処理上の意味が同じとは保証しない。

## 受動読取と予算

既存Prefabの取得には警告を発生させる `GetPrefab` を使わず、キャッシュを読む `TryGetPrefab` を使う。タグは本体と同じ元素ID文字列から純粋な `Tag` 値を作り、`CreateTag`・`TagManager.Create` による名前登録を行わない。データベースや元素表の初期化、Prefab生成、UIの `Configure`、命名コールバック、特殊フォーマッター、昇華のシミュレーションは呼び出さない。

追加投影は既存パネルの共有ノード予算を使う。予算が足りない場合は照会前に不足を示す。この予算は読み取る定義と投影出力の範囲を抑えるもので、本体処理全体のメモリー割当量や処理時間を計測・保証するものではない。

長文では従来の `action=page` と `revision/nextOffset` を使う。[本文読取](codex-read_JP.md)と[カテゴリの図鑑リンク](codex-category-links_JP.md)の契約も参照する。

## 検証の区分と残る範囲

開発版・公開版それぞれで、実ソースをリンクしたホスト回帰と、本体DLLを参照するビルドは成功した。カテゴリ対応済みの旧パネル読取ソースの全文を使う負例は、既存カテゴリ回帰を通過し、コンパイル成功後に新しい代替定義の返却検査で失敗した。直接解決時のPrefab未参照、各欠測・例外、制限対象の非公開、有限値と非有限値、既知0、予算、通常本文とページングの保持はホストで確認した範囲である。

開発版・公開版それぞれの停止した実機では、Prefabの代替が必要な昇華パネルから有限な係数・指数と、読取可能な図鑑の遷移先を取得し、図鑑メタデータとの一致を確認した。直接定義で解決する対照は短い `not_required`、`OFFGASS` の対照には新しい項目が付かないことも確認した。代替対象の反復読取とページ連結後も、生の元素フィールドと代替定義が一致し、ゲーム内時刻・速度・停止状態・対象セーブは前後で変わらなかった。進行・保存・UI構築・初期化は要求していない。

いずれの代表本文も未対応の `CodexImage` が残って `complete=false` であり、`OFFGASS` の通常本文照会には文字数による打切りもあった。この照会を、その本文全体や描画UIの完全取得成功とは扱わない。自然な昇華・実流量、実機のゼロや負値、非有限値・欠測・ロックなどの異常条件を発生させる試験は行っておらず、それらはホストで検証した範囲として分ける。

各版の結果は、それぞれの配備物による独立した読取窓で確認したもので、開発版の成功を公開版の実行証拠として代用していない。

今回の対象はSUBLIMATEの代替定義であり、描画UI・画像・動画・特殊フォーマッター、実流量や全元素・全DLC・全MODの網羅取得を完了したものではない。動物の餌・消費・産出・照度・放射線の取得元と単位などは、引き続き別の確認が必要である。

## 試験後の復帰

両版の検証後、元セーブを停止状態で選び直し、通常終了を経て元の導入物とMOD設定へ復帰した。記録した導入物・設定、両方の保護セーブ、主保存先・共有保存先・既存画像、本体DLLと非対象MODの保持を照合し、開始時と同じ未起動状態を確認した。本体が通常ロード・終了で更新する選択先や採番値は、その保存結果を照合し、設定ファイルへ直接書き戻していない。
