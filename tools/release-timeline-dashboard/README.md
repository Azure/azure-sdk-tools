# Azure SDK Generation Timeline v2

This repository contains a static, Alpine.js-based dashboard that reconstructs Azure SDK generation and release timelines from Azure DevOps Release Plan work items, exact linked GitHub pull requests, and exact linked Azure Pipeline runs.

The public data boundary is `data/snapshot.json`. This single revalidated
bootstrap contains snapshot/schema metadata, selection and coverage counts, the
portfolio index, and normalized correlated boundary facts. Immutable detailed
plan documents remain lazy-loaded from the snapshot's path template. The
browser and Node validator share `js/calculation-engine.mjs`, which
deterministically derives metric outcomes, intervals, scorecards, percentiles,
periods, trends, and cohort membership using `snapshot.generatedAt` as the
clock. Calculation-only changes therefore do not require recollection.

The current V2 dataset inventories management-plane Release Plans changed since March 1, 2026, the earliest scorecard completion period. Flow starts may predate that reporting boundary, so March completions retain their full history without making inventory discovery unbounded. An explicitly populated Release Plan ID and exact spec PR are required before enrichment. Metric observations completed before March are excluded from scorecards, while missing downstream evidence remains visible instead of deleting an otherwise eligible plan. This is a tracked cohort, not a fleet-complete population.

L1 headlines show previous-full-month and rolling-one-month P50 comparisons against the pooled preceding three months, plus a rolling 90-day P90 without a diff. Other stage metrics retain weekly and monthly summaries. Weekly trends retain 13 weeks, while monthly trends grow with available tracked history up to 12 months. S4 measures earliest attributed SDK PR creation through final successful merge per language; individual replacement attempts remain timeline evidence. Release boundaries use a recorded version plus Release Plan Released transition, with a conservative GitHub Release `publishedAt` fallback when historical version evidence is missing. When tag identity is incomplete, monthly metadata from `Azure/azure-sdk/_data/releases` supplies package/version/tag candidates and linked SDK PR evidence disambiguates versions before the GitHub Release timestamp is accepted. The cohort coverage drawer exposes incomplete and excluded populations with source links.

- [Research findings](docs/research-findings.md)
- [V2 architecture plan](docs/v2-architecture-plan.md)
- [Data gap investment backlog](docs/data-gap-backlog.md)
- [E2E metric display and investment assessment](docs/e2e-metrics-coverage.md)

## Run the site

Serve the repository over HTTP so the browser can load the versioned JSON:

```bash
python3 -m http.server 4173
```

Then open <http://localhost:4173/>.

Opening `index.html` directly with a `file://` URL is not supported because
the browser blocks ES module imports and JSON requests from local files.

The dashboard intentionally targets desktop browsers and uses a fixed-width
desktop canvas rather than mobile-specific layouts.

## Refresh the current cohort

The zero-dependency collection pipeline requires authenticated `az` and `gh` CLIs. It selects management-plane plans, preserves mutable PR link history, enriches exact public PRs and exact pipeline runs, builds static view data, and validates redaction and data integrity. Terminal PRs and pipeline runs are reused from the private cache. Individual inaccessible or unusually large artifacts are marked as skipped instead of blocking the cohort.

```bash
node scripts/refresh-v2-data.js \
  --start-at 2026-03-01T00:00:00.000Z \
  --days 180 \
  --limit 0 \
  --mode all-management \
  --build-id "$(date -u +%Y%m%dT%H)"
```

Private normalized inputs are written under ignored `cache/`. Immutable public
details are written under `data/builds/<snapshot-id>/` first, and
`data/snapshot.json` is updated last for atomic publication. The snapshot does
not publish a precomputed scorecard or metric-result aggregate.

The preflight requires only an explicitly populated Release Plan ID and exact spec PR. Plans that fail either root check are not sent to the expensive revision, GitHub, or pipeline collectors. Exact downstream links are enriched when present, but missing evidence produces incomplete metrics and quality warnings. Pipeline names and timestamps are never used to guess missing relationships.

Release Plan creation or change controls the bounded initial inventory query. Plans created before the fixed start are retained as overflow accounting but are not enriched. Publication uses the stricter flow window recorded as `selection.startAt` and `selection.endAt`: the earliest retained event must be on or after the start, and the entire flow must remain inside the window.

SDK PR bodies that explicitly identify a different Release Plan are not attributed as historical attempts. An identity-less historical PR is also excluded when it was already merged before the plan existed and its field was later replaced by a PR created after plan creation. These rules prevent copied bootstrap fields from pulling an earlier release's activity into a later plan while retaining links that lack contradictory identity or lifecycle evidence.

The intended initial production cadence is daily. Scheduling is intentionally left out of this proof until the deployment environment has an Azure identity with Release project access.

## Release Plan profiler

The zero-dependency profiler used for the initial Azure DevOps analysis can be rerun with an authenticated Azure CLI session:

```bash
node scripts/profile-release-plans.js \
  --days 365 \
  --revision-sample 30 \
  > /tmp/release-plan-profile.json
```

It emits aggregate coverage, correlation, quality, and revision-history statistics. It does not emit raw work item field values, identities, comments, or URLs.
