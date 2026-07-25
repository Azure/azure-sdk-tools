# Execution Log — CCR Improvement Loop Polish & Scale

Chronological record of all actions taken while implementing
[`implementation-plan.md`](./implementation-plan.md). Every future change to this
build is appended here (newest phase at the bottom of each section).

Conventions:

- **Gate** = universal gate `pnpm run typecheck && pnpm run lint && pnpm run format:check && pnpm test`
  (vitest) **plus** the dashboard node tests `node --test dashboard/js/aggregate.test.mjs`
  (not covered by `pnpm test`).
- ✅ = verified green in this session. Live/GHEC-token proofs are DEFERRED and not gating.

---

## Setup — preserve current version, implement on a copy

User directive: copy the existing `ccr-improvement-loop/` dir, preserve the
current version, implement the new plan on the copy. Chosen layout (confirmed via
prompt): **canonical** — new implementation lives at `ccr-improvement-loop/`; the
preserved snapshot takes the `-legacy` name; workflow files copied physically;
scope **incremental** (Phase 0 then check in).

Actions:

- `rsync -a --exclude node_modules --exclude coverage ccr-improvement-loop/ ccr-improvement-loop-legacy/`
  — froze a full snapshot of the current dir.
- Copied the 4 external workflow files into
  `ccr-improvement-loop-legacy/workflows-snapshot/`:
  `ccr-improvement-loop.md`, `ccr-improvement-loop.lock.yml`,
  `ccr-dashboard-publish.yml`, `ccr-dashboard-pages.yml`.
- All subsequent edits are made in the canonical `ccr-improvement-loop/` (and the
  external `.github/workflows/*` files); `ccr-improvement-loop-legacy/` is never
  touched again.

Environment: Node v25, pnpm 10.12.4, `node_modules` already present.

---

## Phase 0 — Baseline, determinism, and canonical identity ✅ Gate 0 green

### 0.1 Baseline

- Ran the gate. `typecheck`, `lint`, `test` (97) were green; `format:check` was
  **red** on pre-existing files (planning `.md` docs + `tests/utils.test.ts`).
- `pnpm run format` (prettier `--write`) to normalize → gate green. Established
  the green starting point.

### 0.2 Freeze the clock + `canonicalizeRun`

- `scripts/emit-run-json.ts`:
  - Added `canonicalizeRun(run)` + `CANONICAL_GENERATED_AT` sentinel — strips the
    sole volatile field (`generatedAt`) for deterministic golden/equality asserts.
  - Added `resolveGeneratedAt(env)` — emit timestamp injectable via
    `CCR_GENERATED_AT` (defaults to wall-clock); `main` now uses it instead of
    `new Date().toISOString()`.

### 0.3 Canonical window identity (the completion key)

- **New `scripts/window-id.ts`:** `computeWindowId(...)` →
  `${owner}_${repo}__${windowStart}__${windowEnd}__${cohort}__raw${RAW_SCHEMA_VERSION}`;
  plus `cohortOf(maxPrs)` (`0`/neg/NaN → `uncapped`, else `cap-<N>`),
  `ownerRepoOf`, `runArtifactBase(id)` (`run-<id>`), and the `Cohort` type.
- `scripts/emit-run-json.ts`: `run.id = computeWindowId(...)` (via reworked
  `runIdOf(meta)`); artifact filename via `runArtifactBase`; `ownerRepoOf`
  re-exported from `window-id`; `RunMetaInput` gained optional `cohort`
  (default `uncapped`, not persisted as its own run-meta field).
- `scripts/run-schema.ts`: `SCHEMA_VERSION` 1.0 → **1.1** (forces migration of
  old-format ids); `RAW_SCHEMA_VERSION` stays 1.0. Documented the id change.
- `scripts/prep-run.ts`: computes `cohort` from `--max-prs`; `buildMeta` emits
  `cohort` into `meta.json` so emit reads it.
- `dashboard/js/aggregate.mjs`: `SCHEMA_VERSION` literal 1.0 → 1.1 (+ doc fix).
- **Migration** — new `scripts/migrate-run-schema.ts`: recomputes `run.id`
  (historical files → `uncapped` cohort), bumps `schemaVersion`, renames to
  `run-<id>.json`, revalidates via `parseRun`, regenerates `manifest.json`. Ran
  over `dashboard/data` (14 files), `dashboard/js/fixtures` (6 files), and
  `tests/fixtures/run-sample.json` (in place). Verified: 14 scanned / 0 skipped
  aggregate; manifests consistent; no stale date-first filenames remain.
- `.github/workflows/ccr-dashboard-publish.yml`: added a comment documenting that
  the `run-*.json` glob + `^run-.*\.json$` manifest regex still match windowId
  basenames (behavior already correct — no logic change).

### 0.4 Golden oracle fixture (scoped to #4)

- **New `scripts/pr-evidence.ts`:** `extractPrEvidence(data)` → deterministically
  ordered per-PR normalized record for the five fixed reads + thread
  `threadResolved` + linked issues/labels + commit `committedAt` order. This is
  the Phase 2 REST→GraphQL equivalence target.
- Committed snapshots: `tests/fixtures/oracle/pr-100.evidence.json` (from
  `extractPrEvidence`) and `tests/fixtures/oracle/pr-100.judge-input.json`
  (produced by the real classify→filter→attribute→build-judge-input pipeline).

### 0.5 Tests (new + updated)

- **New `tests/window-id.test.ts`:** `computeWindowId` changes on
  repo/start/end/cohort/rawSchema, stable otherwise; malformed repo throws;
  `cohortOf` mapping; `runArtifactBase` filename matches the dashboard regex.
- **New `tests/pr-evidence.test.ts`:** evidence matches committed snapshot +
  source-order-independent; judge-input over the real pipeline matches snapshot.
- `tests/emit-run-json.test.ts`: run.id = canonical identity; capped vs uncapped
  distinct id; determinism via `canonicalizeRun`.
- `tests/aggregate-runs.test.ts` + `dashboard/js/aggregate.test.mjs`: cohort-only
  difference → distinct id → both survive `dedupeRuns`.
- Version/id fixes: `run-schema.test.ts` (new sample id), `end-to-end.test.ts`
  (schemaVersion 1.1), `prep-run.test.ts` (`buildMeta` cohort),
  `aggregate.test.mjs` (`isValidRun` valid case → 1.1).

### ✅ Gate 0 result

- `typecheck` ✅ · `lint` ✅ · `format:check` ✅ · `vitest` **113** ✅ ·
  dashboard `node --test` **12** ✅.
- Determinism, identity (incl. cohort supersede semantics), dashboard/manifest
  parse-copy, and the committed #4 oracle are all covered and green.
- No lock recompile needed (Phase 0 did not change `ccr-improvement-loop.md`).
- _DEFERRED — needs GHEC token:_ live no-403 proofs (not gating).

### Files touched (Phase 0)

- New: `scripts/window-id.ts`, `scripts/migrate-run-schema.ts`,
  `scripts/pr-evidence.ts`, `tests/window-id.test.ts`, `tests/pr-evidence.test.ts`,
  `tests/fixtures/oracle/pr-100.evidence.json`,
  `tests/fixtures/oracle/pr-100.judge-input.json`.
- Modified: `scripts/emit-run-json.ts`, `scripts/run-schema.ts`,
  `scripts/prep-run.ts`, `dashboard/js/aggregate.mjs`,
  `dashboard/js/aggregate.test.mjs`, `tests/emit-run-json.test.ts`,
  `tests/aggregate-runs.test.ts`, `tests/run-schema.test.ts`,
  `tests/end-to-end.test.ts`, `tests/prep-run.test.ts`,
  `.github/workflows/ccr-dashboard-publish.yml`.
- Migrated (renamed + bumped): 14 `dashboard/data/run-*.json`, 6
  `dashboard/js/fixtures/run-*.json`, their `manifest.json`s, and
  `tests/fixtures/run-sample.json`.

## Phase 1 — Track A: cross-run cache persistence (A1)

**Goal.** Make "resume with zero rework" real: a canonical cache key derived from
the Phase 0 window identity, known **before** prep, so `actions/cache` can restore
the raw PR cache and skip re-fetching an already-mined window.

### What changed

1. **Standalone window/cohort resolver** — new `scripts/resolve-window.ts`.
   - Pure `resolveWindow()` normalizes raw trigger inputs (schedule empty-string
     env, `workflow_dispatch`, `workflow_call`) into one canonical
     `{ windowStart, windowEnd, cohort, maxPrs, rawSchemaVersion, windowId }`.
     Blank bounds → rolling `today − settleDays` window; `max_prs` blank/`0`/`<0`
     → uncapped cohort. Clock injectable via `now`.
   - CLI prints the resolved record as JSON (the workflow reads it to key cache).
   - `prep-run.ts` now **consumes** the resolver instead of owning identity
     resolution (removed the inline window math + `DAY_MS`; dropped the
     `cohortOf` import). prep and the cache key can no longer disagree.
2. **Workflow (`ccr-improvement-loop.md`)** — added, before prep:
   - `Resolve window identity` step (`id: window`) → surfaces `window_id` and
     `raw_schema_version` as step outputs via `resolve-window.ts`.
   - `Restore PR cache` (`actions/cache/restore@v5`): `path: .ccr-cache/pr-*.json`
     only, `restore-keys: ccr-cache-${window_id}-`.
   - `Save PR cache` (`actions/cache/save@v5`, `if: success()`): unique completion
     key `ccr-cache-${window_id}-${run_id}-${run_attempt}`, so a mid-fetch failure
     never saves a poisonable key. Saves **only** `.ccr-cache/pr-*.json` (never
     judged/emitted artifacts — D12).
3. **Mock-gh recorder** — new `tests/helpers/mock-gh.ts`. A throwaway executable
   `gh` on a temp `PATH` that records every argv and serves fixture/synthetic REST
   - GraphQL responses. Reused by Gate 1 and (later) Phases 2 & 4 for request-count
     deltas. Supports fixture overrides and forced per-endpoint failures.
4. **Recompiled** `ccr-improvement-loop.lock.yml` via `gh aw compile` (v0.80.9,
   matches the embedded compiler version). Source + lock committed together.

### ✅ Gate 1 result

- **Resolver tests** (`tests/resolve-window.test.ts`, 8): schedule/dispatch/
  workflow_call shapes → identical `windowId`; rolling = `today − settleDays`,
  start = end − windowDays; `max_prs` blank/`0`/`<0` → uncapped; raw-schema /
  cohort / repo / bound changes each flip the `windowId`.
- **Cache tests** (`tests/cache-persistence.test.ts`, 2): zero-refetch (warm cache
  → 0 `gh api` PR fetches); poison-cache (forced mid-fetch failure writes no
  `pr-<n>.json`; retry completes; later run is all hits).
- Universal gate: `typecheck` ✅ · `lint` ✅ · `format:check` ✅ · `vitest`
  **123** ✅ (was 113; +10) · dashboard `node --test` **12** ✅.
- Lock recompiled (0 errors / 0 warnings) and committed with source.
- _DEFERRED — needs GHEC token:_ live one-uncapped-month no-403 + repeated-window
  all-hits proof (not gating).

### Files touched (Phase 1)

- New: `scripts/resolve-window.ts`, `tests/helpers/mock-gh.ts`,
  `tests/resolve-window.test.ts`, `tests/cache-persistence.test.ts`.
- Modified: `scripts/prep-run.ts` (consume resolver),
  `.github/workflows/ccr-improvement-loop.md` (window + cache steps),
  `.github/workflows/ccr-improvement-loop.lock.yml` (recompiled).

---

## Phase 2 — Injectable request layer + per-PR GraphQL fetch (API-call reduction)

Plan: implementation-plan.md Phase 2 (Step 2.0 client seam, Step 2.1 #4 GraphQL
migration). Goal: cut the per-PR REST fan-out (5 fixed reads + N commit reads)
down to **one GraphQL query per PR** (+ the unavoidable N commit-detail reads),
without changing a single byte of the assembled `pr-<n>.json` payload.

### Step 2.0 — Injectable `GhClient` seam

1. **New `scripts/gh-client.ts`** — the raw-fetch request layer.
   - `GhClient { restJson, graphql }` interface; `defaultGhClient` delegates to
     the real `ghApiJsonAsync` / `ghApiGraphqlAsync` spawners.
   - `makeRecordingClient({ rest?, graphql? })` → a `RecordingClient` that records
     every call (`kind`, endpoint, graphql fields), reports `restCount` /
     `graphqlCount`, and tracks `peakConcurrency` (so the micro-batch seam is
     observable). Test responders serve fixtures without spawning a real `gh`.
2. **`fetch-prs.ts` refactor** — threaded a `client: GhClient` param through
   `fetchPrToCache` (default `defaultGhClient`), `assembleViaRest`,
   `fetchThreadResolution`, and `fetchCommitDetail`. Extracted `buildPrPayload`
   (the single REST→payload normalizer) and exported it plus the `Raw*` request
   shapes and `assembleViaRest` so the GraphQL path can reuse them verbatim.

### ✅ Gate 2.0 result

- `tests/pr-assemble.test.ts` (3): a `RecordingClient` observes exactly the
  legacy REST shape — 5 fixed REST reads + 1 thread-resolution GraphQL + N
  per-commit reads; the assembled record is byte-identical to the reference; and
  `peakConcurrency > 1` proves the bounded-parallel seam still fans out.

### Step 2.1 — Per-PR GraphQL assembler (#4)

1. **New `scripts/pr-graphql.ts`** — `collectGraphqlPr(client, repo, number)` runs
   **one** base GraphQL query per PR (`# op:base`) selecting reviews, comments,
   commits (oids only), review threads, labels, and closing issues, then
   per-connection cursor-paginates any that overflow one 100-node page
   (`connectionQuery` focused ops + `paginateConnection`; a >100-comment thread
   paginates via `node(id:"thread:<idx>")` + `THREAD_COMMENTS_QUERY`). Partial
   responses are tolerated (errors collected into diagnostics) and `rateLimit.cost`
   is summed for observability. GraphQL nodes are converted **back into the same
   `Raw*` shapes** and fed through the **same `buildPrPayload`**, so equivalence is
   structural, not coincidental. `assembleViaGraphql` is now the production default
   inside `fetchPrToCache`; commit **patch** detail stays REST (`commits/{sha}`) —
   that N term is the deferred #6/T3 batch.
   - Normalization parity details preserved exactly: reaction groups →
     REST-summary `{ "+1": n, ... }` (so `mapReactions` behaves identically);
     actor → `RawUser`; PR `state` `MERGED|CLOSED → "closed"`, `OPEN → "open"`;
     `threadResolved` joins `isResolved` only to a thread's **first** comment
     databaseId (others `false`) — matching the REST-hybrid quirk.
2. **`tests/helpers/pr-fixture.ts`** — one logical `PrFixture` projected into both
   a REST client (`buildRestClient`) and a GraphQL client (`buildGraphqlClient`),
   including multi-page connections and a 150-comment thread, so a single source of
   truth drives the equivalence assertion.
3. **`tests/helpers/mock-gh.ts` updated** — the throwaway `gh` shim now serves the
   production per-PR GraphQL base query (valid `pullRequest` + one commit, empty
   connections) so the Phase 1 cross-run cache tests exercise the **GraphQL**
   production path; forced-failure matching widened from REST endpoint to whole
   argv so a `query=# op:base` trigger can abort a mid-fetch run.

### ✅ Gate 2.1 result

- `tests/pr-equivalence.test.ts` (4): over a pathological fixture (101 items per
  connection + a 150-comment thread + outdated/minimized/deleted-author rows) the
  GraphQL-assembled payload is **deep-equal** (canonical sort) to the REST-assembled
  payload, and `extractPrEvidence(gql) === extractPrEvidence(rest)`. Cost-model
  guard: the GraphQL path issues **zero** of the fixed-five REST reads and exactly
  N `commits/{sha}` reads, with `graphqlCount ≥ 1` and a bounded aggregate budget.

### ✅ Gate 2 (universal)

- `typecheck` ✅ · `lint` ✅ · `format:check` ✅ · `vitest` **130** ✅ (was 123;
  +7: 3 assemble + 4 equivalence) · dashboard `node --test` **12** ✅.
- No workflow `.md` changed in Phase 2 (scripts/tests only) → **no lock recompile**.

### Files touched (Phase 2)

- New: `scripts/gh-client.ts`, `scripts/pr-graphql.ts`,
  `tests/helpers/pr-fixture.ts`, `tests/pr-assemble.test.ts`,
  `tests/pr-equivalence.test.ts`.
- Modified: `scripts/fetch-prs.ts` (client seam, extracted `buildPrPayload`,
  GraphQL default, exported `Raw*`/helpers), `tests/helpers/mock-gh.ts` (GraphQL
  base response), `tests/cache-persistence.test.ts` (poison trigger →
  `query=# op:base`).
- _DEFERRED (out of Phase 2 scope):_ #6/T3 commit-patch batching and #7/T2 cross-PR
  batching — the micro-batch seam (`peakConcurrency`) is intentionally preserved for
  them.

### Live GraphQL schema validation (Phase 2, post-gate)

The Gate 2.1 tests validate assembler **logic** against synthetic responses; they
do not prove the query **strings** are accepted by GitHub. Verified separately by
running each query live via `gh api graphql` (token: `repo`, GraphQL rate-limit OK):

- `BASE_QUERY` → ran against a real merged PR (Azure/azure-sdk-tools#16480);
  every field/argument resolved, real data returned (`rateLimit.cost` 2). This
  also validates the shared `REVIEW_SEL`/`ISSUE_SEL`/`COMMIT_SEL`/`LABEL_SEL`
  selections and `closingIssuesReferences`.
- `connectionQuery` (focused top-level pagination) → ran on `reviews(after:$after)`
  with a real endCursor; paginated correctly.
- `THREAD_COMMENTS_QUERY` → ran on a real thread node id
  (`PRRT_kwDOCisHus6TY7Pk`, PR #16475) with a real cursor; resolved to a valid
  empty page.

_Not wired into the automated gate — requires a live token (same class as the
other deferred live checks)._

---

## Phase 3 — Track B (part 1): reusable loop, typed outcomes, durable state, derived backlog

Plan: implementation-plan.md Phase 3. Goal: make the loop callable, give every run
a typed terminal outcome, and derive the backlog each tick from config + today +
a durable completion ledger (so the Phase 4 pacer stays thin). Identity is the
spine: everything is keyed by the canonical `windowId`.

### What changed

1. **Reusable loop (`ccr-improvement-loop.md`).** Added `on: workflow_call` with
   inputs mirroring `workflow_dispatch` (`repo`, `window_start`, `window_end`,
   `max_prs`, all `type: string`). Reconciled the env reads from
   `github.event.inputs.*` → `inputs.*` (populated for BOTH dispatch and
   workflow_call, empty on schedule) and the concurrency group likewise — so a
   scheduled run, a manual dispatch, and a pacer call over the same
   `(repo, window, cohort)` all flow through the SAME Phase 1 resolver and resolve
   to one identical `windowId`. gh-aw rendered a proper reusable workflow (typed
   inputs + outputs + secrets pass-through).
2. **Typed terminal outcome.** New `scripts/emit-outcome.ts`:
   `classifyOutcome(signals)` maps observable signals to exactly one terminal
   value with the precedence `hardFailure → thin-noop → signal-noop →
produced|failed(upload)` — a crashed run with `prCount 0` is `failed`
   (retryable), never mistaken for a legitimate `thin-noop`; `produced` is claimed
   only when the run-JSON upload is confirmed, else `failed`. `buildOutcome`
   writes a schema-validated `outcome-<window_id>.json` whose `artifact_name` is
   DERIVED from the windowId (`run-<window_id>`), stable across attempts, with the
   observed `cost` logged (never fed to an estimator). A deterministic `post-steps`
   block classifies AFTER the agent (settled PR count, agent `noop`, run-JSON
   presence, `job.status`) and uploads the outcome artifact.
3. **Durable-state shapes.** New `scripts/pacer-schema.ts` — strict zod for all
   four persisted Track-B shapes: `PacerConfig` (`pacer/config.json`),
   `OutcomeArtifact`, `LedgerRecord` (`ledger/<window_id>.json` on the future
   `ccr-pacer-state` branch), and `Skipped` (`skipped.json`). Every read/write
   validates; unknown keys are a hard error. (The branch itself is initialized in
   Phase 4; Phase 3 only fixes the record contracts + the reader.)
4. **Settled-only backlog expander.** New `scripts/gen-backlog.ts` —
   `genBacklog(config, {now, settleDays})` expands `start_date … (today −
settleDays)` into `repos × settledPeriods`. Monthly = calendar months
   (`[YYYY-MM-01, last-of-month]`, inclusive); biweekly = fixed 14-day spans
   anchored on `start_date`; weekly = 7-day spans. Emits ONLY windows whose
   inclusive last day `≤ today − settleDays` (a just-closed-but-unsettled period
   is excluded and reappears — same `windowId` — only after it settles).
   Boundaries are inclusive-last-day + next-day start, so — since the fetch search
   is inclusive on both ends (`merged:>=start merged:<=end`) — no PR is counted in
   two adjacent windows.
5. **Derive-pending.** New `scripts/backlog.ts` — `derivePending(desired, ledger,
skipped)` computes `pending = desired − done − skipped`, joined by `windowId`
   (NOT filename): `done` = a ledger record with outcome ∈
   {produced, thin-noop, signal-noop}; `failed` stays pending (retry-eligible);
   `skipped` = listed in `skipped.json`. A ledger record whose id doesn't match a
   desired window marks nothing done. `readLedgerDir`/`readSkippedFile` wrap the
   IO the pacer feeds from the state-branch checkout.
6. **`pacer/config.json`** — the backlog source (repos, start_date, monthly,
   uncapped), validated by `PacerConfigSchema`.

### ✅ Gate 3 result

- **Reusable-loop resolution:** `resolve-window.test.ts` already pins that
  schedule/dispatch/workflow_call shapes over the same `(repo, window, cohort)`
  resolve to one identical `windowId`; the recompiled lock renders `workflow_call`
  with the four inputs and env via `inputs.*`. Validated by **compiling**
  (`gh aw compile` → 0 errors / 0 warnings), not textual comparison.
- **Typed-outcome tests** (`emit-outcome.test.ts`, 10): the full truth table maps
  confirmed-upload / `< minPrs` / agent-`noop` / non-zero-exit / upload-failure to
  `produced | thin-noop | signal-noop | failed`; `artifact_name` is deterministic
  and stable across attempts; the artifact round-trips the strict schema with its
  observed cost.
- **`gen-backlog` tests** (`gen-backlog.test.ts`, 12): monthly/biweekly/weekly
  expansions for a fixed `(config, today)` emit only settled windows (a
  just-closed-but-unsettled month is excluded, then included once it settles);
  biweekly = exactly 14 days, anchored, non-overlapping; adjacent windows never
  share a boundary day; `max_prs` defaults uncapped; multi-repo is repo-major;
  unknown granularity / invalid start_date / bad repo are rejected.
- **`backlog` derive-pending tests** (`backlog.test.ts` 5 + `backlog-io.test.ts`
  3): identity-driven — a drifted `window_id` isn't treated as done;
  produced/thin-noop/signal-noop count done, `failed` does not; empty ledger → all
  pending; fully-ledgered range → empty (termination); skipped removed. IO reader
  validates seeded records and rejects drift.
- Universal gate: `typecheck` ✅ · `lint` ✅ · `format:check` ✅ · `vitest`
  **160** ✅ (was 130; +30) · dashboard `node --test` **12** ✅.
- `ccr-improvement-loop` lock recompiled (0/0) and committed with source.

### Files touched (Phase 3)

- New: `scripts/pacer-schema.ts`, `scripts/gen-backlog.ts`, `scripts/backlog.ts`,
  `scripts/emit-outcome.ts`, `pacer/config.json`, `tests/gen-backlog.test.ts`,
  `tests/backlog.test.ts`, `tests/backlog-io.test.ts`, `tests/emit-outcome.test.ts`.
- Modified: `.github/workflows/ccr-improvement-loop.md` (workflow_call + `inputs.*`
  reconciliation + outcome `post-steps`), `.github/workflows/ccr-improvement-loop.lock.yml`
  (recompiled).
- _DEFERRED to Phase 4:_ the `ccr-pacer-state` orphan branch initialization + the
  ledger-write job; the pacer `uses:` → `.lock.yml` reference. Phase 3 defines the
  record contracts + reader those depend on.

---

## Phase 4 — Track B (part 2): the pacer (static budget check + resume)

Plan: implementation-plan.md Phase 4. Goal: a thin HOURLY cron that drains the
Phase 3 backlog across ticks under a conservative budget — a **static admission
check + resume-next-tick**, deliberately NOT a predictive estimator (D15). All
logic lives in tested TypeScript; the workflow is hand-written plain YAML with no
agent, no gh-aw compile, and no second lock file.

### What changed

1. **`scripts/pace.ts` (pure, tested planner).**
   - `readBudget(rate_limit)` — parses the metered pools (`core` REST — the pool
     that binds — plus `graphql` and `search`), exposing BOTH `remaining` and the
     per-pool `limit` (the latter is what the fresh-hour fit check needs).
   - `estimateCost(window, config)` — returns the FIXED per-window constants
     (`est_rest_calls_per_window` / `est_graphql_points_per_window`), independent
     of the window's PR count. No probes, no cardinality model — which is exactly
     what makes fittability a global property of the granularity.
   - `assertFittable(pending, budget, config)` — the **starvation guard**. If any
     pending window's fixed estimate can't fit a _fresh full hour_
     (`pool.limit − safety_margin`) on some pool, it could never be admitted and
     would defer forever INVISIBLY (a never-admitted window emits no outcome,
     never becomes `failed`, never hits the retry cap). So it throws LOUDLY —
     naming the pool, the estimate, the fresh-hour budget, and the fix — and the
     `plan` job exits non-zero. A window that fits a fresh hour but not this
     depleted tick passes (it is merely deferred).
   - `admit(pending, budget, config)` — greedily admits windows that fit
     `remaining − safety_margin` on EVERY metered pool, decrementing as it goes,
     capped by `max_windows_per_tick` and **≤ 1 window/repo/tick**. Whatever
     doesn't fit is picked up next tick (the cross-run cache makes re-dispatch
     free). A widened `remaining` (a future per-repo T1 token) admits more with NO
     code change.
   - `reconcile(admitted, outcomes, priorLedger, config, opts)` — every admitted
     window MUST have an outcome artifact; a missing one ⇒ `failed`. DONE outcomes
     become terminal ledger records; `failed`/missing increment a DURABLE per-window
     `attempt` and, once it exceeds `retry_cap` (default 1 ⇒ the 2nd failure),
     retire the window to `skipped.json` (no poison loop).
   - CLI modes `plan` (emits the backfill matrix, or `--dry-run` prints the plan +
     pending count and touches nothing) and `reconcile` (folds downloaded
     `outcome-*.json` into `ledger/` + `skipped.json`).
2. **`pacer-schema.ts` extended.** Added the budget knobs to `PacerConfigSchema`
   (`est_rest_calls_per_window` 1200, `est_graphql_points_per_window` 2000,
   `safety_margin` 500, `max_windows_per_tick` 4, `retry_cap` 1 — all defaulted,
   so the existing `pacer/config.json` stays valid) plus `PoolBudgetSchema` /
   `BudgetSchema` and `parseBudget` (picks `limit`+`remaining` from a raw
   `gh api rate_limit` response, ignoring the other pools/fields).
3. **`.github/workflows/ccr-pacer.yml` (plain YAML, hand-written).**
   `on: schedule (hourly) + workflow_dispatch(dry_run)`, `concurrency: ccr-pacer`
   (`cancel-in-progress: false`) so ticks never race the state branch. Three jobs:
   - **`plan`** (`contents: read`) — clone `ccr-pacer-state` (or treat as empty),
     run `pace.ts plan` → `outputs.admitted` + `count`.
   - **`backfill`** — `strategy.matrix.window: fromJSON(needs.plan.outputs.admitted)`,
     `uses: ./.github/workflows/ccr-improvement-loop.lock.yml` per window with
     `secrets: inherit`. The loop's per-repo child concurrency group + the
     ≤ 1-window/repo admission rule serialize same-repo work.
   - **`ledger`** (`if: always()`, `contents: write`) — clone/init the state
     branch, `download-artifact pattern: outcome-*`, `pace.ts reconcile`, then a
     normal (non-force) push with fetch/rebase retry (NO `sleep` — scope guard B4).

### ✅ Gate 4 result

- **Static-admission tests** — fits every pool minus margin; honors
  `max_windows_per_tick`; ≤ 1 window/repo; a low budget on ANY pool (core or
  graphql) blocks admission; `remaining` decrements so a tick can't over-commit; a
  widened `remaining` admits more with no code change.
- **Fit-invariant tests** — an over-budget-for-a-fresh-hour window throws with the
  pool/estimate/fresh-hour/fix in the message (plan exits non-zero); a
  fits-a-fresh-hour-but-not-this-tick window does NOT throw and is merely deferred.
  The starvation regression (unfittable-forever sitting pending, never failed,
  never skipped) fails CI.
- **Reconciliation/retry tests** — DONE outcomes terminal + never retried; missing
  artifact ⇒ `failed` (attempt 1, synthesized run id); a repeat failure increments
  the durable `attempt` and retires to `skipped.json` past the cap; `thin-noop` /
  `signal-noop` are done.
- **Workflow-boundary reconcile** — 2 successful legs + 1 failed leg → all three
  admitted windows recorded correctly (2 done, 1 `failed`→retry) despite the
  failed leg, exactly as the `if: always()` ledger job will see them.
- **Two-tick offline drain** — tick 1 admits (≤1/repo, cap 2) and writes typed
  records; tick 2 re-derives the remainder (done not re-admitted, the `failed`
  retried, previously-deferred now admitted); tick 3 honors the now-`skipped`
  poison window (never pending again).
- **`readBudget` tests** — extracts the three pools (ignoring extra pools/fields),
  throws on a malformed response.
- **Workflow static gate** — `actionlint .github/workflows/ccr-pacer.yml` passes
  (exit 0), which validates the reusable-workflow call boundary: the `with:` inputs
  (`repo`/`window_start`/`window_end`/`max_prs`) all resolve against the real
  `ccr-improvement-loop.lock.yml`.
- **Scope guards (B4)** — no `sleep` command in the pacer (retries are immediate);
  `pace.ts` imports only structural modules (gen-backlog, backlog, run-schema,
  window-id, pacer-schema, utils) — no judge/metric imports.
- Universal gate: `typecheck` ✅ · `lint` ✅ · `format:check` ✅ · `vitest` **179**
  ✅ (was 160; +19) · dashboard `node --test` **12** ✅.
- `ccr-pacer.yml` is plain YAML (no lock); the loop's `.md`/`.lock.yml` are
  untouched this phase.
- _Milestone proofs — DEFERRED (need a GHEC token):_ M2 (a repos×months campaign
  draining across ticks with no 403) and M3 (mid-backfill restart resumes, a
  poison/thin window doesn't wedge). Proven offline now via the workflow-boundary,
  two-tick, and reconciliation tests.

### Files touched (Phase 4)

- New: `scripts/pace.ts`, `tests/pace.test.ts`, `.github/workflows/ccr-pacer.yml`.
- Modified: `scripts/pacer-schema.ts` (budget knobs + `Budget`/`parseBudget`).
- _DEFERRED to Phase 5 (Track C):_ preflight budget guard (C2), dashboard scale /
  size-bucket / repo faceting (C3), and the doc convergence (C4).

---

## Phase 5 — Track C: dashboard scale, preflight guard, hygiene, docs (C2/C3/C4)

Plan: implementation-plan.md Phase 5. Goal: the scale-polish that must land before
Definition of Done — a preflight budget guard so a depleted pool aborts a window
cleanly, a build-free manifest generator + a PR-size coverage slice so the
dashboard ingests a large backfill without hand edits, and doc convergence so no
"deferred" label dangles on work now shipped. No agent, no behavior change to what
the loop measures.

### What changed

1. **C2 — Preflight budget guard (`scripts/prep-run.ts`, `scripts/pace.ts`).**
   - New exported `preflightShortfalls(budget, est)` in `pace.ts`: checks THIS
     MOMENT's live `remaining` on `core`/`graphql`/`search` against one window's
     static cost. The safety margin (a headroom concept sized for the thousands-
     wide hourly pools) is applied to core/graphql only — **never** the tiny
     30/min `search` pool, where an absolute 500 margin would guarantee a false
     shortfall. Returns one entry per short pool ([] ⇒ clear to proceed).
   - `prep-run.ts` gains `--check-budget` (**default on**, so CI is guarded) +
     `--no-check-budget`. When NOT `--skip-fetch`, it fetches `gh api rate_limit`,
     runs the guard, and aborts with a non-zero exit + a clear per-pool message
     **before any fetch or cache write** (no partial cache). Skipped entirely under
     `--skip-fetch` (offline runs spend nothing) — so the offline fixture tests are
     unaffected.
   - Estimate constants centralized in `pacer-schema.ts`
     (`DEFAULT_EST_REST_CALLS_PER_WINDOW`=1200, `…_GRAPHQL_POINTS…`=2000,
     `…_SEARCH_CALLS…`=10, `DEFAULT_SAFETY_MARGIN`=500, plus the admission knobs),
     reused as the Zod `.default()`s AND by the preflight guard — ONE source of
     truth. Added an `est_search_calls_per_window` config knob.

2. **C3 — Dashboard scale.**
   - New `scripts/gen-manifest.ts`: scans a data dir, keeps only `run-*.json`
     (never the manifest itself), sorts byte-order + de-dupes, writes
     `manifest.json` (build-free CLI; the browser can't list a directory, D11). A
     `--check` mode fails if the on-disk manifest is stale. Pure `buildManifest` /
     `isRunFile` are unit-tested; run against the real `dashboard/data` it
     reproduces the checked-in manifest byte-for-byte.
   - **PR-size bucket coverage slice.** `run-schema.ts` gains a `SizeBucket`
     enum (`S`/`M`/`L`/`XL`), a `sizeBucketOf(additions, deletions)` helper (S<50,
     M<200, L<500, XL≥500; `null` when either count is null), and an **optional**
     `sizeBucket` field on the metric slice object (additive — existing run files
     still parse, no schema-version bump). `compute-metrics.ts` appends size cells
     to `ccrCoverage.slices` over the same eligible PRs, from the already-captured
     `additions`/`deletions` — a deterministic recompute over existing raw fields,
     no agent re-run. Unknown-size PRs are excluded from size cells, never
     mislabelled.
   - **Dashboard render.** `aggregate.mjs` exports `SIZE_ORDER`; `poolSlices` was
     already dimension-generic (mixed-dimension cells coexist — pooling by
     `sizeBucket` ignores the `prType` cells and vice versa). `app.mjs` adds a
     `chart-coverage-size` grouped bar (`poolSlices(runs,"ccrCoverage",
"sizeBucket",SIZE_ORDER)`); `index.html` adds the card + canvas.
   - **Repo faceting** was already present (`buildRepoFilter`, `SELECTED_REPOS`,
     per-repo trend lines) — confirmed, no change needed.

3. **C4 — Docs converged (no dangling "deferred").**
   - `api-rate-limit.md` §5 (#4 + pacer now shipped; tiered deferrals) and §7
     (recommended path reflects the shipped pacer + tiered T1/#7/#6).
   - `optimize-api-call-plan.md` §4 (IMPLEMENTED ✅), §8 (IMPLEMENTED ✅), and the
     Sequencing/STATUS block (#4/#8 done, #6/#7 evidence-gated deferrals).
   - `README.md` — new "Backfilling at scale (the pacer)" section with the
     T1/T2/T3 lever tiers; `gen-manifest.ts` referenced in the backfill refresh
     step.
   - `decisions.md` — **D15** (backfill pacer: decision/why + rejected
     alternatives — always-on runner/in-job sleep, cross-workflow dispatch needing
     a PAT, per-run-cap-only, and a committed status-tracked backlog replaced by a
     derived backlog over a durable ledger), **D16** (tiered, evidence-gated REST
     minimization — #1/#4 shipped, #7/#6 deferred), **D17** (per-repo
     App-installation tokens as the primary N-repo lever, config-not-code).
   - `handoff.md` — new newest-first session entry (Phases 0–5 summary) + updated
     "last updated" banner.

### Gate 5 — verification

- **`gen-manifest.ts` tests** — filtering (only `run-*.json`), byte-order sort,
  de-duplication, empty-list; the real data dir round-trips to the checked-in
  manifest (a 50-window set needs no hand edits).
- **`sizeBucket` test** — recompute over PRs yields the expected S/XL buckets with
  correct numerators/denominators; unknown-size PRs are excluded from size cells
  but still counted in overall coverage; the new slice validates against
  `run-schema`. Dashboard `poolSlices` pools `ccrCoverage` by `sizeBucket` in
  `SIZE_ORDER`, ignoring the coexisting `prType` cells.
- **C2 preflight tests** — `preflightShortfalls` returns [] when every pool covers
  a window + margin; flags a core pool one unit short (`need`/`remaining`
  reported); reports multiple short pools (core + search) independently. The
  offline prep-run fixture tests still pass (guard skipped under `--skip-fetch`).
- Universal gate: `typecheck` ✅ · `lint` ✅ · `format:check` ✅ · `vitest` **187**
  ✅ (was 179; +8) · dashboard `node --test` **13** ✅ (was 12; +1).
- `actionlint .github/workflows/ccr-pacer.yml` ✅ (exit 0); `ccr-pacer.yml` remains
  plain YAML (no lock).
- `ccr-improvement-loop.lock.yml` recompiled via `gh aw compile
ccr-improvement-loop` (0 errors / 0 warnings).
- `.github/aw/ccr-improvement-loop-legacy/` never touched.

### Files touched (Phase 5)

- New: `scripts/gen-manifest.ts`, `tests/gen-manifest.test.ts`.
- Modified (scripts): `run-schema.ts` (SizeBucket + `sizeBucketOf` + optional slice
  field), `compute-metrics.ts` (size slices on `ccrCoverage`), `pace.ts`
  (`preflightShortfalls`), `prep-run.ts` (C2 guard + flags), `pacer-schema.ts`
  (exported DEFAULT_* estimate constants + `est_search_calls_per_window`).
- Modified (dashboard): `dashboard/js/aggregate.mjs` (`SIZE_ORDER`),
  `dashboard/js/app.mjs` (size chart), `dashboard/index.html` (card + canvas).
- Modified (tests): `tests/pace.test.ts` (preflight block),
  `tests/compute-metrics.test.ts` (size-slice test),
  `dashboard/js/aggregate.test.mjs` (sizeBucket poolSlices test).
- Modified (docs): `api-rate-limit.md`, `optimize-api-call-plan.md`, `README.md`,
  `decisions.md`, `handoff.md`.
- Recompiled: `.github/workflows/ccr-improvement-loop.lock.yml`.

### Definition of Done — status

All six phases (0–5) landed with each gate green before the next. DoD checklist:
unattended `repos × months` drain (proven offline via workflow-boundary + two-tick

- reconciliation tests; the live no-403 GHEC run is DEFERRED pending a token) ✅;
  settled-only backlog ✅; child loop unchanged in what it measures, pacer the only
  durable writer ✅; static conservative admission metering core+graphql(+search)
  with `assertFittable` ✅; tiered REST minimization (#4 shipped; #7/T1/#6 documented
  decision points) ✅; dashboard ingests a large backfill without hand edits, facets
  by repo, reads honestly ✅; all gates green + lock recompiled + `ccr-pacer.yml`
  plain YAML + docs converged (D15/D16/D17 + handoff) ✅.

---

## Phase 5 — post-review remediation (rubber-duck)

A `/rubber-duck` review of Phase 5 flagged one blocking gap and three lower-severity
notes. Actioned:

- **Blocking — size chart shipped against empty data.** The `sizeBucket` slices are
  generated at run-emit time, so the already-committed `dashboard/data/run-*.json`
  had none and `chart-coverage-size` rendered empty. The committed runs DO carry a
  full `prs` array with `additions`/`deletions`, so the plan's promised
  "deterministic recompute over existing raw fields (no agent re-run)" is feasible.
  Added `scripts/backfill-size-slices.ts` — an **additive, self-verifying**
  migration: it recomputes `ccrCoverage` from each file's own `prs`/`comments` and
  refuses to write unless the recomputed value/denominator and `prType` cells are
  byte-identical to what the file already stores (so the size cells provably
  partition the SAME eligible-PR set), then appends only the size cells and
  re-validates via `parseRun`. Idempotent. Ran it over `dashboard/data` (**14 runs
  updated**, all self-consistent) and `dashboard/js/fixtures` (**6 synthetic
  fixtures correctly refused** — their hand-authored coverage doesn't match their
  `prs`, and D12 keeps fixtures decoupled from live data anyway). Manifests
  regenerated. New `tests/backfill-size-slices.test.ts` pins the add / idempotent /
  refuse paths.
- **Non-blocking, accepted as-is (documented):** (2) the preflight guard uses the
  static `DEFAULT_EST_*` constants rather than pacer config — intentional: the
  child loop's `prep-run` has no pacer config and a conservative standalone guard
  is the right contract. (3) the tiny 30/min `search` pool isn't reserved across
  concurrent windows — `est_search` is a conservative upper bound (~1–2 real calls/
  window), and the #5 secondary-limit limiter is the actual backstop. (4)
  `ccrCoverage.slices` now holds two dimensions (prType + sizeBucket), so a naive
  consumer summing the whole array double-counts — the only consumer (`poolSlices`)
  filters by the requested dim, and the top-level `value`/`denominator` remain the
  authoritative totals.

### Gate re-verification (post-remediation)

- `typecheck` ✅ · `lint` ✅ · `format:check` ✅ · `vitest` **190** ✅ (was 187; +3
  backfill tests) · dashboard `node --test` **13** ✅.
- `dashboard/data` runs now carry size cells (6 prType + 4 size per run); manifests
  up to date (`gen-manifest --check` clean for both dirs).

### Files touched (remediation)

- New: `scripts/backfill-size-slices.ts`, `tests/backfill-size-slices.test.ts`.
- Data: all 14 `dashboard/data/run-*.json` gained `sizeBucket` cells on
  `ccrCoverage.slices`; `dashboard/data/manifest.json` +
  `dashboard/js/fixtures/manifest.json` regenerated.

---

## Post-Phase-5 — README concision (user request)

User asked to make the README "much more concise — quickly explain what this is and
how to onboard (pacer for backfill is the suggested method)."

- Rewrote `README.md` from **363 → 86 lines**: a one-paragraph "what this is", a
  compact metric-summary table, an **Onboarding** section leading with a single
  one-off dispatch and then the **pacer as the recommended backfill method**
  (`pacer/config.json` repo list + start date), a short Dashboard section, and a
  "More" pointer list. Verified every referenced path exists.
- Preserved the detailed metric-defensibility prose (Reading rules, ground truth,
  Q1–Q4 definitions, sampling window, the judge, anti-Goodhart) — moved verbatim
  into a new **`metrics.md`** so the README stays lean without losing the content;
  fixed the one internal anchor that pointed at a removed README section.
- `format:check` clean; internal links (`README.md#onboarding`,
  `metrics.md#reading-rules-...`) resolve.

---

## Trial deployment — pacer run from June 1 (`JennyPng/gh-aw-trial`)

User: "do a test run in the gh-aw-trial repo. push changes there and trigger the
pacer just starting from june 1."

- Set `pacer/config.json` `start_date` → **2026-06-01** (repos, monthly
  granularity, uncapped `max_prs:0` left as-is — "just" the date). July is unsettled
  (`end ≤ today−14d`), so the backlog is exactly **June per repo**.
- Local branch HEAD `== trial/main` (`9539a4272`), so the full Phase 0–5 build was a
  clean fast-forward. Committed the build (`6ec29d135`) — **legacy dir excluded**
  (untracked, never staged) — and pushed to `trial/main`.
- First dry-run dispatch → **startup_failure**: the reusable loop's `agent`/`detection`
  jobs request `copilot-requests: write`, but the pacer's `backfill` caller job didn't
  grant it (a caller's permissions must be a superset of every nested job's). Fixed by
  adding `copilot-requests: write` to the backfill job (`279677156`). actionlint flags
  the scope as "unknown" (its list is stale) — GitHub requires it; non-fatal.
- Re-dispatched `dry_run=true` → **plan passed**. In-Actions `GITHUB_TOKEN` budget is
  **core 5000 / graphql 5000** (not the ~1,000 the old handoff feared), so the
  starvation guard clears (est 1200 rest / 2000 graphql per window). Admitted **2**
  windows (go June, python June), deferred 0.
- Real dispatch (run `30124922457`): plan ✓ → both backfill legs cleared
  pre-activation/activation ✓ → both in the **agent** phase (live loop). Ledger will
  reconcile into `ccr-pacer-state`.

---

## Doc — `how-it-works.html` (ground-up system walkthrough)

User: "generate an html page that walks through ground up how this system works."

- Created **`how-it-works.html`** — a self-contained (no external deps),
  dark-themed walkthrough reusing `rate-limit-walkthrough.html`'s design language
  (CSS variables, `section.step`, `.callout`, `details.check`, `table.limits`, an
  interactive quiz). Added pipeline-flow diagrams and provenance-tagged metric
  cards.
- 12 sections, ground-up: the question → big-picture flow → the three-phase
  architecture (D2) → Phase 1 deterministic prep (window identity, fetch/classify/
  filter/attribute, the raw-only cache) → Phase 2 agent judgment (closed vocabulary,
  no `judge.ts`) → the five metrics + reading rules (each tagged deterministic vs
  judged) → the two eligibility guards + the `missRate`→`ccrRecallRate` cautionary
  tale (D14) → Phase 3 terminal outcome → the pacer (plan/backfill/ledger +
  starvation guard) → the static dashboard → anti-Goodhart/proposal-only → a
  5-question self-check quiz.
- Validated: all tags balanced, every TOC anchor resolves, footer links exist,
  `format:check` clean.

---

## Trial fix — outcome artifact upload + serialize the pacer

First trial pacer run (`30124922457`) surfaced two issues; user: "fix, serialize."

- **Bug: the pacer ledger marked every window `failed`, even the successful python
  one.** `emit-outcome.ts` wrote `outcome-<id>.json` fine, but the "Upload terminal
  outcome" step logged `No files were found with the provided path:
.ccr-runs/outcome-*.json`. Cause: `.ccr-runs` is a **dot/hidden directory** and
  `actions/upload-artifact@v4` excludes hidden files by default
  (`include-hidden-files: false`), so the artifact never uploaded; the ledger reads
  a missing outcome as `failed`. This silently broke the entire pacer success path
  (standalone dispatch was unaffected — only the pacer consumes the outcome
  artifact). **Fix:** added `include-hidden-files: true` to that step in
  `ccr-improvement-loop.md`; recompiled the lock (`gh aw compile`, 0 err/0 warn).
- **Rate-limit race:** both uncapped June cohorts ran concurrently and share one
  installation budget; go hit `403 rate limit exceeded for installation` mid-fetch.
  **Fix:** set `max_windows_per_tick: 1` in `pacer/config.json` to serialize legs
  (verified locally: plan admits go, defers python).
- Committed + pushed the fix to `trial/main`; re-triggered the pacer. The prior
  `failed` ledger records are attempt-1 and retryable (retry_cap 1), so both windows
  re-run — go fresh (its failed fetch saved no cache), python off its saved pr-cache.

### Re-trigger #1 (run 30127018825) — blocked by installation rate-limit

- Committed fix (`b5f189c11`), pushed to `trial/main`, re-dispatched pacer.
- **Serialization confirmed:** plan admitted only the go window; python deferred.
- **But the agent phase never ran.** `pre_activation` hit
  `API rate limit exceeded for installation` during its repository-permission
  check, so it set `activated=false` and all downstream jobs
  (activation/agent/detection/safe_outputs/conclusion) were **skipped**. The
  overall run still reports `success` because skipped-by-design isn't a failure.
- **Cause:** the shared GitHub App installation budget was drained by the earlier
  full agent run (30124922457 at ~20:56, which judged all of python's June PRs and
  fetched go until the 403). `pre_activation`'s permission check fails **closed**
  when it can't reach the API → no leg can activate until the budget resets.
- Reconcile then recorded go as `failed` **attempt 2** (one more fail → permanent
  `skipped`). This is an infrastructure failure, not an analysis failure.
- **Ledger reset:** deleted both window records on `ccr-pacer-state` so the
  rate-limit-caused failures don't cost us the go cohort. Both windows now re-plan
  as fresh attempt 1.
- **The hidden-files fix is still unvalidated** — the agent phase must actually run
  to produce/upload an `outcome-*.json`. Next: wait for the installation budget to
  reset (~hourly), then re-dispatch once and confirm go activates → agent runs →
  outcome artifact uploads → ledger records `success`.

### Added AGENTS.md (package agent-context doc)

- Wrote `.github/aw/ccr-improvement-loop/AGENTS.md` capturing the contextual
  knowledge an AI agent needs: non-negotiable rules (frozen legacy dir, execution
  log, compile discipline, keep local), the two workflows + where their source
  lives, the run pipeline stages, gates/commands (incl. the separate dashboard
  test + prettier-ignores-json), pacer/ledger/state-branch model, config files,
  the hard-won gotchas (hidden-files artifact default, pre_activation fails closed
  under installation rate-limit, shared-budget parallelism, reusable caller-perms
  superset, emit-step `set -uo pipefail`), trial-repo commands, dashboard, a key-
  scripts table, and the doc index. Prettier-clean.

### VALIDATED — hidden-files fix works end-to-end (run 30129518458)

- Re-dispatched the pacer after the installation budget recovered. This tick the
  agent phase **ran fully**: activation ✓, agent ✓ (5m55s), detection ✓,
  safe_outputs ✓, conclusion ✓. Serialization held — only one window (python)
  admitted.
- **Fix confirmed live:** the "Upload terminal outcome" step logged
  `include-hidden-files: true` with `path: .ccr-runs/outcome-*.json`; the outcome
  file wrote `(produced)`; **no** "No files were found" warning.
- **Ledger now records `outcome: "produced"` (attempt 1)** for the python window —
  reconcile could only read that by downloading the uploaded outcome-*.json. Under
  the old bug it read `failed` from a missing artifact. Fix validated.
- Stopped the 40-min retry schedule; no further automated ticks. go window remains
  pending (fresh, will be admitted on a subsequent tick/dispatch).
