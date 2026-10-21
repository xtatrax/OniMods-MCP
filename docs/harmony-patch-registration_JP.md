# Harmony登録の単一化と限定監査

## 対象

`ModInfo.OnLoad` は設定、MCP言語、本体言語のキャッシュ、PLib、翻訳登録、POptionsの準備を終えてから、本体の `UserMod2.OnLoad(harmony)` を1回呼ぶ。本体の入口が `harmony.PatchAll(assembly)` を行うため、追加の明示 `PatchAll()` は行わない。

続く速度観測・入力保護・自動消毒ポリシーの `EnsureInstalled` の順序と、既存の登録ガードは保持する。ツール登録、main-thread bridge、再起動調整、HTTP serverの作成も従来どおり続ける。

既存のdialog observerによる重複配送の冪等化は残す。今回の単一登録で、過去の全観測原因、すべての本体操作経路、他MODの動作まで解決したとはしない。

## 読取窓口

```json
{"domain":"diagnostics","action":"status","includePatches":true}
```

`server_control` の上記指定で、通常のstatusに `patchRegistration` を追加する。既定と明示falseでは監査を呼ばず、監査結果も追加しない。`includePatches` はJSONの真偽値だけを受け付ける。既存の診断集約と `server_status` の互換入口にも同じ引数を定義する。

監査は要求された時点に `Harmony.GetAllPatchedMethods` と `Harmony.GetPatchInfo` を読み、Prefix・Postfix・Transpiler・Finalizerの登録から、PatchMethodがこのMCP Assemblyに属するものだけを集計する。ownerは抽出条件にしない。別ownerがこのAssemblyの同じhookを登録した場合も観測対象になる。他のAssemblyに属するパッチメソッドの内容やAssemblyの配置場所は返さない。ILRepackで同じDLLへ含めたPLibのパッチメソッドは対象に含むため、すべてのownerがMCPのIDになるとは限らない。

パッチを実行するfactoryになり得る `Patch.GetMethod`、再登録、登録解除、状態の再構築は呼ばない。通常の `Patch.PatchMethod` メタデータ取得を使う。Unityのゲーム状態やセーブ、UI選択、時計を取得する窓口は追加しない。

## 集計

グループのキーは、元のMethodBase・パッチ種別・PatchMethodのidentityである。ownerはキーに含めない。同じキーの登録が2件以上なら重複群とする。元メソッドが同じでも異なるPostfixは別の正当な群となる。例えば `Db.Initialize` のMCP側の言語更新とゲーム用サービス初期化を、単に同じ元メソッドだからという理由で重複扱いしない。

- `available`: 登録情報の取得と表示用メタデータの取得が完了したか。
- `source` / `scope`: 取得元と自AssemblyのPatchMethodに限る範囲。
- `totalEntries`: 対象の登録件数。
- `distinctGroups`: 上記キーの群数。
- `duplicateGroups`: countが2以上の群数。
- `owners`: 対象群で観測したowner IDの一覧。
- `rows`: 各群の `original`、`kind`、`patch`、`count`、`owners`。
- `returned` / `limit` / `truncated`: 返却した群数、固定上限128、表示を省略した群の有無。集計自体は全対象を走査する。
- `reasonCode`: 取得不能時の機械的な理由。

`original` と `patch` は `type`、`name`、`signature` で表す。signatureは `戻り型(引数型,...)`、ref/outは型末尾の `&`、配列は `[]`、genericは `<...>` とする。コンストラクターの戻り型は `System.Void` として表示する。

途中の列挙例外、欠けた登録情報・patch list・entry・PatchMethod・ownerなどで観測を完結できない場合は、`available:false` と理由を返す。件数・owners・rows・returned・truncatedはnullであり、0件や重複なしへ補完しない。正常に取得した空集合は0件として区別する。

これはメソッドごとの登録メタデータの読取で、全MODを停止した一括atomic snapshotではない。別の登録者が同時に変更する状態の継続安全性や、登録されるべき全hookの存在までは結果単体で証明しない。受入では、別に用意した期待inventoryと比較し、欠落・追加・重複を調べる。開発版と公開版の機能差により、絶対件数が一致するとは限らない。

## 検証状態

実製品ソースを連結したホスト252条件（bootstrap14、登録監査174、status経路64）が成功した。開発ソースの旧ModInfo全文と9意味変異はコンパイル後に動作試験で検出した。本体と同名のglobal TupleがあるためSystem.Tupleを明示し、参照ビルドで互換性を確認した。速度238・初回開始205・MOD不一致774・UI入力94の既存ホスト回帰も開発ソースで成功している。いずれもnative/Unity/PLib境界は代替で、本体の全パッチ適用を実行する試験ではない。

開発build.74の本体参照ビルドは警告・エラー0。メニュー26、停止ロード27、通常メニュー復帰24、通常再読込27の計104条件を実機受入した。MCP必須30群の完全一致と、固定PLibの条件付き候補に含まれる同梱1群を区別し、全4段階で同じ31登録・重複0を確認した。正当なDb.Initializeの異なるPostfix2本、速度5入口の観測成立、簡易/明示診断・不正型拒否・反復・停止時刻を照合した。ログのDb用初期化1行は補助証拠で、新規Exception行は0。全PLib候補の必須存在や共有registryの選択版を独立に証明したものではない。

新2キーは実機でカタログ値へ解決された。ただし起動時の引数説明は英語、要求時の型エラーは日本語として観測され、metadataの自動言語追従が統一済みとはしない。公開固有IDのbuild.20も参照ビルドは警告・エラー0、新ホスト252と既存速度238・MOD不一致774・UI入力94条件が成功した。公開版でもメニュー26、停止ロード27、通常メニュー復帰24、通常再読込27の計104検査を独立受入し、MCP必須27群＋同梱1群の28登録が全段階で一致、重複0だった。公開に含まれない初回開始ダイアログ分3群を期待へ混ぜず、共通27群は開発版とも一致した。初期化ログは1行、新規Exception行0。

元セーブを停止指定の通常ロードで選び直し、保存せず終了して元の導入版へ戻した。控えた導入物/設定25ファイル、原本/試験入力、共有保存領域241ファイル、非MCP MOD・DLC・元選択先を照合済み。開始時と同じ元版の未ロード応答と本体MainMenu.OnSpawn入口を確認した。prefs全体は書き戻さず通常SESSION_ID変化を保持し、最前面描画の視覚確認や全prefs一致を主張しない。全パッチ実動作、全PLib選出、他MOD全組合せ、継続的非変更の証明とは分ける。

ホストでは実OnLoadの呼出順、単一のbase登録、既存Ensure反復の追加登録なし、正当な複数Postfix、異ownerの同hook重複、他Assemblyの除外、取得失敗時のnull、128群上限、既定statusの監査非呼出を検証した。本体callback・Unity lifecycle全体や他MOD互換性をホストだけで証明しない。

実機ではメニューと停止したテストセーブのロード・通常メニュー復帰・再ロード後に期待inventoryと登録件数を照合する。既存初期化ログは補助証拠であり、ownerの存在だけを示す既存healthyログや、observerの冪等化による1件表示だけを重複登録なしの証明にしない。セーブ・設定の保持と配備DLLの一致は別に照合する。

## ローカライズ

新しい引数説明と真偽値エラーは `patch_registration.include_patches_description`、`patch_registration.invalid_include_patches` をPO4ファイルとruntime JSON3言語へ対応させる。msgctxtは `OniMcp.MCP.` にruntime keyの大文字表記を続ける。暫定 `zh-hant.po` の見本訳と既存 `zh-Hans` runtime訳は別々に保持する。登録ID・型名・機械キー・理由コードは翻訳しない。
