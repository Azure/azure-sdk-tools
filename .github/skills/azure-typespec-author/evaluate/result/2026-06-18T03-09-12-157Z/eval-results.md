## Eval Results

> Timestamp: 2026-06-18T03:25:49.969Z

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\005001.eval.yaml

| Stimulus | Environment | Skills | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 005001-warning-suppress-warning-forced | <details><summary>6 files · 1 command · 1 MCP server</summary>Files: `../fixtures/005001-warning-suppress-warning/main.tsp` → `main.tsp`, `../fixtures/005001-warning-suppress-warning/models.tsp` → `models.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/005001-warning-suppress-warning/stubs.tsp` → `stubs.tsp`, `../fixtures/005001-warning-suppress-warning/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | `azure-typespec-author` | ❌ tool-calls 0/1<br>✅ file-matches 1/1 | 0/1 | 3m 48s | 982,264 | ❌ <a href="#user-content-fn-1" id="ref-1">[1]</a> |

<a href="#user-content-ref-1" id="fn-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`


> Model: claude-opus-4.6
