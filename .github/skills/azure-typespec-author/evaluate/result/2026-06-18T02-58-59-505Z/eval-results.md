## Eval Results

> Timestamp: 2026-06-18T03:04:44.993Z

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002008.eval.yaml

| Stimulus | Environment | Skills | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 002008-ARM-add-parameters-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | `azure-typespec-author` | ✅ tool-calls 1/1<br>❌ file-matches 0/1<br>❌ file-not-matches 0/1 | 0/1 | 4m 07s | 1,208,720 | ❌ <a href="#user-content-fn-1" id="ref-1">[1]</a> |

<a href="#user-content-ref-1" id="fn-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`, `file-not-matches`


> Model: claude-opus-4.6
