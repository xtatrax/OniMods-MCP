# フィルター変更の未対応予行を拒否する

`building_control domain=filter action=set` の single/tree/flat と、`domain=storage action=set_filter` は実設定の変更窓口であり、配置・適合性の予行を提供しない。従来は `dryRun=true` を無視して実更新していた。対象検索より前の共有ガードで、予行要求を明示的に拒否する。正規の集約入口と、同じ書込葉へ到達する互換入口を対象にする。

## 応答の契約

- JSON真偽値の `dryRun=true` は `isError=true`、`reasonCode=unsupported_preview`。`previewSupported=false`、`nativeInvoked=false`、`changed=false` を返す。
- 真偽値以外のdryRun（文字列・数値・null・配列・object）は `reasonCode=invalid_boolean` で拒否する。入力値そのものはエラーへ転載しない。
- 省略またはJSON真偽値falseは、既存の通常変更を実行する。確定操作を行う意図がある場合にだけ使用する。nativeのタグ選択・更新方法や既存の要求ガードは変更しない。
- list/detailは読取のまま。未対応予行の拒否は、対象の存在・タグ適合性・到達性を検証したことを意味しない。現在値の確認には読取窓口を使う。

判定はSingleFilterのSelectedTag操作、Tree/Flat更新、独立したStorageフィルター更新の3つの書込入口に置く。targetやnative状態を先に読んで見かけ上の予行結果を作らず、UIの開閉・直接の状態復元を追加しない。文言はPO正本とruntimeカタログの `filter_preview.*` 3キーへ分離した。

## 検証と限界

公開版自身の製品FilterTools・StorageTools・共通ガード・正規buildingルーターを接続したホストで1158チェックを確認した。全3kindと互換入口、非Boolean、対象検索前の拒否、通常変更、既存収納・現物・複数収納の読取を含む。代替native境界によるホスト確認と実ゲームの実行は区別する。公開側PO/runtimeの新規3キーも、元のPO内容を保持して146検査を通過した。

この公開IDと分離設定を使う独立した実機検証では、空のTreeFilterable収納・既存の単一Filterableを使った。tree/single/独立storageの予行3経路が拒否され、対象の全Storage内容・選択タグ・観測した隣接収納を独立再読取で照合した。省略/falseを使う通常変更と元選択への復帰6段階も確認し、試験中の停止時計を保持した。参照ビルドは警告・エラーとも0件。

FlatTagFilterableの実機正例は取得できず、ホストの代替境界に限る。シミュレーションの進行・運搬・搬送、全フィルター設備や全操作の予行対応を受入れた意味ではない。個別セーブ、実体ID、座標、時刻、生応答、画面、導入物や私有ログは公開差分に含めない。
