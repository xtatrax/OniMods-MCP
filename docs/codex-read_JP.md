# データベース（Codex）のキャッシュ済み本文取得

ゲームが読み込み済みの `CodexCache.entries/subEntries` を参照し、項目検索・分類一覧・既知のテキスト部品の本文を返す。メニュー／ロード中／キャッシュ未準備では初期化せずエラーを返す。ゲームのデータベースを一度開くと、ゲーム側が追加のサブ項目を生成する場合がある。

## 調査と修正

旧実装は `8aff82a` で削除され、入口はクラッシュ懸念により無効化されていた。削除前の `DatabaseTools.cs` には、キャッシュ未準備時の `CodexCacheInit()` 呼出し、検索候補全体の本文走査、不明なウィジェットの文字列プロパティ／フィールドを反射で読む処理があった。過去のクラッシュのスタックは確認できず、原因を一つに断定してはいない。

新実装はゲーム配布DLLの公開メンバーを確認し、既存キャッシュと既知の型だけを参照する。検索はメタデータのみ、本文取得は件数を絞った選択項目のみ。UIの `Configure`、アイコン生成、キャッシュ再初期化、不明なプロパティ取得は呼ばない。DLC制限と現在のセーブのアンロック状態をゲームの公開判定で確認する。

## 操作例

`world_editor action=search domain=database query="酸素" limit=3` でIDと表示名を検索する。IDの完全一致、リッチテキストのタグを除いた表示名／見出しの完全一致を、部分一致より優先する。同名候補はID・分類で区別する。「酸素」は設備分類 `BUILDCATEGORYOXYGEN` と元素 `OXYGEN` の両方が完全一致する。

本文は `read_control domain=database action=query id=OXYGEN`。公開操作ファイルでは `world_editor` へ次の引数を渡す。

```json
{
  "action": "edit",
  "path": "/active/ops/read.md",
  "content": "<<<<<<< SEARCH\n=======\ncall domain=database action=query id=OXYGEN limit=1 maxChars=4000 detail=true\n>>>>>>> REPLACE",
  "dryRun": false,
  "confirm": true,
  "task": "酸素のデータベース本文を読む"
}
```

操作ファイルのedit形式は呼出し方法であり、この `database` 読取は地形・セーブ・UIを操作しない。`detail=true` は取得結果を返すために必要。操作ファイル側には結果の文字数制限があるため、`limit` と `maxChars` を小さくして項目を分けて取得する。

```text
call domain=database action=categories detail=true
call domain=database action=list category=ELEMENTSGAS limit=5 detail=true
call domain=database action=query id=RESEARCHCENTER includeContent=true limit=1 maxChars=4000 detail=true
```

- `query/list/search/categories` に対応。`limit` は既定10・最大50、`maxChars` は1項目あたり既定6000・最大20000。
- `query` は本文あり、`list/search` は本文なしが既定。`world_editor` の検索は常にメタデータのみ。本文の全文検索はしない。
- `knowledge` はデータベース読取の互換経路。旧 `guide`・`mechanics` と `oni://guide/mechanics` は無効のまま。

## 返却値と取得範囲

`source`・`scope=loaded_codex_cache`、キャッシュ件数、分類、検索一致数、返却数、省略数を返す。各項目は `id/cacheKey/parentId/category/name/displayName/title/subtitle` を持つ。`name` はゲームのリッチテキストを保持し、`displayName` はタグを除いた名前。見出しが `STRINGS.*` の場合は `Strings.Get` で現在のゲーム言語へ解決する。MCPインターフェースの選択言語への独自再翻訳は行わない。

本文は `CodexText`、`CodexTextWithTooltip`、`CodexLabelWithIcon`、`CodexIndentedLabelWithIcon` の公開テキストを読み、改行・リンク・装飾タグを保持する。`CodexText.stringKey` は今回の本体DLLではテキストのデバッグ表現であり、ローカライズキーとして扱わない。

| フィールド | 意味 |
| --- | --- |
| `content.available` | コンテナ一覧を取得できたか。本文の完全取得を意味しない |
| `content.complete` | 対応部品の取得にロック・欠測・未対応部品・打切りがないか |
| `blocks` | 部品型と生の本文。対応する部品にはツールチップも含む |
| `unsupportedWidgets` | 読まなかった部品型と件数。未知のプロパティは実行しない |
| `lockedContainers` | アンロックされていない本文コンテナの数 |
| `missingContainers` | 本文リストを取得できないコンテナの数 |
| `dlcFilteredContainers` | 現在のセーブのDLC制限で除外したコンテナの数 |
| `truncated` | 文字数または部品数上限で打ち切ったか |

サブ項目自体がロックされていれば `available=false, reason=locked`、コンテナ一覧がnullなら `reason=content_missing`。取得済みの空文字／空配列は欠測と区別する。1項目の部品走査は最大10,000件。本文の数値は図鑑定義であり、現在の設備・資源・複製人間の実測値ではない。

変換・レシピ・相変化パネルは既知のフィールドだけを構造化して取得する。UIの再現ではなく、`dataFormat=raw_game_fields` の定義値である。温度はK、レシピ時間は秒。変換量の単位はタグ依存で、`continuous` も合わせて読む。`customFormating` や動的説明のコールバックは実行せず、存在を `issues` に報告する。相変化のSUBLIMATEでゲームが参照するPrefab側の代替定義は未取得として報告する。画像・動画は未対応のまま。

折り畳み見出しの本文も、入れ子のコンテナごとにDLC・ロックを確認して取得する。余白・区切り線は `layoutWidgetsSkipped` に数える。`unlockedContentComplete` は未解禁コンテナを取得失敗として数えない判定で、既存 `complete` はロックを含む保守的な判定を維持する。どちらも画像を含む画面そのものの再現を保証しない。パネル内の不足は `panelsWithIssues` と各ブロックの `issues` に報告する。

## 長い項目の分割取得

`read_control domain=database action=page id=OXYGEN pageSize=1000 offset=0` は、1項目の本文JSONを小さな文字列断片として返す。`text` を順番に連結し、`nextOffset=null` になったらJSONとして解析する。2ページ目以降は、最初の応答の `revision` と直前の `nextOffset` を渡す。内容が変化した場合はエラーになり、最初から取り直す。オフセットはUTF-16単位なので、クライアントで文字数から再計算せず返却値をそのまま使う。

```text
call domain=database action=page id=OXYGEN offset=0 pageSize=1000 detail=true
```

この経路は分類一覧を毎回付けず、操作ファイル中継の長さ制限を避ける。`pageSize` は最大2000。元の本文走査にも2,000,000文字・10,000部品・入れ子32段の上限があり、最終的な `content.truncated` を必ず確認する。メタデータの `list/search` も `offset` で続きを取得できる。ゲームキャッシュの初期化、UIのConfigure、アンロック操作は行わない。

## 遺物の調査と回収本文（2026-10-12）

`building_control domain=side_surface surface=facility kind=lore_bearer action=press id=... confirm=true` は通常の調査ボタンを押し、`newlyUnlockedLore` に今回の操作前後の解放ID差分を返す。`records[].databaseEntryId` はゲーム自身の `CodexCache.GetEntryForLock` による対応先、`database` は既存の本文リーダーの応答である。本文中の未解放コンテナ・DLC制限・文字数上限は従来どおり適用される。長文は対応先IDを指定して `database action=page` で再取得する。

古い `LoreBearer.content` は固定文字列キーを参照するため、現行のデータベース解放型の遺物では `MISSING.STRINGS.LORE...` になる。これを本文として返さず `content=null, contentStatus=legacy_text_unavailable_use_database` とする。有効な旧本文は保持する。`BeenSearched` は本文ではなく調査済み時の定型案内なので `alreadyInspectedMessage` に分離する（未調査物にも同じ定型文が入るため、調査状態の判定にも使わない）。

差分が空でも調査失敗とは限らない（既解放、解放候補の枯渇など）。調査の成否はボタン状態でも確認する。キャッシュ未準備・対応項目なし・読取エラーは明示する。読み取りのためにキャッシュ初期化、再調査、アンロックを実行しない。導入前の調査について、ランダムに回収された記録を個々のオブジェクトへ遡って関連付けることは保証しない。回収済み記録そのものは通常のデータベース読取で参照できる。
