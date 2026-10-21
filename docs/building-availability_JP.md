# 建設候補の availableNow

`availableNow` は、本体の設備定義 `BuildingDef.IsAvailable()` と研究条件 `TechItems.IsTechItemComplete(prefabId)` を合わせた読取です。建設メニューに表示されるか、セルへ配置できるか、素材があるか、工事が予約・完成するかを表すものではありません。既存の建設・予行の許可条件は、この表示値から変更しません。

## 値と小さな根拠

カタログ、明示 `prefabId` 候補、通常の文字列候補、順序付き候補は同じ reader を使います。明示 ID の候補で、値を設定せず既定の false を返していた経路も統一します。設備定義を取得できない順序付き候補は、true と仮定しません。

| フィールド | 意味 |
| --- | --- |
| `availableNow` | true: 定義と研究の両方が可。false: 少なくとも一方で不可を確認。null: 判断に必要な取得が不足 |
| `unlocked` | カタログ・既存の拒否詳細にある研究だけの独立した true / false / null |
| `availability.status` | `available` / `unavailable` / `unknown` |
| `availability.reasonCode` | 定義による不可、研究未解禁、取得不足などの機械 ID。可なら null |
| `availability.scope` | `definition_and_research` |
| `availability.researchSource` | `instant_build` / `sandbox` / `native_tech_items`。未取得なら null |

全体の値と研究だけの値は別です。たとえば定義が不可で研究管理を取得できなければ、`availableNow: false` と `unlocked: null` になります。未知を未解禁の false へ、初期値を取得済みへ補いません。JSON の null は省略せず返します。個々の研究や設備状態の大きな一覧は追加しません。

検索の `includeUnavailable: false` は、`availableNow == true` と確認できた定義だけを返します。true なら不可・未知も含めます。文字列候補の並びは従来の一致スコアを優先し、同点では true、false、null の順、その後設備 ID で揃えます。

## 本体の既存キャッシュと判断

新しい availability reader は、型と宣言元を確認した private static `Db._Instance` の現在値を読みます。`Db.Get()` による生成・初期化は行いません。通常モードでは既存の Techs・TechItems、それぞれの resources、Research.Instance の存在を確認した後、通常の建設 UI が使う本体の `TechItems.IsTechItemComplete` へ最終判定を委ねます。

本体の TechItem は、親技術が未完了でも POI で個別解除済みなら true を返せます。この例外も MCP で再実装せず、本体の判定へ委ねます。本体内部の `ParentTech` 読取は `Db.Get().Techs.Get(...)` を使いますが、MCP は既存の Db と必要キャッシュを先に確認し、この内部読取には既存の Db が使われる前提です。MCP が直接 `Db.Get()` を呼んだり、UI の更新を要求したりはしません。

本体では、研究管理が存在していても、その技術にまだ TechInstance が作られていない通常の未着手状態があり、未解除なら false を返します。この意図された false は取得済みの未解禁として保持します。MCP が TechInstance を生成したり、この状態を研究故障・取得失敗と見なしたりはしません。`Research.GetOrAdd`、研究完了処理、研究キューの更新は呼びません。本体の項目リストや順番を別の索引で置き換えることもありません。

通常の定義読取で例外があればその条件は未知です。Db キャッシュ、Techs・TechItems、resources、研究管理の欠測、研究判定の例外も未知です。非 null の空の native resources は、独自の不可条件にせず本体の判定へ渡します。既存の InstantBuildMode / SandboxModeActive による研究条件の省略は、その取得元を `researchSource` で区別します。これは新しい操作許可やサンドボックス権限を与えるものではありません。

## 読取と書込の境界

新しい reader と、候補・検索フィルタ・カタログの `autoMaterialReason` に利用する availability 観測は、既存キャッシュだけを読みます。カタログでは、この観測を旧来の素材・外観情報の取得より先に一度だけ捕捉し、後続の情報取得後に判定をやり直しません。未取得を、後続の初期化によって取得済みへ変えません。

カタログ全体の取得が一切初期化しない、という保証は今回の範囲ではありません。既存の `BuildingFacades` は permits・外観情報に `Db.Get()` などを使う旧処理を保持しています。この別の依存は未移行です。素材・外観の全 getter、本体 UI の描画・表示、すべてのキャッシュの純粋性を今回の修正から証明しません。

既存の `IsTechUnlocked` / `BuildAvailabilityError` と実行・予行のガード本体は維持します。旧ガードは `Techs.IsTechItemComplete` を使うため、POI の個別解除だけで読取が true となっても、従来のガードが書込を拒否する差は残り得ます。このガード移行や POI の操作は今回の範囲外です。拒否時の詳細に availability を付けても、既存の拒否を許可へ変えません。`autoMaterialReason` は捕捉済みの研究・定義の値を使い、未知のときは `building_availability_unknown` を返します。未知を `building_locked_by_research` と呼びません。

## ローカライズと検証範囲

変更した `includeUnavailable` の説明は、`building_availability.include_unavailable` を PO の `OniMcp.MCP.BUILDING_AVAILABILITY.INCLUDE_UNAVAILABLE` に対応させます。4 PO ソースと `building_availability.{en,ja,zh-Hans}.json` の実行時訳を併置します。暫定 `zh-hant.po` と既存 `zh-Hans` 実行時の対応は別に維持します。機械 ID・JSON キー・既存のゲーム名は翻訳しません。

実製品の候補生成・モデル・reader・カタログを連結したホスト試験は、型・null・フィルタ・並び・本体 API への委譲を代替境界上で確認するものです。実ゲームの設備定義、研究状態、メニュー描画や配置可能性の証明とは分けます。実機での現在のセーブ・通常再読込・公開版の受入結果は、実際に確認した範囲だけ追記します。

## 限定受入

実ソースを連結したホストは両版各541条件、ゲーム参照ビルドは両版とも警告・エラー0で通過した。開発側では旧readerや欠測をfalseにする変異版も、コンパイル後に誤判定を検出した。ホストは本体の実装を実行するものではなく、POI個別解除・bypass・欠測注入の受入はこの境界に限る。

開発版・公開固有IDのそれぞれで、未ロード時に研究管理欠測のnullと理由を返し、通常ロード後の2種類の入力で明示ID・カタログ・検索・件数上限を照合した。検査は開発355・公開311条件で、差の44条件は別設定による補給品案内の副本文検査であり、主結果は一致した。旧版で明示IDだけfalseだった設備は取得済みtrueと一致し、通常の研究未解禁falseも維持した。ロード後に取得できた研究判定の取得元はnative_tech_itemsで、保存名や試験用の呼称から有効なbypass経路を推測しない。各読取窓の停止時計を保持し、個別設定・原本・共有保存領域・他MOD・DLC・元選択先と元導入版の未ロードメニューへ復帰した。新規保存や研究変更、進行、建設操作はしていない。

この受入は、描画された建設メニュー・配置可能性・工事結果の一致、実機POI解除・有効bypass、全getterの非初期化を保証しない。旧Techs書込ガードとのPOI解除差、素材・外観の旧依存、全UI比較は残作業とする。個別の保存先・実ID・設定・生応答やログは一般文書に含めない。
