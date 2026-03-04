<!-- cspell:words Deidentification HealthDataAIServices healthdataaiservices DeidServices TypeSpec BrandNew rehydrate crosslanguage scaffolding teardown noninteractive changelogs handwritten provisioning rerecordings azsdk AZSDK automatable westus2 -->
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
- Support for **[Brand New Packages](#brand-new-package)** and existing SDKs
- All five languages (.NET, Java, JavaScript, Python, Go)
- Health Deidentification service end to end
- Both **Agent Mode** and **CLI Mode** paths

---

## Definitions

The terminology from [Scenario 1 Definitions](./0-scenario-1.spec.md#definitions) still applies. Scenario 2 introduces the following additional concepts:

<!-- markdownlint-disable MD033 -->
- **<a id="agent"></a>Agent**: GitHub Copilot running in a Copilot-enabled editor (VS Code, Visual Studio, or IntelliJ) with access to the Azure SDK Tools Model Context Protocol (MCP) server, enabling AI-assisted SDK development workflows.
- **<a id="brand-new-package"></a>Brand New Package**: A service library that has not yet shipped a preview release and therefore requires scaffolding for code, tests, and resources.
- **<a id="typespec-customizations"></a>TypeSpec Customizations**: Updates to TypeSpec inputs (for example, `client.tsp`) performed before invoking generation to tailor the generated SDK surfaces.
- **<a id="code-customizations"></a>Code Customizations**: Handwritten code layered on top of generated output after generation completes, maintained in language-specific customization zones.
- **<a id="live-test"></a>Live Test**: A test that executes against Azure resources. When recording is explicitly enabled, it produces recordings for later playback validation.
- **<a id="test-recording"></a>Test Recording**: Captured HTTP interactions produced by live tests when recording is enabled, reused for playback-based validation.
- **<a id="test-infrastructure"></a>Test Infrastructure**: Azure resources, credentials, configuration, and environment settings required to execute or re-record live tests.

---

## Why Scenario 2 Matters

Without coverage for customization, live testing, and **[Brand New Package](#brand-new-package)** creation the toolset would remain fragmented. Scenario 2 delivers:

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
- Agent-mode interactions occur in **VS Code, Visual Studio, or IntelliJ with GitHub Copilot** with the `azure-rest-api-specs` repo open OR using the **Copilot CLI** at the root of the `azure-rest-api-specs` repo
- Azure subscription permits on-demand resource provisioning and teardown
- No need for users to set environment variables to setup the tool

### In Scope for Scenario 2

- **All [Scenario 1 activities](./0-scenario-1.spec.md#workflow) are included**
- All five languages: .NET, Java, JavaScript, Python, Go
- **Both [Brand New Packages](#brand-new-package) and existing SDKs**
- TypeSpec-based generation for the Health Deidentification service
- Both **data plane and management plane** coverage
- Both **Agent Mode and CLI Mode** validation
- All models available through GitHub Copilot:
  - **Required for testing**: Claude Sonnet 4, Claude Sonnet 4.5, GPT-4o, GPT-5
- Automated environment remediation (install/upgrade tooling)
  - Auto-install of user-level tools and packages that don't require administrator permissions (e.g., language-specific package managers, command-line utilities via user-level package managers)
- **[TypeSpec Customizations](#typespec-customizations)** to shape generated surfaces
- **[Code Customizations](#code-customizations)** to add handwritten code layers
- Creation of **[Live Tests](#live-test)**, and new **[Test Recordings](#test-recording)** for [Brand New Packages](#brand-new-package)
- Re-recording tests for existing SDKs
- Automated handling of Azure resources for live tests
- TypeSpec Authoring
- AI-powered sample generation from natural language descriptions
- Sample translation between programming languages
- **Checkpointing by commit**: Creating git commits after each stage that generates or modifies code to provide rollback points and track incremental changes
- **Stretch Goal**: Creation of bicep files for test resource provisioning for [Brand New Packages](#brand-new-package) may be complex and will need to be more thoroughly investigated before committing to full automation
- **Stretch Goal**: AI-powered assistance for authoring live tests for [Brand New Packages](#brand-new-package) may require additional investigation to determine feasibility and implementation approach

### Out of Scope for Scenario 2

- Outer-loop activities such as committing changes, creating PRs, release management, or package publishing
- GA releases
- Architectural review preparation
  - **Note**: Even though architectural review preparation is out of scope for this scenario, instruction files should still provide guidance on how users can initiate the architectural review process outside of the agent workflow until it can be integrated into future scenarios
  - **Note**: Similarly, package naming approval is required for [Brand New Packages](#brand-new-package), and instruction files should provide guidance on how to initiate the package naming approval process outside of the agent workflow
- Breaking change reviews
- Updating changelog/`README.md`/metadata content for data-plane libraries
  - **Note**: For .NET, metadata update functionality (export-api script) already works for both data-plane and management-plane libraries and is implemented in Scenario 1
- Error resolution assistance beyond environment setup remediation
- Auto-installation of system-level tools or upgrades requiring administrator permissions (e.g., full IDEs like Visual Studio, system-wide SDK bundles, platform toolchains)
  - **Note**: These tools will be detected and reported with installation guidance and links, but not automatically installed

---

## Workflow

1. **Environment Setup** → `azsdk_verify_setup`
   - Verify tools and versions (see [Scenario 1 – Environment Setup](./0-scenario-1.spec.md#1-environment-setup))
   - Optionally remediate missing or out-of-date tools by using `install` subcommand instead of `check`
   - **Note**: This stage carries over from Scenario 1 and will need to be revisited to ensure it works correctly for [Brand New Packages](#brand-new-package)

2. **TypeSpec Authoring** → `azsdk_typespec_authoring` **(Agent Mode only)**
   - AI-powered assistance for authoring or modifying TypeSpec API specifications
   - Leverages Azure SDK knowledge base for guidelines-compliant code
   - Helps with ARM resources, versioning, routing, and compliance fixes
   - **Note**: This tool is only available in Agent Mode as it requires conversational interaction and natural language prompts
   - **Checkpoint**: Create a git commit after TypeSpec changes are applied

3. **Generating** → `azsdk_package_generate_code` (local), `azsdk_run_generate_sdk` (pipeline)
   - Generate SDK code, tests, and samples (see [Scenario 1 – Generating](./0-scenario-1.spec.md#2-generating))
   - Generation tooling now handles library project bootstrapping for [Brand New Packages](#brand-new-package)
   - Both pipeline-based generation (`azsdk_run_generate_sdk`) and local generation (`azsdk_package_generate_code`) workflows are fully supported and should work seamlessly
   - **Checkpoint**: Create a git commit after SDK code generation completes

4. **Customizations** → `azsdk_package_customize_code` **(Agent Mode only)**
   - Unified tool for applying both [TypeSpec Customizations](#typespec-customizations) and [Code Customizations](#code-customizations)
   - Two-phase workflow: Phase A (TypeSpec) → Phase B (Code), code patches applied in Phase B to fix build errors after TypeSpec customizations (Phase A) fail.
   - Automatically determines appropriate customization approach based on request
   - Regenerates SDK after TypeSpec changes and validates builds
   - Provide structured guidance for manual intervention If Phase B cannot resolve errors within scope restrictions
   - **Checkpoint**: **User Review**: Changes remain uncommitted in working directory for user review. 

5. **Testing** → `azsdk_package_run_tests`
   - Single unified testing tool that handles all test modes (live, live-record, playback)
   - Automatically provisions test resources when running live tests if not already available
   - Calls existing test resource provisioning scripts
   - **Stretch Goal**: Creation of bicep files for test resource provisioning for [Brand New Packages](#brand-new-package) may be complex and will need to be more thoroughly investigated before committing to full automation
   - **Stretch Goal**: AI-powered assistance for authoring live tests for [Brand New Packages](#brand-new-package) may require additional investigation to determine feasibility and implementation approach

6. **Sample Generation** → `azsdk_package_samples_generate` **(Agent Mode only)**, `azsdk_package_samples_translate`
   - AI-powered sample generation from natural language descriptions (Agent Mode only)
   - Translate existing samples between programming languages (CLI and Agent Mode)
   - Create language-appropriate samples following SDK patterns and conventions
   - **Note**: Sample generation (`azsdk_package_samples_generate`) is only available in Agent Mode as it requires natural language prompts; sample translation (`azsdk_package_samples_translate`) is available in both CLI and Agent Mode
   - **Checkpoint**: Create a git commit after samples are generated or translated

7. **Update Package/Docs/Metadata** → `azsdk_package_update_metadata`, `azsdk_package_update_version`, `azsdk_package_update_changelog_content`
   - Update package metadata, docs, and changelogs (see [Scenario 1 – Update Package/Docs/Metadata](./0-scenario-1.spec.md#3-update-packagedocsmetadata))
   - **Note**: This stage carries over from Scenario 1 and will need to be revisited to ensure it works correctly for [Brand New Packages](#brand-new-package)
   - **Checkpoint**: Create a git commit after metadata, version, and changelog updates are complete

8. **Validating** → `azsdk_package_run_check`
   - Run final validation checks across languages and stages (see [Scenario 1 – Validating](./0-scenario-1.spec.md#4-validating))
   - **Note**: Some validation issue fixing may fall under other stages (for example, build errors caused by customizations would be addressed in the Customizations stage; generation issues would be addressed in the Generating stage)

⚠️  STOP: Test scenario only. Do NOT commit or create release PRs.

---

## Stage Details

### 1. Environment Setup

The **environment verification requirements** for this stage are unchanged from Scenario 1. Refer to [Scenario 1 – Environment Setup](./0-scenario-1.spec.md#1-environment-setup) for the definition of required tools, checks, and success criteria for verification.

Scenario 2 enhances `azsdk_verify_setup` with an **optional auto-install mode** that **offers to install or upgrade missing or out-of-date tooling when automation is not complex**.

**Tool:** `azsdk_verify_setup`

**Behavior:**

- **Check mode (`check` subcommand)**: Detects and reports on tool presence, versions, and compatibility issues without making changes.
- **Install mode (`install` subcommand)**: When used, offers to remediate detected issues:
  - For tools that can be installed/updated **without administrator permissions** (for example, language-specific package managers, command-line utilities installed via user-level package managers):
    - Performs the installation or upgrade using scripted, repeatable steps.
  - For tools that **require system-level installs or administrator permissions** (for example, full IDEs, system-wide SDK bundles, platform toolchains):
    - The [Agent](#agent) must clearly articulate that the tool requires administrator permissions and is out of scope for automatic installation.
    - Directs the user to the **official installation documentation** and captures links surfaced to the user.

**Limitations & Rules:**

- In verify-only mode (default), tool **only detects and reports** issues without attempting any fixes.
- In auto-install mode, tool **MUST NOT** silently install or upgrade tools; user confirmation is always required in Agent Mode.
- **System-level installs or upgrades requiring administrator permissions are out of scope** for auto-install mode. For these tools, auto-install mode **only verifies** presence/version and **outputs links and instructions** instead of attempting installation. The [Agent](#agent) must clearly communicate why the tool cannot be auto-installed (e.g., "Visual Studio requires administrator permissions for installation").
- When possible, auto-install mode attempts to **align versions** to the recommended baseline (for example, minimum and tested versions for TypeSpec, language toolchains, and `azsdk` itself).
- In CLI/automation contexts (for example, CI pipelines), auto-install mode runs in a **non-interactive mode** where installation behavior is controlled via additional flags (for example, `--no-prompt`, `--allow-upgrade`) to avoid blocking prompts.

**Success:**

- All tools required by Scenario 2 are either:
  - Installed at compatible versions, or
  - Explicitly reported as missing with clear installation guidance and links.
- A **defined set of criteria exists** for determining which tools can be auto-installed/auto-upgraded versus which require manual installation with guidance links (see [Open Questions](#open-questions)).

### 2. TypeSpec Authoring (AI-Powered)

Before or during SDK generation, developers may need to author or modify TypeSpec API specifications. Scenario 2 introduces AI-powered assistance for TypeSpec authoring that leverages the Azure SDK knowledge base to generate standards-compliant code.

**Tool:** `azsdk_typespec_authoring`

**Availability:** Agent Mode only - This tool requires conversational interaction with natural language prompts and is not suitable for CLI usage.

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

- Generated TypeSpec code passes compilation without errors, validated with `azsdk_run_typespec_validation` tool. 
- Generated code follows Azure ARM/DP/SDK guidelines (validated by linter/validator)
- Generated code includes proper decorators and templates (no hallucinated decorators)
- Solution includes relevant documentation references
- Responses correctly interpret natural language intent
- Reduces reviewer comments on TypeSpec standards violations

### 3. Generating

Scenario 2 supports both local and pipeline-based SDK generation workflows to accommodate different development preferences and CI/CD requirements.

#### 3a. Local Generation

**Tool:** `azsdk_package_generate_code`

**Action:**

- Generate SDK code, tests, and samples locally for the requested languages
- **Bootstrap language library project scaffolding** when generating a **[Brand New Package](#brand-new-package)**, including directory structure, package metadata files, and docs (`README.md`).
- Triggers downstream validation hooks (`azsdk_package_build_code`, `azsdk_package_run_tests --mode playback`, `azsdk_package_run_check`)

**Success:**

- Generation succeeds for all five languages (or reports failures with diagnostics)
- Existing customization layers remain untouched while new files are clearly identified
- For **[Brand New Packages](#brand-new-package)**, all required project scaffolding is created correctly for each target language

#### 3b. Pipeline-Based Generation

**Tool:** `azsdk_run_generate_sdk`

**Action:**

- Trigger SDK generation through Azure DevOps pipelines for the requested languages
- Handles language-specific pipeline configurations and generation parameters
- **Bootstrap language library project scaffolding** when generating a **[Brand New Package](#brand-new-package)** through pipeline execution

**Success:**

- Pipeline execution completes successfully for all requested languages
- Generated artifacts are available in pipeline outputs or committed to appropriate branches
- For **[Brand New Packages](#brand-new-package)**, all required project scaffolding is created correctly through pipeline automation

### 4. Customizations

Scenario 2 presents a **unified customization experience**: users describe desired outcomes (rename operations, add helper methods, introduce convenience overloads) without needing to choose mechanism up front. The tooling translates intent into the appropriate implementation path using a two-phase workflow.

Customization requests can come from multiple sources:

- **Direct user input**: Natural language descriptions in Copilot chat
- **API View feedback**: Comments and suggestions from API reviewers
- **Build logs**: Compilation errors, warnings, or validation failures that require code adjustments

**Tool:** `azsdk_package_customize_code`

**Availability:** Agent Mode only - This tool requires conversational interaction with natural language prompts and is not suitable for CLI usage.

**Two-Phase Workflow:**

**Phase A – [TypeSpec Customizations](#typespec-customizations):**

- Analyze the customization request to determine if TypeSpec decorators can address the issues
- Apply `client.tsp` adjustments (decorators, naming, grouping, scope configurations) using [Azure.ClientGenerator.Core](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/) decorators
- Re-run TypeSpec compilation and regenerate SDK code
- Validate build and proceed to Phase B only if issues remain
- **Note**: Phase A may identify parts of the request that cannot be handled via `client.tsp` changes and forward those to Phase B

**Phase B – [Code Customizations](#code-customizations):**

- If Phase A doesn't has build failures and customization files exist, apply language-specific code patches
- Apply handwritten custom code on top of generated output
- Use existing patching mechanisms for SDK code modifications
- Apply safe patches: imports, visibility modifiers, reserved keyword renames, annotations
- Validate final build (maximum of 2 fix cycles to prevent infinite loops)

**Unified Experience:**

- Tool accepts a single `customizationRequest` parameter (natural language, build errors, API review feedback, etc.)
- Attempts TypeSpec fixes first (Phase A), then error-driven code repairs if needed (Phase B)
- Generates consolidated diff of all changes (spec + SDK code)
- Enforces approval checkpoint via user review before applying changes (CLI: interactive prompt, MCP: agent UI)
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

**Brand New Package Considerations:**

**Note**: All [Brand New Package](#brand-new-package) testing considerations below are **stretch goals** for Scenario 2 and require additional investigation before committing to full automation.

- **Bicep File Management**: Test resource provisioning for [Brand New Packages](#brand-new-package) requires bicep files with service-specific parameters, resource types, and configuration details that often require human input and domain knowledge.
  - **Option 1 - Bicep File Generation**: A separate tool (`azsdk_package_create_test_resources`) could provide templates and guidance to assist with bicep file creation, though full automation may not be feasible for all services.
  - **Option 2 - Bicep File Validation**: As an alternative, a validator tool could verify that user-authored bicep files follow repository conventions (e.g., setting correct output variables, following naming patterns) without attempting full generation.
  
- **AI-Powered Live Test Authoring**: Generating initial test scaffolding for [Brand New Packages](#brand-new-package) would involve analyzing TypeSpec operations and generating language-specific test code.
  - Could leverage patterns from existing tests and SDK samples
  - May be handled by a separate tool (`azsdk_package_generate_tests`) rather than being part of `azsdk_package_run_tests`
  - Complexity varies by service and may require human guidance for test scenarios and assertions

**Success:**

- Tests run successfully in all modes (live, live-record, playback)
- Test resources are provisioned and torn down cleanly for live tests
- Test recordings are generated or refreshed correctly
- Playback runs succeed with recordings
- For [Brand New Packages](#brand-new-package), test assets are created (with manual input where needed)

### 6. Sample Generation

**Tools:** `azsdk_package_samples_generate`, `azsdk_package_samples_translate`

**Sample Generation (`azsdk_package_samples_generate`):**

**Availability:** Agent Mode only - This tool requires natural language prompts describing sample requirements and is not suitable for CLI usage.

- Generate language-specific code samples from natural language descriptions
- Support multiple samples from single description with multiple scenarios
- Follow language-specific SDK patterns, conventions, and best practices
- Include proper error handling and authentication patterns
- Requires active Azure authentication and Azure OpenAI access

**Sample Translation (`azsdk_package_samples_translate`):**

**Availability:** CLI and Agent Mode - This tool accepts structured parameters and can be used in both modes.

- Translate existing samples between programming languages
- Automatically detect source and target languages from package structure
- Preserve directory structure and adapt to target language idioms
- Follow Azure SDK guidelines for target language

**Success:**

- Generated/translated samples compile and run successfully
- Samples follow language-specific SDK patterns and conventions
- Samples saved to correct language-specific directories

### 7. Update Package/Docs/Metadata

Unchanged from Scenario 1. Refer to [Scenario 1 – Update Package/Docs/Metadata](./0-scenario-1.spec.md#3-update-packagedocsmetadata) for complete details.

**Enhancements for [Brand New Packages](#brand-new-package):**

- **CI Configuration**: A new tool `azsdk_package_update_ci` will be added to provision and update `ci.yml` files for Brand New Packages
- **Java Metadata**: The `azsdk_package_update_metadata` tool will be enhanced to support parent-level and root-level `pom.xml` updates for Java libraries
- **Data Plane Versioning**: The `azsdk_package_update_version` tool will be extended to update versions for data-plane libraries (currently returns no-op for data-plane libraries in Scenario 1)

**Success (additional for Scenario 2):**

- Tooling correctly handles package metadata updates for both [Brand New Packages](#brand-new-package) and existing SDKs
- CI configuration files are provisioned correctly for Brand New Packages
- Java parent and root `pom.xml` files are updated appropriately
- Version updates work for both data-plane and management-plane libraries

### 8. Validating

Unchanged from Scenario 1. Refer to [Scenario 1 – Validating](./0-scenario-1.spec.md#4-validating) for validation steps and success criteria.

**Success (additional for Scenario 2):**

- Validation checks pass for both [Brand New Packages](#brand-new-package) and existing SDKs

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
  - `azsdk_verify_setup` detects issues in verify-only mode and remediates them in auto-install mode (enhanced from Scenario 1)
  - `azsdk_package_customize_code` applies both TypeSpec and code customizations through unified two-phase workflow (new)
  - `azsdk_package_run_tests` handles all test modes with automatic resource management (new)
- **Both [Brand New Packages](#brand-new-package) and existing SDKs** are fully supported throughout all stages

---

## Agent Prompts

### Full Workflow (Brand New Package)

**Prompt:**

```text
I need to create a new Health Deidentification SDK for all languages.
```

**Expected Agent Activity:**

1. **Agent prompts user** to explain the full workflow for creating a Brand New Package, including environment setup, code generation, test creation, and validation steps.
2. **Agent asks user** which languages to target (or confirms "all five languages").
3. Execute `azsdk_verify_setup` for selected languages to check prerequisites.
   - **Agent reports** environment check results (installed tools, missing tools, version mismatches)
   - **Agent prompts user** asking if they want to auto-install the detected issues
   - If user confirms, execute `azsdk_verify_setup install` to remediate issues
   - **Agent prompts user** for confirmation before installing or upgrading any tools in install mode
4. Invoke `azsdk_package_generate_code` to generate SDK code, along with tests and samples that can be generated from TypeSpec.
   - **Note**: For Brand New Packages, generation automatically bootstraps the complete language library project scaffolding including directory structure, package metadata files, and `README.md`
   - After generation completes, validation is performed (build and playback tests)
   - **Agent reports** generation results and any validation issues
5. **Agent asks user** if any customizations are needed.
   - If yes, invoke `azsdk_package_customize_code` with the customization request:
     - Phase A: Apply TypeSpec customizations to `client.tsp`, regenerate SDK, validate build
     - Phase B (only for error repairs): If Phase A fails and build errors exist with customization files present, apply narrow code repairs (<20 lines, deterministic only) to fix compilation errors
     - **Note**: Phase B does NOT add new features (helper APIs, convenience methods). For proactive feature additions, tool provides Manual Guidance to create customizations outside this workflow.
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
   - **Agent prompts user** asking if they want to auto-install the detected issues
   - If user confirms, execute `azsdk_verify_setup install` to remediate issues
   - **Agent prompts user** for confirmation before installing or upgrading any tools in install mode
4. Invoke `azsdk_package_generate_code` to regenerate SDK code, along with tests and samples that can be generated from TypeSpec.
   - After generation completes, validation is performed (build and playback tests)
   - **Agent reports** generation results and any validation issues
5. **Agent asks user** if any customizations are needed (TypeSpec or code level).
   - In this example, user indicates no customizations needed; agent proceeds to next step
   - If customizations were needed, agent would apply `azsdk_package_customize_code` as in Brand New Package workflow
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
2. Call `azsdk_package_customize_code` with the customization request.
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
2. Call `azsdk_package_customize_code` with the customization request.
3. Tool executes Phase A (TypeSpec) - determines TypeSpec cannot address this request (convenience methods require handwritten code).
4. Tool returns with manual guidance:
   - Language-specific customization file locations
   - Code examples from similar SDKs
   - Naming conventions and patterns to follow

### Live Testing (Brand New Package)

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

### TypeSpec Authoring - Add a new preview API version

**Prompt:**

```text
Add a new preview API version 2025-10-01-preview for service widget resource management
```

**Expected Agent Activity:**

1. analyzes current TypeSpec project to identify namespace and version
2. **Agent calls** `azsdk_typespec_retrieve_solution` with the request and collected information
3. Add a enum option `v2025_10_01_preview` in version enum for this new API version and decorate with `@previewVersion`
4. Add a new example folder for the new version `2025-10-01-preview` and copy any still-relevant examples
5. Ask for features to add to this version. e.g.
    - Add new resources
    - Add new operations to an existing resource
    - Add new models, unions, or enums
    - Deprecate resources
    - Deprecate operations
    - Deprecate models, unions, or enums
6. Collect enough information, e.g. if it's operation, clarify if it is async/LRO operation
7. Update code, by default the features will only be added to this new version
8. Summarize all the actions taken and display the reference docs

### TypeSpec Authoring - Update TypeSpec to follow Azure guidelines

**Prompt:**

```text
Update the TypeSpec code to follow Azure guidelines for service widget resource management.
```

**Expected Agent Activity:**

1. Validate the TypeSpec code and display a list of code snippets that violates Azure guidelines, and the suggested fix
1. Let user confirm which one to fix
1. Apply the code fix
1. Compile the fixed TypeSpec code and let user validate the output

### TypeSpec Authoring - Add a new stable API version

**Prompt:**

```text
Add a new stable API version 2025-10-01 for service widget resource management.
```

**Expected Agent Activity:**

1. analyzes current TypeSpec project to identify namespace and version
2. **Agent calls** `azsdk_typespec_retrieve_solution` with the request and collected information
3. Add a enum option `v2025_10_01` in version enum for this new API version
4. Add a new example folder for the new version `2025-10-01` and copy any still-relevant examples
5. Remove preview resources, operations, models, unions, or enums that are not carried over to the stable version
6. Ask for features to add to this version. e.g.
    - Add new resources
    - Add new operations to an existing resource
    - Add new models, unions, or enums
    - Deprecate resources
    - Deprecate operations
    - Deprecate models, unions, or enums
7. Collect enough information, e.g. if it's operation, clarify if it is async/LRO operation
8. Update code, by default the features will only be added to this new version
9. Summarize all the actions taken and display the reference docs

### Sample Generation

**Prompt:**

```text
I need to generate code samples for the Health Deidentification SDK showing common usage scenarios.
```

**Expected Agent Activity:**

1. **Agent asks user** which language(s) to generate samples for.
2. **Agent asks user** to describe the specific scenarios or operations to demonstrate.
3. **Agent calls** `azsdk_package_samples_generate` with the package path and prompt.
4. Tool analyzes the package and generates samples using Azure OpenAI.
5. **Agent reports** the generated sample files and their locations.
6. **Agent suggests** running the samples to verify they work correctly.

**Prompt:**

```text
Generate samples for the Python Health Deidentification SDK demonstrating: 1) Basic deidentification, 2) Batch processing, 3) Custom redaction rules.
```

**Expected Agent Activity:**

1. **Agent calls** `azsdk_package_samples_generate` with:
   - Package path for Python Health Deidentification SDK
   - Prompt with three specific scenarios
2. Tool generates three separate sample files.
3. **Agent reports** the three generated samples:
   - `basic_deidentification.py`
   - `batch_processing.py`
   - `custom_redaction_rules.py`
4. **Agent confirms** samples follow Python SDK patterns and include error handling.

### Sample Translation

**Prompt:**

```text
I have Python samples for Health Deidentification. Can you translate them to TypeScript?
```

**Expected Agent Activity:**

1. **Agent asks user** to confirm the source package path (Python SDK).
2. **Agent asks user** to confirm the target package path (TypeScript SDK).
3. **Agent calls** `azsdk_package_samples_translate` with:
   - `--from` pointing to Python package
   - `--to` pointing to TypeScript package
4. Tool automatically:
   - Detects Python as source language
   - Discovers Python samples directory
   - Filters to only `.py` files
   - Detects TypeScript as target language
   - Translates each Python sample to TypeScript
   - Preserves directory structure
5. **Agent reports** translation results:
   - Number of samples translated
   - Location of translated samples
   - Any issues encountered
6. **Agent suggests** building and testing the TypeScript samples.

**Prompt:**

```text
Translate all Java samples for Health Deidentification to .NET.
```

**Expected Agent Activity:**

1. **Agent calls** `azsdk_package_samples_translate` with:
   - `--from` pointing to Java package directory
   - `--to` pointing to .NET package directory
2. Tool translates all `.java` files from samples directory to C#.
3. **Agent reports** translation summary:
   - Number of Java samples found
   - Number of C# samples generated
   - Sample locations in .NET package
4. **Agent confirms** translated samples follow .NET SDK conventions.

---

## CLI Commands

*Direct command-line interface usage for [CLI mode](./0-scenario-1.spec.md#cli-mode):*

### 1. Verify and Setup Environment

**Command:**

```bash
# Auto-install mode
azsdk verify setup install --languages "Dotnet,Java,JavaScript,Python,Go"

# Verification-only mode
azsdk verify setup check --languages "Dotnet,Java,JavaScript,Python,Go"
```

**Options:**

- `--languages <list>`: Space-separated list of languages to verify (default: language of current repository)
- `--no-prompt`: Skip confirmation prompts and install automatically (for CI/automation, only available with `install` subcommand)
- `--verbose`: Show detailed output for each operation

**Expected Output (install mode):**

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

**Expected Output (check mode):**

```text
Verifying environment for 5 languages...

✓ .NET SDK 9.0.205 installed
✓ Java JDK 11 installed
✓ Node.js 18.x installed
✓ Python 3.11 installed
✗ Go not found - Install from: https://go.dev/doc/install

Environment verification complete: 4/5 languages ready
Run 'azsdk verify setup install' to auto-install missing tools.
```

### 2. Run Tests

**Command:**

```bash
azsdk package samples translate --from <source_package_path> --to <target_package_path>
```

**Options:**

- `--from <path>`: Path to the source package directory (required)
- `--to <path>`: Path to the target package directory (required)
- `--overwrite`: Overwrite existing files without prompting
- `--model <name>`: Azure OpenAI deployment name to use (default: uses configured model)

**Examples:**

```bash
# Translate Python samples to TypeScript
azsdk package samples translate \
  --from ~/azure-sdk-for-python/sdk/ai/azure-ai-projects \
  --to ~/azure-sdk-for-js/sdk/ai/ai-projects \
  --overwrite

# Translate Java samples to .NET
azsdk package samples translate \
  --from ~/azure-sdk-for-java/sdk/ai/azure-ai-projects \
  --to ~/azure-sdk-for-net/sdk/ai/Azure.AI.Projects

# Translate with specific model
azsdk package samples translate \
  --from ~/azure-sdk-for-net/sdk/ai/Azure.AI.Projects \
  --to ~/azure-sdk-for-python/sdk/ai/azure-ai-projects \
  --model gpt-4o
```

**Expected Output:**

```text
Translating samples from Python to TypeScript...

✓ Source language detected: Python
✓ Target language detected: TypeScript
✓ Discovered 5 Python sample files
✓ Translating basic_usage.py → basic_usage.ts
✓ Translating chat_completions.py → chat_completions.ts
✓ Translating embeddings.py → embeddings.ts
✓ Translating file_operations.py → file_operations.ts
✓ Translating streaming.py → streaming.ts
✓ Validating translated samples

Translation complete:
  5 samples translated successfully
  Samples location: samples/
  Directory structure preserved
```

### 3. Translate Samples

**Command:****

```bash
azsdk package samples translate --from <source_package_path> --to <target_package_path>
```

**Options:**

- `--from <path>`: Path to the source package directory (required)
- `--to <path>`: Path to the target package directory (required)
- `--overwrite`: Overwrite existing files without prompting
- `--model <name>`: Azure OpenAI deployment name to use (default: uses configured model)

**Examples:**

```bash
# Translate Python samples to TypeScript
azsdk package samples translate \
  --from ~/azure-sdk-for-python/sdk/ai/azure-ai-projects \
  --to ~/azure-sdk-for-js/sdk/ai/ai-projects \
  --overwrite

# Translate Java samples to .NET
azsdk package samples translate \
  --from ~/azure-sdk-for-java/sdk/ai/azure-ai-projects \
  --to ~/azure-sdk-for-net/sdk/ai/Azure.AI.Projects

# Translate with specific model
azsdk package samples translate \
  --from ~/azure-sdk-for-net/sdk/ai/Azure.AI.Projects \
  --to ~/azure-sdk-for-python/sdk/ai/azure-ai-projects \
  --model gpt-4o
```

**Expected Output:**

```text
Translating samples from Python to TypeScript...

✓ Source language detected: Python
✓ Target language detected: TypeScript
✓ Discovered 5 Python sample files
✓ Translating basic_usage.py → basic_usage.ts
✓ Translating chat_completions.py → chat_completions.ts
✓ Translating embeddings.py → embeddings.ts
✓ Translating file_operations.py → file_operations.ts
✓ Translating streaming.py → streaming.ts
✓ Validating translated samples

Translation complete:
  5 samples translated successfully
  Samples location: samples/
  Directory structure preserved
```

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
  - Should be configurable via subcommands in CI (for example, default check mode vs. install mode) to avoid surprise installations on shared agents.
  - In CI, verify-only mode (default) should be safe to run always; fix mode should be opt-in.
- Customization and generation:
  - **Open Question**: Should `azsdk_package_customize_code` be used in CI at all, or is it primarily an agent-mode/interactive tool?
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
   - Could use command like `azsdk ci` or `npm ci`.
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
     - Add dedicated `azsdk_package_pack` tool or integrate packing into existing tools

---

## Implementation Plan

Focus this scenario’s delivery on producing high-quality MCP/CLI tool specifications and deterministic flows:

1. Author detailed tool specs for new Scenario 2 tools:
   - Enhanced `azsdk_verify_setup` (with optional fix mode for remediation)
   - Enhanced `azsdk_package_customize_code` (unified two-phase customization workflow)
   - Enhanced `azsdk_package_run_tests` (unified testing with automatic resource management)
2. Confirm integration points with existing tools (`azsdk_package_generate_code`, `azsdk_package_build_code`).
3. Define telemetry events (start, success, failure) and common correlation identifiers across all tools.
4. Document unified customization workflow (Phase A TypeSpec → regenerate → Phase B code → build verification).
5. Document testing flows (automatic resource provisioning, test mode selection, recording capture) for both brand new and existing SDKs.
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

**Brand New Packages:**

- Should [Brand New Package](#brand-new-package) support be included in Scenario 2, or deferred to a later scenario?
- Current assessment: Generation and testing stages would require additional work for Brand New Packages
  - Generation: Project scaffolding and bootstrapping for languages
  - Testing: Test asset creation is complex and may require manual configuration due to service-specific resource requirements
- Decision needed: Include Brand New Package support now, or focus on existing SDK workflows and add Brand New Package support in a future scenario?

### Tool and Process Questions

**Tool Auto-Installation Rules:**

- What are the specific criteria for determining which tools can be auto-installed/auto-upgraded by `azsdk_verify_setup` versus which require manual installation with guidance links?
  - Proposed guideline: Anything requiring system-level or administrator permissions should be considered out of scope, as the agent shouldn't have admin permissions. For example, installing Python, .NET SDK, or Java JDK (system-level installations) would be out of scope, while installing `tsp-client` or similar user-level packages (npm packages, pip packages for current user) would be in scope for auto-installation.

**Customization Decision Logic:**

- What is the complete set of criteria that `azsdk_package_customize_code` should use to determine whether a customization should be done using TypeSpec (Phase A) versus code (Phase B), or both?
- Should the decision logic be language-specific or language-agnostic?

**Test Asset Generation:**

- How much automation is feasible for test asset generation for [Brand New Packages](#brand-new-package)?
- What level of manual configuration should be expected versus automated?
- Should there be templates or wizards to assist with complex service-specific configurations?

**Bicep File Creation for Test Resources:**

- How feasible is it to create a tool that can automatically generate bicep files for test resource provisioning?
- What are the primary challenges in automating bicep file creation (service-specific parameters, resource types, configuration complexity, security/credential requirements)?
- Should this be fully automated, semi-automated with templates, or primarily manual with tooling assistance?
- What level of service-specific domain knowledge is required, and how can the tool accommodate varying levels of complexity across different Azure services?
- Should there be a fallback mechanism or clear guidance when automatic bicep generation is not possible?
- We should consult with the GHCP4A team.

**AI-Powered Live Test Authoring:**

- How feasible is it to create an AI-powered tool that can help developers author live tests for [Brand New Packages](#brand-new-package)?
- What are the primary challenges in automating test authoring (understanding service semantics, generating realistic test data, determining appropriate assertions, handling authentication/authorization)?
- Can the tool leverage TypeSpec operations, SDK samples, and existing test patterns to generate initial test scaffolding?
- Should this be a separate tool (`azsdk_package_generate_tests`) or integrated into existing testing workflows?
- What level of human guidance is needed for test scenario selection, test data generation, and validation logic?
- How can the tool handle service-specific complexities and edge cases?

---

## Related Links

- [Scenario 1 Spec](./0-scenario-1.spec.md)
- [TypeSpec Requirement](https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Requirement)
- [TypeSpec Validation](https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Validation)
- [Specs README](./README.md)
- [Spec Template](./spec-template.md)

---
