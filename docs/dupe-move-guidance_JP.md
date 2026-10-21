# 複製人間の移動案内と公開操作ファイル

`oni://dupes/direct-commands` の既存一覧は、移動の案内をトップの `moveGuidance` に一度だけ返します。各人の `recommendedLookup.move` はその位置への短い参照です。`tools` と既存の各人欄の型を保ち、旧移動ツール名を推奨先から外します。旧APIの再登録・alias復活、新しい公開toolは追加しません。

## 公開入口とテンプレート

公開入口は `world_editor` です。`dupes_control` と `coordinate_control` は内部operationであり、その名前を直接public tools/callへ渡す案内ではありません。

- `tool=world_editor`、`readCommand=read`、選んだ `path` で既存操作ファイルを先に読みます。
- `editCommand=edit`、同じ `path`、`contentTemplate` の `<commandLine>` を埋めた `content` を使います。1回のeditにつき1操作を保ち、外側要求に `task` を指定します。
- `single` は `/active/ops/dupes.md` の `dupes_control domain=command action=move_to` です。現在の複製人間ID・対象query・worldIdを埋め、`<targetQueryToken>` は既存opsがそのまま扱える通常scalarまたは単純な引用文字列だけに限定します。JSON escapeは復号されず、引用符・backslash・制御文字のescapeが必要な名前や、数値/真偽値だけの名前の往復は保証しません。特殊な名前は座標batch等の別経路を使い、parserの意味を案内側で作り替えません。地図上の位置付き表示tokenが名前検索へそのまま渡せるとは扱いません。
- `batch` は `/active/ops/coordinate.md` の `coordinate_control` から `targetTool=dupes_control` へ委譲します。`<itemsJson>` は現在の複製人間IDと明示座標・worldIdを持つJSON配列です。既存parserはobject payloadを文字列として保持し、coordinate入口がJSONへ戻します。この既存契約は変更しません。

単一のquery検索と一括の座標指定を混同しません。batch handlerはquery検索ではなく各itemのx/yまたは既存の最上位既定座標を使います。通常のdupes raw operationへ座標を直接入れると既存capability guardが拒否するため、batchの案内をその経路へ短絡しません。外側のdupes metadataを拡張したものでもありません。

## 確認・予行・結果

`preview` と `execute` のflagsは、外側の `world_editor` 要求へ適用します。

- 予行は外側 `dryRun=true`・`confirm=false`。操作行の構文とcapabilityを確認してdispatch前に止まり、対象queryの解決・到達性・native移動の適格性は評価しません。
- 実行は外側 `dryRun=false`・`confirm=true` に加え、行にも `confirm=true`。batchではpayload内の `confirm=true` も揃えます。外側/子の未確認や予行を、案内が勝手に実行へ変えるものではありません。
- MoveDupe/MoveDupesBatchそのものはdryRun対応ではありません。行やcoordinate wrapperだけのdryRunを、移動しない予行として勧めません。必ず外側のflagsで停止します。

案内を読み取るだけで移動指示は発行しません。既存の `canMoveTo` はNavigatorと既存MoveToLocationMonitor SMIの存在だけで、本体UIのMoveボタン表示や実行許可、目的地の到達性・無障害を保証する値ではありません。実際の移動処理・同意・world/座標検証は変更せず、指示の受理と到着を区別します。

## 翻訳と受入の境界

関連する一覧metadataと追加説明は `dupe_move_guidance.*` のPO4とruntime3へ揃えます。tool/action/JSON key/virtual path/DSL/template placeholderは機械契約のままです。他のskill・assignable・schedule等の案内を一括で修正するものではありません。

実sourceによるhost回帰と、両版の初回・通常再読込で案内の限定受入を行いました。既存resourceの案内から公開操作ファイルを読み、返されたsingle/batchテンプレートを外側world_editorのdryRunで構文予行しました。現在の公開tool集合、各人のID/既存欄/選別・件数制限、単一queryとbatch payloadの転送、親confirmの違いと予行結果、無効な旧名・raw座標・複数操作の拒否、反復・位置と停止時計を照合しました。親子flagsによる実dispatch境界はhostで確認したもので、実機は全件を外側dryRunで止めています。

batchのobject payloadは既存parserでは文字列のまま保持し、coordinate wrapperがJSONへ戻します。構文予行でその形を確認したことを、wrapperを実行した結果やnative移動の適格性の証明とは扱いません。canMoveToは従来のcomponent/SMI存在だけで、実機では対象queryの解決・到達性・Moveボタン表示/許可・移動指示・到着を試験していません。予行後の実行を自動で行う経路は追加していません。

元導入物・対象設定・原本・共有保存・保存先選択・他MOD/DLCと元版の未ロード入口へ復帰しました。同じ接続sessionや全preferencesのbyte一致を主張するものではありません。通常UI全体、実移動と到着、他の案内・旧storage等の整合は親項目に残ります。
