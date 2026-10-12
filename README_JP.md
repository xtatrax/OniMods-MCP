# OniMods

[main からの変更点](docs/main-differences_JP.md) · [公開版の構成と検証範囲](PUBLIC_RELEASE.md)

> 公開用の独立した実験用フォークです。MOD ID: `PublicFork.OniMcp`。この版の不具合はフォーク側 (this fork’s issue tracker)へ報告してください。このフォークのSteam公開経路は無効化しています。

> **通常の利用には、[オリジナル版の OniMods](https://github.com/LIghtJUNction/OniMods) をおすすめします。** このリポジトリは、日本語化や機能改良を試すための派生版です。

元のリポジトリ: [LIghtJUNction/OniMods](https://github.com/LIghtJUNction/OniMods)

[English](README.md) | [简体中文](README_ZH.md)

<p align="center">
  <a href="LICENSE"><img alt="ライセンス" src="https://img.shields.io/github/license/LIghtJUNction/OniMods?style=for-the-badge&logo=opensourceinitiative&logoColor=white" /></a>
  <a href="https://github.com/LIghtJUNction/OniMods/stargazers"><img alt="スター数" src="https://img.shields.io/github/stars/LIghtJUNction/OniMods?style=for-the-badge&logo=github" /></a>
  <a href="https://github.com/LIghtJUNction/OniMods/forks"><img alt="フォーク数" src="https://img.shields.io/github/forks/LIghtJUNction/OniMods?style=for-the-badge" /></a>
  <a href="https://github.com/LIghtJUNction/OniMods/issues"><img alt="Issue 数" src="https://img.shields.io/github/issues/LIghtJUNction/OniMods?style=for-the-badge&logo=github" /></a>
  <a href="https://github.com/LIghtJUNction/OniMods/commits/main"><img alt="最終コミット" src="https://img.shields.io/github/last-commit/LIghtJUNction/OniMods?style=for-the-badge" /></a>
  <a href="https://github.com/LIghtJUNction/OniMods/blob/main/LICENSE"><img alt="MIT ライセンス" src="https://img.shields.io/badge/license-MIT-blue?style=for-the-badge" /></a>
  <a href="mods/OniMcp/README_EN.md"><img alt="ONI MCP ドキュメント" src="https://img.shields.io/badge/ONI%20MCP-Server%20Docs-4f46e5?style=for-the-badge" /></a>
</p>

<p align="center">
  <a href="mods/OniModTemplate/README.md"><img alt="テンプレート" src="https://img.shields.io/badge/OniModTemplate-Template-0ea5e9?style=for-the-badge" /></a>
  <a href="Cargo.toml"><img alt="Rust" src="https://img.shields.io/badge/Rust%20CLI-onim-f46623?style=for-the-badge&logo=rust&logoColor=white" /></a>
  <a href="https://github.com/dotnet/sdk"><img alt=".NET SDK" src="https://img.shields.io/badge/.NET-8.0-5c2d91?style=for-the-badge&logo=dotnet&logoColor=white" /></a>
</p>

<p align="center">
  <a href="mods/OniMcp/README_EN.md"><img alt="ONI MCP Server: AI による Oxygen Not Included のコロニー操作" src="mods/OniMcp/preview.png" width="47%" /></a>
  <a href="mods/CycleTrim/README.md"><img alt="CycleTrim: Oxygen Not Included のパフォーマンス最適化 MOD" src="mods/CycleTrim/preview.png" width="47%" /></a>
</p>

Oxygen Not Included の MOD 開発を支える、モジュール構成の大規模リポジトリです。

- `onim`: Rust 製の ONI MOD 開発用 CLI。初期化・ビルド・インストール・公開までの作業を支援します。
- `OniMcp`: コロニーの状態と安全な操作を、MCP 対応クライアントに提供する MCP サーバー MOD です。

> **互換性に関する注意**: `OniMcp` は `1.0.0` より前の段階では、API に互換性を損なう変更が入る可能性があります。派生プロジェクト、プラグイン、スクリプト、サードパーティー製クライアントを開発する場合は、使用するバージョンを厳密に固定し、実行時のマニフェスト（例: `oni://tools/manifest`）を互換性判断の基準にしてください。

> **AI インフラとオープンソース開発への支援**: OniMcp の開発とエージェントによるテストには、モデル API のクレジットを使用します。MCP クライアント用に OpenAI 互換のエンドポイントが必要な場合は、[LMM API Gateway](https://api.lmm.best) が選択肢の1つです。[ソースコード](https://github.com/TokenNotIncluded/api.lmm.best)は [TokenNotIncluded](https://github.com/TokenNotIncluded) が公開しています。購入は本プロジェクトの開発支援につながり、有益な Issue、プルリクエスト、テストへの貢献に対して API クレジットが提供される場合もあります。OniMods は引き続きオープンソースであり、このサービスの利用は必須ではありません。

## 目次

- [プロジェクト概要](#プロジェクト概要)
- [構成モジュール](#構成モジュール)
- [クイックスタート](#クイックスタート)
- [主要リンク](#主要リンク)
- [参考オープンソースプロジェクト](#参考オープンソースプロジェクト)
- [ONI MCP Server](#oni-mcp-server)
- [onim](#onim)
- [リポジトリ構成](#リポジトリ構成)
- [onim の導入手順](#onim-の導入手順)
- [コマンド一覧](#コマンド一覧)
- [開発と実行時の注意事項](#開発と実行時の注意事項)
- [互換性](#互換性)
- [開発への参加](#開発への参加)
- [必要な環境と依存ツール](#必要な環境と依存ツール)
- [リリースノート](#リリースノート)
- [ライセンス](#ライセンス)

## プロジェクト概要

このリポジトリは、**2つの層からなるプラットフォーム**として構成されています。

1. **開発ツール層（`onim`）**: ONI MOD の作成から配布まで、開発作業を支援します。
2. **MOD 層（`mods/`）**: 役割と責任範囲を明確に分けた、各 MOD の実装とテンプレートを収録しています。

## 構成モジュール

| モジュール | パス | 役割 |
| --- | --- | --- |
| `onim` | [src/](src/) | MOD のライフサイクルを管理する Rust 製 CLI |
| `OniMcp` | [mods/OniMcp/](mods/OniMcp/) | ONI MCP サーバー MOD とツールインターフェース |
| `CycleTrim` | [mods/CycleTrim/](mods/CycleTrim/) | 実測で処理負荷が高いと判明したシミュレーション処理を最適化する軽量 MOD |
| `OniModTemplate` | [mods/OniModTemplate/](mods/OniModTemplate/) | 新しい MOD を作成するためのひな形 |
| 中国語版プロジェクト文書 | [README_ZH.md](README_ZH.md) | 中国語の概要と利用ガイド |
| MCP 実行時リファレンス | [docs/mcp-tools-reference_JP.md](docs/mcp-tools-reference_JP.md) | 現行のツール・リソースのリファレンス |

## クイックスタート

```bash
cargo install --path .
onim setup
onim doctor
onim init MyMod
onim dev -m MyMod
```

## 主要リンク

- ONI MCP ドキュメント（日本語）: [mods/OniMcp/README_JP.md](mods/OniMcp/README_JP.md)
- ONI MCP ドキュメント（英語）: [mods/OniMcp/README_EN.md](mods/OniMcp/README_EN.md)
- ONI MCP ドキュメント（中国語）: [mods/OniMcp/README.md](mods/OniMcp/README.md)
- ONI MCP サーバーの変更履歴: [mods/OniMcp/CHANGELOG_JP.md](mods/OniMcp/CHANGELOG_JP.md)
- テンプレートの変更履歴: [mods/OniModTemplate/CHANGELOG_JP.md](mods/OniModTemplate/CHANGELOG_JP.md)
- CycleTrim の変更履歴: [mods/CycleTrim/CHANGELOG_JP.md](mods/CycleTrim/CHANGELOG_JP.md)
- プロジェクトの背景: [このプロジェクトに関心を寄せる皆さんへ](docs/open-source-letter_JP.md)

## 参考オープンソースプロジェクト

- コミュニティの参考プロジェクト: [zhuiyun.skill](https://github.com/LIghtJUNction/zhuiyun.skill)
- ONI MOD 開発関連の参考資料: [PLib](https://github.com/peterhaneve/PLib)
- ONI 関連のパッチ適用技術: [Harmony](https://github.com/pardeike/Harmony)、[FastTrack](https://github.com/peterhaneve/FastTrack)

## ONI MCP Server

`OniMcp` は、Oxygen Not Included 向けの、**安全性を重視した MCP ネイティブの操作層**として設計されています。

- **`world_editor`**: ワールドの状態をテキストファイルとして扱う編集方式。仮想化されたセーブデータに SEARCH/REPLACE 形式の編集を適用します。
- **`game_control`**: ゲーム速度、一時停止・再開、セーブ・ロードの制御。
- **`navigation_control`**: カメラ、レイヤー表示、スクリーンショットの操作。
- **`building_control`**: 設備の操作と配管・配線などの経路設定。
- **`orders_control`**: 採掘・片付け・清掃・解体の作業指示の制御。
- **`server_control`**: マニフェスト、スクリーンショットのライフサイクル、診断、セッションの状態管理。

### ONI MCP Server の設計

- MCP クライアントに公開するインターフェースは、意図的にコンパクトで安定した構成にしています。
- 以前の集約エントリーポイントは、仮想ファイルの操作手順でのみ使用する内部操作になっており、MCP ツールとしては登録されていません。
- 操作の目的と範囲を明示し、手順の中でプレイヤーの確認を得る設計です。

実行時の詳しい仕様は、[mods/OniMcp/README_JP.md](mods/OniMcp/README_JP.md) を参照してください。

## onim

`onim` は、このリポジトリで使用する Rust 製 CLI です。ONI MOD の初期化、ビルド、インストール、アンインストール、状態確認、公開を行います。

### 基本的な開発フロー

1. `onim setup` で依存ツールをインストールし、ローカル設定を初期化する。
2. `onim init` で MOD のひな形を作成する。
3. `onim dev -m <mod>` で変更をすばやくビルド・反映し、開発を進める。
4. `onim build` と `onim publish` でビルド・公開する。

CycleTrim と OniMcp の公開には、[docs/steam-publishing_JP.md](docs/steam-publishing_JP.md) に定められた、公開対象を固定したヘッドレス実行のワークフローを使用します。

## リポジトリ構成

```text
.
├── onim.toml                     # onim の設定（既定の MOD とエイリアス）
├── Directory.Build.props.example # .NET 設定のテンプレート
├── Directory.Build.props         # ローカルで生成する設定（コミット対象外）
├── Cargo.toml                    # onim CLI のプロジェクト
├── src/                          # onim のソースコード
├── mods/                         # MOD のワークスペース
│   ├── OniModTemplate/            # 再利用可能な MOD テンプレート（ModInfo + Patches/）
│   ├── OniMcp/                    # ONI MCP サーバー
│   │   ├── ModInfo.cs            # エントリーポイントのみ
│   │   ├── Patches/ UI/ Config/ Core/ Server/ Support/
│   │   └── Tools/{Core,Entry,WorldEditor,Shared,Impl}/
│   └── CycleTrim/                 # パフォーマンス最適化 MOD
│       ├── ModInfo.cs            # エントリーポイントのみ
│       ├── Core/                 # パッチ以外の共通ロジック
│       └── Patches/              # Harmony パッチ
├── docs/                         # 参照ドキュメント
└── scripts/                      # 補助スクリプトと検証ツール
```

## onim の導入手順

```bash
# 1. ソースから CLI をインストール
cargo install --path .

# 2. ゲームのインストール先と依存ツールを検出
onim setup

# 3. ローカルの開発環境を確認
onim doctor

# 4. MOD を作成
onim init MyMod --author YourName --desc "Your mod description"

# 5. 開発サイクル
onim dev -m MyMod         # ビルドし、Dev にインストール
onim info                 # インストール済みの MOD を確認

# 6. リリースサイクル
onim install -m MyMod     # Release ビルドを Local にインストール
onim publish -m MyMod     # Steam Workshop に公開

# 7. 後片付け
onim uninstall -m MyMod   # 対象範囲を指定するフラグに対応
```

初めて利用する場合は、最初のビルド前にローカルのビルド設定を作成します。

```bash
cp Directory.Build.props.example Directory.Build.props
# Directory.Build.props の OniGamePath を、ローカルの ONI インストール先に変更
```

この設定は `onim setup` で自動生成することもできます。

## コマンド一覧

| コマンド | 用途 |
| --- | --- |
| `onim setup` | 設定を初期化し、依存ツールを検出する |
| `onim doctor` | パス、ツール、設定済みの MOD ソースを読み取り専用で診断する |
| `onim init <name>` | テンプレートからひな形を作成する |
| `onim build` | MOD をビルドする（Release ビルドには `--release` を指定） |
| `onim build --all` | 設定されているすべての MOD をビルドする |
| `onim dev` | MOD をビルドし、`mods/Dev` にインストールする |
| `onim dev --all` | 設定されているすべての MOD をビルドし、`mods/Dev` にインストールする |
| `onim install` | Release ビルドを作成し、`mods/Local` にインストールする |
| `onim uninstall` | `dev/local/all` で範囲を指定して MOD をアンインストールする |
| `onim info` | Dev・Local・Steam のインストール済み MOD を表示する |
| `onim publish` | Steam Workshop に公開する。`--non-interactive` と `--dry-run` に対応 |
| `onim list` | 設定に登録されている MOD を一覧表示する |

## 開発と実行時の注意事項

- すべての MCP 呼び出しで、操作の意図を表すタスクと、短く明確な説明を指定してください。
- 最初の計画段階では、状態、安全性、マニフェストなどを読み取り専用で確認することを優先してください。
- このプロジェクトは、完全自律プレイで人間の監督を置き換えられるかのような説明はしません。

### 現在の開発方針

- ゲーム操作を**仮想ファイル中心のワークフロー**へ移行する。
- 重要な操作では、明示的な確認と操作範囲の制限を維持する。
- 厳格な安全性チェックを保ち、ツールのバージョン間でインターフェースの互換性を維持する。

## 互換性

- `OniMcp` は 1.0 未満のため、API インターフェースは変更される可能性があります。
- 派生プロジェクトや外部連携では、使用するバージョンを厳密に固定し、実行時のマニフェストを互換性判断の基準にしてください。
- 実行時の確認には `oni://tools/manifest` を使用してください。

## 開発への参加

1. まず Issue を作成し、変更範囲と設計について相談してください。
2. PR は、まとまりのある1つの変更に絞ってください。
3. 動作が変わる場合は、ドキュメントや変更履歴へのリンクを追加・更新してください。
4. マージ前に、ローカルでの作業手順（`onim setup`、関連する検証スクリプト）を確認してください。

MOD の回帰テスト一式を実行するには、Python 3 と .NET SDK 10 を用意し、`python3 scripts/check_mods.py` を実行してください。テストの対象範囲と、ゲームのインストールが必要な追加の確認項目は、[MOD のテスト](docs/mod-testing_JP.md)を参照してください。

## 必要な環境と依存ツール

- [Rust](https://rustup.rs/): `onim` のコンパイルに使用。
- [.NET SDK](https://dotnet.microsoft.com/download): ONI MOD のビルドに使用。
- `unzip` と `tar`: パッケージ化・インストールの補助処理に使用。
- Steam Workshop とローカルの ONI 環境: 統合テストに使用。

## リリースノート

- `OniMcp` のリリース履歴: [mods/OniMcp/CHANGELOG_JP.md](mods/OniMcp/CHANGELOG_JP.md)
- `CycleTrim` の変更履歴: [mods/CycleTrim/CHANGELOG_JP.md](mods/CycleTrim/CHANGELOG_JP.md)
- テンプレートの変更履歴: [mods/OniModTemplate/CHANGELOG_JP.md](mods/OniModTemplate/CHANGELOG_JP.md)

## ライセンス

このリポジトリは MIT ライセンスで公開されています。詳しくは [LICENSE](LICENSE) を参照してください。
