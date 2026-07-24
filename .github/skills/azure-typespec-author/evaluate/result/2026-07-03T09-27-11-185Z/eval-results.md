## Eval Results

> Timestamp: 2026-07-03T09:27:14.300Z

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001001.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001002.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001003.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001004.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001005.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 001005-version-add-preview-after-preview-forced | <details><summary>24 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001005-version-add-preview-after-preview/employee.tsp` → `employee.tsp`, `../fixtures/001005-version-add-preview-after-preview/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001005-version-add-preview-after-preview/shared.tsp` → `shared.tsp`, `../fixtures/001005-version-add-preview-after-preview/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_Get_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_Update_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Operations_List_MaximumSet_Gen.json` → `examples/2021-10-01/Operations_List_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Operations_List_MinimumSet_Gen.json` → `examples/2021-10-01/Operations_List_MinimumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_Delete_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_Update_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Operations_List_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Operations_List_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Operations_List_MinimumSet_Gen.json` → `examples/2024-10-01-preview/Operations_List_MinimumSet_Gen.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s5-1" id="ref-s5-1">[1]</a> |

<a href="#user-content-ref-s5-1" id="fn-s5-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001006.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001007.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 001007-version-add-stable-after-preview-forced | <details><summary>27 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001007-version-add-stable-after-preview/employee.tsp` → `employee.tsp`, `../fixtures/001007-version-add-stable-after-preview/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001007-version-add-stable-after-preview/readme.md` → `readme.md`, `../fixtures/001007-version-add-stable-after-preview/shared.tsp` → `shared.tsp`, `../fixtures/001007-version-add-stable-after-preview/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_Get_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_Update_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Operations_List_MaximumSet_Gen.json` → `examples/2021-10-01/Operations_List_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Operations_List_MinimumSet_Gen.json` → `examples/2021-10-01/Operations_List_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_Delete_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_Update_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Operations_List_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Operations_List_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Operations_List_MinimumSet_Gen.json` → `examples/2024-10-01-preview/Operations_List_MinimumSet_Gen.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s7-1" id="ref-s7-1">[1]</a> |

<a href="#user-content-ref-s7-1" id="fn-s7-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001008.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001009.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001010.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 001010-version-model-property-removed-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `Microsoft.Widget/Widget/employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `Microsoft.Widget/Widget/main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `Microsoft.Widget/Widget/package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `Microsoft.Widget/Widget/package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `Microsoft.Widget/Widget/readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `Microsoft.Widget/Widget/shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `Microsoft.Widget/Widget/tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s10-1" id="ref-s10-1">[1]</a> |

<a href="#user-content-ref-s10-1" id="fn-s10-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001011.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 001011-version-model-property-renamed-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `Microsoft.Widget/Widget/employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `Microsoft.Widget/Widget/main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `Microsoft.Widget/Widget/package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `Microsoft.Widget/Widget/package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `Microsoft.Widget/Widget/readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `Microsoft.Widget/Widget/shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `Microsoft.Widget/Widget/tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s11-1" id="ref-s11-1">[1]</a> |

<a href="#user-content-ref-s11-1" id="fn-s11-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001013.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002001.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002002.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 002002-ARM-define-extension-resource-add-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s14-1" id="ref-s14-1">[1]</a> |

<a href="#user-content-ref-s14-1" id="fn-s14-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002003.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 002003-ARM-define-full-update-operation-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/002003-ARM-define-full-update-operation/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s15-1" id="ref-s15-1">[1]</a> |

<a href="#user-content-ref-s15-1" id="fn-s15-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002004.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002005.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002006.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002007.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002008.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 002008-ARM-add-parameters-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s20-1" id="ref-s20-1">[1]</a> |

<a href="#user-content-ref-s20-1" id="fn-s20-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002009.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002010.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002011.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\003001.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\003002.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004001.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ❌ Errors

- `Error in C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004002.eval.yaml: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.`

_No stimuli were executed._

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004003.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 004003-delete-and-restore-operationId-decorator-forced | <details><summary>13 files · 1 command · 1 MCP server</summary>Files: `../fixtures/004003-delete-and-restore-operationId-decorator/BastionHost.tsp` → `BastionHost.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/BgpConnection.tsp` → `BgpConnection.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRouteGateway.tsp` → `ExpressRouteGateway.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRoutePort.tsp` → `ExpressRoutePort.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRouteProviderPort.tsp` → `ExpressRouteProviderPort.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/NetworkInterface.tsp` → `NetworkInterface.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/P2SVpnGateway.tsp` → `P2SVpnGateway.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/Route.tsp` → `Route.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/VpnServerConfigurationPolicyGroup.tsp` → `VpnServerConfigurationPolicyGroup.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/VpnSiteLinkConnection.tsp` → `VpnSiteLinkConnection.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/004003-delete-and-restore-operationId-decorator/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s28-1" id="ref-s28-1">[1]</a> |

<a href="#user-content-ref-s28-1" id="fn-s28-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.

<hr>

### azure-typespec-author-eval [claude-opus-4.6]

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\005001.eval.yaml

| Stimulus | Environment | Model | Graders | Pass Rate | Duration | Tokens | Verdict |
|---|---|---|---|---|---|---|---|
| 005001-warning-suppress-warning-forced | <details><summary>6 files · 1 command · 1 MCP server</summary>Files: `../fixtures/005001-warning-suppress-warning/main.tsp` → `main.tsp`, `../fixtures/005001-warning-suppress-warning/models.tsp` → `models.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/005001-warning-suppress-warning/stubs.tsp` → `stubs.tsp`, `../fixtures/005001-warning-suppress-warning/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | — | — | — | — | — | ❌ <a href="#user-content-fn-s29-1" id="ref-s29-1">[1]</a> |

<a href="#user-content-ref-s29-1" id="fn-s29-1"><strong>[1]</strong></a> Error: Copilot CLI not found at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\agentic-doc-refinement\node_modules\@github\index.js. Ensure @github/copilot is installed.
