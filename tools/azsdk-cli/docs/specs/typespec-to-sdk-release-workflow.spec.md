# Spec: TypeSpec to SDK Release Workflow - End-to-End Orchestration

## Table of Contents

- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals](#goals)
- [Workflow Design](#workflow-design)
  - [Workflow Stages](#workflow-stages)
  - [Architecture: Skill Chaining with Next Steps](#architecture-skill-chaining-with-next-steps)
- [Known Gaps](#known-gaps)
- [Success Criteria](#success-criteria)
- [Exceptions and Limitations](#exceptions-and-limitations)
- [Open Questions](#open-questions)

---

## Definitions

- **TypeSpec**: Language for describing cloud service APIs. See [typespec.io](https://typespec.io).
- **SDK**: Client libraries generated from TypeSpec for .NET, Java, JavaScript, Python, Go.
- **Release Plan**: Azure DevOps work item tracking end-to-end SDK release across languages. Managed by azsdk-cli tooling.
- **API Spec PR**: Pull request in `azure-rest-api-specs` containing TypeSpec changes.
- **SDK PR**: Pull request in a language SDK repo (e.g., `azure-sdk-for-python`) with generated code.
- **APIView**: Web tool for reviewing SDK public API surface.
- **tspconfig.yaml**: Configuration in TypeSpec project specifying emitter settings per language.
- **tsp-location.yaml**: Configuration in SDK repos pointing to the source TypeSpec project.
- **`@azure-tools/typespec-breaking-change`**: TypeSpec-native breaking change detector. Phase A: same-version regression (any diff = error). Phase B: cross-version evolution (request narrowing / response widening = breaking).
- **Suppression Decorators**: `@approvedBreakingChange` (Phase B) and `@approvedUnversionedChange` (Phase A) — inline approval with auto-labeling.
- **TypeSpec Customizations**: SDK-specific customizations in `client.tsp` (renaming, convenience methods).
- **Code Customizations**: Hand-written SDK code that must be preserved across regeneration.

---

## Background / Problem Statement

### Current State

Azure service teams face a complex, multi-step process to release SDKs from TypeSpec specifications:

1. **Fragmented tooling**: Different tools for TypeSpec authoring, spec validation, breaking change detection, SDK generation, and release management — developed independently with limited integration.
2. **Manual coordination**: Teams manually track progress across multiple repositories (`azure-rest-api-specs`, `azure-sdk-for-*`) and systems (Azure DevOps, GitHub, Service Tree).
3. **Silent failures**: SDK generation failures are not surfaced to users — tools fail without actionable guidance.
4. **Scattered documentation**: Process is documented across EngHub, Wiki, and repo specs with no single authoritative source.
5. **No unified CI chain**: Spec PR validation (compile → lint → breaking change → generate → build) lacks a designed end-to-end failure-handling strategy.

### What Has Changed

- The standalone "release planner" system has been **fully removed**. Release tracking is now handled directly through Azure DevOps work items managed by azsdk-cli.
- `@azure-tools/typespec-breaking-change` provides TypeSpec-native breaking change detection (replacing OpenAPI-based tools for TypeSpec specs). Crystal's breaking change detection spec defines the CI integration approach.

### Why This Matters

- **Time from API definition to SDK release** is measured in weeks for experienced teams, longer for new ones.
- **No agent-assisted end-to-end workflow** exists today — each stage requires manual tool invocation and knowledge of the next step.

---

## Goals

1. Provide an end-to-end guided workflow from TypeSpec authoring to SDK release
2. Enable resuming the workflow from any intermediate state
3. Automatically track release plan status throughout the workflow
4. Surface errors with actionable guidance at every failure point
5. Integrate sub-skills seamlessly for specialized tasks
6. Support all tier-1 SDK languages with appropriate scope:

| Plane | Required Languages |
|-------|-------------------|
| **Management plane** | .NET, Java, JavaScript, Python, Go |
| **Data plane** | .NET, Java, JavaScript, Python |

---

## Workflow Design

### Overview

The workflow is an orchestration layer that:
1. **Identifies user intent** and gathers service details
2. **Assesses current state** (existing release plans, PRs, completed steps)
3. **Orchestrates sub-skills** in correct sequence
4. **Tracks progress** via release plan work items
5. **Handles failures** with troubleshooting guidance

### Workflow Stages

The end-to-end flow consists of 5 stages (10 steps). Gaps are annotated inline.

```
                         ┌─────────────────────────────────┐
                         │  ENTRY POINTS                   │
                         ├─────────────────────────────────┤
                         │ A) User provides initial prompt │
                         │ B) Spec PR merge triggers       │
                         │    auto release plan creation   │
                         │    (two-stage pipeline Stage 1) │
                         └──────────────┬──────────────────┘
                                        │
                                        ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 0: Create or Find Release Plan             │
              │  [Release Plan Skill]                            │
              │  (Tracks progress across all stages)             │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │                                                     │
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Create new release plan │                     │ Find existing release   │
│                         │                     │ plan (resume workflow)  │
└───────────┬─────────────┘                     └───────────┬─────────────┘
            │                                               │
            └───────────────────────┬───────────────────────┘
                                    │
                                    ▼
╔═══════════════════════════════════════════════════════════════════╗
║  STAGE 1: TypeSpec Authoring (local)                             ║
╚═══════════════════════════════════════════════════════════════════╝
                                    │
              ┌───────────────────────────────────────────────────┐
              │  STEP 1: Author TypeSpec                          │
              │  [TypeSpec Authoring Skill]                       │
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
              │  STEP 2: Validate & Compile TypeSpec              │
              │  • TypeSpec compiler                              │
              │  • Linter checks                                 │
              │  • Breaking change detection (Phase A + B)       │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │ FAIL (errors)                                       │ PASS (success)
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Report errors,          │                     │ TypeSpec Ready!         │
│ iterate on TypeSpec     │◀────────────────────│ Extract API version &   │
│ (loop until passing)    │    (if user wants   │ package names           │
└─────────────────────────┘     to fix)         └───────────┬─────────────┘
                                                            │
  Gap: Breaking change findings require manual fix.         ▼
  No agent auto-resolves suppression decorators.  ┌─────────────────────────┐
                                                  │ Namespace Approval      │
                                                  │ (new packages only)     │
                                                  │ • Check if pkg name     │
                                                  │   already approved      │
                                                  │ • Request approval if   │
                                                  │   new namespace         │
                                                  └───────────┬─────────────┘
                                                            │
                                                            ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 3: Open API Spec PR                        │
              │  (manual — developer opens PR)                   │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      │  Gap: No agent-driven
                                      │  transition from local
                                      │  authoring to PR creation.
                                      │
                                      ▼
╔═══════════════════════════════════════════════════════════════════╗
║  STAGE 2: Spec PR Validation (CI)                                ║
╚═══════════════════════════════════════════════════════════════════╝
                                    │
              ┌───────────────────────────────────────────────────┐
              │  STEP 4: CI Validation Pipeline                  │
              │  (auto-triggers on PR open/update)               │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │ • TypeSpec compilation                            │
              │ • LintDiff                                        │
              │ • Breaking change detection (Phase A + B)         │
              │ • APIView token generation                        │
              │ • SDK generation dry-run (spec-gen-sdk)           │
              │ • Labels applied                                  │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │ FAIL                                                │ PASS
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Fix issues, push to PR  │                     │ All checks pass         │
│ (re-triggers pipeline)  │                     │ PR approved & merged    │
└─────────────────────────┘                     └───────────┬─────────────┘
                                                            │
  Gap: Validation steps run independently.                  │
  No unified PR comment with all results + next steps.      │
  No designed failure ordering.                             │
                                                            │
                                                            ▼
╔═══════════════════════════════════════════════════════════════════╗
║  STAGE 3: SDK Generation (per language)                          ║
╚═══════════════════════════════════════════════════════════════════╝
                                    │
              ┌───────────────────────────────────────────────────┐
              │  STEP 5: Generate SDKs                           │
              │  Trigger: spec PR merge → spec-gen-sdk auto-runs │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │                                                     │
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ PIPELINE generation     │                     │ LOCAL generation        │
│ (auto on spec merge)    │                     │ (developer choice)      │
├─────────────────────────┤                     ├─────────────────────────┤
│ • Two-stage pipeline:   │                     │ • azsdk_package_        │
│   1) Release plan create│                     │   generate_code         │
│   2) SDK gen per lang   │                     │ • Build & test locally  │
│ • Auto-creates SDK PRs  │                     │ • Create PR manually    │
│ • Trigger: spec merge   │                     └───────────┬─────────────┘
│   OR manual invocation  │                                 │
└───────────┬─────────────┘                                 │
            │                                               │
 ┌──────────┴──────────┐                                    │
 │ PASS                │ FAIL                               │
 ▼                     ▼                                    │
┌────────────────┐  ┌─────────────────────┐                 │
│ PRs created &  │  │ Report failure,     │                 │
│ linked to      │  │ retry or escalate   │                 │
│ release plan   │  │ (loop back to fix   │                 │
│ automatically  │  │  TypeSpec & retry)  │                 │
└───────┬────────┘  └─────────────────────┘                 │
        │                                                   │
        └───────────────────────┬───────────────────────────┘
                                │
                                ▼
             ┌───────────────────────────────────────────────────┐
             │  STEP 6: Link SDK PRs to Release Plan            │
             │  (Local gen only — pipeline auto-links)          │
             └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
╔═══════════════════════════════════════════════════════════════════╗
║  STAGE 4: SDK PR Validation & API Review                         ║
╚═══════════════════════════════════════════════════════════════════╝
                                    │
              ┌───────────────────────────────────────────────────┐
              │  STEP 7: SDK PR CI Pipeline                      │
              │  (auto-triggers on SDK PR open/update)           │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │ • Build → Test → Lint → Package validation       │
              │ • SDK breaking change detection                   │
              │ • APIView generated for SDK public API surface    │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │ BUILD FAIL                                          │ BUILD PASS
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Custom code drift?      │                     │ CI green                │
├─────────────────────────┤                     │ Awaiting review         │
│ YES: auto-sdk-build-fix │                     └───────────┬─────────────┘
│ label → Copilot agent   │                                 │
│ auto-repairs → re-runs  │                                 │
├─────────────────────────┤                                 │
│ NO: Pipeline troubleshoot│                                │
│ agent diagnoses          │                                │
└───────────┬─────────────┘                                 │
            │                                               │
            └───────────────────────┬───────────────────────┘
                                    │
                                    ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 8: APIView Review                          │
              │  (architects review SDK public API surface)      │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │ Has suggestions                                     │ No suggestions
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Resolve APIView         │                     │ Approved                │
│ suggestions             │                     │                         │
│ [APIView Feedback Skill]│                     │                         │
└───────────┬─────────────┘                     └───────────┬─────────────┘
            │                                               │
            ▼                                               │
┌─────────────────────────┐                                 │
│ Changes required →      │                                 │
│ Update TypeSpec →       │                                 │
│ Re-generate SDK →       │                                 │
│ New commit to PR →      │                                 │
│ CI re-runs (loop back   │                                 │
│ to Step 7)              │                                 │
└───────────┬─────────────┘                                 │
            │                                               │
            └───────────────────────┬───────────────────────┘
                                    │
                                    ▼
              ┌───────────────────────────────────────────────────┐
              │  SDK PR approved & merged                         │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
╔═══════════════════════════════════════════════════════════════════╗
║  STAGE 5: Release Coordination                                   ║
╚═══════════════════════════════════════════════════════════════════╝
                                    │
              ┌───────────────────────────────────────────────────┐
              │  STEP 9: Release SDKs                            │
              │  [Release Skill]                                 │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │ • Check release readiness per language            │
              │ • Namespace approval (if new package)             │
              │ • Trigger release pipeline (manual approval gate) │
              │ • Packages published to registries                │
              │ • Release plan auto-completes                     │
              │ • Service Tree KPI updated                        │
              └───────────────────────────────────────────────────┘

  Gap: No agent-driven transition from Stage 4 → 5.
  User manually triggers release after PR merge.
```

#### Stage ↔ Step Mapping

| Stage | Steps | Transition to Next Stage |
|-------|-------|--------------------------|
| 0 (pre) | Step 0: Release plan | Manual |
| 1 | Steps 1–3: Author, validate, open PR | Manual (developer opens PR) |
| 2 | Step 4: CI validation | Auto (spec-gen-sdk on merge) |
| 3 | Steps 5–6: Generate, link PRs | Auto (PRs created by spec-gen-sdk) |
| 4 | Steps 7–8: CI + APIView + repair | Manual (user triggers release) |
| 5 | Step 9: Release | — |

| Stage | Name | Input | Output | Key Tools |
|-------|------|-------|--------|-----------|
| 1 | TypeSpec Authoring | Service requirements | `.tsp` files, `tspconfig.yaml` | TypeSpec compiler, authoring agent, breaking change tool, linter |
| 2 | Spec PR Validation | PR in `azure-rest-api-specs` | Validation checks, labels, APIView tokens | Validation pipeline, LintDiff, `@azure-tools/typespec-breaking-change`, APIView emitter, spec-gen-sdk dry-run |
| 3 | SDK Generation | Approved/merged spec | SDK PRs per language | tsp-client, language emitters, spec-gen-sdk, azsdk-cli (`generate`, `build`, `test`, `metadata`) |
| 4 | SDK PR Validation & API Review | SDK PRs in language repos | Approved & merged PRs | Language CI, SDK breaking change detector, APIView, feedback resolution agent, pipeline troubleshooting agent, auto SDK PR repair |
| 5 | Release Coordination | Merged SDK PRs | Published packages, KPI update | Release plan tooling, release pipelines, Service Tree |

---

#### Stage 1: TypeSpec Authoring

Developer writes/updates TypeSpec locally: `.tsp` files, `tspconfig.yaml`, local compilation, linting, agent-assisted feedback.

| Tool | Role | Owner |
|------|------|-------|
| TypeSpec compiler | Compile `.tsp` files, catch syntax/type errors | TypeSpec team |
| TypeSpec authoring agent (`azure-typespec-author` skill) | Assist with ARM/data-plane patterns, Azure REST API guidelines | Haoling |
| `@azure-tools/typespec-breaking-change` | Phase A: same-version regression (any diff = error). Phase B: cross-version evolution (request narrowing / response widening = breaking). Inline suppression via decorators. Also runs in Stage 2 CI. | Mark Cowlishaw |
| TypeSpec linter | Static guideline compliance (distinct from LintDiff) | TypeSpec team |

**Gap**: Breaking change tool reports findings with DiffKind, source location, and suggested suppression decorator — but the user must apply fixes manually. No agent currently auto-resolves these findings.

---

#### Stage 2: Spec PR Validation

PR in `azure-rest-api-specs` triggers: compilation → LintDiff → breaking change detection (Phase A + B) → APIView token generation → SDK generation dry-run per language → labels applied.

| Tool | Role | Owner |
|------|------|-------|
| Spec PR validation pipeline | Orchestrates full validation suite on PR open/update | EngSys |
| TypeSpec compiler | CI compilation | TypeSpec team |
| LintDiff | Spectral-based linting on swagger diffs | EngSys |
| `@azure-tools/typespec-breaking-change` | Phase A + B detection. Auto-adds `BreakingChangeReviewRequired` / `VersioningReviewRequired` labels | Mark |
| APIView emitter (`typespec-apiview`) | Generates API surface token files for architect review | APIView team |
| spec-gen-sdk | SDK generation validation — ensures spec can produce SDK code | EngSys (Renhe) |

**Gap**: Validation steps run independently. No designed chain for: (1) failure ordering (should linting run if compilation fails?), (2) how breaking change results gate SDK generation, (3) per-language generation failure reporting back to spec PR. No unified PR comment summarizing all results with next steps.

---

#### Stage 3: SDK Generation

**Trigger**: When a spec PR is merged in `azure-rest-api-specs`, a two-stage pipeline runs: Stage 1 automatically creates/finds the release plan, Stage 2 triggers SDK generation for each configured language and creates SDK PRs in the corresponding language repos (e.g., `azure-sdk-for-python`). The `azsdk_run_generate_sdk` tool drives this via `SpecWorkflowTool`, taking TypeSpec project path, release plan work item ID, language, and release type as inputs. If an existing SDK PR is already open for that language (linked in the release plan), the pipeline pushes to the same branch rather than creating a new PR. Alternatively, developers can generate locally using azsdk-cli for faster iteration before opening a PR.

For each target language: tsp-client syncs TypeSpec → emitter generates code → customizations applied → build → test (playback) → metadata updated → SDK PR created. Two paths: local (developer iterates, creates PR manually) or pipeline (spec-gen-sdk generates and creates PRs automatically on spec PR merge).

| Tool | Role | Owner |
|------|------|-------|
| tsp-client | Syncs TypeSpec project into SDK repo, manages `tsp-location.yaml` | EngSys |
| Language emitters | Generate client library code from TypeSpec (one per language) | Language teams |
| spec-gen-sdk | Pipeline automation — runs full generation workflow, creates SDK PRs | EngSys (Renhe) |
| azsdk-cli (`azsdk_package_generate_code`) | Local orchestration — generate, build, test, validate | azsdk-cli team |
| azsdk-cli (`azsdk_package_build_code`, `azsdk_package_run_tests`, `azsdk_package_run_check`) | Build, test, validate locally | azsdk-cli team |
| azsdk-cli (`azsdk_package_update_changelog_content`, `azsdk_package_update_metadata`, `azsdk_package_update_version`) | Update package metadata | azsdk-cli team |
| `azsdk_customized_code_update` | Apply TypeSpec and code-level customizations (classify → fix → regenerate → rebuild) | azsdk-cli team |

**Gap**: When SDK generation fails, spec-gen-sdk reports a failed pipeline check. The error is buried in build logs — not surfaced as a structured report (which language, which step, what error). No agent helps troubleshoot generation failures. The auto-repair pattern used at Stage 4 (label → Copilot agent → fix → rebuild) could potentially be extended here to diagnose common generation failures.

---

#### Stage 4: SDK PR Validation & API Review

CI runs (build → test → lint → package validation → SDK breaking change detection). If build fails on custom code drift: `auto-sdk-build-fix` label → Copilot cloud agent auto-repairs → commits fix → CI re-runs. APIView generates SDK public API surface review → architects leave comments → developer resolves via TypeSpec changes (re-triggers generation) → iterate until approved and merged.

| Tool | Role | Owner |
|------|------|-------|
| Language CI pipelines | Build, test, lint, package validation | Language teams |
| SDK breaking change detector | Detects breaking changes in generated SDK API surface. Being combined into SDK validation check. Could the auto-repair pattern (below) also apply here for resolution? | Ray & Crystal |
| APIView | SDK public API surface review — architects review and approve | APIView team |
| APIView feedback resolution agent (`azsdk-common-apiview-feedback-resolution` skill) | Helps resolve APIView comments via TypeSpec changes | azsdk-cli team |
| Pipeline troubleshooting agent (`azsdk-common-pipeline-troubleshooting` skill) | Diagnoses CI failures | azsdk-cli team |
| Auto SDK PR repair (`azsdk_customized_code_update` + Copilot cloud agent) | When custom code drifts and breaks the build, `auto-sdk-build-fix` label triggers a Copilot cloud agent to fix custom code, regenerate, and rebuild. Shared orchestration in `eng/common/`, per-language opt-in. | azsdk-cli team |

---

#### Stage 5: Release Coordination

Release plan work item created/updated → namespace approval (if new package) → readiness checked per language → release pipeline triggered (manual approval gate) → packages published → release plan auto-completes → Service Tree KPI updated.

| Tool | Role | Owner |
|------|------|-------|
| Release plan tooling (`azsdk_create_release_plan`, `azsdk_get_release_plan`, etc.) | Create/update/link Azure DevOps work items | azsdk-cli team |
| Release pipeline (`azsdk_release_sdk`) | Check readiness, trigger release | azsdk-cli team |
| Language release pipelines | Publish to PyPI, Maven, npm, NuGet, Go module proxy | Language teams |
| Service Tree integration | Mark service KPIs as completed | EngSys |

**Release type approval differences**:

| Release Type | Approval Gates | Notes |
|-------------|----------------|-------|
| Preview | No architect board review (can be requested) | Fastest path |
| GA (first release) | Architect board review required | Namespace approval needed for new packages |
| GA (update) | Standard review | Breaking changes require separate approval |
| Patch | Standard review | Must maintain backward compatibility |

---

### Architecture: Skill Chaining with Next Steps

The current system uses a **prompt chaining** pattern: independent sub-skills are invoked sequentially, with each tool returning `NextSteps` that guide the LLM agent to the next action. Tool responses return structured data (release plan ID, TypeSpec path, package names) that the LLM agent retains in conversation context. `CommandResponse.NextSteps` is used across 20+ tool and service files.

**What works today**:
- Tools return actionable `NextSteps` strings that LLM agents parse to determine the next tool call
- Cross-tool chaining exists in the release plan flow: `create release plan` → "Update SDK details" → "API spec approved, run SDK generation using `azsdk_run_generate_sdk`"
- Each skill is independently testable and deployable
- Human stays in the loop at each transition (critical for approval-gated stages)

**Gaps to improve orchestration** (without building a full orchestrator):

| Gap | Current State | Improvement |
|-----|---------------|-------------|
| NextSteps are natural language | LLM must interpret free-text like "Run SDK generation using `azsdk_run_generate_sdk`" — works but is fragile | Consider structured NextSteps with explicit tool name + required parameters |
| Chaining is partial (Stages 3–5 only) | No NextSteps connecting Stage 1 → 2. These transitions happen outside the agent | Add cross-tool NextSteps for early stages (authoring → "open spec PR," validation → "generate SDK") |
| Skills don't reference each other | SKILL.md files are fully independent. Chaining relies on LLM interpretation | Document expected skill sequences in workflow skill or prompt |
| No state detection | Agent cannot determine "where am I?" in the workflow | Add a "workflow status" tool that queries release plan + PR status to suggest next stage |
| Errors not structured for agents | Some tools return errors buried in logs or free text | Every tool returns errors in a parseable format with suggested next action |
| Label-driven automation gaps | Labels like `BreakingChangeReviewRequired` and `auto-sdk-build-fix` exist but routing is not fully connected | Connect label events to automation (review routing, custom-code repair, release gating) |

---

## Known Gaps

| # | Gap | Stage | Current State | Desired State | Owner |
|---|-----|-------|---------------|---------------|-------|
| 1 | Breaking change detection reports findings but resolution is manual | 1, 2 | Tool reports breaks + suggests suppression decorator. User applies manually. | Structured findings are surfaced in a format that makes manual resolution straightforward (clear PR comment with fix instructions) | Mark / Crystal |
| 2 | End-to-end CI chain not designed | 2 | Individual validation steps exist but chaining, failure handling, and result reporting back to PR are ad-hoc | Single pipeline with clear per-step failure reporting as PR comments | Ray & Crystal |
| 3 | Label routing for breaking change review undefined | 2 | `@azure-tools/typespec-breaking-change` defines labels but review team routing not connected | Labels auto-route to correct review team | Ray |
| 4 | Generation errors silently fail | 3 | spec-gen-sdk fails with no user-visible error | Structured error report (which language, which step, suggested fix) | Praveen / spec-gen-sdk |
| 5 | No troubleshooting for generation failures | 3 | User must manually diagnose | Agent diagnoses common failures (missing deps, emitter mismatch) | azsdk-cli team |
| 6 | SDK breaking change detection integration in progress | 4 | Being combined into SDK validation check | Fully integrated into language CI | Ray & Crystal |
| 7 | .NET team alignment needed | All | .NET team has `azsdk_customized_code_update` integration and cross-language auto-repair design. TypeSpec linter/fixer pattern (enum-member-casing) proven for .NET, being adapted cross-language. | Shared linter rules reused at all layers; auto-repair opted-in for .NET; remaining tools documented | Sameeksha + Laurent |
| 8 | Scattered documentation | All | Process docs in EngHub, Wiki, repo specs | Single authoritative source | Sameeksha + Praveen |

---

## Success Criteria

This workflow is complete when:

- [ ] Users can complete full TypeSpec → SDK release workflow with agent guidance
- [ ] Agent detects existing state and resumes from appropriate step
- [ ] Release plan is automatically updated at each step
- [ ] All sub-skills integrate seamlessly without context loss between stages
- [ ] Local and pipeline SDK generation paths both work
- [ ] Breaking change findings from CI are surfaced clearly to the user
- [ ] APIView feedback can be resolved within the workflow
- [ ] Works for all tier-1 SDK languages (per language scope table)
- [ ] Errors at every stage produce structured, actionable guidance
- [ ] Service Tree KPI is updated on release completion

---

## Exceptions and Limitations

### Exception 1: Architect Board Review

**Description**: First GA releases require architect board review (human decision-making outside automated workflow). Preview releases do not require review but can request one.

**Impact**: Workflow cannot fully automate GA approval.

**Status**: Being automated via GitHub Forms + Actions in `azure-sdk` repo. Service teams submit review requests via a GitHub Form (replacing email to azsdkarch-help@microsoft.com). Workflow validates URLs, applies `ready-for-review` / `needs-info` labels, and auto-closes when architects apply per-language `<lang>-api-approved` labels. Review itself remains a human decision.

### Exception 2: Breaking Change Reviews

**Description**: SDKs with breaking changes require API breaking change review team approval. This is a separate review process with its own team and labels.

**Impact**: Breaking change releases blocked until review team approves.

**Workaround**: Agent helps prepare suppression decorators with clear reasons, guides review request.

### Exception 3: Package Naming Approval

**Description**: New SDK packages require namespace/naming approval before release.

**Impact**: New package releases blocked until naming approved.

**Status**: Being automated as a merge gate directly on spec PRs in `azure-rest-api-specs`. When a PR modifies `tspconfig.yaml`, the workflow extracts namespaces using `@azure-tools/typespec-metadata` emitter, applies `<lang>-namespace-pending` labels, and blocks merge until authorized architects apply `<lang>-namespace-approved` labels (or `namespace-approved-all` for management plane). Replaces the previous manual email-based triage. Approval resets automatically if namespace values change after approval.

### Exception 4: .NET Team Tooling

**Description**: The .NET team has developed tooling that shares infrastructure with azsdk-cli via `azsdk_customized_code_update` (custom-code auto-repair) and TypeSpec linter/fixer patterns. Their cross-language auto-repair design proposes shared orchestration in `eng/common/` with per-language opt-in.

**Impact**: .NET is ahead on auto-repair and linter integration. Other languages can adopt the same patterns.

**Next step**: Schedule alignment meeting to document remaining .NET-specific tools and confirm integration points.

---

## Open Questions

- [ ] **Q1**: Does `@azure-tools/typespec-breaking-change` CI output provide enough structured context (DiffKind, source location, suggested suppression decorator text) for an agent to auto-resolve? Need confirmation from Mark.
  - Options: (a) Output is already sufficient, (b) Need additional structured output format

- [ ] **Q2**: How should `BreakingChangeReviewRequired` / `VersioningReviewRequired` labels route to the correct review team? Is this existing GitHub CODEOWNERS-based routing or new automation?
  - Options: (a) CODEOWNERS, (b) Custom GitHub Action, (c) Azure DevOps integration

- [ ] **Q3**: What is the .NET team's tooling stack and where are the integration points with azsdk-cli?
  - Context: .NET team has developed independent tooling. Need alignment on shared infrastructure.
  - **Known integration points** (from cross-language auto-repair design):
    - `azsdk_customized_code_update` MCP tool — already supports .NET (classify → fix → regen → rebuild loop for custom-code drift)
    - Auto SDK PR repair — shared orchestration in `eng/common/`, per-language opt-in via thin trigger workflow + `SDK_BUILD_REPAIR_ENABLED` flag
    - Safety validation gate — shared cross-language denylist + per-language build check
    - Copilot cloud agent — triggered by `auto-sdk-build-fix` label on failing SDK PRs
  - **Remaining question**: Are there .NET-specific tools beyond `azsdk_customized_code_update` that should integrate into the shared workflow?
  - **Next step**: Schedule alignment meeting with .NET team.

- [ ] **Q4**: Should spec-gen-sdk generation failures be reported as PR comments, or as structured data that the agent can parse and act on?
  - Options: (a) PR comments only, (b) Structured JSON output + PR comment summary, (c) Both

- [ ] **Q5**: For Patch releases — what triggers the workflow? Is it different from Preview/GA?

- [ ] **Q6**: Is there a process to auto-release SDKs after SDK PRs are merged? Auto SDK PR creation is actively being built via the two-stage pipeline (release plan creation → SDK generation), but the auto-release step after merge is unclear — is anyone actively working on it?
  - Context: Auto-release would complete the end-to-end automation (spec merge → release plan → SDK generation → SDK PR → merge → publish). Currently Stage 5 (Release Coordination) requires manual trigger.
