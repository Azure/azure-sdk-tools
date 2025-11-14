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
- [Implementation Plan](#implementation-plan)
- [Testing Strategy](#testing-strategy)
- [Documentation Updates](#documentation-updates)
- [Metrics/Telemetry](#metricstelemetry)
- [Open Questions](#open-questions)
- [Related Links](#related-links)

---

## Overview

Scenario 2 defines an expanded end-to-end workflow that **builds upon and fully encompasses [Scenario 1](./0-scenario-1.spec.md#overview)**. It includes every step from Scenario 1 (environment setup, SDK generation, package updates, validation) and **adds new stages for [pre-generation customizations](#pre-generation-customizations) and [post-generation customizations](#post-generation-customizations)**, plus both [live tests](#live-test) and [test recordings](#test-recording).

**Service**: Health Deidentification

- [Health Deidentification Data Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/ded7abde9c48ba84df36b53dfcaef48a2c134097/specification/healthdataaiservices/HealthDataAIServices.DeidServices)
- [Health Deidentification MGMT Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/healthdataaiservices/HealthDataAIServices.Management)

**Modes**: Works in both [Agent Mode](./0-scenario-1.spec.md#agent-mode) and [CLI Mode](./0-scenario-1.spec.md#cli-mode)

Scenario 2 validates:

- Complete inner-loop SDK workflow from environment setup → [pre-generation customizations](#pre-generation-customizations) → generation → [post-generation customizations](#post-generation-customizations) → testing ([live](#live-test) and [recorded](#test-recording)) → validation
- Support for **[Net-New SDKs](#net-new-sdk)** and existing SDKs
- Workflows across **all five SDK languages** (.NET, Java, JavaScript, Python, Go)
- Health Deidentification service as the end-to-end test service
- Both **Agent Mode** and **CLI Mode**, mirroring [Scenario 1 coverage](./0-scenario-1.spec.md#overview)

---

## Definitions

The terminology from [Scenario 1 Definitions](./0-scenario-1.spec.md#definitions) still applies. Scenario 2 introduces the following additional concepts:

<!-- markdownlint-disable MD033 -->
- **<a id="net-new-sdk"></a>Net-New SDK**: A service library that has not yet shipped a preview release and therefore requires scaffolding for code, tests, and resources.
- **<a id="pre-generation-customizations"></a>Pre-Generation Customizations**: Updates to TypeSpec inputs (for example, `client.tsp`) performed before invoking generation to tailor the generated SDK surfaces.
- **<a id="post-generation-customizations"></a>Post-Generation Customizations**: Handwritten code layered on top of generated output after generation completes, maintained in language-specific customization zones.
- **<a id="live-test"></a>Live Test**: An automated test that executes against Azure resources, producing recordings for later playback validation.
- **<a id="test-recording"></a>Test Recording**: Captured HTTP interactions produced by live tests and reused for playback-based validation.
- **<a id="test-infrastructure"></a>Test Infrastructure**: Azure resources, credentials, configuration, and environment settings required to execute or re-record live tests.
- **<a id="customization-tools"></a>Customization Tools**: The `azsdk` commands (`customize typespec`, `customize sdk`) that manage pre- and post-generation customization flows.
<!-- markdownlint-enable MD033 -->

---

## Why Scenario 2 Matters

Without a workflow that covers customization and live testing, tooling risks fragmentation and gaps in the developer experience. Scenario 2 ensures:

- Clear success benchmarks for a broader, more realistic SDK workflow
- Repeatable validation that covers customization, testing, and validation stages
- Defined scope for TypeSpec and handwritten code customizations alongside [test infrastructure](#test-infrastructure)
- A foundation for **cross-language consistency** across the expanded stages
- Confidence that [Scenario 1](./0-scenario-1.spec.md) capabilities remain integrated with the new tooling

---

## Context & Assumptions

### Environment

- Windows machine with freshly cloned repositories (`azure-rest-api-specs` plus all five language repositories)
- **[Net-New SDKs](#net-new-sdk)** and existing SDKs present in language repositories
- No test resources or recordings exist ahead of time
- TypeSpec modifications are **local only**
- All agent-mode interactions occur in **VS Code with GitHub Copilot**, with the `azure-rest-api-specs` repository open
- Azure subscription access allows on-demand creation and teardown of test resources

### In Scope for Scenario 2

- **All [Scenario 1 activities](./0-scenario-1.spec.md#workflow) are included**
- All five languages: .NET, Java, JavaScript, Python, Go
- **[Net-New SDKs](#net-new-sdk)** and subsequent preview releases
- TypeSpec-based generation for the Health Deidentification service
- Optional **TypeSpec customizations** and **code customizations**
- Creation of **[live tests](#live-test)** for [Net-New SDKs](#net-new-sdk) and new **[test recordings](#test-recording)**
- Re-recording flows for existing SDKs
- Both **data plane and management plane** coverage
- Both **Agent Mode and CLI Mode** validation
- Existing changelog generation behaviors
- VS Code with GitHub Copilot guidance and automation
- AI models: Claude Sonnet 4 / 4.1, GPT-4, GPT-5
- Automated teardown of Azure resources following live-test runs

### Out of Scope for Scenario 2

- Error resolution assistance
- Architectural review preparation
- Updating changelog content for data-plane libraries
- Breaking change reviews
- GA releases
- Platform support beyond Windows
- Editors other than VS Code
- Outer-loop activities such as committing changes, creating PRs, release management, or package publishing
- Automated Azure subscription provisioning or policy management

---

## Workflow

```text
1. Environment Setup → verify-setup

2. [Pre-Generation Customizations](#pre-generation-customizations) → customize-typespec
   └─ Modify client.tsp
   └─ Validate TypeSpec correctness

3. Generating → generate-sdk
   └─ Generate SDK code, tests, samples

4. [Post-Generation Customizations](#post-generation-customizations) → customize-sdk
   └─ Add handwritten/custom code on top of generated output

5. Testing → test-sdk

   If [net-new SDK](#net-new-sdk):
     - Setup [test infrastructure](#test-infrastructure) & Azure resources
     - Create [live tests](#live-test)
     - Run [live tests](#live-test)
     - Record test interactions → [test recordings](#test-recording)
     - Tear down Azure resources
     - Run recorded tests using [test recordings](#test-recording)

   If existing SDK:
     - Run [live tests](#live-test)
     - Re-record tests where needed → refresh [test recordings](#test-recording)
     - Tear down Azure resources
     - Run recorded tests

6. Update Package/Docs/Metadata → update-package

7. Validating → validate

⚠️  STOP: This is a test scenario only. Do NOT commit these changes or create release PRs.
```

---

## Stage Details

### 1. Environment Setup

Unchanged from Scenario 1. Refer to [Scenario 1 – Environment Setup](./0-scenario-1.spec.md#1-environment-setup) for tool usage and success criteria.

### 2. Pre-Generation Customizations

**Tool:** `customize-typespec`

**Action:**

- Modify `client.tsp` to introduce service-specific customizations
- Validate updated TypeSpec files to ensure compilation across all target languages
- Capture cross-language guidance (naming, feature toggles) ahead of generation

**Success:**

- TypeSpec validation passes for all languages
- Custom TypeSpec assets remain scoped to the local service branch

### 3. Generating

**Tool:** `generate-sdk`

**Action:**

- Generate SDK code, tests, and samples for the requested languages
- Bootstrap language scaffolding when generating a **[Net-New SDK](#net-new-sdk)**
- Trigger downstream validation hooks (`build-sdk`, `run-tests --mode playback`, `validate-samples`)

**Success:**

- Generation succeeds for all five languages (or reports failures with diagnostics)
- Existing customization layers remain untouched while new files are clearly identified

### 4. Post-Generation Customizations

**Tool:** `customize-sdk`

**Action:**

- Apply handwritten custom code on top of generated output
- Place customizations into the correct directories and layers for each language
- Refresh customization templates to reflect the current API surface

**Success:**

- Customizations do not break build or test execution
- Regeneration retains custom code through language-specific layering patterns

### 5. Testing

This stage branches based on whether the SDK is a [Net-New SDK](#net-new-sdk) or an existing library.

#### Net-New SDK Flow

**Tools:** `test live`, `test playback`, supporting Azure CLI automation scripts

- Provision [test infrastructure](#test-infrastructure) and Azure resources
- Author and execute [live tests](#live-test)
- Record interactions during live execution to create [test recordings](#test-recording)
- Tear down test resources once runs complete
- Execute playback runs using newly generated [test recordings](#test-recording)

**Success:**

- [Live tests](#live-test) provision and tear down resources cleanly
- [Test recordings](#test-recording) are generated and stored in language-specific directories
- Playback runs succeed with the new [test recordings](#test-recording)

#### Existing SDK Flow

**Tools:** `test live`, `test playback`

- Execute [live tests](#live-test)
- Re-record failing or outdated tests to refresh [test recordings](#test-recording)
- Tear down temporary resources
- Run playback validation with refreshed [test recordings](#test-recording)

**Success:**

- Re-recordings update only impacted files
- Playback runs confirm compatibility with refreshed [test recordings](#test-recording)

### 6. Update Package/Docs/Metadata

Unchanged from Scenario 1. Refer to [Scenario 1 – Update Package/Docs/Metadata](./0-scenario-1.spec.md#3-update-packagedocsmetadata) for complete details.

### 7. Validating

Unchanged from Scenario 1. Refer to [Scenario 1 – Validating](./0-scenario-1.spec.md#4-validating) for validation steps and success criteria.

---

## Success Criteria

Scenario 2 is complete when:

- The complete workflow executes successfully for all five languages
- Both Agent Mode and CLI Mode scenarios run end to end
- Documentation exists for both modes, reflecting new customization and testing stages
- Runs are repeatable with consistent, deterministic results
- Agent prompts trigger expected tool sequences
- CLI commands execute with expected outputs
- Workflow runs entirely on the local machine
- No changes are committed to repositories
- Live-test recordings are generated or refreshed and playback validation succeeds

---

## Agent Prompts

### Full Workflow

**Prompt:**

```text
I want to run the full Scenario 2 workflow for the Health Deidentification service. Include pre-generation customizations, generation, post-generation customizations, and both live and recorded test flows.
```

**Expected Agent Activity:**

1. Execute `verify-setup` for all five languages to confirm prerequisites.
2. Run `customize typespec` to apply and validate pre-generation updates.
3. Invoke `generate-sdk` with validation flags to build, test (playback), and produce samples.
4. Apply `customize sdk` across languages to rehydrate handwritten layers.
5. Provision resources and run `test live --record` to capture new recordings, then tear down resources.
6. Execute `test playback` to confirm recordings replay cleanly.
7. Run `update-package` to refresh versions, changelogs, and docs.
8. Finish with `validate` and report overall status plus any follow-up actions.

### Pre-Generation Customizations Prompt

**Prompt:**

```text
Update the client.tsp file for the Health Deidentification service and validate the TypeSpec changes.
```

**Expected Agent Activity:**

1. Open the `client.tsp` file and propose edits aligned with the request.
2. Apply the changes via `customize typespec`.
3. Execute validation (`customize typespec --validate-only`) to ensure TypeSpec compilation succeeds across languages.
4. Summarize modifications and surface any validation diagnostics.

### Post-Generation Customizations Prompt

**Prompt:**

```text
Apply post-generation customizations to the generated SDKs for all languages.
```

**Expected Agent Activity:**

1. Identify the customization directories for each language.
2. Run `customize sdk` with the requested language list.
3. Review resulting diffs to confirm handwritten layers applied without overwriting generated code.
4. Report updated files and any manual follow-up required.

### Live Testing (Net-New SDK)

**Prompt:**

```text
Set up test resources, run live tests, and generate test recordings for this new SDK.
```

**Expected Agent Activity:**

1. Provision required Azure resources using `test live --create` (or supporting scripts).
2. Execute live tests with `test live --record` to capture recordings.
3. Persist recorded sessions and catalog their locations per language.
4. Tear down temporary Azure resources and report any cleanup issues.

### Live Testing (Existing SDK)

**Prompt:**

```text
Run live tests for the existing SDK and re-record any failing tests.
```

**Expected Agent Activity:**

1. Execute `test live` for the specified languages to detect drift or failures.
2. Re-run failing suites with recording enabled to refresh only impacted tests.
3. Run `test playback` to confirm refreshed recordings succeed.
4. Summarize outcomes and highlight recordings that changed.

---

## CLI Commands

### Pre-Generation Customizations Command

```bash
azsdk customize typespec --service healthdataaiservices --spec-path <path>
```

**Options:**

- `--service <name>`: Target service short name (required)
- `--spec-path <path>`: Path to the local `azure-rest-api-specs` clone (required)
- `--language <code>`: Optional language filter for validation
- `--validate-only`: Run validation without persisting updated customizations

**Expected Output:**

```text
Applying TypeSpec customizations for healthdataaiservices...

✓ Updated specification/client.tsp
✓ Validation succeeded for .NET, Java, JavaScript, Python, Go

Custom TypeSpec artifacts ready for SDK generation.
```

### Post-Generation Customizations Command

```bash
azsdk customize sdk --service healthdataaiservices --languages .NET Java JavaScript Python Go
```

**Options:**

- `--service <name>`: Service short name (required)
- `--languages <list>`: Space-separated list of languages to update (defaults to the [Scenario 1 language list](./0-scenario-1.spec.md#context--assumptions))
- `--apply-templates`: Rehydrate customization templates with the latest generated files
- `--dry-run`: Display planned changes without writing to disk

**Expected Output:**

```text
Applying SDK customizations for healthdataaiservices...

✓ .NET: Synchronized customization layer (4 files updated)
✓ Java: Added CustomClientBuilder
✓ JavaScript: Updated convenience layer exports
✓ Python: Regenerated partial classes
✓ Go: Adjusted client options

Customization stage complete. Proceed to live testing.
```

### Live Test Creation

```bash
azsdk test live --service healthdataaiservices --create --record
```

**Options:**

- `--service <name>`: Service short name (required)
- `--create`: Provision the Azure resources needed for live testing
- `--record`: Capture recordings during live execution
- `--languages <list>`: Restrict runs to specific languages
- `--resource-group <name>`: Override the default resource group naming convention
- `--subscription <id>`: Target a specific Azure subscription

**Expected Output:**

```text
Provisioning live-test resources for healthdataaiservices...
✓ Resource group azsdk-healthdataaiservices-tests created
✓ Key Vault, Storage, and App Configuration set up

Running live tests with recording enabled...
✓ Python live tests passed (12 recordings captured)
✓ .NET live tests passed (10 recordings captured)

Tearing down Azure resources...
✓ Resource group deleted

Live test stage complete. Recordings available under /recordings.
```

### Recorded Tests

```bash
azsdk test playback --service healthdataaiservices
```

**Options:**

- `--service <name>`: Service short name (required)
- `--languages <list>`: Languages whose recordings should be replayed
- `--fail-fast`: Stop processing after the first failure
- `--baseline <path>`: Compare new recordings against a baseline directory

**Expected Output:**

```text
Running playback tests for healthdataaiservices...

✓ .NET playback tests passed (10 recordings)
✓ Java playback tests passed (9 recordings)
✓ JavaScript playback tests passed (11 recordings)
✓ Python playback tests passed (12 recordings)
✓ Go playback tests passed (8 recordings)

Playback validation complete. All recordings up to date.
```

---

## Implementation Plan

- Establish prerequisites with `verify-setup`, including Azure CLI login and subscription selection.
- Capture TypeSpec deltas using `customize typespec` and review them locally before generation.
- Execute `generate-sdk` for all languages, followed immediately by `customize sdk` to merge handwritten layers.
- Run the live testing flow (`test live --create --record`) for [Net-New SDKs](#net-new-sdk) or significantly changed SDKs; perform targeted re-recordings for existing SDKs.
- Update package metadata and docs, then execute `test playback` and `validate` to ensure clean validation results.

---

## Testing Strategy

- Unit tests cover customization plumbing to ensure deterministic TypeSpec and SDK outputs.
- Integration tests exercise the full Scenario 2 workflow in both Agent Mode and CLI Mode for [Net-New SDK](#net-new-sdk) and existing SDK permutations.
- Live-test automation validates provisioning scripts across Azure regions used by partner teams.
- Playback tests guard against drift between recordings and generated SDK implementations.
- Cross-language smoke tests confirm feature parity across languages.

---

## Documentation Updates

- Refresh Scenario 2 README sections in language repositories to describe customization and live-test expectations.
- Expand the `azsdk` CLI reference with new customization and testing flags introduced in this scenario.
- Update VS Code agent documentation with sample prompts that cover customization and live-test flows.
- Document resource teardown requirements and best practices to prevent orphaned Azure resources.

---

## Metrics/Telemetry

- Track usage of `customize typespec` and `customize sdk` commands by service and language.
- Measure live-test success/failure rates, including resource provisioning outcomes.
- Monitor recording regeneration frequency to identify services with frequent live-test drift.
- Capture end-to-end workflow duration (setup → validation) to benchmark productivity gains over [Scenario 1](./0-scenario-1.spec.md).
- Aggregate Agent Mode prompt success metrics to refine prompts and tooling.

---

## Open Questions

1. Should live-test resource provisioning support multi-region deployments by default to mirror customer scenarios?
2. What retention policy should apply to recordings generated during repeated local runs?
3. Can customization detection be automated to warn when manual edits diverge from recorded expectations?

---

## Related Links

- [Scenario 1 Spec](./0-scenario-1.spec.md)
- [TypeSpec Customization Guidance](https://github.com/Azure/azure-rest-api-specs/wiki/TypeSpec-Customizations)
- [Verify Setup - #12287](https://github.com/Azure/azure-sdk-tools/issues/12287)
- [Generate SDK - #11403](https://github.com/Azure/azure-sdk-tools/issues/11403)
- [Package Metadata Update - #11827](https://github.com/Azure/azure-sdk-tools/issues/11827)
- [Live Test Automation - #12845](https://github.com/Azure/azure-sdk-tools/issues/12845)
- [Customization Tooling Enhancements - #13012](https://github.com/Azure/azure-sdk-tools/issues/13012)

---

