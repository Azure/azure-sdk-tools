# Agent Instructions — CCR Improvement Loop

Context another AI agent needs before touching this package, so you don't repeat
research. Read this first, then the doc it points you at for the task at hand.

## What this package is

An **agentic GitHub workflow** (built with [`gh aw`](https://github.com/githubnext/gh-aw))
that measures whether **Copilot Code Review (CCR)** is actually helping a target
repo, and proposes scoped, cited edits to that repo's Copilot instruction files for
human approval. Each run mines a **settled window** of merged PRs, judges human vs.
CCR review comments, emits a `run-*.json`, and files one tracking issue. A static,
zero-backend dashboard plots the trend across runs. A **pacer** drives large
multi-month/multi-repo backfills unattended under an API-budget ceiling.

Design philosophy is **subtractive**: a few defensible numbers, each from a
deterministic fact or an explicit LLM judgment over the real diff — never a proxy
like line-overlap or keyword sentiment. Rationale lives in [`decisions.md`](decisions.md)
(D1–D17); metric definitions in [`metrics.md`](metrics.md).

## Non-negotiable rules

1. **Never modify, stage, or delete `../ccr-improvement-loop-legacy/`.** It is a
   frozen snapshot of the pre-rewrite version, intentionally untracked. It exists
   only as a fallback. Exclude it from every `git add`.
2. **Document every action in [`execution-log.md`](execution-log.md).** This is a
   user requirement. Append a dated note describing what you changed and why after
   each meaningful step.
3. **The workflow source is compiled.** `gh aw` compiles `ccr-improvement-loop.md`
   into `ccr-improvement-loop.lock.yml`. Any edit to the `.md` **must** be followed
   by a recompile and a commit of **both** files (see "Compile discipline").
4. **Keep work uncommitted / local unless explicitly asked.** Do not push to
   `origin`/upstream. The live test target is the `trial` remote only.

## The two workflows (and where their source lives)

Both live at **repo-root `.github/workflows/`**, NOT in this subdir:

- **`ccr-improvement-loop.md`** → **`ccr-improvement-loop.lock.yml`** — the loop
  itself. gh-aw source (`.md`) + compiled reusable workflow (`.lock.yml`).
  Triggers: `schedule` (weekly Mon), `workflow_dispatch`, and `workflow_call`
  (the pacer calls the lock). All three feed the **same** Phase-1 resolver, so the
  same `(repo, window, cohort)` resolves to one identical `windowId`.
  Dispatch inputs: `repo`, `window_start`, `window_end`, `max_prs` (all optional).
- **`ccr-pacer.yml`** — hand-written **plain YAML** (do **NOT** `gh aw compile`
  it). Hourly `cron` (`17 * * * *`) + `workflow_dispatch` (`dry_run` input).
  Jobs: `plan` (runs `pace.ts`) → `backfill` (matrix `uses:` the lock, one leg per
  admitted window) → `ledger` (reconcile + persist). Reads `pacer/config.json`.

This subdir (`.github/aw/ccr-improvement-loop/`) holds all the **scripts, tests,
configs, references, docs, and the dashboard** the workflows consume.

## Tech stack & how the code runs

- **Pure ESM TypeScript, NO build step.** `.ts` files are run **directly by Node**
  via built-in type stripping. Local Node is v25; **GitHub Actions runs Node 24**.
  Requires Node ≥ 24. `package.json` has `"type": "module"`.
- Scripts are invoked as `node ./scripts/<name>.ts --flags ...` — each is a small
  CLI with explicit args (no hidden globals).
- **pnpm** `10.12.4` (`packageManager` pinned). Only real dependency is `zod`
  (schema validation); everything else is devDeps (eslint, prettier, vitest, tsc).
- Dashboard is **vanilla JS ESM** (`dashboard/js/*.mjs`) — no framework, no bundler.

## Gates — run from THIS directory (`.github/aw/ccr-improvement-loop/`)

```bash
pnpm run typecheck && pnpm run lint && pnpm run format:check && pnpm test
```

- `typecheck` = `tsc --noEmit`; `lint` = `eslint .`; `format:check` = `prettier --check .`;
  `test` = `vitest run`. Last known-green: **199 vitest tests**.
- **Dashboard tests are separate** (Node's built-in runner, not vitest):
  ```bash
  node --test dashboard/js/aggregate.test.mjs   # 13 tests
  ```
- **Prettier's `format:check` ignores `*.json`** (see `.prettierignore`) but covers
  `.md`/`.html`. Format JSON by hand.
- `pnpm run lint:fix` / `pnpm run format` auto-fix.

## Compile discipline (loop only)

```bash
cd /home/jennypeng/azure-sdk-tools        # MUST be repo root, not this subdir
gh aw compile ccr-improvement-loop        # regenerates the .lock.yml
```

- Run from **repo root** — the `.md`/`.lock.yml` live at repo-root
  `.github/workflows/`, not here.
- Commit **both** the `.md` and the `.lock.yml` together.
- `gh aw` should report **0 errors / 0 warnings**.
- `actionlint` (`~/go/bin/actionlint`) flags `copilot-requests` as an "unknown
  scope" — its scope list is **stale**; GitHub requires that permission. Non-fatal,
  ignore it.

## The loop pipeline (what a run does)

Defined in `ccr-improvement-loop.md`. Three stages:

1. **Deterministic prep** (custom steps, normal network, only `GITHUB_TOKEN`) —
   `prep-run.ts` orchestrates: `fetch-prs` → `classify-pr` → `filter-comments` →
   `attribute-comments` → audit. Produces a normalized cache under `.ccr-cache`
   and `.ccr-runs`. No agentic compute here. No-ops below **50 settled PRs**;
   refuses fork/mirror targets; windows are settled-only (`end ≤ today − 14d`).
2. **Agent phase** (read-only Copilot engine) — classifies PRs the rules couldn't
   type, judges comments against the real diff using the **pinned prompts** in
   `references/*.prompt.md`, clusters themes, computes metrics
   (`emit-run-json.ts`), and files **one** tracking issue. All writes routed
   through gh-aw **safe-outputs** (create-issue + upload-artifact) — no direct file
   writes. `copilot-requests: write` lets the default Copilot engine auth with the
   workflow token.
3. **Terminal-outcome post-step** (runs after the agent, always) —
   `emit-outcome.ts` derives one terminal outcome (`success`/`noop`/`failed`) from
   observable signals (settled PR count, whether the agent called `noop`, whether
   run-JSON was produced, job status) and writes/uploads
   `.ccr-runs/outcome-<windowId>.json`. **The pacer consumes this artifact** as the
   window's completion signal.

## The pacer, ledger, and state branch

- `pace.ts plan --config pacer/config.json --state <dir> [--dry-run]` derives the
  pending backlog from `start_date`+`granularity`, runs a **starvation guard**
  (`assertFittable` — throws if a window's fixed cost estimate exceeds the pool
  limit minus safety margin), and **admits** windows: **≤ 1 per repo per tick** and
  **≤ `max_windows_per_tick`** total. Emits a matrix the `backfill` job expands.
- **State lives on a dedicated `ccr-pacer-state` branch**, dir `ledger/`, one JSON
  per window: `{ window_id, repo, window_start/end, cohort, outcome, attempt,
run_id, artifact_name, ts }`. The `ledger` job downloads each leg's outcome
  artifact, reconciles, and commits records. **A missing outcome artifact is read
  as `failed`.**
- **Retry semantics:** `retry_cap: 1` → attempt 1 fails → retried as attempt 2 →
  second fail → permanent `skipped.json`. A deleted ledger file re-plans that
  window fresh (attempt 1). The `pr-*.json` cache makes re-dispatch nearly free.
- Cost model (`pacer/config.json` + `pacer-schema.ts`): `est_rest_calls_per_window`
  ~1200, `est_graphql_points_per_window` ~2000, `safety_margin` 500, `retry_cap` 1.

## Config files

- **`config.json`** — the loop's analysis config: `ccrLogins`, `automationLogins`,
  `excludedPaths`, `minPrs` (50), `windowLagDays` (14), `ccrEnabledSince`.
- **`pacer/config.json`** — the pacer: `repos`, `start_date`, `granularity`
  (`month`), `max_prs` (0 = uncapped), `max_windows_per_tick` (**1** = serialize
  legs so parallel cohorts don't race the shared rate limit).

## Known gotchas (hard-won — don't rediscover these)

- **`actions/upload-artifact@v4` excludes hidden/dot-dir files by default.** Paths
  under `.ccr-runs` silently upload nothing unless the step sets
  `include-hidden-files: true`. This once made the pacer read every window as
  `failed`. If you add an artifact upload of anything under a dot-directory, set it.
- **`pre_activation` fails closed under rate-limit exhaustion.** gh-aw's repo
  permission check makes an API call; when the shared **GitHub App installation**
  budget is drained (e.g. by a prior full agent run), that call throws
  `API rate limit exceeded for installation`, so `activated=false` and the whole
  agent phase is **skipped** — while the overall run still reports `success`
  (skipped-by-design isn't a failure). Symptom: activation/agent/detection jobs
  show "in 0s" and dashes. This is transient infra, not a code bug; the installation
  budget recovers hourly.
- **Parallel legs share one installation rate-limit budget.** The backfill matrix
  runs `fail-fast: false`; two uncapped month cohorts in parallel can exhaust the
  shared budget mid-fetch (HTTP 403). The preflight guard checks each run
  independently vs the ceiling, so it can't see the aggregate. Mitigation in place:
  `max_windows_per_tick: 1` serializes to one leg per hourly tick.
- **A caller job that `uses:` a reusable workflow must grant a _superset_ of every
  nested job's permissions**, or GitHub rejects at load (`startup_failure`). The
  loop's union is `actions:read, contents:read, copilot-requests:write,
issues:write, pull-requests:read` — the pacer's `backfill` job grants all of it.
- **The `emit-outcome` post-step uses `set -uo pipefail` (no `-e`)**, so a thrown
  script can still show a ✓. Check its logged output, not just the step status.
- In-Actions token budgets on the trial repo: **5000/hr core + 5000 graphql + 30
  search** (higher than an early handoff feared).

## Trial / live-test environment

- Live test repo: **`JennyPng/gh-aw-trial`** (personal, public), git remote
  **`trial`**, default branch `main`. Pacer state on branch `ccr-pacer-state`.
- Dispatch the pacer: `gh workflow run ccr-pacer.yml --repo JennyPng/gh-aw-trial`
  (`dry_run` defaults false). Dry-run first to reconfirm admission counts.
- Inspect a run: `gh run view <id> --repo JennyPng/gh-aw-trial`; watch:
  `gh run watch <id> --repo JennyPng/gh-aw-trial --exit-status --interval 30`.
- Read the ledger: `gh api repos/JennyPng/gh-aw-trial/contents/ledger/<file>.json?ref=ccr-pacer-state -q .content | base64 -d`.

## Dashboard

Static, zero-backend. Aggregates `run-*.json` into trend charts. Drop new runs in
`dashboard/data/`, regenerate the file index the browser can't list with
`node scripts/gen-manifest.ts --data-dir dashboard/data`, then
`python3 -m http.server --directory dashboard`. Pages publishing: `dashboard/README.md`.

## Key scripts (in `scripts/`)

| Script                                                      | Role                                                                    |
| ----------------------------------------------------------- | ----------------------------------------------------------------------- |
| `prep-run.ts`                                               | Orchestrates deterministic prep (fetch→classify→filter→attribute→audit) |
| `fetch-prs.ts` / `pr-graphql.ts` / `gh-client.ts`           | Fetch + assemble PR/comment data                                        |
| `classify-pr.ts`                                            | Deterministic PR typing (rules); leaves `needs-agent` for the LLM       |
| `filter-comments.ts` / `attribute-comments.ts`              | Normalize + attribute review comments                                   |
| `build-judge-input.ts`                                      | Assemble the judge prompt input                                         |
| `compute-metrics.ts` / `pr-metrics.ts` / `emit-run-json.ts` | Metrics → `run-*.json`                                                  |
| `emit-outcome.ts`                                           | Terminal outcome → `outcome-<windowId>.json` (pacer signal)             |
| `resolve-window.ts` / `window-id.ts`                        | Settled-window + deterministic windowId logic                           |
| `pace.ts` / `pacer-schema.ts`                               | Pacer planner, starvation guard, admission                              |
| `backlog.ts` / `gen-backlog.ts`                             | Backlog derivation                                                      |
| `aggregate-runs.ts` / `gen-manifest.ts`                     | Dashboard data prep                                                     |
| `run-schema.ts` / `migrate-run-schema.ts`                   | `run-*.json` zod schema + migration                                     |

## Doc index

- [`README.md`](README.md) — concise "what + how to onboard" (pacer for backfill).
- [`metrics.md`](metrics.md) — full metric definitions, validity, honest reading.
- [`decisions.md`](decisions.md) — every design decision, D1–D17.
- [`api-rate-limit.md`](api-rate-limit.md) — API budget, tiers, scaling levers.
- [`references/*.prompt.md`](references/) — pinned agent prompts + closed label
  vocabulary (`controlled-vocabulary.md`). Changing these changes judgments —
  treat as versioned.
- [`how-it-works.html`](how-it-works.html) — self-contained ground-up walkthrough.
- [`execution-log.md`](execution-log.md) — running log of changes (append to it).
