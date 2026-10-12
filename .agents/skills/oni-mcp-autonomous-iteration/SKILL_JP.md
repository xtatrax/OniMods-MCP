---
name: oni-mcp-autonomous-iteration
description: ONI MCP サーバーを反復的に改善するときに使用する。エージェントが onim によるビルド、onim dev によるデプロイ、ゲームの自動起動・セーブ読み込み、テスターエージェントと連携したランタイムテストの一括実行を行い、長時間の自律プレイに向けてツールの使い勝手を改善する。
---

# ONI MCP の自律的な反復改善

ONI MCP サーバーの改善を最初から最後まで進めるためのワークフロー。特に、手動での介入を最小限に抑えながら、テスト用エージェントが Oxygen Not Included を操作できるようにする場合に使用する。

## 作業ループ

1. まず現在の状態を確認する。
   - `git status --short` を実行する。
   - ONI がすでに起動しているか、`http://localhost:8788/mcp/` が応答するかを確認する。
   - スキーマを決めつける前に、`server_control domain=catalog action=manifest` または対象を絞ったカタログ検索で、現在の MCP カタログを読む。

2. ONI を再起動する前に、実装をひとまとまりにする。
   - 実装をまとめて行い、その後に `onim build`、`onim dev`、ONI の再起動、ランタイムテストの一括実行を、それぞれ1回行う形を基本とする。
   - 古い DLL がテストを妨げている場合を除き、小さな編集のたびに再起動しない。
   - 新しいファイルは500行未満に収める。
   - 連番で分割したファイル名ではなく、`GameLaunchTools.cs` のように役割が分かる名前を使う。

3. 手順どおりにビルドしてデプロイする。
   - `onim build` を実行する。
   - `git diff --check` を実行する。
   - `onim dev` を実行する。
   - ONI を再起動するか、ランタイムのカタログで新しいスキーマの読み込みを確認するまでは、デプロイした Dev MOD の DLL はまだ反映されていないものとして扱う。

4. MCP を通じてゲームを起動し、セーブを読み込む。
   - 原則として `game_control domain=launch action=status limit=5` を使う。
   - 最初に `game_control domain=launch action=start dryRun=true confirm=true index=0 resume=false` を呼び出す。
   - ドライランに問題がなければ、`game_control domain=launch action=start confirm=true index=0 resume=false` を呼び出す。
   - `colony_control domain=snapshot action=get profile=minimal` で検証する。
   - テストで時間経過が明示的に必要な場合を除き、テスト中はゲームを一時停止したままにする。
   - Linux でプロセスを完全に再起動する場合は、`game_control domain=launch action=restart_load dryRun=true resume=false` を呼び出してから、`confirm=true` で再度呼び出す。受け付けられた応答には `jobId` と正確なセーブパスが含まれる。Steam で再起動した後は、`stage=loaded` または `stage=failed` になるまで `game_control domain=launch action=restart_status jobId=<id>` で状態を確認する。
   - `restart_load` は、意図的にプロセスをまたぐ非同期処理になっている。中継処理に渡すのは旧 PID と、ローカルで解決した Steam 実行ファイルの絶対パスだけ。その後 Steam の AppID 457140 を起動する。セーブパスや MCP トークンは渡さない。

5. 長時間のプレイでは、トークン消費を抑えた定期確認を使う。
   - 原則として `colony_control domain=snapshot action=get profile=minimal delta=true deltaKey=<loop> watch=stress,food_kcal,red_alert,alerts watchOnly=true` を使う。
   - `watch.alert=false`、`red_alert=false`、食料がしきい値を上回っていること、ストレスが低いことを、続行の目安にする。
   - 独立した状態の読み取りには `server_control domain=batch action=call_many responseMode=summary` を使う。
   - バッチの要約では、完全な出力を求める前に、`valid/failed/executed`、続いて子呼び出しの `summary.next`、`summary.tokenHint`、件数フィールドを確認する。

6. デプロイ後にテスターエージェントからフィードバックを得る。
   - テスターにはタスクと現在の MCP エンドポイントに関する前提だけを渡す。こちらが想定している修正内容は伝えない。
   - 成功した確認項目、進行を妨げるバグ、使い勝手の問題、次に直す価値が高い項目を、簡潔に報告してもらう。
   - まず読み取り専用と `dryRun=true` のテストを優先する。
   - 必要に応じて、カタログスキーマ、起動・状態確認、スナップショット、ワールド検索、配列パターン検索、計画の解析、建築のドライラン、作業指示のドライランメタデータ、電力・電線の自動接続、区域ハンドルを対象に含める。

7. テスターの指摘に基づいて改善を繰り返す。
   - 1回の呼び出しで処理を完結しにくくしている問題や、エージェントにとって分かりにくい点を優先して直す。
   - 再ビルド、再デプロイを行い、影響するランタイム確認だけを再実行する。
   - 自律動作を妨げている場合を除き、無関係なリファクタリングは後回しにする。
   - すでに大きすぎるファイルには、同じ変更で役割に沿った適切な分割も行う場合を除き、行を追加しない。

## ランタイムの確認

MCP エンドポイントが応答するようになったら、同梱のスモークテスト用スクリプトを優先して使う。

```bash
python .agents/skills/oni-mcp-autonomous-iteration/scripts/runtime_smoke.py
```

このスクリプトは、JSON-RPC の初期化、既定の公開インターフェースが正確に7ツールであること（または認証済みの全ツールインターフェース）、起動状態、計画の解析、`world_editor` のアクティブワールドのライフサイクル動作を検証する。コロニーが読み込まれている場合、スナップショットとワールドの配列パターンの読み取りを、全ツールインターフェースでは直接、既定のインターフェースでは公開されている `server_control` のバッチ処理を通じて行う。メインメニューでは、アクティブファイルの応答が構造化された `game_not_loaded` になることを確認し、非公開の読み取り呼び出しは事前検証だけを行う。作業指示は一切出さない。

長期間の生存検証には、次を使う。

```bash
python .agents/skills/oni-mcp-autonomous-iteration/scripts/survival_watch.py --target-cycles 100 --poll-seconds 20 --speed 3
```

100サイクルの実行前に、MCP に生存計画を問い合わせる。

```text
colony_control domain=survival action=plan targetCycles=100
```

`canAttemptLongRun`、`decision`、`blockers` を読み、`nextCalls` を順番に実行する。

1サイクル分を待たずに短時間で検証する場合は、次を使う。

```bash
python .agents/skills/oni-mcp-autonomous-iteration/scripts/survival_watch.py --target-cycles 1 --max-seconds 10 --allow-partial --ignore-critical-diagnostics
```

このスクリプトは MCP を直接初期化し、セーブが読み込まれていることを確認し、速度を設定して、`colony_control domain=snapshot action=get profile=minimal` を定期的に取得する。サイクル・警告のメタデータを簡潔に報告し、`--no-pause-at-end` が指定されていない限り、終了時に一時停止する。報告には `alertLevel`、診断の警告、`diagnosticsIgnored`、`pausedAtEnd` を含む。複製人間がいない、最大ストレスが100%、非常警報モード、警告レベルが critical または error、重大な診断結果がある場合は失敗とする。ただし、スクリプト自体のスモークテスト用に `--ignore-critical-diagnostics` を指定した場合は、重大な診断結果を失敗条件から除外する。

起動関連または計画関連の変更後は、少なくとも次のスモークテストを実施する。中国語・英語の入力例は、元の解析・検索動作を検証するため、そのまま使用する。

```text
server_control domain=diagnostics action=status detail=brief
game_control domain=launch action=status limit=5
game_control domain=launch action=start dryRun=true confirm=true index=0 resume=false
colony_control domain=snapshot action=get profile=minimal
building_control domain=planning action=parse_plan plan="粉砂岩砖@氧气" worldId=0
building_control domain=planning action=parse_plan plan="用粉砂岩建造砖块，锚点氧气" worldId=0
building_control domain=planning action=parse_plan plan="Build two sandstone tiles near the base" worldId=0
read_control domain=world action=search pattern="粉砂岩-泥土-氧气" direction=both matchMode=smart worldId=0 limit=3
building_control domain=planning action=build_area plan="粉砂岩砖@氧气" worldId=0 dryRun=true limit=3
server_control domain=batch action=call_many dryRun=true responseMode=summary calls=[...]
```

計画解析で期待する動作:

- `粉砂岩砖@氧气` は `prefabId=Tile`、`material=SiltStone`、`query=氧气` に解決される。
- `Build two sandstone tiles near the base` は `prefabId=Tile`、`material=SandStone`、`query=base` に解決される。
- `锚点氧气`、`靠近电池`、`目标厕所`、`@氧气` などの自然なアンカー指定から、アンカーの検索クエリが生成されること。
- 素材の不足、対象に到達できない、障害物がある、支持がないなど、実際のゲーム上の理由が報告されるなら、ドライランの失敗は許容する。

## ONI プロセスの扱い

Steam 経由でのみ起動する、同梱の起動補助スクリプトを使う。

```bash
bash .agents/skills/oni-mcp-autonomous-iteration/scripts/launch_oni_mcp.sh
```

実行中の ONI PID に対応する MCP エンドポイントが正常なら、`unity.lock` や Steam に手を加えず、直ちに終了する。それ以外の場合は古い `unity.lock` を削除し、Steam クライアントが存在することを確認して、固定の AppID 457140 を Steam URI でのみ起動する。AppID は変更できない。MCP がオフラインで旧 ONI PID がまだ残っている場合は、そのすべてが終了するまで待ってから URI を送る。起動要求後の成功条件は、起動前にはなかった PID が動作していることと、`http://localhost:8788/mcp/` が正常に応答することの両方。失敗時は簡潔な診断を出力し、ONI の実行ファイルを直接起動する代替処理は行わない。待機時間を調整する例:

```bash
ONI_MCP_WAIT_SECONDS=360 bash .agents/skills/oni-mcp-autonomous-iteration/scripts/launch_oni_mcp.sh
ONI_OLD_PROCESS_EXIT_WAIT_SECONDS=60 bash .agents/skills/oni-mcp-autonomous-iteration/scripts/launch_oni_mcp.sh
```

起動は Steam 経由に限定する。

```bash
pidof OxygenNotIncluded | xargs -r kill
rm -f ~/.local/share/Steam/steamapps/common/OxygenNotIncluded/unity.lock
steam steam://run/457140 >/tmp/oni-steam-launch.log 2>&1 &
```

プロセスと MCP エンドポイントの両方が利用可能になるまで待つ。

```bash
pidof OxygenNotIncluded
curl -fsS --max-time 2 http://localhost:8788/mcp/
```

終了対象を探すコマンドで `pgrep -f OxygenNotIncluded` を使わない。シェルコマンド自体に一致するおそれがある。

## テスターの報告形式

テスターには次の形式で報告を求める。

```markdown
## 結果
- 総合判定: 成功／失敗
- セーブの読み込み: 読み込み済み／未読み込み
- 起動後の一時停止: はい／いいえ

## 成功した項目
- ...

## バグ
- 重大度、ツール呼び出し、実際の結果、期待する結果

## 使い勝手
- エージェントが依然として座標指定、繰り返しの呼び出し、暗黙の知識を必要とした箇所

## 次に直す項目
- 自律動作への影響が大きい順に並べた修正一覧
```

## 長期的な目標

手動での介入を抑え、テスターエージェントが100サイクル生存できることを目指して改善する。

- 座標の直接指定より、意味ベースの検索と再利用可能な区域ハンドルを優先する。
- すべての操作のドライランで、到達可能性、素材の利用可否、リスク、次の操作を説明できるようにする。
- 複製人間の作業を発生させるすべての書き込み・操作経路に、優先度の扱いを含める。直接設定に対応している場合は優先度を設定し、それ以外は既存の作業指示・優先度のエンドポイントを使う、コンパクトな `priorityAction`・`nextActions` を返す。移動経路の梯子、食料、酸素、トイレ、到達可能な素材の採掘、前提となる片付け、救出経路など、生存に不可欠な作業は、ユーザーが別の値を指定していない限り高優先度とし、通常は 7 を使う。
- ツールスキーマでは、集約エントリーポイントから、対応するすべてのパラメーターを利用できるようにする。
- 1回の呼び出しで検索・操作を行う経路では、次のエージェントが大きなマップを読まずに判断できる、コンパクトなメタデータを返す。
- 製造ポッドの報酬には、既存の `building_control domain=side_surface surface=facility kind=printing_pod` を必ず使う。新しい公開ツールは追加しない。`action=list_rewards`、`action=claim rewardIndex=N dryRun=true` の順に使い、報酬を受け取ることが明示された場合だけ `confirm=true` を使う。生存に重要な素材・食料・酸素を受け取った後は、報酬が実際にコロニーの役に立つよう、片付け・収納・建築の優先度に関する後続処理を直ちに計画するか、返す。
