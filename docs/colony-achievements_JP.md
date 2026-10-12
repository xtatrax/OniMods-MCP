# コロニー実績一覧の読み取り

`world_editor action=read path=/active/colony/achievements.oni` で、現在ロード済みコロニーの実績を取得します。Steamアカウント全体の解除履歴とは別です。

- `achievementDetail=brief`（既定）: ID・名称・状態・達成／失敗・勝利条件フラグ。
- `achievementDetail=full`: 説明と要求型を追加。進捗は再評価せず `null` / `not_evaluated_readonly`。
- `achievementStatus=all/completed/incomplete/failed/unknown`、`query`（ID・名称部分一致）、`limit`（1〜200、既定brief100／full10）で必要な範囲だけ取得。
- `counts` は現在のセーブに適合する全定義の件数。`matched` は絞り込み後、`returned` と `truncated` は件数制限後です。

`SaveGame.ColonyAchievementTracker.achievements` のキャッシュ済み `success` / `failed` を直接参照します。定義に対応する状態がない場合は `unknown` とし、未達成や正常値で代用しません。無効な定義と現在のセーブに適合しない定義は除外します。キャッシュの最終更新時刻は不明であり、`observedCycle` は読取時刻のサイクルです。

本体 `IsAchievementUnlocked` は未達成状態で `UpdateAchievement` を呼ぶため使用しません。`CheckAchievements`、要求の `Success` / `Fail` / `GetProgress`、解除・通知・UI生成も呼びません。要求別の現在進捗表示は、安全な取得経路を確認する追加作業です。sandbox／debugによるプラットフォーム実績無効化と、コロニーの保存済み達成記録も同一視しません。

取得例:

```json
{"action":"read","path":"/active/colony/achievements.oni","achievementStatus":"completed","task":"達成済みのコロニー実績を確認"}
```

## Steamアカウント側の実績

`world_editor action=read path=/active/colony/steam_achievements.oni` で、現在ログインしているSteamユーザーの、このゲームの解除状態を取得します。Steamカタログを列挙するため、現在のコロニーでは対象外のDLC実績も含みます。コロニーの保存済み達成状態とは別の一覧です。

簡易版はAPI ID・Steam表示名・解除状態・解除日時（Unix秒）を返します。詳細版は説明・hiddenフラグ・対応するコロニー実績IDを追加します。名前と説明の言語はSteam側の表示属性に従います。`achievementDetail` / `query` / `limit` はコロニー一覧と同じです。状態フィルターは `all/completed/incomplete/unknown` で、Steamに失敗状態がないため `failed` は拒否します。

`GetAchievementAndUnlockTime` が失敗したときは `unlocked=null` / `status=unknown`。Steamプラットフォーム未初期化・カタログ未取得・例外の場合は `available=false` と理由、集計値はnullを返します。解除済みでも時刻0は日時未記録であり、1970年の解除として扱いません。部分読取後の例外では不完全な一覧を破棄します。

本体が初期化したSteamクライアントのキャッシュを使用し、`RequestCurrentStats`、コールバック実行、`SetAchievement`、`ClearAchievement`、`StoreStats`、リセット、外部Web APIは呼びません。解除情報のバックエンド同期時刻は不明です。SteamのキーやユーザーIDの指定は不要です。

取得APIの根拠: [Steamworks ISteamUserStats](https://partner.steamgames.com/doc/api/ISteamUserStats#GetAchievementAndUnlockTime)。ゲーム同梱Steamworks DLLのメソッド署名と、本体のプラットフォーム初期化判定も確認しています。Steam画面との全項目照合は未実施です。
