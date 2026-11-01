#!/usr/bin/env python3
"""Extract exact source ranges; no copied production algorithms in test files."""
from pathlib import Path
import argparse,hashlib,json,re,sys
p=argparse.ArgumentParser()
for key in ['repo-root','registry','candidate','anchors','output']:p.add_argument('--'+key,required=True,type=Path)
p.add_argument('--layout',choices=['split','inline'],required=True);a=p.parse_args()
sys.path.insert(0,str(a.repo_root/'scripts'))
from onimcp_verify_parsing import matching_delimiter
slices=[];records=[];absences=[]
def capture(path,pattern,kind,name,optional=False):
 data=path.read_bytes();text=data.decode();matches=list(re.finditer(pattern,text))
 if not matches and optional:
  absences.append({'source':str(path),'sourceSha256':hashlib.sha256(data).hexdigest(),'kind':kind,'name':name});return
 if len(matches)!=1:raise SystemExit('Expected one exact '+name)
 start=matches[0].start();opening=text.index('{',matches[0].end());end=matching_delimiter(text,opening,'{','}')+1
 source=text[start:end];lo=len(text[:start].encode());hi=len(text[:end].encode());assert data[lo:hi]==source.encode()
 slices.append(source);records.append({'source':str(path),'sourceSha256':hashlib.sha256(data).hexdigest(),'kind':kind,'name':name,'byteStart':lo,'byteEndExclusive':hi,'sliceSha256':hashlib.sha256(source.encode()).hexdigest()})
for name in ['McpTool','McpToolParameter']:
 capture(a.registry,r'(?m)^    public class '+name+r'\b','protocol_tool_type',name)
tool_types='\n\n'.join(slices);slices.clear()
capture(a.anchors,r'(?m)^        private static bool IsDryRun\s*\(','control_helper','IsDryRun')
if a.layout=='inline':
 for name in ['FindPlacementCandidates','HasExplicitUnknownPlacementEvidence','HasPlacementToken']:
  capture(a.candidate,r'(?m)^        (?:private|public) static[^\n]*\b'+name+r'\s*\(','candidate_method',name,name!='FindPlacementCandidates')
header='// Exact production ranges; adjacent provenance JSON pins all bytes.\nusing System;\nusing System.Globalization;\nusing System.Collections.Generic;\nusing System.Linq;\nusing Newtonsoft.Json;\nusing Newtonsoft.Json.Linq;\nusing OniMcp.Core;\nusing OniMcp.Support;\nnamespace OniMcp.Tools {\n'
output=header+tool_types+'\npublic static partial class BuildPlanningTools {\n'+'\n\n'.join(slices)+'\n}}\n'
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(output)
a.output.with_suffix('.ranges.json').write_text(json.dumps(records,indent=2)+'\n')
a.output.with_suffix('.absences.json').write_text(json.dumps(absences,indent=2)+'\n')
