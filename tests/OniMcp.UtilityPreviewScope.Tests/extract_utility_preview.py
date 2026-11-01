from pathlib import Path
import sys,json,hashlib
source=Path(sys.argv[1]);out=Path(sys.argv[2]);registry=Path(sys.argv[3]);s=source.read_text();start=s.index("        public static McpTool AutoConnectUtility()");next_method=s.find("\n        public static McpTool ",start+1);end=next_method if next_method>=0 else s.rindex("    }\n}\n");body=s[start:end]
assert body.rstrip().endswith("}")
out.write_text("using System;using System.Collections.Generic;using System.Linq;using Newtonsoft.Json;using Newtonsoft.Json.Linq;using OniMcp.Core;using OniMcp.Support;namespace OniMcp.Tools{public static partial class BuildPlanningTools{\n"+body+"\n}}\n")
out.with_suffix(".json").write_text(json.dumps({"source":str(source),"sourceSha256":hashlib.sha256(source.read_bytes()).hexdigest(),"startLine":s[:start].count("\n")+1,"endLine":s[:end].count("\n"),"methodSha256":hashlib.sha256(body.encode()).hexdigest()},indent=2))

r=registry.read_text();i=r.index("    public class McpTool\n");dto=r[i:r.rindex("}")];dtofile=out.with_name("ActualUtilityToolTypes.g.cs");dtofile.write_text("using System;using System.Collections.Generic;using System.Globalization;using Newtonsoft.Json.Linq;using OniMcp.Core;namespace OniMcp.Tools{\n"+dto+"\n}\n");dtofile.with_suffix(".json").write_text(json.dumps({"source":str(registry),"sourceSha256":hashlib.sha256(registry.read_bytes()).hexdigest(),"startLine":r[:i].count("\n")+1,"bodySha256":hashlib.sha256(dto.encode()).hexdigest()},indent=2))
