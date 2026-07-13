# CCR dashboard

A static, zero-backend web dashboard that aggregates many CCR Improvement Loop
`run-*.json` files and renders metrics + historical trends. It is the visual
companion to the [`aggregate-runs`](../scripts/aggregate-runs.ts) trend view.

It reads the same reading rules the metrics are built on: **`null` means "not
measured" and renders as a gap, never zero**, and **low-confidence values (small
sample) are flagged, not headlined** (see the package
[README](../README.md#reading-rules-apply-to-every-metric)).

## What it shows

- **Rate trends over time** — one trend chart per rate (CCR catch rate, CCR
  coverage, bug-fix PR rate, addressed / rejected / ignored rates, critical
  catch rate, human comments per PR, PR cycle time, iterations per PR), each
  with one line per repo. Percentage rates share a 0–100% axis; raw-number
  rates auto-scale. `null` renders as a gap, never zero.
- **Addressed rate by severity** — one line per severity across the shown runs.
- **Bug-fix PR rate by repo** — latest run per repo.
- **Per-run headline metrics** — a table of every headline rate per run, with
  `n/a` for null values, an `lc` badge for low-confidence rates, and per-run
  coverage warnings / experiment markers.

Use the **Repos** checkboxes at the top to filter which repositories are included.

## Run locally

The dashboard fetches JSON from `data/`, and browsers block `fetch()` of `file://`
URLs (CORS). So you must serve it over HTTP — opening `index.html` directly will
**not** work. Any static server is fine:

```bash
# from this directory (.github/aw/ccr-improvement-loop/dashboard)
python3 -m http.server 8137
# then open http://127.0.0.1:8137/ in a browser
```

or, equivalently, from the package root:

```bash
python3 -m http.server 8137 --directory dashboard
```

No build step, no install, no backend. Chart.js is vendored locally
(`vendor/chart.umd.min.js`, pinned), so there is **no runtime CDN call** and it
works offline.

## Feeding it data

The browser can't list a folder, so the files to load are listed in
`data/manifest.json`:

```json
{ "runs": ["run-<id>.json", "run-<id>.json"] }
```

To add data:

1. Drop a `run-<id>.json` file into `data/`. It must be a valid
   `schemaVersion "1.0"` run (see [`run-schema.ts`](../scripts/run-schema.ts)).
   Files that are unreadable, invalid JSON, the wrong schema version, or that fail
   validation are **skipped**, never fatal (mirrors `aggregate-runs`' `loadRun`).
2. Add its filename to the `runs` array in `data/manifest.json`.

The sample files currently in `data/` are synthetic fixtures across two repos and
several dates, including a thin-data run (null CCR catch rate, low-confidence
rates, coverage warnings) so the edge-case rendering is visible out of the box.

## Unit tests

The aggregation core (`js/aggregate.mjs`) is a self-contained browser ESM module
that re-implements the semantics of `scripts/aggregate-runs.ts` (dedupe by
`run.id`, skip bad files, `null` != 0, time-ordered trends, latest-per-repo). It is
unit-tested with Node's built-in runner:

```bash
node --test 'dashboard/js/**/*.test.mjs'   # run from the package root
```

These files are intentionally kept out of the package's `tsc`/`vitest` scope (they
are browser code) and given a loose eslint override, so `pnpm lint/typecheck/test`
are unaffected.

## Enabling GitHub Pages later (deferred)

The dashboard is Pages-ready but **not deployed**. To publish it for the team:

1. **Enable Pages** in the repo: Settings → Pages → set the source to this
   `dashboard/` folder (or wire an `actions/deploy-pages` workflow). All asset and
   data paths are relative, so it works under the `https://<org>.github.io/<repo>/…`
   subpath. A `.nojekyll` file is already present so `js/` is served as-is.
2. **Turn on data publishing** by activating
   [`.github/workflows/ccr-dashboard-publish.yml`](../../../workflows/ccr-dashboard-publish.yml):
   uncomment its `workflow_run` (or `schedule`) trigger and resolve the
   `TODO(activate)` — set the `download-artifact` step's `name:` to the exact
   artifact name the loop publishes its run JSON under (read it off a real
   "CCR Improvement Loop" run). That workflow commits new `run-*.json` into `data/`
   and regenerates `data/manifest.json`.

   Same-repo publishing uses the built-in `GITHUB_TOKEN` (`contents: write`) — **no
   PAT, GitHub App, or deploy key is required.**

The agentic loop itself stays **read-only** and untouched; publishing is this
separate, opt-in workflow.
