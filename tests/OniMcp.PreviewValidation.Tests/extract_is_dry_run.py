import sys,hashlib,json
from pathlib import Path
src=Path(sys.argv[1]);out=Path(sys.argv[2]);text=src.read_text();start=text.index("        private static bool IsDryRun(");opening=text.index("{",start);depth=1;end=opening+1
while depth:
 if text[end]=="{":depth+=1
 elif text[end]=="}":depth-=1
 end+=1
body=text[start:end];out.parent.mkdir(parents=True,exist_ok=True);out.write_text("using Newtonsoft.Json.Linq;namespace OniMcp.Tools {public static partial class BuildPlanningTools {\n"+body+"\n}}\n")
out.with_suffix(".source.json").write_text(json.dumps({"sourceSha256":hashlib.sha256(src.read_bytes()).hexdigest(),"methodSha256":hashlib.sha256(body.encode()).hexdigest()},indent=2))
