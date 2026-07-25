# Deferred Work & Scope Decisions — CCR Improvement Loop Polish & Scale

Companion to [`implementation-plan.md`](./implementation-plan.md). The plan itself
covers **only what is being built** (Phases 0–5). This doc holds everything that is
**deliberately not built now**: deferred sub-tracks, decision points, scrapped
alternatives, and the live-proof deferrals that wait on the GHEC token. Nothing
here is a gate; the plan links back to the relevant section where the boundary
matters.

---

## REST-minimization tiers (the T1/T2/T3 framing)

The per-*workflow*-repo budget is **shared across all target repos** (15,000/hr on
GHEC), so it does **not** grow as repos are added. REST minimization therefore
matters for the multi-repo **backfill burst**, not steady state (~24k calls/mo at
6 repos ≈ 0.2% of budget). The old monolithic "C1 near-zero-REST" bundle was split
into three tiers by leverage-per-maintenance:

| Tier | Lever | Status | Where |
|---|---|---|---|
| **T1** | Per-repo App-installation credentials — each repo gets its _own_ budget | **Decision point** (`decisions.md` D17), not built | [below](#t1--per-repo-credential-scaling-decision-point) |
| **T2** | #4 per-PR GraphQL fixed reads (fixed 5 REST reads → separate point pool) | **Active — Phase 2** | plan Phase 2 |
| **T2-batch** | #7 cross-PR GraphQL aliasing/batching | **Deferred** | [below](#7-t2-batch--cross-pr-graphql-aliasingbatching) |
| **T3** | #6 git-derived commit patches (removes the dominant `N` term) | **Deferred/optional** | [below](#6-t3--commit-detail-from-local-git-removes-n) |

Steady-state needs none of these; backfill wall-clock is where T2/T3 help.

---

## Deferred sub-track — #7 (T2-batch): cross-PR GraphQL aliasing/batching

**Not built in this plan.** Aliasing many PRs into one GraphQL query would cut HTTP
request count (fewer secondary points — 1/request — and fewer round trips, ~10–12%
of remaining invocations). Deferred because:

- **Its benefit is not needed at the scheduled workload** — the #5 limiter keeps
  concurrency well under the 100-shared ceiling, and secondary points (2,000/min)
  are nowhere near binding at the pacer's per-hour repo-month rate.
- **Per-PR isolation is the stronger property** — small retry blast radius, natural
  per-PR checkpoint/resumability, and no exposure to GitHub's large-query resource
  limits (which can return partial results / trip the 500k-node cap or the per-query
  CPU guard). Cross-PR batching adds alias/cursor bookkeeping, batch-splitting, and
  selective-retry correlation for a throughput gain that isn't a current requirement.
- Its point effect is **shape-dependent** (batching amortizes the per-query floor
  for cheap queries but can slightly increase cost when per-PR tally >~100), so it
  is not even a reliable budget win.

**Revive only if** a measurement after #4 (elapsed time, limiter queue wait, actual
secondary consumption) shows HTTP-request count is the binding constraint — then add
bounded micro-batching (10–25 PRs) behind the seam preserved in Step 2.1, with
node-limit-aware splitting and per-alias cursor handling.

> The plan **preserves the enabling seam**: Step 2.1's query builder can accept a
> small alias set later without implementing general batching now.

---

## Deferred sub-track — #6 (T3): commit detail from local git (removes `N`)

**Not built in this plan.** Replacing the per-commit `GET /commits/{sha}` loop
with a local **blobless partial clone** (`git clone --filter=blob:none`, git
transport = zero REST/GraphQL pool) would remove the dominant `N` term and drive
per-PR primary-REST cost to ~`1`. It is deferred because it carries the largest
maintenance surface in the design: patch-normalization equivalence across
**merge** (`git diff-tree -m --first-parent`), **rename** (`-M`), **root** (empty
tree), **binary/oversized** (GitHub omit/truncate emulation), **timestamp**
offset, and **fork/unreachable SHA** (`refs/pull/<n>/head`) cases, plus a second
git object cache and clone-bandwidth benchmarking.

**Revive only if,** after T1 (per-repo credentials) and T2 land, a live
`rate_limit` reading still shows REST as the binding constraint at the target
repo/window/backfill scale — i.e. heavy, frequently-repeated multi-repo-year
backfill. If revived: build the `attributed.json` patch-layer golden oracle first
(Phase 0 step 4, out-of-scope layer), then follow the design preserved in
`optimize-api-call-plan.md §6` and hold normalized downstream evidence
(`files`/`distinctFiles`, `committedAt` ordering, `ccrSawCode`) byte-identical.

---

## T1 — per-repo credential scaling (decision point)

`decisions.md` D17. The workflow-repo budget is **shared across all target repos**,
so it does not grow with repo count — the highest-leverage N-repo lever
(`api-rate-limit.md §4 Lever 1`). If/when onboarding pushes the shared 15k/hr pool
near saturation (backfill drain unacceptably long, or steady-state at large N), give
each target repo its **own** budget via a per-repo **GitHub App installation token**
selected by `pace.ts`/the child from repo→credential config. This is a
**config/auth change, not fetch-logic code**, and composes with #4. It is a
**decision point, not built now** (the current no-new-token constraint holds until a
real saturation reading justifies it).

> **Active constraint the plan keeps:** `admit` must read **per-pool** budgets so a
> future per-repo token simply widens `remaining` without code changes. This is
> built and tested in Phase 4 (Gate 4 "widened `remaining`" test), even though the
> token itself is not introduced.

---

## Deferred live proofs (need the GHEC token)

The 15,000/hr GHEC token is not available yet. Every plan gate is satisfied by
**offline mock-`gh`/fixture tests only**. The following live-budget proofs are
**deferred and non-gating** — do not add any test that requires the real token to
pass. They are the `_DEFERRED — needs GHEC token_` / `_Deferred (GHEC)_` tags that
appear beside individual gates and in the milestone map:

- **M1:** one uncapped month completes without a 403; a repeated window is all hits.
- **M2:** live no-403 run with the fixed 5 REST reads on the GraphQL pool.
- **M3:** live run-JSONs produced end-to-end.
- **M4:** a `repos × months` campaign drains across ticks (no 403, no PAT); a
  mid-backfill restart re-derives and resumes; a poison/thin window doesn't wedge.

All four are already **proven offline** via the determinism, cache, workflow-boundary,
two-tick, and reconciliation tests defined in the plan gates.

---

## Scrapped decisions (scope review)

Cut to keep the system low-maintenance while still meeting every success
criterion (N-repo dispatch, 100% PR coverage over the window, no rate-limit
errors, easy onboard/backfill, ongoing cadence, aggregatable dashboard).

- **Monolithic "C1 near-zero-REST" → split into three tiers (T1/T2/T3) by
  leverage-per-maintenance (revised after a 6+-repo scale review).** The
  workflow-repo budget is **shared across all target repos** (`api-rate-limit.md
  §4`), so it does **not** grow with repo count — making REST minimization matter
  for the multi-repo _backfill burst_ (steady-state at 6 repos is ~24k calls/mo ≈
  0.2% of budget, so it needs none of this). Resolution:
  - **T2 = #4 per-PR GraphQL fixed reads → KEPT (active Phase 2); #7 cross-PR
    batching → DEFERRED.** #4 is the budget-relevant lever: it moves the fixed 5
    REST reads onto the separate point pool (per-PR primary-REST `6 + N → 1 + N`),
    with no git-clone equivalence minefield. #7 (aliasing many PRs into one query)
    only trims HTTP count + secondary points the **#5 limiter already covers**, and
    pays for it in **failure isolation** (one 502/node-cap failure re-fetches the
    whole batch; per-PR checkpointing is lost). A micro-batch seam is preserved so
    #7 can be added later if a measurement ever justifies it.
  - **T1 = per-repo App-installation credentials → decision point (D17), not
    built.** The **highest-leverage** N-repo lever (each repo gets its own budget;
    repos stop competing) — but a config/auth change, deferred behind the current
    no-new-token constraint until a real saturation reading justifies it. `admit`
    is written to read per-pool budgets so a future token just widens `remaining`.
  - **T3 = #6 git-derived commit patches (removes `N`) → DEFERRED/optional.** The
    biggest _rate_ win (git transport = zero pool) but the **largest maintenance
    surface**: patch-normalization equivalence across
    merge/rename/binary/oversized/root/fork commits + a second git object cache +
    clone benchmarking. Revive only on a live `rate_limit` reading showing REST
    still binds after T1/T2, i.e. heavy repeated multi-repo-year backfill.
- **Golden-oracle equivalence harness → scoped to #4 (was the full C1 apparatus).**
  Phase 0 builds only the per-PR fixed-read layers (reviews/comments/threads/linked
  issues) that #4 touches. The heavier commit `files`/patch layer
  (`attributed.json`, merge/rename/binary/root/fork) is **not built** unless T3/#6
  is revived. `canonicalizeRun` (clock freeze) stays regardless.
- **Predictive multi-resource admission model → replaced by a static two-pool
  budget check.** Dropped `estimateCost` count-probes, per-connection cardinality
  feedback, `estimates.json`, cold-start upper bounds, serial-lane scheduling, and
  drain-time thresholds. Replaced with fixed conservative per-window estimates
  (REST + GraphQL points) + `≤ 1 window/repo` + `max_windows_per_tick`; anything
  that doesn't fit is picked up next cron tick (the cache makes every re-dispatch
  idempotent). Far less code, no tuning knobs, and it can't 403 because it admits
  under a conservative bound on **both** metered pools.
- **`estimates.json` + outcome `cardinalities` field → removed.** Only existed to
  feed the predictive estimator. Outcome artifacts keep an observed `cost` field
  for `--dry-run`/log visibility only.
