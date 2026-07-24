# Azure TypeSpec Author Skill - Eval Analysis

**Data:** 73 graded records across 18 runs, covering 29 unique test cases.

## Per-Suite Roll-Up

| Suite | Passes | Total | Rate |
|-------|--------|-------|------|
| armtemplate | 15 | 26 | 58% |
| decorators | 6 | 11 | 55% |
| lro | 3 | 5 | 60% |
| versioning | 9 | 27 | 33% |
| warning | 1 | 4 | 25% |
| **OVERALL** | **34** | **73** | **47%** |

## Per-Case Pass Rate (ascending)

| Case | Suite | Passes | Total | Rate | Classification |
|------|-------|--------|-------|------|----------------|
| 001001-version-spread-property-forced | versioning | 0 | 5 | 0% | Systemic |
| 001005-version-add-preview-after-preview-forced | versioning | 0 | 2 | 0% | Systemic |
| 001002-version-default-value-forced | versioning | 0 | 2 | 0% | Systemic |
| 001006-version-add-preview-after-stable-forced | versioning | 0 | 2 | 0% | Systemic |
| 001007-version-add-stable-after-preview-forced | versioning | 0 | 2 | 0% | Systemic |
| 001008-version-add-stable-after-stable-forced | versioning | 0 | 2 | 0% | Systemic |
| 002003-ARM-define-full-update-operation-forced | armtemplate | 0 | 2 | 0% | Systemic |
| 002001-ARM-change-resource-type-forced | armtemplate | 0 | 2 | 0% | Systemic |
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
| 004002-decorate-length-constrains-on-array-item-forced | decorators | 3 | 3 | 100% | PASS |
| 002002-ARM-define-extension-resource-add-forced | armtemplate | 3 | 3 | 100% | PASS |
| 002011-arm-add-check-existence-operation-forced | armtemplate | 3 | 3 | 100% | PASS |
| 001010-version-model-property-removed-forced | versioning | 2 | 2 | 100% | PASS |
| 001011-version-model-property-renamed-forced | versioning | 2 | 2 | 100% | PASS |
| 001013-version-model-property-type-changed-forced | versioning | 2 | 2 | 100% | PASS |
| 002006-ARM-define-child-resource-forced | armtemplate | 2 | 2 | 100% | PASS |
| 002007-ARM-define-custom-action-forced | armtemplate | 2 | 2 | 100% | PASS |
| 002010-arm-action-sync-operation-forced | armtemplate | 2 | 2 | 100% | PASS |
| 003002-arm-modify-response-forced | lro | 2 | 2 | 100% | PASS |

## Failure Analysis

### 001001-version-spread-property-forced (0/5 = 0%) - **Systemic**

**Suite:** versioning

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 5/5 failing run(s)
  - Evidence: The following required tools were not called: /edit/, /azure-sdk-mcp-azsdk_run_typespec_validation/ No disallowed tools were called
  - Evidence: The following required tools were not called: /edit/, /azure-sdk-mcp-azsdk_run_typespec_validation/ No disallowed tools were called
- **file-matches** (kind=`code`) - failed in 5/5 failing run(s)
  - Evidence: Pattern /@@added\(Employee\.identity, (Microsoft\.Widget\.)?Versions\.(v2025_05_04_preview|`2025-05-04-preview`)\);/ not found in any file matching 'employee.tsp'
  - Evidence: Pattern /@@added\(Employee\.identity, (Microsoft\.Widget\.)?Versions\.(v2025_05_04_preview|`2025-05-04-preview`)\);/ not found in any file matching 'employee.tsp'
- **prompt** (kind=`llm`) - failed in 5/5 failing run(s)
  - Evidence: The agent failed to actually implement any changes in the codebase. It provided generic advisory guidance after its tool call failed, without exploring the repository or making concrete edits. While the advice is directionally sound, a coding agent is expected to author actual code changes, not just
  - Evidence: The agent provides the correct core solution (@@added augment decorator) with clear, well-structured output. It correctly addresses the task without introducing unrelated changes. Only minor gap is not showing prerequisite setup (versioning enum, imports), but the key technique is accurate and actio
  - Rubric [2] The agent completed the requested task correctly: The agent's tool call failed, and it fell back to providing general guidance rather than actually implementing changes in the codebase. It never explored the repository, identified the relevant files,
  - Rubric [4] The output is clear and well-structured: The response is well-organized with clear headings, code snippets, an explanation of why the spread broke things, and numbered key steps. It's easy to follow as guidance, even though it doesn't consti

**Root cause:** Agent did not call `edit` tool (MCP tool call failed, agent fell back to advice); Expected file content patterns were not found in agent output

### 001005-version-add-preview-after-preview-forced (0/2 = 0%) - **Systemic**

**Suite:** versioning

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: The following required tools were not called: /web_fetch/ No disallowed tools were called
  - Evidence: The following required tools were not called: /web_fetch/ No disallowed tools were called
- **file-exists** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: No files matching 'examples/2025-05-04-preview/Employees_Get_MaximumSet_Gen.json' found
  - Evidence: No files matching 'examples/2025-05-04-preview/Employees_Get_MaximumSet_Gen.json' found
- **file-not-exists** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Files matching 'examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json' found: examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json
  - Evidence: Files matching 'examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json' found: examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json
- **file-not-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /2024-10-01-preview/ matched in main.tsp
  - Evidence: Pattern /oldAge/ matched in employee.tsp
- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /@added\(Versions\.v2025_05_04_preview\)\s+workLocation\?: WorkLocation;/ not found in any file matching 'employee.tsp'
  - Evidence: Pattern /@typeChangedFrom\(Versions\.v2025_05_04_preview, string\[\]\)/ not found in any file matching 'employee.tsp'

**Root cause:** Agent did not call `web_fetch` to retrieve example JSON from the spec repo (required by eval); Expected file content patterns were not found in agent output

### 001002-version-default-value-forced (0/2 = 0%) - **Systemic**

**Suite:** versioning

**Failing graders:**

- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /@removed\(Versions\.v2025_11_01\)/ not found in any file matching 'employee.tsp'
  - Evidence: Pattern /@renamedFrom\(Versions\.v2025_11_01, "age"\)/ not found in any file matching 'employee.tsp'
- **prompt** (kind=`llm`) - failed in 2/2 failing run(s)
  - Evidence: The agent made a reasonable and TypeSpec-idiomatic solution using `@madeDefault`, which passed validation. The changes are minimal, focused, and well-explained. The only concern is whether the evaluation expected an explicit rename/remove pattern for the old property rather than relying on `@madeDef
  - Evidence: The agent added a default value for `age` that is version-gated, and validation passed. However, the evaluation instructions explicitly state the expected implementation involves renaming and removing the old `age` property for the new version, which the agent did not do. Using `@typeChangedFrom` is
  - Rubric [4] The agent completed the requested task correctly: The agent correctly identified and used the `@madeDefault(Versions.v2025_11_01)` decorator along with setting `age?: int32 = 21`. This is the TypeSpec-idiomatic way to add a version-specific default v
  - Rubric [5] The output is clear and well-structured: The agent's summary clearly explains the two changes made (adding `using TypeSpec.Versioning` and applying `@madeDefault`), and provides a concise explanation of what the decorator does and why older 

**Root cause:** Expected file content patterns were not found in agent output

### 001006-version-add-preview-after-stable-forced (0/2 = 0%) - **Systemic**

**Suite:** versioning

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: The following required tools were not called: /web_fetch/ No disallowed tools were called
  - Evidence: The following required tools were not called: /web_fetch/ No disallowed tools were called

**Root cause:** Agent did not call `web_fetch` to retrieve example JSON from the spec repo (required by eval)

### 001007-version-add-stable-after-preview-forced (0/2 = 0%) - **Systemic**

**Suite:** versioning

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: The following required tools were not called: /web_fetch/ No disallowed tools were called
  - Evidence: The following required tools were not called: /web_fetch/ No disallowed tools were called
- **file-exists** (kind=`code`) - failed in 1/2 failing run(s)
  - Evidence: No files matching 'examples/2025-01-01/Employees_Get_MaximumSet_Gen.json' found
- **file-not-exists** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Files matching 'examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json' found: examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json
  - Evidence: Files matching 'examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json' found: examples/2024-10-01-preview/Employees_Get_MaximumSet_Gen.json
- **file-not-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /2024-10-01-preview/ matched in main.tsp
  - Evidence: Pattern /workLocation\?: WorkLocation;/ matched in employee.tsp
- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /@removed\(Versions\.v2025_01_01\)\s*@renamedFrom\(Versions\.v2025_01_01, "age"\)\s*oldAge\?: int32;/ not found in any file matching 'employee.tsp'
  - Evidence: Pattern /@added\(Versions\.v2025_01_01\)\s*age\?: int32 = 21;/ not found in any file matching 'employee.tsp'

**Root cause:** Agent did not call `web_fetch` to retrieve example JSON from the spec repo (required by eval); Expected file content patterns were not found in agent output

### 001008-version-add-stable-after-stable-forced (0/2 = 0%) - **Systemic**

**Suite:** versioning

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: The following required tools were not called: /web_fetch/ No disallowed tools were called
  - Evidence: The following required tools were not called: /web_fetch/ No disallowed tools were called
- **file-exists** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: No files matching 'examples/2025-01-01/Employees_Get_MaximumSet_Gen.json' found
  - Evidence: No files matching 'examples/2025-01-01/Employees_Get_MaximumSet_Gen.json' found

**Root cause:** Agent did not call `web_fetch` to retrieve example JSON from the spec repo (required by eval); Expected file content patterns were not found in agent output

### 002003-ARM-define-full-update-operation-forced (0/2 = 0%) - **Systemic**

**Suite:** armtemplate

**Failing graders:**

- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /update\s+is\s+ArmCustomPatchSync\s*</ not found in any file matching '*.tsp'
  - Evidence: Pattern /update\s+is\s+ArmCustomPatchSync\s*</ not found in any file matching '*.tsp'

**Root cause:** Expected file content patterns were not found in agent output

### 002001-ARM-change-resource-type-forced (0/2 = 0%) - **Systemic**

**Suite:** armtemplate

**Failing graders:**

- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /Extension\.Read<|Extension\.CreateOrReplaceAsync<|Extension\.CustomPatch(Sync|Async)<|Extension\.DeleteWithoutOkAsync<|Extension\.ListByTarget</ not found in any file matching '*.tsp'
  - Evidence: Pattern /Extension\.Read<|Extension\.CreateOrReplaceAsync<|Extension\.CustomPatch(Sync|Async)<|Extension\.DeleteWithoutOkAsync<|Extension\.ListByTarget</ not found in any file matching '*.tsp'
- **prompt** (kind=`llm`) - failed in 2/2 failing run(s)
  - Evidence: The agent made the core changes correctly (TrackedResource → ExtensionResource, consolidated list operations, removed subscription-level list), and validation passed. However, the evaluation instructions explicitly require a scope-parameterized interface to be defined for the extension resource, whi
  - Evidence: The agent made several correct changes (ExtensionResource base type, removing ManagedServiceIdentity, removing listBySubscription) and validation passed. However, it failed to implement the scope-parameterized interface pattern that the evaluation instructions explicitly require for proper extension
  - Rubric [3] The agent completed the requested task correctly: The agent correctly changed TrackedResource to ExtensionResource, removed listBySubscription, and replaced listByResourceGroup with listByParent using ArmResourceListByParent. TypeSpec validation pass
  - Rubric [5] The output is clear and well-structured: The agent provided a clear summary table showing before/after changes, explained the rationale for each change, and verified the result with validation. The session timeline shows a logical progressio

**Root cause:** Expected file content patterns were not found in agent output

### 002009-arm-add-patch-operation-to-resource-forced (0/2 = 0%) - **Systemic**

**Suite:** armtemplate

**Failing graders:**

- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /update\s+is\s+ArmCustomPatchSync\s*</ not found in any file matching 'employee.tsp'
  - Evidence: Pattern /update\s+is\s+ArmCustomPatchSync\s*</ not found in any file matching 'employee.tsp'

**Root cause:** Expected file content patterns were not found in agent output

### 005001-warning-suppress-warning-forced (1/4 = 25%) - **Systemic**

**Suite:** warning

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 3/3 failing run(s)
  - Evidence: The following required tools were not called: /edit/ No disallowed tools were called
  - Evidence: The following required tools were not called: /azure-sdk-mcp-azsdk_run_typespec_validation/ No disallowed tools were called
- **file-matches** (kind=`code`) - failed in 1/3 failing run(s)
  - Evidence: Pattern /#suppress/ not found in any file matching 'models.tsp'
  - Evidence: Pattern /FIXME: Update justification, follow aka\.ms/tsp/conversion-fix for details/ not found in any file matching 'models.tsp'

**Root cause:** Agent did not call `edit` tool (MCP tool call failed, agent fell back to advice); Expected file content patterns were not found in agent output

### 002004-ARM-define-extension-resource-fromProxyResource-forced (1/3 = 33%) - **Systemic**

**Suite:** armtemplate

**Failing graders:**

- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /Extension\.Read<|Extension\.CreateOrReplaceAsync<|Extension\.CustomPatch(Sync|Async)<|Extension\.DeleteWithoutOkAsync</ not found in any file matching 'badgeAssignment.tsp'
  - Evidence: Pattern /Extension\.ListByTarget</ not found in any file matching 'badgeAssignment.tsp'
- **prompt** (kind=`llm`) - failed in 2/2 failing run(s)
  - Evidence: The agent made targeted, correct changes to convert a ProxyResource into an ExtensionResource with @parentResource(Employee), and validation passed. The output is clear and well-explained. Minor concern about whether @parentResource is the ideal pattern for extension resources vs. extensionScope, bu
  - Evidence: The agent correctly implemented the badge assignment as an ExtensionResource with @parentResource(Employee), validation passed, and the output was clear. The changes were minimal and focused on the task. Minor semantic question about whether ExtensionResource + @parentResource is the ideal pattern, 
  - Rubric [4] The agent completed the requested task correctly: The agent correctly identified the two key changes needed: switching from ProxyResource to ExtensionResource and adding @parentResource(Employee). TypeSpec validation passed. The changes are minimal a
  - Rubric [5] The output is clear and well-structured: The agent's summary clearly explains the two changes made, why they were made, and confirms validation passed. The final file was shown for verification.

**Root cause:** Expected file content patterns were not found in agent output

### 002008-ARM-add-parameters-forced (1/3 = 33%) - **Systemic**

**Suite:** armtemplate

**Failing graders:**

- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /listBySubscription\s+is\s+ArmListBySubscription\s*<\s*Employee\s*,\s*\{\s*\.\.\.(Azure\.Core\.)?TopQueryParameter;\s*\.\.\.(Azure\.Core\.)?SkipQueryParameter;\s*\}\s*>|model\s+\w+\s+(is|extends)\s+(Azure\.Core\.)?StandardListQueryParameters\s*;[\s\S]*?listBySubscription\s+is\s+ArmListBySubs
  - Evidence: Pattern /listBySubscription\s+is\s+ArmListBySubscription\s*<\s*Employee\s*,\s*\{\s*\.\.\.(Azure\.Core\.)?TopQueryParameter;\s*\.\.\.(Azure\.Core\.)?SkipQueryParameter;\s*\}\s*>|model\s+\w+\s+(is|extends)\s+(Azure\.Core\.)?StandardListQueryParameters\s*;[\s\S]*?listBySubscription\s+is\s+ArmListBySubs
- **file-not-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /@query\("\$top"\)|@query\("\$skip"\)/ matched in employee.tsp
  - Evidence: Pattern /@query\("\$top"\)|@query\("\$skip"\)/ matched in employee.tsp

**Root cause:** Expected file content patterns were not found in agent output

### 003001-arm-action-lro-forced (1/3 = 33%) - **Systemic**

**Suite:** lro

**Failing graders:**

- **file-matches** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: Pattern /move\s+is\s+ArmResourceActionAsync\s*<\s*Employee\s*,\s*MoveRequest\s*,\s*MoveResponse\s*,\s*LroHeaders\s*=/ not found in any file matching 'employee.tsp'
  - Evidence: Pattern /ArmCombinedLroHeaders\s*<\s*(FinalResult\s*=\s*MoveResponse|ArmOperationStatus\s*,\s*MoveResponse|(Azure\.ResourceManager\.)?ArmOperationStatus\s*,\s*MoveResponse)\s*>/ not found in any file matching 'employee.tsp'

**Root cause:** Expected file content patterns were not found in agent output

### 004003-delete-and-restore-operationId-decorator-forced (1/3 = 33%) - **Systemic**

**Suite:** decorators

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 2/2 failing run(s)
  - Evidence: The following required tools were not called: /edit/, /azure-sdk-mcp-azsdk_run_typespec_validation/ No disallowed tools were called
  - Evidence: The following required tools were not called: /edit/, /azure-sdk-mcp-azsdk_run_typespec_validation/ No disallowed tools were called

**Root cause:** Agent did not call `edit` tool (MCP tool call failed, agent fell back to advice)

### 004001-decorate-mgmt-resource-name-parameter-forced (2/5 = 40%) - **Systemic**

**Suite:** decorators

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 2/3 failing run(s)
  - Evidence: The following required tools were not called: /edit/, /azure-sdk-mcp-azsdk_run_typespec_validation/ No disallowed tools were called
  - Evidence: The following required tools were not called: /edit/, /azure-sdk-mcp-azsdk_run_typespec_validation/ No disallowed tools were called
- **file-matches** (kind=`code`) - failed in 3/3 failing run(s)
  - Evidence: Pattern /@@minLength\(Employee\.name,\s*1\)/ not found in any file matching '*.tsp'
  - Evidence: Pattern /@@minLength\(Employee\.name,\s*1\)/ not found in any file matching '*.tsp'
- **prompt** (kind=`llm`) - failed in 3/3 failing run(s)
  - Evidence: The agent completely failed to perform any work. Zero tool calls were made, and no output was produced.
  - Evidence: The agent correctly implemented the minLength(1) constraint on the Employee resource name parameter using an appropriate TypeSpec pattern (custom scalar + ResourceNameParameter type parameter). The change compiled successfully, was scoped to the relevant file, and the explanation was clear.
  - Rubric [1] The agent completed the requested task correctly: The agent produced no output and made no tool calls. The MCP server failed to load, and the agent did not attempt any recovery or alternative approach.
  - Rubric [1] The output is clear and well-structured: There is no output at all from the agent.

**Root cause:** Agent did not call `edit` tool (MCP tool call failed, agent fell back to advice); Expected file content patterns were not found in agent output

### 001003-version-required-to-optional-forced (1/2 = 50%) - **Flaky/one-off**

**Suite:** versioning

**Failing graders:**

- **prompt** (kind=`llm`) - failed in 1/1 failing run(s)
  - Evidence: The agent correctly applied the @madeOptional decorator with the right version enum and changed the property to required at the base level, achieving version-specific optionality. Validation passed and no unrelated edits were made. The only minor concern is that the original property was already opt
  - Rubric [4] The agent completed the requested task correctly: The agent used `@madeOptional(Microsoft.Widget.Versions.v2025_05_04_preview)` and changed `age?: int32` to `age: int32`, making `age` required by default but optional in the 2025-05-04-preview version
  - Rubric [5] The output is clear and well-structured: The agent clearly explained what was changed, why, and what the resulting behavior is per API version. The summary is concise and accurate.

**Root cause:** LLM grader scored implementation as incomplete or incorrect

### 001004-version-property-decorator-forced (1/2 = 50%) - **Flaky/one-off**

**Suite:** versioning

**Failing graders:**

- **prompt** (kind=`llm`) - failed in 1/1 failing run(s)
  - Evidence: The agent successfully implemented the requested change using an appropriate TypeSpec versioning pattern. The original property is marked @removed in the preview version, and a new property with the updated visibility is @added with @encodedName to preserve wire compatibility. Validation passed with
  - Rubric [4] The agent completed the requested task correctly: The agent correctly changed the visibility of provisioningState from Lifecycle.Read to Lifecycle.Read and Lifecycle.Create in version 2025-05-04-preview only. It used the standard @removed/@added vers
  - Rubric [5] The output is clear and well-structured: The agent's summary clearly explains what was changed for each version, the pattern used (remove-and-re-add), and why @encodedName was needed. The session timeline shows a logical progression of explo

**Root cause:** LLM grader scored implementation as incomplete or incorrect

### 001009-version-model-property-required-forced (1/2 = 50%) - **Flaky/one-off**

**Suite:** versioning

**Failing graders:**

- **file-matches** (kind=`code`) - failed in 1/1 failing run(s)
  - Evidence: Pattern /email: string;/ not found in any file matching 'Microsoft.Widget/Widget/employee.tsp'

**Root cause:** Expected file content patterns were not found in agent output

### 002005-ARM-define-the-resource-forced (1/2 = 50%) - **Flaky/one-off**

**Suite:** armtemplate

**Failing graders:**

- **tool-calls** (kind=`code`) - failed in 1/1 failing run(s)
  - Evidence: The following required tools were not called: /edit/, /azure-sdk-mcp-azsdk_run_typespec_validation/ No disallowed tools were called
- **file-matches** (kind=`code`) - failed in 1/1 failing run(s)
  - Evidence: Pattern /model Employee is TrackedResource<EmployeeProperties>/ not found in any file matching '*.tsp'
  - Evidence: Pattern /age\?: int32/ not found in any file matching '*.tsp'
- **prompt** (kind=`llm`) - failed in 1/1 failing run(s)
  - Evidence: The agent successfully created a TypeSpec file defining the Employee resource under Microsoft.Widget namespace. The design decisions described align with Azure ARM TypeSpec guidelines. The summary is clear and well-structured. The only limitation is that the full file content isn't visible in the se
  - Rubric [4] The agent completed the requested task correctly: The agent created a TSP file with the Employee resource definition. Based on the visible imports, decorators, and the summary table, it appears to correctly use TrackedResource<EmployeeProperties>, in
  - Rubric [5] The output is clear and well-structured: The agent provided a well-organized summary with a clear design decisions table explaining each aspect (base type, provisioningState, visibility, operations, naming) along with the rationale. The file

**Root cause:** Agent did not call `edit` tool (MCP tool call failed, agent fell back to advice); Expected file content patterns were not found in agent output

## Cross-Cutting Observations

### MCP Tool Infrastructure Failures

Several cases (001001, 004001, 004003, 005001, 002005) show the agent's MCP tool call failing, causing it to fall back to advisory text instead of making code changes. This is an **infrastructure/reliability issue**, not a documentation gap. When the MCP tool works, the agent often produces correct output (as seen in passing runs of the same cases). This inflates failure rates for cases that would otherwise pass.

### `web_fetch` Requirement for Version Bump Cases

All four "add version" cases (001005-001008) require calling `web_fetch` to retrieve example JSON files from the spec repo. The agent consistently fails to do this, suggesting the skill documentation does not instruct the agent to fetch and restructure example JSON files when bumping API versions.

### Pattern Mismatch: Agent Uses Valid But Non-Expected Approaches

In 001002 (default value), the agent uses `@madeDefault` (TypeSpec-idiomatic) instead of the expected `@removed/@renamedFrom/@added` pattern. The LLM grader scores this 4/5 for correctness, but code graders fail it. This suggests either the eval expects a too-specific pattern, or the skill should document the Azure SDK-preferred approach explicitly.

## Candidate Documentation Gaps

| # | Documentation Gap | Cases Affected | Doc Gap? | Justification |
|---|-------------------|----------------|----------|---------------|
| 1 | Versioning spread properties with `@@added` augment decorator | 001001-version-spread-property-forced | Yes | The @@added augment pattern for spread properties is a niche TypeSpec versioning pattern not well documented in general TypeSpec docs. |
| 2 | Adding new API versions (preview/stable) with full file restructuring | 001005-version-add-preview-after-preview-forced, 001006-version-add-preview-after-stable-forced, 001007-version-add-stable-after-preview-forced, 001008-version-add-stable-after-stable-forced | Yes | The skill docs likely lack a step-by-step workflow for bumping API versions including example file management, main.tsp updates, and property migration with decorators like @removed/@renamedFrom. |
| 3 | Adding default values with proper versioning decorators (@removed/@renamedFrom/@added) | 001002-version-default-value-forced | Yes | The expected pattern (remove old property, rename, add new with default) is a specific Azure SDK convention not covered by generic TypeSpec documentation. The skill should document this pattern explicitly. |
| 4 | Using ArmCustomPatchSync for full update/patch operations | 002003-ARM-define-full-update-operation-forced, 002009-arm-add-patch-operation-to-resource-forced | Yes | ArmCustomPatchSync is an ARM-specific template that may not be documented in the skill references. |
| 5 | Changing ARM resource types (e.g., to Extension resources with proper operation templates) | 002001-ARM-change-resource-type-forced | Yes | The Extension resource operation templates are ARM-specific and may not be covered in skill documentation. |
| 6 | Suppressing TypeSpec warnings with #suppress directive | 005001-warning-suppress-warning-forced | No | This is likely a flaky/model inconsistency issue rather than a documentation gap, as the agent passes sometimes. The #suppress pattern is relatively straightforward. |
| 7 | Deleting and restoring @operationId decorators | 004003-delete-and-restore-operationId-decorator-forced | No | Likely model inconsistency. The @operationId pattern is standard TypeSpec. |
| 8 | ARM action LRO (long-running operation) patterns | 003001-arm-action-lro-forced | Yes | ARM LRO patterns require specific templates and decorators that may not be fully documented in the skill. |
| 9 | Decorating management resource name parameters | 004001-decorate-mgmt-resource-name-parameter-forced | Yes | Mgmt resource name parameter decoration patterns may not be well covered in skill docs. |

## Detailed Gap Descriptions

### Gap 1: Versioning spread properties with `@@added` augment decorator

**Affected cases:** 001001-version-spread-property-forced

**Is documentation gap:** Yes

**Evidence:** Agent consistently fails to use @@added augment decorator for spread-in properties. MCP tool calls fail and agent falls back to advisory text. The skill docs may not cover how to version-gate properties inherited via `...` spread syntax.

**Justification:** The @@added augment pattern for spread properties is a niche TypeSpec versioning pattern not well documented in general TypeSpec docs.

### Gap 2: Adding new API versions (preview/stable) with full file restructuring

**Affected cases:** 001005-version-add-preview-after-preview-forced, 001006-version-add-preview-after-stable-forced, 001007-version-add-stable-after-preview-forced, 001008-version-add-stable-after-stable-forced

**Is documentation gap:** Yes

**Evidence:** Agent fails to: (1) call web_fetch to get example JSON from spec repo, (2) restructure example directories for new version, (3) update main.tsp service version, (4) properly remove old version artifacts. The eval expects a comprehensive version bump workflow including example JSON migration.

**Justification:** The skill docs likely lack a step-by-step workflow for bumping API versions including example file management, main.tsp updates, and property migration with decorators like @removed/@renamedFrom.

### Gap 3: Adding default values with proper versioning decorators (@removed/@renamedFrom/@added)

**Affected cases:** 001002-version-default-value-forced

**Is documentation gap:** Yes

**Evidence:** Agent uses @madeDefault or @typeChangedFrom instead of the expected @removed/@renamedFrom/@added pattern. The eval expects a specific rename-old-property-and-add-new pattern.

**Justification:** The expected pattern (remove old property, rename, add new with default) is a specific Azure SDK convention not covered by generic TypeSpec documentation. The skill should document this pattern explicitly.

### Gap 4: Using ArmCustomPatchSync for full update/patch operations

**Affected cases:** 002003-ARM-define-full-update-operation-forced, 002009-arm-add-patch-operation-to-resource-forced

**Is documentation gap:** Yes

**Evidence:** Agent does not produce code using ArmCustomPatchSync template for update operations. May use standard PATCH patterns instead.

**Justification:** ArmCustomPatchSync is an ARM-specific template that may not be documented in the skill references.

### Gap 5: Changing ARM resource types (e.g., to Extension resources with proper operation templates)

**Affected cases:** 002001-ARM-change-resource-type-forced

**Is documentation gap:** Yes

**Evidence:** Agent fails to use Extension-specific operation templates (Extension.Read, Extension.CreateOrReplaceAsync, etc.).

**Justification:** The Extension resource operation templates are ARM-specific and may not be covered in skill documentation.

### Gap 6: Suppressing TypeSpec warnings with #suppress directive

**Affected cases:** 005001-warning-suppress-warning-forced

**Is documentation gap:** No

**Evidence:** Agent sometimes fails this case (25% pass rate), suggesting inconsistent handling.

**Justification:** This is likely a flaky/model inconsistency issue rather than a documentation gap, as the agent passes sometimes. The #suppress pattern is relatively straightforward.

### Gap 7: Deleting and restoring @operationId decorators

**Affected cases:** 004003-delete-and-restore-operationId-decorator-forced

**Is documentation gap:** No

**Evidence:** Agent fails in 2/3 runs for this case.

**Justification:** Likely model inconsistency. The @operationId pattern is standard TypeSpec.

### Gap 8: ARM action LRO (long-running operation) patterns

**Affected cases:** 003001-arm-action-lro-forced

**Is documentation gap:** Yes

**Evidence:** Agent fails in 2/3 runs.

**Justification:** ARM LRO patterns require specific templates and decorators that may not be fully documented in the skill.

### Gap 9: Decorating management resource name parameters

**Affected cases:** 004001-decorate-mgmt-resource-name-parameter-forced

**Is documentation gap:** Yes

**Evidence:** Agent fails 3/5 runs (40% pass rate).

**Justification:** Mgmt resource name parameter decoration patterns may not be well covered in skill docs.
