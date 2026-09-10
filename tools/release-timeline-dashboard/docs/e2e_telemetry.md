# [E2E](#term-e2e) Spec-to-SDK Telemetry: Operational Metrics Proposal

> **Purpose:** Define a set of high-value telemetry that tracks the right metrics across the
> end-to-end spec-to-SDK lifecycle and reflects operational reality against our operational-excellence
> expectations.
> **Scope:** The full [E2E](#term-e2e) process ([TypeSpec](#term-typespec) authoring →
> [spec PR](#term-spec-pr) → SDK generation → release), with
> the Azure SDK Tools Agent as one instrumented layer within it, deliberately broader than
> "just the agent." The platform currently serves internal users only.
> **Audience:** SDK leadership ([SLT](#term-slt)), engineering leads, and service-team stakeholders.
> **Status:** Proposal for review and alignment.
> **Created:** 2026-06-09 · **Revised:** 2026-08-05

---

## BLUF: One Measurement System, Three Leadership Outcomes

The proposal tracks the complete spec-to-SDK system. The leadership scorecard should contain three outcomes:

1. **Time-to-SDK:** how quickly an API reaches customers, shown as [full-calendar](#term-full-calendar),
   [post-spec](#term-post-spec), and [active-cycle](#term-active-cycle) views.
2. **On-time E2E completion:** whether every [in-scope, non-excluded SDK language](#term-in-scope-language)
   releases within a [standard operational window](#term-operational-window).
3. **Manual-intervention-free completion:** whether the flow succeeds without
   [corrective human work](#term-manual-intervention).

Lifecycle-stage, quality, platform-health, adoption, and scale metrics explain why those outcomes
move. They are important diagnostic or context measures.

> **Data-confidence caveat:** Current Power BI values and several event boundaries require
> re-verification before external use. The inventory below distinguishes available, partial, and
> not-yet-instrumented metrics. Section 7 lists the specific confidence gaps.

---

## Section 1: Complete Prioritized Metric Inventory

This is the canonical inventory. Metric IDs identify a stable measurement role rather than implying
that every metric has equal importance.

**ID legend:** **L** = leadership outcome; **S** = lifecycle and schedule diagnostic; **Q** = quality
and platform health; **C** = adoption and scale context; **B** = derived business impact.

| Order | ID | Metric | Group | Decision it supports | Reporting role | Readiness | Existing evidence/report |
|---:|---|---|---|---|---|---|---|
| **1** | **L1** | **Time-to-SDK** (full, post-spec, and active-cycle views) | Leadership outcome | Is E2E delivery becoming faster, and how much delay is within the SDK system? | Headline | Not yet measured E2E | **Partial evidence:** [Spec-timeline](https://benbp.net/azsdk-spec-timeline/) for targeted service API specs |
| **2** | **L2** | **On-time E2E completion rate** | Leadership outcome | Are all expected SDKs reliably reaching customers? | Headline | Partial; needs cross-source join | Not available |
| **3** | **L3** | **Manual-intervention-free completion rate** | Leadership outcome | Is automation removing engineering toil without shifting work elsewhere? | Headline | Partial; intervention contract needed | **Partial evidence:** [Spec-timeline](https://benbp.net/azsdk-spec-timeline/) for targeted service API specs |
| **4** | **S1** | **Spec PR cycle time** | Lifecycle stage | Is pre-merge authoring, validation, review, or approval the bottleneck? | Diagnostic | Available for tracked cohort | **Partial evidence:** [Spec-timeline](https://benbp.net/azsdk-spec-timeline/) for targeted service API specs |
| **5** | **S2** | **Generation trigger latency** | Lifecycle stage | Does automation react promptly after the spec merges? | Diagnostic / regression alert | Partial | Not available as a standalone report |
| **6** | **S3** | **Generation execution time** | Lifecycle stage | Is code generation or pipeline execution slow? | Diagnostic | Partial | Not available as a standalone report |
| **7** | **S4** | **SDK PR cycle time** | Lifecycle stage | Are CI, customization, or review delaying delivery? | Diagnostic | Available for tracked cohort | **Partial evidence:** [Spec-timeline](https://benbp.net/azsdk-spec-timeline/) for targeted service API specs |
| **8** | **S5** | **Release latency** | Lifecycle stage | Are release operations delaying an already-merged SDK? | Diagnostic | Partial | Not available |
| **9** | **S6** | **Release schedule adherence** | Schedule control | Did the SDK release by the service team's committed target date? | Diagnostic / control boundary | Not measured | Not available |
| **10** | **Q1** | **Generated-PR CI-failure rate** | Quality and health | Are generated SDK PRs healthy on first execution? | Quality guardrail | Derivable | Not available as a standalone report |
| **11** | **Q2** | **Spec iterations to SDK-ready** | Quality and health | Are authoring and validation shifting problems left? | TypeSpec diagnostic | Available for tracked cohort | **Partial evidence:** [Spec-timeline](https://benbp.net/azsdk-spec-timeline/) for targeted service API specs |
| **12** | **Q3** | **Spec validation and breaking-change findings** | Quality and health | What defects are caught before SDK generation? | TypeSpec diagnostic | Partial | Not available |
| **13** | **Q4** | **Escaped SDK breaking-change rate** | Quality and health | What customer-impacting defects escaped the E2E gates? | Customer-quality guardrail | Source validation needed | Not available |
| **14** | **Q5** | **Tool error rate** | Quality and health | Is an agent/tool dependency failing before E2E outcomes visibly regress? | Platform-health alert | Available; definition needs reconciliation | **Direct report:** [Power BI dashboard](https://msit.powerbi.com/groups/01cceac6-7b9c-4346-ad3c-efd317c2607e/orgapps/42ba6108-b0d0-405e-b977-8100c10e3b33/report/3e6fba2c-25ab-4754-8ff2-f80c380cd9ce/08c911d6311ba5b55f7b?ctid=72f988bf-86f1-41af-91ab-2d7cd011db47) |
| **15** | **Q6** | **Manual-fix rate** | Quality and health | Where does generated code require corrective editing? | L3 drill-down | Available for tracked cohort | **Partial evidence:** [Spec-timeline](https://benbp.net/azsdk-spec-timeline/) for targeted service API specs |
| **16** | **Q7** | **Review-wait cycles** | Quality and health | How much back-and-forth review churn occurs? | Diagnostic | Available for tracked cohort | **Partial evidence:** [Spec-timeline](https://benbp.net/azsdk-spec-timeline/) |
| **17** | **Q8** | **Author-nag rate** | Quality and health | How often must authors chase reviewers to make progress? | Diagnostic | Available for tracked cohort | **Partial evidence:** [Spec-timeline](https://benbp.net/azsdk-spec-timeline/) for targeted service API specs |
| **18** | **C1** | **[MEU and MEU/MAU ratio](#term-engagement-users)** | Adoption and scale | Are users returning and forming a usage habit? | Adoption context | Available in aggregate; execution-surface segmentation unavailable | **Direct report:** [Power BI dashboard](https://msit.powerbi.com/groups/01cceac6-7b9c-4346-ad3c-efd317c2607e/orgapps/42ba6108-b0d0-405e-b977-8100c10e3b33?ctid=72f988bf-86f1-41af-91ab-2d7cd011db47) |
| **19** | **C2** | **SDK releases per month** | Adoption and scale | What delivery volume does the platform support? | Scale context | Partial | **Supporting source:** [Azure SDK releases](https://azure.github.io/azure-sdk/releases/latest/index.html) |
| **20** | **C3** | **Services completing the tracked flow** | Adoption and scale | How broadly is the E2E process delivering across Azure services? | Scale context | Mostly available | Not available as a standalone report |
| **21** | **C4** | **Package downloads** | Adoption and scale | Are customers consuming the SDKs produced? | Customer-adoption context | Available by ecosystem | No single cross-ecosystem report |
| **22** | **B1** | **Cycle-time reduction** | Derived business impact | How much faster are releases after automation changes? | Leadership value framing | Needs L1 baseline | Not available |
| **23** | **B2** | **Engineering toil removed** | Derived business impact | How much engineering capacity did automation return? | Leadership value framing | Needs L3 baseline and effort model | Not available |

**Prioritization rule:** instrument the leadership outcomes first, then the stage metrics required to
explain them. Quality metrics are guardrails and root-cause signals. Adoption, scale, and derived
business-impact metrics provide context; they do not replace delivery outcomes.

---

## Section 2: Detailed Metric Definitions

Every metric uses the same metadata: name, definition, proposed calculation/[event contract](#term-event-contract),
decision, source, readiness, direction, cadence, and confidence. `TBD` means the calculation or
event contract must be defined before implementation. Logical field names are placeholders until
they are mapped to authoritative source schemas during implementation.

### Leadership outcomes

| ID | Metric name | Definition or boundary | Proposed calculation / event contract | Decision supported | Source | Readiness / baseline | Direction | Cadence | Confidence |
|---|---|---|---|---|---|---|---|---|---|
| **L1** | **Time-to-SDK** | **Full:** spec PR opened → final in-scope, non-excluded SDK released. **Post-spec:** spec PR merged → final release. **Active cycle:** full duration less approved intentional hold intervals. | `Full = max(perLanguageReleaseDate) - specPrOpenDate`; `Post-spec = max(perLanguageReleaseDate) - specPrMergeDate`; `Active = Full - duration(union(approvedHoldIntervals))`. Report median, [P90](#term-p90), and total approved hold days separately. | Is the service-team experience improving, and how much of the delay remains after the spec is ready or outside active execution? | Spec-timeline release windows + authoritative per-language release dates + documented hold events | Not yet measured E2E | Reduce median and P90 without hiding calendar or hold time | Monthly; quarterly leadership trend | Definition high; release/hold joins low |
| **L2** | **On-time E2E completion rate** | Share of eligible [release flows](#term-release-flow) where every in-scope, non-excluded language releases within a standard operational window measured from entry into SDK generation. This is independent of the service-specific committed date used by S6. | `standardDeadline = sdkGenerationEntryDate + standardCompletionWindow`; `count(flows where max(perLanguageReleaseDate) <= standardDeadline) / count(eligible flows whose standard measurement window closed)`. The standard window and abandoned-flow treatment are TBD. | Is the process completing reliably rather than reporting speed only for successes? | Release-plan status + package/release dates + timeline join | Not yet isolated | Establish baseline, then increase | Monthly | Definition medium; standard window and release source unresolved |
| **L3** | **Manual-intervention-free completion rate** | Share of eligible flows completing without regeneration, corrective SDK/spec edits, pipeline reruns, manual release intervention, or another agreed intervention **from entry into SDK generation through final release**. Expected spec authoring before SDK-generation entry does not count. | `count(completed eligible flows with interventionCountDuringFlow = 0) / count(eligible flows whose measurement window closed)`. The intervention taxonomy and treatment of incomplete flows are TBD. | Is automation removing toil and completing cleanly? | Timeline classifications + pipeline reruns + release events | Partial; intervention taxonomy not locked | Establish baseline, then increase | Monthly | Medium-low until intervention events are standardized |

### Lifecycle and schedule diagnostics

**Lifecycle map:** spec PR opened **→ S1 →** spec PR merged **→ S2 →** generation started
**→ S3 →** SDK PR opened **→ S4 →** SDK PR merged **→ S5 →** package released.
**S6 overlays the lifecycle** by comparing final release with the service team's committed target date.

| ID | Metric name | Definition or boundary | Proposed calculation / event contract | Decision supported | Source | Readiness / baseline | Direction | Cadence | Confidence |
|---|---|---|---|---|---|---|---|---|---|
| **S1** | **Spec PR cycle time** | Spec PR opened → merged | `specPrMergeDate - specPrOpenDate` (`specPRDays`); report median and P90. As a diagnostic drill-down, report validation execution duration, blocking rate, rerun count, and time-to-clear by `validationTool`. Attribute critical-path contribution only when validation intervals and the PR merge-ready event are available; do not sum runtimes for tools that execute in parallel. | Should investment target TypeSpec authoring, validation tooling, remediation guidance, review, or approval flow? | Spec-timeline `specPRDays` + validation-run events | Overall available for tracked cohort; per-tool diagnostics are partial | Reduce median and P90; reduce avoidable validation delay without weakening detection | Monthly | High for tracked S1; medium-low for per-tool attribution until event coverage is validated |
| **S2** | **Generation trigger latency** | Spec PR merged → generation pipeline started | Target: `generationStartDate - specPrMergeDate`; exact generation-start event is TBD. Existing combined measure: `firstSdkPrOpenDate - specPrMergeDate` (`pipelineGapDays`), which includes both trigger and execution time. | Is the automated trigger healthy? | Spec merge event + generation-pipeline start | Partial; combined downstream PR creation is reported at roughly 10 minutes, not fleet-validated | Hold near validated baseline; alert on regression | Weekly alert; monthly trend | Medium |
| **S3** | **Generation execution time** | Generation pipeline started → SDK PR opened, per language | `sdkPrOpenDate[language] - generationStartDate[language]`; confirm whether the pipeline emits a language-specific start. | Is generation or pipeline execution the bottleneck? | Generation pipeline + SDK PR creation | Partial | Reduce median and P90 | Monthly | Medium-low until generation-start event is confirmed |
| **S4** | **SDK PR cycle time** | SDK PR opened → merged, per language | `sdkPrMergeDate[language] - sdkPrOpenDate[language]`; report median and P90. | Should investment target CI, customization, or review flow? | Spec-timeline language lanes + GitHub checks/reviews | Available for tracked cohort | Reduce median and P90 | Monthly | High for tracked services |
| **S5** | **Release latency** | SDK PR merged → package released, per language | `perLanguageReleaseDate - sdkPrMergeDate`; authoritative release event is TBD. | Are release operations delaying delivery? | SDK PR merge + authoritative package/release event | Partial | Reduce median and P90 | Monthly | Medium-low until release source is standardized |
| **S6** | **Release schedule adherence** | Actual final in-scope SDK release compared with the service team's committed calendar date. Unlike L2, this measures a service-specific schedule commitment rather than a standard duration. | `scheduleVarianceDays = max(perLanguageReleaseDate) - committedTargetDate`; `onScheduleRate = count(variance <= 0) / count(eligible flows with a committed target)`. Version and timestamp changes to the target date. | Is elapsed calendar time intentional lead time, or did delivery miss the committed schedule? | Release-plan target date history + authoritative release dates | Not measured | Reduce positive variance; increase on-schedule rate | Monthly | Low until target-date history is retained |

Generated SDK code normally exists before the SDK PR opens. Therefore, "SDK PR merged → SDK
generated" is not a valid lifecycle stage. If a trustworthy artifact-complete event exists, split S3
into **generation started → artifact complete** and **artifact complete → SDK PR opened**.
Until a trustworthy generation-start event exists, report `pipelineGapDays` as a combined
**spec-merge-to-SDK-PR-open** measure and do not claim that it isolates S2 from S3.

### Quality and platform-health metrics

| ID | Metric name | Definition or boundary | Proposed calculation / event contract | Decision supported | Source | Readiness / baseline | Direction | Cadence | Confidence |
|---|---|---|---|---|---|---|---|---|---|
| **Q1** | **Generated-PR CI-failure rate** | Share of generated SDK PRs with a failing required CI check. | `count(generated SDK PRs with >=1 failed required check) / count(eligible generated SDK PRs)`; eligibility and rerun treatment are TBD. | Which generator, language, or pipeline failures create intervention? | GitHub checks / Pipeline Witness | Derivable | Reduce | Weekly / monthly | Medium-high after eligibility rules are locked |
| **Q2** | **Spec iterations to SDK-ready** | Spec revisions or iterations required before the API version is SDK-ready. | Use spec-timeline revision/commit count per release window; the exact definition of an iteration and SDK-ready event is TBD. | Are TypeSpec authoring and validation reducing rework? | Spec-timeline revision/commit counts | Available for tracked cohort | Reduce without weakening review | Monthly | Medium; iteration definition must be consistent |
| **Q3** | **Spec validation and breaking-change findings** | Findings raised before SDK generation by validation tools, including LintDiff and breaking-change tools, reported by tool, type, severity, blocking status, and disposition. | Count findings by `validationTool`, `findingType`, `severity`, `isBlocking`, and disposition (`fixed`, `suppressed`, `accepted`, `falsePositive`, `unresolved`). Join findings to validation runs and S1 using `releaseFlowId`, `specPrId`, and `runId`; the authoritative tool inventory and field mappings are TBD. | Which tools shift defects left, which produce actionable findings, and where should tooling or authoring guidance improve? | Validation-run results, review labels, suppressions | Partial; tool inventory and event coverage need validation | Interpret with escaped defects and dispositions; raw increases can mean better detection or more noise | Monthly | Medium-low until tool and disposition coverage is validated |
| **Q4** | **Escaped SDK breaking-change rate** | Customer-impacting SDK breaking changes that escaped required E2E gates. | Proposed: `count(validated escaped-breaking-change issues) / count(eligible released SDK flows)`; issue tagging and denominator are TBD. | Are quality gates protecting customers? | Customer-filed SDK issues; release correlation | Not ready | Reduce toward zero | Quarterly | Low until tagging and denominator are validated |
| **Q5** | **Tool error rate** | Share of eligible tool calls that return a response stating the purpose was not achieved. | `count(error-classified ToolExecuted events) / count(eligible ToolExecuted events)` from `RawEventsDependencies`, reported overall and by `toolname`. Report `clientname` only as an observed emission label; do not interpret it as CLI, Copilot App, or autonomous origin. Surface-specific reporting requires explicit `execution_surface` instrumentation. The exact error classifier must match the Power BI definition. | Which agent/tool dependency needs immediate reliability work? | Power BI + `RawEventsDependencies` | Power BI reports 31.3% from a prior window; re-verify and reconcile with 98.3% operation success | No target until the canonical classifier and baseline are established; then reduce from the validated baseline and alert on material regression | Weekly / monthly | Medium-low until definitions reconcile |
| **Q6** | **Manual-fix rate** | Share of generated SDK PRs requiring corrective hand edits; retain fix count by flow for L3. | `count(generated SDK PRs where totalManualFixes > 0) / count(eligible generated SDK PRs)`; also report `sum(totalManualFixes)` by release flow. | Which generators or scenarios create manual work? | Spec-timeline `totalManualFixes` | Available for tracked cohort | Reduce | Monthly | Medium; classification includes AI-assisted interpretation |
| **Q7** | **Review-wait cycles** | Back-and-forth review cycles per spec or SDK PR. | Report median/P90 `totalReviewWaitCycles` by PR type and language; exact cycle classifier follows spec-timeline. | Where does repeated review churn extend cycle time? | Spec-timeline `totalReviewWaitCycles` | Available for tracked cohort | Reduce avoidable cycles | Monthly | Medium |
| **Q8** | **Author-nag rate** | Share of tracked release flows requiring an author follow-up or reviewer nag. | `count(release flows where totalNags > 0) / count(tracked release flows)`; also report `sum(totalNags)`. | Where is the workflow failing to create pull automatically? | Spec-timeline `totalNags` | Available for tracked cohort | Reduce | Monthly | Medium; classification includes AI-assisted interpretation |

Spec PR cycle time alone cannot prove TypeSpec helped. Interpret S1 with Q2, Q3, review cycles, and
eventual escaped defects. Faster review with more escaped defects would not be an improvement.
Likewise, a PR taking longer after a validation finding does not prove that the validation tool caused
the delay; the tool may have exposed an existing API issue. Use execution duration to identify slow
tools, disposition patterns to identify noisy tools, and time-to-clear plus critical-path contribution
to identify remediation or workflow bottlenecks.

### Adoption, scale, and derived business impact

| ID | Metric name | Definition or boundary | Proposed calculation / event contract | Decision supported | Source | Readiness / baseline | Direction | Cadence | Confidence |
|---|---|---|---|---|---|---|---|---|---|
| **C1** | **MEU and MEU/MAU ratio** | MEU: unique devices active on at least 2 distinct days in 28 days. MEU/MAU: engaged users as a share of monthly active users across eligible events. | `MAU = dcount(devdeviceid with >=1 active day in 28d)`; `MEU = dcount(devdeviceid with >=2 distinct active days in 28d)`; `ratio = MEU / MAU`, after canonical event and traffic-exclusion filters. Report aggregate engagement until explicit execution-surface telemetry exists. | Are users returning and forming a usage habit? | Power BI + `RawEventsDependencies` | Re-baseline aggregate metric; execution-surface segmentation not available | Grow MEU and ratio | Monthly | Medium-low for aggregate engagement; low for any client-segmented interpretation |
| **C2** | **SDK releases per month** | Released SDK packages per month, overall and by language. | Count distinct released package-version-language tuples in the month; authoritative source and deduplication across ADO, release notes, and package managers are TBD. | What delivery scale does the platform support? | Azure DevOps release data, release notes/blog, package managers | Partial | Context only; do not optimize volume independently | Monthly | Medium until source and deduplication are validated |
| **C3** | **Services completing the tracked flow** | Unique services with an eligible release flow that reaches the agreed completion state. | `dcount(serviceId where flow meets L2 completion definition)`; canonical `serviceId` is TBD. | How broadly is the E2E system delivering? | Release plans + release data | Mostly available | Contextual growth | Monthly / quarterly | Medium |
| **C4** | **Package downloads** | Package downloads by ecosystem, with ecosystem-specific caveats. | Sum downloads by package and ecosystem for the reporting period; do not combine ecosystems into one total without a normalization decision. | Are customers consuming the SDKs produced? | NuGet, PyPI, npm, Maven, Go | Available | Contextual growth | Monthly / quarterly | Medium; cross-ecosystem counts are not directly comparable |
| **B1** | **Cycle-time reduction** | Change in L1 before versus after an automation change. | `baseline cohort median/P90 L1 - comparable post-change cohort median/P90 L1`; report days or weeks saved and cohort definitions. | Did automation materially accelerate customer delivery? | L1 baseline + change date | Needs baseline | Increase time saved | Per automation milestone | Low until comparable cohorts exist |
| **B2** | **Engineering toil removed** | Capacity returned by eliminating validated manual steps. | `sum(observed intervention count by type * validated effort per intervention type)`; report assumptions, range, and time period. | Did automation return meaningful engineering capacity? | L3 + workflow study | Needs baseline and model | Increase validated toil removed | Quarterly | Low; never present without methodology |

`clientname` does not reliably identify execution surface or mode. Skills invocations may emit
`copilot-cli`, while MCP tool calls from both the CLI and Copilot App emit
`github-copilot-developer`. Report `clientname` only as the observed label. Do not infer CLI,
Copilot App, or autonomous usage from `clientname` or device-ID format. Before producing
surface-segmented C1 or Q5 results, instrument explicit dimensions such as
`execution_surface = cli | copilot_app | coding_agent` and `invocation_type = skill | mcp`.

---

## Section 3: Reporting and Segmentation Contract

| Rule | Requirement |
|---|---|
| **Parallel languages** | Report each language and end E2E timing when the final in-scope language releases |
| **Excluded languages** | Remove only documented exclusions from the expected-language denominator |
| **Distribution** | Report median and P90; averages alone hide the long tail |
| **Plane** | Split [management plane](#term-management-plane) vs [data plane](#term-data-plane) |
| **Service maturity** | Split net-new services vs new API versions for existing services |
| **Population** | State whether the result is **[fleet-complete](#term-fleet-complete)**, covering every eligible release flow in the defined reporting period, or a **[fixed cohort](#term-fixed-cohort)**, covering a named and unchanged subset measured repeatedly for early trend analysis. Fixed-cohort results are diagnostic and must not be represented as fleet-wide performance. |
| **Intentional holds** | Preserve full-calendar Time-to-SDK and report total approved hold days separately. Subtract a hold from active-cycle time only when it has a standard reason code, accountable owner, start and end timestamps, and approval by the release-plan owner or delegated SDK release owner. Retain an audit history of approvals and changes. Do not silently remove planned lead time from L1. Use S6 to show whether the release met the committed target. |
| **Comparison** | Compare like-for-like cohorts when measuring improvement |
| **Metric status** | Label each number as validated, provisional, or unavailable |

| Frequency | Scorecard content | Audience |
|---|---|---|
| **Weekly** | Q5 tool health, S2 automation regression, top operational failures | Engineering |
| **Monthly** | L1-L3 outcomes, S1-S6 stage drill-down, priority quality guardrails | Team and engineering leads |
| **Quarterly** | L1-L3 trends, B1/B2 validated impact, C2-C4 scale context | Leadership |
| **On demand** | Per-service timeline and root-cause diagnostics | Service-team review |

---

## Section 4: What We Reuse and What We Build

Two existing Power BI dashboards under "MCP Tools and Skills" already cover adoption, engagement,
usage, and platform reliability. Reuse them after validating definitions. Net-new engineering should
focus on lifecycle correlation and release completion.

| Capability | Decision |
|---|---|
| MAU / MEU / MDU, lifecycle, usage by tool/skill/client | Reuse after validating client scope and canonical windows |
| Tool success, error, exception, and dimensional drill-down | Reuse after reconciling the competing success/error definitions |
| Version adoption and session depth | Retain as rollout diagnostics, not leadership outcomes |
| Spec and SDK PR timing | Extend the existing spec-timeline service model |
| Per-language release completion and dates | Build or standardize the authoritative join |
| Manual-intervention events | Standardize across timeline classification, pipeline reruns, edits, and release actions |
| Fleet-complete historical trend | Build durable correlation and storage after the cohort pilot |

The screenshot inventory of existing panels is retained in the session artifact
`files/powerbi-inventory.md`.

---

## Section 5: Spec-Timeline Dashboard Evidence

**Source:** https://github.com/benbp/azsdk-spec-timeline · **Live:** https://benbp.net/azsdk-spec-timeline/

The live dashboard uses sample data generated **April 15, 2026** and has **two data models**:

1. **Per-release timelines (19 scenarios):** the original Gantt view for a single spec PR and its
   downstream SDK PRs, now spanning both management and data plane across 19 service versions (e.g. Key Vault,
   DurableTask, Storage, Monitor, NetApp, Search, ContainerService, NGINX, Playwright).
2. **Service-level timelines (new, 8 services):** a `service-timeline` model that aggregates
   every successfully correlated spec PR and SDK PR for a service over a configurable rolling lookback window
   (`lookback.requestedDays`), grouped into **release windows**. Each release window carries hard
   metrics: `pipelineGapDays`, `totalDurationDays`, `specPRDays`, `totalNags`, `totalManualFixes`,
   `totalUniqueReviewers`, `totalReviewWaitDays`, `totalReviewWaitCycles`. The service-level summary
   adds `totalReleases`, `avgCycleTimeDays`, `avgPipelineGapDays`, `toolCallSuccessRate`, and
   `automationRate` (fraction of SDK PRs automated, 0–1), plus per-language and top-reviewer
   breakdowns.

### How we draw conclusions from it (and how not to)

- **Valid today:** per-service, per-release-window quantities for PRs successfully correlated by the
  timeline process. "Key Vault's last release window had a
  pipeline gap of X days, Y manual fixes, Z review-wait cycles" is real, sourced data, not anecdote.
- **Not yet valid:** treating 8 services or 19 PRs as a statistically representative fleet average.
  The sample is curated, not random. Conclusions should be framed as "observed across N tracked
  services," not "the SDK pipeline takes X days on average."
- **The opportunity:** the service model is the substrate for **month-over-month** comparison.
  Each release window is timestamped and self-contained, so comparing consecutive windows for one
  service is a legitimate trend, and comparing the same window across services reveals language- and
  team-level variance.

### Tracking these at scale without cherry-picking

If we quote metrics from a few hand-picked scenarios, leadership can discount them as anecdote. Two
things resolve this. **Within a service there is already no cherry-picking:** the `service-timeline`
model attempts to ingest every spec PR and SDK PR for that service over the lookback window. A
single-service result is complete only for PRs the process successfully correlates; out-of-pattern
flows and weaker data-plane provenance can create gaps. The remaining choice is **which services**
form the tracked population; three candidate options:

| Option | Definition | Pro | Con |
|---|---|---|---|
| **(a) Fleet-complete** | Every service that released in the trailing 12 months | No selection bias; defensible as full coverage; the strongest "no cherry-picking" claim | Highest run cost and maintenance |
| **(b) Fixed cohort** | A named, unchanged set of ~15–20 services across management + data plane, rerun on a schedule | Cheap; clean month-over-month comparability | The cohort is chosen, so it is a sample, not the fleet |
| **(c) Per-spec** | Run for every merged spec PR individually | Most granular | Highest run cost; harder to aggregate into a trend |

**Recommendation:** target **(a) fleet-complete** because it
removes the selection-bias objection entirely. Start with **(b) a fixed cohort** as the pragmatic
way to stand up month-over-month trends quickly, and expand toward (a) as the run is automated. The
natural tracking unit is **per-service aggregation** (the service model), rolled up across the chosen
population. Fleet-complete claims require both complete service coverage and verified PR correlation.

### What scaling actually requires (engineering prerequisites)

Reaching fleet-complete tracking is not simply a matter of running the skill more often. Two
prerequisites gate it:

- **First-class correlation.** Linking a spec PR to its downstream language PRs and to the API
  version it produced currently takes many API calls (resolving `tsp-location` files, finding the
  spec-repo commit, matching language PRs), and it breaks when teams go outside the standard process.
  **Data plane is the hardest case:** it is difficult to tell which API version a data-plane SDK was
  built from by inspecting the code, so data-plane timelines are less reliable until this metadata is
  first-class. This is the source of the data-plane provenance caveat referenced above.
- **Historical storage.** The release-plan status dashboard is an accurate source of truth but
  reflects only the **latest** state per service (latest PR, latest release). The timeline model
  captures **every iteration**, which is what trend and month-over-month analysis require. Standing up
  a durable store for that per-iteration history (work-item history or a dedicated store) is the
  missing piece before this scales, and linking the two sources would let current-state and
  historical views share one lookup.

**Framing:** the goal is not "run it for X scenarios a month" as an end in itself. It is to turn a
one-shot diagnostic into a **repeatable trend source** for S2 and L1 with the least new engineering,
by scheduling the service model and diffing windows over time.

---

## Section 6: Automation State and Measurement

The automation roadmap changes *which* metric proves the E2E process is working. Automatic SDK PR
creation after spec merge is now in place; the other items remain planned. Building the metric
before each remaining automation ships means we can prove its value instead of scrambling for a
baseline afterward. The corollary:
**capture the baseline now.** Committing a baseline of timeline data today makes future "how much did
this automation save" measurable; reconstructing it a year later is far harder, and in some cases
impossible. Historical GitHub PR information may support retrospective validation of recent
automations once a scalable correlation solution exists, but retrospective coverage and fidelity
must be verified before drawing conclusions about the intended effect.

| Automation | Status | What it removes | The metric that proves it |
|---|---|---|---|
| **Auto release-plan creation on spec PR merge to public `main`** | Planned | Manual plan creation for [GA](#term-pp-ga)-track specs | L2/L3 completion; `create_release_plan` error rate (Q5 diagnostic) |
| **Auto private-preview release-plan creation when a private spec PR gets the `Approved` label** | Planned | Manual plan creation for preview-track specs | Same as above, segmented to preview |
| **Auto SDK-generation PRs once a spec is merged** | In place; fleet timing to verify | Manual generation kickoff and the post-merge wait | S2/S3 should remain near the automated baseline; `run_generate_sdk` error rate diagnoses regressions |
| **Auto SDK release once generation and checks pass** | Planned | Manual release trigger | L1-L3 outcomes; S5 isolates release latency; `release_sdk` error rate diagnoses failures |

**So-what:** L1-L3 prove whether the process delivers. S2/S3 prove that SDK-PR auto-creation
landed and stays healthy. Q5 cuts for `create_release_plan`,
`run_generate_sdk`, and `release_sdk` diagnose which automated component failed.

---

## Section 7: Data Sources and Confidence

It has been a while since the Power BI metrics were reviewed end-to-end. Before presenting any
number externally, run a re-verification pass and record confidence. Known issues to resolve:

| Issue | Why it matters | Action |
|---|---|---|
| **Two "works?" numbers** (98.3% success vs 31.3% error) | Presenting either alone misleads; they measure different things, so a numeric Q5 target is premature | Reconcile definitions, document which classifier is canonical for Q5, establish a validated baseline, and only then set a target |
| **Stale baselines** | `telemetry-baselines.md` is as-of 2026-04-09 with many `TBD`; the old "63 MEU" is stale | Re-query MEU/MAU/error rate; refresh the baselines file; stop citing April numbers as current |
| **MAU differs across panels** (260 vs 479 vs 518) | The same term uses different scopes or windows, which erodes trust if unexplained. We also need to distinguish service-team users, SDK-team users, and test traffic confidently. | Pick one canonical MAU/MEU definition, define exclusion rules for internal test traffic, and label every panel with its scope and window |
| **Execution-surface segmentation unavailable** | `clientname` reflects invocation behavior, not a unique execution surface: Skills may emit `copilot-cli`, while MCP tool calls from both the CLI and Copilot App emit `github-copilot-developer`. Client-name-only filters therefore cannot distinguish CLI, Copilot App, or autonomous traffic. | Preserve `clientname` as an observed label. Instrument explicit `execution_surface = cli \| copilot_app \| coding_agent` and `invocation_type = skill \| mcp`, then validate coverage before publishing segmented C1 or Q5 results. Do not infer surface or mode from `clientname` or device-ID format. |

**Deliverable:** a one-page "metric definitions and confidence" sheet accompanying the dashboards,
so every number shown has a stated definition, source query, and confidence level. This is the
single most credibility-building item.

The source inventory below shows what is available; it does not imply that every available signal
belongs on the scorecard.

| Source | Cluster / location | Best for |
|---|---|---|
| **DevDiv Kusto** | `ddazureclients.kusto.windows.net` (`AzureDevExp`, `AzSdkToolsMcp`) | Agent/tool/skill telemetry: C1 adoption and Q5 errors/exceptions |
| **Pipeline Witness / Azure Data Explorer** | `azsdkengsys.westus2` (Pipelines) | Pipeline timing, test/failure classification, reruns, and release tracking for S2-S6, Q1, and L3 |
| **1ES Kusto (CloudMine)** | `1es.kusto.windows.net` | GitHub/ADO usage as an alternative to the GitHub API: adoption, PR/issue counts, thrive metrics (PR completion time) |
| **1ES Hosted Pools** | `azsdk-pool` resource + hosted-pools Kusto | CI queue delays and pool reliability for deep thrive analysis |
| **Spec-timeline** | `benbp/azsdk-spec-timeline` | Release-window timing, nags, manual fixes, review cycles, and the foundation for L1 and S1-S4 |
| **Release-plan status dashboard** | Azure DevOps release-plan work items | Authoritative **current** state of release plans (latest PR/release per service); complements the timeline model, which supplies the historical per-iteration detail the work items do not retain |
| **Package-manager stats** | NuGet, PyPI, npm, Maven, Go | Download/adoption counts (context, not a headline [OpEx](#term-opex) metric) |

**Curation principle:** pull a metric into the headline set only if it maps to a product decision or
answers one of leadership's questions. High-volume sources (downloads, raw CI counts) are context, not
leads. We are optimizing for a few trusted metrics, not maximum telemetry.

---

## Section 8: Proposed Prioritized Delivery Timeline

**Planning status:** all dates in this section are proposals pending Engineering review and detailed
implementation planning. They are not committed delivery dates. Engineering review must validate
dependencies, resourcing, sequencing, and effort before the timeline is baselined.

**Proposed goal:** show a credible E2E dashboard prototype by **August 21**, deliver a fixed-cohort
E2E baseline by **September 11**, and automate its monthly refresh by **September 18**. Priorities
balance leadership impact with engineering effort: high-impact, low-effort work comes first.

| Priority | Engineering deliverable | Why it matters | Impact | Effort | Proposed target | Definition of done |
|---|---|---|---|---|---|---|
| **P0** | **Lock the lifecycle metric contract** | Prevents teams from building incompatible definitions | High | Low | **Aug 21** | Definition, source, denominator or boundary, exclusions, freshness, and confidence locked for L1-L3 and S1-S6 |
| **P0** | **Dashboard v0 using available timeline data** | Gives stakeholders something concrete to validate | High | Low–Med | **Aug 21** | Working dashboard/page shows the lifecycle, available cohort values for spec PR cycle time and spec merge → SDK PR open, population limits, and placeholders for remaining stages; not represented as a fleet baseline |
| **P1** | **Instrument and baseline S1–S3** | Separates service-team/spec delay from automated generation delay | High | Medium | **Aug 28** | Median/P90 for spec PR cycle time, generation trigger latency, and generation execution time; overall and split by plane, with per-language generation timing |
| **P1** | **Instrument and baseline S4–S6** | Identifies whether CI/review, release operations, or planned scheduling explains the remaining elapsed time | High | Medium | **Sep 4** | Median/P90 SDK PR cycle time and release latency per language; schedule variance and on-schedule rate; generated-PR CI-failure and manual-fix rates included as diagnostics |
| **P2** | **E2E outcome pilot** | Produces the leadership-level measures of speed, completion, and automation quality | Highest | High | **Sep 11** | L1-L3 reported for a fixed representative cohort, split by plane and net-new vs existing service |
| **P2** | **Automate refresh and propose fleet scale-up** | Turns the pilot into an operating metric rather than a one-time analysis | High | Medium | **Sep 18** | Scheduled monthly cohort refresh, durable output, named operating owner, and a bounded backlog for fleet coverage, correlation metadata, and historical storage |

### Proposed weekly decision gates

| Proposed date | Decision |
|---|---|
| **Aug 21** | Is dashboard v0 clear enough to validate the lifecycle and metric definitions with stakeholders? |
| **Aug 28** | Which pre-generation stage is the highest-priority bottleneck? |
| **Sep 4** | Which SDK PR or release-stage failure should Engineering address first? |
| **Sep 11** | Approve the E2E metric set based on the fixed-cohort pilot |
| **Sep 18** | Approve the monthly operating cadence and next investment required for fleet coverage |

**Scope guardrail:** the August 21 dashboard is a transparent prototype built from available data.
Leadership-facing timing claims require fleet-complete coverage; fixed-cohort results remain
diagnostic and must be labeled as such until fleet coverage is validated.

---

## Glossary and Acronyms

| Term | Meaning |
|---|---|
| <a name="term-active-cycle"></a>**Active-cycle Time-to-SDK** | Full-calendar Time-to-SDK minus approved intentional hold intervals; total hold time remains visible separately |
| <a name="term-cca"></a>**CCA** | Copilot Coding Agent, GitHub's autonomous coding agent |
| <a name="term-data-plane"></a>**Data plane / DPG** | The service APIs used to operate on customer data or service resources; DPG means data-plane generated SDK |
| <a name="term-e2e"></a>**E2E** | End to end; here, the complete TypeSpec-authoring-through-SDK-release lifecycle |
| <a name="term-event-contract"></a>**Event contract** | The agreed event name, timestamp boundary, required fields, and source used to calculate a metric consistently |
| <a name="term-fixed-cohort"></a>**Fixed cohort** | A named, unchanged subset of services or releases measured repeatedly for early trend analysis; results are diagnostic and not fleet-wide |
| <a name="term-fleet-complete"></a>**Fleet-complete** | A result covering every eligible release flow in the defined reporting period, with verified correlation across required lifecycle events |
| <a name="term-full-calendar"></a>**Full-calendar Time-to-SDK** | Elapsed calendar time from spec PR opening until the final in-scope SDK language releases, including intentional holds |
| <a name="term-in-scope-language"></a>**In-scope, non-excluded language** | An SDK language expected for the release after removing only exclusions that are explicitly documented in the release plan |
| <a name="term-management-plane"></a>**Management plane** | APIs used to create, configure, and manage Azure resources, typically represented through Azure Resource Manager |
| <a name="term-manual-intervention"></a>**Manual intervention** | Corrective work required after SDK-generation entry, such as regeneration, corrective code or spec edits, pipeline reruns, or a manual release action |
| <a name="term-engagement-users"></a>**MAU / MEU / MDU** | Monthly Active / Meaningful-Engaged / Dedicated Users, active on ≥1 / ≥2 / ≥10 distinct days in a 28-day window |
| <a name="term-opex"></a>**OpEx** | Operational Excellence, the leadership framework used to assess operating effectiveness |
| <a name="term-operational-window"></a>**Standard operational window** | A common duration, measured from SDK-generation entry, within which all expected SDK languages should release; unlike S6, it is not service-specific |
| <a name="term-p90"></a>**P90** | 90th percentile: 90% of measured release flows complete at or below this duration; the slowest 10% take longer |
| <a name="term-post-spec"></a>**Post-spec Time-to-SDK** | Elapsed time from spec PR merge until the final in-scope SDK language releases |
| <a name="term-release-flow"></a>**Release flow** | One API-version delivery instance correlated across its spec PR, generated SDK PRs, and final in-scope package releases |
| <a name="term-pp-ga"></a>**PP / GA** | Public Preview / General Availability release milestones |
| <a name="term-sla"></a>**SLA** | Service-Level Agreement; here, a target on pipeline-gap or Time-to-SDK |
| <a name="term-slt"></a>**SLT** | Senior Leadership Team |
| <a name="term-spec-pr"></a>**Spec PR** | A pull request that proposes an API specification change in the Azure REST API specifications repository |
| <a name="term-typespec"></a>**TypeSpec** | Microsoft's language for defining APIs and generating API descriptions and SDK inputs |
