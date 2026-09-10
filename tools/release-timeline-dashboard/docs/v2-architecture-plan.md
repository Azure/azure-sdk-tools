# V2 architecture and delivery plan

## Decision summary

Build a static, no-build SPA using pinned Alpine.js, ES modules, CSS custom properties, and versioned JSON. Generate the JSON in a scheduled authenticated pipeline whose canonical entity graph is rooted in Azure DevOps Release Plans and enriched only through exact linked GitHub PR and Azure Pipeline identifiers.

Do not put Azure DevOps or GitHub credentials in the browser. Do not make LLM classification part of the required pipeline. Do not implement V2 until the data contract and public-data policy are approved.

## Product goals

V2 should answer four levels of question:

1. **Portfolio:** Where are all active and completed release plans, which stages are slow, and who or what is currently blocking progress?
2. **Service:** How do release cycles for one service compare over time and by language?
3. **Release Plan:** What happened from plan creation through spec, generation, review, merge, and package publication?
4. **Artifact/event:** Which PR, pipeline run, review, comment, commit, failure, or release caused a delay?

The default view should communicate system behavior. User-level behavior should be a drill-down, not a leaderboard.

## Architecture principles

1. **Authoritative linkage before enrichment.** Never search all SDK repositories to discover a relationship that a Release Plan already records.
2. **Immutable facts plus derived intervals.** Preserve raw normalized events, then derive phases, waits, and metrics reproducibly.
3. **Many-to-many correlation.** A plan can have multiple PRs per language; a PR can satisfy multiple plans.
4. **Time-aware mutable fields.** A field replacement closes one link interval and opens another.
5. **Explicit uncertainty.** Unknown, missing, excluded, superseded, and inferred are distinct.
6. **Static delivery, authenticated generation.** Collection runs privately; published JSON is sanitized and cacheable.
7. **Incremental by default.** Closed entities become immutable cache entries; only changed work items and active PRs are refreshed.
8. **Precompute for the browser.** The SPA filters and renders; it does not reconstruct the data graph.

## System design

```text
Azure DevOps Release project
  Release Plan revisions
  API Spec child revisions
  exact generation/release pipeline URLs
            |
            v
  collector -> normalized source cache -> correlation graph
                                             |
GitHub exact linked PRs ----------------------+
Azure Pipeline runs --------------------------+
package feeds/release metadata ---------------+
tool telemetry (optional) --------------------+
                                             |
                                             v
                                  deterministic derivation
                                  validation + redaction
                                             |
                                             v
                           versioned static JSON publication
                                             |
                                             v
                              Alpine.js static SPA
```

### Collector runtime

Use Node.js built-ins plus `az` and `gh` CLIs initially, matching repository constraints. Prefer direct HTTPS with an Azure CLI token for Azure DevOps because it supports revisions and batch endpoints cleanly. Use `gh api` or a GitHub App token in the scheduled environment.

The collector is a build-time tool, not part of the website. Its stages should be separate commands so failures resume from cached source data:

```text
scripts/
  collect-release-plans.js
  collect-github-prs.js
  collect-pipeline-runs.js
  normalize-events.js
  build-view-data.js
  validate-data.js
  publish-data.js
```

The existing profiler should remain a diagnostic tool, not become the production collector.

## Collection plan

### 1. Release Plan inventory and revisions

Initial backfill:

1. Choose an explicit history start, then partition all creation dates from that point to the present to avoid the 1,000-item response ceiling.
2. Batch-fetch parents with relations.
3. Batch-fetch all hierarchy children and retain API Spec children.
4. Fetch all parent and API Spec child revisions.
5. Store raw responses by work item ID and revision.

Incremental update:

1. Query items changed after the last successful watermark with a small overlap.
2. Fetch only affected parents and children.
3. Fetch revisions after the last stored revision number.
4. Rebuild only affected plans, PR entities, service indexes, and aggregate shards.
5. Advance the watermark only after publication succeeds.

The first incremental query must overlap the backfill completion time and use `ChangedDate`, so an older plan modified during backfill is not missed. Use overlap plus idempotent event IDs to tolerate clock skew and interrupted runs.

### 2. Revision-to-event normalization

Diff each revision against its predecessor. Emit typed facts such as:

- `plan.created`, `plan.state_changed`
- `spec.approval_changed`
- `spec.pr_linked`, `spec.pr_replaced`
- `generation.queued`, `generation.started`, `generation.completed`, `generation.failed`
- `sdk.pr_linked`, `sdk.pr_replaced`, `sdk.pr_status_observed`
- `release.approval_pending`, `release.started`, `release.completed`, `release.failed`
- `package.version_observed`
- `language.excluded`, `language.reincluded`

Use a deterministic ID such as `ado:<workItemId>:<revision>:<field>:<newValueHash>`. Keep both old and new values for link/status changes in the private normalized cache; publish only allowlisted values.

### 3. GitHub enrichment

Build the PR set exclusively from:

- All values ever held by API Spec child PR fields
- PR URLs parsed from API Spec review history
- All values ever held by each language's SDK PR field

For each exact PR, collect:

- Repository, number, title, author, labels, draft state
- Created, ready-for-review, merged, closed, and updated timestamps
- Commits with author and timestamp
- Reviews and review states
- Issue comments and review comments
- Requested reviewers and relevant timeline events
- Check-run conclusions for active PRs

Efficiency rules:

- Deduplicate by repository and number before any call.
- Cache raw responses and ETags.
- Treat merged/closed PR metadata, reviews, and commits as immutable after a short settling period.
- Refresh active PRs frequently and terminal PRs rarely.
- Use GraphQL batching for core metadata where it demonstrably reduces cost; keep paginated REST endpoints for event collections with better semantics.
- Store API rate-limit and completeness metadata in each run manifest.

GitHub is authoritative for PR state. ADO PR status is an observation event and quality signal, not the final state.

### 4. Pipeline and release enrichment

Parse exact build IDs from the Release Plan generation and release pipeline URLs. Never infer a pipeline by package/service naming when an exact run is present.

For each exact build:

- Fetch build metadata and timeline records.
- Identify queue, start, finish, result, stage, approval, and retry timestamps.
- Associate the run with plan, language, package, and PR.

Fallback order when exact links are missing:

1. Structured run ID embedded in linked PR metadata or bot comment
2. Release Plan revision timestamp and generation/release status
3. No event; mark the interval unknown

Package publication should use package registry/release metadata for exact time and version where practical. A Release Plan status revision is only an observed completion time.

### 5. Optional telemetry

Tool-call telemetry must carry Release Plan ID, work item ID, PR URL, package, language, invocation ID, start/end time, result, and client kind. Without one of the correlation keys, retain it only in aggregate telemetry and do not guess a plan.

LLM analysis can generate optional narrative summaries after deterministic metrics exist. It must not decide correlation, durations, actor roles, or stage completion.

## Canonical data model

The private build graph should normalize entities:

```text
ReleasePlan
  -> PlanLanguageIntent[]
  -> ApiSpecWorkItem[]
  -> PullRequest[] through time-bounded edges
  -> PipelineRun[]
  -> PackageRelease[]
  -> TimelineEvent[]
  -> DerivedInterval[]
  -> MetricResult[]
```

`MetricDefinition[]` is a versioned registry shared by all builds. `PlanLanguageIntent` is also versioned so metric denominators use the intended package/language set that applied to the flow, not only the latest Release Plan fields.

### Timeline event contract

```json
{
  "id": "github:Azure/azure-sdk-for-net:61030:merged",
  "type": "pr.merged",
  "phase": "sdk-review",
  "occurredAt": "2026-07-22T16:58:35Z",
  "observedAt": "2026-07-22T16:58:35Z",
  "trackId": "sdk:.NET:Azure.ResourceManager.NetApp",
  "actor": {
    "kind": "human",
    "publicId": "github-login"
  },
  "source": {
    "system": "github",
    "entity": "Azure/azure-sdk-for-net#61030",
    "url": "https://github.com/Azure/azure-sdk-for-net/pull/61030"
  },
  "confidence": "authoritative"
}
```

Required semantics:

- `occurredAt`: best external event time
- `observedAt`: collection or work-item update time
- `confidence`: `authoritative`, `observed`, or `inferred`
- `trackId`: stable lane assignment
- `phase`: stable metric bucket
- `actor.kind`: `human`, `bot`, `service`, `unknown`; internal identities are not published

### Link history

Represent correlation as edges, not scalar fields:

```json
{
  "planId": 35326,
  "artifactId": "github:Azure/azure-rest-api-specs#44847",
  "role": "spec-pr",
  "validFrom": "2026-07-21T14:43:39Z",
  "validTo": null,
  "sourceRevision": 3
}
```

A replaced PR remains in history with a closed validity interval. A shared PR has multiple edges.

### Derived intervals

Compute phases from ordered authoritative events:

- Plan intake: plan created -> first spec PR linked
- Spec authoring: spec PR created -> ready for review
- Spec review: ready for review -> merged/closed
- Generation queue: spec merged/approved -> generation started
- Generation execution: generation started -> PR linked or failure
- SDK authoring: PR created -> ready for review
- SDK review: ready for review -> merged/closed
- Release queue: PR merged -> release started/approval pending
- Release execution: release started -> package published

An interval records start/end event IDs, duration, status, waiting-on classification, and confidence. Never fill a missing boundary with `now` for terminal metrics; open intervals are explicitly censored.

### Metric definitions and results

The metric feasibility and investment decisions remain canonical in [`e2e-metrics-coverage.md`](e2e-metrics-coverage.md). The build contract implements those decisions through a versioned registry rather than hard-coded UI calculations:

```json
{
  "id": "S4",
  "version": 1,
  "name": "SDK PR cycle time",
  "unit": "hours",
  "scope": "plan-language-pr",
  "startEvent": {
    "type": "pr.created",
    "role": "sdk-pr"
  },
  "endEvent": {
    "type": "pr.merged",
    "role": "sdk-pr"
  },
  "aggregations": ["p50", "p90"],
  "readiness": "validated"
}
```

Registry requirements:

- A stable metric ID and immutable definition version
- Unit and supported scopes
- Boundary selectors or numerator/denominator rules
- Eligibility, terminal-outcome, and exclusion policy references
- Supported aggregations and minimum sample rule
- Readiness: `validated`, `provisional`, `unavailable`, `external`, or `deferred`

Every calculation emits a typed result, including calculations that cannot produce a value:

```json
{
  "metricId": "S4",
  "definitionVersion": 1,
  "scope": {
    "planId": "35326",
    "language": ".NET",
    "artifactId": "Azure.ResourceManager.NetApp",
    "prId": "github:Azure/azure-sdk-for-net#61030"
  },
  "outcome": "complete",
  "value": 167.6,
  "unit": "hours",
  "evidence": {
    "startEventId": "github:Azure/azure-sdk-for-net:61030:created",
    "endEventId": "github:Azure/azure-sdk-for-net:61030:merged",
    "contributorEventIds": []
  },
  "confidence": "authoritative",
  "sourceCoverage": {
    "required": 2,
    "authoritative": 2,
    "observed": 0,
    "inferred": 0,
    "missing": 0
  },
  "missingBoundaryReason": null
}
```

Result requirements:

- `outcome`: `complete`, `censored`, `excluded`, `incomplete`, or `ineligible`
- `value` and `unit` only for an eligible result with sufficient boundaries
- `numerator` and `denominator` instead of `value` for a rate where appropriate
- Evidence IDs for boundaries or contributing facts
- Source confidence and coverage independent from metric readiness
- A typed missing-boundary reason for every absent value
- The applicable intended-language/package snapshot and approved exclusions for completion metrics

Missing, censored, excluded, incomplete, and ineligible are distinct outcomes. They must never be collapsed to zero. Abandoned, duplicate, failed, and replaced flows remain explicit source outcomes; each metric definition decides whether they are eligible rather than allowing collectors or UI code to drop them implicitly.

### Aggregate metric contract

Aggregates are derived only from versioned `MetricResult` records and retain enough population accounting to reproduce the visible number:

```json
{
  "metricId": "S4",
  "definitionVersion": 1,
  "scope": {
    "kind": "fleet",
    "period": "2026-07"
  },
  "statistics": {
    "p50": 72.4,
    "p90": 211.8
  },
  "population": {
    "eligible": 412,
    "included": 386,
    "censored": 11,
    "incomplete": 9,
    "excluded": 6,
    "ineligible": 38
  },
  "confidenceCounts": {
    "authoritative": 386,
    "observed": 0,
    "inferred": 0
  },
  "cohort": {
    "kind": "fleet-complete",
    "filters": {}
  },
  "contributorRef": "indexes/metric-contributors/S4/2026-07.json"
}
```

Aggregate requirements:

- Definition version, reporting period, cohort kind, and deterministic filters
- Eligible and included counts plus every non-included outcome count
- Confidence/source-coverage distribution
- Minimum-sample suppression where required
- Contributor IDs or a deterministic filter descriptor for evidence drill-down
- No browser-side recomputation across plan files

## Published static JSON

Publish immutable, versioned data under a build ID and update one small pointer last:

```text
data/
  manifest.json
  builds/<build-id>/
    portfolio.json
    aggregates/metric-definitions.json
    aggregates/scorecard.json
    aggregates/lifecycle-monthly.json
    aggregates/quality-monthly.json
    aggregates/cohorts.json
    aggregates/coverage.json
    aggregates/monthly.json
    aggregates/languages.json
    indexes/services.json
    indexes/plans-2026-08.json
    indexes/metric-contributors/...
    services/<service-slug>.json
    plans/<release-plan-id>.json
```

`manifest.json` contains:

- Schema and build version
- Generated time and source watermarks
- Counts, shard list, and hashes
- Completeness/rate-limit warnings
- Minimum compatible UI version

### Payload strategy

- `portfolio.json`: small current cards and global facets.
- Monthly plan index shards: compact rows for search and historical browsing.
- Service files: release-plan summaries, cross-cycle metric distributions/trends, metric coverage, and plan IDs.
- Plan files: complete event tracks, intervals, links, typed metric results/evidence, intended-artifact snapshots, and detail summaries.
- Aggregates: versioned metric definitions, precomputed statistics, population accounting, cohorts, coverage, and contributor references.

The build graph remains normalized, but published plan/service payloads are deliberately denormalized so the browser performs no cross-file join for a selected view.

Future blob hosting only changes the manifest base URL. Versioned prefixes, CORS, immutable cache headers, and atomic pointer updates prevent mixed builds.

## SPA technology

### Runtime

- Pinned Alpine.js 3.x, vendored locally
- Native ES modules
- HTML and CSS without compilation
- Fetch API, URLSearchParams, Intl, and structuredClone
- No router, charting, date, state, or CSS framework

Use Alpine for state and disclosure, not for complex geometry calculations. Put pure timeline scale, filtering, and formatting helpers in ES modules and expose small Alpine stores/components:

```text
js/
  app.js
  data-store.js
  router.js
  metrics.js
  timeline-scale.js
  components/
    portfolio.js
    service-view.js
    plan-view.js
    timeline.js
    detail-drawer.js
```

### URL model

Use history-based query parameters compatible with static hosting:

```text
?view=portfolio
?view=service&service=containerregistry
?view=plan&plan=35326
?view=plan&plan=35326&language=.NET&event=<event-id>
```

Persist filters, selected release, time range, and expanded tracks in the URL where practical. Browser back/forward must restore the view.

## View plan

### 1. Portfolio

- KPI strip: active, finished, abandoned, p50/p90 cycle, current blocked, data quality.
- Search by service, product, package, PR, path, and plan ID.
- Facets: stage, state, action owner, plane, release type, language, creation source, intended month, age, confidence.
- Status columns inspired by the release-plan dashboard: in progress, partially released, not started, recently finished, abandoned.
- Compact per-plan progress bar with aging and action owner.
- Fleet trends and bottleneck distribution below the operational list.

### 2. Service history

Retain V1's strongest full-service behavior:

- Service metadata and freshness
- Release Plan/release-cycle selector, newest first
- All-history and focused-window modes
- Aggregate KPI cards that switch context
- Spec, generation, language PR, and release tracks
- Expandable per-plan/per-PR lanes
- Actor and event filters
- Compressed idle spans with an honest discontinuous axis
- Zoom and horizontal scrolling
- Data-quality warning when a window is incomplete

The Release Plan replaces V1's inferred release window. API version becomes a label, not the grouping key.

### 3. Release Plan detail

- Five-stage workflow header from the existing dashboard vocabulary
- Current action owner and age
- Gantt tracks for plan/spec, each intended language, generation runs, PRs, and releases
- Intended vs actual language/package matrix
- Contextual metrics and percentiles
- Link history showing superseded/shared PRs
- Evidence/confidence badges for approximate timestamps
- Related plans sharing the same PR

### 4. Event and PR detail

A side drawer should show:

- Exact timestamp and duration
- Actor kind and public GitHub identity where applicable
- Source and confidence
- Comment/review excerpt only when public and necessary
- PR labels, commits, approvals, checks, APIView, and links
- Which interval/metric the event starts or ends

## Rendering strategy

Use a hybrid semantic DOM timeline:

- HTML lane labels and controls for accessibility
- Absolutely positioned CSS bars/markers inside each track
- A pure piecewise scale for normal, zoomed, and gap-compacted time
- CSS/SVG axis ticks; no canvas
- Render only selected service/plan details, not the whole portfolio history
- Collapse aggregate tracks by default and cap event marker density until expanded

Precompute event stacks and summary marks to avoid collision work in the browser. If a service still has hundreds of lanes, implement simple range virtualization before adding a dependency.

## Metrics

The canonical metric-by-metric display feasibility, source coverage, required changes, and investment assessment is in [`e2e-metrics-coverage.md`](e2e-metrics-coverage.md). The lists below describe the architectural metric families; they do not imply that every metric is currently measurable or approved for fleet aggregation.

### Core duration metrics

- Plan-created to first spec PR
- Spec PR authoring and review
- Spec merge/approval to first generation start
- Generation queue and execution by language
- SDK PR authoring, review wait, review rounds, and merge
- Merge to release start, approval, and package publication
- End-to-end p50, p75, p90 and censored active age

### Human/automation behavior

- Human comments and reviews by phase
- Time to first human review
- Changes-requested cycles
- Commits after ready-for-review
- Handoffs and unique reviewers
- Generation attempts, retries, and failure rate
- PR replacements and regeneration
- Automation-only versus human-touched cycles
- Manual intervention events only when explicitly evidenced

### Operational metrics

- Current stage aging and action owner
- Abandonment and duplicate rates
- Intended-language completion
- Partial release duration
- Release approval waiting time
- First preview/GA cohorts
- Management versus data plane
- Service, language, creation source, and monthly cohorts

### Data quality metrics

Show completeness separately:

- Missing path, PR, run, release, or version
- Stale ADO versus GitHub status
- Shared or replaced link
- Approximate timestamp count
- Collector freshness and source failure

Do not compare teams or users without minimum sample sizes and clear cohort definitions.

## Privacy and publication

Before publishing any generated JSON:

1. Define an allowlist of Release Plan fields.
2. Remove ADO identity objects, emails, HTML descriptions, internal comments, and raw revision payloads.
3. Publish public GitHub logins only for public PR activity.
4. Classify internal pipeline URLs and product metadata for public suitability.
5. Sanitize all HTML to plain text at collection time.
6. Record a redaction-policy version in the manifest.
7. Validate that no token, email, or unexpected field appears in output.

The browser must never receive the private normalized cache.

## Validation and observability

Each build should fail closed on:

- Invalid schema or timestamp
- Unknown language/status enum
- Unknown metric ID or definition version
- Duplicate event ID with different content
- Dangling plan/artifact edge
- Metric evidence that references a missing event
- Negative interval
- Metric value emitted for a censored, excluded, incomplete, or ineligible result
- Aggregate population counts that do not reconcile with contributor results
- Aggregate result below its minimum sample rule without suppression
- Release preceding PR merge without an explicit override
- Accidental identity/email/secret publication
- Material count drop beyond a configured threshold

Emit a private run report with source counts, API calls, cache hits, changed entities, quality deltas, warnings, and publication hashes. Publish only a sanitized completeness summary.

Golden fixtures should cover:

- Complete five-language release
- In-flight release
- Abandoned and duplicate plan
- Private preview without SDKs
- Excluded/missing-emitter language
- Replaced spec and SDK PR
- Shared PR across plans
- Multiple PRs/packages in one language
- Failed/retried generation and release
- Missing exact release timestamp
- Complete, censored, excluded, incomplete, and ineligible metric results
- Abandoned flow included and excluded under different versioned metric policies
- Aggregate population accounting and minimum-sample suppression

Browser verification should use the repository's Playwright CLI skill across these fixtures, desktop widths, dark/light themes, keyboard navigation, URL restoration, and malformed/missing shard handling.

## Delivery phases and approval gates

### Phase 0: Decisions

Approve:

- Public-data allowlist
- Event and interval vocabulary
- Many-to-many correlation model
- Source precedence and confidence semantics
- Initial metric definitions and readiness
- Metric eligibility, terminal-outcome, abandonment, and exclusion policies
- Fleet-complete versus fixed-cohort semantics and minimum sample rules
- Initial deployment target and update cadence

### Phase 1: Data proof

- Implement backfill/incremental Release Plan collector.
- Normalize parent and child revision events.
- Enrich 20 representative plans from GitHub and exact pipeline URLs.
- Produce event, interval, metric-result, and intended-artifact schemas plus fixtures, quality report, source-coverage measurements, and call-cost measurements.

**Gate:** At least 95% of selected plans have an explainable correlation graph; all unknowns are typed rather than guessed. Candidate metrics report authoritative/observed/inferred/missing boundary coverage before any aggregate is approved.

### Phase 2: Static contract

- Implement the versioned metric registry, deterministic result/evidence calculations, and validation.
- Produce versioned portfolio, service, plan, metric-definition, contributor-index, and aggregate JSON.
- Measure payload sizes and shard strategy on the full one-year corpus.

**Gate:** A static consumer can render all required views without credentials or runtime joins. Every displayed metric resolves to one definition version and evidence, absent values retain typed outcomes, and every aggregate reconciles its eligible/included/excluded populations.

### Phase 3: UI skeleton

- Add vendored Alpine and component stores.
- Build portfolio, service, and plan navigation with fixtures.
- Establish visual tokens and the desktop dashboard layout.

**Gate:** URL-driven navigation, loading/error states, and accessibility work before full timeline rendering.

### Phase 4: Timeline parity

- Implement focused/all-history timelines, lanes, gap compaction, zoom, filters, details, and KPI cards.
- Validate complete/in-flight/missing/shared/replaced cases.

**Gate:** Rough parity with V1 full-service interactions, using Release Plans as windows.

### Phase 5: Fleet operations

- Schedule incremental collection.
- Publish atomically.
- Add freshness and completeness monitoring.
- Tune active/terminal refresh cadence and API budget.

**Gate:** Repeated runs are idempotent, cheap, and recoverable.

## Decisions requested before implementation

1. Is the public site allowed to expose sanitized Release Plan identifiers, product metadata, and internal pipeline links?
2. Should V2 initially cover one year, all Release Plan history, or a staged backfill?
3. Is daily refresh sufficient, with more frequent updates only for active plans?
4. Should user-level behavior be visible by default, or only in plan/PR drill-down?
5. Can the Release Plan tooling team accept the P0 schema/telemetry investments in the accompanying backlog?
6. Which metric definitions, denominator policies, coverage thresholds, and minimum sample rules are approved for the initial scorecard?
