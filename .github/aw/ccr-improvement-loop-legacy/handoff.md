# CCR Improvement Loop — Session Handoff

_Last updated: 2026-07-22. Branch: `experiments/harvest-pull-request-comments`.
Newest session first; the 2026-07-13 dashboard/metrics session follows below._

This document summarizes the work done in this working session on the
`ccr-improvement-loop` agentic workflow and its dashboard. It is a handoff for
whoever (human or agent) picks this up next.

## Context

`.github/aw/ccr-improvement-loop/` is a weekly agentic workflow that measures
whether Copilot Code Review (CCR) helps a target repo. Each run mines a settled
time-window of merged PRs, judges human/CCR review comments, and emits a
schema-1.0 `run-*.json`. A static, zero-backend dashboard (`dashboard/`) aggregates
many such JSONs into metrics + historical trend charts, meant to be shared with the
team via GitHub Pages and kept low-maintenance.

This session had three parts: **(A)** backfilling historical data, **(B)**
redesigning the degenerate `missRate` metric, and **(C)** adding per-slice
breakdown charts. All work is committed (see [Git state](#git-state)).

---

## Session 2026-07-22 — GitHub API rate-limit optimization

**Goal.** Reduce GitHub API pressure in the PR-fetch path so backfills stop
hitting the rate limit, and map out how to scale backfill across many repos.

### Code shipped (`scripts/`, all tests green)
- **#1 — Deleted the unused `commits/{sha}/pulls` call + `commitPrs` field.**
  Verified written-but-never-read (only consumers were test fixtures). Per-PR
  cost `6 + 2N` → `6 + N` (N = commits). `fetch-prs.ts`, `types.ts`, fixtures.
- **#5 — Process-wide request limiter + header-aware backoff** (`utils.ts`):
  new `Semaphore` class + shared `ghRequestLimiter` (default **8**, override via
  `CCR_GH_MAX_CONCURRENCY`) bounds concurrent REST+GraphQL requests;
  `parseRetryAfterMs` makes retries honor `Retry-After`; new async
  `ghApiGraphqlAsync`. Targets **secondary** rate limits (burst/concurrency),
  which enterprise tier never relaxes.
- **#2 — Bounded parallel fan-out** (`fetch-prs.ts`): per-PR reads +
  `fetchThreadResolution` run under one `Promise.all`; per-commit detail under a
  second (order-preserving). All admitted through the shared limiter, so
  `workers × fan-out` can't exceed the ceiling.
- **Deferred:** #3 (selective commit-detail — blocked by `distinctFiles`
  coupling in `prep-summary.ts`) and #4 (GraphQL migration — partial win,
  connections still paginate). Rationale in `optimize-api-call-plan.md`.

### New docs
- **`optimize-api-call-plan.md`** — the #1–#5 items, status, and test plan.
- **`api-rate-limit.md`** — ground-up reference: cost model, primary vs secondary
  limits, the token finding, scaling levers, enterprise (GHEC/GHES) implications.

### Key findings
- **Auth is the real ceiling.** Prep steps run with
  `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}` (workflow `.md`). On the personal
  `gh-aw-trial` repo that's **~1,000 req/hr** — consistent with runs dying at
  ~70 PRs. The workflow comment says "App installation rate limit" but the code
  uses the default token (a gap).
- **Enterprise (GHEC) uplift:** `GITHUB_TOKEN` → **15,000 req/hr/repo** (15×,
  no code change), likely removing the need for a custom App. GHES admins can
  raise/disable limits. Secondary limits are **unchanged** by tier, so the
  limiter stays essential. ([REST rate-limit docs](https://docs.github.com/enterprise-cloud@latest/rest/overview/rate-limits-for-the-rest-api))
- **Python volume (2026 merged PRs/mo):** Jan 178, Feb 267, Mar 296, Apr 282,
  May 326, Jun 288, Jul 230 (MTD). A full month ≈ **~4,000+** endpoint
  invocations — over a 1,000/hr budget, at/near a 5,000/hr one.

### Dispatch experiments (`JennyPng/gh-aw-trial`)
- Pushed the code changes to `trial/main` (commit `f01e85f21`, fast-forward).
- Dispatched a **capped** June 2026 run (`max_prs=75`) → cancelled at user
  request.
- Dispatched an **uncapped** June 2026 run (`max_prs=0`) → **hit the rate
  limit** — confirming the ~1,000/hr `GITHUB_TOKEN` ceiling is the binding
  constraint for full-cohort months on this (personal) repo.

### Recommended next steps
1. **Upgrade auth** — App installation token, or host the workflow on a
   GHEC-owned repo (→ 15,000/hr). Highest-leverage change for scale.
2. **Keep the limiter** (#5) regardless of tier — the only guard vs secondary
   limits.
3. **Multi-repo / deep backfill:** switch to **weekly windows** (~70 PRs ≈
   ~1,000 calls, clears the ≥50-PR artifact threshold) + a **`/rate_limit`-aware
   resumable loop** riding the existing immutable `pr-cache` (idempotent
   re-dispatch, zero rework). Scales to arbitrary repo counts by self-throttling.
4. **More REST headroom:** land **#4 (GraphQL)** to tap the separate GraphQL
   point budget, and **#3** to shrink the `N` term.

### Validation (from `.github/aw/ccr-improvement-loop`)
| Gate | Command | Result |
|------|---------|--------|
| Typecheck | `npm run typecheck` | pass |
| Full suite | `npm test` (vitest) | 97/97 (added `tests/utils.test.ts`) |
| Lint | `npm run lint` | clean |

---

## A. Backfill — 6 monthly historical runs (Python, 2026)

**Goal.** Populate the dashboard with monthly Python `run-*.json`s for 2026 so
trends have real history.

**What was done.**
- Added dispatch inputs `window_start`, `window_end`, and `max_prs` to the
  workflow (`.github/workflows/ccr-improvement-loop.md`), recompiled the lock
  (`gh aw compile ccr-improvement-loop`, v0.80.9).
- Dispatched Jan–Jun 2026 windows for `Azure/azure-sdk-for-python`, **capped at 75
  PRs/window** to stay under the shared GitHub App installation rate limit.
- Downloaded the 6 artifacts, staged them into `dashboard/data/`, regenerated
  `dashboard/data/manifest.json` (now **8 runs**: Jan–Jun Python capped + the two
  pre-existing 2026-07-09 Python & Tools runs).

**Reproducible dispatch playbook (for future backfills).**
- Workflow `concurrency: cancel-in-progress: false` allows only **1 running + 1
  pending per repo** → dispatch **sequentially**, one to completion before the
  next.
- **Cap the window** (`-f max_prs=75` ≈ ~900 API calls, ~15 min). An **uncapped**
  month ≈ 2,400+ calls → exceeds the ~5,000/hr shared budget (failure is
  unobservable externally until the run errors).
- Capped runs survive laptop suspend; long unattended spacing loops do not.
- Run JSON is in the `safe-outputs-upload-artifacts` artifact, **not** `.ccr-runs/`.
- **No-op runs** (success but < 50 settled PRs) produce **no artifact**.
- Find the new run id by diffing
  `gh run list ... | max_by(.createdAt).databaseId` before/after dispatch.
- Dispatch command:
  ```
  gh workflow run ccr-improvement-loop.lock.yml --repo JennyPng/gh-aw-trial \
    -f repo=Azure/azure-sdk-for-python -f window_start=YYYY-MM-01 \
    -f window_end=YYYY-MM-DD -f max_prs=75
  ```

---

## B. Metric redesign — retire `missRate`, add `ccrRecallRate`

**Problem found.** `missRate` was **exactly 1.0 in every non-`n/a` run** — useless.
Its denominator was gated by the per-comment `ccrSawCode` commit-timing sandwich
(`attribute-comments.ts:computeCcrSawCode`): an ask counts only if a CCR review
event falls between the latest commit touching the ask's file and the human
comment. That window **closes precisely when a fix commit lands after CCR's review
— i.e. exactly when CCR succeeded** — so every CCR success was ejected from the
denominator, leaving only misses. It was 100% by construction. The name also read
backwards ("miss rate").

**Fix (`compute-metrics.ts`).** Introduced **`ccrRecallRate`** (direction `up`,
higher = better): _of the substantive, diff-detectable issues human reviewers
raised on **CCR-reviewed** PRs (and that were judged), the share CCR independently
raised the same concern (`ccrAddressedConcern === true`)._ Eligibility is gated at
the **PR level** (`ccrReviewed`), not the fragile per-comment timing gate. Unjudged
asks (`ccrAddressedConcern == null`) abstain from the denominator.
- Named `ccrRecallRate` (not `ccrCatchRate`) to avoid confusion with the
  pre-existing, **unrelated** `criticalCatchRate` metric.
- Exported a pure helper `computeCcrRecallRate(prs, comments, warnings?)` reused
  verbatim by the data migration.
- Added transparency counts `ccrRecallEligible` / `ccrRecallCaught`.

**What was kept.** The agent-derived `isGap` flag (workflow `.md` line 162:
`ask ∧ isSubstantive ∧ diffDetectable ∧ ccrSawCode ∧ !ccrAddressedConcern`) is
**unchanged** and still emitted per comment. It drives `counts.gaps`, theme
clustering, and rule proposals. It is strict-by-design and stays that way; it is
just no longer surfaced as a headline rate. Changing it would require re-running
the agent over history.

**Not touched.** `experiment.missRateBefore` / `missRateAfter` in the run schema
are a **separate, currently-inactive** A/B replay-benchmark feature — left as-is.

**Data migration (no agent re-run).** Existing `run-*.json` carry every raw field
needed (`prs[].ccrReviewed`, `comments[].{isSubstantive, diffDetectable,
pathExcluded, ccrAddressedConcern, pr}`). A throwaway script recomputed
`ccrRecallRate` from those fields, spliced it into `metrics.rates`, deleted
`metrics.rates.missRate`, and re-validated each file with `parseRun`. All 8 staged
runs + the 6 synthetic dashboard fixtures were migrated. Resulting values are
non-degenerate: **Python Jan 0.20, May 0.08, others 0/n** (denominators 1–28).

**Docs.** README Q3 section rewritten; `decisions.md` **D14** added documenting the
self-exclusion bug and the fix (with a supersession note on D4); dashboard README
and workflow `.md` narrative wording updated; lock recompiled.

---

## C. Per-slice breakdown charts (dashboard)

**Goal.** Surface the slice data every metric already carries, "wherever bucketing
makes sense." Four charts were added; **metrics were left as-is** (user chose to
keep `ccrRecallRate` as a headline, not to reframe).

**Charts added** (all in `dashboard/index.html` + rendered by
`app.mjs:renderSliceCharts`):
1. **CCR comment outcomes by severity** — addressed / rejected / ignored, grouped
   by critical / substantive / nit.
2. **Addressed vs ignored by PR type** — where CCR's comments land vs go
   unaddressed, per PR type.
3. **Human asks per PR by PR type** — average count (not a ratio), per PR type.
4. **CCR catch rate by PR type** — `ccrRecallRate` split by PR type.

**Key design choice — pooling.** Each chart **pools per-slice
numerators/denominators across the shown runs** (denominator-weighted), rather than
averaging per-run rates. This fixes the small-sample problem: per-run cells were
1–3 items, but pooled they become robust (e.g. **334** substantive CCR comments,
**250** chore comments). Real signal surfaces — chore comments addressed only
**38%** vs feature **70%**; docs draw the most human asks (0.58/PR) vs chore
(0.09/PR).

**Implementation.**
- `charts.mjs` — new `groupedBar()` helper + `OUTCOME_COLORS`.
- `aggregate.mjs` — new `poolSlices(runs, rateKey, dim, order)` helper +
  `PR_TYPE_ORDER` / `SEVERITY_ORDER` constants. This is **presentation-only** (used
  only by the dashboard); it is intentionally **not** mirrored into the TS
  `aggregate-runs.ts` core.
- `app.mjs` — `renderSliceCharts(runs)` wiring the 4 charts; imports updated.
- `dashboard/js/aggregate.test.mjs` — 2 new `poolSlices` unit tests.

**Note on the "percentage" confusion.** `humanCommentsPerPr` is an **average count
per PR** (`distinct substantive human asks ÷ PRs`), not a percentage. The dashboard
already renders it as a raw number; it just looks fraction-like because most PRs
get zero substantive human asks (values 0.06–0.44). Chart #3 now shows it broken
down by PR type. **PR-size buckets (S/M/L/XL) were considered but NOT added** —
`additions`/`deletions` are captured per PR, but size is not yet a slice dimension
(would need schema + compute + UI changes). This is a candidate for future work.

---

## Verification / gates

All green after every change (run from the tool dir
`.github/aw/ccr-improvement-loop`):

| Gate | Command | Result |
|------|---------|--------|
| Typecheck | `npx tsc --noEmit` | pass |
| Dashboard unit tests | `node --test 'dashboard/js/**/*.test.mjs'` | 11/11 |
| Full suite | `npm test` (vitest) | 88/88 |
| Lint | `npx eslint .` | clean |
| Format | `npm run format:check` (`--write` to fix) | clean |
| Render check | `node scripts/aggregate-runs.ts dashboard/data/run-*.json` | renders `ccrRecallRateOverTime` |

Notes: run scripts via `node scripts/foo.ts` (Node v25, TS via native strip). The
shell wrapper blocks `kill $VAR` (use literal PIDs). `gh aw compile` = v0.80.9.

---

## Git state

- **Branch:** `experiments/harvest-pull-request-comments`.
- **Session commits:**
  - `f01e85f21` — `ccr-improvement-loop: cut GitHub API pressure in fetch-prs`
    (2026-07-22: #1 deletion, #5 limiter, #2 parallel fan-out, `optimize-api-call-plan.md`)
  - `47cfe3042` — `add max_prs backfill input for rate-limit-safe capped runs`
  - `a76425279` — `many new charts, alter missrate to catch rate` (the metric
    redesign, data migration, and slice charts — 31 files)
- **Remotes:** `origin` = JennyPng/azure-sdk-tools, `trial` = JennyPng/gh-aw-trial,
  `upstream` = Azure/azure-sdk-tools. `a76425279` is on
  `upstream/experiments/harvest-pull-request-comments`; `f01e85f21` was pushed to
  `trial/main` (where the workflow runs) as a fast-forward.
- **Uncommitted (2026-07-22):** `api-rate-limit.md` and this `handoff.md` update
  are staged/working-tree only — commit + push to `trial/main` if you want the
  workflow repo to carry them.

---

## Candidate next steps (not started)

- **PR-size bucket dimension** (S/M/L/XL by lines changed) as a new slice — the
  one bucketing the user asked about that isn't yet possible; needs schema +
  compute + UI + history re-migration.
- **Reframe the headline** around CCR comment usefulness (addressed/ignored by
  severity is the most robust signal, denominators 56–334) — proposed but
  **declined this session**; `ccrRecallRate` kept as headline. Revisit if the low
  catch rate proves unactionable.
- **More history / more repos** — backfill additional months or other target repos
  using the playbook in section A.
- Decide whether to open a PR from this branch to `upstream`.

## Key files

| File | Role |
|------|------|
| `scripts/compute-metrics.ts` | Source of truth for all metrics; `computeCcrRecallRate` helper. |
| `scripts/attribute-comments.ts` | `computeCcrSawCode` — the strict gate behind `isGap` (kept). |
| `scripts/aggregate-runs.ts` | CLI aggregation mirror (trend core). |
| `dashboard/js/aggregate.mjs` | Browser aggregation + `poolSlices` (dashboard-only). |
| `dashboard/js/app.mjs` | Dashboard orchestration + chart rendering. |
| `dashboard/js/charts.mjs` | Chart.js wrappers (`lineChart`, `barChart`, `groupedBar`). |
| `dashboard/index.html` | Chart sections + copy. |
| `dashboard/data/*.json` + `manifest.json` | The 8 live runs the dashboard reads. |
| `decisions.md` | Design-decision log (see **D14** for the metric redesign). |
| `README.md` | Metric definitions and rationale (Q1–Q4). |
| `scripts/utils.ts` | `Semaphore` + `ghRequestLimiter` (concurrency cap), `ghApiJsonAsync`/`ghApiGraphqlAsync`, header-aware backoff. |
| `scripts/fetch-prs.ts` | PR fetch/cache; bounded parallel fan-out (`fetchPrToCache`). |
| `optimize-api-call-plan.md` | API-call optimization items #1–#5, status, test plan. |
| `api-rate-limit.md` | Rate-limit reference: cost model, primary/secondary limits, token/enterprise, scaling levers. |
