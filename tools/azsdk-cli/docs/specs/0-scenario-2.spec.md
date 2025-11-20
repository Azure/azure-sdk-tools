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

Scenario 2 defines an expanded end-to-end workflow that **builds upon and fully encompasses [Scenario 1](./0-scenario-1.spec.md#overview)**. It includes every step from Scenario 1 (environment setup, SDK generation, package updates, validation) and **adds new stages for [automated environment remediation](#1-environment-setup), [TypeSpec customizations](#typespec-customizations) and [code customizations](#code-customizations)**, plus both [live tests](#live-test) and [test recordings](#test-recording).

**Tool Automation Strategy**: Scenario 2 completes the set of tools that enable fully automatable SDK development processes, providing agents with access to deterministic, repeatable operations. Future scenarios will focus on AI-powered tools for tasks that require intelligent decision-making and cannot be fully automated—such as data plane README generation, content updates, and other activities that benefit from micro-agent collaboration or AI reasoning.

**Unified Customization Experience**: Although Scenario 2 distinguishes between [TypeSpec customizations](#typespec-customizations) and [code customizations](#code-customizations) at the tooling level, users should experience a **single, seamless customization workflow**. The agent automatically determines whether requested changes should be implemented via TypeSpec edits or handwritten code overlays, routing to the appropriate tool transparently. Clear decision criteria and smooth handoffs between customization types are critical to the user experience.

**Service**: Health Deidentification

- [Health Deidentification Data Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/ded7abde9c48ba84df36b53dfcaef48a2c134097/specification/healthdataaiservices/HealthDataAIServices.DeidServices)
- [Health Deidentification MGMT Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/healthdataaiservices/HealthDataAIServices.Management)

**Modes**: Works in both [Agent Mode](./0-scenario-1.spec.md#agent-mode) and [CLI Mode](./0-scenario-1.spec.md#cli-mode)

Scenario 2 validates:

- Complete inner-loop SDK workflow from environment setup → [TypeSpec customizations](#typespec-customizations) → generation → [code customizations](#code-customizations) → testing ([live](#live-test) and [recorded](#test-recording)) → validation
- Support for **[Net-New SDKs](#net-new-sdk)** and existing SDKs
- Workflows across **all five SDK languages** (.NET, Java, JavaScript, Python, Go)
- Health Deidentification service as the end-to-end test service
- Both **Agent Mode** and **CLI Mode**, mirroring [Scenario 1 coverage](./0-scenario-1.spec.md#overview)

---

## Definitions

The terminology from [Scenario 1 Definitions](./0-scenario-1.spec.md#definitions) still applies. Scenario 2 introduces the following additional concepts:

<!-- markdownlint-disable MD033 -->
- **<a id="net-new-sdk"></a>Net-New SDK**: A service library that has not yet shipped a preview release and therefore requires scaffolding for code, tests, and resources.
- **<a id="typespec-customizations"></a>TypeSpec Customizations**: Updates to TypeSpec inputs (for example, `client.tsp`) performed before invoking generation to tailor the generated SDK surfaces.
- **<a id="code-customizations"></a>Code Customizations**: Handwritten code layered on top of generated output after generation completes, maintained in language-specific customization zones.
- **<a id="live-test"></a>Live Test**: An automated test that executes against Azure resources. When recording is explicitly enabled, it produces recordings for later playback validation.
- **<a id="test-recording"></a>Test Recording**: Captured HTTP interactions produced by live tests when recording is enabled, reused for playback-based validation.
- **<a id="test-infrastructure"></a>Test Infrastructure**: Azure resources, credentials, configuration, and environment settings required to execute or re-record live tests.

---

## Why Scenario 2 Matters

Without a workflow that covers customization, live testing, and [Net-New SDK](#net-new-sdk) creation, tooling risks fragmentation and gaps in the developer experience. Scenario 2 ensures:

- Clear success benchmarks for a broader, more realistic SDK workflow including [Net-New SDK](#net-new-sdk) scaffolding
- Repeatable validation that covers customization, testing, and validation stages
- Defined scope for [TypeSpec customizations](#typespec-customizations) and [code customizations](#code-customizations) alongside [test infrastructure](#test-infrastructure)
- A foundation for **cross-language consistency** across the expanded stages
- Confidence that [Scenario 1](./0-scenario-1.spec.md) capabilities remain integrated with the new tooling

---

## Context & Assumptions

### Environment

- Windows machine with freshly cloned repositories (`azure-rest-api-specs` plus all five language repositories)
- TypeSpec modifications are **local only**
- All agent-mode interactions occur in **VS Code with GitHub Copilot**, with the `azure-rest-api-specs` repository open
- Azure subscription access allows on-demand creation and teardown of test resources

### In Scope for Scenario 2

- **All [Scenario 1 activities](./0-scenario-1.spec.md#workflow) are included**
- All five languages: .NET, Java, JavaScript, Python, Go
- **Both [Net-New SDKs](#net-new-sdk) and existing SDKs**
- TypeSpec-based generation for the Health Deidentification service
- Both **data plane and management plane** coverage
- Both **Agent Mode and CLI Mode** validation
- VS Code with GitHub Copilot guidance and automation
- AI models: Claude Sonnet 4 / 4.1, GPT-4, GPT-5
- **Automated environment remediation** to install or upgrade missing/out-of-date tooling
- **[TypeSpec customizations](#typespec-customizations)** to tailor generated SDK surfaces before or after initial generation
- **[Code customizations](#code-customizations)** to add handwritten code layers on top of generated output
- Creation of **[live tests](#live-test)**, **[test infrastructure](#test-infrastructure)** assets, and new **[test recordings](#test-recording)** for [Net-New SDKs](#net-new-sdk)
- Re-recording flows for existing SDKs
- Automated teardown of Azure resources following live-test runs

### Out of Scope for Scenario 2

- Outer-loop activities such as committing changes, creating PRs, release management, or package publishing
- GA releases
- Architectural review preparation
- Breaking change reviews
- Updating changelog/`README.md`/metadata content for data-plane libraries
- Error resolution assistance beyond environment setup remediation
- Platform support beyond Windows
- Editors other than VS Code

---

## Workflow

**Legend**: 🆕 = New tool for Scenario 2 | Existing tool from Scenario 1

1. **Environment Setup** → `azsdk_verify_setup`, 🆕 `azsdk_package_setup_env`
   - Verify tools and versions (see [Scenario 1 – Environment Setup](./0-scenario-1.spec.md#1-environment-setup))
   - Remediate missing or out-of-date tools with azsdk_package_setup_env

2. **Generating** → `azsdk_package_generate_code`
   - Generate SDK code, tests, and samples (see [Scenario 1 – Generating](./0-scenario-1.spec.md#2-generating))
   - **Note**: Generation tooling now handles library project bootstrapping for [Net-New SDKs](#net-new-sdk)

3. **Determine Customization Approach** → 🆕 `azsdk_package_customization_playbook`
   - Consult playbook to determine whether customizations should be done via TypeSpec or code
   - Get decision criteria and rationale based on Azure SDK guidelines

4. **TypeSpec Customizations** → 🆕 `azsdk_package_customize_typespec`
   - Modify client.tsp
   - Validate TypeSpec correctness

5. **Code Customizations** → 🆕 `azsdk_package_customized_code_update`
   - Add handwritten/custom code on top of generated output

6. **Testing** → `azsdk_package_run_tests`, 🆕 `azsdk_package_test_resources_manage`, 🆕 `azsdk_package_test_assets_create`, 🆕 `azsdk_package_test_mode_set`

   If net-new SDK:
   - Create test asset files with `azsdk_package_test_assets_create`
   - Set up test infrastructure and Azure resources with `azsdk_package_test_resources_manage --action create`
   - Set testing mode to live with recording: `azsdk_package_test_mode_set --mode live-record`
   - Run tests with `azsdk_package_run_tests`
   - Tear down Azure resources with `azsdk_package_test_resources_manage --action delete`
   - Set testing mode to playback: `azsdk_package_test_mode_set --mode playback`
   - Run playback tests with `azsdk_package_run_tests`

   If existing SDK:
   - Set testing mode to live: `azsdk_package_test_mode_set --mode live`
   - Run live tests with `azsdk_package_run_tests`
   - Re-record failing or outdated tests
   - Tear down temporary resources with `azsdk_package_test_resources_manage --action delete`
   - Run playback tests with `azsdk_package_run_tests`

7. **Update Package/Docs/Metadata** → `azsdk_package_update_metadata`, `azsdk_package_update_version`, `azsdk_package_update_changelog_content`
   - Update package metadata, docs, and changelogs (see [Scenario 1 – Update Package/Docs/Metadata](./0-scenario-1.spec.md#3-update-packagedocsmetadata))

8. **Validating** → `azsdk_package_run_check`
   - Run final validation checks across languages and stages (see [Scenario 1 – Validating](./0-scenario-1.spec.md#4-validating))

⚠️  STOP: This is a test scenario only. Do NOT commit these changes or create release PRs.

---

## Stage Details

### 1. Environment Setup

The **environment verification requirements** for this stage are unchanged from Scenario 1. Refer to [Scenario 1 – Environment Setup](./0-scenario-1.spec.md#1-environment-setup) for the definition of required tools, checks, and success criteria for verification.

Scenario 2 adds a **companion environment setup tool** that runs **after** environment verification and **offers to install or upgrade missing or out-of-date tooling when automation is not complex**.

**Tool:** `azsdk_package_setup_env`

**Behavior:**

- Consumes the results from `azsdk_verify_setup` and focuses solely on **remediating issues** that were detected.
- For tools that can be installed/updated with **straightforward automation** (for example, language-specific package managers, small command-line utilities):
  - Offers **guided, explicit confirmation** before performing any install or upgrade.
  - Performs the installation or upgrade using scripted, repeatable steps.
- For tools that are **too large or complex** to manage automatically (for example, full IDEs, heavy emulators, system-wide SDK bundles):
  - Directs the user to the **official installation documentation** and captures links surfaced to the user.

**Limitations & Rules:**

- `azsdk_package_setup_env` **MUST NOT** silently install or upgrade tools; user confirmation is always required in Agent Mode.
- For tools in the "too large/complex" category, `azsdk_package_setup_env` **only verifies** presence/version and **outputs links and instructions** instead of attempting installation.
- When possible, `azsdk_package_setup_env` attempts to **align versions** to the recommended baseline (for example, minimum and tested versions for TypeSpec, language toolchains, and `azsdk` itself).
- In CLI/automation contexts (for example, CI pipelines), `azsdk_package_setup_env` runs in a **non-interactive mode** where installation behavior is controlled via flags (for example, `--auto-install`, `--no-install`, `--allow-upgrade`) to avoid blocking prompts.

**Success:**

- All tools required by Scenario 2 are either:
  - Installed at compatible versions, or
  - Explicitly reported as missing with clear installation guidance and links.
- A **single summarized report** is produced (and logged to disk) that `azsdk_verify_setup`, `azsdk_package_setup_env`, and downstream stages can reference.
- A **defined set of criteria exists** for determining which tools can be auto-installed/auto-upgraded versus which require manual installation with guidance links (see [Open Questions](#open-questions)).

### 2. Generating

**Tool:** `azsdk_package_generate_code`

**Action:**

- Generate SDK code, tests, and samples for the requested languages
- **Bootstrap language library project scaffolding** when generating a **[Net-New SDK](#net-new-sdk)**, including directory structure, build files, test infrastructure, and package metadata
- Trigger downstream validation hooks (`azsdk_package_build_code`, `azsdk_package_run_tests --mode playback`, validation for samples)

**Success:**

- Generation succeeds for all five languages (or reports failures with diagnostics)
- Existing customization layers remain untouched while new files are clearly identified
- For **[Net-New SDKs](#net-new-sdk)**, all required project scaffolding is created correctly for each target language

### 3. Determine Customization Approach

Before making customizations, developers need to understand whether their changes should be implemented via [TypeSpec customizations](#typespec-customizations) or [code customizations](#code-customizations). This decision is based on well-defined Azure SDK guidelines and patterns. Scenario 2 presents a **unified customization experience**: users describe desired outcomes (rename operations, add helper methods, introduce convenience overloads) without needing to choose mechanism up front. The agent/tooling translates intent into the appropriate implementation path.

**Tool:** `azsdk_package_customization_playbook`

**Action:**

- Accept a description of the desired customization.
- Analyze the request against Azure SDK customization guidelines.
- Provide a recommendation (TypeSpec, Code, or Both) with rationale.
- Surface where the changes will land (for example, `client.tsp` vs. language-specific customization folders).
- Hide TypeSpec vs. code distinctions unless troubleshooting or explicitly requested.

**Implications for Agent Mode:**

- Automatically invoke `azsdk_package_customize_typespec` for spec-surface shaping (renames, model tweaks, protocol adjustments).
- Automatically invoke `azsdk_package_customized_code_update` for handwritten overlays (helpers, convenience wrappers, language idioms).
- If "Both" is recommended, run TypeSpec updates first, regenerate, then layer code customizations.
- Provide a concise summary of applied changes, grouped by mechanism.

**Success:**

- Clear recommendation is provided for how to proceed with the customization.
- Rationale is based on documented Azure SDK patterns and best practices.
- User receives a single, plain-language summary of what changed, regardless of mechanism.

### 4. TypeSpec Customizations

Although TypeSpec edits conceptually precede generation, in practice they are often introduced **after an initial generation and feedback on the generated SDK surface**. Scenario 2 reflects this iterative flow by making it natural to:

- Run an initial generation.
- Apply or refine TypeSpec customizations.
- Regenerate the SDK to pick up those changes.

**Tool:** `azsdk_package_customize_typespec`

**Action:****

- Modify `client.tsp` to introduce or refine SDK library customizations based on feedback from the generated SDK.
- Validate updated TypeSpec files to ensure compilation across all target languages.
- Capture cross-language guidance (naming, feature toggles) ahead of subsequent generations.

**Success:**

- TypeSpec validation passes for all languages.
- Regenerate SDKs from TypeSpec succeeds.

### 5. Code Customizations

**Tool:** `azsdk_package_customized_code_update`

**Action:**

- Apply handwritten custom code on top of generated output.
- Place customizations into the correct directories and layers for each language, ensuring **project scaffolding properly separates generated and custom code** to maintain regeneration compatibility.
- Refresh customization templates to reflect the current API surface.
- Immediately run a language build (`azsdk_package_build_code`) for each impacted language to verify layering did not break compilation before proceeding to Testing.

**Success:**

- Customizations do not break build or test execution.
- Regeneration retains custom code through language-specific layering patterns, with **project structure ensuring custom code never interferes with generated code updates**.

<!-- Unified Customization Experience content merged into Section 3 above -->

### 6. Testing

This stage branches based on whether the SDK is a [Net-New SDK](#net-new-sdk) or an existing library.

#### Net-New SDK Flow

**Tools:**

- `azsdk_package_run_tests` – Execute tests (from Scenario 1)
- `azsdk_package_test_resources_manage` – Provision/destroy Azure test resources (with `--action create|delete`)
- `azsdk_package_test_assets_create` – Generate test infrastructure files ([test assets](#test-infrastructure))
- `azsdk_package_test_mode_set` – Switch testing mode (with `--mode live|live-record|playback`)

**Workflow:**

- Create test asset files with `azsdk_package_test_assets_create`
- Provision [test infrastructure](#test-infrastructure) and Azure resources with `azsdk_package_test_resources_manage --action create`
- Switch to live recording mode with `azsdk_package_test_mode_set --mode live-record`
- Execute [live tests](#live-test) with `azsdk_package_run_tests`, capturing [test recordings](#test-recording)
- Tear down test resources with `azsdk_package_test_resources_manage --action delete`
- Switch to playback mode with `azsdk_package_test_mode_set --mode playback`
- Execute playback runs with `azsdk_package_run_tests` using newly generated [test recordings](#test-recording)

**Success:**

- [Live tests](#live-test) provision and tear down resources cleanly
- [Test recordings](#test-recording) are generated
- Playback runs succeed with the new [test recordings](#test-recording)

#### Existing SDK Flow

**Tools:**

- `azsdk_package_run_tests` – Execute tests (from Scenario 1)
- `azsdk_package_test_resources_manage` – Provision/destroy temporary Azure test resources
- `azsdk_package_test_mode_set` – Switch testing mode (with `--mode live|live-record|playback`)

**Workflow:**

- Switch to live mode with `azsdk_package_test_mode_set --mode live` (or `--mode live-record` if re-recording is needed)
- Execute [live tests](#live-test) with `azsdk_package_run_tests`
- Re-record failing or outdated tests to refresh [test recordings](#test-recording) (when in `live-record` mode) — this uses the same live-record + run tests mechanism described in the Net-New SDK flow.
- Tear down temporary resources with `azsdk_package_test_resources_manage --action delete`
- Switch to playback mode with `azsdk_package_test_mode_set --mode playback`
- Run playback validation with `azsdk_package_run_tests` using refreshed [test recordings](#test-recording)

**Success:**

- Re-recordings update only impacted files
- Playback runs confirm compatibility with refreshed [test recordings](#test-recording)

### 7. Update Package/Docs/Metadata

Unchanged from Scenario 1. Refer to [Scenario 1 – Update Package/Docs/Metadata](./0-scenario-1.spec.md#3-update-packagedocsmetadata) for complete details.

**Success (additional for Scenario 2):**

- Tooling correctly handles package metadata updates for both [Net-New SDKs](#net-new-sdk) and existing SDKs

### 8. Validating

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
- No changes are committed to repositories
- Live-test recordings are generated or refreshed and playback validation succeeds
- **All new tools function correctly** as defined in their respective stage details:
  - `azsdk_package_setup_env` remediates environment issues
  - `azsdk_package_customize_typespec` modifies and validates TypeSpec
  - `azsdk_package_customized_code_update` applies code customizations
  - `azsdk_package_customization_playbook` provides TypeSpec vs. code guidance
  - `azsdk_package_run_tests` executes tests in current mode
  - `azsdk_package_test_resources_manage` provisions/destroys Azure resources
  - `azsdk_package_test_assets_create` generates test infrastructure files
  - `azsdk_package_test_mode_set` switches testing modes
- **Both [Net-New SDKs](#net-new-sdk) and existing SDKs** are fully supported throughout all stages

---

## Agent Prompts

### Full Workflow (Net-New SDK)

**Prompt:**

```text
Create a new Health Deidentification SDK across all languages: apply any needed customizations, generate code, add helper APIs, and run live tests with recordings then validate playback.
```

**Expected Agent Activity:**

1. Execute `azsdk_verify_setup` for all five languages to confirm prerequisites.
2. Run `azsdk_package_setup_env` to remediate any missing or out-of-date tools.
3. Consult `azsdk_package_customization_playbook` to determine whether customizations should be TypeSpec or code-based.
4. Run `azsdk_package_customize_typespec` to apply and validate TypeSpec updates.
5. Invoke `azsdk_package_generate_code` with validation flags to build, test (playback), and produce samples.
6. Apply `azsdk_package_customized_code_update` across languages to rehydrate handwritten layers.
7. Provision resources and create test assets using `azsdk_package_test_resources_manage --action create` and `azsdk_package_test_assets_create`.
8. Set test mode to live-record using `azsdk_package_test_mode_set --mode live-record`.
9. Execute tests with `azsdk_package_run_tests` to capture new recordings.
10. Tear down resources with `azsdk_package_test_resources_manage --action delete`.
11. Set test mode to playback using `azsdk_package_test_mode_set --mode playback`.
12. Execute tests with `azsdk_package_run_tests` to confirm recordings replay cleanly.
13. Run `azsdk_package_update_metadata`, `azsdk_package_update_version`, and `azsdk_package_update_changelog_content` to refresh versions, changelogs, and docs.
14. Finish with `azsdk_package_run_check` and report overall status plus any follow-up actions.

### Full Workflow (Existing SDK)

**Prompt:**

```text
Update the existing Health Deidentification SDK with requested customizations, regenerate if needed, refresh any outdated test recordings, and validate playback across languages.
```

**Expected Agent Activity:**

1. Execute `azsdk_verify_setup` for selected languages.
2. Run `azsdk_package_setup_env` for remediation.
3. Consult `azsdk_package_customization_playbook` for mechanism recommendations.
4. Apply TypeSpec updates (if recommended) with `azsdk_package_customize_typespec` and validate.
5. Regenerate via `azsdk_package_generate_code` (if TypeSpec changed) with build/test hooks.
6. Apply handwritten updates using `azsdk_package_customized_code_update`.
7. Set test mode to live (or live-record if re-recording is required) with `azsdk_package_test_mode_set`.
8. Run tests using `azsdk_package_run_tests`; perform targeted re-recording in live-record mode.
9. Switch to playback mode and re-run tests.
10. Update metadata/version/changelog and finalize with `azsdk_package_run_check`.

### TypeSpec Customizations Prompt

**Prompt:**

```text
Refine the Health Deidentification client surface: rename two operations for clarity, collapse redundant parameters into a single options object, and validate the updated TypeSpec across languages.
```

**Expected Agent Activity:**

1. Consult `azsdk_package_customization_playbook` to confirm TypeSpec is the appropriate approach.
2. Open the `client.tsp` file and propose edits aligned with the request.
3. Apply the changes via `azsdk_package_customize_typespec`.
4. Execute validation to ensure TypeSpec compilation succeeds across languages.
5. Summarize modifications and surface any validation diagnostics.

### Code Customizations Prompt

**Prompt:**

```text
Add a convenience helper that wraps the deidentification operation with default configuration and retries for all supported SDK languages.
```

**Expected Agent Activity:**

1. Consult `azsdk_package_customization_playbook` to confirm code customization is the appropriate approach.
2. Identify the customization directories for each language.
3. Run `azsdk_package_customized_code_update` with the requested language list.
4. Review resulting diffs to confirm handwritten layers applied without overwriting generated code.
5. Report updated files and any manual follow-up required.

### Live Testing (Net-New SDK)

**Prompt:**

```text
Set up test resources, run live tests, and generate test recordings for this new SDK.
```

**Expected Agent Activity:**

1. Provision required Azure resources using `azsdk_package_test_resources_manage --action create`.
2. Create test asset files using `azsdk_package_test_assets_create`.
3. Set test mode to live-record using `azsdk_package_test_mode_set --mode live-record`.
4. Execute live tests with `azsdk_package_run_tests` to capture recordings.
5. Persist recorded sessions and catalog their locations per language.
6. Tear down temporary Azure resources using `azsdk_package_test_resources_manage --action delete` and report any cleanup issues.

### Live Testing (Existing SDK)

**Prompt:**

```text
Run live tests for the existing SDK and re-record any failing tests.
```

**Expected Agent Activity:**

1. Set test mode to live using `azsdk_package_test_mode_set --mode live`.
2. Execute `azsdk_package_run_tests` for the specified languages to detect drift or failures.
3. If re-recording is needed, set test mode to live-record using `azsdk_package_test_mode_set --mode live-record`.
4. Re-run failing suites with `azsdk_package_run_tests` to refresh only impacted tests.
5. Set test mode to playback using `azsdk_package_test_mode_set --mode playback`.
6. Run `azsdk_package_run_tests` to confirm refreshed recordings succeed.
7. Summarize outcomes and highlight recordings that changed.

### Re-record Outdated Tests

**Prompt:**

```text
Refresh only outdated or failing test recordings for the Health Deidentification SDK and confirm playback still passes.
```

**Expected Agent Activity:**

1. Set test mode to live-record with `azsdk_package_test_mode_set --mode live-record`.
2. Run filtered tests using `azsdk_package_run_tests --test-filter <pattern>` to capture updated recordings.
3. Switch to playback mode with `azsdk_package_test_mode_set --mode playback`.
4. Re-run the same filtered tests to validate new recordings.
5. Summarize which recordings changed and any remaining discrepancies.

---

## CLI Commands

*Direct command-line interface usage for [CLI mode](./0-scenario-1.spec.md#cli-mode):*

### 1. Setup Environment

**Command:**

```bash
azsdk setup env --languages Dotnet Java JavaScript Python Go
```

**Options:**

- `--languages <list>`: Space-separated list of languages to set up (default: language of current repository)
- `--auto-install`: Automatically install missing dependencies without prompts (for CI/automation)
- `--no-install`: Only verify and report, do not install anything
- `--allow-upgrade`: Allow upgrading out-of-date tools
- `--verbose`: Show detailed output for each operation

**Expected Output:**

```text
Setting up environment for 5 languages...

✓ .NET SDK 9.0.205 already installed
✓ Java JDK 11 already installed
✓ Node.js 18.x already installed
✓ Python 3.11 already installed
? Install Go 1.21? (Y/n)
✓ Go 1.21 installed successfully

Environment setup complete: 5/5 languages ready
```

### 2. Determine Customization Approach

**Command:**

```bash
azsdk customization playbook --description "<customization description>"
```

**Options:**

- `--description <text>`: Description of the desired customization (required)
- `--service <name>`: Service name for context (optional)
- `--language <code>`: Target language for language-specific guidance (optional)

*CLI viability note: This command primarily benefits Agent Mode (interactive reasoning). In CLI Mode its value may be limited to emitting a concise, optionally machine-readable (e.g. JSON) recommendation and rationale for chaining. Retain only if downstream automation can consume the decision.*

### 3. TypeSpec Customizations

**Command:**

```bash
azsdk customize typespec --service healthdataaiservices --spec-path <path>
```

**Options:**

- `--service <name>`: Target service short name (required)
- `--spec-path <path>`: Path to the local `azure-rest-api-specs` clone (required)
- `--language <code>`: Optional language filter for validation
- `--validate-only`: Run validation without persisting updated customizations

*CLI viability note: Direct TypeSpec customization via CLI may be less discoverable than agent-guided edits. If kept, prefer a minimal diff + validation summary or a `--json` flag. Otherwise defer complex refactors to Agent Mode.*

### 4. Code Customizations

**Command:**

```bash
azsdk customized-code update --service healthdataaiservices --languages Dotnet Java JavaScript Python Go
```

**Options:**

- `--service <name>`: Service short name (required)
- `--languages <list>`: Space-separated list of languages to update (default: all)
- `--apply-templates`: Rehydrate customization templates with the latest generated files
- `--dry-run`: Display planned changes without writing to disk

*CLI viability note: Applying handwritten layers is often iterative and review-heavy; Agent Mode may be more appropriate. If retained for CLI Mode, focus on a dry-run diff (`--dry-run`) and a structured list of changed files per language for auditability.*

### 5. Manage Test Resources

**Command:**

```bash
azsdk test-resources manage --service healthdataaiservices --action create
```

**Options:**

- `--service <name>`: Service short name (required)
- `--action <create|delete>`: Action to perform (required)
- `--resource-group <name>`: Override the default resource group naming convention
- `--subscription <id>`: Target a specific Azure subscription
- `--location <region>`: Azure region for resource deployment (default: westus2)

**Expected Output:**

```text
Provisioning live-test resources for healthdataaiservices...

✓ Resource group azsdk-healthdataaiservices-tests created
✓ Key Vault configured
✓ Storage account created
✓ App Configuration deployed

Test resources ready for live testing.
```

### 6. Create Test Assets

**Command:**

```bash
azsdk test-assets create --service healthdataaiservices --languages Dotnet Java JavaScript Python Go
```

**Options:**

- `--service <name>`: Service short name (required)
- `--languages <list>`: Space-separated list of languages (default: all)
- `--output-path <path>`: Override default test assets location

**Expected Output:**

```text
Creating test asset files for healthdataaiservices...

✓ .NET: test-resources.json created
✓ Java: test-resources.json created
✓ JavaScript: test-resources.json created
✓ Python: test-resources.json created
✓ Go: test-resources.json created

Test assets created for 5/5 languages.
```

### 7. Set Test Mode

**Command:**

```bash
azsdk test-mode set --mode live-record
```

**Options:**

- `--mode <live|live-record|playback>`: Testing mode to set (required)
- `--package-path <path>`: Path to specific SDK package (default: current directory)
- `--languages <list>`: Space-separated list of languages (default: detected from package)

**Expected Output:**

```text
Setting test mode to live-record...

✓ Test mode set to live-record

Ready to run tests with recording enabled.
```

### 8. Run Tests

**Command:**

```bash
azsdk package run-tests --package-path <path_to_sdk_package>/
```

**Options:**

- `--package-path <path>`: Path to the specific SDK package directory (required)
- `--test-filter <pattern>`: Run only tests matching pattern
- `--parallel`: Run tests in parallel (when supported)
- `--verbose`: Show detailed test output

**Expected Output:**

```text
Running tests for Health Deidentification SDK...

✓ Python tests passed (12 recordings captured)
✓ .NET tests passed (10 recordings captured)

Test execution complete. Recordings saved to language-specific directories.
```

### 9. Teardown Test Resources

**Command:**

```bash
azsdk test-resources manage --service healthdataaiservices --action delete
```

**Options:**

- `--service <name>`: Service short name (required)
- `--action <create|delete>`: Action to perform (required)
- `--resource-group <name>`: Override the default resource group naming convention
- `--subscription <id>`: Target a specific Azure subscription

**Expected Output:**

```text
Tearing down test resources for healthdataaiservices...

✓ Resource group azsdk-healthdataaiservices-tests deleted

Test resources cleaned up successfully.
```

---

## Pipeline & CI Usage

Scenario 2 also needs to consider **how the CLI tools will be used inside CI/CD pipelines** (for example, GitHub Actions, Azure Pipelines), even though the exact requirements are still evolving.

### Goals

- Enable **non-interactive**, repeatable runs of the Scenario 2 workflow using CLI commands.
- Make it easy to plug `azsdk` commands into existing SDK build/test pipelines with minimal glue.
- Ensure environment setup and customization flows behave predictably in headless environments.

### Early Requirements & Considerations

- `azsdk_package_setup_env` and `azsdk_verify_setup`:
  - Must support **exit codes** that cleanly distinguish "all good", "fixable issues", and "blocking failures".
  - Should be configurable via flags or environment variables in CI (for example, `--no-install` or `AZSDK_SETUP_ENV_MODE=verify-only`) to avoid surprise installations on shared agents.
- Customization and generation:
  - `azsdk_package_customize_typespec`, `azsdk_package_generate_code`, and `azsdk_package_customized_code_update` should be chainable in a single job with clear, machine-readable logs.
  - Commands should accept **explicit paths** for specs, workspaces, and output folders to avoid assumptions about checkout layout.
- Testing:
  - `azsdk_test_live` is expected to be **opt-in** for CI, guarded behind environment variables or flags and properly parameterized with subscriptions/resource groups.
  - `azsdk_test_playback` is expected to be **safe to run by default** in CI for regression validation.

### Open Design Questions for Pipelines

These are intentionally left for follow-up design (Wes to clarify):

1. Should there be a **single high-level pipeline command** (for example, `azsdk scenario run --id scenario-2`) that orchestrates all stages, or should pipelines explicitly compose lower-level commands?
2. How should **secrets and Azure credentials** be passed into `azsdk_test_live` in CI (for example, federated credentials, service principals, managed identities)?
3. What level of **artifact publishing** is expected from Scenario 2 runs in CI (logs only, or also generated SDK zips, recordings, environment reports)?

---

## Implementation Plan

Focus this scenario’s delivery on producing high-quality MCP/CLI tool specifications and deterministic flows:

1. Author detailed tool specs for new Scenario 2 tools:
   - `azsdk_package_setup_env`
   - `azsdk_package_customization_playbook`
   - `azsdk_package_test_resources_manage`
   - `azsdk_package_test_assets_create`
   - `azsdk_package_test_mode_set`
2. Confirm integration points with existing tools (`azsdk_package_generate_code`, `azsdk_package_build_code`, `azsdk_package_run_tests`, `azsdk_package_customized_code_update`).
3. Define telemetry events (start, success, failure) and common correlation identifiers across all new tools.
4. Document unified customization routing (playbook → TypeSpec/code execution → build verification).
5. Document testing flows (net-new vs existing, re-record path) with mode transitions.
6. Finalize prompt catalog revisions and CLI command adjustments (output format notes, removal of static examples).
7. Update shared docs (`mcp-tools.md`, engineering hub) to include new tools and flows.
8. Identify any edge cases for environment remediation (unsupported auto-install) to feed into Open Questions.

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

1. **Tool Auto-Installation Rules**: What are the specific criteria for determining which tools can be auto-installed/auto-upgraded by `azsdk_package_setup_env` versus which require manual installation with guidance links?

2. **Customization Decision Criteria**: What is the complete set of criteria that `azsdk_package_customization_playbook` should use to determine whether a customization should be done using TypeSpec customizations versus code customizations?

---

## Related Links

- [Scenario 1 Spec](./0-scenario-1.spec.md)
- [TypeSpec Requirement](https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Requirement)
- [TypeSpec Validation](https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Validation)
- [Specs README](./README.md)
- [Spec Template](./spec-template.md)

---
