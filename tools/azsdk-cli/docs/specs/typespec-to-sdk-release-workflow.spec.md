# Spec: TypeSpec to SDK Release Workflow - End-to-End Orchestration

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals](#goals)
- [Design Proposal](#design-proposal)
  - [Goal 1: Define API Spec and Release SDK](#goal-1-define-api-spec-and-release-sdk)
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

- **<a id="service-kpi"></a>Service KPI**: Key Performance Indicator in Service Tree for cloud life cycle and these KPIs are marked as completed when a release plan is completed according to service life cycle.

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
2. Support two primary user goals: publishing API Specs & SDKs for release AND experimenting with TypeSpec/SDK generation
3. Enable users to resume the workflow from any intermediate state
4. Automatically track and update release plan status throughout the workflow
5. Show a visual representation of steps completed in the workflow and what's pending.
6. Provide intelligent decision points (local vs. pipeline generation, troubleshooting guidance)
7. Integrate sub-skills seamlessly for specialized tasks (TypeSpec authoring, APIView resolution, etc.)
8. Support all five SDK languages: .NET, Java, JavaScript, Python, Go
9. Create a fully automated agentic workflow to query all required details from the user and run the steps in the workflow and until generating spec and SDK pull requests

---

## Design Proposal

### Overview

The TypeSpec to SDK Release Workflow is an intelligent orchestration skill that guides users through the complete process of defining APIs in TypeSpec and releasing SDKs. The workflow:

1. **Identifies user intent**: Determines whether the user wants to release SDKs or experiment with TypeSpec. This will also create a temporary release plan if a release plan does not exist for the TypeSpec project.
2. **Assesses current state**: Detects existing release plans, PRs, and completed steps
3. **Orchestrates sub-skills**: Invokes specialized skills for TypeSpec authoring, SDK generation, validation, etc.
4. **Tracks progress**: Updates release plan status and local state after each step
5. **Handles failures gracefully**: Provides troubleshooting guidance and alternative paths

### Goal 1: Define API Spec and Release SDK

This is the primary workflow for service teams preparing production SDK releases.

#### Process Flow

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   User      │    │  TypeSpec   │    │ Plan Spec   │    │    SDK      │    │   Release   │
│   Intent    │───▶│  Readiness  │───▶│ and SDK     │───▶│  Readiness  │───▶│    SDK      │
│             │    │             │    │   release   │    │             │    │             │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
      │                  │                  │                  │                  │
      ▼                  ▼                  ▼                  ▼                  ▼
┌───────────┐      ┌───────────┐      ┌───────────┐      ┌───────────┐      ┌───────────┐
│ Create or │      │ Author &  │      │ Update    │      │ Generate, │      │ Merge PRs │
│ find temp │      │ validate  │      │ release   │      │ test &    │      │ & publish │
│ release   │      │ TypeSpec  │      │ plan from │      │ create    │      │ packages  │
│ plan      │      │           │      │ temp to   │      │ SDK PRs   │      │           │
└───────────┘      └───────────┘      │ actual &  │      └───────────┘      └───────────┘
                                      │ update    │
                                      │ details   │
                                      └───────────┘
```

#### Detailed Step-wise Instructions

```
                                    ┌─────────────────┐
                                    │  User provides  │
                                    │  initial prompt │
                                    └────────┬────────┘
                                             │
                                             ▼
                               ┌─────────────────────────────┐
                               │  Identify user goal:        │
                               │  Release SDK or Experiment? │
                               └─────────────┬───────────────┘
                                             │
                          ┌──────────────────┴──────────────────┐
                          │                                     │
                          ▼                                     ▼
              ┌───────────────────────┐             ┌───────────────────────┐
              │   GOAL 1: Release     │             │ GOAL 2: Experiment    │
              │   (This section)      │             │ (See Goal 2)          │
              └───────────┬───────────┘             └───────────┬───────────┘
                          │                                     │
                          ▼                                     ▼                                                    
              ┌───────────────────────┐             ┌───────────────────────┐
              │ Check existing state: │             │ Create a temporary    │
              │ • Release plan?       │             │ release plan          │
              │ • API spec PR?        │             └───────────────────────┘
              │ • SDK PRs?            │
              └───────────┬───────────┘
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ No existing  │  │ Has release  │  │ Has API PR   │
│ state: Start │  │ plan: Resume │  │ only: Create │
│ from Step 1  │  │ from Step 2+ │  │ release plan │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │                 │                 │
       └─────────────────┼─────────────────┘
                         │
                         ▼
              ┌───────────────────────┐
              │  STEP 1: Plan Release │
              │  [Release Plan Skill] │
              └───────────┬───────────┘
                          │
           ┌──────────────┴──────────────┐
           │                             │
           ▼                             ▼
┌─────────────────────┐      ┌─────────────────────┐
│ Create new release  │      │ Use existing        │
│ plan                │      │ release plan        │
└─────────┬───────────┘      └─────────┬───────────┘
          │                            │
          └──────────────┬─────────────┘
                         │
                         ▼
              ┌───────────────────────┐
              │ STEP 2: Author        │
              │ TypeSpec              │
              │ [TypeSpec Authoring]  │
              └───────────┬───────────┘
                          │
           ┌──────────────┴──────────────┐
           │                             │
           ▼                             ▼
┌─────────────────────┐      ┌─────────────────────┐
│ Create new TypeSpec │      │ Update existing     │
│ project             │      │ TypeSpec project    │
└─────────┬───────────┘      └─────────┬───────────┘
          │                            │
          └──────────────┬─────────────┘
                         │
                         ▼
              ┌───────────────────────┐
              │ STEP 3: Validate &    │
              │ Compile TypeSpec      │
              └───────────┬───────────┘
                          │
                          ▼
              ┌───────────────────────┐
              │ TypeSpec compiles?    │
              └───────────┬───────────┘
                          │
           ┌──────────────┴──────────────┐
           │ NO                          │ YES
           ▼                             ▼
┌─────────────────────┐      ┌─────────────────────┐
│ Report errors,      │      │ Extract API version │
│ Return to Step 2    │      │ & package names     │
└─────────────────────┘      └─────────┬───────────┘
                                       │
                                       ▼
                          ┌───────────────────────┐
                          │ STEP 4: Update        │
                          │ Release Plan          │
                          └───────────┬───────────┘
                                      │
                                      ▼
         ┌─────────────────────────────────────────────────────────────┐
         │ Update release plan with API version, package details       │
         │ and TypeSpec details. Changes release plan to in progress   │
         └─────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
                          ┌───────────────────────┐
                          │ STEP 5: Choose SDK    │
                          │ generation method     │
                          └───────────┬───────────┘
                                      │
                   ┌──────────────────┴──────────────────┐
                   │                                     │
                   ▼                                     ▼
        ┌─────────────────────┐              ┌─────────────────────┐
        │ LOCAL generation    │              │ PIPELINE generation │
        │ • Faster iteration  │              │ • Consistent builds │
        │ • Debug locally     │              │ • Auto-creates PRs  │
        └─────────┬───────────┘              └─────────┬───────────┘
                  │                                    │
                  ▼                                    │
        ┌─────────────────────┐                        │
        │ STEP 6: Create API  │                        │
        │ spec pull request   │◄───────────────────────┘
        └─────────┬───────────┘
                  │
                  ▼
        ┌─────────────────────┐
        │ Update release plan │
        │ with API spec PR    │
        └─────────┬───────────┘
                  │
                  ▼
        ┌─────────────────────┐
        │ STEP 7: Generate    │
        │ SDK                 │
        └─────────┬───────────┘
                  │
     ┌────────────┴────────────┐
     │                         │
     ▼                         ▼
┌──────────┐            ┌──────────────┐
│ STEP 7.1 │            │  STEP 7.2    │
│ LOCAL    │            │  PIPELINE    │
└────┬─────┘            └──────┬───────┘
     │                         │
     ▼                         ▼
┌─────────────────┐     ┌─────────────────┐
│ Generate SDK    │     │ Run pipeline    │
│ locally         │     │ generation      │
└────────┬────────┘     └────────┬────────┘
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌─────────────────┐
│ Test & validate │     │ Pipeline        │
│ package         │     │ successful?     │
└────────┬────────┘     └────────┬────────┘
         │                       │
         │              ┌────────┴────────┐
         │              │ NO              │ YES
         │              ▼                 ▼
         │     ┌─────────────────┐  ┌─────────────────┐
         │     │ Fallback to    │  │ PRs created     │
         │     │ local gen for  │  │ automatically   │
         │     │ troubleshooting│  └────────┬────────┘
         │     └────────┬───────┘           │
         │              │                   │
         ▼              ▼                   │
┌─────────────────┐                         │
│ Prepare package │                         │
│ for release     │                         │
│ [Package Skill] │                         │
└────────┬────────┘                         │
         │                                  │
         ▼                                  │
┌─────────────────┐                         │
│ Create SDK PRs  │                         │
│ & link to       │◄────────────────────────┘
│ release plan    │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│ STEP 7.3: Verify PR pipeline status     │
└─────────────────────┬───────────────────┘
                      │
         ┌────────────┴────────────┐
         │ Pipeline failures?      │
         ▼                         ▼
┌─────────────────┐        ┌─────────────────┐
│ YES: Analyze    │        │ NO: Proceed     │
│ pipeline        │        │ to Step 7       │
│ failures        │        │                 │
└────────┬────────┘        └────────┬────────┘
         │                          │
         ▼                          │
┌─────────────────┐                 │
│ Switch to local │                 │
│ SDK generation  │                 │
│ to resolve      │                 │
│ validation &    │                 │
│ test errors     │                 │
└────────┬────────┘                 │
         │                          │
         ▼                          │
┌─────────────────┐                 │
│ Update SDK PRs  │                 │
│ with fixes      │                 │
└────────┬────────┘                 │
         │                          │
         └──────────────┬───────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────┐
│ STEP 8: Check APIView feedback for the SDK pull requests │
└─────────────────────┬────────────────────────────────────┘
                      │
         ┌────────────┴────────────┐
         │ Has any API suggestions?│
         ▼                         ▼
┌─────────────────┐        ┌─────────────────┐
│ YES: Resolve    │        │ NO: Proceed     │
│ APIView         │        │ to release      │
│ suggestions     │        │                 │
│ [APIView Skill] │        │                 │
└────────┬────────┘        └────────┬────────┘
         │                          │
         ▼                          │
┌─────────────────┐                 │
│ Re-run Step 7   │                 │
│ SDK generation  │                 │
└────────┬────────┘                 │
         │                          │
         └──────────────┬───────────┘
                        │
                        ▼
         ┌─────────────────────────────────┐
         │ STEP 9: Release SDKs            │
         └─────────────────┬───────────────┘
                           │
                           ▼
         ┌─────────────────────────────────┐
         │ Wait for SDK PR approval        │
         │ & merge                         │
         └─────────────────┬───────────────┘
                           │
                           ▼
         ┌─────────────────────────────────┐
         │ Release packages                │
         └─────────────────┬───────────────┘
                           │
                           ▼
         ┌─────────────────────────────────┐
         │ Release plan auto-completed     │
         │ Service Tree KPI updated        │
         └─────────────────┬───────────────┘
                           │
                           ▼
                    ┌──────────────┐
                    │  WORKFLOW    │
                    │  COMPLETE    │
                    └──────────────┘
```

##### Step 1: Create a temporary release plan

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

**Status Updates**:

- TypeSpec validation: Completed

---

##### Step 4: Update Release Plan

**Skill Used**: [Prepare Release Plan Skill](#skill-6-prepare-release-plan)

**Actions**:

1. Validate package names:
   - Look for any existing completed release plans for the TypeSpec project
   - Retrieve package names from those completed release plans
   - Compare new package names with existing package names
   - If package names conflict (different from previously released packages):
     - **Warn user** about conflicting package names
     - Inform user they will need to get approval for the new package names
     - Inform user they will need to deprecate the old packages
     - **Note**: This is not recommended if the older package was already released as GA without approvals
2. Update release plan with TypeSpec details:
   - TypeSpec project path
   - API version
   - Package names per language
3. Change release plan status to "In Progress"

**Status Updates**:

- Package name validation: Completed (with warnings if conflicts detected)
- Release plan: Updated with API version, package details, and TypeSpec details
- Release plan status: In Progress

---

##### Step 5: Choose SDK Generation Method

**Decision Point**:

| Factor | Local Generation | Pipeline Generation |
|--------|------------------|---------------------|
| Tools setup | Require to install all required tool to build and compile the sdk | No need of language generation tools locally |
| Debugging | Easy to debug issues | Requires log analysis |
| PR Creation | Manual | Automatic |
| Best For | Iteration, troubleshooting | Production releases |

**Actions**:

1. Present options to user with pros/cons
2. Proceed to Step 6 (API spec PR) then Step 7 (SDK generation)

---

##### Step 6: Create API Spec Pull Request

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

##### Step 7: SDK Generation

###### Step 7.1: Local SDK Generation

**Skill Used**: [Generate SDK Locally Skill](#skill-4-generate-sdk-locally)

**Actions**:

1. Verify environment setup for target languages
2. Run `azsdk_package_generate_code` for each language
3. Build generated code
4. Run tests in playback mode
5. Validate samples
6. Run package validation checks
7. If any step fails:
   - Report errors with troubleshooting guidance
   - Allow user to fix and retry
8. Prepare package for release:
   - Update changelog
   - Update metadata
   - Update version

9. Create SDK pull requests for each language
10. Update release plan with SDK PR links

**Status Updates**:

- SDK generation: Completed per language
- Package preparation: Completed per language
- SDK PRs: Created and linked to release plan

###### Step 7.2: Pipeline SDK Generation



**Actions**:

1. Trigger SDK generation pipeline with release plan details using mcp tool `azsdk_run_generate_sdk`
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

##### Step 8: Resolve APIView Suggestions

**Skill Used**: [APIView Feedback Resolution Skill](#skill-8-apiview-feedback-resolution)

**Actions**:

1. Check APIView for comments on SDK packages using the API view revision created for the PR.
2. If no action items: Proceed to Step 9
3. If action items exist:
   - Display suggestions grouped by priority
   - Guide user through resolving each item
   - Apply TypeSpec customizations if needed
   - Regenerate SDK if changes were made
   - Update SDK PRs

**Status Updates**:

- APIView: Reviewed
- Feedback resolution: Completed

---

##### Step 9: Release SDK

**Actions**:

1. Monitor SDK PR approval status
2. When PRs are approved:
   - Guide user through merge process
3. After merge:
   - Release pipeline automatically triggers
   - Guide user to go to release pipeline run and approve the release of required package.
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
┌─────────────┐    ┌─────────────┐    ┌─────────────┐     ┌─────────────┐            ┌─────────────────────────────────────────────────────────────────┐
│   User      │    │ Create or   │    │  TypeSpec   │     │    SDK      │            │                    OPTIONAL                                     │
│   Intent    │───▶│ find temp   │───▶│  Changes    │───▶│ Generation  │───▶ ? ───▶│  Update Release Plan ──▶ API & SDK Readiness ──▶ Release SDK  │
│             │    │ release plan│    │             │     │  (Local)    │            │                                                                 │
└─────────────┘    └─────────────┘    └─────────────┘     └─────────────┘            └─────────────────────────────────────────────────────────────────┘
                                            │                   │                                              │
                                            ▼                   ▼                                              ▼
                                      ┌───────────┐       ┌───────────┐                                  ┌───────────┐
                                      │ Create or │       │ Generate  │                                  │ Transition│
                                      │ update    │       │ & test    │                                  │ to Goal 1 │
                                      │ TypeSpec  │       │ SDK       │                                  │ workflow  │
                                      │ locally   │       │ locally   │                                  │           │
                                      └───────────┘       └───────────┘                                  └───────────┘
```

#### Detailed Step-wise Instructions

```
                        ┌─────────────────────────┐
                        │  User wants to          │
                        │  experiment with        │
                        │  TypeSpec               │
                        └────────────┬────────────┘
                                     │
                                     ▼
                        ┌─────────────────────────┐
                        │ STEP 1: Create or find  │
                        │ temporary release plan  │
                        └────────────┬────────────┘
                                     │
           ┌─────────────────────────┴─────────────────────────┐
           │                                                   │
           ▼                                                   ▼
┌──────────────────────────┐               ┌──────────────────────────┐
│ Create new temporary     │               │ Find existing temporary │
│ release plan             │               │ release plan for project│
└────────────┬─────────────┘               └────────────┬─────────────┘
             │                                          │
             └────────────────────┬─────────────────────┘
                                     │
                                     ▼
                        ┌─────────────────────────┐
                        │ STEP 2: Author TypeSpec │
                        │ [TypeSpec Authoring]    │
                        └────────────┬────────────┘
                                     │
              ┌──────────────────────┴──────────────────────┐
              │                                             │
              ▼                                             ▼
┌──────────────────────────┐               ┌──────────────────────────┐
│ Create new TypeSpec      │               │ Modify existing          │
│ project                  │               │ TypeSpec project         │
└────────────┬─────────────┘               └────────────┬─────────────┘
             │                                          │
             └────────────────────┬─────────────────────┘
                                  │
                                  ▼
                     ┌─────────────────────────┐
                     │ STEP 3: Validate &      │
                     │ Compile TypeSpec        │
                     └────────────┬────────────┘
                                  │
                                  ▼
                     ┌─────────────────────────┐
                     │ Compilation successful? │
                     └────────────┬────────────┘
                                  │
                     ┌────────────┴────────────┐
                     │ NO                      │ YES
                     ▼                         ▼
          ┌─────────────────────┐  ┌─────────────────────┐
          │ Review errors,      │  │ View compiled       │
          │ iterate on TypeSpec │  │ output (OpenAPI)    │
          └──────────┬──────────┘  └──────────┬──────────┘
                     │                        │
                     └──────────┬─────────────┘
                                │
                                ▼
                   ┌─────────────────────────┐
                   │ Generate SDK from       │
                   │ TypeSpec?               │
                   └────────────┬────────────┘
                                │
              ┌─────────────────┴─────────────────┐
              │ NO                                │ YES
              ▼                                   ▼
   ┌───────────────────────┐         ┌───────────────────────┐
   │ Continue iterating    │         │ STEP 4.1: Verify      │
   │ or end experiment     │         │ Environment Setup     │
   └───────────────────────┘         └───────────┬───────────┘
                                                 │
                                                 ▼
                                    ┌───────────────────────┐
                                    │ All tools installed?  │
                                    └───────────┬───────────┘
                                                │
                               ┌────────────────┴────────────────┐
                               │ NO                              │ YES
                               ▼                                 ▼
                    ┌─────────────────────┐         ┌─────────────────────┐
                    │ Install missing     │         │ STEP 4.2: Generate  │
                    │ tools/dependencies  │         │ SDK Locally         │
                    └──────────┬──────────┘         └──────────┬──────────┘
                               │                               │
                               └───────────────┬───────────────┘
                                               │
                                               ▼
                                  ┌─────────────────────────┐
                                  │ Generate SDK code       │
                                  │ using local generation  │
                                  └───────────┬─────────────┘
                                              │
                                              ▼
                                  ┌─────────────────────────┐
                                  │ STEP 4.3: Run Package   │
                                  │ Checks & Validation     │
                                  └───────────┬─────────────┘
                                              │
                                              ▼
                                  ┌─────────────────────────┐
                                  │ • Build package         │
                                  │ • Run tests (playback)  │
                                  │ • Validate samples      │
                                  │ • Run linting           │
                                  └───────────┬─────────────┘
                                              │
                                              ▼
                                  ┌─────────────────────────┐
                                  │ All checks pass?        │
                                  └───────────┬─────────────┘
                                              │
                             ┌────────────────┴────────────────┐
                             │ NO                              │ YES
                             ▼                                 ▼
                  ┌─────────────────────┐         ┌─────────────────────┐
                  │ Review errors,      │         │ SDK ready for       │
                  │ fix and retry       │         │ local testing       │
                  └─────────────────────┘         └──────────┬──────────┘
                                                             │
                                                             ▼
                                                ┌─────────────────────────┐
                                                │ Want to publish spec    │
                                                │ and release SDK?        │
                                                └───────────┬─────────────┘
                                                            │
                                 ┌──────────────────────────┴──────────────────────────┐
                                 │ NO                                                  │ YES
                                 ▼                                                     ▼
                      ┌───────────────────────┐                          ┌───────────────────────┐
                      │ Experiment complete   │                          │ STEP 5: Transition    │
                      │ (local SDK only)      │                          │ to Release Workflow   │
                      └───────────────────────┘                          └───────────┬───────────┘
                                                                                     │
                                                                                     ▼
                                                                        ┌───────────────────────┐
                                                                        │ 5.1: Update Release   │
                                                                        │ Plan Work Item        │
                                                                        └───────────┬───────────┘
                                                                                    │
                                                                                    ▼
                                                                        ┌───────────────────────┐
                                                                        │ 5.2: Update Package   │
                                                                        │ Metadata & Changelog  │
                                                                        │ [Package Skill]       │
                                                                        └───────────┬───────────┘
                                                                                    │
                                                                                    ▼
                                                                        ┌───────────────────────┐
                                                                        │ 5.3: Create API Spec  │
                                                                        │ Pull Request          │
                                                                        └───────────┬───────────┘
                                                                                    │
                                                                                    ▼
                                                                        ┌───────────────────────┐
                                                                        │ Update release plan   │
                                                                        │ with API spec PR      │
                                                                        └───────────┬───────────┘
                                                                                    │
                                                                                    ▼
                                                                        ┌───────────────────────┐
                                                                        │ 5.4: Create SDK PRs   │
                                                                        │ & Link to Release     │
                                                                        │ Plan                  │
                                                                        └───────────┬───────────┘
                                                                                    │
                                                                                    ▼
                                                                        ┌───────────────────────┐
                                                                        │ 5.5: Follow Goal 1    │
                                                                        │ Steps 8-9 to release  │
                                                                        └───────────┬───────────┘
                                                                                    │
                                                                                    ▼
                                                                             ┌──────────────┐
                                                                             │   COMPLETE   │
                                                                             └──────────────┘
```

##### Step 1: Create or Find Temporary Release Plan

**Skill Used**: [Prepare Release Plan Skill](#skill-6-prepare-release-plan)

**Actions**:

1. Check if a temporary release plan already exists for the TypeSpec project
2. If no temporary release plan exists:
   - Create a new temporary release plan work item
   - Store release plan ID in local workflow state
3. If temporary release plan exists:
   - Retrieve and display release plan details
   - Continue with existing temporary plan

**Status Updates**:

- Temporary release plan: Created/Found
- Local state: Release plan ID stored

---

##### Step 2: Create or update TypeSpec

**Skill Used**: [TypeSpec Authoring Skill](#skill-2-typespec-authoring-and-validation)

**Actions**:

1. Create new TypeSpec project or modify existing one
2. Iterate freely without release constraints
3. No release plan required at this stage

---

##### Step 3: Validate and Compile TypeSpec

**Actions**:

1. Compile TypeSpec
2. Fix any errors
3. View generated OpenAPI output
4. Iterate as needed

---

##### Step 4: Generate SDK (Optional)

If user wants to see generated SDK:

###### Step 4.1: Verify Environment Setup

**Actions**:

1. Check if required tools are installed for target language(s)
2. Install missing tools/dependencies if needed

###### Step 4.2: Generate SDK Locally

**Actions**:

1. Run local SDK generation
2. Build generated code

###### Step 4.3: Run Package Checks & Validation

**Actions**:

1. Build package
2. Run tests (playback mode)
3. Validate samples
4. Run linting
5. Review and fix any errors

---

##### Step 5: Publish (Optional - Transition to Release Workflow)

If user decides to publish the spec and release SDK:

###### Step 5.1: Update Release Plan

**Actions**:

1. Update temporary release plan to actual release plan
2. Update with TypeSpec project details, API version, and package names

###### Step 5.2: Update SDK Package Metadata

**Skill Used**: [Package Release Readiness Skill](#skill-5-package-release-readiness)

**Actions**:

1. Update changelog
2. Update package metadata

###### Step 5.3: Create API Spec Pull Request

**Actions**:

1. Create PR in azure-rest-api-specs
2. Update release plan with PR link

###### Step 5.4: Create SDK PRs & Link to Release Plan

**Actions**:

1. Create SDK pull requests
2. Link PRs to release plan

###### Step 5.5: Release SDKs

**Actions**:

1. Follow Goal 1 Steps 8-9 (APIView resolution and release)

---

## Resumable Workflow Scenarios

The workflow is designed to support users who have already completed some steps manually or encountered failures. The agent detects the current state and continues from the appropriate point.

### Scenario A: Release Plan and API Spec PR Already Exist

**Detection**:

- User mentions existing release plan work item ID or PR link
- Agent queries release plan to find linked API spec PR

**Resume Point**: Continue from Step 7 (SDK Generation)

**Actions**:

1. Retrieve release plan details
2. Verify API spec PR is approved/merged
3. Proceed with SDK generation (local or pipeline)
4. Continue through Steps 8-9

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
│ Resume at Step 7:         │
│ SDK Generation            │
└───────────────────────────┘
```

---

### Scenario B: API Spec PR Exists Without Release Plan

**Detection**:

- User provides API spec PR link
- No release plan found for this PR

**Resume Point**: Create release plan, then continue from Step 7

**Actions**:

1. Create release plan work item and mark TypeSpec authoring was already completed.
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
│ Create release plan,      │
│ link API spec PR and      │
| mark TypeSpec authoring   |
| was already completed.    |
└─────────────┬─────────────┘
              │
              ▼
┌───────────────────────────┐
│ Resume at Step 7:         │
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
- `azsdk_run_typespec_validation`

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
**Skill reference**: TypeSpec customization should refer `TypeSpec Authoring and Validation` skill to validate the TypeSpec
---

### Skill 4: Generate SDK Locally

**Purpose**: Run SDK code generation on local machine for rapid iteration and troubleshooting.

**Capabilities**:

- Verify environment setup for target languages
- Generate SDK code from TypeSpec
- Build generated code
- Troubleshoot generation failures
- Run package tests
- Run package validation
- Generate readme and samples

**Related Tools**:

- `azsdk_package_generate_code`
- `azsdk_verify_setup`
- `azsdk_package_build_code`
- `azsdk_package_run_check`
- `azsdk_package_run_tests`
- `azsdk_package_generate_samples`

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
- `azsdk_update_api_spec_pull_request_in_release_plan`
- `azsdk_link_sdk_pull_request_to_release_plan`
- `azsdk_get_release_plan`

---

### Skill 7: Experiment TypeSpec and SDK

**Purpose**: Allow users to explore TypeSpec and SDK generation without committing to a release.

**Capabilities**:

- Create a temporary release plan
- Create or update TypeSpec projects
- Iterate quickly on TypeSpec changes
- Generate SDKs locally for testing
- Transition to full release workflow when ready

**Uses Skills**: 2, 4, 5

---

### Skill 8: APIView Feedback Resolution

**Purpose**: Review and resolve feedback from APIView reviews.

**Capabilities**:

- Retrieve APIView comments for SDK packages
- Use APIView feedback resolver mcp tool
- Trigger SDK regeneration after TypeSpec changes

**Related Tools**:

- `azsdk_apiview_get_comments`
- `azsdk_typespec_delegate_apiview_feedback`

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

- `azsdk_analyze_pipeline`


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
5. Inform users that SDK must be generated and released for JS and Go (for management plane) and also for JS (for data plane). Management plane must release .NET, JS, Java, Python and Go packages and Dataplane must release .NET, JS, Java and Python.
6. Create and link SDK PRs

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

1. Create a temporary release plan
2. Load existing TypeSpec project
3. Guide through modifications
4. Compile and show differences
5. Generate SDK locally to verify changes
6. Ask if user wants to proceed to release. Convert to actual release plan and proceed if user wants to proceed to release spec and SDK.

---

**Prompt:**

```text
I've been experimenting with TypeSpec and now I'm ready to publish and release.
```

**Expected Agent Activity:**

1. Transition to Goal 1 workflow
2. Update a temporary release plan to actual release plan or create a new release plan if temporary one is missing.
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
5. Regenerate SDK, re-validate SDK, and update PR

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
  - Check if API View are approved. If so, move to release. If not move to below step.
  - Check if API View's have feedback from architects, if so help user resolve feedback.
  - Check if API View's have actively requested reviews from architects, if not tell user to request reviews on their API View. If they do have active requests to review, reach out to the architects or something.

---

## Further enhancements(Out of scope in the initial workflow)

### Breaking change validation and guidance

Agent will have a breaking change detection validation and warn users about the breaking changes. It will guide users to get breaking change approval. 

## Exceptions and Limitations

### Exception 1: Releases Requiring Architect Review

**Description:**
First GA releases require architect board review, which involves human decision-making outside the automated workflow. Preview releases do not require architect approval but a service team can still request a review.

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
