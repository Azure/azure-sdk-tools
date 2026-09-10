# E2E telemetry display coverage and investment assessment

Research date: **August 11, 2026**

This document maps the canonical metrics in [`e2e_telemetry.md`](e2e_telemetry.md) to the current V1 timeline, the proposed V2 collection architecture, and the changes or investments required to display each metric credibly.

It answers four questions:

1. Which canonical E2E metrics are missing from the current site?
2. Which can V2 display from Release Plan, GitHub, pipeline, and package data?
3. Which require new instrumentation, policy, or external systems?
4. Is a separate fleet-level metrics page feasible and appropriate?

This is a display and feasibility assessment. It does not make currently incomplete data authoritative.

## Executive recommendation

V2 can natively support a useful but intentionally narrow first metric set:

- **High confidence:** S1 Spec PR cycle time, S4 SDK PR cycle time, and Q7 review-wait cycles.
- **Provisional where covered:** L1 full/post-spec Time-to-SDK, S2 generation trigger latency, S3 generation execution time, and S5 release latency.
- **Provisional classifier:** Q8 author-nag rate.
- **Provisional scale context after deduplication:** C2 SDK releases per month and C3 services completing the flow.

V2 should not initially publish canonical values for:

- Active-cycle L1, because approved hold intervals do not exist.
- L2, until the standard operational window and eligibility policy are approved.
- L3/Q6, until manual intervention is explicitly instrumented.
- S6, until a true committed target date and its change history exist.
- Q3/Q4, until validation findings and escaped customer issues have stable contracts.
- Q5/C1, which belong in Kusto/Power BI until their definitions and execution-surface telemetry are corrected.
- C4, unless package-adoption analysis becomes an explicit product requirement.
- B1/B2, until comparable historical cohorts and an effort model exist.

A separate fleet-level metrics page is technically feasible and architecturally preferable to mixing leadership trends into the operational portfolio. It should remain out of initial implementation scope until a fixed-cohort proof establishes metric coverage, denominator rules, payload size, privacy, and evidence drill-down.

The implemented dashboard uses a **core-correlated tracked cohort** for that proof. It inventories 180 days of management-plane Release Plans, fast-fails snapshots without an explicit Release Plan ID or exact spec PR, and publishes only flows whose earliest collected event falls inside the same window. This prevents reused historical PRs or runs from extending scorecard trends before the cohort boundary. Missing in-window GitHub, Azure Pipeline, or release-version evidence remains visible through metric-specific incomplete results. Release-pipeline URL coverage is diagnostic for the current observed release metrics. This cohort must not be interpreted as fleet-wide performance.

## Status vocabulary

Every metric should use one of these display states:

| Status | Meaning |
|---|---|
| **Validated candidate** | The required boundaries come from authoritative linked systems and the definition is stable enough to implement and validate. |
| **Provisional** | The metric is useful for covered flows, but source coverage, classifier precision, or policy is incomplete. |
| **Unavailable** | A required boundary, denominator, or event contract does not exist. The UI must omit the value rather than show zero. |
| **External** | The metric is available or better maintained in another reporting system and should be linked rather than duplicated. |
| **Deferred** | It is derivable only after a baseline, integration, or product decision that is outside the initial timeline scope. |

Event confidence is separate:

- **Authoritative:** exact event from its system of record.
- **Observed:** a Release Plan revision records that a state existed at that timestamp.
- **Inferred:** deterministic classification from other events.

## Current site coverage

The existing V1 site has selected per-release and per-service metrics, not the canonical E2E scorecard:

| Existing display | Closest canonical metric | Limitation |
|---|---|---|
| Spec/API review duration (`specPRDays`) | S1 | Good for successfully correlated tracked flows; not a fleet distribution. |
| Pipeline gap: spec merge -> first SDK PR | Combined S2+S3 | It cannot isolate automation trigger latency from generation execution. |
| Fastest/slowest SDK PR and per-language duration | S4 | Good PR boundaries, but no canonical cohort/denominator metadata. |
| End-to-end duration | Partial L1 | V1 often ends at last SDK PR or an inferred release; it does not consistently end at final package publication. |
| Review wait and wait-cycle count | Q7 | Classifier needs a locked cycle definition and bot treatment. |
| Author nags | Q8 | Phrase-based and partly AI-assisted; precision is not published. |
| Manual fixes | Q6/L3 diagnostic | Inferred from phrases and edits, not an explicit intervention event. |
| Release gap and pending releases | S5 diagnostic | Release enrichment is incomplete and historically inferred by pipeline naming. |
| Automation rate | Context for L3 | PR author/title classification does not prove intervention-free completion. |
| Tool calls and success rate | Partial Q5 context | Selected CSV telemetry does not use the canonical Kusto error classifier. |
| Reviewers, PR edits, active time | Supporting diagnostics | Useful but not canonical headline metrics. |

The V2 repository has no implemented site yet. Its architecture already proposes plan, service, portfolio, and aggregate payloads, but it does not yet define a canonical metric registry or map all 23 E2E metrics to display rules.

## What the planned sources can prove

### Release Plan parent revisions

Best for:

- Release-flow identity and current lifecycle
- Plane, release type, service/product identity
- Intended languages where populated
- Language exclusions and their revisions
- Generation pipeline URL/status observations
- SDK PR URL history
- Release status/version observations
- Planned release month

Limitations:

- One mutable field per language cannot represent multiple packages, attempts, or PRs without reconstructing revisions.
- PR status is stale: 459 of 948 released language instances in the one-year profile retained a nonterminal ADO PR status.
- Finished-plan release pipeline URL coverage ranged from 0% to approximately 5% by language.
- Finished-plan released-version coverage was approximately 53-60%.
- The one-year profile contains 197 abandoned plans out of 652. Abandoned-flow policy is therefore a material denominator decision, not a negligible edge case.
- A revision timestamp is when orchestration updated ADO, not necessarily when the external event occurred.

### API Spec child revisions

Best for:

- Active and previous spec PR links
- API version and definition type
- Spec PR replacement history

Limitations:

- The active PR is mutable.
- Child histories are sparse; the sampled median was one revision.
- Review-history HTML is useful recovery evidence but not a structured event contract.

### GitHub

Best for:

- Exact PR created, ready-for-review, merged, closed, and updated timestamps
- Draft transitions
- Commits
- Reviews and changes requested
- Issue and review comments
- Requested reviewers and labels
- Current check state

Limitations:

- Historical first-run/required-check failures are not reliably represented by a single current query.
- Large-scale comments, reviews, commits, and check histories require caching and pagination.
- GitHub cannot identify a Release Plan reliably unless the plan supplies the correlation.

### Azure Pipelines and Pipeline Witness

Best for:

- Queue, start, and finish timestamps
- Per-stage/job result
- Failures and reruns
- Release approval and execution
- Separating S2 from S3 and measuring S5

Limitations:

- Exact generation/release run linkage is incomplete in historical Release Plans.
- Pipeline-name inference is not reliable enough for a fleet metric.
- Failure classification needs stable stage/job categories.

### Package and release sources

Best for:

- Exact package/version publication tuples
- Publication timestamp
- Release count and completion

Limitations:

- APIs and historical behavior differ across NuGet, PyPI, npm, Maven, and Go.
- Release notes, ADO, and package managers can duplicate the same publication.
- Download counts are not directly comparable across ecosystems.

### DevDiv Kusto and Power BI

Best for:

- C1 engagement
- Q5 tool errors/exceptions
- Tool/skill/client usage

Limitations:

- Competing success/error definitions require reconciliation.
- `clientname` is not an execution-surface identifier.
- These private telemetry sources are not part of the proposed public static JSON pipeline.

## Complete metric assessment

### Leadership outcomes

| ID | Metric | Current V1 | Feasibility from planned V2 data | Required changes/investment | Recommendation |
|---|---|---|---|---|---|
| **L1** | Time-to-SDK | Partial end-to-end duration; release boundary inconsistent | **Full/post-spec: provisional.** Exact spec open/merge comes from GitHub; final release can be calculated where every intended language has an authoritative publication time. **Active-cycle: unavailable.** | Add exact per-language publication events, intended-language snapshot, terminal/censored rules, approved hold event contract, and p50/P90 aggregation. | Display full/post-spec on plan/service only when complete; label population and coverage. Do not show active-cycle until holds qualify. Future aggregate candidate. |
| **L2** | On-time E2E completion rate | Missing | Technically derivable after generation entry, final intended-language releases, and a standard window exist. | Approve standard completion window, eligible denominator, abandoned/failed treatment, exclusion rules, and closed measurement-window rule. Improve release completeness. | Do not display initially. Prototype only in aggregate-page feasibility work after policy approval. |
| **L3** | Manual-intervention-free completion rate | Partial heuristic manual-fix counts | Not canonical from planned core data. GitHub can show human touches, and pipelines can show reruns, but neither proves the agreed intervention taxonomy. | Define typed interventions, stage boundary, regeneration/rerun rules, corrective spec/SDK edits, manual release actions, and incomplete-flow denominator. Instrument events with Release Plan/artifact IDs. | Show evidence such as "human-touched" at plan level if useful, but do not label it L3. Defer the fleet rate. |

#### L1 display behavior

For a complete flow:

```text
Full = max(authoritative release time for each intended, non-excluded language)
       - authoritative spec PR opened time

Post-spec = max(authoritative release time)
            - authoritative spec PR merged time
```

The metric result must state:

- Intended languages
- Released languages
- Explicit exclusions
- Missing/unknown languages
- Final boundary source and confidence
- Whether the flow is complete, censored, abandoned, or ineligible

An open flow can show current age, but it must not enter a completed-flow percentile as if `now` were a release boundary. Abandoned flows must remain a separate outcome until policy explicitly defines eligibility for each rate; they cannot be silently dropped from rate denominators or treated as censored successes.

### Lifecycle and schedule diagnostics

| ID | Metric | Current V1 | Feasibility from planned V2 data | Required changes/investment | Recommendation |
|---|---|---|---|---|---|
| **S1** | Spec PR cycle time | Displayed as `specPRDays` | **Validated candidate.** GitHub provides exact open and merge timestamps. | Lock treatment of draft, closed-unmerged, replaced/shared spec PRs, and censored flows. Publish p50/P90 and evidence IDs. | Native plan, service, and aggregate metric. |
| **S2** | Generation trigger latency | Combined into pipeline gap | **Provisional.** Exact where a correlated generation pipeline start exists; observed fallback from status revision is lower confidence. | Persist exact generation run ID, queue/start timestamp, language/package/artifact ID, retry number, and trigger. Establish source-coverage threshold for aggregate use. | Show exact S2 where covered. Otherwise label the combined S2+S3 interval; never silently call it trigger latency. |
| **S3** | Generation execution time | Missing as a separate stage | **Provisional.** Exact generation start plus GitHub PR open can produce a value for each generated language. Historical runs commonly generate multiple languages, so a shared run start may be the only authoritative start boundary. | Map each generation attempt to language/package/PR, preserve retries and failed attempts, record whether the start is shared or language-specific, and decide whether artifact-complete is a separate boundary. | Native plan/service metric for covered runs. Label shared-start values because they include cross-language sequencing or queue effects; aggregate only after coverage validation. |
| **S4** | SDK delivery cycle | Displayed per language | **Validated candidate.** Exact GitHub boundaries from the earliest attributed SDK PR attempt through the final successful merge. | Preserve per-attempt diagnostics, reject contradictory Release Plan identity, and expose incomplete attempt history. | Native plan/service metric. The aggregate includes replacement and retry elapsed time; individual PR cycles remain diagnostic evidence. |
| **S5** | Release latency | Partial release gap | **Provisional.** Exact PR merge plus package publication is sufficient where joined. | Standardize publication source, tuple dedupe, release run/approval events, and version/timestamp completeness. | Native plan/service metric with source confidence; aggregate only above a coverage threshold. |
| **S6** | Release schedule adherence | Missing | **Unavailable.** `SDKReleasemonth` is not a committed calendar date and does not distinguish target changes. | Add committed target date, accountable owner, change history/reason, approval, and final release. Decide eligibility when target changes. | Do not approximate from release month. Defer until the Release Plan contract changes. |

#### Combined S2+S3 fallback

When generation start is missing, V2 can still calculate:

```text
spec merge -> SDK PR opened
```

The UI should call this **Spec-to-SDK PR gap**, not generation trigger latency or execution time. Its tooltip must say it combines automation response, queue, generation, and PR creation.

### Quality and platform health

| ID | Metric | Current V1 | Feasibility from planned V2 data | Required changes/investment | Recommendation |
|---|---|---|---|---|---|
| **Q1** | Generated-PR CI-failure rate | Missing as a standalone rate | **Provisional.** Current checks and exact pipeline runs can identify failures; historical first-attempt required checks need retained snapshots or Pipeline Witness. | Define generated-PR eligibility, required checks, first attempt versus any attempt, reruns, cancellation, and bot/infrastructure failures. | Plan diagnostic first; future aggregate guardrail after history validation. |
| **Q2** | Spec iterations to SDK-ready | PR edits/commits are shown, not canonical iterations | **Provisional proxy.** GitHub commits and spec PR replacements are available, but "iteration" and "SDK-ready" are not locked. | Define iteration boundary (push, validation cycle, review cycle, PR replacement) and SDK-ready event. | Display underlying commits/review cycles now; defer the Q2 label until the definition is approved. |
| **Q3** | Spec validation and breaking-change findings | Missing | **Unavailable canonically.** Labels and suppressions are incomplete proxies. | Emit finding-level tool, type, severity, blocking state, disposition, run ID, and flow ID. Validate tool inventory. | Keep out of native scorecard. Link to validation evidence where available; invest if TypeSpec diagnostics are a V2 goal. |
| **Q4** | Escaped SDK breaking-change rate | Missing | **Unavailable.** The collector has no validated customer-issue-to-release correlation. | Add issue taxonomy/tagging, validated escape review, affected package/version, flow ID, and eligible release denominator. | Not realistic for initial V2. Maintain in a customer-quality system until the contract exists. |
| **Q5** | Tool error rate | Selected tool-call success for some samples | **External.** Canonical data is in DevDiv Kusto/Power BI, not planned public JSON. | Reconcile 98.3% success versus 31.3% error, lock classifier/traffic exclusions, add `execution_surface` and `invocation_type`. | Link to Power BI or a validated exported aggregate. Do not reproduce from V1 CSV logic. |
| **Q6** | Manual-fix rate | Displayed heuristically | **Unavailable canonically; provisional proxy possible.** | Same typed intervention contract as L3; distinguish corrective hand edit from normal customization, bot commit, regeneration, and AI-assisted edit. | Do not call heuristic counts Q6. Show explicit intervention evidence only; defer rate. |
| **Q7** | Review-wait cycles | Displayed with duration/cycle count | **Validated candidate after classifier lock.** GitHub events are sufficient. | Define ready boundary, response event, author-response reset, changes-requested cycle, bot exclusions, and PR replacement treatment. | Native plan/service/aggregate diagnostic with p50/P90. |
| **Q8** | Author-nag rate | Displayed through phrase detection | **Provisional.** Public comments can support an explainable classifier. | Lock phrase/mention/action classifier, sample precision/recall, publish classifier version, and define flow denominator. Q8 measures how often authors must follow up for reviewer or workflow action; it does not measure whether automation created a PR. | Native plan/service diagnostic with provisional badge; aggregate only with precision and minimum-sample disclosure. |

### Adoption, scale, and business impact

| ID | Metric | Current V1 | Feasibility from planned V2 data | Required changes/investment | Recommendation |
|---|---|---|---|---|---|
| **C1** | MEU and MEU/MAU | Missing | **External.** Timeline data does not contain the eligible usage population. | Re-baseline Kusto definitions, remove test traffic, add explicit execution surface and invocation type. | Link to Power BI; do not publish identities or infer surfaces in V2. |
| **C2** | SDK releases per month | Release counts exist for selected service data | **Provisional.** Package/version/language publication tuples can be aggregated. | Pick authoritative publication sources, dedupe ADO/release notes/registries, handle multi-package plans, and define preview/GA counting. | Portfolio context and future metrics page candidate. |
| **C3** | Services completing tracked flow | Missing as a standalone fleet number | **Provisional.** Release Plans provide candidate service/product IDs and completion evidence, but identity changes and incomplete path coverage can split or merge services incorrectly. | Complete the P2 stable-service-identity investment, define L2-like completion, and handle renamed paths/products and multiple plans per service. | Portfolio context and future metrics page candidate only after identity coverage is measured; retain fleet/fixed-cohort label. |
| **C4** | Package downloads | Missing | **Deferred/external.** Requires ecosystem-specific integrations. | Add package identity mapping and per-ecosystem APIs; preserve separate scales and caveats. | Not appropriate for initial timeline scope. Never combine ecosystems into a single unqualified total. |
| **B1** | Cycle-time reduction | Missing | **Deferred.** Derivable once L1 has a trustworthy historical baseline and automation change dates. | Define comparable pre/post cohorts, maturation window, confounders, and median/P90 difference. | Future aggregate analysis only; not a plan-level metric. |
| **B2** | Engineering toil removed | Missing | **Unavailable.** Event count alone cannot establish hours saved. | Validate intervention taxonomy, time-per-intervention study, uncertainty ranges, and period/cohort. | Keep outside V2 until a reviewed effort model exists. |

## Required changes by layer

### Collector changes

#### Required for initial high-value lifecycle metrics

1. Collect all historical Release Plan and API Spec child revisions.
2. Extract every time-bounded spec/SDK PR edge.
3. Fetch exact GitHub PR lifecycle, review, comment, and commit events.
4. Parse exact generation build IDs from Release Plan fields and fetch build timelines.
5. Join exact package publication version/time from an approved source.
6. Preserve intended language/package and exclusion history.

#### Required for provisional quality metrics

1. Retain active-PR required-check snapshots or use Pipeline Witness history.
2. Preserve generation/release attempts and reruns rather than only final state.
3. Version Q7 and Q8 classifiers.
4. Attach source coverage and confidence to every derived result.

#### Additional investment

- Hold interval events for active-cycle L1
- Standard completion policy for L2
- Intervention events for L3/Q6
- Committed target-date history for S6
- Finding/disposition events for Q3
- Customer-issue escape taxonomy for Q4
- Explicit execution surface for Q5/C1

### Canonical data model changes

The authoritative implementation contract for these changes is now in [`v2-architecture-plan.md`](v2-architecture-plan.md#metric-definitions-and-results). The examples below summarize the requirements that led to that contract.

Add a metric registry:

```json
{
  "id": "S4",
  "version": 1,
  "name": "SDK PR cycle time",
  "unit": "hours",
  "scope": "plan-language-pr",
  "startEvent": "sdk.pr.created",
  "endEvent": "sdk.pr.merged",
  "distribution": ["p50", "p90"],
  "status": "validated"
}
```

Add per-flow metric results:

```json
{
  "metricId": "S4",
  "definitionVersion": 1,
  "status": "validated",
  "value": 167.6,
  "unit": "hours",
  "startEventId": "github:...:created",
  "endEventId": "github:...:merged",
  "confidence": "authoritative",
  "population": {
    "flow": "35326",
    "language": ".NET",
    "artifact": "Azure.ResourceManager.NetApp"
  }
}
```

Each result needs:

- Metric ID and definition version
- Scope: flow, language, PR, service, cohort, or fleet
- Value/unit or numerator/denominator
- Start/end evidence or contributing facts
- Status/confidence
- Complete, censored, excluded, incomplete, or ineligible outcome
- Missing-boundary reason
- Source coverage

### Static JSON changes

Possible plan payload:

```text
plans/<id>.json
  metrics[]
  metricEvidence[]
  metricCoverage
```

Possible service payload:

```text
services/<service>.json
  metricDistributions
  metricTrends
  metricCoverage
```

Possible future aggregate payloads:

```text
aggregates/
  metric-definitions.json
  scorecard.json
  lifecycle-monthly.json
  quality-monthly.json
  cohorts.json
  coverage.json
```

Aggregates must be precomputed. The browser should not fetch every plan and recompute fleet statistics.

### Plan and service UI changes

#### KPI cards

Replace generic/average-only cards with:

- L1 full/post-spec when complete
- S1 Spec PR cycle
- Exact S2/S3 or one clearly labeled combined gap
- S4 SDK PR cycle by language
- S5 release latency when authoritative
- Q7 review cycles
- Provisional Q8 nag rate

Cards must show:

- Median/P90 where aggregated
- Population and time period
- Validated/provisional status
- Source confidence
- Coverage or missing-boundary explanation
- Click-through to evidence

#### Timeline evidence

Selecting a metric should highlight its start and end events and the interval between them. A provisional boundary should use a distinct visual treatment and explain its fallback.

#### Missing data

Use:

- `Unavailable: generation start not recorded`
- `Incomplete: 1 of 5 intended languages has no release event`
- `Censored: flow is still active`
- `Excluded: Java approved for exclusion`

Never render these as `0d`.

#### Population labels

Every aggregate must say:

- `Fleet-complete` or `Fixed cohort`
- Eligible flow count
- Included flow count
- Censored/incomplete/excluded count
- Minimum sample suppression, if applied

## Separate fleet metrics page

### Feasibility

Yes. The planned static architecture already supports precomputed aggregate shards and URL-driven views. A separate analytical page is better than overloading the portfolio:

- **Portfolio:** current operational state, search, filters, action owner.
- **Service/plan timeline:** detailed diagnosis and evidence.
- **Metrics page:** historical outcomes, distributions, cohorts, and trends.

### Possible route

```text
?view=metrics
```

### Possible information architecture

#### Scorecard

- L1 full Time-to-SDK p50/P90
- L1 post-spec Time-to-SDK p50/P90
- L2 on-time completion rate, only after policy approval
- L3 intervention-free rate, only after instrumentation

Each card includes status, period, population, coverage, freshness, and confidence.

#### Lifecycle decomposition

- S1-S5 p50/P90
- Per-language S3-S5
- Full versus post-spec difference
- Combined S2+S3 coverage when exact starts are absent
- S6 only after target history exists

#### Quality

- Q1 generated-PR CI-failure rate after eligibility/history validation
- Q6 after intervention instrumentation
- Q7 review cycles
- Q8 provisional nag rate
- Links to external Q5

#### Scale context

- C2 releases/month
- C3 services completing the flow
- External links for C1 and C4 rather than mixed-source headline totals

#### Filters

- Reporting period
- Management/data plane
- Net-new/existing service
- Language
- Preview/GA
- Release Plan creation source
- Fixed cohort/fleet
- Metric confidence

#### Coverage and confidence

A persistent panel should explain why the visible population differs from all Release Plans:

- Eligible
- Included
- Censored
- Incomplete
- Excluded
- Approximate
- Collector/source failures

#### Evidence drill-down

Clicking a metric segment or outlier should open the corresponding filtered service/plan list. Aggregate JSON should carry a deterministic filter descriptor or contributor IDs.

### Why it is not initial scope

The page would be easy to render but hard to make trustworthy. It should not be scoped until:

1. L1 and S1-S5 definitions are versioned.
2. Release boundary coverage is measured and acceptable.
3. Fixed-cohort/fleet-complete labeling is enforced.
4. Minimum sample sizes and censoring are agreed.
5. Aggregate payloads are proven small and precomputed.
6. Each number can drill into evidence.
7. Public-data/privacy review approves all dimensions.

### Metrics that should remain external

| Metric | Reason |
|---|---|
| C1 | Requires private usage telemetry, canonical traffic exclusions, and execution-surface instrumentation. |
| Q5 | Existing Power BI/Kusto should remain canonical after classifier reconciliation. |
| C4 | Ecosystem-specific adoption data has different semantics and adds substantial unrelated collection. |
| Q4 | Customer escape classification belongs in a reviewed quality process, not inferred from issue text. |
| B2 | Effort saved requires a validated workflow study and uncertainty model. |

## Investment priorities

These metric-oriented priorities refine the source-oriented backlog in [`data-gap-backlog.md`](data-gap-backlog.md) and must remain aligned with it.

### P0: trusted lifecycle outcomes

| Investment | Unlocks |
|---|---|
| Exact generation attempt/run IDs and timestamps | S2, S3, Q1, retry diagnostics |
| Exact package publication time/version and release run | L1, L2, S5, C2 |
| Stable Release Plan and artifact IDs in every generated PR/run | Fleet correlation and all metrics |
| Explicit intended language/package artifacts | L1/L2 denominator, completion, C2 |
| Metric eligibility, censoring, confidence, and source coverage contract | Trustworthy plan/service/fleet display |

### P1: completion, schedule, and intervention

| Investment | Unlocks |
|---|---|
| Standard operational window and abandoned-flow policy | L2 |
| Typed intervention events | L3, Q6, B2 foundation |
| Approved hold intervals | Active-cycle L1 |
| Committed target-date history | S6 |
| Generation/release retry, failure, and approval events | Q1, L3, operational attribution |

### P2: quality diagnostics

| Investment | Unlocks |
|---|---|
| Historical required-check attempts | Q1 confidence |
| Validation finding/disposition contract | Q3 |
| APIView lifecycle events | S1/Q3 decomposition |
| Stable Package work item ID | Publication/version joins |
| Stable service identity and history | C3 and cohort continuity |
| Escaped issue tagging and review | Q4 |

### P3: contextual and business metrics

| Investment | Unlocks |
|---|---|
| Explicit execution surface and invocation type | Q5/C1 segmentation |
| Package download integrations | C4 |
| Comparable automation baselines/change annotations | B1 |
| Validated effort-per-intervention study | B2 |

## Recommended display scope

### Initial native candidates

| Metric | Level | Treatment |
|---|---|---|
| S1 | Plan, service, aggregate | Validated candidate |
| S4 | Plan, service, aggregate | Validated candidate |
| Q7 | Plan, service, aggregate | Validate classifier, then native |
| L1 full/post-spec | Plan/service | Show only for complete flows; aggregate after coverage gate |
| S2/S3 | Plan/service | Exact where covered; otherwise combined gap |
| S5 | Plan/service | Show only with authoritative publication event |
| Q8 | Plan/service | Provisional classifier |
| C2/C3 | Portfolio/future metrics | Context with dedupe/identity definitions |

### Defer until investment

- Active-cycle L1
- L2
- L3
- S6
- Q1 fleet rate
- Q2 canonical iteration count
- Q3/Q4
- Q6 fleet rate
- B1/B2

### Keep external initially

- Q5 Tool error rate
- C1 MEU and MEU/MAU
- C4 Package downloads

## Metric review checklist

Before a metric is displayed:

1. Is its definition versioned?
2. Are start/end events or numerator/denominator explicit?
3. Is its unit of analysis clear?
4. Are intended languages and exclusions known?
5. Are open flows censored rather than completed at `now`?
6. Are abandoned, duplicate, failed, and replaced flows treated explicitly?
7. Is the source authoritative, observed, or inferred?
8. Is source coverage above the approved threshold?
9. Is the population fleet-complete or a named fixed cohort?
10. Are p50/P90 used for durations?
11. Can the user open the contributing evidence?
12. Is unavailable data omitted rather than converted to zero?

## Final assessment

The proposed V2 collection can support the central lifecycle story, especially S1, S4, Q7, and coverage-limited L1/S2/S3/S5. It cannot by itself support the entire operational scorecard. The missing metrics are not all equivalent:

- Some need only deterministic derivation and UI work.
- Some need better pipeline/package correlation.
- Some need policy contracts.
- Some need entirely new instrumentation.
- Some belong in existing telemetry systems rather than this public static site.

The correct product strategy is to make a few lifecycle metrics exceptionally trustworthy, expose their evidence and coverage, and add a fleet scorecard only after fixed-cohort aggregation proves that the numbers remain defensible.
