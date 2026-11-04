# 公開ツール説明の正規キーとPO対応

公開ツールの引数説明を、要求時のMCP言語で解決する正規カタログキーへ揃える。既存の説明を汎用の英語説明で上書きしていたgameのquery／limitには、保存一覧の絞り込み順と件数制限を含む説明を適用する。操作名、JSONキー、引数の型・必須条件・enum値と処理経路は保持する。

## 今回の範囲

正規キーの追加は次の8項目。いずれも既存の公開引数であり、機能や引数の追加ではない。

| 公開ツール | 引数 |
| --- | --- |
| `building_control` | `storageScope` |
| `orders_control` | `direction` |
| `server_control` | `includePatches` |
| `world_editor` | `includeAchievementUi`、`achievementDetail`、`achievementStatus`、`specDetail`、`foodDetail` |

`tools.<tool>.parameters.<parameter>` に対応する3言語のruntime値を用意する。既存の領域別カタログに説明がある項目は同値aliasとし、POの元contextへ `Runtime JSON alias key:` 注記を付ける。新しい独自文言は安定したcontextへ登録し、POTと3つのPOを同時に維持する。

英語query／limitは、汎用の検索・上限の意味と、既存のsave/status・sandbox/read/list_story_traitsの説明を併記する。保存一覧の既定20・最大100、絞り込みが件数制限より先であることを保つ。

## 言語と原本

実行時は `Localization/Catalogs/*.json` を読む。MCP言語の選択値は `auto`／`en`／`ja`／`zh-Hans`。POは移行済み文言の正本であり、今回もruntime JSONとの対応を維持する。POを直接読むローダーの受入を示すものではない。

`zh-hant.po` は暫定の原本名で、現在の繁体字サンプルと既存の簡体字runtimeを別に扱う。runtimeの簡体字訳は `Runtime zh-Hans msgstr:` 注記へ保持し、Hantの訳と暗黙に同一視しない。ゲーム由来の名前はゲーム側の解決済み名称を使い、MCP独自の案内文はMCP言語に従う。

## 確認した範囲

公開ソース自身のCore hostは16群を通過し、既存15群を保持した。追加の427条件では、実Navigationファクトリ・英語説明の上書き・Registry・Localizerをリンクし、日本語で生成したmetadataを英語・日本語・簡体字・autoの要求へ切り替える。既存のNavigation action／引数集合と、要求間のschema不変・返却snapshotの独立性を照合した。nativeカメラ実行境界は例外で拒否し、ゲームDLLを実行していない。

共有8項目は純metadata fixtureを実Localizerへ通す検査であり、それぞれのowner factoryを実行した証拠ではない。gameのquery／limitも純metadata fixtureに実英語上書きソースを適用する。旧CoreToolEnglishDescriptions全文への差替えはコンパイルに成功し、その後query／limitの意味条件で失敗した。ホストのNuGetAuditは有効に保った。

Python回帰25件と正式source検査CLIを通過した。正式CLIは46カタログ群、298公開metadata項目、138埋込カタログ、35選定PO context、6runtime aliasを照合する。既知factoryの監査context、PO4からのalias全消失、言語別aliasの不一致、HantとHansの分離、未対応構文の拒否も対象とする。これはソースの限定検査であり、配備DLLが返す実tools/listや画面表示の受入とは分ける。

## 公開版自身の本体参照と停止実機確認

公開版自身の本体参照build.82は0警告・0エラーで通過した。停止実機では配備DLLの読込元・公開識別子 `PublicFork.OniMcp` と専用設定を確認し、通常の `tools/list` で全7toolと各toolの必須string taskを照合した。英語・日本語・簡体字・autoの延べ1216説明がそれぞれのカタログに一致し、取得した英語応答を渡す正式source検査CLIも通過した。

実schemaの非説明部分は、同じbuild.82のauto応答を基準に4設定で一致した。変更前のaction・引数型等の保持は製品の限定ソース差分レビューから確認し、実機の言語設定間の比較とは別の根拠として扱う。代表の設備・素材名も、MCPの3言語で本体の日本語名を保持した。

試験後はMCP言語をautoへ戻し、設定全項目の意味的同一性、保護した元データとチェックポイントの不変、停止時計・速度・観測視点を照合した。通常のMOD不一致確認は予行と通常OK経路で処理し、停止を保持した。

ブラウザー／PLibの実描画、非日本語ゲームのauto追従、全応答・仮想文書・エラーの翻訳完成は今回の確認範囲に含めない。既存ソースreaderの手順と過去の限定確認は[ローカライズのソース検査](localization-source-verifier_JP.md)を参照する。
