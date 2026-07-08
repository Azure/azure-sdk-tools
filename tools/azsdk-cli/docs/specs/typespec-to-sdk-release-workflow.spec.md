# TypeSpec-to-SDK Release Workflow Spec

> [!IMPORTANT]
> **Review goal**: Validate the end-to-end workflow contract, stage ownership, gates, and unresolved decisions.

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
     - TypeSpec compilation
     - TypeSpec LintDiff (guideline compliance)
     - Breaking change detection (Phase A + B)
     - APIView token generation (for SDK-level review at Stage 4)
     - SDK generation dry-run (spec-gen-sdk)
     - Labels applied based on results

3. 🧑‍💻 **Wait for approvals** *(human gating)*
   - **Namespace approval** — required for first preview of new packages (`namespace-<lang>-pending` → `namespace-<lang>-approved`)
   - **ARM review** — required for ARM/management-plane specs (`ARM-Review-Required` → `ARMSignedOff`)
   - **Breaking change review** — required for ARM specs with breaking changes (`BreakingChangeReviewRequired` → `Approved-BreakingChange`)
   - > **Note**: There is no spec-level API review. API review happens only at the SDK level (Stage 4).

4. **Spec PR merges → SDK generation is automatic**
   - Release plan work item created
   - SDK code generated per language via emitters
   - Customizations applied (`client.tsp` + code customizations)
   - SDK PRs opened in each language repo and linked to release plan
   - ⚠️ Generation failures currently fail silently — [Gap #4](#gap-tracker)

5. 🧑‍💻 **SDK PR review & approval** *(human gating)*
   - SDK CI runs automatically:
     - Build → test → lint → package validation
     - SDK breaking change detection
     - APIView generates SDK API surface review (current) or API Review Hub creates review PR (future)
   - **API review**: Architects review generated SDK public API surface
     - `<lang>-api-approved` labels are **informational** — source of truth is ADO Package Work Items (API hash)
     - ARH will assign `<lang>-api-approved` labels on SDK PRs automatically when architect approves
   - **Auto-repair**: `auto-sdk-build-fix` label triggers Copilot agent to fix custom code drift
   - **ARM SDK PRs**: Reviewed by Haoling/Shanghai team with release plans attached

6. 🧑‍💻 **Release** *(human gating)*
   - Release pipeline triggered (becoming automatic — @raych1)
   - Manual approval gate required for security (cannot be removed; ARM approval = Haoling/Shanghai team)
   - Release gate: API Review Hub verifies approved API hash
   - Packages published → release plan completes → Service Tree KPI updated

### For Reviewers

1. **ARM review** (ARM specs only) — Review resource model correctness on spec PRs; apply `ARMSignedOff` label
2. **SDK API review** — Review generated SDK API surface on SDK PRs via APIView (current) or API Review Hub review PRs (future). **There is no spec-level API review** — API review applies only to the generated SDK. `<lang>-api-approved` labels are **informational** — source of truth is ADO Package Work Items (API hash).
3. **SDK PR review** (Haoling/Shanghai team, management plane only) — Review generated SDK PRs that have a release plan attached; approve & merge
4. **Breaking change review** — Review breaking changes flagged by `BreakingChangeReviewRequired` label on spec PRs (ARM specs only)
5. **Namespace review** — Approve new package namespaces; apply `namespace-<lang>-approved` labels
6. **Release approval** — Approve release pipeline runs (Haoling/Shanghai team for ARM)

### For EngSys / SDK Team

1. **Monitor pipelines** — Spec PR validation, SDK generation, and SDK CI pipelines run automatically
2. **Label routing** — Labels like `BreakingChangeReviewRequired`, `namespace-<lang>-pending`, and `auto-sdk-build-fix` trigger review routing and automation
3. **Generation failures** — When SDK generation fails, diagnose via pipeline logs (structured error reporting is a [known gap](#gap-tracker))
4. **Auto-repair** — `auto-sdk-build-fix` label triggers Copilot cloud agent to fix custom code drift on SDK PRs
5. **Release coordination** — `azsdk_release_sdk` checks readiness; release pipelines publish to package registries (manual approval gate required)
6. **Track progress** — [Release plan dashboard](https://aka.ms/azsdk/releaseplan-dashboard) shows where each service is in the process


---

## Workflow map

### Stage summary

| Stage | Entry signal | Exit signal | Primary owner |
|-------|-------------|------------|---------------|
| 1. TypeSpec Authoring | Local TypeSpec change | PR-ready `.tsp` files | TypeSpec team / @prkannap |
| 2. Spec PR Validation | PR opened in `azure-rest-api-specs` | CI pass + labels + approved | @raych1 / @catalinaperalta / EngSys |
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
     │                 │                │── LintDiff     │                │                │
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

> **ARM vs Data Plane divergence** — Same high-level flow but diverge at review gates. ARM requires ARM review sign-off + stricter resource model constraints. Data plane skips ARM review. See: [ARM review](https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/arm-review)
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
| **Owners** | TypeSpec team (compiler/linter), Haoling/Shanghai (authoring agent), @markcowl (breaking change) |
| **Reviewer ask** | Confirm tool list and breaking change workflow |

#### Happy path

1. Write/update `.tsp` files and `tspconfig.yaml`
2. Compile locally with TypeSpec compiler
3. Run linter checks
4. Run breaking change detection (Phase A + B)
5. Ready to open spec PR

#### Failure paths

| Failure | Signal | Owner | Next action | Resolution |
|---------|--------|-------|-------------|------------|
| Compile error | TypeSpec compiler error | Author | Fix `.tsp` syntax/types | Manual |
| Linter violation | Linter warning/error | Author | Fix or suppress with decorator | Manual |
| Breaking change detected | Tool report with DiffKind + source location | Author | Apply suppression decorator or redesign | Manual |

<details>
<summary>Deep spec: tools, contracts, and unresolved questions</summary>

#### Tool contract

| Tool | Role | Owner |
|------|------|-------|
| TypeSpec compiler | Compile `.tsp` files, catch syntax/type errors | TypeSpec team |
| TypeSpec linter | Static guideline compliance (distinct from LintDiff) | TypeSpec team |
| TypeSpec authoring agent (`azure-typespec-author` skill) | Assist with ARM/data-plane patterns, Azure REST API guidelines | Haoling/Shanghai |
| `@azure-tools/typespec-breaking-change` | Phase A: same-version regression. Phase B: cross-version evolution. Inline suppression via decorators. | @markcowl |

#### Gap

Breaking change tool reports findings with DiffKind, source location, and suggested suppression decorator — but resolution is manual. No agent auto-resolves.

#### Next step

Build author-validation loop where agent auto-applies suppression decorators based on structured breaking change output.

#### Open questions

- [ ] Does `@azure-tools/typespec-breaking-change` output provide enough structured context for agent auto-resolution? Need confirmation from @markcowl.

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
| **Owners** | EngSys (pipeline), @raych1 / @catalinaperalta (breaking change), TypeSpec team (lintdiff) |
| **Reviewer ask** | Confirm CI ordering, label semantics, and blocking vs informational |

#### Happy path

1. PR opens → CI triggers automatically
2. TypeSpec compiles → LintDiff runs → breaking change detection → APIView tokens generated → SDK dry-run
3. Labels applied based on results
4. ARM review (if ARM spec) + namespace approval (if new package)
5. All approvals → PR merges

#### Failure paths

| Failure | Signal | Owner | Next action | Resolution |
|---------|--------|-------|-------------|------------|
| TypeSpec compile failure | CI red + compile error | Author | Fix TypeSpec | Manual |
| LintDiff violation | CI warning/error | Author | Fix or request suppression | Manual |
| Breaking change detected | `BreakingChangeReviewRequired` label | @raych1 / @markcowl / @catalinaperalta | Approve, suppress, or redesign | Manual |
| SDK dry-run failure | CI failure in spec-gen-sdk | Author / EngSys | Fix TypeSpec or escalate | Manual |
| Namespace needs approval | `namespace-<lang>-pending` label | Namespace approvers | Apply `namespace-<lang>-approved` | Manual (label) |

<details>
<summary>Deep spec: tools, contracts, and unresolved questions</summary>

#### Tool contract

| Tool | Role | Owner |
|------|------|-------|
| Spec PR validation pipeline | Orchestrates full validation suite | EngSys |
| TypeSpec compiler | CI compilation | TypeSpec team |
| TypeSpec Lintdiff | TypeSpec-native linting on PR diffs. Replacing Swagger-based Spectral LintDiff. Includes suppression process (@catalinaperalta). | EngSys / TypeSpec team |
| `@azure-tools/typespec-breaking-change` | Phase A + B detection at **TypeSpec/spec level**. Auto-adds `BreakingChangeReviewRequired` / `VersioningReviewRequired` labels. Detects breaking changes in the API spec itself. | @markcowl |
| SDK breaking change detector ([PR #15588](https://github.com/Azure/azure-sdk-tools/pull/15588)) | Detects breaking changes in the **generated SDK API surface** (complements spec-level detection). Reports changes that may not be visible at spec level but affect SDK consumers. Being integrated into spec CI validation. | @raych1 / @catalinaperalta |
| APIView emitter (`typespec-apiview`) | Generates API surface tokens for SDK-level architect review (tokens used at Stage 4). **Will be retired with ARH.** | APIView team |
| spec-gen-sdk | SDK generation validation (dry-run) | EngSys (@prkannap) |
| Avocado / OAV | Legacy Swagger validation — **being deprecated** as TypeSpec-native tooling replaces them. | EngSys |

#### Gaps

1. Validation steps run independently — no designed chain for failure ordering or unified PR comment.
2. No endpoint liveness verification before spec PR merge — SDK may be generated for an undeployed API. *(Aspirational)*
3. Avocado/OAV deprecation still in progress.
4. `BreakingChangeReviewRequired` label routing to correct review team is undefined (CODEOWNERS, custom Action, or DevOps?).

#### Open questions

- [ ] How should `BreakingChangeReviewRequired` label route to the correct review team? CODEOWNERS, custom Action, or DevOps?
- [ ] Should spec-gen-sdk failures be PR comments, structured JSON, or both?
- [ ] How do SDK breaking change detector findings (PR #15588) differ from `@azure-tools/typespec-breaking-change` findings? What labels does the SDK detector add, and how are its errors reported?

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
2. **Stage A**: Create or find release plan work item
3. **Stage B**: For each language: tsp-client syncs → emitter generates → customizations applied → build → test → metadata updated → SDK PR created and linked to release plan

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
| tsp-client | Syncs TypeSpec project into SDK repo | EngSys |
| Language emitters | Generate client library code (one per language) | Language teams |
| spec-gen-sdk | Pipeline automation — runs full workflow, creates SDK PRs | EngSys (@prkannap) |
| azsdk-cli (`azsdk_package_generate_code`) | Local orchestration (available for dev iteration) | azsdk-cli team |
| `azsdk_customized_code_update` | Apply TypeSpec and code-level customizations | azsdk-cli team |

#### Gap

Generation errors silently fail. Error is buried in build logs — not surfaced as structured report (which language, which step, what error). No agent helps troubleshoot.

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
| SDK breaking change | Detection report | @raych1 / @catalinaperalta | Review and approve or fix | Manual |

<details>
<summary>Deep spec: tools, contracts, and unresolved questions</summary>

#### Tool contract

| Tool | Role | Owner |
|------|------|-------|
| Language CI pipelines | Build, test, lint, package validation | Language teams |
| SDK breaking change detector | Detects breaking changes in generated SDK API surface. Being combined into validation check. | @raych1 / @catalinaperalta |
| APIView (current) | SDK public API surface review via web UI | APIView team |
| **API Review Hub** (replacing APIView) | Creates synthetic review PRs with `API.md` diffs. PRs never merged — exist only for review. Architects auto-assigned. Approval recorded in ADO Package Work Items (API hash). CI gates release by checking hash. | @tjprescott |
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

> **Note**: `<lang>-api-approved` labels are **informational** — source of truth is ADO Package Work Items (API hash). ARH will assign `<lang>-api-approved` labels on SDK PRs automatically when architect approves in future.

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
3. Readiness checked per language
4. SDK PR approved and merged (changelog, metadata, tests all done *before* merge)
5. Release pipeline triggered — **becoming automatic** (@raych1 working on auto-trigger on SDK PR merge)
6. **Release gate check** — API Review Hub verifies approved API hash
7. Packages published → release plan auto-completes → Service Tree KPI updated

#### The two release processes

| Process | What | Owner | Status |
|---------|------|-------|--------|
| **SDK PR readiness** (before merge) | Make SDK PR release-ready: fix linter failures, test failures, merge conflicts, breaking changes, update changelog/metadata. Tracked in [#15705](https://github.com/Azure/azure-sdk-tools/issues/15705). | @prkannap / Language teams / EngSys | Multiple items open — see issue |
| **Release trigger** (after merge) | Auto-trigger release pipeline when SDK PR merges. Once PR is merged, changelog and metadata are already done — just need to release. | @raych1 | In progress — becoming automatic |

> **Key clarification**: Changelog, metadata, and version updates happen *inside the SDK PR before merge* — they are part of SDK PR readiness. After merge, the only step is triggering the release pipeline (which @raych1 is automating). The manual approval gate on the release pipeline itself cannot be removed for security reasons.

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

Manual approval gate on release pipeline cannot be removed for security (ARM approval = Shanghai team).

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
| `Approved-BreakingChange` | Review team | Breaking change approved | Unblocks | ⚠️ Manual label, gate works |
| `Suppression-Approved` | Review team | Linter suppression approved | Unblocks | ⚠️ Manual label, validation automated |

#### SDK PR Labels (language repos)

| Label | Applied by | Meaning | Blocking? | Automation |
|-------|-----------|---------|-----------|------------|
| `auto-sdk-build-fix` | CI / human | Triggers Copilot auto-repair | No | ✅ Triggers cloud agent |
| `<lang>-api-approved` | ARH (future) / Architect (current) | SDK API approved — **informational only**. Source of truth is ADO (API hash). ARH will assign this label on SDK PRs automatically when architect approves. | Informational | ⚠️ ARH will assign automatically in future |
| `release-plan-linked` | Automation | Marks PR for Haoling/Shanghai team review (ARM SDK PRs) | No | ✅ Auto-applied |
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
| **Spec PR-based namespace approval** ([PR #44085](https://github.com/Azure/azure-rest-api-specs/pull/44085), in progress) | 🔜 In progress | Namespace approval on spec PR merge. | Retires `namespace-review.yml` |

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
| Namespace approval | [Namespace approval (PR #44085)](https://github.com/Azure/azure-rest-api-specs/pull/44085) | Permissions, flow, labels — in progress |
| ARM review | [ARM review](https://eng.ms/docs/products/azure-developer-experience/design/api-specs-pr/arm-review) | ARM-specific gates |
| REST API spec review | [Review process](https://eng.ms/docs/products/azure-developer-experience/design/api-review) | Architect board flow |
| SDK API review (bridge) | [Arch board review process](https://github.com/Azure/azure-sdk/blob/main/.github/workflows/src/arch-board-review/ARCH-BOARD-REVIEW-PROCESS.md) | GitHub Form — **bridge** until ARH |
| API Review Hub | TBD | Synthetic review PRs replacing APIView |
| Mgmt plane release | [Release process](https://eng.ms/docs/products/azure-developer-experience/plan/mgmt-sdk-release-process) | Service + SDK team responsibilities |
| SDK PR readiness gaps | [Tracking issue #15705](https://github.com/Azure/azure-sdk-tools/issues/15705) | Consolidated gaps |
| Release plan dashboard | [Dashboard](https://aka.ms/azsdk/releaseplan-dashboard) | Track release progress |

---

## Decision log

- [ ] **D1**: How does ARH review PR creation get triggered on SDK PRs? (No automation today)
- [ ] **D3**: Should service teams approve SDK PRs? (Not required today)
- [ ] **D4**: Where does breaking-change enforcement live? (Spec level vs SDK level)
- [ ] **D5**: What triggers auto-release after SDK PR merge? (Last E2E automation piece)
- [ ] **D6**: How should `BreakingChangeReviewRequired` route to review team? (CODEOWNERS, Action, or DevOps)
- [ ] **D7**: Should spec-gen-sdk failures be PR comments, structured JSON, or both?
- [ ] **D8**: For patch releases — what triggers the workflow differently?

---

## Gap tracker

<details>
<summary>Gap tracker</summary>

| # | Gap | Stage | Owner | Blocking? | Status |
|---|-----|-------|-------|-----------|--------|
| 1 | End-to-end CI chain not designed (no unified PR comment) | 2 | @raych1 / @prkannap / @catalinaperalta | Yes | Open |
| 2 | Generation errors silently fail — not surfaced as structured report, no agent troubleshooting | 3 | @prkannap / spec-gen-sdk | Yes | Open |
| 3 | SDK PR not fully release-ready after generation: linter failures, test failures, merge conflicts, missing changelog/metadata. Tracked in [#15705](https://github.com/Azure/azure-sdk-tools/issues/15705). | 4 | @prkannap / Language teams | Yes | Open |
| 4 | Release trigger not automated — auto-trigger on SDK PR merge being built | 5 | @raych1 | Yes | In progress |
| 5 | ARH review PR creation not automated on SDK PRs | 4 | @tjprescott | Yes | Open |
| 6 | Breaking change findings require manual resolution | 1, 2 | @markcowl / @catalinaperalta | No | Open |
| 7 | `BreakingChangeReviewRequired` label routing to correct review team undefined | 2 | @raych1 / @markcowl / @catalinaperalta | No | Open |
| 8 | SDK breaking change detection integration in progress | 4 | @raych1 / @catalinaperalta | No | In progress |
| 9 | ARM vs data plane review gates not documented in detail (different review teams, labels, and blocking behavior) | 2, 4 | @samvaity / @prkannap | No | Open |
| 10 | Release pipeline provisioning delay for new RPs | 5 | EngSys | No | In progress |
| 11 | API review feedback agent needs ARH compatibility | 4 | azsdk-cli team | No | Open |
| 12 | Release gate transition (APIView → ARH) | 5 | @tjprescott / EngSys | No | Open |
| 13 | No endpoint liveness verification before spec PR merge | 2 | TBD | No | Aspirational |

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
3. **Spec PR-based namespace approval ([PR #44085](https://github.com/Azure/azure-rest-api-specs/pull/44085), in progress)** — Namespace approval on spec PR merge. CI extracts namespaces, applies `namespace-<lang>-pending` labels, blocks merge until approved.

</details>

<details>
<summary>Exception 2: Breaking Change Reviews</summary>

**Description**: Breaking changes require review team approval. Separate process with own team and labels.

**Impact**: Breaking change releases blocked until approved.

**Workaround**: Agent helps prepare suppression decorators with clear reasons.

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
- **API Review Hub (ARH)**: New service replacing APIView for **SDK-level API review only** — there is no spec-level API review. Creates synthetic "review PRs" in language repos with `API.md` diffs — never merged, exist only for review. Approval recorded in ADO Package Work Items. `<lang>-api-approved` labels are **informational**. ⚠️ ARH review PR creation on SDK PRs is an open design gap.
- **tspconfig.yaml**: Configuration specifying emitter settings per language.
- **tsp-location.yaml**: Configuration in SDK repos pointing to source TypeSpec project.
- **`@azure-tools/typespec-breaking-change`**: TypeSpec-native breaking change detector. Phase A: same-version regression. Phase B: cross-version evolution.
- **Suppression Decorators**: `@approvedBreakingChange` (Phase B) and `@approvedUnversionedChange` (Phase A).
- **TypeSpec Customizations**: SDK-specific customizations in `client.tsp`.
- **Code Customizations**: Hand-written SDK code preserved across regeneration.
- **TypeSpec Lintdiff**: Linting pipeline on spec PR diffs. Replacing Swagger-based Spectral LintDiff.
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
        step2["Step 2: Validate & Compile<br/>• TypeSpec compiler<br/>• Linter checks<br/>• Breaking change detection (Phase A + B)"]
        step2 -->|FAIL| fixLoop["Report errors → iterate on TypeSpec"]
        fixLoop --> step2
        step2 -->|PASS| ready["TypeSpec ready<br/>Extract API version & package names"]
    end

    ready --> step3["Step 3: Open API Spec PR"]
    step3 --> S2

    subgraph S2["STAGE 2: Spec PR Validation (CI)"]
        step4["Step 4: CI Validation Pipeline<br/>• TypeSpec compilation<br/>• LintDiff<br/>• Breaking change detection<br/>• APIView token generation<br/>• SDK generation dry-run<br/>• Labels applied"]
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
        step7 --> trigger["7a. Release trigger (becoming automatic)"]
        trigger --> gate["7b. Release gate (ARH check-gate)"]
        gate --> publish["7c. Packages published → KPI updated"]
    end
```

</details>
