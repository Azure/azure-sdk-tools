# Spec: TypeSpec to SDK Release Workflow - End-to-End Orchestration

## Table of Contents

- [Quick Start](#quick-start)
  - [For Service Teams](#for-service-teams)
  - [For Reviewers](#for-reviewers)
  - [For EngSys / SDK Team](#for-engsys--sdk-team)
- [Decisions Needed](#decisions-needed)
- [Definitions](#definitions)
- [Background / Problem Statement](#background--problem-statement)
- [Goals](#goals)
- [Workflow Design](#workflow-design)
  - [Workflow Stages](#workflow-stages)
    - [High-Level Sequence Diagram](#high-level-sequence-diagram-service-team-journey-spec-pr-entry-point)
  - [Labels & Automation Triggers](#labels--automation-triggers)
  - [Related Process Documentation](#related-process-documentation-external-links)
  - [Architecture: Skill Chaining with Next Steps](#architecture-skill-chaining-with-next-steps)
- [Known Gaps](#known-gaps)
- [Success Criteria](#success-criteria)
- [Exceptions and Limitations](#exceptions-and-limitations)
- [Open Questions](#open-questions)
- [Appendix: Detailed Flowchart](#appendix-detailed-flowchart)

---

## Quick Start

### For Service Teams

1. **Author TypeSpec** — Write your `.tsp` files and `tspconfig.yaml` locally (or use the `azure-typespec-author` agent skill)
2. **Open a spec PR** — Push to `azure-rest-api-specs` and open a PR. CI validates automatically.
3. 🧑‍💻 **Wait for approvals** — The following require human sign-off before spec PR can merge:
   - **Namespace approval** (first preview only) — Architect approves new package namespaces
   - **ARM review** (ARM specs only) — ARM review team signs off on resource model correctness
   - **Breaking change review** (ARM specs only) — If `BreakingChangeReviewRequired` label is applied
   - > **Note**: There is **no spec-level API review**. API review happens only at the SDK level (Stage 4). The TypeSpec spec PR on GitHub is the spec surface, but formal API review (APIView / API Review Hub) applies to the generated SDK.
4. **Spec PR merges → SDK generation is automatic** — Release plan is created, SDKs are generated, and SDK PRs are opened in each language repo automatically ⚠️ *Caveat: generation failures currently fail silently — see [Known Gap #4](#known-gaps)*
5. 🧑‍💻 **SDK PR review & approval** — SDK CI runs automatically. **API review of the generated SDK surface** happens via APIView (current) or API Review Hub review PRs (future). ARH assigns `<lang>-api-approved` labels on SDK PRs as **informational** signals — the source of truth for approval is in ADO Package Work Items (API hash), not labels. For management plane, Shanghai team reviews SDK PRs with release plans. Review requests currently use a [GitHub Form template](https://github.com/Azure/azure-sdk/blob/main/.github/workflows/src/arch-board-review/ARCH-BOARD-REVIEW-PROCESS.md) as a **bridge** (replaces email; will be retired when API Review Hub goes live).
6. 🧑‍💻 **Release** — Once SDK PRs merge, trigger release pipeline (**manual approval gate** required for security; difficult to remove. ARM approval = Shanghai team). Packages publish, release plan completes. See also: [Management plane release responsibilities](https://eng.ms/docs/products/azure-developer-experience/plan/mgmt-sdk-release-process)

### For Reviewers

1. **ARM review** (ARM specs only) — Review resource model correctness on spec PRs; apply `ARMSignedOff` label
2. **SDK API review** — Review generated SDK API surface on SDK PRs via APIView (current) or API Review Hub review PRs (future). **There is no spec-level API review** — API review applies only to the generated SDK. `<lang>-api-approved` labels are **informational** — the source of truth is ADO Package Work Items (API hash approved by ARH).
3. **SDK PR review** (Shanghai team, management plane only) — Review generated SDK PRs that have a release plan attached; approve & merge
4. **Breaking change review** — Review breaking changes flagged by `BreakingChangeReviewRequired` label on spec PRs (ARM specs only)
5. **Namespace review** — Approve new package namespaces; apply `namespace-<lang>-approved` labels
6. **Release approval** — Approve release pipeline runs (Shanghai team for ARM)

### For EngSys / SDK Team

1. **Monitor pipelines** — Spec PR validation, SDK generation, and SDK CI pipelines run automatically
2. **Label routing** — Labels like `BreakingChangeReviewRequired`, `namespace-<lang>-pending`, and `auto-sdk-build-fix` trigger review routing and automation
3. **Generation failures** — When SDK generation fails, diagnose via pipeline logs (structured error reporting is a [known gap](#known-gaps))
4. **Auto-repair** — `auto-sdk-build-fix` label triggers Copilot cloud agent to fix custom code drift on SDK PRs
5. **Release coordination** — `azsdk_release_sdk` checks readiness; release pipelines publish to package registries (manual approval gate required)
6. **Track progress** — [Release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard) shows where each service is in the process

---

## Decisions Needed

| # | Decision | Why it matters | Owner | Status |
|---|----------|---------------|-------|--------|
| D1 | How does ARH review PR creation get triggered on SDK PRs? | No automation today — open design gap | @tjprescott | Open |
| D2 | Are `api-approved` / `<lang>-api-approved` labels authoritative or informational? | ARH tracks approval in ADO Package Work Items (API hash), not via labels. Labels are **informational** signals — the source of truth is the ADO approval state. | @tjprescott | Resolved — labels are informational |
| D3 | Should service teams approve SDK PRs? | Current flow doesn't require it | Laurent | Open |
| D4 | Where does breaking-change enforcement live? | Defines responsibility and UX | TBD | Open |
| D5 | What triggers auto-release after SDK PR merge? | Last missing piece for full E2E automation | @raych1 | Open |

---

## Definitions

- **TypeSpec**: Language for describing cloud service APIs. See [typespec.io](https://typespec.io).
- **SDK**: Client libraries generated from TypeSpec for .NET, Java, JavaScript, Python, Go.
- **Release Plan**: Azure DevOps work item tracking end-to-end SDK release across languages. Managed by azsdk-cli tooling.
- **API Spec PR**: Pull request in `azure-rest-api-specs` or `azure-rest-api-specs-pr` containing TypeSpec changes.
- **SDK PR**: Pull request in a language SDK repo (e.g., `azure-sdk-for-python`) with generated and customized SDK code.
- **APIView**: Current web tool for reviewing SDK public API surface. Being replaced by **API Review Hub**. Operates at **SDK level only** — there is no spec-level API review.
- **API Review Hub**: New service (@tjprescott) that replaces APIView for **SDK-level API review** — there is no spec-level API review (the TypeSpec spec PR on GitHub is the spec surface, but formal API review applies to the generated SDK only). Creates synthetic "review PRs" in language repos containing generated `API.md` files showing API diffs — these PRs are **never merged** and exist only for review. Architects are auto-assigned as reviewers and approve/request changes via standard GitHub PR review. Approval is recorded in **ADO Package Work Items** (API hash stored as "approved"), and CI gates release by checking this hash. ARH assigns `<lang>-api-approved` labels on SDK PRs as **informational** signals. ⚠️ Opening ARH review PRs on generated SDK PRs is an open design gap — not automated today.
- **tspconfig.yaml**: Configuration in TypeSpec project specifying emitter settings per language.
- **tsp-location.yaml**: Configuration in SDK repos pointing to the source TypeSpec project.
- **`@azure-tools/typespec-breaking-change`**: TypeSpec-native breaking change detector. Phase A: same-version regression (any diff = error). Phase B: cross-version evolution (request narrowing / response widening = breaking).
- **Suppression Decorators**: `@approvedBreakingChange` (Phase B) and `@approvedUnversionedChange` (Phase A) — inline approval with auto-labeling.
- **TypeSpec Customizations**: SDK-specific customizations in `client.tsp` (renaming, convenience methods).
- **Code Customizations**: Hand-written SDK code that must be preserved across regeneration.
- **TypeSpec Lintdiff**: Linting pipeline that runs TypeSpec linter rules on spec PR diffs. Validates guideline compliance, flags violations, and supports a suppression process for known exceptions. Replacing the older Swagger-based Spectral LintDiff.
- **spec-gen-sdk**: Pipeline tool that automates SDK generation from specs. Runs as part of spec PR CI (dry-run validation) and on spec PR merge (creates SDK PRs in language repos). Owned by EngSys.

---

## Background / Problem Statement

### Current State

Azure service teams face a complex, multi-step process to release SDKs from TypeSpec specifications:

1. **Fragmented tooling**: Different tools for TypeSpec authoring, spec validation, breaking change detection, SDK generation, and release management — developed independently with limited integration.
2. **Manual coordination**: Teams manually track progress across multiple repositories (`azure-rest-api-specs`, `azure-sdk-for-*`) and systems (Azure DevOps, GitHub, Service Tree). The [release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard) provides visibility but doesn't drive automation.
3. **Silent failures**: SDK generation failures are not surfaced to users — tools fail without actionable guidance.
4. **Scattered documentation**: Process is documented across EngHub, Wiki, and repo specs with no single authoritative source.
5. **No unified CI chain**: Spec PR validation (compile → lint → breaking change → generate → build) lacks a designed end-to-end failure-handling strategy.

### What Has Changed

- The standalone "release planner" system has been **fully removed**. Release tracking is now handled directly through Azure DevOps work items managed by azsdk-cli and the [release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard).
- `@azure-tools/typespec-breaking-change` provides TypeSpec-native breaking change detection (replacing OpenAPI-based tools for TypeSpec specs).

### Why This Matters

- **Time from API definition to SDK release** is measured in weeks for experienced teams, longer for new ones.
- **Partial agent-assisted workflow** exists today via azsdk-cli MCP tools — the agent can help with individual stages but lacks a unified end-to-end orchestration that connects all stages seamlessly.

---

## Goals

1. Provide an end-to-end guided workflow from TypeSpec authoring to SDK release
2. Enable resuming the workflow from any intermediate state ⚠️ *Caveat: resume mechanism not yet defined — the agent should detect existing state (release plan, open PRs) and pick up from the appropriate step, but the specific scenarios and detection logic are a gap to be designed*
3. Automatically track release plan status throughout the workflow
4. Surface errors with actionable guidance at every failure point
5. Integrate sub-skills seamlessly for specialized tasks
6. Support all tier-1 SDK languages with appropriate scope:

| Plane | Required Languages | Optional |
|-------|-------------------|----------|
| **Management plane** | .NET, Java, JavaScript, Python, Go | — |
| **Data plane** | .NET, Java, JavaScript, Python | Go, Rust (planned) |

---

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

The end-to-end flow consists of 5 stages. This section presents the workflow in two views:
1. **High-level sequence diagram** — shows service team interaction timeline (read first)
2. **Detailed flowchart** — shows every step, branching, and gaps (reference)

#### High-Level Sequence Diagram: Service Team Journey (Spec PR Entry Point)

This shows the typical service team interaction when they already have TypeSpec ready and open a spec PR:

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│ Service  │     │ Spec Repo│     │   CI     │     │Reviewers │     │SDK Repos │     │ Release  │
│  Team    │     │  (GH)    │     │Pipeline  │     │(ARM/API) │     │(per lang)│     │ Pipeline │
└────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘
     │                 │                │                │                │                │
     │  Open spec PR   │                │                │                │                │
     │────────────────>│                │                │                │                │
     │                 │                │                │                │                │
     │                 │  PR triggers   │                │                │                │
     │                 │───────────────>│                │                │                │
     │                 │                │                │                │                │
     │                 │                │── Compile      │                │                │
     │                 │                │── LintDiff     │                │                │
     │                 │                │── Breaking chg │                │                │
     │                 │                │── APIView gen  │                │                │
     │                 │                │── SDK dry-run  │                │                │
     │                 │                │── Labels apply │                │                │
     │                 │                │                │                │                │
     │                 │  CI results    │                │                │                │
     │                 │<───────────────│                │                │                │
     │                 │                │                │                │                │
     │  [If FAIL]      │                │                │                │                │
     │  Fix & push     │                │                │                │                │
     │────────────────>│  (re-triggers) │                │                │                │
     │                 │───────────────>│                │                │                │
     │                 │                │                │                │                │
     │                 │  [If PASS] Request reviews      │                │                │
     │                 │───────────────────────────────->│                │                │
     │                 │                │                │                │                │
     │                 │                │                │  ARM review    │                │
     │                 │                │                │  (if ARM spec) │                │
     │                 │                │                │                │                │
     │                 │                │                │  Breaking chg  │                │
     │                 │                │                │  review (ARM)  │                │
     │                 │                │                │                │                │
     │  [If feedback]  │                │                │                │                │
     │  Address review │                │                │                │                │
     │────────────────>│                │                │                │                │
     │                 │                │                │                │                │
     │                 │  Approved +    │                │                │                │
     │                 │  namespace OK  │                │                │                │
     │                 │  → MERGE       │                │                │                │
     │                 │                │                │                │                │
     │                 │  ─ ─ ─ ─ ─ ─ ─ AUTOMATED FROM HERE ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ │
     │                 │                │                │                │                │
     │                 │  Merge event   │                │                │                │
     │                 │───────────────>│                │                │                │
     │                 │                │                │                │                │
     │                 │                │── Create/find release plan      │                │
     │                 │                │── Generate SDK per language     │                │
     │                 │                │── Apply customizations          │                │
     │                 │                │───────────────────────────────────────>│         │
     │                 │                │                │                │  SDK PRs       │
     │                 │                │                │                │  created       │
     │                 │                │                │                │                │
     │                 │                │                │                │── SDK CI runs  │
     │                 │                │                │                │── Build/test   │
     │                 │                │                │                │── API review   │
     │                 │                │                │                │                │
     │  [If build fail on custom code]  │                │                │                │
     │                 │                │                │   auto-repair  │                │
     │                 │                │                │   via Copilot  │                │
     │                 │                │                │────────────────>                │
     │                 │                │                │                │                │
     │  [If API review feedback]        │                │                │                │
     │  Resolve via TypeSpec changes    │                │                │                │
     │─────────────────────────────────────────────────────────────────->│                │
     │                 │                │                │                │                │
     │                 │                │                │                │  SDK PR merged │
     │                 │                │                │                │                │
     │                 │                │                │                │                │
     │  Trigger release (or auto)       │                │                │       ┌───────>│
     │──────────────────────────────────────────────────────────────────────────┘        │
     │                 │                │                │                │                │
     │                 │                │                │                │   Publish pkgs │
     │                 │                │                │                │   Update KPI   │
     │                 │                │                │                │                │
     │  ✅ Done!       │                │                │                │                │
     │<────────────────────────────────────────────────────────────────────────────────────│
     │                 │                │                │                │                │
```

**Key insight**: After spec PR merge, the flow is largely **automated**. The service team only re-engages if:
- SDK CI fails and auto-repair can't fix it
- API review has feedback requiring TypeSpec changes
- Release pipeline requires manual approval (approval gate cannot be removed for security reasons; for ARM, approval is from Shanghai team)

> **Note: ARM vs Data Plane divergence** — ARM (management plane) and data plane specs follow the same high-level flow but diverge at review gates: ARM specs require ARM review team sign-off (`ARM-Review-Required` label) and have stricter resource model constraints. Data plane specs skip ARM review but still require API review. A separate process document will detail these divergences. See: [ARM review process](https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/arm-review) | [Data plane review process](https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/data-plane-review)
>
> **Open question: Data-plane review model** — With the stewardship board being reconsidered, who reviews data-plane PRs? An interesting pattern emerging: for ARM, if the REST API spec looks fine, we assume the SDK is ok to ship. For data-plane, if the SDK looks fine, we could assume the TypeSpec spec is ok to merge. This would mean ARM quality flows spec → SDK, while data-plane quality flows SDK → spec.


#### Stage ↔ Step Mapping

| Stage | Steps | Transition to Next Stage |
|-------|-------|--------------------------|
| 1 | Steps 1–3: Author, validate, open PR | Manual (developer opens PR) |
| 2 | Steps 4–4b: CI validation + ARM/API review | Auto (on spec PR merge) |
| 3 | Automatic: release plan creation + SDK generation | Auto (SDK PRs created on merge) |
| 4 | Steps 5–6: SDK CI + API review + repair | Manual (user triggers release) |
| 5 | Step 7 (sub-steps 7a–7d): Release | — |

---

### Labels & Automation Triggers

Labels are the primary orchestration signals in the workflow. They trigger automation, gate merges, and route reviews. The **Automation** column indicates whether the label → action connection is fully wired today.

#### Spec PR Labels (`azure-rest-api-specs`)

| Label | Applied by | Meaning | Automation |
|-------|-----------|---------|------------|
| `BreakingChangeReviewRequired` | CI | Breaking change detected — routes to review team, blocks merge | ⚠️ Label applied automatically, but routing to review team is manual |
| `VersioningReviewRequired` | CI | Versioning review needed — blocks merge | ⚠️ Label applied, merge block works, but reviewer assignment manual |
| `ARM-Review-Required` | CI | ARM spec — routes to ARM review team, blocks merge | ✅ Fully automated (label + routing + merge gate) |
| `ARMSignedOff` | ARM review team | ARM review approved — unblocks merge | ✅ Manual label, merge gate automated |
| `APIStewardshipBoard-SignedOff` | Stewardship board | Data-plane API approved (process in transition) | ⚠️ Process in transition — stewardship board being reconsidered |
| `namespace-<lang>-pending` | CI | New namespace detected — awaiting architect approval | ✅ Fully automated (CI detects, blocks merge) |
| `namespace-<lang>-approved` | Architect | Namespace approved for this language | ✅ Manual label, merge gate automated |
| `namespace-approved-all` | Architect | Shortcut: approves all languages (mgmt plane) | ✅ Manual label, merge gate automated |
| `Approved-BreakingChange` | Review team | Breaking change approved — unblocks merge | ⚠️ Manual label, merge gate works, but routing to apply it is manual |
| `Suppression-Approved` | Review team | Linter suppression approved (tied to TypeSpec Lintdiff suppression process) | ⚠️ Manual label, suppression validation automated |

#### SDK PR Labels (language repos)

| Label | Applied by | Meaning | Automation |
|-------|-----------|---------|------------|
| `auto-sdk-build-fix` | CI / human | Triggers Copilot cloud agent to auto-repair custom code drift | ✅ Label triggers cloud agent automatically |
| `<lang>-api-approved` | ARH / Architect | SDK API surface approved — **informational only**. Source of truth is ADO Package Work Items (API hash). | ⚠️ ARH will assign label automatically; no auto-routing to architect yet |
| `release-plan-linked` | Automation | SDK team (Shanghai) reviews only PRs with this label | ✅ Applied automatically when release plan linked |
| `ready-for-review` | GitHub Form | Triggers architect board review process | ✅ Applied via GitHub Form workflow |
| `needs-info` | Reviewer | Service team needs to provide more info | ⚠️ Manual label, no automation triggered |
| `review-out-of-date` | API Review Hub | New changes on working branch not yet reflected in review PR | 🔜 Part of API Review Hub (not yet in production) |
| `architecture-review-needed` | API Review Hub | Flags SDK PR for architect review | 🔜 Part of API Review Hub (not yet in production) |

**Automation gaps**: Late spec validation (namespace approval, API version, spec branch) is detected at SDK PR stage instead of spec PR stage. SDK PR title/description not updated on re-generation. See [SDK PR Release Readiness tracking issue](https://github.com/Azure/azure-sdk-tools/issues/15705) for the full gap list including .NET-specific gaps, release pipeline provisioning delays, and pipeline failure categories.

#### Related Process Documentation (External Links)

| Process | Link | Scope |
|---------|------|-------|
| Namespace approval | [Namespace approval (PR #44085)](https://github.com/Azure/azure-rest-api-specs/pull/44085) | Permissions, approval flow, labels — in progress |
| ARM review | [ARM review process](https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/arm-review) | ARM-specific review gates |
| Data plane review | [Data plane review process](https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/data-plane-review) | Data plane review gates |
| REST API spec review | [REST API spec review process](https://eng.ms/docs/products/azure-developer-experience/design/api-review) | Architect board review flow |
| SDK API review (bridge) | [Arch board review process](https://github.com/Azure/azure-sdk/blob/main/.github/workflows/src/arch-board-review/ARCH-BOARD-REVIEW-PROCESS.md) | GitHub Form-based review requests — **bridge** until API Review Hub ships |
| API Review Hub | TBD | Synthetic GitHub PRs with `API.md` diffs replacing APIView (in development) |
| Mgmt plane release | [Management plane SDK release process](https://eng.ms/docs/products/azure-developer-experience/plan/mgmt-sdk-release-process) | Service team + SDK team responsibilities |
| SDK PR readiness gaps | [Tracking issue #15705](https://github.com/Azure/azure-sdk-tools/issues/15705) | Consolidated gaps: late validation, manual fixes, pipeline delays |
| Release plan dashboard | [Release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard) | Track release progress |

---

#### Stage 1: TypeSpec Authoring

Developer writes/updates TypeSpec locally: `.tsp` files, `tspconfig.yaml`, local compilation, linting, agent-assisted feedback. The API version lifecycle (private preview → public preview → stable) determines the TypeSpec authoring approach — new specs start at preview, promotions to stable require breaking change review.

| Tool | Role | Owner |
|------|------|-------|
| TypeSpec compiler | Compile `.tsp` files, catch syntax/type errors | TypeSpec team |
| TypeSpec linter | Static guideline compliance (distinct from LintDiff) | TypeSpec team |
| TypeSpec authoring agent (`azure-typespec-author` skill) | Assist with ARM/data-plane patterns, Azure REST API guidelines | Haoling |
| `@azure-tools/typespec-breaking-change` | Phase A: same-version regression (any diff = error). Phase B: cross-version evolution (request narrowing / response widening = breaking). Inline suppression via decorators. Also runs in Stage 2 CI. | Mark Cowlishaw |

**✅ Supported today**: TypeSpec compiler, authoring agent, breaking change tool, linter all functional. Agent can assist with authoring patterns.

**⚠️ Gap**: Breaking change tool reports findings with DiffKind, source location, and suggested suppression decorator — but the user must apply fixes manually. No agent currently auto-resolves these findings.

**🎯 Next step**: Build author-validation loop where agent auto-applies suppression decorators based on structured breaking change output. Long-term aspirational goal: agent detects existing workflow state and resumes from the appropriate step (hard problem — ok to mention as inspirational, not a near-term priority).

---

#### Stage 2: Spec PR Validation

PR in `azure-rest-api-specs` triggers: compilation → LintDiff → breaking change detection (Phase A + B) → APIView token generation → SDK generation dry-run per language → labels applied. **Note: There is no spec-level API review.** API review (APIView / ARH) happens only at the SDK level (Stage 4).

| Tool | Role | Owner |
|------|------|-------|
| Spec PR validation pipeline | Orchestrates full validation suite on PR open/update | EngSys |
| TypeSpec compiler | CI compilation | TypeSpec team |
| TypeSpec Lintdiff | TypeSpec-native linting on PR diffs — validates guideline compliance, replacing older Swagger-based Spectral LintDiff. Includes a suppression process for known exceptions (linter-based, see @catalinaperalta). | EngSys / TypeSpec team |
| `@azure-tools/typespec-breaking-change` | Phase A + B detection. Auto-adds `BreakingChangeReviewRequired` / `VersioningReviewRequired` labels | Mark |
| APIView emitter (`typespec-apiview`) | Generates API surface token files for SDK-level architect review (tokens used at Stage 4). **Will be retired with ARH.** | APIView team |
| spec-gen-sdk | SDK generation validation — ensures spec can produce SDK code | EngSys (Renhe) |
| Avocado / OAV | Legacy Swagger-based validation tools — **being deprecated** as TypeSpec-native tooling (Lintdiff, breaking change detector) replaces them. Tied to @timotheeguerin's work on example and readme validation. | EngSys |

**Labels applied at this stage** (spec PR):

| Label | Trigger | Next step |
|-------|---------|-----------|
| `BreakingChangeReviewRequired` | Breaking change detected by CI | Routes to breaking change review team (ARM only). Blocks merge until approved or `Approved-BreakingChange` applied. |
| `VersioningReviewRequired` | Versioning issue detected by CI | Blocks merge until review team approves. |
| `ARM-Review-Required` | ARM spec detected by CI | Routes to ARM review team. Blocks merge until `ARMSignedOff` applied. |
| `namespace-<lang>-pending` | New namespace in `tspconfig.yaml` detected by CI | Blocks merge until architect applies `namespace-<lang>-approved`. |
| `Suppression-Approved` | Linter suppression reviewed | Suppression validation automated. |

**✅ Supported today**: Full validation pipeline runs on PR open/update. Labels auto-applied. APIView tokens generated. spec-gen-sdk dry-run validates generation. TypeSpec Lintdiff operational with suppression process.

**⚠️ Gaps**:
1. Validation steps run independently. No designed chain for: failure ordering, how breaking change results gate SDK generation, per-language generation failure reporting back to spec PR. No unified PR comment summarizing all results with next steps.
2. **No endpoint liveness verification** — for public specs, there is no check that the API endpoint is tested and live before the spec PR is merged. If the endpoint is not deployed, the SDK will be generated for an API that doesn't work yet. This should be a pre-merge gate or at minimum a documented prerequisite.
3. **Avocado/OAV deprecation** — Legacy Swagger validation tools still run in CI but should be removed as TypeSpec-native tooling matures.

**🎯 Next steps**:
- Design unified PR comment that aggregates all validation results with clear next actions per failure type.
- *(Aspirational)* Investigate endpoint liveness/testing verification as a pre-merge gate for public specs (hard problem per @prkannap, ok to mention as inspirational).

---

#### Stage 3: SDK Generation

**Trigger**: When a spec PR is merged in `azure-rest-api-specs`, a two-stage pipeline **automatically** runs:
1. **Stage 1 (automatic)**: Creates or finds the release plan work item
2. **Stage 2 (automatic)**: Triggers SDK generation for each configured language, creates SDK PRs in the corresponding language repos (e.g., `azure-sdk-for-python`), and links them to the release plan

The `azsdk_run_generate_sdk` tool drives this via `SpecWorkflowTool`. If an existing SDK PR is already open for that language (linked in the release plan), the pipeline pushes to the same branch rather than creating a new PR.

For each target language: tsp-client syncs TypeSpec → emitter generates code → customizations applied → build → test (playback) → metadata updated → SDK PR created and linked to release plan.

> **Note**: Local SDK generation via azsdk-cli (`azsdk_package_generate_code`) is still available for developer iteration but is no longer the primary path. The goal is for service teams to focus on TypeSpec — SDK generation and release is automated.

| Tool | Role | Owner |
|------|------|-------|
| tsp-client | Syncs TypeSpec project into SDK repo, manages `tsp-location.yaml` | EngSys |
| Language emitters | Generate client library code from TypeSpec (one per language) | Language teams |
| spec-gen-sdk | Pipeline automation — runs full generation workflow, creates SDK PRs | EngSys (Renhe) |
| azsdk-cli (`azsdk_package_generate_code`) | Local orchestration — generate, build, test, validate | azsdk-cli team |
| azsdk-cli (`azsdk_package_build_code`, `azsdk_package_run_tests`, `azsdk_package_run_check`) | Build, test, validate locally | azsdk-cli team |
| azsdk-cli (`azsdk_package_update_changelog_content`, `azsdk_package_update_metadata`, `azsdk_package_update_version`) | Update package metadata | azsdk-cli team |
| `azsdk_customized_code_update` | Apply TypeSpec and code-level customizations (classify → fix → regenerate → rebuild) | azsdk-cli team |

**✅ Supported today**: Two-stage pipeline (release plan → SDK gen) runs on spec merge. SDK PRs auto-created and linked to release plan. `azsdk_customized_code_update` applies customizations. Local generation available via azsdk-cli.

**⚠️ Gap**: When SDK generation fails, spec-gen-sdk reports a failed pipeline check. The error is buried in build logs — not surfaced as a structured report (which language, which step, what error). No agent helps troubleshoot generation failures. The auto-repair pattern used at Stage 4 (label → Copilot agent → fix → rebuild) could potentially be extended here to diagnose common generation failures.

**🎯 Next step**: Structured error reporting from generation pipeline + agent-assisted troubleshooting for common failures.

---

#### Stage 4: SDK PR Validation & API Review

CI runs (build → test → lint → package validation → SDK breaking change detection). If build fails on custom code drift: `auto-sdk-build-fix` label → Copilot cloud agent auto-repairs → commits fix → CI re-runs. **SDK API review happens here** — architects review the generated SDK public API surface (not at spec level). Approval tracked in ADO Package Work Items via ARH (labels are informational).

| Tool | Role | Owner |
|------|------|-------|
| Language CI pipelines | Build, test, lint, package validation | Language teams |
| SDK breaking change detector | Detects breaking changes in generated SDK API surface. Being combined into SDK validation check. Could the auto-repair pattern (below) also apply here for resolution? | Ray & Crystal |
| APIView (current) | SDK public API surface review — architects review and approve via web UI | APIView team |
| **API Review Hub** (replacing APIView) | Creates synthetic review PRs in language repos with generated `API.md` diffs. PRs are never merged — exist only for architect review. Architects auto-assigned as reviewers. Approval recorded in ADO Package Work Items (API hash). CI gates release by checking approved hash. Uses `azure-sdk-automation` GitHub App. | @tjprescott |
| API review feedback resolution agent (`azsdk-common-apiview-feedback-resolution` skill) | Helps resolve API review comments via TypeSpec changes | azsdk-cli team |
| Pipeline troubleshooting agent (`azsdk-common-pipeline-troubleshooting` skill) | Diagnoses CI failures | azsdk-cli team |
| Auto SDK PR repair (`azsdk_customized_code_update` + Copilot cloud agent) | When custom code drifts and breaks the build, `auto-sdk-build-fix` label triggers a Copilot cloud agent to fix custom code, regenerate, and rebuild. Shared orchestration in `eng/common/`, per-language opt-in. | azsdk-cli team |

**Labels applied at this stage** (SDK PR):

| Label | Trigger | Next step |
|-------|---------|-----------|
| `auto-sdk-build-fix` | CI failure on custom code drift (or manual) | Copilot cloud agent auto-repairs, commits fix, CI re-runs. |
| `<lang>-api-approved` | ARH assigns after architect approval | **Informational only.** Source of truth is ADO Package Work Items (API hash). Signals to humans that API review passed. |
| `release-plan-linked` | Automation when release plan linked | Shanghai team reviews only PRs with this label (mgmt plane). |
| `ready-for-review` | GitHub Form workflow | Triggers architect board review process (bridge until ARH). |
| `review-out-of-date` | API Review Hub | New changes on working branch not reflected in review PR. 🔜 Not yet in production. |
| `architecture-review-needed` | API Review Hub | Flags SDK PR for architect review. 🔜 Not yet in production. |

**✅ Supported today**: Language CI pipelines, APIView feedback resolution agent, pipeline troubleshooting agent, auto SDK PR repair (label-triggered). SDK breaking change detection being integrated. API Review Hub has end-to-end prototype working for Python.

**⚠️ Gap**: SDK breaking change detection integration in progress (being combined into validation check). Auto-repair only handles custom-code drift — not all CI failure types. API Review Hub review PR creation on generated SDK PRs is not automated — mechanism TBD (open design gap). API review feedback resolution agent needs evaluation for ARH compatibility. Release gates transitioning from APIView → both → ARH only.

**🎯 Next step**: Complete SDK breaking change integration into CI. Extend auto-repair pattern to cover more failure categories. Transition API review from APIView to API Review Hub (GitHub-native review PRs with release-gating).

---

#### Stage 5: Release Coordination

Release plan work item created/updated → namespace approval (if new package) → readiness checked per language → release pipeline triggered (manual approval gate) → **release gate check via API Review Hub** (`GET /api/releases/check-gate` verifies API approval before release proceeds) → packages published → release plan auto-completes → Service Tree KPI updated.

| Tool | Role | Owner |
|------|------|-------|
| Release plan tooling (`azsdk_create_release_plan`, `azsdk_get_release_plan`, etc.) | Create/update/link Azure DevOps work items | azsdk-cli team |
| Changelog/versioning tool | Automates changelog updates and version management for SDK packages. For **management plane**, changelog is auto-generated reliably (compares SDK with latest GA release). For **data-plane**, auto-generated changelog is not reliable and may require manual curation (see [discussion](https://github.com/Azure/azure-sdk-tools/pull/15248#discussion_r3353097483)). | @jsquire |
| Release pipeline (`azsdk_release_sdk`) | Check readiness, trigger release | azsdk-cli team |
| **API Review Hub release gate** (`GET /api/releases/check-gate`) | Verifies package version has approved API hash before release pipeline proceeds. After release, `POST /api/releases/mark-released` records release and closes review PRs. | @tjprescott |
| Language release pipelines | Publish to PyPI, Maven, npm, NuGet, Go module proxy | Language teams |
| Service Tree integration | Mark service KPIs as completed | EngSys |

**✅ Supported today**: Release plan tooling (create/update/link), `azsdk_release_sdk` readiness check, language release pipelines, Service Tree KPI update, auto-release for configured packages.

**⚠️ Gap**: Release has two distinct processes:

| Process | What | Owner | Status |
|---------|------|-------|--------|
| **SDK PR readiness** (before merge) | Make SDK PR release-ready: fix linter failures, test failures, merge conflicts, breaking changes, update changelog/metadata. Tracked in [#15705](https://github.com/Azure/azure-sdk-tools/issues/15705). | Praveen / Language teams / EngSys | Multiple items open — see issue |
| **Release trigger** (after merge) | Auto-trigger release pipeline when SDK PR merges. Once merged, changelog and metadata are already done — just need to release. | @raych1 | In progress — becoming automatic |

> **Key clarification**: Changelog, metadata, and version updates happen *inside the SDK PR before merge* — they are part of SDK PR readiness. After merge, the only step is triggering the release pipeline. Manual approval gate on the release pipeline cannot be removed for security (ARM approval = Shanghai team).

**🎯 Next step**: Start with phase 2 (auto-trigger release pipeline on SDK PR merge) as it's simpler and speeds up the process immediately. Phase 1 (SDK PR readiness automation) follows.

**Release type approval differences**:

| Release Type | Approval Gates | Notes |
|-------------|----------------|-------|
| Preview (first) | Namespace approval required | Fastest path; namespace approval needed for new packages |
| Preview (update) | No architect board review (can be requested) | Fastest path |
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
| 9 | Release is a two-phase manual process | 5 | Phase 1 (SDK PR readiness: changelog, breaking changes) and Phase 2 (release trigger) both require manual action | Phase 2 first: auto-trigger release pipeline on SDK PR merge. Phase 1 later: automate changelog + readiness | @raych1 / TBD |
| 10 | ARM vs data plane process divergence undocumented | 2, 4 | ARM and data plane follow same high-level flow but diverge at review gates; no single doc captures differences | Linked process docs for each plane with clear divergence points | Sameeksha + Praveen |
| 11 | No endpoint liveness verification before spec PR merge | 2 | For public specs, no check that the API endpoint is tested and live before merge. SDK may be generated for an undeployed API. | Pre-merge gate or documented prerequisite verifying endpoint is tested and live | TBD |
| 12 | No auto-release after SDK PR merge | 5 | Auto SDK PR creation is actively being built (release plan → SDK generation), but auto-release after merge is undefined. This is the last missing piece for full end-to-end automation (spec merge → release plan → SDK generation → SDK PR → merge → publish). | SDK PR merge triggers changelog date update + release pipeline automatically for configured packages | TBD |
| 13 | Late spec validation — issues detected at SDK PR stage | 2, 4 | Namespace approval, API version, and spec branch issues are only caught during SDK PR review instead of at spec PR stage | Validate namespace, API version, and spec branch during spec PR CI — fail early before SDK generation | Praveen / spec-gen-sdk |
| 14 | SDK PR not fully ready for review after generation | 4 | Auto-generated SDK PRs require manual fixes: linter failures (especially samples), recorded test failures (API/version changes), merge conflicts, title/description not updated on re-generation | SDK PRs are generated ready-to-merge: linter-clean, tests passing, no conflicts, metadata updated | Praveen / Language teams |
| 15 | Release pipeline provisioning delay for new RPs | 5 | Pipelines for newly onboarded resource providers are created asynchronously (overnight/weekend batch), delaying release readiness | CI-triggered `prepare-pipelines` pipeline eliminates provisioning delay | EngSys |
| 16 | API Review Hub review PR creation not automated | 4 | ARH review PRs on generated SDK PRs must be opened manually — no automation triggers creation today. Mechanism TBD (could be triggered by SDK PR creation). | SDK PR creation automatically triggers ARH review PR in language repo | @tjprescott |
| 17 | API review feedback resolution agent needs ARH compatibility | 4 | Current agent resolves APIView comments. Needs evaluation for ARH's GitHub-based review comments (should be easier since GitHub is more accessible than APIView API). | Agent works with both APIView and ARH review comments | azsdk-cli team |
| 18 | Release gate transition from APIView to ARH | 5 | Release gates currently check APIView for API approval. Must transition to accept both APIView and ARH, then eventually ARH only. | Release pipeline checks ARH approval state (API hash in ADO) | @tjprescott / EngSys |

> **See also**: [SDK PR Release Readiness tracking issue (#15705)](https://github.com/Azure/azure-sdk-tools/issues/15705) for the full consolidated gap list including .NET-specific gaps, ESRP publishing failures, and network isolation policy impacts.

---

## Success Criteria

This workflow is complete when:

- [ ] Users can complete full TypeSpec → SDK release workflow with agent guidance
- [ ] Agent detects existing state and resumes from appropriate step
- [ ] Release plan is automatically updated at each step
- [ ] All sub-skills integrate seamlessly without context loss between stages
- [ ] Local and pipeline SDK generation paths both work
- [ ] Breaking change findings from CI are surfaced clearly to the user
- [ ] API review feedback can be resolved within the workflow
- [ ] Works for all tier-1 SDK languages (per language scope table)
- [ ] Errors at every stage produce structured, actionable guidance
- [ ] Service Tree KPI is updated on release completion

---

## Exceptions and Limitations

### Exception 1: Architect Board Review

**Description**: First GA releases require architect board review (human decision-making outside automated workflow). First preview releases require namespace approval for new packages. Subsequent preview releases do not require review but can request one. See updated [API review process](https://github.com/Azure/azure-sdk/blob/main/.github/workflows/src/arch-board-review/ARCH-BOARD-REVIEW-PROCESS.md).

**Impact**: Workflow cannot fully automate GA approval or first preview namespace approval.

**Status**: Three workstreams converging on this:
1. **GitHub Forms + Actions (PR #10037, shipped)** — Service teams submit review requests via GitHub Form in `azure-sdk` repo (replacing email). `arch-board-review.yml` template is an explicit **bridge** until API Review Hub ships. `namespace-review.yml` stays long-term. `approval-close.yml` validates authorized approvers and auto-closes issues.
2. **API Review Hub (PR #15789, in progress)** — Replaces APIView with synthetic GitHub PRs in language repos. When this ships, `arch-board-review.yml` can be retired. Namespace approvals remain out of scope for API Review Hub.
3. **Spec PR-based namespace approval (in progress, [PR #44085](https://github.com/Azure/azure-rest-api-specs/pull/44085))** — Namespace approval moves to the spec PR itself. When spec PR merges, namespace is considered approved. Would eventually retire `namespace-review.yml` too and simplify E2E: one approval gate (spec merge) unlocks everything.

### Exception 2: Breaking Change Reviews

**Description**: SDKs with breaking changes require API breaking change review team approval. This is a separate review process with its own team and labels.

**Impact**: Breaking change releases blocked until review team approves.

**Workaround**: Agent helps prepare suppression decorators with clear reasons, guides review request.

### Exception 3: Package Naming Approval

**Description**: New SDK packages require namespace/naming approval before release.

**Impact**: New package releases blocked until naming approved.

**Status**: Being automated as a merge gate directly on spec PRs in `azure-rest-api-specs`. When a PR modifies `tspconfig.yaml`, the workflow extracts namespaces using `@azure-tools/typespec-metadata` emitter, applies `namespace-<lang>-pending` labels, and blocks merge until authorized architects apply `namespace-<lang>-approved` labels (or `namespace-approved-all` for management plane). Replaces the previous manual email-based triage. Approval resets automatically if namespace values change after approval. Longer-term, namespace approval may move entirely to the spec PR flow — when spec PR merges, namespace is considered approved, and SDK generation proceeds automatically without a separate approval gate.

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

---

## Appendix: Detailed Flowchart

The following detailed flowchart shows every step, decision branch, and gap in the end-to-end flow. For a high-level view, see the [sequence diagram](#high-level-sequence-diagram-service-team-journey-spec-pr-entry-point) above.

```
                         ┌─────────────────────────────────┐
                         │  ENTRY POINT A                  │
                         │  User provides initial prompt   │
                         │  (starts at Stage 1)            │
                         └──────────────┬──────────────────┘
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
  Gap: Breaking change findings require manual fix.         │
  No agent auto-resolves suppression decorators.            │
                                                            │
                                                            ▼
              ┌───────────────────────────────────────────────────┐
              │  STEP 3: Open API Spec PR                        │
              │  (agent can help create the PR)                  │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │  Namespace Approval                               │
              │  (new packages only — triggered by spec PR)      │
              │  • Extracts namespaces from tspconfig.yaml       │
              │  • Blocks merge until architect approves         │
              └───────────────────────┬───────────────────────────┘
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
│ (re-triggers pipeline)  │                     └───────────┬─────────────┘
└─────────────────────────┘                                 │
                                                           ▼
             ┌───────────────────────────────────────────────────┐
             │  STEP 4b: ARM Review & Breaking Change Review     │
             │  • ARM review (ARM specs only — architect signs   │
             │    off on resource model correctness)             │
             │  • Breaking change review (ARM specs only)        │
             │  • Note: NO spec-level API review. API review     │
             │    happens at SDK level (Stage 4).                │
             │  • Both triggered by spec PR, block merge         │
             └───────────────────────┬───────────────────────────┘
                                     │
                                     ▼
             ┌───────────────────────────────────────────────────┐
             │  PR approved & merged                             │
             │  (namespace approval + ARM review complete)       │
             └───────────────────────┬───────────────────────────┘
                                                           │
  Gap: Validation steps run independently.                  │
  No unified PR comment with all results + next steps.      │
  No designed failure ordering.                             │
                                                           │
                                                           ▼
                        ┌─────────────────────────────────┐
                        │  ENTRY POINT B                  │
                        │  Spec PR merge triggers         │
                        │  automated SDK generation       │
                        │  (skips Stages 1–2)             │
                        └──────────────┬──────────────────┘
                                       │
                                       ▼
╔═══════════════════════════════════════════════════════════════════╗
║  STAGE 3: Automated SDK Generation (per language)                ║
║  Trigger: spec PR merge                                         ║
╚═══════════════════════════════════════════════════════════════════╝
                                    │
              ┌───────────────────────────────────────────────────┐
              │  Pipeline runs automatically:                     │
              │  1) Create/find release plan (auto)              │
              │  2) Generate SDK per configured language          │
              │  3) Apply customizations                         │
              │  4) Create SDK PRs & link to release plan        │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │ PASS                                                │ FAIL
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ SDK PRs created &       │                     │ Report failure,         │
│ linked to release plan  │                     │ retry or escalate       │
│ automatically           │                     │ (loop back to fix       │
└───────────┬─────────────┘                     │  TypeSpec & retry)      │
            │                                   └─────────────────────────┘
                                      │
                                      ▼
╔═══════════════════════════════════════════════════════════════════╗
║  STAGE 4: SDK PR Validation & API Review                         ║
╚═══════════════════════════════════════════════════════════════════╝
                                    │
              ┌───────────────────────────────────────────────────┐
              │  STEP 5: SDK PR CI Pipeline                      │
              │  (auto-triggers on SDK PR open/update)           │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │ • Build → Test → Lint → Package validation       │
              │ • SDK breaking change detection                   │
              │ • APIView generated for SDK public API surface    │
              │   (future: API Review Hub review PR created)      │
              │ • Labels: auto-sdk-build-fix (if drift),          │
              │   <lang>-api-approved (informational, from ARH)   │
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
              │  STEP 6: SDK API Review (SDK-level only)          │
              │  Architects review generated SDK API surface      │
              │  via APIView (current) or ARH review PRs (future) │
              │  Approval tracked in ADO (API hash), not labels   │
              └───────────────────────┬───────────────────────────┘
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           │ Has suggestions                                     │ No suggestions
           ▼                                                     ▼
┌─────────────────────────┐                     ┌─────────────────────────┐
│ Resolve API review      │                     │ Approved                │
│ suggestions             │                     │                         │
│ [API Review Feedback    │                     │                         │
│  Skill]                 │                     │                         │
└───────────┬─────────────┘                     └───────────┬─────────────┘
            │                                               │
            ▼                                               │
┌─────────────────────────┐                                 │
│ Changes required →      │                                 │
│ Update TypeSpec →       │                                 │
│ Re-generate SDK →       │                                 │
│ New commit to PR →      │                                 │
│ CI re-runs (loop back   │                                 │
│ to Step 5)              │                                 │
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
              │  STEP 7: Release SDKs                            │
              │  [Release Skill]                                 │
              └───────────────────────┬───────────────────────────┘
                                      │
              Note: Changelog, metadata, and version updates
              are done INSIDE the SDK PR before merge.
              After merge, only the release trigger is needed.
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │  7a. Release trigger                              │
              │  • Auto-release (@raych1, becoming automatic)     │
              │  • OR manual trigger via azsdk_release_sdk        │
              │  • Manual approval gate (security, cannot remove) │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │  7b. Release gate check                           │
              │  • API Review Hub: GET /api/releases/check-gate   │
              │    verifies approved API hash                     │
              └───────────────────────┬───────────────────────────┘
                                      │
                                      ▼
              ┌───────────────────────────────────────────────────┐
              │  7c. Post-release (AUTOMATED)                    │
              │  • Packages published to registries              │
              │  • Release plan auto-completes                   │
              │  • Service Tree KPI updated                      │
              └───────────────────────────────────────────────────┘

  SDK PR readiness gaps (before merge) tracked in #15705:
  linter failures, test failures, merge conflicts, pipeline delays.
  Release trigger (@raych1) becoming automatic after SDK PR merge.
```
