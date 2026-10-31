#!/usr/bin/env python3
"""Validate diagnostic-scope PO sources and runtime catalogs without rewriting them."""
import argparse,json,re
from pathlib import Path
from verify_operation_file_localization import read_po,require,PLACEHOLDERS,unique_object

def check(root):
    base=root/"mods/OniMcp/Localization"
    sources={name:read_po(base/"po"/name) for name in ("onimcp.pot","en-us.po","ja-jp.po","zh-hant.po")}
    template=sources["onimcp.pot"]
    for name,records in sources.items():
        require(records.keys()==template.keys(),"PO contexts differ: "+name)
        for context,value in records.items():
            require(value["msgid"]==template[context]["msgid"],"PO source differs: "+context)
            if context and value["msgstr"]:
                require(sorted(PLACEHOLDERS.findall(value["msgstr"]))==sorted(PLACEHOLDERS.findall(value["msgid"])),"PO placeholders differ: "+context)
    catalogs={lang:json.loads((base/"Catalogs"/("diagnostic_scope."+lang+".json")).read_text(),object_pairs_hook=unique_object) for lang in ("en","ja","zh-Hans")}
    keys=set(catalogs["en"])
    suffixes={"summary.suffix","summary.not_evaluated_suffix","diagnostics.description","alerts.description","resource.diagnostics.title","resource.diagnostics.description","resource.alerts.title","resource.alerts.description","snapshot.description","snapshot.include_alerts"}
    require(keys=={"diagnostic_scope."+suffix for suffix in suffixes},"Expected exact10 diagnostic scope keys")
    source_paths=("Tools/Impl/World/SnapshotMetrics.cs","Tools/Impl/World/SnapshotTools.cs","Tools/Impl/Colony/DiagnosticsTools.cs","Tools/Core/OniResourceRegistry.cs")
    used=set()
    for path in source_paths:used.update(re.findall(r'"(diagnostic_scope\.[A-Za-z0-9_.]+)"',(root/"mods/OniMcp"/path).read_text()))
    require(used==keys,"Source uses missing or orphan scope keys")
    require(all(set(value)==keys for value in catalogs.values()),"Runtime keys differ")
    expected={"OniMcp.MCP.DIAGNOSTIC_SCOPE."+key.removeprefix("diagnostic_scope.").upper() for key in keys}
    require({key for key in template if key.startswith("OniMcp.MCP.DIAGNOSTIC_SCOPE.")}==expected,"PO scope contexts differ")
    for key in keys:
        context="OniMcp.MCP.DIAGNOSTIC_SCOPE."+key.removeprefix("diagnostic_scope.").upper()
        require(template[context]["msgstr"]=="","POT has a translation")
        require(catalogs["en"][key]==template[context]["msgid"]==sources["en-us.po"][context]["msgstr"],"English mapping differs")
        require(catalogs["ja"][key]==sources["ja-jp.po"][context]["msgstr"],"Japanese mapping differs")
        for name,records in sources.items():
            marker="#. Runtime zh-Hans msgstr: "
            comments=[x[len(marker):] for x in records[context]["comments"] if x.startswith(marker)]
            require(len(comments)==1 and json.loads(comments[0])==catalogs["zh-Hans"][key],"Hans source differs")
            if name!="onimcp.pot":require(bool(records[context]["msgstr"]),"PO translation missing")
        require(all(bool(value[key]) for value in catalogs.values()),"Runtime translation missing")
        tokens=r"oni://[A-Za-z0-9/-]+|colony_control domain=diagnostic action=[a-z]+|includeAtmosphere=true"
        expected_tokens=sorted(re.findall(tokens,catalogs["en"][key]))
        texts=[catalog[key] for catalog in catalogs.values()]+[sources["zh-hant.po"][context]["msgstr"]]
        require(all(sorted(re.findall(tokens,text))==expected_tokens for text in texts),"Reserved URI or control syntax differs")
    print("Diagnostic scope: 10 keys, PO4/runtime3 and unchanged global PO guards validated.")
if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument("--root",type=Path,default=Path(__file__).resolve().parents[1]);check(parser.parse_args().root)
