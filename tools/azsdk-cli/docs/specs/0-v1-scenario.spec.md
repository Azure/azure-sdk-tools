# Spec: V1 scenario

## Table of Contents

- [Overview](#overview)
- [Definitions](#definitions)
- [Why V1 Scenario Matters](#why-v1-scenario-matters)
- [Context & Assumptions](#context--assumptions)
- [Workflow](#workflow)
- [Stage Details](#stage-details)
- [Success Criteria](#success-criteria)
- [Agent Prompts](#agent-prompts)
- [CLI Commands](#cli-commands)
- [Open Questions](#open-questions)

---

## Overview

The V1 scenario defines the end-to-end workflow for generating and validating [preview SDKs](#preview-release) across all five languages (.NET, Java, JavaScript, Python, Go) using the Health Deidentification service as the test case.

**Service**: Health Deidentification

- [Health Deidentification Data Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/ded7abde9c48ba84df36b53dfcaef48a2c134097/specification/healthdataaiservices/HealthDataAIServices.DeidServices)  
- [Health Deidentification MGMT Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/healthdataaiservices/HealthDataAIServices.Management)

**Modes**: Works in both [agent mode](#agent-mode) and [CLI mode](#cli-mode)  
**Goal**: Prove complete SDK local workflow from setup → generate → validate

---

## Definitions

_Terms used throughout this spec with precise meanings:_

- **<a id="agent-mode"></a>Agent Mode**: Interactive mode where the user provides natural language prompts to the AI agent (GitHub Copilot), and the agent executes the appropriate tools and commands automatically.

- **<a id="cli-mode"></a>CLI Mode**: Direct command-line interface mode where the user manually executes specific `azsdk` commands with explicit options and parameters.

- **<a id="preview-release"></a>Preview Release**: A pre-GA (beta) release of an SDK package (not the first preview). Does not require architect review. Indicates the API is not yet stable and may have breaking changes in future releases.

- **<a id="playback-mode"></a>Playback Mode**: Test execution mode that uses pre-recorded HTTP interactions instead of making live calls to Azure services. Enables fast, reliable testing without requiring live Azure resources.

- **<a id="typespec"></a>TypeSpec**: The specification language used to define Azure service APIs. SDK code is generated from TypeSpec specifications located in the azure-rest-api-specs repository.

- **<a id="typespec-customizations"></a>TypeSpec Customizations**: Modifications made to TypeSpec files (such as [client.tsp](#client-tsp)) to customize the SDK generation process. These customizations affect what code is generated but do not modify the generated code itself. Examples include renaming operations, changing parameter types, or customizing client interfaces.

- **<a id="client-tsp"></a>client.tsp**: An optional TypeSpec customization file that allows SDK authors to customize the generated client interface without modifying the service specification. This is a specific type of [TypeSpec customization](#typespec-customizations).

- **<a id="code-customizations"></a>Code Customizations**: Hand-written modifications made directly to generated SDK code after generation. These customizations exist within the SDK language repositories and must be preserved across regeneration. Examples include adding convenience methods, custom error handling, or language-specific optimizations. Also known as "handwritten code" or "customization layer."

- **<a id="pr-checks"></a>PR Checks**: The automated validation pipeline that runs on pull requests in Azure SDK repositories. Includes build, test, linting, breaking change detection, and other validations.

- **<a id="data-plane"></a>Data Plane**: APIs that interact with service data and perform service-specific operations (e.g., uploading blobs, sending messages). Contrast with management plane.

- **<a id="management-plane"></a>Management Plane**: APIs that manage Azure resources themselves (create, delete, update resource configurations). Also known as Resource Management or ARM APIs.

- **<a id="release-plan"></a>Release Plan**: A coordinated release workflow that tracks the release of SDK packages across multiple languages, ensures all validations pass, and manages the PR and release process end-to-end.

---

## Why V1 Scenario Matters

Without a concrete end-to-end scenario, we risk building tools in isolation that don't integrate well. V1 provides:

- Clear definition of successful completion for all teams
- Repeatable test case for validation
- Scope boundaries to prevent feature creep
- Foundation for cross-language consistency

---

## Context & Assumptions

### Environment

- Windows machine with freshly cloned repos (specs + all 5 language repos)
- Existing SDKs already present in language repos
- Test resources already provisioned
- Test recordings already exist ([playback mode](#playback-mode) only)
- [TypeSpec](#typespec) modifications are local only (single repo)

### In Scope for V1

- **All five languages** (.NET, Java, JavaScript, Python, Go) - no exceptions, all must pass
- **[Preview release](#preview-release)** (not first preview, no architect review required)
- **[TypeSpec](#typespec)-based generation** from Health Deidentification service - creating non-compatible version that ignores existing [code customizations](#code-customizations)
- **With or without [client.tsp](#client-tsp)** - handles both scenarios
- **[Playback testing](#playback-mode)** using existing test recordings
- **Both [data plane](#data-plane) and [management plane](#management-plane)** APIs
- **[Agent](#agent-mode) and [CLI modes](#cli-mode)** - both must work
- **Existing changelog generation** - no changes to current process

### Out of scope for V1

- Working with or creating new [SDK code customizations](#code-customizations)
- Creating new [TypeSpec customizations](#typespec-customizations) ([client.tsp](#client-tsp))
- Breaking changes
- Live test execution
- Test resource management
- Linux/macOS support
- First preview releases
- GA releases
- Release management and PR creation

*Note: V1 focuses on local development workflow up to the point where SDKs are ready for PR creation. The outer loop release process is out of scope.*

---

## Workflow

```text
1. Environment Setup → verify-setup
   └─ Check all requirements for all 5 languages

2. Generating → generate-sdk
   └─ Generate SDK code, tests, samples from TypeSpec
   └─ Validate: build, test (playback mode), samples

3. Update Package/Docs/Metadata → update-package
   └─ Update versions, changelogs, READMEs, metadata files
   └─ Validate: versions, READMEs, changelogs

4. Validating → run-pr-checks
   └─ Run all PR CI checks locally to ensure green PR

⚠️  STOP: This is a test scenario only. Do NOT commit these changes or create release PRs.
```

---

## Stage Details

### 1. Environment Setup
**Tool**: `verify-setup` ([#12287](https://github.com/Azure/azure-sdk-tools/issues/12287))  
**Action**: Check all requirements upfront for all languages  
**Success**: All tools/SDKs installed, user knows what's missing

### 2. Generating

**Tool**: `generate-sdk` ([#11403](https://github.com/Azure/azure-sdk-tools/issues/11403))  
**Action**: Generate SDK code, tests, samples from [TypeSpec](#typespec) for Health Deidentification  
**Success**: Clean generation for all 5 languages  
**Validation**: `build-sdk`, `run-tests` ([playback mode](#playback-mode)), `validate-samples` - ensure generated code compiles, tests pass, and samples are valid

### 3. Update Package/Docs/Metadata

**Tool**: `update-package` ([#11827](https://github.com/Azure/azure-sdk-tools/issues/11827))  
**Action**: Update versions, changelogs, READMEs, language-specific files (pom.xml, _meta.json, etc.)  
**Success**: All metadata correctly updated for preview release  
**Validation**: Validate versions, READMEs, changelogs are correctly formatted and updated

### 4. Validating

**Tools**: `run-pr-checks` ([#11431](https://github.com/orgs/Azure/projects/865/views/4?pane=issue&itemId=122229127))  
**Action**: Run all [PR CI checks](#pr-checks) locally before creating PRs  
**Success**: All checks pass for all languages - PR will be green

---

## Success Criteria

V1 is complete when:

- ✅ Complete workflow executes successfully for all 5 languages
- ✅ Works in both [agent mode](#agent-mode) and [CLI mode](#cli-mode)
- ✅ Documentation exists for both modes
- ✅ Repeatable with consistent results
- ✅ Agent prompts produce expected behavior
- ✅ CLI commands execute with expected options and outputs
- ✅ Workflow runs completely locally *Nothing is committed to the repo*

---

## Agent Prompts

_Natural language prompts that should work in [agent mode](#agent-mode) when V1 is complete:_

### Full Workflow

**Prompt:**

```text
I want to prepare a preview version of the Health Deidentification SDK for all languages. Please verify my setup, generate the SDKs, update package metadata, and run all PR checks.
```

**Expected Agent Activity:**

1. Execute `verify-setup` for all 5 languages
2. Execute `generate-sdk` for Health Deidentification service
3. Execute `update-package` to update versions, changelogs, READMEs
4. Execute `run-pr-checks` locally to validate all checks pass
5. Report status and next steps

### Environment Setup

**Prompt:**
```
Check if my environment is ready to generate SDKs for all languages.
```

**Expected Agent Activity:**
1. Execute `verify-setup` for .NET, Java, JavaScript, Python, Go
2. Report which requirements are met and which are missing
3. Provide guidance on how to install missing dependencies

### Generate SDK

**Prompt:**
```
Generate the Health Deidentification SDK for all languages from the TypeSpec in azure-rest-api-specs.
```

**Expected Agent Activity:**
1. Locate Health Deidentification [TypeSpec](#typespec) specifications
2. Execute `generate-sdk` for all 5 languages
3. Execute `build-sdk` to verify compilation
4. Execute `run-tests` in [playback mode](#playback-mode)
5. Execute `validate-samples` to ensure samples are valid
6. Report generation results for each language

### Update Package Metadata

**Prompt:**
```
Update the package metadata for Health Deidentification SDKs to prepare for a preview release.
```

**Expected Agent Activity:**
1. Execute `update-package` for all 5 languages
2. Update version numbers for [preview release](#preview-release)
3. Update changelogs with recent changes
4. Update READMEs and language-specific metadata files
5. Validate all updates are correctly formatted
6. Report what was updated

### Validate for PR

**Prompt:**
```
Run all PR checks locally for the Health Deidentification SDKs before I create pull requests.
```

**Expected Agent Activity:**
1. Execute `run-pr-checks` for all 5 languages
2. Run build, test, lint, breaking change detection
3. Report which checks passed and which failed
4. Provide guidance on fixing any failures

---

## CLI Commands

_Direct command-line interface usage for [CLI mode](#cli-mode):_

### 1. Verify Setup

**Command:**
```bash
azsdk verify-setup --languages .NET,Java,JavaScript,Python,Go
```

**Options:**
- `--languages <list>`: Comma-separated list of languages to check (default: all)
- `--verbose`: Show detailed output for each check
- `--fix`: Attempt to automatically install missing dependencies

**Expected Output:**
```
Checking environment setup for 5 languages...

✓ .NET SDK 9.0.205 installed
✓ Java JDK 11 installed
✓ Node.js 18.x installed
✓ Python 3.11 installed
✗ Go 1.21 not found

Environment check complete: 4/5 languages ready
Run with --fix to install missing dependencies
```

### 2. Generate SDK

**Command:**
```bash
azsdk generate-sdk --service healthdataaiservices --spec-path <path-to-specs> --languages .NET,Java,JavaScript,Python,Go
```

**Options:**
- `--service <name>`: Service name to generate SDK for (required)
- `--spec-path <path>`: Path to azure-rest-api-specs repository (required)
- `--languages <list>`: Languages to generate (default: all)
- `--output-path <path>`: Output directory for generated SDKs (default: current language repo)
- `--with-tests`: Generate test files
- `--with-samples`: Generate sample files
- `--validate`: Build and test after generation

**Expected Output:**
```
Generating Health Deidentification SDK for 5 languages...

✓ .NET: Generated successfully
  - Code: sdk/healthdataaiservices/Azure.Health.Deidentification
  - Tests: 15 files
  - Samples: 8 files
  
✓ Java: Generated successfully
  - Code: sdk/healthdataaiservices/azure-health-deidentification
  - Tests: 12 files
  - Samples: 8 files

... (similar for other languages)

Generation complete: 5/5 languages successful
```

### 3. Update Package Metadata

**Command:**
```bash
azsdk update-package --service healthdataaiservices --version 1.1.0-beta.1 --languages .NET,Java,JavaScript,Python,Go
```

**Options:**
- `--service <name>`: Service name to update (required)
- `--version <version>`: New version number (required)
- `--languages <list>`: Languages to update (default: all)
- `--update-changelog`: Update changelog files
- `--update-readme`: Update README files
- `--changelog-entry <text>`: Custom changelog entry

**Expected Output:**
```
Updating package metadata for Health Deidentification SDK...

✓ .NET: Updated to 1.1.0-beta.1
  - Version updated in .csproj
  - Changelog updated
  - README version badge updated

✓ Java: Updated to 1.1.0-beta.1
  - Version updated in pom.xml
  - Changelog updated
  - README version badge updated

... (similar for other languages)

Package metadata updated for 5/5 languages
```

### 4. Run PR Checks

**Command:**
```bash
azsdk run-pr-checks --service healthdataaiservices --languages .NET,Java,JavaScript,Python,Go
```

**Options:**
- `--service <name>`: Service name to check (required)
- `--languages <list>`: Languages to check (default: all)
- `--checks <list>`: Specific checks to run (build,test,lint,breaking-changes)
- `--parallel`: Run checks in parallel
- `--fail-fast`: Stop on first failure

**Expected Output:**
```
Running PR checks for Health Deidentification SDK...

.NET:
  ✓ Build passed
  ✓ Tests passed (45/45)
  ✓ Lint passed
  ✓ No breaking changes detected

Java:
  ✓ Build passed
  ✓ Tests passed (38/38)
  ✓ Lint passed
  ✓ No breaking changes detected

... (similar for other languages)

PR checks complete: 5/5 languages passed all checks
```

---

## Open Questions

1. **Cross-language failures**: If one language fails validation, block all or continue with others? → _Proposal: Block all for V1_
2. **Rollback**: Include automated rollback or rely on Git? → _Proposal: Git-based rollback for V1_
3. **Real-world validation**: How to ensure scenario isn't too simplified? → _Proposal: Pilot with 2-3 service teams_

---

## Related Links

- [Verify Setup - #12287](https://github.com/Azure/azure-sdk-tools/issues/12287)
- [Generate SDK - #11403](https://github.com/Azure/azure-sdk-tools/issues/11403)
- [Package Metadata Update - #11827](https://github.com/Azure/azure-sdk-tools/issues/11827)
- [Build SDK](https://github.com/orgs/Azure/projects/865/views/4?pane=issue&itemId=122043733)
- [Run PR Checks - #11431](https://github.com/orgs/Azure/projects/865/views/4?pane=issue&itemId=122229127)
- [DevEx Inner Loop Project](https://github.com/orgs/Azure/projects/865/views/4)
