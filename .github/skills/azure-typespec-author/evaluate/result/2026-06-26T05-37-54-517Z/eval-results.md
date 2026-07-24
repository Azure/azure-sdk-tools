## Eval Results

> Timestamp: 2026-06-26T05:40:42.833Z

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002011.eval.yaml

| Stimulus | Environment | Skills | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 002011-arm-add-check-existence-operation-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | `azure-typespec-author` | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 2m 35s | 378,013 | ✅ |

> Model: claude-opus-4.6
