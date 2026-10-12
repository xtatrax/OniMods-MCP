# MOD の回帰テスト

GitHub Actions の `Mod quality` ワークフローと同じ確認を実行します。

```sh
python3 scripts/check_mods.py
```

必要な環境は Python 3 と .NET SDK 10 です。C# の初回実行時には、NuGet から Newtonsoft.Json を復元します。Steam のインストール、ゲームのアセンブリ、ローカルの `Directory.Build.props` は不要です。`PATH` にない SDK を使用する場合は `--dotnet /path/to/dotnet`、Python によるソースコード上の前提条件の検査だけを実行する場合は `--static-only` を指定します。

このランナーは、ソースコード上の前提条件の検査、`tests/` 配下のすべてのコンソール型回帰テストプロジェクト、既存の CycleTrim の方針に関するアサーションを実行します。各コンソールプログラムは、失敗すると非ゼロの終了コードを返します。これらのプロジェクトは本番コードをリンクし、ゲーム API が必要になる箇所には小さなスタブを使っています。リンクしたロジックの動作は確認できますが、Unity 上の挙動や Harmony パッチの互換性を検証するものではありません。

## ゲーム本体が必要な確認

単独実行のランナーでは、以下の確認は **NOT RUN（未実行）** と表示されます。MOD をリリースする前に、`Directory.Build.props.example` を使ってゲームのパスを設定し、両方の MOD をビルドして、次を実行してください。

```sh
dotnet build mods/OniMcp/OniMcp.csproj -c Debug -warnaserror
dotnet build mods/CycleTrim/CycleTrim.csproj -c Release -warnaserror
python3 scripts/verify_restart_packaging.py
python3 scripts/verify_cycletrim_release_binary.py
python3 scripts/verify_cycletrim_target_contract.py /path/to/Assembly-CSharp.dll
```

CycleTrim のアセンブリ検査には `ilspycmd` が必要です。ONI 内でも MOD を確認してください。MCP クライアントを接続し、リソースを読み取り、失敗する呼び出しを含むバッチを実行し、イベントストリームを開いたままサーバーを再起動します。また、待機中と作業中の複製人間、移動中の動物がいるコロニーをロードします。移動経路、優先度の更新、即時フォールバックが引き続き正しく動作することを確認してください。方針のベンチマークで得た計測時間だけでは、ゲーム内の FPS 向上を立証できません。
