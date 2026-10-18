# 梱包の省略と出力先

OniMcp・CycleTrim・OniModTemplateの通常Buildは、既定でMODフォルダ・ZIP・ソースtar.gzを作る。`SkipModPackaging=true`を明示すると`PackageMod`全体を実行せず、既存の梱包物を削除・再生成しない。コンパイル・メタデータ生成・OniMcpのビルド番号割当を停止する設定ではない。

| プロパティ | 未指定時 |
| --- | --- |
| DistPath | リポジトリのdistディレクトリ |
| DistModPath | DistPath配下のAssemblyNameディレクトリ |
| ZipFile | DistPath配下のAssemblyName.zip |

指定値は条件付き既定値で尊重し、内部のディレクトリ値へ末尾区切りを補う。CLIとimported propsの両方で同じ契約とし、独立したZIP親ディレクトリも作成する。ソースtar.gzはZipFileと独立してDistPathへ置く。

出力先はMODプロジェクトのソースディレクトリ外にする。ソース自身／配下への出力と、ソースを包含するDistModPathは、梱包の削除・作成前に`ONIMODS_PACKAGE_DESTINATION_INSIDE_SOURCE`で拒否する。通常のリポジトリdistは各MODのソース外なので利用できる。検査は正規化パスの境界に基づき、シンボリックリンクを解決した先の保証ではない。

ソースtar.gzはbin／obj／distと、生成された`.build-number`、`.build-number.lock`、`.build-number.*.tmp`を除外する。`BuildNumber.targets`などのビルド実装はソースとして保持する。

```sh
rtk proxy dotnet build mods/OniMcp/OniMcp.csproj -c Release -p:OniManagedPath="$ONI_MANAGED_DIR" -p:SkipModPackaging=true
rtk proxy dotnet build mods/OniMcp/OniMcp.csproj -c Release -p:OniManagedPath="$ONI_MANAGED_DIR" -p:DistPath="$ONI_PACKAGE_DIR"
```

出力先の変更はアップロード・配布を行わない。公開フォークの識別子と公開停止ガードを維持する。

## 検証と限界

実際のPackageModを一時MSBuildプロジェクトへ読み込む回帰は3 MOD・69ケース（通常27／省略12／拒否30）が成功。CLI／imported props、独立出力、空白・末尾区切り、相対パスと別cwd、兄弟接頭辞、ソース重複の事前拒否、ファイルハッシュ保持、ZIPのCRCと入力一致、source除外を確認した。開発側の同じターゲットでは7変異を検出した。fixtureのDLLは架空データで、Build／GenerateModFiles／ゲームの動作は別の受入である。空ディレクトリや時刻属性の完全一致は主張しない。

公開作業ツリーで3 MOD×通常／省略の6回のゲームDLL参照Release Buildが警告0／エラー0、64条件が成功。実DLLとmetadataのZIP一致、source除外、既存distと省略時のカスタム出力のファイルハッシュ保持を照合した。オンラインNuGet監査は今回の実行だけ省略した。

開発IDのOniMcpでは生成ZIPから取り出したファイルでゲームを起動し、48条件でバージョン・MCP公開ツール・代表検索・配備一致・元状態と停止保持を確認した。公開IDの変更後実機受入やCycleTrim／Templateのゲーム動作試験とは区別する。公開IDのDLLは配備していない。

Windows／macOSの実環境とシンボリックリンク先の包含判定は未検証。個別セーブ・座標・生の応答／ログ・利用者設定・開発履歴は含めない。
