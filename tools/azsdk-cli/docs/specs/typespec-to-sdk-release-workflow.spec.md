# TypeSpec-to-SDK Release Workflow Spec

---

## Navigation

| Need | Jump |
|------|------|
| Workflow overview | [Overview](#workflow-overview) |
| Workflow map | [Workflow map](#workflow-map) |
| Stage details | [Stage modules](#stage-modules) |
| Labels & gates | [Cross-cutting contracts](#cross-cutting-contracts) |
| Open decisions | [Decision log](#decision-log) |
| Known gaps | [Gap tracker](#gap-tracker) |

---

## Workflow overview

### Scope

The end-to-end process from TypeSpec authoring → spec PR → SDK generation → SDK PR validation → release.

> **Scope note**: This document covers the **spec-change-triggered** release path. Data shows ~50% of SDK releases (Python, .NET) happen **without a spec change** — for example, customization-only updates, bug fixes, or dependency bumps. That flow shares Stages 4–5 (SDK PR validation → release) but skips Stages 1–3. Additionally, some packages have **no spec at all** (messaging services, companion packages, language ecosystem helpers). These still require namespace/naming approval and architect review but enter the workflow at Stage 4.
>
> **Open question**: Should the no-spec-change and no-spec paths be documented as first-class entry points in this document? See also [#16297](https://github.com/Azure/azure-sdk-tools/issues/16297).

> 🧑‍💻 = requires human approval or manual gating

### The five-stage workflow

```mermaid
flowchart TD
    A["1. TypeSpec Authoring"] --> B["2. Spec PR Validation"]
    B --> C["3. SDK Generation (automatic)"]
    C --> D["4. SDK PR Validation & API Review"]
    D --> E["5. Release Coordination"]
```

### Service team journey

1. **Author TypeSpec**
   - Write `.tsp` files and `tspconfig.yaml` locally
   - Or use `azure-typespec-author` agent skill for guided authoring
   - Compile and lint locally before opening a PR

2. **Open a spec PR**
   - Push to `azure-rest-api-specs` and open a PR
   - CI validates automatically:
     - TypeSpec compilation (includes linting)
     - Suppression review (suppressed lint rules + suppressions.yaml changes)
     - Breaking change detection (spec-level: same-version + cross-version)
     - SDK breaking change detection (management plane only, detects SDK API surface breaks)
     - APIView token generation (for SDK-level review at Stage 4)
     - SDK generation dry-run (spec-gen-sdk)
     - Labels applied based on results

3. 🧑‍💻 **Wait for approvals** *(human gating)*
   - **`PublishToCustomers` label** — required on all PRs targeting `main` or `RPSaaSMaster`. Author self-applies to acknowledge APIs are shipped to customers. Enforced by CI (`summarize-checks`). **Candidate for removal** -- redundant once other gates pass; causes service team confusion.
   - **Namespace approval** — required for first preview of new packages (`namespace-<lang>-pending` → `namespace-<lang>-approved`)
   - **ARM validation** (owned by ARM, non-exhaustive) -- includes ARM review board (`ARM-Review-Required` -> `ARMSignedOff`), breaking change review, and modeling/leasing review. ARM controls what checks apply to their specs.
   - **Breaking change review** -- Spec-level: `BreakingChangeReviewRequired` label auto-applied by CI on any spec PR with breaking changes (review team defined for ARM, undefined for data-plane -- Gap #9). SDK-level: `BreakingChange-{Language}-Sdk` label currently applies to management plane spec PRs only.
   - > **Note**: There is no spec-level API review. API review happens only at the SDK level (Stage 4).

4. **Spec PR merges → SDK generation is automatic (management plane)**
   - Release plan work item created (or existing one found for the TypeSpec project + API type)
   - SDK code generated per language via emitters. SDK PR is created automatically for management plane only. Service teams generate SDK manually using azsdk agent for data-plane.
   - The SDK PR is unique per release plan -- subsequent spec PRs (e.g., client.tsp customizations) regenerate into the same SDK PR
   - Customizations applied (`client.tsp` + code customizations)
   - SDK PRs opened in each language repo and linked to release plan
   - Generation failures visible on [release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard)

5. 🧑‍💻 **SDK PR review & approval** *(human gating)*
   - SDK CI runs automatically:
     - Build → test → lint → package validation
     - SDK breaking change detection
     - APIView generates SDK API surface review (current) or API Review Hub creates review PR (future)
   - **API review**: Architects review generated SDK public API surface
     - `<lang>-api-approved` labels (current) are **informational** — source of truth is the ARH database (API hash)
     - ARH will assign `api-approved` label on SDK PRs automatically when architect approves (replacing per-language labels)
   - **Auto-repair**: `auto-sdk-build-fix` label triggers Copilot agent to fix custom code drift
   - **ARM SDK PRs**: Service team approves SDK PRs (Shanghai team may also review, to be verified)

6. 🧑‍💻 **Release** *(human gating)*
   - The release pipeline is automatically triggered when an SDK pull request is merged, provided the `auto-release` label is applied to the pull request. For ARM, `auto-release` is auto-applied.
   - Manual approval gate required for security (cannot be removed; ARM approval = service team)
   - Release gate: API Review Hub verifies approved API hash
   - Packages published → release plan completes → Service Tree KPI updated
   - **APEX/CPEX KPI auto-check** (ARM): SDK release auto-triggers KPI verification in the APEX/CPEX process. This is the service team's stamp that SDKs were shipped correctly. 7 KPIs cover onboarding (1), plus private/preview/GA milestones (3 ARM, 3 data-plane). Details: @justinprescott / @prkannap.
   - GitHub.io docs automation creates PR in azure-sdk repo to update package index (aka.ms/azsdk). Reviewed and merged by each language team.

### For Reviewers

1. **ARM validation** (ARM specs only) -- ARM owns their validation gates. Includes ARM review board (`ARMSignedOff`), breaking change review, and modeling/leasing review. Non-exhaustive -- ARM can add checks as needed.
2. **SDK API review** — Review generated SDK API surface on SDK PRs via APIView (current) or API Review Hub review PRs (future). **There is no spec-level API review** — API review applies only to the generated SDK. **Current**: `<lang>-api-approved` labels applied manually by architects. **Future (ARH)**: `api-approved` label applied automatically via webhooks/GH App. Labels are **informational** — source of truth is the ARH database (API hash).
3. **SDK PR review** (management plane) -- Service team approves SDK PRs for ARM. Shanghai team may also review (to be verified).
4. **Breaking change review** — Spec-level: `BreakingChangeReviewRequired` label on spec PRs (review team defined for ARM; data-plane routing is Gap #9). SDK-level: `BreakingChange-{Language}-Sdk` label on management plane spec PRs only.
5. **Namespace review** — Approve new package namespaces; apply `namespace-<lang>-approved` labels
6. **Release approval** -- Approve release pipeline runs (service team for ARM)

### For EngSys / SDK Team

1. **Monitor pipelines** — Spec PR validation, SDK generation, and SDK CI pipelines run automatically
2. **Label routing** — Labels like `BreakingChangeReviewRequired`, `namespace-<lang>-pending`, and `auto-sdk-build-fix` trigger review routing and automation
3. **Generation failures** — When SDK generation fails, failures are visible on [release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard). Structured error reporting for agent troubleshooting is a [known gap](#gap-tracker).
4. **Auto-repair** — `auto-sdk-build-fix` label triggers Copilot cloud agent to fix custom code drift on SDK PRs
5. **Release coordination** — `azsdk_release_sdk` checks readiness; release pipelines publish to package registries (manual approval gate required)
6. **Track progress** — [Release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard) shows where each service is in the process


---

## Workflow map

### Stage summary

| Stage | Entry signal | Exit signal | Primary owner |
|-------|-------------|------------|---------------|
| 1. TypeSpec Authoring | Local TypeSpec change | PR-ready `.tsp` files | TypeSpec team / @prkannap |
| 2. Spec PR Validation | PR opened in `azure-rest-api-specs` | CI pass + labels + approved | @raych1 / @chunyu3 / @catalinaperalta / EngSys |
| 3. SDK Generation | Spec PR merged | SDK PRs created per language | spec-gen-sdk / EngSys |
| 4. SDK PR Validation | SDK PR opened in language repo | PR approved & merged | Language teams / architects |
| 5. Release Coordination | SDK PR merged | Packages published + KPI updated | Release tooling / EngSys |

### Sequence diagram

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│ Service  │     │ Spec Repo│     │   CI     │     │Reviewers │     │SDK Repos │     │ Release  │
│  Team    │     │  (GH)    │     │Pipeline  │     │(ARM/API) │     │(per lang)│     │ Pipeline │
└────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘
     │                 │                │                │                │                │
     │  Open spec PR   │                │                │                │                │
     │────────────────>│  CI triggers   │                │                │                │
     │                 │───────────────>│                │                │                │
     │                 │                │── Compile      │                │                │
     │                 │                │── Suppression  │                │                │
     │                 │                │── Breaking chg │                │                │
     │                 │                │── APIView gen  │                │                │
     │                 │                │── SDK dry-run  │                │                │
     │                 │                │── Labels apply │                │                │
     │                 │                │                │                │                │
     │  [If PASS]      │  Request reviews               │                │                │
     │                 │───────────────────────────────->│                │                │
     │                 │                │                │  Reviews       │                │
     │                 │  Approved → MERGE               │                │                │
     │                 │                │                │                │                │
     │                 │  ─ ─ ─ ─ AUTOMATED FROM HERE ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ │
     │                 │                │                │                │                │
     │                 │  Merge event   │                │                │                │
     │                 │───────────────>│── Release plan │                │                │
     │                 │                │── Generate SDK │                │                │
     │                 │                │───────────────────────────────────────>│         │
     │                 │                │                │                │  SDK PRs       │
     │                 │                │                │                │── SDK CI runs  │
     │                 │                │                │                │── API review   │
     │                 │                │                │                │                │
     │  [If build fail on custom code] auto-repair via Copilot          │                │
     │  [If API review feedback] → resolve via TypeSpec changes         │                │
     │                 │                │                │                │  SDK PR merged │
     │                 │                │                │                │                │
     │  Trigger release│                │                │                │       ┌───────>│
     │──────────────────────────────────────────────────────────────────────────┘        │
     │                 │                │                │                │   Publish pkgs │
     │  ✅ Done!       │                │                │                │   Update KPI   │
     │<────────────────────────────────────────────────────────────────────────────────────│
```

**Key insight**: After spec PR merge, the flow is largely **automated**. Service team re-engages only if SDK CI fails, API review has feedback, or release needs manual approval.

> **ARM vs Data Plane divergence** — Same high-level flow but diverge at review gates. ARM requires ARM review sign-off + stricter resource model constraints. Data plane skips ARM review.
>
> **Open question: Data-plane review model** — With the stewardship board being reconsidered, who reviews data-plane PRs? An emerging pattern: for ARM, if the REST API spec looks fine, we assume the SDK is ok to ship. For data-plane, if the SDK looks fine, we could assume the TypeSpec spec is ok to merge. This would mean ARM quality flows spec → SDK, while data-plane quality flows SDK → spec.

---

## Stage modules

<a name="stage-1-typespec-authoring"></a>
### Stage 1 — TypeSpec Authoring

#### Quick card

| Field | Value |
|-------|-------|
| **Purpose** | Author/update TypeSpec locally, validate, prepare for spec PR |
| **Entry signal** | Service team has API requirements |
| **Exit signal** | `.tsp` files compile, lint passes, ready for PR |
| **Owners** | TypeSpec team (compiler/linter/breaking change), Shanghai (authoring agent) |
| **Reviewer ask** | Confirm tool list and breaking change workflow |

#### Happy path

1. Write/update `.tsp` files and `tspconfig.yaml`
2. Compile locally with TypeSpec compiler
3. Run linter checks
4. Run breaking change detection (same-version + cross-version)
5. Ready to open spec PR

#### Failure paths

| Failure | Signal | Owner | Next action | Resolution |
|---------|--------|-------|-------------|------------|
| Compile error | TypeSpec compiler error | Author | Fix `.tsp` syntax/types | Manual |
| Linter violation | Linter warning/error | Author | Fix or suppress with decorator | Manual |
| Breaking change detected | Tool report with DiffKind + source location | Author | Apply suppression decorator or redesign. Agent can suggest suppressions, categorize break type, and invoke authoring skill for alternatives. | Agent-assisted |

<details>
<summary>Deep spec: tools, contracts, and unresolved questions</summary>

#### Tool contract

| Tool | Role | Owner |
|------|------|-------|
| TypeSpec compiler | Compile `.tsp` files, catch syntax/type errors. Lint rules run during compile. | TypeSpec team |
| TypeSpec authoring agent (`azure-typespec-author` skill) | Assist with ARM/data-plane patterns, Azure REST API guidelines | Haoling/Shanghai |
| `@azure-tools/typespec-breaking-change` | Same-version regression + cross-version evolution detection. Inline suppression via decorators. Agent can suggest appropriate suppressions and categorize breaks (correction, intentional evolution, inadvertent). | @markcowl |

#### Gap

Breaking change tool reports findings as diagnostics (diagnostic type + target location). Resolution is manual today; agent-assisted suppression is a future goal.

#### Next step

Build author-validation loop where agent auto-applies suppression decorators based on diagnostic type and target info. Structured JSON output is available from the tool if richer detail is needed.

</details>

---

<a name="stage-2-spec-pr-validation"></a>
### Stage 2 — Spec PR Validation

#### Quick card

| Field | Value |
|-------|-------|
| **Purpose** | Validate spec PR: compile, lint, breaking changes, APIView tokens, SDK dry-run |
| **Entry signal** | PR opened/updated in `azure-rest-api-specs` |
| **Exit signal** | CI passes + review labels applied + approved & merged |
| **Owners** | EngSys (pipeline), @markcowl (spec breaking change), @chunyu3 / @raych1 (SDK breaking change), @catalinaperalta (suppression review), TypeSpec team (compiler/linting) |
| **Reviewer ask** | Confirm CI ordering, label semantics, and blocking vs informational |

#### Happy path

1. PR opens → CI triggers automatically
2. TypeSpec compiles (includes linting) → suppression review → breaking change detection → APIView tokens generated → SDK dry-run → SDK APIView generated
3. Labels applied based on results
4. ARM review (if ARM spec) + namespace approval (if new package)
5. All approvals → PR merges

#### Failure paths

| Failure | Signal | Owner | Next action | Resolution |
|---------|--------|-------|-------------|------------|
| TypeSpec compile failure | CI red + compile error | Author | Fix TypeSpec | Manual |
| Suppression violation | CI warning/error (suppression review tool) | Author | Fix or request suppression approval | Manual |
| Breaking change detected | `BreakingChangeReviewRequired` label | @markcowl | Approve, suppress, or redesign | Manual |
| SDK breaking change detected | `BreakingChange-{Language}-Sdk` label | @chunyu3 / @raych1 | Address breaking change or add suppression | Manual |
| SDK dry-run failure | CI failure in spec-gen-sdk | Author / EngSys | Fix TypeSpec or escalate | Manual |
| Namespace needs approval | `namespace-<lang>-pending` label | Namespace approvers | Apply `namespace-<lang>-approved` | Manual (label) |

<details>
<summary>Deep spec: tools, contracts, and unresolved questions</summary>

#### Tool contract

| Tool | Role | Owner |
|------|------|-------|
| Spec PR validation pipeline | Orchestrates full validation suite | EngSys |
| TypeSpec compiler | CI compilation + linting (lint rules run during compile) | TypeSpec team |
| TypeSpec Suppression Review | Reviews suppressed TypeSpec lint rules. Replacing Swagger-based Spectral LintDiff. New label TBD. Note: TypeSpec decorator suppressions and `suppressions.yaml` changes may be reviewed as separate steps. See [api-reviewer-agent.md](https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/api-reviewer-agent.md). | @catalinaperalta (tool), EngSys (integration) |
| `@azure-tools/typespec-breaking-change` | Same-version + cross-version detection at **TypeSpec/spec level**. Auto-adds `BreakingChangeReviewRequired` / `VersioningReviewRequired` labels. Detects breaking changes in the API spec itself. | @markcowl |
| SDK breaking change detector | Detects breaking changes in the **generated SDK API surface** (complements spec-level detection). Some changes break SDKs but not APIs (e.g., added optional params, parameter order, type renames) and vice versa. Both detectors are needed for complete coverage. | @chunyu3 / @raych1 |
| APIView emitter (`typespec-apiview`) | Generates API surface tokens for SDK-level architect review (tokens used at Stage 4). **Will be retired with ARH.** | APIView team |
| spec-gen-sdk | SDK generation validation (dry-run) | EngSys (@raych1) |
| Avocado / OAV | Legacy Swagger validation -- **being deprecated**, no timeline. Issues overridden when not worth addressing. Exception mechanism being designed for specs that don't emit swagger. | EngSys |
| typespec-autorest (swagger generation) | Generates swagger JSON from TypeSpec for docs pipeline. **Required for foreseeable future**. README also required while swagger is checked in ([directory structure](https://github.com/Azure/azure-rest-api-specs/blob/main/documentation/directory-structure.md)). | TypeSpec team, EngSys (README enforcement) |

#### Gaps

1. Validation steps run independently — no designed chain for failure ordering or unified PR comment.
2. No endpoint liveness verification before spec PR merge — SDK may be generated for an undeployed API. *(Aspirational)*
3. Avocado/OAV deprecation in progress -- no timeline, issues overridden when not worth fixing. Exception list mechanism under discussion.
4. Breaking change label routing undefined for data-plane: `BreakingChangeReviewRequired` applies but has no review team. `BreakingChange-{Language}-Sdk` currently only applies to management plane.

#### Open questions

- [ ] How should breaking change labels route to the correct review team for data-plane?
- [ ] Should spec-gen-sdk failures be PR comments, structured JSON, or both?

</details>

---

<a name="stage-3-sdk-generation"></a>
### Stage 3 — SDK Generation (Automatic)

#### Quick card

| Field | Value |
|-------|-------|
| **Purpose** | Auto-generate SDK PRs per language when spec PR merges |
| **Entry signal** | Spec PR merged in `azure-rest-api-specs` |
| **Exit signal** | SDK PRs created and linked to release plan in each language repo |
| **Owners** | EngSys (spec-gen-sdk), language teams (emitters), azsdk-cli team |
| **Reviewer ask** | Confirm two-stage pipeline and error reporting gaps |

#### Happy path

1. Spec PR merges → pipeline triggers automatically
2. **Stage A**: Create or find active release plan work item for the TypeSpec project and API type (preview/stable). If spec PR adds a new API version and no active plan exists, a new one is created. If no new version, finds existing plan by latest API version.
3. **Stage B**: For each language: tsp-client syncs → emitter generates → customizations applied → build → test → metadata updated → SDK PR created (or updated if one already exists) and linked to release plan

#### Failure paths

| Failure | Signal | Owner | Next action | Resolution |
|---------|--------|-------|-------------|------------|
| Generation failure (any language) | Failed pipeline check (buried in logs) | EngSys / language owner | Investigate logs manually | Manual |
| Customization drift | Build failure in SDK PR | azsdk-cli team | `auto-sdk-build-fix` label → auto-repair | Automatic |
| Release plan creation failure | Pipeline failure | azsdk-cli team | Investigate DevOps connectivity | Manual |

<details>
<summary>Deep spec: tools, contracts, and unresolved questions</summary>

#### Tool contract

| Tool | Role | Owner |
|------|------|-------|
| tsp-client | Syncs TypeSpec project into SDK repo | @catalinaperalta (tool), EngSys (language repo integration) |
| Language emitters | Generate client library code (one per language) | Language teams |
| spec-gen-sdk | Pipeline automation — runs full workflow, creates SDK PRs | EngSys (@raych1) |
| azsdk-cli (`azsdk_package_generate_code`) | Local orchestration (available for dev iteration) | azsdk-cli team |
| `azsdk_customized_code_update` | Apply TypeSpec and code-level customizations | azsdk-cli team |

#### Gap

Generation errors are visible on the [release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard) but not surfaced as structured diagnostics (which language, which step, what error). No agent helps troubleshoot.

#### Next step

Structured error reporting from generation pipeline + agent-assisted troubleshooting.

#### Open questions

- [ ] Should generation failures be reported as PR comments on the spec PR or SDK PR?
- [ ] Can the auto-repair pattern from Stage 4 be extended to diagnose generation failures?

</details>

---

<a name="stage-4-sdk-pr-validation"></a>
### Stage 4 — SDK PR Validation & API Review

#### Quick card

| Field | Value |
|-------|-------|
| **Purpose** | Validate generated SDK PRs: build, test, lint, API review, auto-repair |
| **Entry signal** | SDK PR opened/updated in language repo |
| **Exit signal** | SDK PR approved and merged |
| **Owners** | Language teams (CI), architects (API review), azsdk-cli team (auto-repair) |
| **Reviewer ask** | Confirm auto-repair scope, API review transition (APIView → ARH), and approval mechanism |

#### Happy path

1. SDK PR opens → language CI triggers (build → test → lint → package validation → breaking change detection)
2. APIView generates SDK public API surface review (future: ARH creates review PR)
3. Architects review and approve
4. SDK PR approved and merged

#### Failure paths

| Failure | Signal | Owner | Next action | Resolution |
|---------|--------|-------|-------------|------------|
| Custom code drift | Build failure | azsdk-cli team | `auto-sdk-build-fix` label → Copilot agent auto-repairs | Automatic |
| Other CI failure | Build/test/lint red | Language owner | Pipeline troubleshooting agent diagnoses | Manual (agent-assisted) |
| API review feedback | Architect comments | Author | Resolve via TypeSpec changes → re-generate → new commit → CI re-runs | Manual |
| API review not approved | Architect rejects API surface | Author | Revise TypeSpec design, update spec PR, re-generate SDK. May require follow-up architect discussion. Not all languages auto-approve — rejection path varies per language. | Manual |
| SDK breaking change | `BreakingChange-{Language}-Sdk` label on spec PR | @chunyu3 / @raych1 | Address breaking change or add suppression | Manual |

<details>
<summary>Deep spec: tools, contracts, and unresolved questions</summary>

#### Tool contract

| Tool | Role | Owner |
|------|------|-------|
| Language CI pipelines | Build, test, lint, package validation | Language teams |
| SDK breaking change detector | Detects breaking changes in generated SDK API surface. Being combined into validation check. | @chunyu3 / @raych1 |
| APIView (current) | SDK public API surface review via web UI | APIView team |
| **API Review Hub** (replacing APIView) | Creates synthetic review PRs with `API.md` diffs. PRs never merged — exist only for review. Architects auto-assigned. Approval recorded in ARH database (API hash). CI gates release by checking hash. | @tjprescott |
| API review feedback resolution agent | Helps resolve API review comments via TypeSpec changes | azsdk-cli team |
| Pipeline troubleshooting agent | Diagnoses CI failures | azsdk-cli team |
| Auto SDK PR repair | `auto-sdk-build-fix` label → Copilot cloud agent fixes custom code drift → regenerate → rebuild. Shared orchestration in `eng/common/`, per-language opt-in. | azsdk-cli team |

#### Gaps

1. SDK breaking change detection integration in progress (being combined into validation check).
2. Auto-repair only handles custom-code drift — not all CI failure types.
3. ARH review PR creation on SDK PRs is not automated — mechanism TBD (open design gap).
4. API review feedback resolution agent needs evaluation for ARH compatibility.
5. Release gates transitioning from APIView → both → ARH only.

#### Open questions

- [ ] What triggers ARH review PR creation? SDK PR creation? Manual? Label?
- [ ] How is the review request (tracking issue) linked to the ARH review PR? Swap APIView link with ARH PR link in the tracking board?

#### Confirmed behavior (per @tjprescott)

- ARH uses its own **webhooks + GitHub App** (not GitHub Actions) to propagate approval status.
- When an ARH review PR is **associated with a working SDK PR**, approval automatically applies `api-approved` or `api-changes-requested` labels on the working PR.
- **No separate GitHub Action is needed** for label automation — ARH handles it natively.
- Current state: label step kept manual during transition to avoid premature dependency on ARH while APIView is still active.

> **Note**: **Current**: `<lang>-api-approved` labels applied manually by architects — **informational**. **Future (ARH)**: `api-approved` label assigned automatically via webhooks/GH App. Source of truth is the ARH database (API hash) in both cases.

</details>

---

<a name="stage-5-release-coordination"></a>
### Stage 5 — Release Coordination

#### Quick card

| Field | Value |
|-------|-------|
| **Purpose** | Prepare for release, trigger pipeline, publish packages |
| **Entry signal** | SDK PR merged |
| **Exit signal** | Packages published, release plan completed, KPI updated |
| **Owners** | azsdk-cli team (tooling), EngSys (pipelines), language teams (publish) |
| **Reviewer ask** | Confirm two-phase gap and release type approval differences |

#### Happy path

1. Release plan work item updated
2. Changelog prepared (mgmt: auto-generated; data-plane: manual review needed)
3. SDK PR approved and merged (changelog, metadata, tests all done *before* merge)
4. Release pipeline auto-triggered on SDK PR merge (requires `auto-release` label on the PR; auto-applied for ARM)
5. Readiness checked per language (This happens in the release stage of release pipelines, and it would be expected to run earlier in SDK PR phase)
6. **Release gate check** -- Pipelines check both APIView and ARH for API approval (transitioning to ARH only)
7. Packages published -> release plan auto-completes
8. **APEX/CPEX KPI auto-check** (ARM) -- SDK release auto-triggers KPI verification. 7 KPIs cover: onboarding (1), plus private/preview/GA milestones (3 ARM, 3 data-plane). Service team's stamp that SDKs shipped correctly. Details: @justinprescott / @prkannap.
9. GitHub.io docs automation creates PR in azure-sdk repo to update package index data (aka.ms/azsdk). PR reviewed, approved, and merged by each language team.

#### The two release processes

| Process | What | Owner | Status |
|---------|------|-------|--------|
| **SDK PR readiness** (before merge) | Make SDK PR release-ready: fix linter failures, test failures, merge conflicts, breaking changes, update changelog/metadata. Tracked in [#15705](https://github.com/Azure/azure-sdk-tools/issues/15705). | @raych1 / Language teams / EngSys | Multiple items open — see issue |
| **Release trigger** (after merge) | Auto-trigger release pipeline when SDK PR merges with `auto-release` label. Once PR is merged, changelog and metadata are already done -- just need to release. | @raych1 | Done -- enabled in production |

> **Key clarification**: Changelog, metadata, and version updates happen *when the SDK PR is created*. The validation on these updates should be part of the CI *inside the SDK PR before merge* -- they are part of SDK PR readiness. After merge, the release pipeline is auto-triggered (requires `auto-release` label). The manual approval gate on the release pipeline itself cannot be removed for security reasons.

#### Failure paths

| Failure | Signal | Owner | Next action | Resolution |
|---------|--------|-------|-------------|------------|
| Changelog not ready | Readiness check fails | Author | Update changelog | Manual |
| Pipeline provisioning delay | No pipeline for new RP | EngSys | Wait for overnight batch (or CI-trigger `prepare-pipelines`) | Manual |
| Release gate fails | API hash not approved | Architect | Complete API review | Manual |
| ESRP publish failure | Pipeline failure | ESRP team | Escalate | Manual |

#### Release type approval differences

| Release Type | Approval Gates | Notes |
|-------------|----------------|-------|
| Preview (first) | Namespace approval required | Fastest path; namespace needed for new packages |
| Preview (update) | No architect board review (can be requested) | Fastest path |
| GA (first release) | Architect board review required | Namespace approval if new package |
| GA (update) | Architect board review required (all GA releases) | Breaking changes need separate approval |
| Patch | SDK PR review + CI pass (no architect board review) | Must maintain backward compatibility |

<details>
<summary>Deep spec: tools, contracts, and unresolved questions</summary>

#### Tool contract

| Tool | Role | Owner |
|------|------|-------|
| Release plan tooling (`azsdk_create_release_plan`, etc.) | Create/update/link Azure DevOps work items | azsdk-cli team |
| Changelog/versioning tool | Automates changelog updates. **Mgmt plane**: auto-generated reliably (compare with latest GA). **Data-plane**: not reliable, may need manual curation ([discussion](https://github.com/Azure/azure-sdk-tools/pull/15248#discussion_r3353097483)). | @jsquire |
| Release pipeline (`azsdk_release_sdk`) | Check readiness, trigger release | azsdk-cli team |
| **API Review Hub release gate** | `GET /api/releases/check-gate` verifies API hash. `POST /api/releases/mark-released` records release and closes review PRs. | @tjprescott |
| Language release pipelines | Publish to PyPI, Maven, npm, NuGet, Go module proxy | Language teams |
| Service Tree integration | Mark service KPIs as completed | EngSys |

#### Gap

Two processes, different maturity:
1. **SDK PR readiness** (before merge) — Multiple gaps tracked in [#15705](https://github.com/Azure/azure-sdk-tools/issues/15705): linter failures (especially samples), recorded test failures, merge conflicts, .NET-specific gaps, pipeline provisioning delay. Changelog: mgmt auto-generated reliably, data-plane not reliable.
2. **Release trigger** (after merge) — Auto-trigger on SDK PR merge being built by @raych1. Once merged, changelog/metadata are already done — just need to trigger release.

Manual approval gate on release pipeline cannot be removed for security (ARM approval = service team).

#### Open questions

- [ ] What triggers auto-release after SDK PR merge?
- [ ] For patch releases — what triggers the workflow differently?

</details>

---

## Cross-cutting contracts

### Label contract

#### Spec PR Labels (`azure-rest-api-specs`)

| Label | Applied by | Meaning | Blocking? | Automation |
|-------|-----------|---------|-----------|------------|
| `BreakingChangeReviewRequired` | CI | Breaking change detected | Yes | ⚠️ Label auto, routing manual |
| `VersioningReviewRequired` | CI | Versioning review needed | Yes | ⚠️ Label auto, assignment manual |
| `ARM-Review-Required` | CI | ARM spec — routes to ARM team | Yes | ✅ Fully automated |
| `ARMSignedOff` | ARM team | ARM review approved | Unblocks | ✅ Manual label, gate automated |
| `APIStewardshipBoard-SignedOff` | Stewardship board | Data-plane REST API spec approved (stewardship review) | No (transitioning) | ⚠️ Process in transition |
| `namespace-<lang>-pending` | CI | New namespace detected | Yes | ✅ Fully automated |
| `namespace-<lang>-approved` | Architect | Namespace approved | Unblocks | ✅ Manual label, gate automated |
| `namespace-approved-all` | Architect | Approves all languages (mgmt) | Unblocks | ✅ Manual label, gate automated |
| `Approved-BreakingChange*` | Review team | Breaking change approved (multiple labels exist per break category) | Unblocks | ⚠️ Manual label, gate works |
| `BreakingChange-{Language}-Sdk` | CI | Sdk breaking change detected (management plane only) | Yes | ✅ Fully automated |
| `BreakingChange-{Language}-Sdk-Approved` | Review team | Sdk breaking change approved | Yes | ⚠️ Manual label, validation automated |
| `BreakingChange-{Language}-Sdk-Suppression` | Authors | SDK breaking change suppression updates | Yes | ✅ Fully automated |
| `BreakingChange-{Language}-Sdk-Suppression-Approved` | Review team | SDK breaking change suppression approved | Yes | ⚠️ Manual label, validation automated |
| `PublishToCustomers` | Author | Acknowledges APIs are shipped to customers. Required for PRs targeting `main` or `RPSaaSMaster`. Without it, PR cannot merge. **Low-hanging fruit for removal** -- redundant once all other gates pass; causes confusion for service teams. Could be auto-applied or removed entirely. | Yes | ⚠️ Manual label, enforced by CI |


#### SDK PR Labels (language repos)

| Label | Applied by | Meaning | Blocking? | Automation |
|-------|-----------|---------|-----------|------------|
| `auto-sdk-build-fix` | CI / human | Triggers Copilot auto-repair | No | ✅ Triggers cloud agent |
| `auto-release` | CI (ARM) / human | Triggers release pipeline on SDK PR merge. Auto-applied for ARM management plane. | No | ✅ Auto-applied (ARM) |
| `<lang>-api-approved` (current) / `api-approved` (ARH future) | ARH (future) / Architect (current) | SDK API approved -- **informational only**. Source of truth is ARH database (API hash). Current: architects apply `<lang>-api-approved` manually. Future: ARH assigns `api-approved` automatically. | Informational | ⚠️ Transitioning to ARH |
| `release-plan-linked` | Automation | Marks PR as linked to release plan | No | ✅ Auto-applied |
| `ready-for-review` | GitHub Form | Triggers architect review process | No | ✅ Applied via workflow |
| `needs-info` | Reviewer | Needs more info from service team | No | ⚠️ Manual, no automation |
| `review-out-of-date` | ARH | Review PR stale | No | 🔜 Part of ARH |
| `architecture-review-needed` | ARH | Flags for architect review | No | 🔜 Part of ARH |

> **📋 Proposal: Label naming consistency** — Current labels use mixed conventions: `PascalCase` (`BreakingChangeReviewRequired`, `ARMSignedOff`, `APIStewardshipBoard-SignedOff`), `kebab-case` (`auto-sdk-build-fix`, `release-plan-linked`, `namespace-<lang>-approved`), and hybrid (`ARM-Review-Required`). Consider standardizing to `kebab-case` (e.g., `breaking-change-review-required`, `arm-signed-off`) for new labels, with backward-compatible aliasing for existing ones.

### Approval gates (3 workstreams converging)

| Workstream | Status | Scope | Long-term fate |
|-----------|--------|-------|----------------|
| **GitHub Forms + Actions** (PR #10037, shipped) | ✅ Live | Review intake via `azure-sdk` repo. `arch-board-review.yml` = bridge. `namespace-review.yml` = long-term. | `arch-board-review.yml` retires when ARH ships |
| **API Review Hub** (@tjprescott, in progress) | 🔜 Prototype | SDK-level review via synthetic GitHub PRs. Does NOT operate at spec level. | Replaces APIView for SDK review |
| **Spec PR-based namespace approval** ([process doc](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/src/namespace-approval/NAMESPACE-REVIEW-PROCESS.md), live) | ✅ Live | Namespace approval on spec PR merge. | Retires `namespace-review.yml` |

### Orchestration architecture: skill chaining

The system uses **prompt chaining**: independent sub-skills invoked sequentially, each returning `NextSteps` that guide the LLM agent to the next action. `CommandResponse.NextSteps` is used across 20+ tool and service files.

<details>
<summary>Deep spec: orchestration gaps</summary>

| Gap | Current State | Improvement |
|-----|---------------|-------------|
| NextSteps are natural language | LLM must interpret free-text — works but fragile | Structured NextSteps with explicit tool name + parameters |
| Chaining is partial (Stages 3–5 only) | No NextSteps connecting Stage 1 → 2 | Add cross-tool NextSteps for early stages |
| Skills don't reference each other | SKILL.md files fully independent | Document expected skill sequences |
| No state detection | Agent can't determine "where am I?" | Add workflow status tool (query release plan + PR status) |
| Errors not structured for agents | Some errors buried in logs | Every tool returns parseable errors with suggested next action |
| Label-driven automation gaps | Routing not fully connected | Connect label events to automation |

</details>

### Related process documentation

| Process | Link | Scope |
|---------|------|-------|
| Namespace approval | [Namespace review process](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/src/namespace-approval/NAMESPACE-REVIEW-PROCESS.md) | Permissions, flow, labels -- live |
| SDK API review (architecture board) | [Review process](https://eng.ms/docs/products/azure-developer-experience/design/api-review) | Architecture review board for SDK API review |
| SDK API review (bridge) | [Arch board review process](https://github.com/Azure/azure-sdk/blob/main/.github/workflows/src/arch-board-review/ARCH-BOARD-REVIEW-PROCESS.md) | GitHub Form — **bridge** until ARH |
| API Review Hub | [POC implementation (PR #49)](https://github.com/tjprescott/azure-sdk-tools/pull/49) | Synthetic review PRs replacing APIView |
| Mgmt plane release | [Release process](https://eng.ms/docs/products/azure-developer-experience/plan/mgmt-sdk-release-process) | Service + SDK team responsibilities |
| SDK PR readiness gaps | [Tracking issue #15705](https://github.com/Azure/azure-sdk-tools/issues/15705) | Consolidated gaps |
| Release plan dashboard | [Dashboard](https://aka.ms/azsdk/releaseplan-dashboard) | Track release progress |

---

## Decision log

- [ ] **D1**: How does ARH review PR creation get triggered on SDK PRs? (No automation today)
- [ ] **D3**: Should service teams approve SDK PRs? (Not required today)
- [ ] **D4**: Where does breaking-change enforcement live? (Spec level vs SDK level)
- [ ] **D5**: What triggers auto-release after SDK PR merge? (Last E2E automation piece)
- [ ] **D6**: How should breaking change labels route to review team for data-plane? `BreakingChangeReviewRequired` applies but has no routing. `BreakingChange-{Language}-Sdk` does not apply to data-plane today.
- [ ] **D7**: Should spec-gen-sdk failures be PR comments, structured JSON, or both?
- [ ] **D8**: For patch releases — what triggers the workflow differently?

---

## Gap tracker

<details>
<summary>Gap tracker</summary>

| # | Gap | Stage | Owner | Blocking? | Status |
|---|-----|-------|-------|-----------|--------|
| 1 | End-to-end CI chain not designed (no unified PR comment) | 2 | @raych1 / @prkannap / @catalinaperalta | Yes | Open |
| 2 | Generation errors not surfaced as structured report for agent troubleshooting. Failures are visible on [release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard), but not as actionable diagnostics for automated remediation. | 3 | @raych1 / spec-gen-sdk | No | Partially addressed |
| 3 | SDK PR not fully release-ready after generation: linter failures, test failures, merge conflicts, missing changelog/metadata. Tracked in [#15705](https://github.com/Azure/azure-sdk-tools/issues/15705). | 4 | @raych1 / Language teams | Yes | Open |
| 4 | Release trigger automation | 5 | @raych1 | No | Done -- auto-release label + SDK PR merge triggers pipeline |
| 5 | ARH review PR creation not automated on SDK PRs | 4 | @tjprescott | Yes | Open |
| 6 | Breaking change resolution: agent-assisted for suppression suggestions and break categorization (API correction, intentional evolution, inadvertent). Agent can invoke authoring skill to suggest alternatives. Full automation not feasible -- versioned removals/type changes require human judgment. Source location data needed for unversioned change suggestions. | 1, 2 | @markcowl / @chunyu3 | No | Partially addressed |
| 7 | Breaking change label routing undefined for data-plane: `BreakingChangeReviewRequired` (spec-level) applies but has no review team. `BreakingChange-{Language}-Sdk` (SDK-level) currently only applies to management plane. | 2 | @raych1 / @markcowl / @lmazuel | No | Open |
| 8 | SDK breaking change detection integration in progress | 4 | @chunyu3 / @raych1 | No | In progress |
| 9 | Data-plane review gates undefined: (a) spec-level breaking change review team/routing has no replacement after stewardship board dissolution, (b) SDK-level breaking change label (`BreakingChange-{Language}-Sdk`) not applied to data-plane today, (c) `APIStewardshipBoard-SignedOff` label -- process in transition, no replacement defined | 2, 4 | @samvaity / @prkannap / @lmazuel | No | Open |
| 10 | Release pipeline provisioning delay for new RPs | 5 | EngSys | No | In progress |
| 11 | API review feedback agent needs ARH compatibility | 4 | azsdk-cli team | No | Open |
| 12 | Release gate transition (APIView → ARH) | 5 | @tjprescott / EngSys | No | In progress — pipelines will check both APIView and ARH, then transition to ARH only |
| 13 | No endpoint liveness verification before spec PR merge | 2 | TBD | No | Aspirational |
| 14 | GitHub.io docs PR review is manual per language team -- should be automated | 5 | EngSys / Language teams | No | Open |

> **See also**: [SDK PR Release Readiness tracking issue (#15705)](https://github.com/Azure/azure-sdk-tools/issues/15705)

</details>

---

## Success criteria

- [ ] Users can complete full TypeSpec → SDK release workflow with agent guidance
- [ ] Agent detects existing state and resumes from appropriate step
- [ ] Release plan is automatically updated at each step
- [ ] All sub-skills integrate seamlessly without context loss between stages
- [ ] Local and pipeline SDK generation paths both work
- [ ] Breaking change findings from CI are surfaced clearly
- [ ] API review feedback can be resolved within the workflow
- [ ] Works for all tier-1 SDK languages
- [ ] Errors at every stage produce structured, actionable guidance
- [ ] Service Tree KPI is updated on release completion

---

## Exceptions and limitations

<details>
<summary>Exception 1: Architect Board Review & Namespace Approval</summary>

**Description**: All GA releases should expect architect board review (some languages may opt out for specific scenarios, but the workflow must account for it). First preview requires namespace approval for new packages. New SDK packages require namespace/naming approval before release.

**Impact**: Cannot fully automate GA approval or first preview namespace approval. New package releases blocked until naming approved.

**Status**: Three workstreams converging:
1. **GitHub Forms + Actions (shipped)** — `arch-board-review.yml` is bridge until ARH. `namespace-review.yml` stays long-term.
2. **API Review Hub (in progress)** — Replaces APIView for SDK review. Namespace out of scope.
3. **Spec PR-based namespace approval ([process doc](https://github.com/Azure/azure-rest-api-specs/blob/main/.github/workflows/src/namespace-approval/NAMESPACE-REVIEW-PROCESS.md), live)** — Namespace approval on spec PR merge. CI extracts namespaces, applies `namespace-<lang>-pending` labels, blocks merge until approved.

</details>

<details>
<summary>Exception 2: Breaking Change Reviews</summary>

**Description**: Breaking changes are detected at two levels. Both levels apply to ARM and data-plane specs.

**Spec-level breaking changes** (`BreakingChangeReviewRequired`): Applied by CI on any spec PR with breaking changes (ARM and data-plane). For ARM, routed to ARM breaking change review team who must apply `Approved-BreakingChange` before merge. For data-plane, the label is applied but there is no defined review team or routing today (Gap #9).

**SDK-level breaking changes** (`BreakingChange-{Language}-Sdk`): Applied by CI when generated SDK has breaking changes. Currently only applies to management plane spec PRs. Data-plane specs do not have this label applied today.

**Impact**: Breaking change releases blocked until approved (where review workflow exists).

</details>

<details>
<summary>Exception 3: .NET Team Complementary Tooling</summary>

**Description**: .NET team has developed **complementary** tooling aligned with azsdk-cli — sharing infrastructure via `azsdk_customized_code_update` (custom-code auto-repair) and TypeSpec linter/fixer patterns. Cross-language potential has been surfaced for discussion and integration. Nothing competes or conflicts with the inner-loop work and azsdk-cli tooling.

**Impact**: .NET is ahead on auto-repair and linter integration. Areas with cross-language potential are being integrated into shared orchestration in `eng/common/` with per-language opt-in.

**Next step**: Alignment meeting to document remaining .NET-specific tools.

</details>

---

## Definitions

<details>
<summary>Expand all definitions</summary>

- **TypeSpec**: Language for describing cloud service APIs. See [typespec.io](https://typespec.io).
- **SDK**: Client libraries generated from TypeSpec for .NET, Java, JavaScript, Python, Go.
- **Release Plan**: Azure DevOps work item tracking end-to-end SDK release across languages.
- **API Spec PR**: Pull request in `azure-rest-api-specs` containing TypeSpec changes.
- **SDK PR**: Pull request in a language SDK repo with generated and customized SDK code.
- **APIView**: Current web tool for reviewing SDK public API surface. Being replaced by API Review Hub. Operates at **SDK level only** — there is no spec-level API review.
- **API Review Hub (ARH)**: New service replacing APIView for **SDK-level API review only** — there is no spec-level API review. Creates synthetic "review PRs" in language repos with `API.md` diffs — never merged, exist only for review. Approval recorded in ARH database (API hash). When associated with a working SDK PR, ARH auto-applies `api-approved`/`api-changes-requested` labels via webhooks + GH App (no GH Actions needed). Replaces current per-language `<lang>-api-approved` labels with single `api-approved`. ⚠️ ARH review PR creation trigger on SDK PRs is an open design gap.
- **tspconfig.yaml**: Configuration specifying emitter settings per language.
- **tsp-location.yaml**: Configuration in SDK repos pointing to source TypeSpec project.
- **`@azure-tools/typespec-breaking-change`**: TypeSpec-native breaking change detector. Two modes: same-version regression (unversioned changes within a version) and cross-version evolution (breaking changes across API versions).
- **Suppression Decorators**: `@approvedBreakingChange` (cross-version evolution) and `@approvedUnversionedChange` (same-version regression). Reason field encodes categorization (API correction, intentional evolution, inadvertent).
- **TypeSpec Customizations**: SDK-specific customizations in `client.tsp`.
- **Code Customizations**: Hand-written SDK code preserved across regeneration.
- **TypeSpec Suppression Review**: Reviews suppressed TypeSpec lint rules. Replacing Swagger-based Spectral LintDiff. Owned by @catalinaperalta (tool), EngSys (integration).
- **spec-gen-sdk**: Pipeline tool automating SDK generation from specs.

</details>

---

## Appendix: Detailed Flowchart

<details>
<summary>Expand full detailed flowchart</summary>

```mermaid
flowchart TD
    %% Stage 1: TypeSpec Authoring
    entry["ENTRY POINT<br/>User provides initial prompt"]
    entry --> step1

    subgraph S1["STAGE 1: TypeSpec Authoring (local)"]
        step1{"Step 1: Author TypeSpec"}
        step1 --> newProj["Create new TypeSpec project"]
        step1 --> updateProj["Update existing TypeSpec project"]
        newProj --> step2
        updateProj --> step2
        step2["Step 2: Validate & Compile<br/>• TypeSpec compiler<br/>• Linter checks<br/>• Breaking change detection"]
        step2 -->|FAIL| fixLoop["Report errors → iterate on TypeSpec"]
        fixLoop --> step2
        step2 -->|PASS| ready["TypeSpec ready<br/>Extract API version & package names"]
    end

    ready --> step3["Step 3: Open API Spec PR"]
    step3 --> S2

    subgraph S2["STAGE 2: Spec PR Validation (CI)"]
        step4["Step 4: CI Validation Pipeline<br/>• TypeSpec compilation (includes linting)<br/>• Suppression review<br/>• Breaking change detection<br/>• APIView token generation<br/>• SDK generation dry-run<br/>• Labels applied"]
        step4 -->|FAIL| fixPR["Fix issues → push to PR<br/>(re-triggers pipeline)"]
        step4 -->|PASS| reviews["Step 4b: Reviews<br/>• ARM review<br/>• Namespace approval"]
    end

    reviews --> merged["PR approved & merged"]
    merged --> S3

    subgraph S3["STAGE 3: Automated SDK Generation"]
        genPipeline["Pipeline runs automatically:<br/>1) Create/find release plan<br/>2) Generate SDK per language<br/>3) Apply customizations<br/>4) Create SDK PRs & link to release plan"]
    end

    genPipeline --> S4

    subgraph S4["STAGE 4: SDK PR Validation & API Review"]
        step5["Step 5: SDK PR CI<br/>• Build → Test → Lint → Package validation<br/>• SDK breaking change detection<br/>• API review (APIView / ARH review PR)"]
        step5 -->|FAIL| repair{"Custom code drift?"}
        repair -->|YES| autoRepair["auto-sdk-build-fix → Copilot auto-repair"]
        repair -->|NO| troubleshoot["Pipeline troubleshooting agent"]
        step5 -->|PASS| step6["Step 6: API Review<br/>Architects review SDK public API surface"]
        autoRepair --> step5
        troubleshoot --> step5
    end

    step6 --> sdkMerged["SDK PR approved & merged"]
    sdkMerged --> S5

    subgraph S5["STAGE 5: Release Coordination"]
        step7["Step 7: Release<br/>Changelog/metadata done BEFORE merge"]
        step7 --> trigger["7a. Release pipeline auto-triggered (auto-release label)"]
        trigger --> gate["7b. Release gate (ARH check-gate)"]
        gate --> publish["7c. Packages published → KPI updated"]
    end
```

</details>
