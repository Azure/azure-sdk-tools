# Scope Comparison: TypeSpec-to-SDK Workflow and Next Generation API & SDK Review

## Table of Contents

- [Definitions](#definitions)
- [Executive Summary](#executive-summary)
- [Source Boundaries](#source-boundaries)
- [Scope at a Glance](#scope-at-a-glance)
- [Detailed Scope Comparison](#detailed-scope-comparison)
- [Where the Specs Agree](#where-the-specs-agree)
- [Where the Specs Differ](#where-the-specs-differ)
- [Where the Specs Completely Diverge](#where-the-specs-completely-diverge)
- [Meaningful Nuances](#meaningful-nuances)
- [Material Omissions](#material-omissions)
- [Decision Implications](#decision-implications)
- [Conclusion](#conclusion)

---

## Definitions

- **<a id="workflow-spec"></a>Workflow spec**: `typespec-to-sdk-release-workflow.spec.md`, a current-state and target-state description of the end-to-end TypeSpec-to-SDK release workflow.
- **<a id="review-proposal"></a>Review proposal**: `jonathan-proposal.md`, a proposal to change the API and SDK review operating model.
- **<a id="spec-driven-change"></a>Spec-driven change**: Work that begins with a TypeSpec change and a pull request in `azure-rest-api-specs`.
- **<a id="direct-sdk-change"></a>Direct SDK change**: SDK work without a corresponding spec change, including customization-only updates, bug fixes, dependency changes, and packages with no spec.
- **<a id="review-scope"></a>Review scope**: The artifacts, entry paths, review bodies, gates, and decisions that a document proposes to govern.
- **<a id="release-scope"></a>Release scope**: Activities after review, including SDK pull request readiness, release triggering, package publication, and completion tracking.
- **<a id="scope-divergence"></a>Scope divergence**: A difference in the subject or boundary being governed, rather than merely a difference in implementation detail.

---

## Executive Summary

The documents are complementary in breadth but incompatible in several important review assumptions.

- The [workflow spec](#workflow-spec) is broader across the lifecycle. Its primary scope is the five-stage path from TypeSpec authoring through package release, with detailed tools, labels, owners, failures, and gates. Its main path is [spec-driven](#spec-driven-change); non-spec paths are acknowledged but not modeled as first-class flows. See [Workflow overview > Scope](typespec-to-sdk-release-workflow.spec.md#scope), [The five-stage workflow](typespec-to-sdk-release-workflow.spec.md#the-five-stage-workflow), and [Stage modules](typespec-to-sdk-release-workflow.spec.md#stage-modules).
- The [review proposal](#review-proposal) has narrower [release scope](#release-scope) but broader [review scope](#review-scope). It focuses on replacing a capacity-constrained data-plane review model, and it expressly treats [direct SDK changes](#direct-sdk-change) as first-class entries. It does not redesign detailed release execution. See [The situation](jonathan-proposal.md#the-situation), [Match the review to the change](jonathan-proposal.md#2-match-the-review-to-the-change), and [Explicitly not doing](jonathan-proposal.md#explicitly-not-doing).
- They agree on shifting deterministic checks left, generating SDK evidence before a spec merges, preserving human judgment for ambiguous breaking changes, accounting for non-spec SDK work, and retaining the release pipeline's manual security approval.
- They differ on emphasis and depth. The workflow spec inventories an end-to-end system; the review proposal seeks leadership approval for governance, reviewer allocation, readiness policy, and a delivery roadmap.
- They completely diverge on the central review unit. The workflow spec explicitly says there is no spec-level API review and places architect API review on generated SDK pull requests. The review proposal calls for one combined data-plane reviewer pool to make one decision on the TypeSpec contract, with generated surfaces used as evidence and eligible generated SDKs skipping later architecture review.
- They also diverge on ARM boundaries. The workflow spec includes ARM throughout the end-to-end workflow while documenting its separate gates. The review proposal deliberately leaves ARM review outside the proposed board merger.

The key decision is therefore not which document is more complete. It is whether the organization wants to preserve the workflow spec's separate, downstream SDK API-review model or adopt the review proposal's single data-plane contract decision and provenance-based fast track. If the latter is chosen, the workflow spec would need a targeted revision of Stages 2 and 4, review labels, approval gates, and release-type rules. The workflow and release mechanics outside that review boundary can still supply much of the implementation baseline.

---

## Source Boundaries

This report compares only:

1. [`typespec-to-sdk-release-workflow.spec.md`](typespec-to-sdk-release-workflow.spec.md)
2. [`jonathan-proposal.md`](jonathan-proposal.md)

`jonathan-proposal.md` delegates supporting argument, implementation design, and backlog detail to `design-and-evidence.md` and `appendices.md`; those linked documents are not treated as evidence here. Consequently, an item described only by a link title is credited only to the extent stated in `jonathan-proposal.md` itself. No unstated rationale or intent is inferred.

---

## Scope at a Glance

| Dimension | Workflow spec | Review proposal | Relationship |
|---|---|---|---|
| Primary purpose | Describe and improve the end-to-end TypeSpec-to-SDK release workflow | Replace lost review capacity and redesign the API/SDK review operating model | Different core scope |
| Lifecycle boundary | Local TypeSpec authoring through package publication and KPI updates | Authoring checks through review decisions and delivery efforts; release security gate is preserved but release execution is not designed | Workflow spec is broader |
| Primary entry path | Spec change; non-spec and no-spec paths are a scope note/open question | Spec PR and direct SDK PR are coequal first-class entrances | Proposal has broader intake scope |
| Main artifacts | TypeSpec, spec PR, generated SDKs, SDK PRs, release plans, packages | TypeSpec contract, generated language surfaces, direct SDK PRs, review records | Overlap, with different center of gravity |
| Data plane | Included, but review ownership and routing have known gaps | Primary target of governance and capacity changes | Proposal is deeper on data-plane review |
| ARM/control plane | Included across all five stages with separate ARM validation | Adjacent and explicitly excluded from the board merger | Different boundary |
| Review organization | Existing ARM, stewardship, architecture, package-name, and breaking-change mechanisms are inventoried | Merge the two data-plane boards into one reviewer pool | Direct divergence |
| API review location | SDK level only, mainly Stage 4 | One TypeSpec contract decision for spec-driven work, informed by generated surfaces | Direct divergence |
| SDK generation timing | Dry-run and API surface generation on spec PR; full SDK PR generation after merge | Full supported-language generation and surface evidence during TypeSpec review | Same direction, materially different gate |
| Generated SDK review | Architect review is a Stage 4 activity, subject to release-type rules | Skip architecture review when provenance is approved and no handwritten surface changed | Direct divergence |
| Automation scope | Agent-guided orchestration, auto-repair, structured diagnostics, release-plan updates, release trigger | Shared checks, readiness gate, agentic review, automated intake/status, review ledger | Overlap, different objectives |
| Exceptions | Multiple suppressions, approvals, labels, and workflow-specific exceptions | One narrow recorded-exception model; retire blanket grandfathering | Proposal changes policy beyond workflow inventory |
| Delivery framing | Owners, gaps, open decisions, and success criteria; no dated roadmap | Week-0 decisions, month-1 results, pilots, November outcomes | Proposal is more programmatic |
| Language ambition | Success criterion covers all tier-1 SDK languages | Agent rollout limited to at most three languages in the stated period | Different rollout scope |

---

## Detailed Scope Comparison

| Topic | Agreement, difference, or divergence | Evidence from the workflow spec | Evidence from `jonathan-proposal.md` | Practical consequence |
|---|---|---|---|---|
| End-to-end lifecycle | **Difference** | Scope runs from TypeSpec authoring to release; Stages 1–5 include package publication and KPI updates ([Scope](typespec-to-sdk-release-workflow.spec.md#scope), [Stage 5](typespec-to-sdk-release-workflow.spec.md#stage-5-release-coordination)) | Proposal concentrates on review reform and delivery efforts; it explicitly leaves the manual release approval gate untouched ([Proposed approach](jonathan-proposal.md#proposed-approach), [Explicitly not doing](jonathan-proposal.md#explicitly-not-doing)) | The proposal cannot replace the workflow spec as an operational release reference. |
| Non-spec release paths | **Agreement in recognition; difference in commitment** | Notes that about half of Python and .NET releases may have no spec change and that some packages have no spec, but asks whether to model those paths as first-class ([Scope](typespec-to-sdk-release-workflow.spec.md#scope)) | Makes direct SDK changes and no-spec packages first-class entrances using the same queue, tier, and SLA ([Match the review to the change](jonathan-proposal.md#2-match-the-review-to-the-change)) | Adopting the proposal resolves an open workflow scope question and requires explicit alternate entry paths. |
| Data-plane review | **Complete divergence in operating model** | States there is no spec-level API review; data-plane breaking-change routing and stewardship replacement remain gaps ([For Reviewers](typespec-to-sdk-release-workflow.spec.md#for-reviewers), [Gap tracker](typespec-to-sdk-release-workflow.spec.md#gap-tracker)) | Makes data-plane review reform the central program: one reviewer pool and one TypeSpec contract decision ([Merge the two review boards](jonathan-proposal.md#1-merge-the-two-review-boards)) | Stages 2 and 4 cannot both remain authoritative without reconciling where approval occurs. |
| ARM review | **Difference in boundary, agreement on separation** | Includes ARM in the workflow, with ARM-specific validation, labels, ownership, and KPI behavior ([Service team journey](typespec-to-sdk-release-workflow.spec.md#service-team-journey), [Label contract](typespec-to-sdk-release-workflow.spec.md#label-contract)) | Treats ARM as adjacent, preserves its gates, and excludes it from the board merger ([The situation](jonathan-proposal.md#the-situation), [Explicitly not doing](jonathan-proposal.md#explicitly-not-doing)) | The proposal should be applied as a data-plane overlay, not as a replacement for ARM workflow coverage. |
| Checks during authoring | **Agreement; proposal broadens consistency** | Local compile, lint, and breaking-change checks precede the spec PR; agent-assisted suppression is a future goal ([Stage 1](typespec-to-sdk-release-workflow.spec.md#stage-1-typespec-authoring)) | All review checks should share one command and source across local use, GitHub Actions, and Review Hub ([Give teams the same checks used in review](jonathan-proposal.md#3-give-teams-the-same-checks-used-in-review), [Effort G](jonathan-proposal.md#g-run-every-automated-check-through-one-shared-tool)) | The proposal supplies a stronger distribution and parity requirement than the workflow spec. |
| Readiness before human review | **Difference** | CI must pass before spec merge, but review requests and automated checks coexist within Stage 2; no separate queue-admission policy is defined ([Stage 2](typespec-to-sdk-release-workflow.spec.md#stage-2-spec-pr-validation)) | Automated checks must be green before entry to the board queue; judgment findings must be answered ([Keep unready changes out of the review queue](jonathan-proposal.md#4-keep-unready-changes-out-of-the-review-queue)) | Proposal adoption adds a governance gate, not merely another CI check. |
| SDK generation on spec PR | **Agreement in direction; difference in depth** | Runs an SDK generation dry-run and generates APIView material before merge; full per-language SDK PR generation follows merge ([Stage 2](typespec-to-sdk-release-workflow.spec.md#stage-2-spec-pr-validation), [Stage 3](typespec-to-sdk-release-workflow.spec.md#stage-3-sdk-generation)) | Requires supported SDKs and public-surface changes to be generated and visible during TypeSpec review ([Require SDK generation on spec pull requests](jonathan-proposal.md#6-require-sdk-generation-on-spec-pull-requests), [Effort B](jonathan-proposal.md#b-generate-supported-sdks-during-every-typespec-review)) | “Dry-run succeeded” is insufficient under the proposal; reviewable surface evidence becomes part of the pre-merge gate. |
| Architecture review artifact | **Complete divergence** | Architects review SDK public API surfaces at Stage 4 through APIView or API Review Hub; ARH is described as SDK-level only ([Stage 4](typespec-to-sdk-release-workflow.spec.md#stage-4-sdk-pr-validation), [Approval gates](typespec-to-sdk-release-workflow.spec.md#approval-gates-3-workstreams-converging)) | Combined pool makes one decision on TypeSpec for spec-driven work, while generated language surfaces are shown in the same PR when needed ([Merge the two review boards](jonathan-proposal.md#1-merge-the-two-review-boards), [Effort B](jonathan-proposal.md#b-generate-supported-sdks-during-every-typespec-review)) | The documents assign the authoritative review decision to different lifecycle stages and artifacts. |
| Fast-tracking generated SDKs | **Complete divergence** | Stage 4 includes architect review, while release-type rules exempt preview updates and patches but require board review for all GA releases ([Stage 4](typespec-to-sdk-release-workflow.spec.md#stage-4-sdk-pr-validation), [Release type approval differences](typespec-to-sdk-release-workflow.spec.md#release-type-approval-differences)) | Provenance-linked SDKs with no handwritten surface changes may skip architecture review, without skipping human-authorized release ([Fast-track SDKs](jonathan-proposal.md#7-fast-track-sdks-that-contain-no-new-human-decisions)) | Eligibility based on provenance and human decisions may conflict with release-type-based GA review rules. |
| Breaking changes | **Agreement on detection and human judgment; difference in policy design** | Uses spec-level and SDK-level detectors, labels, suppressions, and manual review; data-plane routing is unresolved ([Stage 2](typespec-to-sdk-release-workflow.spec.md#stage-2-spec-pr-validation), [Exception 2](typespec-to-sdk-release-workflow.spec.md#exceptions-and-limitations)) | Proposes author-time classification while explicitly declining to automate breaking-change review itself ([Identify breaking changes](jonathan-proposal.md#8-identify-breaking-changes-while-the-api-is-being-authored), [Explicitly not doing](jonathan-proposal.md#explicitly-not-doing)) | The proposal can improve early classification, but it does not answer the workflow spec's unresolved ownership and routing question. |
| Agentic review | **Difference** | Agents guide the whole workflow, suggest suppressions, repair custom-code drift, diagnose pipelines, and help resolve API feedback ([For EngSys / SDK Team](typespec-to-sdk-release-workflow.spec.md#for-engsys--sdk-team), [Success criteria](typespec-to-sdk-release-workflow.spec.md#success-criteria)) | Agents are primarily a staged review-capacity measure, beginning with dark launches and gaining authority only from measured results ([Introduce agentic review](jonathan-proposal.md#5-introduce-agentic-review-in-measured-stages), [Effort A](jonathan-proposal.md#a-roll-out-agentic-review-of-data-plane-typespec-pull-requests)) | Workflow agents are broader functionally; proposal agents have clearer calibration and authority boundaries. |
| Review tracking | **Overlap with different systems emphasis** | Release plans and the release-plan dashboard track generation and release progress; labels route work ([Workflow map](typespec-to-sdk-release-workflow.spec.md#workflow-map), [Cross-cutting contracts](typespec-to-sdk-release-workflow.spec.md#cross-cutting-contracts)) | A GitHub/API Review Hub ledger records intake, status, decisions, wait time, and ownership without a new tracking system ([Make review intake and status automatic](jonathan-proposal.md#d-make-review-intake-and-status-automatic)) | Release tracking and review-decision tracking are related but not interchangeable; both may be needed. |
| Exceptions | **Difference** | Documents several tool- and workflow-specific suppression and approval mechanisms, plus proposed label naming consistency ([Label contract](typespec-to-sdk-release-workflow.spec.md#label-contract), [Exceptions and limitations](typespec-to-sdk-release-workflow.spec.md#exceptions-and-limitations)) | Calls for one exception record and replacement of blanket service-level grandfathering ([Replace blanket grandfathering](jonathan-proposal.md#10-replace-blanket-grandfathering-with-recorded-exceptions)) | Proposal adoption adds a cross-system policy model absent from the workflow spec. |
| Manual release approval | **Clear agreement** | Says the release pipeline's manual approval gate cannot be removed for security ([Stage 5](typespec-to-sdk-release-workflow.spec.md#stage-5-release-coordination)) | Explicitly excludes changing that gate ([Explicitly not doing](jonathan-proposal.md#explicitly-not-doing)) | Review acceleration must stop short of bypassing release authorization. |
| Language coverage | **Difference** | Completion requires operation across all tier-1 SDK languages ([Success criteria](typespec-to-sdk-release-workflow.spec.md#success-criteria)) | Near-term scope limits agent shipping to at most three languages, selected by readiness ([Explicitly not doing](jonathan-proposal.md#explicitly-not-doing)) | The proposal is a staged rollout, not evidence that the workflow's cross-language completion criterion has been met. |

---

## Where the Specs Agree

1. **Review should move left.** Both put compile, lint, generation, and breaking-change feedback before final review or release. The proposal strengthens this into a shared-tool parity requirement.
2. **Generated SDK surfaces are evidence about the source contract.** Both generate before spec merge in some form. They disagree on whether that evidence supports an SDK-level decision or a TypeSpec-level decision.
3. **Not every SDK release begins with a spec change.** Both recognize [direct SDK changes](#direct-sdk-change) and packages with no spec. Only the proposal fully incorporates them into its primary intake model.
4. **Automation does not eliminate human judgment.** Both retain people for architecture, ambiguous breaking changes, exceptions, and release authorization.
5. **Breaking-change discovery should happen earlier.** Both seek author-time or PR-time detection rather than late discovery.
6. **The release security gate remains.** Neither document proposes fully automatic publication without human authorization.
7. **ARM remains distinct from data-plane review.** The workflow spec models that distinction inside one end-to-end workflow; the proposal places ARM outside its governance change.

---

## Where the Specs Differ

### Breadth versus depth

The workflow spec is operationally broad: authoring, spec CI, post-merge generation, SDK CI, customization repair, API review, release plans, publication, documentation updates, and KPI completion. The review proposal is organizationally deep: reviewer capacity, authority, queue admission, triage, exception policy, measurement, pilots, and leadership decisions.

### Current/target workflow versus change program

The workflow spec mixes current behavior, transitions, known gaps, and future improvements in a stage reference. The review proposal is explicitly a change program with ten outcomes, week-0 decisions, month-1 results, and November success conditions. This makes the former better for locating operational dependencies and the latter better for deciding whether to fund and authorize governance changes.

### Intake breadth

The workflow spec's diagram and stage model begin with TypeSpec authoring. Its scope note acknowledges other entrances but does not trace them. The review proposal treats a direct SDK PR as a normal entrance and applies the same queue, tier, and SLA while withholding any TypeSpec approval that did not occur.

### Automation objective

The workflow spec uses automation mainly to complete and troubleshoot the release journey. The review proposal uses automation mainly to reduce reviewer load, enforce readiness, create durable review records, and establish evidence for delegating authority.

---

## Where the Specs Completely Diverge

### 1. What receives the authoritative architecture decision

- **Workflow spec:** There is no spec-level API review. Architects approve generated SDK API surfaces in Stage 4.
- **Review proposal:** For a spec-driven data-plane change, the merged reviewer pool makes one decision on the TypeSpec contract. Generated SDK surfaces inform that decision.

This is the most consequential [scope divergence](#scope-divergence). It changes the reviewed artifact, decision timing, reviewer pool, and approval record.

### 2. Whether generated SDKs need a second architecture review

- **Workflow spec:** Architecture review remains a distinct SDK-stage activity, with exemptions described by release type.
- **Review proposal:** A generated SDK may skip architecture review when provenance ties it to an approved contract and no handwritten public surface changed.

These are not merely different optimizations. They define different meanings for “approved”: approval of each SDK surface versus approval inherited from the source contract under stated eligibility.

### 3. Whether the document governs ARM review

- **Workflow spec:** ARM is inside the documented lifecycle, although its validation authority and gates remain separate.
- **Review proposal:** ARM review is explicitly outside the board-merger program.

The proposal therefore cannot be applied wholesale as the new end-to-end workflow for both planes.

### 4. How broad the near-term agent rollout is

- **Workflow spec:** End-state success spans all tier-1 languages and multiple agent roles across all stages.
- **Review proposal:** The stated program will not ship agents in all seven languages and limits the near-term rollout to at most three.

The proposal's rollout can be a pilot toward the workflow target, but it is not equivalent to that target.

---

## Meaningful Nuances

- **“Generation on the spec PR” does not mean the same thing in both documents.** The workflow spec names a dry-run plus APIView generation; the proposal requires supported-language generation with reviewable public-surface changes. A passing generation check alone does not satisfy the proposal's evidence requirement.
- **The workflow spec already exposes the governance gap the proposal addresses.** Its data-plane breaking-change routing and stewardship-signoff replacement are unresolved. The proposal supplies a direction—one data-plane pool—but does not provide the workflow-level labels, owners, or migration mechanics needed to close those gaps.
- **The proposal does not remove language authority entirely.** It retains language-specific review for direct SDK changes and language architects' authority over language-specific surfaces. Its consolidation applies to the duplicated review of spec-driven work, not every SDK decision.
- **Fast-track eligibility and release authorization are separate.** The proposal removes eligible duplicate architecture review, not the manual release approval. This aligns with the workflow spec's security constraint.
- **The proposal's first-class direct SDK path cannot inherit nonexistent TypeSpec approval.** This prevents the unified model from incorrectly treating all SDK changes as generated consequences of an approved contract.
- **The workflow spec's GA rules require explicit reconciliation.** Its table says all GA releases receive architect board review, while the proposal's provenance rule could exempt a generated GA SDK with no new human decisions. The texts do not resolve which rule prevails.
- **Review tracking and release plans solve different questions.** The workflow's release plan answers where a service is in generation and release. The proposal's ledger answers what review is required, what was decided, and where reviewer time was spent.
- **The proposal is intentionally not self-contained at implementation level.** It points to companion documents for evidence and backlog detail. The workflow spec contains substantially more implementation inventory in the compared file itself.

---

## Material Omissions

### Present in the review proposal but absent or underdeveloped in the workflow spec

- Reviewer-capacity collapse as the primary problem statement.
- A decision to merge the data-plane Stewardship and SDK Architecture reviewer pools.
- Release-type/change-risk triage into fast-track, standard, or architect review.
- A readiness gate before work enters the human review queue.
- Measured agent maturity, precision evidence, and staged blocking authority.
- A rule catalogue and pipeline for converting recurring findings into deterministic rules.
- One narrow recorded-exception model replacing blanket grandfathering.
- A review ledger with queue, decision, wait-time, and reviewer-concentration metrics.
- Named pilots, leadership decisions, dated outcomes, and reviewer-allocation commitments.

### Present in the workflow spec but absent or underdeveloped in `jonathan-proposal.md`

- Detailed post-merge generation mechanics, including release-plan creation and per-language SDK PR creation.
- TypeSpec and handwritten code customization application and custom-code drift repair.
- Concrete spec and SDK label contracts.
- Package-name approval mechanics.
- APIView-to-API Review Hub transition details and API-hash release checks.
- SDK PR build, test, lint, package validation, and troubleshooting flows.
- Changelog, metadata, pipeline provisioning, package publication, documentation-index updates, and KPI completion.
- Stage-specific owners, failure paths, and operational gap tracking.
- azsdk-cli skill chaining and end-to-end agent resumption.
- A complete ARM/control-plane release path.

An omission does not establish opposition. It only means the compared source does not define that part of the system.

---

## Decision Implications

1. **Decide the authoritative review artifact first.** Choose between downstream SDK-surface approval and a single TypeSpec contract decision supported by generated evidence.
2. **If adopting the review proposal, update the workflow spec rather than replacing it.** Preserve its authoring, generation, SDK CI, release, and ARM details; revise the data-plane portions of Stages 2 and 4.
3. **Resolve the GA fast-track conflict explicitly.** State whether provenance-based eligibility can bypass the workflow spec's “all GA releases” architecture review rule.
4. **Promote alternate entrances to explicit workflows.** Model direct SDK and no-spec packages with their own entry conditions, inherited checks, and non-inherited approvals.
5. **Keep ARM changes out of the data-plane governance decision.** The proposal expressly does not merge ARM review.
6. **Separate review tracking from release tracking.** Define how the proposed review ledger links to, but does not duplicate, release plans and the release-plan dashboard.
7. **Translate governance decisions into operational contracts.** A board merger, readiness gate, and unified exception record will require concrete owners, labels, status transitions, and failure behavior before the workflow is executable.

---

## Conclusion

The workflow spec answers, “How does a TypeSpec-originated change travel through generation and release?” The review proposal answers, “How should constrained data-plane review capacity decide which artifacts need human judgment?”

They are aligned on earlier automation, generated evidence, first-class recognition of non-spec work, retained human judgment, and release security. They are not aligned on where architecture approval occurs or whether eligible generated SDKs need a second review. Those decisions must be resolved before the documents can serve as one coherent target state. The strongest combined model would use the workflow spec as the operational backbone and apply the review proposal as a deliberately bounded redesign of data-plane intake and review.
