"""Extract complete production members verbatim, never a copied implementation.

The build manifest identifies source hashes, byte ranges and generated hash. The
source override accepts a complete immutable old caller for a semantic negative.
"""
from pathlib import Path
import argparse, hashlib, json

def member(text, signature):
    assert text.count(signature) == 1, signature
    start = text.index(signature)
    opening = text.index("{", start)
    depth = 0
    state = "code"
    i = opening
    while i < len(text):
        ch = text[i]
        nxt = text[i:i+2]
        if state == "line":
            if ch == "\n": state = "code"
        elif state == "comment":
            if nxt == "*/": state = "code"; i += 1
        elif state == "verbatim":
            if nxt == '""': i += 1
            elif ch == '"': state = "code"
        elif state in ("string", "char"):
            if ch == "\\": i += 1
            elif ch == ('"' if state == "string" else "'"): state = "code"
        elif nxt == "//": state = "line"; i += 1
        elif nxt == "/*": state = "comment"; i += 1
        elif nxt == '@"': state = "verbatim"; i += 1
        elif ch == '"': state = "string"
        elif ch == "'": state = "char"
        elif ch == "{": depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                end = i + 1
                return text[start:end], len(text[:start].encode()), len(text[:end].encode())
        i += 1
    raise AssertionError("Unclosed member: " + signature)

def main():
    args = argparse.ArgumentParser()
    args.add_argument("--repo", required=True)
    args.add_argument("--source", required=True)
    args.add_argument("--output", required=True)
    a = args.parse_args()
    repo, caller, output = Path(a.repo).resolve(), Path(a.source).resolve(), Path(a.output).resolve()
    specs = [(caller, "        public static McpTool GetPowerSummary()")]
    room = repo / "mods/OniMcp/Tools/Impl/Build/RoomListingTools.cs"
    infra = repo / "mods/OniMcp/Tools/Impl/Build/PowerInfrastructureHelpers.cs"
    specs += [(room, signature) for signature in [
        "        private static CircuitAggregate GetCircuit(",
        "        private static Dictionary<string, object> PowerDeviceInfo(",
        "        private static Dictionary<string, object> PowerIssueInfo(",
        "        private static bool IsUnconnectedCircuit(",
        "        private static bool IsOperational("]]
    specs += [(infra, signature) for signature in [
        "        private static double Round(", "        private static void CellXY(",
        "        private sealed class CircuitAggregate"]]
    records, parts = [], []
    def extract(path, signature):
        data = path.read_bytes(); text = data.decode(); piece, start, end = member(text, signature)
        assert data[start:end] == piece.encode(), "Extraction must preserve original bytes"
        records.append({"source": str(path), "sourceSha256": hashlib.sha256(data).hexdigest(),
            "signature": signature.strip(), "startByte": start, "endByteExclusive": end,
            "memberSha256": hashlib.sha256(data[start:end]).hexdigest()})
        return piece
    for path, signature in specs: parts.append(extract(path, signature))
    registry = repo / "mods/OniMcp/Tools/Core/OniToolRegistry.cs"
    dto = [extract(registry, signature) for signature in ["    public class McpTool\n", "    public class McpToolParameter\n"]]
    code = "using System;\nusing System.Collections.Generic;\nusing System.Globalization;\nusing System.Linq;\nusing Newtonsoft.Json;\nusing Newtonsoft.Json.Linq;\nusing OniMcp.Core;\nusing OniMcp.Support;\nusing UnityEngine;\nnamespace OniMcp.Tools\n{\n    public static partial class PowerAndRoomTools\n    {\n" + "\n\n".join(parts) + "\n    }\n" + "\n\n".join(dto) + "\n}\n"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(code)
    manifest = {"extraction": "verbatim_complete_members", "repo": str(repo), "caller": str(caller),
        "output": str(output), "generatedSha256": hashlib.sha256(code.encode()).hexdigest(), "members": records}
    output.with_suffix(".manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")

if __name__ == "__main__": main()
