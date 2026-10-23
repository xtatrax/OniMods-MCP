# 元素カテゴリから読取可能な図鑑リンクを取得する

既存のデータベース本文読取で `CodexElementCategoryList` を選択したとき、そのパネルへ `categoryMembers` を追加する。取得範囲は、読み込み済みのタグ別Assetから、現在読取可能な図鑑IDと名前へ対応付けられたリンクの投影である。新しいAPIや操作は追加しない。検索で対象の分類IDを確認し、既存の本文取得または `action=page` を使う。本文を含めない検索・一覧ではAssetのメンバーを走査しない。

各 `members[]` は次の2項目だけを持つ。

| 項目 | 内容 |
| --- | --- |
| `entryId` | 読取可能な既存図鑑のID |
| `name` | 図鑑キャッシュのゲーム言語による名前。リッチテキストを保持する |

生のPrefab ID・タグ・Asset名・現物ID・座標はメンバー行へ追加しない。従来の `data.categoryTag` は選択したカテゴリの定義値として保持する。同じ図鑑IDへ対応する参照は一行へまとめる。

## 取得元と対応付け

`source` は `Assets.GetPrefabsWithTag/CodexCache.FormatLinkID/entries/subEntries`、`scope` は `normalized_cached_codex_links_with_readable_metadata`、`identitySemantics` は `codex_link_projection_not_raw_prefab_identity`。

本体のタグ別Assetキャッシュを読み、既存図鑑キャッシュへ一意に対応する場合だけ名前とIDを返す。大文字・小文字の差は許容するが、アンダースコアの除去などで情報を失う正規化を必要とする別名は未解決とする。図鑑のIDとキーが一致しない場合や、サブ項目の親を確認できない場合も推測で補わない。

DLC・無効項目・親の制限・サブ項目のアンロック状態を守り、除外または未解決となった対象の識別子や名前を返さない。名前は図鑑キャッシュから読み、Prefabの命名コールバックを実行しない。

`categoryMembers` はこの限定投影の容器名である。生のPrefabとの完全な同一性、本体UIの全行、その画面の描画状態を保証しない。`renderedUiComplete=false` を保持する。

## 欠測・空・打切り

| 項目 | 意味 |
| --- | --- |
| `available` | カテゴリに対するAssetキャッシュのリストを取得できたか |
| `members` | 取得した図鑑IDと名前の配列。取得不能なら `null` |
| `returned` | 重複を除いた返却リンク数。取得不能なら `null` |
| `matched` | 走査を終え、未解決がない場合の既知リンク数。未取得・未解決・打切りでは `null` |
| `filteredMembers` | DLC・無効・ロック・親の条件で伏せた参照数。対象のIDや名前を含めない |
| `unresolvedMembers` | 別名・不整合・欠測などで対応付けを確定できなかった参照数 |
| `truncated` | キャッシュ複製後の走査・出力予算で打ち切ったか |
| `complete` | この投影に除外・未解決・打切りがなかったか |

取得不能では件数と配列を `null` のまま保つ。正当に取得した空のリストは `available=true`、`members=[]`、`returned=matched=0` になる。返却0件でも `unresolvedMembers` や `truncated` がある場合、カテゴリに対応するものが存在しないとは判断しない。

`returned/matched` は重複を除いた図鑑リンク数、`filteredMembers/unresolvedMembers` は走査した参照の集計であり、同じ数え方ではない。生Prefabの総数として扱わない。

不足は `category_members_unavailable`、`category_members_filtered`、`category_members_unresolved`、`data_limit` で示す。既存の `panelsWithIssues`、`complete`、`unlockedContentComplete` はこれらを保守的に扱う。本文・折り畳み・DLC・ロックと長文のリビジョン確認は既存経路を保つ。

## 予算の範囲とページ取得

`postCopyMetadataWalkBounded=true` は、Assetキャッシュを複製した後の走査と投影出力に、パネルのノード予算を適用することを示す。本体の `Assets.GetPrefabsWithTag` は、その前に該当するキャッシュリスト全体を複製するため、`nativeCacheCopyBounded=false` を返す。

各ページは対象本文のJSONを作り直す。条件を満たすカテゴリではそのキャッシュ複製も繰り返すため、`pageRegeneratesNativeCacheCopy=true` とする。これらは経路の性質を示す値で、実行回数・総割当量・処理時間の計測値や固定上限ではない。文字数制限とノード予算が、コピーを含む総作業量・総メモリーを制限するとは扱わない。

長文は既存の `action=page` と返却された `revision/nextOffset` を使う。[本文取得とページ取得の契約](codex-read_JP.md)も参照する。

## 読取境界と残る範囲

読み込み済みのキャッシュと既知フィールドだけを参照する。データベースやAssetを初期化せず、UIの `Configure`、Prefabの命名、特殊フォーマッターや未知のゲッターを実行して値を作らない。未準備の場合は不足を示す。

今回の範囲に、描画UI・画像・動画の取得、生Prefabの完全な同一性や全UIメンバーとの対応は含まれない。未実行の特殊フォーマッター、SUBLIMATEのPrefab代替定義、動物の餌・消費・産出の単位、照度・放射線の取得元は引き続き個別確認が必要で、未取得を0や機能なしへ補わない。

## 限定した検証結果

開発版・公開版それぞれで、実ソースをリンクした既存・追加のホスト回帰と、本体参照ビルドが成功した。旧パネル読取ソースの全文を差し替えた負例は、警告・エラーなしでコンパイルした後、新しいカテゴリ返却契約の検査で失敗した。別名の非公開、DLC・無効・ロック、既知の空と取得不能、ノード予算の境界はホストで検証した範囲であり、実機ですべて再現したものではない。

実機は、各版自身のDLLと識別子を確認して別々に実施した。停止中の一カテゴリについて、返却された図鑑ID・名前と現在読取可能なメタデータの一致、同一照会の再読、既存のページ境界から本文を連結して同じ投影を復元できること、通常本文の返却と未一致IDの空結果を確認した。未解決参照は集計だけを保持し、`matched=null` と `complete=false` を保った。未解決の対象や理由は推測で名前付けしない。

両版とも読取前後の時計6項目・停止状態・対象セーブが一致した。読取では時間進行・保存・図鑑パネルの構築・図鑑初期化を要求していない。未対応の画像部品と通常本文の未取得部分は残り、本文全体も完全取得とは扱わない。描画UI、生Prefabとの完全な対応や全メンバーの取得成功へ一般化しない。試験後は元の選択先と導入物・設定へ復帰し、セーブ等の保全とゲームの停止を照合した。
