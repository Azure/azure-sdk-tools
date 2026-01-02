# Spec: Customizing Clients

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals and Exceptions/Limitations](#goals-and-exceptionslimitations)
- [Design Proposal](#design-proposal)
- [Usage Scenarios for Testing](#usage-scenarios-for-testing)
- [Pipeline/CI Considerations](#pipelineci-considerations)
- [Success Criteria](#success-criteria)
- [Open Questions](#open-questions)
- [Alternatives Considered](#alternatives-considered)

---

## Definitions

_Terms used throughout this spec with precise meanings:_

- **TypeSpec**: The specification language used to define Azure service APIs. SDK code is generated from TypeSpec specifications located in the azure-rest-api-specs repository.

- **<a id="typespec-customizations"></a>TypeSpec Customizations**: SDK-specific customizations made in the `client.tsp` file to control SDK generation. These include decorators, naming adjustments, and grouping modifications. This term does not refer to modifications of the service API TypeSpec files.

- **<a id="code-customizations"></a>Code Customizations**: Language-specific modifications applied to generated SDK code as a **post-emitter generation step**. These customizations are automatically reapplied during SDK regeneration through language-specific mechanisms (e.g., Java customization classes, Python `_patch.py` files, .NET partial classes). Examples include adding convenience methods, custom error handling, language-specific optimizations, imports, visibility modifiers, reserved keyword renames, and annotations. Also known as "handwritten code" or "customization layer."

- **API View**: A web-based tool for reviewing SDK APIs. It allows language architects and SDK team members to provide feedback on just the SDK APIs without needing to understand the underlying implementation.

- **<a id="customization-workflow"></a>Customization Workflow**: The end-to-end AI-assisted interactive process triggered by various entry points (build failures, API view feedback, PR comments, user prompts, etc.) that applies fixes and customizations to ensure SDK functionality. Requires mandatory user approval before applying changes.

- **<a id="approval-checkpoint"></a>Approval Checkpoint**: A mandatory user confirmation step before applying changes, implemented through the agent UI in MCP contexts.

- **<a id="entry-points"></a>Entry Points**: Various triggers that initiate the customization workflow including build failures, API view comments, PR feedback, user prompts, and linting/typing checks.

---

## Background / Problem Statement

Service teams spend significant manual effort updating customized code when regenerating SDKs from updated TypeSpec specifications, leading to compilation failures and time-consuming manual fixes that can take months of back-and-forth communication. TypeSpec specs are written to describe service APIs, however our team supports generating SDKs in no less than 7 languages, each differing in their idioms. There are two primary types of customizations that users can apply, each with distinct challenges:

**[TypeSpec Customizations](#typespec-customizations)** face several problems: service teams cannot be expected to understand SDK API patterns and best practices on their own, AI agents make mistakes when applying customizations even with existing documentation, particularly when `scope` is required or customizations span multiple languages, and there's no shared process across language teams for identifying when TypeSpec customizations are needed.

**[Code Customizations](#code-customizations)** face several challenges: different languages support various preservation mechanisms that need individual maintenance, manual effort required to update customized code when TypeSpec specifications change, and compilation failures when customizations don't match new generated code.

### Current State

Currently, feedback and triggers for applying both TypeSpec and SDK code customizations can come from various sources:

1. **From reviews**: API View feedback, PR comments requesting naming changes or SDK restructuring
2. **From automated processes**:
   - **Build failures**: Compilation errors, syntax issues, import problems
   - **Code analyzers**: .NET API compat tool, linting violations (mypy, flake8)
   - **Typing checks**: Python mypy validation, other language-specific type checking
3. **From manually triggered processes**: Breaking changes analysis, changelog review, manual user prompts through conversational interfaces

#### TypeSpec Customization Workflows by Language

**.NET**:
The .NET SDK has code analyzers and an [API compat](https://learn.microsoft.com/en-us/dotnet/fundamentals/apicompat/overview) tool that run during their SDK build (`dotnet build`) that identifies problematic code (e.g. class names that don't follow .NET naming conventions) and suggests fixes.
Many of these fixes are handled by adding customizations to `client.tsp` for .NET, such as renaming models with `@clientName`. Currently these fixes are manually applied after SDK builds fail.
_.NET has a clear flow for identifying and applying `client.tsp` customizations via: Emit SDK -> Build SDK -> Apply fixes -> Repeat._

**Go**:
The Go SDK has a [breaking changes](https://github.com/Azure/azure-sdk-for-go/blob/instruction/.github/prompts/go-sdk-breaking-changes-review.prompts.md) prompt that analyzes the generated SDK's changelog for specific patterns and maps those to `client.tsp` customizations where possible.
_We don't currently have a clear step in our process for identifying breaking changes, but the flow for Go is: Emit SDK -> Analyze changelog & apply fixes -> Repeat._

**Python**:
The Python SDK has [breaking changes](https://github.com/Azure/azure-sdk-for-python/tree/main/scripts/breaking_changes_checker) checks that may be resolvable via `client.tsp` customizations, but there is no automated process for identifying or applying these client.tsp customizations today.

**Java**:
Java SDKs use TypeSpec configuration to control code customization behavior:

- **Partial Update Configuration (`partial-update: true`)**: Configured in TypeSpec to enable developers to edit generated code directly, with the emitter preserving manual changes during regeneration. Used by [23+ services](https://github.com/search?q=repo%3AAzure%2Fazure-rest-api-specs%20partial-update%3A%20true&type=code). This TypeSpec setting enables the emitter to preserve customizations during code generation.

#### SDK Code Customization Workflows by Language

**Java**:
SDKs handle code customizations through:

- **Customization Classes (`customization-class`)**: Separate Java classes that modify generated code using AST manipulation. Used by [12+ services](https://github.com/search?q=repo%3AAzure%2Fazure-rest-api-specs+customization-class%3A&type=code). Handles complex transformations like method visibility changes and reserved keyword conflicts.

**Python**:
Python SDKs handle code customizations through:

- **`_patch.py` Files**: The primary mechanism for Python SDK customizations, where developers create `_patch.py` files that modify generated code through function replacement. This allows for adding convenience methods, modifying class behavior, and implementing Python-specific patterns while preserving changes during regeneration.

#### Cross-Language Challenges

**All Languages**:
All language SDKs have their APIs reviewed in API View. Common feedback includes renaming models per language, or changing operations that appear in a client. We have documentation for customizing SDKs such as [renaming types](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/09renaming/), but recent (10/14/2025) testing shows that even with the current documentation, AI agents have difficulty applying these customizations correctly in a consistent manner: particularly when `scope` is required or customizations are spread over multiple languages/sessions. For example, when creating custom clients using `@client` with different naming for different languages, many times the agent would properly create the custom client for the first language prompted, but then incorrectly use a `@clientName` for the next language prompted.

#### Gaps

The current state exposes gaps in both TypeSpec customization and code customization workflows:

**TypeSpec Customization Gaps:**

- **AI agents are not experts on client customizations.** They make mistakes when applying customizations to `client.tsp`, even with access to the existing documentation.
- **AI agents don't know when to suggest client customizations.** There is no mechanism in azsdk-cli today to steer AI agents to focus on applying `client.tsp` customizations.
- **Nothing shared across languages.** Each language team has their own process for identifying and applying `client.tsp` customizations.
- **Difficult to apply changes from outside the inner loop.** We lack a process for updating `client.tsp` from outside the azure-rest-api-specs repo.

**Code Customization Workflow Gaps:**

- **Manual maintenance burden.** Service teams must manually update partial-update files, `_patch.py` files, and customization classes when TypeSpec specifications change, often leading to compilation failures.
- **Outdated customization logic.** Customization classes and AST manipulation logic become outdated when generated code structure changes, requiring manual updates.
- **No automated conflict detection.** There's no automated way to detect when code customizations conflict with new generated code patterns.
- **Cross-language inconsistency.** Each language has different approaches for applying and maintaining code customizations, with no shared tooling or processes.
- **Preservation complexity.** Different preservation mechanisms (partial-update, `_patch.py`, customization classes, partial classes) require language-specific knowledge and maintenance.

### Why This Matters

We should minimize the amount of time service teams spend on customizing client SDKs. Let them focus on what they are experts on - the service API - instead of figuring out the right way to apply SDK customizations.

We can reduce the time spent in the outer loop by detecting and handling common client customization scenarios automatically while devs are still in the inner loop. When service teams still have feedback to address from the outer loop, we should make it both easy to apply and to verify those changes are correct.

SDK teams also shouldn't each be responsible for teaching AI agents how to apply `client.tsp` customizations. This should be a shared responsibility that the azsdk-cli can help facilitate.

---

## Goals and Exceptions/Limitations

### Goals

What are we trying to achieve with this design?

- [ ] Improve the service team experience for authoring `client.tsp` customizations
- [ ] Simplify the process of _how_ to apply `client.tsp` customizations for language teams
- [ ] Automate the detection and resolution of build failures through intelligent application of both TypeSpec and code customizations
- [ ] Provide a unified approach for handling both TypeSpec and SDK code customizations through a single workflow
- [ ] Enable AI agents to become experts in both TypeSpec decorators and language-specific code customization patterns

### Limitations

**Phase B (Code Customizations) Scope:**

Phase B focuses initially on **deterministic, mechanical transformations** where fixes are unambiguous:

- ✅ **In Scope**: Duplicate field detection and removal, variable reference updates after renames, simple build fixes (imports, visibility modifiers, reserved keyword renames, type mismatches)
- ⚠️ **Stretch Goal**: Complex convenience methods, behavioral changes affecting SDK semantics, intricate AST manipulation patterns
- ❌ **Out of Scope**: Architectural decisions about SDK surface design, complex language-specific idioms requiring deep domain expertise

**Rationale:** The approval checkpoint provides a safety mechanism allowing us to attempt Phase B automation for mechanical fixes while users retain final control. Complex transformations require additional validation and may be deferred to future iterations based on real-world success rates.

---

## Design Proposal

### Overview

The design will address the identified gaps by implementing a unified two-phase customization workflow that:

1. **Creates AI agent expertise** through a shareable TypeSpec client customizations reference document that teaches agents how to apply `Azure.ClientGenerator.Core` decorators effectively.
2. **Enhances the existing CustomizedCodeUpdateTool** to implement intelligent Phase A (TypeSpec customizations) and Phase B (code customizations) remediation, providing a spec-first approach to build failure resolution.
3. **Enables cross-repository workflow** through git patch support, allowing `client.tsp` customizations to be applied from SDK repositories back to azure-rest-api-specs.

### Detailed Design

#### 1. TypeSpec client customizations reference document

A TypeSpec client customizations reference document will serve as the foundation for teaching AI agents about the customizations available via the `Azure.ClientGenerator.Core` library. It includes concise documentation and examples for the decorators available in the library, as well as cover some common scenarios.

This is a living document - it can be updated over time as new decorators are added to the library or new common scenarios are identified. This is _not_ intended to contain guidance for every scenario each language SDK may encounter. For example, Go breaking changes detection looks for specific patterns in the changelog to identify client.tsp customizations. These patterns would not be covered in this document.

This lives in `eng/common/knowledge/customizing-client-tsp.md` so that all language SDK repos can reference it when needed.

#### Usage

This document will be referenced by [eng/common/instructions/azsdk-tools/typespec-docs.instructions.md](eng/common/instructions/azsdk-tools/typespec-docs.instructions.md). These instructions are already included in the azure-rest-api-specs repo when the user asks TypeSpec-related questions.

Additionally, it can be referenced by custom prompts or agents when knowledge of how to apply client.tsp customizations is needed.

#### 2. Package Customize Code Tool

There is an existing [CustomizedCodeUpdateTool](https://github.com/Azure/azure-sdk-tools/blob/main/tools/azsdk-cli/Azure.Sdk.Tools.Cli/Tools/TypeSpec/CustomizedCodeUpdateTool.cs) that supports updating customized SDK code after generating an SDK from TypeSpec. Splitting `client.tsp` customization into its own tool (see alternative 1) will put the burden on our users to know whether TypeSpec or SDK code customizations are required to address feedback or breakages. Applying and verifying `client.tsp` customizations should be merged with this existing tool.

Name (MCP): `azsdk_package_customize_code`

#### Current Inputs

- `--package-path`/`packagePath`: The path to the package (SDK) directory to check. Lives in one of the `azure-sdk-for-*` repos.
- `--update-commit-sha`/`commitSha`: The SHA of the commit in `azure-rest-api-specs` to use when regenerating the SDK.

#### Current behavior

The tool currently works primarily in an `azure-sdk-for-*` repo.

```mermaid
flowchart TD
    Gen[<b>1. SDK Code Generation</B><br/>• Generate SDK code using updated sha<br/>]

    Gen --> Patch[<b>2. Code Customization Updates</b><br/>• AI generates patches from diff + current code<br/>• Apply patches using ClientCustomizationCodePatchTool<br/>]

    Patch --> Validate[<b>3. Validation and Iteration</b><br/>• Compile SDK code<br/>• If build fails → retry AI patching loop max 2x<br/>]

    style Gen fill:#fff9c4
    style Patch fill:#f3e5f5
    style Validate fill:#e8f5e9
```

### Proposed Changes

The existing CustomizedCodeUpdateTool will be enhanced to implement a two-phase customization workflow:

**Enhanced Tool Behavior:**

The tool accepts customization requests from multiple [entry points](#entry-points) through the `customizationRequest` parameter. This unified text input allows the tool to handle:

- Build failures (compilation errors, linting, typing checks)
- User prompts (natural language requests from agents)
- API review feedback (comments from API View or PRs)
- Breaking changes analysis output

**Two-Phase Workflow with Classifier:**

1. **Context Classifier**
   The classifier uses an LLM to analyze the customization request and determines which phase to move to. There are 4 possible outcomes:

   - _Proceed to Phase A_: Chosen if TypeSpec customizations can address any of the outstanding points in the customization request
   - _Proceed to Phase B_: Chosen if changes are needed, and none of them can be addressed via TypeSpec customizations
   - _Failure_: Stall detection. Chosen if changes are needed and either phase has repeated errors
   - _Success_: Chosen if no changes further changes are needed

1. **Phase A – [TypeSpec Customizations](#typespec-customizations):**

   - Analyze the customization request to determine if TypeSpec decorators can address the issues
   - Apply `client.tsp` adjustments (decorators, naming, grouping, scope configurations)
   - Re-run TypeSpec compilation and regenerate SDK code
   - Validate build and proceed to classifier

1. **Phase B – [Code Customizations](#code-customizations):**

   - Apply language-specific code patches
   - Focus on **deterministic patterns**: duplicate field removal, variable reference updates, simple build fixes
   - Use existing ClientCustomizationCodePatchTool for SDK code modifications
   - Apply mechanical transformations: imports, visibility modifiers, reserved keyword renames, type mismatches
   - **Note**: Complex convenience methods and behavioral changes are stretch goals requiring additional validation
   - Validate final build

1. **Summary & Approval:**
   - Present summary of changes made to local repository files (TypeSpec and SDK code)
   - Enforce approval before commit through agent UI
   - Maximum of 2 fix cycles to prevent infinite loops
   - Provide next step instructions for users

#### New inputs

- `--package-path`/`packagePath`: The path to the package (SDK) directory to check. Lives in one of the `azure-sdk-for-*` repos.
- `--customization-request`/`customizationRequest`: A text blob containing the customization request. This supports multiple [entry points](#entry-points):
  - **Build failures**: Compilation errors, linting violations, typing check failures
  - **User prompts**: Natural language requests like "rename the FooClient to BarClient for .NET"
  - **API review feedback**: Feedback from API View or PR comments
  - **Breaking changes**: Output from breaking changes analysis tools
- [optional] `--typespec-project-path`/`typespecProjectPath`: The path to the TypeSpec project directory containing `tspconfig.yaml`. Used when operating from the azure-rest-api-specs repository to specify which TypeSpec project to work with.

**Workflow:**

```mermaid
flowchart TD
    Entry[<b>1. Entry Point</b><br/>• Build failures provide error messages<br/>• User prompts provide natural language requests<br/>• API review provides feedback comments<br/>• Breaking changes analysis provides change details]

    Entry --> Classify

    subgraph Tool [CustomizeCodeTool]
        Classify[<b>Classify Context</b><br/>]
        Classify --> |Remaining issues fixable with TSP<br/>AND has <b>not</b> entered Phase B|PhaseA[<b>Phase A: TypeSpec Customizations</b><br/>• AI updates client.tsp<br/>• Track progress<br/>• Increment Phase A counter]
        Classify --> |Remaining issues <b>not</b> fixable with TSP<br/>OR has entered Phase B| PhaseB[<b>Phase B: Code Customizations</b><br/>• AI generates patches<br/>• Apply patches with ClientCustomizationCodePatchTool<br/>• Increment Phase B counter]
        Classify --> |Max iterations exceed<br/>OR stalled progress detected| Failure
        Classify --> |No changes needed| Success

        PhaseA --> RegenSDK[<b>Regenerate SDK</b><br/>• Re-run TypeSpec compilation<br/>• Regenerate SDK code<br/>]
        PhaseB -->RegenSDK

        RegenSDK --> ValidateRegen{Does generation pass?}
        ValidateRegen --> |No, pass context| Classify
        ValidateRegen --> |Yes| BuildSDK[<b>Build SDK</b><br/>Compile SDK code<br/>]

        BuildSDK --> |pass context| Classify

    end

    style Entry fill:#fff9c4
    style Classify fill:#e1f5fe
    style PhaseA fill:#bbdefb
    style PhaseB fill:#f3e5f5
    style Success fill:#c8e6c9
    style Failure fill:#ffcdd2
    style RegenSDK fill:#ffe082
    style RegenSDK fill:#ffe082
    style BuildSDK fill:#ffe082
    style Tool fill:none,stroke:#666,stroke-width:2px,stroke-dasharray: 5 5
```

**Benefits:**

- **Single Tool Experience**: Users don't need to know whether TypeSpec or code fixes are needed
- **Spec-First Approach**: Always attempts TypeSpec solutions before falling back to code patches
- **Intelligent Routing**: Tool determines the appropriate fix phase based on failure analysis

#### Classifier

The classifier is LLM-based and responsible for analyzing the current context and determining which phase to move to next.

For the 1st milestone, the classifier will work fairly simply:
Using the client customizations reference doc, it will analyze the current context to determine if any of the outstanding issues can be addressed via TypeSpec customizations.

This approach has shown some promise when filenames contain `customization` or `generated` are included in the context, but further testing is needed to validate its effectiveness.

For future milestones, we can explore more advanced techniques that detect whether the impacted code is generated or handwritten, and use that to inform phase selection.

#### Context Tracking

The customization workflow is iterative and may loop multiple times as it applies fixes and encounters new issues. The classifier needs to know what changes have been tried and what issues remain so that it can determine what phase to move onto.

Consider a scenario where a TypeSpec rename breaks existing code customizations (illustrative):

1. User requests: _"Rename getItems to listItems for Python"_
2. Phase A applies `@@clientName` decorator successfully
3. SDK regenerates successfully
4. Build fails: `_patch.py` references the old `getItems` method name
5. Loop back to start... but with what context?

**Approach: Simple Context Concatenation**

As changes are made in each phase and validation step, we append to the original context to create the context for the tool's next iteration. This gives the classifier the history of changes and any new issues to consider as a result.

_Example context:_

```
Iteration 1 context:
  "Rename getItems to listItems for Python"

Iteration 2 context:
  "Rename getItems to listItems for Python

   --- TypeSpec Changes Applied ---
   Added @@clientName(getItems, "listItems", "python") to client.tsp
   SDK regenerated successfully.

   --- Build Result ---
   Build failed: _patch.py:42 - NameError: 'getItems' is not defined"

Iteration 3 context:
  [previous context]
  +
  "--- Code Changes Applied ---
   Updated _patch.py to reference 'listItems' instead of 'getItems'

   --- Build Result ---
   Build succeeded."
```

_Considerations:_

Context will grow with each iteration and there is a risk of confusing the LLM evaluating the context. We may consider a more structured approach if stall detection or hallucinations/repetition becomes an issue.

#### 3. Git Patch Support

There are at least 2 scenarios where we want to commit `client.tsp` changes back to the azure-rest-api-specs repo:

- When locally building and testing changes to `client.tsp` before submitting a PR.
- When CI can detect and suggest `client.tsp` changes automatically.

This spec only attempts to address the first scenario where a service team is working locally. Applying changes automatically in CI is out of scope for this spec but can be considered in the future.

**Operating from azure-sdk-for-\* repo with azure-rest-api-specs clone**

When operating from within an SDK repo with a local clone of the azure-rest-api-specs repo, we can directly apply the changes to the local repo clones. No special work is needed. Having all repos available locally is a requirement for this spec.

### MCP Usage Examples

**Example 1: User-provided customization request**

```json
{
  "tool": "azsdk_package_customize_code",
  "arguments": {
    "packagePath": "/path/to/sdk",
    "customizationRequest": "Rename FooClient to BarClient for .NET"
  }
}
```

**Example 2: Working from azure-rest-api-specs repo**

```json
{
  "tool": "azsdk_package_customize_code",
  "arguments": {
    "packagePath": "/path/to/sdk",
    "typespecProjectPath": "/path/to/specs/Foo/",
    "customizationRequest": "Breaking changes detected: FooOptions.timeout property type changed from int to Duration"
  }
}
```

**Example 3: Build failure handling**

```json
{
  "tool": "azsdk_package_customize_code",
  "arguments": {
    "packagePath": "/path/to/sdk",
    "customizationRequest": "Build failed with: error CS0246: The type or namespace name 'FooModel' could not be found"
  }
}
```

### Pipeline/CI Considerations

**Primary Usage Mode: Agent-Interactive**

The `azsdk_package_customize_code` tool is **primarily designed for agent-mode/interactive workflows** where human decision-making is essential:

- **Reviewing diffs**: Users need to inspect proposed changes across TypeSpec and SDK code before approval
- **Approving changes**: Mandatory approval checkpoint ensures users validate customizations align with intent
- **Iterative refinement**: Complex customizations often require feedback loops where users guide the tool toward the desired outcome
- **Ambiguous requests**: Natural language prompts and API review feedback require interpretation and validation

**CI/Pipeline Usage: Out of Scope**

The tool is **not recommended for CI/pipeline usage** for the following reasons:

1. **Human judgment required**: Customizations involve architectural decisions about SDK surface design that require domain expertise and approval
2. **Non-deterministic AI behavior**: Phase B (code customizations) uses AI-generated patches that may vary across runs, making CI results unpredictable
3. **Interactive approval needed**: The approval checkpoint is a critical safety mechanism to prevent unintended changes
4. **Complex error handling**: Build failures may require multiple iteration cycles and human interpretation to resolve correctly

**Recommended CI/Pipeline Workflow**

Instead of running `azsdk_package_customize_code` in CI, follow this pattern:

1. **Local Development**: Developers apply customizations interactively using agent mode
2. **Commit Changes**: Approved TypeSpec (`client.tsp`) and SDK code customizations are committed to source control
3. **CI Validation**: CI runs generation + build + test on the committed customizations using standard workflow tools:
   - `azsdk_package_generate_code` to regenerate SDK from committed TypeSpec
   - `azsdk_package_build_code` to validate builds
   - `azsdk_package_run_tests` to execute test suites

---

## Usage Scenarios for Testing

This section provides concrete test scenarios to validate the two-phase customization workflow. Each scenario demonstrates realistic conditions where both TypeSpec and code customizations may be required.

### Scenario 1: Customization Conflict After Non-Breaking TypeSpec Addition

**Description:** Service team adds optional property `operationId` to TypeSpec, but Java customization already injects this field manually, causing duplicate field compilation error.

**Entry Point:** Build failure

**Problem:** TypeSpec now generates `operationId`, conflicting with existing `addField("operationId")` in `DocumentIntelligenceCustomizations.java`.

**Error:**

```
[ERROR] Failed to execute goal org.apache.maven.plugins:maven-compiler-plugin:3.13.0:compile
/azure-ai-documentintelligence/src/main/java/com/azure/ai/documentintelligence/models/AnalyzeOperationDetails.java:[178,20] variable operationId is already defined in class AnalyzeOperationDetails
```

**Workflow Execution:**

| Phase                           | Action                                                                                                   | Result                                                 |
| ------------------------------- | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------ |
| **Phase A: TypeSpec**           | Analyze build failure<br/>Determine no TypeSpec changes needed<br/>(property already exists in spec)     | No TypeSpec modifications<br/>Forward issue to Phase B |
| **Phase B: Code Customization** | Detect duplicate field injection<br/>Remove `addField("operationId")` from customization<br/>Rebuild SDK | Build succeeds<br/>Customization simplified            |

**Key Learning:** Non-breaking TypeSpec additions can break existing customizations that manually inject the same fields.

---

### Scenario 2: API Review Feedback Requiring Multi-Language Customizations

**Description:** API review requests renaming model `AIProjectConnectionEntraIDCredential` to use "Id" (not "ID") in .NET, requiring scoped TypeSpec changes.

**Entry Point:** API review feedback

**Problem:** Model name doesn't follow .NET casing conventions ("Id" vs "ID").

**Workflow Execution:**

| Phase                 | Action                                                                                                                        | Result                                                              |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| **Phase A: TypeSpec** | Analyze feedback requirements<br/>Apply `@clientName` with proper scoping for .NET<br/>Regenerate .NET SDK<br/>Validate build | SDK regenerates successfully<br/>Build passes<br/>No Phase B needed |

**Key Learning:** API review naming feedback typically resolved with scoped `@clientName` decorators. Tool validates all affected language builds.

---

### Scenario 3: TypeSpec Rename Causing Customization Drift

**Description:** Service team renames property `displayName` → `name` in TypeSpec. Java customization still references old name `getField("displayName")`, causing "cannot find symbol" error.

**Entry Point:** Build failure after regeneration

**Problem:** Customization references non-existent field after TypeSpec rename.

**Error:**

```
cannot find symbol: method getField(String)
Note: Field 'displayName' no longer exists in generated model
```

**Workflow Execution:**

| Phase                           | Action                                                                                                                       | Result                                                                                                                              |
| ------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| **Phase A: TypeSpec**           | Regenerate SDK with updated TypeSpec<br/>Rename is intentional and correct<br/>No TypeSpec changes needed from SDK developer | SDK regenerated successfully<br/>Generated model now has `name` instead of `displayName`<br/>Build fails due to customization drift |
| **Phase B: Code Customization** | Detect reference to non-existent field `displayName`<br/>Update customization to reference `name`<br/>Rebuild SDK            | Build succeeds<br/>Customization aligned with new property name                                                                     |

**Key Learning:** Non-breaking TypeSpec renames break customizations referencing old names. Both phases needed to align spec and customization code.

---

### Scenario 4: Hide Operation from Python SDK

**Description:** Hide internal polling operation `getCreateProjectStatus` from Python SDK using language-scoped `@access` decorator.

**Entry Point:** User prompt ("Remove get_create_project_status from Python SDK")

**Workflow Execution:**

| Phase                 | Action                                                                                                          | Result                                                                                                   |
| --------------------- | --------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| **Phase A: TypeSpec** | Apply `@access` decorator to mark operation as internal for Python<br/>Regenerate Python SDK<br/>Validate build | SDK regenerates successfully<br/>Operation hidden from public API<br/>Build passes<br/>No Phase B needed |

**Key Learning:** `@access` decorator provides language-scoped visibility control without code customizations.

---

### Scenario 5: .NET Build Errors from Analyzer

**Description:** .NET analyzer errors (AZC0030, AZC0012) for naming violations: model ends with "Parameters", type name "Tasks" too generic.

**Entry Point:** Build failure (.NET analyzer)

**Errors:**

- `AZC0030`: Model name ends with 'Parameters'
- `AZC0012`: Type name 'Tasks' too generic

**Workflow Execution:**

| Phase                 | Action                                                                                                                                            | Result                                                                                             |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| **Phase A: TypeSpec** | Parse analyzer error messages<br/>Apply `@clientName` decorators for .NET<br/>Rename problematic types<br/>Regenerate .NET SDK<br/>Validate build | SDK regenerates with new names<br/>Analyzer errors resolved<br/>Build passes<br/>No Phase B needed |

**Key Learning:** .NET analyzer errors resolved with scoped `@clientName` decorators, no code customizations required.

---

### Scenario 6: Create Python Subclient Architecture

**Description:** Restructure Python SDK with main client (`DocumentProcessingClient`) for service operations and subclient (`ProjectClient`) for project-scoped operations.

**Entry Point:** User prompt ("Use 2 clients for Python SDK: one main client and one sub-client that specifies the project id")

**Workflow Execution:**

| Phase                 | Action                                                                                                                                                                                                                                              | Result                                                                              |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| **Phase A: TypeSpec** | Create `client.tsp` with custom client definitions<br/>Define main client for project operations<br/>Define subclient for document operations<br/>Use `@client` and `@clientInitialization` decorators<br/>Regenerate Python SDK<br/>Validate build | SDK regenerates with two-client architecture<br/>Build passes<br/>No Phase B needed |

**Key Learning:** Complex client architecture achieved with TypeSpec decorators alone, no code customizations required.

---

### Testing Checklist

Use these scenarios to validate the customization workflow implementation:

- [ ] **Scenario 1**: Duplicate field injection detection and removal (Phase B focus)
- [ ] **Scenario 2**: API review feedback with single or multi-language scoping (Phase A focus)
- [ ] **Scenario 3**: TypeSpec property rename causing customization drift (Both phases)
- [ ] **Scenario 4**: Hide operation from Python SDK (Phase A focus)
- [ ] **Scenario 5**: .NET build errors from analyzer (Phase A focus)
- [ ] **Scenario 6**: Create Python subclient architecture (Phase A focus)
- [ ] **Approval workflow**: Agent UI approval checkpoint
- [ ] **Change summary**: Tool summarizes changes made to local TypeSpec and SDK files
- [ ] **Max retry limit**: Tool stops after 2 fix cycles to prevent infinite loops
- [ ] **Rollback capability**: Changes can be reverted if not approved

---

## Success Criteria

- [ ] **Automated Detection**: Correctly identify build failure types across all supported languages
- [ ] **Phase A Success**: Successfully apply [TypeSpec Customizations](#typespec-customizations) for common specification issues
- [ ] **Phase B Success**: Successfully apply [Code Customizations](#code-customizations) for common code issues
- [ ] **Approval Workflow**: Enforce [approval checkpoints](#approval-checkpoint) in both contexts
- [ ] **Cross-Language**: Support .NET, Java, JavaScript, Python, and Go

## Open Questions

### Future Feature-Level Customizations

- **Scope Definition**: How do we define boundaries for feature-level vs. fix-level customizations?
- **Approval Strategy**: Should feature-level changes require additional approval layers?

### Cross-Language Standardization

- **Fix Patterns**: How do we maintain consistency while respecting language-specific idioms?
- **Error Detection**: Should error categorization be language-agnostic or language-specific?

### Performance and Scale

- **Concurrent Builds**: How do we handle multiple simultaneous customization workflows?
- **Resource Management**: What are the performance implications of two-phase fixes?

### Phase B Automation Boundaries

- **Pattern Recognition**: What criteria determine whether a code customization is "deterministic" vs. "complex"? Should we maintain a catalog of known mechanical patterns?
- **Success Metrics**: What success rate threshold should we achieve for deterministic patterns before expanding Phase B scope to more complex transformations?
- **Language-Specific Complexity**: Should Java AST manipulation patterns be treated differently than Python `_patch.py` updates given their brittleness?
- **Escape Hatches**: When Phase B fails repeatedly, should we surface guidance documents ("read this doc for code customization") rather than continue attempting automation?

## Alternatives Considered

### Alternative 1: Emit-Validate-Propose Loop

**Description:**
[Original design proposal](https://gist.github.com/chrisradek/9ab52a0a13faac6b794d32be87c26785)
A CLI command/MCP tool that takes a typespec project path and a package path (emitted SDK path) as input. This then runs a loop of: Emit SDK -> Validate SDK -> Propose customizations to `client.tsp` -> repeat.

**Pros:**

- Codifies the loop .NET already has into a reusable tool that enforces a sequence of steps
- Can be used by multiple languages - each language provides their implementation of the validate/propose steps

**Cons:**

- Too specific to .NET's workflow of emit, build, fix. Go better served with emit, validateAndFix.
- Not clear when AI should call the MCP tool, as each language may have a different process (e.g. .NET build vs Go changelog analysis)

**Why not chosen:**

The original design only attempted to address 1 of the 3 areas: providing the infra in azsdk-cli to steer customizations. It relied on each language to provide their own mechanism for both identifying and applying `client.tsp` customizations. It was also completely separate from the existing `CustomizedCodeUpdateTool` MCP tool, meaning it had to perform many of the same steps (e.g. generate/build SDK) while not being clear when it should be invoked.

---
