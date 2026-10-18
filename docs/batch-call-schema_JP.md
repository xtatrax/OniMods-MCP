# バッチ呼出配列の要素型

`server_control domain=batch action=call_many`の`calls`と別名`items`に、`items: {type: object}`を公開する。実行側が従来要求しているobject形式と、ツールの公開スキーマを揃えた。要素型はRegistry、翻訳ごとの独立コピー、fullカタログ、opsのToolSummaryでも保持する。未指定の配列や他の型は変更しない。

要素内の別名やargumentsの互換性、最大20件、requireAllValid、確認と予行、既定の小さい概要応答はそのまま。現在game_controlの時刻読取もdangerous分類により子のconfirm=trueを必要とする。外側confirmは自動継承されず、defaults.confirmより子のfalseが優先される。外側batchのdryRunはHandler呼出0回、子のdryRunは予行Handlerを呼ぶ場合があり、呼出回数とゲーム変更数を区別する。

## 検証範囲

新規ホスト760条件（Registry投影・コピー560、入力・確認188、手動metadata12）と既存Core/Tools/OperationResults回帰が成功。Coreはツールfactoryとゲーム境界を代替するため、実定義の確認は実機と分けている。投影欠落・浅いコピー・無関係な配列の一律object化の3変異を検出した。

公開側の既存Core試験で不足していたポリシー参照と最小のゲーム／設定代替定義を補い、Coreは14グループで確認した。開発専用の施工進捗試験は取り込まない。ゲームDLL参照Compileは警告0・エラー0。オンラインNuGet監査の接続失敗を受け、今回のコマンドだけ当該照会を省略した。設定変更や依存脆弱性監査の成功を意味しない。

開発IDの実機30条件で3窓口のItemsと他metadata不変、object実行・無効型拒否・確認・予行・上限・停止保持を確認した。公開IDでの変更後実機受入は未実施。個別セーブ・ID・座標・応答・ログ・クライアント設定は公開へ含めない。
