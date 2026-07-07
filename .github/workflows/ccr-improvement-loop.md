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
  - name: Deterministic prep (fetch → classify → filter → attribute → trace)
    working-directory: .github/aw/ccr-improvement-loop
    env:
      GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
    run: |
      set -euo pipefail
      mkdir -p "$CCR_CACHE" "$CCR_RUNS"
      node ./scripts/fetch-prs.ts --repo "$TARGET_REPO" --state merged \
        --min-prs 50 --settle-days 14 --cache-dir "$CCR_CACHE" --quiet
      GLOB="$CCR_CACHE/pr-*.json"
      node ./scripts/classify-pr.ts        --glob "$GLOB" --cache-dir "$CCR_CACHE"
      node ./scripts/filter-comments.ts    --glob "$GLOB" --cache-dir "$CCR_CACHE"
      node ./scripts/attribute-comments.ts --glob "$GLOB" \
        --filtered "$CCR_CACHE/filtered.json" --cache-dir "$CCR_CACHE"
      node ./scripts/trace-bug-origin.ts   --repo "$TARGET_REPO" \
        --classified "$CCR_CACHE/classified.json" --glob "$GLOB" \
        --cache-dir "$CCR_CACHE" || true
      # Minimal run metadata for emit-run-json (agent may refine model/hashes).
      printf '%s' "{\"repo\":\"$TARGET_REPO\",\"windowStart\":\"\",\"windowEnd\":\"$(date -u +%F)\",\"windowLagDays\":14,\"prState\":\"merged\",\"model\":\"agentic-workflow\",\"modelTool\":\"gh-aw\",\"temperature\":0,\"matchedCcrLogin\":\"copilot-pull-request-reviewer[bot]\",\"promptHashes\":{},\"vocabularyHash\":null,\"toolVersion\":\"1.0\",\"ccrEnabledSince\":null}" > "$CCR_CACHE/meta.json"
---

# CCR Improvement Loop

Measure whether **Copilot Code Review (CCR)** is helping `${{ env.TARGET_REPO }}`
this window, then report the result as one cited tracking issue. This run is
scheduled or manually dispatched — there is no triggering issue/PR. The
deterministic prep steps have already fetched the settled PRs and written the
normalized cache to `${{ env.CCR_CACHE }}` (`classified.json`, `filtered.json`,
`attributed.json`, `traced.json`, `meta.json`).

Do the agent-judgment work below using the **pinned prompts** in the workflow
support package under `.github/aw/ccr-improvement-loop/references/`. Never invent
a label outside the closed
[controlled-vocabulary.md](../aw/ccr-improvement-loop/references/controlled-vocabulary.md).

## 1. Judge the comments

Read `${{ env.CCR_CACHE }}/attributed.json`. Batch the rows (don't judge one per
turn). Apply
[judge.prompt.md](../aw/ccr-improvement-loop/references/judge.prompt.md):

- human `ask` rows → decide `isSubstantive`, `diffDetectable`, `category`, and
  `ccrAddressedConcern` (against that PR's CCR comments).
- CCR comment rows → decide `severity`, `category`, and `outcome`
  (addressed / rejected / ignored / unclear) from the post-comment change +
  replies.

Feed only the comment body + minimal diff hunk — never full-file or `excludedPaths`
content. Write the augmented rows to `${{ env.CCR_CACHE }}/judged.json`, deriving
`isGap = ask && isSubstantive && diffDetectable && ccrSawCode && !ccrAddressedConcern`
and `theme = isSubstantive ? category : null`. Leave any un-judgeable row's judge
fields `null` with `judgeStatus: "failed"`.

## 2. Cluster and promote themes

Cluster the `isGap` rows per
[theme.prompt.md](../aw/ccr-improvement-loop/references/theme.prompt.md), apply
the promotion gate, and write `${{ env.CCR_CACHE }}/themes.json`.

## 3. Compute metrics (deterministic)

```bash
node .github/aw/ccr-improvement-loop/scripts/emit-run-json.ts \
  --meta "$CCR_CACHE/meta.json" \
  --classified "$CCR_CACHE/classified.json" \
  --attributed "$CCR_CACHE/judged.json" \
  --verified-misses "$CCR_CACHE/traced.json" \
  --themes "$CCR_CACHE/themes.json" \
  --glob "$CCR_CACHE/pr-*.json" \
  --out-dir "$CCR_RUNS" --print-summary
```

The emitted `run-*.json` is the system of record; its schema validation rejects
any label outside the controlled vocabulary, so a mislabel fails the run rather
than skewing a metric.

## 4. Report (safe outputs only — proposal, no file writes)

- Emit an **`upload-artifact`** safe output for `${{ env.CCR_RUNS }}/run-*.json`
  (durable trend record).
- Emit **one `create-issue`** safe output titled
  `CCR Improvement Loop — <repo> (<window>)` whose body contains, in GitHub-flavored
  markdown with nested headings starting at `###`:
  - headline metrics **sliced** (miss rate; addressed/ignored by severity; CCR
    coverage), each read as a trend, with any n < 5 slice flagged low-confidence;
  - verified-miss highlights (fix PR → introducing PR), if any;
  - the ranked **promoted** themes with source-PR citations;
  - for each promoted theme, one **generalized** proposed `.github/` instruction
    rule (imperative, not tied to a specific PR/file), for human approval.

This workflow is **proposal-only**: do not edit repository files or open pull
requests. All writes go through the safe outputs above.

## No-op

If fewer than 50 settled PRs were available, or there is no substantive signal to
report, call `noop` with a one-line reason instead of creating an issue.
