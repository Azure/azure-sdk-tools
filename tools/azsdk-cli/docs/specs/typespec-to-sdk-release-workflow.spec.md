# Spec: TypeSpec to SDK Release Workflow - End-to-End Orchestration

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals](#goals)
- [Design Proposal](#design-proposal)
  - [Goal 1: Publish API Spec and Release SDK](#goal-1-publish-api-spec-and-release-sdk)
  - [Goal 2: Experiment TypeSpec and Test SDK Generation](#goal-2-experiment-typespec-and-test-sdk-generation)
- [Resumable Workflow Scenarios](#resumable-workflow-scenarios)
- [Sub-Skills Overview](#sub-skills-overview)
- [Agent Prompts](#agent-prompts)
- [Success Criteria](#success-criteria)
- [Open Questions](#open-questions)
- [Exceptions and Limitations](#exceptions-and-limitations)

---

## Definitions

_Terms used throughout this spec with precise meanings:_

### Core Concepts

- **<a id="typespec"></a>TypeSpec**: A language for describing cloud service APIs and generating other API description languages, client and service code, documentation, and other assets. TypeSpec provides highly extensible core language primitives that can describe API shapes common among REST, OpenAPI, GraphQL, gRPC, and other protocols. See [TypeSpec official documentation](https://typespec.io)

- **<a id="sdk"></a>SDK (Software Development Kit)**: Client libraries generated from TypeSpec specifications that allow developers to interact with Azure services in their preferred programming language (.NET, Java, JavaScript, Python, Go).

- **<a id="api-spec"></a>API Specification**: The TypeSpec definition that describes the service's API surface, including operations, models, and endpoints. Stored in the `azure-rest-api-specs` repository.

- **<a id="api-version"></a>API Version**: A versioned identifier (e.g., `2024-01-01`, `2024-01-01-preview`) that represents a specific version of the service's API contract.

### Release and Planning

- **<a id="release-plan"></a>Release Plan**: A coordinated release workflow tracked in Azure DevOps that manages the end-to-end release of SDK packages across multiple languages. It tracks API spec PR, SDK PRs, release status, and ensures all validations pass before release.

- **<a id="release-plan-work-item"></a>Release Plan Work Item**: An Azure DevOps work item that tracks the release plan details including TypeSpec project path, API version, SDK language details, PR links, and release status.

- **<a id="sdk-release-type"></a>SDK Release Type**: The type of SDK release being prepared. Common types include:
  - **Preview**: A pre-GA (beta) release indicating the API is not yet stable and may have breaking changes.
  - **GA (General Availability)**: A stable release with backward compatibility guarantees.
  - **Patch**: A bug fix release that maintains backward compatibility.

- **<a id="service-tree"></a>Service Tree**: Microsoft's service metadata repository that tracks service ownership, compliance, and KPIs. When an SDK is released, the corresponding service KPI is automatically marked as completed.

- **<a id="service-kpi"></a>Service KPI**: Key Performance Indicator in Service Tree that tracks whether a service has released SDKs for all required languages.

### Pull Requests and Reviews

- **<a id="api-spec-pr"></a>API Spec Pull Request**: A pull request in the `azure-rest-api-specs` repository that contains TypeSpec changes defining or updating the service's API.

- **<a id="sdk-pr"></a>SDK Pull Request**: A pull request in a language-specific SDK repository (e.g., `azure-sdk-for-python`) containing generated SDK code, tests, samples, and documentation.

- **<a id="apiview"></a>APIView**: A web-based tool for reviewing the public API surface of SDK packages. Architects and reviewers use APIView to ensure API consistency and compliance with Azure SDK design guidelines.

- **<a id="apiview-suggestions"></a>APIView Suggestions**: Comments and feedback left by reviewers in APIView that require changes to the API surface or SDK implementation.

### SDK Generation

- **<a id="local-sdk-generation"></a>Local SDK Generation**: Running SDK code generation on a developer's local machine using the `azsdk_package_generate_code` tool. Useful for rapid iteration, testing, and troubleshooting.

- **<a id="pipeline-sdk-generation"></a>Pipeline SDK Generation**: Running SDK code generation through Azure DevOps pipelines using the `azsdk_run_generate_sdk` tool. Provides consistent, reproducible builds and automatic PR creation.

- **<a id="tsp-config"></a>tspconfig.yaml**: Configuration file in the TypeSpec project that specifies SDK generation options, emitter settings, and package metadata for each target language.

- **<a id="tsp-location"></a>tsp-location.yaml**: Configuration file in SDK repositories that points to the source TypeSpec project location, enabling SDK regeneration from the same spec.

### TypeSpec Authoring

- **<a id="typespec-customizations"></a>TypeSpec Customizations**: SDK-specific customizations made in the `client.tsp` file to control SDK generation. Examples include renaming operations, adding convenience methods, or modifying parameter types for better SDK ergonomics.

- **<a id="code-customizations"></a>Code Customizations**: Hand-written modifications made directly to generated SDK code after generation. These customizations exist within the SDK language repositories and must be preserved across regeneration.

- **<a id="emitter-options"></a>Emitter Options**: Configuration options passed to TypeSpec emitters that control how SDK code is generated for a specific language. Examples include package version, namespace, and feature flags.

### Validation and Testing

- **<a id="package-validation"></a>Package Validation**: A set of checks that verify the SDK package is ready for release, including changelog validation, dependency checks, linting, and build verification.

- **<a id="pr-checks"></a>PR Checks (CI Validation)**: Automated validation pipelines that run on pull requests in SDK repositories. Includes build, test, linting, breaking change detection, and other validations.

- **<a id="playback-mode"></a>Playback Mode**: Test execution mode that uses pre-recorded HTTP interactions instead of making live calls to Azure services. Enables fast, reliable testing without requiring live Azure resources.

### Workflow States

- **<a id="workflow-step-status"></a>Workflow Step Status**: The current state of a step in the release workflow:
  - **Not Started**: Step has not begun.
  - **In Progress**: Step is currently being executed.
  - **Completed**: Step finished successfully.
  - **Failed**: Step encountered an error and needs attention.
  - **Skipped**: Step was intentionally bypassed (e.g., user already completed it).

---

## Background / Problem Statement

### Current State

Azure service teams face a complex, multi-step process to release SDKs from TypeSpec specifications. The current workflow involves:

1. **Fragmented tooling**: Different tools and processes for TypeSpec authoring, validation, SDK generation, and release management.
2. **Manual coordination**: Service teams must manually track progress across multiple repositories (azure-rest-api-specs, azure-sdk-for-*) and systems (Azure DevOps, GitHub, Service Tree).
3. **Knowledge gaps**: Understanding the complete workflow requires expertise across TypeSpec, SDK generation, APIView reviews, and release processes.
4. **Error-prone handoffs**: Transitioning between steps often leads to missed configurations, incorrect metadata, or forgotten updates.
5. **Difficulty resuming**: When users complete some steps manually or encounter failures, there's no easy way to continue the workflow from the current state.

### Why This Matters

- **Time to define api to release SDK**: Service teams spend significant time navigating the process from defining API spec to releasing SDK.
- **Onboarding friction**: New service teams struggle to understand the end-to-end process.

An intelligent, guided workflow that orchestrates the entire TypeSpec-to-SDK release process will dramatically improve developer productivity, reduce errors, and ensure consistent, high-quality SDK releases.

---

## Goals

1. Provide an end-to-end guided workflow from TypeSpec authoring to SDK release
2. Support two primary user goals: publishing SDKs for release AND experimenting with TypeSpec/SDK generation
3. Enable users to resume the workflow from any intermediate state
4. Automatically track and update release plan status throughout the workflow
5. Provide intelligent decision points (local vs. pipeline generation, troubleshooting guidance)
6. Integrate sub-skills seamlessly for specialized tasks (TypeSpec authoring, APIView resolution, etc.)
7. Support all five SDK languages: .NET, Java, JavaScript, Python, Go

---

## Design Proposal

### Overview

The TypeSpec to SDK Release Workflow is an intelligent orchestration skill that guides users through the complete process of defining APIs in TypeSpec and releasing SDKs. The workflow:

1. **Identifies user intent**: Determines whether the user wants to release SDKs or experiment with TypeSpec
2. **Assesses current state**: Detects existing release plans, PRs, and completed steps
3. **Orchestrates sub-skills**: Invokes specialized skills for TypeSpec authoring, SDK generation, validation, etc.
4. **Tracks progress**: Updates release plan status and local state after each step
5. **Handles failures gracefully**: Provides troubleshooting guidance and alternative paths

### Goal 1: Define API Spec and Release SDK

This is the primary workflow for service teams preparing production SDK releases.

#### Process Flow

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   User      │    │  Prepare    │    │  TypeSpec   │    │    SDK      │    │   Release   │
│   Intent    │───▶│  Release    │───▶│  Readiness  │───▶│  Readiness  │───▶│    SDK      │
│             │    │   Plan      │    │             │    │             │    │             │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
                         │                  │                  │                  │
                         ▼                  ▼                  ▼                  ▼
                   ┌───────────┐      ┌───────────┐      ┌───────────┐      ┌───────────┐
                   │ Create or │      │ Author &  │      │ Generate, │      │ Merge PRs │
                   │ identify  │      │ validate  │      │ test &    │      │ & publish │
                   │ release   │      │ TypeSpec  │      │ create    │      │ packages  │
                   │ plan      │      │           │      │ SDK PRs   │      │           │
                   └───────────┘      └───────────┘      └───────────┘      └───────────┘
```

#### Detailed Step-wise Instructions

##### Step 1: Plan API Spec and SDK Release

**Skill Used**: [Prepare Release Plan Skill](#skill-6-prepare-release-plan)

**Actions**:

1. Check if a release plan already exists for the TypeSpec project
2. If no release plan exists:
   - Prompt user for required information (target release month, TypeSpec project path or service tree ID and product tree ID)
   - Create new release plan work item in Azure DevOps
3. If release plan exists:
   - Retrieve and display release plan details
   - Confirm user wants to continue with existing plan
4. Update local workflow state with release plan ID

**Status Updates**:

- Release plan: Created/Identified
- Local state: Release plan ID stored

---

##### Step 2: Define or Update TypeSpec

**Skill Used**: [TypeSpec Authoring Skill](#skill-2-typespec-authoring-and-validation)

**Actions**:

1. Determine if creating new TypeSpec project or updating existing
2. For new projects:
   - Initialize TypeSpec project structure
   - Guide user through defining service operations
3. For existing projects:
   - Load current TypeSpec definitions
   - Guide user through modifications
4. Apply Azure guidelines and best practices
5. Commit changes locally (checkpoint)

**Status Updates**:

- TypeSpec authoring: In Progress → Completed
- Local state: TypeSpec project path stored

---

##### Step 3: Validate and Compile TypeSpec

**Skill Used**: [TypeSpec Authoring Skill](#skill-2-typespec-authoring-and-validation)

**Actions**:

1. Run TypeSpec compilation
2. If compilation fails:
   - Report errors with guidance
   - Return to Step 2 for corrections
3. If compilation succeeds:
   - Extract API version from TypeSpec
   - Extract package names for each language
4. Update release plan with TypeSpec details:
   - TypeSpec project path
   - API version
   - Package names per language

**Status Updates**:

- TypeSpec validation: Completed
- Release plan: Updated with API version and package names

---

##### Step 4: Choose SDK Generation Method

**Decision Point**:

| Factor | Local Generation | Pipeline Generation |
|--------|------------------|---------------------|
| Tools setup | Require to install all required tool to build and compile the sdk | No need of language generation tools locally |
| Debugging | Easy to debug issues | Requires log analysis |
| PR Creation | Manual | Automatic |
| Best For | Iteration, troubleshooting | Production releases |

**Actions**:

1. Present options to user with pros/cons
2. Record user's choice
3. Proceed to Step 5 (API spec PR) then Step 6 (SDK generation)

---

##### Step 5: Create API Spec Pull Request

**Actions**:

1. Stage TypeSpec changes in local git repository
2. Create branch and commit
3. Push to remote and create PR in azure-rest-api-specs
4. Update release plan with API spec PR link
5. Provide guidance on PR approval process:
   - Required reviewers
   - CI checks to monitor
   - Expected timeline

**Status Updates**:

- API spec PR: Created
- Release plan: Updated with PR link

---

##### Step 6: SDK Generation

###### Step 6.1: Local SDK Generation

**Skill Used**: [Generate SDK Locally Skill](#skill-4-generate-sdk-locally)

**Actions**:

1. Verify environment setup for target languages
2. Run `azsdk_package_generate_code` for each language
3. Build generated code
4. Run tests in playback mode
5. Validate samples
6. If any step fails:
   - Report errors with troubleshooting guidance
   - Allow user to fix and retry
7. Prepare package for release:
   - Update changelog
   - Update metadata
   - Update version
8. Run package validation checks
9. Create SDK pull requests for each language
10. Update release plan with SDK PR links

**Status Updates**:

- SDK generation: Completed per language
- Package preparation: Completed per language
- SDK PRs: Created and linked to release plan

###### Step 6.2: Pipeline SDK Generation

**Skill Used**: [Pipeline SDK Generation](#skill-4-generate-sdk-locally) (via `azsdk_run_generate_sdk`)

**Actions**:

1. Trigger SDK generation pipeline with release plan details
2. Monitor pipeline status for each language
3. If pipeline succeeds:
   - PRs are automatically created
   - Update release plan with PR links
4. If pipeline fails for any language:
   - Report failure details
   - Offer to switch to local generation for troubleshooting
   - Guide user through [Generate SDK Locally Skill](#skill-4-generate-sdk-locally)

**Status Updates**:

- Pipeline: Running → Succeeded/Failed
- SDK PRs: Auto-linked to release plan

---

##### Step 7: Resolve APIView Suggestions

**Skill Used**: [APIView Feedback Resolution Skill](#skill-8-apiview-feedback-resolution)

**Actions**:

1. Check APIView for comments on SDK packages
2. If no action items: Proceed to Step 8
3. If action items exist:
   - Display suggestions grouped by priority
   - Guide user through resolving each item
   - Apply TypeSpec customizations if needed
   - Regenerate SDK if changes were made
   - Update SDK PRs
4. Return to Step 6 if significant changes required

**Status Updates**:

- APIView: Reviewed
- Feedback resolution: Completed

---

##### Step 8: Release SDK

**Actions**:

1. Monitor SDK PR approval status
2. When PRs are approved:
   - Guide user through merge process
3. After merge:
   - Release pipeline automatically triggers
   - Packages published to registries
4. When all languages released:
   - Release plan auto-completes
   - Service Tree KPI updated

**Status Updates**:

- SDK PRs: Merged
- Release: Completed
- Release plan: Auto-completed
- Service KPI: Updated

---

### Goal 2: Experiment TypeSpec and Test SDK Generation

This workflow is for users who want to explore TypeSpec or validate SDK generation without committing to a full release.

#### Process Flow to experiment TypeSpec and SDK from TypeSpec

```
┌─────────────┐    ┌─────────────┐     ┌─────────────┐            ┌─────────────────────────────────────────────────────────────────┐
│   User      │    │  TypeSpec   │     │    SDK      │            │                    OPTIONAL                                     │
│   Intent    │───▶│  Changes    │───▶│ Generation  │───▶ ? ───▶│  Prepare Release Plan ──▶ API & SDK Readiness ──▶ Release SDK  │
│             │    │             │     │  (Local)    │            │                                                                 │
└─────────────┘    └─────────────┘     └─────────────┘            └─────────────────────────────────────────────────────────────────┘
                         │                  │                                              │
                         ▼                  ▼                                              ▼
                   ┌───────────┐      ┌───────────┐                                  ┌───────────┐
                   │ Create or │      │ Generate  │                                  │ Transition│
                   │ update    │      │ & test    │                                  │ to Goal 1 │
                   │ TypeSpec  │      │ SDK       │                                  │ workflow  │
                   │ locally   │      │ locally   │                                  │           │
                   └───────────┘      └───────────┘                                  └───────────┘
```

#### Detailed Step-wise Instructions

##### Step 1: Create or update TypeSpec

**Skill Used**: [TypeSpec Authoring Skill](#skill-2-typespec-authoring-and-validation)

**Actions**:

1. Create new TypeSpec project or modify existing one
2. Iterate freely without release constraints
3. No release plan required at this stage

---

##### Step 2: Validate and Compile TypeSpec

**Actions**:

1. Compile TypeSpec
2. Fix any errors
3. View generated OpenAPI output
4. Iterate as needed

---

##### Step 3: Generate SDK (Optional)

If user wants to see generated SDK:

###### Step 3.1: Verify Environment Setup

**Actions**:

1. Check if required tools are installed for target language(s)
2. Install missing tools/dependencies if needed

###### Step 3.2: Generate SDK Locally

**Actions**:

1. Run local SDK generation
2. Build generated code

###### Step 3.3: Run Package Checks & Validation

**Actions**:

1. Build package
2. Run tests (playback mode)
3. Validate samples
4. Run linting
5. Review and fix any errors

---

##### Step 4: Publish (Optional - Transition to Release Workflow)

If user decides to publish the spec and release SDK:

###### Step 4.1: Create Release Plan

**Actions**:

1. Create release plan work item
2. Update with TypeSpec project details

###### Step 4.2: Update SDK Package Metadata

**Skill Used**: [Package Release Readiness Skill](#skill-5-package-release-readiness)

**Actions**:

1. Update changelog
2. Update package metadata

###### Step 4.3: Create API Spec Pull Request

**Actions**:

1. Create PR in azure-rest-api-specs
2. Update release plan with PR link

###### Step 4.4: Create SDK PRs & Link to Release Plan

**Actions**:

1. Create SDK pull requests
2. Link PRs to release plan

###### Step 4.5: Release SDKs

**Actions**:

1. Follow Goal 1 Steps 7-8 (APIView resolution and release)

---

## Resumable Workflow Scenarios

The workflow is designed to support users who have already completed some steps manually or encountered failures. The agent detects the current state and continues from the appropriate point.

### Scenario A: Release Plan and API Spec PR Already Exist

**Detection**:

- User mentions existing release plan work item ID or PR link
- Agent queries release plan to find linked API spec PR

**Resume Point**: Continue from Step 6 (SDK Generation)

**Actions**:

1. Retrieve release plan details
2. Verify API spec PR is approved/merged
3. Proceed with SDK generation (local or pipeline)
4. Continue through Steps 7-8

```
┌───────────────────────────┐
│ Detected: Release plan    │
│ exists with API spec PR   │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ Verify API spec PR status │
│ (Approved/Merged?)        │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ Resume at Step 6:         │
│ SDK Generation            │
└───────────────────────────┘
```

---

### Scenario B: API Spec PR Exists Without Release Plan

**Detection**:

- User provides API spec PR link
- No release plan found for this PR

**Resume Point**: Create release plan, then continue from Step 6

**Actions**:

1. Create release plan work item
2. Link existing API spec PR to release plan
3. Extract TypeSpec details from PR
4. Proceed with SDK generation

```
┌───────────────────────────┐
│ Detected: API spec PR     │
│ exists, no release plan   │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ Create release plan and   │
│ link API spec PR          │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ Resume at Step 6:         │
│ SDK Generation            │
└───────────────────────────┘
```

---

### Scenario C: Pipeline SDK Generation Failed

**Detection**:

- User reports pipeline failure
- Or pipeline status shows failed for specific language(s)

**Resume Point**: Troubleshoot using local generation

**Actions**:

1. Identify failed language(s)
2. Switch to local SDK generation for debugging
3. Guide user through error resolution
4. If TypeSpec customization needed, invoke [TypeSpec Customization Skill](#skill-3-typespec-customization)
5. Regenerate SDK locally
6. Create SDK PR manually and link to release plan

```
┌───────────────────────────┐
│ Detected: Pipeline        │
│ generation failed         │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ Switch to local gen for   │
│ troubleshooting           │
│ [Local Gen Skill]         │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ Identify and resolve      │
│ errors                    │
└─────────────┬─────────────┘
              │
    ┌─────────┴─────────┐
    │ TypeSpec change   │ Code fix
    │ needed?           │ only
    ▼                   ▼
┌─────────────┐   ┌─────────────┐
│ Apply       │   │ Apply fix   │
│ TypeSpec    │   │ & regenerate│
│ customization│   │             │
└──────┬──────┘   └──────┬──────┘
       │                 │
       └────────┬────────┘
                │
                ▼
┌───────────────────────────┐
│ Create SDK PR & link to   │
│ release plan              │
└───────────────────────────┘
```

---

### Scenario D: SDK Generated, Need Package Preparation Help

**Detection**:

- User has generated SDK code
- Needs help with changelog, metadata, or validation

**Resume Point**: Package release preparation

**Actions**:

1. Identify which package preparation steps are needed
2. Update changelog content
3. Update package metadata
4. Run validation checks
5. Create or update SDK PR

```
┌───────────────────────────┐
│ Detected: SDK generated,  │
│ needs package prep        │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ Run Package Release       │
│ Readiness Skill           │
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ • Update changelog        │
│ • Update metadata         │
│ • Run validations         │
│ • Create/update SDK PR    │
└───────────────────────────┘
```

---

## Sub-Skills Overview

The end-to-end workflow orchestrates these specialized sub-skills:

### Skill 1: TypeSpec to SDK Workflow (This Document)

**Purpose**: End-to-end orchestration of the complete TypeSpec to SDK release process.

**Capabilities**:

- Identify user intent (release vs. experiment)
- Detect current workflow state and resume appropriately
- Orchestrate sub-skills in correct sequence
- Track and update release plan status
- Handle decision points (local vs. pipeline generation)
- Provide guidance at each step

**Related Tools**:

- `azsdk_create_release_plan`
- `azsdk_get_release_plan_for_spec_pr`
- `azsdk_update_sdk_details_in_release_plan`
- `azsdk_link_sdk_pull_request_to_release_plan`

---

### Skill 2: TypeSpec Authoring and Validation

**Purpose**: AI-powered assistance for creating and modifying TypeSpec API specifications.

**Capabilities**:

- Create new TypeSpec projects following Azure guidelines
- Modify existing TypeSpec definitions
- Ensure compliance with ARM/Data Plane guidelines
- Compile and validate TypeSpec
- Provide suggestions based on Azure SDK knowledge base

**Related Tools**:

- `azsdk_typespec_generate_authoring_plan`
- TypeSpec compiler commands

**Spec Reference**: [typespec-authoring.spec.md](typespec-authoring.spec.md)

---

### Skill 3: TypeSpec Customization

**Purpose**: Apply SDK-specific customizations to TypeSpec to control generated code.

**Capabilities**:

- Modify `client.tsp` for SDK-specific behavior
- Add convenience methods
- Rename operations or parameters
- Apply language-specific customizations

**Related Tools**:

- `azsdk_package_customize_code`

**Spec Reference**: [3-customizing-client-tsp.spec.md](3-customizing-client-tsp.spec.md)

---

### Skill 4: Generate SDK Locally

**Purpose**: Run SDK code generation on local machine for rapid iteration and troubleshooting.

**Capabilities**:

- Verify environment setup for target languages
- Generate SDK code from TypeSpec
- Build generated code
- Troubleshoot generation failures
- Compare local generation with pipeline results

**Related Tools**:

- `azsdk_package_generate_code`
- `azsdk_verify_setup`

---

### Skill 5: Package Release Readiness

**Purpose**: Prepare SDK packages for release with proper metadata, changelog, and validation.

**Capabilities**:

- Update changelog entries
- Update package metadata (version, authors, description)
- Run package validation checks (changelog, dependencies, linting)
- Verify package is ready for PR creation

**Related Tools**:

- `azsdk_package_update_changelog_content`
- `azsdk_package_update_metadata`
- `azsdk_package_run_check`

**Spec Reference**: [6-package-updater.spec.md](6-package-updater.spec.md)

---

### Skill 6: Prepare Release Plan

**Purpose**: Create and manage release plan work items for coordinated SDK releases.

**Capabilities**:

- Create new release plan work items
- Update release plan with TypeSpec details
- Link API spec PR to release plan
- Link SDK PRs to release plan
- Track release status

**Related Tools**:

- `azsdk_create_release_plan`
- `azsdk_get_release_plan_for_spec_pr`
- `azsdk_update_sdk_details_in_release_plan`
- `azsdk_update_api_spec_pull_request_in_release_`
- `azsdk_link_sdk_pull_request_to_release_plan`

---

### Skill 7: Experiment TypeSpec and SDK

**Purpose**: Allow users to explore TypeSpec and SDK generation without committing to a release.

**Capabilities**:

- Create temporary TypeSpec projects
- Iterate quickly on TypeSpec changes
- Generate SDKs locally for testing
- Transition to full release workflow when ready

**Uses Skills**: 2, 4, 5

---

### Skill 8: APIView Feedback Resolution

**Purpose**: Review and resolve feedback from APIView reviews.

**Capabilities**:

- Retrieve APIView comments for SDK packages
- Categorize feedback by priority
- Guide user through resolving suggestions
- Apply necessary TypeSpec or code changes
- Trigger SDK regeneration after changes

**Related Tools**:

- `azsdk_apiview_get_comments`

---

### Skill 9: PR and CI Pipeline Troubleshooting

**Purpose**: Diagnose and resolve failures in SDK pull requests and CI pipelines.

**Capabilities**:

- Analyze pipeline build logs
- Identify common failure patterns
- Provide resolution guidance
- Guide through local reproduction
- Suggest fixes for failing checks

**Related Tools**:

- Pipeline analysis tools (activate via `activate_pipeline_analysis_tools`)

---

## Agent Prompts

### Goal 1: Release SDK Prompts

#### Starting a New Release

**Prompt:**

```text
I need to create a new TypeSpec project and release SDK for my Azure service.
```

**Expected Agent Activity:**

1. Ask clarifying questions (service name, target languages, release timeline)
2. Create release plan work item
3. Initialize TypeSpec project
4. Guide through TypeSpec authoring
5. Continue with full release workflow

---

**Prompt:**

```text
I have updated my TypeSpec and need to release a new version of the SDK.
```

**Expected Agent Activity:**

1. Identify TypeSpec project location
2. Check for existing release plan or create new one
3. Validate TypeSpec changes
4. Update release plan with new API version
5. Continue with SDK generation and release

---

**Prompt:**

```text
I need to update my API and release SDK for Python, .NET, and Java.
```

**Expected Agent Activity:**

1. Identify TypeSpec project
2. Guide through TypeSpec modifications
3. Create release plan for specified languages only
4. Generate SDKs for Python, .NET, and Java
5. Create and link SDK PRs

---

#### Resuming an Existing Workflow

**Prompt:**

```text
I have a release plan (work item 12345) and the API spec PR is already merged. Help me generate the SDKs.
```

**Expected Agent Activity:**

1. Retrieve release plan details using work item ID
2. Verify API spec PR is merged
3. Determine SDK generation method preference
4. Generate SDKs and create PRs
5. Link SDK PRs to release plan

---

**Prompt:**

```text
My SDK generation pipeline failed for Python. Help me troubleshoot and fix it.
```

**Expected Agent Activity:**

1. Ask for release plan ID or pipeline run URL
2. Analyze pipeline failure
3. Switch to local SDK generation for troubleshooting
4. Guide through error resolution
5. Regenerate SDK and create PR manually

---

**Prompt:**

```text
I have generated the SDK locally but need help preparing it for release.
```

**Expected Agent Activity:**

1. Identify SDK package location
2. Run package validation checks
3. Update changelog
4. Update metadata
5. Guide through PR creation

---

### Goal 2: Experiment TypeSpec Prompts

**Prompt:**

```text
I need to create a new TypeSpec project and see what the generated SDK looks like.
```

**Expected Agent Activity:**

1. Clarify this is for experimentation (not release)
2. Initialize TypeSpec project
3. Guide through TypeSpec authoring
4. Compile and validate TypeSpec
5. Ask if user wants to generate SDK
6. If yes, run local SDK generation

---

**Prompt:**

```text
I want to experiment with changing my TypeSpec and checking the generated SDK.
```

**Expected Agent Activity:**

1. Load existing TypeSpec project
2. Guide through modifications
3. Compile and show differences
4. Generate SDK locally to verify changes
5. Ask if user wants to proceed to release

---

**Prompt:**

```text
I've been experimenting with TypeSpec and now I'm ready to publish and release.
```

**Expected Agent Activity:**

1. Transition to Goal 1 workflow
2. Create release plan
3. Update package metadata and changelog
4. Create API spec PR
5. Continue with full release workflow

---

### Troubleshooting Prompts

**Prompt:**

```text
The APIView has comments I need to address before my SDK PR can be approved.
```

**Expected Agent Activity:**

1. Retrieve APIView comments
2. Categorize by priority
3. Guide through resolving each comment
4. Apply TypeSpec customizations if needed
5. Regenerate SDK and update PR

---

**Prompt:**

```text
My SDK PR CI is failing. Help me fix it.
```

**Expected Agent Activity:**

1. Identify failing checks
2. Analyze error messages
3. Provide specific guidance for each failure
4. Guide through local reproduction if needed
5. Help apply fixes and update PR

---

## Success Criteria

This workflow is complete when:

- Users can complete full TypeSpec to SDK release workflow with agent guidance
- Workflow correctly identifies user intent (release vs. experiment) from natural language
- Workflow detects existing state and resumes from appropriate step
- Release plan is automatically updated at each step
- All sub-skills integrate seamlessly
- Local and pipeline SDK generation paths both work correctly
- APIView feedback can be retrieved and resolved within workflow
- Works for all 5 SDK languages (.NET, Java, JavaScript, Python, Go)
- Clear guidance provided at each decision point
- Errors and failures are handled gracefully with troubleshooting guidance

---

## Open Questions

- **How should the workflow handle architect review requirements for first preview/GA?**
  - Context: These require human approval outside the workflow
  - Options: Block workflow, parallel track, manual override

- **Should experiment workflow results be persisted for later release conversion?**
  - Context: Users may want to convert experiments to releases later
  - Options: Auto-save state, manual save, ephemeral only

---

## Exceptions and Limitations

### Exception 1: First GA Releases Requiring Architect Review

**Description:**
First GA releases require architect board review, which involves human decision-making outside the automated workflow.

**Impact:**
The workflow cannot fully automate the architect review process.

### Exception 2: Breaking Change Reviews

**Description:**
SDKs with breaking changes require additional review processes that cannot be fully automated.

**Impact:**
Breaking change releases may require manual intervention.

### Exception 3: Package Naming Approval for New Packages

**Description:**
Brand new SDK packages require package naming approval before release.

**Impact:**
New package releases are blocked until naming is approved.

**Workaround:**
The workflow prompts users to initiate package naming approval and tracks the status. Users can continue with SDK generation while awaiting approval.
