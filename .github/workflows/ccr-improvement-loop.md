---
description: >-
  Weekly check on whether Copilot Code Review is helping a target repo:
  deterministic metrics prep, agent-judged gaps/outcomes, and a single cited
  tracking issue. Proposal-only — no file writes.
on:
  schedule: weekly on monday
  workflow_dispatch:
    inputs:
      repo:
        description: "Target repo (owner/name); defaults to this repository"
        required: false
      window_start:
        description: "Backfill window start YYYY-MM-DD (empty = rolling weekly window)"
        required: false
      window_end:
        description: "Backfill window end YYYY-MM-DD (empty = settle cutoff, today - settle-days)"
        required: false
# One run per (repo, ref) — no schedule/dispatch races.
concurrency:
  group: ccr-loop-${{ github.event.inputs.repo || github.repository }}
  cancel-in-progress: false
# Read-only agent job. Every write (the tracking issue, the run-JSON artifact)
# is routed through safe-outputs; copilot-requests:write lets the default
# Copilot engine authenticate with the workflow token.
permissions:
  contents: read
  actions: read
  issues: read
  pull-requests: read
  copilot-requests: write
network:
  allowed:
    - defaults
    - node
tools:
  github:
    mode: gh-proxy
    toolsets: [default]
safe-outputs:
  create-issue:
    title-prefix: "[ccr-loop] "
    labels: [ccr-improvement-loop, automation]
    close-older-issues: true
    max: 1
  upload-artifact:
    allowed-paths:
      - ".ccr-runs/run-*.json"
env:
  TARGET_REPO: ${{ github.event.inputs.repo || github.repository }}
  # Optional explicit backfill window; empty on the weekly schedule so prep-run
  # falls back to its rolling settled window.
  WINDOW_START: ${{ github.event.inputs.window_start }}
  WINDOW_END: ${{ github.event.inputs.window_end }}
  CCR_CACHE: ${{ github.workspace }}/.ccr-cache
  CCR_RUNS: ${{ github.workspace }}/.ccr-runs
# Deterministic data prep runs as custom steps (outside the agent sandbox, with
# normal network). Only secrets.GITHUB_TOKEN is used here. These produce the
# normalized cache the agent judges; no agentic compute happens in steps.
steps:
  - name: Checkout
    uses: actions/checkout@v4
    with:
      persist-credentials: false
  - name: Setup Node
    uses: actions/setup-node@v4
    with:
      node-version: "24"
  - name: Install pnpm
    uses: pnpm/action-setup@v4
    with:
      version: "10.12.4"
  - name: Install dependencies
    working-directory: .github/aw/ccr-improvement-loop
    run: pnpm install --frozen-lockfile
  - name: Refuse fork/mirror targets
    env:
      GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
    run: |
      set -euo pipefail
      if [ "$(gh repo view "$TARGET_REPO" --json isFork --jq .isFork)" = "true" ]; then
        echo "::error::Target $TARGET_REPO is a fork; refusing to mine. See references/upstream-fork-check.md."
        exit 1
      fi
  - name: Deterministic prep (fetch → classify → filter → attribute → audit)
    working-directory: .github/aw/ccr-improvement-loop
    env:
      GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
    run: |
      set -euo pipefail
      mkdir -p "$CCR_CACHE" "$CCR_RUNS"
      # prep-run orchestrates every stage with explicit-arg subprocess calls,
      # writes meta.json + prep-summary.json, and exits non-zero if a fatal
      # audit check (duplicate rowIds / judge-input ids) trips. The window flags
      # are appended only when a manual backfill supplies them; the weekly
      # schedule leaves them empty and prep-run uses its rolling window.
      node ./scripts/prep-run.ts --repo "$TARGET_REPO" --cache-dir "$CCR_CACHE" \
        ${WINDOW_START:+--window-start "$WINDOW_START"} \
        ${WINDOW_END:+--window-end "$WINDOW_END"}
---

# CCR Improvement Loop

Measure whether **Copilot Code Review (CCR)** is helping `${{ env.TARGET_REPO }}`
this window, then report the result as one cited tracking issue. This run is
scheduled or manually dispatched — there is no triggering issue/PR. The
deterministic prep steps have already fetched the settled PRs and written the
normalized cache to `${{ env.CCR_CACHE }}` (`classified.json`, `filtered.json`,
`attributed.json`, `judge-input.json`, `meta.json`,
`prep-summary.json`). Read `prep-summary.json` first — it reports comment counts
by author kind, CCR inline counts, identity-integrity checks, and any evidence
gaps you should account for in the methodology note.

Do the agent-judgment work below using the **pinned prompts** in the workflow
support package under `.github/aw/ccr-improvement-loop/references/`. Never invent
a label outside the closed
[controlled-vocabulary.md](../aw/ccr-improvement-loop/references/controlled-vocabulary.md).

## 1. Classify PRs the deterministic rules could not type

The deterministic `classify-pr.ts` prep only types a PR when a label,
Conventional-Commit title prefix, or linked-issue label applies; every other PR
is left `prType: null`, `prTypeSource: "unknown"`, `classificationStatus:
"needs-agent"`. Resolve those now — otherwise `prType` and `bugFixPrRate` (the
merged-bug signal) stay empty.

Read `${{ env.CCR_CACHE }}/classified.json` and select every PR whose
`classificationStatus == "needs-agent"`. Batch them (don't classify one per
turn). For each, apply
[classify-pr.prompt.md](../aw/ccr-improvement-loop/references/classify-pr.prompt.md)
using the PR title (and labels/body from the matching
`${{ env.CCR_CACHE }}/pr-*.json` cache file) to pick exactly one `prType`
(`bug-fix | feature | refactor | docs | test | chore`). Then set
`prTypeSource: "agent"` and `classificationStatus: "complete"` on that row.
Leave already-`complete` rows untouched. Write the updated array back to
`${{ env.CCR_CACHE }}/classified.json` (same shape) so the Step 4 emit picks it
up and `bugFixPrRate` reflects the true bug-fix count for the window.

## 2. Judge the comments

Read `${{ env.CCR_CACHE }}/judge-input.json` for the bounded evidence and
`${{ env.CCR_CACHE }}/attributed.json` for the rows to augment. Batch the judge
input `items` (don't judge one per turn). Apply
[judge.prompt.md](../aw/ccr-improvement-loop/references/judge.prompt.md):

- human `ask` rows → decide `isSubstantive`, `diffDetectable`, `category`, and
  `ccrAddressedConcern` (against that PR's CCR comments).
- CCR comment rows → decide `severity`, `category`, and `outcome`
  (addressed / rejected / ignored / unclear) from the post-comment change +
  replies.

Use only the evidence already present in `judge-input.json` — never full-file or
`excludedPaths` content. Write the augmented rows to `${{ env.CCR_CACHE }}/judged.json`, deriving
`isGap = ask && isSubstantive && diffDetectable && ccrSawCode && !ccrAddressedConcern`
and `theme = isSubstantive ? category : null`. Leave any un-judgeable row's judge
fields `null` with `judgeStatus: "failed"`.

## 3. Cluster and promote themes

Cluster the `isGap` rows per
[theme.prompt.md](../aw/ccr-improvement-loop/references/theme.prompt.md), apply
the promotion gate, and write `${{ env.CCR_CACHE }}/themes.json`.

## 4. Compute metrics (deterministic)

```bash
node .github/aw/ccr-improvement-loop/scripts/emit-run-json.ts \
  --meta "$CCR_CACHE/meta.json" \
  --classified "$CCR_CACHE/classified.json" \
  --attributed "$CCR_CACHE/judged.json" \
  --themes "$CCR_CACHE/themes.json" \
  --glob "$CCR_CACHE/pr-*.json" \
  --out-dir "$CCR_RUNS" --print-summary
```

The emitted `run-*.json` is the system of record; its schema validation rejects
any label outside the controlled vocabulary, so a mislabel fails the run rather
than skewing a metric.

## 5. Report (safe outputs only — proposal, no file writes)

- Emit an **`upload-artifact`** safe output for `${{ env.CCR_RUNS }}/run-*.json`
  (durable trend record).
- Emit **one `create-issue`** safe output titled
  `CCR Improvement Loop — <repo> (<window>)` whose body contains, in GitHub-flavored
  markdown with nested headings starting at `###`:
  - headline metrics **sliced** (miss rate; addressed/ignored by severity; CCR
    coverage; bug-fix PR rate), each read as a trend, with any low-confidence
    slice (n < 5) or rate (n < 10) flagged;
  - the ranked **promoted** themes with source-PR citations;
  - for each promoted theme, one **generalized** proposed `.github/` instruction
    rule (imperative, not tied to a specific PR/file), for human approval.

This workflow is **proposal-only**: do not edit repository files or open pull
requests. All writes go through the safe outputs above.

## No-op

If fewer than 50 settled PRs were available, or there is no substantive signal to
report, call `noop` with a one-line reason instead of creating an issue.
