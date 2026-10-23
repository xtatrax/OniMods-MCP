# 電力サマリーの値の意味

電力サマリーは既存の値を保ち、最上位に一度だけ `valueSemantics` を返します。`summary`、回路行、設備詳細へ同じ説明を繰り返しません。新しい電力・燃料の測定値や判定は追加しません。

| メタデータ | 機械 ID | 対象 |
| --- | --- | --- |
| `netActiveWatts` | `generator_rating_minus_consumer_active_demand` | サマリーと回路行の `netActiveWatts` |
| `generatorScope` | `selected_components_generators_including_transformers` | 既存の発電機集計対象 |
| `generatorEmpty` | `native_generator_is_empty_count` | サマリーの `generatorEmpty` |
| `generatorDetailEmpty` | `native_generator_is_empty` | 発電機詳細の `empty` |
| `consumerUsedWatts` | `native_energy_consumer_watts_used_sum` | サマリーと回路行の `consumerUsedWatts` |

## 定格差と現在の消費

`netActiveWatts` は、対象発電機すべての `Generator.WattageRating` の和から、対象消費設備すべての `EnergyConsumer.WattsNeededWhenActive` の和を引いた既存の値です。発電機や消費設備の現在の稼働を条件にした差ではありません。正値から現在の充電余力、負値から現在の電力不足を断定できません。

native の `WattageRating` は設備定義の定格に既存の発電出力属性を加味します。単なる設備定義の固定値でも、実際に配分された電力の測定値でもありません。`WattsNeededWhenActive` は稼働時の要求なので、現在稼働していなくても正になる場合があります。`netRatedWatts` は消費側を `BaseWattageRating` とした従来の差のままです。

`consumerUsedWatts` は既存の `EnergyConsumer.WattsUsed` の合計です。この native component の値と、通常の電力分配で更新される回路側の消費キャッシュは別の観測です。電池の増減や一定時間のエネルギー移送を独立測定した値ではありません。

集計対象は従来どおり、ワールドで選別した `Components.Generators` と `Components.EnergyConsumers` です。`PowerTransformer` も発電機の registry へ登録されます。一方、本体の回路側では通常の発電機と変圧器を別に扱うため、このサマリーの定格合計を通常UIの回路発電・発電可能量と同一視できません。メタデータは、その型が現在の対象ワールドに存在するという報告ではありません。

## 空の発電機と燃料

`generatorEmpty` は native の `Generator.IsEmpty` が true だった件数、発電機詳細の `empty` は同じ native predicate の個体値です。燃料 Storage の質量、必要な燃料タグの不足、残り運転時間を調べた結果ではありません。消費設備やバッテリー詳細の既存 `empty` は、この意味メタデータの対象外です。

確認した本体の基底実装は、利用可能な内部エネルギーが0以下かを `IsEmpty` として返します。`IsEmpty` と `JoulesAvailable` は virtual なので、MOD由来の派生型を独自の式で再判定せず、その native getter の値を保ちます。`generatorStoredJ` と発電機詳細の `storedJ` は既存の `JoulesAvailable` に由来するJで、燃料のkgではありません。

## 保持する契約と検証範囲

旧数値、getter、ワールド選別、詳細の件数上限、回路集計、診断、欠測時の既存 fallback、丸めは変更しません。差の丸めは従来どおり集計後に行うため、表示済みの丸め値同士の減算と常に同じになるとは限りません。詳細の上限が集計対象全体を切り詰めるという意味でもありません。

この追加は固定メタデータと意味説明だけです。状態更新、電力分配、燃料の評価、UI更新や新しい native getter の呼出しは追加しません。読取値が得られたことは、ロード後の全キャッシュが再評価済みであることを意味しません。

開発版 `0.2.5+build.91` と公開版 `0.2.5+build.32` の初回ロード・通常再読込を停止中に受入れました。5項目の機械IDが結果の最上位に一度だけ現れること、既定の簡易取得、完全詳細、`limit=1`、既存resource、反復取得を照合しました。完全詳細の取得件数を先に確認したうえで、個体の native bool と全体・回路別件数、既存の差の式を照合しています。

同じ停止読取内の既存サマリー・回路行・詳細・診断を照合し、再読込時に採番が変わり得る回路IDは、全現物の所属を使って正規化しました。正規化対象は既知の回路行と設備行の `circuitId` / `circuitKey` だけで、二次診断の配線ID等を一括除外していません。6項目のゲーム時計と保護セーブの内容も変わっていません。

追加8行を除いた実装が変更前のソースとバイト単位で一致することを確認し、各版の実ソースを使った既存ホスト65条件も成功しました。このホスト検査は従来の集計・取得契約の回帰確認であり、新しい意味注記の正しさ自体を証明するものではありません。意味は、導入中の本体DLLから確認した native getter・registry・回路側の実装と照合しています。変更前DLLの実機生応答は本試験に保持されていないため、変更前後の実機結果の完全一致を根拠にはしていません。

試験後は開始時のゲーム未起動状態へ戻し、元の導入物・対象設定・原本・共有保存・保存先選択・他MOD・DLCを照合しました。MCPの対象設定値とDLCは変更せず、起動に伴うセッション番号の増加は区別しています。

停止中の値は、ロード後の native cache の観測です。燃料の有無と稼働の組合せ、変圧器の実物、実際の電力配分・充放電、通常UIの描画、全発電機型や全MOD由来 getter の意味は未検証条件として残ります。親の電力検証項目全体を完了したとは扱いません。
