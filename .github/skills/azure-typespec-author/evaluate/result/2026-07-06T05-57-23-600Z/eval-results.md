## Eval Results

> Timestamp: 2026-07-06T05:59:26.670Z

Evaluation suite for azure-typespec-author.

> **Environment**: 1 file · 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001001.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001001-version-spread-property-forced | <details><summary>8 files · 1 command · 1 MCP server</summary>Files: `../fixtures/instructions-test/copilot-instructions.md` → `.github/copilot-instructions.md`, `../fixtures/001-share-version-new-feature/employee.tsp` → `employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>❌ file-matches 0/1<br>❌ prompt 0/1 | 0/1 | 1m 44s | 49,900 | 2 | <details><summary>1 calls</summary>azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1</details> | ❌ <a href="#user-content-fn-1" id="ref-1">[1]</a> |

<a href="#user-content-ref-1" id="fn-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`, `file-matches`, `prompt`


> Model: claude-opus-4.6 | Executor: copilot-sdk
