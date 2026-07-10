# Design decisions — CCR Improvement Loop

This document records the **core design decisions** behind the CCR Improvement
Loop (the weekly workflow that measures whether Copilot Code Review is helping a
repo) and the dashboard that visualizes its output. It captures _what_ was
decided, _why_, the _alternatives rejected_, and the known _consequences_ — so
future changes don't silently undo a deliberate choice.

For the metric definitions themselves, see [`README.md`](README.md). This file is
about the engineering decisions, not the statistics.

---

## D1 — Subtractive metric set: defend a few, not display many

**Decision.** Report a small set of metrics, each anchored in either a
**deterministic fact** or an **explicit LLM judgment from real evidence**. When a
metric can only be produced by a weak proxy, remove it rather than ship it.

**Why.** No amount of schema rigor downstream fixes a bad proxy. A credible small
set is worth more than a broad set that collapses under a reviewer's first
question.

**Rejected alternatives.** The earlier design measured CCR usefulness with
structural shortcuts — line-range overlap, "a later commit touched the file,"
regex sentiment on replies. Each was individually noisy; all were removed. The
full list of rejected metrics, kept auditable, is below.

| Rejected                                                                 | Why it was invalid                                                                                                                                                |
| ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Line-range overlap** (`ccrOverlap`, `ccrOverlapRate`, `overlapTiming`) | Co-location is not the same concern. Counted unrelated nearby comments as catches and missed same-issue-different-line. Replaced by LLM same-concern judgment.    |
| **`actedOn` = a later commit touched the file**                          | True on nearly every active PR; measured "file still being edited," not usefulness. Replaced by line-level `ccrOutcome`.                                          |
| **`resolved` = thread-resolved OR regex-positive reply**                 | Brittle keyword matching ("not fixed yet" false-matched; "good catch, but by design" matched both signals). Folded into the judged `rejected`/`ignored` outcomes. |
| **`overlapTiming` "same round" via 60-second wall clock**                | Fragile heuristic that no metric decision consumed.                                                                                                               |

**Consequences.** The dashboard shows fewer columns than a naive tool would, and
some questions ("is CCR good at everything?") are intentionally out of scope.

---

## D2 — Three-phase pipeline: deterministic prep → agent judgment → deterministic math

**Decision.** Split every run into three strictly separated phases:

1. **Deterministic prep** — `prep-run.ts` orchestrates `fetch-prs.ts` →
   `classify-pr.ts` → `filter-comments.ts` → `attribute-comments.ts` →
   `build-judge-input.ts` → `prep-summary.ts`, using only the GitHub token (no
   LLM). It produces the normalized cache (`attributed.json`,
   `judge-input.json`, `meta.json`, `prep-summary.json`).
2. **Agent judgment** — the agent makes bounded per-item judgments only
   (classify `needs-agent` PRs, judge comments, cluster themes) against the
   pinned prompts in `references/`.
3. **Deterministic metrics** — `emit-run-json.ts` calls `compute-metrics.ts` /
   `pr-metrics.ts` over the judged rows to produce the final `run-*.json`.

**Why.** The agent should decide only what requires judgment; everything
mechanical stays deterministic and reproducible. This makes the numbers
recomputable and the agent's contribution auditable in isolation.

**Consequences.** The agent never computes a metric — it only labels evidence.
Adding a metric means adding deterministic math over existing judged fields, not
asking the agent for a number.

---

## D3 — Agent judges only bounded, pre-assembled evidence

**Decision.** The agent judges from a fixed, truncated evidence packet built by
`build-judge-input.ts`: the comment body, its `diffHunk`, and — for CCR comments
— a `postCommentDiff` (later commits touching the same file) plus the PR author's
`authorReplies`. Budgets are enforced (`--max-body-chars`, `--max-diff-chars`;
defaults 2000 / 4000).

**Why.** Bounding the evidence keeps runs cheap and deterministic in scope, and
prevents the judge from reaching for whole-file or repo-wide context it can't be
held accountable to. The judge prompt explicitly forbids using anything outside
the provided evidence ([`judge.prompt.md`](references/judge.prompt.md)).

**Consequences.** On heavy churn the diff can be truncated; this is tracked
(`missingPostCommentDiff` in `prep-summary.ts`) rather than hidden.
`diffDetectable` is deliberately conservative — issues needing outside knowledge
are marked not detectable.

---

## D4 — `ccrSawCode` eligibility gate: don't blame CCR for rounds it never saw

**Decision.** A human review ask only enters the `missRate` denominator if CCR
had a real opportunity to catch it. `attribute-comments.ts:computeCcrSawCode`
computes this deterministically: some CCR review event must fall **at or after**
the latest commit touching the ask's file (at/before the comment) **and at or
before** the human comment.

**Why.** If a human comments on code pushed after CCR's last review, CCR never
had the chance — scoring that as a miss would be unfair and would make the metric
indefensible.

**Rejected alternative.** Counting every substantive human ask as a potential
miss regardless of review timing.

**Known limitation (documented, not hidden).** "CCR saw this code" is inferred
from **timestamps** of CCR review submissions and inline comments
(`ccrReviewEventTimes`) versus commit timestamps. It is a deterministic
**proxy**, not a guarantee that CCR rendered that exact revision — the softest
link in the chain, and a candidate for future hardening.

---

## D5 — Closed-vocabulary, four-way comment outcomes with an explicit abstention

**Decision.** For each CCR comment the agent assigns exactly one
`ccrOutcome ∈ {addressed, rejected, ignored, unclear}`, judged only from
`postCommentDiff` + `authorReplies`. `unclear` is a first-class bucket and is
**excluded** from the rate denominators.

**Why.** `rejected` and `ignored` capture the negative signal a single "useful
rate" would hide (a high `ignoredRate` on critical comments is a real problem).
Keeping `unclear` lets the judge abstain instead of guessing, which protects the
rates from forced-choice noise.

**Consequences.** Outcome rates are reported over eligible, decided comments
only; `unclear`/`null` counts are surfaced separately.

---

## D6 — Same-concern judgment, not location overlap (`ccrAddressedConcern`)

**Decision.** Whether CCR "already caught" a human ask is an LLM judgment that a
CCR comment raises the **same underlying concern** — same issue even at a
different line or wording — not line-range proximity
([`judge.prompt.md`](references/judge.prompt.md)).

**Why.** The old overlap heuristic counted two unrelated nearby comments as a
catch and missed CCR flagging the same class of issue elsewhere. Judging the
concern removes that co-location fallacy in both directions.

---

## D7 — Honesty invariants in the data model

**Decision.** Encode four honesty rules directly in the schema and metric math
(`run-schema.ts`, `compute-metrics.ts`):

- **`null`, never `0`, for an empty denominator** — "not measured" and "measured
  zero" are different facts and render differently.
- **`n ≥ 5` or suppress** — any figure or slice below the threshold is
  low-confidence and never headlined; thin weeks yield `coverageWarnings`, not
  false precision.
- **Normalize by PR type; slice by severity** — a rate move must survive "was it
  just a heavier bug-fix week?"
- **Record the ruler** — judge model, pinned prompt hashes, and vocabulary hash
  are stored per run so a trend can't silently mix two different instruments.

**Why.** These are the properties that make the output trustable, which is the
entire purpose of the metric.

**Consequences.** Real runs often report `n/a` for a metric in a thin window
(e.g. a repo week with zero eligible asks) — by design, not a bug.

---

## D8 — Proxy metrics are labeled as proxies (`bugFixPrRate`)

**Decision.** Keep `bugFixPrRate` (share of merged PRs that are bug fixes) as a
**deterministic count** and explicitly label it a proxy, not proof of an escaped
bug CCR could have caught.

**Rejected alternative.** Tracing a bug-fix's lines back to the introducing PR
via `git blame` to build a "verified miss." It was fragile across squashes,
renames, and force-pushes, and a single weekly window yielded ~0–2 traceable
fixes (almost always `n < 5`), producing no usable signal.

**Consequences.** Read `bugFixPrRate` next to `missRate`, never alone. Causal
tracing may return if it can be made robust.

---

## D9 — Proposal-only via safe outputs (no repository writes)

**Decision.** The workflow never edits repo files or opens PRs. Every write — the
run-JSON artifact and the single tracking issue with proposed `.github/`
instruction edits — is routed through gh-aw **safe outputs**
(`upload-artifact`, `create-issue`), and the agent job runs read-only. If there
is nothing to report it calls `noop`.

**Why.** A measurement/advisory tool should not mutate the repo it measures;
proposal-only keeps it safe to run broadly and keeps a human in the loop for any
change to instruction files.

**Consequences.** Consuming the output (dashboard, instruction edits) is a
separate, human- or workflow-gated step.

---

## D10 — Target repo is a parameter, not a fork

**Decision.** One run measures one target repo, resolved as
`TARGET_REPO = inputs.repo || github.repository`
([`.github/workflows/ccr-improvement-loop.md`](../../workflows/ccr-improvement-loop.md)).
The concurrency group is per-repo and the emitted filename embeds
`owner_repo`, so multiple repos coexist without races or collisions. A fork/mirror
target is refused ([`references/upstream-fork-check.md`](references/upstream-fork-check.md)).

**Why.** Teams need to point the same, single workflow at different repos
(`gh workflow run … -f repo=Owner/Name`) without copying the whole setup. The
per-repo isolation is what lets the dashboard aggregate many repos safely.

**Consequences.** The `.md` is the gh-aw source; changing the standing target
means editing it and **recompiling** to `ccr-improvement-loop.lock.yml` (the file
GitHub actually runs).

---

## D11 — Dashboard: static, zero-backend, manifest-driven

**Decision.** Visualize runs with a fully static site: vendored (pinned) Chart.js,
plain ES modules, relative URLs only, and a `data/manifest.json` listing the
`run-*.json` files to load. No server, no build step, no database, no token.

**Why.** The explicit requirement was **low maintenance** and easy team sharing.
A static site works locally (`python3 -m http.server`) and unchanged under a
GitHub Pages subpath; there is no backend to operate or secure.

**Rejected alternatives.** A server/API (operational burden), a build pipeline
(maintenance), a runtime CDN for Chart.js (supply-chain + offline fragility — so
it's vendored).

**Consequences.** The browser can't glob a folder, so `manifest.json` must be
kept in sync with `data/`. Trends need ≥2 runs to render as lines. Time axes use
pre-formatted category labels because a Chart.js time scale would require an
un-vendored date adapter.

---

## D12 — Tests use dedicated fixtures, decoupled from live dashboard data

**Decision.** The dashboard's `node --test` suite reads fixtures from
`dashboard/js/fixtures/`, **not** the live `dashboard/data/` folder.

**Why.** Tests originally read `data/`, so swapping synthetic samples for real run
data broke assertions (expected run counts, a null-metric run, etc.). Production
data and test data must not be coupled.

**Consequences.** Real runs can be added, removed, or replaced in `data/` freely
without touching tests; edge cases the tests need (null metrics, coverage
warnings, multiple repos) live permanently in the fixtures folder.

---

## Cross-cutting principle

Where judgment is irreducible, use the agent — on bounded evidence, with a pinned
prompt. Everywhere else, stay deterministic. Prefer the honest `n/a` and the
stated limitation over the impressive number that can't survive scrutiny.
