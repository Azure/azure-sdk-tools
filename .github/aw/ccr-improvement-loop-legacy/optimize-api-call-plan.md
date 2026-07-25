# Optimize GitHub API Call Plan — `ccr-improvement-loop`

Goal: reduce GitHub API consumption (and secondary-rate-limit risk) in the PR
fetch path so uncapped windows stay under the shared GitHub App installation
budget (~5,000 req/hr).

## Cost model (today)

Per PR, `fetchPrToCache` (`scripts/fetch-prs.ts`) issues:

- **5 REST** (fixed): `pulls/{n}`, `.../reviews`, `.../comments`,
  `issues/{n}/comments`, `.../commits`
- **1 GraphQL** (fixed): thread resolution + linked issues
- **2 REST per commit** (the multiplier): `commits/{sha}` (files/patches) and
  `commits/{sha}/pulls` (commit→PR mapping)

Total ≈ **6 + 2N** *endpoint invocations* for a PR with *N* commits. The
per-commit loop dominates on large PRs.

> ⚠️ These are **lower bounds, not request counts.** Every async call uses
> `gh api --paginate`, so reviews/comments/commits (and commits touching >300
> files) can each span multiple HTTP requests, and `ghApiJsonAsync` retries add
> more. Do **not** treat `6 + 2N` as a budget guarantee — validate with a
> `GET /rate_limit` reading before/after a representative run.

---

## 1. Delete the `commits/{sha}/pulls` call + `commitPrs` field — IMPLEMENTED ✅

`commitPrs` is **written but never read** by any consumer (verified: appears
only in `fetch-prs.ts`, the optional `types.ts` field, and test fixtures).

**Deletion is safe because the field is unread — full stop.** (Do not justify
it via squash-merge: squash controls base-branch *integration*, not whether a
contributor merged/rebased `main` *into* a PR branch, and the workflow can be
dispatched against any repo per the README. Any merge-sync timeline pollution
already exists today and is unaffected by removing an unused field.)

The one *hypothetical* use would be filtering base-branch/merge commits out of
the attribution timeline (`attribute-comments.ts`, `build-judge-input.ts`). If a
merge-commit repo is ever targeted and attribution accuracy matters, the fix is
to **wire `commitPrs` in** (keep a commit only if its introducing PR == this
PR), not to have left it unused. **Decision: delete now; document the topology
gap.**

Effect: per-PR cost `6 + 2N` → **`6 + N`** (halves the dominant term).

Changes:
- `scripts/fetch-prs.ts`: remove the `commitPrs` accumulator, the
  `commits/{sha}/pulls` fetch (the `try/catch` block), and `commitPrs` from the
  written payload. Drop the "commit→PR mapping" line from the header comment.
- Add a one-line note that commit→PR filtering is the correct fix if a
  merge-commit repo is ever targeted.
- `scripts/types.ts`: remove the optional `commitPrs` field.
- Tests/fixtures: remove `commitPrs` from `tests/build-judge-input.test.ts`,
  `tests/attribute-comments.test.ts`, and `tests/fixtures/pr-sample.json`.

Risk: none — field is unread. Optionally add a regression test for a PR branch
containing a merge-from-base commit to document the unsupported topology. Bump
`RAW_SCHEMA_VERSION` if cache compatibility matters; otherwise stale caches
simply omit an unused field.

---

## 2. Parallelize the fan-out — IMPLEMENTED ✅

Naive `Promise.all` is **not safe here**: `runWithConcurrency` caps PR *workers*
(default 6), not requests. Fanning out all fixed calls + every commit means a
65-commit PR × 6 workers ≈ ~390 concurrent `gh` processes → **worse**
secondary-rate-limit risk, the opposite of the goal. Also `fetchThreadResolution`
was synchronous (`ghJsonSync`) and couldn't join `Promise.all`.

**Done (depends on #5's limiter):**
- `fetchThreadResolution` converted to async via `ghApiGraphqlAsync` so the
  GraphQL call is bounded by the same limiter.
- In `fetchPrToCache`, the 5 independent per-PR reads + thread resolution now run
  under one `Promise.all`; the per-commit `commits/{sha}` detail fetches run under
  a second `Promise.all` (`commitsRaw.map`), which preserves commit order.
- All requests flow through the shared `ghRequestLimiter`, so total in-flight is
  capped regardless of `workers × per-worker fan-out`.

Result: lower wall-clock per PR at a *controlled* request rate; identical payload
(same calls, order-preserving assembly). Verified by `Semaphore` ceiling test +
typecheck + full suite (97 tests green).

---

## 3. Selective commit-detail fetching — DEFERRED (blocked at fetch layer) ⛔

Intent: fetch `commits/{sha}` files/patches only for paths tied to surviving
candidate comments, removing much of the `N` term.

**Investigation result — not safely doable at the fetch layer today.** Two
blockers found:

1. **`distinctFiles` couples to *every* commit's file list.**
   `prep-summary.ts:78-82` builds `distinctFiles` by iterating `commit.files`
   across **all** PRs (not just commented ones). Skipping commit-detail for
   no-comment PRs would silently shrink that emitted stat — a behavior
   regression, not a free optimization.
2. **Per-commit *timing* attribution needs per-commit files.**
   `attribute-comments.ts:118-127` and `build-judge-input.ts:43-55` pick the
   latest commit that touched a path *before* the comment. `GET /pulls/{n}/files`
   (one aggregate call) gives the union of changed files but **loses per-commit
   `committedAt`**, so it cannot replace the per-commit fetch for attribution.

The one clean sub-win — skip the whole `commits/{sha}` loop when a PR has **zero
inline comments** (the only consumer of commit files/patches) — is still blocked
by (1): those PRs' files would vanish from `distinctFiles`.

**Correct fix (out of scope here):** move commit-detail fetching into a later
stage that runs *after* comment filtering, and source `distinctFiles` from a
PR-level `pulls/{n}/files` count (1 call/PR) decoupled from the attribution
timeline. That is a cross-stage refactor touching `fetch-prs` → `filter` →
`prep-summary`; deferred to keep this change low-risk. #1 + #2 + #5 already
address the acute rate-limit pressure.

## 4. Collapse REST fan-out into GraphQL (larger change) — DEFERRED 🕐

A GraphQL query per PR *can* return reviews, inline comments (+ thread
`isResolved`), issue comments, and commits together. **Caveats confirmed during
#2/#5 work:**
- These are paginated connections — the existing thread query already truncates
  threads@100 and linked issues/labels@20 — so a "single query" claim is false
  for large PRs; it needs cursor loops + field-equivalence tests vs. the
  fully-paginated REST path.
- Per-commit **file patches** (`build-judge-input`) are not cleanly available in
  GraphQL, so the dominant `N` term stays on REST regardless.

Given #1 (`6+2N`→`6+N`) + #2 (bounded parallelism) + #5 (limiter/backoff) already
relieve the acute pressure, and the migration is high-effort for a partial win,
this is deferred unless real runs still hit the ceiling. The new
`ghApiGraphqlAsync` helper (added in #2) is the building block if revisited.

---

## 5. Centralized limiter + honor rate-limit headers — IMPLEMENTED ✅

`ghApiJsonAsync` previously only backed off *after* a 403/secondary error, with
fixed exponential backoff that ignored GitHub's `Retry-After` header, and nothing
bounded cross-call concurrency.

**Done (in `scripts/utils.ts`):**
- `Semaphore` class — a counting semaphore bounding concurrent async ops.
- `ghRequestLimiter` — a process-wide `Semaphore` (default 8, override via
  `CCR_GH_MAX_CONCURRENCY`), kept well under GitHub's ~100-concurrent guidance.
- `ghApiJsonAsync` **and** the new async `ghApiGraphqlAsync` both admit every
  request through `ghRequestLimiter` (shared REST+GraphQL ceiling) via a common
  `spawnGhJson` runner.
- `parseRetryAfterMs` + `withGhRetry` — retry now prefers the server-advertised
  `Retry-After` / "retry after N seconds" delay, falling back to exponential.

Note: `GET /rate_limit` preflight was intentionally **not** added — it covers
only the *primary* budget and cannot prevent secondary (burst) limits, which the
limiter now handles directly. It remains a possible coarse primary-budget
admission control if ever needed.

Verified: `Semaphore` peak-concurrency + release-on-rejection tests and
`parseRetryAfterMs` tests in `tests/utils.test.ts` (7 tests); typecheck + lint +
full suite (97 tests) green.

---

## 6. Move per-commit detail to local git — PROPOSED (attacks the `N` term off-budget) 🔬

The dominant `N` term is Phase 3 of `fetchPrToCache` (`scripts/fetch-prs.ts`):
one `GET /commits/{sha}` REST call per commit, purely to obtain files + patches +
commit time. **Every field it writes is a native git concept**, so it can be
sourced from a local clone with **zero GitHub API calls**:

| Written field (`fetch-prs.ts`, ~L403–L409) | Local-git source (no API) |
|---|---|
| `sha` | `git rev-list` / `git log` |
| `committedAt` | `git log --format=%cI <sha>` (committer date, matching the current `c.commit.committer?.date` preference) |
| `files` (filenames) | `git diff-tree --no-commit-id --name-only -r <sha>` |
| `patches` (per-file diff text) | `git show <sha>` / `git diff` |

**Why this is different from #3/#4 (and stronger).** Both prior items failed
*specifically on patches*: #3 is blocked by the `distinctFiles` coupling, and #4
notes per-commit file patches "are not cleanly available in GraphQL, so the
dominant `N` term stays on REST regardless." Git yields patches natively. Crucially,
**git-protocol operations do not draw from the REST or GraphQL rate-limit pools** —
they use the separate, far more generous git transport budget. So this does not
*shrink* `N`; it **removes `N` from the rate-limited path entirely**, leaving the
per-PR API cost at ≈ `6 + 0` (the fixed calls only).

**Feasibility / caveats:**
- The workflow can be dispatched against any repo (per the README), including very
  large ones (`azure-sdk-for-python`). Use a **blobless partial clone**
  (`git clone --filter=blob:none`) so history is cheap and blobs are fetched on
  demand — do **not** full-clone up front.
- CI checkout is usually shallow (`fetch-depth: 1`); this needs `fetch-depth: 0`
  or targeted `git fetch origin <sha>` for exactly the mined commits.
- Trade-off is local git CPU/bandwidth vs. API budget — the correct direction when
  the primary REST ceiling (not disk) is what fails runs.
- **Cannot** replace the fixed GraphQL call: thread `isResolved` + linked issues
  are GitHub review-model state, absent from git. Reviews/comments likewise stay
  on the API. Git only eliminates the commit-detail term.
- Preserve the existing `distinctFiles` contract (`prep-summary.ts:78-82`) and
  per-commit `committedAt` attribution (`attribute-comments.ts`,
  `build-judge-input.ts`) — the git-derived `files`/`committedAt` must be
  field-equivalent to today's REST-derived values (golden-output test, as in #3).

**Effect:** per-PR API cost `6 + N` → **`6`** on the rate-limited path; the `N`
work shifts to git. Directly relieves the primary-budget ceiling that #3/#4 left
intact.

## 7. Batch the fixed calls via GraphQL aliasing — PROPOSED (extends #4) 🔬

#4 already frames "one GraphQL query per PR" to collapse the 5 fixed REST reads.
The **batching** angle adds what #4 does not: GraphQL **aliasing puts multiple
PRs (or commits) in a single HTTP request**:

```graphql
{ p0: repository(...) { pullRequest(number: 101) { ... } }
  p1: repository(...) { pullRequest(number: 102) { ... } } }
```

**Two wins beyond plain #4:**
- **Separate budget pool** — GraphQL is point-metered (~5,000 points/hr)
  independent of the REST hourly budget (already noted in
  `api-rate-limit.md §4, Lever 2`); the fixed calls move off the REST ceiling.
- **Fewer HTTP requests → direct secondary-limit relief** — batching N PRs into a
  few requests structurally lowers in-flight count, which is exactly what the #5
  `Semaphore` fights. This reduces burst pressure rather than merely throttling it.

**Hard limits (unchanged from #4):**
- Connections still paginate (threads@100, linked issues/labels@20); a batched
  query needs cursor loops + field-equivalence tests vs. the fully-paginated REST
  path. Batching multiplies pagination complexity across aliases.
- **GraphQL still does not expose commit patch text**, so batching cannot touch
  the `N` term — that is #6's job, not this one.
- Point cost scales with node count, so an over-wide batch can exhaust the GraphQL
  pool or trip GraphQL's own CPU-cost guard; batch width must be tuned and still
  admitted through `ghRequestLimiter`.

**Effect:** the fixed `6` moves onto the separate GraphQL pool in batched requests.
Composed with **#6** (which removes `N`), per-PR consumption on the *primary REST*
budget — the ceiling that actually fails runs — drops toward **near zero**. That
is a qualitative shift beyond #1–#5, which all left the primary REST path loaded.

## 8. Cloud pacer — spread dispatches under the hourly refill — PROPOSED (Lever 3 + Lever 4) 🔬

#1–#7 reduce cost *per PR*; #8 is orthogonal — it bounds cost *per hour* by
spacing whole runs across GitHub's hourly budget refill, so an arbitrarily large
backlog (`repos × windows`) drains without any single hour exceeding the ceiling.
Fully async in the cloud: a `cron` **is** the pacing clock — no always-on process,
no in-run waiting. This is the concrete build of `api-rate-limit.md §4` Levers 3
(spread) and 4 (orchestrate).

**What already supports it (verified in
`.github/workflows/ccr-improvement-loop.md`):**
- The workflow is `workflow_dispatch`-able with `repo`, `window_start`,
  `window_end`, `max_prs` inputs — an orchestrator can hand it precisely-sized
  chunks.
- `concurrency: group: ccr-loop-${repo}`, `cancel-in-progress: false` — the
  per-repo group already serializes runs (1 running + 1 pending), so a pacer can
  never stack overlapping runs on the same repo.
- The immutable `pr-*.json` cache makes each dispatch idempotent, so a resumed
  continuation re-processes nothing (#1–#7's cache invariant).

**Design — a second workflow `ccr-pacer.yml`** (`schedule: cron` hourly +
`workflow_dispatch`):
1. Read a **backlog** of `(repo, window)` jobs — a `repos × months` matrix stored
   as a committed JSON file, an Actions artifact, or a repo variable.
2. `gh api rate_limit` → read remaining **primary** budget for the dispatching
   token (free, read-only).
3. If `remaining > threshold`, pop the next 1–N jobs and dispatch the child:
   ```bash
   gh workflow run ccr-improvement-loop \
     -f repo=Azure/azure-sdk-for-python \
     -f window_start=2026-06-01 -f window_end=2026-06-30 -f max_prs=0
   ```
4. Mark those jobs dispatched (update the state file/variable).
5. Exit. Next hour, `cron` fires again and drains the next chunk.

The cron cadence *is* the throttle: one window of ~70 PRs ≈ ~1,000 calls fits one
hour under the 1,000/hr `GITHUB_TOKEN` ceiling (§3), so ~1 window/hour stays under
budget with zero in-run waiting. On an App/PAT (5,000/hr) the pacer can release
~4–5 windows/hour, or one uncapped monthly window.

**Three GitHub-specific blockers (must be designed around):**
1. **The default `GITHUB_TOKEN` cannot trigger another workflow** — events it
   dispatches are suppressed to prevent recursion. The pacer must dispatch the
   child with a **PAT or GitHub App token** (`secrets.PACER_TOKEN`). This aligns
   with Lever 1 (moving off the 1,000/hr ceiling anyway).
2. **Cross-run cache persistence is NOT currently wired** — today `CCR_CACHE` is
   `${{ github.workspace }}/.ccr-cache`, written fresh each run with **no**
   `actions/cache` restore or artifact download. So the "resume with zero rework"
   benefit is only *potential*: it requires adding `actions/cache` (keyed by
   `repo` + window) or artifact upload/download of the `pr-*.json` cache. Until
   that is added, each dispatch re-fetches its window from scratch — the pacer
   still bounds calls/hour, but resume/idempotency across runs does not yet hold.
3. **Do not `sleep` to pace** — in-job sleeps burn runner minutes for nothing.
   Use the cron cadence for spacing; `cron` is imprecise under load, but spacing
   (not precision) is what's wanted.

**Budget note.** If pacer and children share one token they share one budget;
**separate credentials per repo → separate budgets** (Lever 1), so repos stop
competing. Regardless of tier the in-process `Semaphore`/`ghRequestLimiter` (#5)
stays essential — the pacer spaces the *primary* budget across hours, but only the
limiter guards the *secondary* (burst) limits, which dispatch-level pacing can't
see.

**Variant — self-re-dispatch.** The child, on low `/rate_limit` remaining or
hitting `max_prs`, appends its continuation to the backlog (or re-dispatches
itself). Same two prerequisites: the PAT (blocker #1) and persisted cache
(blocker #2).

**Effect:** decouples total backlog size from the per-hour ceiling — the clean
pattern for multi-repo / deep backfill. Composes with #1–#7 (cheaper runs) and
Lever 1 (higher ceiling) rather than replacing them.

---

## Sequencing — STATUS

1. **#1 dead-code deletion** — ✅ DONE (`6+2N` → `6+N`).
2. **#5 global request limiter + header-aware backoff** — ✅ DONE.
3. **#2 bounded parallel fan-out** (on top of #5) — ✅ DONE.
4. **#3 selective commit-detail fetching** — ⛔ DEFERRED: blocked by
   `distinctFiles` coupling + per-commit timing needs; requires a cross-stage
   refactor.
5. **#4 GraphQL migration** — 🕐 DEFERRED: partial win, high effort; revisit only
   if real runs still hit the ceiling.
6. **#6 local-git commit detail** — 🔬 PROPOSED: moves the dominant `N` term off
   the rate-limited path entirely (git transport budget); the structural fix #3/#4
   couldn't reach because both were blocked on patches. Needs partial-clone +
   `fetch-depth: 0` and field-equivalence golden tests.
7. **#7 GraphQL-batched fixed calls** — 🔬 PROPOSED: aliases multiple PRs into
   batched GraphQL requests on the separate point pool; relieves secondary limits.
   Composes with #6 to drive primary-REST cost toward zero. Blocked on the same
   pagination/field-equivalence work as #4; cannot touch `N` (patches).
8. **#8 cloud pacer** — 🔬 PROPOSED: hourly `cron` orchestrator drains a
   `repos × windows` backlog, one chunk/hour under a `rate_limit` check — fully
   async in the cloud. Orthogonal to #1–#7 (bounds cost *per hour*, not per PR).
   Prereqs: a PAT/App token (default `GITHUB_TOKEN` can't trigger workflows) and
   cross-run cache persistence (not currently wired).

Net shipped: fewer requests per PR (#1) **and** those requests now issued
concurrently under a hard, header-aware secondary-rate-limit ceiling (#2 + #5).
Remaining follow-ups (#3, #4) are documented with concrete blockers.

---

## Test Plan

Tooling: `vitest` (`npm test` = `vitest run`; `npm run test:coverage`). Tests
are pure-function unit tests over helpers exported from `scripts/*.ts` with
fixtures under `tests/fixtures/` — no live `gh` calls (see
`tests/fetch-prs.test.ts`). Every change below must keep `npm test`,
`npm run typecheck`, and `npm run lint` green.

### Prerequisite for testability
The fetch layer currently calls `gh` via module-level `ghApiJsonAsync` /
`ghJsonSync`, which unit tests cannot observe. Items #2/#3/#5 require making the
request function **injectable** (pass a `fetchFn`/`gh` client into
`fetchPrToCache`, defaulting to the real one) so tests can substitute a mock
that records call order, arguments, and concurrency. Do this refactor first; it
is behavior-preserving.

### Item #1 — delete `commitPrs` + `commits/{sha}/pulls`
- **Regression (no dead field):** given a mocked PR with commits, assert the
  written payload has **no `commitPrs` key** and that the mock recorded **zero**
  `commits/{sha}/pulls` requests.
- **Call-count guard:** for a PR with *N* commits, assert exactly *N*
  `commits/{sha}` requests and **no** `.../pulls` requests (locks in `6+N`).
- **Fixture cleanup:** remove `commitPrs` from `tests/fixtures/pr-sample.json`,
  `tests/build-judge-input.test.ts`, `tests/attribute-comments.test.ts`; those
  suites must still pass unchanged (proves consumers never read it).
- **Type guard:** `npm run typecheck` fails if any code still references
  `commitPrs` after the `types.ts` field is removed — that *is* the test.
- **Schema:** if `RAW_SCHEMA_VERSION` is bumped, extend `tests/run-schema.test.ts`
  to pin the new value.

### Item #3 — selective commit-detail fetching
- **Fetch-only-what-matters:** mock a PR with 3 commits touching files A/B/C but
  candidate comments only on A; assert `commits/{sha}` details are fetched only
  for commits touching A, and skipped commits incur **zero** detail requests.
- **No-candidate PR:** a PR with no surviving comments fetches **zero**
  `commits/{sha}` details.
- **Accuracy invariant (golden):** run `attribute-comments` + `build-judge-input`
  over a fixture **before and after** the reorder; assert byte-identical output
  (patches + `ccrSawCode` gate unchanged). This is the safety net proving the
  optimization is behavior-preserving.

### Item #5 — global request limiter + header-aware backoff
- **Concurrency ceiling:** drive many requests through the limiter with a mock
  that records max simultaneous in-flight; assert it never exceeds `K`.
- **`Retry-After` honored:** mock a `403` secondary-limit response carrying
  `Retry-After: 2`; assert the limiter waits ~2s (fake timers) rather than the
  fixed exponential value.
- **Primary-budget admission:** mock `GET /rate_limit` with low `remaining`;
  assert the run pauses/defers until reset instead of hammering into a `403`.
- **Backoff regex regression:** keep the existing `utils.ts` retriable-error
  matching covered (rate limit / secondary / abuse / 5xx / timeout).

### Item #2 — parallelize the fan-out
- **Cap respected:** with the injected limiter and a mock recording peak
  concurrency, fan out the fixed + per-commit calls and assert peak in-flight
  ≤ `K` (guards against the `workers × per-worker fan-out` explosion).
- **Equivalence:** assert the cached payload is identical whether calls run
  sequentially or parallelized (ordering of merged arrays must be
  deterministic — sort if needed).
- **No limiter → no parallelism:** a guard/test ensuring parallel fan-out is not
  enabled unless the limiter is present (fail-safe wiring).

### Cross-cutting / manual validation
- **Full suite + coverage:** `npm test` and `npm run test:coverage` after each
  item; no coverage regression on `fetch-prs.ts`.
- **Live smoke (manual, capped):** one real `--max-prs 5` run against a target
  repo with `GET /rate_limit` sampled **before and after**; record actual
  request consumption to validate the `6+N` (and post-#3) model against reality —
  the plan's cost numbers are lower bounds until measured this way.
