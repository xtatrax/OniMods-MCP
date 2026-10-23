# 技能の対象モデルと通常UI経路

`dupes_control domain=skill` は、既存のDBを読む技能定義catalogと、対象を指定した候補・習得履歴を分ける。DBが未生成の場合、読取のためにDb.GetやInitializeを呼ばず利用不能とする。

## 一覧と取得不能

対象なしの `action=list` は従来の非deprecated定義を返し、`requiredDuplicantModel` を本体値のまま添える。対象ありでは、スキル画面と同じPersonality.modelと必要モデルのnative Tag比較を使い、既知不適合かつ未習得の行だけをquery・件数制限より前に除外する。技能名やBionicSkillsグループからモデルを推測しない。件数は返却行数であり、全件数や全UI許可の保証ではない。

| 項目 | 意味 |
| --- | --- |
| `targetModel` / `modelSource` | 既存Personality.modelの値と根拠 |
| `requiredDuplicantModel` | native定義。nullを全モデル可へ補わない |
| `modelMatch` | nativeのモデル比較。必要モデルがnullなら未判定のnull |
| `candidateScope` | モデル一致／不適合、モデル未判定、または既習得履歴の区別 |
| `mastered` / `granted` | 現在取得できた本体の習得・外部付与の履歴 |
| `canMaster` / `conditions` | native GetSkillMasteryConditions→CanMasterSkillの結果。完全なUI適合・未習得・将来の習得成功ではない |
| `masteryStatus` / `masteryReasonCode` | 条件読取の欠測理由。nullをfalseへ補わない |

モデル不適合の詳細では、取得済みの `mastered=false` は `model_mismatch`、習得履歴を取得できないnullは `mastery_unknown_model_mismatch`、既習得trueは `mastered_history_model_mismatch` とする。scopeラベルを変えても、一覧の除外やnative UIの実行ガードは変更しない。

既習得の不適合技能は消さず、`mastered_history_model_mismatch` として履歴を残す。必要モデル未判定の既習得行も履歴と未判定を分ける。管理Markdownの列も「本体の習得条件」とモデル一致を別に表示し、「習得可能」と言い換えない。

id/nameの指定が解決できない場合、無指定catalog成功へ落とさない。id未発見からnameへの補完、id/nameの不一致、同名の曖昧さを拒否する。DB・対象・resume・personality/model等が取得できない一覧は利用不能を返し、`returned` と `skills` はnullとする。管理JSON/Markdownもそのエラーを空の成功表へ変えない。

## 現在の正式な呼出経路

簡易ツール面で `dupes_control` は内部操作IDであり、直接のtools/callに公開されているとは限らない。現在の正式な窓口では、`world_editor command=read path=/active/management/skills.md format=json` にid/name/query/limitを付ける。`includeUi=true` は必要な場合だけ添える。住民とポイントの一覧は `/active/management/dupes.md` のJSON読取を使う。リソース `oni://dupes/skills` も一覧専用であり、UI表示や習得へ転用しない。

明示的なUI表示・通常習得は `/active/ops/dupes.md` への単一コマンドで行う。たとえば `call domain=skill action=open id=17 confirm=true detail=true`、または `call domain=skill action=learn id=17 skillId=Mining1 confirm=true detail=true`。world_editor側にもtaskとconfirm=trueを渡し、実行時だけdryRun=falseとする。例のIDは利用者が観測した正確な対象へ置き換える。detail=trueは、操作結果の本体JSONを保持するための指定である。

正式opsのdryRunは外側の予行で停止し、内部の技能handlerを呼ばない。従って、その応答に内部handlerのcached widget観測が含まれるとは限らず、内部handler直のdryRun試験と同一視しない。通常world_editorの既存scopeが実行中だけInstantBuildを無効にし終了時に復帰する場合も、元のglobal設定がfalseだった証明とは分ける。

## 任意の既存UI観測

一覧の `includeUi=true` だけで、既存の対象一致スキル画面と返却行のwidget状態を添える。既定false。読取で画面を開く・選択する・Refreshする操作は行わない。閉鎖、別対象、未取得を区別し、widget辞書への存在だけを表示や習得許可としない。管理読取からも同じflagを転送する。

必要なら `action=open confirm=true` がManagementMenu.OpenSkills(target)を一度呼び、通常UIを開く。`dryRun=true` では開かない。単一技能の `action=learn dryRun=true` は、モデル不適合で一覧から除外される技能でも、その技能と既存widgetの観測を返す。画面表示・クリック・習得・付与はしない。

UI観測のvisibleは、選択対象と親画面・widget ID・activeSelf/activeInHierarchyを照合した現在のcache情報であり、nativeの習得可能boolや全画素の可視性ではない。必要モデルnullを「全モデル可」とする根拠にも使わない。

## 通常習得と明示的な外部付与

実行にはconfirmが必要。通常learnはInstantBuildModeをfalseと確認し、ManagementMenu.OpenSkillsで正確な対象を開く。選択target、menuの画面、画面/子widgetの有効状態、親skillsScreen、実skillIDを照合してから、非nullのLeft PointerEventDataを1回だけnative OnPointerClickへ渡す。ポイント・前提・職業等の規則は本体へ任せる。

入力disabled、確認不能な画面stack、対象スキル画面より手前の別modal、EventSystem不在等は拒否する。スキル画面自身のmodalは別modalとして数えない。stack観測は本体KButton routingの根拠であり、任意のpointer raycast到達性の一般証明ではない。

native操作後は習得を再取得し、未習得のままなら成功としない。画面操作後の拒否や例外はpartialとUI呼出有無を返し、再試行・直接MasterSkill・隠れたwidget・ポイント追加へのfallbackは行わない。`changed` の `changeScope=skill_mastery` と、UIを開いたことを表す `uiInvoked` は分ける。

従来の `force=true` は明示的な外部GrantSkill経路として別に残す。通常UIが失敗した場合のfallbackではない。forceでもdryRunはGrantSkillを呼ばない。hatや既存force付与の意味を、この変更で通常UI規則へ読み替えない。

## 翻訳と検証範囲

独自文言のPO contextは `OniMcp.MCP.SKILL_MODEL.<KEYの大文字形>`、現行runtimeキーは `skill_model.<key>`。例: `OniMcp.MCP.SKILL_MODEL.TABLE_TARGET_HEADER` → `skill_model.table_target_header`。PO4ファイルとruntimeのskill_model.en/ja/zh-Hans.jsonを対応させる。暫定zh-hant.poと現行zh-Hansの訳は別に保持し、後者は各PO項目のRuntime zh-Hans注記へ記録する。言語マッピングやPOローダーは変更しない。

実ソースをリンクした専用ホスト127条件、管理表示233条件と関連既存群を確認した。旧製品全文の負試験はコンパイル後にモデル除外の条件で失敗し、型衝突を含む境界fixtureとゲームDLL参照ビルドも確認した。これらはnative実機の習得成功や全UIの証明ではない。開発側の正式経路で、初回と通常再読込の一覧・モデル別UI・外側予行を照合した。別名保存した試験分岐では通常モデルとバイオニックの各1技能を通常UIで習得し、各1ポイント消費と習得状態の変化を確認した。両モデルの不適合技能は、本体条件がtrueでも対象一致のcached widgetが非表示で、クリック・付与・ポイント消費なしに拒否した。停止時刻と他の住民の状態を保持した。公開側も固有ID・独立DLLで同じ初回／通常再読込と、各モデルの通常UI習得・非表示拒否を受入れた。元の導入物・設定・他MOD／DLC・選択セーブと開始時の未起動状態へ復帰し、原本と試験分岐を保持した。再読込は保護した入力の再読込であり、習得後分岐の再ロードによる永続化確認とは別である。実欠測・必要モデルnull・任意のpointer raycast・明示force付与・内部handler直dryRunの実機境界は未受入のまま残す。model-filterを現在DLCを含む全UI許可と呼ばず、全GUI・全MOD・将来状態の保証へ広げない。


既知falseのラベル修正は実ソースのホスト128条件で確認し、旧ソースはコンパイル成功後に追加条件で失敗した。開発・公開の別IDで初回と通常再読込後に、通常型とバイオニックの不適合技能を通常UI経路へ各一度要求し、既知false・modelMatch=false・model_mismatchと非表示widgetによる無クリック拒否を確認した。全住民のポイント・習得履歴、停止時刻、原本と既存ファイルを保持し、保存や時間進行は要求していない。null欠測と既習得trueの不適合履歴はホスト対照であり、この追加試験の実機受入には含めない。
