# GitHub API Rate Limits — CCR Improvement Loop

A consolidated reference for everything we've explored about GitHub API rate
limits in this workflow: where the pressure comes from, how GitHub meters us,
what we've already shipped, and how to scale backfill across many repos.

Companion doc: [`optimize-api-call-plan.md`](./optimize-api-call-plan.md) tracks
the concrete optimization items (#1–#5) and their status.

---

## 1. Where the load comes from

The rate-limited work all lives in the **deterministic prep** steps (not the
agent): `scripts/fetch-prs.ts` (`fetchPrToCache`) hits the GitHub API for every
mined PR. Per PR it issues:

- **5 fixed REST calls:** `pulls/{n}`, `.../reviews`, `.../comments`,
  `issues/{n}/comments`, `.../commits`
- **1 GraphQL call:** thread resolution + linked issues (`fetchThreadResolution`)
- **1 REST call per commit:** `commits/{sha}` (files/patches)
  - _(historically 2/commit — the second, `commits/{sha}/pulls`, was removed; see
    §5 item #1)_

**Cost model:** ≈ **`6 + N`** endpoint invocations per PR (`N` = commits), down
from `6 + 2N`.

> ⚠️ These are **endpoint invocations, not HTTP requests.** Every async call uses
> `gh api --paginate`, so reviews/comments/commits (and commits touching >300
> files) can each span multiple requests, and retries add more. Treat `6 + N` as
> a **lower bound**; validate real consumption with `GET /rate_limit`.

**Observed scale (Azure/azure-sdk-for-python, 2026 merged PRs/month):**

| Jan | Feb | Mar | Apr | May | Jun | Jul* |
| --: | --: | --: | --: | --: | --: | ---: |
| 178 | 267 | 296 | 282 | 326 | 288 | 230* |

`*` July = month-to-date. So a full month ≈ **~280–330 PRs**, i.e. **~4,000+**
endpoint invocations for an uncapped month.

---

## 2. How GitHub meters us: two limits, not one

### Primary rate limit — the hourly _budget_

A per-hour quota that **refills each hour**. Exceeding it returns `403` with
`X-RateLimit-Remaining: 0` and a reset timestamp. Visible any time via the free
`GET /rate_limit`. REST and GraphQL have **separate** primary pools — REST is
metered in **requests**, GraphQL in **points** (see §3a).

### Secondary rate limit — the _behavior_ throttle

An **anti-abuse governor independent of the quota**. Even with thousands of
requests remaining, GitHub rejects you for _how_ you call:

- too many **concurrent** requests (guidance: keep well under ~100),
- too many requests/points **per minute** (bursts),
- CPU-heavy GraphQL, rapid content creation (N/A — we're read-only).

Returns `403`/`429`, often with a **`Retry-After`** header. **Key consequence:**
`GET /rate_limit` shows only the _primary_ budget, so it can read "plenty
remaining" while you're being throttled by a _secondary_ limit. Enterprise tier
(see §6) raises primary limits but **does not** raise secondary limits.

---

## 3. The decisive factor: which token authenticates

The prep steps run with `GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}`
(`.github/workflows/ccr-improvement-loop.md`). The workflow _comment_ says calls
are bounded "under the App installation rate limit," but the implementation uses
the **default Actions `GITHUB_TOKEN`** — a gap that sets the real ceiling.

| Auth method                                               | Primary limit (REST)                |
| --------------------------------------------------------- | ----------------------------------- |
| **`GITHUB_TOKEN`** (default Actions), non-enterprise repo | **1,000 / hour / repo**             |
| `GITHUB_TOKEN`, GitHub Enterprise Cloud (GHEC) repo       | **15,000 / hour / repo**            |
| GitHub App installation                                   | 5,000 / hour (→ **15,000** on GHEC) |
| Personal Access Token (PAT)                               | 5,000 / hour / user                 |

Source: [GitHub REST API rate-limit docs](https://docs.github.com/enterprise-cloud@latest/rest/overview/rate-limits-for-the-rest-api).

Because the workflow repo (`gh-aw-trial`) is **personal**, the effective ceiling
is **~1,000 req/hr** — consistent with runs failing at ~70 PRs (~1,000 calls).
**This is the single highest-leverage variable for scale.**

---

## 3a. GraphQL: a _separate_ pool, metered in points

After #7 (batched GraphQL fixed reads) the five fixed per-PR REST reads move onto
the GraphQL pool, which is **independent of the REST budget** and generally ample.

| Auth method                                               | Primary limit (GraphQL)                           |
| --------------------------------------------------------- | ------------------------------------------------- |
| **`GITHUB_TOKEN`** (default Actions), non-enterprise repo | **1,000 points / hour / repo**                    |
| **`GITHUB_TOKEN`, GHEC (enterprise-account resources)**   | **15,000 points / hour / repo**                   |
| GitHub App installation, non-GHEC                         | 5,000 pts/hr (+50/repo >20 repos, cap **12,500**) |
| GitHub App / OAuth owned by a GHEC org                    | **10,000 points / hour**                          |
| Personal Access Token (PAT) / user                        | 5,000 points / hour / user                        |

Source: [GitHub GraphQL rate-limit docs](https://docs.github.com/en/graphql/overview/rate-limits-and-query-limits-for-the-graphql-api).
Note this **matches** the REST ceiling for GHEC `GITHUB_TOKEN` (15k) — it is **not**
a ~5k pool; 5k is the PAT/user number.

**Point cost ≠ 1/query.** GitHub's formula: sum the requests needed to fulfill
every connection (assume each hits its `first`/`last`; parent connections
_multiply_ children), divide by 100, round, minimum 1. Read the exact number back
via `rateLimit { cost }`. For our batched fixed-reads query,
`points ≈ round(B × (6 + T) / 100)` where `B` = PRs per batch and
`T = reviewThreads first:`. Concretely: 1 PR ≈ **1 pt**, 20 PRs (tuned `T=20`) ≈
**~5 pts**, 20 PRs (worst `T=100`) ≈ **~21 pts**; a whole uncapped month (~300 PRs)
≈ **~75–330 points** — ~2% of a 15k/hr pool. **Primary points do not bind.**

**What actually binds on GraphQL** (tune batch width against these, not points):

- **500,000-node hard cap _per query_.** Nesting explodes node count —
  `B × T × comments/thread` (e.g. 20 PRs × 100 threads × 100 comments = 200k nodes
  on one connection). Cap `B`, `reviewThreads first:`, and inner `comments first:`
  so worst-case nodes stay well under 500k.
- **Secondary limits** (§2), separate from the primary points and **not** raised by
  enterprise: **2,000 GraphQL points/min**, **≤60 CPU-s/min for GraphQL**, **≤100
  concurrent** (shared REST+GraphQL). This is the `ghRequestLimiter` (#5) domain.

---

## 4. Levers for scaling (multi-repo backfill)

The core problem at scale: **one shared per-workflow-repo budget**, and every
target repo draws from it. Four independent levers:

### Lever 1 — Raise the ceiling (auth)

- Swap `GH_TOKEN` to a **GitHub App installation token** (5,000/hr; 15,000 on
  GHEC) or **PAT** (5,000/hr). 5–15× more budget, no fetch-logic change.
- **Separate credentials per repo → separate budgets** (repos stop competing).
- On GHEC, the plain `GITHUB_TOKEN` already gives 15,000/hr — often no custom App
  needed (see §6).

### Lever 2 — Cut calls per PR (cheaper work)

- **#3 selective commit-detail:** fetch `commits/{sha}` only where needed —
  shrinks the dominant `N` term. (Deferred; blocked by a `distinctFiles`
  coupling — see `optimize-api-call-plan.md`.)
- **#4 GraphQL migration:** move reviews/comments/threads onto the **separate
  GraphQL point budget** — effectively a _second_ pool, ~doubling capacity.
  Per-commit patches stay on REST. (Deferred; connections still paginate.)

### Lever 3 — Spread work under the hourly refill

- **Weekly windows** instead of monthly: ~70 PRs ≈ ~1,000 calls/week, fits one
  hour, and clears the **≥50-settled-PR** artifact threshold for high-volume
  repos. Same total volume, amortized across separate hourly budgets — **only
  works if dispatches are spaced across hours**, not fired back-to-back.
- **Budget-aware, resumable backfill:** the **immutable `pr-cache` already makes
  fetch idempotent**, so a run can check `GET /rate_limit`, work until budget is
  low, then re-dispatch itself next hour and **resume from cache with zero
  rework**. Repo-count-agnostic; the clean pattern for large backfills.

### Lever 4 — Orchestrate multi-repo backfill

- A **central pacer** that releases `(repo, window)` jobs so aggregate calls/hour
  stay under budget (e.g. one window/hour, interleaved across repos). The per-repo
  `concurrency` group (`cancel-in-progress: false`, 1 running + 1 pending) already
  serializes within a repo.
- **Partition** the backlog as a `repos × months` matrix, drained by the pacer +
  resume logic so no single hour exceeds the ceiling.

**Cross-cutting enabler:** the cache underpins Levers 3 & 4 — every re-dispatch is
idempotent and resumable.

---

## 5. What we've already shipped

See `optimize-api-call-plan.md` for details and test coverage.

- **#1 — Deleted the unused `commits/{sha}/pulls` call + `commitPrs` field.**
  Written but never read; per-PR cost `6+2N` → `6+N`.
- **#5 — Process-wide request limiter + header-aware backoff** (`scripts/utils.ts`):
  `Semaphore` + `ghRequestLimiter` (default 8, `CCR_GH_MAX_CONCURRENCY` override)
  bound concurrent REST+GraphQL requests; `parseRetryAfterMs` makes retries honor
  `Retry-After`; new async `ghApiGraphqlAsync`. **Directly targets secondary
  limits**, which enterprise tier never relaxes.
- **#2 — Bounded parallel fan-out** (`scripts/fetch-prs.ts`): per-PR reads and
  per-commit detail fetches run under `Promise.all`, all admitted through the
  shared limiter so `workers × fan-out` can't exceed the ceiling.
- **#4 — Per-PR fixed reads migrated to GraphQL** (`scripts/*graphql*`,
  `fetch-prs.ts`): the fixed 5 REST reads per PR now ride the **separate GraphQL
  point pool**, cutting per-PR primary-REST from `6 + N` → `1 + N`. Points don't
  bind (a month ≈ a few hundred), so this frees the binding REST budget for the
  shared, 6+-repo case.
- **Backfill pacer** (`scripts/pace.ts`, `.github/workflows/ccr-pacer.yml`): an
  hourly `cron` orchestrator that admits `≤ 1 window/repo` per tick against a
  static `core`/`graphql`(/search) budget check, dispatches the child loop, and
  reconciles outcomes into a durable `ccr-pacer-state` ledger — a `repos × months`
  backfill drains unattended, never exceeds a ceiling, and resumes with zero
  rework. A plan-time `assertFittable` invariant guarantees no window defers
  forever. See §6/`decisions.md` D15.

**Deferred (tiered, evidence-gated):** **#7** cross-PR batching (failure-isolation
cost > the secondary-limit win the #5 limiter already covers), **T1** per-repo
App-installation token budgets (the primary N-repo lever — config, not code), and
**#6** git-patch `N`-removal (highest-maintenance). Revived only on a real
saturation / binding-REST reading. See `decisions.md` D16/D17.

---

## 6. Enterprise (GHEC / GHES) implications

Moving the **workflow repo** into an enterprise org changes the ceiling:

**GitHub Enterprise Cloud (GHEC):**

- `GITHUB_TOKEN`: **1,000 → 15,000 req/hr/repo** — a 15× jump, no code/auth change.
- Often **removes the need for a custom App** (plain token already ≥ App limit).
- Uplift is tied to **enterprise ownership of the token/workflow repo**, not the
  target repo — reading public repos still gets 15k.
- ~3 full month-repos/hr under one budget; uncapped monthly per repo becomes easy.

**GitHub Enterprise Server (GHES, self-hosted):**

- Admins can **raise or disable** rate limits entirely — ceiling may be far higher
  or effectively unlimited. Confirm with the instance admin.

**Unchanged by enterprise (still require our work):**

- **Secondary/burst limits** — concurrency and points-per-minute throttles are
  _not_ raised. The limiter (#5) stays essential.
- **Shared, finite, per-workflow-repo budget** — at large N, still need weekly
  windows + resumable pacing.
- **Search API:** 30 requests/minute (the per-window PR listing). Never the
  bottleneck, but don't parallelize searches.

> First thing on enterprise: confirm the real number with `GET /rate_limit` from
> inside the workflow — it varies by GHEC vs GHES and org config.

---

## 7. Recommended path

1. **Now / near-term:** upgrade auth (App token, or land on a GHEC-owned repo) to
   move off the 1,000/hr `GITHUB_TOKEN` ceiling. Likely makes uncapped monthly
   viable per repo.
2. **Keep** the concurrency limiter (#5) regardless of tier — it's the only guard
   against _secondary_ limits.
3. **For multi-repo / deep backfill:** use the **backfill pacer** (`ccr-pacer.yml`)
   — a `/rate_limit`-aware, resumable hourly loop over the derived backlog that
   rides the existing cache. **Shipped.** Scales to arbitrary repo counts because
   it self-throttles instead of relying on a cap.
4. **Further REST headroom, tiered & evidence-gated:** **#4 (GraphQL)** is
   **shipped** (fixed reads moved to the point pool). The next lever if REST
   actually saturates is **T1 per-repo App-installation token budgets** (config,
   not code); **#7** cross-PR batching and **#6** git-patch `N`-removal stay
   deferred decision points (see `decisions.md` D16/D17).

---

## 8. Quick reference

- **Verify budget:** `gh api rate_limit` (free; shows primary REST + GraphQL +
  search pools; does **not** reflect secondary limits). GraphQL `remaining` is in
  **points**, not requests.
- **Concurrency override:** `CCR_GH_MAX_CONCURRENCY` (default 8).
- **Cap a window:** dispatch input `max_prs` (`0`/empty = uncapped full cohort).
- **REST cost rule of thumb:** `~ (6 + N) × PRs` endpoint invocations, `N` = avg
  commits/PR; after #7 the fixed 5 move to GraphQL → `~ (1 + N) × PRs` on REST;
  multiply by pagination factor for a true request estimate.
- **GraphQL cost rule of thumb:** `points ≈ round(B × (6 + T) / 100)` per batched
  query (`B` = PRs/batch, `T = reviewThreads first:`); a month ≈ ~75–330 pts.
  Points don't bind — watch the **500k-node/query cap** and **secondary** (2k
  pts/min, 60 CPU-s/min, 100 concurrent) limits instead. Read actual cost via
  `rateLimit { cost }`.
- **No-op threshold:** windows with < 50 settled PRs emit **no artifact**.
