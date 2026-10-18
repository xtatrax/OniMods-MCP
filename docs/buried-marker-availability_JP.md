# 埋蔵マーカーの集計範囲と欠測

地図の通常ビューを非compactで読んだ場合の補足だけを変更する。セル単体のpresent／absent／not_solid／unobserved／unavailableは維持し、生成・採掘・公開範囲の拡大や診断は行わない。

| availability | markerCount | 意味 |
| --- | --- | --- |
| observed | 0以上 | 全要求セルが観測済み。可視固体は本体の結果、開放セルは対象外として区別 |
| partial | null | 観測済みセルと未観測／取得不能セルが混在 |
| unobserved | null | 全要求セルが可視性・active worldの範囲外 |
| unavailable | null | 有効world外や取得失敗などで観測済み状態がない |
| not_applicable | null | 要求が空、または全セルが観測済み開放セルで埋蔵判定の対象外 |

markerCountはMarkdown内の値で、不明は文字列のnullとして明示する。presentは確認済み件数で、不明セルを含む場合は下限値。requestedをpresent／absent／not_solid／unobserved／unavailableに分配し、readableは可視かつactive world内のセル数を別に示す。本体コンポーネント欠損時も可視セルという根拠は保持する。

座標は観測済みpresentのみ最大32件。omittedはその確認済み件数から算出し、未観測の埋蔵数を推測しない。未知範囲でcells=noneを返さずunknown、全開放はnot_applicableとする。未訪問というフラグだけでセルを非表示と決めない。

各セルの既存world・可視性検査を先に行い、非表示・他worldでは固体判定やマーカー取得へ進まない。可視固体につき本体IsEntombedItemを最大1回読み、取得失敗はunavailableとする。キャッシュの再評価・再試行・UI初期化・ゲーム進行は追加しない。

表示文はPOのOniMcp.MCP.BURIED_MARKERS.SUMMARYをburied_markers.summaryへ対応させ、現行3言語JSONにも保持する。zh-hant.poの暫定扱いと、既存zh-Hans実行文を分離する方針は維持する。検査はscripts/verify_buried_marker_localization.py。

## 検証と限界

公開側で実ソース連結ホスト204条件・既存Tools回帰・翻訳1キーの4 PO／3実行JSON照合、ゲーム同梱DLL参照Compile（警告0／エラー0）が成功した。依存のオンライン監査は実行単位で省略する。

開発IDでは代表条件の実機99条件を確認した。公開IDでの変更後実機受入は未実施で、開発版の結果を公開版の実機確認と扱わない。欠損コンポーネント・読取例外・同worldの非表示・座標32件上限はホスト試験の範囲。個別のセーブ・座標・実機応答・ログ・設定を含めない。
