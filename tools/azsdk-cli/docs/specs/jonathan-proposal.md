# Next Generation API & SDK Review

---

## Contents

1. [Executive summary](#1-executive-summary)
2. [Roadmap](#2-roadmap)

**This is the document everyone should read.** Section 1 sets out the proposed changes and the leadership decisions they require. Section 2 turns them into a delivery plan. Together they are sufficient to decide whether to back the proposal.

The measures, open decisions, supporting argument and implementation design are in [Design and evidence](design-and-evidence.md). Detailed source material and delivery backlogs remain in [the appendices](appendices.md).

---

## 1. Executive summary

### The situation

For spec-driven work, several review functions now operate over what is, increasingly, **one artifact**. The **API Stewardship Board** reviews data-plane REST and TypeSpec contracts. The **SDK Architecture Board** reviews the client libraries generated from those contracts. But SDK review is not always downstream of a spec: many releases start with a direct SDK change, and some packages have no spec at all. Breaking-change review grew up as a function of the Stewardship Board rather than as a body sitting across both. Who owns it now is unclear. **ARM runs its own control-plane review** alongside all of this, with its own gates, rule set and governing contract. That separation is a long-standing agreement between teams, and this document treats ARM as adjacent rather than absorbed.

Moving the Architecture Board to an async, GitHub-native model was right and is not being walked back. But three things have changed the problem.

**First, reviewer capacity collapsed.** The bulk of data-plane REST API review was carried by three people, and all three have retired. The scheduled REST board is shut down. Coverage today is essentially one person doing it alongside a full-time role, with one possible reinforcement some weeks away. Ignite is coming.

**Second, the surface moved, and part of the review capability was left behind.** Per-language surface diffing, cross-version comparison and a durable record now live in GitHub and run as CI against the artifacts teams already work on. The capabilities lost with APIView were its surface linting and guideline diagnostics, and the APIView Copilot reviewer. Restoring them is real work that has not yet been done. The deterministic checks have an obvious destination: the author's machine, firing while they type, long before a reviewer or a pipeline is involved. The Copilot review capability belongs in the new agentic review flow, against the same language surface.

**Third, and this was always true, the review boards never had the authority.** The data-plane stewardship board has never been able to tell a service its API is bad and that it cannot ship. It advises; it does not mandate. Even the separation between data-plane and control-plane review is an agreement between teams rather than a structure anyone imposed. That is not an obstacle to this programme. It is the argument for its shape. Where review cannot be enforced through hierarchy, it has to be enforced through defaults, tooling and friction. A red check in CI is authority that does not require a VP, and a scaffold that produces a conformant API by default is authority nobody has to exercise at all.

**This is therefore not a programme to improve a working process. It is a programme to replace capacity that no longer exists**, and to put the automation back where the work now happens. There is no steady state to return to. The realistic alternative to building this is new API versions merging with no substantive review at all.

### Proposed approach

Ten changes. Each links to the section that sets it out properly. Sizing is indicative and needs confirming with the owning teams.

#### 1. [Merge the two review boards](design-and-evidence.md#76-one-board-over-one-artifact)

The API Stewardship Board and SDK Architecture Board become one reviewer pool. For a spec-driven change, that pool makes one decision on the TypeSpec contract rather than reviewing the contract and its generated SDKs separately. A direct SDK change still enters through its SDK pull request and receives language-specific review. ARM review stays separate, and per-language architects retain authority over language-specific surfaces.

**Win:** A materially larger reviewer pool, one decision instead of two, and less architect time per change.

**Cost:** Small engineering effort and a **large governance change**. This is the leadership ask.

#### 2. [Match the review to the change](design-and-evidence.md#71-one-lifecycle-several-entrances)

The existing release workflow classifies a release by where it sits in its lifecycle: first preview, preview update, first GA, GA update, or patch. That release type determines whether a change can be fast-tracked, needs a standard review, or needs an architect. **A direct SDK change is a first-class entrance, not an exception:** many SDK releases have no corresponding spec change, and some packages have no spec at all. They enter through the SDK pull request and follow the same queue, tier and SLA, but cannot inherit approval from a TypeSpec review that did not happen.

**Win:** Architect time goes only to changes that need architectural judgement.

**Cost:** Medium. Requires ledger and triage automation.

#### 3. [Give teams the same checks used in review](design-and-evidence.md#82-the-toolbox)

Linters, agent review, breaking-change classification and generation checks sit behind one command and editor experience, built from the same source used by the Review Hub and GitHub Actions.

**Win:** Teams find problems while authoring rather than during review; every finding moved left is one a reviewer never sees.

**Cost:** Medium. Mostly consolidation and distribution of capabilities that already exist.

#### 4. [Keep unready changes out of the review queue](design-and-evidence.md#83-the-readiness-gate)

A change enters the board queue only after its automated checks are green. Findings that require judgement must be answered, but do not have to disappear.

**Win:** Reviewers stop spending time on lint-grade findings and incomplete submissions.

**Cost:** Small engineering effort and **high political cost**, because teams may see the gate as the boards offloading review work onto them.

#### 5. [Introduce agentic review in measured stages](design-and-evidence.md#93-the-maturity-ladder)

Agents begin as advisory reviewers and earn more authority only when their results support it. The first blocking step is simpler: review comments must be resolved before merge ([§9.5](design-and-evidence.md#95-the-blocking-question)).

**Win:** First-pass review arrives in minutes instead of weeks, and findings cannot be silently ignored.

**Cost:** Medium. The agents largely exist; calibration and the reference corpus are the main work.

#### 6. [Require SDK generation on spec pull requests](design-and-evidence.md#105-generation-is-a-gate-merging-is-not-releasing)

Each service records the SDK languages it supports, and the spec pull request must generate successfully for those languages before it can proceed.

**Win:** The generated SDK surface becomes evidence about the contract while the contract is still cheap to change.

**Cost:** Medium. Generation already works, but data-plane generation must become automatic, reliable and bounded.

#### 7. [Fast-track SDKs that contain no new human decisions](design-and-evidence.md#101-eligibility)

A generated SDK can skip architecture review when its provenance links it to an approved contract and no hand-written surface has changed. This shortens review without changing the separate, human-authorised release step ([§10.5](design-and-evidence.md#105-generation-is-a-gate-merging-is-not-releasing)).

**Win:** Removes the largest duplicated review without touching a security control.

**Cost:** Large. Depends on reliable public-surface diffing for each language.

#### 8. [Identify breaking changes while the API is being authored](design-and-evidence.md#103-breaking-changes-are-the-hard-interlock)

The policy's existing tables become an automated classifier that tells an author whether a change is additive, needs a new API version, requires breaking-change review, or still needs human classification.

**Win:** Discovery moves from "a quarter late" to "while typing".

**Cost:** Medium. The policy is well specified, but the mechanically decidable subset must be measured first.

#### 9. [Turn repeated findings into rules](design-and-evidence.md#85-the-rule-catalogue-and-the-gap-pipeline)

A published catalogue shows what is checked. Review findings with no existing rule enter a gap register, and recurring mechanical findings become candidate linter rules that run in the editor.

**Win:** The deterministic layer improves systematically instead of depending on someone being annoyed enough to act.

**Cost:** Medium. The pipeline is the deliverable, not any single rule.

#### 10. [Replace blanket grandfathering with recorded exceptions](design-and-evidence.md#74-exceptions-make-it-hard-but-make-it-possible)

Legacy deviations are declared at the narrowest practical scope rather than exempting an entire service forever. Suppressions, exemptions, overrides and waivers use [one exception record](design-and-evidence.md#75-one-exception-record).

**Win:** Machines can distinguish approved deviations from defects, and teams learn one exception mechanism instead of six.

**Cost:** Small engineering effort and a **large policy change**. Needs a leadership decision and probably OCTO.

Items 1, 4 and 10 need leadership air cover. Items 2, 5, 8 and 9 are mostly engineering. Items 6 and 7 are the biggest prize and the longest pole. Item 3 has the shortest path from funding to a result a partner team can feel. The **review ledger** ([§12](design-and-evidence.md#12-tracking-on-github)) underpins all of them and is deliberately not listed as a change, because it is assembled from GitHub issues, labels and the existing API Review Hub rather than built as new infrastructure.

These ten items describe **what changes**. [The roadmap](#2-roadmap) groups the work needed to deliver them into seven efforts. The two lists are not one-to-one: a proposed change may depend on several efforts, and an effort may deliver parts of several changes.

### Management decisions

Five of the ten changes above are engineering, and work on them can start tomorrow. These five are not, and none of them can be worked around.

1. **A decision on merging the two boards** ([§7.6](design-and-evidence.md#76-one-board-over-one-artifact)). One pool; one decision for spec-driven work, and the same intake for direct SDK work. The single largest capacity lever available, and the only one that cannot be reached through engineering.
2. **A mandate for the readiness gate** ([§8.3](design-and-evidence.md#83-the-readiness-gate)). Most friction, most leverage.
3. **Backing to retire blanket grandfathering** ([§7.4](design-and-evidence.md#74-exceptions-make-it-hard-but-make-it-possible)), including the OCTO conversation. A policy ask, not an engineering one.
4. **Move existing SDK Architecture Board members into data-plane review** ([§5.2](design-and-evidence.md#52-capacity-is-the-binding-constraint)). This is the capacity bridge before automation lands and the practical first step toward one reviewer pool. It is [a delivery effort in its own right](#f-move-existing-sdk-architecture-board-capacity-into-the-shared-pool), with named allocation, supervised reviews and exit criteria.
5. **Confirmation of the tentative task owners and their allocation** ([the backlog](appendices.md#the-backlog)), **two pilot services**, and an owner for effort C, which has none. Storage is proposed as the hard case, plus one well-behaved service as the control.

**Ten things need confirming before any of this is committed** ([§2](#2-roadmap), week 0): task-owner allocation and an owner for effort C, Ignite dates, appetite for the board merge, authority for the readiness gate, unresolved-comments blocking, who owns breaking change review, the OCTO position on [§7.4](design-and-evidence.md#74-exceptions-make-it-hard-but-make-it-possible), reviewer availability for the next two quarters, two pilot services, and whether the generated SDK is the artifact that would be released. Each can invalidate work if discovered in October rather than August.

### What success looks like by November

- A service team goes from TypeSpec commit to released SDK in every language, for the common case, with no scheduled meeting.
- **One reviewer pool handles both spec-driven and SDK-originated work**, and no change is reviewed by two boards in two forums for the same reason.
- The board's queue is majority Deep-tier work, and smaller than it was in August.
- A team can run every check used by CI and the Review Hub, on their own machine, before they open a PR, **from the same tool**.
- Teams learn they are triggering an eight-week breaking-change process while they are still typing.
- A dashboard can answer "how long does review take and where does the time go".
- Unresolved review comments block a spec PR, and at least one agent rule class blocks on the strength of published precision data.
- **No actively-changing service depends on a single reviewer.**

The same proposal seen from a service team's side is in [Appendix I](appendices.md#i-the-view-from-a-service-team), and it is the shortest useful summary of what all of this is for.

---

## 2. Roadmap

The ten changes in [§1](#1-executive-summary) are the outcomes this proposal asks leadership to back. This section is the delivery view: seven efforts group the work needed to produce those outcomes, but one comes first. The operating model must be agreed before the programme commits to implementing it. The mapping is deliberately many-to-many. For example, requiring SDK generation depends on language readiness, deterministic checks, tracking and the shared local tool; none of those workstreams is a second proposal for SDK generation.

> **The real deadline is Ignite, not November.** Everything that reduces load without requiring a decision, meaning the reviewer copilot, advisory agents, derived intake and breaking-change classification, is pulled as early as it will go.

### First: agree the operating model

Before delivery is committed, leadership and the boards need to decide whether to merge the boards, require automated checks before work enters the review queue, replace service-wide grandfathering with recorded exceptions, and run two end-to-end pilots. Engineering can implement those decisions, but it cannot supply their authority. The backlog tracks the decision and its follow-through as effort E.

**Agreement needed in week 0:** A recorded yes or no on the board merge, the readiness gate and retiring blanket grandfathering; two named pilot services; and a named decision owner for each unresolved policy question.

**What can proceed before agreement:** Reversible work that remains useful under either answer, including baselining, agent dark launches, technical spikes and consolidating existing checks. No rollout should assume a policy answer that has not been given.

### The delivery efforts

#### A. [Roll out agentic review of data-plane TypeSpec pull requests](design-and-evidence.md#9-agentic-review)

Introduce agentic review in stages. First run it without posting comments on recently merged TypeSpec pull requests and compare its findings with what human reviewers found. Once its results are trustworthy, let it comment on live pull requests. It earns any blocking authority later, one class of finding at a time.

**First result:** In month 1, a dark launch over real merged TypeSpec pull requests produces the first credible precision and value measurements.

**What could stop it:** Nothing external. It needs evaluation and engineering work, but no policy decision.

#### B. [Generate supported SDKs during every TypeSpec review](design-and-evidence.md#105-generation-is-a-gate-merging-is-not-releasing)

As part of every TypeSpec review, automatically generate the SDKs for every language the service supports and show their public-surface changes in the same pull request. Language architects can review those surfaces there when needed, while the spec is still cheap to change, rather than through a separate review later. Generation and the surface evidence are automatic; language-architect review is not required for every change. A short [review digest](appendices.md#h-restoring-a-review-view) keeps the meaningful changes visible without making reviewers read generated files and examples by hand.

**First result:** In month 1, a readiness table identifies which languages can join this flow immediately, what blocks the others and who can answer for each language.

**What could stop it:** Generation and public-surface comparison are not yet reliable in every language. Both have to be trustworthy before that language can join the automatic flow.

#### C. [Define and encode what gets checked](design-and-evidence.md#85-the-rule-catalogue-and-the-gap-pipeline)

This effort owns the **content of the checks**. Publish what the linters already cover, record review findings that have no rule, and turn recurring mechanical findings into deterministic rules. Apply the same approach to the [breaking-change policy](design-and-evidence.md#103-breaking-changes-are-the-hard-interlock), encoding what can be decided from a diff and sending ambiguous cases to a human. Effort G then makes these checks run consistently on a laptop, in CI and in the Review Hub.

**First result:** In month 1, a historical-PR spike shows how much of the breaking-change policy can be classified mechanically and how accurately.

#### D. [Make review intake and status automatic](design-and-evidence.md#12-tracking-on-github)

When a pull request changes a public API, it enters the review process automatically. The team sees one status showing which reviews are required, which are complete, what is blocking progress and who needs to act next. Decisions and waiting time are recorded as the review moves, so nobody has to file a separate request, reconcile several trackers or reconstruct the history later. The durable record behind that status uses GitHub and the existing API Review Hub rather than a new tracking system.

**First result:** In month 1, public-API pull requests begin creating their own review records, and the existing history establishes a baseline for review volume, delay and reviewer concentration.

**What could stop it:** GitHub may not represent several parallel reviews at the required scale. A short spike tests that assumption; if it fails, the same status is surfaced from the existing API Review Hub rather than by building a new store.

#### F. [Move existing SDK Architecture Board capacity into the shared pool](design-and-evidence.md#52-capacity-is-the-binding-constraint)

Assign existing SDK Architecture Board members to data-plane review, have them complete supervised reviews with the remaining stewardship reviewer, and capture service context, precedents and known exceptions while that knowledge is still available. This is not a search for a new reviewer population: it is the transition from two pools reviewing successive forms of the same artifact to one pool reviewing TypeSpec. Automation makes that pool stretch further; it does not replace the need for someone to adjudicate difficult cases and exceptions.

**First result:** In month 1, at least two SDK Architecture Board members are assigned time for data-plane review and begin the supervised transition.

**What could stop it:** Leadership may endorse one reviewer pool without allocating board members time to work in it. The named assignments and supervised reviews make that commitment concrete.

#### G. [Run every automated check through one shared tool](design-and-evidence.md#82-the-toolbox)

This effort owns **how checks are run and delivered**, not what the rules say. Put every check that can block a pull request behind one command that teams can run before pushing. GitHub Actions and the API Review Hub invoke the same underlying tool, so a green local run means CI will be green for the same reasons. An editor experience and prebuilt environment come later only if partner-team adoption justifies them.

**First result:** In month 1, one command runs today's checks locally and returns the same verdict as CI.

**What could stop it:** Drift. If CI or the Review Hub reimplements a check instead of invoking the shared tool, local and remote results stop meaning the same thing.

Detailed resourcing assumptions, the week-0 checklist, the full backlog, sequencing rules and readiness-gate promotion criteria are in [Appendix O](appendices.md#o-detailed-delivery-plan).
ss
### Explicitly not doing

Naming exclusions is what makes the rest credible in four months.

- **Not rebuilding APIView.** The *diagnostics* move into the toolbox and CI, on the surface teams already use ([§8.2](design-and-evidence.md#82-the-toolbox)).
- **Not building a tracking system.** The ledger is GitHub plus a thin orchestrator over what already exists ([§12.1](design-and-evidence.md#121-the-review-ledger)).
- **Not merging with ARM review.** [§7.6](design-and-evidence.md#76-one-board-over-one-artifact) merges the two data-plane reviewer pools. Control-plane review stays separate and keeps its own gates.
- **Not automating the breaking change review process itself.** Automation classifies, detects and links only.
- **Not touching the release pipeline's manual approval gate.** It is a security control, not review latency ([§10.5](design-and-evidence.md#105-generation-is-a-gate-merging-is-not-releasing)).
- **Not shipping agents in all seven languages.** Three at most, chosen by readiness.
- **Not hard-blocking on agent findings this year.**
- **Not auditing existing services for undeclared exemptions.** Declaration at point of change only.