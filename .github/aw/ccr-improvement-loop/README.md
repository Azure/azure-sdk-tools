# CCR Improvement Loop

An agentic workflow that measures whether **Copilot Code Review (CCR)** is
actually helping on a repo, then feeds the answer back as Copilot instruction-file
rule proposals for human approval. Each run mines a settled window of merged PRs,
judges human vs. CCR review comments, and emits a `run-*.json`; a static dashboard
plots the trend across runs.

The design is **subtractive** — a few numbers we can defend, each from a
deterministic fact or an explicit LLM judgment over the real diff, never a proxy
like line-overlap or keyword sentiment. The rationale lives in
[`decisions.md`](decisions.md); this file is just how to run it.

## What you get

| Metric                                           | Question                                         |
| ------------------------------------------------ | ------------------------------------------------ |
| `humanReviewBurden`                              | Are humans doing less review work?               |
| `addressedRate` / `rejectedRate` / `ignoredRate` | Are CCR's comments useful? (by severity)         |
| `ccrRecallRate`                                  | Of issues humans caught, how many did CCR catch? |
| `ccrCoverage`                                    | What share of eligible PRs did CCR review?       |
| `bugFixPrRate`                                   | Merged-bug trend (proxy, watch alongside recall) |

Read every number as a **trend across runs**, never a single-window verdict;
denominators below `n ≥ 5` are suppressed as low-confidence. Full definitions,
validity, and limits are in [`metrics.md`](metrics.md).

## Onboarding

The workflow measures **one target repo per run** and is dispatchable by the
`repo` input. GitHub runs the compiled `ccr-improvement-loop.lock.yml`, so any edit
to [`ccr-improvement-loop.md`](../../workflows/ccr-improvement-loop.md) must be
followed by `gh aw compile` and a commit of **both** files.

**1. A single recent window** (smoke test / weekly diagnostic):

```bash
gh workflow run ccr-improvement-loop --repo <workflow-repo> \
  -f repo=Azure/azure-sdk-for-python
```

**2. Backfill history — use the pacer (recommended).** To build a trend over many
months and/or repos, don't dispatch by hand — run the **pacer**
([`ccr-pacer.yml`](../../workflows/ccr-pacer.yml)). It's an hourly `cron` that
derives the pending backlog, admits only the windows that fit the current API
budget (`≤ 1 window/repo/tick`), dispatches the loop for each, and reconciles
outcomes into a durable `ccr-pacer-state` branch. It drains **unattended**, never
exceeds a rate-limit ceiling, and resumes with **zero rework** after any
interruption (the `pr-*.json` cache makes re-dispatch free). Configure the repo
list + start date in [`pacer/config.json`](pacer/config.json) and enable the
workflow; see [`api-rate-limit.md`](api-rate-limit.md) §7 and
[`decisions.md`](decisions.md) D15.

> At scale the levers are tiered and evidence-gated — raise the ceiling with
> per-repo App tokens (T1) only when a real run shows the shared budget binding.
> See [`decisions.md`](decisions.md) D16/D17.

**Notes.**

- The default `GITHUB_TOKEN` reads **public** target repos; a **private** target
  needs a PAT / App with `pull-requests: read` + `issues: read`.
- Runs refuse fork/mirror targets and **no-op below 50 settled PRs**.
- Windows are settled-only (`end ≤ today − 14d`); trend points key on `windowEnd`,
  so same-day backfills still spread across their real windows.

## Dashboard

A static, zero-backend dashboard aggregates the `run-*.json` files into trend
charts. Drop new runs in `dashboard/data/`, regenerate the index with
`node scripts/gen-manifest.ts --data-dir dashboard/data` (the browser can't list a
directory), then serve it:

```bash
python3 -m http.server --directory dashboard
```

See [`dashboard/README.md`](dashboard/README.md) for GitHub Pages publishing.

## More

- [`metrics.md`](metrics.md) — full metric definitions, why they're defensible,
  and how to read each honestly.
- [`decisions.md`](decisions.md) — every design decision (D1–D17).
- [`api-rate-limit.md`](api-rate-limit.md) — API budget, tiers, and scaling levers.
- [`references/judge.prompt.md`](references/judge.prompt.md) — the pinned judge
  prompt and closed label vocabulary.
