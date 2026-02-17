# Spec: TypeSpec to SDK Release Workflow - End-to-End Orchestration

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals](#goals)
- [Design Proposal](#design-proposal)
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

- **<a id="temporary-release-plan"></a>Temporary Release Plan**: A release plan in draft/temporary state used during TypeSpec experimentation and local SDK generation. The temporary release plan is converted to an actual release plan when the user decides to create an API spec pull request and proceed with the release workflow. This allows users to iterate on TypeSpec and SDK generation without committing to a full release.

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
8. Support all five tier-1 SDK languages: .NET, Java, JavaScript, Python, Go
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

### Unified TypeSpec to SDK Workflow

This workflow supports both experimentation and production SDK releases through a unified flow. Users start with a temporary release plan that can be converted to an actual release plan when ready to proceed with the full release.

#### Process Flow

```
┌───────────────────┐    ┌───────────────────┐    ┌───────────────────┐    ┌────────────────────────────────────────────────────────────────────────────┐
│    User Intent    │    │    Temporary      │    │    TypeSpec       │    │                          Choose Path                                       │
│                   │───▶│   Release Plan    │───▶│    Readiness     │───▶│                                                                           │
│ ┌───────────────┐ │    │ ┌───────────────┐ │    │ ┌───────────────┐ │    │  ┌─────────────────────┐              ┌─────────────────────────────────┐  │
│ │ • Identify    │ │    │ │ • Create or   │ │    │ │ • Author      │ │    │  │   EXPERIMENTATION   │              │      RELEASE WORKFLOW           │  │
│ │   release vs  │ │    │ │   find temp   │ │    │ │   TypeSpec    │ │    │  │                     │              │                                 │  │
│ │   experiment  │ │    │ │   release     │ │    │ │ • Compile &   │ │    │  │ ┌─────────────────┐ │     ┌─ ─────▶│ ┌─────────────────────────────┐ │ │
│ │ • Gather      │ │    │ │   plan        │ │    │ │   validate    │ │    │  │ │ Generate SDK    │ │     │        │ │ Create API Spec PR          │ │  │
│ │   service     │ │    │ │ • Store plan  │ │    │ │ • Extract API │ │    │  │ │ locally (no PR) │ │     │        │ │ (converts temp to actual    │ │  │
│ │   details     │ │    │ │   ID locally  │ │    │ │   version &   │ │    │  │ │ • Build & test  │ │     │        │ │  release plan)              │ │  │
│ │               │ │    │ │               │ │    │ │   pkg names   │ │    │  │ │ • Run checks    │ │     │        │ │ • Update release plan       │ │  │
│ └───────────────┘ │    │ └───────────────┘ │    │ └───────────────┘ │    │  │ └────────┬────────┘ │     │        │ │ • Link PR to plan           │ │  │
└───────────────────┘    └───────────────────┘    └───────────────────┘    │  │          │          │     │        │ └─────────────┬───────────────┘ │  │
                                                                           │  │          ▼          │     │        │               │                 │  │
                                                                           │  │ ┌─────────────────┐ │     │        │               ▼                 │  │
                                                                           │  │ │ Transition to   │─┼─────┘        │ ┌─────────────────────────────┐ │  │
                                                                           │  │ │ publish changes │ │              │ │ SDK Generation              │ │  │
                                                                           │  │ │ and release     │ │              │ │ (skip if already local gen) │ │  │
                                                                           │  │ └─────────────────┘ │              │ │ • Local or pipeline         │ │  │
                                                                           │  └─────────────────────┘              │ └─────────────┬───────────────┘ │  │
                                                                           │                                       │               │                 │  │
                                                                           │                                       │               ▼                 │  │
                                                                           │                                       │ ┌─────────────────────────────┐ │  │
                                                                           │                                       │ │ SDK PRs + APIView + Release │ │  │
                                                                           │                                       │ │ • Create & link SDK PRs     │ │  │
                                                                           │                                       │ │ • Resolve APIView feedback  │ │  │
                                                                           │                                       │ │ • Merge & publish packages  │ │  │
                                                                           │                                       │ └─────────────────────────────┘ │  │
                                                                           │                                       └─────────────────────────────────┘  │
                                                                           └────────────────────────────────────────────────────────────────────────────┘
```

#### Key Workflow Concepts

1. **Temporary Release Plan**: Created at workflow start for tracking purposes. Remains temporary during experimentation.
2. **TypeSpec Readiness**: Author and validate TypeSpec regardless of whether releasing or experimenting.
3. **Path Selection**: After TypeSpec is ready, choose to experiment (generate SDK locally) or proceed to release (create API spec PR).
4. **Conversion Point**: The temporary release plan converts to an actual release plan when an API spec PR is created.
5. **Skip SDK Generation**: If SDK was already generated locally during experimentation, skip regeneration when transitioning to release.
6. **SDK PR Linking**: SDK pull requests must be linked to the release plan when created.

#### Detailed Step-wise Instructions

```
                                    ┌─────────────────┐
                                    │  User provides  │
                                    │  initial prompt │
                                    └────────┬────────┘
                                             │
                                             ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 1: Create or find temporary release plan    │
              │  [Release Plan Skill]                             │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │                                                     │
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Create new temporary    │                     │ Find existing temporary │
│ release plan            │                     │ or actual release plan  │
└───────────┬─────────────┘                     └───────────┬─────────────┘
            │                                               │
            └───────────────────────┬───────────────────────┘
                                    │
                                    ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 2: TypeSpec Readiness                      │
              │  [TypeSpec Authoring & Validation Skill]         │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │                                                     │
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Create new TypeSpec     │                     │ Update existing         │
│ project                 │                     │ TypeSpec project        │
└───────────┬─────────────┘                     └───────────┬─────────────┘
            │                                               │
            └───────────────────────┬───────────────────────┘
                                    │
                                    ▼
              ┌───────────────────────────────────────────────────┐
              │  Validate & Compile TypeSpec                      │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │ NO (errors)                                         │ YES (success)
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Report errors,          │                     │ TypeSpec Ready!         │
│ iterate on TypeSpec     │                     │ Extract API version &   │
└─────────────────────────┘                     │ package names           │
                                                └───────────┬─────────────┘
                                                            │
                                                            ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 3: Choose Path                             │
              │  Experimentation OR Release?                     │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │                                                     │
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ PATH A: EXPERIMENTATION │                     │ PATH B: RELEASE         │
│ Generate SDK locally    │                     │ Create API Spec PR      │
│ (no PR creation)        │                     │ (converts temp to       │
│                         │                     │  actual release plan)   │
└───────────┬─────────────┘                     └───────────┬─────────────┘
            │                                               │
            ▼                                               │
┌─────────────────────────┐                                 │
│ STEP 3A: Local SDK      │                                 │
│ Generation              │                                 │
├─────────────────────────┤                                 │
│ • Verify environment    │                                 │
│ • Generate SDK locally  │                                 │
│ • Build SDK             │                                 │
│ • Customize TypeSpec &  │                                 │
│   regenerate if errors  │                                 │
│ • Test & run validation │                                 │
└───────────┬─────────────┘                                 │
            │                                               │
            ▼                                               │
┌─────────────────────────┐                                 │
│ Continue experimenting  │                                 │
│ OR transition to        │────────────────────────────────▶│
│ release workflow?       │                                 │
└───────────┬─────────────┘                                 │
            │ (stay experimenting)                          │
            ▼                                               │
┌─────────────────────────┐                                 │
│ Iterate: modify         │                                 │
│ TypeSpec, regenerate    │                                 │
│ SDK, test locally       │                                 │
└─────────────────────────┘                                 │
                                                            │
                                                            ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 4: Update Release Plan & Create API Spec PR │
              │  (Converts temporary to actual release plan)      │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │ • Update release plan with API version & details │
              │ • Convert temporary release plan to actual       │
              │ • Create API spec PR in azure-rest-api-specs     │
              │ • Link API spec PR to release plan               │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 5: SDK Generation                          │
              │  (Skip if already generated locally)             │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │                                                     │
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ SDK already generated   │                     │ Need to generate SDK    │
│ locally?                │                     │                         │
└───────────┬─────────────┘                     └───────────┬─────────────┘
            │                                               │ 
            │                                               │
            │                        ┌──────────────────────┴──────────────────────┐
            │                        │                                             │
            │                        ▼                                             ▼
            │           ┌─────────────────────────┐             ┌─────────────────────────┐
            │           │ LOCAL generation        │             │ PIPELINE generation     │
            │           │ • Faster iteration      │             │ • Consistent builds     │
            │           │ • Debug locally         │             │ • Auto-creates PRs      │
            │           └───────────┬─────────────┘             └───────────┬─────────────┘
            │                       │                                       │
            │                       ▼                                       ▼
            │           ┌─────────────────────────┐             ┌─────────────────────────┐
            │           │ Generate SDK locally    │             │ Run pipeline generation │
            │           │ Build & test package    │             └───────────┬─────────────┘
            │           └───────────┬─────────────┘                         │
            │                       │                           ┌───────────┴───────────┐
            │                       │                           │ PASS                  │ FAIL
            │                       │                           ▼                       ▼
            │                       │             ┌─────────────────────┐  ┌─────────────────────┐
            │                       │             │ PRs created         │  │ Fallback to local   │
            │                       │             │ automatically       │  │ gen for debug       │
            │                       │             └──────────┬──────────┘  └───────────┬─────────┘
            │                       │                        │                         │
            │                       ▼                        │                         │
            │           ┌─────────────────────────┐          │                         │
            │           │ Prepare package for     │          │                         │
            │           │ release                 │◀─────────┴─────────────────────────┘
            │           │ [Package Skill]         │
            │           └───────────┬─────────────┘
            │                       │
            └───────────────────────┼───────────────────────────────────────────────────┐
                                    │                                                   │
                                    ▼                                                   │
              ┌───────────────────────────────────────────────────┐                     │
              │  STEP 6: Create SDK PRs & Link to Release Plan    │◀────────────────────┘
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │ • Link SDK PRs to release plan                    │
              │ • Verify PR pipeline status                       │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 7: Check APIView Feedback                   │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │ Has API suggestions?                                │ No suggestions
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Resolve APIView         │                     │ Proceed to release      │
│ suggestions             │                     │                         │
│ [APIView Skill]         │                     │                         │
└───────────┬─────────────┘                     └───────────┬─────────────┘
            │                                               │
            ▼                                               │
┌─────────────────────────┐                                 │
│ Re-run SDK generation   │                                 │
│ if changes required     │                                 │
└───────────┬─────────────┘                                 │
            │                                               │
            └───────────────────────┬───────────────────────┘
                                    │
                                    ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 8: Release SDKs                            │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │ • Wait for SDK PR approval & merge               │
              │ • Release pipeline triggers                      │
              │ • Approve release in pipeline                    │
              │ • Packages published to registries               │
              │ • Release plan auto-completes                    │
              │ • Service Tree KPI updated                       │
              └───────────────────────────────────────────────────┘
```

##### Step 1: Create or Find Temporary Release Plan

**Skill Used**: [Prepare Release Plan Skill](#skill-6-prepare-release-plan)

**Actions**:

1. Check if a temporary release plan already exists for the TypeSpec project
2. If no release plan exists:
   - Prompt user for required information (TypeSpec project path or service tree ID and product tree ID)
   - Create new **temporary** release plan work item in Azure DevOps
3. If release plan exists:
   - Retrieve and display release plan details
   - Determine if it's a temporary or actual release plan
4. Update local workflow state with release plan ID

**Status Updates**:

- Temporary release plan: Created/Identified
- Local state: Release plan ID stored

---

##### Step 2: TypeSpec Readiness (Define or Update TypeSpec)

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
6. Run TypeSpec compilation and validation
7. If compilation fails:
   - Report errors with guidance
   - Iterate on TypeSpec until successful
8. If compilation succeeds:
   - Extract API version from TypeSpec
   - Extract package names for each language

**Status Updates**:

- TypeSpec authoring: In Progress → Completed
- TypeSpec validation: Completed
- Local state: TypeSpec project path stored

---

##### Step 3: Choose Path (Experimentation OR Release)

After TypeSpec is ready, user chooses one of two paths:

| Path | Description | When to Use |
|------|-------------|-------------|
| **Path A: Experimentation** | Generate SDK locally, iterate quickly | Testing API design, exploring SDK shape |
| **Path B: Release** | Create API spec PR, follow release process | Ready to publish spec and release SDK |

---

###### Step 3A: Local SDK Generation (Experimentation Path)

**Skill Used**: [Generate SDK Locally Skill](#skill-4-generate-sdk-locally), [TypeSpec Customization Skill](#skill-3-typespec-customization)

**Purpose**: Generate and test SDK locally without creating PRs.

**Actions**:

1. Verify environment setup for target languages
2. Run local SDK generation (`azsdk_package_generate_code`)
3. Build generated code
4. If build fails or SDK customization is needed:
   - Apply TypeSpec customizations using [TypeSpec Customization Skill](#skill-3-typespec-customization)
   - Regenerate SDK and rebuild
5. Run tests
6. Validate and run linting

**At this point, user can either:**

- Continue iterating (modify TypeSpec, customize, regenerate SDK locally)
- Transition to release workflow (proceed to Step 4)

**Status Updates**:

- Local SDK generation: Completed for selected languages
- Build/Test: Validated locally

---

##### Step 4: Update Release Plan & Create API Spec PR

**Skill Used**: [Prepare Release Plan Skill](#skill-6-prepare-release-plan)

**Purpose**: Convert temporary release plan to actual and create API spec PR.

**Actions**:

1. Prompt user for additional release information (target release month, release type)
2. **Convert temporary release plan to actual release plan**
3. Validate package names:
   - Look for any existing completed release plans for the TypeSpec project
   - Retrieve package names from those completed release plans
   - Compare new package names with existing package names
   - If package names conflict (different from previously released packages):
     - **Warn user** about conflicting package names
     - Inform user they will need to get approval for the new package names
     - Inform user they will need to deprecate the old packages
4. Update release plan with TypeSpec details:
   - TypeSpec project path
   - API version
   - Package names per language
5. Change release plan status to "In Progress"
6. Stage TypeSpec changes in local git repository
7. Create branch and commit
8. Push to remote and create PR in azure-rest-api-specs
9. Update release plan with API spec PR link

**Status Updates**:

- Release plan: Converted to actual, updated with API version and package details
- Release plan status: In Progress
- API spec PR: Created and linked to release plan

---

##### Step 5: SDK Generation

**Purpose**: Generate SDK packages. **Skip if already generated locally during experimentation (Step 3A)**.

**Decision Point**:

| Factor | Local Generation | Pipeline Generation |
|--------|------------------|---------------------|
| Tools setup | Requires all language tools installed locally | No local tools needed |
| Debugging | Easy to debug issues | Requires log analysis |
| PR Creation | Manual (Step 6) | Automatic |
| Best For | Iteration, troubleshooting | Production releases |

###### Option A: Use Existing Local SDK (Skip Generation)

If SDK was already generated locally during Step 3A:

1. Confirm local SDK is still valid and up-to-date
2. Proceed directly to Step 6 (Create SDK PRs)

###### Option B: Local SDK Generation

**Skill Used**: [Generate SDK Locally Skill](#skill-4-generate-sdk-locally), [TypeSpec Customization Skill](#skill-3-typespec-customization)

**Actions**:

1. Verify environment setup for target languages
2. Run `azsdk_package_generate_code` for each language
3. Build generated code
4. If build fails:
   - Apply TypeSpec customizations using [TypeSpec Customization Skill](#skill-3-typespec-customization)
   - Regenerate SDK and rebuild
5. Run tests
6. Validate and run linting
7. If any step fails:
   - Report errors with troubleshooting guidance
   - Allow user to fix and retry
8. Prepare package for release:
   - Update changelog
   - Update metadata
   - Update version

**Status Updates**:

- SDK generation: Completed per language
- Package preparation: Completed per language

###### Option C: Pipeline SDK Generation

**Actions**:

1. Trigger SDK generation pipeline with release plan details using `azsdk_run_generate_sdk`
2. Monitor pipeline status for each language
3. If pipeline succeeds:
   - PRs are automatically created
   - Update release plan with PR links
   - Skip to Step 7 (APIView feedback)
4. If pipeline fails for any language:
   - Report failure details
   - Offer to switch to local generation for troubleshooting

**Status Updates**:

- Pipeline: Running → Succeeded/Failed
- SDK PRs: Auto-linked to release plan (if pipeline generation)

---

##### Step 6: Create SDK PRs & Link to Release Plan

**Purpose**: Create SDK pull requests and link them to the release plan.

**Actions**:

1. Create SDK pull requests for each target language if not already generated.
2. **Link SDK PRs to release plan**
3. Verify PR pipeline status. Fix TypeSpec and rerun SDK generation if there are any build errors.

**Note**: If pipeline generation (Step 5 Option C) was used, this step is automatic.

**Status Updates**:

- SDK PRs: Created and linked to release plan

---

##### Step 7: Check APIView Feedback

**Skill Used**: [APIView Feedback Resolution Skill](#skill-8-apiview-feedback-resolution)

**Actions**:

1. Check APIView for comments on SDK packages using the API view revision created for the PR
2. If no action items: Proceed to Step 8
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

##### Step 8: Release SDK

**Actions**:

1. Monitor SDK PR approval status
2. When PRs are approved:
   - Guide user through merge process
3. After merge:
   - Release pipeline automatically triggers
   - Guide user to go to release pipeline run and approve the release of required package
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

## Resumable Workflow Scenarios

The workflow is designed to support users who have already completed some steps manually or encountered failures. The agent detects the current state and continues from the appropriate point.

### Scenario A: Release Plan and API Spec PR Already Exist

**Detection**:

- User mentions existing release plan work item ID or PR link
- Agent queries release plan to find linked API spec PR

**Resume Point**: Continue from Step 5 (SDK Generation)

**Actions**:

1. Retrieve release plan details
2. Verify API spec PR is approved/merged
3. Proceed with SDK generation (local or pipeline)
4. Continue through Steps 6-8

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
│ Resume at Step 5:         │
│ SDK Generation            │
└───────────────────────────┘
```

---

### Scenario B: API Spec PR Exists Without Release Plan

**Detection**:

- User provides API spec PR link
- No release plan found for this PR

**Resume Point**: Create release plan, then continue from Step 5

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
│ Resume at Step 5:         │
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

1. Identify the release plan
1. Identify failed language(s)
1. Switch to local SDK generation for debugging
1. Guide user through error resolution
1. If TypeSpec customization needed, invoke [TypeSpec Customization Skill](#skill-3-typespec-customization)
1. Regenerate SDK locally
1. Create SDK PR manually and link to release plan

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

1. Identify the release plan using the TypeSpec project of the package.
1. Identify which package preparation steps are needed
1. Update changelog content
1. Update package metadata
1. Run validation checks
1. Create or update SDK PR

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

**Spec Reference**: [customizing-client-tsp.spec.md](3-customizing-client-tsp.spec.md)
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

### Workflow Prompts

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

#### Experimentation Scenarios

**Prompt:**

```text
I need to create a new TypeSpec project and see what the generated SDK looks like.
```

**Expected Agent Activity:**

1. Create temporary release plan
2. Initialize TypeSpec project
3. Guide through TypeSpec authoring
4. Compile and validate TypeSpec
5. Ask if user wants to generate SDK locally
6. If yes, run local SDK generation
7. User can continue iterating or proceed to release workflow

---

**Prompt:**

```text
I want to experiment with changing my TypeSpec and checking the generated SDK.
```

**Expected Agent Activity:**

1. Create or find temporary release plan
2. Load existing TypeSpec project
3. Guide through modifications
4. Compile and show differences
5. Generate SDK locally to verify changes
6. Ask if user wants to proceed to release (convert temporary to actual release plan)

---

**Prompt:**

```text
I've been experimenting with TypeSpec and now I'm ready to publish and release.
```

**Expected Agent Activity:**

1. Convert temporary release plan to actual release plan (or create new if missing)
2. Update package metadata and changelog
3. Create API spec PR
4. Continue with full release workflow (SDK generation, PRs, release)

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
- Works for all tier-1 SDK languages (.NET, Java, JavaScript, Python, Go)
- Clear guidance provided at each decision point
- Errors and failures are handled gracefully with troubleshooting guidance

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
