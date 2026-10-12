# Windows 開発時の注意事項（`onim dev`）

## 実際の Windows 環境で起きた問題

1. **MOD のインストール先**: `onim` は `AppData\LocalLow\Klei\Oxygen Not Included\mods\Dev` に書き込んでいましたが、ゲームは `Documents\Klei\OxygenNotIncluded\mods` から MOD を読み込んでいました（Player.log の `Loaded assembly ...\Documents\Klei\OxygenNotIncluded\mods\...` を参照）。
2. **Expand-Archive と空白を含むパス**: `Oxygen Not Included` を含む展開先パスを引用符で囲まないと、PowerShell の複数の引数に分割されていました。
3. **ソースの tar アーカイブ**: Windows の `tar` では `tar -czf 'D:\...'` が失敗します。MOD の ZIP 自体には問題ありませんでした。

## このリポジトリでの修正

- `[Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)` で Windows の既知のフォルダーとして実際の「ドキュメント」の場所を取得し、その下の `Klei\OxygenNotIncluded` にインストールします。`USERPROFILE\Documents` や LocalLow へのフォールバックは行いません。
- `dev` と `install` で共通のアーカイブ処理を使います。Windows では `-NoProfile -NonInteractive` を指定して PowerShell を呼び出し、`Expand-Archive -LiteralPath` と `-DestinationPath` の値を安全に単一引用符で囲みます。他のプラットフォームでは、シェルを介さずにパスを直接 `unzip` に渡します。
- tar に渡すパスは二重引用符で囲み、ソースの tar アーカイブを作成できなければビルドを失敗させます。
- 任意の補助ツールとして `scripts/install_onim.bat` があります。`vswhere` で `Microsoft.VisualStudio.Component.VC.Tools.x86.x64` を含む最新の Visual Studio を探し、`vcvars64` を実行してから Cargo でインストールします。検出された `vcvars64.bat` のパスを変更するには、`ONIM_VCVARS64` を設定します。

## 確認方法

```bat
onim dev -m OniMcp
:: Oxygen Not Included を再起動する
:: Dev 版 MOD を有効にする。同じ staticID の Steam Workshop 版は無効にする
```
