# azure-typespec-author — Code-Quality Documentation Gap Analysis

Analysis of the **Code Quality** eval across **18 runs** covering **29 unique test cases** (73 graded
records total), used to identify gaps in the skill's documentation (`SKILL.md` + `references/`).

## Source

| | |
|---|---|
| Pipeline | `azure-typespec-author-benchmark` |
| Branch | Data collected across 18 evaluation runs |
| Job | Code Quality (`forced.eval.yaml`, skill force-loaded) |
| Judge / agent model | `claude-opus-4.6` |
| Graded records | 73 across 29 unique cases |
| Run count | 18 runs |
| Date | 2026-06-17 through 2026-07-03 |

## Overall result

| Metric | Value |
|---|---|
| Total passes | 34 / 73 |
| Aggregate pass rate | **47%** |

Per-suite aggregate:

| Suite | Passes | Total | Rate |
|---|---|---|---|
| versioning | 9 | 27 | 33% |
| warning | 1 | 4 | 25% |
| decorators | 6 | 11 | 55% |
| armtemplate | 15 | 26 | 58% |
| lro | 3 | 5 | 60% |
| **Overall** | **34** | **73** | **47%** |

## Per-case pass rate (summary)

Sorted ascending by pass rate.

| Case | Suite | Passes | Total | Rate | Classification |
|---|---|---|---|---|---|
| 001001-version-spread-property-forced | versioning | 0 | 5 | 0% | Systemic |
| 001002-version-default-value-forced | versioning | 0 | 2 | 0% | Systemic |
| 001005-version-add-preview-after-preview-forced | versioning | 0 | 2 | 0% | Systemic |
| 001006-version-add-preview-after-stable-forced | versioning | 0 | 2 | 0% | Systemic |
| 001007-version-add-stable-after-preview-forced | versioning | 0 | 2 | 0% | Systemic |
| 001008-version-add-stable-after-stable-forced | versioning | 0 | 2 | 0% | Systemic |
| 002001-ARM-change-resource-type-forced | armtemplate | 0 | 2 | 0% | Systemic |
| 002003-ARM-define-full-update-operation-forced | armtemplate | 0 | 2 | 0% | Systemic |
| 002009-arm-add-patch-operation-to-resource-forced | armtemplate | 0 | 2 | 0% | Systemic |
| 005001-warning-suppress-warning-forced | warning | 1 | 4 | 25% | Systemic |
| 002004-ARM-define-extension-resource-fromProxyResource-forced | armtemplate | 1 | 3 | 33% | Systemic |
| 002008-ARM-add-parameters-forced | armtemplate | 1 | 3 | 33% | Systemic |
| 003001-arm-action-lro-forced | lro | 1 | 3 | 33% | Systemic |
| 004003-delete-and-restore-operationId-decorator-forced | decorators | 1 | 3 | 33% | Systemic |
| 004001-decorate-mgmt-resource-name-parameter-forced | decorators | 2 | 5 | 40% | Systemic |
| 001003-version-required-to-optional-forced | versioning | 1 | 2 | 50% | Flaky |
| 001004-version-property-decorator-forced | versioning | 1 | 2 | 50% | Flaky |
| 001009-version-model-property-required-forced | versioning | 1 | 2 | 50% | Flaky |
| 002005-ARM-define-the-resource-forced | armtemplate | 1 | 2 | 50% | Flaky |
| 001010-version-model-property-removed-forced | versioning | 2 | 2 | 100% | PASS |
| 001011-version-model-property-renamed-forced | versioning | 2 | 2 | 100% | PASS |
| 001013-version-model-property-type-changed-forced | versioning | 2 | 2 | 100% | PASS |
| 002002-ARM-define-extension-resource-add-forced | armtemplate | 3 | 3 | 100% | PASS |
| 002006-ARM-define-child-resource-forced | armtemplate | 2 | 2 | 100% | PASS |
| 002007-ARM-define-custom-action-forced | armtemplate | 2 | 2 | 100% | PASS |
| 002010-arm-action-sync-operation-forced | armtemplate | 2 | 2 | 100% | PASS |
| 002011-arm-add-check-existence-operation-forced | armtemplate | 3 | 3 | 100% | PASS |
| 003002-arm-modify-response-forced | lro | 2 | 2 | 100% | PASS |
| 004002-decorate-length-constrains-on-array-item-forced | decorators | 3 | 3 | 100% | PASS |

## Per-case pass rate (case × run matrix)

### versioning — 9 / 27

| Case | Rate | Detail |
|---|---|---|
| 001001-version-spread-property-forced | 0% | 0/5 — systemic; agent never calls `edit`, never produces `@@added` augment |
| 001002-version-default-value-forced | 0% | 0/2 — systemic; agent uses `@madeDefault` instead of expected `@removed/@renamedFrom/@added` |
| 001005-version-add-preview-after-preview-forced | 0% | 0/2 — systemic; agent never calls `web_fetch`, never restructures example files |
| 001006-version-add-preview-after-stable-forced | 0% | 0/2 — systemic; agent never calls `web_fetch` |
| 001007-version-add-stable-after-preview-forced | 0% | 0/2 — systemic; agent never calls `web_fetch`, does not restructure files |
| 001008-version-add-stable-after-stable-forced | 0% | 0/2 — systemic; agent never calls `web_fetch`, does not produce expected files |
| 001003-version-required-to-optional-forced | 50% | 1/2 — flaky; LLM grader scored one run as incomplete |
| 001004-version-property-decorator-forced | 50% | 1/2 — flaky; LLM grader scored one run as incomplete |
| 001009-version-model-property-required-forced | 50% | 1/2 — flaky; `email: string;` pattern not found in one run |
| 001010-version-model-property-removed-forced | 100% | 2/2 |
| 001011-version-model-property-renamed-forced | 100% | 2/2 |
| 001013-version-model-property-type-changed-forced | 100% | 2/2 |

### armtemplate — 15 / 26

| Case | Rate | Detail |
|---|---|---|
| 002001-ARM-change-resource-type-forced | 0% | 0/2 — systemic; agent does not use Extension.* operation templates |
| 002003-ARM-define-full-update-operation-forced | 0% | 0/2 — systemic; agent does not use `ArmCustomPatchSync` |
| 002009-arm-add-patch-operation-to-resource-forced | 0% | 0/2 — systemic; agent does not use `ArmCustomPatchSync` |
| 002004-ARM-define-extension-resource-fromProxyResource-forced | 33% | 1/3 — systemic; agent does not use Extension.* templates |
| 002008-ARM-add-parameters-forced | 33% | 1/3 — systemic; agent uses `@query` instead of `TopQueryParameter`/`SkipQueryParameter` spread |
| 002005-ARM-define-the-resource-forced | 50% | 1/2 — flaky; MCP tool failure in one run |
| 002002-ARM-define-extension-resource-add-forced | 100% | 3/3 |
| 002006-ARM-define-child-resource-forced | 100% | 2/2 |
| 002007-ARM-define-custom-action-forced | 100% | 2/2 |
| 002010-arm-action-sync-operation-forced | 100% | 2/2 |
| 002011-arm-add-check-existence-operation-forced | 100% | 3/3 |

### lro — 3 / 5

| Case | Rate | Detail |
|---|---|---|
| 003001-arm-action-lro-forced | 33% | 1/3 — systemic; agent does not use `ArmResourceActionAsync` with `ArmCombinedLroHeaders` |
| 003002-arm-modify-response-forced | 100% | 2/2 |

### decorators — 6 / 11

| Case | Rate | Detail |
|---|---|---|
| 004003-delete-and-restore-operationId-decorator-forced | 33% | 1/3 — systemic; MCP tool call failure → agent falls back to advice |
| 004001-decorate-mgmt-resource-name-parameter-forced | 40% | 2/5 — systemic; agent does not produce `@@minLength(Employee.name, 1)` |
| 004002-decorate-length-constrains-on-array-item-forced | 100% | 3/3 |

### warning — 1 / 4

| Case | Rate | Detail |
|---|---|---|
| 005001-warning-suppress-warning-forced | 25% | 1/4 — systemic; MCP tool failure → agent does not call `edit` in 3/4 runs |

## Failure reasons

### Systemic failures (recur across runs → documentation gaps)

#### 001001-version-spread-property-forced — failed **5/5**

**Reason:** Agent does not know how to version-gate properties inherited via `...` spread syntax. It fails to use the `@@added` augment decorator pattern. In all 5 runs, the agent either had MCP tool failures or produced advisory text instead of code. The `@@added(Employee.identity, Versions.v2025_05_04_preview)` pattern was never produced.

**Evidence:** `file-matches` grader: pattern `/@@added\(Employee\.identity, ...\)/` not found in any run. `tool-calls` grader: `/edit/` never called in 5/5 runs.

#### 001002-version-default-value-forced — failed **2/2**

**Reason:** Agent uses the TypeSpec-idiomatic `@madeDefault` decorator instead of the Azure SDK-expected pattern of `@removed/@renamedFrom/@added` to rename the old property and add a new one with a default value. The LLM grader scored the agent 4/5 for correctness, confirming the approach is valid TypeSpec—but it does not match the expected Azure convention.

**Evidence:** `file-matches` grader: patterns `/@removed\(Versions\.v2025_11_01\)/` and `/@renamedFrom\(Versions\.v2025_11_01, "age"\)/` not found.

#### 001005-version-add-preview-after-preview-forced — failed **2/2**

**Reason:** Agent does not call `web_fetch` to retrieve example JSON from the spec repo, does not restructure example directories for the new API version, and does not properly remove old version references from `main.tsp` and `employee.tsp`.

**Evidence:** `tool-calls`: `/web_fetch/` not called in 2/2 runs. `file-exists`: `examples/2025-05-04-preview/Employees_Get_MaximumSet_Gen.json` not created. `file-not-exists`: old `examples/2024-10-01-preview/` still present. `file-not-matches`: old version string `2024-10-01-preview` still in `main.tsp`.

#### 001006-version-add-preview-after-stable-forced — failed **2/2**

**Reason:** Same root cause as 001005. Agent does not call `web_fetch` to retrieve example JSON for the new API version.

**Evidence:** `tool-calls`: `/web_fetch/` not called in 2/2 runs.

#### 001007-version-add-stable-after-preview-forced — failed **2/2**

**Reason:** Same root cause as 001005. Agent does not call `web_fetch`, does not restructure example files, and does not remove old version artifacts.

**Evidence:** `tool-calls`: `/web_fetch/` not called in 2/2 runs. `file-not-exists`: old preview examples still present. `file-not-matches`: old preview version still referenced.

#### 001008-version-add-stable-after-stable-forced — failed **2/2**

**Reason:** Same root cause as 001005. Agent does not call `web_fetch` and does not create expected example files for the new version.

**Evidence:** `tool-calls`: `/web_fetch/` not called in 2/2 runs. `file-exists`: `examples/2025-01-01/Employees_Get_MaximumSet_Gen.json` not found.

#### 002001-ARM-change-resource-type-forced — failed **2/2**

**Reason:** Agent changes the base type from `TrackedResource` to `ExtensionResource` correctly, but does not use Extension-specific operation templates (`Extension.Read`, `Extension.CreateOrReplaceAsync`, `Extension.CustomPatchSync`, etc.). The LLM grader scored 3/5 for correctness—core change was right but the scope-parameterized interface was missing.

**Evidence:** `file-matches`: pattern `/Extension\.Read<|Extension\.CreateOrReplaceAsync</` not found in any `.tsp` file.

#### 002003-ARM-define-full-update-operation-forced — failed **2/2**

**Reason:** Agent does not use the `ArmCustomPatchSync` template for defining a full (PUT-like) update operation. It likely uses standard PATCH patterns instead.

**Evidence:** `file-matches`: pattern `/update\s+is\s+ArmCustomPatchSync\s*</` not found in any `.tsp` file in 2/2 runs.

#### 002009-arm-add-patch-operation-to-resource-forced — failed **2/2**

**Reason:** Same root cause as 002003. Agent does not use `ArmCustomPatchSync` for the patch operation.

**Evidence:** `file-matches`: pattern `/update\s+is\s+ArmCustomPatchSync\s*</` not found in `employee.tsp` in 2/2 runs.

#### 002004-ARM-define-extension-resource-fromProxyResource-forced — failed **2/3**

**Reason:** Agent converts `ProxyResource` to `ExtensionResource` with `@parentResource(Employee)` but does not use Extension-specific operation templates (`Extension.Read`, `Extension.CreateOrReplaceAsync`, `Extension.ListByTarget`, etc.).

**Evidence:** `file-matches`: Extension.* operation patterns not found in `badgeAssignment.tsp`. LLM grader scored 4/5—approach was valid but did not match expected template usage.

#### 002008-ARM-add-parameters-forced — failed **2/3**

**Reason:** Agent uses raw `@query("$top")` / `@query("$skip")` decorators instead of the expected `TopQueryParameter` / `SkipQueryParameter` spread pattern from `Azure.Core`. The eval expects the standard Azure query parameter models.

**Evidence:** `file-not-matches`: `/@query\("\$top"\)|@query\("\$skip"\)/` matched (should not be present). `file-matches`: expected `TopQueryParameter`/`SkipQueryParameter` spread pattern not found.

#### 003001-arm-action-lro-forced — failed **2/3**

**Reason:** Agent does not use `ArmResourceActionAsync` with `ArmCombinedLroHeaders` for the long-running action operation. It likely uses a simpler async pattern without the combined LRO headers.

**Evidence:** `file-matches`: patterns `/move\s+is\s+ArmResourceActionAsync\s*</` and `/ArmCombinedLroHeaders\s*</` not found in `employee.tsp`.

#### 004003-delete-and-restore-operationId-decorator-forced — failed **2/3**

**Reason:** MCP tool call failure caused the agent to fall back to advisory text instead of making code changes via `edit`. This is primarily an infrastructure issue, but the consistency of failure (2/3) suggests the agent lacks recovery strategies.

**Evidence:** `tool-calls`: `/edit/` and `/azure-sdk-mcp-azsdk_run_typespec_validation/` not called in 2/2 failing runs.

#### 004001-decorate-mgmt-resource-name-parameter-forced — failed **3/5**

**Reason:** Agent does not produce the `@@minLength(Employee.name, 1)` augment decorator pattern. In 2/3 failing runs the MCP tool call failed entirely; in 1/3 the agent produced a custom-scalar-based approach instead of the expected augment pattern.

**Evidence:** `file-matches`: `/@@minLength\(Employee\.name,\s*1\)/` not found in any `.tsp` file. `tool-calls`: `/edit/` not called in 2/3 failing runs. LLM grader scored one failing run 1/5 (no output at all).

#### 005001-warning-suppress-warning-forced — failed **3/4**

**Reason:** In 3 of 4 runs the agent's MCP tool calls failed and it did not call `edit` or `azsdk_run_typespec_validation`. This is primarily an infrastructure issue, but the `#suppress` pattern itself may need clearer documentation (exact syntax with `FIXME: Update justification`).

**Evidence:** `tool-calls`: `/edit/` not called in 3/3 failing runs. `file-matches`: `/#suppress/` not found in 1/3 failing runs.

### Flaky / one-off failures (each failed in only 1 run → not doc gaps)

#### 001003-version-required-to-optional-forced — failed **1/2**

**Reason:** LLM grader scored the implementation as slightly incomplete. The agent correctly used `@madeOptional` but the original property was already optional, creating ambiguity. Passed in the other run.

#### 001004-version-property-decorator-forced — failed **1/2**

**Reason:** LLM grader scored the implementation down in one run. The agent used a valid `@removed/@added` pattern with `@encodedName`, which is correct but scored slightly below threshold. Passed in the other run.

#### 001009-version-model-property-required-forced — failed **1/2**

**Reason:** `file-matches` grader did not find `email: string;` pattern in one run. Agent may have used a slightly different syntax. Passed in the other run.

#### 002005-ARM-define-the-resource-forced — failed **1/2**

**Reason:** MCP tool call failure in one run caused the agent to fall back to advisory text. When MCP tools worked (other run), the case passed. Infrastructure issue, not a documentation gap.

## Documentation gaps

### ⭐ Gap 1 — API version bump workflow (add preview/stable version with full file restructuring)

**Priority:** Highest — affects 4 cases, all at 0% pass rate.

**Cases:** 001005, 001006, 001007, 001008.

**Gap:** The skill documentation does not include a step-by-step workflow for adding a new API version (preview or stable). The workflow must cover:
1. Fetching example JSON files from the spec repo via `web_fetch`
2. Restructuring `examples/` directories (removing old version dir, creating new)
3. Updating the `Versions` enum in `main.tsp`
4. Migrating properties with `@removed`, `@renamedFrom`, `@added`, `@typeChangedFrom` decorators
5. Updating the service version decorator

**Why this is a doc gap (not eval noise):** All 4 cases fail in every run with the same root cause (agent never calls `web_fetch`, never restructures example files). The agent clearly lacks knowledge of this workflow.

**Doc action:** Add a reference document or SKILL.md section titled "Adding a new API version" with the complete step-by-step workflow, including the `web_fetch` pattern for example JSON retrieval.

### Gap 2 — Versioning spread properties with `@@added` augment decorator

**Priority:** High — 1 case at 0% across 5 runs.

**Cases:** 001001.

**Gap:** The skill documentation does not cover how to version-gate properties inherited via `...` spread syntax. The `@@added(Model.property, Versions.vX)` augment decorator pattern is required but not documented.

**Why this is a doc gap (not eval noise):** 0/5 pass rate with consistent failure mode (agent never produces the `@@added` augment pattern). Even when MCP tools work, the agent does not know this syntax.

**Doc action:** Add documentation for the augment decorator pattern (`@@added`, `@@removed`) when versioning spread-in properties, with examples showing `@@added(Model.property, Versions.vX)`.

### Gap 3 — `ArmCustomPatchSync` for update/patch operations

**Priority:** High — 2 cases at 0% pass rate.

**Cases:** 002003, 002009.

**Gap:** The skill documentation does not reference the `ArmCustomPatchSync` template for defining full-update (PUT-like) or custom patch operations on ARM resources.

**Why this is a doc gap (not eval noise):** Both cases fail in all runs with the identical missing pattern (`update is ArmCustomPatchSync<...>`). The agent uses other PATCH patterns that are not the expected ARM template.

**Doc action:** Add `ArmCustomPatchSync` to the ARM operation templates reference, with usage examples for both full-update and patch scenarios.

### Gap 4 — Adding default values with `@removed/@renamedFrom/@added` pattern

**Priority:** High — 1 case at 0% pass rate.

**Cases:** 001002.

**Gap:** When adding a version-specific default value to a property, the Azure SDK convention is to remove the old property with `@removed`, rename it with `@renamedFrom`, and add a new property with the default. The agent instead uses `@madeDefault`, which is TypeSpec-idiomatic but does not match the expected Azure pattern.

**Why this is a doc gap (not eval noise):** 0/2 pass rate. The agent consistently chooses a different (but valid) approach, indicating the Azure-specific convention is not documented.

**Doc action:** Document the `@removed/@renamedFrom/@added` pattern for adding version-specific default values, and clarify when to use it vs. `@madeDefault`.

### Gap 5 — Extension resource operation templates (`Extension.*`)

**Priority:** High — 2 cases, 0% and 33% pass rates.

**Cases:** 002001, 002004.

**Gap:** When converting or defining Extension resources, the agent does not use the Extension-specific operation templates (`Extension.Read`, `Extension.CreateOrReplaceAsync`, `Extension.CustomPatchSync`, `Extension.DeleteWithoutOkAsync`, `Extension.ListByTarget`). It uses generic ARM patterns instead.

**Why this is a doc gap (not eval noise):** The agent consistently fails to produce Extension.* operation templates across all failing runs. The LLM grader confirms the agent's core changes are correct but the operation template pattern is missing.

**Doc action:** Add a reference document for Extension resource patterns including the complete set of `Extension.*` operation templates and when to use each.

### Gap 6 — ARM LRO action with `ArmResourceActionAsync` + `ArmCombinedLroHeaders`

**Priority:** Medium — 1 case at 33% pass rate.

**Cases:** 003001.

**Gap:** The skill documentation does not cover the pattern for defining ARM long-running action operations using `ArmResourceActionAsync` with `ArmCombinedLroHeaders` for specifying LRO polling headers.

**Why this is a doc gap (not eval noise):** 1/3 pass rate with consistent failure pattern (agent does not produce the `ArmCombinedLroHeaders` configuration). The one pass may have been a lucky pattern match.

**Doc action:** Add LRO action operation examples showing `ArmResourceActionAsync<Resource, Request, Response, LroHeaders = ArmCombinedLroHeaders<...>>`.

### Gap 7 — Standard query parameter spread pattern (`TopQueryParameter`, `SkipQueryParameter`)

**Priority:** Medium — 1 case at 33% pass rate.

**Cases:** 002008.

**Gap:** When adding query parameters to list operations, the agent uses raw `@query("$top")` / `@query("$skip")` decorators instead of the standard Azure.Core `TopQueryParameter` / `SkipQueryParameter` spread models.

**Why this is a doc gap (not eval noise):** 2/3 failing runs show the same pattern mismatch. The agent knows to add query parameters but uses the wrong approach.

**Doc action:** Document the standard query parameter spread pattern: `ArmListBySubscription<Employee, { ...TopQueryParameter; ...SkipQueryParameter; }>`.

### Gap 8 — Management resource name parameter augment decorators

**Priority:** Medium — 1 case at 40% pass rate.

**Cases:** 004001.

**Gap:** The skill documentation does not cover applying augment decorators (e.g., `@@minLength(Employee.name, 1)`) to management resource name parameters. The agent sometimes uses alternative approaches (custom scalar types) or fails entirely.

**Why this is a doc gap (not eval noise):** 3/5 failures; in runs where MCP tools worked, the agent still used incorrect approaches. Partial infrastructure overlap but the knowledge gap is present independently.

**Doc action:** Add examples for constraining ARM resource name parameters using augment decorators like `@@minLength`, `@@maxLength`, `@@pattern`.

## Prioritized summary

| Priority | Gap | Cases | Agg. fail rate |
|---|---|---|---|
| ⭐ Highest | Gap 1 — API version bump workflow | 001005, 001006, 001007, 001008 | 0/8 (0%) |
| High | Gap 2 — Versioning spread properties (`@@added`) | 001001 | 0/5 (0%) |
| High | Gap 3 — `ArmCustomPatchSync` template | 002003, 002009 | 0/4 (0%) |
| High | Gap 4 — Default values with `@removed/@renamedFrom/@added` | 001002 | 0/2 (0%) |
| High | Gap 5 — Extension resource operation templates | 002001, 002004 | 1/5 (20%) |
| Medium | Gap 6 — ARM LRO action + `ArmCombinedLroHeaders` | 003001 | 1/3 (33%) |
| Medium | Gap 7 — Standard query parameter spread | 002008 | 1/3 (33%) |
| Medium | Gap 8 — Resource name parameter augment decorators | 004001 | 2/5 (40%) |

Total systemic-failure cases: **15** (of 29 unique cases).
Total cases explained by documentation gaps: **13** (Gaps 1–8).
Remaining systemic cases (004003, 005001): primarily MCP infrastructure failures, not doc gaps.

## Caveats

- **MCP tool infrastructure failures** inflate failure rates for cases 001001, 004001, 004003, 005001, and 002005. When MCP tools fail, the agent cannot call `edit` or `azsdk_run_typespec_validation`, causing it to produce advisory text instead of code. These failures are infrastructure noise, not documentation gaps. Cases 004003 and 005001 are classified as systemic but their root cause is primarily infrastructure, not missing documentation.
- **LLM grader variance** affects flaky cases (001003, 001004). The LLM grader sometimes scores valid implementations slightly below threshold. These are eval noise.
- **Small sample sizes** — many cases have only 2 runs. Cases with 50% pass rate (1/2) are classified as flaky but could be systemic with more data.
- **Eval pattern strictness** — some cases (001002, 002001) show the agent producing valid TypeSpec that passes `tsc` validation but does not match the eval's expected regex pattern. The eval may be overly prescriptive for these cases, or the skill genuinely needs to document the preferred Azure convention.
- **Scope** — this analysis covers only the code-quality (`forced.eval.yaml`) job. Skill-invocation accuracy is out of scope.
