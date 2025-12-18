<!-- cspell:words Deidentification HealthDataAIServices healthdataaiservices DeidServices TypeSpec NetNew rehydrate crosslanguage scaffolding teardown noninteractive changelogs handwritten provisioning rerecordings azsdk AZSDK automatable westus2 -->
# Spec: Scenario 2

## Table of Contents

- [Overview](#overview)
- [Definitions](#definitions)
- [Why Scenario 2 Matters](#why-scenario-2-matters)
- [Context & Assumptions](#context--assumptions)
- [Workflow](#workflow)
- [Stage Details](#stage-details)
- [Success Criteria](#success-criteria)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)
- [Pipeline & CI Usage](#pipeline--ci-usage)
- [Implementation Plan](#implementation-plan)
- [Documentation Updates](#documentation-updates)
- [Metrics/Telemetry](#metricstelemetry)
- [Open Questions](#open-questions)
- [Related Links](#related-links)

---

## Overview

Scenario 2 extends **[Scenario 1](./0-scenario-1.spec.md#overview)** by adding: automated environment remediation, customization (TypeSpec + code), and live / recorded testing. All Scenario 1 stages (environment setup, generation, package metadata & docs updates, validation) remain.

**Tool Automation Strategy**: Scenario 2 rounds out deterministic inner-loop tooling while introducing AI-powered tools and features that assist with authoring tasks such as TypeSpec specification creation and modification. Future scenarios will layer additional AI assistance onto judgment-heavy tasks (for example, README authoring and broader doc updates).

**Unified Customization Experience**: Users describe the change; the customization tool intelligently chooses TypeSpec or code (or both). The tool analyzes the request and automatically determines the appropriate implementation path, hiding mechanism details unless needed.

**Service**: Health Deidentification

- [Health Deidentification Data Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/ded7abde9c48ba84df36b53dfcaef48a2c134097/specification/healthdataaiservices/HealthDataAIServices.DeidServices)
- [Health Deidentification MGMT Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/healthdataaiservices/HealthDataAIServices.Management)

**Modes**: Works in both [Agent Mode](./0-scenario-1.spec.md#agent-mode) and [CLI Mode](./0-scenario-1.spec.md#cli-mode)

Scenario 2 validates:

- Expanded inner loop: setup → generation → customization → testing (live / recorded) → validation
- Support for **[Net-New SDKs](#net-new-sdk)** and existing SDKs
- All five languages (.NET, Java, JavaScript, Python, Go)
- Health Deidentification service end to end
- Both **Agent Mode** and **CLI Mode** paths

---

## Definitions

The terminology from [Scenario 1 Definitions](./0-scenario-1.spec.md#definitions) still applies. Scenario 2 introduces the following additional concepts:

<!-- markdownlint-disable MD033 -->
- **<a id="agent"></a>Agent**: GitHub Copilot running in a Copilot-enabled editor (VS Code, Visual Studio, or IntelliJ) with access to the Azure SDK Tools Model Context Protocol (MCP) server, enabling AI-assisted SDK development workflows.
- **<a id="net-new-sdk"></a>Net-New SDK**: A service library that has not yet shipped a preview release and therefore requires scaffolding for code, tests, and resources.
- **<a id="typespec-customizations"></a>TypeSpec Customizations**: Updates to TypeSpec inputs (for example, `client.tsp`) performed before invoking generation to tailor the generated SDK surfaces.
- **<a id="code-customizations"></a>Code Customizations**: Handwritten code layered on top of generated output after generation completes, maintained in language-specific customization zones.
- **<a id="live-test"></a>Live Test**: An automated test that executes against Azure resources. When recording is explicitly enabled, it produces recordings for later playback validation.
- **<a id="test-recording"></a>Test Recording**: Captured HTTP interactions produced by live tests when recording is enabled, reused for playback-based validation.
- **<a id="test-infrastructure"></a>Test Infrastructure**: Azure resources, credentials, configuration, and environment settings required to execute or re-record live tests.

---

## Why Scenario 2 Matters

Without coverage for customization, live testing, and **[Net-New SDK](#net-new-sdk)** creation the toolset would remain fragmented. Scenario 2 delivers:

- Clear success benchmarks (including scaffolding)
- Repeatable validation across customization and testing
- Explicit scope for TypeSpec vs. code customization and supporting **[Test Infrastructure](#test-infrastructure)**
- Cross-language consistency for new stages
- Confirmation that Scenario 1 capabilities integrate cleanly

---

## Context & Assumptions

### Environment

- Windows, macOS, or Linux machine with freshly cloned repositories (`azure-rest-api-specs` plus all five language repositories)
- TypeSpec modifications are **local only**
- Agent-mode interactions occur in **VS Code, Visual Studio, or IntelliJ with GitHub Copilot** with `azure-rest-api-specs` open
- Azure subscription permits on-demand resource provisioning and teardown
- No need for users to set environment variables to setup the tool

### In Scope for Scenario 2

- **All [Scenario 1 activities](./0-scenario-1.spec.md#workflow) are included**
- All five languages: .NET, Java, JavaScript, Python, Go
- **Both [Net-New SDKs](#net-new-sdk) and existing SDKs**
- TypeSpec-based generation for the Health Deidentification service
- Both **data plane and management plane** coverage
- Both **Agent Mode and CLI Mode** validation
- All models available through GitHub Copilot:
  - **Required for testing**: Claude Sonnet 4, Claude Sonnet 4.5, GPT-4o, GPT-5
- Automated environment remediation (install/upgrade tooling)
- **[TypeSpec Customizations](#typespec-customizations)** to shape generated surfaces
- **[Code Customizations](#code-customizations)** to add handwritten code layers
- Creation of **[Live Tests](#live-test)**, and new **[Test Recordings](#test-recording)** for [Net-New SDKs](#net-new-sdk)
- Re-recording tests for existing SDKs
- Automated handling of Azure resources for live tests
- TypeSpec Authoring

### Out of Scope for Scenario 2

- Outer-loop activities such as committing changes, creating PRs, release management, or package publishing
- GA releases
- Architectural review preparation
  - **Note**: Even though architectural review preparation is out of scope for this scenario, instruction files should still provide guidance on how users can initiate the architectural review process outside of the agent workflow until it can be integrated into future scenarios
  - **Note**: Similarly, package naming approval is required for [Net-New SDKs](#net-new-sdk), and instruction files should provide guidance on how to initiate the package naming approval process outside of the agent workflow
- Breaking change reviews
- Updating changelog/`README.md`/metadata content for data-plane libraries
  - **Note**: For .NET, metadata update functionality (export-api script) already works for both data-plane and management-plane libraries and is implemented in Scenario 1
- Error resolution assistance beyond environment setup remediation

---

## Workflow

1. **Environment Setup** → `azsdk_verify_setup`
   - Verify tools and versions (see [Scenario 1 – Environment Setup](./0-scenario-1.spec.md#1-environment-setup))
   - Optionally remediate missing or out-of-date tools with `--fix` option
   - **Note**: This stage carries over from Scenario 1 and will need to be revisited to ensure it works correctly for [Net-New SDKs](#net-new-sdk)

2. **TypeSpec Authoring** → `azsdk_typespec_authoring`
   - AI-powered assistance for authoring or modifying TypeSpec API specifications
   - Leverages Azure SDK knowledge base for guidelines-compliant code
   - Helps with ARM resources, versioning, routing, and compliance fixes

3. **Generating** → `azsdk_package_generate_code`
   - Generate SDK code, tests, and samples (see [Scenario 1 – Generating](./0-scenario-1.spec.md#2-generating))
   - **Note**: Generation tooling now handles library project bootstrapping for [Net-New SDKs](#net-new-sdk)

4. **Customizations** → `azsdk_customized_code_update`
   - Unified tool for applying both [TypeSpec Customizations](#typespec-customizations) and [Code Customizations](#code-customizations)
   - Two-phase workflow: Phase A (TypeSpec) → Phase B (Code)
   - Automatically determines appropriate customization approach based on request
   - Regenerates SDK after TypeSpec changes and validates builds

5. **Testing** → `azsdk_package_run_tests`
   - Single unified testing tool that handles all test modes (live, live-record, playback)
   - Automatically provisions test resources when running live tests if not already available
   - Calls existing test resource provisioning scripts
   - **Note**: Creation of bicep files for test resource provisioning for [Net-New SDKs](#net-new-sdk) may be complex and will need to be more thoroughly investigated before committing to full automation

6. **Update Package/Docs/Metadata** → `azsdk_package_update_metadata`, `azsdk_package_update_version`, `azsdk_package_update_changelog_content`
   - Update package metadata, docs, and changelogs (see [Scenario 1 – Update Package/Docs/Metadata](./0-scenario-1.spec.md#3-update-packagedocsmetadata))
   - **Note**: This stage carries over from Scenario 1 and will need to be revisited to ensure it works correctly for [Net-New SDKs](#net-new-sdk)

7. **Validating** → `azsdk_package_run_check`
   - Run final validation checks across languages and stages (see [Scenario 1 – Validating](./0-scenario-1.spec.md#4-validating))
   - **Note**: Some validation issue fixing may fall under other stages (for example, build errors caused by customizations would be addressed in the Customizations stage; generation issues would be addressed in the Generating stage)

⚠️  STOP: Test scenario only. Do NOT commit or create release PRs.

---

## Stage Details

### 1. Environment Setup

The **environment verification requirements** for this stage are unchanged from Scenario 1. Refer to [Scenario 1 – Environment Setup](./0-scenario-1.spec.md#1-environment-setup) for the definition of required tools, checks, and success criteria for verification.

Scenario 2 enhances `azsdk_verify_setup` with an **optional fix mode** that **offers to install or upgrade missing or out-of-date tooling when automation is not complex**.

**Tool:** `azsdk_verify_setup`

**Behavior:**

- **Default mode (verify only)**: Detects and reports on tool presence, versions, and compatibility issues without making changes.
- **Fix mode (`--fix` flag)**: When enabled, offers to remediate detected issues:
  - For tools that can be installed/updated with **straightforward automation** (for example, language-specific package managers, small command-line utilities):
    - Performs the installation or upgrade using scripted, repeatable steps.
  - For tools that are **too large or complex** to manage automatically (for example, full IDEs, heavy emulators, system-wide SDK bundles):
    - The [Agent](#agent) must clearly articulate that the tool is too large or complex for automatic installation and explain why.
    - Directs the user to the **official installation documentation** and captures links surfaced to the user.

**Limitations & Rules:**

- In verify-only mode (default), tool **only detects and reports** issues without attempting any fixes.
- In fix mode, tool **MUST NOT** silently install or upgrade tools; user confirmation is always required in Agent Mode.
- For tools in the "too large/complex" category, fix mode **only verifies** presence/version and **outputs links and instructions** instead of attempting installation. The [Agent](#agent) must clearly communicate why the tool cannot be auto-installed (e.g., "Visual Studio is too large and complex for automatic installation").
- When possible, fix mode attempts to **align versions** to the recommended baseline (for example, minimum and tested versions for TypeSpec, language toolchains, and `azsdk` itself).
- In CLI/automation contexts (for example, CI pipelines), fix mode runs in a **non-interactive mode** where installation behavior is controlled via flags (for example, `--auto-install`, `--no-install`, `--allow-upgrade`) to avoid blocking prompts.

**Success:**

- All tools required by Scenario 2 are either:
  - Installed at compatible versions, or
  - Explicitly reported as missing with clear installation guidance and links.
- A **single summarized report** is produced (and logged to disk) that `azsdk_verify_setup` and downstream stages can reference.
- A **defined set of criteria exists** for determining which tools can be auto-installed/auto-upgraded versus which require manual installation with guidance links (see [Open Questions](#open-questions)).

### 2. TypeSpec Authoring (AI-Powered)

Before or during SDK generation, developers may need to author or modify TypeSpec API specifications. Scenario 2 introduces AI-powered assistance for TypeSpec authoring that leverages the Azure SDK knowledge base to generate standards-compliant code.

**Tool:** `azsdk_typespec_authoring`

**Purpose:** Provide intelligent, context-aware assistance for TypeSpec authoring by integrating with Azure SDK RAG (Retrieval-Augmented Generation) knowledge base. Helps developers define or edit TypeSpec following Azure Resource Manager (ARM) patterns, Data Plane (DP) standards, SDK guidelines, and TypeSpec best practices.

**Capabilities:**

- **Intent-Driven Development**: Users describe their intent in natural language (e.g., "add a new ARM resource named 'Asset' with CRUD operations"), and the AI guides them through the correct TypeSpec implementation
- **Guidelines Compliance**: Generates TypeSpec code that adheres to Azure guidelines, avoiding common anti-patterns and hallucinated decorators
- **Contextual References**: Provides links to relevant Azure documentation and best practices for each suggestion
- **Versioning Support**: Assists with adding new API versions following Azure versioning guidelines (preview vs stable, breaking change policies)
- **Resource Hierarchy**: Helps define parent-child resource relationships with correct routing using `@parentResource` and `@route` decorators
- **Common Scenarios**: Handles ARM resource creation, path corrections, versioning changes, and fixing non-compliant code patterns

**Input Parameters:**

- `--request`: The TypeSpec-related request or task description (required)
- `--additional-information`: Additional context for the request (optional)
- `--typespec-source-path`: Path to TypeSpec source file or folder (optional, defaults to current directory)

**Workflow:**

1. User describes TypeSpec authoring task in natural language
2. Tool analyzes existing TypeSpec project structure and current state
3. Tool queries Azure SDK Knowledge Base with structured request
4. Knowledge Base returns RAG-powered solution with step-by-step guidance
5. Tool formats solution with documentation references
6. Agent applies changes to TypeSpec files and presents results to user

**Examples:**

- Adding ARM resources: "add an ARM resource named 'Asset' with CRUD operations"
- Updating routes: "change the route for interface Assets to include employees/{employeeName} before assets/{assetName}"
- Versioning: "add a new preview API version 2025-10-01-preview for service widget"
- Fixing compliance: "update this TypeSpec to follow Azure ARM guidelines"

**Success:**

- Generated TypeSpec code passes compilation without errors
- Generated code follows Azure ARM/DP/SDK guidelines (validated by linter/validator)
- Generated code includes proper decorators and templates (no hallucinated decorators)
- Solution includes relevant documentation references
- Responses correctly interpret natural language intent
- Reduces reviewer comments on TypeSpec standards violations

### 3. Generating

**Tool:** `azsdk_package_generate_code`

**Action:**

- Generate SDK code, tests, and samples for the requested languages
- **Bootstrap language library project scaffolding** when generating a **[Net-New SDK](#net-new-sdk)**, including directory structure, package metadata files, and docs (`README.md`).
- Triggers downstream validation hooks (`azsdk_package_build_code`, `azsdk_package_run_tests --mode playback`, `azsdk_package_run_check`)

**Success:**

- Generation succeeds for all five languages (or reports failures with diagnostics)
- Existing customization layers remain untouched while new files are clearly identified
- For **[Net-New SDKs](#net-new-sdk)**, all required project scaffolding is created correctly for each target language

### 4. Customizations

Scenario 2 presents a **unified customization experience**: users describe desired outcomes (rename operations, add helper methods, introduce convenience overloads) without needing to choose mechanism up front. The tooling translates intent into the appropriate implementation path using a two-phase workflow.

**Tool:** `azsdk_customized_code_update`

**Two-Phase Workflow:**

**Phase A – [TypeSpec Customizations](#typespec-customizations):**

- Analyze the customization request to determine if TypeSpec decorators can address the issues
- Apply `client.tsp` adjustments (decorators, naming, grouping, scope configurations) using [Azure.ClientGenerator.Core](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/) decorators
- Re-run TypeSpec compilation and regenerate SDK code
- Validate build and proceed to Phase B only if issues remain
- **Note**: Phase A may identify parts of the request that cannot be handled via `client.tsp` changes and forward those to Phase B

**Phase B – [Code Customizations](#code-customizations):**

- If Phase A doesn't resolve all issues, apply language-specific code patches
- Apply handwritten custom code on top of generated output
- Place customizations into the correct directories and layers for each language, ensuring **project scaffolding properly separates generated and custom code** to maintain regeneration compatibility
- Use existing patching mechanisms for SDK code modifications
- Apply safe patches: imports, visibility modifiers, reserved keyword renames, annotations
- Validate final build (maximum of 2 fix cycles to prevent infinite loops)

**Unified Experience:**

- Tool accepts a single `customizationRequest` parameter (natural language, build errors, API review feedback, etc.)
- Automatically determines whether TypeSpec, code, or both approaches are needed
- Generates consolidated diff of all changes (spec + SDK code)
- Enforces approval checkpoint before applying changes (CLI: interactive prompt, MCP: agent UI)
- Provides concise summary of applied changes, regardless of mechanism

**Success:**

- Customization request is analyzed and appropriate phase(s) are executed
- TypeSpec validation passes for all languages when Phase A is applied
- Build succeeds after customizations are applied
- Regeneration retains custom code through language-specific layering patterns
- User receives single, plain-language summary of what changed

### 5. Testing

This stage uses a unified testing tool that handles all test modes and automatically manages test resources.

**Tool:** `azsdk_package_run_tests`

**Unified Testing Approach:**

- Single tool handles all test modes: `live`, `live-record`, and `playback`
- Automatically checks for and provisions test resources when running live tests if not already available
- Calls existing test resource provisioning and teardown scripts
- Supports test mode selection via command-line options
- Automatically tears down resources after live test completion

**Workflow:**

1. **Select test mode** using `--mode` option (`live`, `live-record`, or `playback`)
2. **For live/live-record modes**: Tool automatically checks if test resources exist
   - If resources don't exist, provisions them using existing test resource provisioning scripts
   - Configures test environment with resource connection information
3. **Execute tests** with optional `--test-filter` for targeted test runs
   - Being able to only run select test may help when fixing issues with specific tests, so user doesn't have to wait for all tests to run.
4. **For live-record mode**: Captures test recordings during execution
5. **Cleanup**: Optionally tears down resources using `--cleanup` flag

**Net-New SDK Considerations:**

- **Creation of bicep files** for test resource provisioning for [Net-New SDKs](#net-new-sdk) may be complex and will need to be more thoroughly investigated before committing to full automation
- **Note**: Bicep file creation would be handled by a separate tool (`azsdk_package_create_test_resources`) rather than being part of `azsdk_package_run_tests`
- Service-specific parameters, resource types, and configuration details often require human input and domain knowledge
- Tool provides templates and guidance but may not fully automate bicep file creation for new services

**Success:**

- Tests run successfully in all modes (live, live-record, playback)
- Test resources are provisioned and torn down cleanly for live tests
- Test recordings are generated or refreshed correctly
- Playback runs succeed with recordings
- For [Net-New SDKs](#net-new-sdk), test assets are created (with manual input where needed)

### 6. Update Package/Docs/Metadata

Unchanged from Scenario 1. Refer to [Scenario 1 – Update Package/Docs/Metadata](./0-scenario-1.spec.md#3-update-packagedocsmetadata) for complete details.

**Enhancements for [Net-New SDKs](#net-new-sdk):**

- **CI Configuration**: A new tool `azsdk_package_update_ci` will be added to provision and update `ci.yml` files for net-new SDKs
- **Java Metadata**: The `azsdk_package_update_metadata` tool will be enhanced to support parent-level and root-level `pom.xml` updates for Java libraries
- **Data Plane Versioning**: The `azsdk_package_update_version` tool will be extended to update versions for data-plane libraries (currently returns no-op for data-plane libraries in Scenario 1)

**Success (additional for Scenario 2):**

- Tooling correctly handles package metadata updates for both [Net-New SDKs](#net-new-sdk) and existing SDKs
- CI configuration files are provisioned correctly for net-new SDKs
- Java parent and root `pom.xml` files are updated appropriately
- Version updates work for both data-plane and management-plane libraries

### 7. Validating

Unchanged from Scenario 1. Refer to [Scenario 1 – Validating](./0-scenario-1.spec.md#4-validating) for validation steps and success criteria.

**Success (additional for Scenario 2):**

- Validation checks pass for both [Net-New SDKs](#net-new-sdk) and existing SDKs

---

## Success Criteria

Scenario 2 is complete when:

- The complete workflow executes successfully for all five languages
- Both Agent Mode and CLI Mode scenarios run end to end
- Documentation exists for both modes, reflecting new customization and testing stages
- **Runs are repeatable**: CLI Mode produces deterministic results; Agent Mode may vary in execution path but MCP tools produce deterministic outputs given the same inputs
- Agent prompts trigger expected tool sequences
- CLI commands execute with expected outputs
- Workflow runs entirely on the local machine
- Testing succeeds in all modes (live, live-record, playback) with automatic resource provisioning and cleanup, test recordings are generated or refreshed correctly, and test filtering works as expected
- **All Scenario 2 tooling enhancements and additions function correctly** as defined in their respective stage details:
  - `azsdk_verify_setup` detects issues in verify-only mode and remediates them in fix mode (enhanced from Scenario 1)
  - `azsdk_customized_code_update` applies both TypeSpec and code customizations through unified two-phase workflow (new)
  - `azsdk_package_run_tests` handles all test modes with automatic resource management (new)
- **Both [Net-New SDKs](#net-new-sdk) and existing SDKs** are fully supported throughout all stages

---

## Agent Prompts

### Full Workflow (Net-New SDK)

**Prompt:**

```text
I need to create a new Health Deidentification SDK for all languages.
```

**Expected Agent Activity:**

1. **Agent prompts user** to explain the full workflow for creating a net-new SDK, including environment setup, code generation, test creation, and validation steps.
2. **Agent asks user** which languages to target (or confirms "all five languages").
3. Execute `azsdk_verify_setup` for selected languages to check prerequisites.
   - **Agent reports** environment check results (installed tools, missing tools, version mismatches)
   - **Agent prompts user** asking if they want to fix the detected issues
   - If user confirms, execute `azsdk_verify_setup --fix` to remediate issues
   - **Agent prompts user** for confirmation before installing or upgrading any tools in fix mode
4. Invoke `azsdk_package_generate_code` to generate SDK code, along with tests and samples that can be generated from TypeSpec.
   - **Note**: For net-new SDKs, generation automatically bootstraps the complete language library project scaffolding including directory structure, package metadata files, and `README.md`
   - After generation completes, validation is performed (build and playback tests)
   - **Agent reports** generation results and any validation issues
5. **Agent asks user** if any customizations are needed (TypeSpec or code level).
   - If yes, apply `azsdk_customized_code_update` to handle both TypeSpec and code customizations:
     - Phase A: Apply TypeSpec customizations to `client.tsp`, regenerate SDK, validate build
     - Phase B: Apply code customizations if needed (helper APIs, convenience methods), validate final build
     - Present consolidated diff and **obtain user approval** before applying changes
6. Create test infrastructure for the new SDK:
   - **Agent asks user** for service-specific details needed for test resource provisioning (resource types, required parameters, configuration values)
   - **Note**: Creation of bicep files for test resource provisioning may require manual input or configuration due to service-specific requirements. This complexity will need to be more thoroughly investigated before committing to full automation
   - Tool may provide templates and guidance but may not fully automate bicep file creation
7. Create initial tests for the new SDK:
   - Identify representative scenarios from TypeSpec operations and samples
   - Generate test scaffolding and basic test cases for each language
   - Follow language-specific testing patterns and conventions
   - **Agent reports** what test scenarios were identified and asks for confirmation before generating tests
8. Execute tests with `azsdk_package_run_tests --mode live-record`.
   - **Agent prompts user** to confirm Azure subscription and region for test resource provisioning
   - Tool automatically provisions resources based on test infrastructure
   - **Agent reports** test results and recording generation status
9. Execute tests with `azsdk_package_run_tests --mode playback` to confirm recordings replay cleanly.
   - **Agent reports** playback test results
10. Run `azsdk_package_update_metadata`, `azsdk_package_update_version`, and `azsdk_package_update_changelog_content` to refresh versions, changelogs, and docs.
    - **Note**: This step is only performed for management plane libraries at this time
11. Execute `azsdk_package_run_check` to perform final validation.
    - **Agent reports** validation results
    - For any validations that failed, run available fix tooling to automatically remediate issues where possible
    - **Agent reports** which issues were auto-fixed and which require manual intervention
12. Once all checks pass, open a draft PR to the language repositories with the new library code.
    - **Agent prompts user** for PR title and description
    - **Agent reports** PR URLs for each language repository

### Full Workflow (Existing SDK)

**Prompt:**

```text
There are new changes in the Health Deidentification API spec. I need to regenerate and validate the SDK for all languages.
```

**Expected Agent Activity:**

1. **Agent prompts user** to explain the workflow for updating an existing SDK, including environment verification, generation, optional customizations, and validation steps.
2. **Agent asks user** which languages to target (or confirms "all five languages").
3. Execute `azsdk_verify_setup` for selected languages to check prerequisites.
   - **Agent reports** environment check results (installed tools, missing tools, version mismatches)
   - **Agent prompts user** asking if they want to fix the detected issues
   - If user confirms, execute `azsdk_verify_setup --fix` to remediate issues
   - **Agent prompts user** for confirmation before installing or upgrading any tools in fix mode
4. Invoke `azsdk_package_generate_code` to regenerate SDK code, along with tests and samples that can be generated from TypeSpec.
   - After generation completes, validation is performed (build and playback tests)
   - **Agent reports** generation results and any validation issues
5. **Agent asks user** if any customizations are needed (TypeSpec or code level).
   - In this example, user indicates no customizations needed; agent proceeds to next step
   - If customizations were needed, agent would apply `azsdk_customized_code_update` as in net-new SDK workflow
6. Execute tests with `azsdk_package_run_tests --mode playback` to validate the generated code.
   - **Agent reports** playback test results
7. If applicable for management plane, run `azsdk_package_update_metadata`, `azsdk_package_update_version`, and `azsdk_package_update_changelog_content`.
8. Execute `azsdk_package_run_check` to perform final validation.
   - **Agent reports** validation results
   - For any validations that failed, run available fix tooling to automatically remediate issues where possible
   - **Agent reports** which issues were auto-fixed and which require manual intervention
9. Once all checks pass, open a draft PR to the language repositories with the updated library code.
   - **Agent prompts user** for PR title and description
   - **Agent reports** PR URLs for each language repository

### Customizations Prompt

**Note**: The specifics of the changes being requested in these example prompts are purposefully generic to convey the type of change (TypeSpec-based vs. convenience layer) rather than prescribe exact implementation details.

**Prompt:**

```text
I need to adjust the client surface for the Health Deidentification SDK. Can you help me make some naming changes?
```

**Expected Agent Activity:**

1. **Agent asks user** for specific details about the desired changes.
2. Call `azsdk_customized_code_update` with the customization request.
3. Tool executes Phase A (TypeSpec Customizations):
   - Analyzes request and determines TypeSpec changes are appropriate (operation renames, parameter grouping, etc.)
   - Opens the `client.tsp` file and applies edits aligned with the request
   - Regenerates SDK code from updated TypeSpec
   - Validates build succeeds
4. Tool presents consolidated diff of changes.
5. User approves changes.
6. Tool summarizes applied modifications.
7. **Agent reports** which files were modified and what changes were made.

**Prompt:**

```text
I need to add some convenience methods to the Health Deidentification SDK to make it easier to use for common scenarios.
```

**Expected Agent Activity:**

1. **Agent asks user** for specific details about the convenience methods needed.
2. Call `azsdk_customized_code_update` with the customization request.
3. Tool executes Phase A (TypeSpec) - determines TypeSpec cannot address this request (convenience methods require handwritten code).
4. Tool executes Phase B (Code Customizations):
   - Identifies customization directories for each language
   - Applies handwritten convenience layer code on top of generated SDK
   - Validates builds succeed
5. Tool presents consolidated diff of changes.
6. User approves changes.
7. **Agent reports** which files were created/modified in the convenience layer for each language.

### Live Testing (Net-New SDK)

**Prompt:**

```text
Set up test resources, run live tests, and generate test recordings for this new SDK.
```

**Expected Agent Activity:**

1. Note: Bicep files for test resource provisioning may need manual creation or configuration due to service-specific requirements. This complexity will need to be more thoroughly investigated before committing to full automation.
2. Execute `azsdk_package_run_tests --mode live-record` (tool automatically provisions resources if not already available).
3. Tests run in live-record mode, capturing test recordings.
4. Persist recorded sessions and catalog their locations per language.
5. Execute `azsdk_package_run_tests --mode playback` to validate recordings.
6. Optionally, execute `azsdk_package_run_tests --mode live-record --cleanup` to tear down resources after completion.

### Live Testing (Existing SDK)

**Prompt:**

```text
Run live tests for the existing SDK and re-record any failing tests.
```

**Expected Agent Activity:**

1. Execute `azsdk_package_run_tests --mode live` for the specified languages to detect drift or failures.
2. If re-recording is needed, execute `azsdk_package_run_tests --mode live-record --test-filter <pattern>` to refresh only impacted tests.
3. Tool automatically provisions resources if not already available.
4. Execute `azsdk_package_run_tests --mode playback` to confirm refreshed recordings succeed.
5. Summarize outcomes and highlight recordings that changed.

### Re-record Outdated Tests

**Prompt:**

```text
Refresh only outdated or failing test recordings for the Health Deidentification SDK and confirm playback still passes.
```

**Expected Agent Activity:**

1. Execute `azsdk_package_run_tests --mode live-record --test-filter <pattern>` to capture updated recordings for specific tests.
2. Tool automatically provisions resources if needed.
3. Execute `azsdk_package_run_tests --mode playback --test-filter <pattern>` to validate new recordings.
4. Summarize which recordings changed and any remaining discrepancies.

### TypeSpec Authoring - Add New API Version

**Prompt:**

```text
I need to add a new preview API version to the Health Deidentification TypeSpec. Can you help me add version 2025-10-01-preview?
```

**Expected Agent Activity:**

1. **Agent calls** `azsdk_typespec_authoring` with the request
2. Tool analyzes current TypeSpec project structure and identifies version enum
3. Tool queries Azure SDK Knowledge Base for versioning guidelines
4. Tool generates solution:
   - Add enum option to version enum following YYYY-MM-DD format
   - Decorate with `@previewVersion` for preview versions
   - Add new example folder and copy still-relevant examples
5. **Agent reports** changes and provides documentation links:
   - Link to Azure TypeSpec versioning guide
   - Link to preview version guidelines

### TypeSpec Authoring - Add ARM Resource

**Prompt:**

```text
I need to add a new ARM resource to the Health Deidentification service TypeSpec with standard CRUD operations.
```

**Expected Agent Activity:**

1. **Agent calls** `azsdk_typespec_authoring` with the request
2. Tool analyzes current TypeSpec project to identify namespace and version
3. Tool queries Azure SDK Knowledge Base for ARM resource patterns
4. Tool generates solution:
   - Create tracked resource model with properties
   - Add resource interface with `@armResourceOperations` decorator
   - Include standard CRUD operations using ARM templates
   - Add list operations following ARM patterns
5. **Agent applies** generated TypeSpec code
6. **Agent reports** what was created and provides documentation links:
   - Link to ARM resource type guide
   - Link to ARM operation templates

### TypeSpec Authoring - Fix Resource Hierarchy

**Prompt:**

```text
I need to update the Health Deidentification TypeSpec to establish a parent-child resource relationship. Can you help me set up the correct routing?
```

**Expected Agent Activity:**

1. **Agent calls** `azsdk_typespec_authoring` with the request and current resource definitions
2. Tool identifies this as a parent-child resource hierarchy scenario
3. Tool queries Azure SDK Knowledge Base for `@parentResource` pattern
4. Tool generates solution:
   - Add `@parentResource` decorator to establish hierarchy
   - Add required path parameters
   - Update route to include parent path segment
5. **Agent applies** changes to TypeSpec files
6. **Agent reports** changes made and provides documentation links:
   - Link to child resource guide
   - Link to `@parentResource` decorator documentation

---

## CLI Commands

*Direct command-line interface usage for [CLI mode](./0-scenario-1.spec.md#cli-mode):*

### 1. Verify and Setup Environment

**Command:**

```bash
azsdk verify setup --languages "Dotnet,Java,JavaScript,Python,Go" --fix
```

**Options:**

- `--languages <list>`: Space-separated list of languages to verify (default: language of current repository)
- `--fix`: Enable fix mode to remediate detected issues (default: verify-only mode)
- `--auto-install`: Automatically install missing dependencies without prompts (for CI/automation, requires `--fix`)
- `--allow-upgrade`: Allow upgrading out-of-date tools (requires `--fix`)
- `--verbose`: Show detailed output for each operation

**Expected Output (with --fix):**

```text
Verifying environment for 5 languages...

✓ .NET SDK 9.0.205 already installed
✓ Java JDK 11 already installed
✓ Node.js 18.x already installed
✓ Python 3.11 already installed
✗ Go not found

? Install Go 1.25? (Y/n)
✓ Go 1.25 installed successfully

Environment verification complete: 5/5 languages ready
```

**Expected Output (verify-only mode):**

```text
Verifying environment for 5 languages...

✓ .NET SDK 9.0.205 installed
✓ Java JDK 11 installed
✓ Node.js 18.x installed
✓ Python 3.11 installed
✗ Go not found - Install from: https://go.dev/doc/install

Environment verification complete: 4/5 languages ready
Use --fix flag to remediate issues automatically.
```

### 2. TypeSpec Authoring

**Command:**

```bash
azsdk typespec authoring --request "add a new preview API version 2025-10-01-preview"
```

**Options:**

- `--prompt<string>`: The TypeSpec-related prompt or task description (required)
- `--additional-information <string>`: Additional context for the request (optional)
- `--typespec-source-path <path>`: Path to TypeSpec source file or folder (optional, defaults to current directory)

**Expected Output:**

```text
**Solution:** To add a new API version '2025-10-01-preview' for your service in TypeSpec, you need to update your version enum and ensure all changes are tracked with versioning decorators.

**Step-by-step guidance:**
1. Update the Versions enum in your versioned namespace to include the new version. Each version string should follow the YYYY-MM-DD format. Since this is a preview version, use the '-preview' suffix and decorate with @previewVersion.

@previewVersion
v2025_10_01_preview: "2025-10-01-preview"

2. Add an example folder for this version (examples/2025-10-01-preview) and copy any still-relevant examples from the previous version.

**References:**
- How to define a preview version: https://azure.github.io/typespec-azure/docs/howtos/versioning/preview-version
- Azure TypeSpec versioning guide: https://azure.github.io/typespec-azure/docs/howtos/versioning
```

**Error Cases:**

```text
✗ Error: Required argument missing: --request

Usage: azsdk typespec authoring --request <request> [--additional-information <info>] [--typespec-source-path <path>]
```

### 3. Apply Customizations

**Note**: It is unclear whether a CLI command for customizations should exist, as customizations may not be a good use case for non-interactive CLI execution. See the [Pipeline & CI Usage](#pipeline--ci-usage) section for discussion on whether `azsdk_customized_code_update` should be used in CI at all.

**Command:**

```bash
azsdk customized-code update --package-path <path_to_sdk> --customization-request "<request>"
```

**Options:**

- `--package-path <path>`: Path to the SDK package directory (required)
- `--customization-request <text>`: Description of customization to apply - supports build errors, user requests, API review feedback (required)
- `--typespec-project-path <path>`: Path to TypeSpec project directory (optional, for working from azure-rest-api-specs)
- `--dry-run`: Display planned changes without applying them
- `--auto-approve`: Skip approval checkpoint (for CI/automation)

**Expected Output:**

```text
Applying customizations...

Phase A: TypeSpec Customizations
✓ Applied @clientName("BarClient", "csharp") to FooClient
✓ Regenerated SDK code
✓ Build validation passed

Phase B: Code Customizations
(No code customizations needed)

Consolidated Changes:
  client.tsp:
    + @clientName("BarClient", "csharp")

Approve these changes? (Y/n)
```

### 3. Run Tests

**Command:**

```bash
azsdk package run-tests --package-path <path_to_sdk_package> --mode live-record
```

**Options:**

- `--package-path <path>`: Path to the specific SDK package directory (required)
- `--mode <live|live-record|playback>`: Test execution mode (default: playback)
- `--test-filter <pattern>`: Run only tests matching pattern
- `--cleanup`: Tear down test resources after completion (only for live/live-record modes)
- `--resource-group <name>`: Override default resource group name
- `--subscription <id>`: Target specific Azure subscription
- `--location <region>`: Azure region for resource deployment (default: westus2)

**Expected Output:**

```text
Running tests in live-record mode...

✓ Checking test resources...
✓ Resources not found, provisioning...
✓ Resource group azsdk-healthdataaiservices-tests created
✓ Test resources deployed

✓ Running tests...
✓ 15/15 tests passed
✓ Test recordings captured

Test resources remain available for future runs.
Use --cleanup flag to tear down resources.
```

- `--parallel`: Run tests in parallel (when supported)
- `--verbose`: Show detailed test output

**Expected Output:**

```text
Running tests for Health Deidentification SDK...

✓ Python tests passed (12 recordings captured)
✓ .NET tests passed (10 recordings captured)

Test execution complete. Recordings saved to language-specific directories.
```

**Note:** Teardown is handled automatically by the `--cleanup` flag on the test command. To manually tear down resources:

```bash
azsdk package run-tests --package-path <path_to_sdk_package> --mode live --cleanup
```

Or use the cleanup flag separately after tests have completed.

---

## Pipeline & CI Usage

Scenario 2 also needs to consider **how the CLI tools will be used inside CI/CD pipelines** (for example, GitHub Actions, Azure Pipelines), even though the exact requirements are still evolving.

### Goals

- Enable **non-interactive**, repeatable runs of the Scenario 2 workflow using CLI commands.
- Make it easy to plug `azsdk` commands into existing SDK build/test pipelines with minimal glue.
- Ensure environment setup and customization flows behave predictably in headless environments.

### Early Requirements & Considerations

- `azsdk_verify_setup`:
  - Must support **exit codes** that cleanly distinguish "all good", "fixable issues", and "blocking failures".
  - Should be configurable via flags or environment variables in CI (for example, default verify-only mode vs. `--fix` mode with `--auto-install`) to avoid surprise installations on shared agents.
  - In CI, verify-only mode (default) should be safe to run always; fix mode should be opt-in.
- Customization and generation:
  - **Open Question**: Should `azsdk_customized_code_update` be used in CI at all, or is it primarily an agent-mode/interactive tool?
    - **Agent Mode Argument**: Customizations typically require human decision-making (reviewing diffs, approving changes, iterative refinement), making them better suited for interactive agent workflows where developers can provide feedback and guidance.
    - **CI Argument**: Some customizations (like applying consistent API review feedback patterns or automated fixes) might be deterministic enough to run in CI with `--auto-approve` flag.
    - **Hybrid Approach**: Customizations are performed locally in agent mode, committed to source control, and CI only runs generation + build + test on the committed customizations.
  - If CI usage is supported: Commands should accept **explicit paths** for specs, workspaces, and output folders to avoid assumptions about checkout layout, and the tool should provide `--auto-approve` flag for non-interactive CI usage.
  - `azsdk_package_generate_code` should be usable in CI with clear, machine-readable logs for validation workflows.
- Testing:
  - `azsdk_package_run_tests --mode live` or `--mode live-record` is expected to be **opt-in** for CI, guarded behind environment variables or flags and properly parameterized with subscriptions/resource groups.
  - `azsdk_package_run_tests --mode playback` is expected to be **safe to run by default** in CI for regression validation.
  - Tool should support `--cleanup` flag to automatically tear down resources after live test completion in CI.

### Open Design Questions for Pipelines

These are intentionally left for follow-up design (Wes to clarify):

1. Should there be a **single high-level pipeline command** (for example, `azsdk scenario run --id scenario-2`) that orchestrates all stages, or should pipelines explicitly compose lower-level commands?
   - Current thinking: Likely no super high-level command, but hopefully some bigger build and test commands that can be dropped into pipelines
   - **Single Package vs. Package Sets**: Current MCP tools/CLI commands work on a single package, but pipelines typically build a set of packages
     - **Option A**: Enable CLI to accept and process package sets (multiple packages in one invocation)
     - **Option B**: Pipeline loops to call CLI commands one package at a time
     - Trade-offs exist for both approaches; need to experiment with a few commands to determine best approach
   - Decision needed after trying commands in practice to understand performance, logging, error handling, and developer experience implications
2. How should **secrets and Azure credentials** be passed into `azsdk_test_live` in CI (for example, federated credentials, service principals, managed identities)?
   - Current thinking: Credentials would be like AzCLI or AzPS logins for the pipeline step running a particular command
   - Using chained credentials should be sufficient (pipeline environment already has authenticated context)
   - Commands should leverage existing Azure authentication mechanisms rather than requiring explicit credential parameters
3. What level of **artifact publishing** is expected from Scenario 2 runs in CI (logs only, or also generated SDK zips, recordings, environment reports)?
   - Keep existing artifacts that pipelines currently have; tools must produce them in locations the pipeline can pick up
   - Expected artifacts include:
     - **Build packages** for the given language (primary artifacts)
     - **Logs** from tool execution
     - **Other context-specific artifacts** as needed for the given scenario
   - **Open Question**: Should there be a dedicated packing/packaging tool?
     - Packing step is currently missing from the tools
     - Not needed for local development scenarios
     - Required for pipeline scenarios to produce distributable artifacts
     - Decision needed: Add dedicated `azsdk_package_pack` tool or integrate packing into existing tools

---

## Implementation Plan

Focus this scenario’s delivery on producing high-quality MCP/CLI tool specifications and deterministic flows:

1. Author detailed tool specs for new Scenario 2 tools:
   - Enhanced `azsdk_verify_setup` (with optional fix mode for remediation)
   - Enhanced `azsdk_customized_code_update` (unified two-phase customization workflow)
   - Enhanced `azsdk_package_run_tests` (unified testing with automatic resource management)
2. Confirm integration points with existing tools (`azsdk_package_generate_code`, `azsdk_package_build_code`).
3. Define telemetry events (start, success, failure) and common correlation identifiers across all tools.
4. Document unified customization workflow (Phase A TypeSpec → regenerate → Phase B code → build verification).
5. Document testing flows (automatic resource provisioning, test mode selection, recording capture) for both net-new and existing SDKs.
6. Finalize prompt catalog revisions and CLI command adjustments (consolidated commands, simplified options).
7. Update shared docs (`mcp-tools.md`, engineering hub) to include updated tools and flows.
8. Identify any edge cases for environment remediation (unsupported auto-install) and test asset generation complexity to feed into Open Questions.

---

## Documentation Updates

- Update the MCP tools list in `mcp-tools.md` with all new tools introduced in Scenario 2
- Update Eng Hub documentation with Scenario 2 workflow, customization guidance, and testing patterns

---

## Metrics/Telemetry

- Collect telemetry for all MCP tools and CLI commands introduced in Scenario 2
- Track tool usage patterns, success/failure rates, and execution duration to identify opportunities for improvement

---

## Open Questions

### Scope Considerations

**Net-New SDKs:**

- Should [Net-New SDK](#net-new-sdk) support be included in Scenario 2, or deferred to a later scenario?
- Current assessment: Generation and testing stages would require additional work for Net-New SDKs
  - Generation: Project scaffolding and bootstrapping for languages
  - Testing: Test asset creation is complex and may require manual configuration due to service-specific resource requirements
- Decision needed: Include Net-New SDK support now, or focus on existing SDK workflows and add Net-New support in a future scenario?

### Tool and Process Questions

**Tool Auto-Installation Rules:**

- What are the specific criteria for determining which tools can be auto-installed/auto-upgraded by `azsdk_verify_setup` versus which require manual installation with guidance links?

**Customization Decision Logic:**

- What is the complete set of criteria that `azsdk_customized_code_update` should use to determine whether a customization should be done using TypeSpec (Phase A) versus code (Phase B), or both?
- Should the decision logic be language-specific or language-agnostic?

**Test Asset Generation:**

- How much automation is feasible for test asset generation for [Net-New SDKs](#net-new-sdk)?
- What level of manual configuration should be expected versus automated?
- Should there be templates or wizards to assist with complex service-specific configurations?

**Bicep File Creation for Test Resources:**

- How feasible is it to create a tool that can automatically generate bicep files for test resource provisioning?
- What are the primary challenges in automating bicep file creation (service-specific parameters, resource types, configuration complexity, security/credential requirements)?
- Should this be fully automated, semi-automated with templates, or primarily manual with tooling assistance?
- What level of service-specific domain knowledge is required, and how can the tool accommodate varying levels of complexity across different Azure services?
- Should there be a fallback mechanism or clear guidance when automatic bicep generation is not possible?

---

## Related Links

- [Scenario 1 Spec](./0-scenario-1.spec.md)
- [TypeSpec Requirement](https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Requirement)
- [TypeSpec Validation](https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Validation)
- [Specs README](./README.md)
- [Spec Template](./spec-template.md)

---
