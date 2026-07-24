## Eval Results

> Timestamp: 2026-07-03T06:06:29.139Z

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001001.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001001.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001001-version-spread-property-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>❌ file-matches 0/1<br>❌ prompt 0/1 | 0/1 | 43.0s | 46,219 | 2 | <details><summary>1 calls</summary>azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1</details> | ❌ <a href="#user-content-fn-s1-1" id="ref-s1-1">[1]</a> |

<a href="#user-content-ref-s1-1" id="fn-s1-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`, `file-matches`, `prompt`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001002.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001002.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001002-version-default-value-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001002-version-default-value/employee.tsp` → `employee.tsp`, `../fixtures/001002-version-default-value/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001002-version-default-value/readme.md` → `readme.md`, `../fixtures/001002-version-default-value/shared.tsp` → `shared.tsp`, `../fixtures/001002-version-default-value/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>❌ file-matches 0/1<br>❌ prompt 0/1 | 0/1 | 1m 23s | 172,783 | 7 | <details><summary>8 calls</summary>view: 4, edit: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s2-1" id="ref-s2-1">[1]</a> |

<a href="#user-content-ref-s2-1" id="fn-s2-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`, `file-matches`, `file-matches`, `prompt`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001003.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001003.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001003-version-required-to-optional-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 1m 06s | 121,866 | 5 | <details><summary>7 calls</summary>view: 3, glob: 2, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001004.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001004.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001004-version-property-decorator-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 1m 43s | 179,555 | 7 | <details><summary>8 calls</summary>view: 5, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001005.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001005.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001005-version-add-preview-after-preview-forced | <details><summary>24 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001005-version-add-preview-after-preview/employee.tsp` → `employee.tsp`, `../fixtures/001005-version-add-preview-after-preview/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001005-version-add-preview-after-preview/shared.tsp` → `shared.tsp`, `../fixtures/001005-version-add-preview-after-preview/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_Get_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Employees_Update_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Operations_List_MaximumSet_Gen.json` → `examples/2021-10-01/Operations_List_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2021-10-01/Operations_List_MinimumSet_Gen.json` → `examples/2021-10-01/Operations_List_MinimumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_Delete_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Employees_Update_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Operations_List_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Operations_List_MaximumSet_Gen.json`, `../fixtures/001005-version-add-preview-after-preview/examples/2024-10-01-preview/Operations_List_MinimumSet_Gen.json` → `examples/2024-10-01-preview/Operations_List_MinimumSet_Gen.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>❌ file-exists 0/1<br>❌ file-not-exists 0/1<br>❌ file-matches 0/1<br>❌ file-not-matches 0/1 | 0/1 | 1m 48s | 190,390 | 7 | <details><summary>12 calls</summary>view: 8, edit: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s5-1" id="ref-s5-1">[1]</a> |

<a href="#user-content-ref-s5-1" id="fn-s5-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`, `file-exists`, `file-not-exists`, `file-not-matches`, `file-not-matches`, `file-matches`, `file-not-matches`, `file-matches`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001006.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001006.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001006-version-add-preview-after-stable-forced | <details><summary>24 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001006-version-add-preview-after-stable/employee.tsp` → `employee.tsp`, `../fixtures/001006-version-add-preview-after-stable/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001006-version-add-preview-after-stable/shared.tsp` → `shared.tsp`, `../fixtures/001006-version-add-preview-after-stable/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/001006-version-add-preview-after-stable/examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2021-10-01/Employees_Get_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2021-10-01/Employees_Update_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2021-10-01/Operations_List_MaximumSet_Gen.json` → `examples/2021-10-01/Operations_List_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2021-10-01/Operations_List_MinimumSet_Gen.json` → `examples/2021-10-01/Operations_List_MinimumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Employees_Delete_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Employees_Get_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2024-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2024-10-01/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Employees_Update_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Operations_List_MaximumSet_Gen.json` → `examples/2024-10-01/Operations_List_MaximumSet_Gen.json`, `../fixtures/001006-version-add-preview-after-stable/examples/2024-10-01/Operations_List_MinimumSet_Gen.json` → `examples/2024-10-01/Operations_List_MinimumSet_Gen.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>✅ file-exists 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 0/1 | 1m 08s | 160,527 | 6 | <details><summary>13 calls</summary>view: 8, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, glob: 1, edit: 1, powershell: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s6-1" id="ref-s6-1">[1]</a> |

<a href="#user-content-ref-s6-1" id="fn-s6-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001007.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001007.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001007-version-add-stable-after-preview-forced | <details><summary>27 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001007-version-add-stable-after-preview/employee.tsp` → `employee.tsp`, `../fixtures/001007-version-add-stable-after-preview/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001007-version-add-stable-after-preview/readme.md` → `readme.md`, `../fixtures/001007-version-add-stable-after-preview/shared.tsp` → `shared.tsp`, `../fixtures/001007-version-add-stable-after-preview/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_Get_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Employees_Update_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Operations_List_MaximumSet_Gen.json` → `examples/2021-10-01/Operations_List_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2021-10-01/Operations_List_MinimumSet_Gen.json` → `examples/2021-10-01/Operations_List_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_Delete_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2024-10-01-preview/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Employees_Update_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Operations_List_MaximumSet_Gen.json` → `examples/2024-10-01-preview/Operations_List_MaximumSet_Gen.json`, `../fixtures/001007-version-add-stable-after-preview/examples/2024-10-01-preview/Operations_List_MinimumSet_Gen.json` → `examples/2024-10-01-preview/Operations_List_MinimumSet_Gen.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>❌ file-exists 0/1<br>❌ file-not-exists 0/1<br>❌ file-matches 0/1<br>❌ file-not-matches 0/1 | 0/1 | 1m 18s | 213,685 | 8 | <details><summary>13 calls</summary>view: 8, edit: 3, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s7-1" id="ref-s7-1">[1]</a> |

<a href="#user-content-ref-s7-1" id="fn-s7-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`, `file-exists`, `file-not-exists`, `file-not-matches`, `file-not-matches`, `file-matches`, `file-matches`, `file-not-matches`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001008.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001008.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001008-version-add-stable-after-stable-forced | <details><summary>27 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001008-version-add-stable-after-stable/employee.tsp` → `employee.tsp`, `../fixtures/001008-version-add-stable-after-stable/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/001008-version-add-stable-after-stable/readme.md` → `readme.md`, `../fixtures/001008-version-add-stable-after-stable/shared.tsp` → `shared.tsp`, `../fixtures/001008-version-add-stable-after-stable/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Employees_Get_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2021-10-01/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Employees_Update_MaximumSet_Gen.json` → `examples/2021-10-01/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Operations_List_MaximumSet_Gen.json` → `examples/2021-10-01/Operations_List_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2021-10-01/Operations_List_MinimumSet_Gen.json` → `examples/2021-10-01/Operations_List_MinimumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_CreateOrUpdate_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Employees_Delete_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_Delete_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Employees_Get_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_Get_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_ListByResourceGroup_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json` → `examples/2024-10-01/Employees_ListByResourceGroup_MinimumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Employees_ListBySubscription_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_ListBySubscription_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Employees_ListBySubscription_MinimumSet_Gen.json` → `examples/2024-10-01/Employees_ListBySubscription_MinimumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Employees_Update_MaximumSet_Gen.json` → `examples/2024-10-01/Employees_Update_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Operations_List_MaximumSet_Gen.json` → `examples/2024-10-01/Operations_List_MaximumSet_Gen.json`, `../fixtures/001008-version-add-stable-after-stable/examples/2024-10-01/Operations_List_MinimumSet_Gen.json` → `examples/2024-10-01/Operations_List_MinimumSet_Gen.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>❌ file-exists 0/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 0/1 | 1m 10s | 153,968 | 6 | <details><summary>9 calls</summary>view: 6, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s8-1" id="ref-s8-1">[1]</a> |

<a href="#user-content-ref-s8-1" id="fn-s8-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`, `file-exists`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001009.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001009.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001009-version-model-property-required-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `Microsoft.Widget/Widget/employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `Microsoft.Widget/Widget/main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `Microsoft.Widget/Widget/package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `Microsoft.Widget/Widget/package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `Microsoft.Widget/Widget/readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `Microsoft.Widget/Widget/shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `Microsoft.Widget/Widget/tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 56.6s | 121,720 | 5 | <details><summary>6 calls</summary>view: 4, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001010.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001010.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001010-version-model-property-removed-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `Microsoft.Widget/Widget/employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `Microsoft.Widget/Widget/main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `Microsoft.Widget/Widget/package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `Microsoft.Widget/Widget/package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `Microsoft.Widget/Widget/readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `Microsoft.Widget/Widget/shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `Microsoft.Widget/Widget/tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1 | 1/1 | 40.6s | 120,465 | 5 | <details><summary>6 calls</summary>view: 4, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001011.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001011.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001011-version-model-property-renamed-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `Microsoft.Widget/Widget/employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `Microsoft.Widget/Widget/main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `Microsoft.Widget/Widget/package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `Microsoft.Widget/Widget/package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `Microsoft.Widget/Widget/readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `Microsoft.Widget/Widget/shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `Microsoft.Widget/Widget/tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1 | 1/1 | 55.8s | 146,909 | 6 | <details><summary>7 calls</summary>view: 4, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001013.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\001013.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 001013-version-model-property-type-changed-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/001-share-version-new-feature/employee.tsp` → `Microsoft.Widget/Widget/employee.tsp`, `../fixtures/001-share-version-new-feature/main.tsp` → `Microsoft.Widget/Widget/main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `Microsoft.Widget/Widget/package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `Microsoft.Widget/Widget/package.json`, `../fixtures/001-share-version-new-feature/readme.md` → `Microsoft.Widget/Widget/readme.md`, `../fixtures/001-share-version-new-feature/shared.tsp` → `Microsoft.Widget/Widget/shared.tsp`, `../fixtures/001-share-version-new-feature/tspconfig.yaml` → `Microsoft.Widget/Widget/tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 56.3s | 147,148 | 6 | <details><summary>7 calls</summary>view: 4, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002001.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002001.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002001-ARM-change-resource-type-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>❌ file-matches 0/1<br>✅ file-not-matches 1/1<br>❌ prompt 0/1 | 0/1 | 1m 18s | 207,172 | 8 | <details><summary>11 calls</summary>view: 5, glob: 2, edit: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s13-1" id="ref-s13-1">[1]</a> |

<a href="#user-content-ref-s13-1" id="fn-s13-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`, `prompt`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002002.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002002.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002002-ARM-define-extension-resource-add-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1 | 1/1 | 1m 03s | 175,694 | 7 | <details><summary>9 calls</summary>view: 5, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, create: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002003.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002003.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002003-ARM-define-full-update-operation-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/002003-ARM-define-full-update-operation/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>❌ file-matches 0/1 | 0/1 | 1m 03s | 150,704 | 6 | <details><summary>8 calls</summary>view: 5, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s15-1" id="ref-s15-1">[1]</a> |

<a href="#user-content-ref-s15-1" id="fn-s15-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002004.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002004.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002004-ARM-define-extension-resource-fromProxyResource-forced | <details><summary>8 files · 1 command · 1 MCP server</summary>Files: `../fixtures/002004-ARM-define-extension-resource/badgeAssignment.tsp` → `badgeAssignment.tsp`, `../fixtures/002004-ARM-define-extension-resource/employee.tsp` → `employee.tsp`, `../fixtures/002004-ARM-define-extension-resource/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>❌ file-matches 0/1<br>❌ prompt 0/1 | 0/1 | 59.6s | 157,354 | 6 | <details><summary>11 calls</summary>view: 8, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s16-1" id="ref-s16-1">[1]</a> |

<a href="#user-content-ref-s16-1" id="fn-s16-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`, `file-matches`, `prompt`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002005.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002005.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002005-ARM-define-the-resource-forced | <details><summary>6 files · 1 command · 1 MCP server</summary>Files: `../fixtures/002005-ARM-define-the-resource/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 1m 05s | 175,775 | 7 | <details><summary>9 calls</summary>view: 6, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002006.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002006.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002006-ARM-define-child-resource-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 1m 04s | 178,275 | 7 | <details><summary>11 calls</summary>view: 7, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, create: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002007.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002007.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002007-ARM-define-custom-action-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 1m 06s | 204,499 | 8 | <details><summary>10 calls</summary>view: 6, edit: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002008.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002008.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002008-ARM-add-parameters-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>❌ file-matches 0/1<br>❌ file-not-matches 0/1 | 0/1 | 1m 05s | 147,617 | 6 | <details><summary>6 calls</summary>view: 3, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s20-1" id="ref-s20-1">[1]</a> |

<a href="#user-content-ref-s20-1" id="fn-s20-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`, `file-not-matches`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002009.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002009.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002009-arm-add-patch-operation-to-resource-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/002009-arm-add-patch-operation-to-resource/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>❌ file-matches 0/1<br>✅ prompt 1/1 | 0/1 | 58.6s | 172,174 | 7 | <details><summary>8 calls</summary>view: 3, glob: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s21-1" id="ref-s21-1">[1]</a> |

<a href="#user-content-ref-s21-1" id="fn-s21-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002010.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002010.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002010-arm-action-sync-operation-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/002010-arm-action-sync-operation/employee.tsp` → `employee.tsp`, `../fixtures/002010-arm-action-sync-operation/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/002010-arm-action-sync-operation/readme.md` → `readme.md`, `../fixtures/002010-arm-action-sync-operation/shared.tsp` → `shared.tsp`, `../fixtures/002010-arm-action-sync-operation/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 53.3s | 148,084 | 6 | <details><summary>8 calls</summary>view: 5, edit: 2, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002011.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\002011.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 002011-arm-add-check-existence-operation-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 54.8s | 146,018 | 6 | <details><summary>8 calls</summary>view: 3, glob: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\003001.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\003001.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 003001-arm-action-lro-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/003-long-running-operation-share/employee.tsp` → `employee.tsp`, `../fixtures/003-long-running-operation-share/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/003-long-running-operation-share/readme.md` → `readme.md`, `../fixtures/003-long-running-operation-share/shared.tsp` → `shared.tsp`, `../fixtures/003-long-running-operation-share/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>❌ file-matches 0/1<br>✅ prompt 1/1 | 0/1 | 1m 03s | 147,983 | 6 | <details><summary>8 calls</summary>view: 5, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s24-1" id="ref-s24-1">[1]</a> |

<a href="#user-content-ref-s24-1" id="fn-s24-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`, `file-matches`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\003002.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\003002.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 003002-arm-modify-response-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ prompt 1/1 | 1/1 | 3m 03s | 452,780 | 17 | <details><summary>19 calls</summary>view: 6, powershell: 5, glob: 3, grep: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004001.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004001.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 004001-decorate-mgmt-resource-name-parameter-forced | <details><summary>9 files · 1 command · 1 MCP server</summary>Files: `../fixtures/Microsoft.Widget/Widget/employee.tsp` → `employee.tsp`, `../fixtures/Microsoft.Widget/Widget/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/Microsoft.Widget/Widget/readme.md` → `readme.md`, `../fixtures/Microsoft.Widget/Widget/shared.tsp` → `shared.tsp`, `../fixtures/Microsoft.Widget/Widget/tspconfig.yaml` → `tspconfig.yaml`, `../fixtures/Microsoft.Widget/Widget/preview/2024-10-01-preview/widget.json` → `preview/2024-10-01-preview/widget.json`, `../fixtures/Microsoft.Widget/Widget/stable/2021-11-01/widget.json` → `stable/2021-11-01/widget.json`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>❌ file-matches 0/1<br>❌ prompt 0/1 | 0/1 | 1m 24s | 149,528 | 6 | <details><summary>7 calls</summary>glob: 2, view: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, edit: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ❌ <a href="#user-content-fn-s26-1" id="ref-s26-1">[1]</a> |

<a href="#user-content-ref-s26-1" id="fn-s26-1"><strong>[1]</strong></a> Failed grader(s): `file-matches`, `prompt`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004002.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004002.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 004002-decorate-length-constrains-on-array-item-forced | <details><summary>7 files · 1 command · 1 MCP server</summary>Files: `../fixtures/004002-decorate-length-constrains-on-array-item/employee.tsp` → `employee.tsp`, `../fixtures/004002-decorate-length-constrains-on-array-item/main.tsp` → `main.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/004002-decorate-length-constrains-on-array-item/readme.md` → `readme.md`, `../fixtures/004002-decorate-length-constrains-on-array-item/shared.tsp` → `shared.tsp`, `../fixtures/004002-decorate-length-constrains-on-array-item/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ✅ tool-calls 1/1<br>✅ file-matches 1/1<br>✅ file-not-matches 1/1<br>✅ prompt 1/1 | 1/1 | 1m 07s | 172,896 | 7 | <details><summary>8 calls</summary>grep: 2, view: 2, edit: 2, azure-sdk-mcp-azsdk_typespec_generate_authoring_plan: 1, azure-sdk-mcp-azsdk_run_typespec_validation: 1</details> | ✅ |

> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004003.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\004003.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 004003-delete-and-restore-operationId-decorator-forced | <details><summary>13 files · 1 command · 1 MCP server</summary>Files: `../fixtures/004003-delete-and-restore-operationId-decorator/BastionHost.tsp` → `BastionHost.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/BgpConnection.tsp` → `BgpConnection.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRouteGateway.tsp` → `ExpressRouteGateway.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRoutePort.tsp` → `ExpressRoutePort.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/ExpressRouteProviderPort.tsp` → `ExpressRouteProviderPort.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/NetworkInterface.tsp` → `NetworkInterface.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/P2SVpnGateway.tsp` → `P2SVpnGateway.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/Route.tsp` → `Route.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/VpnServerConfigurationPolicyGroup.tsp` → `VpnServerConfigurationPolicyGroup.tsp`, `../fixtures/004003-delete-and-restore-operationId-decorator/VpnSiteLinkConnection.tsp` → `VpnSiteLinkConnection.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/004003-delete-and-restore-operationId-decorator/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>✅ file-not-matches 1/1<br>✅ file-matches 1/1 | 0/1 | 1m 46s | 264,736 | 8 | <details><summary>8 calls</summary>grep: 5, powershell: 2, view: 1</details> | ❌ <a href="#user-content-fn-s28-1" id="ref-s28-1">[1]</a> |

<a href="#user-content-ref-s28-1" id="fn-s28-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`


> Model: claude-opus-4.6 | Executor: copilot-sdk

<hr>

### azure-typespec-author-eval [claude-opus-4.6] (C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\005001.eval.yaml)

Evaluation suite for azure-typespec-author.

> **Environment**: 1 MCP server

#### ⚠️ Warnings

- `[stimuli-filtered] Running 1 of 3 stimuli (2 filtered by tags)` at C:\workspace\azure-sdk-tools\.github\skills\azure-typespec-author\evaluate\evals\005001.eval.yaml

| Stimulus | Environment | Graders | Pass Rate | Duration | Tokens | Turns | Tool Calls | Verdict |
|---|---|---|---|---|---|---|---|---|
| 005001-warning-suppress-warning-forced | <details><summary>6 files · 1 command · 1 MCP server</summary>Files: `../fixtures/005001-warning-suppress-warning/main.tsp` → `main.tsp`, `../fixtures/005001-warning-suppress-warning/models.tsp` → `models.tsp`, `../fixtures/Microsoft.Widget/Widget/package-lock.json` → `package-lock.json`, `../fixtures/Microsoft.Widget/Widget/package.json` → `package.json`, `../fixtures/005001-warning-suppress-warning/stubs.tsp` → `stubs.tsp`, `../fixtures/005001-warning-suppress-warning/tspconfig.yaml` → `tspconfig.yaml`<br/>Commands: `node -e "const fs=require('fs');const t=process.env.FIXTURE_NODE_MODULES;if(t&amp;&amp;fs.existsSync(t)&amp;&amp;!fs.existsSync('node_modules')){fs.symlinkSync(t,'node_modules','junction')}"`<br/>MCP: `azure-sdk-mcp` (stdio)</details> | ❌ tool-calls 0/1<br>✅ file-matches 1/1 | 0/1 | 7m 11s | 1,062,695 | 30 | <details><summary>37 calls</summary>powershell: 24, view: 10, edit: 3</details> | ❌ <a href="#user-content-fn-s29-1" id="ref-s29-1">[1]</a> |

<a href="#user-content-ref-s29-1" id="fn-s29-1"><strong>[1]</strong></a> Failed grader(s): `tool-calls`


> Model: claude-opus-4.6 | Executor: copilot-sdk
