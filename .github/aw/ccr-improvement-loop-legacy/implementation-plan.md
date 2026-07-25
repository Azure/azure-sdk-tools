# Implementation Plan — CCR Improvement Loop Polish & Scale

Executable, step-by-step build order derived from [`polish-plan.md`](./polish-plan.md).
Every phase ends in a **checkpoint gate**: a concrete, named set of tests/commands
that must be green before the next phase starts. If a gate is red, fix forward or
revert — never proceed past a red gate.

> **Revision note.** This plan was hardened after a design review, then
> **right-sized after a scope review**. It covers **only what is being built**;
> deferred sub-tracks, decision points, scrapped alternatives, and live-proof
> deferrals live in the companion
> [`deferred-and-scope.md`](./deferred-and-scope.md).
> The load-bearing pieces are: (1) a single **durable state store** (a
> `ccr-pacer-state` branch) replaces the "read outputs from the checkout"
> assumption; (2) a **canonical window identity** stamped into the run JSON
> replaces filename matching; (3) backlog windows are **settled-only**
> (end ≤ today − settleDays); (4) the child returns a **typed terminal outcome**;
> (5) admission uses a **static conservative budget check + resume-next-tick**,
> not a predictive multi-resource estimator.
>
> **REST minimization is the one active scale lever: #4 (T2) per-PR GraphQL fixed
> reads, in Phase 2.** The per-*workflow*-repo budget is **shared across all target
> repos** (15,000/hr on GHEC), so it does not grow as repos are added; #4 moves the
> fixed 5 REST reads onto the _separate_ GraphQL point pool. Steady-state (~24k
> calls/mo at 6 repos ≈ 0.2% of budget) needs nothing more; the other tiers (T1
> per-repo tokens, #7 cross-PR batching, #6 git patches) are **deferred** — see
> [`deferred-and-scope.md`](./deferred-and-scope.md).

## Ground rules (apply to every step)

- **The universal gate** (run from `.github/aw/ccr-improvement-loop/`) after each
  step, and always at a checkpoint:
  ```bash
  pnpm run typecheck && pnpm run lint && pnpm run format:check && pnpm test
  ```
  (`package.json` scripts: `test` = `vitest run`; Node ≥ 24, native TS strip, no
  build step.) Nothing merges with any of these red.
- **New behavior ships with a test that failed before the change and passes
  after.** For **#4 (per-PR GraphQL fixed reads, Phase 2)** the test is an
  _equivalence golden test written and passing before_ the migration is wired in,
  measured at the **normalized downstream evidence contract, not raw REST bytes**
  (the per-PR fixed-read fields — reviews, comments, threads/`isResolved`, linked
  issues/labels — plus `committedAt` ordering, byte-identical after
  normalization). #7 and #6 stay **deferred**
  ([`deferred-and-scope.md`](./deferred-and-scope.md)); their heavier oracles are
  not built here.
- **Proposal-only / deterministic-prep → agent-judgment → deterministic-math
  stays intact** for the child loop (`decisions.md` D2, D9). The pacer is plain
  deterministic orchestration; it is the _only_ component permitted durable
  writes, and only its ledger job writes state (never to the target repo's tree).
- **No new PAT/App token, no always-on process, no in-job `sleep`, no importing
  prior TS/Python impls** (`polish-plan.md` non-goals). The pacer persists state
  with the plain `GITHUB_TOKEN`; its ledger job needs `contents: write`, which is
  repo-wide in GitHub Actions YAML, so protect all non-state branches with a
  branch ruleset and script the ledger to push only normal, non-force updates to
  `ccr-pacer-state`. A `GITHUB_TOKEN` push does **not** trigger another
  push-triggered workflow; that recursion guard is event-based.
- **The 15,000/hr GHEC token is not available yet (arriving later).** Every gate
  here is satisfied by **offline mock-`gh`/fixture tests only**. Live-budget proofs
  (no-403 runs, real `rate_limit` numbers) are **DEFERRED — needs GHEC token** and
  **not gating**. Do not add any test that requires the real token to pass.
- **Recompile `ccr-improvement-loop.lock.yml`** whenever
  `ccr-improvement-loop.md` changes; commit the lock alongside source. A reusable
  `uses:` must reference the **compiled** `*.lock.yml`, never the `.md` source.
  `ccr-pacer.yml` is a hand-written plain Actions YAML workflow, not gh-aw, so it
  has **no** lock file.

## Grounding: what exists today (verified in code)

- Prep steps + agent prompt: `../../workflows/ccr-improvement-loop.md`
  (compiled → `ccr-improvement-loop.lock.yml`). `CCR_CACHE =
${{ github.workspace }}/.ccr-cache`, written fresh each run (no cross-run cache).
- `fetchPrToCache` (`scripts/fetch-prs.ts:362`) issues **5 fixed REST reads**
  (`pulls/{n}`, `.../reviews`, `.../comments`, `issues/{n}/comments`,
  `pulls/{n}/commits`; lines 381–385) then a **per-commit `GET /commits/{sha}`**
  loop (~394 — the dominant `N` term). `committedAt` prefers
  `c.commit.committer?.date ?? c.commit.author?.date`.
- Async requests go through `ghRequestLimiter` (`utils.ts:269`, the #5 `Semaphore`)
  via `ghApiJsonAsync`/`ghApiGraphqlAsync`; this handles secondary/burst limits and
  stays essential regardless of tier.
- **Run identity today is weak:** `runIdOf(windowEnd, repo)` =
  `${windowEnd}_${owner}_${name}` (`emit-run-json.ts:113`) — it encodes **only**
  `windowEnd` + repo, not `windowStart`, cap, or schema. Because `run.id` is the
  dedupe/idempotency key and `run-${run.id}.json` filename, Phase 0 changes
  `run.id` itself to the canonical window identity end-to-end.
- **`generatedAt` is wall-clock** (`emit-run-json.ts:294`,
  `new Date().toISOString()`) — a golden byte-compare of run JSON is impossible
  without freezing/stripping it (Phase 0).
- **Settled-window methodology:** the rolling path ends at `today − settleDays`
  (`prep-run.ts:223-232`); explicit `--window-end` bypasses that cutoff. A backlog
  that clamps to "today" would mine unsettled PRs (Phase 3 fixes this).
- **`run-*.json` is uploaded as a workflow artifact** via `safe-outputs:
upload-artifact` (`ccr-improvement-loop.md` safe-outputs, `.ccr-runs/run-*.json`);
  it is **not** written into the checkout's `dashboard/data/` (a separate publish
  workflow does that). Reusable-workflow jobs do **not** share a workspace — so
  "read prior run JSON from the checkout" is unavailable (Phase 3 durable store).
- Cache versioning: `RAW_SCHEMA_VERSION` = `"1.0"`, `SCHEMA_VERSION` = `"1.0"`
  (`scripts/run-schema.ts:11-12`).
- Child no-ops (emits no `run-*.json`) when `< 50` settled PRs
  (`config.json:minPrs`).
- **Default runs are uncapped.** `max_prs` empty/`0` = uncapped full cohort
  (`MAX_PRS`; `prep-run.ts --max-prs` default `"0"`). Preserve this everywhere;
  capping is opt-in only.

---

## Phase 0 — Baseline, determinism, and canonical identity

**Goal:** make later gates _possible_ — a deterministic oracle and a real
completion key — before any feature work.

1. **Baseline.** Run the universal gate; record the pass. Green starting point.
2. **Freeze the clock for equivalence.** Make `generatedAt` injectable (a `now()`
   param / env default) so golden compares are deterministic, **and** define a
   `canonicalizeRun(run)` helper that strips/normalizes volatile fields
   (`generatedAt`) for equality assertions. Golden tests compare canonicalized
   output.
3. **Canonical window identity (the completion key).** Add `computeWindowId(...)`
   returning a stable string:
   `${owner}_${repo}__${window_start}__${window_end}__${cohort}__raw${RAW_SCHEMA_VERSION}`
   where `cohort = "uncapped" | "cap-<N>"`. Wire it end-to-end: `emit-run-json`
   sets `run.id = windowId`, the artifact file is `run-<windowId>.json`, and the
   existing `dedupeRuns` implementations keep keying on `run.id` unchanged. This
   is a **data migration**: bump `SCHEMA_VERSION`, add a migration/recompute step
   like existing history migrations, and update/test `aggregate-runs.ts`,
   `dashboard/js/aggregate.mjs`, the dashboard manifest, and
   `ccr-dashboard-publish.yml` copy-by-basename behavior for the new filenames.
   The supersede semantics intentionally change: a capped run no longer
   supersedes an uncapped run for the same repo/dates. This one encoding is reused
   for run records, ledger records, skip entries, and cache keys.
4. **Golden oracle fixture (scoped to #4).** Snapshot the current normalized
   evidence at the layers #4 touches — the **per-PR normalized record** (reviews,
   inline/issue comments, thread `isResolved`, linked issues/labels) and
   `judge-input.json` — over `tests/fixtures/pr-sample.json`. This is the oracle
   the Phase 2 GraphQL migration diffs against. The **commit `files`/patch layer
   (`attributed.json`) is out of scope here** — it's only needed if #6 (deferred)
   is revived.

### ✅ Gate 0

- Universal gate green.
- **Determinism test:** two emits with a fixed injected clock produce identical
  `canonicalizeRun` output; the raw outputs differ only in `generatedAt`.
- **Identity tests:** `computeWindowId` changes when repo/start/end/cohort/schema
  changes and is stable otherwise; `emit-run-json` sets `run.id` to that value and
  writes `run-<windowId>.json`; two runs differing only by cohort get distinct
  `run.id`s and both survive `dedupeRuns` in `aggregate-runs.ts` and
  `dashboard/js/aggregate.mjs`; the dashboard manifest and
  `ccr-dashboard-publish.yml` copy-by-basename path still parse/copy them.
- Committed golden oracle snapshot (per-PR fixed-read layers, for #4) loaded by
  the suite.

---

## Phase 1 — Track A: cross-run cache persistence (A1)

Makes "resume with zero rework" real. Independent of fetch strategy, and its key
is stated in terms of the Phase 0 identity. The canonical key must be known
**before** prep starts.

1. **Standalone window/cohort resolver.** Extract deterministic window + cohort
   resolution from `prep-run.ts` into a pure resolver callable before cache
   restore. Move the input normalization that affects identity here: schedule
   (empty → rolling), `workflow_dispatch`, and later `workflow_call` all resolve
   through the same function, including `max_prs` default `0`/uncapped and the
   rolling `today − settleDays` window. `prep-run.ts` consumes the resolved values
   instead of owning identity resolution.
2. Surface `RAW_SCHEMA_VERSION` where the workflow can read it (step output in
   `ccr-improvement-loop.md`) and compute the canonical `windowId` before prep.
3. **`actions/cache` step pair** in `ccr-improvement-loop.md`: restore by the
   canonical prefix (`ccr-cache-${windowId}-`) before prep, but save only after a
   successful full raw-fetch under a unique completion key (for example, include
   run attempt / completed marker), so an immutable mid-fetch cache can never
   poison retries. Restore/save **only** `.ccr-cache/pr-*.json` (raw PR cache) —
   never the judged/emitted artifacts (cheap to recompute; coupling invites
   staleness, D12).
4. Recompile `ccr-improvement-loop.lock.yml`; commit source + lock together.

### ✅ Gate 1 (cache persistence)

- **Resolver tests:** schedule, dispatch, and workflow-call-shaped inputs produce
  the same canonical `windowId` for the same repo/window/cohort; rolling windows
  match the existing `today − settleDays` semantics; `max_prs` empty/`0` resolves
  to the uncapped cohort.
- **Zero-refetch test:** same `(repo, window, cohort)` prep twice against a seeded
  `.ccr-cache`; the second run issues **zero** `gh api` PR fetches (all hits),
  asserted with an injected/mock `gh` recorder (build the recorder here; Phase 2
  and Phase 4 reuse it).
- **Poison-cache test:** a failed-mid-fetch run does not save the final cache key;
  retry completes the raw fetch; a subsequent run restores the completed cache and
  does zero refetch.
- **Key-invalidation test:** bumping `RAW_SCHEMA_VERSION` (or cohort) changes the
  cache key, forcing a clean re-fetch.
- Universal gate green; `ccr-improvement-loop` lock recompiled + committed.
- _DEFERRED — needs GHEC token:_ one uncapped month completes without a 403; a
  repeated window is all hits.

## Phase 2 — Track C (T2): fixed reads onto GraphQL (#4, per-PR), equivalence-gated

**Scope: #4 only, one GraphQL query per PR** — move the five fixed REST reads
(`pulls/{n}`, `.../reviews`, `.../comments`, `issues/{n}/comments`,
`pulls/{n}/commits`) **plus thread `isResolved` + linked issues** onto a **single
per-PR GraphQL query** on the **separate GraphQL pool** (15,000 pts/hr on GHEC
`GITHUB_TOKEN`, 10k GHEC-App, 5k PAT — _not_ ~5k; see `api-rate-limit.md`). The
budget win is the **separate pool** (frees REST budget), and it is **independent
of cross-PR batching**. **Commit patch text stays on REST** (`commits/{sha}`) —
removing that `N` term is #6 (T3), **deferred** —
and **cross-PR aliasing/batching is #7 (T2-batch), also deferred**
([`deferred-and-scope.md`](./deferred-and-scope.md)).

Why active (not deferred) even with a 15k token: the workflow-repo budget is
**shared across all target repos** (`api-rate-limit.md §4`), so at 6+ repos the
backfill burst competes for one REST pool; #4 moves the fixed 5 onto the parallel
GraphQL pool, roughly halving REST pressure per PR.

**Scope honestly — #4 is not "trivially simple."** Even per-PR (no batching), #4
requires: **per-connection cursor pagination** (GraphQL connections cap at 100, so
any PR with >100 reviews/comments/commits/threads/linked issues paginates),
**partial-response handling** (`data` + `errors` together — define accept/retry/
reject), and — the real load-bearing risk — **REST↔GraphQL semantic equivalence**
(ordering, author identity, review states, deleted/minimized comments, timestamps).
Silent metric drift is worse than a 403, so the equivalence gate is the point of
this phase, not an afterthought.

### Step 2.0 — Injectable request layer (enabling refactor)

Thread a `fetchFn`/`gh` client into `fetchPrToCache` (`fetch-prs.ts:362`),
defaulting to the real async helpers, so tests substitute the Phase 1 recorder
(call count, args, concurrency). Behavior-preserving.

- **Gate 2.0:** per-PR fixed-read oracle unchanged; the recorder observes the
  current pattern (5 fixed REST reads + 1 GraphQL + `N` per-commit reads).
  Universal gate green.

### Step 2.1 — #4: fixed reads onto one GraphQL query per PR

Collapse the five fixed REST reads **plus thread `isResolved`/linked issues** into
**one GraphQL query per PR**, admitted through `ghRequestLimiter` (#5).

- **Per-connection pagination is mandatory even without batching.** Every nested
  connection (reviews, inline comments, issue comments, commit shas, review
  threads@100, linked issues/labels@20) carries its **own cursor**; cursor-loop
  each to exhaustion, with deterministic ordering matching REST. Handle **partial
  GraphQL responses** (`data` + `errors`) explicitly; read `rateLimit { cost }`
  back per query for observability.
- **Points don't drive this decision, and the effect is shape-dependent.** The
  documented cost is `round(Σ connection-requests / 100)` with a 1-point floor per
  query, so per-PR queries pay that floor ~300×/month (~300 pts) — still ~2% of a
  15k/hr pool. Do **not** hard-code assumptions from the estimate; read actual
  `rateLimit.cost`. The pacer's per-hour repo-month bound (Phase 4) keeps the
  **aggregate** across N repos within pool headroom (the constraint is
  `~ per-PR-floor × PRs × repo-months-per-hour`, not a single window).
- **Preserve a bounded micro-batch seam.** Keep the query builder able to accept
  a small alias set (10–25 PRs) later **without** implementing general batching now
  — so #7 can be added behind the same interface if a measurement ever justifies it.
- **Commit patch text stays on REST** (`commits/{sha}`) — #6/T3, not this step.
- **Gate 2.1 (equivalence):** golden test compares the assembled per-PR record
  GraphQL-vs-REST over fixtures that **exceed one page on every connection**
  (reviews, inline, issue comments, commits, threads, linked issues, labels) and
  over **pathological PRs** (deleted/minimized comments, mixed review states,
  many-page commits); assert normalized output identical, no truncation. Recorder
  asserts the fixed 5 reads are **GraphQL, not REST** (per-PR primary-REST cost
  drops to `1 + N`: the PR-list search + commit detail). Universal gate green.

### ✅ Gate 2 (T2 / #4 complete)

- 2.0/2.1 gates green; the five fixed REST reads are **gone** (per-PR GraphQL on
  the separate pool); per-PR primary-REST cost `6 + N → 1 + N`.
- **Cost-model regression test:** mock `gh` recorder asserts the fixed reads are
  GraphQL, not per-PR REST — a future change reintroducing per-PR fixed REST reads
  **fails CI**.
- **Aggregate-budget assertion:** given the pacer's max repo-months/hour, the
  worst-case per-PR-floor GraphQL point total stays under the 15k/hr pool with
  margin (documents the scheduling assumption the "300 is trivial" claim relies on).
- The Phase 0 per-PR fixed-read oracle still matches (no information loss).

> **Deferred sub-tracks — not built here.** #7 (cross-PR GraphQL aliasing/batching,
> T2-batch) and #6 (commit detail from local git, T3) are documented — with their
> rationale, revival triggers, and the seams the plan preserves for them — in
> [`deferred-and-scope.md`](./deferred-and-scope.md). Step 2.1 keeps the
> micro-batch seam so #7 can be added later without rework.

---

## Phase 3 — Track B (part 1): reusable loop, typed outcomes, durable state, derived backlog

The backlog is **derived each tick** from config + today's date + a **durable
completion ledger**, not a hand-seeded status file. Durability and identity are
solved here so the pacer (Phase 4) can be thin.

1. **Make `ccr-improvement-loop.md` callable.**
   - Add `on: workflow_call` with `inputs` mirroring `workflow_dispatch` (`repo`,
     `window_start`, `window_end`, `max_prs`). Reuse the Phase 1 resolver for all
     input normalization; Phase 3 only wires the `workflow_call` boundary and
     reconciles existing `github.event.inputs.*` reads to the resolver outputs.
2. **Typed terminal outcome artifact.** A deterministic post-step writes and
   uploads an outcome artifact named `outcome-<window_id>.json` through the
   existing safe-output/upload path. The JSON contains
   `{ window_id, outcome, artifact_name, run_id, attempt, ts, cost }`
   where `outcome ∈ produced | thin-noop | signal-noop | failed` and `cost` is the
   observed budget spend (logged for `--dry-run` visibility, not fed to an
   estimator). `artifact_name`
   is derived deterministically from `window_id` (for example, `run-<window_id>`),
   not read from unstable upload temp IDs. Mark `produced` **only after** the run
   JSON artifact upload is confirmed; if the upload or safe-output processing
   fails, classify `failed`. `thin-noop` (`< minPrs`) and `signal-noop` (≥ minPrs
   but the agent called `noop`) are explicit done outcomes.
3. **Durable state store — a `ccr-pacer-state` branch.** One orphan branch is the
   single source of truth (git-native, auditable, no PAT):
   - `ledger/<window_id>.json` — one record per terminal window:
     `{ window_id, repo, window_start, window_end, cohort, schema_version,
outcome, attempt, run_id, artifact_name, ts }`.
   - `skipped.json` — poison windows retired after the retry cap.
     The pacer reads this branch (`git fetch origin ccr-pacer-state` into a subdir)
     and the Phase 4 ledger job initializes it as an empty orphan branch if absent.
     Records are keyed by canonical `window_id`, so appends don't collide and
     writes are idempotent (re-writing the same terminal record is a no-op).
     _(No `estimates.json` — the simplified static budget check in Phase 4 needs no
     per-connection cost feedback loop; see
     [`deferred-and-scope.md`](./deferred-and-scope.md).)_
4. **`scripts/gen-backlog.ts` (pure, settled-only window expander).** From
   `pacer/config.json` `{ repos, start_date, granularity, max_prs? }`
   (`granularity ∈ month|biweekly|week`, monthly default; `max_prs` default
   `0`/uncapped) and a "today", expand `start_date … (today − settleDays)` into
   candidate windows. **Emit only closed, settled windows** whose
   `window_end ≤ today − settleDays`; never emit a trailing partial/unsettled
   window. Biweekly = fixed 14-day windows anchored on `start_date`, clamped so the
   last emitted window still ends ≤ the settle cutoff. Each candidate carries its
   `window_id` (Phase 0).
5. **`scripts/backlog.ts` (derive pending).** `pending = desired − done − skipped`,
   where **done** = a `ledger/<window_id>.json` with `outcome ∈ {produced,
thin-noop, signal-noop}` exists (matched by `window_id`, **not** filename), and
   **skipped** = listed in `skipped.json`. `failed` records are _not_ done —
   eligible for one retry (Phase 4).
6. Add zod schemas for `config.json`, outcome artifacts, ledger records, and
   `skipped.json`.

### ✅ Gate 3 (reusable loop + durable derived backlog)

- **Input-normalization + lock test:** the compiled `ccr-improvement-loop.lock.yml`
  resolves `repo/window/max_prs` identically for schedule, dispatch, and
  workflow_call fixtures via the Phase 1 resolver; the `uses:` reference points at
  the **`.lock.yml`**, not the `.md`. Validate by compiling, not just textual 1:1
  input comparison.
- **Typed-outcome artifact tests:** the classifier maps each scenario (confirmed
  run-artifact upload / `< minPrs` / agent `noop` / non-zero exit / upload failure)
  to the correct `produced|thin-noop|signal-noop|failed` and writes a deterministic
  `outcome-<window_id>.json` with stable `artifact_name` and observed `cost`.
- **`gen-backlog.ts` tests:** monthly/biweekly/weekly expansions for a fixed
  `(config, today)` emit only windows ending ≤ `today − settleDays` (assert a
  just-closed-but-unsettled period is **excluded**, and appears only after it
  settles); biweekly windows are exactly 14 days, anchored, non-overlapping;
  `max_prs` defaults uncapped; unknown granularity / invalid range error.
- **`backlog.ts` derive-pending tests (identity-driven):** with a seeded fake
  `ledger/` + `skipped.json`, `pending` matches by `window_id` — a ledger record
  whose repo/start/cohort/schema differs is **not** treated as done; `produced`/
  `thin-noop`/`signal-noop` count done, `failed` does not; empty ledger → all
  pending; fully-ledgered range → empty (termination).
- Universal gate green; `ccr-improvement-loop` lock recompiled + committed.

## Phase 4 — Track B (part 2): the pacer (static budget check + resume)

Thin cron wrapper over tested TypeScript. The pacer workflow is hand-written,
plain deterministic GitHub Actions YAML — no agent execution, no gh-aw compile,
and no second lock file. Admission is a **static conservative budget check +
resume-next-tick**, not a predictive estimator (see
[`deferred-and-scope.md`](./deferred-and-scope.md)).

1. **`scripts/pace.ts` (planner, pure + testable).**
   - `readBudget()` — parse `gh api rate_limit`, take remaining across the metered
     pools: `core` REST (carries `1 + N` per PR after Phase 2's #4 — the primary
     constraint), `graphql` points, and `search` (30/min). Meter `graphql` for
     **hygiene, not because points bind**: a whole uncapped month of per-PR fixed
     reads is only ~300 points (per-query floor) against a 15k/hr pool. The GraphQL
     exposure that actually bites at scale is the **aggregate across repos** — the
     constraint is `~ per-PR-floor × PRs × repo-months-per-hour`, which the pacer's
     `max_windows_per_tick` bounds — plus per-query resource limits (500k-node cap,
     CPU guard) handled in Phase 2 + `ghRequestLimiter` (#5), not this check.
   - `estimateCost(window)` — a **static conservative per-window upper bound** from
     fixed constants (configurable `est_rest_calls_per_window` and
     `est_graphql_points_per_window`, each defaulting to a value that covers a
     high-volume uncapped month with margin). No count probes, no per-connection
     cardinality model, no `estimates.json`. A window whose fixed estimate exceeds
     the **currently remaining** usable budget on **any** metered pool is
     **deferred to a later tick** (the tick's budget is already spent, not a
     property of the window) with a logged reason — never silently admitted; the
     cache makes the re-dispatch free.
   - `assertFittable(pending, budget)` — **plan-time invariant that closes the
     starvation hole.** A deferred window only resumes next tick if it can fit a
     _fresh, full_ hour. Because `estimateCost` is a fixed constant, "fits a fresh
     hour" is a **global property of the configured granularity, not per repo**: if
     `estimateCost(window) > freshHourUsable − safetyMargin` on **any** metered
     pool, that window (and every window at that granularity) can **never** be
     admitted and would defer forever — invisibly, since a never-admitted window
     produces no outcome artifact, never becomes `failed`, and so never hits the
     retry cap that retires poison windows to `skipped.json`. So at plan time,
     assert every pending window's static estimate fits a fresh full hour's usable
     budget on every pool. On violation, **fail the `plan` job loudly** (non-zero
     exit, clear message naming the pool, the estimate, the fresh-hour budget, and
     the fix: lower `est_*_per_window`, raise the ceiling via T1, or configure a
     finer `granularity`). Detect the whole-config error at admission time, never
     let it hide as a perpetually-pending window. `readBudget()` also exposes the
     per-pool _limit_ (not just remaining) so this check uses the fresh-hour
     ceiling, independent of the current tick's spend.
   - `derivePending()` — `gen-backlog` (settled desired) minus ledger-done minus
     skipped, from the fetched state branch.
   - `admit(pending, budget)` — greedily pick pending windows whose fixed estimate
     fits `remaining − safetyMargin` on **every** metered pool, capped by a small
     `max_windows_per_tick`. Preserve **≤ 1 concurrent window per repo**. Whatever
     doesn't fit this tick is simply picked up next cron tick (the cache makes
     every re-dispatch idempotent and resumable, and `assertFittable` guarantees a
     deferred window _can_ fit a fresh hour, so a defer is always temporary — never
     a livelock) — so no drain-time modeling or serial-lane machinery is needed.
     Across different repos it may admit N concurrent lanes within budget.
   - `reconcile()` — all admitted windows must have an outcome artifact after the
     tick. Missing artifact = `failed`; a `failed` ledger record → one retry
     (increment `attempt`); on the second `failed`, move to `skipped.json` with a
     reason (retry cap = 1, durable `attempt` in the ledger, no poison loop).
   - `--dry-run` — print the admission plan (windows + est cost per pool +
     remaining pending count) and exit without touching the state branch.

   > **T1 — per-repo credential scaling (decision point, not built).** `admit` must
   > read **per-pool** budgets so a future per-repo App-installation token simply
   > widens `remaining` without code changes (built + tested in Gate 4). The token
   > itself, its rationale, and the saturation trigger are in
   > [`deferred-and-scope.md`](./deferred-and-scope.md) (`decisions.md` D17).
2. **`ccr-pacer.yml`** — `on: schedule: cron (hourly)` + `workflow_dispatch`; a
   **plain YAML** workflow with serialized `concurrency: { group: ccr-pacer,
cancel-in-progress: false }` so two ticks never race. No gh-aw source, no lock
   file, no agent steps. Job graph:
   - `plan`: fetch `ccr-pacer-state` (or create an empty orphan branch if absent)
     → `pace.ts` (reconcile + derive + admit) → `outputs.admitted`.
   - `backfill`: `strategy.matrix.window: ${{ fromJSON(needs.plan.outputs.admitted) }}`,
     `uses: ./.github/workflows/ccr-improvement-loop.lock.yml` with per-window
     inputs. The child `group: ccr-loop-${repo}` (1 running + 1 pending) plus the
     `≤ 1 window/repo` admission rule keep same-repo work serialized.
   - `ledger`: `if: always()`, `permissions: contents: write`, download all
     `outcome-<window_id>.json` artifacts from this tick, reconcile every admitted
     window (missing outcome artifact = `failed`), write/update
     `ledger/<window_id>.json`, then fetch/rebase/retry and normal non-force push
     to `ccr-pacer-state` with `GITHUB_TOKEN`. Because `contents: write` is
     repo-wide, require a branch ruleset protecting all non-state branches; the
     script still writes only the state branch. No status field to flip; records
     are canonical + idempotent.
3. **Scope guards (B4):** no in-job `sleep`; `pace.ts` imports no judge/metric
   modules.

### ✅ Gate 4 (pacer)

- **Static-admission tests:** given `{core, graphql, search}` remaining and the
  fixed per-window estimates, `admit` picks windows that fit **every** metered
  pool minus safety margin, honors `max_windows_per_tick`, and stops; a low budget
  on any pool blocks admission (mechanical two-pool check — in practice `core` REST
  is the pool that binds); a single window exceeding usable budget is
  deferred/split, never admitted. Unfit windows remain pending for the next tick.
  A widened `remaining` (simulating a per-repo T1 token) admits more without code
  changes.
- **Fit-invariant tests (`assertFittable`, starvation guard):** a window whose
  static estimate exceeds a **fresh full hour's** usable budget on any pool (even
  though the current tick's `remaining` is low for an unrelated reason) makes the
  `plan` job **exit non-zero** with a message naming the pool/estimate/fresh-hour
  budget/fix — it is **not** silently left pending; conversely, a window that fits
  a fresh hour but not this tick's depleted `remaining` passes `assertFittable` and
  is merely deferred (stays pending, no error). A regression that lets an
  unfittable-forever window sit pending — never `failed`, never retired to
  `skipped.json` — **fails CI**.
- **Concurrency tests:** same-repo work is never concurrent (`≤ 1 window/repo` per
  tick + child concurrency group); across repos, multiple lanes may run within
  budget.
- **Reconciliation/retry tests:** a `failed` record or missing outcome artifact →
  one retry (`attempt` incremented durably) → `skipped.json` on the second failure
  (cap = 1, can't loop); `thin-noop`/`signal-noop` are done, never retried/wedged.
- **`--dry-run` test:** prints the plan and remaining-pending count, mutates
  neither the state branch nor `skipped.json`, issues no `gh` call beyond
  `rate_limit`.
- **Workflow-boundary integration test (the key gate):** compile/exercise the real
  `ccr-improvement-loop.lock.yml` reusable boundary from the plain `ccr-pacer.yml`
  with two successful matrix legs and one failing leg; the ledger job runs with
  `if: always()`, downloads outcome artifacts, and records all three admitted
  windows correctly (2 done, 1 failed → retry) despite the failed leg.
- **Two-tick offline integration test:** over a fake state-branch dir and stubbed
  outcome artifacts — **tick 1** admits work and writes typed ledger records;
  **tick 2** derives the correct remainder (done windows not re-admitted,
  `skipped` honored, a `failed` retried once, previously-deferred windows now
  admitted). Unit tests alone won't catch these workflow-boundary bugs.
- **Git-state tests:** absent `ccr-pacer-state` initializes an empty orphan branch;
  non-fast-forward push fetches/rebases/retries; pushes are normal non-force
  updates; branch ruleset requirement is documented.
- **Scope-guard assertions:** no `sleep`; no judge/metric imports.
- Universal gate green; `ccr-improvement-loop` lock recompiled + committed;
  `ccr-pacer.yml` is plain YAML (no lock).
- _Milestone proofs — DEFERRED, needs GHEC token:_ a `repos × months` campaign
  drains across ticks → run-JSONs, derived `pending` ends empty, no 403, no PAT
  (M2); a mid-backfill restart re-derives and resumes, a poison/thin window
  doesn't wedge (M3). Proven offline now via the workflow-boundary, two-tick, and
  reconciliation tests.

## Phase 5 — Track C: dashboard scale, preflight guard, hygiene, docs (C2, C3, C4)

Parallel-safe; must land before Definition of Done.

1. **C2 — Preflight budget guard.** Optional `gh api rate_limit` preflight at the
   top of `prep-run.ts` behind `--check-budget` (default on in CI): if the metered
   resources (**`core` REST and `graphql` points**, plus search) are below the
   static per-window cost estimate, abort early with a clear message and non-zero
   exit (no partial cache). Meters the same pools as Phase 4 admission.
2. **C3 — Dashboard.**
   - `scripts/gen-manifest.ts` — regenerate `dashboard/data/manifest.json` from the
     `run-*.json` present (browser can't glob, D11). Build-free local script.
   - **PR-size bucket (S/M/L/XL)** `sizeBucket` slice via schema +
     `compute-metrics.ts` + a chart, from already-captured `additions`/`deletions`;
     one deterministic recompute over existing raw fields (no agent re-run).
   - **Repo faceting** — trend charts filter by `owner_repo`; never compare repos
     on raw absolute burden (trend within a repo only).
3. **C4 — Hygiene & docs.** Pacer `--dry-run` (Phase 4) already done. Converge
   docs: `api-rate-limit.md §7` → "pacer + #4 shipped, #7/per-repo-token & #6
   deferred"; `optimize-api-call-plan.md §8` → #4 ✅, #7/#3/#6 deferred;
   `README.md` "Backfilling at scale" (shared budget, T1/T2/T3 tiers); `decisions.md`
   **D15** (backfill pacer — decision/why/rejected alternatives incl. always-on
   runner, in-job sleep, cross-workflow dispatch needing a PAT, per-run cap only,
   **committed status-tracked backlog** [replaced by a derived backlog over a
   durable canonical ledger]), **D16** (**REST minimization tiered** — #4 per-PR
   GraphQL fixed reads shipped for the shared-budget/6+-repo case; #7 cross-PR
   batching deferred [failure-isolation cost > secondary-limit win the #5 limiter
   already covers]; #6 git-patch `N`-removal deferred as highest-maintenance,
   evidence-gated), and **D17**
   (**per-repo App-installation credentials** as the primary N-repo scale lever —
   config not code, decision point pending a saturation reading); a `handoff.md`
   session entry.

### ✅ Gate 5 (scale polish)

- **`gen-manifest.ts` test:** a set of `run-*.json` → correct `manifest.json`; a
  50-window set needs no hand edits.
- **`sizeBucket` migration test:** recompute over an existing fixture yields the
  expected buckets; new slice validates against `run-schema`.
- **C2 preflight test:** mock REST/graphql/search budget below the static cost
  estimate → non-zero exit + clear message, no cache written; above cost → proceeds.
- Docs converged (D15, D16, D17, handoff entry); no dangling "deferred" labels.
- Universal gate green; `ccr-improvement-loop` lock recompiled + committed;
  `ccr-pacer.yml` remains plain YAML (no lock).

---

## Final acceptance — Definition of Done

- [ ] A `repos × months` backfill drains **unattended** under `cron`, never
      exceeding the metered ceilings, **zero rework** on resume — completion state
      durable in `ccr-pacer-state`, matched by canonical `run.id`/`window_id`;
      whatever doesn't fit a tick's budget is picked up on the next tick.
      _(Proven offline via the workflow-boundary, two-tick + reconciliation tests;
      the live no-403 GHEC run is DEFERRED until the token lands.)_
- [ ] Backlog windows are **settled-only** (end ≤ today − settleDays); no unsettled
      trailing window is ever mined or ledgered.
- [ ] Child loop unchanged in what it **measures** (D2, D9); the pacer is the only
      durable writer. Its ledger job has repo-wide `contents: write`, protected by
      branch rulesets and scripted to write only the state branch.
- [ ] Admission uses a **static conservative budget check + resume-next-tick**,
      metering **both `core` REST and `graphql` points** (plus search), with
      `≤ 1 window/repo` concurrency — no predictive estimator, count probes, or
      drain-time modeling. A plan-time `assertFittable` invariant guarantees every
      deferred window can fit a fresh hour, so a defer is never permanent
      starvation; an unfittable-forever granularity fails the `plan` job loudly
      instead of hiding as a perpetually-pending window.
- [ ] **REST minimization is tiered for the shared, 6+-repo budget:** #4 per-PR
      GraphQL fixed reads **shipped** (fixed 5 REST reads → separate point pool;
      per-PR primary-REST `6 + N → 1 + N`); **#7 cross-PR batching**, **T1 per-repo
      App-token budgets** and **#6 git-patch `N`-removal** are documented decision
      points, revived only on a real saturation / binding-REST reading.
- [ ] Dashboard ingests a large backfill without hand-editing, facets by repo,
      reads honestly.
- [ ] All gates green: `pnpm test`, `typecheck`, `lint`, `format:check`;
      `ccr-improvement-loop.lock.yml` recompiled + committed; `ccr-pacer.yml` is
      plain YAML (no lock); docs converged; `D15`, `D16`, `D17`, `handoff.md`
      recorded.

## Milestone → Phase map

| Milestone                          | Phases  | Proof                                                                                                                                                                                             |
| ---------------------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| M1 Foundation + cache resume       | 0, 1    | _Offline:_ deterministic canonicalized emits; repeated window = all cache hits; poison-cache + key-invalidation. _Deferred (GHEC):_ uncapped month, no 403                                       |
| M2 REST minimization (T2, #4)      | 0, 2    | _Offline:_ per-PR fixed-read oracle identical GraphQL-vs-REST across pagination; fixed 5 REST reads gone (`6 + N → 1 + N`); cost-model regression. _Deferred (GHEC):_ live no-403                 |
| M3 Reusable loop + derived backlog | 3       | _Offline:_ typed outcomes classified; derive-pending matches by `window_id`; settled-only windows. _Deferred (GHEC):_ live run-JSONs                                                              |
| M4 Pacer                           | 4       | _Offline:_ static two-pool admission + `≤1` concurrent/repo, workflow-boundary artifacts, two-tick integration (deferred windows resumed), reconciliation/retry-cap. _Deferred (GHEC):_ live drain |
| M5 Scale polish                    | 5       | Manifest auto-gen, `sizeBucket` migration, two-pool preflight — all offline                                                                                                                       |
| (opt) T1 per-repo tokens / #6 git  | 4 / 2   | Decision points; revive on saturation (T1) or binding-REST (#6) reading                                                                                                                          |

---

## Scrapped decisions (scope review)

The alternatives cut to keep the system low-maintenance — the C1→T1/T2/T3
re-tiering, the predictive multi-resource admission model, `estimates.json`, and
the full-C1 golden-oracle apparatus — with the reasoning for each cut, are recorded
in [`deferred-and-scope.md`](./deferred-and-scope.md).
