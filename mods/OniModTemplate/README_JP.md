# OniModTemplate

## 目次

- [用途](#用途)
- [クイックスタート](#クイックスタート)
- [変更履歴](#変更履歴)
- [参考リンク](#参考リンク)
- [関連プロジェクト](#関連プロジェクト)

## 用途

新しい MOD プロジェクトをすばやく作成するための、最小構成の Oxygen Not Included MOD テンプレートです。

- `onim init` でひな形を作成する際の標準的なディレクトリ構成とビルド設定を提供します。
- Harmony パッチは `Patches/` に置きます。
- 必要に応じて、再利用するロジックは `Core/`、実行時の UI・オーバーレイは `UI/`、オプションは `Config/` に置きます。
- MOD のルートには、`ModInfo.cs` とプロジェクト・メタデータファイルだけを置きます。
- 新規開発の立ち上げ、パッチの検証、公開までを見据えた MOD 開発に適しています。

## クイックスタート

リポジトリのルートで実行します。

```bash
onim build -m OniModTemplate
onim dev -m OniModTemplate
```

## 変更履歴

この作業ツリーには、テンプレートのコミット済み履歴がまだありません。以下を参照してください。

- [CHANGELOG_JP.md](CHANGELOG_JP.md)

## 参考リンク

- リポジトリ: [LIghtJUNction/OniMods](https://github.com/LIghtJUNction/OniMods)
- テンプレートのプロジェクト: [OniModTemplate.csproj](OniModTemplate.csproj)
- MOD のオプション用ライブラリ: [PLib](https://github.com/peterhaneve/PLib)

## 関連プロジェクト

- [FastTrack](https://github.com/peterhaneve/FastTrack)（ONI のパッチ実装の参考）
- [Harmony](https://github.com/pardeike/Harmony)（パッチ適用フレームワーク）
- [zhuiyun.skill](https://github.com/LIghtJUNction/zhuiyun.skill)（ONI エージェントの操作手順）
