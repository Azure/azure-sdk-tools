## Eval Results

> Timestamp: 2026-06-18T03:12:23.541Z

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004001.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|
| 004001-decorate-mgmt-resource-name-parameter-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>❌ file-matches 0/1<br>❌ prompt 0/1 | 0/1 | 7.4s | 0 | ❌ <a href="#user-content-fn-s1-1" id="ref-s1-1">[1]</a> |

<a href="#user-content-ref-s1-1" id="fn-s1-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`, `file-matches`, `prompt`


> Model: claude-opus-4.6

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004002.eval.yaml

| Stimulus | Environment | Skills | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 004002-decorate-length-constrains-on-array-item-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/004002-decorate-length-constrains-on-array-item/employee.tsp` → `employee.tsp`, `../fixtures/004002-decorate-length-constrains-on-array-item/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/004002-decorate-length-constrains-on-array-item/readme.md` → `readme.md`, `../fixtures/004002-decorate-length-constrains-on-array-item/shared.tsp` → `shared.tsp`, `../fixtures/004002-decorate-length-constrains-on-array-item/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | `azure-typespec-author` | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ file-not-matches 1/1<br>✅ prompt 1/1 | 1/1 | 1m 42s | 449,611 | ✅ |

> Model: claude-opus-4.6

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004003.eval.yaml

| Stimulus | Environment | Skills | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 004003-delete-and-restore-operationId-decorator-forced | <details><summary>13 files · 1 command · 1 MCP server</summary>Files: `../fixtures/004003-delete-and-restore-operationId-decorator/BastionHost.tsp` → `BastionHost.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/BgpConnection.tsp` → `BgpConnection.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRouteGateway.tsp` → `ExpressRouteGateway.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRoutePort.tsp` → `ExpressRoutePort.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRouteProviderPort.tsp` → `ExpressRouteProviderPort.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/NetworkInterface.tsp` → `NetworkInterface.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/P2SVpnGateway.tsp` → `P2SVpnGateway.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/Route.tsp` → `Route.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/VpnServerConfigurationPolicyGroup.tsp` → `VpnServerConfigurationPolicyGroup.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/VpnSiteLinkConnection.tsp` → `VpnSiteLinkConnection.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/004003-delete-and-restore-operationId-decorator/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | `azure-typespec-author` | ✅ tool-calls 1/1<br>✅ file-not-matches 1/1<br>✅ file-matches 1/1 | 1/1 | 7m 15s | 3,249,315 | ✅ |

> Model: claude-opus-4.6
