# ONI MCP ツールリファレンス

ツールの選択、複数ツールを組み合わせた手順の作成、パラメーターの指定、プロンプトやリソースに対応する集約ツールの呼び出しを検討する際に、このリファレンスを参照する。

## 目次

- [機能の検索](#機能の検索)
- [読み取り・書き込み・実行](#読み取り書き込み実行)
- [標準的な手順](#標準的な手順)
- [パラメーターの規則](#パラメーターの規則)
- [バッチ処理と効率化](#バッチ処理と効率化)
- [ツールの分類](#ツールの分類)
- [プロンプトによる手順](#プロンプトによる手順)
- [リソース URI](#リソース-uri)
- [早見表](#早見表)

## 機能の検索

| 目的 | 呼び出し |
|---|---|
| 公開されている機能全体を確認する | `server_control domain=catalog action=manifest` |
| 意図に応じて絞り込む | `server_control domain=catalog action=search query=<intent> detail=brief` |
| 目的を達成する手順を調べる | `server_control domain=catalog action=guide goal=<goal>` |
| 対応範囲を確認する | `server_control domain=catalog action=coverage` |
| 安全性のメタデータを監査する | `server_control domain=catalog action=static_audit` |

マニフェストには、現在の公開機能をまとめたコンパクトな集約インターフェースが示される。旧来の細分化されたツール名は互換用の入り口として扱い、優先的には使用しない。

## 読み取り・書き込み・実行

- **読み取り（Read）:** スナップショット、複製人間の詳細、資源、インフラ、マップなどの状態を取得する。安全に再試行できるのは、引数と対象の状態が変わっていない場合に限る。
- **書き込み（Write）:** スケジュール、優先度、フィルター、しきい値、名前、方針などを設定する。通常は中程度のリスクがあり、確認を必要とする。
- **実行（Execute）:** 一時停止・再開、作業指示、解体、建築、カメラ操作、セーブなどを行う。マップを変更する危険な操作には確認が必要。

リソース（`oni://...`）はキャッシュ可能なスナップショット。ツールはパラメーターを指定して行うリアルタイムの操作。プロンプトは標準的な呼び出し手順を示すが、それ自体では実行しない。

## 標準的な手順

### コロニーの健全性チェック

```text
colony_control domain=snapshot action=get profile=minimal delta=true watch=stress,food_kcal,red_alert,alerts
colony_control domain=snapshot action=get profile=brief        # minimal で懸念が示された場合のみ
read_control domain=resources action=food                      # 食料の詳細が必要な場合
read_control domain=infrastructure action=power_summary        # 電力の問題が示された場合
read_control domain=world action=thermal_overheat_risk         # 熱の問題が示された場合
read_control domain=infrastructure action=rooms                # 部屋の整備状況が判断に関わる場合
```

### 単純で低リスクな操作

1. 対象の状態を読み取る。
2. 対象範囲を限定した意味ベースの呼び出し、または仮想ファイルのパッチを dry-run する。
3. 確認を得て実行する。
4. 対応する対象を絞った読み取りで結果を検証する。

短い範囲の採掘、モップ掛け、片付け、床の敷設、設定変更、または確認済みの建築計画に使用する。

### 複雑な空間配置の計画

```text
game_control domain=speed action=pause
read_control domain=world action=area_snapshot preset=construction|utilities encoding=rle
read_control domain=world action=text_map profile=standard encoding=plain includeElements=true
# 必要に応じて目視確認する場合のみ
navigation_control action=switch_view view=<overlay> screenshot=true
# dry-run、実行、同じ矩形範囲の再読み取りの順に進める
```

仮想ファイルで正確なマップ編集を行う場合は、座標が明示されたマップヘッダーを使う。同じ範囲に意味ベースの操作を繰り返す場合は、`areaId` を使う。

### 複製人間の管理

```text
colony_control domain=read action=dupes
dupes_control domain=info action=detail id=<id>
dupes_control domain=info action=needs id=<id>
dupes_control domain=priority action=list id=<id>
# 必要な変更だけをバッチ処理する
dupes_control domain=priority action=list id=<id>   # 結果を検証する
```

`id` を必要とするツールを使う前に、名簿から名前に対応する数値 ID を取得する。

### 設定の一括更新

```text
server_control domain=batch action=call_many
  dryRun=true responseMode=summary requireAllValid=true stopOnError=true
  defaults={confirm:true}
  items=[
    {t:dupes_control,a:{domain:priority,action:set,id:1,choreGroup:Dig,priority:4}},
    {t:colony_control,a:{domain:management,kind:schedule,action:set_block,schedule:"Default",hour:3,group:Sleep}}
  ]
```

検証に成功したら `dryRun=false` で再度呼び出し、影響を受けた各ドメインを確認する。

## パラメーターの規則

### `worldId`

- アクティブなワールドを対象とする場合は省略する。
- 特定の小惑星やロケット内部を対象とする場合は、その ID を指定する。
- `-1` は、その操作の説明で全ワールドを対象とする動作が明記されている場合にのみ使う。

### `limit`、詳細度、エンコーディング

- `limit` は小さく保つ。最大件数を要求するよりも、ページ分割や絞り込みを使う。
- 判断には `detail=brief|compact` を使い、`full` は詳細な原因調査が必要な場合に限る。
- 対象を絞った読みやすいマップには `profile=standard encoding=plain` を使う。
- 広範囲の読み取り専用走査には `profile=scan encoding=rle` を使う。
- マップのパッチには `format=edit compact=false` を使う。
- バッチ処理の応答は通常 `responseMode=summary` とし、再試行ループには `errors`、個々の子呼び出しの正確な応答が必要な場合には `full` を使う。

### 座標と領域

- 生の矩形座標は、読み取り専用のマップ確認、および型付き操作ファイルで明示的に対応している構文で使用できる。
- 通常の集約操作ツールでは、生の座標ではなく、意味ベースのクエリ、対象、計画、または `areaId` を使う。
- 座標は左下を原点とするワールドのセル座標。返された絶対座標を保持し、`rx/ry` と `x/y` を混同しない。
- 再利用する領域は `read_control domain=area action=define` で定義する。必要に応じて隣接する領域ハンドルを統合し、一時的に作成したカスタムハンドルは使用後に登録を解除する。

### 確認

- 読み取りツールには確認は不要。
- 中程度以上のリスクを伴う変更では、実行時の呼び出しに `confirm=true` が必要。
- まず dry-run する。許可を得る前に、検証を通すためだけに確認フラグを付けない。

## バッチ処理と効率化

### 書き込み前に読み取る

対象のスケジュール、方針、設備、複製人間、マップの状態を正確に読み取り、差分をローカルで計算し、変更するフィールドだけを送信してから、同じ対象を再度読み取る。

### 独立した読み取りの並列実行

```text
server_control domain=batch action=call_many responseMode=summary items=[
  {t:colony_control,a:{domain:read,action:status}},
  {t:colony_control,a:{domain:diagnostic,action:diagnostics}},
  {t:read_control,a:{domain:resources,action:food}},
  {t:read_control,a:{domain:infrastructure,action:power_summary}}
]
```

後の呼び出しに、前の呼び出しが返す ID やハンドルが必要な場合は、同じバッチにまとめない。

### 差分更新

関連する一覧全体を一度読み取り、変更点をローカルで計算し、その変更だけをバッチで送信する。最後に1回読み直して検証する。

### キャッシュの方針

カタログの結果は、現在ロードされている実行環境の間はキャッシュできる。名簿、在庫、スケジュール、部屋の概要、安定した状態情報は、ゲームの状態が急速に進行していない場合に限り、短時間キャッシュしてよい。書き込み、セル単位の状態、頻繁に変わる警告、現在のカメラ状態、電力網の変更中の電力状態はキャッシュしない。変更操作や速度変更のたびに、関連する読み取りのキャッシュを無効にする。

## ツールの分類

| 分類 | 読み取り | 設定・書き込み | 実行 |
|---|---|---|---|
| コロニー | スナップショット、状態、診断、警告、レポート | 診断設定、管理 | 通知に関する操作 |
| 複製人間 | 名簿、詳細、能力値、欲求、状態、優先度、スキル | 優先度、スキル、帽子、改名、割り当て対象 | 明示的に対応している移動や行動の強制 |
| スケジュール | 管理用のスケジュール一覧 | 作成、時間枠の設定、割り当て、最適化 | なし |
| 資源 | 在庫、食料、アイテム検索、収納の詳細、食事の状態 | ピン留め、収納フィルター、食事方針 | なし |
| 設備 | 一覧・概要、定義、素材、候補、設定一覧 | プレビュー、自動接続、有効化・切り替え・コピー、外観設定 | 対応する計画手順での作業指定・建築計画 |
| 作業指示 | 優先度一覧 | 優先度の設定 | 採掘、モップ掛け、片付け、消毒、キャンセル、収穫、解体、捕獲、配管の中身の取り出し・切断 |
| インフラ | 電力概要・ポート、部屋、配線・配管マップ | 対応する設定の変更 | 作業指示・建築ツールでの配線・配管作業の指定 |
| ワールド | ワールド、セル、元素、マップ、熱リスク、領域 | 領域の定義・統合・登録解除 | 作業指示・建築計画によるマップ変更 |
| カメラ | 現在のビュー取得 | なし | 移動、フォーカス、オーバーレイ、スクリーンショット |
| ゲーム | 時刻、セーブデータ、DLC・状態の読み取り | 対応する状態・DLC の設定 | 一時停止、再開、速度変更、セーブ・ロード・終了 |
| サーバー | manifest、search、guide、coverage、diagnostics | なし | 集約ツールのバッチ呼び出し |
| 仮想ファイル | pwd、ls、read、zoom、search、symbols | edit、blueprint | 確認済みの edit・batch |

## プロンプトによる手順

| プロンプト | 使用する場面 | 推奨される呼び出し順 |
|---|---|---|
| `colony_triage` | 健全性を手早く確認する | colony status → diagnostics → alerts → food |
| `next_cycle_plan` | 短期的な計画を立てる | colony summary → inventory → research → schedules → dupes |
| `inspect_area` | 空間配置を調べる | ワールドのテキストマップ → 対象領域のスナップショット・詳細 |
| `dupe_care_review` | 複製人間の状態を点検する | dupes → schedules → detail/needs/attributes |
| `power_audit` | 電力を点検する | 電力概要 → 必要に応じて対象ポート・設定の詳細 |
| `rooms_overview` | 部屋の整備状況を確認する | 部屋一覧 → 種類・大きさによる絞り込み |
| `thermal_audit` | 熱リスクを調べる | 過熱リスク → 元素や温度マップの詳細 |

プロンプトは呼び出し手順を説明するもの。リソースやツールの呼び出しは別途行う。

## リソース URI

| URI | 対応するツール | 用途 |
|---|---|---|
| `oni://colony/status` | `colony_control domain=read action=status` | 基本状態 |
| `oni://colony/diagnostics` | `colony_control domain=diagnostic action=diagnostics` | 診断された問題 |
| `oni://colony/alerts` | `colony_control domain=diagnostic action=alerts` | 現在の警告 |
| `oni://colony/summary` | `colony_control domain=report action=summary` | 計画用の概要 |
| `oni://resources/inventory` | `read_control domain=resources action=inventory` | 在庫量 |
| `oni://resources/food` | `read_control domain=resources action=food` | 食料と消費期限 |
| `oni://power/summary` | `read_control domain=infrastructure action=power_summary` | 電力回路の健全性 |
| `oni://rooms/list` | `read_control domain=infrastructure action=rooms` | 部屋の整備状況 |
| `oni://thermal/overheat-risk` | `read_control domain=world action=thermal_overheat_risk` | 熱リスクの順位 |
| `oni://world/elements` | `read_control domain=world action=element_summary` | 元素の質量・温度 |
| `oni://world/text-map` | `read_control domain=world action=text_map` | 地形とオーバーレイ |
| `oni://dupes` | `colony_control domain=read action=dupes` | 名簿 |
| `oni://schedules` | `colony_control domain=management kind=schedule action=list` | スケジュール |
| `oni://research/status` | `colony_control domain=management kind=research action=status` | 研究の状態 |
| `oni://tools/manifest` | `server_control domain=catalog action=manifest` | 公開ツールのカタログ |
| `oni://tools/search` | `server_control domain=catalog action=search` | 条件に応じたツールの検索 |

テンプレートによってはクエリパラメーターを指定できる。例: `oni://power/summary?worldId=2&includeDetails=true`。

## 早見表

| 状況 | 最初の呼び出し | 次に行うこと | 検証 |
|---|---|---|---|
| 何が起きているか知りたい | minimal スナップショット | 問題が示されたドメインを絞って読む | minimal・brief スナップショット |
| 電力の問題に対処したい | 電力概要 | 電力ポート、または設定・建築計画 | 電力概要 |
| 何かを建築したい | 定義・素材の検索 | 候補と意味ベースの計画、または正確なマップパッチ | マップ・設備の再読み取り |
| 複製人間を管理したい | 名簿 | detail・needs を確認し、範囲を限定して更新 | 名簿・詳細 |
| 熱を確認したい | 過熱リスク | 元素や温度のマップ | 同じリスク領域 |
| 操作の計画を立てたい | カタログの guide | search とツールの dry-run | 関連するリアルタイムの読み取り |
| 設定をまとめて変更したい | バッチの dry-run | バッチの実行 | 影響を受けたドメイン |
| ツールを探したい | カタログの search | guide または manifest | 安全性が重要な場合は static audit |
| 領域単位で操作したい | 領域の定義・読み取り | 対象範囲を限定した意味ベースの操作 | 領域の読み取り |
| カメラを操作したい | ビュー取得 | 必要な場合のみ移動・フォーカス | ビュー取得 |
| 研究を確認したい | 研究の状態 | 研究一覧 | 研究の状態 |
| ロケットを調べたい | ロケットの状態 | ロケットの詳細 | ロケットの状態 |
| 収納を管理したい | 収納の一覧・詳細 | フィルターの設定 | 収納の詳細 |
| 自動化を管理したい | 自動化の一覧 | 対応する自動化機能の設定 | 自動化の一覧 |

## エラーの対処

| エラー | 次の対応 |
|---|---|
| パラメーターが不足している・無効 | カタログを検索し、現在のスキーマに従う |
| 対象が見つからない | 名簿・設備・オブジェクトの一覧を更新する |
| 到達可能な対象がない | 複製人間の状態と正確な経路範囲を確認する。許可なく救助を強行しない |
| 確認が必要 | dry-run を行い、許可を取得・確認してから confirm を指定する |
| タイムアウト | 返されたタスクの状態を確認するか、対象範囲を縮小する |
| サンドボックス・即時建築で研究が未解除と判定される | 実行中の DLL が古いものとして扱う。テストのために研究を予約しない。後で安全に再ロードする必要があると伝える |
