# Polish & Scale Plan — CCR Improvement Loop

The final consolidation plan. Everything up to now proved the idea works: the
metrics are defensible ([`README.md`](./README.md), [`decisions.md`](./decisions.md)),
the fetch path is cheaper and burst-safe ([`optimize-api-call-plan.md`](./optimize-api-call-plan.md)
#1/#2/#5 shipped), and the rate-limit landscape is mapped
([`api-rate-limit.md`](./api-rate-limit.md)). This plan takes it from _"works on
one window when babysat"_ to **_"drains a `repos × months` backlog unattended,
under budget, and stays clean enough to hand off."_**

It has one **load-bearing feature** — a **backfill pacer** that spaces work under
GitHub's hourly refill — wrapped in the **polish** (code, tests, docs, dashboard)
that makes the whole package scalable and elegant rather than merely functional.

**Budget assumption.** This plan assumes the workflow repo lives (or will live) in
a **GHEC-owned org**, where the plain `GITHUB_TOKEN` gives **15,000 req/hr/repo**
with zero code or auth change (`api-rate-limit.md §6`). No custom PAT/App token is
introduced — moving the project to the correct repo _is_ the auth story. Confirm
the real number once with `gh api rate_limit` from inside the workflow. At
15,000/hr an uncapped Python month (~4,000+ invocations) fits comfortably, so
per-hour pacing — not per-window capping — is the scaling lever.

Guiding constraints (carried from prior sessions, do not violate):

- **Low maintenance over reliability theater.** Lean on the harness — `cron`
  cadence, the immutable cache, the in-process limiter — not bespoke enforcement
  code. (`design tradeoffs`, `decisions.md` cross-cutting principle.)
- **Proposal-only, deterministic-prep / agent-judgment / deterministic-math**
  stays intact (`decisions.md` D2, D9). The pacer is _orchestration around_ the
  loop; it never touches judgment or metrics.
- **Every change keeps `npm test` (vitest), `npm run typecheck`, `npm run lint`,
  `npm run format:check` green.** Scripts run via `node scripts/foo.ts` (Node ≥
  24, native TS strip). No build step.

---

## The shape of the work

Three tracks, sequenced so the pacer lands on a foundation that can actually
support it. The pacer's one real prerequisite is **cross-run cache persistence**
(so a resumed window re-processes nothing), which comes first as **Track A**; the
pacer itself is **Track B**; the surrounding **polish** is **Track C** and can
proceed in parallel.

| Track | Theme                                                    | Blocking?              |
| ----- | -------------------------------------------------------- | ---------------------- |
| **A** | Scale foundation — cross-run cache persistence           | Prereq for B           |
| **B** | The backfill pacer — spread work under the hourly refill | The centerpiece        |
| **C** | Polish — code, tests, docs, dashboard                    | Parallel, non-blocking |

---

## Track A — Scale foundation (cache persistence)

The pacer's one hard prerequisite, from
[`optimize-api-call-plan.md` §8](./optimize-api-call-plan.md) blocker #2. (Its
other historical blocker — the token ceiling — is dissolved by the GHEC move; see
the budget assumption above.)

### A1 — Persist the cache across runs (make resume real)

**Problem.** `CCR_CACHE` is `${{ github.workspace }}/.ccr-cache`
(`ccr-improvement-loop.md:62`), written **fresh each run** — no `actions/cache`
restore, no artifact download. The immutable `pr-*.json` cache makes each fetch
_idempotent within a run_, but the "resume with zero rework" benefit the pacer
leans on is only **potential** until the cache survives across runs
(`optimize-api-call-plan.md §8`, blocker #2). Until then every run re-fetches its
window from scratch — the pacer still bounds calls/hour, but a retried or resumed
window pays full price again.

**Do (pick the lower-maintenance option — `actions/cache`).**

- Extract deterministic window + cohort resolution from `prep-run.ts` into a
  standalone resolver available before cache restore. Schedule, dispatch, and
  workflow-call inputs all normalize through it, including rolling
  `today − settleDays` windows and `max_prs` default `0`/uncapped.
- Add an `actions/cache` step keyed by the immutable canonical window identity:
  restore by prefix `ccr-cache-${window_id}-` before prep, but save under a unique
  completion key only after a successful full raw-fetch. Because Actions caches are
  immutable, a mid-fetch failure must never freeze an incomplete cache under the
  final key.
- **Scope the cache key to the raw PR cache only**, not the judged/emitted
  artifacts — those are cheap to recompute and coupling them invites staleness
  bugs (mirrors the D12 "don't couple test data to live data" instinct).
- Include `RAW_SCHEMA_VERSION` in the canonical `window_id` so a fetch-schema
  change invalidates stale caches automatically instead of silently resuming on an
  incompatible shape.

**Acceptance.** Run the same `(repo, window, cohort)` twice; the second run's
prep logs show **zero** `gh api` PR fetches (all cache hits) and finishes in a
fraction of the wall-clock time. A failed-mid-fetch run does not poison the cache:
retry completes, and the following run is zero-refetch. A `RAW_SCHEMA_VERSION` bump
forces a clean re-fetch.

**Why `actions/cache` over artifact up/download.** Cache is one declarative step
pair with automatic key-based restore; artifacts need explicit
upload/download/run-id plumbing. Low maintenance wins (`design tradeoffs`).

---

## Track B — The backfill pacer (the centerpiece)

A cron-driven pacer that drains an arbitrarily large `repos × windows` backlog by
running one chunk of work per hourly tick — so no single hour exceeds the
15,000/hr ceiling and the total backlog size is **decoupled** from the per-hour
limit. This is the concrete build of `api-rate-limit.md §4` Levers 3 (spread) + 4
(orchestrate) and `optimize-api-call-plan.md §8`.

**The core insight — the cron cadence _is_ the pacer.** No always-on process, no
in-job `sleep` burning runner minutes, no in-run waiting. An hourly `cron` fires,
checks the budget, runs what fits, and exits. GitHub's own scheduler is the clock.
This is the elegant, low-maintenance shape the user asked for.

**Token-free by construction.** The pacer does **not** dispatch a separate
workflow (which would hit GitHub's "the default `GITHUB_TOKEN` can't trigger
another workflow" recursion guard and force a PAT). Instead, the loop is exposed
as a **reusable workflow** (`on: workflow_call`) and the pacer **`uses:`** it as a
job in its own cron-triggered run. A reusable-workflow call is not an event
trigger, so it needs no PAT/App token; the plain `GITHUB_TOKEN` (15,000/hr on
GHEC) does the API work, and only the ledger job requests repo-wide
`contents: write` to update the protected state branch. One tick = one run that
processes its admitted window(s) inline via the reusable loop.

### B1 — Backlog model (derived, over a durable canonical ledger)

The backlog is **computed each tick** from config + today's date + a **durable
completion ledger** — not a hand-seeded, status-tracked file. This deletes both
manual steps (seeding, committing status flips) while keeping completion state
durable, canonical, and atomic (the three properties a review flagged as
mandatory for a derived model to be sound).

- **Config, not a job list.** A tiny committed `pacer/config.json`:
  `{ repos, start_date, granularity, max_prs? }` (`granularity ∈
month | biweekly | week`, monthly default). The only human-authored input.
- **Canonical window identity.** Every window has a stable
  `window_id = ${owner}_${repo}__${start}__${end}__${cohort}__raw<RAW_SCHEMA_VERSION>`
  (`cohort = uncapped | cap-<N>`), and `emit-run-json` sets **`run.id = window_id`**
  end-to-end, making the file `run-<window_id>.json`. Existing dedupe logic keeps
  keying on `run.id`, so this is a data migration: bump `SCHEMA_VERSION`, recompute
  or migrate existing `run-*.json`, and test `aggregate-runs.ts`,
  `dashboard/js/aggregate.mjs`, the dashboard manifest, and
  `ccr-dashboard-publish.yml` copy-by-basename behavior. The semantics
  deliberately change: a capped run no longer supersedes an uncapped run of the
  same dates. One encoding is reused for run records, ledger records, skips, and
  cache keys.
- **Desired windows = settled only.** `scripts/gen-backlog.ts` expands
  `start_date … (today − settleDays)` at the granularity into candidate windows,
  emitting **only closed, settled** windows (`window_end ≤ today − settleDays`) —
  never a trailing partial/unsettled window (matches the child's rolling
  methodology; a daily re-clamp to "today" would re-mine unsettled PRs). Monthly
  default; biweekly = fixed 14-day windows anchored on `start_date`; weekly for
  more trend points. `max_prs` defaults `0`/uncapped.
- **Durable store = a `ccr-pacer-state` branch.** One orphan branch holds
  `ledger/<window_id>.json` (one typed record per terminal window),
  `skipped.json` (poison windows), and `estimates.json` (actual cost/cardinality
  observations from completed children). The pacer reads it (`git fetch`), creates
  the empty orphan branch if absent, and only the ledger job writes updated records
  with the plain `GITHUB_TOKEN`. Its `contents: write` permission is repo-wide, not
  branch-scoped, so non-state branches are protected by a branch ruleset and the
  script uses normal non-force fetch/rebase/retry pushes only to `ccr-pacer-state`.
  A `GITHUB_TOKEN` push does not trigger another push-triggered workflow. Records
  are keyed by `window_id`, so writes are idempotent and collision-free — **not**
  read from the checkout (the child uploads `run-*.json` and outcome JSON as
  artifacts, and reusable-workflow jobs don't share a workspace).
- **Pending = desired − ledger-done − skipped**, matched by `window_id`. A
  re-derived-but-done window is skipped by the ledger (or, if re-run, is all cache
  hits via A1 + near-zero REST via C1).

### B2 — The pacer loop (`ccr-pacer.yml`)

`ccr-pacer.yml` is a **hand-written, plain, deterministic GitHub Actions YAML**
workflow: no agent execution, no gh-aw compilation, and **no second lock file**.
`ccr-improvement-loop` remains the only gh-aw workflow with a `.lock.yml`. The
pacer uses `on: schedule: cron (hourly)` + `workflow_dispatch`, **serialized** with
`concurrency: { group: ccr-pacer, cancel-in-progress: false }` so two ticks never
race. Each tick:

1. **Read budget** — `gh api rate_limit`, take remaining across the resources the
   fetch actually uses **after C1**: `graphql` points (the new binding constraint),
   `core` REST (now near-zero use), and `search` (30/min). Admission is
   multi-resource, not REST-only. (Secondary/burst limits stay handled in-process
   by the #5 `Semaphore`, which the pacer cannot see or pace.)
2. **Estimate, derive & admit** — derive `pending = settled-desired − ledger-done −
skipped` by `window_id`, then count-probe only as Search remaining allows. Each
   admitted window reserves **both** its count probe and later PR-list Search.
   `estimates.json` is updated from completed children's outcome artifacts with
   actual GraphQL `rateLimit.cost`, search/core use, and per-connection
   cardinalities; on cold start, use a conservative upper bound that covers
   high-comment / low-PR windows rather than trusting PR count alone. Admit windows
   whose multi-resource estimate fits `remaining − safetyMargin`; a single
   over-budget window is split/deferred, never silently admitted. Preserve **≤ 1
   concurrent window per repo**, but do not equate that with ≤1 per tick: when
   budget and runner runtime permit, same-repo windows may be admitted as a serial
   lane inside the same serialized tick, or the plan must meet a documented maximum
   drain-time threshold.
3. **Run the window inline** — invoke the reusable loop for admitted windows (no
   cross-workflow dispatch, no PAT; `uses:` the **compiled `.lock.yml`**):
   ```yaml
   jobs:
     backfill:
       strategy:
         matrix: { window: ${{ fromJSON(needs.plan.outputs.admitted) }} }
       uses: ./.github/workflows/ccr-improvement-loop.lock.yml
       with:
         repo: ${{ matrix.window.repo }}
         window_start: ${{ matrix.window.window_start }}
         window_end: ${{ matrix.window.window_end }}
         max_prs: ${{ matrix.window.max_prs }}
   ```
   The child writes a deterministic **outcome artifact** named
   `outcome-<window_id>.json` via safe-output/upload. `produced` is declared only
   after the run JSON artifact upload is confirmed; upload/safe-output failure is
   `failed`. `artifact_name` is derived from `window_id`, not unstable upload temp
   IDs.
4. **Ledger the outcome** — because matrix reusable-workflow outputs do not
   aggregate reliably, the `ledger` job runs with `if: always()`, downloads all
   `outcome-<window_id>.json` artifacts from the tick, and reconciles every
   admitted window. Any admitted window with no outcome artifact is treated as
   `failed`. It writes/updates `ledger/<window_id>.json` and `estimates.json` on
   the `ccr-pacer-state` branch, then fetches/rebases/retries a normal non-force
   `GITHUB_TOKEN` push.
5. **Exit.** Next hour, `cron` re-derives and drains the next chunk.

**Overlap safety comes from pacer serialization + explicit per-repo lanes.** The
pacer's own `concurrency: ccr-pacer` serializes ticks, and because a tick runs its
admitted work to completion (the `ledger` job waits with `if: always()`), the next
tick can't start until this one exits — so a running window is never re-admitted,
no lease required. The child's `group: ccr-loop-${repo}` (1 running + 1 pending) is
_not_ a queue, so the pacer must not rely on it for throughput; same-repo
throughput is either serial inside one tick or bounded by an explicit drain-time
acceptance threshold.

### B3 — Self-healing & termination

- **Resume for free** — the backlog is re-derived every tick from today's date +
  config + the durable ledger, and A1 persists the cache, so a pacer restart just
  recomputes `pending`; a done window is skipped by its `window_id` ledger record
  (or, if re-run, is all cache hits).
- **No-op tolerance** — the child returns `thin-noop` (`< minPrs`) or `signal-noop`
  (≥ `minPrs`, agent called `noop`) as an explicit typed outcome; the pacer
  ledgers it as done (not failed) so thin windows never wedge or re-derive.
- **Termination** — when derived `pending` is empty (all settled windows ledgered
  done or skipped), the tick is a no-op; the hourly cron idles once caught up.
- **Failure isolation** — a `failed` outcome is _not_ done: the ledger keeps a
  durable `attempt` count; reconciliation retries **once**, then moves the window to
  `skipped.json` with a logged reason (retry cap = 1, no poison loop). A missing
  outcome artifact for an admitted window is reconciled as `failed` and retried;
  only confirmed run-artifact upload can produce `produced`.

### B4 — What the pacer must **not** do (scope guards)

- **No in-job `sleep` to pace** — spacing comes from `cron` cadence, not burned
  runner minutes (`optimize-api-call-plan.md §8`, blocker #3).
- **No parallel searches** — the Search API is 30 req/min; the per-window PR
  listing is one search, never parallelize (`api-rate-limit.md §6`).
- **No judgment / metric logic** — the pacer only admits and runs windows; all
  measurement stays in the reusable loop (D2 separation preserved).

**Acceptance.** A 7-month × 1-repo campaign (`pacer/config.json`, `start_date` 7
months back) drains within the documented maximum drain-time threshold on the
15,000/hr GHEC token with **no 403** and **no PAT**, each window's typed outcome
recorded as a `ledger/<window_id>.json` on the `ccr-pacer-state` branch (thin/no-op
windows recorded `thin-noop`/`signal-noop`, not failed), and the derived `pending`
set ending **empty**. A mid-campaign restart re-derives and resumes with zero
rework; re-running after completion is a clean no-op.

---

## Track C — Polish

Makes the package elegant, cheap, and handoff-ready. **C1 is sequenced _before_
the pacer** (not parallel): it changes which resource is the binding constraint
(REST → GraphQL points + git bandwidth), so the pacer's admission model must be
built against the post-C1 cost, not a REST ceiling C1 has vacated. C2–C4 remain
parallel/non-blocking.

### C1 — API-call optimization program (fold in all deferred items)

An agent implements this, so **implementation effort is not a reason to defer
anything** — the earlier "deferred / high-effort / partial-win" labels in
[`optimize-api-call-plan.md`](./optimize-api-call-plan.md) (#3, #4, #6, #7) are
dropped. The **only** gate is information-preservation: every optimization must
produce **byte-identical _normalized downstream evidence_** (the fields consumers
read — `committedAt` ordering, `files`/`distinctFiles`, and the patch text feeding
`ccrSawCode`/diff-detectability), proven by a golden test _written and passing
before_ the optimization is wired in. Note the bar is the **normalized evidence
contract, not raw REST payload bytes** — git cannot reproduce REST's exact patch
serialization (headers, binary-omit, truncation), and volatile fields
(`generatedAt`, wall-clock) are frozen/stripped before compare. If a change cannot
be shown equivalent at this contract, it does not ship — full stop.

The pacer bounds cost _per hour_; this program cuts cost _per PR_ toward **near
zero on the rate-limited path**, so an uncapped month fits a single tick with
headroom. Implement in this order (each step's equivalence test is its
precondition):

**Step 0 — Make the request layer injectable (enabling refactor).** Pass a
`fetchFn`/`gh` client into `fetchPrToCache` (defaulting to the real one) so tests
can substitute a mock recording call count, args, and concurrency
(`optimize-api-call-plan.md`, "Prerequisite for testability"). Behavior-preserving;
unlocks every equivalence test below.

**Step 1 — #6 local-git commit detail (removes the dominant `N` term).** The
per-commit `GET /commits/{sha}` loop is the multiplier. Every field it writes —
`sha`, `committedAt`, `files`, `patches` — is a native git concept
(`optimize-api-call-plan.md §6`), so source them from a local clone at **zero
GitHub API cost** (git-transport budget is a separate, generous pool):

- `committedAt` ← `git log --format=%cI <sha>`, **offset-normalized** to match the
  current `c.commit.committer?.date` REST representation.
- `files` ← `git diff-tree` with rename detection (`-M`); for **merge** commits use
  an explicit `-m --first-parent` rule (a bare `diff-tree -r` emits nothing for
  merges), and handle **root** commits (diff vs. the empty tree).
- `patches` ← `git show <sha>` **normalized** to GitHub's per-file `patch` shape:
  strip commit headers, split per file, and reproduce GitHub's binary-omit /
  oversized-truncation behavior.
- Use a **blobless partial clone** (`git clone --filter=blob:none`) of the
  **target** repo, fetching `refs/pull/<n>/head` (and `.../merge` where relevant)
  or targeted `git fetch origin <sha>` for exactly the mined commits — **fork-PR
  commits are not reachable from the base branch**, so never assume default-branch
  reachability, and never full-clone up front. Add the clone as a prep step keyed
  off `TARGET_REPO`, and **cache the target git object store across ticks**
  (separate from the raw PR cache). **Benchmark a representative month first** — a
  blobless clone lazily refetches blobs on `git show`, which at
  `azure-sdk-for-python` scale can be many large fetches; record the decision.
- **Equivalence gate:** a golden test runs `attribute-comments` +
  `build-judge-input` over fixtures covering **merge, rename, binary, oversized,
  root, and fork-PR** commits and asserts the **normalized downstream evidence**
  (`files`, `distinctFiles`, `committedAt` ordering, `ccrSawCode`) is
  byte-identical REST-derived vs git-derived — **not** raw REST payload bytes
  (git can't reproduce those). Ship only when green.

Effect: per-PR API cost `6 + N` → **`6`**. This **supersedes #3** (selective
commit-detail): #6 removes `N` rather than shrinking it, and dissolves #3's
`distinctFiles`-coupling blocker because git yields every commit's files for free,
so `distinctFiles` keeps its exact current contract with no API calls.

**Step 2 — #4 + #7 move the fixed `6` calls onto GraphQL (off the REST ceiling).**
The five fixed REST reads (`pulls/{n}`, reviews, PR comments, issue comments,
commits list) plus thread resolution collapse into GraphQL queries on the
**separate point-metered pool**, and **#7 aliasing** batches multiple PRs into one
HTTP request — cutting in-flight count (direct secondary-limit relief) as well as
primary-REST load:

- Reviews, inline comments (+ thread `isResolved`), issue comments, commit shas,
  and linked issues all come back in one aliased query per batch of PRs.
- **Pagination is per-alias, per-connection — not one batch-wide loop.** Every PR
  alias and every nested connection (reviews, inline comments, issue comments,
  commit shas, threads@100, linked issues/labels@20) carries its **own cursor**;
  cursor-loop each to exhaustion with deterministic ordering matching REST — no
  silent truncation. Handle **partial GraphQL responses** (data + errors)
  explicitly, inspect `rateLimit { cost remaining }`, and **split an over-wide
  batch** before it trips the point/CPU guard.
- **Equivalence gate:** a golden test compares the assembled per-PR record from
  the GraphQL/batched path against the current REST path over fixtures that
  **exceed one page on _every_ connection** (reviews, inline, issue comments,
  commits, threads, linked issues, labels — not just threads/comments); assert
  byte-identical normalized output. Ship only when green.
- Batch width is tuned and still admitted through `ghRequestLimiter` (#5), so an
  over-wide batch can't exhaust the GraphQL point pool or trip its CPU-cost guard.
- Commit **patch text** stays out of GraphQL (it isn't cleanly exposed) — but
  Step 1 already moved patches to git, so nothing regresses here.

Effect: the fixed `6` leaves the primary-REST budget for the GraphQL pool, in
fewer HTTP requests. Composed with Step 1, per-PR **primary-REST** consumption
drops toward **near zero** — the qualitative shift beyond the shipped #1/#2/#5.

**Net after C1:** per-PR primary-REST cost ≈ **0** (patches on git, fixed reads on
the GraphQL pool), **normalized downstream evidence** identical to today, each step
locked by a fixture-driven golden test (`tests/`, vitest, no live `gh`). The GHEC
hourly budget then effectively bounds only GraphQL points + git bandwidth, not the
REST ceiling that historically failed runs — which is exactly why the pacer's
admission (Track B) meters those resources, not REST.

### C2 — Preflight budget guard in the child (defense in depth)

Add an **optional** `gh api rate_limit` preflight at the top of `prep-run.ts`
(behind a `--check-budget` flag, default on in CI): if the **metered resources the
fetch actually uses after C1** (GraphQL points, plus search) are below the
estimated window cost, **abort early with a clear message and non-zero exit**
rather than dying mid-fetch with a partial cache. Meters the same resources as the
pacer's admission (not REST-only, which C1 has vacated). Complements — does not
replace — the pacer (spaces at admission time) and the #5 limiter (secondary
limits).

### C3 — Dashboard scale & polish

The static, zero-backend, manifest-driven dashboard (D11) already pools slices
(handoff §C). Scale-oriented polish:

- **Auto-manifest for backfill volume.** A one-liner
  `scripts/gen-manifest.ts` that regenerates `dashboard/data/manifest.json` from
  the `run-*.json` files present, so a 50-window backfill doesn't require
  hand-editing the manifest (the browser can't glob — D11). Keep it a build-free
  local script, not a runtime dependency.
- **PR-size bucket dimension (S/M/L/XL)** — the one slice the user asked about
  that isn't yet possible (`handoff.md` candidate next steps). `additions` /
  `deletions` are already captured per PR; add a `sizeBucket` slice dimension
  through schema + `compute-metrics.ts` + a dashboard chart. Requires a history
  re-migration (like the `ccrRecallRate` migration in handoff §B) — do it as one
  deterministic recompute over existing raw fields, **no agent re-run**.
- **Repo comparison affordance** — the filename/manifest already carry
  `owner_repo`; ensure trend charts can filter/facet by repo so a multi-repo
  backfill reads cleanly (never compare repos on raw absolute burden — trend
  within a repo only, `README.md` reading rules).

### C4 — Code, test, and doc hygiene

- **Cost-model regression test.** Lock the post-C1 invariant with a mock `gh`
  recording call count/args: **zero** `commits/{sha}` REST calls (patches now come
  from git) and the fixed reads issued as batched GraphQL, not per-PR REST. A
  future change that reintroduces a per-commit or per-PR REST call fails CI. This
  is the guard that keeps the pacer's cost estimate honest.
- **Pacer dry-run mode** (`--dry-run`) — print the admission plan (jobs + estimated
  cost/tick) without running anything, for safe review before a big campaign.
- **Docs convergence.** Fold the shipped pieces of this plan into the existing
  docs rather than letting four rate-limit docs drift: `api-rate-limit.md §7`
  recommended path → "pacer shipped"; `optimize-api-call-plan.md §8` status → ✅;
  a short **"Backfilling at scale"** section in `README.md` pointing at the pacer.
  Add a `decisions.md` **D15 — backfill pacer** entry (decision / why / rejected
  alternatives [always-on runner, in-job sleep, cross-workflow dispatch requiring a
  PAT, per-run cap only, **committed status-tracked backlog** — replaced by a
  derived backlog computed from config + today + a durable canonical-`window_id`
  ledger on the `ccr-pacer-state` branch, with typed child outcomes] /
  consequences) and a **D16 — API cost reduced to near-zero REST (git patches +
  GraphQL fixed reads), gated on normalized-evidence equivalence (not raw bytes)**
  entry, both matching the existing D-series format.
- **`handoff.md` update** — new session entry summarizing pacer + Track A/C
  outcomes (and the reusable-workflow / GHEC-budget assumptions), git state, and
  validation table, matching the existing format.

---

## Sequencing & milestones

Reordered after review so the pacer is built against the real post-optimization
cost model (C1 lands before B):

```
Phase 0 (determinism + canonical window_id)
  ─▶ A1 (resolver + cross-run cache, keyed by run.id/window_id)
  ─▶ C1 (Step 0 inject ─▶ #6 git ─▶ #4/#7 GraphQL, each equivalence-gated)
  ─▶ B1 (derived backlog + durable ccr-pacer-state ledger + typed child outcomes)
  ─▶ B2/B3 (plain-YAML pacer: artifacts, multi-resource admission, ≤1 concurrent/repo, serialized) ─▶ B4 guards
  ─▶ ✅ unattended backfill
C2 (preflight) ─ C3 (dashboard) ─ C4 (hygiene)   ── parallel, merge continuously
```

1. **Milestone 1 — Foundation + near-zero REST.** Phase 0 (frozen clock, canonical
   `run.id = window_id` and data migration), A1 (resolver + cross-run cache), and
   C1 (Step 0 → #6 → #4/#7) land; `ccr-improvement-loop` lock recompiled. _Proof:_
   each C1 step's golden test asserts **byte-identical
   normalized evidence** over merge/rename/binary/root/fork-PR fixtures; the
   cost-model regression test asserts **zero** per-PR REST; a repeated window is all
   cache hits. _Deferred (GHEC):_ uncapped month, no 403.
2. **Milestone 2 — Reusable loop + derived backlog.** B1: durable
   `ccr-pacer-state` ledger, settled-only `gen-backlog`, derive-pending by
   `window_id`. _Proof:_ typed-outcome artifact + derive-pending tests;
   settled-only expansion excludes unsettled trailing windows.
3. **Milestone 3 — Pacer.** B2/B3/B4: plain YAML pacer, outcome-artifact ledgering,
   multi-resource admission, ≤1 concurrent/repo, serialized ticks, reconciliation,
   and drain-time threshold. _Proof (offline):_ a workflow-boundary test with two
   successes + one failing leg ledgers all outcomes; the two-tick integration test
   (tick 1 records outcomes → tick 2 derives the remainder), retry-cap, and
   concurrency/throughput tests pass; a poison/thin window doesn't wedge; a restart
   resumes cleanly.
4. **Milestone 4 — Scale polish.** C2 (multi-resource preflight), C3 (manifest
   auto-gen, `sizeBucket`, repo faceting), C4 (docs converged). _Proof:_ a
   50-window backfill needs no manual manifest edits.

---

## Definition of done

- A `repos × months` backfill drains **unattended** under `cron`, never exceeding
  the metered ceilings, with **zero rework** on resume — completion state durable
  in the `ccr-pacer-state` ledger, matched by canonical `run.id`/`window_id`, and
  same-repo throughput either uses serial lanes or meets the documented maximum
  drain-time threshold.
- Backlog windows are **settled-only** (end ≤ today − settleDays); no unsettled
  trailing window is ever mined or ledgered.
- Per-PR **primary-REST cost is ~0** (commit patches from local git, fixed reads
  batched onto the GraphQL pool), with the **normalized downstream evidence
  identical** to the pre-optimization output (not raw REST bytes) — proven by a
  golden test per C1 step over merge/rename/binary/root/fork-PR fixtures.
- Admission meters the **real post-C1 constraints** (GraphQL points, bounded Search
  count/list calls, high-cardinality GraphQL pagination, and concurrency), not the
  vacated REST ceiling or a PR-count-only proxy.
- The child loop is unchanged in what it _measures_ — proposal-only,
  deterministic-prep / agent-judgment / deterministic-math intact (D2, D9); the
  pacer is the only durable writer. Its ledger job has repo-wide `contents: write`,
  protected by branch rulesets and scripted to write only the state branch.
- Dashboard ingests a large backfill without hand-editing, facets by repo, and
  reads honestly (`n/a` over false precision).
- All gates green: `pnpm test`, `pnpm run typecheck`, `pnpm run lint`,
  `pnpm run format:check`; `ccr-improvement-loop.lock.yml` recompiled and
  committed; `ccr-pacer.yml` is plain YAML (no lock); docs converged; `D15`,
  `D16`, and a `handoff.md` entry recorded.

## Explicit non-goals

- **No information loss for API savings.** Every call-reduction ships only after a
  golden test proves the **normalized downstream evidence** unchanged; a change
  that can't be proven equivalent at that contract is rejected regardless of budget
  saved.
- **No always-on orchestrator / external scheduler.** The `cron` cadence is the
  pacer (`design tradeoffs` — low maintenance).
- **No custom PAT / GitHub App token.** The GHEC `GITHUB_TOKEN` (15,000/hr) plus a
  reusable-workflow (`workflow_call`) invocation cover both budget and triggering —
  no elevated credential is introduced.
- **No second gh-aw workflow / lock for the pacer.** `ccr-pacer.yml` is plain,
  deterministic Actions YAML; only `ccr-improvement-loop` is compiled to a lock.
- **No branch-scoped write-token claim.** `contents: write` is repo-wide; safety
  comes from branch rulesets plus normal non-force state-branch pushes, not YAML
  token scoping.
- **No new reliability/enforcement layer.** Lean on `concurrency`, the immutable
  cache, branch protection, and the `Semaphore`; add code only where the harness
  genuinely can't cover the gap.
- **No metric or judgment changes** driven by scale — the pacer is orchestration
  only. Metric evolution (e.g. reframing the headline) stays a separate, deliberate
  decision (`handoff.md`).
- **No importing/wrapping of prior TS/Python implementations** — this package
  stands on its own (`project scope`).
