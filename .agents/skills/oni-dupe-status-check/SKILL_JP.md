---
name: oni-mcp-dupe-status-check
description: ONI の複製人間の状態確認を求められたとき、身動きが取れない・閉じ込められた・目的地に到達できない疑いがあるとき、想定外の場所で待機または睡眠していると報告されたとき、救助の優先度判断が必要なとき、健康状態や移動経路を手早く点検したいときに使用する。まず dupes_control domain=info action=status_check で確認し、問題が検出された範囲だけを追加調査する。ユーザーから明示的な許可がない限り、救助操作は実行しない。
---

# ONI MCP 複製人間の状態チェック

## 目的

閉じ込められている、目的地に到達できない、疲労している、窒息しかけている、空腹である、または作業を正常に進められない可能性がある複製人間を、すばやく見つける。このスキルは、原則として読み取りのみで使用する。

## 最初に行う確認

まず、状態をまとめて確認できるツールを使う。

```
dupes_control domain=info action=status_check radius=8 includeReachableSamples=true
```

特定の複製人間だけを確認する場合:

```
dupes_control domain=info action=status_check name=<dupeName> radius=10
```

目的地や拠点のセルに到達できるか確認する場合:

```
dupes_control domain=info action=status_check targetX=<x> targetY=<y> targetWorldId=<worldId> radius=10
```

`dupes_control domain=info action=status_check` では必要な情報が得られない場合を除き、`dupes_control domain=info action=detail/needs`、スクリーンショット、広範囲のマップ走査から調査を始めない。

## 対象範囲と重複調査の防止

- このスキルの対象は、健康状態、移動経路、閉じ込め、作業が進まない原因、救助の優先度判断に限る。複製人間の改名、能力に応じた役割分担、スケジュール設定には使用しない。
- `dupes_control domain=info action=status_check` を、複製人間の一覧を取得する汎用ツールとして使わない。名簿が必要なら `colony_control domain=read action=dupes`、能力値が必要なら `dupes_control domain=info action=attributes`、改名には `dupes_control domain=command action=auto_rename`/`dupes_control domain=command action=rename` を使う（まず `server_control domain=catalog action=search` でスキーマを確認する）。
- 同じ引数での `dupes_control domain=info action=status_check` の呼び出しは、1回までとする。結果だけでは情報が足りない場合は、返された `scanRect`、`nextRead`、または不足している項目に応じて、対象を絞った読み取りに切り替える。
- 問題が検出された複製人間がいなければ、そのまま「リスクは見つかりませんでした」と報告する。マップ、スクリーンショット、能力値の追加取得は行わない。

## 追加調査

`flagged > 0` の場合は、問題が検出された複製人間だけを調べる。

```
read_control domain=world action=area_snapshot x1=<scanRect[0]> y1=<scanRect[1]> x2=<scanRect[2]> y2=<scanRect[3]> worldId=<worldId> preset=construction encoding=plain includeScreenshot=false
```

必要な場合に限り、次の情報を追加で確認する。

- `dupes_control domain=info action=detail id=<id>`: 能力値やスケジュールなどの関連情報を確認する。
- `dupes_control domain=side_screen action=todos id=<id> includeBlocked=true`: 作業の実行に必要な条件のうち、満たされていないものを確認する。
- `navigation_control action=focus_dupe id=<id>`: ユーザーが画面上で対象の複製人間に注目したい場合にのみ使用する。
- スクリーンショットは目視確認にのみ使い、座標の推定には使わない。

## リスクの判断

次の場合は、緊急性が高いと判断する。

- `risk=critical`
- 原因に `no_reachable_nearby_cells`、`low_breath`、`invalid_current_cell` が含まれる
- 複製人間が液体の中や極端な温度の場所にいる、またはカロリーやスタミナが著しく低い

次の場合は、今後の作業計画で対処すべき警告として扱う。

- `target_unreachable`
- `very_few_reachable_nearby_cells`
- 現在の作業に異常がある、待機中または作業がない、あるいは拠点から離れた場所で眠っている

ツールの応答に含まれる `scanRect` と `nextRead` を使う。独自の判断で広範囲のマップ検索を追加しない。

## 救助方針

原則は読み取りのみ。ユーザーが明示的に修正・救助・実行を求めていない限り、次のツールは呼び出さない。

- 採掘、建築、複製人間の移動、ゲームの再開など、ゲームの状態を変更する書き込み・実行ツール全般

救助操作を行う際は、次の手順に従う。

1. ユーザーが検討や計画を求めている場合は、まずゲームを一時停止する。
2. 問題が検出された範囲の `scanRect` を確認する。
3. 採掘や足場の建築によって通行可能な経路を開くことを優先する。状況を確認せずに複製人間を移動させない。
4. `building_control domain=planning action=search_defs/materials/placement_candidates/preview` で足場の計画を確認する。明示的な許可を得てから、単一の anchor を指定した `build_area` の dry-run を行い、その後 confirm で実行する。
5. 操作後は `dupes_control domain=info action=status_check` で結果を検証する。

`dupes_control domain=info action=status_check targetX/targetY` または `dupes_control domain=command action=move_to` の到達可能性チェックで、目的のセルに到達できると確認されない限り、そこへ複製人間を移動させない。

## 出力形式

簡潔な日本語で報告する。

```
複製人間の状態:
リスク:
閉じ込めの疑い:
周辺環境・移動経路:
提案:
```

複製人間の名前、座標、リスクを示し、次に安全に行える情報確認または操作を1つ提案する。問題が検出された複製人間がいなければ、その旨だけを伝え、追加のツール呼び出しは行わない。
