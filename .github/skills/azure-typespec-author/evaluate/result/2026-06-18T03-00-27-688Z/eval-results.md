## Eval Results

> Timestamp: 2026-06-18T03:02:22.958Z

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\003001.eval.yaml

| Stimulus | Environment | Skills | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 003001-arm-action-lro-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/003-long-running-operation-share/employee.tsp` → `employee.tsp`, `../fixtures/003-long-running-operation-share/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/003-long-running-operation-share/readme.md` → `readme.md`, `../fixtures/003-long-running-operation-share/shared.tsp` → `shared.tsp`, `../fixtures/003-long-running-operation-share/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | `azure-typespec-author` | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 1m 43s | 412,511 | ✅ |

> Model: claude-opus-4.6
