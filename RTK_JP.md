# RTK - Rust Token Killer（Codex CLI）

**用途**: シェルコマンドの出力を最適化し、トークン消費を抑える CLI プロキシ。

## 規則

シェルコマンドには、必ず `rtk` を前置する。

例:

```bash
rtk git status
rtk cargo test
rtk npm run build
rtk pytest -q
```

## 管理用コマンド

```bash
rtk gain            # トークン削減量の統計
rtk gain --history  # 最近のコマンドの削減履歴
rtk proxy <cmd>     # 出力をフィルタリングせず、そのままコマンドを実行
```

## 動作確認

```bash
rtk --version
rtk gain
which rtk
```
