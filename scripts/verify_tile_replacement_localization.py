#!/usr/bin/env python3
"""Validate native tile-replacement PO4/runtime3 mappings without rewriting them."""
from pathlib import Path
import argparse,json,re
from verify_operation_file_localization import PLACEHOLDERS,read_po,require,unique_object

SUFFIXES=("permission_required","scope_unavailable","target_unverifiable","target_changed","candidate_rejected",
 "pending_conflict","marked_work_conflict","instant_mode_enabled","ui_unavailable","ui_busy","ui_initializing",
 "preview_unavailable","priority_unavailable","native_failed","cleanup_failed","priority_restore_failed",
 "queue_not_observed","observation_unavailable","protected_state_changed","terrain_unverifiable")
CONTEXT_PREFIX="OniMcp.MCP.TILE_REPLACEMENT."

def check(root):
    base=root/"mods/OniMcp/Localization"
    sources={name:read_po(base/"po"/name) for name in ("onimcp.pot","en-us.po","ja-jp.po","zh-hant.po")}
    template=sources["onimcp.pot"]
    contexts={CONTEXT_PREFIX+suffix.upper() for suffix in SUFFIXES}
    catalogs={lang:json.loads((base/"Catalogs"/("tile_replacement."+lang+".json")).read_text(),object_pairs_hook=unique_object)
        for lang in ("en","ja","zh-Hans")}
    keys={"tile_replacement."+suffix for suffix in SUFFIXES}
    require(all(set(catalog)==keys for catalog in catalogs.values()),"Tile-replacement runtime keys differ")
    for name,records in sources.items():
        require(records.keys()==template.keys(),"Global PO contexts differ: "+name)
        require({context for context in records if context.startswith(CONTEXT_PREFIX)}==contexts,"Tile-replacement PO contexts differ: "+name)
        for context,row in records.items():
            require(row["msgid"]==template[context]["msgid"],"PO source differs: "+context)
            if context and row["msgstr"]:
                require(sorted(PLACEHOLDERS.findall(row["msgstr"]))==sorted(PLACEHOLDERS.findall(row["msgid"])),"PO placeholders differ: "+context)
    for suffix in SUFFIXES:
        key="tile_replacement."+suffix;context=CONTEXT_PREFIX+suffix.upper()
        require(template[context]["msgstr"]=="","POT has a translation: "+key)
        require(catalogs["en"][key]==template[context]["msgid"]==sources["en-us.po"][context]["msgstr"],"English tile source/runtime differs: "+key)
        require(catalogs["ja"][key]==sources["ja-jp.po"][context]["msgstr"],"Japanese tile source/runtime differs: "+key)
        for name,records in sources.items():
            row=records[context]
            require("#. Runtime JSON key: "+key in row["comments"],"Tile runtime key mapping differs: "+key)
            marker="#. Runtime zh-Hans msgstr: "
            values=[json.loads(comment[len(marker):]) for comment in row["comments"] if comment.startswith(marker)]
            require(values==[catalogs["zh-Hans"][key]],"Separate Hans tile source/runtime differs: "+key)
            require(name=="onimcp.pot" or bool(row["msgstr"]),"Missing tile PO translation: "+key)
        for lang,catalog in catalogs.items():
            require(bool(catalog[key]) and sorted(PLACEHOLDERS.findall(catalog[key]))==sorted(PLACEHOLDERS.findall(catalogs["en"][key])),"Tile runtime text/placeholders differ: "+key)
        texts=[catalog[key] for catalog in catalogs.values()]+[sources["zh-hant.po"][context]["msgstr"]]
        tokens={"scope_unavailable":["Neutral"],"candidate_rejected":["Replaceable","CanReplace"],"terrain_unverifiable":["Solid=true","Foundation=false"]}.get(suffix,[])
        require(all(all(text.count(token)==catalogs["en"][key].count(token) for token in tokens) for text in texts),"Native tile contract tokens differ: "+key)
    source="\n".join((root/"mods/OniMcp/Tools/Impl/Build"/name).read_text() for name in
        ("BuildPlanningTileReplacement.cs","BuildPlanningTileReplacementState.cs","NativeTileReplacement.cs","NativeTileReplacementState.cs"))
    require('"tile_replacement." +' in source,"Tile reason localization prefix missing")
    quoted=set(re.findall(r'"([a-z_]+)"',source))
    require(set(SUFFIXES)<=quoted,"Tile reason code has no corresponding source use")
    return len(keys)

if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument("--root",type=Path,default=Path(__file__).resolve().parents[1])
    print("Native tile replacement: "+str(check(parser.parse_args().root))+" PO4/runtime3 keys validated.")
